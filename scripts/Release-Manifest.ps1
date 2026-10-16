param(
    [Parameter(Mandatory)][string]$Directory,
    [ValidateSet('Create','Verify')][string]$Mode = 'Verify'
)
$ErrorActionPreference = 'Stop'
$root = Get-Item -LiteralPath $Directory
if (-not $root.PSIsContainer -or ($root.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
    throw 'Release directory must be a physical directory, not a link.'
}
$manifestPath = Join-Path $root.FullName 'release-manifest.json'
$files = [Collections.Generic.List[object]]::new()
$pending = [Collections.Generic.Stack[string]]::new()
$pending.Push($root.FullName)
while ($pending.Count -gt 0) {
    foreach ($item in Get-ChildItem -LiteralPath $pending.Pop() -Force) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "Release contains a link: $($item.Name)"
        }
        if ($item.PSIsContainer) { $pending.Push($item.FullName); continue }
        if ($item.FullName -eq $manifestPath) { continue }
        $files.Add([pscustomobject]@{
            Path = [IO.Path]::GetRelativePath($root.FullName,$item.FullName).Replace('\','/')
            Bytes = $item.Length
            SHA256 = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash
        })
    }
}
if ($files.Count -eq 0) { throw 'Release directory is empty.' }
$entries = @($files | Sort-Object Path)
if ($Mode -eq 'Create') {
    if (Test-Path -LiteralPath $manifestPath) {
        throw 'Manifest already exists. Review the changed release before explicitly removing its previous manifest.'
    }
    $json = [pscustomobject]@{ SchemaVersion=1; Files=$entries } | ConvertTo-Json -Depth 5
    # CreateNew prevents accidentally overwriting an existing baseline.
    $stream = [IO.File]::Open($manifestPath,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try {
        $bytes = [Text.UTF8Encoding]::new($false).GetBytes($json)
        $stream.Write($bytes,0,$bytes.Length)
    } finally { $stream.Dispose() }
    Write-Output "Created release manifest: $($entries.Count) files. This is an integrity baseline, not a signature or source attestation."
    return
}
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'Release manifest is missing.' }
if ((Get-Item -LiteralPath $manifestPath).Length -gt 4MB) { throw 'Release manifest exceeds 4 MiB.' }
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.SchemaVersion -ne 1 -or @($manifest.Files).Count -ne $entries.Count) {
    throw 'Release manifest schema or file count mismatch.'
}
$expected = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($entry in $manifest.Files) {
    if ([string]::IsNullOrWhiteSpace($entry.Path) -or $entry.SHA256 -notmatch '^[0-9a-fA-F]{64}$' -or
        $entry.Bytes -isnot [long] -and $entry.Bytes -isnot [int]) { throw 'Invalid release manifest entry.' }
    if (-not $expected.TryAdd($entry.Path,$entry)) { throw 'Duplicate release manifest entry.' }
}
foreach ($entry in $entries) {
    if (-not $expected.ContainsKey($entry.Path)) { throw "Unlisted release file: $($entry.Path)" }
    $baseline = $expected[$entry.Path]
    if ($baseline.Bytes -ne $entry.Bytes -or $baseline.SHA256 -ne $entry.SHA256) {
        throw "Release file changed: $($entry.Path)"
    }
}
Write-Output "Verified release manifest: $($entries.Count) files."
