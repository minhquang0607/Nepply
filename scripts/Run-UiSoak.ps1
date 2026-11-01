#requires -Version 7.4
param(
    [ValidateRange(1,1440)][int]$Minutes=1440,
    [string]$TestExecutable
)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
if([string]::IsNullOrWhiteSpace($TestExecutable)) {
    $TestExecutable=Join-Path $projectRoot 'tests/Nepply.Tests/bin/Release/net10.0-windows10.0.19041.0/win-x64/Nepply.Tests.exe'
}
$executable=(Resolve-Path -LiteralPath $TestExecutable).Path
$binaryRoot=Split-Path $executable -Parent
$binaries=@($executable,(Join-Path $binaryRoot 'Nepply.Tests.dll'),(Join-Path $binaryRoot 'Nepply.dll'))
$hashes=@($binaries | ForEach-Object {Get-FileHash -LiteralPath $_ -Algorithm SHA256 | Select-Object Path,Hash})
$runRoot=Join-Path $projectRoot ('artifacts/standalone-soak/'+[DateTime]::Now.ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runRoot -ErrorAction Stop | Out-Null
$start=[DateTimeOffset]::Now
$metadata=[ordered]@{Started=$start.ToString('o');RequestedMinutes=$Minutes;Profile='normal';RunnerPid=$PID;Binaries=$hashes;Output=$runRoot}
$metadata | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $runRoot 'run-start.json') -Encoding utf8
Write-Host "Results: $runRoot"
Write-Host 'Keep the terminal and machine running until it finishes. Do not rebuild the binary under test.'
Write-Host 'Uses 500 fixture items and Windows metrics, alternating show/hide off screen; does not accept native sensors or Shell.'
$exitCode=$null
$failure=$null
$accepted=$false
try {
    # Foreground execution: this script neither creates a scheduled job nor changes power policy.
    $PSNativeCommandUseErrorActionPreference=$false
    & $executable $runRoot --ui-soak --minutes $Minutes --profile normal 2>&1 |
        Tee-Object -FilePath (Join-Path $runRoot 'console.log')
    $exitCode=$LASTEXITCODE
    $summaryPath=Join-Path $runRoot 'ui-soak-summary.json'
    if(!(Test-Path -LiteralPath $summaryPath)) {throw 'No summary; the run did not complete.'}
    $summary=Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
    $unchanged=$true
    foreach($binary in $hashes) {
        if((Get-FileHash -LiteralPath $binary.Path -Algorithm SHA256).Hash -ne $binary.Hash) {$unchanged=$false}
    }
    $accepted=$exitCode -eq 0 -and $summary.Completed -eq $true -and $summary.LoopsStopped -eq $true -and
        $summary.RequestedMinutes -eq $Minutes -and $summary.Seconds -ge 60*$Minutes -and $unchanged
    if(!$accepted) {throw 'The run did not meet completion conditions or the binary changed; see the summary.'}
}
catch {$failure=$_.Exception.ToString();Write-Warning $failure}
finally {
    [ordered]@{Started=$start.ToString('o');Ended=[DateTimeOffset]::Now.ToString('o');ExitCode=$exitCode;
        RunCompleted=$accepted;Error=$failure;Note='RunCompleted confirms duration and teardown only; it does not prove the absence of leaks.'} |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $runRoot 'run-result.json') -Encoding utf8
}
if(!$accepted) {exit 1}
Write-Host 'Ran for the full duration. Analyze the JSONL for CPU/RAM/handles next; this is not whole-app acceptance.'
exit 0
