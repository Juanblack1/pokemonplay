[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$CheckPath,
    [Parameter(Mandatory)][string]$FixtureRoot,
    [Parameter(Mandatory)][switch]$OwnedCiDesktop
)
$ErrorActionPreference = 'Stop'
if (-not $OwnedCiDesktop -or $env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_OS -ne 'Windows' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') {
    throw 'Display preparation requires an explicitly owned GitHub-hosted Windows CI desktop.'
}
$CheckPath = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $CheckPath).Path)
$FixtureRoot = [IO.Path]::GetFullPath($FixtureRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
$evidence = $FixtureRoot + '-display'
$null = New-Item -ItemType Directory -Path $evidence -Force
function Write-Evidence([string]$name, $value) {
    ConvertTo-Json -InputObject $value -Depth 10 | Set-Content -LiteralPath (Join-Path $evidence $name) -Encoding utf8
}
# Supported APIs, dynamic flags 0 only: no registry/global/unsafe flags or DPI overrides.
# https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-enumdisplaysettingsw
# https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-changedisplaysettingsexw
Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class CiOwnedDisplay {
 [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] public struct Device {
  public uint cb;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string DeviceName;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string DeviceString;
  public uint StateFlags;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string DeviceID;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string DeviceKey;
 }
 [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] public struct Mode {
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string dmDeviceName;
  public ushort dmSpecVersion,dmDriverVersion,dmSize,dmDriverExtra;
  public uint dmFields;
  public int dmPositionX,dmPositionY;
  public uint dmDisplayOrientation,dmDisplayFixedOutput;
  public short dmColor,dmDuplex,dmYResolution,dmTTOption,dmCollate;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string dmFormName;
  public ushort dmLogPixels;
  public uint dmBitsPerPel,dmPelsWidth,dmPelsHeight,dmDisplayFlags,dmDisplayFrequency;
  public uint dmICMMethod,dmICMIntent,dmMediaType,dmDitherType,dmReserved1,dmReserved2,dmPanningWidth,dmPanningHeight;
 }
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
 [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool EnumDisplayDevices(string name,uint index,ref Device device,uint flags);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern bool EnumDisplaySettings(string device,uint index,ref Mode mode);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int ChangeDisplaySettingsEx(string device,ref Mode mode,IntPtr window,uint flags,IntPtr parameter);
 [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr window,out Rect rectangle);
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window,out Rect rectangle);
 public static Device NewDevice() => new Device {cb=(uint)Marshal.SizeOf<Device>()};
 public static Mode NewMode() => new Mode {dmSize=(ushort)Marshal.SizeOf<Mode>(),dmDriverExtra=0};
 public static bool EnumPrimary(uint index,ref Device device,out int immediateError) {
  // PowerShell converts $null to an empty string for .NET string arguments.
  // Keep the Win32 NULL device name inside C#, where it remains a true NULL.
  Marshal.SetLastPInvokeError(0);
  bool result=EnumDisplayDevices(null,index,ref device,0);
  immediateError=Marshal.GetLastPInvokeError();return result;
 }
 public static Mode Candidate(Mode original,Mode available) {
  original.dmBitsPerPel=available.dmBitsPerPel;original.dmPelsWidth=available.dmPelsWidth;original.dmPelsHeight=available.dmPelsHeight;
  original.dmDisplayFrequency=available.dmDisplayFrequency;original.dmDisplayFlags=available.dmDisplayFlags;
  original.dmFields=0x007c0000; return original;
 }
}
'@
if ([Runtime.InteropServices.Marshal]::SizeOf([type][CiOwnedDisplay+Mode]) -ne 220) { throw 'Unexpected DEVMODEW ABI size.' }
if ([Runtime.InteropServices.Marshal]::SizeOf([type][CiOwnedDisplay+Device]) -ne 840) { throw 'Unexpected DISPLAY_DEVICEW ABI size.' }
function Current-Mode([string]$device) {
    $mode = [CiOwnedDisplay]::NewMode()
    if (-not [CiOwnedDisplay]::EnumDisplaySettings($device, [uint32]::MaxValue, [ref]$mode)) { throw 'Cannot capture current display mode.' }
    return $mode
}
function Metrics([string]$device) {
    $mode = Current-Mode $device
    $track = [Windows.Forms.SystemInformation]::MaxWindowTrackSize
    return [ordered]@{mode=$mode;maxTrackWidth=$track.Width;maxTrackHeight=$track.Height;virtualScreen=[Windows.Forms.SystemInformation]::VirtualScreen}
}
function Same-Mode($left,$right) {
    foreach ($name in @('dmPelsWidth','dmPelsHeight','dmBitsPerPel','dmDisplayFrequency','dmDisplayFlags','dmDisplayOrientation','dmDisplayFixedOutput','dmPositionX','dmPositionY')) {
        if ($left.$name -ne $right.$name) { return $false }
    }
    return $true
}
$primary = $null
$deviceResults = @()
for ($index=0; $index -lt 64; $index++) {
    $device = [CiOwnedDisplay]::NewDevice()
    $immediateError = 0
    $enumerated = [CiOwnedDisplay]::EnumPrimary($index,[ref]$device,[ref]$immediateError)
    $deviceResults += @{index=$index;result=$enumerated;immediateError=$immediateError;device=$device}
    Write-Evidence 'display-devices.json' $deviceResults
    if (-not $enumerated) { break }
    if (($device.StateFlags -band 5) -eq 5) { $primary=$device;break }
}
if (-not $primary) { Write-Evidence 'preflight-failed.json' @{reason='Native enumeration did not yield an attached primary display; inspect display-devices.json';status='failed'};throw 'Native display enumeration preflight failed.' }
$deviceName = $primary.DeviceName
$original = Current-Mode $deviceName
if ($original.dmDriverExtra -ne 0) { throw 'Unsupported private display driver data; refuse incomplete DEVMODE restoration.' }
$before = Metrics $deviceName
Write-Evidence 'original.json' @{device=$primary;mode=$original;metrics=$before}
$available = @()
for ($index=0; $index -lt 4096; $index++) {
    $mode = [CiOwnedDisplay]::NewMode()
    if (-not [CiOwnedDisplay]::EnumDisplaySettings($deviceName,$index,[ref]$mode)) { break }
    $available += $mode
}
if ($index -eq 4096) { throw 'Display mode enumeration exceeded bound.' }
Write-Evidence 'available-modes.json' $available
$tests=@();$restoreNeeded=$false;$child=$null;$childIdentity=$null;$failure=$null
$stdout=$FixtureRoot+'.stdout.log';$stderr=$FixtureRoot+'.stderr.log'
Write-Evidence 'candidate-tests.json' $tests
try {
    if ($before.maxTrackWidth -lt 1280 -or $before.maxTrackHeight -lt 900 -or $original.dmPelsWidth -lt 1280 -or $original.dmPelsHeight -lt 900) {
        $candidates=@($available | Where-Object {$_.dmPelsWidth -ge 1280 -and $_.dmPelsHeight -ge 900 -and $_.dmBitsPerPel -eq 32} | Sort-Object @{Expression={[long]$_.dmPelsWidth*$_.dmPelsHeight}},@{Expression={if($_.dmDisplayFrequency -eq $original.dmDisplayFrequency){0}else{1}}},dmDisplayFrequency)
        $selected=$null
        foreach ($mode in $candidates) {
            if ($tests.Count -ge 32) { throw 'Environmental preflight failed: supported-mode test bound exceeded.' }
            $candidate=[CiOwnedDisplay]::Candidate($original,$mode)
            $test=[CiOwnedDisplay]::ChangeDisplaySettingsEx($deviceName,[ref]$candidate,[IntPtr]::Zero,2,[IntPtr]::Zero)
            $tests+=@{mode=$candidate;cdsTest=$test}
            Write-Evidence 'candidate-tests.json' $tests
            if ($test -eq 0) {$selected=$candidate;break}
        }
        if (-not $selected) {throw 'Environmental preflight failed: no enumerated/tested supported mode meets 1280x900.'}
        # Mark before mutation: a failed apply return cannot skip restoration.
        $restoreNeeded=$true
        $result=[CiOwnedDisplay]::ChangeDisplaySettingsEx($deviceName,[ref]$selected,[IntPtr]::Zero,0,[IntPtr]::Zero)
        Write-Evidence 'apply.json' @{selected=$selected;result=$result;flags=0}
        if ($result -ne 0) {throw "Dynamic display change failed ($result); restart is not success."}
    } else {Write-Evidence 'apply.json' @{changed=$false;reason='Original mode already sufficient'}}
    $deadline=[Diagnostics.Stopwatch]::StartNew()
    do {
        $actual=Metrics $deviceName
        Write-Evidence 'actual-preflight.json' $actual
        if ($actual.maxTrackWidth -ge 1280 -and $actual.maxTrackHeight -ge 900 -and $actual.mode.dmPelsWidth -ge 1280 -and $actual.mode.dmPelsHeight -ge 900) {break}
        if ($deadline.ElapsedMilliseconds -gt 10000) {throw 'Environmental preflight failed: actual display/tracking remained below 1280x900.'}
        Start-Sleep -Milliseconds 100
    } while ($true)
    # Real native client and outer bounds, not cached ClientSize or simulated max tracking.
    $container=[Windows.Forms.Panel]::new();$probe=[Windows.Forms.Form]::new()
    try {
        $probe.TopLevel=$false;$probe.FormBorderStyle='None';$container.Controls.Add($probe)
        $null=$container.Handle;$null=$probe.Handle
        $container.ClientSize=[Drawing.Size]::new(1280,900);$probe.ClientSize=[Drawing.Size]::new(1280,900)
        $client=[CiOwnedDisplay+Rect]::new();$outer=[CiOwnedDisplay+Rect]::new()
        $clientRead=[CiOwnedDisplay]::GetClientRect($probe.Handle,[ref]$client)
        $outerRead=[CiOwnedDisplay]::GetWindowRect($probe.Handle,[ref]$outer)
        Write-Evidence 'client-preflight.json' @{clientRead=$clientRead;outerRead=$outerRead;nativeClient=$client;nativeOuter=$outer;managedClient=$probe.ClientSize;scope='owned hidden embedded Form; exact native layout assertions unchanged'}
        if (-not $clientRead -or -not $outerRead -or $probe.ClientSize.Width -ne 1280 -or $probe.ClientSize.Height -ne 900 -or ($client.Right-$client.Left) -ne 1280 -or ($client.Bottom-$client.Top) -ne 900 -or ($outer.Right-$outer.Left) -ne 1280 -or ($outer.Bottom-$outer.Top) -ne 900) {throw 'Actual native embedded Form client/bounds preflight failed.'}
    } finally {$probe.Dispose();$container.Dispose()}
    $child=Start-Process -FilePath $CheckPath -ArgumentList @('--embedding-lifecycle',('"'+$FixtureRoot+'"')) -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    $childIdentity=@{pid=$child.Id;start=$child.StartTime.ToUniversalTime().Ticks;path=$CheckPath}
    if (-not $child.WaitForExit(120000)) {throw 'Lifecycle watchdog expired; owned emergency cleanup is a failure.'}
    Get-Content -LiteralPath $stdout;Get-Content -LiteralPath $stderr
    if ($child.ExitCode -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $FixtureRoot 'passed'))) {throw 'Lifecycle checks failed.'}
} catch {
    $failure=$_
    Write-Evidence 'failure.json' @{status='failed';reason=$_.Exception.Message}
} finally {
    try {
        foreach ($file in @(Get-ChildItem -LiteralPath $FixtureRoot -Filter 'parent-owned.json' -Recurse -ErrorAction SilentlyContinue)) {
            $identity=Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
            $owned=Get-Process -Id $identity.Pid -ErrorAction SilentlyContinue
            $path=[IO.Path]::GetFullPath($identity.Executable)
            if ($owned -and $path.StartsWith($FixtureRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -and $owned.StartTime.ToUniversalTime().Ticks -eq $identity.StartUtcTicks -and $owned.Path -eq $path) {
                $owned.Kill();$null=$owned.WaitForExit(5000)
                $failure=[Exception]::new('Owned emergency cleanup was required; test failed.')
            }
        }
    } catch {$failure=$_} finally {
        try {
            if ($child -and -not $child.HasExited) {
                if ($child.Id -eq $childIdentity.pid -and $child.StartTime.ToUniversalTime().Ticks -eq $childIdentity.start -and $child.Path -eq $childIdentity.path) {$child.Kill();$null=$child.WaitForExit(5000)}
                $failure=[Exception]::new('Lifecycle parent required emergency cleanup.')
            }
        } catch {$failure=$_} finally {
            try {
                $restoreCode=[CiOwnedDisplay]::ChangeDisplaySettingsEx($deviceName,[ref]$original,[IntPtr]::Zero,0,[IntPtr]::Zero)
                $clock=[Diagnostics.Stopwatch]::StartNew()
                do {
                    $restored=Metrics $deviceName
                    $restoredExact=(Same-Mode $restored.mode $original) -and $restored.maxTrackWidth -eq $before.maxTrackWidth -and $restored.maxTrackHeight -eq $before.maxTrackHeight
                    if ($restoredExact -or $clock.ElapsedMilliseconds -gt 10000) {break}
                    Start-Sleep -Milliseconds 100
                } while ($true)
                Write-Evidence 'restore.json' @{attempted=$true;applyAttempted=$restoreNeeded;result=$restoreCode;exact=$restoredExact;actual=$restored}
                if ($restoreCode -ne 0 -or -not $restoredExact) {throw 'Original CI display mode/metrics restoration failed.'}
            } catch {Write-Evidence 'restore-failed.json' @{reason=$_.Exception.Message};$failure=$_}
        }
    }
}
if ($failure) {throw $failure}
Write-Evidence 'wrapper-passed.json' @{status='passed';scope='supported real CI display mode, exact lifecycle/layout and exact original restoration'}
