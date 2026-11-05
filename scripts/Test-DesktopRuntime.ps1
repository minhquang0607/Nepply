#requires -Version 7.4
# Release gate for the main feature on a real Desktop. CI only has fixtures for this.
# Without -Live it is a dry run: read-only checks and the plan, nothing changed or written.
# With -Live it hides the real Desktop icons and restarts Explorer, so it asks the person
# at the console each time. There is deliberately no switch to skip that question.
[CmdletBinding()]
param(
    [switch]$Live,
    [ValidatePattern('^v\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$')][string]$ReleaseTag,
    [string]$TestExecutable,
    [string]$GuardianExecutable
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'Release-Layout.ps1')
. (Join-Path $PSScriptRoot 'Desktop-Runtime.ps1')

$ExitPassed = 0; $ExitFailed = 1; $ExitRefused = 2; $ExitDesktopLeftHidden = 3
if (-not $TestExecutable) { $TestExecutable = Join-Path $repo 'tests/Nepply.Tests/bin/Release/net10.0-windows10.0.19041.0/win-x64/Nepply.Tests.exe' }
if (-not $GuardianExecutable) { $GuardianExecutable = Join-Path $repo 'src/Nepply.DesktopGuardian/bin/Release/net10.0-windows/win-x64/Nepply.DesktopGuardian.exe' }
$TestExecutable = [IO.Path]::GetFullPath($TestExecutable)
$GuardianExecutable = [IO.Path]::GetFullPath($GuardianExecutable)
$binaries = @($TestExecutable,$GuardianExecutable)
$stages = @(Get-NepplyDesktopRuntimeStages)
$journal = Join-Path $env:LOCALAPPDATA 'Nepply/desktop-visibility.lease'

# Independent of the binaries under test on purpose: rescue must work when they are broken.
# ExplorerDesktopVisibility.cs holds the same window search; the contract test compares them.
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
public static class NepplyDesktopRuntimeNative
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out UIntPtr result);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string name, string title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string name, string title);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    private static void Locate(out IntPtr view, out IntPtr list)
    {
        view = FindWindowEx(FindWindow("Progman", null), IntPtr.Zero, "SHELLDLL_DefView", null);
        for (IntPtr worker = IntPtr.Zero; view == IntPtr.Zero;)
        {
            worker = FindWindowEx(IntPtr.Zero, worker, "WorkerW", null);
            if (worker == IntPtr.Zero) break;
            view = FindWindowEx(worker, IntPtr.Zero, "SHELLDLL_DefView", null);
        }
        list = view == IntPtr.Zero ? IntPtr.Zero : FindWindowEx(view, IntPtr.Zero, "SysListView32", null);
    }
    public static bool? IsVisible()
    {
        IntPtr view, list;
        Locate(out view, out list);
        return list == IntPtr.Zero ? (bool?)null : IsWindowVisible(list);
    }
    public static uint ShellProcessId()
    {
        IntPtr shell = GetShellWindow();
        uint id = 0;
        if (shell != IntPtr.Zero) GetWindowThreadProcessId(shell, out id);
        return id;
    }
    // Explorer offers a toggle, so act only on an observed hidden state, under the app's lock.
    public static bool ShowThroughExplorerOption()
    {
        string sid = WindowsIdentity.GetCurrent().User.Value;
        using (var mutex = new Mutex(false, @"Local\Nepply-DesktopVisibility-" + sid))
        {
            bool acquired;
            try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(2)); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) return false;
            try
            {
                IntPtr view, list;
                Locate(out view, out list);
                if (list == IntPtr.Zero) return false;
                if (IsWindowVisible(list)) return true;
                UIntPtr ignored;
                SendMessageTimeout(view, 0x0111, (IntPtr)0x7402, IntPtr.Zero, 0x0002 | 0x0020, 500, out ignored);
                return IsWindowVisible(list);
            }
            finally { mutex.ReleaseMutex(); }
        }
    }
    public static bool ShowThroughWindow()
    {
        IntPtr view, list;
        Locate(out view, out list);
        if (list == IntPtr.Zero) return false;
        ShowWindow(list, 8);
        return IsWindowVisible(list);
    }
}
'@

function Get-RunningNepplyProcesses {
    foreach ($name in Get-NepplyDesktopRuntimeProcessNames) {
        $found = @(Get-Process -Name $name -ErrorAction SilentlyContinue)
        if ($found.Count) { "$name (PID $(($found | ForEach-Object Id) -join ', '))" }
        $found | ForEach-Object { $_.Dispose() }
    }
}

