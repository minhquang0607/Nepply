$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Test-ReleaseMetadata.ps1')
$fixtures=@(
    @{File='Nepply.exe';Bytes=100;Version='2.5.0-beta.1';SHA256=('a'*64);Signature='NotSigned'},
    @{File='Setup.exe';Bytes=100;Version='2.5.0-beta.1 ';SHA256=('b'*64);Signature='NotSigned'}
)
Assert-ReleaseMetadata -Artifacts $fixtures -ExpectedVersion '2.5.0-beta.1'
$checks=1
foreach($scenario in @('mismatched-installer','missing-artifact','empty-file','invalid-hash','unsigned','beta-as-stable')) {
    $items=@($fixtures | ForEach-Object { $_.Clone() })
    switch($scenario) {
        'mismatched-installer' { $items[1].Version='2.4.1' }
        'missing-artifact' { $items=@($items[0]) }
        'empty-file' { $items[0].Bytes=0 }
        'invalid-hash' { $items[0].SHA256='invalid' }
    }
    $rejected=$false
    try { Assert-ReleaseMetadata -Artifacts $items -ExpectedVersion '2.5.0-beta.1' -RequireSigned:($scenario -eq 'unsigned') -RequireStable:($scenario -eq 'beta-as-stable') }
    catch { $rejected=$true }
    if (-not $rejected) { throw "FAIL release gate accepted $scenario" }
    Write-Output "PASS release gate rejects $scenario"
    $checks++
}
$publisher='C'*40
$signed=@($fixtures | ForEach-Object { $item=$_.Clone();$item.Version='2.5.0';$item.Signature='Valid';$item.SignerThumbprint=$publisher;$item.TimestampThumbprint='D'*40;$item })
Assert-ReleaseMetadata -Artifacts $signed -ExpectedVersion '2.5.0' -RequireStable -AllowedSignerThumbprints $publisher
$checks++
foreach($scenario in @('wrong-publisher','no-timestamp','no-policy','invalid-policy','uninstaller-wrong-publisher')) {
    $items=@($signed | ForEach-Object {$_.Clone()})
    $policy=@($publisher)
    switch($scenario) {
        'wrong-publisher' {$items[0].SignerThumbprint='E'*40}
        'no-timestamp' {$items[1].TimestampThumbprint=$null}
        'no-policy' {$policy=@()}
        'invalid-policy' {$policy=@('not-a-certificate')}
        'uninstaller-wrong-publisher' {$items[1].SignerThumbprint='E'*40}
    }
    $rejected=$false
    try {
        if($scenario -eq 'uninstaller-wrong-publisher') {Assert-ArtifactSigner -Artifact $items[1] -AllowedSignerThumbprints $policy}
        else {Assert-ReleaseMetadata -Artifacts $items -ExpectedVersion '2.5.0' -RequireSigned -AllowedSignerThumbprints $policy}
    } catch {$rejected=$true}
    if(!$rejected) {throw "FAIL release gate accepted $scenario"}
    Write-Output "PASS release gate rejects $scenario"
    $checks++
}
Assert-ReleaseMetadata -Artifacts $signed -ExpectedVersion '2.5.0' -RequireSigned -AllowedSignerThumbprints @(('E'*40),$publisher)
$checks++
Write-Output "RESULT: $checks release metadata fixture checks passed. Signatures in fixtures are simulated, not certificate verification."
