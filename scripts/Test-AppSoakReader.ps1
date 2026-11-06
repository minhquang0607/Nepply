#requires -Version 7.4
# Checks Read-AppSoak.ps1 on a made-up run, and that Measure-AppSoak.ps1 refuses CI.
# Touches no process and no Desktop.
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$failures=[Collections.Generic.List[string]]::new()
function Check([string]$name,[bool]$ok) { if($ok) { Write-Host "PASS: $name" } else { Write-Host "FAIL: $name"; $failures.Add($name) } }

$run=Join-Path $projectRoot ('artifacts/app-soak-test/'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run | Out-Null
try {
    $started=[DateTimeOffset]::new(2026,11,6,0,0,0,[TimeSpan]::Zero)
    [ordered]@{ Started=$started.ToString('o'); IntervalSeconds=60; GuardianAtStart=$true; ProductVersion='test'; Windows='1.0' } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'run-start.json') -Encoding utf8
    # Three hours, one sample a minute. The UI grows 10 handles an hour; the guardian
    # restarts once and spends one CPU second a minute; one hour is lost to sleep.
    $lines=foreach($minute in 0..180) {
        if($minute -gt 100 -and $minute -lt 110) { continue }
        $guardianPid=if($minute -lt 90) { 200 } else { 201 }
        $guardianStart=if($minute -lt 90) { '2026-11-06T00:00:00Z' } else { '2026-11-06T01:30:00Z' }
        $guardianCpu=if($minute -lt 90) { $minute } else { $minute-90 }
        [ordered]@{ Seconds=60*$minute; Processes=@(
            [ordered]@{Role='ui';Pid=100;Started='2026-11-06T00:00:00Z';CpuSeconds=0;PrivateBytes=100MB;WorkingSet=1;Handles=700+[Math]::Floor($minute/6);Threads=20;Gdi=26;User=30}
            [ordered]@{Role='guardian';Pid=$guardianPid;Started=$guardianStart;CpuSeconds=$guardianCpu;PrivateBytes=10MB;WorkingSet=1;Handles=100;Threads=5;Gdi=0;User=0}
        ) } | ConvertTo-Json -Depth 4 -Compress
    }
    # The last record is cut off, as if the writer were still busy.
    Set-Content -LiteralPath (Join-Path $run 'samples.jsonl') -Value (($lines -join "`n")+"`n{`"Seconds`":99999") -Encoding utf8 -NoNewline
    Set-Content -LiteralPath (Join-Path $run 'scan-statistics.log') -Encoding utf8 -Value @(
        '[2026-11-05T23:00:00.0000000+00:00] INFO: Desktop scans in the last 60 min: 50 full, 0 partial, 0 failed; reading took 9.0 s.'
        '[2026-11-06T01:00:00.0000000+00:00] INFO: Desktop scans in the last 60 min: 12 full, 3 partial, 1 failed; reading took 2.5 s.'
        '[2026-11-06T02:00:00.0000000+00:00] INFO: Desktop scans in the last 60 min: 12 full, 0 partial, 0 failed; reading took 2.0 s.')
    $result=& (Join-Path $PSScriptRoot 'Read-AppSoak.ps1') -ArtifactDirectory $run | ConvertFrom-Json
    $ui=$result.Roles | Where-Object Role -eq 'ui'
    $guardian=$result.Roles | Where-Object Role -eq 'guardian'
    Check 'A cut-off last line is skipped' ($result.Samples -eq 172)
    Check 'A pause longer than three intervals counts as a gap' ($result.LongGaps -eq 1)
    Check 'Growth shows as the change between first and last hour' ($ui.Metrics.Handles.Change -gt 15 -and $ui.Metrics.Handles.Change -lt 25)
    Check 'A flat metric shows no change' ($ui.Metrics.Gdi.Change -eq 0)
    Check 'A restarted process counts as two instances' ($guardian.Instances -eq 2)
    Check 'CPU skips the gap and the restart' ($guardian.CpuPercentOfOneCore -gt 1.4 -and $guardian.CpuPercentOfOneCore -lt 1.7)
    Check 'A role that never ran is reported absent' ((($result.Roles | Where-Object Role -eq 'sensor-host').Present) -eq $false)
    Check 'Scan lines from before the run are ignored' ($result.DesktopScans.HourlyReports -eq 2 -and $result.DesktopScans.Full -eq 24)
    Check 'Scan totals add up' ($result.DesktopScans.Partial -eq 3 -and $result.DesktopScans.Failed -eq 1 -and $result.DesktopScans.ReadingSeconds -eq 4.5)

    $env:CI='true'
    try { & (Join-Path $PSScriptRoot 'Measure-AppSoak.ps1') -Minutes 1 | Out-Null; Check 'The soak refuses to run in CI' $false }
    catch { Check 'The soak refuses to run in CI' ($_.Exception.Message -like '*does not run in CI*') }
    finally { Remove-Item Env:CI }
}
finally { Remove-Item -LiteralPath $run -Recurse -Force }
if($failures.Count) { throw "$($failures.Count) app soak reader check(s) failed." }
Write-Host 'App soak reader checks passed.'
