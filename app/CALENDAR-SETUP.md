# Show Google Calendar in Assistant

The **TODAY / AGENDA** section replaces the large top-left date dial. It shows calendar events for the current day using your PC's local date and time zone. Busy days scroll; hover an event for its full time range and location.

## Connect once

1. Open [Google Calendar settings](https://calendar.google.com/calendar/u/0/r/settings) in your browser.
2. Under **Settings for my calendars**, choose the calendar you want Assistant to show.
3. Select **Integrate calendar** and copy **Secret address in iCal format**. Use the secret address, not the public address or browser address. Keep your calendar private.
4. In Assistant's top-left **TODAY / AGENDA** area, click **CONNECT**. Paste the link into the masked field and click **SAVE**. Paste it directly into Assistant, not into this chat.
5. Assistant checks the feed and shows today's events. It saves the connection only after the feed downloads and parses successfully.

Google documents these steps in [Sync your calendar with computer programs](https://support.google.com/calendar/answer/37648?hl=en). If an organization's calendar does not offer a secret address, its administrator may restrict that feature.

## Daily use

- Assistant refreshes at startup, every **five minutes while open**, and when the local calendar day changes. **REFRESH** requests an update immediately. A timestamp shows the last successful download. Google's published feed may lag behind edits; this is periodic read-only sync, not an instant update channel.
- Recurring events, exclusions, moved/cancelled occurrences, all-day events, and events overlapping midnight are supported. All-day end dates are exclusive. Overnight end times show **(+1d)** where appropriate. Times follow Windows' local time zone; restart Assistant after changing that system setting.
- Today's events remain listed after their scheduled end. This is an agenda, not a completion tracker. It displays events from the one linked calendar; it does not combine other calendars or retrieve Google Tasks.
- **MANAGE** lets you paste a replacement link. **CANCEL** clears the entry field and cancels a pending replacement. **REMOVE LINK** deletes Assistant's saved link and agenda cache. It does not delete anything from Google Calendar. Revoke the feed itself with **Reset** beside Google's secret address if needed.
- **HELP** opens Google's instructions in your browser. The connection form stays inside the HUD.

## Read your agenda

The **NEXT UP** dial above the conversation shows your next timed event from today's or tomorrow's synced calendar, its start time, and a live countdown. Its glowing ring fills during the final 60 minutes. Long titles are shortened to fit; hover for the full title and time. Saved offline data is labelled **SAVED**. When no future timed event remains, it can show an event in progress, an all-day event, or **ALL CLEAR**. A missing calendar shows setup/unavailable status rather than an empty day.

With a configured, confirmed, fresh Maps work route, the dial switches to **DEPARTURE** when leaving for work is the next action within four hours. It includes your arrival buffer. During the final 15 minutes it turns amber; when departure is due it shows **LEAVE NOW**. An earlier calendar event keeps priority over a later departure. Stale/offline route data returns the dial to calendar mode, and **ON MY WAY** dismisses that shift's departure display. See [COMMUTE-SETUP.md](COMMUTE-SETUP.md).

Click **▷ READ** or say **“Assistant, read my agenda”** while LISTEN is enabled. F8 accepts **“read my agenda”** without the wake phrase when Assistant has focus. Assistant refreshes first, then reads your events in your saved voice. Offline readings identify the saved calendar. Very long agendas announce the remaining event count; all events remain visible on screen. The agenda is read on request, without automatic startup narration.

The cleaner agenda uses an open timeline and small READ, CONNECT/MANAGE, refresh-arrow, and WORK controls. WORK configures [departure reminders](COMMUTE-SETUP.md).

## Offline and privacy

A saved copy of today's successful agenda can remain visible offline with an **OFFLINE / saved** label. A failed download is not shown as an empty day. Yesterday's saved agenda is cleared when the day changes, rather than being presented as today's schedule. A link change cannot reuse another calendar's cached events. Without an initial successful sync, no events are invented.

The private URL and today's agenda cache are encrypted using Windows current-user protection (DPAPI) in `%LOCALAPPDATA%\JarvisDesk\calendar-link.protected` and `calendar-agenda.protected`. Programs running as your Windows user can access that user's protected data. The masked URL is never copied to diagnostics, previews, or the download package. Treat the link like a password: anyone who has it may be able to read that calendar.

Assistant fetches only the supported HTTPS private-feed address on `calendar.google.com`, does not follow redirects, and performs read-only GET requests. It does not upload, edit, create, or delete Google events, and it does not send calendar data to ChatGPT. Requested narration uses your selected voice: with Azure selected, event titles and times go to Microsoft for speech synthesis. Calendar links and event locations are excluded from agenda narration. Work route checks send the starting point and destination to Google Maps as described in COMMUTE-SETUP.md. The Google Calendar connection available in this support chat is separate from the desktop app's saved private-feed connection.

The cache also includes next-day events for early work departures before midnight. The app limits a feed to 20 MB, 20,000 source events, and 200 events per day. Complex recurrence evaluation has limits and reports failure rather than silently dropping events. A failed refresh retains a clearly labelled saved agenda for the same date and calendar when available.

## Build and check

Keep the entire extracted folder together, including all DLL files and **Syra.exe.config**. **Install.cmd** copies these dependencies automatically. No separate calendar runtime or Google Cloud project is needed. The included parser libraries and licenses are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

Run **Build.cmd** to compile the source with the bundled libraries. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Test-Calendar.ps1` runs synthetic recurrence, date/time, URL restriction, cancellation, and encrypted-cache tests. It uses fixture calendars and never requests your private feed.
