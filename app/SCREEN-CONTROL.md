# Screens off after 10 minutes; wake by voice

The installed configuration uses a **10-minute inactivity timer** and starts microphone listening with Assistant. Click **SCREENS / 10 MIN** in the left column to view or change it.

- **Assistant, wake up** or **Assistant, screens on** turns the screens on and restarts the timer.
- **Assistant, screens off** turns the screens off immediately.
- Keyboard, mouse, and accepted voice commands restart the timer. Assistant defers automatic screens-off while speaking, dictating a question, or processing a ChatGPT request.
Say **wake up**, **screens on**, or **screens off** directly while listening. No assistant name is required. Low-confidence commands are ignored.

Screen control keeps the **computer awake and Windows unlocked** while Assistant is listening. It temporarily pauses the screen saver without changing your saved Windows screen-saver profile or its password setting. **STOP LISTENING**, disabling the mode, or closing Assistant releases its power requests and restores the previous screen-saver state. Pausing LISTEN also pauses voice wake. Restarting Assistant with this feature enabled starts listening again.

**Win+L still locks Windows.** A locked session is not unlocked by voice: sign in normally with your PIN, password, or Windows Hello. Manually sleeping/hibernating the computer also stops ordinary app listening. The app never stores Windows sign-in credentials, removes your PIN, enables automatic sign-in, or types into a sign-in screen. It pauses power control on manual lock and starts a fresh timer after normal unlock. Anyone physically at an unlocked computer can use it.

The feature controls Windows display standby for connected monitors that support it; it cannot turn on a monitor switched off using its physical power button. Other hardware/software can affect display behavior. Test the spoken commands with your own microphone and monitors after installation. If speech is unavailable, use the keyboard or mouse to wake the screens.

Voice wake sends the Windows display-on request plus two mouse-movement input events. The pointer moves one physical pixel and returns to its original position, using the entire virtual desktop so multiple monitors and negative coordinates are supported. With a mouse button held, it sends stationary activity to avoid dragging. No clicks, scrolling, or keyboard input are sent. This fallback handles display drivers that accept the older display-on request without actually waking. The spoken response says a wake is being requested; a software acknowledgment alone cannot verify the physical monitor's state.

## Setup and restoring previous settings

This PC was configured with **Configure-Screens.ps1**, which backs up the current Windows power plan's display timeouts and sets plugged-in and battery display timeouts to **600 seconds**. It enables Assistant screen control at 10 minutes. Computer sleep timeouts and sign-in requirements are unchanged; Assistant requests that the computer stay awake only while its voice screen mode is active. The saved Windows display timeout remains 10 minutes after Assistant closes. Changes made in the Assistant panel apply to Assistant's timer while the feature runs.

For another PC, extract the complete ZIP, install Assistant, close it, then run from the extracted Assistant folder:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Configure-Screens.ps1
```

Reopen Assistant. To restore the earlier Windows display timeouts and disable the feature, close Assistant and run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Configure-Screens.ps1 -Restore
```

The backup is `%LOCALAPPDATA%\JarvisDesk\screen-windows-backup.json`. Feature preferences are in `screens.json`; a nonsecret `screen-status.json` reports the running process, listening state, active mode, timeout, and errors. Neither file contains voice recordings or passwords. Recognition is local; spoken confirmations use your selected voice provider.

If Assistant exits unexpectedly, Windows releases its process's awake request. The temporary screen-saver change is recovered at Assistant's next launch or when Windows begins a new sign-in session. Restart Assistant and close it normally to restore the saver immediately. The recovery record is `screen-control-restore.json` and is deleted after a successful restore.

Windows organization/device inactivity policies or Dynamic Lock may prevent unlocked screen control. Assistant reports this and does not override those settings. Resolve personal Dynamic Lock preferences through Windows Sign-in options if needed.

The implementation uses Microsoft's documented [display power command](https://learn.microsoft.com/en-us/windows/win32/menurc/wm-syscommand), [session input inactivity](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getlastinputinfo), [awake requests](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-setthreadexecutionstate), and [screen-saver settings](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfow).
