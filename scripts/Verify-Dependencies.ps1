param([string]$Project = 'src/Nepply/Nepply.csproj', [string]$Report = 'artifacts/security/nuget-audit.json')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Test-NuGetAuditReport.ps1')
$projectPath = (Resolve-Path -LiteralPath $Project).Path
$reportPath = [IO.Path]::GetFullPath($Report)
New-Item -ItemType Directory -Path (Split-Path $reportPath -Parent) -Force | Out-Null
$result = & dotnet list $projectPath package --vulnerable --include-transitive --format json --output-version 1 --no-restore
$auditExit = $LASTEXITCODE
$json = $result -join [Environment]::NewLine
$json | Set-Content -LiteralPath $reportPath -Encoding utf8
if ($auditExit -ne 0) { throw "NuGet audit failed (exit $auditExit); no clean result may be inferred." }
Assert-NuGetAuditReport -Json $json -ExpectedProject $projectPath
Write-Output 'PASS: no known vulnerable dependencies reported by configured NuGet sources. This is not a full security audit.'
