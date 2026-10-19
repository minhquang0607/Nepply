using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Nepply.Modules.Updates;

/// <summary>
/// Approved update publisher: the leaf's exact CN and O, issued directly by a CA
/// whose public key is pinned. Leaf certificates rotate (renewals, short-lived
/// cloud certificates), so they are never pinned themselves. Pinning the issuer's
/// key rather than its certificate survives CA reissue and cross-signed chains.
/// </summary>
internal sealed record SignerRule(string CommonName, string Organization, IReadOnlyList<string> IssuerSpkiSha256)
{
    private const string CommonNameOid = "2.5.4.3", OrganizationOid = "2.5.4.10";

    internal bool IsWellFormed => !string.IsNullOrWhiteSpace(CommonName) && !string.IsNullOrWhiteSpace(Organization)
        && IssuerSpkiSha256.Count > 0 && IssuerSpkiSha256.All(pin => pin.Length == 64 && pin.All(char.IsAsciiHexDigit));

    internal bool Matches(X509Certificate2 leaf, X509Certificate2 issuer)
    {
        // The provider chain must really pair this leaf with its immediate issuer.
        if (!leaf.IssuerName.RawData.AsSpan().SequenceEqual(issuer.SubjectName.RawData)) return false;
        if (SingleAttribute(leaf.SubjectName, CommonNameOid) != CommonName || SingleAttribute(leaf.SubjectName, OrganizationOid) != Organization) return false;
        string pin = SpkiSha256(issuer);
        return IssuerSpkiSha256.Any(approved => string.Equals(approved, pin, StringComparison.OrdinalIgnoreCase));
    }

    internal static string SpkiSha256(X509Certificate2 certificate) =>
        Convert.ToHexString(SHA256.HashData(certificate.PublicKey.ExportSubjectPublicKeyInfo()));

    // Null when absent, repeated or inside a multi-valued RDN: ambiguous names never match.
    private static string? SingleAttribute(X500DistinguishedName name, string oid)
    {
        string? value = null;
        foreach (var rdn in name.EnumerateRelativeDistinguishedNames())
        {
            if (rdn.HasMultipleElements) return null;
            if (rdn.GetSingleElementType().Value != oid) continue;
            if (value != null) return null;
            value = rdn.GetSingleElementValue();
        }
        return value;
    }
}
