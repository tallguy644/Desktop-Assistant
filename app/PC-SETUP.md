# Install on another Windows PC

This is a Windows desktop application. The package targets **Windows 10/11 x64** with **.NET Framework 4.8 or newer**. Windows 11 is recommended. The app has been tested on the development Windows 11 PC; it is not certified for every PC or hardware configuration. macOS, Linux, 32-bit Windows and Windows on ARM are not supported builds of this package.

1. Copy **Desktop-Assistant-Setup.exe** to the other PC and run it. The installer is unsigned, so Windows may show a publisher/SmartScreen prompt. Alternatively, extract **Desktop-Assistant-Windows.zip** and run `Skynet\Install.cmd`.
2. Choose one of that PC's detected monitors. Leave Interactive desktop selected for background mode, or use normal window mode. Windows startup is optional.
3. The app discovers local Start-menu shortcuts and common Windows applications. Open **APP MAPPINGS** to review trusted executable/shortcut paths and add missing apps. App paths are discovered on each PC rather than copied from the development computer.
4. Open **COLORS / SETUP** to choose any RGB colors for the HUD, rings and core. Select **PC SETUP** for local readiness information.
5. For voice input, enable microphone access for desktop apps in Windows and install English speech recognition if it is missing. Choose an installed microphone in Windows sound settings. Manual controls and typed chat remain available without speech recognition.
6. Windows synthetic voices work offline when installed. For an online voice, configure your own service and credentials through **VOICE SETTINGS**. Sign in to ChatGPT separately on each PC. Calendar and commute configuration are also private to that Windows account.
7. For temperatures, GPU usage and fan/pump RPM, install a compatible Libre Hardware Monitor build, run it with the permissions its sensors require, and enable its local web server on port 8085. Missing or unsupported sensors remain **Unavailable**. Generic Fan # labels do not establish radiator fan or pump wiring. CPU/RAM usage uses native Windows counters and does not require this monitor service.
8. Install the Microsoft WebView2 Runtime if the optional Google Maps route browser is unavailable. Internet services still need a connection and their normal accounts.

The HUD scales to its selected monitor; the normal window fits smaller primary-display work areas. A saved monitor that no longer exists falls back to the primary display. Desktop/taskbar integration depends on Windows Explorer, and the app can return to window mode if its desktop host changes.

The installer includes the application, dependencies and guides, not the developer's accounts, API keys, calendar links, addresses or conversation history. Preferences and protected account data live under `%LOCALAPPDATA%\JarvisDesk` for the current Windows account. To move colors alone, copy `appearance.json` while the app is closed. Set up apps and sign-ins afresh on each PC.

Official prerequisites:

- [.NET Framework system requirements](https://learn.microsoft.com/en-us/dotnet/framework/get-started/system-requirements)
- [Install .NET Framework](https://learn.microsoft.com/en-us/dotnet/framework/install/)
- [Microsoft WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)
- [Libre Hardware Monitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)
