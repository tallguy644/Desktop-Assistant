using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows.Interop;
using System.Windows.Media;

namespace JarvisDesk {
 public static class PackageEnvironment {
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern int GetCurrentPackageFullName(ref uint length,System.Text.StringBuilder name);
  public static bool IsPackaged { get {try{uint length=0;int result=GetCurrentPackageFullName(ref length,null);return result==122 || result==0;}catch{return false;}} }
 }
 [DataContract] public sealed class DesktopPreferences {
  [DataMember] public bool Enabled;
  [DataMember] public bool HideHudTaskbar;
  [DataMember] public string Monitor="";
  public static DesktopPreferences Load(string path) {
   try {using(var file=File.OpenRead(path)) {var value=(DesktopPreferences)new DataContractJsonSerializer(typeof(DesktopPreferences)).ReadObject(file);value.Monitor=value.Monitor??"";return value;}}
   catch {return new DesktopPreferences();}
  }
  public void Save(string path) {
   Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";
   using(var file=File.Create(temp)) new DataContractJsonSerializer(typeof(DesktopPreferences)).WriteObject(file,this);
   if(File.Exists(path)) File.Replace(temp,path,null);else File.Move(temp,path);
  }
 }
 public sealed class DesktopMonitor {
  public string Device; public bool Primary; public DesktopNative.Rect Bounds,WorkArea;
  public override string ToString(){return Device+" / "+Bounds.Width+" x "+Bounds.Height+(Primary?" / primary":"");}
 }
 public static class DesktopNative {
  [StructLayout(LayoutKind.Sequential)] public struct Rect {
   public int Left,Top,Right,Bottom;
   public int Width {get{return Right-Left;}} public int Height {get{return Bottom-Top;}}
   public bool Same(Rect other){return Left==other.Left && Top==other.Top && Right==other.Right && Bottom==other.Bottom;}
  }
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct MonitorInfo {public int Size;public Rect Monitor,Work;public uint Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string Device;}
  delegate bool MonitorCallback(IntPtr monitor,IntPtr dc,ref Rect rect,IntPtr data);
  [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr dc,IntPtr clip,MonitorCallback callback,IntPtr data);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
  [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
  [DllImport("user32.dll")] public static extern IntPtr GetShellWindow();
  [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr window);
  [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr window);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
  [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window,int command);
  public static void ConcealBackend(IntPtr window){if(IsWindowVisible(window))ShowWindow(window,0);}
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out Rect rect);
  [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr window,out Rect rect);
  [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool SetProp(IntPtr window,string name,IntPtr value);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr RemoveProp(IntPtr window,string name);
  [DllImport("user32.dll",SetLastError=true)] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
  [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
  [DllImport("user32.dll")] static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
  [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr window,int id,uint modifiers,uint key);
  [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr window,int id);
  static IntPtr Physical(){return SetThreadDpiAwarenessContext(new IntPtr(-4));}
  static void RestoreDpi(IntPtr previous){if(previous!=IntPtr.Zero) SetThreadDpiAwarenessContext(previous);}
  public static List<DesktopMonitor> Monitors(){
   var previous=Physical();try {
    var result=new List<DesktopMonitor>();
    MonitorCallback callback=delegate(IntPtr monitor,IntPtr dc,ref Rect rect,IntPtr data){var info=new MonitorInfo {Size=Marshal.SizeOf(typeof(MonitorInfo))};if(GetMonitorInfo(monitor,ref info)) result.Add(new DesktopMonitor {Device=info.Device,Bounds=info.Monitor,WorkArea=info.Work,Primary=(info.Flags&1)!=0});return true;};
    if(!EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,callback,IntPtr.Zero) || result.Count==0) throw new IOException("Windows did not provide a monitor for desktop mode.");
    return result;
   }finally{RestoreDpi(previous);}
  }
  public static string CurrentMonitor(IntPtr window){
   var previous=Physical();try {var info=new MonitorInfo {Size=Marshal.SizeOf(typeof(MonitorInfo))};return GetMonitorInfo(MonitorFromWindow(window,2),ref info)?info.Device:"";}finally{RestoreDpi(previous);}
  }
  public sealed class Attachment : IDisposable {
   readonly IntPtr host;
   HwndSource source;
   Rect placed;
   public event Action<int,int,int,int> PointerMoved;
   IntPtr PointerHook(IntPtr window,int message,IntPtr wParam,IntPtr lParam,ref bool handled){
    if(message==0x0200 && PointerMoved!=null){Rect client;if(GetClientRect(window,out client)){long coordinates=lParam.ToInt64();PointerMoved((short)(coordinates&0xFFFF),(short)((coordinates>>16)&0xFFFF),client.Width,client.Height);}}
    return IntPtr.Zero;
   }
   public bool Active {get{return source!=null && !source.IsDisposed;}}
   public bool Healthy {get{return Active && IsWindow(host) && host==GetShellWindow() && GetParent(source.Handle)==host;}}
   public IntPtr Host {get{return host;}}
   public IntPtr Handle {get{return source==null?IntPtr.Zero:source.Handle;}}
   public Attachment(Visual content,DesktopMonitor monitor){
    host=GetShellWindow();if(host==IntPtr.Zero || !IsWindow(host)) throw new IOException("Windows Explorer is not ready. Try desktop mode again from the tray menu.");
    Rect parentRect;var physical=Physical();try{if(!GetWindowRect(host,out parentRect))throw new IOException("The Windows desktop is unavailable.");}finally{RestoreDpi(physical);}
    var previous=SetThreadDpiAwarenessContext(GetWindowDpiAwarenessContext(host));
    try {
     var area=monitor.WorkArea;
     // A shell-owned top-level popup gets its own compositor surface. Embedding WPF as a
     // WS_CHILD of Progman can produce a fully clipped black surface on Windows 11.
     // HWND_BOTTOM keeps this interactive HUD behind ordinary application windows.
     var settings=new HwndSourceParameters("Syra / interactive surface") {ParentWindow=host,WindowStyle=unchecked((int)(0x80000000|0x02000000|0x04000000)),ExtendedWindowStyle=0x80,PositionX=area.Left,PositionY=area.Top,Width=area.Width,Height=area.Height};
     source=new HwndSource(settings);
     // Opt out of Explorer's fullscreen detection before the surface is shown.
     // Otherwise a full-monitor HUD can cause Explorer to lower its taskbar.
     if(!SetProp(source.Handle,"NonRudeHWND",new IntPtr(1)))throw new Win32Exception(Marshal.GetLastWin32Error());
     source.AddHook(PointerHook);
     source.SizeToContent=System.Windows.SizeToContent.Manual;source.CompositionTarget.BackgroundColor=System.Windows.Media.Colors.Black;source.RootVisual=content;placed=area;
     if(!SetWindowPos(source.Handle,new IntPtr(1),0,0,0,0,0x0001|0x0002|0x0010|0x0200|0x0040))throw new Win32Exception(Marshal.GetLastWin32Error());
    }catch{Dispose();throw;}finally{RestoreDpi(previous);}
   }
   public void Place(Rect rect,bool force=false){
    if(!Active || (!force && placed.Same(rect))) return;
    var previous=Physical();try {
     Rect parentRect;if(!GetWindowRect(host,out parentRect))throw new IOException("The Windows desktop changed.");
     if(!SetWindowPos(source.Handle,new IntPtr(1),rect.Left,rect.Top,rect.Width,rect.Height,0x0010|0x0200)) throw new Win32Exception(Marshal.GetLastWin32Error());placed=rect;
    }finally{RestoreDpi(previous);}
   }
   public Rect ActualBounds(){var previous=Physical();try {Rect value;GetWindowRect(Handle,out value);return value;}finally{RestoreDpi(previous);}}
   public void Dispose(){
    PointerMoved=null;if(source!=null){if(!source.IsDisposed){source.RemoveHook(PointerHook);RemoveProp(source.Handle,"NonRudeHWND");source.RootVisual=null;source.Dispose();}source=null;}
   }
  }
 }
}



