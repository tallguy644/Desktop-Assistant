using System;
using System.IO;
using System.Collections.Generic;
using JarvisDesk;
class ChatNamingTests {
 static int count;
 static void Check(bool ok,string label) { if(!ok) throw new Exception(label);Console.WriteLine("PASS "+label);count++; }
 static int Main(string[] args) {
  try {
   string path=Path.Combine(args[0],"chat-name.json");
   Check(ChatNamePreferences.Load(path).Name=="Assistant","Missing chat name preserves the current default");
   var messages=new List<ChatMessage>{new ChatMessage {Role="user",Content="Hello"}};
   foreach(string name in new[]{"Nova","Echo Prime","Luna-7","O'Neil","\u5149"}) {
    new ChatNamePreferences {Name=name}.Save(path);Check(ChatNamePreferences.Load(path).Name==name,"Chat name persists");
    var body=ChatGptClient.BuildConversation("test",messages,name);
    var values=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Dictionary<string,object>>(body);
    Check(((string)values["instructions"]).StartsWith("You are "+name+","),"Future requests use the chat identity");
    Check(((string)values["instructions"]).Contains("no action tools") && messages[0].Content=="Hello","Naming preserves tool restrictions and original messages");
    Check(AssistantIdentity.CurrentName=="Assistant" && AssistantIdentity.WakeName=="assistant","Chat name leaves the app and command identity unchanged");
    Check(PersonalGreeting.Build("Chris",new DateTime(2026,10,19,9,0,0))=="Good morning, Chris. Systems online.","Chat name leaves the person's greeting unchanged");
   }
   foreach(string name in new[]{"","  ","<script>","Nova\"",new string('A',25)}) {
    bool rejected=false;try {new ChatNamePreferences {Name=name}.Save(path);}catch(ArgumentException){rejected=true;}
    Check(rejected,"Reject invalid chat name");
    Check(ChatNamePreferences.Load(path).Name=="\u5149","Invalid input leaves the saved name intact");
   }
   Check(ChatNamePreferences.Validate("  Echo   Prime ")=="Echo Prime","Normalize name spacing");
   File.WriteAllText(path,"invalid json");Check(ChatNamePreferences.Load(path).Name=="Assistant","Corrupt settings recover to the default");
   Console.WriteLine(count+" chat naming checks passed.");return 0;
  }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
 }
}
