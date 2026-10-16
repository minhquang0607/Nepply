function Assert-NoticeInventory {
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary]$Inventory,
        [Parameter(Mandatory)][string[]]$ExpectedPackages,
        [Parameter(Mandatory)][string]$RuntimeVersion
    )
    if ($Inventory.Target -cne 'net10.0-windows10.0.19041.0/win-x64' -or $Inventory.RuntimeVersion -cne $RuntimeVersion) {
        throw 'Notice inventory target/runtime mismatch.'
    }
    if ($Inventory.Packages -isnot [array] -or $Inventory.Packages.Count -eq 0 -or $ExpectedPackages.Count -eq 0) {
        throw 'Notice inventory must contain packages.'
    }
    $remaining = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($expected in $ExpectedPackages) { if (-not $remaining.Add($expected)) { throw 'Duplicate expected package.' } }
    foreach ($package in $Inventory.Packages) {
        if ($package -isnot [System.Collections.IDictionary] -or $package.Package -isnot [string] -or
            $package.Package -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]*/[0-9][A-Za-z0-9.+-]*$' -or
            -not $remaining.Remove($package.Package)) { throw 'Unknown or duplicate notice package.' }
        if ($package.MissingLicenseText -isnot [bool] -or $package.MissingLicenseText) {
            throw "Missing or unresolved license text: $($package.Package)"
        }
        if ($package.Files -isnot [array] -or $package.Files.Count -eq 0 -or $package.NoticeEvidence -isnot [array]) {
            throw "Missing notice evidence: $($package.Package)"
        }
        $files = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($name in $package.Files) {
            if ($name -isnot [string] -or $name -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]*$' -or
                -not $files.Add($name)) { throw 'Invalid or duplicate notice filename.' }
        }
        foreach ($notice in $package.NoticeEvidence) {
            if ($notice -isnot [System.Collections.IDictionary] -or $notice.File -isnot [string] -or
                -not $files.Remove($notice.File) -or
                ($notice.Bytes -isnot [long] -and $notice.Bytes -isnot [int]) -or $notice.Bytes -le 0 -or
                $notice.SHA256 -isnot [string] -or $notice.SHA256 -notmatch '^[a-fA-F0-9]{64}$') {
                throw 'Invalid or duplicate notice file evidence.'
            }
        }
        if ($files.Count -ne 0) { throw 'Notice files are missing hash evidence.' }
    }
    if ($remaining.Count -ne 0) { throw 'Notice inventory omits resolved packages.' }
}
