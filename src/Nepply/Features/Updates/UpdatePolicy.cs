using Microsoft.Win32;
using Nepply.Services;
using System.IO;

namespace Nepply.Modules.Updates;

internal static class UpdatePolicy
{
    internal const string Repository = "minhquang0607/Nepply";
    // Release gate: fill with the approved publisher (scripts/Get-SignerPolicy.ps1 on a
    // signed Setup), through reviewed source changes only. Never trust policy from
    // downloaded metadata. During an issuing-CA key rollover list both issuer pins.
    internal static readonly SignerRule[] Signers = [];
    internal static string? InstalledDirectory()
    {
        if (ProcessPackageIdentity.IsPackaged) return null;
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{5E9F4A2B-1A8D-4C9C-8A5E-F7C4E8A1D3B2}_is1");
        if (key?.GetValue("InstallLocation") is not string location) return null;
        var full = Path.GetFullPath(location).TrimEnd(Path.DirectorySeparatorChar);
        var executable = Path.GetFullPath(Path.Combine(full, "Nepply.exe"));
        return string.Equals(executable, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase) ? full : null;
    }
    // Built locally from a tag that already parsed as a release version; the feed's own
    // html_url is never opened. Manual download is the path while auto-install is unavailable.
    internal static Uri ReleasePageUri(string tag)
    {
        _ = ReleaseVersion.Parse(tag);
        return new($"https://github.com/{Repository}/releases/tag/{Uri.EscapeDataString(tag)}");
    }
    internal static bool IsAssetUri(Uri uri) => uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0
        && uri.Host == "github.com" && uri.AbsolutePath.StartsWith("/" + Repository + "/releases/download/", StringComparison.Ordinal)
        && uri.Query.Length == 0 && uri.Fragment.Length == 0;
    internal static bool IsDownloadUri(Uri uri) => IsAssetUri(uri) ||
        (uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0 && uri.Host == "release-assets.githubusercontent.com");
}
