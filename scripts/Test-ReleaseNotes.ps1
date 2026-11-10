#requires -Version 7.4
# Checks every packaging/release-notes/<version>.json. With -RequireCurrent, the project
# version must have one: release.yml attaches it to the release as release-notes.json.
param([switch]$RequireCurrent)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Release-Notes.ps1')
$repo = Split-Path $PSScriptRoot -Parent
$folder = Join-Path $repo 'packaging/release-notes'
$files = @(Get-ChildItem -LiteralPath $folder -Filter *.json -File)
foreach ($file in $files) {
    $version = [IO.Path]::GetFileNameWithoutExtension($file.Name)
    if ($version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-[0-9A-Za-z.-]+)?$') { throw "Release notes file name is not a version: $($file.Name)" }
    try { $items = Assert-NepplyReleaseNotes -Bytes ([IO.File]::ReadAllBytes($file.FullName)) -Version $version }
    catch { throw "$($file.Name): $($_.Exception.Message)" }
    Write-Output "PASS $($file.Name): $items items in each of 8 languages"
}
if ($RequireCurrent) {
    [xml]$project = Get-Content (Join-Path $repo 'src/Nepply/Nepply.csproj') -Raw
    $current = [string]$project.Project.PropertyGroup.Version
    if (-not (Test-Path -LiteralPath (Join-Path $folder "$current.json"))) {
        throw "Missing packaging/release-notes/$current.json: write the update window summary for this release."
    }
    Write-Output "PASS release notes exist for $current"
}
Write-Output "RESULT: $($files.Count) release notes files checked. Translation quality is not checked."
