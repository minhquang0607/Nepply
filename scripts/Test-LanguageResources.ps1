#requires -Version 7.4
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$sourceRoot = Join-Path $projectRoot 'src/Nepply'
$document = [System.Text.Json.JsonDocument]::Parse([IO.File]::ReadAllText((Join-Path $sourceRoot 'Resources/Strings.json')))
$keys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
try {
    foreach ($entry in $document.RootElement.EnumerateObject()) {
        if (-not $keys.Add($entry.Name)) { throw "Duplicate resource: $($entry.Name)" }
        $values = @($entry.Value.EnumerateArray())
        if ($values.Count -ne 8) { throw "Expected 8 languages: $($entry.Name)" }
        $arity = [Text.CompositeFormat]::Parse($values[0].GetString()).MinimumArgumentCount
        foreach ($value in $values) {
            $textValue = $value.GetString()
            if ([string]::IsNullOrWhiteSpace($textValue)) { throw "Empty resource: $($entry.Name)" }
            if ([Text.CompositeFormat]::Parse($textValue).MinimumArgumentCount -ne $arity) { throw "Format arguments mismatch: $($entry.Name)" }
            if ($textValue -match '&#(?:\d+|x[\da-fA-F]+);') { throw "XML character reference in JSON text: $($entry.Name)" }
        }
    }
    $consumerCount = 0
    foreach ($file in Get-ChildItem -LiteralPath $sourceRoot -Recurse -File) {
        if ($file.Extension -notin '.cs','.xaml' -or $file.FullName -match '[\\/](bin|obj)[\\/]') { continue }
        foreach ($match in [regex]::Matches([IO.File]::ReadAllText($file.FullName),'Loc\.([A-Za-z][A-Za-z0-9_]*)')) {
            if (-not $keys.Contains($match.Groups[1].Value)) { throw "Missing resource $($match.Value) in $($file.FullName)" }
            $consumerCount++
        }
    }
    Write-Output "PASS: $($keys.Count) resources × 8 languages; $consumerCount literal Loc.* references."
    Write-Output 'Static structure only; dynamic lookup, translation quality and rendered UI still require tests.'
}
finally { $document.Dispose() }
