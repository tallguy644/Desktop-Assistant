$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
Push-Location (Join-Path $root 'app')
try{& .\Build.cmd;if($LASTEXITCODE -ne 0){throw 'Application build failed.'}}finally{Pop-Location}
$dist=Join-Path $root 'dist';New-Item -ItemType Directory -Path $dist -Force | Out-Null
$staging=Join-Path $dist ('installer-'+[guid]::NewGuid().ToString('N'));$target=Join-Path $staging 'Skynet';New-Item -ItemType Directory -Path $target -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $root 'app') -File | Where-Object {$_.Extension -in '.exe','.dll','.config','.ico','.cmd','.ps1','.md'} | Copy-Item -Destination $target
Copy-Item -LiteralPath (Join-Path $root 'app\Licenses') -Destination $target -Recurse
Copy-Item -LiteralPath (Join-Path $root 'PRIVACY.md') -Destination $target
$zip=Join-Path $dist 'Syra-Windows.zip';if(Test-Path -LiteralPath $zip){Remove-Item -LiteralPath $zip}
# Write entries with forward slashes so the ZIP also extracts correctly outside Windows (Compress-Archive in PowerShell 5.1 writes backslashes).
Add-Type -AssemblyName System.IO.Compression,System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try{Get-ChildItem -LiteralPath $target -Recurse -File | ForEach-Object {$entry='Skynet/'+$_.FullName.Substring($target.Length+1).Replace('\','/');[void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$_.FullName,$entry,[IO.Compression.CompressionLevel]::Optimal)}}finally{$archive.Dispose()}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /platform:anycpu /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll ('/win32icon:'+(Join-Path $root 'app\Skynet.ico')) ('/resource:'+$zip+',payload.zip') ('/out:'+(Join-Path $dist 'Syra-Setup.exe')) (Join-Path $PSScriptRoot 'DesktopInstaller.cs')
if($LASTEXITCODE -ne 0){throw 'Installer build failed.'}
Get-ChildItem -LiteralPath $dist -File | Get-FileHash -Algorithm SHA256 | Format-Table -AutoSize
