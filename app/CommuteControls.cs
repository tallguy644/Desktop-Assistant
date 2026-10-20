using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace JarvisDesk {
 public partial class Desk {
  TextBlock commuteSummary,commuteStatus;
  Border commuteDrawer;
  PasswordBox commuteOrigin,commuteDestination;
  TextBox commuteTitle,commuteBuffer,commuteLead;
  CheckBox commuteEnabled;
  CommutePreferences commutePreferences=new CommutePreferences();
  TravelEstimate commuteEstimate;
  DepartureNotices commuteNotices=new DepartureNotices();
  MapsBrowser mapsBrowser;
  string activeRouteKey="",mapsMessage="",pendingMapsKey="";
  AgendaItem commuteEvent;
  DateTime nextCommuteCheck,commuteReadUntil,nextCommutePaint;
  bool commuteLoaded;
  string CommutePath { get { return Path.Combine(dataDir,"commute-settings.protected"); } }
  string TravelPath { get { return Path.Combine(dataDir,"commute-travel.protected"); } }
  string NoticesPath { get { return Path.Combine(dataDir,"commute-notices.protected"); } }
  void CreateCommutePanel(Canvas stage) {
   var body=new StackPanel { Margin=new Thickness(22) };
   body.Children.Add(Label("WORK / DEPARTURE CONTROL",24,cyan));
   body.Children.Add(Label("Google Maps driving time + your calendar's next Work event. Arrive 15 minutes early, or choose your own buffer.",12,muted));
   commuteEnabled=new CheckBox { Content="Spoken departure reminders while " + AssistantIdentity.CurrentName + " is open",Foreground=cyan,Margin=new Thickness(0,10,0,8) };body.Children.Add(commuteEnabled);
   body.Children.Add(Label("STARTING ADDRESS / private, encrypted on this PC",10,cyan));commuteOrigin=CommuteAddressBox(body);
   body.Children.Add(Label("WORK ADDRESS / optional override; otherwise calendar event location",10,cyan));commuteDestination=CommuteAddressBox(body);
   commuteTitle=CommuteInput(body,"CALENDAR EVENT TITLE TO MATCH",80);
   var numbers=new StackPanel { Orientation=Orientation.Horizontal };var left=new StackPanel { Width=280 };var right=new StackPanel { Width=280 };commuteBuffer=CommuteInput(left,"ARRIVE EARLY / MINUTES",3);commuteLead=CommuteInput(right,"HEADS-UP BEFORE LEAVING / MINUTES",2);numbers.Children.Add(left);numbers.Children.Add(right);body.Children.Add(numbers);
   body.Children.Add(Label("Save & Check opens a private Maps browser. Confirm the correct route once. Future checks read current traffic every 5 minutes near your shift. Maps can change its page; unavailable readings pause reminders.",12,muted));
   commuteStatus=Label("",11,cyan);commuteStatus.MinHeight=48;body.Children.Add(commuteStatus);
   var actions=new WrapPanel();actions.Children.Add(CompactButton("SAVE & CHECK",SaveCommute));actions.Children.Add(CompactButton("OPEN MAPS",delegate { CheckMaps(true); }));actions.Children.Add(CompactButton("READ COMMUTE",ReadCommute));actions.Children.Add(CompactButton("ON MY WAY",OnMyWay));actions.Children.Add(CompactButton("CLOSE / ESC",CloseCommutePanel));body.Children.Add(actions);
   body.Children.Add(Label("Your starting point and destination go to Google Maps when checked. Spoken summaries use your saved voice. No API key is required. Estimates reflect traffic now, rather than a guaranteed future journey.",10,muted));
   commuteDrawer=PanelBox(new ScrollViewer { Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto });commuteDrawer.Visibility=Visibility.Collapsed;Place(stage,commuteDrawer,345,110,760,605);
  }
  PasswordBox CommuteAddressBox(StackPanel host) { var box=new PasswordBox { MaxLength=500,Height=30,Padding=new Thickness(6),Background=Ink(3,19,27),Foreground=Brushes.White,BorderBrush=Ink(0,103,115),Margin=new Thickness(0,0,0,9) };host.Children.Add(box);return box; }
  TextBox CommuteInput(StackPanel host,string title,int length) { host.Children.Add(Label(title,10,cyan));var box=new TextBox { MaxLength=length,Height=29,Padding=new Thickness(6),Background=Ink(3,19,27),Foreground=Brushes.White,BorderBrush=Ink(0,103,115),Margin=new Thickness(0,0,12,9) };host.Children.Add(box);return box; }
  void InitializeCommute() {
   try { if(File.Exists(CommutePath)) { commutePreferences=ChatGptClient.ReadProtected<CommutePreferences>(CommutePath);commutePreferences.Validate(); } } catch { commutePreferences=new CommutePreferences();Log("Work route settings need attention. Open WORK to configure them."); }
   // Always obtain a new Maps reading after restarting; a saved estimate is never an alarm source.
   try { if(File.Exists(NoticesPath)) commuteNotices=ChatGptClient.ReadProtected<DepartureNotices>(NoticesPath)??new DepartureNotices(); } catch { }
   commuteLoaded=true;
   Closing+=delegate { if(mapsBrowser!=null) mapsBrowser.DisposeBrowser(); };
  }
  void OpenCommutePanel() {
   if(!buildingSetupPage){OpenUnifiedSettings("commute");return;}
   CloseScreenPanel();
   commuteOrigin.Password=commutePreferences.Origin;commuteDestination.Password=commutePreferences.Destination;commuteTitle.Text=commutePreferences.WorkTitle;commuteBuffer.Text=commutePreferences.ArrivalBufferMinutes.ToString(CultureInfo.InvariantCulture);commuteLead.Text=commutePreferences.LeadMinutes.ToString(CultureInfo.InvariantCulture);commuteEnabled.IsChecked=commutePreferences.Enabled || !File.Exists(CommutePath);
   commuteStatus.Text=mapsMessage.Length>0?mapsMessage:"Enter your starting address here, then Save & Check. Your calendar must be connected.";appDrawer.Visibility=Visibility.Collapsed;sensorDrawer.Visibility=Visibility.Collapsed;commuteDrawer.Visibility=Visibility.Visible;
  }
  void CloseCommutePanel() { if(InSetup("commute") && !buildingSetupPage){setupWindow.Close();return;} commuteOrigin.Clear();commuteDestination.Clear();commuteDrawer.Visibility=Visibility.Collapsed; }
  void SaveCommute() {
   int buffer,lead;if(!Int32.TryParse(commuteBuffer.Text,NumberStyles.None,CultureInfo.InvariantCulture,out buffer) || !Int32.TryParse(commuteLead.Text,NumberStyles.None,CultureInfo.InvariantCulture,out lead)) { commuteStatus.Text="Enter whole minutes for arrival buffer and heads-up.";return; }
   try {
    var draft=new CommutePreferences { Enabled=commuteEnabled.IsChecked==true,Origin=commuteOrigin.Password,Destination=commuteDestination.Password,WorkTitle=commuteTitle.Text,ArrivalBufferMinutes=buffer,LeadMinutes=lead };draft.Validate();
    if(draft.Origin==commutePreferences.Origin && draft.Destination==commutePreferences.Destination) { draft.ConfirmedRouteKey=commutePreferences.ConfirmedRouteKey;draft.CanonicalOrigin=commutePreferences.CanonicalOrigin;draft.CanonicalDestination=commutePreferences.CanonicalDestination; }
    ChatGptClient.WriteProtected(CommutePath,draft);commutePreferences=draft;commuteEstimate=null;activeRouteKey="";nextCommuteCheck=DateTime.MaxValue;
    if(draft.Enabled) CheckMaps(true);else { mapsMessage="Departure reminders are off.";commuteStatus.Text=mapsMessage; }
   } catch(ArgumentException ex) { commuteStatus.Text=ex.Message; } catch { commuteStatus.Text="Could not save work settings. Try again."; }
  }
  bool FindWork() {
   commuteEvent=null;activeRouteKey="";
   if(!commutePreferences.Enabled || calendarUrl.Length==0 || agendaSnapshot==null || !agendaSnapshot.Matches(DateTime.Today,TimeZoneInfo.Local,CalendarFeed.SourceId(calendarUrl))) return false;
   commuteEvent=Commute.WorkEvent(agendaSnapshot,commutePreferences,DateTime.Now);if(commuteEvent==null) return false;
   string destination=String.IsNullOrWhiteSpace(commutePreferences.Destination)?commuteEvent.Location:commutePreferences.Destination;
   if(String.IsNullOrWhiteSpace(destination)) return false;
   activeRouteKey=Commute.RouteKey(commutePreferences.Origin,destination);return true;
  }
  async void CheckMaps(bool visible) {
   if(!FindWork()) { mapsMessage=!commutePreferences.Enabled?"Save your work settings first.":commuteEvent==null?"Connect or refresh your calendar with an upcoming Work event today or tomorrow.":"Add a location to your Work event, or set a work address here.";commuteStatus.Text=mapsMessage;return; }
   if(agendaOffline) { mapsMessage="Calendar is offline. Refresh it before checking a departure time.";commuteStatus.Text=mapsMessage;return; }
   string destination=String.IsNullOrWhiteSpace(commutePreferences.Destination)?commuteEvent.Location:commutePreferences.Destination;
   try {
    var url=Commute.MapsUrl(commutePreferences.Origin,destination);pendingMapsKey=activeRouteKey;
    if(mapsBrowser==null) { mapsBrowser=new MapsBrowser(this,Path.Combine(dataDir,"maps-browser"));mapsBrowser.RouteRead+=AcceptMapsReading;mapsBrowser.Problem+=delegate(string error) { mapsMessage=error;commuteStatus.Text=error;commuteEstimate=null; }; }
    mapsMessage="Checking Google Maps…";commuteStatus.Text=mapsMessage;nextCommuteCheck=DateTime.UtcNow.AddMinutes(5);
    await mapsBrowser.NavigateRoute(url,visible);
   } catch { mapsMessage="Could not open your route. Check the addresses and WebView2 Runtime.";commuteStatus.Text=mapsMessage; }
  }
  void AcceptMapsReading(MapsReading reading,bool confirm) {
   if(!FindWork() || pendingMapsKey!=activeRouteKey) { commuteEstimate=null;mapsMessage="Your work event changed. Check the route again.";commuteStatus.Text=mapsMessage;return; }
   try {
    int minutes=Commute.ParseDuration(reading.Duration);
    if(confirm) { commutePreferences.ConfirmedRouteKey=activeRouteKey;commutePreferences.CanonicalOrigin=reading.Origin;commutePreferences.CanonicalDestination=reading.Destination;ChatGptClient.WriteProtected(CommutePath,commutePreferences); }
    if(commutePreferences.ConfirmedRouteKey!=activeRouteKey || reading.Origin!=commutePreferences.CanonicalOrigin || reading.Destination!=commutePreferences.CanonicalDestination) { commuteEstimate=null;mapsMessage="Check the map's starting point and destination, then click USE THIS ROUTE.";commuteStatus.Text=mapsMessage;return; }
    commuteEstimate=new TravelEstimate { RouteKey=activeRouteKey,Origin=reading.Origin,Destination=reading.Destination,Minutes=minutes,UpdatedUtc=DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture) };ChatGptClient.WriteProtected(TravelPath,commuteEstimate);
    mapsMessage=CommuteDescription();commuteStatus.Text=mapsMessage;
   } catch { commuteEstimate=null;mapsMessage="The Maps reading could not be used or saved. Check the route again.";commuteStatus.Text=mapsMessage; }
  }
  bool CalendarFresh() { DateTime stamp;return !agendaOffline && agendaSnapshot!=null && DateTime.TryParse(agendaSnapshot.UpdatedUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out stamp) && stamp.Kind==DateTimeKind.Utc && DateTime.UtcNow>=stamp && DateTime.UtcNow-stamp<=TimeSpan.FromMinutes(15); }
  string CommuteDescription() {
   if(!FindWork()) return "No upcoming work route is ready. Open Work to check your settings and calendar.";
   if(!CalendarFresh() || !Commute.Fresh(commuteEstimate,activeRouteKey,DateTime.UtcNow)) return "A fresh calendar and confirmed Google Maps estimate are needed. Open Maps to check the route.";
   var leave=Commute.LeaveAt(commuteEvent,commuteEstimate.Minutes,commutePreferences.ArrivalBufferMinutes);
   return "Current driving time is "+commuteEstimate.Minutes+" minutes. "+(leave<DateTime.Now?"Your calculated departure time was ":"Leave at ")+leave.ToString("h:mm tt",CultureInfo.InvariantCulture)+(leave.Date>DateTime.Today?" tomorrow":"")+" to arrive "+commutePreferences.ArrivalBufferMinutes+" minutes before work. Traffic may change.";
  }
  void ReadCommute() { commuteReadUntil=DateTime.UtcNow.AddSeconds(85);if(!Commute.Fresh(commuteEstimate,activeRouteKey,DateTime.UtcNow)) CheckMaps(true); }
  void OnMyWay() { if(!FindWork()) { commuteStatus.Text="No upcoming work event to dismiss.";return; } string trip=Commute.TripKey(commuteEvent,activeRouteKey);try { var saved=new DepartureNotices { TripKey=trip,OnMyWay=true };ChatGptClient.WriteProtected(NoticesPath,saved);commuteNotices=saved;mapsMessage="On your way / reminders dismissed for this shift.";commuteStatus.Text=mapsMessage; } catch { commuteStatus.Text="Could not save dismissal. Try again."; } }
  void CheckCommuteReminders() {
   if(!commuteLoaded || closing || DateTime.UtcNow<nextCommutePaint) return;nextCommutePaint=DateTime.UtcNow.AddSeconds(1);
   bool work=FindWork();bool fresh=work && CalendarFresh() && Commute.Fresh(commuteEstimate,activeRouteKey,DateTime.UtcNow);
   if(!commutePreferences.Enabled) commuteSummary.Text="WORK / SET UP TRAVEL";
   else if(!work) commuteSummary.Text=commuteEvent==null?"WORK / NO UPCOMING SHIFT":"WORK / ADD DESTINATION";
   else if(!fresh) commuteSummary.Text="WORK / CHECK MAPS";
   else { var leave=Commute.LeaveAt(commuteEvent,commuteEstimate.Minutes,commutePreferences.ArrivalBufferMinutes);commuteSummary.Text="LEAVE / "+leave.ToString("h:mm tt",CultureInfo.InvariantCulture)+(leave.Date>DateTime.Today?" TOMORROW":"")+" · "+commuteEstimate.Minutes+" MIN"; }
   commuteSummary.ToolTip=mapsMessage;
   if(work && !agendaOffline && commutePreferences.ConfirmedRouteKey==activeRouteKey && commuteEvent.Start-DateTime.Now<=TimeSpan.FromHours(4) && DateTime.UtcNow>=nextCommuteCheck) CheckMaps(false);
   if(speaking || questionListening) return;
   if(commuteReadUntil!=DateTime.MinValue) { if(DateTime.UtcNow>commuteReadUntil) { commuteReadUntil=DateTime.MinValue;SpeakLocalSummary(CommuteDescription()); } else if(fresh || !work || mapsMessage.StartsWith("Check the map",StringComparison.Ordinal)) { commuteReadUntil=DateTime.MinValue;SpeakLocalSummary(CommuteDescription()); } return; }
   if(!fresh) return;
   string tripKey=Commute.TripKey(commuteEvent,activeRouteKey);if(commuteNotices.TripKey!=tripKey) commuteNotices=new DepartureNotices { TripKey=tripKey };
   if(commuteNotices.OnMyWay) return;
   DateTime departure=Commute.LeaveAt(commuteEvent,commuteEstimate.Minutes,commutePreferences.ArrivalBufferMinutes);double remaining=(departure-DateTime.Now).TotalMinutes;
   bool now=remaining<=0 && !commuteNotices.LeaveNow,heads=remaining>0 && remaining<=commutePreferences.LeadMinutes && !commuteNotices.HeadsUp;
   if(!now && !heads) return;
   bool oldHead=commuteNotices.HeadsUp,oldNow=commuteNotices.LeaveNow;commuteNotices.HeadsUp=true;if(now) commuteNotices.LeaveNow=true;
   try { ChatGptClient.WriteProtected(NoticesPath,commuteNotices); } catch { commuteNotices.HeadsUp=oldHead;commuteNotices.LeaveNow=oldNow;mapsMessage="Departure reminders cannot save their state. Check local storage.";return; }
   string message=now?"It's time to leave for work. ":"Work departure heads-up. ";SpeakLocalSummary(message+CommuteDescription(),true);
  }
 }
}
