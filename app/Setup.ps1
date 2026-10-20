param([switch]$Uninstall)
$ErrorActionPreference = 'Stop'
$installRoot = Join-Path $env:LOCALAPPDATA 'JarvisDesk\bin'
$desktopLink = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Desktop Assistant.lnk'
$startLink = Join-Path ([Environment]::GetFolderPath('Programs')) 'Desktop Assistant.lnk'
$backgroundLinks=@((Join-Path ([Environment]::GetFolderPath('Desktop')) 'Desktop Assistant Background.lnk'),(Join-Path ([Environment]::GetFolderPath('Programs')) 'Desktop Assistant Background.lnk'))
if (Get-Process -Name JarvisDesk,Skynet -ErrorAction SilentlyContinue) { throw 'Close the assistant before installing or uninstalling.' }
if ($Uninstall) {
    foreach ($link in @($desktopLink, $startLink)+$backgroundLinks) { if (Test-Path -LiteralPath $link) { Remove-Item -LiteralPath $link } }
    $runPath='HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    $startupValue=(Get-ItemProperty -LiteralPath $runPath -Name DesktopAssistant -ErrorAction SilentlyContinue).DesktopAssistant
    if($startupValue -eq ('"'+(Join-Path $installRoot 'Skynet.exe')+'"')) {Remove-ItemProperty -LiteralPath $runPath -Name DesktopAssistant -ErrorAction SilentlyContinue}
    $expected = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'JarvisDesk\bin'))
    if ([IO.Path]::GetFullPath($installRoot) -ne $expected) { throw 'Unexpected installation path.' }
    if (Test-Path -LiteralPath $installRoot) { Remove-Item -LiteralPath $installRoot -Recurse -Force }
    Write-Host 'Removed app and shortcuts. Your mappings have been kept.'
    exit
}
if (!(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Skynet.exe'))) { throw 'Skynet.exe is missing. Run Build.cmd first.' }
New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
foreach ($name in @('Skynet.exe', 'TaskbarRecovery.exe', 'Skynet.exe.config', 'Skynet.ico', 'Microsoft.Web.WebView2.Core.dll', 'Microsoft.Web.WebView2.Wpf.dll', 'WebView2Loader.dll', 'Ical.Net.dll', 'NodaTime.dll', 'Portable.System.DateTimeOnly.dll', 'System.Memory.dll', 'System.Buffers.dll', 'System.Numerics.Vectors.dll', 'System.Runtime.CompilerServices.Unsafe.dll', 'README.md', 'PRIVACY.md', 'DESKTOP-SETUP.md', 'PC-SETUP.md', 'APPEARANCE.md', 'VOICE-SETUP.md', 'CHATGPT-SETUP.md', 'CALENDAR-SETUP.md', 'COMMUTE-SETUP.md', 'SCREEN-CONTROL.md', 'Configure-Screens.ps1', 'TEMPERATURE-ALERTS.md', 'THIRD-PARTY-NOTICES.md', 'Uninstall.cmd', 'Setup.ps1')) {
    $source = Join-Path $PSScriptRoot $name
    $destination = Join-Path $installRoot $name
    if ([IO.Path]::GetFullPath($source) -ne [IO.Path]::GetFullPath($destination)) {
        # Windows may briefly retain the image mapping after normal app shutdown.
        for($attempt=0;$attempt -lt 12;$attempt++) {
            try { Copy-Item -LiteralPath $source -Destination $destination -Force; break }
            catch [System.IO.IOException] { if($attempt -eq 11){throw};Start-Sleep -Milliseconds 250 }
        }
    }
}
if ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'Licenses')) -ne [IO.Path]::GetFullPath((Join-Path $installRoot 'Licenses'))) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Licenses') -Destination $installRoot -Recurse -Force }
$shell = New-Object -ComObject WScript.Shell
foreach ($link in @($desktopLink, $startLink)) {
    $shortcut = $shell.CreateShortcut($link)
    $shortcut.TargetPath = Join-Path $installRoot 'Skynet.exe'
    $shortcut.Arguments = ''
    $shortcut.WorkingDirectory = $installRoot
    $shortcut.Description = 'Customizable desktop voice assistant'
    $shortcut.IconLocation = (Join-Path $installRoot 'Skynet.ico') + ',0'
    $shortcut.Save()
}
foreach($link in $backgroundLinks) {
    $shortcut=$shell.CreateShortcut($link)
    $shortcut.TargetPath=Join-Path $installRoot 'Skynet.exe'
    $shortcut.Arguments='--desktop'
    $shortcut.WorkingDirectory=$installRoot
    $shortcut.Description='Interactive desktop background with voice, chat and system sensors'
    $shortcut.IconLocation=(Join-Path $installRoot 'Skynet.ico')+',0'
    $shortcut.Save()
}
# Retain the original settings location and encryption identity during the rename.
# Remove only the previous shortcuts that target this same installation.
$oldExecutable = Join-Path $installRoot 'JarvisDesk.exe'
foreach ($oldShortcutPath in @((Join-Path ([Environment]::GetFolderPath('Desktop')) 'JARVIS Desk.lnk'), (Join-Path ([Environment]::GetFolderPath('Programs')) 'JARVIS Desk.lnk'), (Join-Path ([Environment]::GetFolderPath('Desktop')) 'Skynet.lnk'), (Join-Path ([Environment]::GetFolderPath('Programs')) 'Skynet.lnk'))) {
    if (Test-Path -LiteralPath $oldShortcutPath) {
        $oldShortcut = $shell.CreateShortcut($oldShortcutPath)
        if ($oldShortcut.TargetPath -eq $oldExecutable -or $oldShortcut.TargetPath -eq (Join-Path $installRoot 'Skynet.exe')) { Remove-Item -LiteralPath $oldShortcutPath }
    }
}
foreach ($oldBinary in @('JarvisDesk.exe', 'JarvisDesk.ico')) {
    $oldBinaryPath = Join-Path $installRoot $oldBinary
    if (Test-Path -LiteralPath $oldBinaryPath) { Remove-Item -LiteralPath $oldBinaryPath }
}
Write-Host 'Desktop Assistant installed. Your app mappings and voice settings have been kept.'




