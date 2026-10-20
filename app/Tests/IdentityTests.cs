using System;
using System.IO;
using System.Collections.Generic;
using JarvisDesk;
class IdentityTests {
 static int count;
 static void Check(bool ok,string label) { if(!ok) throw new Exception(label);Console.WriteLine("PASS "+label);count++; }
 static int Main(string[] args) {
  try {
   string path=Path.Combine(args[0],"personal-greeting.json");
   Check(PersonalGreeting.Load(path).Name=="","Existing profiles start without guessing the person's name");
   foreach(int hour in new[]{0,4,5,11,12,16,17,23}) {
    string expected=hour>=5 && hour<12?"Good morning":hour>=12 && hour<17?"Good afternoon":"Good evening";
    var time=new DateTime(2026,10,19,hour,59,59);
    Check(PersonalGreeting.Build("Chris",time)==expected+", Chris. Systems online.","Time-dependent greeting at "+hour);
    Check(PersonalGreeting.Build("",time)==expected+". Systems online.","Unnamed greeting has no extra punctuation at "+hour);
   }
   foreach(string name in new[]{"Chris","Anne-Marie","O'Neil","Jos\u00e9","\u674e\u534e",""}) {
    new PersonalGreeting {Name=name}.Save(path);Check(PersonalGreeting.Load(path).Name==name,"Personal name persists across reloads");
    Check(AssistantIdentity.CurrentName=="Assistant" && AssistantIdentity.WakeName=="assistant","Person's name does not rename the assistant or wake phrase");
   }
   Check(PersonalGreeting.Validate("  Chris   Smith  ")=="Chris Smith","Trim and normalize personal name spacing");
   foreach(string name in new[]{"<script>","Chris\"","123",new string('A',41)}) { bool rejected=false;try {new PersonalGreeting {Name=name}.Save(path);}catch(ArgumentException) {rejected=true;}Check(rejected,"Reject unsupported personal name"); }
   File.WriteAllText(path,"not json");Check(PersonalGreeting.Load(path).Name=="","Corrupt greeting settings recover safely");
   Check(Rules.Parse("Assistant open Chrome",true,false)=="chrome" && Rules.Parse("open Chrome",false,false)=="chrome","Named and optional launch commands remain available");
   Check(LocalIntent.Parse("Assistant read my agenda",true,false)=="agenda" && ScreenIntent.Parse("wake up",false)=="wake","Agenda and screen commands remain available");
   string body=ChatGptClient.BuildConversation("test",new List<ChatMessage>{new ChatMessage {Role="user",Content="Hello"}});
   var decoded=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Dictionary<string,object>>(body);
   Check(((string)decoded["instructions"]).Contains("You are Assistant,") && !body.Contains("Chris"),"Chat uses neutral assistant identity without sending the saved person's name");
   Console.WriteLine(count+" personal greeting checks passed.");return 0;
  }catch(Exception ex) {Console.Error.WriteLine(ex);return 1;}
 }
}
