using System.ComponentModel;
using Nepply.Services;

internal static class PackageIdentityTests
{
    internal static void Run(Action<string, bool> check)
    {
        check("Only explicit NO_PACKAGE is unpackaged", !ProcessPackageIdentity.Classify(15700, 0));
        check("Identity size query detects package", ProcessPackageIdentity.Classify(122, 80));
        foreach (int code in new[] { 5, 87, 15701 })
        {
            bool rejected = false;
            try { ProcessPackageIdentity.Classify(code, 0); }
            catch (Win32Exception ex) { rejected = ex.NativeErrorCode == code; }
            check("Package API error is not unpackaged: " + code, rejected);
        }
        foreach (var query in new[] { (122, 0u), (122, 1u), (0, 0u) })
        {
            bool rejected = false;
            try { ProcessPackageIdentity.Classify(query.Item1, query.Item2); }
            catch (InvalidOperationException) { rejected = true; }
            check("Malformed identity size query rejected: " + query, rejected);
        }
        AutoStartService.EnsureUnpackaged(false);
        bool packagedRejected = false;
        try { AutoStartService.EnsureUnpackaged(true); }
        catch (InvalidOperationException) { packagedRejected = true; }
        check("Packaged process cannot enter registry startup backend", packagedRejected);
        // Read-only native check; this test never calls Apply or modifies startup.
        bool identity = ProcessPackageIdentity.IsPackaged;
        check("Native package identity query is stable", identity == ProcessPackageIdentity.IsPackaged);
    }
}
