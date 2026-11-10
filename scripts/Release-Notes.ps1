# Rules for packaging/release-notes/<version>.json, the short "what's new" file that the
# update window shows in the app's language. The app (ReleaseSummaryFile.cs) accepts a
# subset of these rules; releases must meet all of them.
$script:NepplyNoteLanguages = @('vi','en','ja','de','zh','ru','fr','th')

function Assert-NepplyReleaseNotes {
    param([Parameter(Mandatory)][byte[]]$Bytes, [Parameter(Mandatory)][string]$Version)
    if ($Bytes.Length -eq 0 -or $Bytes.Length -gt 65536) { throw 'Release notes must be 1 byte to 64 KiB.' }
    if ($Bytes.Length -ge 3 -and $Bytes[0] -eq 0xEF -and $Bytes[1] -eq 0xBB -and $Bytes[2] -eq 0xBF) { throw 'Release notes must be UTF-8 without a byte order mark.' }
    $text = [Text.UTF8Encoding]::new($false, $true).GetString($Bytes)
    $document = [Text.Json.JsonDocument]::Parse($text)
    try {
        $root = $document.RootElement
        if ($root.ValueKind -ne 'Object') { throw 'Release notes must be a JSON object.' }
        $names = @($root.EnumerateObject() | ForEach-Object Name)
        if (($names | Sort-Object) -join ',' -cne 'notes,version') { throw 'Release notes must hold exactly "version" and "notes".' }
        $declared = $root.GetProperty('version')
        if ($declared.ValueKind -ne 'String' -or $declared.GetString() -cne $Version) { throw "Release notes declare '$declared', expected '$Version'." }
        $notes = $root.GetProperty('notes')
        if ($notes.ValueKind -ne 'Object') { throw '"notes" must be an object of languages.' }
        $languages = @($notes.EnumerateObject() | ForEach-Object Name)
        if (($languages | Sort-Object) -join ',' -cne (($script:NepplyNoteLanguages | Sort-Object) -join ',')) {
            throw "Release notes need exactly these languages: $($script:NepplyNoteLanguages -join ', ')."
        }
        $count = $null
        foreach ($language in $notes.EnumerateObject()) {
            if ($language.Value.ValueKind -ne 'Array') { throw "Language '$($language.Name)' must be a list." }
            $items = @($language.Value.EnumerateArray())
            if ($items.Count -lt 1 -or $items.Count -gt 20) { throw "Language '$($language.Name)' needs 1 to 20 items." }
            if ($null -eq $count) { $count = $items.Count }
            elseif ($items.Count -ne $count) { throw "Language '$($language.Name)' has $($items.Count) items, others have $count." }
            foreach ($item in $items) {
                if ($item.ValueKind -ne 'String') { throw "Language '$($language.Name)' has an item that is not text." }
                $value = $item.GetString()
                if ($value -cne $value.Trim() -or $value.Length -lt 1 -or $value.Length -gt 400) { throw "Language '$($language.Name)' has an empty, padded or overlong item." }
                if ($value.ToCharArray() | Where-Object { [char]::IsControl($_) }) { throw "Language '$($language.Name)' has a control character." }
            }
        }
        $count
    }
    finally { $document.Dispose() }
}
