using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace JarvisDesk {
 public partial class Desk {
  System.Windows.Shapes.Path cpuDialArc, ramDialArc, gpuDialArc;
  Border[] calendarCells;
  long referenceClockSecond;
  void BuildReferenceHud() {
   WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.CanResizeWithGrip;
   // Retain native caption movement, restore-on-drag, and edge resizing in the custom HUD.
   var chrome = new System.Windows.Shell.WindowChrome { CaptionHeight=70, ResizeBorderThickness=new Thickness(6), GlassFrameThickness=new Thickness(0), CornerRadius=new CornerRadius(0), UseAeroCaptionButtons=false };
   System.Windows.Shell.WindowChrome.SetWindowChrome(this,chrome);
   SizeChanged += delegate {
    double scale=Math.Min(ActualWidth/1440.0,ActualHeight/810.0);
    chrome.CaptionHeight=Math.Max(6,(ActualHeight-810*scale)/2+70*scale);
   };
   Resources.Add(typeof(Button), (Style)System.Windows.Markup.XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='Frame' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Frame' Property='Background' Value='{DynamicResource HudHoverBackground}'/><Setter TargetName='Frame' Property='BorderBrush' Value='{DynamicResource HudHoverBorder}'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Frame' Property='BorderBrush' Value='White'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>"));
   Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar), (Style)System.Windows.Markup.XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ScrollBar'><Setter Property='Width' Value='4'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'><Grid Background='Transparent'><Track x:Name='PART_Track' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' IsDirectionReversed='True'><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='Thumb'><Border Background='{DynamicResource HudScrollThumb}'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb></Track></Grid></ControlTemplate></Setter.Value></Setter></Style>"));
   foreach(string key in new[]{"HudHoverBackground","HudHoverBorder","HudScrollThumb"})((Style)Resources[typeof(Button)]).Resources[key]=Resources[key];
   ((Style)Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)]).Resources["HudScrollThumb"]=Resources["HudScrollThumb"];
   InitializeTileStyle();
   var stage = new Canvas { Width = 1440, Height = 810, Background = Brushes.Black, ClipToBounds = true };
   Content = new Viewbox { Stretch = Stretch.Uniform, Child = stage };
   hudTaskbarTab=CompactButton("TASKBAR ↑",RevealHudTaskbar);hudTaskbarTab.Visibility=Visibility.Collapsed;hudTaskbarTab.FontSize=9;hudTaskbarTab.Padding=new Thickness(2,0,2,0);hudTaskbarTab.ToolTip="Hover or click to show this monitor's taskbar.";hudTaskbarTab.MouseEnter+=delegate{try{RevealHudTaskbar();}catch(Exception ex){Log(ex.Message);}};
   DrawHoneycomb(stage);
   calendarCells = new Border[31];
   for (int day = 1; day <= 31; day++) {
    var label = Label(day.ToString("00"), 14, cyan); label.Margin = new Thickness(0); label.TextAlignment = TextAlignment.Center;
    var cell = new Border { Child = label, CornerRadius = new CornerRadius(3), Padding = new Thickness(3, 1, 3, 1) }; calendarCells[day - 1] = cell; Place(stage, cell, 8 + (day - 1) * 42.8, 1, 30, 25);
   }
   Place(stage,CompactButton("COLORS / SETUP",OpenAppearanceSettings),1178,32,128,28);
   Place(stage, CompactButton("_", MinimizeDeck), 1313, 32, 30, 28);
   Place(stage, CompactButton("□", ToggleDeckSize), 1349, 32, 30, 28); Place(stage, CompactButton("×", Close), 1385, 32, 30, 28);
   Wire(stage, 48, 40, 48, 782, Ink(0, 168, 174), 1); Wire(stage, 53, 267, 53, 747, cyan, 4); Wire(stage, 62, 40, 309, 40, Ink(0, 92, 101), 1);
   CreateCalendarPanel(stage);
   clock = Label(DateTime.Now.ToString("HH:mm:ss"), 15, cyan); Place(stage, clock, 270, 50, 130, 30);
   var cpuGauge = PercentDial("CPU", out cpuLoad, out cpuDialArc, 138); Place(stage, cpuGauge, 298, 98, 138, 138);
   var ramGauge = PercentDial("RAM", out ramStatus, out ramDialArc, 138); Place(stage, ramGauge, 446, 35, 138, 138);
   ramDetails = Label("", 11, cyan); Place(stage, ramDetails, 447, 180, 176, 28);
   Wire(stage, 581, 83, 745, 83, cyan, 1.5); Wire(stage, 745, 83, 780, 110, cyan, 1.5);
   Place(stage, Label("VOICE / HARDWARE INTERFACE", 10, muted), 601, 74, 200, 25);
   CreateNextUpDial(stage);
   var thermal = BuildThermalReadings();
   Place(stage, ReferenceSection("THERMAL READINGS · °F", thermal, 230, 205), 76, 266, 230, 205);
   var memory = new StackPanel(); memory.Children.Add(Label("LIVE CPU / GPU / RAM LOAD", 10, muted));
   cpuBar = LoadBar(); gpuBar = LoadBar(); ramBar = LoadBar(); memory.Children.Add(BarTrack(cpuBar, 7)); memory.Children.Add(BarTrack(gpuBar, 7)); memory.Children.Add(BarTrack(ramBar, 0)); Place(stage, memory, 77, 480, 262, 51);
   var links = new StackPanel(); chatNavigationButton=CompactButton("◉  CONVERSATION",OpenChatPanel);links.Children.Add(chatNavigationButton); links.Children.Add(CompactButton("◉  SENSOR READINGS", delegate { OpenSensorOverlay(); })); links.Children.Add(CompactButton("◉  APP MAPPINGS", delegate { OpenAppOverlay(); })); links.Children.Add(CompactButton("◉  VOICE SETTINGS", OpenVoiceSettings)); links.Children.Add(CompactButton("◉  YOUR NAME / GREETING", OpenNameSettings)); screenSettingsLink=CompactButton("◉  SCREENS / OFF",OpenScreenSettings); links.Children.Add(screenSettingsLink); links.Children.Add(CompactButton("◉  EXPAND / F11", ToggleDeckSize)); foreach(Button item in links.Children) { item.Height=22;item.FontSize=11;item.Padding=new Thickness(4,2,4,2);item.Margin=new Thickness(0,0,3,2); } Place(stage, links, 76, 548, 240, 170);
   var reactor = new Viewbox { Child = VoiceOrb(), Stretch = Stretch.Uniform }; Place(stage, reactor, 327, 185, 470, 470);
   state.TextAlignment = TextAlignment.Center; Place(stage, state, 459, 676, 220, 25);
   var launches = new StackPanel(); launches.Children.Add(CompactButton("●  Chrome", delegate { LaunchMapped("chrome"); })); launches.Children.Add(CompactButton("●  Spotify", delegate { LaunchMapped("spotify"); })); launches.Children.Add(CompactButton("●  Notepad", delegate { LaunchMapped("notepad"); })); Place(stage, launches, 611, 108, 150, 83);
   var extraLinks = new StackPanel(); extraLinks.Children.Add(CompactButton("●  Applications", OpenAppOverlay)); extraLinks.Children.Add(CompactButton("●  Sensor array", OpenSensorOverlay)); Place(stage, extraLinks, 269, 596, 172, 93);
   var gpuGauge = PercentDial("GPU LOAD", out gpuLoad, out gpuDialArc, 158); Place(stage, gpuGauge, 961, 36, 158, 158);
   var cooling = new StackPanel(); coolingSpeeds = SensorText(122); coolingSpeeds.Foreground = cyan; cooling.Children.Add(coolingSpeeds); cooling.Children.Add(Label("FANS / PUMP SENSORS\nCHANNEL ASSIGNMENT UNCONFIRMED", 9, muted)); Place(stage, ReferenceSection("COOLING / RPM", cooling, 242, 209), 1160, 90, 242, 209);
   var board = new StackPanel(); boardTemps = SensorText(117); boardTemps.Foreground = cyan; board.Children.Add(boardTemps); Place(stage, ReferenceSection("MOTHERBOARD / °F", board, 242, 165), 1160, 319, 242, 165);
   var connection = new StackPanel(); sensorState = Label("CONNECTING…", 11, cyan); connection.Children.Add(sensorState); sensorTime = Label("", 10, muted); connection.Children.Add(sensorTime); voiceInfo = Label("", 9, muted); connection.Children.Add(voiceInfo); Place(stage, ReferenceSection("LOCAL SYSTEM LINK", connection, 242, 138), 1160, 504, 242, 138);
   Place(stage, GlassCard(10), 913, 648, 500, 145);
   Place(stage, Label("CPU / GPU  ·  LIVE THERMAL HISTORY", 11, cyan), 923, 657, 480, 27);
   var cpuPlot = new Canvas { Background = ThemeArgb(20,0,99,110,ThemeRole.Accent) }; cpuTrend = new Polyline { Stroke = cyan, StrokeThickness = 1.3 }; cpuPlot.Children.Add(cpuTrend); Place(stage, cpuPlot, 929, 686, 260, 48);
   var gpuPlot = new Canvas { Background = Brushes.Transparent }; gpuTrend = new Polyline { Stroke = Ink(0, 145, 154), StrokeThickness = 1.3 }; gpuPlot.Children.Add(gpuTrend); Place(stage, gpuPlot, 929, 733, 260, 48);
   Wire(stage, 929, 728, 1402, 728, Ink(0, 70, 78), 1); Wire(stage, 929, 780, 1402, 780, Ink(0, 70, 78), 1);
   Place(stage, Label("CPU", 10, cyan), 1220, 700, 70, 20); Place(stage, Label("GPU", 10, muted), 1220, 748, 70, 20);
   var tiles = new StackPanel { Orientation = Orientation.Horizontal };
   listenButton = FeatureTile("\uE720", "LISTEN", "Listen for voice commands", ToggleListen); tiles.Children.Add(listenButton);
   tiles.Children.Add(FeatureTile("\uE7C8", "TALK·F8", "Push to talk (F8)", PushToTalk));
   tiles.Children.Add(FeatureTile("\uE8BD", "CHAT", "Conversation", OpenChatPanel));
   tiles.Children.Add(FeatureTile("\uE71D", "APPS", "App mappings", OpenAppOverlay));
   tiles.Children.Add(FeatureTile("\uE9D9", "SENSORS", "Sensor readings", OpenSensorOverlay));
   tiles.Children.Add(FeatureTile("\uE713", "SETUP", "Colors and PC setup", OpenAppearanceSettings));
   Place(stage, tiles, 366, 704, 420, 62);
   heard = Label("SAY “OPEN NOTEPAD” OR “WAKE UP”", 10, cyan); Place(stage, heard, 489, 779, 390, 22);
   Place(stage,hudTaskbarTab,1284,790,128,18);
   subtitle = Label("Ready when you are.", 10, muted); Place(stage, subtitle, 77, 730, 260, 28);
   log = new TextBox { IsReadOnly = true, Background = Brushes.Transparent, Foreground = muted, BorderThickness = new Thickness(0), FontFamily = new FontFamily("Consolas"), FontSize = 8, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden }; Place(stage, log, 76, 762, 285, 37);
   CreateReferenceOverlays(stage);
   CreateChatPanel(stage);
   CreateCommutePanel(stage);
   CreateScreenPanel(stage);
   KeyDown += delegate(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == System.Windows.Input.Key.F11) ToggleDeckSize(); if (e.Key == System.Windows.Input.Key.Escape) { appDrawer.Visibility = Visibility.Collapsed; sensorDrawer.Visibility = Visibility.Collapsed; CloseCommutePanel(); CloseScreenPanel(); HideChatPanel(); } };
   UpdateReferenceClock();
  }
  Button CompactButton(string title, Action action) {
   var button = Action(title, action); System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(button,true); button.Background = Brushes.Transparent; button.BorderBrush = Brushes.Transparent; button.BorderThickness = new Thickness(0); button.Padding = new Thickness(4, 5, 4, 5); button.Margin = new Thickness(0, 0, 3, 3); button.Width = Math.Max(34, title.Length * 7 + 16); button.FontSize = 12; button.Foreground = cyan; return button;
  }
  void ToggleDeckSize() { if(IsDesktop) { DisableDesktop(true);Activate();return; } WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }
  void OpenSensorOverlay() { CloseScreenPanel();sensorDrawer.Visibility = Visibility.Visible; appDrawer.Visibility = Visibility.Collapsed; HideChatPanel(); }
  void OpenAppOverlay() { CloseScreenPanel();appDrawer.Visibility = Visibility.Visible; sensorDrawer.Visibility = Visibility.Collapsed; HideChatPanel(); }
  void LaunchMapped(string name) { var map = maps.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase)); if (map == null) { OpenAppOverlay(); Log("Add a mapping for " + name + " to use this launcher."); } else Launch(map); }
  Grid ReferenceSection(string title, UIElement content, double width, double height) {
   TextBlock heading;return ReferenceSection(title,content,width,height,out heading);
  }
  Grid ReferenceSection(string title, UIElement content, double width, double height,out TextBlock heading) {
   var result = new Grid { Width = width, Height = height }; result.Children.Add(GlassCard(10));
   var frame = new Canvas { IsHitTestVisible = false }; Wire(frame, 12, 29, width - 12, 29, ThemeArgb(70, 0, 200, 220, ThemeRole.Accent), 1);
   frame.Children.Add(new Rectangle { Width = 3, Height = 13, RadiusX = 1.5, RadiusY = 1.5, Fill = cyan, Margin = new Thickness(10, 9, 0, 0), Effect = themePalette.Glow(Color.FromRgb(0, 230, 235), ThemeRole.Accent, 8, 0.9) });
   var text = new DockPanel { Margin = new Thickness(19, 6, 10, 6) }; heading = Label(title, 12, cyan); DockPanel.SetDock(heading, Dock.Top); text.Children.Add(heading); text.Children.Add(content); result.Children.Add(frame); result.Children.Add(text); return result;
  }
  Rectangle LoadBar() { return new Rectangle { Height = 4, RadiusX = 2, RadiusY = 2, Fill = cyan, Width = 0, HorizontalAlignment = HorizontalAlignment.Left, Effect = themePalette.Glow(Color.FromRgb(0, 230, 235), ThemeRole.Accent, 8, 0.8) }; }
  Grid BarTrack(Rectangle bar, double bottom) { var track = new Grid { Width = 260, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, bottom) }; track.Children.Add(new Rectangle { Height = 4, RadiusX = 2, RadiusY = 2, Fill = ThemeArgb(45, 0, 200, 220, ThemeRole.Accent) }); track.Children.Add(bar); return track; }
  Grid PercentDial(string caption, out TextBlock value, out System.Windows.Shapes.Path arc, double size) {
   var result = new Grid { Width = size, Height = size };
   result.Children.Add(new Ellipse { Width = size - 14, Height = size - 14, Fill = ThemeRadial(Color.FromArgb(70, 0, 110, 140), Color.FromArgb(14, 0, 40, 60), ThemeRole.Accent), Stroke = ThemeArgb(90, 0, 200, 220, ThemeRole.Accent), StrokeThickness = 1 });
   result.Children.Add(Ring(size - 5, ThemeArgb(55, 0, 170, 190, ThemeRole.Accent), 8, null, 0));
   arc = Arc(size, size / 2 - 3, -90, -89.9, cyan, 8); arc.StrokeStartLineCap = PenLineCap.Round; arc.StrokeEndLineCap = PenLineCap.Round; arc.Effect = themePalette.Glow(Color.FromRgb(0, 230, 235), ThemeRole.Accent, 12, 0.9); result.Children.Add(arc);
   var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center }; var label = Label(caption, 11, cyan); label.TextAlignment = TextAlignment.Center; text.Children.Add(label); value = Label("—", 28, cyan); value.FontFamily = new FontFamily("Bahnschrift Condensed"); value.TextAlignment = TextAlignment.Center; value.Margin = new Thickness(0); text.Children.Add(value); result.Children.Add(text); return result;
  }
  void UpdateDial(System.Windows.Shapes.Path arc, double? percent, double size) {
   arc.Visibility = percent.HasValue && percent.Value > 0 ? Visibility.Visible : Visibility.Hidden; if (!percent.HasValue) return;
   double span = Math.Max(0.001, Math.Min(99.999, percent.Value)) * 3.6;
   arc.Data = Arc(size, size / 2 - 3, -90, -90 + span, cyan, 8).Data;
  }
  void UpdateReferenceClock() {
   var now = DateTime.Now;
   long second=now.Ticks/TimeSpan.TicksPerSecond;if(second==referenceClockSecond) return;referenceClockSecond=second;
   if(clock!=null) clock.Text=now.ToString("HH:mm:ss");
   for (int day = 1; day <= 31; day++) { var cell = calendarCells[day - 1]; cell.Visibility = day <= DateTime.DaysInMonth(now.Year, now.Month) ? Visibility.Visible : Visibility.Hidden; cell.Background = day == now.Day ? cyan : Brushes.Transparent; ((TextBlock)cell.Child).Foreground = day == now.Day ? Brushes.Black : cyan; }
  }
  void CreateReferenceOverlays(Canvas stage) {
   var appHost = new DockPanel(); var close = Action("CLOSE APP MAPPINGS / ESC", delegate { appDrawer.Visibility = Visibility.Collapsed; }); DockPanel.SetDock(close, Dock.Top); appHost.Children.Add(close); appHost.Children.Add(BuildAppPanel()); appDrawer = PanelBox(appHost); appDrawer.Visibility = Visibility.Collapsed; Place(stage, appDrawer, 345, 130, 760, 595);
   var sensors = new DockPanel(); var closeSensors = Action("CLOSE SENSOR READINGS / ESC", delegate { sensorDrawer.Visibility = Visibility.Collapsed; }); DockPanel.SetDock(closeSensors, Dock.Top); sensors.Children.Add(closeSensors);
   var tools = new WrapPanel(); tools.Children.Add(Action("SENSOR SETUP", delegate { MessageBox.Show("Keep Libre Hardware Monitor running with its local web server at 127.0.0.1:8085. All temperatures are Fahrenheit. Fan and pump RPM channels retain their hardware labels until their physical connections are confirmed. The app ZIP includes a connection-repair helper.", "Sensor setup"); })); tools.Children.Add(Action("DOWNLOAD MONITOR", delegate { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/releases", UseShellExecute = true }); })); tools.Children.Add(Action("COPY DIAGNOSTICS",CopyDiagnostics)); DockPanel.SetDock(tools, Dock.Bottom); sensors.Children.Add(tools);
   sensorDetails = SensorText(410); sensors.Children.Add(sensorDetails); sensorDrawer = PanelBox(sensors); sensorDrawer.Visibility = Visibility.Collapsed; Place(stage, sensorDrawer, 345, 130, 760, 595);
  }
 }
}






