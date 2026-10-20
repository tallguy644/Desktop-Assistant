using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using Microsoft.Win32;

namespace JarvisDesk {
 [DataContract] public sealed class ScreenPreferences {
  [DataMember] public bool Enabled;
  [DataMember] public int TimeoutMinutes=10;
  [DataMember] public bool RequireWakePhrase=true;
  [OnDeserializing] void SetDefaults(StreamingContext context) { TimeoutMinutes=10;RequireWakePhrase=true; }
  public void Validate() { if(TimeoutMinutes<1 || TimeoutMinutes>120) throw new ArgumentException("Screen timeout must be 1–120 whole minutes."); }
  public static ScreenPreferences Load(string path) { if(!File.Exists(path)) return new ScreenPreferences();using(var file=File.OpenRead(path)) { var value=(ScreenPreferences)new DataContractJsonSerializer(typeof(ScreenPreferences)).ReadObject(file);value.Validate();return value; } }
  public void Save(string path) { Validate();string temp=path+".tmp";using(var file=File.Create(temp)) new DataContractJsonSerializer(typeof(ScreenPreferences)).WriteObject(file,this);if(File.Exists(path)) File.Replace(temp,path,null);else File.Move(temp,path); }
 }
 public static class ScreenIntent {
  public static string[] Phrases { get { return new string[] {AssistantIdentity.WakeName+" wake up",AssistantIdentity.WakeName+" screens on",AssistantIdentity.WakeName+" screens off","wake up","screens on","screens off"}; } }
  public static string Parse(string text,bool requireWake=true) {
   string clean=System.Text.RegularExpressions.Regex.Replace((text??"").ToLowerInvariant(),@"[^a-z0-9 ]"," ");clean=System.Text.RegularExpressions.Regex.Replace(clean,@"\s+"," ").Trim();
   bool named=AssistantIdentity.StripWake(ref clean);if(!named && requireWake) return null;
   return clean=="wake up" || clean=="screens on"?"wake":clean=="screens off"?"off":null;
  }
 }
 public interface IScreenPower {
  void Begin();void Hold(bool display);void Displays(bool on);void End();
 }
 public sealed class ScreenIdleController {
  readonly IScreenPower power;
  uint activity,input;
  public bool Active { get; private set; }
  public bool DisplaysOff { get; private set; }
  public ScreenIdleController(IScreenPower backend) { power=backend; }
  public static uint Elapsed(uint now,uint then) { return unchecked(now-then); }
  public void Update(uint now,uint lastInput,bool enabled,bool unlocked,bool busy,int timeoutMinutes) {
   if(!enabled || !unlocked) { Stop();return; }
   if(!Active) { try { power.Begin();power.Hold(true);Active=true;activity=now;input=lastInput;DisplaysOff=false; } catch { power.End();throw; } }
   if(input!=lastInput) { input=lastInput;Touch(now); }
   if(!DisplaysOff && !busy && Elapsed(now,activity)>=(uint)timeoutMinutes*60000) Off(now);
  }
  public void Touch(uint now) { activity=now;if(Active && DisplaysOff) { power.Hold(true);DisplaysOff=false; } }
  public void Wake(uint now) { if(!Active) throw new InvalidOperationException("Enable screen control and LISTEN first.");power.Hold(true);power.Displays(true);DisplaysOff=false;activity=now; }
  public void Off(uint now) { if(!Active) throw new InvalidOperationException("Enable screen control and LISTEN first.");try { power.Hold(false);power.Displays(false);DisplaysOff=true;activity=now; } catch { power.Hold(true);throw; } }
  public void Stop() { if(!Active) return;try { power.End(); } finally { Active=false;DisplaysOff=false; } }
 }
 public sealed class ScreenRestoreState { public bool SaverActive;public int ProcessId;public long ProcessStarted; }
 [StructLayout(LayoutKind.Sequential)] public struct ScreenMouseInput { public int X,Y;public uint MouseData,Flags,Time;public UIntPtr ExtraInfo; }
 [StructLayout(LayoutKind.Sequential)] public struct ScreenWakeInput { public uint Type;public ScreenMouseInput Mouse; }
 public static class DisplayWakeInput {
  static int Normalize(int value,int origin,int extent) { return (int)Math.Max(0,Math.Min(65535,(((long)value-origin)*65536+32768)/extent)); }
  public static ScreenWakeInput[] Build(int x,int y,int left,int top,int width,int height,bool buttonHeld=false) {
   if(width<1 || height<1) throw new ArgumentException("The Windows desktop dimensions are unavailable.");
   int targetX=x,targetY=y;
   if(!buttonHeld) { if(width>1) targetX=x<(long)left+width-1?x+1:x-1;else if(height>1) targetY=y<(long)top+height-1?y+1:y-1; }
   const uint flags=0x0001|0x2000|0x4000|0x8000; // MOVE, NOCOALESCE, VIRTUALDESK, ABSOLUTE; no buttons or keys.
   return new[] {
    new ScreenWakeInput { Type=0,Mouse=new ScreenMouseInput { X=Normalize(targetX,left,width),Y=Normalize(targetY,top,height),Flags=flags } },
    new ScreenWakeInput { Type=0,Mouse=new ScreenMouseInput { X=Normalize(x,left,width),Y=Normalize(y,top,height),Flags=flags } }
   };
  }
 }
 public sealed class WindowsScreenPower : IScreenPower {
  readonly string restorePath;
  bool begun;
  ScreenRestoreState saved;
  public WindowsScreenPower(string path) { restorePath=path; }
  [StructLayout(LayoutKind.Sequential)] struct LASTINPUTINFO { public uint cbSize,dwTime; }
  [DllImport("user32.dll",SetLastError=true)] static extern bool GetLastInputInfo(ref LASTINPUTINFO info);
  [DllImport("user32.dll",SetLastError=true,EntryPoint="SystemParametersInfoW")] static extern bool GetParameter(uint action,uint param,out int value,uint flags);
  [DllImport("user32.dll",SetLastError=true,EntryPoint="SystemParametersInfoW")] static extern bool SetParameter(uint action,uint param,IntPtr value,uint flags);
  [DllImport("kernel32.dll",SetLastError=true)] static extern uint SetThreadExecutionState(uint flags);
  [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr wParam,IntPtr lParam,uint flags,uint timeout,out IntPtr result);
  [StructLayout(LayoutKind.Sequential)] struct POINT { public int X,Y; }
  [DllImport("user32.dll",SetLastError=true)] static extern bool GetCursorPos(out POINT point);
  [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
  [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
  [DllImport("user32.dll",SetLastError=true)] static extern uint SendInput(uint count,ScreenWakeInput[] inputs,int size);
  [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
  [DllImport("user32.dll",SetLastError=true)] static extern IntPtr OpenInputDesktop(uint flags,bool inherit,uint access);
  [DllImport("user32.dll",SetLastError=true,CharSet=CharSet.Unicode)] static extern bool GetUserObjectInformation(IntPtr handle,int index,StringBuilder text,int length,out int needed);
  [DllImport("user32.dll")] static extern bool CloseDesktop(IntPtr desktop);
  public static uint Now { get { return unchecked((uint)Environment.TickCount); } }
  public static uint LastInput { get { var info=new LASTINPUTINFO { cbSize=(uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };if(!GetLastInputInfo(ref info)) throw new IOException("Windows input inactivity could not be read.");return info.dwTime; } }
  public static bool Unlocked {
   get { IntPtr desktop=OpenInputDesktop(0,false,1);if(desktop==IntPtr.Zero) return false;try { int needed;var name=new StringBuilder(256);return GetUserObjectInformation(desktop,2,name,name.Capacity*2,out needed) && name.ToString().Equals("Default",StringComparison.OrdinalIgnoreCase); } finally { CloseDesktop(desktop); } }
  }
  public static bool ScreenSaverActive { get { int value;if(!GetParameter(0x10,0,out value,0)) throw new IOException("Windows screen-saver state could not be read.");return value!=0; } }
  static bool Positive(RegistryKey root,string path,string name) { using(var key=root.OpenSubKey(path)) { var value=key==null?null:key.GetValue(name);int number;return value!=null && Int32.TryParse(value.ToString(),out number) && number>0; } }
  public static string PolicyProblem() {
   if(Positive(Registry.LocalMachine,@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System","InactivityTimeoutSecs") || Positive(Registry.LocalMachine,@"SOFTWARE\Microsoft\PolicyManager\current\device\DeviceLock","MaxInactivityTimeDeviceLock")) return "Windows enforces automatic locking. Screen control cannot keep this session unlocked under that policy.";
   if(Positive(Registry.CurrentUser,@"Software\Microsoft\Windows NT\CurrentVersion\Winlogon","EnableGoodbye")) return "Windows Dynamic Lock is enabled. Turn it off in Windows Sign-in options before using unlocked screen control.";
   if(Positive(Registry.CurrentUser,@"Software\Policies\Microsoft\Windows\Control Panel\Desktop","ScreenSaveActive") || Positive(Registry.LocalMachine,@"SOFTWARE\Policies\Microsoft\Windows\Control Panel\Desktop","ScreenSaveActive")) return "A Windows screen-saver policy is active. Screen control cannot override that policy.";
   return null;
  }
  public void Recover() {
   if(!File.Exists(restorePath)) return;
   ScreenRestoreState value;using(var file=File.OpenRead(restorePath)) value=ChatJson.Serializer().Deserialize<ScreenRestoreState>(new StreamReader(file).ReadToEnd());
   bool alive=false;try { var process=Process.GetProcessById(value.ProcessId);alive=process.Id!=Process.GetCurrentProcess().Id && process.StartTime.ToUniversalTime().Ticks==value.ProcessStarted; } catch(ArgumentException) { } catch(InvalidOperationException) { }
   if(alive) throw new IOException("Another screen-control session is still active.");
   if(!Unlocked) return;
   if(!SetParameter(0x11,value.SaverActive?1u:0u,IntPtr.Zero,2)) throw new IOException("Windows could not restore the previous screen-saver state.");File.Delete(restorePath);
  }
  public void Begin() {
   if(begun) return;var problem=PolicyProblem();if(problem!=null) throw new IOException(problem);
   Recover();if(!Unlocked) throw new IOException("Unlock Windows with your normal sign-in method first.");
   var process=Process.GetCurrentProcess();saved=new ScreenRestoreState { SaverActive=ScreenSaverActive,ProcessId=process.Id,ProcessStarted=process.StartTime.ToUniversalTime().Ticks };
   string temp=restorePath+".tmp";File.WriteAllText(temp,ChatJson.Serializer().Serialize(saved));if(File.Exists(restorePath)) File.Replace(temp,restorePath,null);else File.Move(temp,restorePath);
   // Session-only change; the saved Windows profile and password/PIN are untouched.
   if(!SetParameter(0x11,0,IntPtr.Zero,2)) { Recover();throw new IOException("Windows could not pause the screen saver."); }begun=true;
  }
  public void Hold(bool display) { if(SetThreadExecutionState(0x80000001u|(display?2u:0u))==0) throw new IOException("Windows could not keep voice screen control awake."); }
  public void Displays(bool on) {
   IntPtr result;
   if(!on) { if(SendMessageTimeout(new IntPtr(0xffff),0x112,new IntPtr(0xf170),new IntPtr(2),2,1500,out result)==IntPtr.Zero) throw new IOException("Windows could not send the screen power command.");return; }
   if(!Unlocked) throw new IOException("Unlock Windows with your normal sign-in method first.");
   // Some Windows/display drivers acknowledge SC_MONITORPOWER(-1) without waking.
   // Also provide real input activity: a one-pixel move and an absolute return.
   SendMessageTimeout(new IntPtr(0xffff),0x112,new IntPtr(0xf170),new IntPtr(-1),2,200,out result);
   IntPtr previous=IntPtr.Zero;
   try {
    try { previous=SetThreadDpiAwarenessContext(new IntPtr(-4)); } catch(EntryPointNotFoundException) { }
    POINT point;if(!GetCursorPos(out point)) throw new IOException("Windows could not read the pointer position for screen wake.");
    bool held=false;foreach(int button in new[] {1,2,4,5,6}) if(GetAsyncKeyState(button)<0) held=true;
    var inputs=DisplayWakeInput.Build(point.X,point.Y,GetSystemMetrics(76),GetSystemMetrics(77),GetSystemMetrics(78),GetSystemMetrics(79),held);
    int size=Marshal.SizeOf(typeof(ScreenWakeInput));uint sent=SendInput((uint)inputs.Length,inputs,size);
    if(sent!=(uint)inputs.Length) {
     if(sent>0) SendInput(1,new[] {inputs[inputs.Length-1]},size);
     throw new IOException("Windows blocked the screen-wake input. Use your keyboard or mouse to wake the screens.");
    }
   } finally { if(previous!=IntPtr.Zero) SetThreadDpiAwarenessContext(previous); }
  }
  public void End() {
   SetThreadExecutionState(0x80000000u);begun=false;
   if(saved!=null && Unlocked && SetParameter(0x11,saved.SaverActive?1u:0u,IntPtr.Zero,2)) { if(File.Exists(restorePath)) File.Delete(restorePath);saved=null; }
   // If Windows was manually locked, defer restoring the screen saver to unlock/next launch.
  }
 }
}
