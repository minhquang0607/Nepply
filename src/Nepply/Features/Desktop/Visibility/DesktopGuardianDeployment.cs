using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace Nepply.Services;

/// <summary>Deploys only the bundled guardian; never accepts executable paths from settings.</summary>
internal static class DesktopGuardianDeployment
{
    internal static Task<IDesktopGuardianSession> StartAsync(CancellationToken cancellation) => Task.Run(async () =>
    {
        string source = Path.Combine(AppContext.BaseDirectory, "guardian", "Nepply.DesktopGuardian.exe");
        string executable;
        // Serialize deployment with the previous helper's entire recovery lifetime.
        // Do not hold a thread-affine mutex across await.
        using (DesktopGuardianFence.Acquire(TimeSpan.FromSeconds(15)))
        {
            cancellation.ThrowIfCancellationRequested();
            ValidateRegularFile(source);
            string expected = typeof(DesktopGuardianDeployment).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "";
            if (FileVersionInfo.GetVersionInfo(source).FileVersion != expected)
                throw new IOException("Desktop guardian version does not match this application.");
            executable = ProcessPackageIdentity.IsPackaged
                ? CopyToCache(source, Windows.Storage.ApplicationData.Current.LocalCacheFolder.Path)
                : source;
        }
        return (IDesktopGuardianSession)await DesktopGuardianConnection.StartAsync(executable, cancellation, DesktopVisibilityLease.JournalPath).ConfigureAwait(false);
    }, cancellation);

    internal static string CopyToCache(string source, string cacheRoot)
    {
        ValidateRegularFile(source);
        string directory = Path.Combine(Path.GetFullPath(cacheRoot), "DesktopGuardian");
        ValidateAncestors(directory);
        Directory.CreateDirectory(directory);
        ValidateAncestors(directory);
        string target = Path.Combine(directory, "Nepply.DesktopGuardian.exe");
        if (File.Exists(target)) ValidateRegularFile(target);
        // One fixed payload per package, not a new executable for every hide/show.
        // LocalCache belongs to the package; package removal owns its final cleanup.
        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] expected = SHA256.HashData(input);
        if (File.Exists(target))
        {
            using var existing = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (CryptographicOperations.FixedTimeEquals(expected, SHA256.HashData(existing))) return target;
        }
        input.Position = 0;
        string temporary = Path.Combine(directory, "pending-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                input.CopyTo(output);
                output.Flush(true);
                output.Position = 0;
                if (!CryptographicOperations.FixedTimeEquals(expected, SHA256.HashData(output)))
                    throw new IOException("Desktop guardian copy verification failed.");
            }
            ValidateAncestors(directory);
            if (File.Exists(target)) ValidateRegularFile(target);
            File.Move(temporary, target, true);
            return target;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void ValidateRegularFile(string path)
    {
        ValidateAncestors(path);
        var file = new FileInfo(path);
        if (!file.Exists || file.Length is <= 0 or > 157286400 || (file.Attributes & FileAttributes.Directory) != 0)
            throw new IOException("Desktop guardian payload is missing or invalid.");
    }

    private static void ValidateAncestors(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Desktop guardian paths must not contain links.");
    }
}
