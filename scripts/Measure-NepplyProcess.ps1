param(
    [Parameter(Mandatory)][int]$RootProcessId,
    [ValidateRange(60,86400)][int]$DurationSeconds=900,
    [ValidateRange(2,60)][int]$IntervalSeconds=10,
    [string]$OutputDirectory='artifacts/performance',
    [string]$Scenario='preview-visible-native-disabled'
)
$ErrorActionPreference='Stop'
$root=Get-Process -Id $RootProcessId
$startIdentity=$root.StartTime.ToUniversalTime()
$output=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$clock=[Diagnostics.Stopwatch]::StartNew()
$rows=[Collections.Generic.List[object]]::new()
$seen=@{}
while($clock.Elapsed.TotalSeconds -lt $DurationSeconds) {
    $root=Get-Process -Id $RootProcessId -ErrorAction SilentlyContinue
    if(-not $root -or $root.StartTime.ToUniversalTime() -ne $startIdentity) { break }
    $inventory=@(Get-CimInstance Win32_Process -Property ProcessId,ParentProcessId)
    $ids=[Collections.Generic.HashSet[int]]::new(); [void]$ids.Add($RootProcessId)
    do {
        $added=$false
        foreach($entry in $inventory) {
            if($ids.Contains([int]$entry.ParentProcessId) -and $ids.Add([int]$entry.ProcessId)) { $added=$true }
        }
    } while($added)
    $private=0L; $working=0L; $handles=0; $threads=0; $cpuDelta=0.0; $count=0
    foreach($processId in $ids) {
        $process=Get-Process -Id $processId -ErrorAction SilentlyContinue
        if(-not $process) { continue }
        try {
            $key="$processId/$($process.StartTime.ToUniversalTime().Ticks)"
            $cpu=$process.TotalProcessorTime.TotalSeconds
            if($seen.ContainsKey($key)) { $cpuDelta += [Math]::Max(0,$cpu-$seen[$key]) }
            $seen[$key]=$cpu
            $private += $process.PrivateMemorySize64; $working += $process.WorkingSet64
            $handles += $process.HandleCount; $threads += $process.Threads.Count; $count++
        } finally { $process.Dispose() }
    }
    $seconds=$clock.Elapsed.TotalSeconds
    $elapsed=if($rows.Count) {$seconds-$rows[$rows.Count-1].Seconds} else {0}
    $row=[pscustomobject]@{Seconds=[Math]::Round($seconds,3);Processes=$count;PrivateMiB=$private/1MB;WorkingSetMiB=$working/1MB;Handles=$handles;Threads=$threads;CpuPercent=if($elapsed -gt 0){100*$cpuDelta/$elapsed/[Environment]::ProcessorCount}else{$null}}
    $rows.Add($row)
    $row | Export-Csv -LiteralPath (Join-Path $output 'samples.csv') -Append -NoTypeInformation
    Start-Sleep -Seconds $IntervalSeconds
}
$steady=@($rows | Where-Object Seconds -ge 60)
$summary=[ordered]@{Scenario=$Scenario;RequestedSeconds=$DurationSeconds;ObservedSeconds=$clock.Elapsed.TotalSeconds;Samples=$rows.Count;CompletedDuration=($clock.Elapsed.TotalSeconds -ge $DurationSeconds);NativeSensorClaim=$false;Note='Read-only sampled process tree; short-lived helpers between samples may be missed. Working sets may double-count shared pages. This is not a memory-leak verdict or a native telemetry benchmark.'}
if($steady.Count -gt 1) {
    $summary.PrivateMiBStart=$steady[0].PrivateMiB; $summary.PrivateMiBEnd=$steady[-1].PrivateMiB
    $summary.PrivateMiBPeak=($steady.PrivateMiB | Measure-Object -Maximum).Maximum
    $summary.HandlesStart=$steady[0].Handles; $summary.HandlesEnd=$steady[-1].Handles
    $summary.MeanCpuPercent=($steady.CpuPercent | Measure-Object -Average).Average
}
$summary | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'summary.json') -Encoding utf8
$summary
