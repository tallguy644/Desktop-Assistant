@echo off
setlocal
cd /d "%~dp0"
set "FX=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
if not exist "%FX%\csc.exe" set "FX=%WINDIR%\Microsoft.NET\Framework\v4.0.30319"
set "SPEECH=%WINDIR%\Microsoft.NET\assembly\GAC_MSIL\System.Speech\v4.0_4.0.0.0__31bf3856ad364e35\System.Speech.dll"
"%FX%\csc.exe" /nologo /target:winexe /platform:x64 /out:Skynet.exe /win32icon:Skynet.ico /reference:"%FX%\WPF\PresentationFramework.dll" /reference:"%FX%\WPF\PresentationCore.dll" /reference:"%FX%\WPF\WindowsBase.dll" /reference:"%FX%\netstandard.dll" /reference:Microsoft.Web.WebView2.Core.dll /reference:Microsoft.Web.WebView2.Wpf.dll /reference:Ical.Net.dll /reference:NodaTime.dll /reference:Portable.System.DateTimeOnly.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xaml.dll /reference:"%SPEECH%" /reference:System.Runtime.Serialization.dll /reference:System.Security.dll /reference:System.Management.dll /reference:System.Web.dll /reference:System.Web.Extensions.dll DesktopHost.cs DesktopTaskbar.cs DesktopControls.cs Appearance.cs ComputerSetup.cs AssistantIdentity.cs ChatNaming.cs Skynet.cs Telemetry.cs HudVisuals.cs ReferenceHud.cs NaturalVoice.cs VoiceControls.cs ChatGptClient.cs ChatGptResponses.cs ChatPanel.cs CalendarFeed.cs CalendarPanel.cs AgendaSpeech.cs Commute.cs MapsBrowser.cs CommuteControls.cs NextUp.cs ScreenPower.cs ScreenControls.cs TemperatureAlerts.cs TemperatureAlertControls.cs
if errorlevel 1 (echo Build failed. & pause & exit /b 1)
"%FX%\csc.exe" /nologo /target:winexe /platform:x64 /out:TaskbarRecovery.exe /reference:"%FX%\WPF\PresentationCore.dll" /reference:"%FX%\WPF\PresentationFramework.dll" /reference:"%FX%\WPF\WindowsBase.dll" /reference:System.Runtime.Serialization.dll DesktopHost.cs DesktopTaskbar.cs TaskbarRecovery.cs
if errorlevel 1 exit /b 1
echo Built Skynet.exe and TaskbarRecovery.exe














