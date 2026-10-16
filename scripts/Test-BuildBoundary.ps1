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
