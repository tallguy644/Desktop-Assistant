using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web;
using System.Web.Script.Serialization;

namespace JarvisDesk {
 public sealed class ChatAccount {
  public string ClientId, Subject, Email, AccessToken, RefreshToken, IdToken, Scope, Model;
  public long ExpiresAt;
 }
 public sealed class ChatMessage { public string Role, Content; }
 public sealed class ChatModel {
  public string Slug, DisplayName;
  public override string ToString() { return DisplayName; }
 }
 public sealed class ChatConnectionException : Exception {
  public ChatConnectionException(string message) : base(message) { }
 }
 public static class ChatJson {
  public static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength = 2 * 1024 * 1024, RecursionLimit = 64 }; }
  public static Dictionary<string, object> Parse(string text) { return Serializer().Deserialize<Dictionary<string, object>>(text); }
  public static string Encode(object value) { return Serializer().Serialize(value); }
  public static string Text(IDictionary<string, object> data, string key) { object value; return data != null && data.TryGetValue(key, out value) && value is string ? (string)value : ""; }
  public static long Number(IDictionary<string, object> data, string key) { object value; long result; return data != null && data.TryGetValue(key, out value) && Int64.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), out result) ? result : 0; }
  public static Dictionary<string, object> Object(IDictionary<string, object> data, string key) { object value; return data != null && data.TryGetValue(key, out value) ? value as Dictionary<string, object> : null; }
  public static IEnumerable<object> Items(IDictionary<string, object> data, string key) { object value; return data != null && data.TryGetValue(key, out value) && value is IEnumerable && !(value is string) ? ((IEnumerable)value).Cast<object>() : Enumerable.Empty<object>(); }
 }
 public sealed partial class ChatGptClient {
  const string Issuer = "https://auth.openai.com", Resource = "https://api.openai.com/v1";
  const string AuthEndpoint = Issuer + "/api/accounts/authorize", TokenEndpoint = Issuer + "/api/accounts/oauth/token";
  const string Scope = "openid profile email offline_access resource.invoke chatgpt.tokens.use.direct";
  readonly string directory, accountFile;
  readonly object accountGate = new object();
  ChatAccount account;
  static readonly byte[] StorageEntropy = Encoding.UTF8.GetBytes("Skynet.ChatGPT.v1");
  public ChatGptClient(string dataDirectory) {
   directory = dataDirectory; Directory.CreateDirectory(directory); accountFile = Path.Combine(directory, "chatgpt-account.protected");
   if (File.Exists(accountFile)) { try { account = ReadProtected<ChatAccount>(accountFile); } catch { account = null; } }
  }
  public bool Connected { get { lock(accountGate) return account != null && !String.IsNullOrEmpty(account.AccessToken) && HasPlanScope(account.Scope); } }
  public string AccountLabel { get { lock(accountGate) return Connected ? (String.IsNullOrWhiteSpace(account.Email) ? "ChatGPT connected" : "Connected / " + account.Email) : "Not connected"; } }
  public string SelectedModel { get { lock(accountGate) return account == null ? "" : account.Model ?? ""; } }
  public static long Now { get { return (long)(DateTime.UtcNow - new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds; } }
  public static bool HasPlanScope(string scope) { return (scope ?? "").Split(' ').Contains("chatgpt.tokens.use.direct"); }
  public static string Base64Url(byte[] value) { return Convert.ToBase64String(value).TrimEnd('=').Replace('+','-').Replace('/','_'); }
  public static byte[] Decode64(string value) { string padded = value.Replace('-','+').Replace('_','/'); return Convert.FromBase64String(padded + new string('=', (4 - padded.Length % 4) % 4)); }
  static string RandomValue() { var bytes = new byte[32]; using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes); return Base64Url(bytes); }
  public static string PkceChallenge(string verifier) { using (var hash = SHA256.Create()) return Base64Url(hash.ComputeHash(Encoding.ASCII.GetBytes(verifier))); }
  public static bool Equal(string a, string b) { if (a == null || b == null) return false; int different = a.Length ^ b.Length; for (int i = 0; i < Math.Min(a.Length,b.Length); i++) different |= a[i] ^ b[i]; return different == 0; }
  public static void WriteProtected(string path, object value) {
   byte[] encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(ChatJson.Encode(value)), StorageEntropy, DataProtectionScope.CurrentUser);
   string temporary = path + ".tmp"; File.WriteAllBytes(temporary, encrypted);
   if (File.Exists(path)) File.Replace(temporary,path,null); else File.Move(temporary,path);
  }
  public static T ReadProtected<T>(string path) { return ChatJson.Serializer().Deserialize<T>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path),StorageEntropy,DataProtectionScope.CurrentUser))); }
  void SaveAccount(ChatAccount value) { WriteProtected(accountFile,value); lock(accountGate) account = value; }
  public void SetModel(string model) { lock(accountGate) { if (account == null) return; account.Model = model; WriteProtected(accountFile,account); } }
  public void Disconnect() { lock(accountGate) { if (File.Exists(accountFile)) File.Delete(accountFile); account = null; } }
  string HostId() {
   string file = Path.Combine(directory,"chatgpt-host.txt"); Guid id;
   if (File.Exists(file)) { string saved = File.ReadAllText(file).Trim(); if (saved.StartsWith("urn:uuid:") && Guid.TryParse(saved.Substring(9),out id)) return saved; }
   string host = "urn:uuid:" + Guid.NewGuid().ToString("D"); File.WriteAllText(file,host); return host;
  }
  public static string AuthorizationUrl(string clientId, string hostId, string callback, string state, string nonce, string verifier, string idToken) {
   var values = new Dictionary<string,string> { {"client_id",clientId}, {"ext_agent_host_id",hostId}, {"redirect_uri",callback}, {"response_type","code"}, {"scope",Scope}, {"resource",Resource}, {"state",state}, {"nonce",nonce}, {"code_challenge_method","S256"}, {"code_challenge",PkceChallenge(verifier)} };
   if (clientId == "dynamic_agent_client") values["agent_name_hint"] = AssistantIdentity.CurrentName;
   else if (!String.IsNullOrWhiteSpace(idToken)) values["id_token_hint"] = idToken;
   return AuthEndpoint + "?" + Form(values);
  }
  static string Form(IDictionary<string,string> values) { return String.Join("&",values.Select(p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value))); }
  public static Dictionary<string,string> CheckCallback(string target, string expectedHost, string expectedState, string expectedClient) {
   if (!target.StartsWith("/auth/callback?", StringComparison.Ordinal)) throw new ChatConnectionException("The sign-in callback path did not match.");
   var query = HttpUtility.ParseQueryString(new Uri("http://" + expectedHost + target).Query);
   if (query.GetValues("state") == null || query.GetValues("state").Length != 1 || !Equal(query["state"],expectedState)) throw new ChatConnectionException("The sign-in callback could not be verified. Try connecting again.");
   if (!String.IsNullOrEmpty(query["error"])) throw new ChatConnectionException("ChatGPT sign-in was declined or plan sharing was not approved. You can try again.");
   string code = query["code"], client = query["client_id"];
   if (query.GetValues("code") == null || query.GetValues("code").Length != 1 || String.IsNullOrWhiteSpace(code) || code.Length > 8192) throw new ChatConnectionException("Sign-in did not return a valid authorization code.");
   if (query.GetValues("client_id") != null && query.GetValues("client_id").Length != 1) throw new ChatConnectionException("The sign-in client could not be verified.");
   if (expectedClient == "dynamic_agent_client") { if (String.IsNullOrWhiteSpace(client) || !client.StartsWith("oaiapp_",StringComparison.Ordinal) || client.Length > 256) throw new ChatConnectionException("ChatGPT did not finish registering " + AssistantIdentity.CurrentName + ". Try connecting again."); }
   else { if (!String.IsNullOrWhiteSpace(client) && !Equal(client,expectedClient)) throw new ChatConnectionException("The sign-in account registration changed. Disconnect first to use another account."); client = expectedClient; }
   return new Dictionary<string,string> { {"code",code}, {"client_id",client} };
  }
  public void SignIn(Action<string> launchBrowser, CancellationToken cancellation) {
   ChatAccount previous; lock(accountGate) previous = account;
   string clientId = previous == null || String.IsNullOrWhiteSpace(previous.ClientId) ? "dynamic_agent_client" : previous.ClientId;
   string state = RandomValue(), nonce = RandomValue(), verifier = RandomValue();
   var listener = new TcpListener(IPAddress.Loopback,0); listener.Start();
   try {
    string host = "127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
    string callback = "http://" + host + "/auth/callback";
    launchBrowser(AuthorizationUrl(clientId,HostId(),callback,state,nonce,verifier,previous == null ? null : previous.IdToken));
    Dictionary<string,string> approved = null; DateTime deadline = DateTime.UtcNow.AddMinutes(5);
    using (cancellation.Register(delegate { listener.Stop(); })) {
     while (approved == null) {
      cancellation.ThrowIfCancellationRequested(); if (DateTime.UtcNow > deadline) throw new ChatConnectionException("Sign-in timed out. Click Continue with ChatGPT to try again.");
      if (!listener.Pending()) { cancellation.WaitHandle.WaitOne(100); continue; }
      using (var connection = listener.AcceptTcpClient()) using (var stream = connection.GetStream()) {
       stream.ReadTimeout = 2500; stream.WriteTimeout = 2500;
       string target = null, requestHost = null; var header = new StringBuilder();
       try {
        while (header.Length < 16384 && !header.ToString().EndsWith("\r\n\r\n",StringComparison.Ordinal)) { int next = stream.ReadByte(); if (next < 0) break; header.Append((char)next); }
        string[] lines = header.ToString().Split(new string[] {"\r\n"},StringSplitOptions.None); string[] request = lines[0].Split(' ');
        if (request.Length == 3 && request[0] == "GET") target = request[1];
        foreach (string line in lines) if (line.StartsWith("Host:",StringComparison.OrdinalIgnoreCase)) { if (requestHost != null) throw new IOException(); requestHost = line.Substring(5).Trim(); }
        if (requestHost != host || target == null || !target.StartsWith("/auth/callback?",StringComparison.Ordinal)) { Reply(stream,false,"This address is only used for " + AssistantIdentity.CurrentName + " sign-in."); continue; }
        approved = CheckCallback(target,host,state,clientId);
        Reply(stream,true,AssistantIdentity.CurrentName + " received your sign-in. Return to " + AssistantIdentity.CurrentName + " to finish connecting.");
       } catch (ChatConnectionException) { Reply(stream,false,AssistantIdentity.CurrentName + " could not verify this sign-in. Return to the app and try again."); throw; }
       catch (IOException) { continue; }
      }
     }
    }
    var tokens = TokenRequest(new Dictionary<string,string> { {"grant_type","authorization_code"}, {"client_id",approved["client_id"]}, {"code",approved["code"]}, {"code_verifier",verifier}, {"redirect_uri",callback}, {"resource",Resource} },cancellation);
    string issuedIdToken = ChatJson.Text(tokens,"id_token");
    var claims = VerifyIdentity(issuedIdToken,approved["client_id"],nonce,previous == null ? null : previous.Subject,ReadKeys(cancellation));
    var candidate = FromTokens(tokens,approved["client_id"],claims,null);
    if (!HasPlanScope(candidate.Scope)) throw new ChatConnectionException("Sign-in succeeded, but permission to use your ChatGPT plan was not granted. Enable plan sharing during sign-in.");
    SaveAccount(candidate);
   } catch (SocketException) { cancellation.ThrowIfCancellationRequested(); throw new ChatConnectionException("The local sign-in connection closed. Try connecting again."); }
   catch (ObjectDisposedException) { cancellation.ThrowIfCancellationRequested(); throw; }
   finally { listener.Stop(); }
  }
  static void Reply(Stream stream, bool accepted, string message) {
   byte[] body = Encoding.UTF8.GetBytes("<!doctype html><meta charset=\"utf-8\"><title>" + AssistantIdentity.CurrentName + " sign-in</title><body style=\"background:#030d14;color:#00e6eb;font:20px system-ui;padding:48px\"><h1>" + AssistantIdentity.CurrentName + "</h1><p>" + HttpUtility.HtmlEncode(message) + "</p></body>");
   byte[] headers = Encoding.ASCII.GetBytes("HTTP/1.1 " + (accepted ? "200 OK" : "400 Bad Request") + "\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: " + body.Length + "\r\nCache-Control: no-store\r\nReferrer-Policy: no-referrer\r\nContent-Security-Policy: default-src 'none'; style-src 'unsafe-inline'\r\nConnection: close\r\n\r\n"); stream.Write(headers,0,headers.Length); stream.Write(body,0,body.Length);
  }
  static ChatAccount FromTokens(IDictionary<string,object> tokens, string client, IDictionary<string,object> claims, ChatAccount prior) {
   string access = ChatJson.Text(tokens,"access_token"), type = ChatJson.Text(tokens,"token_type"); long seconds = ChatJson.Number(tokens,"expires_in");
   if (String.IsNullOrWhiteSpace(access) || !type.Equals("Bearer",StringComparison.OrdinalIgnoreCase) || seconds <= 0 || seconds > 604800) throw new ChatConnectionException("ChatGPT returned incomplete sign-in credentials. Connect again.");
   return new ChatAccount { ClientId = client, AccessToken = access, RefreshToken = Nonempty(ChatJson.Text(tokens,"refresh_token"),prior == null ? "" : prior.RefreshToken), IdToken = Nonempty(ChatJson.Text(tokens,"id_token"),prior == null ? "" : prior.IdToken), Scope = Nonempty(ChatJson.Text(tokens,"scope"),prior == null ? "" : prior.Scope), ExpiresAt = Now + seconds, Subject = claims == null ? prior.Subject : ChatJson.Text(claims,"sub"), Email = claims == null ? prior.Email : ChatJson.Text(claims,"email"), Model = prior == null ? "" : prior.Model };
  }
  static string Nonempty(string value, string fallback) { return String.IsNullOrWhiteSpace(value) ? fallback : value; }
  static Dictionary<string,object> ReadKeys(CancellationToken cancellation) {
   var discovery = GetJson(Issuer + "/.well-known/openid-configuration",null,cancellation);
   if (ChatJson.Text(discovery,"issuer") != Issuer || ChatJson.Text(discovery,"authorization_endpoint") != AuthEndpoint || ChatJson.Text(discovery,"token_endpoint") != TokenEndpoint || ChatJson.Text(discovery,"jwks_uri") != Issuer + "/.well-known/jwks.json") throw new ChatConnectionException("OpenAI sign-in discovery could not be verified.");
   return GetJson(Issuer + "/.well-known/jwks.json",null,cancellation);
  }
  public static Dictionary<string,object> VerifyIdentity(string jwt, string client, string nonce, string subject, Dictionary<string,object> keys) {
   try {
    string[] parts = (jwt ?? "").Split('.'); if (parts.Length != 3) throw new Exception();
    var header = ChatJson.Parse(Encoding.UTF8.GetString(Decode64(parts[0]))); var claims = ChatJson.Parse(Encoding.UTF8.GetString(Decode64(parts[1])));
    if (ChatJson.Text(header,"alg") != "RS256" || String.IsNullOrEmpty(ChatJson.Text(header,"kid"))) throw new Exception();
    var key = ChatJson.Items(keys,"keys").OfType<Dictionary<string,object>>().SingleOrDefault(k => ChatJson.Text(k,"kid") == ChatJson.Text(header,"kid") && ChatJson.Text(k,"kty") == "RSA" && (ChatJson.Text(k,"use") == "" || ChatJson.Text(k,"use") == "sig") && (ChatJson.Text(k,"alg") == "" || ChatJson.Text(k,"alg") == "RS256"));
    if (key == null) throw new Exception();
    using (var rsa = new RSACryptoServiceProvider()) { rsa.PersistKeyInCsp = false; rsa.ImportParameters(new RSAParameters { Modulus = Decode64(ChatJson.Text(key,"n")), Exponent = Decode64(ChatJson.Text(key,"e")) }); if (!rsa.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]),CryptoConfig.MapNameToOID("SHA256"),Decode64(parts[2]))) throw new Exception(); }
    bool audience = ChatJson.Text(claims,"aud") == client || ChatJson.Items(claims,"aud").OfType<string>().Contains(client);
    if((!String.IsNullOrEmpty(ChatJson.Text(claims,"azp")) && ChatJson.Text(claims,"azp")!=client) || (ChatJson.Items(claims,"aud").Count()>1 && ChatJson.Text(claims,"azp")!=client)) throw new Exception();
    if (ChatJson.Text(claims,"iss") != Issuer || !audience || ChatJson.Number(claims,"exp") <= Now - 5 || ChatJson.Number(claims,"iat") <= 0 || ChatJson.Number(claims,"iat") > Now + 5 || ChatJson.Number(claims,"nbf") > Now + 5 || String.IsNullOrWhiteSpace(ChatJson.Text(claims,"sub"))) throw new Exception();
    if (nonce != null && !Equal(ChatJson.Text(claims,"nonce"),nonce)) throw new Exception();
    if (!String.IsNullOrWhiteSpace(subject) && !Equal(ChatJson.Text(claims,"sub"),subject)) throw new Exception();
    return claims;
   } catch { throw new ChatConnectionException("The ChatGPT account identity could not be verified. No new credentials were saved."); }
  }
  string AccessToken(CancellationToken cancellation) {
   ChatAccount current; lock(accountGate) current=account;
   if (current == null || !HasPlanScope(current.Scope)) throw new ChatConnectionException("Click Continue with ChatGPT before sending a question.");
   if (current.ExpiresAt > Now + 120) return current.AccessToken;
   if (String.IsNullOrWhiteSpace(current.RefreshToken)) throw new ChatConnectionException("Your ChatGPT sign-in needs renewal. Click Continue with ChatGPT.");
   var tokens = TokenRequest(new Dictionary<string,string> { {"grant_type","refresh_token"}, {"client_id",current.ClientId}, {"refresh_token",current.RefreshToken}, {"resource",Resource} },cancellation);
   var claims = String.IsNullOrWhiteSpace(ChatJson.Text(tokens,"id_token")) ? null : VerifyIdentity(ChatJson.Text(tokens,"id_token"),current.ClientId,null,current.Subject,ReadKeys(cancellation));
   var renewed = FromTokens(tokens,current.ClientId,claims,current); if (!HasPlanScope(renewed.Scope)) throw new ChatConnectionException("ChatGPT plan sharing is no longer enabled. Reconnect your account.");
   lock(accountGate) { if(!Object.ReferenceEquals(current,account)) throw new ChatConnectionException("The connected account changed. Try again."); SaveAccount(renewed); } return renewed.AccessToken;
  }
  static Dictionary<string,object> TokenRequest(IDictionary<string,string> form, CancellationToken cancellation) { return JsonRequest(TokenEndpoint,"POST",null,Form(form),"application/x-www-form-urlencoded",cancellation); }
  static Dictionary<string,object> GetJson(string url, string bearer, CancellationToken cancellation) { return JsonRequest(url,"GET",bearer,null,null,cancellation); }
  static HttpWebRequest Request(string url, string method, string bearer, int timeout) {
   var uri = new Uri(url);
   if (uri.Scheme != "https" || (uri.Host != "auth.openai.com" && uri.Host != "api.openai.com") || !uri.IsDefaultPort || !String.IsNullOrEmpty(uri.UserInfo)) throw new ChatConnectionException("The ChatGPT service address is invalid.");
   ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
   var request = (HttpWebRequest)WebRequest.Create(uri); request.Method = method; request.AllowAutoRedirect = false; request.Timeout = timeout; request.ReadWriteTimeout = timeout; request.UserAgent = "Skynet/3.0"; request.KeepAlive = false;
   if (!String.IsNullOrEmpty(bearer)) request.Headers[HttpRequestHeader.Authorization] = "Bearer " + bearer;
   return request;
  }
  static Dictionary<string,object> JsonRequest(string url, string method, string bearer, string body, string contentType, CancellationToken cancellation) {
   var request = Request(url,method,bearer,20000);
   try { using(cancellation.Register(delegate { request.Abort(); })) {
    cancellation.ThrowIfCancellationRequested(); if (body != null) { request.ContentType = contentType; byte[] bytes = Encoding.UTF8.GetBytes(body); request.ContentLength = bytes.Length; using(var stream = request.GetRequestStream()) stream.Write(bytes,0,bytes.Length); }
    using(var response = (HttpWebResponse)request.GetResponse()) {
     if (response.StatusCode != HttpStatusCode.OK) throw new ChatConnectionException("ChatGPT returned an unexpected service response.");
     using(var reader = new StreamReader(response.GetResponseStream(),Encoding.UTF8)) { var result = new StringBuilder(); char[] buffer = new char[4096]; int n; while((n=reader.Read(buffer,0,buffer.Length))>0) { cancellation.ThrowIfCancellationRequested(); if (result.Length+n>2*1024*1024) throw new ChatConnectionException("The ChatGPT service response was too large."); result.Append(buffer,0,n); } return ChatJson.Parse(result.ToString()); }
    }
   }} catch(WebException ex) { cancellation.ThrowIfCancellationRequested(); throw ServiceError(ex); }
   catch(ChatConnectionException) { throw; } catch(OperationCanceledException) { throw; } catch { throw new ChatConnectionException("The ChatGPT connection could not be completed. Check your connection and try again."); }
  }
  static ChatConnectionException ServiceError(WebException ex) {
   var response = ex.Response as HttpWebResponse; if(response == null) return new ChatConnectionException("Could not reach ChatGPT. Check your internet connection and try again.");
   int status = (int)response.StatusCode; response.Dispose();
   if(status == 401) return new ChatConnectionException("Your ChatGPT sign-in expired or was rejected. Click Continue with ChatGPT to reconnect.");
   if(status == 403) return new ChatConnectionException("ChatGPT plan sharing or this model is unavailable for your account or workspace.");
   if(status == 429) return new ChatConnectionException("ChatGPT usage or request limits were reached. Check your ChatGPT plan usage and try later.");
   if(status == 400) return new ChatConnectionException("ChatGPT could not accept the request. Reconnect or choose another available model.");
   return new ChatConnectionException("ChatGPT is temporarily unavailable (HTTP " + status + "). Try again shortly.");
  }
 }
}
