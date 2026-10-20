$ErrorActionPreference = 'Stop'
$assemblyPath = Join-Path $PSScriptRoot 'Skynet.exe'
$null = [Reflection.Assembly]::LoadFrom($assemblyPath)
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('SkynetChatTests-' + [guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $testDirectory | Out-Null
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (!(Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
    $testLibrary = Join-Path $testDirectory 'ChatTests.dll'
    & $compiler /nologo /target:library ("/out:" + $testLibrary) ("/reference:" + $assemblyPath) /reference:System.Web.dll /reference:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'Tests\ChatTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Chat test compilation failed.' }
    $tests = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes($testLibrary))
    $tests.GetType('SkynetChatTests').GetMethod('Run').Invoke($null,[object[]]@([string]$testDirectory))
}
finally {
    $resolvedTest = [IO.Path]::GetFullPath($testDirectory)
    $resolvedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (!$resolvedTest.StartsWith($resolvedTemp,[StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected temporary test path; leaving it untouched.' }
    if (Test-Path -LiteralPath $resolvedTest) { Remove-Item -LiteralPath $resolvedTest -Recurse -Force }
}
