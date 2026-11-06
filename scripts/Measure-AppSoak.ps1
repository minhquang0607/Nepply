#requires -Version 7.4
<#
Whole-app soak (audit F-01, F-08). Watches a Nepply that the user started and set up
by hand, normally with Desktop icons hidden, and samples every process in its tree.
It only reads: it never hides or shows icons, never starts or stops Nepply, and
never changes settings. Keep the terminal and the machine running until it ends.

Results go to artifacts/app-soak/<time>/. Read them, also while the run is going,
with scripts/Read-AppSoak.ps1.
#>
param(
    [ValidateRange(1,2880)][int]$Minutes=1440,
    [ValidateRange(5,600)][int]$IntervalSeconds=30,
    [int]$ProcessId
)
$ErrorActionPreference='Stop'
if($env:CI -or $env:GITHUB_ACTIONS) { throw 'The app soak needs a real Desktop session; it does not run in CI.' }
$projectRoot=Split-Path $PSScriptRoot -Parent

Add-Type -Namespace NepplySoak -Name Native -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern uint GetGuiResources(System.IntPtr process, uint flags);
'@

function Get-Role([string]$name,[string]$commandLine) {
    if($name -like 'Nepply.DesktopGuardian*') { return 'guardian' }
    if($commandLine -match '(^|\s)"?--desktop-host') { return 'desktop-host' }
    if($commandLine -match '(^|\s)"?--sensor-host') { return 'sensor-host' }
    if($name -eq 'Nepply.exe') { return 'ui' }
    return 'other'
}

$inventory=@(Get-CimInstance Win32_Process -Property ProcessId,ParentProcessId,Name,CommandLine,ExecutablePath)
if(-not $ProcessId) {
    $candidates=@($inventory | Where-Object { $_.Name -eq 'Nepply.exe' -and (Get-Role $_.Name $_.CommandLine) -eq 'ui' })
    if($candidates.Count -ne 1) { throw "Found $($candidates.Count) Nepply windows; start exactly one, or pass -ProcessId." }
    $ProcessId=[int]$candidates[0].ProcessId
}
$rootEntry=$inventory | Where-Object ProcessId -eq $ProcessId
if(-not $rootEntry -or (Get-Role $rootEntry.Name $rootEntry.CommandLine) -ne 'ui') { throw "Process $ProcessId is not the Nepply app." }
$root=Get-Process -Id $ProcessId
$rootStart=$root.StartTime.ToUniversalTime()
$executable=$rootEntry.ExecutablePath
$guardianAtStart=@($inventory | Where-Object { (Get-Role $_.Name $_.CommandLine) -eq 'guardian' }).Count -gt 0

