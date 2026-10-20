using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace JarvisDesk {
 [DataContract] public sealed class ChatNamePreferences {
  [DataMember] public string Name="Assistant";
  public static string Validate(string name) {
   name=Regex.Replace((name??"").Trim(),@"\s+"," ");
   if(!Regex.IsMatch(name,@"^[\p{L}\p{N}][\p{L}\p{M}\p{N} .'’_\-]{0,23}$")) throw new ArgumentException("Choose a name with 1–24 letters, numbers, spaces, periods, apostrophes, hyphens or underscores.");
   return name;
  }
  public static ChatNamePreferences Load(string path) {
   try { using(var file=File.OpenRead(path)) { var value=(ChatNamePreferences)new DataContractJsonSerializer(typeof(ChatNamePreferences)).ReadObject(file);value.Name=Validate(value.Name);return value; } }
   catch { return new ChatNamePreferences(); }
  }
  public void Save(string path) {
   Name=Validate(Name);Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";
   using(var file=File.Create(temp)) new DataContractJsonSerializer(typeof(ChatNamePreferences)).WriteObject(file,this);
   if(File.Exists(path)) File.Replace(temp,path,null);else File.Move(temp,path);
  }
 }
 public partial class Desk {
  ChatNamePreferences chatNamePreferences=new ChatNamePreferences();
  TextBlock chatHeading;
  Button chatNameButton,chatNavigationButton;
  string ChatNamePath { get { return Path.Combine(dataDir,"chat-name.json"); } }
  string ChatDisplayName { get { return chatNamePreferences.Name; } }
  string ChatDisplayUpper { get { return ChatDisplayName.ToUpperInvariant(); } }
  void ApplyChatName() {
   chatHeading.Text=ChatDisplayUpper+" / CONVERSATION";chatHeading.ToolTip=ChatDisplayName;
   chatHeading.TextWrapping=TextWrapping.NoWrap;chatHeading.TextTrimming=TextTrimming.CharacterEllipsis;
   chatNavigationButton.Content=new TextBlock { Text=ChatDisplayUpper+" / CHAT",TextWrapping=TextWrapping.NoWrap,TextTrimming=TextTrimming.CharacterEllipsis,MaxWidth=220 };
   chatNavigationButton.Width=240;chatNavigationButton.ToolTip=ChatDisplayName+" / conversation";
   chatTranscript.ToolTip="Your conversation with "+ChatDisplayName+". Questions and recent chat context are sent to OpenAI.";
   PaintChatHistory();
  }
  public void OpenChatNameSettings() {
   if(chatBusy || questionListening) { chatStatus.Text="Finish or cancel the current question before changing the chat name.";return; }
   var dialog=new Window { Title="Name your chat",Owner=this,Width=530,Height=335,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Ink(3,13,20),Foreground=cyan };
   dialog.Resources.Add(typeof(Button),Resources[typeof(Button)]);
   var form=new StackPanel { Margin=new Thickness(24) };dialog.Content=form;
   form.Children.Add(Label("NAME YOUR CHAT",22,cyan));
   form.Children.Add(Label("Choose a name for the conversation heading and reply labels. Your personal startup greeting keeps your own name. The chat name is also included with future questions to ChatGPT.",12,muted));
   var input=new TextBox { Name="ChatNameInput",Text=ChatDisplayName,MaxLength=24,Padding=new Thickness(7),FontSize=18,Margin=new Thickness(0,0,0,12) };form.Children.Add(input);
   var status=Label("Examples: Nova, Luna, Echo",11,muted);form.Children.Add(status);
   var buttons=new WrapPanel();form.Children.Add(buttons);
   buttons.Children.Add(Action("SAVE",delegate {
    try { var value=new ChatNamePreferences { Name=input.Text };value.Save(ChatNamePath);chatNamePreferences=value;ApplyChatName();chatStatus.Text="Chat name saved.";dialog.Close(); }
    catch(Exception ex) { status.Text=ex.Message; }
   }));buttons.Children.Add(Action("CANCEL",dialog.Close));
   dialog.Loaded+=delegate { input.Focus();input.SelectAll(); };dialog.ShowDialog();
  }
 }
}
