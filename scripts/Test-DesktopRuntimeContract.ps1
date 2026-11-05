#requires -Version 7.4
# Safe for CI: checks the rules of Test-DesktopRuntime.ps1 and its stage loop against a
# fake process. Never hides Desktop icons, never restarts Explorer. A pass says nothing
# about real Explorer behavior; only a live run of the script itself can.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Desktop-Runtime.ps1')
$repo = Split-Path $PSScriptRoot -Parent
$entry = Join-Path $PSScriptRoot 'Test-DesktopRuntime.ps1'
$passed = 0
function Check([string]$Name,[bool]$Condition) {
    if (-not $Condition) { throw "FAIL $Name" }
    $script:passed++
    Write-Output "PASS $Name"
}
function Read-RepoFile([string]$Path) { Get-Content -LiteralPath (Join-Path $repo $Path) -Raw }

# The stage table must match what the test binary really dispatches and writes.
$stages = @(Get-NepplyDesktopRuntimeStages)
$program = Read-RepoFile 'tests/Nepply.Tests/Program.cs'
$testFiles = @(Get-ChildItem -LiteralPath (Join-Path $repo 'tests/Nepply.Tests') -Filter '*.cs' | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw })
Check 'Every stage has a unique name and flag' ((@($stages.Name | Sort-Object -Unique).Count -eq $stages.Count) -and (@($stages.Flag | Sort-Object -Unique).Count -eq $stages.Count))
foreach ($stage in $stages) {
    Check "$($stage.Flag) is dispatched by the test binary" ($program.Contains("args.Contains(`"$($stage.Flag)`")"))
    $writers = @($testFiles | Where-Object { $_.Contains("`"$($stage.ResultFile)`"") })
    Check "$($stage.ResultFile) comes from one test file" ($writers.Count -eq 1)
    Check "$($stage.ResultFile) reports every field the verdict reads" ($writers[0] -match '\bPassed =' -and $writers[0] -match '\bJournalRemains =' -and $writers[0] -match '\bFinal =')
}
foreach ($flag in '--production-guardian-runtime','--production-guardian-crash','--desktop-visibility-runtime','--desktop-crash-runtime','--window-visibility-runtime','--window-crash-runtime') {
    Check "$flag is covered by a stage" ($stages.Flag -ccontains $flag)
}
Check 'Stages that restart Explorer run after all others' (($stages.RestartExplorer | Select-Object -Last 2) -notcontains $false -and @($stages | Where-Object RestartExplorer).Count -eq 2)
Check 'ShowWindow stages rescue with ShowWindow' (@($stages | Where-Object { $_.Flag -like '--window-*' -and $_.Visibility -cne 'Window' }).Count -eq 0)
Check 'Explorer stages rescue through the Explorer option' (@($stages | Where-Object { $_.Flag -notlike '--window-*' -and $_.Visibility -cne 'Explorer' }).Count -eq 0)

# Never in automation.
foreach ($workflow in Get-ChildItem -LiteralPath (Join-Path $repo '.github/workflows') -File) {
    $text = Get-Content -LiteralPath $workflow.FullName -Raw
    Check "$($workflow.Name) never runs the live script" ($text -notmatch 'Test-DesktopRuntime\.ps1')
    foreach ($stage in $stages) { Check "$($workflow.Name) never passes $($stage.Flag)" (-not $text.Contains($stage.Flag)) }
}

# Refusals.
$commit = 'a' * 40
function New-CleanState { @{DesktopVisible=$true;RunningProcesses=@();ProductionJournal=$null;GitCommit=$commit
    Binaries=@([pscustomobject]@{Name='Nepply.Tests.exe';Exists=$true;Commit=$commit},[pscustomobject]@{Name='Nepply.DesktopGuardian.exe';Exists=$true;Commit=$commit})} }
function Test-Refused([string]$Name,[scriptblock]$Change,[string]$Pattern) {
    $state = New-CleanState; & $Change $state
    $reasons = @(Get-NepplyDesktopRuntimeRefusals -State $state)
    Check "Refuses: $Name" (@($reasons | Where-Object { $_ -match $Pattern }).Count -ge 1)
}
Check 'A clean state has no refusals' (@(Get-NepplyDesktopRuntimeRefusals -State (New-CleanState)).Count -eq 0)
Test-Refused 'Desktop already hidden' { param($s) $s.DesktopVisible=$false } 'already hidden'
Test-Refused 'Desktop unavailable' { param($s) $s.DesktopVisible=$null } 'not found'
foreach ($name in Get-NepplyDesktopRuntimeProcessNames) { Test-Refused "$name running" { param($s) $s.RunningProcesses=@("$name (PID 1)") } ([regex]::Escape($name)) }
Test-Refused 'production journal present' { param($s) $s.ProductionJournal='desktop-visibility.lease' } 'recovery journal'
Test-Refused 'unknown commit' { param($s) $s.GitCommit=$null } 'Git commit'
Test-Refused 'missing binary' { param($s) $s.Binaries[0].Exists=$false } 'missing'
Test-Refused 'binary from another commit' { param($s) $s.Binaries[1].Commit='b'*40 } 'rebuild'
Test-Refused 'binary with no commit' { param($s) $s.Binaries[0].Commit=$null } 'unknown commit'
Check 'Reads the commit from ProductVersion' ((Get-NepplyBinaryCommit "2.0.0-rc.1+$commit") -ceq $commit -and $null -eq (Get-NepplyBinaryCommit '2.0.0') -and $null -eq (Get-NepplyBinaryCommit ''))

# Consent.
Check 'Interactive console may be asked' (@(Get-NepplyLiveSessionRefusals -Interactive $true -InputRedirected $false).Count -eq 0)
Check 'Refuses redirected input' (@(Get-NepplyLiveSessionRefusals -Interactive $true -InputRedirected $true).Count -gt 0)
Check 'Refuses a non-interactive session' (@(Get-NepplyLiveSessionRefusals -Interactive $false -InputRedirected $false).Count -gt 0)
Check 'Refuses CI' (@(Get-NepplyLiveSessionRefusals -Interactive $true -InputRedirected $false -CiVariables @('CI')).Count -gt 0)
$phrase = Get-NepplyDesktopRuntimeConsentPhrase
Check 'Exact phrase is consent' (Test-NepplyDesktopRuntimeConsent $phrase)
foreach ($wrong in @('', $null, 'y', 'yes', $phrase.ToLowerInvariant(), " $phrase")) { Check "'$wrong' is not consent" (-not (Test-NepplyDesktopRuntimeConsent $wrong)) }

# Verdicts.
$good = [pscustomobject]@{Passed=$true;JournalRemains=$false;Final=$true}
function Get-Verdict([hashtable]$Change=@{}) {
    $arguments = @{ExitCode=0;TimedOut=$false;Failure=$null;Result=$good;DesktopVisibleAfter=$true;RescueUsed=$false}
    foreach ($key in $Change.Keys) { $arguments[$key]=$Change[$key] }
    Get-NepplyStageVerdict @arguments
}
Check 'All sources agree: pass' (Get-Verdict).Passed
Check 'Exit code alone cannot pass' (-not (Get-Verdict @{ExitCode=1}).Passed)
Check 'No exit code cannot pass' (-not (Get-Verdict @{ExitCode=$null}).Passed)
Check 'Timeout cannot pass' (-not (Get-Verdict @{TimedOut=$true}).Passed)
Check 'Orchestration failure cannot pass' (-not (Get-Verdict @{Failure='Explorer did not come back'}).Passed)
Check 'Missing result file cannot pass' (-not (Get-Verdict @{Result=$null}).Passed)
Check 'Result without Passed cannot pass' (-not (Get-Verdict @{Result=[pscustomobject]@{Passed=$false;JournalRemains=$false;Final=$true}}).Passed)
Check 'A remaining journal cannot pass' (-not (Get-Verdict @{Result=[pscustomobject]@{Passed=$true;JournalRemains=$true;Final=$true}}).Passed)
Check 'A test that read hidden icons cannot pass' (-not (Get-Verdict @{Result=[pscustomobject]@{Passed=$true;JournalRemains=$false;Final=$false}}).Passed)
Check 'Hidden Desktop afterwards cannot pass' (-not (Get-Verdict @{DesktopVisibleAfter=$false}).Passed)
Check 'Unknown Desktop afterwards cannot pass' (-not (Get-Verdict @{DesktopVisibleAfter=$null}).Passed)
Check 'A script rescue cannot pass' (-not (Get-Verdict @{RescueUsed=$true}).Passed)

# Acceptance.
$names = @($stages.Name)
$allPassed = @($names | ForEach-Object { [pscustomobject]@{Name=$_;Passed=$true} })
Check 'Every stage on a clean tree is accepted' (Test-NepplyRunAccepted -ExpectedStages $names -StageResults $allPassed -Dirty $false -BinariesUnchanged $true)
Check 'A dirty tree is not accepted' (-not (Test-NepplyRunAccepted -ExpectedStages $names -StageResults $allPassed -Dirty $true -BinariesUnchanged $true))
Check 'Changed binaries are not accepted' (-not (Test-NepplyRunAccepted -ExpectedStages $names -StageResults $allPassed -Dirty $false -BinariesUnchanged $false))
Check 'A partial run is not accepted' (-not (Test-NepplyRunAccepted -ExpectedStages $names -StageResults @($allPassed | Select-Object -First 5) -Dirty $false -BinariesUnchanged $true))
Check 'A run with no stages is not accepted' (-not (Test-NepplyRunAccepted -ExpectedStages $names -StageResults @() -Dirty $false -BinariesUnchanged $true))
$oneFailed = @($allPassed | ForEach-Object { [pscustomobject]@{Name=$_.Name;Passed=($_.Name -cne $names[0])} })
Check 'One failed stage is not accepted' (-not (Test-NepplyRunAccepted -ExpectedStages $names -StageResults $oneFailed -Dirty $false -BinariesUnchanged $true))
$row = Format-NepplyDesktopRuntimeRow -Tag 'v2.0.0-rc.2' -Started ([datetimeoffset]'2026-11-05T10:00:00+07:00') -Commit $commit -WindowsBuild '26300.1' -Accepted $true -SummaryHash 'ABC'
Check 'Checklist row records tag, date, commit, build, result and hash' ($row -ceq '| `v2.0.0-rc.2` | 2026-11-05 | `aaaaaaa` | 26300.1 | PASS | `ABC` |')

# Stage loop, against a fake test process. Nothing here can reach Explorer.
$fakeRoot = Join-Path ([IO.Path]::GetTempPath()) ('nepply-desktop-runtime-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fakeRoot | Out-Null
try {
    $fake = Join-Path $fakeRoot 'fake.ps1'
    Set-Content -LiteralPath $fake -Encoding utf8 -Value @'
param([string]$Output,[string]$Mode)
switch ($Mode) {
    'pass' { 'fake stdout'; exit 0 }
    'fail' { exit 7 }
    'hang' { Start-Sleep -Seconds 120; exit 0 }
    'restart' {
        Set-Content -LiteralPath (Join-Path $Output 'ready-for-explorer-restart') -Value $PID
        $deadline=[DateTime]::UtcNow.AddSeconds(30)
        while ([DateTime]::UtcNow -lt $deadline) { if (Test-Path -LiteralPath (Join-Path $Output 'explorer-restarted')) { exit 0 }; Start-Sleep -Milliseconds 50 }
        exit 9
    }
}
'@
    $pwshPath = (Get-Command pwsh -CommandType Application | Select-Object -First 1).Source
    function Invoke-Fake([string]$Mode,[bool]$RestartExplorer=$false,[scriptblock]$RestartAction={},[int]$TimeoutSeconds=60) {
        $output = Join-Path $fakeRoot ([Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $output | Out-Null
        $script:lastFakeOutput = $output
        Invoke-NepplyStageProcess -FilePath $pwshPath -Arguments @('-NoLogo','-NoProfile','-NonInteractive','-File',$fake,$output,$Mode) -OutputDirectory $output `
            -RestartExplorer $RestartExplorer -RestartAction $RestartAction -TimeoutSeconds $TimeoutSeconds
    }
    $run = Invoke-Fake 'pass'
    Check 'Stage loop returns exit code and output' ($run.ExitCode -eq 0 -and -not $run.TimedOut -and -not $run.Failure -and $run.Output -match 'fake stdout')
    $run = Invoke-Fake 'fail'
    Check 'Stage loop reports a failing exit code' ($run.ExitCode -eq 7)
    $script:restarts = 0
    $run = Invoke-Fake 'restart' $true { $script:restarts++ }
    Check 'Stage loop restarts Explorer once, then acknowledges' ($run.ExitCode -eq 0 -and $run.Restarted -and $script:restarts -eq 1 -and (Test-Path -LiteralPath (Join-Path $lastFakeOutput 'explorer-restarted')))
    $script:restarts = 0
    $run = Invoke-Fake 'restart' $true { $script:restarts++; throw 'Explorer did not come back' }
    Check 'A failed Explorer restart stops the test without acknowledging' ($run.Failure -match 'did not come back' -and $null -eq $run.ExitCode -and -not (Test-Path -LiteralPath (Join-Path $lastFakeOutput 'explorer-restarted')) -and $script:restarts -eq 1)
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $run = Invoke-Fake 'hang' $false {} 2
    Check 'A hung test is stopped at its time limit' ($run.TimedOut -and $null -eq $run.ExitCode -and $clock.Elapsed.TotalSeconds -lt 30)
} finally { Remove-Item -LiteralPath $fakeRoot -Recurse -Force }

# Entry script.
$errors = $null; $tokens = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($entry,[ref]$tokens,[ref]$errors)
Check 'Entry script parses' ($errors.Count -eq 0)
$entryParameters = @($ast.ParamBlock.Parameters | ForEach-Object { $_.Name.VariablePath.UserPath } | Sort-Object)
Check 'Entry script has no parameter that could skip the question' (($entryParameters -join ',') -ceq 'GuardianExecutable,Live,ReleaseTag,TestExecutable')
$entryText = Get-Content -LiteralPath $entry -Raw
$gate = $entryText.IndexOf('$sessionRefusals.Count -or $refusals.Count', [StringComparison]::Ordinal)
$consent = $entryText.IndexOf('Test-NepplyDesktopRuntimeConsent $typed', [StringComparison]::Ordinal)
$firstStage = $entryText.IndexOf('Invoke-NepplyStageProcess -FilePath', [StringComparison]::Ordinal)
$dryRunExit = $entryText.IndexOf('if (-not $Live)', [StringComparison]::Ordinal)
Check 'Dry run leaves first, then refusals, then the question, then stages' ($dryRunExit -gt 0 -and $gate -gt $dryRunExit -and $consent -gt $gate -and $firstStage -gt $consent)

# What the entry script copies from the product must still match it.
$native = Read-RepoFile 'src/Nepply/Features/Desktop/Visibility/ExplorerDesktopVisibility.cs'
foreach ($shared in 'Progman','WorkerW','SHELLDLL_DefView','SysListView32','Local\Nepply-DesktopVisibility-','0x7402','0x0002 | 0x0020') {
    Check "Entry script and product share $shared" ($native.Contains($shared) -and $entryText.Contains($shared))
}
Check 'ShowWindow rescue uses the product command' ($native -match 'ShowWithoutActivating = 8' -and $entryText.Contains('ShowWindow(list, 8)'))
$lease = Read-RepoFile 'src/Nepply/Features/Desktop/Visibility/DesktopVisibilityLease.cs'
Check 'Journal path matches the product' ($lease.Contains('ProductIdentity.Name, "desktop-visibility.lease"') -and (Read-RepoFile 'src/Nepply/Composition/ProductIdentity.cs') -match 'Name\s*=\s*"Nepply"' -and $entryText.Contains("'Nepply/desktop-visibility.lease'"))

# Live must refuse here. The binary is missing on purpose: even with a broken gate this cannot hide anything.
$before = Test-Path -LiteralPath (Join-Path $repo 'artifacts/desktop-runtime')
$missing = Join-Path $repo ('artifacts/missing-' + [Guid]::NewGuid().ToString('N') + '/Nepply.Tests.exe')
$liveOutput = ($phrase | & $pwshPath -NoLogo -NoProfile -NonInteractive -File $entry -Live -TestExecutable $missing 2>&1 | Out-String)
$liveExit = $LASTEXITCODE
Check 'Live with piped input is refused' ($liveExit -eq 2 -and $liveOutput -match 'Refused')
Check 'Live refusal names the session' ($liveOutput -match 'redirected|not interactive|never runs in automation')
$dryOutput = (& $pwshPath -NoLogo -NoProfile -NonInteractive -File $entry 2>&1 | Out-String)
$dryExit = $LASTEXITCODE
Check 'Dry run says what it is and exits as passed or refused' ($dryExit -in 0,2 -and $dryOutput -match 'DRY RUN')
foreach ($stage in $stages) { Check "Dry run lists $($stage.Flag)" ($dryOutput.Contains($stage.Flag)) }
Check 'Neither run wrote results' ((Test-Path -LiteralPath (Join-Path $repo 'artifacts/desktop-runtime')) -eq $before)

# Documentation.
foreach ($document in 'CONTRIBUTING.md','CONTRIBUTING.en.md','docs/release/PUBLIC_RELEASE_CHECKLIST.md') {
    $text = Read-RepoFile $document
    Check "$document explains the script" ($text.Contains('scripts/Test-DesktopRuntime.ps1') -and $text.Contains('-Live'))
}
Check 'Checklist has the run record' ((Read-RepoFile 'docs/release/PUBLIC_RELEASE_CHECKLIST.md').Contains('| Tag | Ngày | Commit | Windows | Kết quả | SHA-256 của `summary.json` |'))

Write-Output "PASS: $passed checks. No Desktop change. Live Explorer behavior is not covered here."
