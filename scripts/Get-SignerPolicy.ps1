#requires -Version 7
# Prints the updater SignerRule for an approved, already-signed Nepply Setup.
# Review the output before copying it into UpdatePolicy.Signers; this reads, never trusts.
param([Parameter(Mandatory)][string]$File)
$ErrorActionPreference = 'Stop'
$artifact = Get-Item -LiteralPath $File
if ($artifact.PSIsContainer -or ($artifact.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Expected a regular signed file.' }
$signature = Get-AuthenticodeSignature -LiteralPath $artifact.FullName
if ($signature.Status -ne 'Valid') { throw "Signature is not valid: $($signature.Status)" }
$leaf = $signature.SignerCertificate
$chain = [Security.Cryptography.X509Certificates.X509Chain]::new()
try {
    $chain.ChainPolicy.RevocationMode = 'Online'
    if (-not $chain.Build($leaf) -or $chain.ChainElements.Count -lt 2) { throw 'Could not build a trusted chain with an issuing CA.' }
    $issuer = $chain.ChainElements[1].Certificate
} finally { $chain.Dispose() }
function Get-SingleAttribute([Security.Cryptography.X509Certificates.X500DistinguishedName]$Name, [string]$Oid) {
    $values = @($Name.EnumerateRelativeDistinguishedNames() | Where-Object { -not $_.HasMultipleElements -and $_.GetSingleElementType().Value -eq $Oid } | ForEach-Object { $_.GetSingleElementValue() })
    if ($values.Count -ne 1) { throw "Leaf subject must contain exactly one $Oid." }
    $values[0]
}
$commonName = Get-SingleAttribute $leaf.SubjectName '2.5.4.3'
$organization = Get-SingleAttribute $leaf.SubjectName '2.5.4.10'
$pin = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($issuer.PublicKey.ExportSubjectPublicKeyInfo()))
[pscustomobject]@{ CommonName = $commonName; Organization = $organization; Issuer = $issuer.Subject; IssuerSpkiSha256 = $pin; LeafNotAfter = $leaf.NotAfter }
$escape = { param($value) $value.Replace('\', '\\').Replace('"', '\"') }
Write-Output "new(`"$(& $escape $commonName)`", `"$(& $escape $organization)`", [`"$pin`"]),"
