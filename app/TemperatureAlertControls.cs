using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace JarvisDesk {
 public partial class Desk {
  TemperatureAlertPreferences temperaturePreferences=new TemperatureAlertPreferences();
  readonly TemperatureAlertEngine temperatureAlertEngine=new TemperatureAlertEngine();
  StackPanel thermalReadings, thermalSettings;
  TextBlock thermalBadge, thermalSettingsStatus;
  CheckBox thermalEnabled;
  TextBox cpuAlertLimit, gpuAlertLimit, thermalCooldown;
  bool temperatureAlertsLoaded, thermalVoiceInProgress;
  string queuedChatSpeech;
  string TemperatureSettingsPath { get { return Path.Combine(dataDir,"temperature-alerts.json"); } }
  void InitializeTemperatureAlerts() {
   try { temperaturePreferences=TemperatureAlertPreferences.Load(TemperatureSettingsPath); if(!File.Exists(TemperatureSettingsPath)) temperaturePreferences.Save(TemperatureSettingsPath); }
   catch { temperaturePreferences=new TemperatureAlertPreferences(); Log("Temperature alerts use defaults: CPU and GPU above 175 °F. Open TEMP ALERTS to check settings."); }
   temperatureAlertsLoaded=true; UpdateThermalBadge();
  }
  Grid BuildThermalReadings() {
   var host=new Grid(); thermalReadings=new StackPanel();
   thermalReadings.Children.Add(Label("CPU PACKAGE",11,cyan)); cpuTemp=Label("—",35,cyan); thermalReadings.Children.Add(cpuTemp);
   thermalReadings.Children.Add(Label("GPU CORE",11,cyan)); gpuTemp=Label("—",35,cyan); thermalReadings.Children.Add(gpuTemp);
   var actions=new StackPanel { Orientation=Orientation.Horizontal };
   actions.Children.Add(ChatControl("TEMP ALERTS",116,ToggleThermalSettings)); thermalBadge=Label("ON / 175°F",9,muted); thermalBadge.Width=82; thermalBadge.VerticalAlignment=VerticalAlignment.Center; thermalBadge.Margin=new Thickness(0,0,0,5); actions.Children.Add(thermalBadge); thermalReadings.Children.Add(actions); host.Children.Add(thermalReadings);
   thermalSettings=new StackPanel { Visibility=Visibility.Collapsed };
   thermalEnabled=new CheckBox { Content="Spoken temperature alerts",Foreground=cyan,FontSize=11,Margin=new Thickness(0,0,0,6) }; thermalSettings.Children.Add(thermalEnabled);
   cpuAlertLimit=ThermalNumber(thermalSettings,"CPU above °F",70); gpuAlertLimit=ThermalNumber(thermalSettings,"GPU above °F",70); thermalCooldown=ThermalNumber(thermalSettings,"Repeat / minutes",55);
   thermalSettingsStatus=Label("2 high samples · 9°F reset margin",9,muted); thermalSettingsStatus.Margin=new Thickness(0,0,0,4); thermalSettings.Children.Add(thermalSettingsStatus);
   var buttons=new WrapPanel(); buttons.Children.Add(ChatControl("SAVE",60,SaveThermalSettings)); buttons.Children.Add(ChatControl("CANCEL",65,CloseThermalSettings)); buttons.Children.Add(ChatControl("TEST",60,TestThermalSpeech)); thermalSettings.Children.Add(buttons); host.Children.Add(thermalSettings); return host;
  }
  TextBox ThermalNumber(StackPanel parent,string title,double width) {
   var row=new StackPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,5) };
   var label=Label(title,10,cyan); label.Width=112; label.VerticalAlignment=VerticalAlignment.Center; label.Margin=new Thickness(0); row.Children.Add(label);
   var input=new TextBox { Width=width,Height=23,MaxLength=3,Padding=new Thickness(4,2,4,2),FontSize=11,Background=Ink(3,19,27),Foreground=Brushes.White,BorderBrush=Ink(0,103,115) }; row.Children.Add(input); parent.Children.Add(row); return input;
  }
  void ToggleThermalSettings() {
   if(thermalSettings.Visibility==Visibility.Visible) { CloseThermalSettings(); return; }
   thermalEnabled.IsChecked=temperaturePreferences.Enabled; cpuAlertLimit.Text=temperaturePreferences.CpuLimitF.ToString(CultureInfo.InvariantCulture); gpuAlertLimit.Text=temperaturePreferences.GpuLimitF.ToString(CultureInfo.InvariantCulture); thermalCooldown.Text=temperaturePreferences.CooldownMinutes.ToString(CultureInfo.InvariantCulture);
   thermalSettingsStatus.Text="2 high samples · 9°F reset margin"; thermalReadings.Visibility=Visibility.Collapsed; thermalSettings.Visibility=Visibility.Visible;
  }
  void CloseThermalSettings() { thermalSettings.Visibility=Visibility.Collapsed; thermalReadings.Visibility=Visibility.Visible; }
  void SaveThermalSettings() {
   int cpu,gpu,cooldown;
   if(!Int32.TryParse(cpuAlertLimit.Text,NumberStyles.None,CultureInfo.InvariantCulture,out cpu) || !Int32.TryParse(gpuAlertLimit.Text,NumberStyles.None,CultureInfo.InvariantCulture,out gpu) || !Int32.TryParse(thermalCooldown.Text,NumberStyles.None,CultureInfo.InvariantCulture,out cooldown)) { thermalSettingsStatus.Text="Enter whole numbers for each setting."; return; }
   var candidate=new TemperatureAlertPreferences { Enabled=thermalEnabled.IsChecked==true,CpuLimitF=cpu,GpuLimitF=gpu,CooldownMinutes=cooldown };
   try { candidate.Save(TemperatureSettingsPath); temperaturePreferences=candidate; temperatureAlertEngine.Reset(); UpdateThermalBadge(); CloseThermalSettings(); Log("Temperature alert settings saved."); }
   catch(ArgumentException) { thermalSettingsStatus.Text="Limits 100–250°F; repeat 1–60 min."; } catch { thermalSettingsStatus.Text="Could not save temperature alert settings."; }
  }
  void ObserveTemperatures(SystemSnapshot snapshot) {
   if(!temperatureAlertsLoaded) return;
   temperatureAlertEngine.Observe(snapshot.CpuTemperatureCelsius,snapshot.GpuTemperatureCelsius,snapshot.ReadAt,DateTime.UtcNow,temperaturePreferences); UpdateThermalBadge();
  }
  void UpdateThermalBadge() {
   var engine=temperatureAlertEngine; bool active=temperaturePreferences.Enabled;
   cpuTemp.Foreground=active && engine.CpuHigh?Brushes.OrangeRed:cyan; gpuTemp.Foreground=active && engine.GpuHigh?Brushes.OrangeRed:cyan;
   thermalBadge.Foreground=active && (engine.CpuHigh || engine.GpuHigh)?Brushes.OrangeRed:muted;
   thermalBadge.Text=!active?"OFF":engine.CpuHigh && engine.GpuHigh?"BOTH HIGH":engine.CpuHigh?"CPU HIGH":engine.GpuHigh?"GPU HIGH":!engine.CpuAvailable && !engine.GpuAvailable?"NO FEED":engine.CpuAvailable && engine.GpuAvailable?"ON":"PARTIAL";
   thermalBadge.ToolTip="Spoken alerts "+(active?"enabled":"disabled")+". CPU above "+temperaturePreferences.CpuLimitF+" °F; GPU above "+temperaturePreferences.GpuLimitF+" °F. Two high readings; repeat at most every "+temperaturePreferences.CooldownMinutes+" minutes per component.";
  }
  VoicePreferences ThermalVoice() {
   var source=voicePreferences;
   return new VoicePreferences { Provider=source.Provider,Region=source.Region,Voice=source.Voice,DesktopVoice=source.DesktopVoice,ProtectedKey=source.ProtectedKey,Rate=source.Rate,Volume=source.Volume,WindowsFallback=true };
  }
  void CheckThermalAlerts() {
   if(!temperatureAlertsLoaded || closing) return;
   var now=DateTime.UtcNow; temperatureAlertEngine.Expire(now); UpdateThermalBadge();
   if(!temperaturePreferences.Enabled || speaking || questionListening) return;
   var batch=temperatureAlertEngine.Due(now,temperaturePreferences); if(!batch.Any) return;
   temperatureAlertEngine.MarkAttempt(batch,now); thermalVoiceInProgress=true;
   Log("Temperature warning / "+(batch.Cpu && batch.Gpu?"CPU and GPU":batch.Cpu?"CPU":"GPU"));
   SpeakText(batch.Message,ThermalVoice(),false,delegate(string error) {
    thermalVoiceInProgress=false;
    if(error!=null) Log("Temperature alert speech failed. Check Voice Settings and sound output.");
    var pending=queuedChatSpeech; queuedChatSpeech=null;
    if(pending!=null && readChatAloud.IsChecked==true && !closing) SpeakChatReply(pending);
   });
  }
  void TestThermalSpeech() {
   if(speaking || questionListening) { thermalSettingsStatus.Text="Wait for speech or question listening to finish."; return; }
   SpeakText(AssistantIdentity.CurrentName + " temperature alert test. Spoken warnings are ready.",ThermalVoice(),false,delegate(string error) { thermalSettingsStatus.Text=error==null?"Voice test complete.":"Voice test failed. Check Voice Settings."; });
  }
 }
}
