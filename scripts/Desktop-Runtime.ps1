# Rules behind Test-DesktopRuntime.ps1. Nothing here reads or changes Explorer: every
# fact arrives as an argument, and Explorer actions are callbacks, so CI can test the
# rules and the stage loop against a fake process without touching a real Desktop.

function Get-NepplyDesktopRuntimeStages {
    # Visibility names the mechanism a stage used, so rescue reverses the right one:
    # toggling Explorer's saved option after a ShowWindow hide would hide icons for good.
    # Stages that restart Explorer run last, so a failure elsewhere costs the operator less.
    $stage = { param($Name,$Flag,$ResultFile,$Visibility,$Guardian,$RestartExplorer)
        [pscustomobject]@{Name=$Name;Flag=$Flag;ResultFile=$ResultFile;Visibility=$Visibility;Guardian=$Guardian;RestartExplorer=$RestartExplorer} }
    @(
        & $stage 'guardian-normal'    '--production-guardian-runtime' 'production-guardian-runtime.json' 'Explorer' $true  $false
        & $stage 'guardian-crash'     '--production-guardian-crash'   'production-guardian-crash.json'   'Explorer' $true  $false
        & $stage 'explorer-crash'     '--desktop-crash-runtime'       'desktop-crash-result.json'        'Explorer' $false $false
        & $stage 'window-crash'       '--window-crash-runtime'        'window-crash-result.json'         'Window'   $false $false
        & $stage 'explorer-restart'   '--desktop-visibility-runtime'  'desktop-visibility-runtime.json'  'Explorer' $false $true
        & $stage 'window-restart'     '--window-visibility-runtime'   'window-visibility-runtime.json'   'Window'   $false $true
    )
}

function Get-NepplyDesktopRuntimeProcessNames { @('Nepply','Nepply.DesktopGuardian','Nepply.Tests') }
function Get-NepplyDesktopRuntimeConsentPhrase { 'HIDE MY DESKTOP ICONS' }

function Get-NepplyBinaryCommit {
    # .NET appends the source commit to ProductVersion after '+'.
    param([AllowEmptyString()][AllowNull()][string]$ProductVersion)
    if ($ProductVersion -match '\+([0-9a-fA-F]{40})$') { $Matches[1].ToLowerInvariant() } else { $null }
}

function Get-NepplyDesktopRuntimeRefusals {
    # Hashtable keys: DesktopVisible ($true, $false or $null when unavailable),
    # RunningProcesses, ProductionJournal (path if present), GitCommit, Binaries
    # (Name, Exists, Commit). Returns every reason, so one run shows them all.
    param([Parameter(Mandatory)][hashtable]$State)
    $refusals = [Collections.Generic.List[string]]::new()
    foreach ($name in @($State.RunningProcesses)) {
        if ($name) { $refusals.Add("$name is running; close it first (Nepply stays open in the tray).") }
    }
    if ($State.DesktopVisible -eq $false) {
        $refusals.Add('Desktop icons are already hidden; show them first, because a hidden Desktop cannot be told apart from a change made by this test.')
    } elseif ($null -eq $State.DesktopVisible) {
        $refusals.Add('The Desktop icon window was not found; Explorer may not be running as the shell.')
    }
    if ($State.ProductionJournal) {
        $refusals.Add("A Nepply recovery journal exists at $($State.ProductionJournal); start and exit Nepply once so it restores what it owns.")
    }
    if (-not $State.GitCommit) { $refusals.Add('Cannot read the Git commit, so the binaries cannot be matched to the source.') }
    foreach ($binary in @($State.Binaries)) {
        if (-not $binary.Exists) { $refusals.Add("$($binary.Name) is missing; build Release first.") }
        elseif ($State.GitCommit -and $binary.Commit -cne $State.GitCommit) {
            $refusals.Add("$($binary.Name) was built from $(if ($binary.Commit) { $binary.Commit } else { 'an unknown commit' }), not $($State.GitCommit); rebuild Release.")
        }
    }
    @($refusals)
}

function Get-NepplyLiveSessionRefusals {
    # A live run needs a person at a console; piped or scheduled input must not count as consent.
    param([bool]$Interactive, [bool]$InputRedirected, [AllowEmptyCollection()][string[]]$CiVariables = @())
    $refusals = [Collections.Generic.List[string]]::new()
    if (-not $Interactive) { $refusals.Add('The session is not interactive.') }
    if ($InputRedirected) { $refusals.Add('Input is redirected; the confirmation must be typed at a console.') }
    foreach ($name in $CiVariables) { if ($name) { $refusals.Add("$name is set; this test never runs in automation.") } }
    @($refusals)
}

function Test-NepplyDesktopRuntimeConsent {
    param([AllowEmptyString()][AllowNull()][string]$Typed)
    $Typed -ceq (Get-NepplyDesktopRuntimeConsentPhrase)
}

