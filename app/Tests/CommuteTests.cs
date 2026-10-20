using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using JarvisDesk;
public static class CommuteTests {
 static void Check(bool value,string name) { if(!value) throw new Exception(name);Console.WriteLine("PASS "+name); }
 static void Reject(Action action,string name) { bool rejected=false;try { action(); } catch(ArgumentException) { rejected=true; }Check(rejected,name); }
 public static int Main(string[] args) {
  try {
   var now=new DateTime(2026,10,19,23,30,0);var work=new AgendaItem { Title="Work",Location="Sample workplace",StartTicks=now.Date.AddDays(1).AddMinutes(30).Ticks,EndTicks=now.Date.AddDays(1).AddHours(8).Ticks };
   var prefs=new CommutePreferences { Enabled=true,Origin="Sample home" };prefs.Validate();
   Check(prefs.ArrivalBufferMinutes==15,"Default arrival is 15 minutes early");
   var agenda=new AgendaSnapshot { Day="2026-10-19",Items=new List<AgendaItem> { new AgendaItem { Title="Homework",StartTicks=now.AddMinutes(1).Ticks },new AgendaItem { Title="Work",AllDay=true,StartTicks=now.AddMinutes(2).Ticks } },NextDayItems=new List<AgendaItem> { work } };
   Check(Commute.WorkEvent(agenda,prefs,now)==work,"Find tomorrow's early shift before midnight, excluding homework and all-day entries");
   Check(Commute.WorkEvent(agenda,prefs,work.Start)==null,"Skip shifts that have already started");
   prefs.Enabled=false;Check(Commute.WorkEvent(agenda,prefs,now)==null,"Disabled reminders do not select a work event");prefs.Enabled=true;
   Check(Commute.LeaveAt(work,28,15)==now.Date.AddHours(23).AddMinutes(47),"Departure subtracts driving time and 15-minute arrival buffer across midnight");
   Check(Commute.ParseDuration("28 min")==28 && Commute.ParseDuration("1 hr 12 min")==72 && Commute.ParseDuration("2 hours")==120,"Parse exact Maps minutes and hours");
   foreach(var text in new[] { "", "28–40 min", "usually 28 min", "0 min", "13 hr", "28 min via sample road", "unknown", "-1 min" }) Reject(delegate { Commute.ParseDuration(text); },"Reject missing, ambiguous, or unsupported Maps duration");
   var url=Commute.MapsUrl("Sample home & x=1","Sample workplace #1");Check(url.Host=="www.google.com" && url.Query.Contains("travelmode=driving") && url.Query.Contains("%26") && url.Fragment=="","Maps URL encodes private inputs as driving route parameters");
   Reject(delegate { Commute.MapsUrl("","work"); },"Missing origin cannot become a route");
   Reject(delegate { CommutePreferences.Address("home\nwork"); },"Reject multiline address input");
   string key=Commute.RouteKey("home","work");var travel=new TravelEstimate { RouteKey=key,Minutes=28,UpdatedUtc=DateTime.UtcNow.AddMinutes(-9).ToString("o") };
   Check(Commute.Fresh(travel,key,DateTime.UtcNow),"Accept a recent estimate for the exact route");Check(!Commute.Fresh(travel,Commute.RouteKey("other","work"),DateTime.UtcNow),"Reject a cached estimate for another origin");
   travel.UpdatedUtc=DateTime.UtcNow.AddMinutes(-11).ToString("o");Check(!Commute.Fresh(travel,key,DateTime.UtcNow),"Stale Maps estimates cannot trigger departure alarms");travel.UpdatedUtc=DateTime.UtcNow.AddMinutes(1).ToString("o");Check(!Commute.Fresh(travel,key,DateTime.UtcNow),"Reject future-dated travel readings");
   string trip=Commute.TripKey(work,key);work.StartTicks+=TimeSpan.TicksPerMinute;Check(trip!=Commute.TripKey(work,key),"Changed work start creates a distinct reminder occurrence");
   var saved=new DepartureNotices { TripKey=trip,HeadsUp=true,LeaveNow=true,OnMyWay=true };string path=Path.Combine(args[0],"commute-test.protected");ChatGptClient.WriteProtected(path,saved);var restored=ChatGptClient.ReadProtected<DepartureNotices>(path);Check(restored.TripKey==trip && restored.LeaveNow && restored.OnMyWay && !System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains(trip),"Reminder dismissals survive restart in encrypted storage");
   agenda.Items=new List<AgendaItem> { new AgendaItem { Title="A & B <meeting>",Location="PRIVATE ADDRESS",StartTicks=now.Date.AddHours(9).Ticks,EndTicks=now.Date.AddHours(10).Ticks } };
   string speech=AgendaSpeech.Build(agenda,now.Date,true);Check(speech.Contains("saved calendar") && speech.Contains("9:00 AM") && !speech.Contains("PRIVATE ADDRESS"),"Agenda narration identifies offline cache and excludes locations");
   Check(AgendaSpeech.Build(agenda,now.Date.AddDays(1),false).Contains("unavailable"),"Yesterday's agenda cannot be narrated as today");
   agenda.Items=Enumerable.Range(0,200).Select(i=>new AgendaItem { Title=new string('x',300),AllDay=true }).ToList();Check(AgendaSpeech.Build(agenda,now.Date,false).Length<=1200 && AgendaSpeech.Build(agenda,now.Date,false).Contains("more events"),"Long agendas stay within speech limits and announce remaining entries");
   Check(LocalIntent.Parse("Assistant, read my agenda",true,false)=="agenda" && LocalIntent.Parse("read my agenda",true,false)==null && LocalIntent.Parse("read my agenda",true,true)=="agenda","Agenda commands enforce wake phrase with push-to-talk fallback");
   Check(LocalIntent.Parse("Assistant when should I leave for work",true,false)=="commute" && LocalIntent.Parse("Assistant open my work route",true,false)=="maps" && LocalIntent.Parse("Assistant delete my calendar",false,false)==null,"Only explicit safe calendar and Maps intents are recognized");
   string ics="BEGIN:VCALENDAR\r\nVERSION:2.0\r\nBEGIN:VEVENT\r\nUID:early\r\nDTSTAMP:20261001T000000Z\r\nDTSTART:20261020T003000\r\nDTEND:20261020T080000\r\nSUMMARY:Work\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";
   var parsed=CalendarFeed.Parse(ics,now.Date,TimeZoneInfo.Utc,"test",CancellationToken.None);Check(parsed.Items.Count==0 && parsed.NextDayItems.Count==1,"Calendar caches next-day shifts without displaying them in today's agenda");
   return 0;
  } catch(Exception ex) { Console.Error.WriteLine(ex);return 1; }
 }
}
