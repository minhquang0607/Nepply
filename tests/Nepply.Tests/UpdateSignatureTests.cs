using System.IO;
using System.Security.Cryptography;
using Nepply.Modules.Updates;

internal static class UpdateSignatureTests
{
    internal static void Run(string output, string signedFile, string approvedFingerprint, Action<string, bool> check)
    {
        bool verified = false;
        try { AuthenticodeVerifier.Verify(signedFile, [approvedFingerprint]); verified = true; }
        catch (CryptographicException) { }
        check("Authenticode accepts OS-trusted embedded signer matching SHA256 pin", verified);
        bool wrong = false;
        try { AuthenticodeVerifier.Verify(signedFile, [new string('0', 64)]); }
        catch (CryptographicException) { wrong = true; }
        check("Authenticode refuses different signer pin", wrong);
        string unsigned = Path.Combine(output, "unsigned-fixture.exe");
        File.WriteAllText(unsigned, "This is not executable code.");
        bool refused = false;
        try { AuthenticodeVerifier.Verify(unsigned, [approvedFingerprint]); }
        catch (CryptographicException) { refused = true; }
        check("Authenticode refuses unsigned invalid executable", refused);
        string tampered = Path.Combine(output, "tampered-fixture.exe");
        File.Copy(signedFile, tampered, false);
        using (var file = new FileStream(tampered, FileMode.Open, FileAccess.ReadWrite))
        { file.Position = Math.Min(4096, file.Length - 1); int original = file.ReadByte(); file.Position--; file.WriteByte((byte)(original ^ 1)); }
        bool mismatch = false;
        try { AuthenticodeVerifier.Verify(tampered, [approvedFingerprint]); }
        catch (CryptographicException) { mismatch = true; }
        check("Authenticode refuses tampered signed executable", mismatch);
    }
}
