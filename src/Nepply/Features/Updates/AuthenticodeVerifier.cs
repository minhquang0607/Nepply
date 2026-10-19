using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Nepply.Modules.Updates;

internal static class AuthenticodeVerifier
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FileInfo { public uint Size; [MarshalAs(UnmanagedType.LPWStr)] public string Path; public IntPtr Handle, Subject; }
    [StructLayout(LayoutKind.Sequential)]
    private struct TrustData
    {
        public uint Size; public IntPtr Callback, Sip; public uint Ui, Revocation, Union; public IntPtr File;
        public uint StateAction; public IntPtr State, Url; public uint Flags, Context; public IntPtr SignatureSettings;
    }
    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref TrustData data);
    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern IntPtr WTHelperProvDataFromStateData(IntPtr state);
    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern IntPtr WTHelperGetProvSignerFromChain(IntPtr provider, uint signer, [MarshalAs(UnmanagedType.Bool)] bool counterSigner, uint counterIndex);
    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern IntPtr WTHelperGetProvCertFromChain(IntPtr signer, uint index);
    // Prefix-only views of provider-owned structures. Never free these pointers;
    // WTD_STATEACTION_CLOSE releases the entire verified provider state.
    [StructLayout(LayoutKind.Sequential)]
    private struct ProviderSigner { public uint Size; public System.Runtime.InteropServices.ComTypes.FILETIME Time; public uint Count; public IntPtr Chain; }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProviderCertificate { public uint Size; public IntPtr Certificate; }
    [StructLayout(LayoutKind.Sequential)]
    private struct CertificateContext { public uint Encoding; public IntPtr Encoded; public uint Length; }
    internal static void Verify(string path, IReadOnlyCollection<SignerRule> signers)
    {
        if (signers.Count == 0) throw new InvalidOperationException("Signing policy has not been configured.");
        if (!signers.All(rule => rule.IsWellFormed)) throw new InvalidOperationException("Signing policy is malformed.");
        var file = new FileInfo { Size = (uint)Marshal.SizeOf<FileInfo>(), Path = path };
        IntPtr pointer = Marshal.AllocHGlobal(Marshal.SizeOf<FileInfo>());
        var action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
        var data = new TrustData { Size = (uint)Marshal.SizeOf<TrustData>(), Ui = 2, Revocation = 1, Union = 1, File = pointer, StateAction = 1, Flags = 0x80 };
        bool initialized = false;
        try
        {
            Marshal.StructureToPtr(file, pointer, false); initialized = true;
            if (WinVerifyTrust(new IntPtr(-1), ref action, ref data) != 0) throw new CryptographicException("Untrusted installer signature.");
            var provider = WTHelperProvDataFromStateData(data.State);
            var signerPointer = provider == IntPtr.Zero ? IntPtr.Zero : WTHelperGetProvSignerFromChain(provider, 0, false, 0);
            if (signerPointer == IntPtr.Zero) throw new CryptographicException("Missing verified signer.");
            var signer = Marshal.PtrToStructure<ProviderSigner>(signerPointer);
            // Leaf plus at least its issuer; a self-signed leaf has no CA to pin.
            if (signer.Count < 2 || signer.Chain == IntPtr.Zero) throw new CryptographicException("Missing signer chain.");
            using var leaf = ReadChainCertificate(signerPointer, 0);
            using var issuer = ReadChainCertificate(signerPointer, 1);
            if (!signers.Any(rule => rule.Matches(leaf, issuer))) throw new CryptographicException("Unexpected installer publisher.");
        }
        finally
        {
            if (initialized)
            {
                data.StateAction = 2;
                WinVerifyTrust(new IntPtr(-1), ref action, ref data);
                Marshal.DestroyStructure<FileInfo>(pointer);
            }
            Marshal.FreeHGlobal(pointer);
        }
    }
    // Copies the encoded certificate out of provider-owned memory before state close.
    private static X509Certificate2 ReadChainCertificate(IntPtr signer, uint index)
    {
        var entry = WTHelperGetProvCertFromChain(signer, index);
        if (entry == IntPtr.Zero) throw new CryptographicException("Missing certificate.");
        var certificate = Marshal.PtrToStructure<ProviderCertificate>(entry);
        if (certificate.Certificate == IntPtr.Zero) throw new CryptographicException("Missing certificate.");
        var context = Marshal.PtrToStructure<CertificateContext>(certificate.Certificate);
        if (context.Encoded == IntPtr.Zero || context.Length is 0 or > 65536) throw new CryptographicException("Invalid certificate size.");
        byte[] encoded = new byte[context.Length]; Marshal.Copy(context.Encoded, encoded, 0, encoded.Length);
        return X509CertificateLoader.LoadCertificate(encoded);
    }
}
