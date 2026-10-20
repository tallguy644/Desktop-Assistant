using System;
using System.Linq;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace JarvisDesk {
 public static class DesktopSizing {
  public static Size Fit(double width,double height,double wantedWidth,double wantedHeight){if(Double.IsNaN(width)||Double.IsNaN(height)||width<=0||height<=0)throw new ArgumentException("A valid display size is required.");return new Size(Math.Min(wantedWidth,Math.Max(1,width-24)),Math.Min(wantedHeight,Math.Max(1,height-24)));}
 }
 public partial class Desk {
  bool startupFitted;
  void FitStartupWindow(){if(startupFitted)return;startupFitted=true;var area=SystemParameters.WorkArea;var size=DesktopSizing.Fit(area.Width,area.Height,1366,768);MinWidth=Math.Min(1080,size.Width);MinHeight=Math.Min(608,size.Height);Width=size.Width;Height=size.Height;Left=area.Left+(area.Width-size.Width)/2;Top=area.Top+(area.Height-size.Height)/2;}
  public void OpenComputerSetup(Window owner){
   var dialog=new Window {Title="Set up this PC",Owner=owner??this,Width=650,Height=600,MinWidth=520,MinHeight=440,WindowStartupLocation=WindowStartupLocation.CenterScreen,Background=Ink(3,13,20),Foreground=cyan};dialog.Resources.Add(typeof(Button),Resources[typeof(Button)]);
   var body=new StackPanel {Margin=new Thickness(24)};dialog.Content=new ScrollViewer {Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};body.Children.Add(Label("SET UP THIS PC",23,cyan));body.Children.Add(Label("This package targets Windows 10/11 x64 PCs. Each PC discovers its own apps and monitors. Sign in and configure optional services separately on each Windows account.",12,muted));
   var report=Label("Checking this PC...",12,cyan);body.Children.Add(report);bool closed=false;dialog.Closed+=delegate{closed=true;};
   dialog.Loaded+=async delegate{try{string text=await Task.Run(delegate{var result=new StringBuilder();result.AppendLine("Windows process: "+(Environment.Is64BitProcess?"64-bit":"32-bit"));try{result.AppendLine("English speech recognition: "+(SpeechRecognitionEngine.InstalledRecognizers().Any(r=>r.Culture.TwoLetterISOLanguageName=="en")?"available":"install English speech support"));}catch{result.AppendLine("Speech recognition: unavailable; manual controls remain usable.");}try{using(var voice=new SpeechSynthesizer())result.AppendLine("Installed synthetic voices: "+voice.GetInstalledVoices().Count);}catch{result.AppendLine("Speech output: no accessible Windows voice.");}try{result.AppendLine("Monitors detected: "+DesktopNative.Monitors().Count);}catch{result.AppendLine("Monitor details: unavailable.");}result.AppendLine("CPU and RAM usage: native Windows counters.");result.AppendLine("CPU / GPU temperatures and RPM: require a compatible hardware-monitor feed.");return result.ToString();});if(!closed)report.Text=text;}catch{if(!closed)report.Text="Some checks could not complete. Manual controls remain available.";}};
   body.Children.Add(Label("Temperatures, GPU load and fan speeds depend on the sensors your hardware exposes. Missing readings stay Unavailable. Generic Fan # labels do not identify radiator wiring or pump connections.",12,muted));
   var tools=new WrapPanel();body.Children.Add(tools);tools.Children.Add(Action("OPEN SETUP GUIDE",delegate{try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {FileName=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"PC-SETUP.md"),UseShellExecute=true});}catch{report.Text="Open PC-SETUP.md in the installation folder for setup steps.";}}));tools.Children.Add(Action("PRIVACY",delegate{try{var notice=new Window {Title="Privacy",Owner=dialog,Width=700,Height=580,Content=new ScrollViewer {Content=new TextBlock {Text=System.IO.File.ReadAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"PRIVACY.md")),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(24)}}};notice.ShowDialog();}catch{report.Text="Privacy notice: https://github.com/tallguy644/Desktop-Assistant/blob/main/PRIVACY.md";}}));tools.Children.Add(Action("CLOSE",dialog.Close));dialog.ShowDialog();
  }
 }
}
