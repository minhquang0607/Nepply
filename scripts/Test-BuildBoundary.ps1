$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$projectFile = Join-Path $projectRoot 'src/Nepply/Nepply.csproj'
$sourceRoot = Split-Path $projectFile -Parent
# Evaluate only the first-party project. Never evaluate or execute a reference project.
$raw = & dotnet msbuild $projectFile -nologo '-getItem:Compile,Page,ApplicationDefinition,EmbeddedResource,Resource'
if ($LASTEXITCODE -ne 0) { throw 'Could not evaluate Nepply build inputs.' }
$items = ($raw -join "`n" | ConvertFrom-Json).Items
$count = 0
foreach ($kind in $items.PSObject.Properties) {
    foreach ($item in $kind.Value) {
        $path = [IO.Path]::GetFullPath($item.FullPath)
        if (-not $path.StartsWith($sourceRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw "External build input is not allowed: $path"
        }
        $relative = [IO.Path]::GetRelativePath($projectRoot, $path)
        if ($relative -match '(^|[\\/])(Research|thư viện cho AETHER|Tests|SensorDumper|artifacts|publish)([\\/]|$)') {
            throw "Non-production input included in Nepply: $relative"
        }
        $count++
    }
}
if ($count -eq 0) { throw 'Build input inspection returned no items.' }
Write-Output "PASS: $count first-party build inputs; no reference/research/test source in Nepply."

# SDK patch differences must not silently change the implicit single-file tool dependency.
$toolRaw = & dotnet msbuild $projectFile -nologo -target:PinNepplyILLinkPack -getItem:KnownILLinkPack
if ($LASTEXITCODE -ne 0) { throw 'Could not evaluate the ILLink build-tool pin.' }
$packs = ($toolRaw -join "`n" | ConvertFrom-Json).Items.KnownILLinkPack
$net10 = @($packs | Where-Object { $_.TargetFramework -eq 'net10.0' })
$lock = Get-Content -LiteralPath (Join-Path $sourceRoot 'packages.lock.json') -Raw | ConvertFrom-Json
$locked = $lock.dependencies.'net10.0-windows10.0.19041'.'Microsoft.NET.ILLink.Tasks'.resolved
if ($net10.Count -ne 1 -or !$locked -or $net10[0].ILLinkPackVersion -ne $locked) {
    throw 'ILLink build-tool pin and packages.lock.json must agree.'
}
Write-Output "PASS: ILLink build-tool pin matches locked dependency $locked."
