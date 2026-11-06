#requires -Version 7.4
<#
Summarizes a run of scripts/Measure-AppSoak.ps1, also while it is still going. It
compares the first and last hour after warm-up for each long-lived process and adds
up the hourly Desktop scan counts that Nepply writes to its log. It reports numbers
only; deciding whether anything leaks is left to the reader.
#>
param(
    [Parameter(Mandatory)][string]$ArtifactDirectory,
    [ValidateRange(0,240)][double]$WarmupMinutes=10,
    [string]$LogDirectory=(Join-Path $env:LOCALAPPDATA 'Nepply\Logs')
)
$ErrorActionPreference='Stop'
$start=Get-Content -LiteralPath (Join-Path $ArtifactDirectory 'run-start.json') -Raw | ConvertFrom-Json
$stream=[IO.File]::Open((Join-Path $ArtifactDirectory 'samples.jsonl'),[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
try { $raw=[IO.StreamReader]::new($stream).ReadToEnd() } finally { $stream.Dispose() }
# A live writer may not have finished its last line. Never parse a partial record.
$lastNewline=$raw.LastIndexOf("`n")
if($lastNewline -lt 0) { throw 'No complete sample yet.' }
$samples=@($raw.Substring(0,$lastNewline).Split("`n") | Where-Object { $_.Trim() } | ForEach-Object { $_ | ConvertFrom-Json })
if($samples.Count -lt 2) { throw 'Need at least two samples.' }

$gapLimit=3*[double]$start.IntervalSeconds
$gaps=0
for($i=1;$i -lt $samples.Count;$i++) { if($samples[$i].Seconds-$samples[$i-1].Seconds -gt $gapLimit) { $gaps++ } }

function Get-Median([double[]]$values) {
    if(-not $values.Count) { return $null }
    $sorted=$values | Sort-Object
    $middle=[int][Math]::Floor($sorted.Count/2)
    if($sorted.Count % 2) { return $sorted[$middle] }
    return ($sorted[$middle-1]+$sorted[$middle])/2
}

$lastSecond=[double]$samples[-1].Seconds
$warmup=60*$WarmupMinutes
$firstWindow=@($samples | Where-Object { $_.Seconds -ge $warmup -and $_.Seconds -lt $warmup+3600 })
$lastWindow=@($samples | Where-Object { $_.Seconds -gt $lastSecond-3600 -and $_.Seconds -ge $warmup })
$metrics=[ordered]@{ PrivateMiB={ param($p) $p.PrivateBytes/1MB }; Handles={ param($p) $p.Handles }; Gdi={ param($p) $p.Gdi }
    User={ param($p) $p.User }; Threads={ param($p) $p.Threads } }

$roles=foreach($role in @('ui','guardian','sensor-host')) {
    $present=@($samples | Where-Object { $_.Processes | Where-Object Role -eq $role })
    if(-not $present.Count) { [ordered]@{Role=$role; Present=$false}; continue }
    $byMetric=[ordered]@{}
    foreach($name in $metrics.Keys) {
        $read={ param($window) @($window | ForEach-Object {
            $values=@($_.Processes | Where-Object Role -eq $role | ForEach-Object { & $metrics[$name] $_ })
            if($values.Count) { ($values | Measure-Object -Sum).Sum }
        }) }
        $all=@(& $read $present)
        $first=Get-Median (& $read $firstWindow)
        $last=Get-Median (& $read $lastWindow)
        $byMetric[$name]=[ordered]@{
            FirstHourMedian=$first; LastHourMedian=$last
            Change=$(if($null -ne $first -and $null -ne $last) { [Math]::Round($last-$first,2) } else { $null })
            Minimum=($all | Measure-Object -Minimum).Minimum; Maximum=($all | Measure-Object -Maximum).Maximum
        }
    }
    # CPU per process instance, so a restarted process does not count as negative time.
    $cpu=0.0; $instances=@{}
    for($i=1;$i -lt $samples.Count;$i++) {
        $seconds=$samples[$i].Seconds-$samples[$i-1].Seconds
        if($seconds -le 0 -or $seconds -gt $gapLimit) { continue }
        foreach($process in @($samples[$i].Processes | Where-Object Role -eq $role)) {
            $previous=$samples[$i-1].Processes | Where-Object { $_.Pid -eq $process.Pid -and $_.Started -eq $process.Started }
            if($previous) { $cpu+=[Math]::Max(0,$process.CpuSeconds-$previous.CpuSeconds) }
        }
    }
    foreach($sample in $present) { foreach($process in @($sample.Processes | Where-Object Role -eq $role)) { $instances["$($process.Pid)/$($process.Started)"]=$true } }
    [ordered]@{ Role=$role; Present=$true; SamplesPresent=$present.Count; Instances=$instances.Count
        CpuPercentOfOneCore=[Math]::Round(100*$cpu/[Math]::Max(1,$lastSecond),3); Metrics=$byMetric }
}

$statisticsPath=Join-Path $ArtifactDirectory 'scan-statistics.log'
$invariant=[Globalization.CultureInfo]::InvariantCulture
# ConvertFrom-Json already turns ISO dates into DateTime; reparsing its text would use the local date format.
$started=if($start.Started -is [DateTime]) { [DateTimeOffset]$start.Started } else { [DateTimeOffset]::Parse($start.Started,$invariant) }
$lines=if(Test-Path -LiteralPath $statisticsPath) { @(Get-Content -LiteralPath $statisticsPath) }
    elseif(Test-Path -LiteralPath $LogDirectory) { @(Get-ChildItem -LiteralPath $LogDirectory -Filter 'nepply-ui.log*' -File | ForEach-Object { Get-Content -LiteralPath $_.FullName }) }
    else { @() }
$full=0; $partial=0; $failed=0; $readSeconds=0.0; $reports=0
foreach($line in $lines) {
    if($line -match '^\[(?<time>[^\]]+)\] INFO: Desktop scans in the last (?<minutes>\d+) min: (?<full>\d+) full, (?<partial>\d+) partial, (?<failed>\d+) failed; reading took (?<read>[\d.]+) s\.$' -and
        [DateTimeOffset]::Parse($Matches.time,$invariant) -ge $started) {
        $reports++; $full+=[int]$Matches.full; $partial+=[int]$Matches.partial; $failed+=[int]$Matches.failed
        $readSeconds+=[double]::Parse($Matches.read,$invariant)
    }
}
$hours=$lastSecond/3600
[ordered]@{
    Samples=$samples.Count; ObservedHours=[Math]::Round($hours,2); WarmupMinutes=$WarmupMinutes; LongGaps=$gaps
    GuardianAtStart=$start.GuardianAtStart; ProductVersion=$start.ProductVersion; Windows=$start.Windows
    Roles=@($roles)
    DesktopScans=[ordered]@{ HourlyReports=$reports; Full=$full; Partial=$partial; Failed=$failed; ReadingSeconds=[Math]::Round($readSeconds,1)
        FullPerDay=$(if($hours -gt 0) { [Math]::Round(24*$full/$hours,1) } else { $null }) }
    Note='First and last hour are medians after warm-up; with less than about two hours they overlap. A gap is a pause longer than three intervals, such as sleep. Scan counts come from the hourly log lines and miss the hour still open.'
} | ConvertTo-Json -Depth 6