function Get-NepplyStageVerdict {
    # Every source must agree: exit code, the test's own result file, and the Desktop
    # read back afterwards. A rescue means the product did not restore, so never a pass.
    param(
        [Nullable[int]]$ExitCode, [bool]$TimedOut, [AllowNull()][string]$Failure,
        [AllowNull()]$Result, [AllowNull()]$DesktopVisibleAfter, [bool]$RescueUsed
    )
    $reasons = [Collections.Generic.List[string]]::new()
    if ($TimedOut) { $reasons.Add('The stage hit its time limit and was stopped.') }
    if ($Failure) { $reasons.Add($Failure) }
    if ($null -eq $ExitCode) { $reasons.Add('No exit code.') }
    elseif ($ExitCode -ne 0) { $reasons.Add("Exit code $ExitCode.") }
    if ($null -eq $Result) { $reasons.Add('The test wrote no result file.') }
    else {
        if ($Result.Passed -ne $true) { $reasons.Add('The result file does not report Passed.') }
        if ($Result.JournalRemains -ne $false) { $reasons.Add('The QA recovery journal was not cleared.') }
        if ($Result.Final -ne $true) { $reasons.Add('The test did not read the icons back as visible.') }
    }
    if ($DesktopVisibleAfter -ne $true) { $reasons.Add('Desktop icons were not visible after the stage.') }
    if ($RescueUsed) { $reasons.Add('The script had to restore the Desktop itself.') }
    [pscustomobject]@{Passed=($reasons.Count -eq 0);Reasons=@($reasons)}
}

function Test-NepplyRunAccepted {
    # A partial run, a dirty tree or a rebuilt binary can still help diagnosis, but it
    # cannot stand as evidence for a tag.
    param([string[]]$ExpectedStages, [AllowEmptyCollection()][object[]]$StageResults, [bool]$Dirty, [bool]$BinariesUnchanged)
    if ($Dirty -or -not $BinariesUnchanged) { return $false }
    $passed = @($StageResults | Where-Object { $_.Passed -eq $true } | ForEach-Object { $_.Name })
    foreach ($name in $ExpectedStages) { if ($passed -cnotcontains $name) { return $false } }
    $true
}

function Format-NepplyDesktopRuntimeRow {
    param([string]$Tag, [datetimeoffset]$Started, [string]$Commit, [string]$WindowsBuild, [bool]$Accepted, [string]$SummaryHash)
    '| {0} | {1} | `{2}` | {3} | {4} | `{5}` |' -f $(if ($Tag) { "``$Tag``" } else { '-' }), $Started.ToString('yyyy-MM-dd'),
        $Commit.Substring(0,[Math]::Min(7,$Commit.Length)), $WindowsBuild, $(if ($Accepted) { 'PASS' } else { 'FAIL' }), $SummaryHash
}

function Invoke-NepplyStageProcess {
    # Runs one test process to its end. The test hands over by file to restart Explorer.
    # The process is always gone on return, including on Ctrl+C; the caller then checks
    # the Desktop and rescues if needed.
    param(
        [Parameter(Mandatory)][string]$FilePath, [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][string]$OutputDirectory, [bool]$RestartExplorer,
        [scriptblock]$RestartAction, [int]$TimeoutSeconds = 300
    )
    $start = [Diagnostics.ProcessStartInfo]::new($FilePath)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $ready = Join-Path $OutputDirectory 'ready-for-explorer-restart'
    $acknowledged = Join-Path $OutputDirectory 'explorer-restarted'
    $timedOut = $false; $failure = $null; $exitCode = $null; $restarted = $false; $output = ''
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $clock = [Diagnostics.Stopwatch]::StartNew()
        try {
            while (-not $process.HasExited) {
                if ($RestartExplorer -and -not $restarted -and (Test-Path -LiteralPath $ready)) {
                    & $RestartAction
                    $restarted = $true
                    Set-Content -LiteralPath $acknowledged -Value 'restarted' -Encoding utf8
                }
                if ($clock.Elapsed.TotalSeconds -ge $TimeoutSeconds) { $timedOut = $true; break }
                Start-Sleep -Milliseconds 100
            }
        } catch { $failure = $_.Exception.Message }
        if ($process.HasExited) { $exitCode = $process.ExitCode }
    } finally {
        if (-not $process.HasExited) { $process.Kill(); [void]$process.WaitForExit(10000) }
        # A child that inherited the pipe can keep it open past this process's exit.
        if ($stdout -and $stdout.Wait(2000)) { $output += $stdout.Result }
        if ($stderr -and $stderr.Wait(2000)) { $output += $stderr.Result }
        $process.Dispose()
    }
    [pscustomobject]@{ExitCode=$exitCode;TimedOut=$timedOut;Failure=$failure;Restarted=$restarted;Output=$output}
}
