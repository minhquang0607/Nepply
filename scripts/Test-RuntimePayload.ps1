function Assert-NepplyRuntimePayload {
    param(
        [Parameter(Mandatory)][string]$Directory,
        [Parameter(Mandatory)][string]$RuntimeConfig,
        [Parameter(Mandatory)][string]$CorePack,
        [Parameter(Mandatory)][string]$DesktopPack,
        [Parameter(Mandatory)][string]$Version
    )
    $ErrorActionPreference = 'Stop'
    $config = Get-Content -LiteralPath $RuntimeConfig -Raw | ConvertFrom-Json
    foreach ($name in @('Microsoft.NETCore.App','Microsoft.WindowsDesktop.App')) {
        $framework = @($config.runtimeOptions.includedFrameworks | Where-Object name -CEQ $name)
        if ($framework.Count -ne 1 -or $framework[0].version -cne $Version) { throw "Runtime config mismatch: $name" }
    }
    $required = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($name in @('coreclr.dll','System.Private.CoreLib.dll','PresentationFramework.dll','WindowsBase.dll')) { [void]$required.Add($name) }
    $checked = 0
    # Packs share facade filenames; the WPF profile may choose either pinned copy.
    # WindowsBase/PresentationFramework specifically must come from Desktop.
    $expected = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($pack in @($CorePack,$DesktopPack)) {
        foreach ($assetRoot in @('runtimes/win-x64/native','runtimes/win-x64/lib/net10.0')) {
            $origin = Join-Path $pack $assetRoot
            if (-not (Test-Path -LiteralPath $origin -PathType Container)) { continue }
            foreach ($asset in Get-ChildItem -LiteralPath $origin -File -Recurse) {
                $relative = [IO.Path]::GetRelativePath($origin,$asset.FullName)
                if ($pack -eq $CorePack -and $relative -in @('WindowsBase.dll','PresentationFramework.dll')) { continue }
                if (-not $expected.ContainsKey($relative)) { $expected[$relative] = [Collections.Generic.List[string]]::new() }
                $expected[$relative].Add($asset.FullName)
            }
        }
    }
    foreach ($relative in $expected.Keys) {
        $target = Join-Path $Directory $relative
        if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { continue }
        $actualHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
        $matched = $false
        foreach ($candidate in $expected[$relative]) {
            if ((Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash -ceq $actualHash) { $matched=$true; break }
        }
        if (-not $matched) { throw "Runtime asset mismatch: $relative" }
        [void]$required.Remove($relative)
        $checked++
    }
    if ($required.Count -ne 0) { throw "Required runtime files missing: $($required -join ', ')" }
    return $checked
}
