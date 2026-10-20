using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace JarvisDesk {
 public static class LocalIntent {
  public static readonly string[] Phrases={"read my agenda","read today's agenda","what is my agenda","what's on my calendar today","what do i have today","when should i leave for work","when do i need to leave for work","open my work route","open google maps for work"};
  public static string Parse(string text,bool wakeRequired,bool ptt) {
   string clean=Regex.Replace((text??"").ToLowerInvariant(),@"[^a-z0-9' ]"," "); clean=Regex.Replace(clean,@"\s+"," ").Trim();
   clean=AssistantIdentity.Normalize(clean); bool wake=AssistantIdentity.StripWake(ref clean); if(!wake && wakeRequired && !ptt) return null;
   int index=Array.IndexOf(Phrases,clean); return index<0?null:index<5?"agenda":index<7?"commute":"maps";
  }
 }
 public static class AgendaSpeech {
  public static string Build(AgendaSnapshot snapshot,DateTime day,bool offline) {
   if(snapshot==null || snapshot.Day!=day.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture) || snapshot.Items==null) return "Your agenda is unavailable. Connect Google Calendar or refresh your calendar in " + AssistantIdentity.CurrentName + ".";
   var text=new StringBuilder(offline?"Using your saved calendar. ":"");
   if(snapshot.Items.Count==0) return text+"You have no calendar events today.";
   text.Append("Today's agenda. "); int count=0;
   foreach(var item in snapshot.Items) {
    string title=Regex.Replace(item.Title??"Untitled event",@"[\x00-\x1F\x7F]+"," "); if(title.Length>120) title=title.Substring(0,117)+"…";
    string time=item.AllDay?"All day":item.Start.Date<day.Date?"Continuing from yesterday":item.Start.ToString("h:mm tt",CultureInfo.InvariantCulture);
    string entry=time+", "+title+". "; if(text.Length+entry.Length>1050) break; text.Append(entry); count++;
   }
   if(count<snapshot.Items.Count) text.Append("And "+(snapshot.Items.Count-count)+" more events shown on screen.");
   return text.ToString();
  }
 }
 public partial class Desk {
  DateTime agendaReadUntil;
  void ReadAgenda() { agendaReadUntil=DateTime.UtcNow.AddMinutes(2); if(calendarUrl.Length>0 && !agendaBusy) RefreshCalendar(); }
  void CheckAgendaNarration() {
   if(agendaReadUntil==DateTime.MinValue) return;
   if(DateTime.UtcNow>agendaReadUntil) { agendaReadUntil=DateTime.MinValue; return; }
   if(speaking || questionListening || agendaBusy) return;
   agendaReadUntil=DateTime.MinValue;
   bool valid=calendarUrl.Length>0 && agendaSnapshot!=null && agendaSnapshot.Matches(DateTime.Today,TimeZoneInfo.Local,CalendarFeed.SourceId(calendarUrl));
   SpeakLocalSummary(AgendaSpeech.Build(valid?agendaSnapshot:null,DateTime.Today,agendaOffline));
  }
  void SpeakLocalSummary(string text,bool forceFallback=false) {
   thermalVoiceInProgress=true;
   SpeakText(text,forceFallback?ThermalVoice():voicePreferences,false,delegate(string error) {
    thermalVoiceInProgress=false;if(error!=null) Log("Spoken summary failed. Check Voice Settings.");
    var pending=queuedChatSpeech;queuedChatSpeech=null;if(pending!=null && readChatAloud.IsChecked==true && !closing) SpeakChatReply(pending);
   });
  }
 }
}
