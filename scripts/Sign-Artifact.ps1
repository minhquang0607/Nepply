param(
    [Parameter(Mandatory)][string]$File,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-fA-F]{40}$')][string]$CertificateThumbprint,
    [Parameter(Mandatory)][uri]$TimestampUrl,
    [Parameter(Mandatory)][string]$SignTool
)
$ErrorActionPreference='Stop'
if($TimestampUrl.Scheme -ne 'https') { throw 'Use an HTTPS RFC3161 timestamp endpoint from your signing provider.' }
$artifact=Get-Item -LiteralPath $File
if($artifact.PSIsContainer -or ($artifact.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Expected a regular artifact file.' }
$tool=Get-Item -LiteralPath $SignTool
if((Get-AuthenticodeSignature -LiteralPath $tool.FullName).Status -ne 'Valid') { throw 'SignTool must have a valid Authenticode signature.' }
$certificate=Get-Item -LiteralPath "Cert:/CurrentUser/My/$CertificateThumbprint"
if(-not $certificate.HasPrivateKey -or $certificate.NotAfter -le (Get-Date) -or $certificate.NotBefore -gt (Get-Date)) { throw 'A currently valid signing certificate with accessible private key is required.' }
if(-not ($certificate.EnhancedKeyUsageList | Where-Object ObjectId -eq '1.3.6.1.5.5.7.3.3')) { throw 'Certificate is not enabled for code signing.' }
& $tool.FullName sign /sha1 $CertificateThumbprint /fd SHA256 /tr $TimestampUrl.AbsoluteUri /td SHA256 $artifact.FullName
if($LASTEXITCODE -ne 0) { throw 'Signing failed; do not publish this artifact.' }
& $tool.FullName verify /pa /all /v $artifact.FullName
if($LASTEXITCODE -ne 0) { throw 'Signature verification failed.' }
$signature=Get-AuthenticodeSignature -LiteralPath $artifact.FullName
if($signature.Status -ne 'Valid' -or -not $signature.TimeStamperCertificate -or $signature.SignerCertificate.Thumbprint -ine $CertificateThumbprint) { throw 'Signature, timestamp or expected publisher mismatch.' }
Write-Output 'Signature and timestamp verified. Regenerate the payload manifest after signing, before compiling the installer.'
