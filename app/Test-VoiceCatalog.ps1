param([string]$TestDirectory)
$ErrorActionPreference='Stop'
if(!$TestDirectory){$TestDirectory=Join-Path ([IO.Path]::GetTempPath()) ('DesktopVoiceCatalog-'+[guid]::NewGuid().ToString('N'))}
New-Item -ItemType Directory -Path $TestDirectory -Force | Out-Null
$app=Join-Path $PSScriptRoot 'Syra.exe'
$null=[Reflection.Assembly]::LoadFrom($app)
$library=Join-Path $TestDirectory 'VoiceCatalogTests.dll'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:library ('/out:'+$library) ('/reference:'+$app) /reference:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'Tests\AzureVoiceCatalogTests.cs')
if($LASTEXITCODE -ne 0){throw 'Voice catalog test compilation failed.'}
$tests=[Reflection.Assembly]::Load([IO.File]::ReadAllBytes($library))
$tests.GetType('AzureVoiceCatalogTests').GetMethod('Run').Invoke($null,[object[]]@([string]$TestDirectory))
