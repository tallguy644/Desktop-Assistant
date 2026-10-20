using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace JarvisDesk {
 // Controls one existing Explorer taskbar; never changes the global auto-hide setting.
 public sealed class DesktopTaskbar : IDisposable {
  [StructLayout(LayoutKind.Sequential)] public struct Point {public int X,Y;}
  [StructLayout(LayoutKind.Sequential)] struct AppBarData {public uint Size;public IntPtr Window;public uint Callback,Edge;public DesktopNative.Rect Bounds;public IntPtr Parameter;}
  delegate bool EnumCallback(IntPtr window,IntPtr data);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumCallback callback,IntPtr data);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder name,int count);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out DesktopNative.Rect rect);
  [DllImport("user32.dll")] static extern bool GetCursorPos(out Point point);
  [DllImport("user32.dll")] static extern bool GetPhysicalCursorPos(out Point point);
  [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
  [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window,int command);
  [DllImport("user32.dll")] static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
  [DllImport("user32.dll",SetLastError=true)] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
  [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
  [DllImport("shell32.dll")] static extern UIntPtr SHAppBarMessage(uint message,ref AppBarData data);
  IntPtr bar; uint explorer; readonly string device; readonly DesktopNative.Rect monitor;
  DesktopNative.Rect barBounds; bool originalVisible,originalTopmost,disposed; DateTime keepUntil;
  EventWaitHandle guardStop; Process guard;
  public IntPtr Handle {get{return bar;}}
  public string Device {get{return device;}}
  public bool OriginalTopmost {get{return originalTopmost;}}
  public DateTime LastPollUtc {get;private set;}
  public DateTime LastDirectRevealUtc {get;private set;}
  public int DirectRevealCount {get;private set;}
  public bool Hidden {get{return Valid(bar,explorer,device) && !DesktopNative.IsWindowVisible(bar);}}
  public bool Healthy {get{return !disposed && Valid(bar,explorer,device);}}
  static string Class(IntPtr window){var name=new StringBuilder(128);GetClassName(window,name,128);return name.ToString();}
  static uint OwnerProcess(IntPtr window){uint process;GetWindowThreadProcessId(window,out process);return process;}
  static bool Taskbar(IntPtr window){string name=Class(window);return name=="Shell_SecondaryTrayWnd" || name=="Shell_TrayWnd";}
  static bool Valid(IntPtr window,uint process,string device){return window!=IntPtr.Zero && DesktopNative.IsWindow(window) && Taskbar(window) && OwnerProcess(window)==process && process==OwnerProcess(DesktopNative.GetShellWindow()) && DesktopNative.CurrentMonitor(window)==device;}
  public static uint GlobalState(){var data=new AppBarData {Size=(uint)Marshal.SizeOf(typeof(AppBarData))};return (uint)SHAppBarMessage(4,ref data).ToUInt64();}
  public DesktopTaskbar(DesktopMonitor target){
   device=target.Device;monitor=target.Bounds;explorer=OwnerProcess(DesktopNative.GetShellWindow());
   if((GlobalState()&1)!=0)throw new InvalidOperationException("Windows-wide taskbar auto-hide is already enabled. Turn it off in Windows to use HUD-only auto-hide.");
   EnumWindows(delegate(IntPtr window,IntPtr data){if(Taskbar(window) && Valid(window,explorer,device) && DesktopNative.IsWindowVisible(window)){bar=window;return false;}return true;},IntPtr.Zero);
   if(bar==IntPtr.Zero)throw new InvalidOperationException("No visible Windows taskbar was found on the HUD monitor.");
   var previous=SetThreadDpiAwarenessContext(new IntPtr(-4));try{if(!GetWindowRect(bar,out barBounds))throw new InvalidOperationException("The HUD taskbar position is unavailable.");}finally{if(previous!=IntPtr.Zero)SetThreadDpiAwarenessContext(previous);}
   originalVisible=true;
   originalTopmost=(GetWindowLongPtr(bar,-20).ToInt64()&8)!=0;
   // The companion restores this exact taskbar if the assistant unexpectedly exits.
   string token=Guid.NewGuid().ToString("N");guardStop=new EventWaitHandle(false,EventResetMode.ManualReset,"Local\\JarvisDesk.TaskbarGuard."+token);
   try{using(var ready=new EventWaitHandle(false,EventResetMode.ManualReset,"Local\\JarvisDesk.TaskbarGuard."+token+".ready")){using(var current=Process.GetCurrentProcess()){
    string arguments="--taskbar-guard "+current.Id+" "+current.StartTime.ToUniversalTime().Ticks+" "+bar.ToInt64()+" "+explorer+" "+token+" "+Convert.ToBase64String(Encoding.UTF8.GetBytes(device))+" "+(originalTopmost?"1":"0");
    guard=Process.Start(new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"TaskbarRecovery.exe"),arguments){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
   }if(guard==null || !ready.WaitOne(3000) || guard.HasExited)throw new InvalidOperationException("Taskbar recovery could not start; the taskbar was left visible.");}}
   catch{Dispose();throw;}
  }
  public static bool Contains(DesktopNative.Rect rect,Point point,int margin){return point.X>=rect.Left-margin && point.X<rect.Right+margin && point.Y>=rect.Top-margin && point.Y<rect.Bottom+margin;}
  public void RequestReveal(){
   if(!Healthy)return;var now=DateTime.UtcNow;LastDirectRevealUtc=now;DirectRevealCount++;keepUntil=now.AddSeconds(5);
   if(!SetWindowPos(bar,new IntPtr(-1),0,0,0,0,0x0001|0x0002|0x0010|0x0040))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
  }
  public bool RevealFromClientPoint(int x,int y,int width,int height){
   if(width<=0 || height<=0 || x<0 || y<0 || x>=width || y>=height)return false;
   var physical=new Point {X=monitor.Left+(int)((long)x*monitor.Width/width),Y=monitor.Top+(int)((long)y*monitor.Height/height)};
   if(!AtEdge(monitor,barBounds,physical))return false;RequestReveal();return true;
  }
  public static bool AtEdge(DesktopNative.Rect screen,DesktopNative.Rect taskbar,Point point){
   if(!Contains(screen,point,0))return false;
   if(taskbar.Width>=taskbar.Height)return Math.Abs(taskbar.Top-screen.Top)<Math.Abs(taskbar.Bottom-screen.Bottom)?point.Y<screen.Top+12:point.Y>=screen.Bottom-12;
   return Math.Abs(taskbar.Left-screen.Left)<Math.Abs(taskbar.Right-screen.Right)?point.X<screen.Left+12:point.X>=screen.Right-12;
  }
  static bool ShellMenuActive(string device){
   var foreground=GetForegroundWindow();if(DesktopNative.CurrentMonitor(foreground)!=device)return false;string name=Class(foreground);
   if(Taskbar(foreground) || name=="#32768" || name=="NotifyIconOverflowWindow")return true;
   try{using(var process=Process.GetProcessById((int)OwnerProcess(foreground)))return process.ProcessName=="StartMenuExperienceHost" || process.ProcessName=="SearchHost" || process.ProcessName=="ShellExperienceHost";}catch{return false;}
  }
  public void Update(Point point,DateTime now,bool shellInteraction){
   if(!Healthy)throw new InvalidOperationException("The HUD taskbar changed. Auto-hide stopped and the taskbar was restored.");
   if((GlobalState()&1)!=0)throw new InvalidOperationException("Windows-wide auto-hide was enabled. HUD-only auto-hide has stopped.");
   bool visible=DesktopNative.IsWindowVisible(bar);
   if(AtEdge(monitor,barBounds,point) || (visible && Contains(barBounds,point,10)) || shellInteraction)keepUntil=now.AddMilliseconds(1000);
   // ShowWindow alone can leave a secondary taskbar behind an activated HUD.
   // Keep it above the HUD while revealed, without stealing keyboard focus.
   if(now<keepUntil){if(!SetWindowPos(bar,new IntPtr(-1),0,0,0,0,0x0001|0x0002|0x0010|0x0040))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}
   else if(visible){ShowWindow(bar,0);SetWindowPos(bar,new IntPtr(originalTopmost?-1:-2),0,0,0,0,0x0001|0x0002|0x0010);}
  }
  public void Tick(){var previous=SetThreadDpiAwarenessContext(new IntPtr(-4));try{Point point;if(GetPhysicalCursorPos(out point)){LastPollUtc=DateTime.UtcNow;Update(point,LastPollUtc,ShellMenuActive(device) || (Contains(monitor,point,0) && (GetAsyncKeyState(0x5B)<0 || GetAsyncKeyState(0x5C)<0)));}}finally{if(previous!=IntPtr.Zero)SetThreadDpiAwarenessContext(previous);}}
  static void Restore(IntPtr window,bool topmost){SetWindowPos(window,new IntPtr(topmost?-1:-2),0,0,0,0,0x0001|0x0002|0x0010|0x0040);}
  public void Dispose(){if(disposed)return;disposed=true;try{if(originalVisible && Valid(bar,explorer,device))Restore(bar,originalTopmost);}finally{if(guardStop!=null){guardStop.Set();guardStop.Dispose();guardStop=null;}if(guard!=null){guard.Dispose();guard=null;}}}
  public static int RunGuard(string[] args){
   try{
    if(args.Length!=8 || (args[7]!="0" && args[7]!="1"))return 2;bool topmost=args[7]=="1";int id=Int32.Parse(args[1]);long ticks=Int64.Parse(args[2]);var window=new IntPtr(Int64.Parse(args[3]));uint shell=UInt32.Parse(args[4]);Guid token;if(!Guid.TryParseExact(args[5],"N",out token))return 2;string device=Encoding.UTF8.GetString(Convert.FromBase64String(args[6]));
    using(var stop=EventWaitHandle.OpenExisting("Local\\JarvisDesk.TaskbarGuard."+token.ToString("N"))){
     try{using(var parent=Process.GetProcessById(id)){
      if(parent.StartTime.ToUniversalTime().Ticks!=ticks || !String.Equals(parent.MainModule.FileName,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Skynet.exe"),StringComparison.OrdinalIgnoreCase))return 2;
      using(var ready=EventWaitHandle.OpenExisting("Local\\JarvisDesk.TaskbarGuard."+token.ToString("N")+".ready"))ready.Set();
      while(!parent.HasExited){if(stop.WaitOne(300))return 0;}
     }}catch(ArgumentException){} // Parent already exited before the companion started.
     if(Valid(window,shell,device))Restore(window,topmost);
    }return 0;
   }catch{return 2;}
  }
 }
}

