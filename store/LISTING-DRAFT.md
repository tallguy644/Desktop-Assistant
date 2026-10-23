# Store listing draft — review before submission

Copy these into Partner Center → your app → Store listings. Everything here is a draft; edit freely.

**Product name:** Syra (reserve it first in Partner Center. If it's taken, try "Syra Desktop" or "Syra HUD" and put the reserved name in `store-identity.json` as `DisplayName`.)

**Short description (up to 100 characters for some placements):**
Your voice-controlled desktop HUD: system stats, app launches, agenda and a phone companion.

**Description:**

Turn your Windows desktop into a personal command center. Syra combines an animated reactor HUD in your own colors with voice shortcuts to your apps, a daily agenda, live system monitoring and an optional phone companion. Use it as a movable window or as an interactive desktop on the monitor you choose.

Say "open" and a mapped app name, or use push-to-talk. Add a private read-only calendar feed for today's agenda and Next Up, and get a spoken heads-up when it's time to leave for work. Connect compatible local hardware monitoring to see CPU and GPU temperatures, load and fan speeds, with optional spoken temperature alerts. Turn your screens off and wake them by voice.

Pair the optional Syra Companion Android app to control your PC from your phone at home or, with Tailscale, from anywhere: launch apps, play/pause and skip Spotify or YouTube, change PC volume, wake or turn off screens, watch live stats and ask questions. Colors you pick on the PC or the phone stay in sync. Pairing uses a one-time code, and only your home network or your own Tailscale network can connect.

Optional online voices and AI chat use your own eligible provider accounts. Manual controls remain available when voice or online services aren't. Requires Windows 10/11 x64. Hardware sensors, microphones, language support and optional services vary by PC. Thermal alerts supplement your PC's built-in protections.

**Features (one per line in Partner Center):**
- Animated HUD with your own accent, ring and core colors
- Voice and push-to-talk launching of apps you choose
- Daily agenda, Next Up and work-departure reminders
- Live CPU, GPU, RAM, temperature and fan monitoring
- Voice screen off / wake
- Optional phone companion: media, volume, apps, stats, screens and questions
- Desktop mode on the monitor you choose
- Optional AI chat with your own account

**Category:** Productivity (or Utilities & tools)

**Search terms:** voice assistant, desktop HUD, system monitor, PC remote, media remote

**Support contact:** https://github.com/tallguy644/Desktop-Assistant/issues

**Privacy policy URL:** https://github.com/tallguy644/Desktop-Assistant/blob/main/PRIVACY.md (merge the phone-companion pull request first so this page covers the companion)

**Screenshots (at least 1, 1366×768 or larger, up to 10):** HUD in default colors; Setup → Appearance & colors; Setup → Phone companion (with no real addresses visible); a calendar/agenda view with made-up events. Avoid real names, addresses, calendar entries or account details.

**Notes for certification (Submission options → Notes for certification):**
Syra is a WPF desktop app packaged as MSIX (runFullTrust). runFullTrust is needed for the WPF UI, launching apps the user has whitelisted, reading local performance counters, Windows speech recognition, multi-monitor desktop integration and the bundled taskbar-recovery helper. The microphone is used only while the user turns on listening or push-to-talk. privateNetworkClientServer and the firewall rule are used only by the optional phone companion, which is off by default (Setup → Phone companion); when enabled it accepts only paired phones on private/Tailscale addresses with a per-phone token. No account is required to test core features; AI chat, online voices and calendar are optional and need the reviewer's own accounts. No remote command execution, no privilege elevation.

**Still to decide in Partner Center:** price (free?), markets, age rating questionnaire, and whether to start with a private (hidden) audience before going public.
