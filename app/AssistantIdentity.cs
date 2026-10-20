using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace JarvisDesk {
 public static class AssistantIdentity {
  public static string CurrentName { get { return "Assistant"; } }
  public static string Upper { get { return "ASSISTANT"; } }
  public static string WakeName { get { return "assistant"; } }
  public static string Normalize(string text) { return Regex.Replace(Regex.Replace((text??"").ToLowerInvariant(),@"[^a-z0-9 ]"," "),@"\s+"," ").Trim(); }
  public static bool StripWake(ref string text) {
   string prefix=WakeName+" "; if(!text.StartsWith(prefix,StringComparison.Ordinal)) return false;
   text=text.Substring(prefix.Length);return true;
  }
 }
 [DataContract] public sealed class PersonalGreeting {
  [DataMember] public string Name="";
  public static string Validate(string name) {
   name=Regex.Replace((name??"").Trim(),@"\s+"," ");
   if(name.Length>0 && !Regex.IsMatch(name,@"^[\p{L}][\p{L}\p{M} .'’\-]{0,39}$")) throw new ArgumentException("Use up to 40 letters, spaces, periods, apostrophes or hyphens. You can also leave your name blank.");
   return name;
  }
  public static PersonalGreeting Load(string path) {
   try { using(var stream=File.OpenRead(path)) { var value=(PersonalGreeting)new DataContractJsonSerializer(typeof(PersonalGreeting)).ReadObject(stream);value.Name=Validate(value.Name);return value; } }
   catch { return new PersonalGreeting(); }
  }
  public void Save(string path) {
   Name=Validate(Name);Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";
   using(var stream=File.Create(temp)) new DataContractJsonSerializer(typeof(PersonalGreeting)).WriteObject(stream,this);
   if(File.Exists(path)) File.Replace(temp,path,null);else File.Move(temp,path);
  }
  public static string Build(string name,DateTime localTime) {
   name=Validate(name);
   string greeting=localTime.Hour>=5 && localTime.Hour<12?"Good morning":localTime.Hour>=12 && localTime.Hour<17?"Good afternoon":"Good evening";
   return greeting+(name.Length>0?", "+name:"")+". Systems online.";
  }
 }
 public partial class Desk {
  PersonalGreeting personalGreeting=new PersonalGreeting();
  string PersonalGreetingPath { get { return Path.Combine(dataDir,"personal-greeting.json"); } }
  public bool NeedsNameSetup { get { return !File.Exists(PersonalGreetingPath); } }
  public string OnlineAnnouncement { get { return PersonalGreeting.Build(personalGreeting.Name,DateTime.Now); } }
  public void OpenNameSettings() {
   if(!buildingSetupPage){OpenUnifiedSettings("greeting");return;}
   var dialog=new Window { Title="Your name / personal greeting",Owner=this,Width=560,Height=410,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Ink(3,13,20),Foreground=cyan };
   dialog.Resources.Add(typeof(Button),Resources[typeof(Button)]);
   var form=new StackPanel { Margin=new Thickness(24) };dialog.Content=form;
   form.Children.Add(Label("WHAT SHOULD I CALL YOU?",22,cyan));
   form.Children.Add(Label("Enter your name for a personal startup greeting. Morning, afternoon and evening follow your computer's local time. Leave blank for a greeting without a name.",12,muted));
   var input=new TextBox { Name="PersonalNameInput",Text=personalGreeting.Name,MaxLength=40,Padding=new Thickness(7),FontSize=18,Margin=new Thickness(0,0,0,12) };form.Children.Add(input);
   var status=Label(PersonalGreeting.Build(personalGreeting.Name,DateTime.Now),12,muted);form.Children.Add(status);
   input.TextChanged+=delegate { try { status.Text=PersonalGreeting.Build(input.Text,DateTime.Now); } catch(Exception ex) { status.Text=ex.Message; } };
   var buttons=new WrapPanel();form.Children.Add(buttons);
   buttons.Children.Add(Action("SAVE",delegate {
    try { var value=new PersonalGreeting { Name=input.Text };value.Save(PersonalGreetingPath);personalGreeting=value;Log("Personal greeting saved.");subtitle.Text="Personal greeting saved.";dialog.Close(); }
    catch(Exception ex) { status.Text=ex.Message; }
   }));
   buttons.Children.Add(Action("PREVIEW",delegate { try { SpeakText(PersonalGreeting.Build(input.Text,DateTime.Now)); } catch(Exception ex) { status.Text=ex.Message; } }));
   buttons.Children.Add(Action("CANCEL",dialog.Close));
   form.Children.Add(Label("Your name is saved on this PC. With an online voice, greeting text is sent to your voice provider when spoken.",10,muted));
   form.Loaded+=delegate { input.Focus();input.SelectAll(); };PresentSetupDialog(dialog);
  }
 }
}
