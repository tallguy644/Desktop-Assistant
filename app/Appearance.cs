using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace JarvisDesk {
 [DataContract] public sealed class ThemePreferences {
  [DataMember] public string Accent="#00E6EB";
  [DataMember] public string Rings="#A5D7FF";
  [DataMember] public string Core="#F22330";
  public static string Hex(string value){value=(value??"").Trim();if(!value.StartsWith("#"))value="#"+value;if(!Regex.IsMatch(value,"^#[0-9a-fA-F]{6}$"))throw new ArgumentException("Enter a six-digit color, such as #66CCFF.");return value.ToUpperInvariant();}
  public void Validate(){Accent=Hex(Accent);Rings=Hex(Rings);Core=Hex(Core);}
  public ThemePreferences Copy(){return new ThemePreferences {Accent=Accent,Rings=Rings,Core=Core};}
  public static ThemePreferences Load(string path){try{using(var file=File.OpenRead(path)){var value=(ThemePreferences)new DataContractJsonSerializer(typeof(ThemePreferences)).ReadObject(file);value.Validate();return value;}}catch{return new ThemePreferences();}}
  public void Save(string path){Validate();Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";using(var file=File.Create(temp))new DataContractJsonSerializer(typeof(ThemePreferences)).WriteObject(file,this);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
 }
 public enum ThemeRole {Accent,Rings,Core,Fixed}
 public sealed class ThemeSwatch : DependencyObject {
  public static readonly DependencyProperty ValueProperty=DependencyProperty.Register("Value",typeof(Color),typeof(ThemeSwatch));
  public Color Value {get{return (Color)GetValue(ValueProperty);}set{SetValue(ValueProperty,value);}}
 }
 public sealed class ThemePalette {
  sealed class Entry {public Color Original;public ThemeRole Role;public ThemeSwatch Swatch;public SolidColorBrush Brush;}
  readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>();ThemePreferences preferences;
  public ThemePalette(ThemePreferences value){preferences=value.Copy();preferences.Validate();}
  static Color Parse(string value){return (Color)ColorConverter.ConvertFromString(value);}
  public static Color Tint(Color original,Color choice){
   double peak=Math.Max(original.R,Math.Max(original.G,original.B));if(peak==0)return original;
   double lowest=Math.Min(original.R,Math.Min(original.G,original.B));double pale=lowest/peak*0.42;
   double brightness=peak/255.0;double chosenPeak=Math.Max(choice.R,Math.Max(choice.G,choice.B));
   // Keep very dark choices readable on the black HUD while preserving their hue.
   double lift=chosenPeak<145?145-chosenPeak:0;
   Func<byte,byte> channel=c=>(byte)Math.Max(0,Math.Min(255,((c+lift)*(1-pale)+255*pale)*brightness));
   return Color.FromArgb(original.A,channel(choice.R),channel(choice.G),channel(choice.B));
  }
  Color Map(Color original,ThemeRole role){if(role==ThemeRole.Fixed)return original;string value=role==ThemeRole.Core?preferences.Core:role==ThemeRole.Rings?preferences.Rings:preferences.Accent;string initial=role==ThemeRole.Core?"#F22330":role==ThemeRole.Rings?"#A5D7FF":"#00E6EB";return value==initial?original:Tint(original,Parse(value));}
  Entry Get(Color original,ThemeRole role){string key=((int)role)+":"+original.ToString();Entry entry;if(entries.TryGetValue(key,out entry))return entry;entry=new Entry {Original=original,Role=role,Swatch=new ThemeSwatch {Value=Map(original,role)}};entries.Add(key,entry);return entry;}
  void Bind(DependencyObject target,DependencyProperty property,Entry entry){BindingOperations.SetBinding(target,property,new Binding("Value"){Source=entry.Swatch,Mode=BindingMode.OneWay});}
  public SolidColorBrush Brush(Color original,ThemeRole role){var entry=Get(original,role);if(entry.Brush==null){entry.Brush=new SolidColorBrush();Bind(entry.Brush,SolidColorBrush.ColorProperty,entry);}return entry.Brush;}
  public GradientStop Stop(Color original,double offset,ThemeRole role){var stop=new GradientStop {Offset=offset};Bind(stop,GradientStop.ColorProperty,Get(original,role));return stop;}
  public DropShadowEffect Glow(Color original,ThemeRole role,double blur,double opacity){var effect=new DropShadowEffect {BlurRadius=blur,Opacity=opacity,ShadowDepth=0};Bind(effect,DropShadowEffect.ColorProperty,Get(original,role));return effect;}
  public void Apply(ThemePreferences value){var copy=value.Copy();copy.Validate();preferences=copy;foreach(var entry in entries.Values)entry.Swatch.Value=Map(entry.Original,entry.Role);}
 }
 public partial class Desk {
  ThemePalette themePalette;ThemePreferences themePreferences;
  string ThemePath {get{return Path.Combine(dataDir,"appearance.json");}}
  void InitializeAppearance(){themePreferences=ThemePreferences.Load(ThemePath);themePalette=new ThemePalette(themePreferences);cyan=themePalette.Brush(Color.FromRgb(0,230,235),ThemeRole.Accent);muted=themePalette.Brush(Color.FromRgb(57,157,166),ThemeRole.Accent);Resources["HudHoverBackground"]=Ink(22,62,67);Resources["HudHoverBorder"]=cyan;Resources["HudScrollThumb"]=Ink(0,99,107);}
  Brush RingInk(byte r,byte g,byte b){return themePalette.Brush(Color.FromRgb(r,g,b),ThemeRole.Rings);}
  Brush CoreInk(byte r,byte g,byte b){return themePalette.Brush(Color.FromRgb(r,g,b),ThemeRole.Core);}
  Brush ThemeArgb(byte a,byte r,byte g,byte b,ThemeRole role){return themePalette.Brush(Color.FromArgb(a,r,g,b),role);}
  RadialGradientBrush ThemeRadial(Color first,Color last,ThemeRole role){return new RadialGradientBrush {GradientStops=new GradientStopCollection {themePalette.Stop(first,0,role),themePalette.Stop(last,1,role)}};}
  public void OpenAppearanceSettings(){
   var before=themePreferences.Copy();bool saved=false;
   var dialog=new Window {Title="Appearance / colors",Owner=this,Width=610,Height=555,MinHeight=510,MinWidth=550,WindowStartupLocation=WindowStartupLocation.CenterScreen,Background=Ink(3,13,20),Foreground=cyan};dialog.Resources.Add(typeof(Button),Resources[typeof(Button)]);
   var form=new StackPanel {Margin=new Thickness(24)};dialog.Content=new ScrollViewer {Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};form.Children.Add(Label("MAKE IT YOURS",23,cyan));form.Children.Add(Label("Choose each color with the picker or enter a hex code. Very dark colors are lifted for readability. Preview keeps your chat, sensors and animations running.",12,muted));
   Func<string,string,TextBox> row=delegate(string title,string value){form.Children.Add(Label(title,11,cyan));var line=new DockPanel {Margin=new Thickness(0,0,0,10)};var box=new TextBox {Text=value,Width=145,FontSize=16,Padding=new Thickness(6),MaxLength=7,HorizontalAlignment=HorizontalAlignment.Left};var pick=Action("PICK COLOR",delegate{try{using(var picker=new System.Windows.Forms.ColorDialog {FullOpen=true,AnyColor=true}){var color=(Color)ColorConverter.ConvertFromString(ThemePreferences.Hex(box.Text));picker.Color=System.Drawing.Color.FromArgb(color.R,color.G,color.B);if(picker.ShowDialog(new HudColorOwner(new System.Windows.Interop.WindowInteropHelper(dialog).Handle))==System.Windows.Forms.DialogResult.OK)box.Text=String.Format("#{0:X2}{1:X2}{2:X2}",picker.Color.R,picker.Color.G,picker.Color.B);}}catch(Exception ex){MessageBox.Show(dialog,ex.Message,"Color");}});pick.Margin=new Thickness(10,0,0,0);DockPanel.SetDock(box,Dock.Left);line.Children.Add(box);line.Children.Add(pick);form.Children.Add(line);return box;};
   var accent=row("HUD ACCENT / TEXT AND CIRCUITS",before.Accent);var rings=row("ORB HALO / WAVEFORM / ORBITS",before.Rings);var core=row("ORB CORE",before.Core);
   var presets=new WrapPanel();form.Children.Add(presets);Action<string,string,string,string> preset=delegate(string name,string a,string r,string c){var button=CompactButton(name,delegate{accent.Text=a;rings.Text=r;core.Text=c;});button.FontSize=10;presets.Children.Add(button);};preset("ORIGINAL","#00E6EB","#A5D7FF","#F22330");preset("PURPLE","#C79BFF","#BFAAFF","#FF64AD");preset("GREEN","#5AFFA0","#B9FFD3","#FFB347");preset("AMBER","#FFC25C","#FFE0A1","#FF6859");
   var status=Label("Your colors are saved for this Windows account.",11,muted);form.Children.Add(status);Func<ThemePreferences> draft=delegate{var value=new ThemePreferences {Accent=accent.Text,Rings=rings.Text,Core=core.Text};value.Validate();return value;};
   var buttons=new WrapPanel();form.Children.Add(buttons);buttons.Children.Add(Action("PREVIEW",delegate{try{themePalette.Apply(draft());status.Text="Preview active. Save to keep it; Cancel restores your colors.";}catch(Exception ex){status.Text=ex.Message;}}));buttons.Children.Add(Action("SAVE COLORS",delegate{try{var value=draft();value.Save(ThemePath);themePalette.Apply(value);themePreferences=value;saved=true;dialog.Close();}catch(Exception ex){status.Text=ex.Message;}}));buttons.Children.Add(Action("CANCEL",dialog.Close));buttons.Children.Add(Action("PC SETUP",delegate{OpenComputerSetup(dialog);}));
   dialog.Closed+=delegate{if(!saved)themePalette.Apply(before);};dialog.ShowDialog();
  }
  sealed class HudColorOwner : System.Windows.Forms.IWin32Window {readonly IntPtr handle;public HudColorOwner(IntPtr value){handle=value;}public IntPtr Handle {get{return handle;}}}
 }
}

