function Assert-ReleaseMetadata {
    param(
        [Parameter(Mandatory)][object[]]$Artifacts,
        [Parameter(Mandatory)][string]$ExpectedVersion,
        [switch]$RequireSigned,
        [switch]$RequireStable,
        [string[]]$AllowedSignerThumbprints
    )
    if ($ExpectedVersion -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') { throw 'Expected release version is not valid.' }
    if ($RequireStable -and $ExpectedVersion.Contains('-')) { throw 'Stable release blocked: prerelease version.' }
    if ($Artifacts.Count -ne 2) { throw 'Release requires exactly application and installer metadata.' }
    if ($RequireSigned -or $RequireStable) {
        Assert-SignerPolicy -AllowedSignerThumbprints $AllowedSignerThumbprints
    }
    foreach ($artifact in $Artifacts) {
        if (-not $artifact.File -or $artifact.Bytes -le 0 -or $artifact.SHA256 -notmatch '^[a-fA-F0-9]{64}$') { throw 'Incomplete artifact metadata.' }
        $version=([string]$artifact.Version).Trim().Split('+')[0]
        if ($version -cne $ExpectedVersion) { throw "Release version mismatch: $($artifact.File) has $version, expected $ExpectedVersion." }
        if (($RequireSigned -or $RequireStable) -and $artifact.Signature -ne 'Valid') { throw "Release blocked: missing/invalid signature for $($artifact.File)." }
        if ($RequireSigned -or $RequireStable) { Assert-ArtifactSigner -Artifact $artifact -AllowedSignerThumbprints $AllowedSignerThumbprints }
    }
}

function Assert-SignerPolicy {
    param([string[]]$AllowedSignerThumbprints)
    if (!$AllowedSignerThumbprints -or @($AllowedSignerThumbprints | Where-Object { $_ -notmatch '^[0-9a-fA-F]{40}$' }).Count) {
        throw 'Signed release requires an explicit reviewed certificate allowlist.'
    }
}

function Assert-ArtifactSigner {
    param([Parameter(Mandatory)]$Artifact,[string[]]$AllowedSignerThumbprints)
    Assert-SignerPolicy -AllowedSignerThumbprints $AllowedSignerThumbprints
    if ($Artifact.Signature -ne 'Valid' -or $Artifact.SignerThumbprint -notin $AllowedSignerThumbprints -or
        $Artifact.TimestampThumbprint -notmatch '^[0-9a-fA-F]{40}$') {
        throw "Signature, approved signer or timestamp missing for $($Artifact.File)."
    }
}
