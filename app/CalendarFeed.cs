using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Evaluation;

namespace JarvisDesk {
 public sealed class CalendarSettings { public string FeedUrl; }
 public sealed class AgendaItem {
  public string Title, Location;
  public long StartTicks, EndTicks;
  public bool AllDay;
  public DateTime Start { get { return new DateTime(StartTicks,DateTimeKind.Unspecified); } }
  public DateTime End { get { return new DateTime(EndTicks,DateTimeKind.Unspecified); } }
  public string TimeLabel(DateTime day) {
   if(AllDay) return "ALL DAY";
   return (Start.Date<day.Date?"YESTERDAY ":"")+Start.ToString("h:mm tt",CultureInfo.InvariantCulture)+" – "+End.ToString("h:mm tt",CultureInfo.InvariantCulture)+(End.Date>day.Date?" (+"+(End.Date-day.Date).Days+"d)":"");
  }
 }
 public sealed class AgendaSnapshot {
  public string Day, TimeZoneId, Source, UpdatedUtc;
  public List<AgendaItem> Items;
  public List<AgendaItem> NextDayItems;
  static bool Valid(List<AgendaItem> items) { return items!=null && items.Count<=200 && items.All(i=>i!=null && i.StartTicks>=DateTime.MinValue.Ticks && i.EndTicks<=DateTime.MaxValue.Ticks && i.EndTicks>=i.StartTicks && i.Title!=null && i.Title.Length<=300 && (i.Location??"").Length<=300); }
  public bool Matches(DateTime day,TimeZoneInfo zone,string source) { return Day==day.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture) && TimeZoneId==zone.Id && Source==source && Valid(Items) && (NextDayItems==null || Valid(NextDayItems)); }
 }
 public sealed class CalendarFeedException : Exception { public CalendarFeedException(string message):base(message){} }
 public static class CalendarFeed {
  const int MaxBytes=20*1024*1024;
  public static Uri ValidateLink(string text) {
   Uri uri;
   if(!Uri.TryCreate((text??"").Trim(),UriKind.Absolute,out uri) || uri.Scheme!="https" || !uri.Host.Equals("calendar.google.com",StringComparison.OrdinalIgnoreCase) || uri.Port!=443 || uri.UserInfo.Length!=0 || uri.Query.Length!=0 || uri.Fragment.Length!=0 || !Regex.IsMatch(uri.AbsolutePath,@"^/calendar/ical/[^/]+/private-[A-Za-z0-9_-]{16,128}/basic\.ics$"))
    throw new CalendarFeedException("Use Google's Secret address in iCal format. Paste it here privately.");
   return uri;
  }
  public static string SourceId(string url) { using(var hash=SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(url))); }
  public static HttpWebRequest CreateRequest(string url) {
   var request=(HttpWebRequest)WebRequest.Create(ValidateLink(url));
   request.Method="GET"; request.AllowAutoRedirect=false; request.Proxy=null; request.Timeout=20000; request.ReadWriteTimeout=10000;
   request.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate; request.CachePolicy=new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore); request.UserAgent="Skynet-Calendar/1.0";
   return request;
  }
  public static AgendaSnapshot Download(string url,DateTime day,TimeZoneInfo zone,CancellationToken cancellation) {
   ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
   var request=CreateRequest(url);
   try {
    using(cancellation.Register(delegate { request.Abort(); }))
    using(var response=(HttpWebResponse)request.GetResponse()) {
     if(response.StatusCode!=HttpStatusCode.OK) throw new CalendarFeedException("Google did not return a calendar. Check the private link.");
     if(response.ContentLength>MaxBytes) throw new CalendarFeedException("This calendar feed is too large (20 MB limit).");
     using(var stream=response.GetResponseStream()) using(var memory=new MemoryStream()) {
      byte[] buffer=new byte[8192]; int count;
      while((count=stream.Read(buffer,0,buffer.Length))>0) { cancellation.ThrowIfCancellationRequested(); if(memory.Length+count>MaxBytes) throw new CalendarFeedException("This calendar feed is too large (20 MB limit)."); memory.Write(buffer,0,count); }
      return Parse(Encoding.UTF8.GetString(memory.ToArray()),day,zone,SourceId(url),cancellation);
     }
    }
   } catch(WebException ex) {
    cancellation.ThrowIfCancellationRequested(); if(ex.Response!=null) ex.Response.Dispose();
    throw new CalendarFeedException("Calendar sync failed. Check your connection and private link.");
   }
  }
  public static AgendaSnapshot Parse(string text,DateTime day,TimeZoneInfo zone,string source,CancellationToken cancellation) {
   try {
    cancellation.ThrowIfCancellationRequested();
    if(String.IsNullOrWhiteSpace(text) || text.Length>MaxBytes || !text.TrimStart('\uFEFF',' ','\r','\n','\t').StartsWith("BEGIN:VCALENDAR",StringComparison.OrdinalIgnoreCase)) throw new CalendarFeedException("The link did not return an iCal calendar.");
    var calendar=Ical.Net.Calendar.Load(text.TrimStart('\uFEFF')); if(calendar==null) throw new CalendarFeedException("The calendar feed is empty or invalid.");
    if(calendar.Events.Count>20000) throw new CalendarFeedException("This calendar contains too many entries to display.");
    // Query a padded range that includes long events already in progress. The final
    // filter uses local calendar days, including floating and date-only entries.
    double longest=0;
    foreach(var entry in calendar.Events) {
     cancellation.ThrowIfCancellationRequested();
     if(entry.DtStart!=null) longest=Math.Max(longest,(entry.DtStart.Add(entry.EffectiveDuration).Value-entry.DtStart.Value).TotalDays);
    }
    if(longest>36600) throw new CalendarFeedException("An event duration is outside the supported calendar range.");
    day=DateTime.SpecifyKind(day.Date,DateTimeKind.Unspecified); var end=day.AddDays(1);
    DateTime beginUtc=TimeZoneInfo.ConvertTimeToUtc(day,zone).AddDays(-Math.Ceiling(longest)-2);
    DateTime endUtc=TimeZoneInfo.ConvertTimeToUtc(end,zone).AddDays(2);
    var result=new List<AgendaItem>(); var tomorrow=new List<AgendaItem>(); var started=DateTime.UtcNow; int scanned=0;
    var occurrences=calendar.GetOccurrences<CalendarEvent>(new CalDateTime(beginUtc,"UTC"),new EvaluationOptions { MaxUnmatchedIncrementsLimit=1000 });
    foreach(var occurrence in occurrences) {
     cancellation.ThrowIfCancellationRequested(); if(++scanned>50000 || (DateTime.UtcNow-started).TotalSeconds>15) throw new CalendarFeedException("This calendar's repeating rules take too long to evaluate.");
     var startTime=occurrence.Period.StartTime;
     if((startTime.IsFloating?startTime.Value:startTime.AsUtc)>=endUtc) break;
     var entry=occurrence.Source as CalendarEvent;
     if(entry==null || String.Equals(entry.Status,"CANCELLED",StringComparison.OrdinalIgnoreCase)) continue;
     var endTime=occurrence.Period.EffectiveEndTime??startTime;
     DateTime start=LocalTime(startTime,zone), finish=LocalTime(endTime,zone);
     if(start>=end.AddDays(1) || (finish<=day && !(finish==start && start>=day))) continue;
     var item=new AgendaItem { Title=Plain(entry.Summary,"Untitled event"),Location=Plain(entry.Location,""),StartTicks=start.Ticks,EndTicks=Math.Max(start.Ticks,finish.Ticks),AllDay=!startTime.HasTime };
     if(start<end) result.Add(item); else tomorrow.Add(item);
     if(result.Count>200 || tomorrow.Count>200) throw new CalendarFeedException("More than 200 events overlap a day. Use a smaller calendar.");
    }
    return new AgendaSnapshot { Day=day.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),TimeZoneId=zone.Id,Source=source,UpdatedUtc=DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture),Items=result.OrderBy(i=>!i.AllDay).ThenBy(i=>i.StartTicks).ToList(),NextDayItems=tomorrow.OrderBy(i=>i.StartTicks).ToList() };
   } catch(OperationCanceledException) { throw; } catch(CalendarFeedException) { throw; } catch { throw new CalendarFeedException("Could not read the calendar's events or time zones. Check the feed and try again."); }
  }
  static DateTime LocalTime(CalDateTime value,TimeZoneInfo zone) { return DateTime.SpecifyKind(value.IsFloating?value.Value:TimeZoneInfo.ConvertTimeFromUtc(value.AsUtc,zone),DateTimeKind.Unspecified); }
  static string Plain(string value,string fallback) { string clean=Regex.Replace(value??"",@"[\x00-\x1F\x7F]+"," ").Trim(); return clean.Length==0?fallback:clean.Substring(0,Math.Min(300,clean.Length)); }
 }
}
