using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;

namespace JarvisDesk {
 [DataContract] public sealed class VoicePreferences {
  [DataMember] public string Provider = "windows";
  [DataMember] public string Region = "";
  [DataMember] public string Voice = "en-US-LunaNeural";
  [DataMember] public string VoiceLocale = "";
  [DataMember] public string DesktopVoice = "";
  [DataMember] public string ProtectedKey = "";
  [DataMember] public int Rate = -30;
  [DataMember] public int Volume = 90;
  [DataMember] public bool WindowsFallback = true;
  static readonly byte[] Entropy = Encoding.UTF8.GetBytes("JarvisDesk.AzureSpeech.v1");
  public string ReadKey() { return String.IsNullOrEmpty(ProtectedKey) ? "" : Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(ProtectedKey), Entropy, DataProtectionScope.CurrentUser)); }
  public void SetKey(string key) { ProtectedKey = String.IsNullOrWhiteSpace(key) ? "" : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(key.Trim()), Entropy, DataProtectionScope.CurrentUser)); }
  public void Validate() {
   if (Provider != "windows" && Provider != "azure") throw new ArgumentException("Choose an available voice provider.");
   if (Rate < -50 || Rate > 25 || Volume < 0 || Volume > 100) throw new ArgumentException("Voice speed or volume is outside the supported range.");
   if (Provider == "azure") {
    if (!Regex.IsMatch(Region ?? "", "^[a-z0-9]{3,32}$")) throw new ArgumentException("Enter your Azure Speech region, for example eastus, without spaces.");
    if (!AzureVoiceCatalog.ValidName(Voice)) throw new ArgumentException("Choose a natural voice from the Azure catalog.");
    if (!String.IsNullOrEmpty(VoiceLocale) && !AzureVoiceCatalog.ValidLocale(VoiceLocale)) throw new ArgumentException("Choose a valid voice language.");
    if (!Regex.IsMatch(ReadKey(), "^[A-Za-z0-9+/=_-]{20,256}$")) throw new ArgumentException("Enter a valid Azure Speech resource key in the private key field.");
   }
  }
  public static VoicePreferences Load(string path) {
   if (!File.Exists(path)) return new VoicePreferences();
   using (var stream = File.OpenRead(path)) return (VoicePreferences)new DataContractJsonSerializer(typeof(VoicePreferences)).ReadObject(stream);
  }
  public void Save(string path) {
   Validate(); string temp = path + ".tmp";
   using (var stream = File.Create(temp)) new DataContractJsonSerializer(typeof(VoicePreferences)).WriteObject(stream, this);
   if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
  }
 }
 public static class NaturalVoice {
  public static string BuildSsml(VoicePreferences settings, string text) {
   if (String.IsNullOrWhiteSpace(text) || text.Length > 1200) throw new ArgumentException("Voice text must contain between 1 and 1,200 characters.");
   var output = new StringBuilder(); var options = new XmlWriterSettings { OmitXmlDeclaration = true };
   using (var xml = XmlWriter.Create(output, options)) {
    xml.WriteStartElement("speak", "http://www.w3.org/2001/10/synthesis"); xml.WriteAttributeString("version", "1.0"); xml.WriteAttributeString("xml", "lang", "http://www.w3.org/XML/1998/namespace", !String.IsNullOrEmpty(settings.VoiceLocale)?settings.VoiceLocale:(settings.Voice=="ja-JP-NanamiNeural" || settings.Voice=="zh-CN-XiaoxiaoNeural"?"en-US":AzureVoiceCatalog.LocaleOf(settings.Voice)));
    xml.WriteStartElement("voice", "http://www.w3.org/2001/10/synthesis"); xml.WriteAttributeString("name", settings.Voice);
    bool conversational = settings.Voice == "en-US-LunaNeural";
    if (conversational) { xml.WriteStartElement("mstts", "express-as", "https://www.w3.org/2001/mstts"); xml.WriteAttributeString("style", "conversation"); }
    bool prosody = !AzureVoiceCatalog.IsHd(settings.Voice);
    if(prosody) { xml.WriteStartElement("prosody", "http://www.w3.org/2001/10/synthesis"); xml.WriteAttributeString("rate", (settings.Rate >= 0 ? "+" : "") + settings.Rate + "%"); xml.WriteAttributeString("volume", settings.Volume.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
    xml.WriteString(text); if(prosody) xml.WriteEndElement(); if (conversational) xml.WriteEndElement(); xml.WriteEndElement(); xml.WriteEndElement();
   }
   return output.ToString();
  }
  public static HttpWebRequest BuildRequest(VoicePreferences settings) {
   settings.Validate();
   ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
   var request = (HttpWebRequest)WebRequest.Create("https://" + settings.Region + ".tts.speech.microsoft.com/cognitiveservices/v1");
   request.Method = "POST"; request.AllowAutoRedirect = false; request.Timeout = 15000; request.ReadWriteTimeout = 15000; request.KeepAlive = false;
   request.ContentType = "application/ssml+xml"; request.Headers["Ocp-Apim-Subscription-Key"] = settings.ReadKey(); request.Headers["X-Microsoft-OutputFormat"] = "riff-24khz-16bit-mono-pcm"; request.UserAgent = "Skynet/2.1";
   return request;
  }
  public static bool IsWave(byte[] bytes) { return bytes != null && bytes.Length >= 44 && Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && Encoding.ASCII.GetString(bytes, 8, 4) == "WAVE"; }
  public static byte[] Synthesize(VoicePreferences settings, string text, CancellationToken cancellation) {
   var request = BuildRequest(settings);
   return FetchWave(request, BuildSsml(settings, text), cancellation);
  }
  internal static byte[] FetchWave(HttpWebRequest request, string ssml, CancellationToken cancellation) {
   try {
    using (cancellation.Register(delegate { request.Abort(); })) {
     cancellation.ThrowIfCancellationRequested(); byte[] body = Encoding.UTF8.GetBytes(ssml); request.ContentLength = body.Length;
     using (var stream = request.GetRequestStream()) stream.Write(body, 0, body.Length);
     using (var response = (HttpWebResponse)request.GetResponse()) using (var source = response.GetResponseStream()) using (var output = new MemoryStream()) {
      if (response.StatusCode != HttpStatusCode.OK) throw new IOException("The online voice service returned an unexpected response.");
      byte[] buffer = new byte[8192]; int count;
      while ((count = source.Read(buffer, 0, buffer.Length)) > 0) { cancellation.ThrowIfCancellationRequested(); if (output.Length + count > 4 * 1024 * 1024) throw new IOException("The online voice response was too large."); output.Write(buffer, 0, count); }
      byte[] wave = output.ToArray(); if (!IsWave(wave)) throw new IOException("The online voice service did not return playable WAV audio."); return wave;
     }
    }
   } catch (WebException ex) {
    cancellation.ThrowIfCancellationRequested();
    var response = ex.Response as HttpWebResponse;
    if (response != null) { int status = (int)response.StatusCode; response.Dispose();
     if (status == 401 || status == 403) throw new IOException("Azure rejected the Speech key or region. Check Voice Settings.");
     if (status == 429) throw new IOException("Azure Speech limit or service capacity reached. Retry shortly or check your Azure resource.");
     if (status == 400) throw new IOException("Azure could not use that voice request. Check the voice and region in Voice Settings.");
     throw new IOException("Azure Speech is unavailable (HTTP " + status + ").");
    }
    throw new IOException("Could not reach the online voice service. Check your internet connection and Speech region.");
   }
  }
 }
}
