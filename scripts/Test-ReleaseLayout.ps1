$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Release-Layout.ps1')
$repo = Split-Path $PSScriptRoot -Parent
$root = New-NepplyArtifactDirectory -Repository $repo -Path ('artifacts/release-layout-tests-' + [Guid]::NewGuid().ToString('N'))
$passed = 0
function Check([string]$Name,[bool]$Condition) {
    if (-not $Condition) { throw "FAIL $Name" }
    $script:passed++
    Write-Output "PASS $Name"
}
function Reject([string]$Name,[scriptblock]$Action) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected=$true }
    Check $Name $rejected
}
$version = Get-NepplyReleaseVersion -Repository $repo
[xml]$guardianProject = Get-Content -LiteralPath (Join-Path $repo 'src/Nepply.DesktopGuardian/Nepply.DesktopGuardian.csproj') -Raw
Check 'Guardian project repeats the app version the app requires' ([string]$guardianProject.Project.PropertyGroup.Version -ceq $version.Version -and [string]$guardianProject.Project.PropertyGroup.FileVersion -ceq $version.FileVersion)
Check 'Single version source with semver and file version' ($version.Version -match '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$' -and $version.FileVersion -match '^\d+\.\d+\.\d+\.\d+$')
Reject 'Refuses to write to the artifacts root' { New-NepplyArtifactDirectory -Repository $repo -Path 'artifacts' }
Reject 'Refuses traversal outside artifacts' { New-NepplyArtifactDirectory -Repository $repo -Path 'artifacts/../../outside-nepply' }
Reject 'Refuses to overwrite an earlier output' { New-NepplyArtifactDirectory -Repository $repo -Path $root }
$absolute = New-NepplyArtifactDirectory -Repository $repo -Path (Join-Path $root 'absolute')
Check 'Accepts a contained absolute path' ($absolute -eq (Join-Path $root 'absolute'))
$fixture = Join-Path $root 'fixture'
New-Item -ItemType Directory -Path (Join-Path $fixture '.github/workflows'),(Join-Path $fixture 'bin'),(Join-Path $fixture 'obj') | Out-Null
[IO.File]::WriteAllText((Join-Path $fixture 'app.cs'),'// fixture')
[IO.File]::WriteAllText((Join-Path $fixture '.github/workflows/test.yml'),'name: fixture')
[IO.File]::WriteAllText((Join-Path $fixture 'bin/generated.txt'),'fixture')
[IO.File]::WriteAllText((Join-Path $fixture 'obj/generated.txt'),'fixture')
$copy = Join-Path $root 'copy'
Copy-NepplySourceTree -Source $fixture -Destination $copy
Check 'Keeps source code' (Test-Path -LiteralPath (Join-Path $copy 'app.cs'))
Check 'Keeps hidden workflow folders in source' (Test-Path -LiteralPath (Join-Path $copy '.github/workflows/test.yml'))
Check 'Excludes bin/obj' (-not (Test-Path -LiteralPath (Join-Path $copy 'bin')) -and -not (Test-Path -LiteralPath (Join-Path $copy 'obj')))
[IO.File]::WriteAllText((Join-Path $fixture 'fixture.key'),'fake-key-test-only')
Reject 'Refuses key files in the source bundle' { Copy-NepplySourceTree -Source $fixture -Destination (Join-Path $root 'rejected-copy') }
[xml]$manifest = Get-Content -LiteralPath (Join-Path $repo 'packaging/msix/AppxManifest.xml') -Raw
Check 'MSIX QA uses its own identity' ($manifest.Package.Identity.Name -ceq 'Nepply.Internal.Preview')
Check 'MSIX runs the executable at the right path' ($manifest.Package.Applications.Application.Executable -ceq 'App\Nepply.exe')
Check 'MSIX declares startup without enabling it' ($manifest.Package.Applications.Application.Extensions.Extension.StartupTask.Enabled -ceq 'false')
Check 'MSIX does not request admin' ($manifest.Package.Capabilities.Capability.Name -ceq 'runFullTrust')
$inno=Get-Content -LiteralPath (Join-Path $repo 'packaging/inno/Nepply.iss') -Raw
Check 'Inno takes the version from the build instead of a hardcoded beta' ($inno.Contains('AppVersion={#ReleaseVersion}') -and $inno.Contains('VersionInfoVersion={#ReleaseFileVersion}') -and $inno -notmatch '2\.5\.0-beta')
Write-Output "RESULT: $passed release layout checks passed. Not install/uninstall or Store acceptance."
