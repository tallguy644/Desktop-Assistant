using System;
using System.Collections.Generic;
using JarvisDesk;
public static class ScreensTests {
 sealed class Fake : IScreenPower {
  public int Begins,Ends,Offs,Ons;public bool HoldDisplay,Failure;
  public void Begin() { Begins++; }
  public void End() { Ends++; }
  public void Hold(bool display) { HoldDisplay=display; }
  public void Displays(bool on) { if(Failure) throw new Exception("Power failure");if(on) Ons++;else Offs++; }
 }
 static void Check(bool value,string text) { if(!value) throw new Exception(text);Console.WriteLine("PASS "+text); }
 public static int Main() {
  try {
   var preferences=new ScreenPreferences();Check(!preferences.Enabled && preferences.TimeoutMinutes==10,"New installs default to a ten-minute timer with voice screen control opt-in");
   preferences.TimeoutMinutes=0;bool invalid=false;try { preferences.Validate(); } catch(ArgumentException) { invalid=true; }Check(invalid,"Reject zero-minute inactivity timeout");
   var power=new Fake();var controller=new ScreenIdleController(power);
   controller.Update(1000,500,true,true,false,10);Check(controller.Active && power.Begins==1 && power.HoldDisplay,"Activation holds the system and display awake without switching monitors off");
   controller.Update(600999,500,true,true,false,10);Check(power.Offs==0,"Screens stay on until the full ten minutes elapse");
   controller.Update(601000,500,true,true,false,10);Check(controller.DisplaysOff && power.Offs==1 && !power.HoldDisplay,"Ten minutes releases the display hold and sends one screen-off request");
   controller.Update(602000,500,true,true,false,10);Check(power.Offs==1,"An idle screen-off state does not repeat power commands");
   controller.Wake(603000);Check(!controller.DisplaysOff && power.Ons==1 && power.HoldDisplay,"Voice wake powers displays on and restores the display hold");
   controller.Update(1202999,500,true,true,false,10);Check(power.Offs==1,"Voice wake restarts the full inactivity timer");
   controller.Update(1203000,500,true,true,false,10);Check(power.Offs==2,"Screens can time out again after voice wake");
   controller.Update(1204000,1204000,true,true,false,10);Check(!controller.DisplaysOff && power.HoldDisplay,"Keyboard or mouse activity restores active screen control");
   controller.Touch(1700000);controller.Update(1800000,1204000,true,true,false,10);Check(power.Offs==2,"Accepted voice activity extends the timer without synthetic keyboard input");
   controller.Update(2300000,1204000,true,true,true,10);Check(power.Offs==2,"Question dictation, speech, or an active chat can defer automatic screens-off");
   controller.Update(2300001,1204000,true,true,false,10);Check(power.Offs==3,"Deferred idle timeout can apply after active speech or chat ends");
   controller.Update(2301000,1204000,true,false,false,10);Check(!controller.Active && power.Ends==1,"Manual Windows lock suspends power control and releases the awake request");
   controller.Update(2400000,1204000,true,true,false,10);Check(controller.Active && !controller.DisplaysOff && power.Begins==2,"Normal Windows unlock starts a fresh timer without bypassing authentication");
   controller.Update(2401000,1204000,false,true,false,10);Check(!controller.Active && power.Ends==2,"Stopping listening or disabling mode releases power and restores Windows behavior");
   controller.Stop();Check(power.Ends==2,"Repeated stop is harmless");
   Check(ScreenIdleController.Elapsed(50,UInt32.MaxValue-49)==100,"Idle timing survives Windows tick-count wraparound");
   controller.Update(UInt32.MaxValue-300000,0,true,true,false,10);controller.Update(300000,0,true,true,false,10);Check(controller.DisplaysOff,"Ten-minute timeout remains correct across tick-count wraparound");
   controller.Wake(300001);power.Failure=true;bool failed=false;try { controller.Off(300002); } catch { failed=true; }Check(failed && power.HoldDisplay && !controller.DisplaysOff,"Failed screen-off request restores the display hold and reports failure");
   Check(ScreenIntent.Parse("Assistant, wake up")=="wake" && ScreenIntent.Parse("Assistant screens on")=="wake" && ScreenIntent.Parse("Assistant screens off")=="off","Recognize the three explicit screen commands");
   Check(ScreenIntent.Parse("wake up")==null && ScreenIntent.Parse("turn off")==null && ScreenIntent.Parse("Assistant unlock Windows")==null,"Wake-required mode ignores unnamed power commands and has no unlock action");
   Check(ScreenIntent.Parse("wake up",false)=="wake" && ScreenIntent.Parse("screens on",false)=="wake" && ScreenIntent.Parse("screens off",false)=="off","Optional-wake mode accepts the three direct screen commands");
   Check(ScreenIntent.Parse("Assistant wake up",false)=="wake" && ScreenIntent.Parse("Assistant screens off",false)=="off","Named commands still work when the wake phrase is optional");
   Check(ScreenIntent.Parse("please wake up",false)==null && ScreenIntent.Parse("unlock Windows",false)==null && ScreenIntent.Parse("turn off",false)==null,"Optional-wake mode stays limited to exact screen commands");
   var serializer=new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(ScreenPreferences));
   using(var stream=new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes("{\"Enabled\":true,\"TimeoutMinutes\":10}"))) { var old=(ScreenPreferences)serializer.ReadObject(stream);Check(old.RequireWakePhrase && old.TimeoutMinutes==10,"Older preferences retain the wake requirement until the user changes it"); }
   using(var stream=new System.IO.MemoryStream()) { serializer.WriteObject(stream,new ScreenPreferences { Enabled=true,TimeoutMinutes=10,RequireWakePhrase=false });stream.Position=0;var saved=(ScreenPreferences)serializer.ReadObject(stream);Check(!saved.RequireWakePhrase && saved.Enabled && saved.TimeoutMinutes==10,"Optional-wake preference survives serialization without changing the timeout"); }
   Check(System.Runtime.InteropServices.Marshal.SizeOf(typeof(ScreenWakeInput))==(IntPtr.Size==8?40:28),"Wake input matches the native Windows INPUT layout");
   var moves=DisplayWakeInput.Build(-1000,300,-1920,0,3840,1080);
   Check(moves.Length==2 && moves[0].Type==0 && moves[1].Type==0 && moves[0].Mouse.Flags==0xe001 && moves[1].Mouse.Flags==0xe001 && moves[0].Mouse.MouseData==0 && moves[1].Mouse.MouseData==0,"Wake fallback sends only two movement events, without clicks, scroll, or keyboard input");
   int originalX=(int)((long)moves[1].Mouse.X*3840/65536)-1920,originalY=(int)((long)moves[1].Mouse.Y*1080/65536);
   Check(originalX==-1000 && originalY==300,"Absolute return restores the original pointer position on negative-coordinate monitors");
   Check((int)((long)moves[0].Mouse.X*3840/65536)-1920==-999,"Wake fallback first moves by one physical pixel");
   var edge=DisplayWakeInput.Build(1919,1079,0,0,1920,1080);Check((int)((long)edge[0].Mouse.X*1920/65536)==1918 && (int)((long)edge[1].Mouse.X*1920/65536)==1919,"Screen-edge wake movement stays inward and returns to the original pixel");
   var held=DisplayWakeInput.Build(500,300,0,0,1920,1080,true);Check(held[0].Mouse.X==held[1].Mouse.X && held[0].Mouse.Y==held[1].Mouse.Y,"Held mouse buttons use stationary activity rather than dragging");
   return 0;
  } catch(Exception ex) { Console.Error.WriteLine(ex);return 1; }
 }
}
