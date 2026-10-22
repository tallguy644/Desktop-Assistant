using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

// Phone companion: a small token-protected JSON API that the Syra Android app talks to.
// Kept free of WPF so it can be compiled and tested on its own (Tests/CompanionTests.cs).
namespace JarvisDesk {
 public sealed class CompanionDevice {
  public string Id, Name, TokenHash, PairedUtc, LastSeenUtc;
 }
 public sealed class CompanionSettings {
  public const int DefaultPort = 47820;
  public bool Enabled;
  public int Port = DefaultPort;
  public List<CompanionDevice> Devices = new List<CompanionDevice>();
  public void Normalize() {
   if (Port < 1024 || Port > 65535) Port = DefaultPort;
   if (Devices == null) Devices = new List<CompanionDevice>();
   Devices = Devices.Where(d => d != null && !String.IsNullOrEmpty(d.TokenHash) && d.TokenHash.Length == 64).Take(CompanionAuth.MaxDevices).ToList();
   foreach (var d in Devices) { if (String.IsNullOrEmpty(d.Id)) d.Id = Guid.NewGuid().ToString("N"); d.Name = CompanionAuth.CleanDeviceName(d.Name); }
  }
 }

 public static class CompanionAuth {
  public const int MaxDevices = 8;
  static readonly RandomNumberGenerator random = RandomNumberGenerator.Create();
  public static string NewToken() { var bytes = new byte[32]; lock (random) random.GetBytes(bytes); return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_'); }
  public static string Hash(string token) {
   using (var sha = SHA256.Create()) return String.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(token ?? "")).Select(b => b.ToString("x2")));
  }
  public static string NewPin() {
   var bytes = new byte[4]; uint value;
   do { lock (random) random.GetBytes(bytes); value = BitConverter.ToUInt32(bytes, 0); } while (value >= 4294000000u); // avoid modulo bias
   return (value % 1000000u).ToString("D6", CultureInfo.InvariantCulture);
  }
  public static bool FixedEquals(string a, string b) {
   if (a == null || b == null || a.Length != b.Length) return false;
   int diff = 0; for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i]; return diff == 0;
  }
  public static CompanionDevice Find(CompanionSettings settings, string token) {
   if (settings == null || String.IsNullOrEmpty(token) || token.Length > 128) return null;
   string hash = Hash(token); CompanionDevice match = null;
   foreach (var d in settings.Devices) if (FixedEquals(d.TokenHash, hash)) match = d;
   return match;
  }
  public static string CleanDeviceName(string name) {
   var clean = new string((name ?? "").Where(c => !Char.IsControl(c)).ToArray()).Trim();
   if (clean.Length == 0) clean = "Android phone"; if (clean.Length > 40) clean = clean.Substring(0, 40);
   return clean;
  }
  // Only home-network and Tailscale addresses may connect. Public internet sources are refused even if a port is forwarded.
  public static bool AllowedRemote(IPAddress address) {
   if (address == null) return false;
   if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
   if (IPAddress.IsLoopback(address)) return true;
   var b = address.GetAddressBytes();
   if (address.AddressFamily == AddressFamily.InterNetwork) {
    return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 100 && b[1] >= 64 && b[1] <= 127);
   }
   if (address.AddressFamily == AddressFamily.InterNetworkV6) return address.IsIPv6LinkLocal || (b[0] & 0xFE) == 0xFC; // fc00::/7 includes Tailscale fd7a:115c:a1e0::/48
   return false;
  }
 }

 // One short-lived pairing code at a time. Five wrong guesses cancel it.
 public sealed class CompanionPairing {
  readonly object gate = new object();
  string pin; DateTime expiresUtc; int attempts;
  public const int Lifetime = 180, MaxAttempts = 5;
  public string Start(DateTime nowUtc) { lock (gate) { pin = CompanionAuth.NewPin(); expiresUtc = nowUtc.AddSeconds(Lifetime); attempts = 0; return pin; } }
  public void Cancel() { lock (gate) pin = null; }
  public bool Active(DateTime nowUtc) { lock (gate) return pin != null && nowUtc < expiresUtc; }
  public int SecondsLeft(DateTime nowUtc) { lock (gate) return pin == null ? 0 : Math.Max(0, (int)Math.Ceiling((expiresUtc - nowUtc).TotalSeconds)); }
  public string Current(DateTime nowUtc) { lock (gate) return pin != null && nowUtc < expiresUtc ? pin : null; }
  public bool TryUse(string guess, DateTime nowUtc) {
   lock (gate) {
    if (pin == null || nowUtc >= expiresUtc) { pin = null; return false; }
    bool ok = CompanionAuth.FixedEquals(pin, (guess ?? "").Trim());
    if (ok) { pin = null; return true; }
    if (++attempts >= MaxAttempts) pin = null;
    return false;
   }
  }
 }

 // Per-address lockout after repeated bad tokens or pairing codes.
 public sealed class CompanionThrottle {
  readonly Dictionary<string, List<DateTime>> failures = new Dictionary<string, List<DateTime>>();
  public const int Limit = 10; public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);
  public bool Blocked(string key, DateTime nowUtc) { lock (failures) { List<DateTime> list; if (!failures.TryGetValue(key, out list)) return false; list.RemoveAll(t => nowUtc - t > Window); return list.Count >= Limit; } }
  public void Fail(string key, DateTime nowUtc) { lock (failures) { List<DateTime> list; if (!failures.TryGetValue(key, out list)) { if (failures.Count > 500) failures.Clear(); failures[key] = list = new List<DateTime>(); } list.Add(nowUtc); } }
  public void Clear(string key) { lock (failures) failures.Remove(key); }
 }

 public sealed class CompanionRequest {
  public string Method, Path, Body;
  public Dictionary<string, string> Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
  public IPAddress Remote;
  public string Header(string name) { string v; return Headers.TryGetValue(name, out v) ? v : ""; }
  public string BearerToken { get { string auth = Header("Authorization"); return auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? auth.Substring(7).Trim() : ""; } }
  public Dictionary<string, object> Json() {
   if (String.IsNullOrWhiteSpace(Body)) return new Dictionary<string, object>();
   try { return CompanionJson.Serializer().Deserialize<Dictionary<string, object>>(Body) ?? new Dictionary<string, object>(); }
   catch { throw new CompanionHttpException(400, "Request body must be a JSON object."); }
  }
  public string Text(string key) { object v; var data = Json(); return data.TryGetValue(key, out v) && v is string ? (string)v : ""; }
 }
 public sealed class CompanionResponse {
  public int Status = 200; public object Body;
  public static CompanionResponse Ok(object body) { return new CompanionResponse { Body = body }; }
  public static CompanionResponse Error(int status, string message) { return new CompanionResponse { Status = status, Body = new Dictionary<string, object> { { "error", message } } }; }
 }
 public sealed class CompanionHttpException : Exception { public readonly int Status; public CompanionHttpException(int status, string message) : base(message) { Status = status; } }

 public static class CompanionJson {
  public static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength = 1024 * 1024, RecursionLimit = 32 }; }
 }

 public static class CompanionHttp {
  public const int MaxHeaderBytes = 8192, MaxBodyBytes = 16384;
  // Minimal HTTP/1.1 reader: one request per connection, Content-Length bodies only.
  public static CompanionRequest Read(Stream stream) {
   var header = new MemoryStream(); int matched = 0;
   while (matched < 4) {
    int b = stream.ReadByte(); if (b < 0) throw new CompanionHttpException(400, "Connection closed early.");
    header.WriteByte((byte)b); if (header.Length > MaxHeaderBytes) throw new CompanionHttpException(431, "Headers too large.");
    matched = (b == (matched % 2 == 0 ? '\r' : '\n')) ? matched + 1 : (b == '\r' ? 1 : 0);
   }
   var lines = Encoding.ASCII.GetString(header.ToArray()).Split(new[] { "\r\n" }, StringSplitOptions.None);
   var first = lines[0].Split(' ');
   if (first.Length != 3 || !first[2].StartsWith("HTTP/1.", StringComparison.Ordinal)) throw new CompanionHttpException(400, "Malformed request line.");
   var request = new CompanionRequest { Method = first[0].ToUpperInvariant(), Path = first[1].Split('?')[0] };
   for (int i = 1; i < lines.Length; i++) { int colon = lines[i].IndexOf(':'); if (colon > 0) request.Headers[lines[i].Substring(0, colon).Trim()] = lines[i].Substring(colon + 1).Trim(); }
   if (request.Header("Transfer-Encoding").Length > 0) throw new CompanionHttpException(411, "Chunked bodies are not supported.");
   int length = 0; string lengthText = request.Header("Content-Length");
   if (lengthText.Length > 0 && (!Int32.TryParse(lengthText, NumberStyles.None, CultureInfo.InvariantCulture, out length) || length < 0)) throw new CompanionHttpException(400, "Invalid Content-Length.");
   if (length > MaxBodyBytes) throw new CompanionHttpException(413, "Request too large.");
   var body = new byte[length]; int read = 0;
   while (read < length) { int n = stream.Read(body, read, length - read); if (n <= 0) throw new CompanionHttpException(400, "Body ended early."); read += n; }
   request.Body = Encoding.UTF8.GetString(body);
   return request;
  }
  public static byte[] Write(CompanionResponse response) {
   byte[] body = Encoding.UTF8.GetBytes(CompanionJson.Serializer().Serialize(response.Body ?? new Dictionary<string, object>()));
   string head = "HTTP/1.1 " + response.Status.ToString(CultureInfo.InvariantCulture) + " " + Reason(response.Status) + "\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: " + body.Length.ToString(CultureInfo.InvariantCulture) + "\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n";
   var bytes = Encoding.ASCII.GetBytes(head); var all = new byte[bytes.Length + body.Length];
   Buffer.BlockCopy(bytes, 0, all, 0, bytes.Length); Buffer.BlockCopy(body, 0, all, bytes.Length, body.Length); return all;
  }
  static string Reason(int status) {
   switch (status) { case 200: return "OK"; case 400: return "Bad Request"; case 401: return "Unauthorized"; case 403: return "Forbidden"; case 404: return "Not Found"; case 409: return "Conflict"; case 411: return "Length Required"; case 413: return "Payload Too Large"; case 429: return "Too Many Requests"; case 431: return "Request Header Fields Too Large"; case 503: return "Service Unavailable"; case 504: return "Gateway Timeout"; default: return "Error"; }
  }
 }

 public sealed class CompanionServer : IDisposable {
  TcpListener listener; CancellationTokenSource lifetime; readonly SemaphoreSlim slots = new SemaphoreSlim(8);
  readonly Func<CompanionRequest, CompanionResponse> handler;
  public int Port { get; private set; }
  public bool Running { get { return listener != null; } }
  public CompanionServer(Func<CompanionRequest, CompanionResponse> handler) { this.handler = handler; }
  public void Start(int port) {
   Stop();
   var socket = new TcpListener(IPAddress.IPv6Any, port);
   try { socket.Server.DualMode = true; socket.Start(); }
   catch (SocketException) { socket = new TcpListener(IPAddress.Any, port); socket.Start(); }
   listener = socket; Port = port; lifetime = new CancellationTokenSource();
   var token = lifetime.Token; var active = socket;
   Task.Run(async delegate {
    while (!token.IsCancellationRequested) {
     TcpClient client;
     try { client = await active.AcceptTcpClientAsync().ConfigureAwait(false); } catch { if (token.IsCancellationRequested) return; continue; }
     if (!slots.Wait(0)) { client.Close(); continue; }
     var _ = Task.Run(delegate { try { Serve(client); } finally { slots.Release(); } });
    }
   });
  }
  void Serve(TcpClient client) {
   using (client) {
    try {
     client.ReceiveTimeout = 10000; client.SendTimeout = 10000;
     var remote = ((IPEndPoint)client.Client.RemoteEndPoint).Address;
     var stream = client.GetStream(); CompanionResponse response;
     if (!CompanionAuth.AllowedRemote(remote)) response = CompanionResponse.Error(403, "Only your home network or Tailscale can reach this companion.");
     else {
      try { var request = CompanionHttp.Read(stream); request.Remote = remote; response = handler(request) ?? CompanionResponse.Error(404, "Not found."); }
      catch (CompanionHttpException ex) { response = CompanionResponse.Error(ex.Status, ex.Message); }
      catch (Exception) { response = CompanionResponse.Error(500, "The request could not be completed."); }
     }
     var bytes = CompanionHttp.Write(response); stream.Write(bytes, 0, bytes.Length); stream.Flush();
    } catch { /* Client disconnected or timed out. */ }
   }
  }
  public void Stop() {
   if (lifetime != null) { lifetime.Cancel(); lifetime.Dispose(); lifetime = null; }
   if (listener != null) { try { listener.Stop(); } catch { } listener = null; }
  }
  public void Dispose() { Stop(); }
  public static List<string> LocalAddresses() {
   var found = new List<string>();
   try {
    foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()) {
     if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
     foreach (var a in nic.GetIPProperties().UnicastAddresses) {
      var ip = a.Address; if (ip.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(ip) || !CompanionAuth.AllowedRemote(ip)) continue;
      var b = ip.GetAddressBytes(); bool tailscale = b[0] == 100 && b[1] >= 64 && b[1] <= 127;
      found.Add(ip.ToString() + (tailscale ? "  (Tailscale - works anywhere)" : "  (home Wi-Fi)"));
     }
    }
   } catch { }
   return found.OrderBy(s => s.Contains("Tailscale") ? 0 : 1).ToList();
  }
 }
}
