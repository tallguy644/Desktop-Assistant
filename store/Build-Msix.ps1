param(
 [string]$IdentityName,[string]$Publisher,[string]$PublisherDisplayName,
 [string]$DisplayName='Desktop Assistant',[string]$Version='1.0.0.0',
 [string]$MakeAppxPath,[switch]$DevelopmentPreview
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
if($DevelopmentPreview){$IdentityName='DesktopAssistant.DevelopmentPreview';$Publisher='CN=DesktopAssistant-Development';$PublisherDisplayName='Development preview';$DisplayName='Desktop Assistant Preview'}
if(!$IdentityName -or !$Publisher -or !$PublisherDisplayName){throw 'Supply the exact Identity Name, Publisher and PublisherDisplayName from Partner Center, or explicitly select -DevelopmentPreview.'}
if($Version -notmatch '^\d+\.\d+\.\d+\.0$'){throw 'Use a four-part Store version ending in .0, such as 1.0.0.0.'}
if(!$MakeAppxPath){$tool=Get-Command makeappx.exe -ErrorAction SilentlyContinue;if($tool){$MakeAppxPath=$tool.Source}}
if(!$MakeAppxPath -or !(Test-Path -LiteralPath $MakeAppxPath)){throw 'Pass -MakeAppxPath pointing to the Windows SDK x64 makeappx.exe. See SUBMISSION.md.'}
$dist=Join-Path $root 'dist';New-Item -ItemType Directory -Path $dist -Force | Out-Null
$payload=Join-Path $dist ('msix-payload-'+[guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Path (Join-Path $payload 'app'),(Join-Path $payload 'Assets') -Force | Out-Null
# Explicit whitelist: no personal profiles, setup scripts, history, tests or source files in MSIX.
$names=@('Skynet.exe','TaskbarRecovery.exe','Skynet.exe.config','Skynet.ico','Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.Wpf.dll','WebView2Loader.dll','Ical.Net.dll','NodaTime.dll','Portable.System.DateTimeOnly.dll','System.Memory.dll','System.Buffers.dll','System.Numerics.Vectors.dll','System.Runtime.CompilerServices.Unsafe.dll')
foreach($name in $names){Copy-Item -LiteralPath (Join-Path $root "app\$name") -Destination (Join-Path $payload 'app')}
Copy-Item -LiteralPath (Join-Path $root 'app\Licenses') -Destination (Join-Path $payload 'app') -Recurse
Get-ChildItem -LiteralPath (Join-Path $root 'app') -Filter '*.md' | Copy-Item -Destination (Join-Path $payload 'app')
Copy-Item -LiteralPath (Join-Path $root 'PRIVACY.md') -Destination (Join-Path $payload 'app')
Add-Type -AssemblyName System.Drawing
$image=[Drawing.Image]::FromFile((Join-Path $root 'app\Skynet-icon.png'))
try {foreach($entry in @(@('StoreLogo',50),@('Square44x44Logo',44),@('Square150x150Logo',150))){$bitmap=New-Object Drawing.Bitmap([int]$entry[1]),([int]$entry[1]);$graphics=[Drawing.Graphics]::FromImage($bitmap);try{$graphics.Clear([Drawing.Color]::Transparent);$graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic;$graphics.DrawImage($image,0,0,$bitmap.Width,$bitmap.Height);$bitmap.Save((Join-Path $payload ('Assets\'+$entry[0]+'.png')),[Drawing.Imaging.ImageFormat]::Png)}finally{$graphics.Dispose();$bitmap.Dispose()}}}finally{$image.Dispose()}
$manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Package.appxmanifest.template') -Raw
foreach($name in @('IdentityName','Publisher','PublisherDisplayName','DisplayName','Version')){$manifest=$manifest.Replace('{{'+$name+'}}',[Security.SecurityElement]::Escape((Get-Variable -Name $name -ValueOnly)))}
[IO.File]::WriteAllText((Join-Path $payload 'AppxManifest.xml'),$manifest,[Text.UTF8Encoding]::new($false))
$fileName=if($DevelopmentPreview){'Desktop-Assistant-PREVIEW-unsigned.msix'}else{'Desktop-Assistant-'+$Version+'-x64-unsigned.msix'}
$output=Join-Path $dist $fileName
& $MakeAppxPath pack /d $payload /p $output /o
if($LASTEXITCODE -ne 0){throw 'MakeAppx failed validation or packaging.'}
$kind=if($DevelopmentPreview){'DEVELOPMENT IDENTITY ONLY. Not a Store submission package.'}else{'Identity supplied by publisher. Submit only after certification checks.'}
@("$kind",'Unsigned: Store submission uses Microsoft signing; local installation requires a separately trusted test signature.',"Package: $fileName","SHA256: $((Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash)",'Packaged installation / activation still require signed testing on a clean PC.') | Set-Content -LiteralPath (Join-Path $dist 'PACKAGE-STATUS.txt')
Write-Output $output
