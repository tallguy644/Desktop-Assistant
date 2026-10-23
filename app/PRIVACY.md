# Desktop Assistant privacy notice

Effective October 20, 2026. This notice describes the current desktop prototype. Publisher/support: [tallguy644 on GitHub](https://github.com/tallguy644/Desktop-Assistant/issues). Do not post credentials or private logs in public issues.

The app has no developer-operated analytics, advertising, or remote storage service. Optional connections contact their respective providers directly. Their privacy policies and account terms also apply.

## Data used

- **Microphone:** Windows speech recognition handles voice commands and dictated questions while listening is enabled. Recognized text may be displayed locally. A question submitted to ChatGPT is sent as text; this app does not upload microphone recordings to ChatGPT. Windows speech settings may affect Microsoft's own processing.
- **Speech output:** local Windows voices operate through Windows. Optional Azure Speech sends the text to be spoken to Microsoft, which can include a greeting, agenda, or chat answer. The account key and region are supplied by you. Loading the voice catalog contacts the regional Azure Speech service using that key; public voice metadata is cached locally for browsing without a connection. The catalog cache contains no account key.
- **AI chat:** optional OpenAI sign-in sends authentication requests to OpenAI. Submitted prompts and relevant conversation context are sent to OpenAI for responses. Account identifiers, refresh tokens and local conversation history are stored for the current Windows account. Disconnect to revoke local access; use your provider's account settings for provider-side controls.
- **Calendar:** your private calendar feed URL is fetched over the network. Calendar events are cached locally and used for agenda and departure reminders. Anyone with that private URL can potentially read the feed; never publish it.
- **Maps:** route locations and addresses entered for commute planning are sent to Google Maps when you use that feature. Embedded WebView2 may retain cookies, website data and sign-in state in its local profile. Website privacy policies govern that browsing activity.
- **Hardware and apps:** Windows counters and your configured hardware monitor feed provide local system readings. Mappings contain local app paths. Only mapped applications are launched.
- **Phone companion (off by default):** when you turn it on in Setup, the app listens on a local network port so phones you pair can request app launches, screen wake/off, media and volume keys, color changes, system readings, the title of what's playing in Spotify or a YouTube browser tab, agenda, commute and chat answers. Only addresses on your home network or your Tailscale network are accepted, and every request needs that phone's pairing token. Tokens are stored on the PC only as hashes inside Windows current-user data protection; on the phone the token is encrypted with an Android Keystore key. Traffic is plain HTTP: Tailscale encrypts it end to end, while on home Wi-Fi it stays inside your network. Questions asked from the phone are handled exactly like typed questions (sent to OpenAI). Remove a phone in Setup to revoke it; turning the companion off closes the port. It cannot run arbitrary commands or open files.
- **Personalization and diagnostics:** your entered name, colors, display choices, thresholds and settings are saved locally. Status files may contain sensor errors, timing and monitor identifiers. They are not automatically sent to the developer.

## Storage and controls

The unpackaged app uses `%LOCALAPPDATA%\JarvisDesk`. Sensitive service credentials, private calendar/commute configuration and conversation data use Windows current-user data protection where implemented. Other preferences and diagnostic status files are plain local files. Windows/browser profile storage has its own protections. Do not treat this folder as a portable backup or share it publicly.

Turn off listening, disconnect services and clear the conversation using the app controls when desired. To remove all unpackaged local data, close the app and delete `%LOCALAPPDATA%\JarvisDesk` after uninstalling; the normal uninstaller preserves preferences. Packaged Windows installations can also use Windows **Settings > Apps > Installed apps > Advanced options > Reset**, if available. Review/remove any shared legacy profile separately. Provider-side data and browser account history require provider controls; deleting local files does not delete those records.

Microphone use and online services are optional. Typed/manual controls remain available. No information is sold by this app. Report privacy concerns through the support link without including sensitive values. Update this notice if the distributed app's behavior changes.
