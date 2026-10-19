using System.IO;
using System.Security.Cryptography;
using Nepply.Modules.Updates;

internal static class UpdateSignatureTests
{
    // Expected values come from scripts/Get-SignerPolicy.ps1 on the same OS-trusted signed file.
    internal static void Run(string output, string signedFile, string commonName, string organization, string issuerSpkiSha256, Action<string, bool> check)
    {
        var approved = new SignerRule(commonName, organization, [issuerSpkiSha256]);
        bool verified = false;
        try { AuthenticodeVerifier.Verify(signedFile, [approved]); verified = true; }
        catch (CryptographicException) { }
        check("Authenticode accepts OS-trusted signer matching publisher and issuer pin", verified);
        bool wrongIssuer = false;
        try { AuthenticodeVerifier.Verify(signedFile, [approved with { IssuerSpkiSha256 = [new string('0', 64)] }]); }
        catch (CryptographicException) { wrongIssuer = true; }
        check("Authenticode refuses unpinned issuing CA", wrongIssuer);
        bool wrongPublisher = false;
        try { AuthenticodeVerifier.Verify(signedFile, [approved with { CommonName = commonName + " Impostor" }]); }
        catch (CryptographicException) { wrongPublisher = true; }
        check("Authenticode refuses different publisher name", wrongPublisher);
        string unsigned = Path.Combine(output, "unsigned-fixture.exe");
        File.WriteAllText(unsigned, "This is not executable code.");
        bool refused = false;
        try { AuthenticodeVerifier.Verify(unsigned, [approved]); }
        catch (CryptographicException) { refused = true; }
        check("Authenticode refuses unsigned invalid executable", refused);
        string tampered = Path.Combine(output, "tampered-fixture.exe");
        File.Copy(signedFile, tampered, false);
        using (var file = new FileStream(tampered, FileMode.Open, FileAccess.ReadWrite))
        { file.Position = Math.Min(4096, file.Length - 1); int original = file.ReadByte(); file.Position--; file.WriteByte((byte)(original ^ 1)); }
        bool mismatch = false;
        try { AuthenticodeVerifier.Verify(tampered, [approved]); }
        catch (CryptographicException) { mismatch = true; }
        check("Authenticode refuses tampered signed executable", mismatch);
    }
}
