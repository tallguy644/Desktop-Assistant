# Work departure reminders

1. Connect Google Calendar using **CONNECT** in the agenda. Your work shift must be a timed event today or tomorrow, with a title containing **Work** (change that matching phrase under **WORK**).
2. Click **WORK** at the bottom of the agenda. Enter your starting address privately in Assistant. Add a work address override if your calendar event has no location. Addresses are masked in this form.
3. Keep **Spoken departure reminders** enabled. **Arrive early** defaults to **15 minutes**. The heads-up defaults to **10 minutes before departure**.
4. Choose **SAVE & CHECK**. A real Google Maps browser opens. Check the starting point, destination, and driving route. Resolve any Google consent screen if one appears, then check again if necessary. Choose **USE THIS ROUTE** to confirm the route.
5. Keep Assistant open, connected to the internet, and awake. The agenda footer shows the calculated departure time. The map window may be closed; Assistant retains its private browser in the background while running.

Departure time = work start − current Google Maps driving minutes − your arrival buffer. For an 8:00 AM shift, a 30-minute drive, and a 15-minute arrival buffer, leave at 7:15 AM. Estimates describe traffic **now**, rather than a prediction or guarantee for a later journey.

Assistant checks every five minutes during the four hours before the next shift. After restarting, a previously confirmed route can refresh in the background. A changed origin, destination, or resolved Maps address requires another confirmation. This build supports English driving summaries with a single duration, up to 12 hours. Page changes, offline Maps, consent, missing routes, other travel modes, ambiguous times, and stale readings cannot become departure alarms. Open **WORK → OPEN MAPS** when attention is needed.

There is one heads-up and one leave-now reminder per work occurrence. **ON MY WAY** dismisses reminders for that shift, including after restarting. A schedule change creates a new occurrence. Reminders require a successful calendar refresh within 15 minutes and a confirmed Maps reading within 10 minutes. They do not run while Assistant is closed or the PC is asleep. A missed departure time may be announced when Assistant becomes available before the shift starts.

Click **READ COMMUTE** or say **“Assistant, when should I leave for work?”** while LISTEN is enabled. **“Assistant, open my work route”** opens the Maps browser. F8 accepts these commands without the wake phrase while Assistant has focus. F9 is for ChatGPT question dictation. Set up and save settings before using these controls; CLOSE/ESC discards edits that have not been saved.

## Browser and privacy

No Google Maps API key or paid Maps API account is required. The route uses Google's documented [Maps URLs](https://developers.google.com/maps/documentation/urls/get-started). Google receives the entered origin and destination and may store them according to its own policies. The embedded Microsoft Edge WebView2 browser uses a separate profile in `%LOCALAPPDATA%\JarvisDesk\maps-browser`; it does not import your Chrome/Edge profile or sign-in. Browser profile files may retain route history, cache, and Google cookies. Google sign-in is not required for ordinary routes.

The work settings, latest estimate, and reminder dismissals are encrypted with Windows current-user protection in `commute-settings.protected`, `commute-travel.protected`, and `commute-notices.protected`. Programs running as your Windows user can access that user's data. Saved estimates are not reused for alarms after startup until a new reading succeeds. Assistant does not send route addresses to ChatGPT or speak them in commute summaries. Spoken timing summaries use the selected voice; with Azure selected, that summary text goes to Microsoft.

This Windows x64 build needs [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/). It is already installed on the tested PC. If Maps cannot open elsewhere, install the Evergreen x64 runtime from Microsoft and restart Assistant. Google may change its page structure; in that case Assistant reports that Maps needs attention and pauses reminders. The regular app launcher, hardware monitoring, and calendar remain usable.
