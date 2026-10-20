using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace JarvisDesk {
 // A real browser reads only the visible Maps route summary. No account cookies
 // from the user's other browsers, API keys, remote debugging, or hidden APIs.
 public sealed class MapsBrowser : Window {
  readonly WebView2 browser=new WebView2();
  readonly TextBlock status=new TextBlock { TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8) };
  readonly Button useRoute=new Button { Content="USE THIS ROUTE",Margin=new Thickness(8),Padding=new Thickness(12,6,12,6),IsEnabled=false };
  readonly DispatcherTimer poll=new DispatcherTimer { Interval=TimeSpan.FromSeconds(3) };
  readonly string profile;
  bool initialized,disposed,reading,navigationReady,backgroundHost;
  int request;
  DateTime deadline;
  MapsReading candidate;
  public event Action<MapsReading,bool> RouteRead;
  public event Action<string> Problem;
  public MapsBrowser(Window owner,string profilePath) {
   Owner=owner; profile=profilePath; Title=AssistantIdentity.CurrentName + " / Google Maps"; Width=1100;Height=780;MinWidth=800;MinHeight=560;WindowStartupLocation=WindowStartupLocation.CenterOwner;
   Background=new SolidColorBrush(Color.FromRgb(3,13,20));Foreground=Brushes.Cyan;
   var layout=new DockPanel();var toolbar=new StackPanel();var note=new TextBlock { Text="Confirm the starting point and destination on the map, then select USE THIS ROUTE. Estimates use current driving traffic.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8) }; toolbar.Children.Add(note);toolbar.Children.Add(status);toolbar.Children.Add(useRoute);DockPanel.SetDock(toolbar,Dock.Top);layout.Children.Add(toolbar);layout.Children.Add(browser);Content=layout;
   useRoute.Click+=delegate { if(candidate!=null && !disposed) { if(RouteRead!=null) RouteRead(candidate,true); Hide(); } };
   Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e) { if(!disposed) { e.Cancel=true;Hide(); } };
   poll.Tick+=async delegate { await ReadRoute(); };
  }
  public void Reveal() { if(!disposed) { backgroundHost=false;Opacity=1;ShowInTaskbar=true;ShowActivated=true;WindowStartupLocation=WindowStartupLocation.Manual;Left=Owner.Left+Math.Max(0,(Owner.ActualWidth-Width)/2);Top=Owner.Top+Math.Max(0,(Owner.ActualHeight-Height)/2);Show();Activate(); } }
  public async Task NavigateRoute(Uri route,bool visible) {
   int generation=++request; candidate=null;navigationReady=false;useRoute.IsEnabled=false;poll.Stop(); status.Text="Opening Google Maps…";
   if(visible) Reveal();else if(!IsVisible) { backgroundHost=true;Opacity=0;ShowInTaskbar=false;ShowActivated=false;WindowStartupLocation=WindowStartupLocation.Manual;Left=-20000;Top=-20000;Show(); }
   try {
    if(!initialized) {
     var options=new CoreWebView2EnvironmentOptions { Language="en-US" };
     var environment=await CoreWebView2Environment.CreateAsync(null,profile,options);
     if(disposed || generation!=request) return;
     await browser.EnsureCoreWebView2Async(environment); initialized=true;
     browser.CoreWebView2.Settings.IsStatusBarEnabled=false;browser.CoreWebView2.Settings.AreDevToolsEnabled=false;
     browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled=false;
     browser.CoreWebView2.PermissionRequested+=delegate(object sender,CoreWebView2PermissionRequestedEventArgs e) { e.State=CoreWebView2PermissionState.Deny; };
     browser.CoreWebView2.NewWindowRequested+=delegate(object sender,CoreWebView2NewWindowRequestedEventArgs e) { e.Handled=true; };
     browser.CoreWebView2.DownloadStarting+=delegate(object sender,CoreWebView2DownloadStartingEventArgs e) { e.Cancel=true; };
     browser.CoreWebView2.NavigationStarting+=delegate(object sender,CoreWebView2NavigationStartingEventArgs e) { navigationReady=false;if(!Allowed(e.Uri)) { e.Cancel=true; Fail("Maps tried to open another site. Check your route in Google Maps."); } candidate=null;useRoute.IsEnabled=false; };
     browser.CoreWebView2.NavigationCompleted+=delegate(object sender,CoreWebView2NavigationCompletedEventArgs e) { navigationReady=e.IsSuccess;if(!e.IsSuccess) Fail("Google Maps did not load. Open Maps and check your connection."); };
    }
    if(disposed || generation!=request) return;
    deadline=DateTime.UtcNow.AddSeconds(75);browser.CoreWebView2.Navigate(route.AbsoluteUri);poll.Start();
   } catch { if(!disposed) Fail("Maps browser could not start. Install or update Microsoft Edge WebView2 Runtime, then try again."); }
  }
  static bool Allowed(string value) { Uri uri;return Uri.TryCreate(value,UriKind.Absolute,out uri) && uri.Scheme=="https" && uri.Port==443 && uri.UserInfo.Length==0 && ((uri.Host=="www.google.com" && (uri.AbsolutePath.StartsWith("/maps",StringComparison.Ordinal) || uri.AbsolutePath.StartsWith("/sorry/",StringComparison.Ordinal))) || uri.Host=="consent.google.com" || uri.Host=="accounts.google.com"); }
  async Task ReadRoute() {
   if(disposed || !initialized || reading) return;
   if(DateTime.UtcNow>deadline) { Fail("Maps needs attention. Open the Maps window, resolve any consent screen, and check the route again."); return; }
   if(!navigationReady) return;
   Uri uri; if(!Uri.TryCreate(browser.CoreWebView2.Source,UriKind.Absolute,out uri) || uri.Host!="www.google.com" || !uri.AbsolutePath.StartsWith("/maps/dir/",StringComparison.Ordinal)) return;
   int generation=request;reading=true;
   try {
    string json=await browser.CoreWebView2.ExecuteScriptAsync(RouteScript);
    if(disposed || generation!=request || json=="null" || json.Length>12000) return;
    var value=ChatJson.Serializer().Deserialize<MapsReading>(json);
    if(value==null || value.Mode!="Driving" || String.IsNullOrWhiteSpace(value.Origin) || String.IsNullOrWhiteSpace(value.Destination)) return;
    CommutePreferences.Address(value.Origin);CommutePreferences.Address(value.Destination);int minutes=Commute.ParseDuration(value.Duration);
    candidate=value;useRoute.IsEnabled=true;poll.Stop();status.Text=value.Origin+" → "+value.Destination+"\n"+minutes+" minutes / current traffic. Check the route before confirming.";
    if(RouteRead!=null) RouteRead(value,false);
    if(backgroundHost) Hide();
   } catch { /* An incomplete or changed page never becomes an estimate. */ }
   finally { reading=false; }
  }
  public const string RouteScript=@"(()=>{const r=document.querySelector('div[data-trip-index=""0""]');const m=document.querySelector('[role=""radio""][aria-checked=""true""][data-tooltip=""Driving""]');const a=document.querySelector('#directions-searchbox-0 input');const b=document.querySelector('#directions-searchbox-1 input');if(!r||!m||!a||!b||!r.getClientRects().length)return null;const d=r.innerText.split('\n').map(x=>x.trim()).find(x=>/^(?:(\d+)\s*(?:hr|hrs|hours?)\s*)?(?:(\d+)\s*(?:min|mins|minutes?))?$/.test(x)&&/\d/.test(x));return d?{Origin:a.value,Destination:b.value,Duration:d,Mode:'Driving',Page:location.href}:null})()";
  void Fail(string text) { poll.Stop();candidate=null;useRoute.IsEnabled=false;status.Text=text;if(backgroundHost) Hide();if(Problem!=null) Problem(text); }
  public void DisposeBrowser() { disposed=true;request++;poll.Stop();browser.Dispose();Close(); }
 }
}
