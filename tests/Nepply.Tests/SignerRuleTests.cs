using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Nepply.Modules.Updates;

// In-memory certificates only: nothing touches a certificate store or trust.
internal static class SignerRuleTests
{
    internal static void Run(Action<string, bool> check)
    {
        using var caKey = RSA.Create(2048);
        using var ca = Authority("CN=Fixture Issuing CA, O=Fixture CA", caKey);
        using var leaf = Leaf("CN=Nepply Publisher, O=Nepply Publisher, C=VN", ca);
        using var renewed = Leaf("CN=Nepply Publisher, O=Nepply Publisher, L=Hanoi, C=VN", ca);
        string pin = SignerRule.SpkiSha256(ca);
        var rule = new SignerRule("Nepply Publisher", "Nepply Publisher", [pin]);
        check("Signer rule well-formed with publisher and issuer pin", rule.IsWellFormed);
        check("Signer rule accepts approved publisher from pinned issuer", rule.Matches(leaf, ca));
        check("Signer rule accepts renewed leaf from same issuer key", rule.Matches(renewed, ca));
        check("Signer rule ignores issuer pin letter case", new SignerRule("Nepply Publisher", "Nepply Publisher", [pin.ToLowerInvariant()]).Matches(leaf, ca));

        using var reissuedCa = Authority("CN=Fixture Issuing CA, O=Fixture CA", caKey);
        check("Signer rule survives issuing CA reissue with same key", rule.Matches(leaf, reissuedCa));

        check("Signer rule refuses different common name", !new SignerRule("Other Publisher", "Nepply Publisher", [pin]).Matches(leaf, ca));
        check("Signer rule refuses different organization", !new SignerRule("Nepply Publisher", "Other", [pin]).Matches(leaf, ca));
        check("Signer rule refuses case-variant publisher", !new SignerRule("nepply publisher", "Nepply Publisher", [pin]).Matches(leaf, ca));

        using var otherKey = RSA.Create(2048);
        using var impostorCa = Authority("CN=Fixture Issuing CA, O=Fixture CA", otherKey);
        using var impostor = Leaf("CN=Nepply Publisher, O=Nepply Publisher, C=VN", impostorCa);
        check("Signer rule refuses same names from unpinned CA key", !rule.Matches(impostor, impostorCa));
        var rollover = new SignerRule("Nepply Publisher", "Nepply Publisher", [pin, SignerRule.SpkiSha256(impostorCa)]);
        check("Signer rule accepts each listed issuer during rollover", rollover.Matches(leaf, ca) && rollover.Matches(impostor, impostorCa));

        using var unrelatedCa = Authority("CN=Unrelated CA", caKey);
        check("Signer rule refuses issuer that did not issue the leaf", !rule.Matches(leaf, unrelatedCa));

        using var duplicate = Leaf("CN=Nepply Publisher, CN=Nepply Publisher, O=Nepply Publisher", ca);
        check("Signer rule refuses repeated common name", !rule.Matches(duplicate, ca));
        using var multiValued = Leaf("CN=Nepply Publisher + O=Nepply Publisher", ca);
        check("Signer rule refuses multi-valued name element", !rule.Matches(multiValued, ca));
        using var missing = Leaf("CN=Nepply Publisher", ca);
        check("Signer rule refuses leaf without organization", !rule.Matches(missing, ca));

        check("Signer rule malformed without issuer pin", !new SignerRule("Nepply Publisher", "Nepply Publisher", []).IsWellFormed);
        check("Signer rule malformed with short pin", !new SignerRule("Nepply Publisher", "Nepply Publisher", ["ABCD"]).IsWellFormed);
        check("Signer rule malformed with non-hex pin", !new SignerRule("Nepply Publisher", "Nepply Publisher", [new string('G', 64)]).IsWellFormed);
        check("Signer rule malformed without publisher", !new SignerRule(" ", "Nepply Publisher", [pin]).IsWellFormed);
        bool malformed = false;
        try { AuthenticodeVerifier.Verify("not-used.exe", [new SignerRule("Nepply Publisher", "Nepply Publisher", [])]); }
        catch (InvalidOperationException) { malformed = true; }
        check("Update fails closed with malformed signer policy", malformed);
    }

    private static X509Certificate2 Authority(string subject, RSA key)
    {
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
    }

    private static X509Certificate2 Leaf(string subject, X509Certificate2 issuer)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(new X500DistinguishedName(subject), key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.3")], false));
        return request.Create(issuer, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddDays(1), RandomNumberGenerator.GetBytes(16));
    }
}
