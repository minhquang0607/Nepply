using System.IO;
using Nepply.Services;

internal static class DesktopGuardianDeploymentTests
{
    internal static void Run(string output, Action<string, bool> check)
    {
        // File-copy fixtures only. These bytes are never executed.
        string root = Path.Combine(output, "guardian-payload-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string source = Path.Combine(root, "fixture.bin"), cache = Path.Combine(root, "cache");
        File.WriteAllBytes(source, [1, 2, 3, 4]);
        string target = DesktopGuardianDeployment.CopyToCache(source, cache);
        check("Guardian cache copy matches bundled bytes", File.ReadAllBytes(target).SequenceEqual(new byte[] { 1, 2, 3, 4 }));
        DateTime timestamp = File.GetLastWriteTimeUtc(target);
        check("Guardian unchanged payload reuses one path", DesktopGuardianDeployment.CopyToCache(source, cache) == target && File.GetLastWriteTimeUtc(target) == timestamp);
        File.WriteAllBytes(source, [5, 6, 7, 8, 9]);
        check("Guardian payload update replaces the same cache entry", DesktopGuardianDeployment.CopyToCache(source, cache) == target && File.ReadAllBytes(target).SequenceEqual(new byte[] { 5, 6, 7, 8, 9 }));
        check("Guardian cache has no per-session payload accumulation", Directory.GetFiles(Path.GetDirectoryName(target)!).Length == 1);
        bool failed = false;
        try { DesktopGuardianDeployment.CopyToCache(Path.Combine(root, "missing.bin"), cache); } catch (IOException) { failed = true; }
        check("Guardian missing source is rejected without damaging cache", failed && File.ReadAllBytes(target).SequenceEqual(new byte[] { 5, 6, 7, 8, 9 }));
        File.WriteAllBytes(source, []);
        failed = false;
        try { DesktopGuardianDeployment.CopyToCache(source, cache); } catch (IOException) { failed = true; }
        check("Guardian empty source is rejected", failed);
    }
}
