using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace JarvisDesk {
 public sealed class ScreenRuntimeStatus { public int ProcessId,TimeoutMinutes;public bool Enabled,Listening,Active,DisplaysOff,RequireWakePhrase;public string Error,LastCommand,LastCommandUtc; }
 public partial class Desk {
  ScreenPreferences screenPreferences=new ScreenPreferences();
  ScreenIdleController screenController;
  WindowsScreenPower screenPower;
  Border screenDrawer;
  Button screenSettingsLink;
  CheckBox screenEnabled;
  TextBox screenTimeout;
  TextBlock screenStatus,screenCommandHelp;
  DateTime nextScreenCheck,lastScreenCommand,screenRetry;
  string screenError="";
  bool screenLoaded;
  string lastScreenStatus="";
  string screenLastCommand="",screenLastCommandUtc="";
  string ScreenSettingsPath { get { return Path.Combine(dataDir,"screens.json"); } }
  void CreateScreenPanel(Canvas stage) {
   var form=new StackPanel { Margin=new Thickness(20) };
   form.Children.Add(Label("SCREENS / VOICE WAKE",24,cyan));
   form.Children.Add(Label("Let the screens turn off while " + AssistantIdentity.CurrentName + " stays awake and listens for you.",13,muted));
   screenEnabled=new CheckBox { Content="Enable voice screen control and start listening with " + AssistantIdentity.CurrentName,Foreground=cyan,FontSize=12,Margin=new Thickness(0,10,0,16) };form.Children.Add(screenEnabled);
   form.Children.Add(Label("INACTIVITY TIMEOUT / MINUTES",11,cyan));screenTimeout=new TextBox { Width=100,HorizontalAlignment=HorizontalAlignment.Left,MaxLength=3,Height=30,Padding=new Thickness(6),Background=Ink(3,19,27),Foreground=Brushes.White,BorderBrush=Ink(0,103,115),Margin=new Thickness(0,0,0,15) };form.Children.Add(screenTimeout);
   screenCommandHelp=Label("",13,cyan);form.Children.Add(screenCommandHelp);
   form.Children.Add(Label("Windows stays unlocked while this mode is listening. Anyone at the computer can use it. Win+L still locks Windows and requires your usual sign-in. STOP LISTENING or closing " + AssistantIdentity.CurrentName + " releases the awake request and restores the screen saver.",12,muted));
   screenStatus=Label("",11,cyan);screenStatus.MinHeight=46;form.Children.Add(screenStatus);
   var buttons=new WrapPanel();buttons.Children.Add(CompactButton("SAVE",SaveScreenSettings));buttons.Children.Add(CompactButton("WAKE SCREENS",delegate { ScreenCommand("wake",false); }));buttons.Children.Add(CompactButton("SCREENS OFF",delegate { ScreenCommand("off",false); }));buttons.Children.Add(CompactButton("CLOSE / ESC",CloseScreenPanel));form.Children.Add(buttons);
   screenDrawer=PanelBox(form);screenDrawer.Visibility=Visibility.Collapsed;Place(stage,screenDrawer,345,140,760,505);
  }
  void InitializeScreenControl() {
   try { screenPreferences=ScreenPreferences.Load(ScreenSettingsPath); } catch { Log("Screen settings could not be loaded. Open SCREENS to configure them."); }
   screenPreferences.RequireWakePhrase=false;UpdateWakeGuidance();
   screenPower=new WindowsScreenPower(Path.Combine(dataDir,"screen-control-restore.json"));screenController=new ScreenIdleController(screenPower);
   try { screenPower.Recover(); } catch(Exception ex) { screenError=ex.Message;Log(screenError); }
   screenLoaded=true;
   Loaded+=delegate { if(screenPreferences.Enabled && recognizer!=null && !continuous) ToggleListen();RefreshScreenControl(); };
   Closing+=delegate { try { screenController.Stop();screenPower.End(); } catch { /* Recovery record is retained for the next launch. */ } WriteScreenStatus(); };
  }
  void OpenScreenSettings() { screenEnabled.IsChecked=screenPreferences.Enabled;screenTimeout.Text=screenPreferences.TimeoutMinutes.ToString(CultureInfo.InvariantCulture);screenStatus.Text=screenError.Length>0?screenError:ScreenModeDescription();appDrawer.Visibility=Visibility.Collapsed;sensorDrawer.Visibility=Visibility.Collapsed;CloseCommutePanel();screenDrawer.Visibility=Visibility.Visible; }
  void CloseScreenPanel() { screenDrawer.Visibility=Visibility.Collapsed; }
  void SaveScreenSettings() {
   int minutes;if(!Int32.TryParse(screenTimeout.Text,NumberStyles.None,CultureInfo.InvariantCulture,out minutes)) { screenStatus.Text="Enter a whole number of minutes.";return; }
   try {
    var settings=new ScreenPreferences { Enabled=screenEnabled.IsChecked==true,TimeoutMinutes=minutes,RequireWakePhrase=false };settings.Save(ScreenSettingsPath);screenPreferences=settings;screenError="";screenRetry=DateTime.MinValue;
    if(settings.Enabled && !continuous) { if(questionListening) EndQuestionDictation();if(!continuous) ToggleListen(); }
    RefreshScreenControl();TouchScreenActivity();screenStatus.Text=screenError.Length>0?screenError:ScreenModeDescription();
   } catch(Exception ex) { screenStatus.Text=ex is ArgumentException?ex.Message:"Screen settings could not be saved. Try again."; }
  }
  void UpdateWakeGuidance() {
   screenCommandHelp.Text="Say: wake up / screens off\nKeyboard, mouse, and accepted voice commands restart the timer.";
   screenCommandHelp.ToolTip="Say commands directly while listening. No assistant name is required.";
   heard.Text="SAY “OPEN NOTEPAD” OR “WAKE UP”";
  }
  string ScreenModeDescription() { return !screenPreferences.Enabled?"Voice screen control is off.":screenController!=null && screenController.Active?screenController.DisplaysOff?"Screens off / listening for wake up.":"Active / screens off after "+screenPreferences.TimeoutMinutes+" minutes. Windows stays unlocked.":"Paused / turn on LISTEN and keep Windows unlocked."; }
  void RefreshScreenControl() { nextScreenCheck=DateTime.MinValue;CheckScreenControl(); }
  void CheckScreenControl() {
   if(!screenLoaded || closing || DateTime.UtcNow<nextScreenCheck) return;nextScreenCheck=DateTime.UtcNow.AddSeconds(1);
   try {
    bool enabled=screenPreferences.Enabled && (continuous || (questionListening && resumeLaunchListening));
    if(enabled && DateTime.UtcNow<screenRetry) return;
    bool unlocked=enabled && WindowsScreenPower.Unlocked;
    screenController.Update(WindowsScreenPower.Now,enabled?WindowsScreenPower.LastInput:0,enabled,unlocked,speaking || questionListening || chatBusy,screenPreferences.TimeoutMinutes);
    screenSettingsLink.Content="◉  SCREENS / "+(screenPreferences.Enabled?screenPreferences.TimeoutMinutes+" MIN":"OFF");screenSettingsLink.ToolTip=ScreenModeDescription();
    if(screenDrawer.Visibility==Visibility.Visible && screenError.Length==0) screenStatus.Text=ScreenModeDescription();
   } catch(Exception ex) { screenError=ex.Message;screenRetry=DateTime.UtcNow.AddMinutes(1);try { screenController.Stop(); } catch { } screenSettingsLink.ToolTip=screenError;screenStatus.Text=screenError;Log("Screen control: "+screenError); }
   finally { WriteScreenStatus(); }
  }
  void WriteScreenStatus() {
   try {
    string json=ChatJson.Serializer().Serialize(new ScreenRuntimeStatus { ProcessId=System.Diagnostics.Process.GetCurrentProcess().Id,Enabled=screenPreferences.Enabled,TimeoutMinutes=screenPreferences.TimeoutMinutes,Listening=continuous && running,Active=screenController!=null && screenController.Active,DisplaysOff=screenController!=null && screenController.DisplaysOff,RequireWakePhrase=screenPreferences.RequireWakePhrase,Error=screenError,LastCommand=screenLastCommand,LastCommandUtc=screenLastCommandUtc });
    if(json==lastScreenStatus) return;string path=Path.Combine(dataDir,"screen-status.json"),temp=path+".tmp";File.WriteAllText(temp,json);if(File.Exists(path)) File.Replace(temp,path,null);else File.Move(temp,path);lastScreenStatus=json;
   } catch { /* Status reporting cannot interrupt voice screen control. */ }
  }
  void TouchScreenActivity() { if(screenLoaded && screenController!=null && screenController.Active) screenController.Touch(WindowsScreenPower.Now); }
  void ScreenCommand(string command,bool spoken) {
   if(!screenPreferences.Enabled) { Log("Enable SCREENS / VOICE WAKE first.");return; }
   if(!WindowsScreenPower.Unlocked) return;
   RefreshScreenControl();
   if(!screenController.Active) { Log(screenError.Length>0?screenError:"Turn on LISTEN to use voice screen control.");return; }
   try {
    screenLastCommand=command;screenLastCommandUtc=DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture);
    if(command=="wake") { screenController.Wake(WindowsScreenPower.Now);screenError="";Log("Screen wake signals sent / inactivity timer restarted.");if(spoken && !speaking) SpeakText("Bringing your screens online."); }
    else { screenController.Off(WindowsScreenPower.Now);Log("Screens off / voice wake is listening.");if(spoken && !speaking) SpeakText("Screens off."); }
   } catch(Exception ex) { screenError=ex.Message;Log(screenError);screenStatus.Text=screenError; }
   finally { WriteScreenStatus(); }
  }
 }
}
