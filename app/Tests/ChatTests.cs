using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using JarvisDesk;

public static class SkynetChatTests {
 static List<string> passed;
 static void Check(bool value,string message) { if(!value) throw new Exception(message); passed.Add("PASS " + message); }
 static void Reject(Action action,string message) { bool rejected=false; try { action(); } catch(ChatConnectionException) { rejected=true; } Check(rejected,message); }
 static string Event(object value) { return "data: " + ChatJson.Encode(value) + "\n\n"; }
 static Dictionary<string,object> D(params object[] pairs) { var result=new Dictionary<string,object>(); for(int i=0;i<pairs.Length;i+=2) result[(string)pairs[i]]=pairs[i+1]; return result; }
 static string Token(RSACryptoServiceProvider rsa,Dictionary<string,object> claims,string alg="RS256") {
  string header=ChatGptClient.Base64Url(Encoding.UTF8.GetBytes(ChatJson.Encode(D("alg",alg,"kid","test-key")))) + "." + ChatGptClient.Base64Url(Encoding.UTF8.GetBytes(ChatJson.Encode(claims)));
  return header + "." + ChatGptClient.Base64Url(rsa.SignData(Encoding.ASCII.GetBytes(header),CryptoConfig.MapNameToOID("SHA256")));
 }
 public static string[] Run(string directory) {
  passed=new List<string>(); Directory.CreateDirectory(directory);
  Check(ChatGptClient.PkceChallenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk")=="E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM","RFC 7636 S256 PKCE challenge");
  string callback="http://127.0.0.1:54321/auth/callback", host="urn:uuid:"+Guid.NewGuid();
  var authorization=new Uri(ChatGptClient.AuthorizationUrl("dynamic_agent_client",host,callback,"state","nonce","verifier",null)); var query=HttpUtility.ParseQueryString(authorization.Query);
  Check(authorization.Host=="auth.openai.com" && query["client_id"]=="dynamic_agent_client" && query["agent_name_hint"]==AssistantIdentity.CurrentName && query["redirect_uri"]==callback && query["ext_agent_host_id"]==host && query["code_challenge_method"]=="S256" && query["nonce"]=="nonce" && query["scope"].Contains("chatgpt.tokens.use.direct"),"Official dynamic registration, loopback callback, host ID, nonce, and plan scope");
  var returned=HttpUtility.ParseQueryString(new Uri(ChatGptClient.AuthorizationUrl("oaiapp_saved",host,callback,"state","nonce","verifier","dummy-id-hint")).Query);
  Check(returned["id_token_hint"]=="dummy-id-hint" && returned["agent_name_hint"]==null,"Returning account uses saved registration without a new app registration");
  Check(ChatGptClient.CheckCallback("/auth/callback?state=state&code=code&client_id=oaiapp_test","127.0.0.1:54321","state","dynamic_agent_client")["client_id"]=="oaiapp_test","Validated new-registration callback");
  Reject(()=>ChatGptClient.CheckCallback("/auth/callback?state=wrong&code=code&client_id=oaiapp_test","127.0.0.1:54321","state","dynamic_agent_client"),"Reject mismatched callback state");
  Reject(()=>ChatGptClient.CheckCallback("/auth/callback?state=state&state=state&code=code&client_id=oaiapp_test","127.0.0.1:54321","state","dynamic_agent_client"),"Reject duplicate callback state");
  Reject(()=>ChatGptClient.CheckCallback("/auth/callback?state=state&code=code","127.0.0.1:54321","state","dynamic_agent_client"),"Require issued registration ID on first sign-in");
  Reject(()=>ChatGptClient.CheckCallback("/auth/callback?state=state&code=code&client_id=oaiapp_other","127.0.0.1:54321","state","oaiapp_saved"),"Reject changed registration ID during returning sign-in");
  Reject(()=>ChatGptClient.CheckCallback("/callback?state=state&code=code","127.0.0.1:54321","state","oaiapp_saved"),"Reject callback path changes");
  Reject(()=>ChatGptClient.CheckCallback("/auth/callback?state=state&error=access_denied","127.0.0.1:54321","state","dynamic_agent_client"),"Handle declined plan-sharing consent");
  using(var rsa=new RSACryptoServiceProvider(2048)) {
   rsa.PersistKeyInCsp=false; var pub=rsa.ExportParameters(false); var keys=D("keys",new object[] { D("kid","test-key","kty","RSA","use","sig","alg","RS256","n",ChatGptClient.Base64Url(pub.Modulus),"e",ChatGptClient.Base64Url(pub.Exponent)) });
   var claims=D("iss","https://auth.openai.com","aud","oaiapp_test","sub","test-user","iat",ChatGptClient.Now,"exp",ChatGptClient.Now+600,"nonce","nonce");
   string valid=Token(rsa,claims); Check(ChatGptClient.VerifyIdentity(valid,"oaiapp_test","nonce",null,keys)["sub"].Equals("test-user"),"Validate RS256 signature and identity claims with public JWK");
   Reject(()=>ChatGptClient.VerifyIdentity(valid,"oaiapp_other","nonce",null,keys),"Reject wrong ID-token audience");
   Reject(()=>ChatGptClient.VerifyIdentity(valid,"oaiapp_test","wrong",null,keys),"Reject wrong ID-token nonce");
   Reject(()=>ChatGptClient.VerifyIdentity(valid,"oaiapp_test","nonce","other-user",keys),"Reject identity switching during reauthorization");
   claims["azp"]="oaiapp_other"; Reject(()=>ChatGptClient.VerifyIdentity(Token(rsa,claims),"oaiapp_test","nonce",null,keys),"Reject mismatched authorized-party claim"); claims.Remove("azp");
   Reject(()=>ChatGptClient.VerifyIdentity(Token(rsa,claims,"none"),"oaiapp_test","nonce",null,keys),"Reject unsupported ID-token algorithms");
   claims["exp"]=ChatGptClient.Now-100; Reject(()=>ChatGptClient.VerifyIdentity(Token(rsa,claims),"oaiapp_test","nonce",null,keys),"Reject expired ID tokens");
   claims["exp"]=ChatGptClient.Now+600; claims["iss"]="https://example.com"; Reject(()=>ChatGptClient.VerifyIdentity(Token(rsa,claims),"oaiapp_test","nonce",null,keys),"Reject wrong ID-token issuer");
   claims["iss"]="https://auth.openai.com"; string signed=Token(rsa,claims); var parts=signed.Split('.'); parts[1]=ChatGptClient.Base64Url(Encoding.UTF8.GetBytes(ChatJson.Encode(D("sub","forged")))); Reject(()=>ChatGptClient.VerifyIdentity(String.Join(".",parts),"oaiapp_test","nonce",null,keys),"Reject forged token payloads");
  }
  string protectedPath=Path.Combine(directory,"fixture.protected"); var credentials=new ChatAccount { ClientId="oaiapp_fixture",AccessToken="dummy-secret-access",RefreshToken="dummy-secret-refresh",Scope="resource.invoke chatgpt.tokens.use.direct" };
  ChatGptClient.WriteProtected(protectedPath,credentials); ChatGptClient.WriteProtected(protectedPath,credentials);
  Check(!Encoding.UTF8.GetString(File.ReadAllBytes(protectedPath)).Contains("dummy-secret") && ChatGptClient.ReadProtected<ChatAccount>(protectedPath).RefreshToken==credentials.RefreshToken,"DPAPI encrypted credentials and atomic roundtrip"); File.Delete(protectedPath);
  Check(ChatGptClient.HasPlanScope(credentials.Scope) && !ChatGptClient.HasPlanScope("openid profile chatgpt.tokens.use.direct.fake"),"Require exact granted plan-sharing scope");
  var messages=new List<ChatMessage> { new ChatMessage { Role="user",Content="Hello & <world>" } }; var payload=ChatJson.Parse(ChatGptClient.BuildConversation("available-model",messages));
  Check(payload["store"].Equals(false) && payload["stream"].Equals(true) && !payload.ContainsKey("tools") && !payload.ContainsKey("previous_response_id") && !payload.ContainsKey("temperature") && ChatJson.Items(payload,"input").Count()==1,"Streaming request uses supported plan fields and has no action tools");
  Reject(()=>ChatGptClient.BuildConversation("model",new List<ChatMessage> { null }),"Reject invalid message records");
  Reject(()=>ChatGptClient.BuildConversation("model",new List<ChatMessage> { new ChatMessage { Role="system",Content="bad role" } }),"Reject unsupported conversation roles");
  var models=ChatGptClient.ParseModels(D("models",new object[] { D("visibility","list","slug","first","display_name","First"),D("visibility","hidden","slug","hidden"),D("visibility","list","slug","second","display_name","Second") }));
  Check(models.Count==2 && models[0].Slug=="first" && models[1].Slug=="second","Model picker uses visible server catalog in server order");
  string completed=Event(D("type","response.completed","response",D("status","completed")));
  Check(ChatGptClient.IsStreamResponse(HttpStatusCode.OK,"") && ChatGptClient.IsStreamResponse(HttpStatusCode.OK,"text/event-stream; charset=utf-8") && !ChatGptClient.IsStreamResponse(HttpStatusCode.OK,"application/json") && !ChatGptClient.IsStreamResponse(HttpStatusCode.NoContent,""),"Accept real plan-sharing SSE responses with omitted media type while requiring HTTP success");
  string stream=Event(D("type","response.output_text.delta","delta","Hello "))+Event(D("type","response.output_text.delta","delta","world."))+completed;
  var delta=new StringBuilder(); Check(ChatGptClient.ReadEvents(new StringReader(stream),s=>delta.Append(s),CancellationToken.None)=="Hello world." && delta.ToString()=="Hello world.","Streamed text requires successful response completion");
  Reject(()=>ChatGptClient.ReadEvents(new StringReader("data: [DONE]\n\n"),null,CancellationToken.None),"Reject disconnected streams without completion");
  Reject(()=>ChatGptClient.ReadEvents(new StringReader(Event(D("type","response.completed","response",D("status","incomplete")))),null,CancellationToken.None),"Reject incomplete terminal status");
  Reject(()=>ChatGptClient.ReadEvents(new StringReader(Event(D("type","response.failed","response",D("error",D("code","subscription_sharing_usage_limit_exceeded","message","dummy-secret-access"))))),null,CancellationToken.None),"Handle plan usage failures inside a started stream");
  string finalOnly=Event(D("type","response.completed","response",D("status","completed","output",new object[] { D("content",new object[] { D("type","output_text","text","Final text") }) })));
  Check(ChatGptClient.ReadEvents(new StringReader(finalOnly),null,CancellationToken.None)=="Final text","Read completed output when a stream has no text deltas");
  using(var cancelled=new CancellationTokenSource()) { cancelled.Cancel(); bool stopped=false; try { ChatGptClient.ReadEvents(new StringReader(stream),null,cancelled.Token); } catch(OperationCanceledException) { stopped=true; } Check(stopped,"Cancel response stream processing"); }
  using(var cancellation=new CancellationTokenSource()) {
   var client=new ChatGptClient(Path.Combine(directory,"cancel-profile")); bool cancelled=false;
   try { client.SignIn(url=>cancellation.Cancel(),cancellation.Token); } catch(OperationCanceledException) { cancelled=true; }
   Check(cancelled && !client.Connected,"Cancelled browser sign-in closes the loopback listener without saving an account");
  }
  using(var cancellation=new CancellationTokenSource()) {
   var client=new ChatGptClient(Path.Combine(directory,"callback-profile")); Task fakeBrowser=null; bool rejected=false;
   try { client.SignIn(url=> {
    var q=HttpUtility.ParseQueryString(new Uri(url).Query); var uri=new Uri(q["redirect_uri"]);
    fakeBrowser=Task.Run(()=> { using(var tcp=new TcpClient("127.0.0.1",uri.Port)) using(var s=tcp.GetStream()) { s.ReadTimeout=3000; string req="GET /auth/callback?state=wrong&code=fixture&client_id=oaiapp_fixture HTTP/1.1\r\nHost: "+uri.Authority+"\r\n\r\n"; byte[] b=Encoding.ASCII.GetBytes(req); s.Write(b,0,b.Length); byte[] buffer=new byte[2048]; int n=s.Read(buffer,0,buffer.Length); if(!Encoding.ASCII.GetString(buffer,0,n).StartsWith("HTTP/1.1 400")) throw new Exception("Expected invalid callback response."); } });
   },cancellation.Token); } catch(ChatConnectionException) { rejected=true; }
   if(fakeBrowser!=null && !fakeBrowser.Wait(5000)) throw new Exception("Fake browser did not finish."); Check(rejected && !client.Connected,"Actual loopback callback rejects incorrect state without contacting OpenAI");
  }
  return passed.ToArray();
 }
}
