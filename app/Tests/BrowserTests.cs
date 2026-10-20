using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using JarvisDesk;
public static class BrowserTests {
 [STAThread] public static int Main(string[] args) {
  int result=1;var app=new Application();var browser=new WebView2();var window=new Window { Content=browser,Width=900,Height=600,ShowInTaskbar=false,ShowActivated=false,Opacity=0,Left=-20000,Top=-20000,WindowStartupLocation=WindowStartupLocation.Manual };
  var timeout=new DispatcherTimer { Interval=TimeSpan.FromSeconds(45) };timeout.Tick+=delegate { Console.Error.WriteLine("Browser fixture timed out");browser.Dispose();window.Close();app.Shutdown(); };timeout.Start();
  window.Loaded+=async delegate {
   try {
    var environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(args[0],"browser-profile"),new CoreWebView2EnvironmentOptions { Language="en-US" });
    await browser.EnsureCoreWebView2Async(environment);Console.WriteLine("PASS installed WebView2 runtime initializes with an isolated profile");
    await Load(browser,"<div id='directions-searchbox-0'><input value='Sample home'></div><div id='directions-searchbox-1'><input value='Sample work'></div><div role='radio' aria-checked='true' data-tooltip='Driving'></div><div data-trip-index='0'>28 min<br>13.3 miles<br>via sample road</div>");
    var json=await browser.CoreWebView2.ExecuteScriptAsync(MapsBrowser.RouteScript);var read=ChatJson.Serializer().Deserialize<MapsReading>(json);
    if(read==null || read.Origin!="Sample home" || read.Destination!="Sample work" || Commute.ParseDuration(read.Duration)!=28) throw new Exception("Route DOM parser failed");Console.WriteLine("PASS production route reader extracts visible driving time and canonical addresses");
    await Load(browser,"<div>Consent or loading screen</div>");if(await browser.CoreWebView2.ExecuteScriptAsync(MapsBrowser.RouteScript)!="null") throw new Exception("Consent became a route");Console.WriteLine("PASS consent/loading page cannot become a travel estimate");
    await Load(browser,"<div id='directions-searchbox-0'><input value='Sample home'></div><div id='directions-searchbox-1'><input value='Sample work'></div><div role='radio' aria-checked='true' data-tooltip='Transit'></div><div data-trip-index='0'>28 min</div>");if(await browser.CoreWebView2.ExecuteScriptAsync(MapsBrowser.RouteScript)!="null") throw new Exception("Transit became driving");Console.WriteLine("PASS transit mode cannot be mistaken for a driving route");result=0;
   } catch(Exception ex) { Console.Error.WriteLine(ex.GetType().Name+": "+ex.Message); }
   finally { timeout.Stop();browser.Dispose();window.Close();app.Shutdown(); }
  };
  window.Show();app.Run();return result;
 }
 static Task Load(WebView2 browser,string html) { var done=new TaskCompletionSource<bool>();EventHandler<CoreWebView2NavigationCompletedEventArgs> handler=null;handler=delegate(object sender,CoreWebView2NavigationCompletedEventArgs e) { browser.CoreWebView2.NavigationCompleted-=handler;if(e.IsSuccess) done.SetResult(true);else done.SetException(new Exception("Fixture failed to load")); };browser.CoreWebView2.NavigationCompleted+=handler;browser.NavigateToString(html);return done.Task; }
}
