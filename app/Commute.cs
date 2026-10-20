using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace JarvisDesk {
 public sealed class CommutePreferences {
  public bool Enabled;
  public string Origin="",Destination="",WorkTitle="Work",ConfirmedRouteKey="",CanonicalOrigin="",CanonicalDestination="";
  public int ArrivalBufferMinutes=15,LeadMinutes=10;
  public void Validate() {
   Origin=Address(Origin); Destination=Address(Destination); WorkTitle=(WorkTitle??"").Trim();
   if(Enabled && Origin.Length==0) throw new ArgumentException("Enter your starting address privately here.");
   if(WorkTitle.Length==0 || WorkTitle.Length>80 || WorkTitle.Any(Char.IsControl)) throw new ArgumentException("Enter a work event title, up to 80 characters.");
   if(ArrivalBufferMinutes<0 || ArrivalBufferMinutes>120 || LeadMinutes<1 || LeadMinutes>60) throw new ArgumentException("Arrival buffer: 0–120 min. Heads-up: 1–60 min.");
  }
  public static string Address(string value) { value=(value??"").Trim(); if(value.Length>500 || value.Any(Char.IsControl)) throw new ArgumentException("Use an address up to 500 characters without line breaks."); return value; }
 }
 public sealed class MapsReading { public string Origin,Destination,Duration,Mode,Page; }
 public sealed class TravelEstimate { public string RouteKey,Origin,Destination,UpdatedUtc; public int Minutes; }
 public sealed class DepartureNotices { public string TripKey=""; public bool HeadsUp,LeaveNow,OnMyWay; }
 public static class Commute {
  public static AgendaItem WorkEvent(AgendaSnapshot agenda,CommutePreferences settings,DateTime now) {
   if(agenda==null || agenda.Items==null || !settings.Enabled) return null;
   return agenda.Items.Concat(agenda.NextDayItems??new List<AgendaItem>()).Where(i=>!i.AllDay && i.Start>now && Regex.IsMatch(i.Title??"",@"(?<!\w)"+Regex.Escape(settings.WorkTitle)+@"(?!\w)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)).OrderBy(i=>i.StartTicks).FirstOrDefault();
  }
  public static string RouteKey(string origin,string destination) { return CalendarFeed.SourceId(CommutePreferences.Address(origin)+"\n"+CommutePreferences.Address(destination)); }
  public static string TripKey(AgendaItem item,string routeKey) { return CalendarFeed.SourceId(item.StartTicks+"|"+item.EndTicks+"|"+item.Title+"|"+routeKey); }
  public static Uri MapsUrl(string origin,string destination) {
   origin=CommutePreferences.Address(origin); destination=CommutePreferences.Address(destination);
   if(origin.Length==0 || destination.Length==0) throw new ArgumentException("Set a starting address and a work destination first.");
   string url="https://www.google.com/maps/dir/?api=1&origin="+Uri.EscapeDataString(origin)+"&destination="+Uri.EscapeDataString(destination)+"&travelmode=driving";
   if(url.Length>2048) throw new ArgumentException("These addresses make the Maps link too long. Use shorter street addresses."); return new Uri(url);
  }
  public static int ParseDuration(string text) {
   var match=Regex.Match((text??"").Trim(),@"^(?:(\d{1,2})\s*(?:hr|hrs|hour|hours)\s*)?(?:(\d{1,3})\s*(?:min|mins|minute|minutes))?$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
   if(!match.Success || (!match.Groups[1].Success && !match.Groups[2].Success)) throw new ArgumentException("Maps has not provided a single driving time yet.");
   int value=(match.Groups[1].Success?Int32.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture)*60:0)+(match.Groups[2].Success?Int32.Parse(match.Groups[2].Value,CultureInfo.InvariantCulture):0);
   if(value<1 || value>720) throw new ArgumentException("This route's driving time is outside the supported 1–720 minute range."); return value;
  }
  public static bool Fresh(TravelEstimate estimate,string key,DateTime utcNow) { DateTime stamp; return estimate!=null && estimate.RouteKey==key && estimate.Minutes>=1 && estimate.Minutes<=720 && DateTime.TryParse(estimate.UpdatedUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out stamp) && stamp.Kind==DateTimeKind.Utc && stamp<=utcNow && utcNow-stamp<=TimeSpan.FromMinutes(10); }
  public static DateTime LeaveAt(AgendaItem item,int minutes,int buffer) { return item.Start.AddMinutes(-minutes-buffer); }
 }
}
