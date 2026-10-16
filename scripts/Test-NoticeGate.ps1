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

. (Join-Path $PSScriptRoot 'Test-MonoPosixNoticeEvidence.ps1')
$monoHash='vSN/L1uaVwKsiLa95bYu2SGkF0iY3xMblTfxc8alSziPuVfJpj3geVqHGAA75J7cZkMuKpFVikz82Lo6y6LLdA=='
$noticeHash=(Get-FileHash -LiteralPath (Join-Path (Split-Path $PSScriptRoot -Parent) 'src/Nepply/Resources/Licenses/Mono.Posix-1.0.0-NOTICES.md') -Algorithm SHA256).Hash
$monoAssets=@(
    @{Asset='runtimes/win-x64/lib/netstandard2.0/Mono.Posix.NETStandard.dll';SHA256='BE0215D90462ADBAE5F03D6E5C3A969C9842A866B013DF7A395769C5C352FA7E'},
    @{Asset='runtimes/win-x64/native/MonoPosixHelper.dll';SHA256='A57141B26D6EA3326B165BDCB15CA2BCD34737370F52358E12FFC8DF41B92C62'},
    @{Asset='runtimes/win-x64/native/libMonoPosixHelper.dll';SHA256='B16F77C155A36559A44DA32E17E0BF63D899DB0BB48C6590BC8EBA7BB0FF87A8'}
)
Assert-MonoPosixNoticeEvidence -PackageSha512 $monoHash -Assets $monoAssets -NoticeSha256 $noticeHash
foreach ($scenario in @('package','asset','missing','extra','notice')) {
    $assets=@($monoAssets | ForEach-Object {$_.Clone()})
    $ph=$monoHash; $nh=$noticeHash
    switch ($scenario) {
        'package' {$ph='wrong'}
        'asset' {$assets[0].SHA256='0'*64}
        'missing' {$assets=@($assets | Select-Object -Skip 1)}
        'extra' {$assets+=$assets[0]}
        'notice' {$nh='0'*64}
    }
    $rejected=$false
    try {Assert-MonoPosixNoticeEvidence -PackageSha512 $ph -Assets $assets -NoticeSha256 $nh}
    catch {$rejected=$true}
    if (-not $rejected) {throw "FAIL Mono.Posix accepted $scenario"}
    Write-Output "PASS Mono.Posix rejects $scenario"
}
Write-Output 'RESULT: 6 Mono.Posix notice evidence checks passed.'
