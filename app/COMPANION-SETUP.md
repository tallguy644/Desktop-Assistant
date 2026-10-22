# Phone companion setup

The **Syra Companion** Android app lets you control Syra from your phone:

- **PC tab** – live CPU/RAM/GPU load, CPU/GPU temperatures (°F), fan speeds, and **Wake** / **Off** buttons for your screens.
- **Apps tab** – tap any of your Syra app mappings to open it on the PC.
- **Ask tab** – type or speak a question (answered by the ChatGPT account connected in Syra), or tap **Agenda** / **Commute**. Optionally have the answer spoken on the PC too.

The companion is **off by default** and only paired phones can use it.

## 1. Get the builds

Both are built automatically by GitHub Actions (the **Phone companion** workflow):

- **Syra with companion support** – download the `Syra-with-companion` artifact and replace `Skynet.exe` in your install folder, or run `app\Build.cmd` yourself.
- **Android app** – download the `SyraCompanion-apk` artifact on your phone, unzip it, and open `SyraCompanion.apk`. Android will ask you to allow installs from your browser or file manager the first time.

## 2. Install Tailscale (to connect from anywhere)

Tailscale creates a private, encrypted network between your own devices. Nothing is exposed to the internet and you don't need port forwarding.

1. Install Tailscale on the PC (tailscale.com/download) and sign in.
2. Install Tailscale from the Play Store on your phone and sign in **with the same account**.
3. Leave Tailscale connected on both. Your PC gets an address starting with `100.`.

Skip this if you only want the companion at home on Wi-Fi.

## 3. Turn on the companion in Syra

1. Open Syra → **SETUP** → **Phone companion**.
2. Check **Allow paired phones to connect** and choose **SAVE** (port `47820` by default).
3. Choose **ALLOW IN FIREWALL** and approve the Windows prompt. (If you skip this, Windows may ask the first time your phone connects. Allow it.)
4. The page lists the addresses to use. The **Tailscale** one works anywhere; the **home Wi-Fi** one only works at home.

## 4. Pair your phone

1. In Syra, choose **PAIR NEW PHONE**. A 6-digit code appears for three minutes.
2. In Syra Companion, enter the PC address, the port and the code, then tap **PAIR**.

The phone then appears under **Paired phones**. Pair up to 8 phones.

## Notes

- **Screens Wake/Off** use Syra's existing screen control: turn on **Screens & voice wake** in Setup, and Syra must be listening. Windows lock still requires your normal sign-in.
- **Ask** needs ChatGPT connected in Syra. Answers can take up to a couple of minutes.
- **Agenda** and **Commute** need Google Calendar and Work & commute set up in Syra.
- The phone only polls the PC while the app is open, so it doesn't drain your battery in the background.
- To revoke a phone, select it in Setup and choose **REMOVE SELECTED**, or use **Unpair this phone** in the app menu. Turning the companion off closes the port entirely.

## Security

- Only home-network (10.x, 172.16–31.x, 192.168.x) and Tailscale (100.64–127.x) addresses are accepted; public internet addresses are refused even if a port is forwarded.
- Each phone gets its own random 256-bit token. Syra stores only a hash of it, protected with Windows data protection; the phone encrypts it with an Android Keystore key.
- Pairing codes work once, expire after three minutes, and are cancelled after five wrong guesses. Ten failed attempts from one address lock it out for ten minutes.
- The phone can only do what's listed above: launch **mapped** apps, control screens, read status/agenda/commute, and ask questions. It can't run commands or open files.
- Traffic is HTTP. Over Tailscale it is encrypted by Tailscale; on home Wi-Fi it stays on your local network. Don't use the home-Wi-Fi address on networks you don't trust—use Tailscale instead.

## Building the Android app yourself

Open `companion/android` in Android Studio, or run `./gradlew assembleDebug` there with JDK 17 and the Android SDK installed. The APK lands in `app/build/outputs/apk/debug/`.

## Companion API (for reference)

All requests except `hello` and `pair` need `Authorization: Bearer <token>`. JSON in and out.

| Method | Path | Body | Returns |
|---|---|---|---|
| GET | `/api/hello` | – | `{app:"syra", api:1}` |
| POST | `/api/pair` | `{code, device}` | `{token, name, deviceId}` |
| GET | `/api/status` | – | name, state, listening, stats, screens, chat, calendar, commute |
| GET | `/api/apps` | – | `{apps:[names]}` |
| POST | `/api/apps/launch` | `{name}` | `{message}` |
| POST | `/api/screens` | `{action:"wake"\|"off"}` | `{message}` |
| POST | `/api/ask` | `{question, speak}` | `{answer}` |
| GET | `/api/agenda` | – | `{text, offline, items:[{time,title,location,allDay}]}` |
| GET | `/api/commute` | – | `{text}` |
| POST | `/api/unpair` | – | `{ok:true}` |
