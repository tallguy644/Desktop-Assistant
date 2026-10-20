param([string]$MonitorFolder = (Join-Path $env:USERPROFILE 'Downloads\LibreHardwareMonitor'))
$ErrorActionPreference = 'Stop'
if (Get-Process -Name LibreHardwareMonitor -ErrorAction SilentlyContinue) {
    throw 'Fully exit Libre Hardware Monitor using File > Exit, then run this repair again. Minimizing it does not close it.'
}
$configPath = Join-Path $MonitorFolder 'LibreHardwareMonitor.config'
if (!(Test-Path -LiteralPath $configPath)) { throw "Monitor configuration not found: $configPath" }
$backupPath = $configPath + '.backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff')
Copy-Item -LiteralPath $configPath -Destination $backupPath
$xml = New-Object System.Xml.XmlDocument
$xml.PreserveWhitespace = $true
$xml.Load($configPath)
$settings = $xml.SelectSingleNode('/configuration/appSettings')
if ($null -eq $settings) { throw 'Unexpected monitor configuration format. No changes saved.' }
$changes = @{ listenerIp = '127.0.0.1'; listenerPort = '8085'; runWebServerMenuItem = 'true' }
foreach ($key in $changes.Keys) {
    $entry = $settings.SelectSingleNode("add[@key='$key']")
    if ($null -eq $entry) { $entry = $xml.CreateElement('add'); $entry.SetAttribute('key', $key); [void]$settings.AppendChild($entry) }
    $entry.SetAttribute('value', $changes[$key])
}
$tempPath = $configPath + '.jarvis-repair.tmp'
$xml.Save($tempPath)
[IO.File]::Replace($tempPath, $configPath, $backupPath)
Write-Host 'Repaired: localhost 127.0.0.1, port 8085, web server enabled.'
Write-Host "Backup: $backupPath"
Write-Host 'Now reopen LibreHardwareMonitor.exe using Run as administrator. Keep SKYNET open; it reconnects automatically.'
