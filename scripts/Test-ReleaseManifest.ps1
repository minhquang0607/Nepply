$ErrorActionPreference='Stop'
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('Nepply-manifest-test-'+[guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path $fixture
$payload=Join-Path $fixture 'sample.txt'
$extra=Join-Path $fixture 'extra.txt'
$manifest=Join-Path $fixture 'release-manifest.json'
$nested=Join-Path $fixture 'licenses'
$notice=Join-Path $nested 'Thông báo.txt'
$script=Join-Path $PSScriptRoot 'Release-Manifest.ps1'
$checks=0
function Assert-Rejected([scriptblock]$Action,[string]$Name) {
    $rejected=$false
    try { & $Action | Out-Null } catch { $rejected=$true }
    if(-not $rejected) {throw "FAIL: $Name"}
    Write-Output "PASS: $Name"
    $script:checks++
}
try {
    Assert-Rejected { & $script -Directory $fixture -Mode Create } 'empty release'
    [IO.File]::WriteAllText($payload,'original')
    $null=New-Item -ItemType Directory -Path $nested
    [IO.File]::WriteAllText($notice,'fixture license')
    Assert-Rejected { & $script -Directory $fixture } 'missing manifest'
    & $script -Directory $fixture -Mode Create
    & $script -Directory $fixture
    $checks++
    $baseline=[IO.File]::ReadAllText($manifest)
    $parsed=$baseline | ConvertFrom-Json
    if(@($parsed.Files | Where-Object Path -eq 'licenses/Thông báo.txt').Count -ne 1) {throw 'Nested Unicode path missing'}
    $checks++
    $parsed.SchemaVersion=2
    [IO.File]::WriteAllText($manifest,($parsed | ConvertTo-Json -Depth 5))
    Assert-Rejected { & $script -Directory $fixture } 'unknown manifest schema'
    $parsed=$baseline | ConvertFrom-Json
    $parsed.Files[1]=$parsed.Files[0]
    [IO.File]::WriteAllText($manifest,($parsed | ConvertTo-Json -Depth 5))
    Assert-Rejected { & $script -Directory $fixture } 'duplicate manifest identity'
    [IO.File]::WriteAllText($manifest,$baseline)
    Assert-Rejected { & $script -Directory $fixture -Mode Create } 'baseline overwrite'
    [IO.File]::WriteAllText($payload,'modified')
    Assert-Rejected { & $script -Directory $fixture } 'same-size modified payload'
    [IO.File]::WriteAllText($payload,'original')
    [IO.File]::WriteAllText($extra,'extra')
    Assert-Rejected { & $script -Directory $fixture } 'unlisted file'
    Remove-Item -LiteralPath $extra
    Remove-Item -LiteralPath $payload
    Assert-Rejected { & $script -Directory $fixture } 'missing payload'
    Write-Output "RESULT: $checks manifest checks passed; fixture data only, no application execution."
} finally {
    foreach($path in @($payload,$extra,$manifest,$notice)) {
        if(Test-Path -LiteralPath $path -PathType Leaf) {Remove-Item -LiteralPath $path}
    }
    # Non-recursive: never remove unexpected fixture contents.
    if(Test-Path -LiteralPath $nested -PathType Container) {Remove-Item -LiteralPath $nested}
    Remove-Item -LiteralPath $fixture
}
