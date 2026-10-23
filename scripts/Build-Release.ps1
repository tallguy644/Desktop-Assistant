$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
Push-Location (Join-Path $root 'app')
try{& .\Build.cmd;if($LASTEXITCODE -ne 0){throw 'Application build failed.'}}finally{Pop-Location}
$dist=Join-Path $root 'dist';New-Item -ItemType Directory -Path $dist -Force | Out-Null
$staging=Join-Path $dist ('installer-'+[guid]::NewGuid().ToString('N'));$target=Join-Path $staging 'Syra';New-Item -ItemType Directory -Path $target -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $root 'app') -File | Where-Object {$_.Extension -in '.exe','.dll','.config','.ico','.cmd','.ps1','.md'} | Copy-Item -Destination $target
Copy-Item -LiteralPath (Join-Path $root 'app\Licenses') -Destination $target -Recurse
Copy-Item -LiteralPath (Join-Path $root 'PRIVACY.md') -Destination $target
$zip=Join-Path $dist 'Desktop-Assistant-Windows.zip';Compress-Archive -LiteralPath $target -DestinationPath $zip -Force
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /platform:anycpu /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll ('/win32icon:'+(Join-Path $root 'app\Syra.ico')) ('/resource:'+$zip+',payload.zip') ('/out:'+(Join-Path $dist 'Desktop-Assistant-Setup.exe')) (Join-Path $PSScriptRoot 'DesktopInstaller.cs')
if($LASTEXITCODE -ne 0){throw 'Installer build failed.'}
Get-ChildItem -LiteralPath $dist -File | Get-FileHash -Algorithm SHA256 | Format-Table -AutoSize
