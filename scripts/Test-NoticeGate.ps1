$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Test-NoticeInventory.ps1')
function New-NoticeFixture {
    return @{ Target='net10.0-windows10.0.19041.0/win-x64'; RuntimeVersion='10.0.12'; Packages=@(
        @{Package='Example/1.0.0'; MissingLicenseText=$false; Files=@('LICENSE.txt');
          NoticeEvidence=@(@{File='LICENSE.txt';Bytes=123;SHA256=('a'*64)})}
    ) }
}
Assert-NoticeInventory -Inventory (New-NoticeFixture) -ExpectedPackages @('Example/1.0.0') -RuntimeVersion '10.0.12'
$checks=1
foreach ($scenario in @('wrong-target','wrong-runtime','empty','omitted','unknown','duplicate-package','unresolved','missing-status','string-status','empty-files','path-traversal','missing-evidence','duplicate-evidence','bad-hash','empty-text')) {
    $fixture=New-NoticeFixture
    $expected=@('Example/1.0.0')
    switch ($scenario) {
        'wrong-target' {$fixture.Target='linux-x64'}
        'wrong-runtime' {$fixture.RuntimeVersion='9.0.0'}
        'empty' {$fixture.Packages=@()}
        'omitted' {$expected+= 'Missing/1.0.0'}
        'unknown' {$fixture.Packages[0].Package='Other/1.0.0'}
        'duplicate-package' {$fixture.Packages+= $fixture.Packages[0]}
        'unresolved' {$fixture.Packages[0].MissingLicenseText=$true}
        'missing-status' {$fixture.Packages[0].Remove('MissingLicenseText')}
        'string-status' {$fixture.Packages[0].MissingLicenseText='false'}
        'empty-files' {$fixture.Packages[0].Files=@()}
        'path-traversal' {$fixture.Packages[0].Files=@('../LICENSE.txt')}
        'missing-evidence' {$fixture.Packages[0].NoticeEvidence=@()}
        'duplicate-evidence' {$fixture.Packages[0].NoticeEvidence+= $fixture.Packages[0].NoticeEvidence[0]}
        'bad-hash' {$fixture.Packages[0].NoticeEvidence[0].SHA256='bad'}
        'empty-text' {$fixture.Packages[0].NoticeEvidence[0].Bytes=0}
    }
    $rejected=$false
    try {Assert-NoticeInventory -Inventory $fixture -ExpectedPackages $expected -RuntimeVersion '10.0.12'}
    catch {$rejected=$true}
    if (-not $rejected) {throw "FAIL notice gate accepted $scenario"}
    Write-Output "PASS notice gate rejects $scenario"
    $checks++
}
Write-Output "RESULT: $checks notice inventory fixture checks passed. This does not validate legal obligations or actual release files."

. (Join-Path $PSScriptRoot 'Test-ExcludedPackages.ps1')
function New-ExcludedFixture([string]$Runtime, [string]$Native) {
    @{ targets=@{ 'net10.0-windows10.0.19041.0/win-x64'=@{
        'Mono.Posix.NETStandard/1.0.0'=@{ runtime=@{ $Runtime=@{} }; native=@{ $Native=@{} } } } } }
}
Assert-NepplyExcludedPackages -Graph (New-ExcludedFixture 'runtimes/win-x64/lib/netstandard2.0/_._' 'runtimes/win-x64/native/_._')
Write-Output 'PASS excluded package gate accepts placeholder-only assets'
foreach ($scenario in @('managed','native')) {
    $graph = if ($scenario -eq 'managed') { New-ExcludedFixture 'runtimes/win-x64/lib/netstandard2.0/Mono.Posix.NETStandard.dll' 'runtimes/win-x64/native/_._' }
        else { New-ExcludedFixture 'runtimes/win-x64/lib/netstandard2.0/_._' 'runtimes/win-x64/native/libMonoPosixHelper.dll' }
    $rejected=$false
    try {Assert-NepplyExcludedPackages -Graph $graph}
    catch {$rejected=$true}
    if (-not $rejected) {throw "FAIL excluded package gate accepted $scenario asset"}
    Write-Output "PASS excluded package gate rejects $scenario asset"
}
$leakRoot = Join-Path ([IO.Path]::GetTempPath()) ('nepply-excluded-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $leakRoot | Out-Null
try {
    Set-Content -LiteralPath (Join-Path $leakRoot 'libMonoPosixHelper.dll') -Value 'fixture'
    $rejected=$false
    try {Assert-NepplyExcludedPackages -Graph (New-ExcludedFixture 'runtimes/win-x64/lib/netstandard2.0/_._' 'runtimes/win-x64/native/_._') -Directory $leakRoot}
    catch {$rejected=$true}
    if (-not $rejected) {throw 'FAIL excluded package gate accepted leaked payload file'}
    Write-Output 'PASS excluded package gate rejects leaked payload file'
} finally { Remove-Item -LiteralPath $leakRoot -Recurse -Force }
$restored = Get-Content -LiteralPath (Join-Path (Split-Path $PSScriptRoot -Parent) 'src/Nepply/obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
Assert-NepplyExcludedPackages -Graph $restored
Write-Output 'PASS restored Windows graph ships no excluded package assets'
Write-Output 'RESULT: 5 excluded package checks passed.'
