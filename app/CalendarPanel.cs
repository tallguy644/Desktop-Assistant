using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace JarvisDesk {
 public partial class Desk {
  TextBlock agendaDate, agendaStatus;
  StackPanel agendaItems, agendaSetup;
  ScrollViewer agendaScroll;
  PasswordBox agendaLink;
  Button agendaManage, agendaRefresh, agendaSave, agendaRemove;
  string calendarUrl="";
  AgendaSnapshot agendaSnapshot;
  DateTime agendaDay, nextAgendaRefresh=DateTime.MaxValue;
  string agendaZone;
  bool agendaBusy, agendaLoaded, agendaOffline, agendaReplacing;
  int agendaGeneration;
  CancellationTokenSource agendaOperation;
  string CalendarLinkPath { get { return Path.Combine(dataDir,"calendar-link.protected"); } }
  string AgendaCachePath { get { return Path.Combine(dataDir,"calendar-agenda.protected"); } }
  void CreateCalendarPanel(Canvas stage) {
   var layout=new DockPanel();
   agendaDate=Label("",11,cyan); agendaDate.Margin=new Thickness(0,0,0,4); DockPanel.SetDock(agendaDate,Dock.Top); layout.Children.Add(agendaDate);
   var footer=new StackPanel(); agendaStatus=Label("CONNECT for your Google agenda",9,muted); agendaStatus.MinHeight=15; agendaStatus.MaxHeight=28; agendaStatus.Margin=new Thickness(0,3,0,3); agendaStatus.SetBinding(FrameworkElement.ToolTipProperty,new System.Windows.Data.Binding("Text") { Source=agendaStatus }); footer.Children.Add(agendaStatus);
   commuteSummary=Label("WORK / SET UP TRAVEL",9,muted); commuteSummary.MaxHeight=26; commuteSummary.Margin=new Thickness(0,1,0,2); footer.Children.Insert(0,commuteSummary);
   var actions=new WrapPanel(); var read=CalendarControl("▷ READ",46,ReadAgenda); read.ToolTip="Read today's agenda using your saved voice"; actions.Children.Add(read); agendaManage=CalendarControl("CONNECT",54,ToggleCalendarSetup); actions.Children.Add(agendaManage); agendaRefresh=CalendarControl("↻",22,RefreshCalendar); agendaRefresh.ToolTip="Refresh Google Calendar"; actions.Children.Add(agendaRefresh); var work=CalendarControl("WORK",36,OpenCommutePanel); work.ToolTip="Work route, arrival buffer, and departure reminders"; actions.Children.Add(work); footer.Children.Add(actions); DockPanel.SetDock(footer,Dock.Bottom); layout.Children.Add(footer);
   var content=new Grid(); agendaItems=new StackPanel(); agendaScroll=new ScrollViewer { Content=agendaItems, VerticalScrollBarVisibility=ScrollBarVisibility.Auto, HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled }; content.Children.Add(agendaScroll);
   agendaSetup=new StackPanel { Visibility=Visibility.Collapsed };
   var help=Label("PRIVATE CALENDAR LINK",9,muted); help.Margin=new Thickness(0,0,0,5); agendaSetup.Children.Add(help);
   agendaLink=new PasswordBox { Height=25, MaxLength=2048, Background=Brushes.Transparent, Foreground=cyan, BorderBrush=Ink(0,103,115), BorderThickness=new Thickness(0,0,0,1), Padding=new Thickness(3), Margin=new Thickness(0,0,0,6), ToolTip="Private Google Calendar Secret address in iCal format. Stored encrypted on this PC." }; agendaSetup.Children.Add(agendaLink);
   var saveRow=new WrapPanel(); agendaSave=CalendarControl("SAVE",52,SaveCalendarLink); saveRow.Children.Add(agendaSave); saveRow.Children.Add(CalendarControl("CANCEL",60,CancelCalendarSetup)); saveRow.Children.Add(CalendarControl("HELP",52,OpenCalendarHelp)); agendaSetup.Children.Add(saveRow);
   agendaRemove=CalendarControl("REMOVE LINK",120,RemoveCalendarLink); agendaRemove.Margin=new Thickness(0,4,0,0); agendaSetup.Children.Add(agendaRemove); content.Children.Add(agendaSetup); layout.Children.Add(content);
   Place(stage,ReferenceSection("TODAY / AGENDA",layout,208,240),76,102,208,240);
  }
  Button CalendarControl(string title,double width,Action action) {
   var button=CompactButton(title,action); button.Width=width; button.FontSize=9; button.Padding=new Thickness(0,4,0,4); button.Margin=new Thickness(0,0,3,0); button.IsEnabledChanged+=delegate { button.Opacity=button.IsEnabled?1:0.3; }; return button;
  }
  void InitializeCalendar() {
   agendaDay=DateTime.Today; agendaZone=TimeZoneInfo.Local.Id;
   try { if(File.Exists(CalendarLinkPath)) calendarUrl=CalendarFeed.ValidateLink(ChatGptClient.ReadProtected<CalendarSettings>(CalendarLinkPath).FeedUrl).AbsoluteUri; } catch { agendaStatus.Text="Saved link unavailable. CONNECT again."; }
   try { if(calendarUrl.Length>0 && File.Exists(AgendaCachePath)) { var saved=ChatGptClient.ReadProtected<AgendaSnapshot>(AgendaCachePath); if(saved!=null && saved.Matches(agendaDay,TimeZoneInfo.Local,CalendarFeed.SourceId(calendarUrl))) { agendaSnapshot=saved; agendaOffline=true; } } } catch { }
   agendaLoaded=true; PaintAgenda();
   if(calendarUrl.Length>0) { nextAgendaRefresh=DateTime.UtcNow; Loaded+=delegate { RefreshCalendar(); }; }
   Closing+=delegate { if(agendaOperation!=null) agendaOperation.Cancel(); };
  }
  void CheckCalendar() {
   if(!agendaLoaded) return;
   if(agendaDay!=DateTime.Today || agendaZone!=TimeZoneInfo.Local.Id) {
    agendaDay=DateTime.Today; agendaZone=TimeZoneInfo.Local.Id; agendaSnapshot=null; agendaOffline=false;
    agendaGeneration++; if(agendaOperation!=null) agendaOperation.Cancel(); agendaBusy=false; nextAgendaRefresh=DateTime.UtcNow; PaintAgenda();
   }
   if(calendarUrl.Length>0 && !agendaBusy && DateTime.UtcNow>=nextAgendaRefresh) RefreshCalendar();
  }
  void PaintAgenda() {
   agendaDate.Text=agendaDay.ToString("ddd · MMM d",CultureInfo.InvariantCulture).ToUpperInvariant();
   agendaItems.Children.Clear();
   if(agendaSnapshot==null) agendaItems.Children.Add(Label(calendarUrl.Length==0?"Your day, at a glance.\nConnect Google Calendar to see today's events here.":agendaOffline?"Calendar unavailable.\nUse REFRESH to try again.":"Loading today's events…",11,muted));
   else if(agendaSnapshot.Items.Count==0) agendaItems.Children.Add(Label("No calendar events today.",12,cyan));
   else foreach(var item in agendaSnapshot.Items) {
    var row=new StackPanel { Margin=new Thickness(0,2,0,8),ToolTip=item.TimeLabel(agendaDay)+(String.IsNullOrEmpty(item.Location)?"":"\n"+item.Location) };
    var time=Label(item.TimeLabel(agendaDay),9,cyan); time.Margin=new Thickness(0,0,0,3); row.Children.Add(time);
    var title=Label(item.Title,12,Brushes.White); title.FontFamily=new FontFamily("Segoe UI"); title.Margin=new Thickness(0); row.Children.Add(title);
    var entry=new Grid { Margin=new Thickness(0,1,0,2) }; var track=new Grid { Width=8,HorizontalAlignment=HorizontalAlignment.Left,IsHitTestVisible=false }; track.Children.Add(new System.Windows.Shapes.Rectangle { Width=1,Fill=Ink(0,65,74),Margin=new Thickness(0,8,0,0) }); track.Children.Add(new System.Windows.Shapes.Ellipse { Width=5,Height=5,Fill=cyan,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,3,0,0) }); entry.Children.Add(track); row.Margin=new Thickness(15,1,0,6); entry.Children.Add(row); agendaItems.Children.Add(entry);
   }
   agendaManage.Content=calendarUrl.Length>0?"MANAGE":"CONNECT"; agendaRefresh.IsEnabled=calendarUrl.Length>0 && !agendaBusy; agendaSave.IsEnabled=!agendaBusy; agendaRemove.IsEnabled=calendarUrl.Length>0;
   if(agendaBusy) agendaStatus.Text="SYNCING / Google Calendar…";
   else if(calendarUrl.Length==0) agendaStatus.Text="CONNECT / read-only calendar sync";
   else if(agendaSnapshot!=null) {
    DateTime stamp; string updated=DateTime.TryParse(agendaSnapshot.UpdatedUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out stamp)?stamp.ToLocalTime().ToString("h:mm tt",CultureInfo.InvariantCulture):"unknown time";
    agendaStatus.Text=(agendaOffline?"OFFLINE / saved ":"SYNCED / ")+updated+(agendaOffline?"":" · 5 min");
   }
  }
  void ToggleCalendarSetup() { OpenUnifiedSettings("calendar"); }
  void CancelCalendarSetup() { if(agendaReplacing && agendaBusy) { agendaGeneration++; agendaOperation.Cancel(); agendaBusy=false; agendaReplacing=false; nextAgendaRefresh=DateTime.UtcNow.AddMinutes(5); } CloseCalendarSetup(); if(InSetup("calendar"))setupWindow.Close(); }
  void CloseCalendarSetup() { agendaLink.Clear(); agendaSetup.Visibility=InSetup("calendar")?Visibility.Visible:Visibility.Collapsed; agendaScroll.Visibility=Visibility.Visible; PaintAgenda(); }
  void OpenCalendarHelp() { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName="https://support.google.com/calendar/answer/37648?hl=en",UseShellExecute=true }); }
  void SaveCalendarLink() {
   if(agendaBusy) return;
   try {
    string candidate=CalendarFeed.ValidateLink(agendaLink.Password).AbsoluteUri;
    // Fetch and parse successfully before replacing a working saved connection.
    StartCalendarFetch(candidate,true);
   } catch(CalendarFeedException ex) { agendaStatus.Text=ex.Message; } catch { agendaStatus.Text="Could not save the calendar link. Try again."; }
  }
  void RefreshCalendar() { if(!agendaLoaded || agendaBusy || calendarUrl.Length==0) return; StartCalendarFetch(calendarUrl,false); }
  void StartCalendarFetch(string candidate,bool replace) {
   if(agendaBusy) return;
   if(agendaOperation!=null) { agendaOperation.Cancel(); agendaOperation.Dispose(); }
   agendaOperation=new CancellationTokenSource(TimeSpan.FromSeconds(40)); var cancellation=agendaOperation.Token;
   int generation=++agendaGeneration; DateTime day=DateTime.Today; TimeZoneInfo zone=TimeZoneInfo.Local; agendaBusy=true; agendaReplacing=replace; PaintAgenda();
   Task.Run(delegate {
    AgendaSnapshot result=null; string error=null;
    try { result=CalendarFeed.Download(candidate,day,zone,cancellation); }
    catch(OperationCanceledException) { error="Calendar sync timed out. Try REFRESH."; }
    catch(CalendarFeedException ex) { error=ex.Message; }
    catch { error="Calendar sync failed. Try again."; }
    if(closing || Dispatcher.HasShutdownStarted) return;
    Dispatcher.BeginInvoke(new Action(delegate {
     if(closing || generation!=agendaGeneration) return;
     agendaBusy=false; agendaReplacing=false; nextAgendaRefresh=DateTime.UtcNow.AddMinutes(5);
     if(day!=DateTime.Today || zone.Id!=TimeZoneInfo.Local.Id) { CheckCalendar(); return; }
     if(error==null) {
      try {
       if(replace) ChatGptClient.WriteProtected(CalendarLinkPath,new CalendarSettings { FeedUrl=candidate });
       calendarUrl=candidate; agendaSnapshot=result; agendaOffline=false;
       ChatGptClient.WriteProtected(AgendaCachePath,result); if(replace) CloseCalendarSetup(); else PaintAgenda();
      } catch { PaintAgenda(); agendaStatus.Text="Calendar loaded; local save failed. Retry."; }
     } else { agendaOffline=true; PaintAgenda(); agendaStatus.Text=(agendaSnapshot==null?"":"OFFLINE / saved events. ")+error; }
    }));
   });
  }
  void RemoveCalendarLink() {
   agendaGeneration++; if(agendaOperation!=null) agendaOperation.Cancel(); agendaBusy=false;
   try { if(File.Exists(CalendarLinkPath)) File.Delete(CalendarLinkPath); if(File.Exists(AgendaCachePath)) File.Delete(AgendaCachePath); }
   catch { agendaStatus.Text="Could not remove saved calendar files."; return; }
   calendarUrl=""; agendaSnapshot=null; nextAgendaRefresh=DateTime.MaxValue; agendaOffline=false; CloseCalendarSetup();
  }
 }
}
