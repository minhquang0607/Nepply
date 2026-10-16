#requires -Version 7.4
param([Parameter(Mandatory)][string]$ArtifactDirectory,[ValidateRange(0,28800)][double]$WarmupSeconds=0)
$ErrorActionPreference = 'Stop'
$samplePath = Join-Path $ArtifactDirectory 'ui-soak-samples.jsonl'
$stream = [IO.File]::Open($samplePath,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
$reader = $null
try { $reader = [IO.StreamReader]::new($stream); $raw = $reader.ReadToEnd() }
finally { if ($null -ne $reader) { $reader.Dispose() } else { $stream.Dispose() } }
# A live writer may not have finished its last line. Never parse a partial record.
$lastNewline = $raw.LastIndexOf("`n")
if ($lastNewline -lt 0) { throw 'Chưa có mẫu hoàn chỉnh.' }
$samples = @($raw.Substring(0,$lastNewline).Split("`n") | Where-Object { $_.Trim().Length -gt 0 } | ForEach-Object { $_ | ConvertFrom-Json })
$intervals = [Collections.Generic.List[object]]::new()
for ($i=1; $i -lt $samples.Count; $i++) {
    $previous = $samples[$i-1]
    $current = $samples[$i]
    $seconds = [double]$current.Seconds - [double]$previous.Seconds
    $cpu = [double]$current.CpuSeconds - [double]$previous.CpuSeconds
    if ($seconds -le 0 -or $cpu -lt 0) { throw "Mốc thời gian/CPU không đơn điệu ở mẫu $i." }
    $intervals.Add([pscustomobject]@{
        Seconds=$seconds; CpuSeconds=$cpu; Active=[bool]$current.Active
        StablePhase=($current.Active -eq $previous.Active)
        LongGap=($seconds -gt 90)
        AfterWarmup=([double]$previous.Seconds -ge $WarmupSeconds)
    })
}
$phases = foreach ($active in @($true,$false)) {
    # Exclude phase boundaries and long gaps rather than diluting CPU averages.
    $group = @($intervals | Where-Object { $_.AfterWarmup -and $_.StablePhase -and -not $_.LongGap -and $_.Active -eq $active })
    $seconds = ($group | Measure-Object Seconds -Sum).Sum
    $cpu = ($group | Measure-Object CpuSeconds -Sum).Sum
    [pscustomobject]@{ Active=$active; StableIntervals=$group.Count; ObservedSeconds=$seconds
        CpuPercentOfOneCore=$(if ($seconds -gt 0) { [Math]::Round(100*$cpu/$seconds,3) } else { $null }) }
}
$summaryPath = Join-Path $ArtifactDirectory 'ui-soak-summary.json'
$summary = if (Test-Path -LiteralPath $summaryPath) { Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json } else { $null }
$ranges = foreach ($metric in @('ManagedBytes','PrivateMemorySize64','HandleCount','Threads','Gdi','User')) {
    $range = $samples | Measure-Object -Property $metric -Minimum -Maximum
    [pscustomobject]@{Metric=$metric; First=$samples[0].$metric; Last=$samples[-1].$metric; Minimum=$range.Minimum; Maximum=$range.Maximum}
}
[pscustomobject]@{
    Samples=$samples.Count; LastSampleSeconds=$samples[-1].Seconds
    WarmupSeconds=$WarmupSeconds; Profile=$summary.Profile
    SummaryPresent=($null -ne $summary); Completed=$summary.Completed
    LongGaps=@($intervals | Where-Object LongGap).Count
    PhaseCpu=$phases; Ranges=$ranges
    Note='Chỉ phân tích log; không xác nhận tiến trình còn sống, không tự kết luận leak/pass. CPU tính theo một core; bỏ boundary và khoảng cách mẫu trên 90 giây. Cần xác minh PID/session riêng.'
} | ConvertTo-Json -Depth 5
