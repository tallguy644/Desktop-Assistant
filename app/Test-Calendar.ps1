param([ValidateSet('Calendar','TemperatureAlerts','Commute','Browser','NextUp','Screens','Identity','ChatNaming','Companion')][string]$Suite='Calendar')
$ErrorActionPreference='Stop'
$testDirectory=Join-Path ([IO.Path]::GetTempPath()) ('SkynetCalendarTests-'+[guid]::NewGuid().ToString('N'))
try {
 New-Item -ItemType Directory -Path $testDirectory | Out-Null
 foreach($name in @('Syra.exe','Syra.exe.config')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $testDirectory }
 Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.dll' | Copy-Item -Destination $testDirectory
 $testExecutable=Join-Path $testDirectory ($Suite+'Tests.exe')
 Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Syra.exe.config') -Destination ($testExecutable+'.config')
 $compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
 if(!(Test-Path -LiteralPath $compiler)) { $compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
 $framework=Split-Path $compiler
 $serializationReference='/reference:System.Runtime.Serialization.dll'
 & $compiler /nologo /target:exe $serializationReference ('/out:'+$testExecutable) ('/reference:'+(Join-Path $framework 'WPF\PresentationFramework.dll')) ('/reference:'+(Join-Path $framework 'WPF\PresentationCore.dll')) ('/reference:'+(Join-Path $framework 'WPF\WindowsBase.dll')) /reference:System.Xaml.dll /reference:System.Web.Extensions.dll ('/reference:'+(Join-Path $PSScriptRoot 'Microsoft.Web.WebView2.Core.dll')) ('/reference:'+(Join-Path $PSScriptRoot 'Microsoft.Web.WebView2.Wpf.dll')) ('/reference:'+(Join-Path $PSScriptRoot 'Syra.exe')) (Join-Path $PSScriptRoot ('Tests\'+$Suite+'Tests.cs'))
 if($LASTEXITCODE -ne 0) { throw 'Calendar test compilation failed.' }
 & $testExecutable $testDirectory
 if($LASTEXITCODE -ne 0) { throw 'Calendar checks failed.' }
} finally {
 $resolved=[IO.Path]::GetFullPath($testDirectory)
 $temporary=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
 if(!$resolved.StartsWith($temporary,[StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected temporary test path; leaving it untouched.' }
 for($attempt=0;$attempt -lt 10 -and (Test-Path -LiteralPath $resolved);$attempt++) {
  try { Remove-Item -LiteralPath $resolved -Recurse -Force } catch { if($attempt -eq 9) { throw };Start-Sleep -Milliseconds 500 }
 }
}

