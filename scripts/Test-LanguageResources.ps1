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
    $keyCount = 0
    # Vietnamese is one of eight languages: user-facing text lives only in Strings.json.
    # Allowed: the bilingual settings search index and each language's own name.
    $vietnameseAllowed = @('Features/Search/SettingsSearch.cs', 'Presentation/Localization/LocalizationService.cs')
    $vietnameseLiteral = '"(?:[^"\\\r\n]|\\.)*[àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđĐ](?:[^"\\\r\n]|\\.)*"'
    $keyedCall = '(?:FromKey|LocalizationService\.Get|WithUserMessage|ShowActionNotice|DismissActionNotice)\(\s*"([A-Za-z][A-Za-z0-9_]*)"'
    foreach ($file in Get-ChildItem -LiteralPath $sourceRoot -Recurse -File) {
        if ($file.Extension -notin '.cs','.xaml' -or $file.FullName -match '[\\/](bin|obj)[\\/]') { continue }
        $text = [IO.File]::ReadAllText($file.FullName)
        foreach ($match in [regex]::Matches($text,'Loc\.([A-Za-z][A-Za-z0-9_]*)')) {
            if (-not $keys.Contains($match.Groups[1].Value)) { throw "Missing resource $($match.Value) in $($file.FullName)" }
            $consumerCount++
        }
        if ($file.Extension -ne '.cs') { continue }
        foreach ($match in [regex]::Matches($text,$keyedCall)) {
            if (-not $keys.Contains($match.Groups[1].Value)) { throw "Missing resource key `"$($match.Groups[1].Value)`" in $($file.FullName)" }
            $keyCount++
        }
        $relative = [IO.Path]::GetRelativePath($sourceRoot,$file.FullName).Replace('\','/')
        if ($relative -notin $vietnameseAllowed -and ($found = [regex]::Match($text,$vietnameseLiteral)).Success) {
            throw "Hard-coded Vietnamese text in ${relative}: $($found.Value). Add a Strings.json key instead."
        }
    }
    Write-Output "PASS: $($keys.Count) resources × 8 languages; $consumerCount literal Loc.* references; $keyCount literal resource keys in code."
    Write-Output 'PASS: no hard-coded Vietnamese text outside Strings.json and the allowlisted search index/language names.'
    Write-Output 'Static structure only; dynamic lookup, translation quality and rendered UI still require tests.'
}
finally { $document.Dispose() }
