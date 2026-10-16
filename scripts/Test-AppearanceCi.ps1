param(
    [string]$Executable = 'tests/Nepply.Tests/bin/Release/net10.0-windows10.0.19041.0/win-x64/Nepply.Tests.exe',
    [string]$OutputDirectory = 'artifacts/ci-appearance',
    [ValidateRange(30,180)][int]$TimeoutSeconds = 120
)
$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path -LiteralPath $Executable).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
if ($output.Contains('"')) { throw 'Invalid output path.' }
New-Item -ItemType Directory -Path $output -Force | Out-Null
$stdout = Join-Path $output 'stdout.txt'
$stderr = Join-Path $output 'stderr.txt'
$process = Start-Process -FilePath $exe -ArgumentList @(('"' + $output + '"'),'--appearance-only') -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
try {
    if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
        # Only inspect the test process created above; never attach to the user's app.
        $stack = Join-Path $PSScriptRoot '../artifacts/ci-tools/dotnet-stack.exe'
        if (Test-Path -LiteralPath $stack) {
            & $stack report --process-id $process.Id | Out-File (Join-Path $output 'timeout-stacks.txt') -Encoding utf8
        }
        throw "Appearance fixture exceeded $TimeoutSeconds seconds; inspect stdout and timeout-stacks."
    }
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Appearance fixture failed with exit code $($process.ExitCode)." }
} finally {
    if (!$process.HasExited) { $process.Kill($true); $process.WaitForExit(5000) | Out-Null }
    $process.Dispose()
    if (Test-Path -LiteralPath $stdout) { Get-Content -LiteralPath $stdout }
    if (Test-Path -LiteralPath $stderr) { Get-Content -LiteralPath $stderr }
}