function Get-GitFacts {
    $commit = (& git -C $repo rev-parse HEAD 2>$null)
    if ($LASTEXITCODE -ne 0) { $commit = $null }
    $status = @(& git -C $repo status --porcelain 2>$null)
    $dirty = ($LASTEXITCODE -ne 0) -or ($status.Count -gt 0)
    $tag = (& git -C $repo describe --tags --exact-match 2>$null)
    if ($LASTEXITCODE -ne 0) { $tag = $null }
    [pscustomobject]@{Commit=$commit;Dirty=$dirty;ExactTag=$tag}
}

function Get-PreflightRefusals([string]$Commit) {
    $binaryFacts = foreach ($path in $binaries) {
        $exists = Test-Path -LiteralPath $path -PathType Leaf
        [pscustomobject]@{Name=[IO.Path]::GetFileName($path);Exists=$exists
            Commit=$(if ($exists) { Get-NepplyBinaryCommit (Get-Item -LiteralPath $path).VersionInfo.ProductVersion })}
    }
    @(Get-NepplyDesktopRuntimeRefusals -State @{
        DesktopVisible=[NepplyDesktopRuntimeNative]::IsVisible()
        RunningProcesses=@(Get-RunningNepplyProcesses)
        ProductionJournal=$(if (Test-Path -LiteralPath $journal) { $journal })
        GitCommit=$Commit
        Binaries=@($binaryFacts)
    })
}

function Get-WindowsBuild {
    $key = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
    "$($key.CurrentBuildNumber).$($key.UBR)"
}

function Get-StageArguments($Stage, [string]$Output) {
    $arguments = @($Output,$Stage.Flag)
    # The test reads the guardian path from its last argument.
    if ($Stage.Guardian) { $arguments += $GuardianExecutable }
    $arguments
}

function Restart-ExplorerShell {
    $previous = [NepplyDesktopRuntimeNative]::ShellProcessId()
    if (-not $previous) { throw 'Cannot find the Explorer shell process to restart.' }
    Stop-Process -Id $previous -Force
    $clock = [Diagnostics.Stopwatch]::StartNew(); $started = $false
    while ($clock.Elapsed.TotalSeconds -lt 60) {
        $current = [NepplyDesktopRuntimeNative]::ShellProcessId()
        if ($current -and $current -ne $previous -and $null -ne [NepplyDesktopRuntimeNative]::IsVisible()) { return }
        # Windows normally restarts its shell. Opening explorer.exe while a shell runs would only open a folder.
        if (-not $started -and -not $current -and $clock.Elapsed.TotalSeconds -ge 10) { Start-Process explorer.exe; $started = $true }
        Start-Sleep -Milliseconds 200
    }
    throw 'Explorer did not come back within 60 seconds.'
}

function Stop-LeftoverProcesses {
    # Stopping a QA owner first lets its armed guardian do the real recovery.
    foreach ($process in @(Get-Process -Name 'Nepply.Tests' -ErrorAction SilentlyContinue)) {
        try { if ($process.Path -and [string]::Equals($process.Path,$TestExecutable,[StringComparison]::OrdinalIgnoreCase)) { $process.Kill(); [void]$process.WaitForExit(10000) } }
        finally { $process.Dispose() }
    }
    $clock = [Diagnostics.Stopwatch]::StartNew()
    while ($clock.Elapsed.TotalSeconds -lt 30) {
        $guardians = @(Get-Process -Name 'Nepply.DesktopGuardian' -ErrorAction SilentlyContinue)
        $count = $guardians.Count
        $guardians | ForEach-Object { $_.Dispose() }
        if (-not $count) { return }
        Start-Sleep -Milliseconds 200
    }
    # Both must not act on the Desktop at once, so end a stuck guardian before any rescue.
    foreach ($process in @(Get-Process -Name 'Nepply.DesktopGuardian' -ErrorAction SilentlyContinue)) {
        try { if ($process.Path -and [string]::Equals($process.Path,$GuardianExecutable,[StringComparison]::OrdinalIgnoreCase)) { $process.Kill(); [void]$process.WaitForExit(10000) } }
        finally { $process.Dispose() }
    }
}

