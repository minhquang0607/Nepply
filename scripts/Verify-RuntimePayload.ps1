param(
    [Parameter(Mandatory)][string]$Directory,
    [string]$Assets = 'src/Nepply/obj/project.assets.json',
    [string]$RuntimeConfigName = 'Nepply.runtimeconfig.json'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Test-RuntimePayload.ps1')
$repo = Split-Path $PSScriptRoot -Parent
[xml]$props = Get-Content -LiteralPath (Join-Path $repo 'Directory.Build.props') -Raw
$version = [string]$props.Project.PropertyGroup.NepplyRuntimeVersion
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Explicit runtime pin required.' }
$graph = Get-Content -LiteralPath (Join-Path $repo $Assets) -Raw | ConvertFrom-Json -AsHashtable
$packs = @{}
foreach ($name in @('Microsoft.NETCore.App.Runtime.win-x64','Microsoft.WindowsDesktop.App.Runtime.win-x64')) {
    foreach ($cache in $graph.packageFolders.Keys) {
        $path = Join-Path $cache ($name.ToLowerInvariant()+'/'+$version)
        if (Test-Path -LiteralPath $path -PathType Container) { $packs[$name]=$path; break }
    }
    if (-not $packs.ContainsKey($name)) { throw "Missing pinned runtime pack: $name/$version" }
}
$count = Assert-NepplyRuntimePayload -Directory $Directory -RuntimeConfig (Join-Path $Directory $RuntimeConfigName) -Version $version `
    -CorePack $packs['Microsoft.NETCore.App.Runtime.win-x64'] -DesktopPack $packs['Microsoft.WindowsDesktop.App.Runtime.win-x64']
Write-Output "PASS: $count runtime files match pinned $version packs; required Core/WPF files present. Multifile payload only, not runtime execution or vulnerability audit."
. (Join-Path $PSScriptRoot 'Get-WindowsSdkNoticePackage.ps1')
. (Join-Path $PSScriptRoot 'Test-SdkProjectionPayload.ps1')
$sdkKey=Get-WindowsSdkNoticePackage $graph
$sdkPack=$null
foreach ($cache in $graph.packageFolders.Keys) {
    $candidate=Join-Path $cache $sdkKey.ToLowerInvariant()
    if (Test-Path -LiteralPath $candidate -PathType Container) { $sdkPack=$candidate; break }
}
if (-not $sdkPack) { throw "Missing restored SDK projection pack: $sdkKey" }
Assert-SdkProjectionPayload -Directory $Directory -PackageDirectory $sdkPack
Write-Output "PASS: both WinRT projection DLLs match $sdkKey byte-for-byte."
