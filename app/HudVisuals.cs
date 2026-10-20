using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace JarvisDesk {
 public partial class Desk {
  Ellipse coreAura;
  sealed class ReactorRotor { public RotateTransform Rotation; public double Seconds; public int Index; }
  readonly List<ReactorRotor> reactorRotors=new List<ReactorRotor>();
  bool reactorAccessing,chatQuestionInFlight;
  static Point Polar(double cx, double cy, double radius, double degrees) { double angle = degrees * Math.PI / 180; return new Point(cx + radius * Math.Cos(angle), cy + radius * Math.Sin(angle)); }
  static Polygon Hexagon(double size, Brush stroke, Brush fill, double thickness) {
   var points = new PointCollection(); for (int i = 0; i < 6; i++) points.Add(Polar(size / 2, size / 2, size / 2 - 2, 30 + i * 60));
   return new Polygon { Width = size, Height = size, Points = points, Stroke = stroke, Fill = fill, StrokeThickness = thickness, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
  }
  void Spin(UIElement element, double seconds) {
   // Rasterize each unchanged ring layer once; animate only its transform.
   var cache=new BitmapCache(2) { EnableClearType=false,SnapsToDevicePixels=false };cache.Freeze();element.CacheMode=cache;
   RenderOptions.SetBitmapScalingMode(element,BitmapScalingMode.Linear);
   element.RenderTransformOrigin = new Point(0.5, 0.5); var rotation = new RotateTransform(); element.RenderTransform = rotation;
   var rotor=new ReactorRotor { Rotation=rotation,Seconds=seconds,Index=reactorRotors.Count };reactorRotors.Add(rotor);AnimateRotor(rotor,reactorAccessing);
  }
  void AnimateRotor(ReactorRotor rotor,bool accessing) {
   double angle=rotor.Rotation.Angle;
   if(accessing) {
    double sweep=(rotor.Seconds>0?1:-1)*(24+rotor.Index*11);
    var scan=new DoubleAnimationUsingKeyFrames { Duration=TimeSpan.FromSeconds(2.6+rotor.Index*0.22),RepeatBehavior=RepeatBehavior.Forever };
    scan.KeyFrames.Add(new LinearDoubleKeyFrame(angle,KeyTime.FromPercent(0)));
    scan.KeyFrames.Add(new EasingDoubleKeyFrame(angle+sweep,KeyTime.FromPercent(0.25)) { EasingFunction=new SineEase { EasingMode=EasingMode.EaseInOut } });
    scan.KeyFrames.Add(new EasingDoubleKeyFrame(angle-sweep,KeyTime.FromPercent(0.75)) { EasingFunction=new SineEase { EasingMode=EasingMode.EaseInOut } });
    scan.KeyFrames.Add(new EasingDoubleKeyFrame(angle,KeyTime.FromPercent(1)) { EasingFunction=new SineEase { EasingMode=EasingMode.EaseInOut } });
    scan.Freeze();rotor.Rotation.BeginAnimation(RotateTransform.AngleProperty,scan,HandoffBehavior.SnapshotAndReplace);
   } else {
    var idle=new DoubleAnimation(angle,angle+(rotor.Seconds>0?360:-360),TimeSpan.FromSeconds(Math.Abs(rotor.Seconds))) { RepeatBehavior=RepeatBehavior.Forever };
    idle.Freeze();rotor.Rotation.BeginAnimation(RotateTransform.AngleProperty,idle,HandoffBehavior.SnapshotAndReplace);
   }
  }
  void RefreshReactorActivity() {
   bool accessing=chatQuestionInFlight || questionListening;
   if(accessing!=reactorAccessing) {
    reactorAccessing=accessing;foreach(var rotor in reactorRotors) AnimateRotor(rotor,accessing);
    SetCorePulse(accessing || continuous);
   }
   if(state==null) return;
   if(accessing) state.Text=chatQuestionInFlight?"ACCESSING DATA":"RECEIVING QUESTION";
   else if(state.Text=="ACCESSING DATA" || state.Text=="RECEIVING QUESTION") state.Text=continuous?"LISTENING":"STANDBY";
  }
  System.Windows.Shapes.Path Arc(double size, double radius, double from, double to, Brush brush, double width) {
   var start = Polar(size / 2, size / 2, radius, from); var end = Polar(size / 2, size / 2, radius, to);
   var figure = new PathFigure { StartPoint = start }; figure.Segments.Add(new ArcSegment(end, new Size(radius, radius), 0, to - from > 180, SweepDirection.Clockwise, true));
   var geometry=new PathGeometry(new PathFigure[] { figure });geometry.Freeze();
   return new System.Windows.Shapes.Path { Width = size, Height = size, Data = geometry, Stroke = brush, StrokeThickness = width, StrokeStartLineCap = PenLineCap.Flat, StrokeEndLineCap = PenLineCap.Flat, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
  }
  Grid BuildExtremeReactor() {
   var ice = Ink(127, 249, 255); var blue = Ink(39, 129, 255); var gold = Ink(255, 185, 84);
   var reactor = new Grid { Width = 700, Height = 640, IsHitTestVisible = false };
   coreAura = new Ellipse { Width = 624, Height = 624, Fill = new RadialGradientBrush(Color.FromArgb(125, 8, 124, 191), Colors.Transparent) }; reactor.Children.Add(coreAura);
   var outerMarks = new Canvas { Width = 640, Height = 640, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
   for (int i = 0; i < 144; i++) {
    double angle = i * 2.5; var a = Polar(320, 320, i % 6 == 0 ? 296 : 305, angle); var b = Polar(320, 320, 313, angle);
    Wire(outerMarks, a.X, a.Y, b.X, b.Y, i % 18 == 0 ? gold : i % 6 == 0 ? ice : Ink(39, 91, 118), i % 6 == 0 ? 1.5 : 0.7);
   }
   reactor.Children.Add(outerMarks);
   reactor.Children.Add(Ring(596, Ink(28, 103, 143), 1, null, 0));
   var armor = new Grid { Width = 574, Height = 574 };
   for (int i = 0; i < 12; i++) {
    armor.Children.Add(Arc(574, 274, i * 30 + 2, i * 30 + 25, i % 4 == 0 ? blue : Ink(22, 69, 96), 19));
    armor.Children.Add(Arc(574, 285, i * 30 + 2, i * 30 + 24, i % 4 == 0 ? ice : cyan, 1));
   }
   Spin(armor, 100); reactor.Children.Add(armor);
   reactor.Children.Add(Ring(535, cyan, 2, new DoubleCollection { 1, 4 }, -70));
   var six = new Grid { Width = 516, Height = 516 };
   for (int i = 0; i < 6; i++) {
    six.Children.Add(Arc(516, 251, i * 60 + 5, i * 60 + 50, i % 2 == 0 ? ice : blue, 5));
    six.Children.Add(Arc(516, 242, i * 60 + 5, i * 60 + 47, cyan, 1));
   }
   Spin(six, -38); reactor.Children.Add(six);
   reactor.Children.Add(Ring(462, Ink(14, 45, 69), 17, new DoubleCollection { 1.5, 0.4 }, 0));
   reactor.Children.Add(Ring(444, blue, 2, null, 0));
   var iris = new Grid { Width = 418, Height = 418 };
   for (int i = 0; i < 18; i++) {
    var blade = new Polygon { Points = new PointCollection { Polar(209, 209, 201, i * 20), Polar(209, 209, 201, i * 20 + 13), Polar(209, 209, 176, i * 20 + 22), Polar(209, 209, 176, i * 20 + 7) }, Fill = i % 3 == 0 ? new SolidColorBrush(Color.FromArgb(160, 20, 137, 215)) : Ink(13, 55, 83), Stroke = Ink(35, 117, 148), StrokeThickness = 0.5 };
    iris.Children.Add(blade);
   }
   Spin(iris, 58); reactor.Children.Add(iris);
   var reticle = Hexagon(365, Ink(39, 123, 163), new SolidColorBrush(Color.FromArgb(65, 2, 15, 26)), 1); Spin(reticle, -115); reactor.Children.Add(reticle);
   reactor.Children.Add(Ring(336, ice, 1, null, 0));
   var sweep = new Canvas { Width = 328, Height = 328, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.44 };
   for (int i = 0; i < 24; i++) { var p = Polar(164, 164, 160, -90 + i * 2.8); Wire(sweep, 164, 164, p.X, p.Y, new SolidColorBrush(Color.FromArgb((byte)(8 + i * 2), 43, 223, 255)), 2); }
   Spin(sweep, 12); reactor.Children.Add(sweep);
   var globe = new Grid { Width = 308, Height = 308, Opacity = 0.5 };
   globe.Children.Add(Ring(308, cyan, 1, null, 0));
   foreach (double width in new double[] { 58, 130, 204, 269 }) globe.Children.Add(new Ellipse { Width = width, Height = 308, Stroke = cyan, StrokeThickness = 0.5 });
   foreach (double height in new double[] { 58, 130, 204, 269 }) globe.Children.Add(new Ellipse { Width = 308, Height = height, Stroke = blue, StrokeThickness = 0.6 });
   globe.RenderTransform = new RotateTransform(-17); globe.RenderTransformOrigin = new Point(0.5, 0.5); reactor.Children.Add(globe);
   reactor.Children.Add(new Ellipse { Width = 225, Height = 225, Fill = new RadialGradientBrush(Color.FromArgb(240, 3, 27, 42), Color.FromArgb(0, 2, 20, 37)) });
   var center = new StackPanel { Width = 278, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
   var caption = Label("P E R S O N A L   A S S I S T A N T", 8, cyan); caption.TextAlignment = TextAlignment.Center; center.Children.Add(caption);
   var title = Label(AssistantIdentity.Upper, 37, Brushes.White); title.FontFamily = new FontFamily("Bahnschrift"); title.TextAlignment = TextAlignment.Center; title.Effect = new DropShadowEffect { Color = Colors.Cyan, BlurRadius = 28, ShadowDepth = 0, Opacity = 0.8 }; center.Children.Add(title);
   state = Label("STANDBY", 17, ice); state.TextAlignment = TextAlignment.Center; center.Children.Add(state);
   var caption2 = Label("V O I C E   C O R E", 9, muted); caption2.TextAlignment = TextAlignment.Center; center.Children.Add(caption2); reactor.Children.Add(center);
   var brackets = new Canvas { Width = 700, Height = 640, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
   for (int i = 0; i < 4; i++) {
    double a = i * 90 + 45; var p = Polar(350, 320, 289, a); var shape = new Polygon { Points = new PointCollection { new Point(p.X - 5, p.Y - 5), new Point(p.X + 5, p.Y - 5), new Point(p.X + 5, p.Y + 5), new Point(p.X - 5, p.Y + 5) }, Fill = gold, Effect = new DropShadowEffect { Color = Colors.Orange, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.6 } }; brackets.Children.Add(shape);
   }
   reactor.Children.Add(brackets); SetCorePulse(false); return reactor;
  }
  void SetCorePulse(bool active) {
   if (coreAura == null) return;
   coreAura.BeginAnimation(OpacityProperty, new DoubleAnimation(active ? 0.65 : 0.4, active ? 1 : 0.78, TimeSpan.FromSeconds(active ? 1.6 : 4.5)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
  }
  void AddExtremeScenery(Canvas stage) {
   var subdued = Ink(11, 40, 58); var blue = Ink(22, 97, 159);
   // Perspective deck, circuit traces, and indexing marks are decorative.
   for (int i = -8; i <= 8; i++) Wire(stage, 720 + i * 40, 690, 720 + i * 180, 900, subdued, 0.7);
   foreach (int y in new int[] { 716, 748, 791, 846, 898 }) Wire(stage, 345, y, 1095, y, subdued, 0.6);
   for (int i = 0; i < 11; i++) {
    double y = 148 + i * 54; double inset = i % 2 == 0 ? 0 : 13;
    Wire(stage, 6, y, 20, y, blue, 1); Wire(stage, 20, y, 20 + inset, y + 12, blue, 1);
    Wire(stage, 1420, y, 1434, y, blue, 1);
   }
   foreach (int x in new int[] { 360, 1080 }) {
    Wire(stage, x, 158, x, 214, cyan, 1.5); Wire(stage, x, 660, x, 711, cyan, 1.5);
    for (int i = 0; i < 8; i++) Wire(stage, x - 4, 337 + i * 8, x + 4, 337 + i * 8, subdued, 1);
   }
   var crest = new Canvas { Width = 380, Height = 34 };
   for (int i = 0; i < 30; i++) { var bar = new Rectangle { Width = 4, Height = i % 5 == 0 ? 16 : 7, Fill = i % 5 == 0 ? cyan : blue }; Place(crest, bar, i * 12, 9, 4, i % 5 == 0 ? 16 : 7); }
   Place(stage, crest, 544, 38, 380, 34);
   Place(stage, Label("V O I C E   O P E R A T I O N S", 9, muted), 554, 80, 350, 20);
   var traces = new Canvas { IsHitTestVisible = false };
   foreach (int y in new int[] { 245, 498, 681 }) {
    Wire(traces, 307, y, 350, y, cyan, 1); Wire(traces, 350, y, 395, y + (y > 400 ? -35 : 35), cyan, 1);
    traces.Children.Add(new Ellipse { Width = 5, Height = 5, Fill = cyan, Margin = new Thickness(347, y - 2, 0, 0) });
   }
   traces.Opacity = 0.5; stage.Children.Add(traces);
  }
  void AddPanelDetails(Canvas edge, double width, double height, Brush accent) {
   for (int i = 0; i < 6; i++) Wire(edge, width - 92 + i * 6, 8, width - 89 + i * 6, 14, accent, 1);
   Wire(edge, 20, 44, width - 22, 44, Ink(20, 56, 72), 0.5);
   Wire(edge, 7, 28, 7, height - 28, Ink(16, 51, 70), 0.5);
   for (int i = 0; i < 5; i++) Wire(edge, 16 + i * 8, height - 6, 20 + i * 8, height - 6, accent, 1);
  }
 }
}
