# Interactive desktop background

This mode runs the same assistant as an interactive desktop on one monitor. It covers that monitor's desktop icons while enabled and stays behind ordinary application windows. By default it fills the usable work area. With HUD-only taskbar auto-hide enabled, it fills the monitor and reveals that taskbar at the screen edge. The original wallpaper and icons are not deleted or replaced.

## Install and start

1. Run **Desktop-Assistant-Setup.exe**, choose your monitor, and leave **Interactive desktop** selected. **Start with Windows** is optional.
2. Alternatively, extract the ZIP and run **Install-Desktop.cmd**. Close an existing assistant first. This installs for your Windows account without an administrator password.
3. The **Desktop Assistant Background** shortcut enters desktop mode. **Desktop Assistant** restores your saved desktop/window choice. Use the tray menu or Ctrl+Alt+D to choose window mode. Opening either shortcut while the app is already running switches the existing instance rather than starting a duplicate.
4. Right-click the reactor icon in the Windows notification area and select **Desktop settings / monitor** to change screens, enable or disable desktop mode, or choose **Start with Windows**. The tray icon may be under the hidden-icons arrow.

## Use and return to window mode

- Click the HUD to type and use its controls. Enter sends a chat question; Shift+Enter adds a line. F8 and F9 work when the HUD has keyboard focus.
- Continuous voice commands work while other programs are in front when **LISTEN** is enabled. Existing microphone permissions and command mappings still apply.
- **Ctrl+Alt+D** switches between desktop and window mode, unless another program already owns that hotkey. The tray menu remains available in that case.
- **F11** returns a pinned HUD to window mode. The minimize button also restores window mode before minimizing.
- Choose **Window mode** from the tray menu to expose desktop icons again. Choose **Exit** to stop the assistant completely.
- Changing modes preserves the chat draft, conversation, microphone state and running sensor/calendar services. Personal names, chat names, voice and account settings stay in the same Windows-user profile.

## Auto-hide only the HUD taskbar

In the tray icon's **Desktop settings / monitor** window, enable **Auto-hide taskbar on the HUD monitor only**, then **SAVE & APPLY**. Move the pointer to the bottom edge of the HUD monitor to reveal its taskbar. You can also hover or click the small **TASKBAR ↑** tab at the lower-right edge of the HUD to reveal it for five seconds. Move away and it hides after a short delay. The other monitors keep their existing taskbar behavior.

This option works while desktop mode is running and does not change the Windows-wide auto-hide setting. Window mode, disabling this option, or exiting restores the taskbar. A small recovery companion also restores it if the assistant unexpectedly exits. If Windows-wide auto-hide is already enabled, switch that Windows setting off before enabling HUD-only auto-hide.

## Dependencies and limits

Use Windows 10/11 x64 with .NET Framework 4.8. WebView2 Runtime is needed for the Maps browser. CPU/GPU/fan sensors still require Libre Hardware Monitor and its local feed. Online voices, ChatGPT and Google services retain their separate setup and network requirements. No personal keys, logins, calendars or addresses are included in the installer.

Desktop hosting depends on Windows Explorer. If the shell or display arrangement changes, the app checks the host and monitor placement and attempts to restore window mode when the desktop host is unavailable. A disconnected saved monitor falls back to the primary monitor. This mode does not bypass Windows sign-in or keep listening through PC sleep.

To remove it, use **Exit** from the tray, then run **Uninstall.cmd** in `%LOCALAPPDATA%\JarvisDesk\bin`. The app's own startup entry and shortcuts are removed; your saved profile is retained.

The installer is unsigned; Windows may show an unfamiliar-publisher or SmartScreen prompt. It is not a Microsoft Store-certified package.



