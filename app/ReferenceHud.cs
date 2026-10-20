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
   Resources.Add(typeof(Button), (Style)System.Windows.Markup.XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border CornerRadius='6' x:Name='Frame' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Frame' Property='Background' Value='{DynamicResource HudHoverBackground}'/><Setter TargetName='Frame' Property='BorderBrush' Value='{DynamicResource HudHoverBorder}'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Frame' Property='BorderBrush' Value='White'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>"));
   Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar), (Style)System.Windows.Markup.XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ScrollBar'><Setter Property='Width' Value='4'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'><Grid Background='Transparent'><Track x:Name='PART_Track' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' IsDirectionReversed='True'><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='Thumb'><Border Background='{DynamicResource HudScrollThumb}'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb></Track></Grid></ControlTemplate></Setter.Value></Setter></Style>"));
   foreach(string key in new[]{"HudHoverBackground","HudHoverBorder","HudScrollThumb"})((Style)Resources[typeof(Button)]).Resources[key]=Resources[key];
   ((Style)Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)]).Resources["HudScrollThumb"]=Resources["HudScrollThumb"];
   var stage = new Canvas { Width = 1440, Height = 810, Background = Brushes.Black, ClipToBounds = true };
   Content = new Viewbox { Stretch = Stretch.Uniform, Child = stage };
   hudTaskbarTab=CompactButton("TASKBAR ↑",RevealHudTaskbar);hudTaskbarTab.Visibility=Visibility.Collapsed;hudTaskbarTab.FontSize=9;hudTaskbarTab.Padding=new Thickness(2,0,2,0);hudTaskbarTab.ToolTip="Hover or click to show this monitor's taskbar.";hudTaskbarTab.MouseEnter+=delegate{try{RevealHudTaskbar();}catch(Exception ex){Log(ex.Message);}};
   DrawCircuitField(stage);
   DrawHolographicStage(stage);
   Place(stage,CompactButton("SETUP",OpenSettings),1130,32,90,28);
   Place(stage,CompactButton("EXPAND / F11",ToggleDeckSize),1230,32,128,28);
   Place(stage,CompactButton("_",MinimizeDeck),1366,32,26,28);
   Place(stage,CompactButton("×",Close),1400,32,26,28);
   CreateCalendarPanel(stage);
   clock = Label(DateTime.Now.ToString("HH:mm:ss"), 27, cyan); Place(stage, clock, 76, 42, 200, 42);
   var cpuGauge = PercentDial("CPU", out cpuLoad, out cpuDialArc, 138); Place(stage, cpuGauge, 308, 53, 138, 138);
   var ramGauge = PercentDial("RAM", out ramStatus, out ramDialArc, 138); Place(stage, ramGauge, 465, 53, 138, 138);
   ramDetails = Label("", 11, cyan); Place(stage, ramDetails, 466, 29, 176, 24);
   CreateNextUpDial(stage);
   var thermal = BuildThermalReadings();
   Place(stage, ReferenceSection("THERMAL READINGS · °F", thermal, 230, 225), 76, 366, 230, 225);
   var memory = new StackPanel(); memory.Children.Add(Label("LIVE CPU / GPU / RAM LOAD", 10, muted));
   cpuBar = new Rectangle { Height = 3, Fill = cyan, Width = 0, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 7) }; gpuBar = new Rectangle { Height = 3, Fill = cyan, Width = 0, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 7) }; ramBar = new Rectangle { Height = 3, Fill = cyan, Width = 0, HorizontalAlignment = HorizontalAlignment.Left }; memory.Children.Add(cpuBar); memory.Children.Add(gpuBar); memory.Children.Add(ramBar); Place(stage, memory, 77, 604, 230, 51);
   var reactor = new Viewbox { Child = ReferenceReactor(), Stretch = Stretch.Uniform }; Place(stage, reactor, 327, 185, 470, 470);
   state.TextAlignment = TextAlignment.Center; Place(stage, state, 459, 676, 220, 25);
   var gpuGauge = PercentDial("GPU LOAD", out gpuLoad, out gpuDialArc, 158); Place(stage, gpuGauge, 619, 43, 158, 158);
   var cooling = new StackPanel(); coolingSpeeds = SensorText(122); coolingSpeeds.Foreground = cyan; cooling.Children.Add(coolingSpeeds); cooling.Children.Add(Label("FANS / PUMP SENSORS\nCHANNEL ASSIGNMENT UNCONFIRMED", 9, muted)); Place(stage, ReferenceSection("COOLING / RPM", cooling, 242, 209), 1160, 90, 242, 209);
   var board = new StackPanel(); boardTemps = SensorText(117); boardTemps.Foreground = cyan; board.Children.Add(boardTemps); Place(stage, ReferenceSection("MOTHERBOARD / °F", board, 242, 165), 1160, 319, 242, 165);
   var connection = new StackPanel(); sensorState = Label("CONNECTING…", 11, cyan); connection.Children.Add(sensorState); sensorTime = Label("", 10, muted); connection.Children.Add(sensorTime); voiceInfo = Label("", 9, muted); connection.Children.Add(voiceInfo); Place(stage, ReferenceSection("LOCAL SYSTEM LINK", connection, 242, 138), 1160, 504, 242, 138);
   Place(stage, Label("CPU / GPU  ·  LIVE THERMAL HISTORY", 11, cyan), 923, 657, 480, 27);
   var cpuPlot = new Canvas { Background = ThemeArgb(20,0,99,110,ThemeRole.Accent) }; cpuTrend = new Polyline { Stroke = cyan, StrokeThickness = 1.3 }; cpuPlot.Children.Add(cpuTrend); Place(stage, cpuPlot, 929, 686, 260, 48);
   var gpuPlot = new Canvas { Background = Brushes.Transparent }; gpuTrend = new Polyline { Stroke = Ink(0, 145, 154), StrokeThickness = 1.3 }; gpuPlot.Children.Add(gpuTrend); Place(stage, gpuPlot, 929, 733, 260, 48);
   Wire(stage, 929, 728, 1402, 728, Ink(0, 70, 78), 1); Wire(stage, 929, 780, 1402, 780, Ink(0, 70, 78), 1);
   Place(stage, Label("CPU", 10, cyan), 1220, 700, 70, 20); Place(stage, Label("GPU", 10, muted), 1220, 748, 70, 20);
   var controls = new WrapPanel(); listenButton = CompactButton("◉  LISTEN", ToggleListen); listenButton.Width = 150; controls.Children.Add(listenButton); controls.Children.Add(CompactButton("◉  PUSH TO TALK / F8", PushToTalk)); Place(stage, controls, 502, 707, 410, 42);
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
   var button = Action(title, action); System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(button,true); button.Background = ThemeArgb(115,13,43,65,ThemeRole.Accent); button.BorderBrush = ThemeArgb(90,110,189,235,ThemeRole.Accent); button.BorderThickness = new Thickness(1); button.Padding = new Thickness(4, 5, 4, 5); button.Margin = new Thickness(0, 0, 3, 3); button.Width = Math.Max(34, title.Length * 7 + 16); button.FontSize = 12; button.Foreground = cyan; return button;
  }
  void ToggleDeckSize() { if(IsDesktop) { DisableDesktop(true);Activate();return; } WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }
  void OpenSensorOverlay() { OpenUnifiedSettings("sensors"); }
  void OpenAppOverlay() { OpenUnifiedSettings("apps"); }
  void LaunchMapped(string name) { var map = maps.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase)); if (map == null) { OpenAppOverlay(); Log("Add a mapping for " + name + " to use this launcher."); } else Launch(map); }
  Grid ReferenceSection(string title, UIElement content, double width, double height) {
   TextBlock heading;return ReferenceSection(title,content,width,height,out heading);
  }
  Grid ReferenceSection(string title, UIElement content, double width, double height,out TextBlock heading) {
   var result=new Grid {Width=width,Height=height};result.Children.Add(new Border {Name="HologramBacking",Width=width,Height=height,CornerRadius=new CornerRadius(16),BorderThickness=new Thickness(1),BorderBrush=ThemeArgb(65,166,217,255,ThemeRole.Accent),Background=ThemeArgb(12,94,178,230,ThemeRole.Accent),RenderTransform=new TranslateTransform(7,-7),IsHitTestVisible=false});result.Children.Add(HologramGlass(width,height));var sheen=new Rectangle {Height=height*0.48,RadiusX=16,RadiusY=16,VerticalAlignment=VerticalAlignment.Top,Fill=new LinearGradientBrush {StartPoint=new Point(0,0),EndPoint=new Point(0.8,1),GradientStops=new GradientStopCollection {themePalette.Stop(Color.FromArgb(30,211,242,255),0,ThemeRole.Accent),themePalette.Stop(Colors.Transparent,1,ThemeRole.Accent)}},IsHitTestVisible=false};result.Children.Add(sheen);
   var highlights=new Canvas {IsHitTestVisible=false};Wire(highlights,14,1,width*0.48,1,ThemeArgb(175,151,225,255,ThemeRole.Accent),1.3);Wire(highlights,1,18,1,48,cyan,2);result.Children.Add(highlights);
   var text=new DockPanel {Margin=new Thickness(12,8,12,8)};heading=Label(title,11,cyan);heading.FontFamily=new FontFamily("Segoe UI");heading.FontWeight=FontWeights.SemiBold;DockPanel.SetDock(heading,Dock.Top);text.Children.Add(heading);text.Children.Add(content);result.Children.Add(text);return result;
  }
  Grid PercentDial(string caption, out TextBlock value, out System.Windows.Shapes.Path arc, double size) {
   var result = new Grid { Width = size, Height = size }; result.Children.Add(Ring(size - 5, RingInk(30,80,123), 6, null, 0)); result.Children.Add(Ring(size - 22, RingInk(30,60,90), 1, null, 0));
   arc = Arc(size, size / 2 - 3, -90, -89.9, RingInk(144,218,255), 6); result.Children.Add(arc);
   var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center }; var label = Label(caption, 11, cyan); label.TextAlignment = TextAlignment.Center; text.Children.Add(label); value = Label("—", 28, cyan); value.FontFamily = new FontFamily("Bahnschrift Condensed"); value.TextAlignment = TextAlignment.Center; value.Margin = new Thickness(0); text.Children.Add(value); result.Children.Add(text); return result;
  }
  void UpdateDial(System.Windows.Shapes.Path arc, double? percent, double size) {
   arc.Visibility = percent.HasValue && percent.Value > 0 ? Visibility.Visible : Visibility.Hidden; if (!percent.HasValue) return;
   double span = Math.Max(0.001, Math.Min(99.999, percent.Value)) * 3.6;
   arc.Data = Arc(size, size / 2 - 3, -90, -90 + span, cyan, 10).Data;
  }
  void UpdateReferenceClock() {
   var now = DateTime.Now;
   long second=now.Ticks/TimeSpan.TicksPerSecond;if(second==referenceClockSecond) return;referenceClockSecond=second;
   if(clock!=null) clock.Text=now.ToString("HH:mm:ss");
  }
  Grid ReferenceReactor() {
   var result = new Grid { Width = 560, Height = 560, IsHitTestVisible = false };
   coreAura = new Ellipse { Width = 540, Height = 540, Fill = ThemeRadial(Color.FromArgb(105,70,130,210),Colors.Transparent,ThemeRole.Rings) }; result.Children.Add(coreAura);
   result.Children.Add(Ring(526, RingInk(80, 119, 157), 2, null, 0));
   var outer = new Grid { Width = 516, Height = 516,Opacity=0.27 };
   outer.Children.Add(Arc(516, 254, -118, -29, RingInk(116, 179, 230), 3)); outer.Children.Add(Arc(516, 254, -19, 67, RingInk(147, 201, 241), 2)); outer.Children.Add(Arc(516, 254, 83, 177, RingInk(107, 162, 207), 3)); outer.Children.Add(Arc(516, 254, 194, 231, RingInk(134, 189, 230), 2));
   outer.Children.Add(Arc(516, 245, -97, 22, RingInk(38, 65, 93), 13)); outer.Children.Add(Arc(516, 245, 39, 145, RingInk(58, 92, 129), 13)); outer.Children.Add(Arc(516, 245, 166, 221, RingInk(51, 79, 112), 11));
   var ports = new Canvas { Width = 516, Height = 516 };
   foreach (double angle in new double[] { -91, -79, -67, -55, -9, 4, 17, 62, 75, 88, 101, 114, 127, 173, 185, 197 }) {
    var p = Polar(258, 258, 245, angle); var slot = new Rectangle { Fill = RingInk(8, 18, 34), Stroke = RingInk(107, 162, 207), StrokeThickness = 0.7, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new RotateTransform(angle + 90) }; Place(ports, slot, p.X - 10, p.Y - 3, 20, 6);
   }
   outer.Children.Add(ports); Spin(outer, 100); result.Children.Add(outer);
   var ticks = new Canvas { Width = 486, Height = 486, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
   for (int i = 0; i < 72; i++) { var a = Polar(243, 243, i % 3 == 0 ? 217 : 224, i * 5); var b = Polar(243, 243, 236, i * 5); Wire(ticks, a.X, a.Y, b.X, b.Y, i % 3 == 0 ? RingInk(185, 220, 255) : RingInk(98, 139, 180), i % 3 == 0 ? 1.2 : 0.65); }
   result.Children.Add(ticks);
   result.Children.Add(Ring(410, RingInk(38, 65, 93), 20, null, 0));
   var casing = new Grid { Width = 410, Height = 410,Opacity=0.36 };
   casing.Children.Add(Arc(410, 198, -110, 84, ThemeArgb(155,140,188,235,ThemeRole.Rings), 15)); casing.Children.Add(Arc(410, 198, 109, 228, RingInk(89, 133, 181), 15));
   casing.Children.Add(Arc(410, 208, -61, 121, RingInk(163, 205, 242), 1)); casing.Children.Add(Arc(410, 186, -120, 69, RingInk(189, 221, 250), 1)); Spin(casing, 82); result.Children.Add(casing);
   result.Children.Add(HologramOrb());
   var mainBand = new Grid { Width = 370, Height = 370,Opacity=0.3 };
   mainBand.Children.Add(Arc(370, 174, -83, 118, RingInk(165, 215, 255), 18)); mainBand.Children.Add(Arc(370, 174, 127, 157, RingInk(133, 179, 223), 18)); mainBand.Children.Add(Arc(370, 174, 164, 232, RingInk(97, 144, 189), 16));
   mainBand.Children.Add(Arc(370, 185, -91, 105, RingInk(181, 215, 245), 1));
   var bandPorts = new Canvas { Width = 370, Height = 370 };
   foreach (double angle in new double[] { -55, -20, 12, 45, 80, 109 }) { var p = Polar(185, 185, 173, angle); var notch = new Rectangle { Fill = RingInk(32, 63, 98), RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new RotateTransform(angle + 90) }; Place(bandPorts, notch, p.X - 2, p.Y - 5, 4, 10); }
   mainBand.Children.Add(bandPorts); Spin(mainBand, -43); result.Children.Add(mainBand);
   result.Children.Add(Ring(317, RingInk(48, 75, 108), 5, null, 0));
   var internalTracks = new Grid { Width = 302, Height = 302 };
   internalTracks.Children.Add(Arc(302, 146, -80, 24, RingInk(157, 203, 246), 3)); internalTracks.Children.Add(Arc(302, 146, 39, 152, RingInk(113, 158, 205), 3)); internalTracks.Children.Add(Arc(302, 146, 163, 267, RingInk(138, 187, 231), 3));
   internalTracks.Children.Add(Arc(302, 135, -95, 12, RingInk(79, 114, 158), 2)); internalTracks.Children.Add(Arc(302, 135, 50, 137, RingInk(165, 200, 239), 2)); internalTracks.Children.Add(Arc(302, 135, 170, 241, RingInk(96, 140, 184), 2)); Spin(internalTracks, 52); result.Children.Add(internalTracks);
   result.Children.Add(Ring(245, RingInk(62, 90, 130), 8, null, 0)); result.Children.Add(Ring(227, RingInk(175, 210, 249), 1, null, 0)); result.Children.Add(Ring(213, RingInk(117, 153, 207), 3, new DoubleCollection { 48, 3, 13, 2 }, 36)); result.Children.Add(Ring(193, RingInk(197, 225, 255), 2, null, 0));
   result.Children.Add(new Ellipse { Width = 194, Height = 194, Fill = ThemeRadial(Color.FromArgb(210,245,24,38),Colors.Transparent,ThemeRole.Core), Effect = themePalette.Glow(Colors.Red,ThemeRole.Core,30,0.4) });
   var core = new Grid { Width = 142, Height = 142 }; core.Children.Add(new Ellipse { Fill = ThemeRadial(Color.FromRgb(246,65,70),Color.FromRgb(116,4,18),ThemeRole.Core), Stroke = CoreInk(242, 35, 48), StrokeThickness = 1 });
   var corePlates = new Grid { Width = 130, Height = 130 };
   for (int i = 0; i < 6; i++) corePlates.Children.Add(Arc(130, 56, i * 60 + 5, i * 60 + 43, i % 2 == 0 ? CoreInk(174, 14, 32) : CoreInk(224, 30, 44), 11));
   Spin(corePlates, -26); core.Children.Add(corePlates);
   var orb = new Ellipse { Width = 57, Height = 57, Fill = new RadialGradientBrush { Center = new Point(0.4, 0.3), GradientOrigin = new Point(0.35, 0.2), GradientStops = new GradientStopCollection { themePalette.Stop(Color.FromRgb(165,29,38),0,ThemeRole.Core), themePalette.Stop(Color.FromRgb(105,5,20),0.5,ThemeRole.Core), themePalette.Stop(Color.FromRgb(45,1,10),1,ThemeRole.Core) } }, Stroke = CoreInk(158, 24, 40), StrokeThickness = 1.5, Effect = themePalette.Glow(Colors.Red,ThemeRole.Core,14,0.5) }; core.Children.Add(orb); result.Children.Add(core);
   // Separate accent rotors stay independent of the cyan ring assemblies.
   AddRedReactorAccent(result,"RedAccentPrimary",380,185,197,275,-19,2,252);
   AddRedReactorAccent(result,"RedAccentOuter",478,232,-65,-29,31,1.5,220);
   AddRedReactorAccent(result,"RedAccentMiddle",328,159,45,94,27,1.3,235);
   AddRedReactorAccent(result,"RedAccentInner",228,109,133,162,-13,1.5,216);
   AddHologramOrbits(result);state = Label("STANDBY", 14, cyan); SetCorePulse(false); return result;
  }
  void AddRedReactorAccent(Grid reactor,string name,double size,double radius,double from,double to,double seconds,double thickness,byte red) {
   var accent=new Grid { Name=name,Width=size,Height=size,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false };
   accent.Children.Add(Arc(size,radius,from,to,CoreInk(red,24,36),thickness));
   accent.Children.Add(Arc(size,radius,to+6,to+10,CoreInk(red,45,48),thickness+0.5));
   Spin(accent,seconds);reactor.Children.Add(accent);
  }

  void DrawCircuitField(Canvas stage) {
   var field = new Canvas { IsHitTestVisible = false, CacheMode = new BitmapCache() };
   var paths = new Point[][] {
    new Point[] { new Point(0, 110), new Point(145, 110), new Point(168, 88), new Point(630, 88) },
    new Point[] { new Point(0, 255), new Point(240, 255), new Point(265, 278), new Point(420, 278), new Point(449, 250), new Point(836, 250) },
    new Point[] { new Point(0, 342), new Point(300, 342), new Point(324, 365), new Point(420, 365) },
    new Point[] { new Point(0, 491), new Point(239, 491), new Point(267, 520), new Point(428, 520) },
    new Point[] { new Point(0, 587), new Point(326, 587), new Point(353, 616), new Point(489, 616) },
    new Point[] { new Point(0, 718), new Point(238, 718), new Point(282, 749), new Point(443, 749) },
    new Point[] { new Point(765, 193), new Point(821, 165), new Point(1124, 165), new Point(1145, 145), new Point(1440, 145) },
    new Point[] { new Point(892, 326), new Point(1035, 326), new Point(1060, 349), new Point(1440, 349) },
    new Point[] { new Point(896, 460), new Point(956, 442), new Point(1440, 442) },
    new Point[] { new Point(839, 580), new Point(1114, 580), new Point(1139, 601), new Point(1440, 601) },
    new Point[] { new Point(851, 643), new Point(903, 622), new Point(1440, 622) }
   };
   foreach (var points in paths) { var trace = new Polyline { Points = new PointCollection(points), Stroke = Ink(0, 91, 115), StrokeThickness = 1, Opacity = 0.75, Effect = themePalette.Glow(Colors.DeepSkyBlue,ThemeRole.Accent,7,0.8) }; field.Children.Add(trace); var end = points[points.Length - 1]; Place(field, new Ellipse { Fill = cyan, Effect = themePalette.Glow(Colors.Cyan,ThemeRole.Accent,10,0.8) }, end.X - 4, end.Y - 4, 8, 8); }
   var random = new Random(44); for (int i = 0; i < 80; i++) { double size = i % 7 == 0 ? 6 : 3; Place(field, new Ellipse { Fill = i % 3 == 0 ? Ink(0, 190, 152) : Ink(0, 96, 112), Opacity = 0.5 }, random.Next(70, 1430), random.Next(60, 795), size, size); }
   field.Opacity = 0.08; stage.Children.Add(field);
  }
  void CreateReferenceOverlays(Canvas stage) {
   var appHost = new DockPanel();appHost.Children.Add(BuildAppPanel());appDrawer=PanelBox(appHost);appDrawer.Visibility=Visibility.Collapsed;Place(stage,appDrawer,345,130,760,595);
   var sensors = new DockPanel();
   var tools = new WrapPanel(); tools.Children.Add(Action("DOWNLOAD MONITOR", delegate { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/releases", UseShellExecute = true }); })); tools.Children.Add(Action("COPY DIAGNOSTICS",CopyDiagnostics)); DockPanel.SetDock(tools, Dock.Bottom); sensors.Children.Add(tools);
   sensorDetails = SensorText(410); sensors.Children.Add(sensorDetails); sensorDrawer = PanelBox(sensors); sensorDrawer.Visibility = Visibility.Collapsed; Place(stage, sensorDrawer, 345, 130, 760, 595);
  }
 }
}






