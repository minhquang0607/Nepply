# Nepply ships the .NET runtime inside the app, so a Windows Update or .NET patch never
# reaches users until the pin in Directory.Build.props moves and a new build ships.
# Input is Microsoft's channel feed (release-metadata/<channel>/releases.json).
function Get-NepplyRuntimePin {
    param([Parameter(Mandatory)][string]$Repository)
    [xml]$props = Get-Content -LiteralPath (Join-Path $Repository 'Directory.Build.props') -Raw
    $pin = [string]$props.Project.PropertyGroup.NepplyRuntimeVersion
    if ($pin -notmatch '^\d+\.\d+\.\d+$') { throw "NepplyRuntimeVersion '$pin' is not an exact runtime version." }
    $pin
}

function Assert-RuntimeCurrency {
    param([Parameter(Mandatory)][System.Collections.IDictionary]$Feed, [Parameter(Mandatory)][string]$Pinned)
    if ($Pinned -notmatch '^(\d+)\.(\d+)\.(\d+)$') { throw "Pinned runtime '$Pinned' is not an exact version." }
    $pinnedVersion = [version]$Pinned
    $channel = "$($pinnedVersion.Major).$($pinnedVersion.Minor)"
    if ([string]$Feed['channel-version'] -cne $channel) { throw "Feed is for channel '$($Feed['channel-version'])', not $channel." }
    if ([string]$Feed['support-phase'] -ceq 'eol') { throw ".NET $channel is out of support; move to a supported channel." }
    $releases = @($Feed['releases'])
    if ($releases.Count -eq 0) { throw 'Feed lists no releases.' }

    $known = $false
    $newer = [Collections.Generic.List[object]]::new()
    foreach ($release in $releases) {
        if ($release -isnot [System.Collections.IDictionary]) { throw 'Malformed release entry.' }
        $runtime = [string]$release['runtime']?['version']
        $desktop = [string]$release['windowsdesktop']?['version']
        if ($runtime -notmatch '^\d+\.\d+\.\d+$') { continue } # Previews and release candidates.
        if ($desktop -cne $runtime) { throw "Release $runtime has no matching Windows Desktop runtime." }
        if ($release['security'] -isnot [bool]) { throw "Release $runtime does not state whether it is a security release." }
        $version = [version]$runtime
        if ($version -eq $pinnedVersion) { $known = $true }
        elseif ($version -gt $pinnedVersion) { $newer.Add($release) }
    }
    if (-not $known) { throw "Pinned runtime $Pinned is not a released $channel runtime." }

    $security = @($newer | Where-Object { $_['security'] })
    if ($security.Count) {
        $lines = foreach ($release in $security) {
            $cves = @($release['cve-list'] | ForEach-Object { $_['cve-id'] }) -join ', '
            "  $($release['runtime']['version']) ($($release['release-date'])): $(if ($cves) { $cves } else { 'no CVE list' })"
        }
        throw ("Bundled runtime $Pinned misses security releases. Raise NepplyRuntimeVersion in Directory.Build.props:`n" + ($lines -join "`n"))
    }
    if ($newer.Count) {
        $latest = ($newer | ForEach-Object { [version]$_['runtime']['version'] } | Sort-Object -Descending)[0]
        return "Runtime $Pinned has no missing security releases; $latest is newer without security fixes."
    }
    "Runtime $Pinned is the latest $channel release."
}