function Restore-DesktopIfHidden([string]$Visibility) {
    $used = $false; $started = $false
    $clock = [Diagnostics.Stopwatch]::StartNew()
    while ($clock.Elapsed.TotalSeconds -lt 45) {
        $visible = [NepplyDesktopRuntimeNative]::IsVisible()
        if ($visible -eq $true) { break }
        if ($visible -eq $false) {
            $used = $true
            if ($Visibility -ceq 'Window') { [void][NepplyDesktopRuntimeNative]::ShowThroughWindow() }
            else { [void][NepplyDesktopRuntimeNative]::ShowThroughExplorerOption() }
        } elseif (-not $started -and -not [NepplyDesktopRuntimeNative]::ShellProcessId() -and $clock.Elapsed.TotalSeconds -ge 15) {
            Start-Process explorer.exe; $started = $true
        }
        Start-Sleep -Milliseconds 300
    }
    [pscustomobject]@{RescueUsed=$used;Visible=[NepplyDesktopRuntimeNative]::IsVisible()}
}

$git = Get-GitFacts
$refusals = @(Get-PreflightRefusals $git.Commit)
$ciVariables = @('CI','GITHUB_ACTIONS','TF_BUILD' | Where-Object { [Environment]::GetEnvironmentVariable($_) })
$sessionRefusals = @(Get-NepplyLiveSessionRefusals -Interactive ([Environment]::UserInteractive) -InputRedirected ([Console]::IsInputRedirected) -CiVariables $ciVariables)

if (-not $Live) {
    Write-Output 'DRY RUN: nothing was hidden, restarted or written.'
    Write-Output "Commit: $($git.Commit)$(if ($git.Dirty) { ' (uncommitted changes: a live run would not count as evidence)' })"
    Write-Output "Windows build: $(Get-WindowsBuild)"
    Write-Output "Desktop icons visible now: $([NepplyDesktopRuntimeNative]::IsVisible())"
    Write-Output 'A live run would do, stopping at the first failure:'
    foreach ($stage in $stages) {
        Write-Output "  $($stage.Name): Nepply.Tests.exe <output> $($stage.Flag)$(if ($stage.Guardian) { ' <guardian>' })$(if ($stage.RestartExplorer) { ' (restarts Explorer)' })"
    }
    if ($sessionRefusals.Count) { Write-Output 'This session could not start a live run:'; $sessionRefusals | ForEach-Object { Write-Output "  - $_" } }
    if ($refusals.Count) {
        Write-Output 'Preflight would refuse a live run:'
        $refusals | ForEach-Object { Write-Output "  - $_" }
        exit $ExitRefused
    }
    Write-Output 'Preflight passed. To run for real, use -Live at a console.'
    exit $ExitPassed
}

foreach ($reason in @($sessionRefusals + $refusals)) { Write-Output "Refused: $reason" }
if ($sessionRefusals.Count -or $refusals.Count) { exit $ExitRefused }

$phrase = Get-NepplyDesktopRuntimeConsentPhrase
Write-Output @"
This hides your real Desktop icons several times over a few minutes.
It also restarts Explorer twice: File Explorer windows close and the taskbar reloads.
Save your work in File Explorer and leave the mouse alone until it ends.
It stops at the first failure and restores the icons. If they stay hidden, it says how to bring them back.
"@
$typed = $null
try { $typed = Read-Host "Type $phrase to continue" } catch { $typed = $null }
if (-not (Test-NepplyDesktopRuntimeConsent $typed)) { Write-Output 'Refused: the confirmation did not match. Nothing was changed.'; exit $ExitRefused }

