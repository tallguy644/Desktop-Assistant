using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace JarvisDesk {
 // Holographic look: glowing voice orb with waveform, honeycomb backdrop, glass cards and feature tiles.
 // Colors go through the theme palette: Accent = text/cards/tiles, Rings = halo/waveform/orbits, Core = orb sphere.
 public partial class Desk {
  readonly List<ScaleTransform> waveBars=new List<ScaleTransform>();
  bool waveActive, previewWaveActive;

  // ---------- Voice orb ----------
  Grid VoiceOrb() {
   const double size=560, c=size/2;
   var result=new Grid { Width=size, Height=size, IsHitTestVisible=false };
   coreAura=new Ellipse { Width=520, Height=520, Fill=ThemeRadial(Color.FromArgb(120,40,150,255),Colors.Transparent,ThemeRole.Rings) };result.Children.Add(coreAura);

   // Orbit rings tilted into perspective around the orb.
   result.Children.Add(Orbit(500,-14,0.30,RingInk(120,190,255),1.2,64));
   result.Children.Add(Orbit(430,11,0.26,RingInk(80,160,240),2,-48));
   result.Children.Add(Orbit(372,-4,0.22,RingInk(170,225,255),1,36));

   // Flat HUD rings close to the orb.
   result.Children.Add(Ring(300,ThemeArgb(90,120,190,255,ThemeRole.Rings),1,new DoubleCollection { 2,5 },-90));
   var ticks=new Grid { Width=262,Height=262 };
   for(int i=0;i<4;i++) ticks.Children.Add(Arc(262,128,i*90+8,i*90+62,RingInk(120,200,255),2));
   Spin(ticks,40);result.Children.Add(ticks);

   // Waveform banks either side of the orb.
   result.Children.Add(WaveBank(true,c));result.Children.Add(WaveBank(false,c));

   // Halo and sphere.
   result.Children.Add(new Ellipse { Width=236,Height=236,Fill=ThemeRadial(Color.FromArgb(150,60,170,255),Colors.Transparent,ThemeRole.Rings) });
   result.Children.Add(new Ellipse { Width=206,Height=206,Stroke=RingInk(140,215,255),StrokeThickness=2.5,Effect=themePalette.Glow(Color.FromRgb(70,180,255),ThemeRole.Rings,26,0.95) });
   var sphere=new Ellipse { Width=192,Height=192,Fill=new RadialGradientBrush {
    GradientOrigin=new Point(0.38,0.3),Center=new Point(0.45,0.4),RadiusX=0.62,RadiusY=0.62,
    GradientStops=new GradientStopCollection {
     themePalette.Stop(Color.FromRgb(150,235,255),0,ThemeRole.Core),
     themePalette.Stop(Color.FromRgb(40,150,240),0.38,ThemeRole.Core),
     themePalette.Stop(Color.FromRgb(14,58,140),0.78,ThemeRole.Core),
     themePalette.Stop(Color.FromRgb(6,20,58),1,ThemeRole.Core) } },
    Stroke=ThemeArgb(200,170,235,255,ThemeRole.Core),StrokeThickness=1.2 };
   result.Children.Add(sphere);
   // Soft specular highlight for a glassy sphere.
   result.Children.Add(new Ellipse { Width=110,Height=62,Margin=new Thickness(-40,-104,0,0),Fill=new RadialGradientBrush(Color.FromArgb(90,255,255,255),Color.FromArgb(0,255,255,255)) });

   result.Children.Add(Microphone());
   state=Label("STANDBY",14,cyan);SetCorePulse(false);SetWaveActive(false);return result;
  }
  Grid Orbit(double diameter,double tilt,double squash,Brush brush,double thickness,double seconds) {
   var holder=new Grid { Width=diameter,Height=diameter,RenderTransformOrigin=new Point(0.5,0.5) };
   var group=new TransformGroup();group.Children.Add(new ScaleTransform(1,squash));group.Children.Add(new RotateTransform(tilt));holder.RenderTransform=group;
   var ring=new Grid { Width=diameter,Height=diameter };
   ring.Children.Add(new Ellipse { Stroke=brush,StrokeThickness=thickness,Opacity=0.55 });
   ring.Children.Add(Arc(diameter,diameter/2-1,-40,70,brush,thickness*2.2));
   ring.Children.Add(Arc(diameter,diameter/2-1,150,190,brush,thickness*2.2));
   Spin(ring,seconds);holder.Children.Add(ring);return holder;
  }
  Canvas Microphone() {
   var white=new SolidColorBrush(Color.FromArgb(245,240,252,255));white.Freeze();
   var glyph=new Canvas { Width=80,Height=96,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Effect=themePalette.Glow(Color.FromRgb(150,230,255),ThemeRole.Core,18,0.9) };
   glyph.Children.Add(new System.Windows.Shapes.Path { Fill=white,Data=Geometry.Parse("M 26,30 A 14,14 0 0 1 54,30 L 54,52 A 14,14 0 0 1 26,52 Z") });
   glyph.Children.Add(new System.Windows.Shapes.Path { Stroke=white,StrokeThickness=5,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Data=Geometry.Parse("M 16,50 A 24,24 0 0 0 64,50 M 40,74 L 40,84 M 28,86 L 52,86") });
   return glyph;
  }
  Canvas WaveBank(bool left,double center) {
   const int count=22;const double gap=7.4,barWidth=3.6,start=106,maxHeight=150;
   var bank=new Canvas { Width=560,Height=560,Effect=themePalette.Glow(Color.FromRgb(60,180,255),ThemeRole.Rings,12,0.85) };
   var fill=new LinearGradientBrush { StartPoint=new Point(0.5,0),EndPoint=new Point(0.5,1),GradientStops=new GradientStopCollection {
    themePalette.Stop(Color.FromArgb(60,90,200,255),0,ThemeRole.Rings),themePalette.Stop(Color.FromArgb(255,150,225,255),0.5,ThemeRole.Rings),themePalette.Stop(Color.FromArgb(60,90,200,255),1,ThemeRole.Rings) } };
   for(int i=0;i<count;i++) {
    double distance=start+i*gap;double envelope=Math.Pow(1-(double)i/count,1.4)*0.85+0.15;double height=maxHeight*envelope;
    var scale=new ScaleTransform(1,0.2);waveBars.Add(scale);
    var bar=new Rectangle { Width=barWidth,Height=height,RadiusX=barWidth/2,RadiusY=barWidth/2,Fill=fill,RenderTransformOrigin=new Point(0.5,0.5),RenderTransform=scale,Opacity=1-0.55*i/count };
    Canvas.SetLeft(bar,left?center-distance-barWidth:center+distance);Canvas.SetTop(bar,center-height/2);bank.Children.Add(bar);
   }
   return bank;
  }
  void SetWaveActive(bool active) {
   waveActive=active;var random=new Random(7);
   for(int i=0;i<waveBars.Count;i++) {
    var scale=waveBars[i];var animation=new DoubleAnimationUsingKeyFrames { RepeatBehavior=RepeatBehavior.Forever,AutoReverse=true };
    if(active) {
     animation.Duration=TimeSpan.FromSeconds(0.9+random.NextDouble()*0.7);
     for(int k=0;k<=4;k++) animation.KeyFrames.Add(new EasingDoubleKeyFrame(0.25+random.NextDouble()*0.75,KeyTime.FromPercent(k/4.0)) { EasingFunction=new SineEase() });
    } else {
     double phase=(i%11)/11.0;animation.Duration=TimeSpan.FromSeconds(2.6);
     animation.KeyFrames.Add(new EasingDoubleKeyFrame(0.08+0.06*phase,KeyTime.FromPercent(0)) { EasingFunction=new SineEase() });
     animation.KeyFrames.Add(new EasingDoubleKeyFrame(0.16+0.14*(1-phase),KeyTime.FromPercent(1)) { EasingFunction=new SineEase() });
    }
    animation.Freeze();scale.BeginAnimation(ScaleTransform.ScaleYProperty,animation,HandoffBehavior.SnapshotAndReplace);
   }
  }
  // Called on the HUD timer: the waveform comes alive while listening, thinking or speaking.
  void RefreshWave() {
   bool active=previewWaveActive || continuous || questionListening || chatQuestionInFlight || speaking || pttUntil>DateTime.UtcNow;
   if(active!=waveActive) SetWaveActive(active);
  }

  // ---------- Honeycomb backdrop ----------
  void DrawHoneycomb(Canvas stage) {
   stage.Background=new LinearGradientBrush(Color.FromRgb(2,6,13),Color.FromRgb(3,14,26),90);
   const double radius=30;double w=Math.Sqrt(3)*radius,rowHeight=radius*1.5;
   var outlines=new StreamGeometry();var lit=new StreamGeometry();var random=new Random(21);
   using(var all=outlines.Open()) using(var glow=lit.Open()) {
    for(int row=-1;row*rowHeight<850;row++) for(int col=-1;col*w<1480;col++) {
     double cx=col*w+(row%2==0?0:w/2),cy=row*rowHeight;
     AddHex(all,cx,cy,radius-2);
     if(random.NextDouble()<0.06) AddHex(glow,cx,cy,radius-5);
    }
   }
   outlines.Freeze();lit.Freeze();
   // Brighter toward the edges, fading out behind the orb.
   var mask=new RadialGradientBrush { Center=new Point(0.39,0.52),GradientOrigin=new Point(0.39,0.52),RadiusX=0.62,RadiusY=0.8,GradientStops=new GradientStopCollection { new GradientStop(Color.FromArgb(10,0,0,0),0),new GradientStop(Color.FromArgb(60,0,0,0),0.45),new GradientStop(Color.FromArgb(255,0,0,0),1) } };
   var field=new Canvas { Width=1440,Height=810,IsHitTestVisible=false,OpacityMask=mask,CacheMode=new BitmapCache() };
   field.Children.Add(new System.Windows.Shapes.Path { Data=outlines,Stroke=ThemeArgb(70,0,170,210,ThemeRole.Accent),StrokeThickness=1 });
   field.Children.Add(new System.Windows.Shapes.Path { Data=lit,Fill=ThemeArgb(26,0,200,235,ThemeRole.Accent),Stroke=ThemeArgb(150,0,220,240,ThemeRole.Accent),StrokeThickness=1.2,Effect=themePalette.Glow(Color.FromRgb(0,200,240),ThemeRole.Accent,10,0.7) });
   stage.Children.Add(field);
  }
  static void AddHex(StreamGeometryContext context,double cx,double cy,double r) {
   context.BeginFigure(Polar(cx,cy,r,-90),true,true);
   for(int i=1;i<6;i++) context.LineTo(Polar(cx,cy,r,-90+i*60),true,false);
  }

  // ---------- Glass cards ----------
  Border GlassCard(double radius) {
   return new Border { CornerRadius=new CornerRadius(radius),BorderThickness=new Thickness(1),IsHitTestVisible=false,
    Background=new LinearGradientBrush { StartPoint=new Point(0,0),EndPoint=new Point(0,1),GradientStops=new GradientStopCollection { themePalette.Stop(Color.FromArgb(58,0,120,150),0,ThemeRole.Accent),themePalette.Stop(Color.FromArgb(26,0,50,80),1,ThemeRole.Accent) } },
    BorderBrush=new LinearGradientBrush { StartPoint=new Point(0,0),EndPoint=new Point(1,1),GradientStops=new GradientStopCollection { themePalette.Stop(Color.FromArgb(200,0,230,240),0,ThemeRole.Accent),themePalette.Stop(Color.FromArgb(50,0,140,170),0.5,ThemeRole.Accent),themePalette.Stop(Color.FromArgb(150,0,200,230),1,ThemeRole.Accent) } } };
  }

  // ---------- Feature tiles ----------
  void InitializeTileStyle() {
   Resources["HudTile"]=(Style)System.Windows.Markup.XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='Frame' CornerRadius='12' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Frame' Property='Background' Value='{DynamicResource HudHoverBackground}'/><Setter TargetName='Frame' Property='BorderBrush' Value='{DynamicResource HudHoverBorder}'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Frame' Property='BorderBrush' Value='{DynamicResource HudHoverBorder}'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter TargetName='Frame' Property='Opacity' Value='0.75'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>");
   foreach(string key in new[]{"HudHoverBackground","HudHoverBorder"})((Style)Resources["HudTile"]).Resources[key]=Resources[key];
  }
  Button FeatureTile(string glyph,string title,string tip,Action action) {
   var button=Action(title,action);button.Style=(Style)Resources["HudTile"];
   System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(button,true);
   button.Width=62;button.Height=62;button.Padding=new Thickness(0);button.Margin=new Thickness(0,0,8,0);button.ToolTip=tip;
   button.Background=new LinearGradientBrush { StartPoint=new Point(0,0),EndPoint=new Point(0,1),GradientStops=new GradientStopCollection { themePalette.Stop(Color.FromArgb(70,0,140,170),0,ThemeRole.Accent),themePalette.Stop(Color.FromArgb(28,0,60,90),1,ThemeRole.Accent) } };
   button.BorderBrush=ThemeArgb(170,0,220,235,ThemeRole.Accent);
   var content=new StackPanel { HorizontalAlignment=HorizontalAlignment.Center };
   var icon=new TextBlock { Text=glyph,FontFamily=new FontFamily("Segoe MDL2 Assets"),FontSize=20,Foreground=cyan,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,0,0,5),Effect=themePalette.Glow(Color.FromRgb(0,230,235),ThemeRole.Accent,12,0.9) };
   var label=new TextBlock { Text=title,FontFamily=new FontFamily("Consolas"),FontSize=9,Foreground=cyan,HorizontalAlignment=HorizontalAlignment.Center };
   content.Children.Add(icon);content.Children.Add(label);button.Content=content;button.Tag=label;return button;
  }
  // The listen tile keeps its icon; only its caption follows the listening state.
  void SetListenLabel(string text) {
   var label=listenButton==null?null:listenButton.Tag as TextBlock;
   if(label!=null) label.Text=text.StartsWith("STOP")?"STOP":"LISTEN";
   else if(listenButton!=null) listenButton.Content=text;
  }
 }
}
