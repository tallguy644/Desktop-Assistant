param([switch]$Restore)
$ErrorActionPreference='Stop'
if(Get-Process -Name Syra,Skynet -ErrorAction SilentlyContinue) { throw 'Close Syra before configuring or restoring screen settings.' }
$dataDirectory=Join-Path $env:LOCALAPPDATA 'JarvisDesk'
New-Item -ItemType Directory -Path $dataDirectory -Force | Out-Null
$backupPath=Join-Path $dataDirectory 'screen-windows-backup.json'
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class SkynetPowerPlan {
 [DllImport("powrprof.dll")] static extern uint PowerGetActiveScheme(IntPtr root,out IntPtr scheme);
 [DllImport("powrprof.dll")] static extern uint PowerReadACValueIndex(IntPtr root,ref Guid scheme,ref Guid subgroup,ref Guid setting,out uint value);
 [DllImport("powrprof.dll")] static extern uint PowerReadDCValueIndex(IntPtr root,ref Guid scheme,ref Guid subgroup,ref Guid setting,out uint value);
 [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr memory);
 public static Guid Active() { IntPtr scheme;if(PowerGetActiveScheme(IntPtr.Zero,out scheme)!=0) throw new Exception("Could not read the current power plan.");try { return (Guid)Marshal.PtrToStructure(scheme,typeof(Guid)); } finally { LocalFree(scheme); } }
 public static uint Display(Guid plan,bool ac) { var group=new Guid("7516b95f-f776-4464-8c53-06167f40cc99");var setting=new Guid("3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e");uint value;uint code=ac?PowerReadACValueIndex(IntPtr.Zero,ref plan,ref group,ref setting,out value):PowerReadDCValueIndex(IntPtr.Zero,ref plan,ref group,ref setting,out value);if(code!=0) throw new Exception("Could not read the display timeout.");return value; }
}
'@
function Set-DisplayTimeouts([Guid]$Plan,[uint32]$AC,[uint32]$DC) {
 & powercfg.exe /setacvalueindex $Plan.ToString() SUB_VIDEO VIDEOIDLE $AC
 if($LASTEXITCODE -ne 0) { throw 'Windows could not save the plugged-in display timeout.' }
 & powercfg.exe /setdcvalueindex $Plan.ToString() SUB_VIDEO VIDEOIDLE $DC
 if($LASTEXITCODE -ne 0) { throw 'Windows could not save the battery display timeout.' }
 if([SkynetPowerPlan]::Active() -eq $Plan) { & powercfg.exe /setactive $Plan.ToString();if($LASTEXITCODE -ne 0) { throw 'Windows could not apply the display timeout.' } }
 if([SkynetPowerPlan]::Display($Plan,$true) -ne $AC -or [SkynetPowerPlan]::Display($Plan,$false) -ne $DC) { throw 'Display timeout verification failed.' }
}
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'Syra.exe'))
$type=$assembly.GetType('JarvisDesk.ScreenPreferences')
$settingsPath=Join-Path $dataDirectory 'screens.json'
$settings=$type.GetMethod('Load').Invoke($null,[object[]]@([string]$settingsPath))
if($Restore) {
 if(!(Test-Path -LiteralPath $backupPath)) { throw 'No saved screen-timeout backup was found.' }
 $backup=Get-Content -LiteralPath $backupPath -Raw | ConvertFrom-Json
 Set-DisplayTimeouts ([Guid]$backup.Plan) ([uint32]$backup.AC) ([uint32]$backup.DC)
 $settings.Enabled=$false;$settings.Save($settingsPath)
 Write-Output 'Restored the previous Windows display timeouts and disabled Syra voice screen control. Manual sign-in settings are unchanged.'
 exit
}
$plan=[SkynetPowerPlan]::Active()
if(!(Test-Path -LiteralPath $backupPath)) {
 @{ Plan=$plan.ToString();AC=[SkynetPowerPlan]::Display($plan,$true);DC=[SkynetPowerPlan]::Display($plan,$false) } | ConvertTo-Json | Set-Content -LiteralPath $backupPath -Encoding UTF8
}
Set-DisplayTimeouts $plan 600 600
$settings.Enabled=$true;$settings.TimeoutMinutes=10;$settings.Save($settingsPath)
Write-Output 'PASS Windows display timeout is 600 seconds on AC and battery. Syra voice screen control is enabled at 10 minutes.'
Write-Output 'Previous display timeouts were backed up. Password, PIN, Windows Hello, manual lock, and computer sleep timeouts were not changed.'
