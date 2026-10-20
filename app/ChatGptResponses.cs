using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace JarvisDesk {
 public sealed partial class ChatGptClient {
  public List<ChatModel> Models(CancellationToken cancellation) {
   var catalog = GetJson(Resource + "/models",AccessToken(cancellation),cancellation);
   return ParseModels(catalog);
  }
  public static List<ChatModel> ParseModels(Dictionary<string,object> catalog) {
   return ChatJson.Items(catalog,"models").OfType<Dictionary<string,object>>().Where(m => ChatJson.Text(m,"visibility") == "list" && !String.IsNullOrWhiteSpace(ChatJson.Text(m,"slug"))).Select(m => new ChatModel { Slug = ChatJson.Text(m,"slug"), DisplayName = String.IsNullOrWhiteSpace(ChatJson.Text(m,"display_name")) ? ChatJson.Text(m,"slug") : ChatJson.Text(m,"display_name") }).GroupBy(m => m.Slug).Select(g => g.First()).ToList();
  }
  public static string BuildConversation(string model, IList<ChatMessage> messages) {
   return BuildConversation(model,messages,"Assistant");
  }
  public static string BuildConversation(string model, IList<ChatMessage> messages,string chatName) {
   chatName=ChatNamePreferences.Validate(chatName);
   if (String.IsNullOrWhiteSpace(model) || model.Length > 256) throw new ChatConnectionException("Choose an available ChatGPT model first.");
   if(messages == null || messages.Count == 0 || messages.Count > 40 || messages.Any(m => m == null || (m.Role != "user" && m.Role != "assistant") || String.IsNullOrWhiteSpace(m.Content)) || messages.Sum(m => m.Content.Length) > 32000) throw new ChatConnectionException("The conversation is too large or contains an invalid message. Start a new chat.");
   return ChatJson.Encode(new Dictionary<string,object> {
    {"model",model}, {"store",false}, {"stream",true},
    {"instructions","You are " + chatName + ", a friendly personal desktop assistant. Reply naturally in American English, usually within 90 words, using short spoken sentences. This conversation has no action tools: do not claim to open apps, change settings, read system sensors, or access files. The app handles saved launch commands separately. If asked to perform an action, explain that limitation honestly. Answer the user's question directly; avoid unnecessary formatting and long lists. Never request account passwords, API keys, or sign-in tokens."},
    {"input",messages.Select(m => new Dictionary<string,object> { {"role",m.Role}, {"content",m.Content} }).ToArray()}
   });
  }
  public string Ask(string model, IList<ChatMessage> messages, Action<string> onDelta, CancellationToken cancellation) {
   return Ask(model,messages,onDelta,cancellation,"Assistant");
  }
  public string Ask(string model, IList<ChatMessage> messages, Action<string> onDelta, CancellationToken cancellation,string chatName) {
   string body = BuildConversation(model,messages,chatName);
   var request = Request(Resource + "/responses","POST",AccessToken(cancellation),60000);
   request.ContentType = "application/json"; request.Accept = "text/event-stream";
   try { using(cancellation.Register(delegate { request.Abort(); })) {
    cancellation.ThrowIfCancellationRequested(); byte[] bytes = Encoding.UTF8.GetBytes(body); request.ContentLength = bytes.Length;
    using(var stream = request.GetRequestStream()) stream.Write(bytes,0,bytes.Length);
    using(var response = (HttpWebResponse)request.GetResponse()) {
     if (!IsStreamResponse(response.StatusCode,response.ContentType)) throw new ChatConnectionException("ChatGPT did not return a conversation stream.");
     using(var reader = new StreamReader(response.GetResponseStream(),Encoding.UTF8)) return ReadEvents(reader,onDelta,cancellation);
    }
   }} catch(WebException ex) { cancellation.ThrowIfCancellationRequested(); throw ServiceError(ex); }
   catch(ChatConnectionException) { throw; } catch(OperationCanceledException) { throw; }
   catch { throw new ChatConnectionException("The ChatGPT reply was interrupted. The incomplete reply was not added to the conversation."); }
  }
  public static bool IsStreamResponse(HttpStatusCode status,string contentType) {
   // Some plan-sharing responses omit Content-Type. The SSE parser still requires
   // valid events and a response.completed event with completed status.
   return status==HttpStatusCode.OK && (String.IsNullOrWhiteSpace(contentType) || contentType.IndexOf("text/event-stream",StringComparison.OrdinalIgnoreCase)>=0);
  }
  public static string ReadEvents(TextReader reader, Action<string> onDelta, CancellationToken cancellation) {
   var text = new StringBuilder(); var data = new StringBuilder(); int total = 0; bool completed = false; string line;
   while((line = reader.ReadLine()) != null) {
    cancellation.ThrowIfCancellationRequested(); total += line.Length;
    if(total > 4*1024*1024 || line.Length > 1024*1024) throw new ChatConnectionException("The ChatGPT response exceeded the local size limit.");
    if(line.Length == 0) { if(data.Length > 0) { if(ConsumeEvent(data.ToString(),text,onDelta)) { completed = true; break; } data.Clear(); } }
    else if(line.StartsWith("data:",StringComparison.Ordinal)) { if(data.Length>0) data.Append('\n'); data.Append(line.Substring(5).TrimStart(' ')); }
   }
   if(!completed && data.Length>0) completed = ConsumeEvent(data.ToString(),text,onDelta);
   if(!completed) throw new ChatConnectionException("ChatGPT disconnected before completing its answer. Please try again.");
   if(text.Length == 0) throw new ChatConnectionException("ChatGPT completed without a text answer. Try another question or model.");
   return text.ToString().Trim();
  }
  static bool ConsumeEvent(string raw, StringBuilder text, Action<string> delta) {
   if(raw == "[DONE]") return false;
   Dictionary<string,object> item; try { item = ChatJson.Parse(raw); } catch { throw new ChatConnectionException("The ChatGPT reply stream could not be read."); }
   string type = ChatJson.Text(item,"type");
   if(type == "response.output_text.delta" || type == "response.refusal.delta") {
    string value = ChatJson.Text(item,"delta"); if(text.Length + value.Length > 20000) throw new ChatConnectionException("The reply is too long for this conversation. Ask for a shorter answer.");
    text.Append(value); if(delta != null) delta(value);
   }
   if(type == "response.failed" || type == "response.incomplete" || type == "error") {
    var response = ChatJson.Object(item,"response"); var error = ChatJson.Object(response,"error") ?? ChatJson.Object(item,"error"); string code = ChatJson.Text(error,"code");
    if(code == "subscription_sharing_usage_limit_exceeded" || code == "subscription_sharing_usage_unavailable" || code == "rate_limit_exceeded" || code == "insufficient_quota") throw new ChatConnectionException("Your ChatGPT plan usage is unavailable or its limit was reached. Check ChatGPT usage settings and try later.");
    throw new ChatConnectionException("ChatGPT could not finish the answer. Try again or choose another model.");
   }
   if(type == "response.completed") {
    var response = ChatJson.Object(item,"response");
    if(ChatJson.Text(response,"status") != "completed") throw new ChatConnectionException("ChatGPT did not finish the answer successfully.");
    if(text.Length == 0) {
     foreach(var output in ChatJson.Items(response,"output").OfType<Dictionary<string,object>>()) foreach(var content in ChatJson.Items(output,"content").OfType<Dictionary<string,object>>()) {
      string value = ChatJson.Text(content,"type") == "output_text" ? ChatJson.Text(content,"text") : ChatJson.Text(content,"type") == "refusal" ? ChatJson.Text(content,"refusal") : "";
      if(text.Length+value.Length>20000) throw new ChatConnectionException("The completed answer exceeded the local size limit."); text.Append(value);
     }
     if(text.Length>0 && delta != null) delta(text.ToString());
    }
    return true;
   }
   return false;
  }
 }
}
