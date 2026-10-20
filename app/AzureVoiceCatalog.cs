using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace JarvisDesk {
 [DataContract] public sealed class AzureVoiceInfo {
  [DataMember] public string ShortName;
  [DataMember] public string DisplayName;
  [DataMember] public string Locale;
  [DataMember] public string LocaleName;
  [DataMember] public string Gender;
  [DataMember] public string VoiceType;
  [DataMember] public string Status;
  public bool IsHd {get{return AzureVoiceCatalog.IsHd(ShortName);}}
  public override string ToString(){return (String.IsNullOrWhiteSpace(DisplayName)?ShortName:DisplayName)+" / "+(LocaleName??Locale)+" / "+(Gender??"")+(IsHd?" / "+(ShortName.Contains(":")?ShortName.Substring(ShortName.IndexOf(":")+1):"HD"):"")+(Status=="Preview"?" / Preview":"");}
 }
 public static class AzureVoiceCatalog {
  public static bool ValidName(string value){return value!=null && value.Length<=180 && Regex.IsMatch(value,@"^[a-z]{2,3}-(?:[A-Z][a-z]{3}-)?(?:[A-Z]{2}|[0-9]{3})-[A-Za-z0-9][A-Za-z0-9:._-]*$");}
  public static bool ValidLocale(string value){return value!=null && value.Length<=64 && Regex.IsMatch(value,@"^[a-z]{2,3}(?:-[A-Za-z0-9]{2,8}){1,3}$");}
  public static bool IsHd(string name){return (name??"").IndexOf("DragonHD",StringComparison.OrdinalIgnoreCase)>=0 || (name??"").EndsWith("NeuralHD",StringComparison.OrdinalIgnoreCase);}
  public static string LocaleOf(string name){var match=Regex.Match(name??"",@"^([a-z]{2,3}-(?:[A-Z][a-z]{3}-)?(?:[A-Z]{2}|[0-9]{3})(?:-[a-z]{3,8})?)-");return match.Success?match.Groups[1].Value:"en-US";}
  public static void ValidateConnection(string region,string key){if(!Regex.IsMatch(region??"",@"^[a-z0-9]{3,32}$"))throw new ArgumentException("Enter your Azure Speech region, such as eastus.");if(!Regex.IsMatch(key??"",@"^[A-Za-z0-9+/=_-]{20,256}$"))throw new ArgumentException("Enter your Azure Speech key in the private field first.");}
  public static HttpWebRequest BuildRequest(string region,string key){ValidateConnection(region,key);ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;var request=(HttpWebRequest)WebRequest.Create("https://"+region+".tts.speech.microsoft.com/cognitiveservices/voices/list");request.Method="GET";request.AllowAutoRedirect=false;request.Timeout=15000;request.ReadWriteTimeout=15000;request.KeepAlive=false;request.Accept="application/json";request.Headers["Ocp-Apim-Subscription-Key"]=key;return request;}
  public static List<AzureVoiceInfo> Parse(byte[] json){
   if(json==null || json.Length>4*1024*1024)throw new IOException("Azure voice catalog exceeded the supported size.");
   using(var stream=new MemoryStream(json)){var voices=(AzureVoiceInfo[])new DataContractJsonSerializer(typeof(AzureVoiceInfo[])).ReadObject(stream);if(voices==null)throw new IOException("Azure returned no voice catalog.");var result=voices.Where(v=>v!=null && ValidName(v.ShortName) && ValidLocale(v.Locale) && (v.VoiceType=="Neural" || v.ShortName.IndexOf("Neural",StringComparison.OrdinalIgnoreCase)>=0)).GroupBy(v=>v.ShortName,StringComparer.Ordinal).Select(g=>g.First()).OrderBy(v=>v.LocaleName??v.Locale).ThenBy(v=>v.DisplayName??v.ShortName).ToList();if(result.Count==0)throw new IOException("Azure returned no usable natural voices. Your previous catalog was kept.");return result;}
  }
  public static List<AzureVoiceInfo> Fetch(string region,string key,CancellationToken cancellation){
   var request=BuildRequest(region,key);
   try{using(cancellation.Register(delegate{request.Abort();})){cancellation.ThrowIfCancellationRequested();using(var response=(HttpWebResponse)request.GetResponse())using(var input=response.GetResponseStream())using(var output=new MemoryStream()){if(response.StatusCode!=HttpStatusCode.OK)throw new IOException("Azure returned an unexpected voice catalog response.");var buffer=new byte[8192];int count;while((count=input.Read(buffer,0,buffer.Length))>0){cancellation.ThrowIfCancellationRequested();if(output.Length+count>4*1024*1024)throw new IOException("Azure voice catalog exceeded the supported size.");output.Write(buffer,0,count);}return Parse(output.ToArray());}}}
   catch(WebException ex){cancellation.ThrowIfCancellationRequested();var response=ex.Response as HttpWebResponse;if(response!=null){int code=(int)response.StatusCode;response.Dispose();if(code==401 || code==403)throw new IOException("Azure rejected the Speech key or region. Check those private fields.");if(code==429)throw new IOException("Azure is limiting voice catalog requests. Try Refresh later.");throw new IOException("Azure voice catalog is unavailable (HTTP "+code+").");}throw new IOException("Could not reach Azure. Your existing voices are still available.");}
  }
  static string CachePath(string folder,string region){if(!Regex.IsMatch(region??"",@"^[a-z0-9]{3,32}$"))throw new ArgumentException("Invalid Speech region.");return Path.Combine(folder,"azure-voices-"+region+".json");}
  public static List<AzureVoiceInfo> Load(string folder,string region){try{string path=CachePath(folder,region);var info=new FileInfo(path);if(info.Length>4*1024*1024)return new List<AzureVoiceInfo>();return Parse(File.ReadAllBytes(path));}catch{return new List<AzureVoiceInfo>();}}
  public static void Save(string folder,string region,List<AzureVoiceInfo> voices){string path=CachePath(folder,region);Directory.CreateDirectory(folder);string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";using(var file=File.Create(temp))new DataContractJsonSerializer(typeof(AzureVoiceInfo[])).WriteObject(file,voices.ToArray());if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
  public static List<AzureVoiceInfo> Filter(IEnumerable<AzureVoiceInfo> voices,string query,string locale,string gender){string[] words=(query??"").Trim().Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);return voices.Where(v=>(String.IsNullOrEmpty(locale)||v.Locale==locale) && (String.IsNullOrEmpty(gender)||v.Gender==gender) && words.All(word=>(v.ToString()+" "+v.ShortName).IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0)).ToList();}
  public static List<AzureVoiceInfo> Starter(string current){var names=new[]{"en-US-LunaNeural","en-US-JennyNeural","ja-JP-NanamiNeural","en-HK-YanNeural","zh-CN-XiaoxiaoNeural","en-GB-LibbyNeural","en-GB-RyanNeural","en-GB-ThomasNeural","en-GB-SoniaNeural"}.ToList();if(ValidName(current) && !names.Contains(current))names.Add(current);return names.Select(n=>new AzureVoiceInfo {ShortName=n,DisplayName=n.Substring(LocaleOf(n).Length+1).Replace("Neural",""),Locale=LocaleOf(n),LocaleName=LocaleOf(n),VoiceType="Neural",Gender=""}).ToList();}
 }
}
