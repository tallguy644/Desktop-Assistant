using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace JarvisDesk {
 public partial class Desk {
  CompanionSettings companionSettings = new CompanionSettings();
  CompanionServer companionServer;
  readonly CompanionPairing companionPairing = new CompanionPairing();
  readonly CompanionThrottle companionThrottle = new CompanionThrottle();
  SystemSnapshot companionSnapshot;
  string companionError = "";
  string CompanionPath { get { return Path.Combine(dataDir, "companion.protected"); } }

  void InitializeCompanion() {
   try { if (File.Exists(CompanionPath)) companionSettings = ChatGptClient.ReadProtected<CompanionSettings>(CompanionPath) ?? new CompanionSettings(); }
   catch { companionSettings = new CompanionSettings(); Log("Phone companion settings could not be read. Pair your phone again in Setup."); }
   companionSettings.Normalize();
   ApplyCompanionServer();
   Closing += delegate { if (companionServer != null) companionServer.Stop(); };
  }
  void SaveCompanion() { companionSettings.Normalize(); ChatGptClient.WriteProtected(CompanionPath, companionSettings); }
  void ApplyCompanionServer() {
   if (companionServer == null) companionServer = new CompanionServer(HandleCompanion);
   if (!companionSettings.Enabled) { companionServer.Stop(); companionError = ""; return; }
   if (companionServer.Running && companionServer.Port == companionSettings.Port) return;
   try { companionServer.Start(companionSettings.Port); companionError = ""; Log("Phone companion listening on port " + companionSettings.Port + "."); }
   catch (Exception ex) { companionServer.Stop(); companionError = "Phone companion could not start on port " + companionSettings.Port + ": " + ex.Message; Log(companionError); }
  }

  // ---- API (runs on a worker thread; UI state is touched only through OnUi) ----
  T OnUi<T>(Func<T> work) {
   if (closing || Dispatcher.HasShutdownStarted) throw new CompanionHttpException(503, AssistantIdentity.CurrentName + " is shutting down.");
   return Dispatcher.Invoke(work, DispatcherPriority.Normal, CancellationToken.None, TimeSpan.FromSeconds(15));
  }
  CompanionResponse HandleCompanion(CompanionRequest request) {
   string key = request.Remote.ToString(); DateTime now = DateTime.UtcNow;
   string route = request.Method + " " + request.Path.TrimEnd('/');
   if (route == "GET /api/hello") return CompanionResponse.Ok(new Dictionary<string, object> { { "app", "syra" }, { "api", 1 } });
   if (companionThrottle.Blocked(key, now)) return CompanionResponse.Error(429, "Too many failed attempts. Wait ten minutes and try again.");
   if (route == "POST /api/pair") return CompanionPair(request, key, now);
   var device = OnUi(delegate { var d = CompanionAuth.Find(companionSettings, request.BearerToken); if (d != null) d.LastSeenUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture); return d; });
   if (device == null) { companionThrottle.Fail(key, now); return CompanionResponse.Error(401, "This phone is not paired. Pair it again from " + AssistantIdentity.CurrentName + " Setup."); }
   companionThrottle.Clear(key);
   switch (route) {
    case "GET /api/status": {
     NowPlayingInfo playing = null; try { playing = NowPlayingReader.Read(); } catch { }
     return OnUi(delegate { var status = CompanionStatus(); status["nowPlaying"] = playing == null ? null : new Dictionary<string, object> { { "source", playing.Source }, { "title", playing.Title ?? "" }, { "playing", playing.Playing } }; return CompanionResponse.Ok(status); });
    }
    case "POST /api/theme": {
     var body = request.Json(); object a, r, c; body.TryGetValue("accent", out a); body.TryGetValue("rings", out r); body.TryGetValue("core", out c);
     var colors = new ThemePreferences { Accent = a as string, Rings = r as string, Core = c as string };
     try { colors.Validate(); } catch (ArgumentException ex) { return CompanionResponse.Error(400, ex.Message); }
     return OnUi(delegate { return CompanionTheme(colors); });
    }
    case "POST /api/media": {
     string action = request.Text("action");
     if (!MediaKeys.Known(action)) return CompanionResponse.Error(400, "Use playpause, next, previous, volup, voldown or mute.");
     MediaKeys.Send(action); Thread.Sleep(300);
     return CompanionResponse.Ok(new Dictionary<string, object> { { "message", MediaKeys.Message(action) } });
    }
    case "GET /api/apps": return OnUi(delegate { return CompanionResponse.Ok(new Dictionary<string, object> { { "apps", maps.Select(m => m.Name).OrderBy(n => n).ToArray() } }); });
    case "POST /api/apps/launch": { string name = Rules.Normalize(request.Text("name")); return OnUi(delegate { return CompanionLaunch(name); }); }
    case "POST /api/screens": { string action = request.Text("action"); return OnUi(delegate { return CompanionScreens(action); }); }
    case "POST /api/ask": return CompanionAsk(request.Text("question"), request.Json().ContainsKey("speak") && true.Equals(request.Json()["speak"]));
    case "GET /api/agenda": return CompanionAgenda();
    case "GET /api/commute": return CompanionCommute();
    case "POST /api/unpair": return OnUi(delegate { companionSettings.Devices.Remove(device); SaveCompanion(); Log("Phone unpaired: " + device.Name); return CompanionResponse.Ok(new Dictionary<string, object> { { "ok", true } }); });
   }
   return CompanionResponse.Error(404, "Unknown request.");
  }
  CompanionResponse CompanionPair(CompanionRequest request, string key, DateTime now) {
   var body = request.Json(); object code, name; body.TryGetValue("code", out code); body.TryGetValue("device", out name);
   if (!companionPairing.TryUse(code as string, now)) { companionThrottle.Fail(key, now); return CompanionResponse.Error(401, "That pairing code is wrong or expired. Choose PAIR NEW PHONE in " + AssistantIdentity.CurrentName + " Setup for a new one."); }
   string token = CompanionAuth.NewToken();
   var device = new CompanionDevice { Id = Guid.NewGuid().ToString("N"), Name = CompanionAuth.CleanDeviceName(name as string), TokenHash = CompanionAuth.Hash(token), PairedUtc = now.ToString("o", CultureInfo.InvariantCulture) };
   return OnUi(delegate {
    if (companionSettings.Devices.Count >= CompanionAuth.MaxDevices) return CompanionResponse.Error(409, "Too many paired phones. Remove one in Setup first.");
    companionSettings.Devices.Add(device); SaveCompanion(); companionThrottle.Clear(key); Log("Phone paired: " + device.Name); RefreshCompanionPage();
    return CompanionResponse.Ok(new Dictionary<string, object> { { "token", token }, { "name", AssistantIdentity.CurrentName }, { "deviceId", device.Id } });
   });
  }
  Dictionary<string, object> CompanionStatus() {
   var s = companionSnapshot; var stats = new Dictionary<string, object>();
   if (s != null) {
    stats["cpu"] = s.CpuUsage; stats["ram"] = s.RamUsage; stats["gpu"] = s.GpuUsage;
    stats["cpuTempF"] = s.CpuTemperatureCelsius.HasValue ? (object)Math.Round(s.CpuTemperatureCelsius.Value * 9 / 5 + 32, 1) : null;
    stats["gpuTempF"] = s.GpuTemperatureCelsius.HasValue ? (object)Math.Round(s.GpuTemperatureCelsius.Value * 9 / 5 + 32, 1) : null;
    stats["memory"] = s.Memory; stats["board"] = SystemMonitor.FahrenheitDisplay(s.Board); stats["sensorStatus"] = s.Status;
    stats["fans"] = s.Fans.Where(f => f.Value > 0).Select(f => new Dictionary<string, object> { { "name", f.Name }, { "rpm", Math.Round(f.Value) } }).ToArray();
    stats["readAt"] = s.ReadAt == default(DateTime) ? null : s.ReadAt.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
   }
   bool chatReady = chatClient != null && chatClient.Connected && chatModels != null && chatModels.SelectedItem is ChatModel;
   return new Dictionary<string, object> {
    { "name", AssistantIdentity.CurrentName }, { "state", state.Text }, { "listening", continuous },
    { "stats", stats },
    { "screens", new Dictionary<string, object> { { "enabled", screenPreferences.Enabled }, { "active", screenController != null && screenController.Active }, { "off", screenController != null && screenController.DisplaysOff } } },
    { "chat", new Dictionary<string, object> { { "ready", chatReady }, { "busy", chatBusy } } },
    { "calendar", calendarUrl.Length > 0 }, { "commute", commutePreferences != null && commutePreferences.Enabled },
    { "theme", new Dictionary<string, object> { { "accent", themePreferences.Accent }, { "rings", themePreferences.Rings }, { "core", themePreferences.Core } } }
   };
  }
  // Colors chosen on the phone: saved exactly like SAVE COLORS in Setup, applied live to the HUD.
  CompanionResponse CompanionTheme(ThemePreferences colors) {
   bool reopen = setupWindow != null && setupSection == "colors";
   if (reopen) ReleaseSetupSection(); // an open, unsaved color page would otherwise restore its old colors when it closes
   try { colors.Save(ThemePath); } catch { return CompanionResponse.Error(500, "Colors could not be saved on the PC."); }
   themePalette.Apply(colors); themePreferences = colors.Copy(); Log("Colors changed from phone.");
   if (reopen) SelectSetupSection("colors");
   return CompanionResponse.Ok(new Dictionary<string, object> { { "message", "Colors updated on your PC." } });
  }
  CompanionResponse CompanionLaunch(string name) {
   var map = maps.FirstOrDefault(m => m.Name == name);
   if (map == null) return CompanionResponse.Error(404, "No app is mapped as \"" + name + "\".");
   if ((DateTime.UtcNow - lastLaunch).TotalSeconds < 2) return CompanionResponse.Error(429, "Wait a moment between launches.");
   try { TouchScreenActivity(); heard.Text = "PHONE / OPEN " + name.ToUpperInvariant(); Launch(map); }
   catch (Exception ex) { Log(ex.Message); return CompanionResponse.Error(409, ex.Message); }
   return CompanionResponse.Ok(new Dictionary<string, object> { { "message", "Opened " + map.Name + "." } });
  }
  CompanionResponse CompanionScreens(string action) {
   if (action != "wake" && action != "off") return CompanionResponse.Error(400, "Use wake or off.");
   if (!screenPreferences.Enabled) return CompanionResponse.Error(409, "Turn on Screens & voice wake in " + AssistantIdentity.CurrentName + " Setup first.");
   if (!WindowsScreenPower.Unlocked) return CompanionResponse.Error(409, "Windows is locked. Screen control resumes after you sign in.");
   RefreshScreenControl();
   if (!screenController.Active) return CompanionResponse.Error(409, screenError.Length > 0 ? screenError : AssistantIdentity.CurrentName + " must be listening (LISTEN) for screen control.");
   ScreenCommand(action, false);
   return CompanionResponse.Ok(new Dictionary<string, object> { { "message", action == "wake" ? "Screens waking." : "Screens off." } });
  }
  CompanionResponse CompanionAsk(string question, bool speak) {
   question = (question ?? "").Trim();
   if (question.Length == 0) return CompanionResponse.Error(400, "Type a question first.");
   if (question.Length > 4000) return CompanionResponse.Error(413, "That question is too long.");
   var done = new TaskCompletionSource<string[]>();
   OnUi(delegate { SendChat(question, speak, delegate(string answer, string error) { done.TrySetResult(new[] { answer, error }); }); return true; });
   if (!done.Task.Wait(TimeSpan.FromSeconds(170))) return CompanionResponse.Error(504, "The answer took too long. Check " + AssistantIdentity.CurrentName + " on your PC.");
   var result = done.Task.Result;
   if (result[0] == null) return CompanionResponse.Error(409, result[1] ?? "The question could not be answered.");
   return CompanionResponse.Ok(new Dictionary<string, object> { { "answer", result[0] } });
  }
  CompanionResponse CompanionAgenda() {
   bool linked = OnUi(delegate {
    if (calendarUrl.Length == 0) return false;
    if (!CompanionAgendaValid() && !agendaBusy) RefreshCalendar();
    return true;
   });
   if (!linked) return CompanionResponse.Error(409, "Connect Google Calendar in " + AssistantIdentity.CurrentName + " Setup first.");
   for (int i = 0; i < 30 && OnUi(delegate { return agendaBusy; }); i++) Thread.Sleep(500);
   return OnUi(delegate {
    bool valid = CompanionAgendaValid();
    var items = valid && agendaSnapshot.Items != null ? agendaSnapshot.Items.Select(item => (object)new Dictionary<string, object> {
     { "title", item.Title ?? "Untitled event" }, { "location", item.Location ?? "" }, { "allDay", item.AllDay },
     { "time", item.AllDay ? "All day" : item.Start.Date < DateTime.Today ? "Continuing" : item.Start.ToString("h:mm tt", CultureInfo.InvariantCulture) } }).ToArray() : new object[0];
    return CompanionResponse.Ok(new Dictionary<string, object> { { "text", AgendaSpeech.Build(valid ? agendaSnapshot : null, DateTime.Today, agendaOffline) }, { "offline", agendaOffline }, { "items", items } });
   });
  }
  bool CompanionAgendaValid() { return calendarUrl.Length > 0 && agendaSnapshot != null && agendaSnapshot.Matches(DateTime.Today, TimeZoneInfo.Local, CalendarFeed.SourceId(calendarUrl)); }
  CompanionResponse CompanionCommute() {
   bool waiting = OnUi(delegate {
    if (!commuteLoaded || !commutePreferences.Enabled) return false;
    bool work = FindWork(); bool fresh = work && CalendarFresh() && Commute.Fresh(commuteEstimate, activeRouteKey, DateTime.UtcNow);
    if (work && !fresh && !agendaOffline) { CheckMaps(false); return true; }
    return false;
   });
   for (int i = 0; waiting && i < 50; i++) {
    Thread.Sleep(500);
    waiting = OnUi(delegate { return FindWork() && !Commute.Fresh(commuteEstimate, activeRouteKey, DateTime.UtcNow) && !mapsMessage.StartsWith("Check the map", StringComparison.Ordinal) && !mapsMessage.StartsWith("Could not", StringComparison.Ordinal); });
   }
   return OnUi(delegate {
    if (!commutePreferences.Enabled) return CompanionResponse.Error(409, "Set up Work & commute in " + AssistantIdentity.CurrentName + " Setup first.");
    return CompanionResponse.Ok(new Dictionary<string, object> { { "text", CommuteDescription() } });
   });
  }

  // ---- Setup page ----
  CheckBox companionEnabledBox; TextBox companionPortBox; TextBlock companionStatusText, companionPinText; ListBox companionDeviceList; DispatcherTimer companionPageTimer;
  void OpenCompanionSettings() {
   var form = new StackPanel { Margin = new Thickness(24) };
   form.Children.Add(Label("PHONE COMPANION", 24, cyan));
   form.Children.Add(Label("Control " + AssistantIdentity.CurrentName + " from the Syra Companion Android app: launch apps, wake or turn off screens, see PC stats, and ask questions. Only paired phones on your home network or your Tailscale network can connect.", 12, muted));
   companionEnabledBox = new CheckBox { Content = "Allow paired phones to connect", IsChecked = companionSettings.Enabled, Foreground = cyan, FontSize = 12, Margin = new Thickness(0, 10, 0, 12) }; form.Children.Add(companionEnabledBox);
   form.Children.Add(Label("PORT", 11, cyan));
   companionPortBox = new TextBox { Text = companionSettings.Port.ToString(CultureInfo.InvariantCulture), Width = 100, MaxLength = 5, HorizontalAlignment = HorizontalAlignment.Left, Height = 30, Padding = new Thickness(6), Background = Ink(3, 19, 27), Foreground = System.Windows.Media.Brushes.White, BorderBrush = Ink(0, 103, 115), Margin = new Thickness(0, 0, 0, 12) }; form.Children.Add(companionPortBox);
   var row = new WrapPanel(); row.Children.Add(CompactButton("SAVE", SaveCompanionSettings)); row.Children.Add(CompactButton("ALLOW IN FIREWALL", AllowCompanionFirewall)); form.Children.Add(row);
   companionStatusText = Label("", 12, cyan); form.Children.Add(companionStatusText);
   form.Children.Add(Label("PAIR A PHONE", 14, cyan));
   form.Children.Add(Label("Open Syra Companion on your phone, enter one of the addresses above, then type this code. Codes last three minutes.", 12, muted));
   var pairRow = new WrapPanel(); pairRow.Children.Add(CompactButton("PAIR NEW PHONE", StartCompanionPairing)); form.Children.Add(pairRow);
   companionPinText = Label("", 30, cyan); companionPinText.FontFamily = new System.Windows.Media.FontFamily("Consolas"); form.Children.Add(companionPinText);
   form.Children.Add(Label("PAIRED PHONES", 14, cyan));
   companionDeviceList = new ListBox { MinHeight = 60, MaxHeight = 140, Background = System.Windows.Media.Brushes.Transparent, Foreground = System.Windows.Media.Brushes.White, BorderBrush = Ink(29, 89, 108), Margin = new Thickness(0, 0, 0, 10) }; form.Children.Add(companionDeviceList);
   var removeRow = new WrapPanel(); removeRow.Children.Add(CompactButton("REMOVE SELECTED", RemoveCompanionDevice)); form.Children.Add(removeRow);
   setupContent.Content = new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
   companionPageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; companionPageTimer.Tick += delegate { RefreshCompanionPage(); }; companionPageTimer.Start();
   setupRestores.Add(delegate { if (companionPageTimer != null) companionPageTimer.Stop(); companionPageTimer = null; companionPinText = null; companionStatusText = null; companionDeviceList = null; });
   RefreshCompanionPage();
  }
  void RefreshCompanionPage() {
   if (companionStatusText == null) return;
   if (!companionSettings.Enabled) companionStatusText.Text = "Off. Turn it on and SAVE to let your phone connect.";
   else if (companionError.Length > 0) companionStatusText.Text = companionError;
   else { var addresses = CompanionServer.LocalAddresses(); companionStatusText.Text = "Listening on port " + companionSettings.Port + ". Phone address:\n" + (addresses.Count == 0 ? "No network address found." : String.Join("\n", addresses.Select(a => "  " + a))) + (addresses.Any(a => a.Contains("Tailscale")) ? "" : "\nInstall Tailscale on this PC and your phone to connect from anywhere."); }
   var pin = companionPairing.Current(DateTime.UtcNow);
   if (companionPinText != null) companionPinText.Text = pin == null ? "" : pin.Substring(0, 3) + " " + pin.Substring(3) + "   (" + companionPairing.SecondsLeft(DateTime.UtcNow) + "s)";
   if (companionDeviceList != null) {
    var selected = companionDeviceList.SelectedItem as CompanionDeviceRow;
    var rows = companionSettings.Devices.Select(d => new CompanionDeviceRow(d)).ToList();
    if (companionDeviceList.Items.Count != rows.Count || !rows.Select(r => r.ToString()).SequenceEqual(companionDeviceList.Items.Cast<object>().Select(o => o.ToString()))) {
     companionDeviceList.ItemsSource = rows; if (selected != null) companionDeviceList.SelectedItem = rows.FirstOrDefault(r => r.Device.Id == selected.Device.Id);
    }
   }
  }
  sealed class CompanionDeviceRow {
   public readonly CompanionDevice Device; public CompanionDeviceRow(CompanionDevice d) { Device = d; }
   public override string ToString() {
    DateTime seen; string last = DateTime.TryParse(Device.LastSeenUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out seen) ? "last used " + seen.ToLocalTime().ToString("MMM d h:mm tt", CultureInfo.InvariantCulture) : "not used this session";
    return Device.Name + "   /   " + last;
   }
  }
  void SaveCompanionSettings() {
   int port; if (!Int32.TryParse(companionPortBox.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out port) || port < 1024 || port > 65535) { companionStatusText.Text = "Use a port from 1024 to 65535."; return; }
   companionSettings.Enabled = companionEnabledBox.IsChecked == true; companionSettings.Port = port;
   try { SaveCompanion(); } catch { companionStatusText.Text = "Settings could not be saved. Try again."; return; }
   if (companionServer != null && companionServer.Running && companionServer.Port != port) companionServer.Stop();
   ApplyCompanionServer(); if (!companionSettings.Enabled) companionPairing.Cancel(); RefreshCompanionPage();
  }
  void StartCompanionPairing() {
   if (!companionSettings.Enabled || companionServer == null || !companionServer.Running) { companionStatusText.Text = "Turn on the phone companion and SAVE before pairing."; return; }
   if (companionSettings.Devices.Count >= CompanionAuth.MaxDevices) { companionStatusText.Text = "Remove a paired phone first."; return; }
   companionPairing.Start(DateTime.UtcNow); RefreshCompanionPage();
  }
  void RemoveCompanionDevice() {
   var row = companionDeviceList.SelectedItem as CompanionDeviceRow; if (row == null) return;
   companionSettings.Devices.Remove(row.Device); try { SaveCompanion(); } catch { companionStatusText.Text = "Could not save. Try again."; return; }
   Log("Phone removed: " + row.Device.Name); RefreshCompanionPage();
  }
  void AllowCompanionFirewall() {
   string exe = Process.GetCurrentProcess().MainModule.FileName; int port = companionSettings.Port;
   string rule = "\"Syra phone companion\"";
   string args = "/c netsh advfirewall firewall delete rule name=" + rule + " >nul & netsh advfirewall firewall add rule name=" + rule + " dir=in action=allow protocol=TCP localport=" + port.ToString(CultureInfo.InvariantCulture) + " program=\"" + exe + "\" profile=private,public";
   try { var p = Process.Start(new ProcessStartInfo { FileName = "cmd.exe", Arguments = args, Verb = "runas", UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden }); if (p != null) p.WaitForExit(15000); companionStatusText.Text = "Firewall rule added for port " + port + "."; }
   catch (System.ComponentModel.Win32Exception) { companionStatusText.Text = "Firewall change was cancelled. Windows may ask again the first time your phone connects."; }
  }
 }
}