$started=[DateTimeOffset]::Now
$runRoot=Join-Path $projectRoot ('artifacts/app-soak/'+$started.ToString('yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $runRoot -ErrorAction Stop | Out-Null
$version=(Get-Item -LiteralPath $executable).VersionInfo
$build=Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
[ordered]@{
    Started=$started.ToString('o'); RequestedMinutes=$Minutes; IntervalSeconds=$IntervalSeconds
    RootProcessId=$ProcessId; RootStartedUtc=$rootStart.ToString('o')
    Executable=$executable; ProductVersion=$version.ProductVersion
    Sha256=(Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash
    Windows="$($build.CurrentBuildNumber).$($build.UBR)"
    GuardianAtStart=$guardianAtStart
    Note='GuardianAtStart is how the Setup edition shows that icons are hidden; the Store edition has no guardian.'
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runRoot 'run-start.json') -Encoding utf8
Write-Host "Results: $runRoot"
if(-not $guardianAtStart) { Write-Warning 'No guardian is running, so Desktop icons are probably not hidden. The audit asks for a run with icons hidden.' }
Write-Host 'Read-only. Keep this terminal and the machine running; do not close or update Nepply.'

$samplesPath=Join-Path $runRoot 'samples.jsonl'
$seen=@{}
$clock=[Diagnostics.Stopwatch]::StartNew()
$endReason='Duration'
while($clock.Elapsed.TotalMinutes -lt $Minutes) {
    $root=Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if(-not $root -or $root.StartTime.ToUniversalTime() -ne $rootStart) { $endReason='AppExited'; break }
    $root.Dispose()
    $inventory=@(Get-CimInstance Win32_Process -Property ProcessId,ParentProcessId,Name,CommandLine)
    $ids=[Collections.Generic.HashSet[int]]::new(); [void]$ids.Add($ProcessId)
    do {
        $added=$false
        foreach($entry in $inventory) { if($ids.Contains([int]$entry.ParentProcessId) -and $ids.Add([int]$entry.ProcessId)) { $added=$true } }
    } while($added)
    # The guardian may be started detached; include it by name as well.
    foreach($entry in $inventory) { if((Get-Role $entry.Name $entry.CommandLine) -eq 'guardian') { [void]$ids.Add([int]$entry.ProcessId) } }
    $processes=foreach($id in $ids) {
        $entry=$inventory | Where-Object ProcessId -eq $id
        $process=Get-Process -Id $id -ErrorAction SilentlyContinue
        if(-not $process -or -not $entry) { continue }
        try {
            $role=Get-Role $entry.Name $entry.CommandLine
            $key="$id/$($process.StartTime.ToUniversalTime().Ticks)"
            $seen[$key]=$role
            [ordered]@{
                Role=$role; Pid=$id; Started=$process.StartTime.ToUniversalTime().ToString('o')
                CpuSeconds=[Math]::Round($process.TotalProcessorTime.TotalSeconds,3)
                PrivateBytes=$process.PrivateMemorySize64; WorkingSet=$process.WorkingSet64
                Handles=$process.HandleCount; Threads=$process.Threads.Count
                Gdi=[NepplySoak.Native]::GetGuiResources($process.Handle,0); User=[NepplySoak.Native]::GetGuiResources($process.Handle,1)
            }
        } catch { } finally { $process.Dispose() }
    }
    $line=[ordered]@{ Seconds=[Math]::Round($clock.Elapsed.TotalSeconds,1); Time=[DateTimeOffset]::Now.ToString('o'); Processes=@($processes) } |
        ConvertTo-Json -Depth 4 -Compress
    Add-Content -LiteralPath $samplesPath -Value $line -Encoding utf8
    Start-Sleep -Seconds $IntervalSeconds
}

$logLines=@()
$logDirectory=Join-Path $env:LOCALAPPDATA 'Nepply\Logs'
if(Test-Path -LiteralPath $logDirectory) {
    # Rotated copies keep the same prefix. Keep only lines written during this run.
    $logLines=@(Get-ChildItem -LiteralPath $logDirectory -Filter 'nepply-ui.log*' -File | ForEach-Object {
        Get-Content -LiteralPath $_.FullName | Where-Object {
            $_ -match '^\[(?<time>[^\]]+)\] INFO: Desktop scans in the last' -and
                [DateTimeOffset]::Parse($Matches.time,[Globalization.CultureInfo]::InvariantCulture) -ge $started
        }
    } | Sort-Object)
}
Set-Content -LiteralPath (Join-Path $runRoot 'scan-statistics.log') -Value $logLines -Encoding utf8
[ordered]@{
    Started=$started.ToString('o'); Ended=[DateTimeOffset]::Now.ToString('o'); EndReason=$endReason
    ObservedMinutes=[Math]::Round($clock.Elapsed.TotalMinutes,1)
    ProcessesSeen=@($seen.Values | Group-Object | ForEach-Object { [ordered]@{Role=$_.Name; Count=$_.Count} })
    Note='Samples miss helpers that live shorter than the interval; count scans from scan-statistics.log. This file does not decide whether anything leaks.'
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $runRoot 'run-result.json') -Encoding utf8
& (Join-Path $PSScriptRoot 'Read-AppSoak.ps1') -ArtifactDirectory $runRoot
if($endReason -ne 'Duration') { Write-Warning "Stopped early: $endReason."; exit 1 }
exit 0
