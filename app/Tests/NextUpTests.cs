using System;
using System.Collections.Generic;
using JarvisDesk;
public static class NextUpTests {
 static readonly DateTime Now=new DateTime(2026,10,19,8,0,0);
 static AgendaItem Event(string title,DateTime start,DateTime end) { return new AgendaItem { Title=title,StartTicks=start.Ticks,EndTicks=end.Ticks }; }
 static AgendaSnapshot Agenda(params AgendaItem[] items) { return new AgendaSnapshot { Day="2026-10-19",Items=new List<AgendaItem>(items),NextDayItems=new List<AgendaItem>() }; }
 static void Check(bool value,string name) { if(!value) throw new Exception(name);Console.WriteLine("PASS "+name); }
 public static int Main() {
  try {
   var agenda=Agenda(Event("Later",Now.AddHours(3),Now.AddHours(4)),Event("Soon",Now.AddMinutes(30),Now.AddHours(1)),Event("Finished",Now.AddHours(-2),Now.AddHours(-1)));
   var state=NextUp.Build(agenda,Now,false);Check(state.Kind=="event" && state.Title=="Soon" && state.Value=="30 MIN","Select the next timed event by start time, skipping completed entries");
   Check(Math.Abs(state.Fraction-0.5)<0.001,"Countdown ring is halfway filled at 30 minutes");
   var later=NextUp.Build(agenda,Now.AddMinutes(10),false);Check(later.Value=="20 MIN" && later.Fraction>state.Fraction,"Countdown and ring advance with wall-clock time");
   Check(NextUp.Countdown(TimeSpan.FromSeconds(45))=="45 SEC" && NextUp.Countdown(TimeSpan.FromHours(2)+TimeSpan.FromMinutes(15))=="2h 15m","Countdown supports seconds, minutes, and hours");
   Check(NextUp.Countdown(TimeSpan.Zero)=="NOW","Countdown never shows negative time");
   var overnight=Agenda();overnight.NextDayItems.Add(Event("Early shift",Now.Date.AddDays(1).AddHours(1),Now.Date.AddDays(1).AddHours(8)));Check(NextUp.Build(overnight,Now,false).Detail.StartsWith("TMRW "),"Next-day event displays an explicit tomorrow label");
   var current=Agenda(Event("Current",Now.AddMinutes(-20),Now.AddMinutes(20)));Check(NextUp.Build(current,Now,false).Kind=="ongoing","Show an event in progress when no future timed event exists");
   current.Items.Add(Event("Next",Now.AddMinutes(5),Now.AddMinutes(30)));Check(NextUp.Build(current,Now,false).Title=="Next","An upcoming event takes priority over an ongoing event");
   var day=Event("Day-long",Now.Date,Now.Date.AddDays(1));day.AllDay=true;Check(NextUp.Build(Agenda(day),Now,false).Kind=="all-day","All-day entries do not receive a false timed countdown");
   Check(NextUp.Build(Agenda(),Now,false).Kind=="empty","A successfully synced empty calendar shows all clear");
   Check(NextUp.Build(null,Now,false).Kind=="unavailable","Missing calendar is unavailable rather than an invented clear day");
   var old=Agenda();old.Day="2026-10-18";Check(NextUp.Build(old,Now,false).Kind=="unavailable","Yesterday's cache cannot appear as today's next event");
   Check(NextUp.Build(agenda,Now,true).Caption=="SAVED / NEXT" && NextUp.Build(agenda,Now,true).Tooltip.Contains("Offline"),"Offline event countdown is clearly labelled as a saved calendar");
   var depart=NextUp.Build(agenda,Now,false,Now.AddMinutes(12),Now.AddMinutes(55));Check(depart.Kind=="leave" && depart.Value=="12 MIN" && depart.Urgent,"A verified departure before the next event takes priority and turns urgent");
   Check(NextUp.Build(agenda,Now,false,Now.AddHours(1),Now.AddHours(2)).Title=="Soon","An earlier calendar event stays ahead of a later departure");
   var due=NextUp.Build(agenda,Now,false,Now.AddMinutes(-2),Now.AddMinutes(30));Check(due.Value=="LEAVE NOW" && due.Fraction==1,"Due departure uses leave now instead of a negative countdown");
   Check(NextUp.Build(agenda,Now,true,Now.AddMinutes(12),Now.AddMinutes(55)).Kind=="event","Offline calendar cannot present a live departure countdown");
   Check(NextUp.Build(agenda,Now,false,Now.AddMinutes(-15),Now.AddMinutes(-1)).Kind=="event","A shift that has started cannot keep showing leave now");
   Check(NextUp.Build(Agenda(),Now,false,Now.AddHours(6),Now.AddHours(7)).Kind=="empty","Distant departure waits until the four-hour work window");
   return 0;
  } catch(Exception ex) { Console.Error.WriteLine(ex);return 1; }
 }
}
