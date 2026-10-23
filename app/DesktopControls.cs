using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.Win32;

namespace JarvisDesk {
 public static class DesktopCommands {
  static string Name(int index,string directory){using(var hash=System.Security.Cryptography.SHA256.Create()){string identity=Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();string suffix=BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(identity))).Replace("-","").Substring(0,24);return "Local\\JarvisDesk.Desktop."+suffix+"."+index;}}
  public static EventWaitHandle Open(int index,string directory){return new EventWaitHandle(false,EventResetMode.AutoReset,Name(index,directory));}
  public static bool Send(string[] args){return Send(args,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"JarvisDesk"));}
  public static bool Send(string[] args,string directory){try {bool savedDesktop=DesktopPreferences.Load(Path.Combine(directory,"desktop.json")).Enabled;int index=args.Contains("--desktop-settings")?2:args.Contains("--desktop")?1:args.Contains("--window")?0:savedDesktop?1:0;using(var signal=EventWaitHandle.OpenExisting(Name(index,directory))) signal.Set();return true;}catch{return false;}}
 }
 public partial class Desk {
  DesktopPreferences desktopPreferences=new DesktopPreferences();
  DesktopNative.Attachment desktopAttachment;
  System.Windows.Forms.NotifyIcon desktopTray;
  System.Drawing.Icon desktopTrayIcon;
  DispatcherTimer desktopTimer;
  EventWaitHandle[] desktopSignals;
  HwndSource desktopSource;
  Rect desktopWindowBounds;
  WindowState desktopOldState;
  ResizeMode desktopOldResize;
  WindowChrome desktopOldChrome;
  bool desktopOldTaskbar,desktopIntegrating,desktopChanging;
  FrameworkElement desktopView;
  DateTime nextDesktopCheck;
  DesktopTaskbar hudTaskbar; System.Threading.Timer hudTaskbarTimer;
  readonly object hudTaskbarGate=new object();
  Button hudTaskbarTab;
  void UpdateHudTaskbarTab(){if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(new Action(UpdateHudTaskbarTab));return;}if(hudTaskbarTab!=null)hudTaskbarTab.Visibility=IsDesktop && hudTaskbar!=null?Visibility.Visible:Visibility.Collapsed;}
  void RevealHudTaskbar(){lock(hudTaskbarGate){if(hudTaskbar!=null)hudTaskbar.RequestReveal();}WriteDesktopStatus();}
  void DesktopHudPointerMoved(int x,int y,int width,int height){lock(hudTaskbarGate){if(hudTaskbar!=null){try{hudTaskbar.RevealFromClientPoint(x,y,width,height);}catch(Exception ex){hudTaskbarError=ex.Message;}}}}
  bool hudTaskbarFailed; string hudTaskbarError="";
  DesktopNative.Rect DesktopArea(DesktopMonitor monitor){var taskbar=hudTaskbar;return taskbar!=null && taskbar.Device==monitor.Device?monitor.Bounds:monitor.WorkArea;}
  void StopHudTaskbar(){lock(hudTaskbarGate){if(hudTaskbarTimer!=null){hudTaskbarTimer.Dispose();hudTaskbarTimer=null;}if(hudTaskbar!=null){hudTaskbar.Dispose();hudTaskbar=null;}UpdateHudTaskbarTab();}}
  void ApplyHudTaskbar(){
   lock(hudTaskbarGate){
   if(!IsDesktop || !desktopPreferences.HideHudTaskbar){StopHudTaskbar();hudTaskbarError="";return;}var target=DesktopTarget();if(hudTaskbar!=null && hudTaskbar.Device==target.Device && hudTaskbar.Healthy)return;StopHudTaskbar();if(hudTaskbarFailed)return;
   try{hudTaskbar=new DesktopTaskbar(target);hudTaskbarTimer=new System.Threading.Timer(PollHudTaskbar,null,0,80);hudTaskbarError="";UpdateHudTaskbarTab();}catch(Exception ex){hudTaskbarFailed=true;hudTaskbarError=ex.Message;Log(hudTaskbarError);}
   }
  }
  void PollHudTaskbar(object state){
   lock(hudTaskbarGate){if(hudTaskbar==null)return;try{hudTaskbar.Tick();}catch(Exception ex){hudTaskbarError=ex.Message;hudTaskbarFailed=true;StopHudTaskbar();Dispatcher.BeginInvoke(new Action(delegate{if(IsDesktop)desktopAttachment.Place(DesktopTarget().WorkArea);Log(hudTaskbarError);WriteDesktopStatus();}),DispatcherPriority.Send);}}
  }
  string desktopError="";
  string DesktopSettingsPath {get{return Path.Combine(dataDir,"desktop.json");}}
  bool IsDesktop {get{return desktopAttachment!=null && desktopAttachment.Active;}}
  string StartupCommand {get{return "\""+System.Reflection.Assembly.GetExecutingAssembly().Location+"\"";}}
  bool StartupEnabled(){if(PackageEnvironment.IsPackaged)return false;using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))return key!=null && (key.GetValue("DesktopAssistant") as string)==StartupCommand;}
  void SetDesktopStartup(bool enabled){if(PackageEnvironment.IsPackaged)return;using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){if(enabled) key.SetValue("DesktopAssistant",StartupCommand,RegistryValueKind.String);else if((key.GetValue("DesktopAssistant") as string)==StartupCommand) key.DeleteValue("DesktopAssistant",false);}}
  DesktopMonitor DesktopTarget(){var monitors=DesktopNative.Monitors();return monitors.FirstOrDefault(m=>m.Device==desktopPreferences.Monitor)??monitors.FirstOrDefault(m=>m.Primary)??monitors[0];}
  public void StartDesktopIntegration(string[] args){
   if(desktopIntegrating) return;desktopIntegrating=true;desktopPreferences=DesktopPreferences.Load(DesktopSettingsPath);
   var selected=args.FirstOrDefault(a=>a.StartsWith("--monitor=",StringComparison.Ordinal));if(selected!=null) desktopPreferences.Monitor=selected.Substring(10);
   if(args.Contains("--desktop")) {desktopPreferences.Enabled=true;if(String.IsNullOrEmpty(desktopPreferences.Monitor))desktopPreferences.Monitor=DesktopNative.CurrentMonitor(new WindowInteropHelper(this).Handle);desktopPreferences.Save(DesktopSettingsPath);}
   if(args.Contains("--window")) {desktopPreferences.Enabled=false;desktopPreferences.Save(DesktopSettingsPath);}
   desktopSignals=new[]{DesktopCommands.Open(0,dataDir),DesktopCommands.Open(1,dataDir),DesktopCommands.Open(2,dataDir)};
   desktopTray=new System.Windows.Forms.NotifyIcon {Text="Desktop Assistant / desktop controls"};
   string icon=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Syra.ico");
   if(File.Exists(icon)) {desktopTrayIcon=new System.Drawing.Icon(icon);desktopTray.Icon=desktopTrayIcon;}else desktopTray.Icon=System.Drawing.SystemIcons.Application;
   var menu=new System.Windows.Forms.ContextMenuStrip();
   menu.Items.Add("Interactive desktop",null,delegate{DesktopUI(delegate{EnableDesktop(true);});});
   menu.Items.Add("Window mode",null,delegate{DesktopUI(delegate{DisableDesktop(true);Show();Activate();});});
   menu.Items.Add("Desktop settings / monitor",null,delegate{DesktopUI(OpenDesktopSettings);});
   menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
   menu.Items.Add("Exit",null,delegate{DesktopUI(Close);});desktopTray.ContextMenuStrip=menu;
   desktopTray.DoubleClick+=delegate{DesktopUI(OpenDesktopSettings);};desktopTray.Visible=true;
   desktopSource=HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);desktopSource.AddHook(DesktopHook);
   if(!DesktopNative.RegisterHotKey(desktopSource.Handle,481,0x4000|0x0002|0x0001,0x44)) Log("Ctrl+Alt+D is already assigned. Desktop controls remain available in the tray.");
   desktopTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(500)};desktopTimer.Tick+=delegate{CheckDesktopIntegration();};desktopTimer.Start();
   Closing+=delegate{CloseDesktopIntegration();};
   if(desktopPreferences.Enabled) DesktopUI(delegate{EnableDesktop(false);});
   WriteDesktopStatus();if(args.Contains("--desktop-settings")) OpenDesktopSettings();
  }
  void DesktopUI(Action action){if(closing)return;try{action();desktopError="";}catch(Exception ex){desktopError=ex.Message;Log(desktopError);if(desktopTray!=null)desktopTray.ShowBalloonTip(7000,"Desktop mode",desktopError,System.Windows.Forms.ToolTipIcon.Warning);}WriteDesktopStatus();}
  [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
  struct DesktopWindowPosition {public IntPtr Window,After;public int X,Y,Width,Height;public uint Flags;}
  IntPtr DesktopHook(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled){
   if(IsDesktop && !desktopChanging){
    if(message==0x0018 && wParam!=IntPtr.Zero)Dispatcher.BeginInvoke(new Action(ConcealDesktopBackend),DispatcherPriority.Send);
    if(message==0x0046 && lParam!=IntPtr.Zero){var position=(DesktopWindowPosition)System.Runtime.InteropServices.Marshal.PtrToStructure(lParam,typeof(DesktopWindowPosition));if((position.Flags&0x0040)!=0){position.Flags=(position.Flags&~0x0040u)|0x0080;System.Runtime.InteropServices.Marshal.StructureToPtr(position,lParam,false);}}
    if(message==0x0112){long command=wParam.ToInt64()&0xFFF0;if(command==0xF120 || command==0xF030 || command==0xF020){handled=true;return IntPtr.Zero;}}
   }
   if(message==0x0312 && wParam.ToInt32()==481){handled=true;DesktopUI(delegate{if(IsDesktop){DisableDesktop(true);Activate();}else EnableDesktop(true);});}return IntPtr.Zero;
  }
  void EnableDesktop(bool save){
   if(desktopChanging) return;desktopChanging=true;try {
    var target=DesktopTarget();
    if(IsDesktop) {ApplyHudTaskbar();desktopAttachment.Place(DesktopArea(target));if(save){desktopPreferences.Enabled=true;desktopPreferences.Monitor=target.Device;desktopPreferences.Save(DesktopSettingsPath);}return;}
    desktopWindowBounds=WindowState==WindowState.Normal?new Rect(Left,Top,ActualWidth,ActualHeight):RestoreBounds;
    desktopOldState=WindowState;desktopOldResize=ResizeMode;desktopOldTaskbar=ShowInTaskbar;desktopOldChrome=WindowChrome.GetWindowChrome(this);
    desktopView=(FrameworkElement)Content;
    if(!desktopView.Resources.MergedDictionaries.Contains(Resources))desktopView.Resources.MergedDictionaries.Add(Resources);
    desktopView.KeyDown+=DesktopViewKeyDown;Content=null;
    try{desktopAttachment=new DesktopNative.Attachment(desktopView,target);desktopAttachment.PointerMoved+=DesktopHudPointerMoved;IsVisibleChanged+=DesktopBackendVisibilityChanged;ShowInTaskbar=false;Hide();}catch{IsVisibleChanged-=DesktopBackendVisibilityChanged;if(desktopAttachment!=null)desktopAttachment.Dispose();Content=desktopView;desktopView.KeyDown-=DesktopViewKeyDown;desktopView=null;desktopAttachment=null;ShowInTaskbar=desktopOldTaskbar;throw;}
    ApplyHudTaskbar();desktopAttachment.Place(DesktopArea(target));
    desktopPreferences.Enabled=true;desktopPreferences.Monitor=target.Device;if(save)desktopPreferences.Save(DesktopSettingsPath);Log("Interactive desktop active on "+target.Device+". Ctrl+Alt+D returns to window mode.");
   }finally{desktopChanging=false;WriteDesktopStatus();}
  }
  void ConcealDesktopBackend(){if(!IsDesktop || desktopChanging)return;if(IsVisible)Hide();DesktopNative.ConcealBackend(new WindowInteropHelper(this).Handle);}
  void DesktopBackendVisibilityChanged(object sender,DependencyPropertyChangedEventArgs e){
   // The detached owner keeps services and dialogs alive, but has no content to display.
   // Shell restore / owned-window activation must not expose it above the actual HUD.
   if(IsDesktop && IsVisible && !desktopChanging) Dispatcher.BeginInvoke(new Action(ConcealDesktopBackend),DispatcherPriority.Send);
  }
  void RestoreDesktopWindow(){
   StopHudTaskbar();
   IsVisibleChanged-=DesktopBackendVisibilityChanged;
   if(desktopAttachment!=null) desktopAttachment.Dispose();
   if(desktopView!=null){desktopView.KeyDown-=DesktopViewKeyDown;Content=desktopView;desktopView=null;}
   WindowChrome.SetWindowChrome(this,desktopOldChrome);ResizeMode=desktopOldResize;ShowInTaskbar=desktopOldTaskbar;
   WindowState=WindowState.Normal;if(!desktopWindowBounds.IsEmpty && desktopWindowBounds.Width>0){Left=desktopWindowBounds.Left;Top=desktopWindowBounds.Top;Width=desktopWindowBounds.Width;Height=desktopWindowBounds.Height;}
   if(desktopOldState==WindowState.Maximized) WindowState=WindowState.Maximized;
   Show();
  }
  void DesktopViewKeyDown(object sender,System.Windows.Input.KeyEventArgs e){
   if(!IsDesktop || e.Handled)return;
   var forwarded=new System.Windows.Input.KeyEventArgs(e.KeyboardDevice,PresentationSource.FromVisual(desktopView),e.Timestamp,e.Key) {RoutedEvent=System.Windows.Input.Keyboard.KeyDownEvent};RaiseEvent(forwarded);e.Handled=forwarded.Handled;
  }
  void DisableDesktop(bool save){
   if(desktopChanging) return;desktopChanging=true;try {if(desktopAttachment!=null) RestoreDesktopWindow();desktopAttachment=null;desktopPreferences.Enabled=false;if(save)desktopPreferences.Save(DesktopSettingsPath);}finally{desktopChanging=false;WriteDesktopStatus();}
  }
  void MinimizeDeck(){if(IsDesktop) DisableDesktop(true);WindowState=WindowState.Minimized;}
  void CheckDesktopIntegration(){
   if(closing)return;
   if(desktopSignals[0].WaitOne(0)) DesktopUI(delegate{DisableDesktop(true);Show();WindowState=WindowState.Normal;Activate();});
   if(desktopSignals[1].WaitOne(0)) DesktopUI(delegate{EnableDesktop(true);});
   if(desktopSignals[2].WaitOne(0)) DesktopUI(OpenDesktopSettings);
   if(DateTime.UtcNow<nextDesktopCheck)return;nextDesktopCheck=DateTime.UtcNow.AddSeconds(2);
   if(desktopAttachment!=null) DesktopUI(delegate{ConcealDesktopBackend();if(!desktopAttachment.Healthy){DisableDesktop(true);throw new IOException("The Windows desktop host changed. Window mode has been restored; you can enable desktop mode again from the tray.");}ApplyHudTaskbar();desktopAttachment.Place(DesktopArea(DesktopTarget()));});
  }
  public void OpenDesktopSettings(){
   if(!buildingSetupPage){OpenUnifiedSettings("desktop");return;}
   var dialog=new Window {Title="Interactive desktop settings",Owner=this,Width=660,Height=600,MinWidth=590,MinHeight=460,WindowStartupLocation=WindowStartupLocation.CenterScreen,Background=Ink(3,13,20),Foreground=cyan};dialog.Resources.Add(typeof(Button),Resources[typeof(Button)]);
   var form=new StackPanel {Margin=new Thickness(24)};dialog.Content=new ScrollViewer {Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
   form.Children.Add(Label("INTERACTIVE DESKTOP",23,cyan));form.Children.Add(Label("Use the HUD as a working desktop on one monitor. It covers icons on that monitor and stays behind regular apps. Voice, chat, calendar and sensors continue running.",12,muted));
   var enabled=new CheckBox {Content="Enable interactive desktop mode",IsChecked=IsDesktop,Foreground=cyan,Margin=new Thickness(0,8,0,14)};form.Children.Add(enabled);
   var taskbar=new CheckBox {Content="Auto-hide taskbar on the HUD monitor only",IsChecked=desktopPreferences.HideHudTaskbar,Foreground=cyan,Margin=new Thickness(0,0,0,12)};form.Children.Add(taskbar);
   form.Children.Add(Label("Move to that monitor's screen edge to reveal its taskbar. Other taskbars keep their current behavior. Restored when desktop mode stops.",11,muted));
   form.Children.Add(Label("MONITOR",11,cyan));var monitor=new ComboBox {ItemsSource=DesktopNative.Monitors(),Margin=new Thickness(0,0,0,14),Padding=new Thickness(6)};monitor.SelectedItem=monitor.Items.Cast<DesktopMonitor>().FirstOrDefault(m=>m.Device==desktopPreferences.Monitor)??monitor.Items.Cast<DesktopMonitor>().FirstOrDefault(m=>m.Primary)??monitor.Items[0];form.Children.Add(monitor);
   var startup=new CheckBox {Content=PackageEnvironment.IsPackaged?"Startup: manage in Windows Settings > Apps > Startup":"Start with Windows",IsEnabled=!PackageEnvironment.IsPackaged,IsChecked=StartupEnabled(),Foreground=cyan,Margin=new Thickness(0,5,0,14)};form.Children.Add(startup);
   form.Children.Add(Label("Ctrl+Alt+D switches between desktop and window mode. The tray icon also opens these settings. F11 returns a pinned HUD to window mode.",12,muted));
   var status=Label(hudTaskbarError.Length>0?hudTaskbarError:desktopError.Length>0?desktopError:IsDesktop?"Desktop mode is active.":"Window mode is active.",11,cyan);form.Children.Add(status);
   var buttons=new WrapPanel();form.Children.Add(buttons);buttons.Children.Add(Action("SAVE & APPLY",delegate {
    bool beforeStartup=StartupEnabled();var previous=new DesktopPreferences {Enabled=desktopPreferences.Enabled,Monitor=desktopPreferences.Monitor,HideHudTaskbar=desktopPreferences.HideHudTaskbar};
    try{var choice=monitor.SelectedItem as DesktopMonitor;if(choice==null)throw new ArgumentException("Choose a monitor first.");desktopPreferences.Monitor=choice.Device;desktopPreferences.HideHudTaskbar=taskbar.IsChecked==true;hudTaskbarFailed=false;if(enabled.IsChecked==true)EnableDesktop(false);else DisableDesktop(false);SetDesktopStartup(startup.IsChecked==true);desktopPreferences.Enabled=enabled.IsChecked==true;desktopPreferences.Save(DesktopSettingsPath);desktopError="";WriteDesktopStatus();if(hudTaskbarError.Length>0){status.Text=hudTaskbarError;return;}dialog.Close();}
    catch(Exception ex){try{SetDesktopStartup(beforeStartup);desktopPreferences=previous;if(previous.Enabled)EnableDesktop(false);else DisableDesktop(false);}catch{}status.Text=ex.Message;}
   }));buttons.Children.Add(Action("CANCEL",dialog.Close));PresentSetupDialog(dialog);
  }
  void WriteDesktopStatus(){
   if(!desktopIntegrating)return;try{var taskbar=hudTaskbar;string json=ChatJson.Serializer().Serialize(new {ProcessId=System.Diagnostics.Process.GetCurrentProcess().Id,Active=IsDesktop,Healthy=IsDesktop && desktopAttachment.Healthy,Monitor=desktopPreferences.Monitor,Error=desktopError,TaskbarAutoHide=taskbar!=null,TaskbarHidden=taskbar!=null && taskbar.Hidden,TaskbarError=hudTaskbarError,TaskbarPollUtc=taskbar==null?"":taskbar.LastPollUtc.ToString("o"),TaskbarDirectRevealUtc=taskbar==null?"":taskbar.LastDirectRevealUtc.ToString("o"),TaskbarDirectReveals=taskbar==null?0:taskbar.DirectRevealCount});string path=Path.Combine(dataDir,"desktop-status.json"),temp=path+".tmp";File.WriteAllText(temp,json);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}catch{}
  }
  void CloseDesktopIntegration(){
   StopHudTaskbar();
   if(desktopTimer!=null)desktopTimer.Stop();
   if(desktopSource!=null){DesktopNative.UnregisterHotKey(desktopSource.Handle,481);desktopSource.RemoveHook(DesktopHook);}
   if(desktopAttachment!=null){try{desktopAttachment.Dispose();}catch{}}desktopAttachment=null;
   if(desktopTray!=null){desktopTray.Visible=false;desktopTray.Dispose();desktopTray=null;}if(desktopTrayIcon!=null)desktopTrayIcon.Dispose();
   if(desktopSignals!=null)foreach(var signal in desktopSignals)signal.Dispose();WriteDesktopStatus();
  }
 }
}









