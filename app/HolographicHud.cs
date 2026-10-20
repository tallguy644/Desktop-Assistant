using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace JarvisDesk {
 public partial class Desk {
  LinearGradientBrush HologramSurface(){return new LinearGradientBrush {StartPoint=new Point(0,0),EndPoint=new Point(1,1),GradientStops=new GradientStopCollection {themePalette.Stop(Color.FromRgb(6,20,35),0,ThemeRole.Accent),themePalette.Stop(Color.FromRgb(13,39,61),0.48,ThemeRole.Accent),themePalette.Stop(Color.FromRgb(2,10,21),1,ThemeRole.Accent)}};}
  Border HologramGlass(double width,double height){
   return new Border {Name="HologramGlass",Width=width,Height=height,CornerRadius=new CornerRadius(16),BorderThickness=new Thickness(1),BorderBrush=new LinearGradientBrush {StartPoint=new Point(0,0),EndPoint=new Point(1,1),GradientStops=new GradientStopCollection {themePalette.Stop(Color.FromArgb(220,171,228,255),0,ThemeRole.Accent),themePalette.Stop(Color.FromArgb(65,88,152,201),0.5,ThemeRole.Accent),themePalette.Stop(Color.FromArgb(170,91,195,246),1,ThemeRole.Accent)}},Background=new LinearGradientBrush {StartPoint=new Point(0,0),EndPoint=new Point(0.85,1),GradientStops=new GradientStopCollection {themePalette.Stop(Color.FromArgb(205,39,87,120),0,ThemeRole.Accent),themePalette.Stop(Color.FromArgb(180,15,46,73),0.36,ThemeRole.Accent),themePalette.Stop(Color.FromArgb(210,6,24,43),1,ThemeRole.Accent)}},Effect=themePalette.Glow(Color.FromRgb(61,154,215),ThemeRole.Accent,24,0.35)};
  }
  Grid HologramOrb(){
   var orb=new Grid {Name="HolographicOrb",Width=355,Height=355,IsHitTestVisible=false};
   orb.Children.Add(new Ellipse {Fill=new RadialGradientBrush {GradientOrigin=new Point(0.32,0.22),Center=new Point(0.5,0.5),RadiusX=0.65,RadiusY=0.65,GradientStops=new GradientStopCollection {themePalette.Stop(Color.FromArgb(200,152,222,255),0,ThemeRole.Rings),themePalette.Stop(Color.FromArgb(220,31,123,179),0.24,ThemeRole.Rings),themePalette.Stop(Color.FromArgb(240,3,30,61),0.68,ThemeRole.Rings),themePalette.Stop(Color.FromArgb(180,51,162,225),1,ThemeRole.Rings)}},Stroke=RingInk(117,205,255),StrokeThickness=1.5,Effect=themePalette.Glow(Color.FromRgb(64,175,255),ThemeRole.Rings,30,0.65)});
   var meridians=new Grid {Opacity=0.18};foreach(double width in new[]{110.0,230.0,330.0})meridians.Children.Add(new Ellipse {Width=width,Height=330,Stroke=RingInk(177,228,255),StrokeThickness=0.6});foreach(double height in new[]{85.0,190.0})meridians.Children.Add(new Ellipse {Width=330,Height=height,Stroke=RingInk(177,228,255),StrokeThickness=0.6});orb.Children.Add(meridians);
   return orb;
  }
  void AddHologramOrbits(Grid host){
   foreach(double angle in new[]{-24.0,25.0}){
    var orbit=new Ellipse {Name=angle<0?"HologramOrbitFront":"HologramOrbitBack",Width=546,Height=178,Stroke=ThemeArgb(190,150,220,255,ThemeRole.Rings),StrokeThickness=angle<0?2.5:1,RenderTransformOrigin=new Point(0.5,0.5),RenderTransform=new RotateTransform(angle),IsHitTestVisible=false,Effect=themePalette.Glow(Color.FromRgb(99,195,255),ThemeRole.Rings,12,0.6)};host.Children.Add(orbit);
    var light=new Ellipse {Width=546,Height=178,Stroke=RingInk(212,242,255),StrokeThickness=3,StrokeDashArray=new DoubleCollection {18,190},RenderTransformOrigin=new Point(0.5,0.5),RenderTransform=new RotateTransform(angle),IsHitTestVisible=false};var motion=new DoubleAnimation(0,208,TimeSpan.FromSeconds(angle<0?19:27)) {RepeatBehavior=RepeatBehavior.Forever};motion.Freeze();light.BeginAnimation(Shape.StrokeDashOffsetProperty,motion);host.Children.Add(light);
   }
  }
  void DrawHolographicStage(Canvas stage){
   stage.Background=HologramSurface();
   var layer=new Canvas {Name="HolographicProjection",Width=1440,Height=810,IsHitTestVisible=false};
   var haze=new Ellipse {Fill=ThemeRadial(Color.FromArgb(78,18,93,173),Colors.Transparent,ThemeRole.Rings)};Place(layer,haze,210,90,790,680);
   var rightHaze=new Ellipse {Fill=ThemeRadial(Color.FromArgb(35,18,128,185),Colors.Transparent,ThemeRole.Accent)};Place(layer,rightHaze,805,30,620,690);
   var floor=new Canvas {Opacity=0.27,CacheMode=new BitmapCache()};for(int i=0;i<13;i++)Wire(floor,562,594,80+i*100,810,Ink(22,85,122),0.6);for(int i=0;i<6;i++){double y=635+i*i*6;Wire(floor,70,y,1410,y,Ink(16,72,107),0.6);}layer.Children.Add(floor);
   var beam=new Polygon {Points=new PointCollection {new Point(562,663),new Point(364,324),new Point(760,324)},Fill=new LinearGradientBrush {StartPoint=new Point(0.5,1),EndPoint=new Point(0.5,0),GradientStops=new GradientStopCollection {themePalette.Stop(Color.FromArgb(50,77,185,250),0,ThemeRole.Rings),themePalette.Stop(Color.FromArgb(2,70,163,235),1,ThemeRole.Rings)}}};layer.Children.Add(beam);
   foreach(double factor in new[]{1.0,0.78,0.55}){
    double width=420*factor,height=100*factor;
    var orbit=new Ellipse {Width=width,Height=height,Stroke=ThemeArgb(150,110,199,255,ThemeRole.Rings),StrokeThickness=factor==1?1.4:0.8};Place(layer,orbit,562-width/2,659-height/2,width,height);
    var sweep=new Ellipse {Width=width,Height=height,Stroke=RingInk(160,218,255),StrokeThickness=2,StrokeDashArray=new DoubleCollection {13,87},Opacity=0.7};Place(layer,sweep,562-width/2,659-height/2,width,height);
    var motion=new DoubleAnimation(0,100,TimeSpan.FromSeconds(18+factor*12)) {RepeatBehavior=RepeatBehavior.Forever};motion.Freeze();sweep.BeginAnimation(Shape.StrokeDashOffsetProperty,motion);
   }
   var source=new Ellipse {Fill=ThemeRadial(Color.FromArgb(150,114,210,255),Colors.Transparent,ThemeRole.Rings)};Place(layer,source,466,633,192,52);
   Wire(layer,420,663,367,588,ThemeArgb(90,148,210,255,ThemeRole.Rings),1);Wire(layer,704,663,757,588,ThemeArgb(90,148,210,255,ThemeRole.Rings),1);
   for(int i=0;i<24;i++){double angle=i*Math.PI/12;double x=562+210*Math.Cos(angle),y=659+50*Math.Sin(angle);Wire(layer,x,y,562+218*Math.Cos(angle),659+55*Math.Sin(angle),RingInk(86,151,199),0.8);}
   stage.Children.Add(layer);
  }
 }
}
