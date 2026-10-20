using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace JarvisDesk {
 public sealed class NextUpState {
  public string Kind="unavailable",Caption="NEXT UP",Value="—",Title="CONNECT CALENDAR",Detail="TODAY / AGENDA",Tooltip="Connect Google Calendar in the agenda to see your next event.";
  public double Fraction;
  public bool Urgent;
 }
 public static class NextUp {
  public static NextUpState Build(AgendaSnapshot agenda,DateTime now,bool offline,DateTime? departure=null,DateTime? workStart=null) {
   var state=new NextUpState();
   if(agenda==null || agenda.Day!=now.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture) || agenda.Items==null) return state;
   var events=agenda.Items.Concat(agenda.NextDayItems??new List<AgendaItem>()).ToList();
   var upcoming=events.Where(i=>!i.AllDay && i.Start>now).OrderBy(i=>i.StartTicks).FirstOrDefault();
   // A verified departure takes priority when it is the next action to take.
   if(!offline && departure.HasValue && workStart.HasValue && workStart.Value>now && departure.Value<=now.AddHours(4) && (upcoming==null || departure.Value<=upcoming.Start)) {
    TimeSpan remaining=departure.Value-now;bool due=remaining<=TimeSpan.Zero;
    state.Kind="leave";state.Caption="DEPARTURE";state.Value=due?"LEAVE NOW":Countdown(remaining);state.Title="LEAVE FOR WORK";state.Detail=Clock(departure.Value,now);state.Fraction=Progress(remaining);state.Urgent=remaining.TotalMinutes<=15;
    state.Tooltip="Leave for work at "+departure.Value.ToString("ddd h:mm tt",CultureInfo.InvariantCulture)+". Includes your arrival buffer and current verified Maps driving time. Traffic may change.";return state;
   }
   if(upcoming!=null) {
    state.Kind="event";state.Caption=offline?"SAVED / NEXT":"NEXT UP";state.Value=Countdown(upcoming.Start-now);state.Title=upcoming.Title;state.Detail=Clock(upcoming.Start,now);state.Fraction=Progress(upcoming.Start-now);
    state.Tooltip=(offline?"Offline / saved calendar\n":"")+upcoming.Title+"\n"+upcoming.Start.ToString("ddd h:mm tt",CultureInfo.InvariantCulture)+"\nRing fills during the final 60 minutes.";return state;
   }
   var ongoing=events.Where(i=>!i.AllDay && i.Start<=now && i.End>now).OrderBy(i=>i.EndTicks).FirstOrDefault();
   if(ongoing!=null) { state.Kind="ongoing";state.Caption=offline?"SAVED / NOW":"HAPPENING NOW";state.Value="IN PROGRESS";state.Title=ongoing.Title;state.Detail="UNTIL "+ongoing.End.ToString("h:mm tt",CultureInfo.InvariantCulture);state.Fraction=1;state.Tooltip=(offline?"Offline / saved calendar\n":"")+ongoing.Title+"\n"+ongoing.TimeLabel(now.Date);return state; }
   var allDay=events.FirstOrDefault(i=>i.AllDay && i.Start.Date<=now.Date && i.End.Date>now.Date);
   if(allDay!=null) { state.Kind="all-day";state.Caption=offline?"SAVED / TODAY":"TODAY";state.Value="ALL DAY";state.Title=allDay.Title;state.Detail="NO TIMED EVENTS";state.Tooltip=(offline?"Offline / saved calendar\n":"")+allDay.Title+"\nAll-day event. See the agenda for all entries.";return state; }
   state.Kind="empty";state.Caption=offline?"SAVED / NEXT":"NEXT UP";state.Value="—";state.Title="ALL CLEAR";state.Detail="NO UPCOMING EVENTS";state.Tooltip=(offline?"Offline / saved calendar. ":"")+"No upcoming timed events in today's and tomorrow's synced agenda.";return state;
  }
  static double Progress(TimeSpan remaining) { return Math.Max(0.015,Math.Min(1,1-remaining.TotalMinutes/60)); }
  static string Clock(DateTime value,DateTime now) { return (value.Date>now.Date?"TMRW ":value.Date<now.Date?"YEST ":"")+value.ToString("h:mm tt",CultureInfo.InvariantCulture); }
  public static string Countdown(TimeSpan remaining) {
   if(remaining<=TimeSpan.Zero) return "NOW";
   if(remaining.TotalSeconds<60) return Math.Ceiling(remaining.TotalSeconds).ToString("0",CultureInfo.InvariantCulture)+" SEC";
   int minutes=(int)Math.Ceiling(remaining.TotalMinutes);
   return minutes<60?minutes+" MIN":minutes/60+"h "+(minutes%60).ToString("00",CultureInfo.InvariantCulture)+"m";
  }
 }
 public partial class Desk {
  Grid nextUpDial;
  TextBlock nextUpCaption,nextUpValue,nextUpTitle,nextUpDetail;
  System.Windows.Shapes.Path nextUpArc;
  DateTime nextUpPaint;
  void CreateNextUpDial(Canvas stage) {
   nextUpDial=new Grid { Width=156,Height=156,ToolTip="Your next calendar event, with a live countdown." };
   nextUpDial.Children.Add(Ring(153,Ink(0,47,57),1,null,0));
   var ticks=new Canvas { Width=156,Height=156,IsHitTestVisible=false };
   for(int i=0;i<36;i++) { var a=Polar(78,78,73,i*10);var b=Polar(78,78,i%3==0?69:71,i*10);Wire(ticks,a.X,a.Y,b.X,b.Y,Ink(0,100,112),i%3==0?1.2:0.7); }nextUpDial.Children.Add(ticks);
   nextUpDial.Children.Add(Ring(134,Ink(0,53,63),5,null,0));
   nextUpArc=Arc(144,67,-90,-89.9,cyan,5);nextUpArc.HorizontalAlignment=HorizontalAlignment.Center;nextUpArc.VerticalAlignment=VerticalAlignment.Center;nextUpArc.Effect=new DropShadowEffect { Color=Colors.Cyan,BlurRadius=9,ShadowDepth=0,Opacity=0.6 };nextUpDial.Children.Add(nextUpArc);
   var text=new Canvas { Width=156,Height=156,IsHitTestVisible=false };
   nextUpCaption=NextUpLabel("NEXT UP",9);Place(text,nextUpCaption,22,32,112,14);
   nextUpValue=NextUpLabel("—",25);nextUpValue.FontFamily=new FontFamily("Bahnschrift Condensed");Place(text,nextUpValue,15,48,126,33);
   nextUpTitle=NextUpLabel("CONNECT CALENDAR",10);nextUpTitle.TextWrapping=TextWrapping.Wrap;Place(text,nextUpTitle,23,82,110,29);
   nextUpDetail=NextUpLabel("TODAY / AGENDA",8);nextUpDetail.Foreground=muted;Place(text,nextUpDetail,12,116,132,15);
   nextUpDial.Children.Add(text);Place(stage,nextUpDial,790,35,156,156);
  }
  TextBlock NextUpLabel(string text,double size) { var label=Label(text,size,cyan);label.TextAlignment=TextAlignment.Center;label.Margin=new Thickness(0);label.TextTrimming=TextTrimming.CharacterEllipsis;label.TextWrapping=TextWrapping.NoWrap;return label; }
  void UpdateNextUp() {
   if(nextUpDial==null || DateTime.UtcNow<nextUpPaint) return;nextUpPaint=DateTime.UtcNow.AddSeconds(1);
   bool valid=calendarUrl.Length>0 && agendaSnapshot!=null && agendaSnapshot.Matches(DateTime.Today,TimeZoneInfo.Local,CalendarFeed.SourceId(calendarUrl));
   DateTime? departure=null,start=null;
   if(valid && FindWork() && CalendarFresh() && commutePreferences.ConfirmedRouteKey==activeRouteKey && Commute.Fresh(commuteEstimate,activeRouteKey,DateTime.UtcNow)) {
    bool dismissed=commuteNotices!=null && commuteNotices.OnMyWay && commuteNotices.TripKey==Commute.TripKey(commuteEvent,activeRouteKey);
    if(!dismissed) { departure=Commute.LeaveAt(commuteEvent,commuteEstimate.Minutes,commutePreferences.ArrivalBufferMinutes);start=commuteEvent.Start; }
   }
   var state=NextUp.Build(valid?agendaSnapshot:null,DateTime.Now,agendaOffline,departure,start);
   if(!valid && calendarUrl.Length>0) { state.Title=agendaBusy?"SYNCING CALENDAR":"CALENDAR UNAVAILABLE";state.Detail=agendaBusy?"PLEASE WAIT":"REFRESH AGENDA";state.Tooltip="A current-day calendar is needed. Refresh your agenda to try again."; }
   PaintNextUp(state);
  }
  void PaintNextUp(NextUpState state) {
   Brush color=state.Urgent?Brushes.Orange:cyan;
   nextUpCaption.Text=state.Caption;nextUpValue.Text=state.Value;nextUpValue.FontSize=state.Value.Length>10?15:state.Value.Length>7?20:25;nextUpTitle.Text=state.Title;nextUpDetail.Text=state.Detail;nextUpDial.ToolTip=state.Tooltip;
   nextUpCaption.Foreground=color;nextUpValue.Foreground=color;nextUpArc.Stroke=color;((DropShadowEffect)nextUpArc.Effect).Color=state.Urgent?Colors.Orange:Colors.Cyan;
   nextUpArc.Visibility=state.Fraction>0?Visibility.Visible:Visibility.Hidden;
   if(state.Fraction>0) nextUpArc.Data=Arc(144,67,-90,-90+Math.Min(0.9999,state.Fraction)*360,color,5).Data;
  }
 }
}
