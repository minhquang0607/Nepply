function Assert-MonoPosixNoticeEvidence {
    param(
        [Parameter(Mandatory)][string]$PackageSha512,
        [Parameter(Mandatory)][object[]]$Assets,
        [Parameter(Mandatory)][string]$NoticeSha256
    )
    # Chỉ dùng notice đã rà cho đúng payload Windows x64 1.0.0.
    # Hash không phải bằng chứng tái tạo binary từ source upstream.
    if ($PackageSha512 -cne 'vSN/L1uaVwKsiLa95bYu2SGkF0iY3xMblTfxc8alSziPuVfJpj3geVqHGAA75J7cZkMuKpFVikz82Lo6y6LLdA==') {
        throw 'Mono.Posix package identity changed; review its notices again.'
    }
    if ($NoticeSha256 -ine '470B218717689B7FE386BD6D52358741C0E5B12135072E22DE6F75D2A1680BF5') {
        throw 'Mono.Posix notice text changed; review required.'
    }
    $expected = @{
        'runtimes/win-x64/lib/netstandard2.0/Mono.Posix.NETStandard.dll' = 'BE0215D90462ADBAE5F03D6E5C3A969C9842A866B013DF7A395769C5C352FA7E'
        'runtimes/win-x64/native/MonoPosixHelper.dll' = 'A57141B26D6EA3326B165BDCB15CA2BCD34737370F52358E12FFC8DF41B92C62'
        'runtimes/win-x64/native/libMonoPosixHelper.dll' = 'B16F77C155A36559A44DA32E17E0BF63D899DB0BB48C6590BC8EBA7BB0FF87A8'
    }
    foreach ($asset in $Assets) {
        if (-not $expected.ContainsKey([string]$asset.Asset) -or $expected[$asset.Asset] -ine $asset.SHA256) {
            throw 'Mono.Posix runtime/native payload changed; review its notices again.'
        }
        $expected.Remove($asset.Asset)
    }
    if ($expected.Count -ne 0) { throw 'Mono.Posix payload evidence is incomplete.' }
}
