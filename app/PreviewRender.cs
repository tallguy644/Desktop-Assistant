using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace JarvisDesk {
 // Renders the HUD to PNG files without user interaction, for build-time visual review.
 // Usage: Skynet.exe --render-preview <output-folder>  (writes hud-idle, hud-listening and hud-theme-purple PNGs)
 // Uses a throwaway settings folder so personal settings are never read or written.
 static class PreviewRender {
  const int StageWidth=1440, StageHeight=810;
  public static int Run(string outputFolder) {
   outputFolder=Path.GetFullPath(outputFolder);Directory.CreateDirectory(outputFolder);
   string settings=Path.Combine(Path.GetTempPath(),"SyraPreview-"+Guid.NewGuid().ToString("N"));
   int exitCode=1;
   try {
    var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
    var desk=new Desk(settings);
    desk.ShowActivated=false;desk.ShowInTaskbar=false;desk.WindowStartupLocation=WindowStartupLocation.Manual;
    desk.Loaded+=delegate{desk.Left=-32000;desk.Top=-32000;};
    var steps=new DispatcherTimer { Interval=TimeSpan.FromSeconds(3) };int step=0;
    steps.Tick+=delegate {
     try {
      if(step==0){Save(desk,Path.Combine(outputFolder,"hud-idle.png"));desk.PreviewListening();}
      else if(step==1){Save(desk,Path.Combine(outputFolder,"hud-listening.png"));desk.PreviewTheme("#C79BFF","#BFAAFF","#FF64AD");}
      else{Save(desk,Path.Combine(outputFolder,"hud-theme-purple.png"));exitCode=0;steps.Stop();desk.Close();app.Shutdown();}
      step++;
     } catch(Exception ex){File.WriteAllText(Path.Combine(outputFolder,"render-error.txt"),ex.ToString());steps.Stop();app.Shutdown();}
    };
    // Never let a stuck dialog hang the build.
    var watchdog=new DispatcherTimer { Interval=TimeSpan.FromSeconds(90) };watchdog.Tick+=delegate{File.WriteAllText(Path.Combine(outputFolder,"render-error.txt"),"Timed out.");app.Shutdown();};
    desk.Loaded+=delegate{steps.Start();watchdog.Start();};
    desk.Show();app.Run();
   } catch(Exception ex){File.WriteAllText(Path.Combine(outputFolder,"render-error.txt"),ex.ToString());exitCode=1;}
   finally { try{if(Directory.Exists(settings))Directory.Delete(settings,true);}catch{} }
   return exitCode;
  }
  static void Save(Desk desk,string path) {
   var viewbox=desk.Content as Viewbox;var stage=viewbox!=null?viewbox.Child as FrameworkElement:desk.Content as FrameworkElement;
   if(stage==null)throw new InvalidOperationException("HUD stage not found.");
   var visual=new DrawingVisual();
   using(var context=visual.RenderOpen()){context.DrawRectangle(Brushes.Black,null,new Rect(0,0,StageWidth,StageHeight));context.DrawRectangle(new VisualBrush(stage){Stretch=Stretch.Fill},null,new Rect(0,0,StageWidth,StageHeight));}
   var bitmap=new RenderTargetBitmap(StageWidth,StageHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
   var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
   using(var file=File.Create(path))encoder.Save(file);
  }
 }
 public partial class Desk {
  // Puts the HUD into its listening look for preview renders, without opening the microphone.
  // Applies a color preset for preview renders only; nothing is saved.
  internal void PreviewTheme(string accent,string rings,string core) { themePalette.Apply(new ThemePreferences { Accent=accent,Rings=rings,Core=core }); }
  internal void PreviewListening() { SetState("LISTENING","Say “open” and an app name, “wake up,” or “screens off.”"); }
 }
}
