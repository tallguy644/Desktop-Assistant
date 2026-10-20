using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using JarvisDesk;
public static class CalendarTests {
 static readonly DateTime Day=new DateTime(2026,10,19);
 static readonly TimeZoneInfo Zone=TimeZoneInfo.FindSystemTimeZoneById("US Mountain Standard Time");
 static string Wrap(string events) { return "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Skynet tests//EN\r\n"+events+"END:VCALENDAR\r\n"; }
 static string Event(string id,string start,string end,string extra,string title) { return "BEGIN:VEVENT\r\nUID:"+id+"\r\nDTSTAMP:20261001T000000Z\r\nDTSTART"+start+"\r\n"+(end==null?"":"DTEND"+end+"\r\n")+extra+"SUMMARY:"+title+"\r\nEND:VEVENT\r\n"; }
 static AgendaSnapshot Read(string events) { return CalendarFeed.Parse(Wrap(events),Day,Zone,"fixture",CancellationToken.None); }
 static void Check(bool value,string name) { if(!value) throw new Exception(name); Console.WriteLine("PASS "+name); }
 static void Reject(Action action,string name) { try { action(); } catch(CalendarFeedException) { Check(true,name); return; } throw new Exception("Accepted "+name); }
 public static int Main(string[] args) {
  try {
   var work=Read(Event("night",":20261020T003000Z",":20261020T090000Z","","Night shift"));
   Check(work.Items.Count==1 && work.Items[0].Start.Hour==17 && work.Items[0].Start.Minute==30 && work.Items[0].End.Day==20 && work.Items[0].TimeLabel(Day).Contains("(+1d)"),"UTC conversion and overnight event labels");
   var prior=Read(Event("prior",":20261019T050000Z",":20261019T090000Z","","Prior overnight"));
   Check(prior.Items.Count==1 && prior.Items[0].Start.Day==18,"Include an overnight event already in progress at midnight");
   var boundary=Read(Event("ended",":20261019T060000Z",":20261019T070000Z","","Ended")+Event("tomorrow",":20261020T070000Z",":20261020T080000Z","","Tomorrow"));
   Check(boundary.Items.Count==0,"Day end and event end are exclusive boundaries");
   var allDay=Read(Event("a",";VALUE=DATE:20261019",";VALUE=DATE:20261020","","All day")+Event("b",";VALUE=DATE:20261018",";VALUE=DATE:20261019","","Yesterday"));
   Check(allDay.Items.Count==1 && allDay.Items[0].AllDay && allDay.Items[0].TimeLabel(Day)=="ALL DAY","All-day dates retain exclusive end dates");
   Check(Read(Event("long",";VALUE=DATE:20260901",";VALUE=DATE:20261101","","Long event")).Items.Count==1,"Include long events spanning today");
   var weekly=Event("weekly",";TZID=America/Phoenix:20261005T173000",";TZID=America/Phoenix:20261006T020000","RRULE:FREQ=WEEKLY;BYDAY=MO\r\n","Weekly shift");
   Check(Read(weekly).Items.Single().Start.Hour==17,"Expand a weekly recurring shift with an IANA time zone");
   var master=Event("repeat",";TZID=America/Phoenix:20261012T090000",";TZID=America/Phoenix:20261012T100000","RRULE:FREQ=WEEKLY;BYDAY=MO;COUNT=4\r\n","Original");
   var moved=Event("repeat",";TZID=America/Phoenix:20261019T110000",";TZID=America/Phoenix:20261019T120000","RECURRENCE-ID;TZID=America/Phoenix:20261019T090000\r\n","Moved");
   var changed=Read(master+moved);
   Check(changed.Items.Count==1 && changed.Items[0].Title=="Moved" && changed.Items[0].Start.Hour==11,"Moved recurring occurrence replaces the original");
   Check(Read(master+Event("repeat",";TZID=America/Phoenix:20261019T090000",";TZID=America/Phoenix:20261019T100000","RECURRENCE-ID;TZID=America/Phoenix:20261019T090000\r\nSTATUS:CANCELLED\r\n","Cancelled")).Items.Count==0,"Cancelled recurring instance suppresses the original");
   Check(Read(master.Replace("COUNT=4\r\n","COUNT=4\r\nEXDATE;TZID=America/Phoenix:20261019T090000\r\n")).Items.Count==0,"EXDATE excludes a repeating occurrence");
   Check(Read(Event("cancelled",":20261019T160000Z",":20261019T170000Z","STATUS:CANCELLED\r\n","Cancelled")).Items.Count==0,"Cancelled standalone events are hidden");
   var floatTime=Read(Event("floating",":20261019T223000",":20261019T233000","","Floating"));
   Check(floatTime.Items.Single().Start.Hour==22,"Floating event times use the PC's local wall clock");
   var duration=Read(Event("duration",":20261019T160000Z",null,"DURATION:PT2H\r\n","Duration"));
   Check((duration.Items.Single().End-duration.Items.Single().Start).TotalHours==2,"DURATION events get their effective end time");
   Check(Read(Event("point",":20261019T070000Z",null,"","Point")).Items.Count==1,"Zero-duration events at day start remain visible");
   var dst=CalendarFeed.Parse(Wrap(Event("dst",";TZID=America/New_York:20261101T090000",";TZID=America/New_York:20261101T100000","","DST")),new DateTime(2026,11,1),TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"),"fixture",CancellationToken.None);
   Check(dst.Items.Single().Start.Hour==6,"Different time zones convert correctly on a daylight-saving transition day");
   var escaped=Read(Event("text",":20261019T160000Z",":20261019T170000Z","","Planning\\, budget \r\n and coffee"));
   Check(escaped.Items.Single().Title=="Planning, budget and coffee","Unfold and unescape iCalendar event titles");
   var sorted=Read(Event("timed",":20261019T160000Z",":20261019T170000Z","","Timed")+Event("allday",";VALUE=DATE:20261019",";VALUE=DATE:20261020","","All day"));
   Check(sorted.Items[0].AllDay,"All-day entries appear before timed entries");
   Check(Read("").Items.Count==0,"A valid empty calendar shows no events");
   Reject(delegate { CalendarFeed.Parse("<html>Sign in</html>",Day,Zone,"fixture",CancellationToken.None); },"Reject an HTML sign-in page instead of pretending the day is empty");
   var url="https://calendar.google.com/calendar/ical/test%40example.com/private-0123456789abcdef0123456789abcdef/basic.ics";
   Check(CalendarFeed.ValidateLink(url).Host=="calendar.google.com","Accept the private Google iCal URL format");
   foreach(var bad in new string[] {url.Replace("https:","http:"),url.Replace("calendar.google.com/","calendar.google.com.evil.example/"),url+"?x=1",url+"#x",url.Replace("private-0123456789abcdef0123456789abcdef","public"),"file:///C:/secret.ics"}) Reject(delegate { CalendarFeed.ValidateLink(bad); },"Reject unsupported calendar link");
   var request=CalendarFeed.CreateRequest(url);
   Check(request.Method=="GET" && !request.AllowAutoRedirect && request.Headers["Authorization"]==null,"Read-only GET request prevents redirects and sends no unrelated credentials");
   Check(work.Matches(Day,Zone,"fixture") && !work.Matches(Day.AddDays(1),Zone,"fixture") && !work.Matches(Day,TimeZoneInfo.Utc,"fixture") && !work.Matches(Day,Zone,"other"),"Saved agenda is bound to its date, time zone, and calendar source");
   string storage=Path.Combine(args[0],"agenda-test.protected"); ChatGptClient.WriteProtected(storage,work);
   Check(!Encoding.UTF8.GetString(File.ReadAllBytes(storage)).Contains("Night shift") && ChatGptClient.ReadProtected<AgendaSnapshot>(storage).Matches(Day,Zone,"fixture"),"Agenda cache encrypts and round-trips using Windows user protection");
   var cancelled=new CancellationToken(true); bool stopped=false;
   try { CalendarFeed.Parse(Wrap(weekly),Day,Zone,"fixture",cancelled); } catch(OperationCanceledException) { stopped=true; }
   Check(stopped,"Cancelled evaluation stops without returning a fake empty agenda");
   return 0;
  } catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
 }
}
