using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Xml;
using JarvisDesk;

public static class AzureVoiceCatalogTests {
 static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
 static void Reject(Action action,string message){bool failed=false;try{action();}catch{failed=true;}Check(failed,message);}
 static AzureVoiceInfo Voice(string name,string locale,string gender){return new AzureVoiceInfo {ShortName=name,DisplayName=name,Locale=locale,LocaleName=locale,Gender=gender,VoiceType="Neural"};}
 public static void Run(string directory){
  var voices=new List<AzureVoiceInfo> {Voice("en-US-JennyNeural","en-US","Female"),Voice("en-US-Andrew:DragonHDLatestNeural","en-US","Male"),Voice("zh-CN-liaoning-XiaobeiNeural","zh-CN-liaoning","Female"),Voice("iu-Latn-CA-TaqqiqNeural","iu-Latn-CA","Male"),Voice("en-US-JennyNeural","en-US","Female")};
  string json=new JavaScriptSerializer().Serialize(voices);var parsed=AzureVoiceCatalog.Parse(Encoding.UTF8.GetBytes(json));Check(parsed.Count==4,"parse multilingual, dialect and HD voices; remove duplicates");
  Check(AzureVoiceCatalog.Filter(parsed,"Xiaobei","","Female").Count==1 && AzureVoiceCatalog.Filter(parsed,"Andrew","en-US","Female").Count==0,"combine name search, language and gender filters");
  for(int i=0;i<1300;i++)voices.Add(Voice("en-US-Fixture"+i+"Neural","en-US","Female"));Check(AzureVoiceCatalog.Parse(Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(voices))).Count==1304,"keep a large catalog without a hard-coded voice count");
  foreach(string bad in new[]{"https://evil.example","en-US-Test\"/>","en-US-Test<voice>","../test","",null})Check(!AzureVoiceCatalog.ValidName(bad),"reject unsafe voice identifiers");
  Check(AzureVoiceCatalog.LocaleOf("zh-CN-liaoning-XiaobeiNeural")=="zh-CN-liaoning" && AzureVoiceCatalog.LocaleOf("iu-Latn-CA-TaqqiqNeural")=="iu-Latn-CA","derive dialect and script locales correctly");
  var settings=new VoicePreferences {Provider="azure",Region="eastus",Voice="fr-FR-DeniseNeural",VoiceLocale="fr-FR"};settings.SetKey(new string('a',32));settings.Validate();
  var document=new XmlDocument();document.LoadXml(NaturalVoice.BuildSsml(settings,"Hello <world> & friends."));Check(document.DocumentElement.GetAttribute("lang","http://www.w3.org/XML/1998/namespace")=="fr-FR" && document.InnerText=="Hello <world> & friends.","use the selected locale and XML-escape spoken text");
  settings.Voice="en-US-Andrew:DragonHDLatestNeural";settings.VoiceLocale="en-US";settings.Validate();document.LoadXml(NaturalVoice.BuildSsml(settings,"Ready."));Check(document.GetElementsByTagName("prosody").Count==0,"HD voices omit unsupported prosody controls");
  settings.Voice="en-US-JennyNeural";document.LoadXml(NaturalVoice.BuildSsml(settings,"Ready."));Check(document.GetElementsByTagName("prosody").Count==1,"standard natural voices retain speed and volume controls");
  settings.Voice="ja-JP-NanamiNeural";settings.VoiceLocale="";document.LoadXml(NaturalVoice.BuildSsml(settings,"Ready."));Check(document.DocumentElement.GetAttribute("lang","http://www.w3.org/XML/1998/namespace")=="en-US","retain existing Japanese-accent English voice behavior");
  var request=AzureVoiceCatalog.BuildRequest("eastus",new string('a',32));Check(request.Method=="GET" && !request.AllowAutoRedirect && request.RequestUri.AbsoluteUri=="https://eastus.tts.speech.microsoft.com/cognitiveservices/voices/list" && request.RequestUri.Query=="","catalog request pins HTTPS Azure host and never redirects credentials");request.Abort();
  Reject(delegate{AzureVoiceCatalog.BuildRequest("eastus.evil.example",new string('a',32));},"reject region host injection");
  using(var cancel=new CancellationTokenSource()){cancel.Cancel();Reject(delegate{AzureVoiceCatalog.Fetch("eastus",new string('a',32),cancel.Token);},"cancel before contacting Azure");}
  AzureVoiceCatalog.Save(directory,"eastus",parsed);Check(AzureVoiceCatalog.Load(directory,"eastus").Count==4 && AzureVoiceCatalog.Load(directory,"westus").Count==0,"cache public metadata separately for each region");Reject(delegate{AzureVoiceCatalog.Save(directory,"../escape",parsed);},"reject cache path traversal");
  File.WriteAllText(Path.Combine(directory,"azure-voices-eastus.json"),"invalid JSON");Check(AzureVoiceCatalog.Load(directory,"eastus").Count==0,"corrupt cache falls back safely");Reject(delegate{AzureVoiceCatalog.Parse(Encoding.UTF8.GetBytes("[]"));},"empty catalog cannot replace usable voice choices");
  foreach(double speed in new[]{0.0,1400.0,0.0}){var snapshot=new SystemSnapshot();SystemMonitor.ApplyFans(snapshot,new[]{new SensorReading {Name="Radiator",Parent="board",Identifier="/lpc/fan/0",SensorType="Fan",Value=speed}});Check(snapshot.Fans.Count==1 && (speed>0?snapshot.Cooling.Contains("Radiator: 1400 RPM"):!snapshot.Cooling.Contains("Radiator:")),"fan display follows positive RPM while retaining raw readings");}
 }
}
