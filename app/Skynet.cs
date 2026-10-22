using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

[assembly: System.Reflection.AssemblyTitle("Syra")]
[assembly: System.Reflection.AssemblyProduct("Syra")]
[assembly: System.Reflection.AssemblyDescription("Customizable desktop voice assistant")]

namespace JarvisDesk {
 [DataContract] public class AppMap {
  [DataMember] public string Name { get; set; }
  [DataMember] public string Path { get; set; }
  public override string ToString() { return Name.ToUpperInvariant() + "   /   " + System.IO.Path.GetFileNameWithoutExtension(Path); }
 }
 public static class Rules {
  public static string Normalize(string s) { return Regex.Replace((s ?? "").Trim().ToLowerInvariant(), @"\s+", " "); }
  public static bool ValidName(string s) { return Regex.IsMatch(s ?? "", @"^[a-z0-9][a-z0-9 ]{0,49}$"); }
  public static bool ValidPath(string path) {
   if (String.IsNullOrWhiteSpace(path) || !System.IO.Path.IsPathRooted(path) || path.StartsWith(@"\\")) return false;
   string ext = System.IO.Path.GetExtension(path);
   return (ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) || ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase)) && File.Exists(path);
  }
  public static string Parse(string text, bool wakeRequired, bool ptt) {
   string value = AssistantIdentity.Normalize(text); bool wake = AssistantIdentity.StripWake(ref value);
   
   if (wakeRequired && !ptt && !wake) return null;
   return value.StartsWith("open ") ? value.Substring(5) : null;
  }
  public static void WriteMaps(string path, List<AppMap> maps) {
   string temp = path + ".tmp";
   using (var stream = File.Create(temp)) new DataContractJsonSerializer(typeof(List<AppMap>)).WriteObject(stream, maps);
   if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
  }
  public static List<AppMap> ReadMaps(string path) {
   using (var stream = File.OpenRead(path)) return (List<AppMap>)new DataContractJsonSerializer(typeof(List<AppMap>)).ReadObject(stream);
  }
 }
 public partial class Desk : Window {
  bool announcedOnline;
  Brush cyan;
  Brush muted;
  readonly string dataDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JarvisDesk");
  List<AppMap> maps = new List<AppMap>();
  SpeechRecognitionEngine recognizer; SpeechSynthesizer speaker;
  TextBlock state, subtitle, heard, voiceInfo; TextBox nameBox, pathBox, log;
  ListBox appList; Button listenButton;
  bool continuous, speaking, closing; DateTime pttUntil = DateTime.MinValue, suppressUntil = DateTime.MinValue, lastLaunch = DateTime.MinValue;
  DispatcherTimer timer; string configNotice;
  readonly SystemMonitor systemMonitor = new SystemMonitor();
  TextBlock cpuLoad, gpuLoad, cpuTemp, gpuTemp, ramStatus, sensorState, sensorTime, clock, ramDetails;
  TextBox boardTemps, coolingSpeeds, sensorDetails; Polyline cpuTrend, gpuTrend; Rectangle cpuBar, gpuBar, ramBar; Border appDrawer, sensorDrawer;
  bool telemetryBusy; DateTime lastTelemetry = DateTime.MinValue, telemetryStarted;
  string ConfigPath { get { return System.IO.Path.Combine(dataDir, "apps.json"); } }
  public Desk() : this(null) { }
  public void AnnounceOnline() {
   if (announcedOnline || closing) return;
   announcedOnline = true;
   string greeting=OnlineAnnouncement;
   SetState("SYSTEMS ONLINE", greeting);
   SpeakText(greeting);
  }
  public Desk(string settingsDirectory) {
   if (settingsDirectory != null) dataDir = settingsDirectory;
   InitializeAppearance();
   personalGreeting=PersonalGreeting.Load(PersonalGreetingPath);
   chatNamePreferences=ChatNamePreferences.Load(ChatNamePath);
   Title = AssistantIdentity.CurrentName; Width = 1366; Height = 768; MinWidth = 1080; MinHeight = 608;
   string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Syra.ico");
   if (File.Exists(iconPath)) { using (var iconStream = File.OpenRead(iconPath)) Icon = BitmapDecoder.Create(iconStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames.OrderByDescending(frame => frame.PixelWidth).First(); }
   WindowStartupLocation = WindowStartupLocation.CenterScreen;
   Background = new SolidColorBrush(Color.FromRgb(6, 13, 24)); Foreground = Brushes.White; FontFamily = new FontFamily("Segoe UI");
   Loaded+=delegate{FitStartupWindow();};
   BuildUI(); LoadMaps(); RefreshList(); InitVoice(); InitializeChat(); InitializeCalendar(); InitializeTemperatureAlerts(); InitializeCommute(); InitializeScreenControl(); InitializeCompanion(); MigrateLegacyStartup();
   timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
   timer.Tick += delegate { UpdateReferenceClock(); CheckQuestionTimeout(); CheckCalendar(); CheckThermalAlerts(); CheckCommuteReminders(); UpdateNextUp(); CheckAgendaNarration(); CheckScreenControl();
    if (!continuous && pttUntil != DateTime.MinValue && DateTime.UtcNow > pttUntil) { pttUntil = DateTime.MinValue; StopRecognition(); SetState("STANDBY", "Push-to-talk window ended. Ready when you are."); }
    if (telemetryBusy && (DateTime.UtcNow - telemetryStarted).TotalSeconds > 12) ShowSystem(new SystemSnapshot { Status = "Sensor query timed out. Readings unavailable until the feed recovers." });
    if (!telemetryBusy && (DateTime.UtcNow - lastTelemetry).TotalSeconds >= 3) RefreshSystem();
   };
   timer.Start();
   RefreshSystem();
   KeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.F8) { PushToTalk(); e.Handled = true; } };
   Closing += delegate { closing = true; CloseChat(); CancelOnlineVoice(); timer.Stop(); if (recognizer != null) { recognizer.RecognizeAsyncCancel(); recognizer.Dispose(); } if (speaker != null) { speaker.SpeakAsyncCancelAll(); speaker.Dispose(); } };
   if (configNotice != null) Log(configNotice);
  }
  Brush Ink(byte r, byte g, byte b) { return themePalette.Brush(Color.FromRgb(r,g,b),r>g*1.3 && r>b*1.3?ThemeRole.Fixed:ThemeRole.Accent); }
  TextBlock Label(string text, double size, Brush color) { return new TextBlock { Text = text, FontSize = size, Foreground = Object.ReferenceEquals(color,cyan)?Ink(181,224,255):color, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Segoe UI") }; }
  Button Action(string title, Action action) {
   var button = new Button { Content = title, Width = Math.Max(48, title.Length * 7 + 30), HorizontalAlignment = HorizontalAlignment.Left, Foreground = cyan, Background = Ink(8, 29, 40), BorderBrush = Ink(29, 100, 120), BorderThickness = new Thickness(1), Padding = new Thickness(15, 11, 15, 11), Margin = new Thickness(0, 0, 8, 10), Cursor = Cursors.Hand, FontSize = 12, FontFamily = new FontFamily("Consolas") };
   button.Click += delegate { try { action(); } catch (Exception ex) { Log(ex.Message); SetState("ATTENTION", ex.Message); } }; return button;
  }
  Border PanelBox(UIElement child) { return new Border { BorderBrush = Ink(29, 89, 108), BorderThickness = new Thickness(1), Background = Ink(4, 15, 24), Padding = new Thickness(20), Child = child }; }
  void Place(Canvas canvas, UIElement element, double x, double y, double width, double height) { var view = element as FrameworkElement; if (view != null) { view.Width = width; view.Height = height; } Canvas.SetLeft(element, x); Canvas.SetTop(element, y); canvas.Children.Add(element); }
  void Wire(Canvas canvas, double x1, double y1, double x2, double y2, Brush brush, double thickness) { canvas.Children.Add(new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = brush, StrokeThickness = thickness, IsHitTestVisible = false }); }
  Grid AngularPanel(string title, UIElement content, double width, double height, Brush accent) {
   var grid = new Grid { Width = width, Height = height };
   grid.Children.Add(new Polygon { Points = new PointCollection { new Point(0, 18), new Point(18, 0), new Point(width - 42, 0), new Point(width, 28), new Point(width, height - 18), new Point(width - 18, height), new Point(0, height) }, Fill = new LinearGradientBrush(Color.FromRgb(7, 24, 35), Color.FromRgb(3, 12, 22), 90), Stroke = Ink(27, 81, 99), StrokeThickness = 1 });
   var edge = new Canvas { IsHitTestVisible = false }; Wire(edge, 18, 0, 78, 0, accent, 3); Wire(edge, 0, height - 42, 0, height - 10, accent, 2); Wire(edge, width - 70, height, width - 18, height, accent, 2); AddPanelDetails(edge, width, height, accent); grid.Children.Add(edge);
   var stack = new DockPanel { Margin = new Thickness(20, 17, 20, 16) };
   var heading = Label(title, 12, accent); DockPanel.SetDock(heading, Dock.Top); stack.Children.Add(heading); stack.Children.Add(content); grid.Children.Add(stack); return grid;
  }
  Ellipse Ring(double diameter, Brush brush, double thickness, DoubleCollection dash, double seconds) {
   var ring = new Ellipse { Width = diameter, Height = diameter, Stroke = brush, StrokeThickness = thickness, StrokeDashArray = dash, RenderTransformOrigin = new Point(0.5, 0.5) };
   if (seconds != 0) Spin(ring,seconds);
   return ring;
  }
  void BuildUI() { BuildReferenceHud(); }
  void CopyDiagnostics() { Clipboard.SetText(AssistantIdentity.Upper + " / Cinematic HUD / Fahrenheit\nCPU: " + cpuTemp.ToolTip + "\nGPU: " + gpuTemp.ToolTip + "\n" + sensorState.ToolTip + "\n" + sensorTime.Text + "\n" + sensorDetails.Text); Log("Sensor diagnostics copied."); }
  TextBox SensorText(double height) { return new TextBox { Height = height, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Brushes.Transparent, Foreground = muted, BorderThickness = new Thickness(0), FontSize = 12, FontFamily = new FontFamily("Consolas"), Margin = new Thickness(0, 0, 0, 8) }; }
  readonly List<double> cpuHistory = new List<double>(), gpuHistory = new List<double>();
  void PlotTemperature(Polyline line, List<double> history, string text) {
   var match = Regex.Match(text, @"[-+]?\d+(?:\.\d+)?"); double value;
   if (!match.Success || !Double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) { history.Clear(); line.Points.Clear(); return; }
   history.Add(value); if (history.Count > 60) history.RemoveAt(0);
   double min = history.Min() - 2, max = history.Max() + 2; var points = new PointCollection();
   for (int i = 0; i < history.Count; i++) points.Add(new Point(4 + i * 250.0 / Math.Max(1, history.Count - 1), 40 - (history[i] - min) / (max - min) * 34));
   line.Points = points;
  }

  UIElement BuildAppPanel() {
   var right = new Grid(); right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); right.RowDefinitions.Add(new RowDefinition()); right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
   right.Children.Add(Label("AUTHORIZED APPLICATIONS", 14, cyan));
   appList = new ListBox { Background = Brushes.Transparent, Foreground = Brushes.White, BorderThickness = new Thickness(0), FontSize = 13, Margin = new Thickness(0, 8, 0, 14) }; Grid.SetRow(appList, 1); right.Children.Add(appList);
   appList.SelectionChanged += delegate { var map = appList.SelectedItem as AppMap; if (map != null) { nameBox.Text = map.Name; pathBox.Text = map.Path; } };
   var edit = new StackPanel(); Grid.SetRow(edit, 2); right.Children.Add(edit);
   edit.Children.Add(Label("SPOKEN NAME", 10, muted)); nameBox = new TextBox { Background = Ink(7, 16, 28), Foreground = Brushes.White, CaretBrush = cyan, BorderBrush = muted, Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(7) }; edit.Children.Add(nameBox);
   edit.Children.Add(Label("TRUSTED EXECUTABLE OR SHORTCUT", 10, muted)); pathBox = new TextBox { Background = Ink(7, 16, 28), Foreground = Brushes.White, CaretBrush = cyan, BorderBrush = muted, Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(7) }; edit.Children.Add(pathBox);
   var row = new WrapPanel(); row.Children.Add(Action("BROWSE", Browse)); row.Children.Add(Action("SAVE MAPPING", SaveMapping)); row.Children.Add(Action("OPEN SELECTED", delegate { var map = appList.SelectedItem as AppMap; if (map != null) Launch(map); })); row.Children.Add(Action("REMOVE", RemoveMapping)); edit.Children.Add(row);
   return PanelBox(right);
  }
  void RefreshSystem() {
   telemetryBusy = true; telemetryStarted = DateTime.UtcNow;
   System.Threading.Tasks.Task.Run(delegate {
    SystemSnapshot snapshot;
    try { snapshot = systemMonitor.Read(); } catch (Exception ex) { snapshot = new SystemSnapshot { Status = "System status unavailable: " + ex.Message, ReadAt = DateTime.UtcNow }; }
    if (closing || Dispatcher.HasShutdownStarted) return;
    Dispatcher.BeginInvoke(new Action(delegate { if (closing) return; telemetryBusy = false; lastTelemetry = DateTime.UtcNow; ShowSystem(snapshot); }));
   });
  }
  void ShowSystem(SystemSnapshot snapshot) {
   companionSnapshot = snapshot;
   cpuLoad.Text = (snapshot.CpuUsage.HasValue ? snapshot.CpuUsage.Value.ToString("0") + "%" : "—"); cpuBar.Width = snapshot.CpuUsage.HasValue ? Math.Max(0, Math.Min(260, snapshot.CpuUsage.Value * 2.6)) : 0;
   gpuLoad.Text = (snapshot.GpuUsage.HasValue ? snapshot.GpuUsage.Value.ToString("0") + "%" : "—"); gpuLoad.ToolTip = snapshot.GpuUsageDetail; gpuBar.ToolTip = snapshot.GpuUsageDetail;
   gpuBar.Width = snapshot.GpuUsage.HasValue ? Math.Max(0, Math.Min(260, snapshot.GpuUsage.Value * 2.6)) : 0;
   string cpu = SystemMonitor.FahrenheitDisplay(snapshot.CpuTemperature), gpu = SystemMonitor.FahrenheitDisplay(snapshot.GpuTemperature);
   var cpuValue = Regex.Match(cpu, @"[-+]?\d+(?:\.\d+)? °F"); var gpuValue = Regex.Match(gpu, @"[-+]?\d+(?:\.\d+)? °F");
   cpuTemp.Text = cpuValue.Success ? cpuValue.Value : "—"; gpuTemp.Text = gpuValue.Success ? gpuValue.Value : "—";
   cpuTemp.ToolTip = cpu; gpuTemp.ToolTip = gpu;
   ramStatus.Text = (snapshot.RamUsage.HasValue ? snapshot.RamUsage.Value.ToString("0") + "%" : "—"); ramDetails.Text = snapshot.Memory; ramBar.Width = snapshot.RamUsage.HasValue ? Math.Max(0, Math.Min(260, snapshot.RamUsage.Value * 2.6)) : 0;
   UpdateDial(cpuDialArc, snapshot.CpuUsage, 138); UpdateDial(ramDialArc, snapshot.RamUsage, 138); UpdateDial(gpuDialArc, snapshot.GpuUsage, 158);
   PlotTemperature(cpuTrend, cpuHistory, cpuTemp.Text); PlotTemperature(gpuTrend, gpuHistory, gpuTemp.Text);
   boardTemps.Text = Regex.Replace(SystemMonitor.FahrenheitDisplay(snapshot.Board), @"\s*\[[^\]]+\]", ""); sensorDetails.Text = SystemMonitor.FahrenheitDisplay(snapshot.Details);
   coolingSpeeds.Text = snapshot.Cooling; coolingSpeeds.ToolTip = snapshot.CoolingDetail;
   sensorState.Text = snapshot.Fans.Count > 0 || snapshot.GpuUsage.HasValue || snapshot.Status.StartsWith("Sensor feed connected") ? "SENSOR FEED CONNECTED" : "SENSOR FEED UNAVAILABLE"; sensorState.ToolTip = snapshot.Status;
   sensorTime.Text = snapshot.ReadAt == default(DateTime) ? "No current sample" : "READ / " + snapshot.ReadAt.ToLocalTime().ToString("HH:mm:ss") + " · 3 SEC CYCLE";
   ObserveTemperatures(snapshot);
  }
  void Log(string message) { log.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + message + Environment.NewLine); if (log.Text.Length > 14000) log.Text = log.Text.Substring(log.Text.Length - 10000); log.ScrollToEnd(); }
  void SetState(string title, string message) { state.Text = title; subtitle.Text = message; SetCorePulse(title == "LISTENING" || title == "SPEAK NOW" || title == "ASK " + AssistantIdentity.Upper); }
  void LoadMaps() {
   Directory.CreateDirectory(dataDir);
   if (File.Exists(ConfigPath)) {
    try {
     var loaded = Rules.ReadMaps(ConfigPath);
     if (loaded == null || loaded.Any(m => m == null || !Rules.ValidName(m.Name) || String.IsNullOrWhiteSpace(m.Path)) || loaded.GroupBy(m => m.Name).Any(g => g.Count() > 1)) throw new InvalidDataException("Invalid mappings");
     maps = loaded; return;
    } catch { string backup = ConfigPath + ".invalid-" + DateTime.Now.ToString("yyyyMMddHHmmssfff"); File.Copy(ConfigPath, backup); configNotice = "Invalid settings backed up to " + backup; }
   }
   AddDefault("notepad", System.IO.Path.Combine(Environment.SystemDirectory, "notepad.exe"));
   AddDefault("calculator", System.IO.Path.Combine(Environment.SystemDirectory, "calc.exe"));
   foreach (string baseDir in new string[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) }) AddDefault("chrome", System.IO.Path.Combine(baseDir, @"Google\Chrome\Application\chrome.exe"));
   foreach (string folder in new string[] { Environment.GetFolderPath(Environment.SpecialFolder.Programs), Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms) }) Discover(folder);
   Rules.WriteMaps(ConfigPath, maps);
  }
  void AddDefault(string name, string path) { if (Rules.ValidName(name) && Rules.ValidPath(path) && !maps.Any(m => m.Name == name)) maps.Add(new AppMap { Name = name, Path = path }); }
  void Discover(string folder) {
   try {
    foreach (string file in Directory.GetFiles(folder, "*.lnk")) {
     string name = Rules.Normalize(System.IO.Path.GetFileNameWithoutExtension(file));
     if (name.Contains("uninstall") || name.Contains("jarvis") || name.Contains("skynet")) continue;
     if (name == "google chrome") name = "chrome";
     AddDefault(name, file);
    }
    foreach (string sub in Directory.GetDirectories(folder)) { if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) == 0) Discover(sub); }
   } catch (UnauthorizedAccessException) { } catch (IOException) { }
  }
  void RefreshList() { appList.ItemsSource = null; appList.ItemsSource = maps.OrderBy(m => m.Name).ToList(); }
  void Browse() { var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Apps and shortcuts|*.exe;*.lnk", Title = "Choose a trusted application", DereferenceLinks = false }; if (dialog.ShowDialog() == true) pathBox.Text = dialog.FileName; }
  void SaveMapping() {
   string name = Rules.Normalize(nameBox.Text), path = pathBox.Text.Trim();
   if (!Rules.ValidName(name)) throw new Exception("Use a name of 1–50 letters, numbers, and spaces.");
   if (!Rules.ValidPath(path)) throw new Exception("Choose an existing local .exe or trusted .lnk file. Arguments and URLs are not supported.");
   var updated = maps.Where(m => m.Name != name).ToList(); updated.Add(new AppMap { Name = name, Path = path });
   Rules.WriteMaps(ConfigPath, updated); maps = updated; RefreshList(); UpdateGrammar(); Log("Saved: " + name);
  }
  void RemoveMapping() { var selected = appList.SelectedItem as AppMap; if (selected == null) return; var updated = maps.Where(m => m != selected).ToList(); Rules.WriteMaps(ConfigPath, updated); maps = updated; RefreshList(); UpdateGrammar(); Log("Removed: " + selected.Name); }
  void InitVoice() {
   InitializeVoicePreferences();
   try {
    speaker = new SpeechSynthesizer();
    var voices = speaker.GetInstalledVoices().Where(v => v.Enabled && v.VoiceInfo.Culture.TwoLetterISOLanguageName == "en").ToList();
    var voice = voices.FirstOrDefault(v => v.VoiceInfo.Culture.Name == "en-GB") ?? voices.FirstOrDefault();
    if (voice != null) speaker.SelectVoice(voice.VoiceInfo.Name);
    defaultDesktopVoice = speaker.Voice.Name;
    speaker.SpeakCompleted += delegate(object sender, SpeakCompletedEventArgs e) { if (!closing) Dispatcher.BeginInvoke(new Action(delegate { if(e.Prompt != desktopPrompt) return; desktopPrompt = null; var callback = desktopVoiceCallback; desktopVoiceCallback = null; FinishVoice(e.Error == null ? null : "Windows voice playback failed.", callback); })); };
    UpdateVoiceLabel();
   } catch (Exception ex) { Log("Speech output unavailable: " + ex.Message); speaker = null; }
   try {
    var info = SpeechRecognitionEngine.InstalledRecognizers().FirstOrDefault(r => r.Culture.TwoLetterISOLanguageName == "en");
    if (info == null) { Log("No English speech recognizer. Install English desktop speech support; manual launch is available."); return; }
    recognizer = new SpeechRecognitionEngine(info);
    recognizer.SpeechRecognized += delegate(object sender, SpeechRecognizedEventArgs e) {
     string text = e.Result.Text; float confidence = e.Result.Confidence; bool question=e.Result.Grammar != null && e.Result.Grammar.Name=="skynet-question";
     Dispatcher.BeginInvoke(new Action(delegate { if(question) OnQuestionRecognized(text,confidence); else OnRecognized(text, confidence); }));
    };
    recognizer.RecognizeCompleted += delegate(object sender, RecognizeCompletedEventArgs e) { if (!closing) Dispatcher.BeginInvoke(new Action(delegate {
     running = false; stopping = false;
     if (e.Error != null) { if(questionListening) grammarDirty=true; questionListening=false; resumeLaunchListening=false; questionUntil=DateTime.MinValue; if(chatStatus!=null) chatStatus.Text="Microphone stopped. Try again or type your question."; continuous = false; pttUntil = DateTime.MinValue; listenButton.Content = "LISTEN"; SetState("MIC ERROR", e.Error.Message); Log(e.Error.Message); if(chatClient!=null) UpdateChatAccount(); }
     try { if (grammarDirty) UpdateGrammar(); else if (continuous || questionListening || DateTime.UtcNow < pttUntil) StartRecognition(); }
     catch (Exception ex) { continuous = false; pttUntil = DateTime.MinValue; listenButton.Content = "LISTEN"; SetState("VOICE ERROR", ex.Message); Log(ex.Message); }
    })); };
    UpdateGrammar(); UpdateVoiceLabel(); Log("Ready. Listening is off. Choose LISTEN or PUSH TO TALK.");
   } catch (Exception ex) { Log("Recognition unavailable: " + ex.Message); if (recognizer != null) recognizer.Dispose(); recognizer = null; }
  }
  void UpdateGrammar() {
   if (recognizer == null) return;
   grammarDirty = true;
   if (running) { StopRecognition(); return; }
   bool restart = continuous || DateTime.UtcNow < pttUntil;
   recognizer.UnloadAllGrammars(); grammarDirty = false;
   if(questionListening) { var dictation=new DictationGrammar { Name="skynet-question" }; recognizer.LoadGrammar(dictation); if(!StartRecognition()) { questionListening=false; questionUntil=DateTime.MinValue; chatStatus.Text="Could not start question dictation. Check microphone access or type instead."; continuous=resumeLaunchListening; resumeLaunchListening=false; UpdateGrammar(); UpdateChatAccount(); } return; }
   {
    var commands = new List<string>(); foreach (var map in maps) { commands.Add("open " + map.Name); commands.Add(AssistantIdentity.WakeName + " open " + map.Name); }
    foreach(var phrase in LocalIntent.Phrases) { commands.Add(phrase); commands.Add(AssistantIdentity.WakeName+" "+phrase); }
    commands.AddRange(ScreenIntent.Phrases);
    var builder = new GrammarBuilder { Culture = recognizer.RecognizerInfo.Culture }; builder.Append(new Choices(commands.ToArray())); recognizer.LoadGrammar(new Grammar(builder) { Name="skynet-launch" });
    if (restart) StartRecognition();
   }
  }
  bool running, stopping, grammarDirty;
  bool StartRecognition() {
   if (recognizer == null) { SetState("VOICE UNAVAILABLE", "Install English desktop speech support. Manual launch is ready."); return false; }
   if (running) return true;
   try { recognizer.SetInputToDefaultAudioDevice(); recognizer.RecognizeAsync(RecognizeMode.Multiple); running = true; return true; }
   catch (Exception ex) { Log("Microphone: " + ex.Message); SetState("MIC UNAVAILABLE", "Check Windows microphone permissions and your default input device."); return false; }
  }
  void StopRecognition() { if (recognizer != null && running && !stopping) { stopping = true; recognizer.RecognizeAsyncCancel(); } }
  void ToggleListen() {
   if(questionListening) EndQuestionDictation();
   if (continuous) { continuous = false; pttUntil = DateTime.MinValue; StopRecognition(); listenButton.Content = "LISTEN"; SetState("STANDBY", "Microphone listening is off."); }
   else if (StartRecognition()) { continuous = true; listenButton.Content = "STOP LISTENING"; SetState("LISTENING","Say “open” and an app name, “wake up,” or “screens off.”"); }
   RefreshScreenControl();
  }
  void PushToTalk() { if(questionListening) EndQuestionDictation(); if (StartRecognition()) { pttUntil = DateTime.UtcNow.AddSeconds(8); SetState("SPEAK NOW", "Eight seconds · say “open” and a saved app name."); } }
  void OnRecognized(string text, float confidence) {
   if (closing || questionListening || (!continuous && DateTime.UtcNow >= pttUntil)) return;
   if(screenPreferences.Enabled && !WindowsScreenPower.Unlocked) return;
   string screenIntent=ScreenIntent.Parse(text,false);
   if(screenIntent!=null) {
    if(confidence<0.75f || (speaking && screenIntent!="wake") || (DateTime.UtcNow-lastScreenCommand).TotalSeconds<1) return;
    lastScreenCommand=DateTime.UtcNow;heard.Text="HEARD / “"+text+"”";ScreenCommand(screenIntent,true);return;
   }
   if(speaking || chatBusy || DateTime.UtcNow<suppressUntil) return;
   if (confidence < 0.62f) { Log("Unclear speech ignored. Try again."); return; }
   string intent=LocalIntent.Parse(text,false,DateTime.UtcNow<pttUntil);
   if(intent!=null) {
    if((DateTime.UtcNow-lastLaunch).TotalSeconds<2) return;lastLaunch=DateTime.UtcNow;heard.Text="HEARD / “"+text+"”";
    TouchScreenActivity();
    bool usedPtt=pttUntil!=DateTime.MinValue;pttUntil=DateTime.MinValue;if(usedPtt && !continuous) StopRecognition();
    if(intent=="agenda") ReadAgenda();else if(intent=="commute") ReadCommute();else CheckMaps(true);return;
   }
   string name = Rules.Parse(text, false, DateTime.UtcNow < pttUntil);
   if (name == null) return;
   var map = maps.FirstOrDefault(m => m.Name == name); if (map == null) return;
   TouchScreenActivity();
   if ((DateTime.UtcNow - lastLaunch).TotalSeconds < 2) return;
   heard.Text = "HEARD / “" + text + "”";
   bool wasPtt = pttUntil != DateTime.MinValue; pttUntil = DateTime.MinValue;
   if (wasPtt && !continuous) StopRecognition();
   try { Launch(map); } catch (Exception ex) { Log(ex.Message); SetState("LAUNCH FAILED", ex.Message); }
  }
  void Launch(AppMap map) {
   if (!Rules.ValidPath(map.Path)) throw new Exception("App target is missing or invalid. Update the mapping for " + map.Name + ".");
   Process.Start(new ProcessStartInfo { FileName = map.Path, UseShellExecute = true }); lastLaunch = DateTime.UtcNow;
   Log("Opened " + map.Name); SetState(continuous ? "LISTENING" : "STANDBY", "Opened " + map.Name + ".");
   SpeakText("Opening " + map.Name + ".");
  }
 }
 static class Program {
  [STAThread] static int Main(string[] args) {
   if(args.Length>0 && args[0]=="--taskbar-guard")return DesktopTaskbar.RunGuard(args);
   if (args.Contains("--self-test")) return SelfTest(args.Length > 1 ? args[1] : System.IO.Path.GetTempPath());
   bool first; using (var mutex = new Mutex(true, "Local\\JarvisDesk.V1", out first)) {
    if (!first) { if(DesktopCommands.Send(args)) return 0; MessageBox.Show(AssistantIdentity.CurrentName + " is already running."); return 0; }
    try { var app = new Application(); var desk = new Desk(); desk.Loaded += delegate { if(desk.NeedsNameSetup) desk.OpenNameSettings(); desk.AnnounceOnline(); if (args.Contains("--voice-settings")) desk.OpenVoiceSettings(); if(args.Contains("--chat") || args.Contains("--connect-chat")) desk.OpenChatPanel(); if(args.Contains("--connect-chat")) desk.ConnectChat();desk.StartDesktopIntegration(args); }; app.Run(desk); }
    catch (Exception ex) { MessageBox.Show(ex.Message, AssistantIdentity.CurrentName + " startup error"); return 1; }
   }
   return 0;
  }
  static int SelfTest(string testDirectory) {
   var results = new List<string>(); string scratch = System.IO.Path.Combine(testDirectory, "JarvisDesk-test-" + Guid.NewGuid().ToString("N") + ".json");
   try {
    Action<bool, string> check = delegate(bool passed, string label) { if (!passed) throw new Exception(label); results.Add("PASS " + label); };
    check(Rules.Parse(AssistantIdentity.CurrentName + " open Chrome", true, false) == "chrome", "Wake-word command");
    check(Rules.Parse("Jarvis open Chrome", true, false) == null && Rules.Parse(AssistantIdentity.CurrentName + " open Spotify", true, true) == "spotify", AssistantIdentity.CurrentName + " wake phrase replaces the previous name");
    check(Rules.Parse("open spotify", true, false) == null, "Wake-word enforcement");
    check(Rules.Parse("open notepad", true, true) == "notepad", "Push-to-talk bypass");
    check(Rules.Parse("delete files", false, true) == null, "Reject unrelated commands");
    check(Rules.Parse("open chrome", false, false) == "chrome", "Optional wake word");
    check(!Rules.ValidName("chrome & calc") && Rules.ValidName("visual studio code"), "Mapping name validation");
    check(!Rules.ValidPath("https://example.com") && !Rules.ValidPath("cmd.exe /c calc") && !Rules.ValidPath(@"\\server\app.exe"), "Reject URLs, arguments, and network targets");
    string exe = System.IO.Path.Combine(Environment.SystemDirectory, "notepad.exe"); check(Rules.ValidPath(exe), "Accept local installed executable");
    var maps = new List<AppMap> { new AppMap { Name = "notepad", Path = exe } }; Rules.WriteMaps(scratch, maps); Rules.WriteMaps(scratch, maps);
    check(Rules.ReadMaps(scratch)[0].Path == exe, "Atomic settings save and roundtrip");
    var system = new SystemSnapshot();
    SystemMonitor.ApplyTemperatures(system, new SensorReading[] {
     new SensorReading { Name = "CPU Package", Parent = "/intelcpu/0", Value = 62 },
     new SensorReading { Name = "CPU Core #1", Parent = "/intelcpu/0", Value = 68 },
     new SensorReading { Name = "GPU Core", Parent = "/gpu-nvidia/0", Value = 55 },
     new SensorReading { Name = "GPU Core", Parent = "/gpu-amd/1", Value = 58 },
     new SensorReading { Name = "System", Parent = "/lpc/nct6798d/0", Value = 39 },
     new SensorReading { Name = "Invalid", Parent = "/intelcpu/0", Value = Double.NaN },
     new SensorReading { Name = "Distance to TjMax", Parent = "/intelcpu/0", Value = 60 },
     new SensorReading { Name = "Invalid", Parent = "/lpc/nct6798d/0", Value = 255 }
    });
    check(system.CpuTemperature.StartsWith("62.0") && system.GpuTemperature.StartsWith("58.0"), "CPU package preference and multiple GPU selection (synthetic sensor fixtures)");
    check(system.Board.Contains("System: 39.0") && !system.Details.Contains("Invalid") && !system.Details.Contains("Distance"), "Motherboard labels, invalid-value filtering, and exclusion of thermal margins from temperature readouts");
    var offline = new SystemSnapshot(); SystemMonitor.ApplyTemperatures(offline, new SensorReading[0]);
    check(offline.CpuTemperature == "Unavailable" && offline.GpuTemperature == "Unavailable", "Missing sensor readings stay unavailable");
    check(SystemMonitor.FahrenheitDisplay("0.0 °C / 100.0 °C / -10.0 °C") == "32.0 °F / 212.0 °F / 14.0 °F" && SystemMonitor.FahrenheitDisplay("Unavailable") == "Unavailable", "Fahrenheit display conversion, negative readings, and unavailable values");
    var webSensors = SystemMonitor.ParseWebFeed("{\"Children\":[{\"Children\":[{\"Text\":\"CPU Package\",\"SensorId\":\"/intelcpu/0/temperature/0\",\"Type\":\"Temperature\",\"Value\":\"999 F\",\"RawValue\":61.5},{\"Text\":\"GPU Core\",\"SensorId\":\"/gpu-nvidia/0/temperature/0\",\"Value\":\"131.0 °F\"},{\"Text\":\"System\",\"SensorId\":\"/lpc/nct/0/temperature/0\",\"Value\":\"38,5 °C\"},{\"Text\":\"CPU Core\",\"SensorId\":\"/intelcpu/0/temperature/1\",\"Value\":\"-\",\"RawValue\":null},{\"Text\":\"CPU Total\",\"SensorId\":\"/intelcpu/0/load/0\",\"Type\":\"Load\",\"Value\":\"15 %\"}]}]}");
    var webResult = new SystemSnapshot(); SystemMonitor.ApplyTemperatures(webResult, webSensors);
    check(webSensors.Count == 3 && webResult.CpuTemperature.StartsWith("61.5") && webResult.GpuTemperature.StartsWith("55.0") && webResult.Board.Contains("38.5"), "Nested local web JSON, raw Celsius preference, Fahrenheit conversion, decimal commas, and missing values");
    var unitStrings = SystemMonitor.ParseWebFeed("{\"Children\":[{\"Text\":\"CPU Package\",\"SensorId\":\"/intelcpu/0/temperature/0\",\"RawValue\":\"39.0 °C\",\"Value\":\"102.2 °F\"},{\"Text\":\"GPU Core\",\"SensorId\":\"/gpu-nvidia/0/temperature/0\",\"RawValue\":\"Unavailable\",\"Value\":\"104.0 °F\"}]}");
    check(unitStrings.Count == 2 && Math.Abs(unitStrings[0].Value - 39) < 0.01 && Math.Abs(unitStrings[1].Value - 40) < 0.01, "RawValue strings with units and formatted fallback when raw value is invalid");
    var mixedSensors = SystemMonitor.ParseWebSensors("{\"Children\":[{\"Text\":\"CPU Package\",\"SensorId\":\"/intelcpu/0/temperature/0\",\"RawValue\":\"45.0 °C\"},{\"Text\":\"GPU Core\",\"Type\":\"Load\",\"SensorId\":\"/gpu-nvidia/0/load/0\",\"RawValue\":\"0.0 %\"},{\"Text\":\"GPU Memory\",\"Type\":\"Load\",\"SensorId\":\"/gpu-nvidia/0/load/1\",\"Value\":\"95.0 %\"},{\"Text\":\"D3D 3D\",\"Type\":\"Load\",\"SensorId\":\"/gpu-nvidia/0/load/2\",\"RawValue\":50}]}", true);
    var loadResult = new SystemSnapshot(); SystemMonitor.ApplyTemperatures(loadResult, mixedSensors); SystemMonitor.ApplyGpuLoads(loadResult, mixedSensors);
    check(loadResult.GpuUsage.HasValue && loadResult.GpuUsage.Value == 0 && loadResult.GpuTemperature == "Unavailable" && loadResult.CpuTemperature.StartsWith("45.0"), "GPU zero utilization, core preference over memory/3D sensors, and load/temperature separation");
    mixedSensors.Add(new SensorReading { Name = "D3D 3D", SensorType = "Load", Parent = "/gpu-intel-integrated/0", Value = 12.5 });
    mixedSensors.Add(new SensorReading { Name = "GPU Core", SensorType = "Load", Parent = "/gpu-amd/1", Value = 101 });
    SystemMonitor.ApplyGpuLoads(loadResult, mixedSensors);
    check(loadResult.GpuUsage.Value == 12.5 && loadResult.GpuUsageDetail.Contains("gpu-intel-integrated"), "Busiest adapter, integrated GPU 3D fallback, and out-of-range rejection");
    SystemMonitor.ApplyGpuLoads(loadResult, new SensorReading[0]); check(!loadResult.GpuUsage.HasValue, "Missing GPU utilization stays unavailable");
    var fanSensors = SystemMonitor.ParseWebSensors("{\"Children\":[{\"Text\":\"Fan #1\",\"Type\":\"Fan\",\"SensorId\":\"/lpc/ite/0/fan/0\",\"RawValue\":\"2150 RPM\"},{\"Text\":\"Fan #2\",\"Type\":\"Fan\",\"SensorId\":\"/lpc/ite/0/fan/1\",\"RawValue\":0},{\"Text\":\"Pump\",\"Type\":\"Fan\",\"SensorId\":\"/cooler/0/fan/0\",\"RawValue\":\"Unavailable\",\"Value\":\"3260 RPM\"},{\"Text\":\"GPU Fan\",\"Type\":\"Fan\",\"SensorId\":\"/gpu-nvidia/0/fan/1\",\"RawValue\":900},{\"Text\":\"Fan Control\",\"Type\":\"Control\",\"SensorId\":\"/lpc/ite/0/control/0\",\"Value\":\"50 %\"},{\"Text\":\"Bad RPM\",\"Type\":\"Fan\",\"SensorId\":\"/cooler/0/fan/1\",\"RawValue\":-1}]}", true);
    var fanResult = new SystemSnapshot(); SystemMonitor.ApplyTemperatures(fanResult, fanSensors); SystemMonitor.ApplyGpuLoads(fanResult, fanSensors); SystemMonitor.ApplyFans(fanResult, fanSensors);
    check(fanSensors.Count == 4 && fanResult.Cooling.Contains("2150 RPM") && !fanResult.Cooling.Contains("Fan #2: 0 RPM") && fanResult.Fans.Any(f=>f.Value==0) && fanResult.Cooling.Contains("Pump: 3260 RPM") && !fanResult.Cooling.Contains("GPU Fan") && fanResult.Details.Contains("GPU Fan: 900 RPM"), "Fan RPM strings/numbers, raw fallback, valid zero, generic labels, and separate GPU fan details");
    check(fanResult.CpuTemperature == "Unavailable" && !fanResult.GpuUsage.HasValue, "Fan RPM is isolated from temperatures, utilization, and fan-control percentages");
    SystemMonitor.ApplyFans(fanResult, new SensorReading[0]); check(fanResult.Fans.Count == 0 && fanResult.Cooling.StartsWith("No fan"), "Unavailable cooling data clears prior fan readings");
    var custom = new SystemSnapshot(); SystemMonitor.ApplyTemperatures(custom, new SensorReading[] { new SensorReading { Name = "Renamed die", Parent = "/amdcpu/0", Value = 52 }, new SensorReading { Name = "GPU Hot Spot", Parent = "/nvidiagpu/0", Value = 70 } });
    check(custom.CpuTemperature.Contains("Renamed die: 52.0") && custom.GpuTemperature.Contains("GPU Hot Spot: 70.0"), "Explicitly labelled fallback for renamed CPU sensors and non-core GPU readings");
    var live = new SystemMonitor().Read();
    check(live.ReadAt != default(DateTime) && !String.IsNullOrEmpty(live.Status), "System monitor read and offline-provider handling");
    results.Add("INFO telemetry: " + live.Status + "; RAM " + live.Memory);
    results.Add("INFO recognizers: " + String.Join(", ", SpeechRecognitionEngine.InstalledRecognizers().Select(r => r.Culture.Name)));
    try { using (var synth = new SpeechSynthesizer()) results.Add("INFO voices: " + String.Join(", ", synth.GetInstalledVoices().Select(v => v.VoiceInfo.Name))); }
    catch (Exception ex) { results.Add("WARNING speech output unavailable in this environment: " + ex.Message); }
    results.Add("All automated checks passed. Microphone and real app launches require interactive testing."); File.WriteAllLines(System.IO.Path.Combine(testDirectory, "Skynet-selftest.txt"), results); return 0;
   } catch (Exception ex) { results.Add("FAIL " + ex.ToString()); File.WriteAllLines(System.IO.Path.Combine(testDirectory, "Skynet-selftest.txt"), results); return 1; }
   finally { if (File.Exists(scratch)) File.Delete(scratch); }
  }
 }
}









