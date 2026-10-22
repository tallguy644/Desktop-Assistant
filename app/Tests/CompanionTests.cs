using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using JarvisDesk;
public static class CompanionTests {
 static void Check(bool value, string text) { if (!value) throw new Exception(text); Console.WriteLine("PASS " + text); }
 static CompanionRequest Parse(string raw) { return CompanionHttp.Read(new MemoryStream(Encoding.UTF8.GetBytes(raw))); }
 static int StatusOf(Action action) { try { action(); return 0; } catch (CompanionHttpException ex) { return ex.Status; } }
 public static int Main() {
  try {
   string t1 = CompanionAuth.NewToken(), t2 = CompanionAuth.NewToken();
   Check(t1.Length == 43 && t1 != t2 && t1.All(c => Char.IsLetterOrDigit(c) || c == '-' || c == '_'), "Tokens are 256-bit, URL-safe and unique");
   Check(CompanionAuth.Hash(t1).Length == 64 && CompanionAuth.Hash(t1) == CompanionAuth.Hash(t1) && CompanionAuth.Hash(t1) != CompanionAuth.Hash(t2), "Token hashes are stable SHA-256 hex");
   var settings = new CompanionSettings(); settings.Devices.Add(new CompanionDevice { Name = "Pixel", TokenHash = CompanionAuth.Hash(t1) });
   Check(CompanionAuth.Find(settings, t1) != null && CompanionAuth.Find(settings, t2) == null && CompanionAuth.Find(settings, "") == null && CompanionAuth.Find(settings, new string('a', 500)) == null, "Only a paired token authenticates");
   Check(Enumerable.Range(0, 200).Select(i => CompanionAuth.NewPin()).All(p => p.Length == 6 && p.All(Char.IsDigit)), "Pairing codes are six digits");

   var pairing = new CompanionPairing(); var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
   string pin = pairing.Start(now);
   Check(!pairing.TryUse("000000" == pin ? "111111" : "000000", now) && pairing.TryUse(pin, now) && !pairing.TryUse(pin, now), "A pairing code works once");
   pin = pairing.Start(now); Check(!pairing.TryUse(pin, now.AddSeconds(CompanionPairing.Lifetime)), "Pairing codes expire after three minutes");
   pin = pairing.Start(now); string wrong = pin == "123456" ? "654321" : "123456";
   for (int i = 0; i < CompanionPairing.MaxAttempts; i++) pairing.TryUse(wrong, now);
   Check(!pairing.TryUse(pin, now) && !pairing.Active(now), "Five wrong guesses cancel the pairing code");

   var throttle = new CompanionThrottle();
   for (int i = 0; i < CompanionThrottle.Limit; i++) throttle.Fail("10.0.0.5", now);
   Check(throttle.Blocked("10.0.0.5", now) && !throttle.Blocked("10.0.0.6", now) && !throttle.Blocked("10.0.0.5", now.AddMinutes(11)), "Repeated failures lock out one address for ten minutes");

   Check(new[] { "127.0.0.1", "192.168.1.20", "10.1.2.3", "172.16.0.9", "172.31.255.1", "100.101.102.103", "::1", "fd7a:115c:a1e0::1", "fe80::1", "::ffff:192.168.0.4" }.All(a => CompanionAuth.AllowedRemote(IPAddress.Parse(a))), "Home network, Tailscale and loopback addresses are allowed");
   Check(new[] { "8.8.8.8", "172.32.0.1", "100.128.0.1", "100.63.255.255", "2001:4860:4860::8888", "::ffff:8.8.8.8" }.All(a => !CompanionAuth.AllowedRemote(IPAddress.Parse(a))), "Public internet addresses are refused");

   var request = Parse("POST /api/ask?x=1 HTTP/1.1\r\nHost: pc\r\nAuthorization: Bearer abc\r\nContent-Type: application/json\r\nContent-Length: 27\r\n\r\n{\"question\":\"Hi there 😀\"}".Replace("27", Encoding.UTF8.GetByteCount("{\"question\":\"Hi there 😀\"}").ToString()));
   Check(request.Method == "POST" && request.Path == "/api/ask" && request.BearerToken == "abc" && request.Text("question") == "Hi there 😀", "Requests parse method, path, bearer token and UTF-8 JSON");
   Check(StatusOf(delegate { Parse("GET / HTTP/1.1\r\nContent-Length: 999999\r\n\r\n"); }) == 413, "Oversized bodies are refused");
   Check(StatusOf(delegate { Parse("GARBAGE\r\n\r\n"); }) == 400 && StatusOf(delegate { Parse("GET / HTTP/1.1\r\nHost: x"); }) == 400, "Malformed and truncated requests are refused");
   Check(StatusOf(delegate { Parse("POST / HTTP/1.1\r\nTransfer-Encoding: chunked\r\n\r\n"); }) == 411, "Chunked bodies are refused");
   Check(StatusOf(delegate { Parse("GET / HTTP/1.1\r\nX: " + new string('a', 9000) + "\r\n\r\n"); }) == 431, "Huge headers are refused");
   Check(StatusOf(delegate { Parse("POST / HTTP/1.1\r\nContent-Length: 3\r\n\r\n[1]").Json(); }) == 400, "Non-object JSON is refused");

   string written = Encoding.UTF8.GetString(CompanionHttp.Write(CompanionResponse.Error(401, "nope")));
   Check(written.StartsWith("HTTP/1.1 401 Unauthorized\r\n") && written.EndsWith("{\"error\":\"nope\"}") && written.Contains("Content-Length: 16\r\n"), "Responses carry status, length and JSON");

   int port = FreePort();
   using (var server = new CompanionServer(delegate(CompanionRequest r) { return CompanionResponse.Ok(new Dictionary<string, object> { { "path", r.Path }, { "token", r.BearerToken } }); })) {
    server.Start(port);
    string reply = RoundTrip(port, "GET /api/status HTTP/1.1\r\nAuthorization: Bearer xyz\r\n\r\n");
    Check(reply.StartsWith("HTTP/1.1 200") && reply.Contains("\"path\":\"/api/status\"") && reply.Contains("\"token\":\"xyz\""), "Loopback round trip through the live server");
    server.Stop(); bool refused = false; try { RoundTrip(port, "GET / HTTP/1.1\r\n\r\n"); } catch (SocketException) { refused = true; }
    Check(refused && !server.Running, "Stopping the server closes the port");
   }
   var loaded = new CompanionSettings { Port = 5, Devices = null }; loaded.Normalize();
   Check(loaded.Port == CompanionSettings.DefaultPort && loaded.Devices.Count == 0, "Invalid saved settings fall back to safe defaults");
   Func<string, string, KeyValuePair<string, string>> w = delegate(string n, string t) { return new KeyValuePair<string, string>(n, t); };
   var np = NowPlayingReader.FromWindows(new[] { w("Spotify", "Daft Punk - Harder, Better, Faster, Stronger") });
   Check(np.Source == "Spotify" && np.Playing && np.Title == "Daft Punk - Harder, Better, Faster, Stronger", "Spotify window title gives the playing track");
   np = NowPlayingReader.FromWindows(new[] { w("Spotify", "Spotify Premium") });
   Check(np.Source == "Spotify" && !np.Playing && np.Title == "", "Idle Spotify window reads as paused");
   np = NowPlayingReader.FromWindows(new[] { w("chrome", "(3) Lo-fi beats to relax to - YouTube - Google Chrome"), w("Spotify", "Spotify Free") });
   Check(np.Source == "YouTube" && np.Title == "Lo-fi beats to relax to", "YouTube tab title is cleaned and beats paused Spotify");
   np = NowPlayingReader.FromWindows(new[] { w("msedge", "Video - YouTube \u2014 Microsoft Edge"), w("Spotify", "Artist - Song") });
   Check(np.Source == "Spotify" && np.Title == "Artist - Song", "Playing Spotify wins over a YouTube tab");
   Check(NowPlayingReader.FromWindows(new[] { w("chrome", "Inbox - Gmail"), w("notepad", "x - YouTube") }) == null, "Other tabs and apps are ignored");
   Check(MediaKeys.Known("playpause") && MediaKeys.Known("volup") && !MediaKeys.Known("shutdown") && !MediaKeys.Known(null), "Only media actions are accepted");
   Console.WriteLine("All companion checks passed."); return 0;
  } catch (Exception ex) { Console.WriteLine("FAIL " + ex); return 1; }
 }
 static int FreePort() { var l = new TcpListener(IPAddress.Loopback, 0); l.Start(); int p = ((IPEndPoint)l.LocalEndpoint).Port; l.Stop(); return p; }
 static string RoundTrip(int port, string raw) {
  using (var client = new TcpClient()) {
   client.Connect(IPAddress.Loopback, port); client.ReceiveTimeout = 5000;
   var stream = client.GetStream(); var bytes = Encoding.ASCII.GetBytes(raw); stream.Write(bytes, 0, bytes.Length);
   using (var reader = new StreamReader(stream, Encoding.UTF8)) return reader.ReadToEnd();
  }
 }
}
