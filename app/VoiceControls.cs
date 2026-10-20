using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Speech.Synthesis;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace JarvisDesk {
 public partial class Desk {
  VoicePreferences voicePreferences = new VoicePreferences();
  readonly CancellationTokenSource voiceCancellation = new CancellationTokenSource();
  readonly object voiceAudioGate = new object();
  readonly Dictionary<string, byte[]> voiceCache = new Dictionary<string, byte[]>();
  SoundPlayer onlinePlayer;
  CancellationTokenSource activeVoiceCancellation;
  int voiceGeneration;
  Prompt desktopPrompt;
  string defaultDesktopVoice;
  Action<string> desktopVoiceCallback;
  string VoiceSettingsPath { get { return System.IO.Path.Combine(dataDir, "voice.json"); } }
  void InitializeVoicePreferences() {
   try { voicePreferences = VoicePreferences.Load(VoiceSettingsPath) ?? new VoicePreferences(); voicePreferences.Validate(); }
   catch { voicePreferences = new VoicePreferences(); Log("Saved voice settings could not be used. Open Voice Settings to configure them."); }
  }
  void UpdateVoiceLabel(string overrideText = null) {
   string name = voicePreferences.Provider == "azure" ? voicePreferences.Voice.Replace("en-US-", "").Replace("en-GB-", "").Replace("Neural", "") + " / Azure natural" : speaker == null ? "Unavailable" : String.IsNullOrWhiteSpace(voicePreferences.DesktopVoice) ? defaultDesktopVoice : voicePreferences.DesktopVoice;
   voiceInfo.Text = "VOICE / " + (overrideText ?? name) + "\nRECOGNIZER / " + (recognizer == null ? "unavailable" : recognizer.RecognizerInfo.Culture.Name);
   voiceInfo.ToolTip = "Open VOICE SETTINGS to choose a natural online voice, adjust its speed, and preview it.";
  }
  void FinishVoice(string error, Action<string> callback) {
   speaking = false; suppressUntil = DateTime.UtcNow.AddMilliseconds(700);
   if (!closing && callback != null) callback(error);
  }
  void SpeakDesktop(VoicePreferences settings, string text, Action<string> callback) {
   if (speaker == null) { FinishVoice("Windows speech output is unavailable.", callback); return; }
   try {
    string desktopName = String.IsNullOrWhiteSpace(settings.DesktopVoice) ? defaultDesktopVoice : settings.DesktopVoice;
    if (!String.IsNullOrWhiteSpace(desktopName)) speaker.SelectVoice(desktopName);
    speaker.Rate = Math.Max(-3, Math.Min(3, (int)Math.Round(settings.Rate / 10.0))); speaker.Volume = settings.Volume;
    desktopVoiceCallback = callback; desktopPrompt = speaker.SpeakAsync(text);
   } catch { FinishVoice("The selected Windows voice could not be played.", callback); }
  }
  void SpeakText(string text, VoicePreferences settings = null, bool preview = false, Action<string> callback = null) {
   if (closing) return;
   if (speaking) { if (callback != null) callback("A voice is already speaking. Wait a moment and try again."); return; }
   settings = settings ?? voicePreferences;
   try { settings.Validate(); } catch (Exception ex) { if (callback != null) callback(ex.Message); else Log("Voice Settings need attention."); return; }
   speaking = true; int generation=++voiceGeneration;
   if (settings.Provider != "azure") { SpeakDesktop(settings, text, callback); return; }
   var playbackCancellation=CancellationTokenSource.CreateLinkedTokenSource(voiceCancellation.Token); activeVoiceCancellation=playbackCancellation;
   var selected = settings;
   System.Threading.Tasks.Task.Run(delegate {
    string failure = null;
    try {
     string cacheKey = selected.Voice + "|" + selected.Rate + "|" + selected.Volume + "|" + text; byte[] wave;
     wave=null; if(!preview) lock(voiceAudioGate) voiceCache.TryGetValue(cacheKey,out wave);
     if (wave==null) {
      wave = NaturalVoice.Synthesize(selected, text, playbackCancellation.Token);
      if (!preview) { lock(voiceAudioGate) { if (voiceCache.Count >= 16) voiceCache.Clear(); voiceCache[cacheKey] = wave; } }
     }
     playbackCancellation.Token.ThrowIfCancellationRequested();
     using (var stream = new MemoryStream(wave)) using (var player = new SoundPlayer(stream)) {
      player.Load();
      lock (voiceAudioGate) { playbackCancellation.Token.ThrowIfCancellationRequested(); onlinePlayer = player; }
      using(playbackCancellation.Token.Register(delegate { try { player.Stop(); } catch { } })) { playbackCancellation.Token.ThrowIfCancellationRequested(); player.PlaySync(); }
      lock (voiceAudioGate) if(Object.ReferenceEquals(onlinePlayer,player)) onlinePlayer = null;
     }
    } catch (OperationCanceledException) { return; }
    catch (Exception ex) { failure = ex is IOException ? ex.Message : "Online voice playback failed. Check your sound output and Voice Settings."; }
    finally { playbackCancellation.Dispose(); }
    if (closing || Dispatcher.HasShutdownStarted) return;
    Dispatcher.BeginInvoke(new Action(delegate {
     if (closing || generation!=voiceGeneration) return;
     activeVoiceCancellation=null;
     if (failure != null) {
      Log(failure);
      if (!preview && selected.WindowsFallback && speaker != null) { UpdateVoiceLabel("Windows fallback / online unavailable"); SpeakDesktop(selected, text, callback); return; }
     } else UpdateVoiceLabel();
     FinishVoice(failure, callback);
    }));
   });
  }
  void CancelOnlineVoice() {
   voiceCancellation.Cancel();
   StopCurrentSpeech();
  }
  void StopCurrentSpeech() {
   thermalVoiceInProgress=false; queuedChatSpeech=null;
   agendaReadUntil=DateTime.MinValue;commuteReadUntil=DateTime.MinValue;
   ++voiceGeneration;
   if(activeVoiceCancellation!=null) { try { activeVoiceCancellation.Cancel(); } catch(ObjectDisposedException) { } activeVoiceCancellation=null; }
   desktopPrompt=null; desktopVoiceCallback=null;
   if(speaker!=null) { try { speaker.SpeakAsyncCancelAll(); } catch { } }
   lock (voiceAudioGate) { if (onlinePlayer != null) { try { onlinePlayer.Stop(); } catch { } onlinePlayer = null; } }
   speaking=false; suppressUntil=DateTime.UtcNow.AddMilliseconds(700);
  }
  public void OpenVoiceSettings() {
   var dialog = new Window { Title = AssistantIdentity.CurrentName + " Voice Settings", Width = 650, Height = 755, MinWidth = 570, MinHeight = 640, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Ink(3, 13, 20), Foreground = cyan, FontFamily = new FontFamily("Segoe UI") };
   dialog.Resources.Add(typeof(Button), Resources[typeof(Button)]);
   var layout = new DockPanel(); dialog.Content = layout;
   var footer = new StackPanel { Margin = new Thickness(26, 8, 26, 18) }; DockPanel.SetDock(footer, Dock.Bottom); layout.Children.Add(footer);
   var form = new StackPanel { Margin = new Thickness(26, 26, 26, 8) }; layout.Children.Add(new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
   form.Children.Add(Label("NATURAL VOICE", 24, cyan));
   form.Children.Add(Label("Choose a voice and preview it before saving. Azure reads confirmation text online; microphone recognition stays on this PC.", 12, muted));
   form.Children.Add(Label("VOICE PROVIDER", 10, cyan));
   var provider = new ComboBox { Margin = new Thickness(0, 0, 0, 13), Padding = new Thickness(6), Foreground = Brushes.Black };
   provider.Items.Add("Azure natural voices / online"); provider.Items.Add("Installed Windows voices / offline"); provider.SelectedIndex = voicePreferences.Provider == "azure" || !File.Exists(VoiceSettingsPath) ? 0 : 1; form.Children.Add(provider);
   form.Children.Add(Label("NATURAL VOICE / ACCENT", 10, cyan));
   var naturalVoice = new ComboBox { Margin = new Thickness(0, 0, 0, 13), Padding = new Thickness(6), Foreground = Brushes.Black };
   string[] ids = { "en-US-LunaNeural", "en-US-JennyNeural", "ja-JP-NanamiNeural", "en-HK-YanNeural", "zh-CN-XiaoxiaoNeural", "en-GB-LibbyNeural", "en-GB-RyanNeural", "en-GB-ThomasNeural", "en-GB-SoniaNeural" };
   foreach (string caption in new string[] { "Luna / American female / conversational", "Jenny / American female", "Nanami / English / Japanese accent / female", "Yan / English (Hong Kong) / female", "Xiaoxiao / English / Mandarin Chinese accent / female", "Libby / British female", "Ryan / British male", "Thomas / British male", "Sonia / British female" }) naturalVoice.Items.Add(caption);
   naturalVoice.SelectedIndex = Math.Max(0, Array.IndexOf(ids, voicePreferences.Voice)); form.Children.Add(naturalVoice);
   form.Children.Add(Label("For a lighter Chinese-influenced English voice, try Yan. Compare Nanami and Xiaoxiao in Preview. Accent strength is fixed by each synthetic voice.",11,muted));
   form.Children.Add(Label("AZURE SPEECH REGION / e.g. eastus", 10, cyan)); var region = new TextBox { Text = voicePreferences.Region, Padding = new Thickness(6), Margin = new Thickness(0, 0, 0, 12) }; form.Children.Add(region);
   form.Children.Add(Label("AZURE SPEECH KEY / saved with Windows user encryption", 10, cyan)); var key = new PasswordBox { Padding = new Thickness(6), Margin = new Thickness(0, 0, 0, 12) }; try { key.Password = voicePreferences.ReadKey(); } catch { } form.Children.Add(key);
   form.Children.Add(Label("An Azure account and Speech resource are required. Usage is billed or limited according to your Azure plan. Enter the key here, not in chat.", 11, muted));
   form.Children.Add(Label("WINDOWS FALLBACK VOICE", 10, cyan));
   var desktop = new ComboBox { Margin = new Thickness(0, 0, 0, 13), Padding = new Thickness(6), Foreground = Brushes.Black };
   if (speaker != null) foreach (var installed in speaker.GetInstalledVoices().Where(v => v.Enabled)) desktop.Items.Add(installed.VoiceInfo.Name);
   int desktopIndex = desktop.Items.IndexOf(voicePreferences.DesktopVoice); desktop.SelectedIndex = desktopIndex >= 0 ? desktopIndex : desktop.Items.Count > 0 ? 0 : -1; form.Children.Add(desktop);
   var rateText = Label("", 11, cyan); form.Children.Add(rateText); var rate = new Slider { Minimum = -50, Maximum = 25, TickFrequency = 5, IsSnapToTickEnabled = true, Value = voicePreferences.Rate, Margin = new Thickness(0, 0, 0, 13) }; form.Children.Add(rate);
   Action updateRate = delegate { rateText.Text = "SPEED / " + (rate.Value >= 0 ? "+" : "") + rate.Value.ToString("0") + "%"; }; rate.ValueChanged += delegate { updateRate(); }; updateRate();
   var volumeText = Label("", 11, cyan); form.Children.Add(volumeText); var volume = new Slider { Minimum = 0, Maximum = 100, TickFrequency = 5, IsSnapToTickEnabled = true, Value = voicePreferences.Volume, Margin = new Thickness(0, 0, 0, 13) }; form.Children.Add(volume);
   Action updateVolume = delegate { volumeText.Text = "VOLUME / " + volume.Value.ToString("0") + "%"; }; volume.ValueChanged += delegate { updateVolume(); }; updateVolume();
   var fallback = new CheckBox { Content = "Use Windows voice if the online voice is unavailable", IsChecked = voicePreferences.WindowsFallback, Foreground = muted, Margin = new Thickness(0, 0, 0, 15) }; form.Children.Add(fallback);
   var status = Label("", 12, cyan); footer.Children.Add(status);
   Func<VoicePreferences> draft = delegate {
    var settings = new VoicePreferences { Provider = provider.SelectedIndex == 0 ? "azure" : "windows", Region = region.Text.Trim().ToLowerInvariant(), Voice = ids[Math.Max(0, naturalVoice.SelectedIndex)], DesktopVoice = desktop.SelectedItem as string ?? "", Rate = (int)rate.Value, Volume = (int)volume.Value, WindowsFallback = fallback.IsChecked == true };
    settings.SetKey(key.Password); settings.Validate(); return settings;
   };
   var buttons = new WrapPanel();
   buttons.Children.Add(Action("PREVIEW", delegate {
    try {
     var settings = draft(); status.Text = "Preparing voice preview…";
     SpeakText(OnlineAnnouncement, settings, true, delegate(string error) { status.Text = error ?? "Preview complete. Save to use this voice for confirmations."; });
    } catch (Exception ex) { status.Text = ex.Message; }
   }));
   buttons.Children.Add(Action("SAVE VOICE", delegate { try { var settings = draft(); settings.Save(VoiceSettingsPath); voicePreferences = settings; UpdateVoiceLabel(); Log("Voice preferences saved."); dialog.Close(); } catch (Exception ex) { status.Text = ex.Message; } }));
   buttons.Children.Add(Action("CANCEL", dialog.Close)); footer.Children.Add(buttons);
   var help = Action("AZURE SETUP HELP", delegate { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "https://learn.microsoft.com/en-us/azure/ai-services/speech-service/get-started-text-to-speech", UseShellExecute = true }); }); footer.Children.Add(help);
   dialog.ShowDialog(); key.Clear();
  }
 }
}
