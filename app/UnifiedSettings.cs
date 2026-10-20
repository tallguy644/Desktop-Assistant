using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace JarvisDesk {
 public partial class Desk {
  Window setupWindow,setupPageLifetime;
  ContentControl setupContent;
  TextBlock setupHeading;
  readonly Dictionary<string,Button> setupNavigation=new Dictionary<string,Button>();
  readonly List<Action> setupRestores=new List<Action>();
  string setupSection="";
  bool buildingSetupPage,releasingSetupPage,setupCloseHooked;
  bool InSetup(string section){return setupWindow!=null && setupSection==section;}

  public void OpenSettings(){OpenUnifiedSettings("colors");}
  public void OpenUnifiedSettings(string section){
   if(setupWindow==null){
    var area=SystemParameters.WorkArea;
    setupWindow=new Window {Title="Settings / Desktop Assistant",Owner=this,Width=Math.Min(1100,area.Width-24),Height=Math.Min(790,area.Height-24),MinWidth=Math.Min(840,area.Width-24),MinHeight=Math.Min(520,area.Height-24),WindowStartupLocation=WindowStartupLocation.CenterScreen,Background=HologramSurface(),Foreground=cyan,FontFamily=new FontFamily("Segoe UI")};
    setupWindow.Resources.Add(typeof(Button),Resources[typeof(Button)]);
    var shell=new DockPanel {Margin=new Thickness(22)};setupWindow.Content=shell;
    var header=new DockPanel {Margin=new Thickness(0,0,0,18)};DockPanel.SetDock(header,Dock.Top);shell.Children.Add(header);
    var done=Action("DONE",delegate{setupWindow.Close();});DockPanel.SetDock(done,Dock.Right);header.Children.Add(done);
    var intro=new StackPanel();intro.Children.Add(Label("SETTINGS / CONTROL CENTER",23,cyan));intro.Children.Add(Label("Save changes within each section. Switching sections discards unsaved edits.",11,muted));header.Children.Add(intro);
    var body=new Grid();body.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(205)});body.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});shell.Children.Add(body);
    var navigation=new StackPanel {Margin=new Thickness(0,0,18,0)};var navScroll=new ScrollViewer {Content=navigation,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};body.Children.Add(navScroll);
    string[] keys={"colors","voice","greeting","chat-name","chat","apps","calendar","commute","thermal","sensors","screens","desktop","pc","privacy"};
    string[] names={"Appearance & colors","Voice & speech","Your name & greeting","Chat name","Chat account & model","Applications & mappings","Google Calendar","Work & commute","Temperature alerts","Sensor readings & setup",ScreenSettingsTitle,"Desktop & startup","Set up this PC","Privacy"};
    for(int i=0;i<keys.Length;i++){string key=keys[i];var button=Action(names[i],delegate{SelectSetupSection(key);});button.Name="Setup_"+key.Replace('-','_');button.Width=182;button.FontSize=11;button.Padding=new Thickness(10,10,6,10);button.HorizontalContentAlignment=HorizontalAlignment.Left;button.Margin=new Thickness(0,0,0,5);setupNavigation.Add(key,button);navigation.Children.Add(button);}
    var page=new DockPanel();Grid.SetColumn(page,1);body.Children.Add(page);setupHeading=Label("",13,muted);setupHeading.Margin=new Thickness(4,0,0,12);DockPanel.SetDock(setupHeading,Dock.Top);page.Children.Add(setupHeading);setupContent=new ContentControl {HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch};page.Children.Add(setupContent);
    setupWindow.PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Escape){e.Handled=true;setupWindow.Close();}};
    setupWindow.Closed+=delegate{ReleaseSetupSection();setupWindow=null;setupContent=null;setupHeading=null;setupNavigation.Clear();};
    if(!setupCloseHooked){Closing+=CloseSetupWithApp;setupCloseHooked=true;}
    setupWindow.Show();
   }
   SelectSetupSection(section);if(!setupWindow.IsVisible)setupWindow.Show();if(setupWindow.WindowState==WindowState.Minimized)setupWindow.WindowState=WindowState.Normal;setupWindow.Activate();
  }
  void CloseSetupWithApp(object sender,System.ComponentModel.CancelEventArgs e){if(setupWindow!=null)setupWindow.Close();Closing-=CloseSetupWithApp;}
  void ReleaseSetupSection(){
   releasingSetupPage=true;setupSection="";
   if(setupContent!=null)setupContent.Content=null;
   var lifetime=setupPageLifetime;setupPageLifetime=null;if(lifetime!=null)lifetime.Close();
   for(int i=setupRestores.Count-1;i>=0;i--)setupRestores[i]();setupRestores.Clear();
   CloseThermalSettings();CloseCalendarSetup();CloseScreenPanel();CloseCommutePanel();
   releasingSetupPage=false;
  }
  void SelectSetupSection(string section){
   if(!setupNavigation.ContainsKey(section))section="colors";
   if(setupSection==section)return;
   ReleaseSetupSection();setupSection=section;setupHeading.Text=setupNavigation[section].Content.ToString().ToUpperInvariant();
   foreach(var entry in setupNavigation){entry.Value.Background=entry.Key==section?Ink(13,52,65):Brushes.Transparent;entry.Value.BorderBrush=entry.Key==section?cyan:Ink(16,43,54);}
   buildingSetupPage=true;
   try{
    switch(section){
     case "colors":OpenAppearanceSettings();break;
     case "voice":OpenVoiceSettings();break;
     case "greeting":OpenNameSettings();break;
     case "chat-name":OpenChatNameSettings();break;
     case "desktop":OpenDesktopSettings();break;
     case "pc":OpenComputerSetup(null);break;
     case "apps":appList.SelectedIndex=-1;nameBox.Clear();pathBox.Clear();MountSetupElement((FrameworkElement)appDrawer.Child);break;
     case "sensors":
      var sensors=new DockPanel {Margin=new Thickness(20)};var guide=new StackPanel();guide.Children.Add(Label("LOCAL HARDWARE MONITOR",23,cyan));guide.Children.Add(Label("Keep Libre Hardware Monitor running with its remote web server enabled at 127.0.0.1:8085. Temperatures display in Fahrenheit. Fan and pump channels use their hardware labels until you confirm the physical connections.",12,muted));DockPanel.SetDock(guide,Dock.Top);sensors.Children.Add(guide);MoveSetupElement((FrameworkElement)sensorDrawer.Child,sensors);setupContent.Content=sensors;break;
     case "screens":OpenScreenSettings();MountSetupElement((FrameworkElement)screenDrawer.Child);screenDrawer.Visibility=Visibility.Collapsed;break;
     case "commute":OpenCommutePanel();MountSetupElement((FrameworkElement)commuteDrawer.Child);commuteDrawer.Visibility=Visibility.Collapsed;break;
     case "thermal":ToggleThermalSettings();thermalReadings.Visibility=Visibility.Visible;MountSetupElement(thermalSettings);break;
     case "calendar":
      agendaLink.Clear();agendaSetup.Visibility=Visibility.Visible;var calendar=new StackPanel {Margin=new Thickness(24)};calendar.Children.Add(Label("GOOGLE CALENDAR",24,cyan));calendar.Children.Add(Label("Read-only sync using your private iCal link. Your saved link stays encrypted on this PC. Leave the field blank to keep the current connection.",12,muted));MoveSetupElement(agendaSetup,calendar);MoveSetupElement(agendaStatus,calendar);setupContent.Content=new ScrollViewer {Content=calendar,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};break;
     case "chat":
      var chat=new StackPanel {Margin=new Thickness(24)};chat.Children.Add(Label("CHAT ACCOUNT / MODEL",24,cyan));MoveSetupElement(chatAccount,chat);MoveSetupElement((FrameworkElement)chatSettings.Content,chat);MoveSetupElement(chatStatus,chat);setupContent.Content=new ScrollViewer {Content=chat,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};break;
     case "privacy":setupContent.Content=new ScrollViewer {Content=Label(File.ReadAllText(Path.Combine(Path.GetDirectoryName(typeof(Desk).Assembly.Location),"PRIVACY.md")),13,cyan),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(24)};break;
    }
   }catch(Exception ex){setupContent.Content=Label("This section could not open. "+ex.Message,13,cyan);}finally{buildingSetupPage=false;}
  }
  // Share the existing controls and handlers, restoring their original HUD parents on exit.
  // This keeps validation, live status, protected credentials and account operations in one implementation.
  void DetachSetupElement(FrameworkElement element){
   var parent=element.Parent;
   var panel=parent as Panel;if(panel!=null){int index=panel.Children.IndexOf(element);panel.Children.Remove(element);setupRestores.Add(delegate{var current=element.Parent as Panel;if(current!=null)current.Children.Remove(element);panel.Children.Insert(Math.Min(index,panel.Children.Count),element);});return;}
   var decorator=parent as Decorator;if(decorator!=null){decorator.Child=null;setupRestores.Add(delegate{var current=element.Parent as Panel;if(current!=null)current.Children.Remove(element);decorator.Child=element;});return;}
   var content=parent as ContentControl;if(content!=null){content.Content=null;setupRestores.Add(delegate{var current=element.Parent as Panel;if(current!=null)current.Children.Remove(element);content.Content=element;});return;}
   throw new InvalidOperationException("The settings control has no supported parent.");
  }
  void MoveSetupElement(FrameworkElement element,Panel target){DetachSetupElement(element);target.Children.Add(element);}
  void MountSetupElement(FrameworkElement element){DetachSetupElement(element);setupContent.Content=element is ScrollViewer?(object)element:new ScrollViewer {Content=element,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};setupRestores.Add(delegate{var scroll=element.Parent as ScrollViewer;if(scroll!=null)scroll.Content=null;});}
  void PresentSetupDialog(Window dialog){
   setupPageLifetime=dialog;var content=dialog.Content;dialog.Content=null;setupContent.Content=content;
   dialog.Closed+=delegate{if(setupPageLifetime==dialog)setupPageLifetime=null;if(!releasingSetupPage && setupContent!=null){setupSection="";setupContent.Content=Label("Choose a section to continue.",14,cyan);}};
  }
 }
}
