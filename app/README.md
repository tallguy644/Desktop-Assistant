# Syra

A customizable Windows desktop HUD with an animated reactor, voice app launching, spoken responses, system sensors, daily agenda, and optional AI chat.

**Status: working desktop prototype; Microsoft Store submission preparation.** The preview MSIX uses a development identity and is unsigned. It is not an approved Store app.

## Install on Windows

Requires Windows 10/11 **x64**, .NET Framework 4.8, and WebView2 Runtime for embedded chat/Maps. ARM64 and 32-bit Windows are not supported by this build. Hardware sensors require a compatible Libre Hardware Monitor feed; unavailable sensors are displayed honestly.

1. Download this repository using **Code > Download ZIP**, extract it, and open `app`.
2. Run `Install.cmd` for a normal window, or `Install-Desktop.cmd` for an interactive desktop. These scripts install for your Windows account. Review the scripts before running them. The binaries are currently unsigned.
3. Open **COLORS / SETUP** to select your colors and run the PC setup checks. Choose your monitor in Desktop settings. F11 returns to window mode; the tray menu also controls desktop mode.
4. Configure application mappings, select **LISTEN**, and say “open Notepad.” F8 provides push-to-talk. Only explicitly mapped local applications/shortcuts are launched.
5. Configure optional services privately inside the app. Never put keys, calendar links, accounts, or personal configuration files in this repository.

See [PC setup](app/PC-SETUP.md), [desktop controls](app/DESKTOP-SETUP.md), [voice setup](app/VOICE-SETUP.md), [colors](app/APPEARANCE.md), and the other guides in `app`.

## Build

On Windows, run `app\Build.cmd` to compile with the .NET Framework compiler. Dependencies and their license texts are included. `scripts\Build-Release.ps1` builds the standalone setup and portable ZIP in `dist`. `store\Build-Msix.ps1 -DevelopmentPreview -MakeAppxPath <path-to-makeappx.exe>` builds the unsigned Store preview. See [Store setup](store/SUBMISSION.md) for the SDK tools and final identity requirements.

The **Windows build** GitHub Actions workflow runs the build, self-test, test suites and packaging on every push and pull request; the installer, ZIP and MSIX preview are attached to each run as artifacts.

Tests: run `app\Skynet.exe --self-test <empty-test-folder>`; run `app\Test-Chat.ps1` and `app\Test-Calendar.ps1` with Windows PowerShell. These test fixtures use synthetic data. Native display/taskbar, packaged activation, and actual microphone/sensor behavior must also be tested on target PCs.

## Features and limits

- Animated, configurable HUD colors; movable window or interactive desktop on one monitor.
- Whitelisted app launches, optional wake phrase, push-to-talk, original synthetic voices. No actor voice is supplied or claimed.
- CPU/RAM usage; optional CPU/GPU/board temperatures in Fahrenheit, GPU load and fan RPM. Spoken thermal alerts are configurable and supplement your hardware's own protections.
- Private read-only calendar link, agenda and Next Up; optional Google Maps commute view. Sensor labels cannot identify radiator fan wiring automatically.
- Optional ChatGPT sign-in and online speech require internet, eligible accounts and provider availability. No claim of free, unlimited or universal subscription access. Review current provider distribution requirements before a commercial release.
- Screen timeout/wake does not bypass Windows authentication. Desktop/taskbar behavior depends on Explorer and display configuration and remains a release test focus.

[Privacy](PRIVACY.md) · [Support](https://github.com/tallguy644/Desktop-Assistant/issues) · [Third-party notices](app/THIRD-PARTY-NOTICES.md) · [License status](LICENSE-STATUS.md)

The historical internal filename `Skynet.exe` and settings folder `JarvisDesk` remain for compatibility. They are not the product name. Personal settings are deliberately excluded from this repository.
