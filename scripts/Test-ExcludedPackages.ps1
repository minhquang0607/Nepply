# Packages in the restore graph that must never ship on Windows. Re-including one
# needs a fresh notice and provenance review, not just a lock-file update.
$NepplyExcludedPackages = @('Mono.Posix.NETStandard')

function Assert-NepplyExcludedPackages {
    param(
        [Parameter(Mandatory)][hashtable]$Graph,
        [string]$Directory
    )
    $target = $Graph.targets['net10.0-windows10.0.19041.0/win-x64']
    if (-not $target) { throw 'Restore the Windows x64 target first.' }
    foreach ($key in $target.Keys) {
        if ($key.Split('/')[0] -notin $NepplyExcludedPackages) { continue }
        $item = $target[$key]
        $shipped = @(@($item.runtime.Keys) + @($item.native.Keys) | Where-Object { $_ -and $_ -notmatch '/_\._$' })
        if ($shipped.Count -gt 0) { throw "Excluded package would ship on Windows: $key ($($shipped -join ', '))" }
    }
    if ($Directory) {
        $leaked = @(Get-ChildItem -LiteralPath $Directory -Recurse -File | Where-Object Name -Match '^(lib)?MonoPosixHelper\.dll$|^Mono\.Posix')
        if ($leaked.Count -gt 0) { throw "Excluded payload present: $($leaked.Name -join ', ')" }
    }
}