$started = [DateTimeOffset]::Now
$folder = if ($ReleaseTag) { $ReleaseTag } else { 'untagged' }
$runRoot = New-NepplyArtifactDirectory -Repository $repo -Path ("artifacts/desktop-runtime/$folder/" + $started.ToString('yyyyMMdd-HHmmss') + '-' + $git.Commit.Substring(0,7))
$hashes = @($binaries | ForEach-Object { Get-FileHash -LiteralPath $_ -Algorithm SHA256 | Select-Object Path,Hash })
$results = [Collections.Generic.List[object]]::new()
$desktopLeftHidden = $false
$failure = $null
Write-Output "Results: $runRoot"
try {
    foreach ($stage in $stages) {
        $between = @(Get-PreflightRefusals $git.Commit)
        if ($between.Count) { throw "State changed before $($stage.Name): $($between -join ' ')" }
        $output = Join-Path $runRoot $stage.Name
        New-Item -ItemType Directory -Path $output -ErrorAction Stop | Out-Null
        Write-Output "$($stage.Name): running"
        $run = $null; $check = $null
        try {
            $run = Invoke-NepplyStageProcess -FilePath $TestExecutable -Arguments (Get-StageArguments $stage $output) -OutputDirectory $output `
                -RestartExplorer $stage.RestartExplorer -RestartAction { Restart-ExplorerShell }
        } finally {
            # Runs on Ctrl+C too. Never leave a Desktop hidden because the run was interrupted.
            Stop-LeftoverProcesses
            $check = Restore-DesktopIfHidden $stage.Visibility
            if ($check.Visible -ne $true) { $desktopLeftHidden = $true }
        }
        Set-Content -LiteralPath (Join-Path $output 'console.log') -Value $run.Output -Encoding utf8
        $resultPath = Join-Path $output $stage.ResultFile
        $result = $null
        if (Test-Path -LiteralPath $resultPath) {
            try { $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json } catch { $result = $null }
        }
        $verdict = Get-NepplyStageVerdict -ExitCode $run.ExitCode -TimedOut $run.TimedOut -Failure $run.Failure -Result $result `
            -DesktopVisibleAfter $check.Visible -RescueUsed $check.RescueUsed
        $results.Add([pscustomobject]@{Name=$stage.Name;Flag=$stage.Flag;Passed=$verdict.Passed;Reasons=$verdict.Reasons;ExitCode=$run.ExitCode
            TimedOut=$run.TimedOut;RescueUsed=$check.RescueUsed;DesktopVisibleAfter=$check.Visible;Result=$result})
        Write-Output "$($stage.Name): $(if ($verdict.Passed) { 'PASS' } else { 'FAIL ' + ($verdict.Reasons -join ' ') })"
        if (-not $verdict.Passed) { break }
    }
} catch { $failure = $_.Exception.Message; Write-Output "FAIL $failure" }
finally {
    $unchanged = -not @($hashes | Where-Object { (Get-FileHash -LiteralPath $_.Path -Algorithm SHA256).Hash -cne $_.Hash }).Count
    $names = @($stages | ForEach-Object Name)
    $accepted = (-not $failure) -and (Test-NepplyRunAccepted -ExpectedStages $names -StageResults @($results) -Dirty $git.Dirty -BinariesUnchanged $unchanged)
    $windowsBuild = Get-WindowsBuild
    $summaryPath = Join-Path $runRoot 'summary.json'
    [ordered]@{
        Started=$started.ToString('o');Ended=[DateTimeOffset]::Now.ToString('o');ReleaseTag=$ReleaseTag;Commit=$git.Commit;Dirty=$git.Dirty;ExactTag=$git.ExactTag
        WindowsBuild=$windowsBuild;Binaries=$hashes;BinariesUnchanged=$unchanged;Stages=@($results);NotRun=@($names | Where-Object { @($results | ForEach-Object Name) -cnotcontains $_ })
        Accepted=$accepted;DesktopLeftHidden=$desktopLeftHidden;Error=$failure
        Scope='Real Explorer on this Windows build, from an unpackaged test process with isolated QA journals. Not installer, MSIX, Store or uninstall acceptance.'
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding utf8
    $row = Format-NepplyDesktopRuntimeRow -Tag $ReleaseTag -Started $started -Commit $git.Commit -WindowsBuild $windowsBuild -Accepted $accepted `
        -SummaryHash (Get-FileHash -LiteralPath $summaryPath -Algorithm SHA256).Hash
    Set-Content -LiteralPath (Join-Path $runRoot 'checklist-row.md') -Value $row -Encoding utf8
}
Write-Output "Summary: $summaryPath"
Write-Output 'Row for docs/release/PUBLIC_RELEASE_CHECKLIST.md:'
Write-Output $row
if ($desktopLeftHidden) {
    Write-Output 'DESKTOP ICONS ARE STILL HIDDEN. Right-click the Desktop, then View > Show desktop icons. If that is already ticked, restart Windows Explorer from Task Manager.'
    exit $ExitDesktopLeftHidden
}
if (-not $accepted) { Write-Output 'NOT ACCEPTED. See the summary. Do not use this run as evidence for a tag.'; exit $ExitFailed }
Write-Output 'ACCEPTED for this commit and Windows build only. This is not installer or Store acceptance.'
exit $ExitPassed
