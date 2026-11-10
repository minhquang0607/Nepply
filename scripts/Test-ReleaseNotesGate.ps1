#requires -Version 7.4
# Fixture checks for Release-Notes.ps1. Real files are checked by Test-ReleaseNotes.ps1.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Release-Notes.ps1')
function New-Notes { [ordered]@{ version='9.0.0'; notes=[ordered]@{ vi=@('a','b'); en=@('a','b'); ja=@('a','b'); de=@('a','b'); zh=@('a','b'); ru=@('a','b'); fr=@('a','b'); th=@('a','b') } } }
function Bytes($value) { [Text.Encoding]::UTF8.GetBytes(($value | ConvertTo-Json -Depth 5)) }
$checks = 0
if ((Assert-NepplyReleaseNotes -Bytes (Bytes (New-Notes)) -Version '9.0.0') -ne 2) { throw 'FAIL valid notes rejected' }
Write-Output 'PASS release notes gate accepts complete notes'; $checks++
foreach ($scenario in @('wrong-version','missing-language','extra-language','extra-key','uneven','empty-list','too-many','overlong','padded','control','not-text','bom','oversized')) {
    $notes = New-Notes; $bytes = $null
    switch ($scenario) {
        'wrong-version' { $notes.version = '9.0.1' }
        'missing-language' { $notes.notes.Remove('th') }
        'extra-language' { $notes.notes['es'] = @('a','b') }
        'extra-key' { $notes['body'] = 'x' }
        'uneven' { $notes.notes.de = @('a') }
        'empty-list' { foreach ($key in @($notes.notes.Keys)) { $notes.notes[$key] = @() } }
        'too-many' { foreach ($key in @($notes.notes.Keys)) { $notes.notes[$key] = @(1..21 | ForEach-Object { "item $_" }) } }
        'overlong' { $notes.notes.vi = @(('x' * 401), 'b') }
        'padded' { $notes.notes.vi = @(' a', 'b') }
        'control' { $notes.notes.vi = @("a`nb", 'b') }
        'not-text' { $notes.notes.vi = @(1, 'b') }
        'bom' { $bytes = [byte[]](@(0xEF,0xBB,0xBF) + (Bytes $notes)) }
        'oversized' { $bytes = [Text.Encoding]::UTF8.GetBytes(' ' * 65537) }
    }
    if ($null -eq $bytes) { $bytes = Bytes $notes }
    $rejected = $false
    try { Assert-NepplyReleaseNotes -Bytes $bytes -Version '9.0.0' | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw "FAIL release notes gate accepted $scenario" }
    Write-Output "PASS release notes gate rejects $scenario"; $checks++
}
Write-Output "RESULT: $checks release notes fixture checks passed."
