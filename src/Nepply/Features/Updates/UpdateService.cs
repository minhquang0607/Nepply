using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Nepply.Services;

namespace Nepply.Modules.Updates;

/// <param name="Summaries">Every release newer than the running one up to this one, newest first.</param>
internal sealed record UpdateRelease(string Tag, ReleaseVersion Version, string Notes, Uri Asset, long Bytes, string Sha256,
    IReadOnlyList<SummarySource>? Summaries = null);

internal sealed class PreparedUpdate(string path, string directory, FileStream guard) : IDisposable
{
    private bool _started;
    internal void Start()
    {
        // Keep the verified file open without write/delete sharing through CreateProcess.
        // The read-only guard has been held continuously since signature/hash verification.
        if (!string.Equals(UpdatePolicy.InstalledDirectory(), directory, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Installation changed during download.");
        var start = new ProcessStartInfo(path) { UseShellExecute = false, WorkingDirectory = directory };
        foreach (string argument in new[] { "/SP-", "/SILENT", "/NORESTART", "/NEPPLYUPDATE=1", "/DIR=" + directory }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Installer did not start.");
        _started = true;
    }
    public void Dispose()
    {
        guard.Dispose();
        // Setup still needs its executable while running. Do not delete it underneath Inno.
        if (!_started) UpdateService.RemoveDownload(path);
    }
}

internal sealed class UpdateService : IDisposable
{
    internal const long MaximumInstallerBytes = 512L * 1024 * 1024;
    private readonly HttpClient _http;
    internal static string CurrentVersion => typeof(UpdateService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
    internal UpdateService() : this(new HttpClientHandler { AllowAutoRedirect = false }) { }
    internal UpdateService(HttpMessageHandler handler)
    {
        _http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Nepply-Updater/1.0");
    }
    internal async Task<UpdateRelease?> CheckAsync(CancellationToken cancellation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{UpdatePolicy.Repository}/releases?per_page=30");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
        using var bytes = new MemoryStream();
        await CopyBoundedAsync(stream, bytes, 2 * 1024 * 1024, null, timeout.Token).ConfigureAwait(false);
        return ParseReleases(bytes.ToArray(), ReleaseVersion.Parse(CurrentVersion));
    }
    internal static UpdateRelease? ParseReleases(byte[] json, ReleaseVersion current)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
        if (document.RootElement.ValueKind != JsonValueKind.Array) throw new FormatException("Invalid release feed.");
        UpdateRelease? latest = null;
        var summaries = new List<SummarySource>();
        foreach (var entry in document.RootElement.EnumerateArray().Take(30))
        {
            if (entry.GetProperty("draft").GetBoolean()) continue;
            bool prerelease = entry.GetProperty("prerelease").GetBoolean();
            string tag = entry.GetProperty("tag_name").GetString() ?? "";
            ReleaseVersion version;
            try { version = ReleaseVersion.Parse(tag); } catch (Exception ex) when (ex is FormatException or OverflowException) { continue; }
            if (current.Pre.Length == 0 && (prerelease || version.Pre.Length > 0)) continue;
            if (version.CompareTo(current) <= 0) continue;
            if (summaries.All(known => known.Version.CompareTo(version) != 0)) summaries.Add(Summary(entry, tag, version));
            if (latest != null && version.CompareTo(latest.Version) <= 0) continue;
            string name = "Nepply-" + tag.TrimStart('v') + "-Setup.exe";
            var assets = entry.GetProperty("assets").EnumerateArray().Where(a => a.GetProperty("name").GetString() == name).ToArray();
            if (assets.Length != 1) continue;
            var asset = assets[0];
            long size = asset.GetProperty("size").GetInt64();
            string digest = asset.TryGetProperty("digest", out var d) ? d.GetString() ?? "" : "";
            if (size <= 0 || size > MaximumInstallerBytes || !digest.StartsWith("sha256:", StringComparison.Ordinal) || digest.Length != 71 || !digest[7..].All(char.IsAsciiHexDigit)) continue;
            if (!Uri.TryCreate(asset.GetProperty("browser_download_url").GetString(), UriKind.Absolute, out var url) || !UpdatePolicy.IsAssetUri(url)) continue;
            string expectedPath = $"/{UpdatePolicy.Repository}/releases/download/{Uri.EscapeDataString(tag)}/{Uri.EscapeDataString(name)}";
            if (url.AbsolutePath != expectedPath) continue;
            string notes = entry.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "";
            latest = new(tag, version, notes.Length > 20000 ? notes[..20000] : notes, url, size, digest[7..]);
        }
        return latest == null ? null : latest with
        {
            Summaries = summaries.Where(source => source.Version.CompareTo(latest.Version) <= 0)
                .OrderByDescending(source => source.Version).Take(ReleaseSummaryFile.MaximumReleases).ToArray()
        };
    }
    // Same rules as the installer: exact name and path under this release, bounded size, SHA-256.
    private static SummarySource Summary(JsonElement entry, string tag, ReleaseVersion version)
    {
        var assets = entry.GetProperty("assets").EnumerateArray().Where(a => a.GetProperty("name").GetString() == ReleaseSummaryFile.AssetName).ToArray();
        if (assets.Length != 1) return new(tag, version, null, 0, null);
        var asset = assets[0];
        long size = asset.GetProperty("size").GetInt64();
        string digest = asset.TryGetProperty("digest", out var d) ? d.GetString() ?? "" : "";
        string expectedPath = $"/{UpdatePolicy.Repository}/releases/download/{Uri.EscapeDataString(tag)}/{ReleaseSummaryFile.AssetName}";
        if (size <= 0 || size > ReleaseSummaryFile.MaximumBytes || !digest.StartsWith("sha256:", StringComparison.Ordinal) || digest.Length != 71 || !digest[7..].All(char.IsAsciiHexDigit)
            || !Uri.TryCreate(asset.GetProperty("browser_download_url").GetString(), UriKind.Absolute, out var url) || !UpdatePolicy.IsAssetUri(url) || url.AbsolutePath != expectedPath)
            return new(tag, version, null, 0, null);
        return new(tag, version, url, size, digest[7..]);
    }
    /// <summary>
    /// Reads each newer release's summary. A summary that is missing, too large, altered or
    /// malformed only loses its own text; it never fails the update check.
    /// </summary>
    internal async Task<IReadOnlyList<ReleaseSummary>> GetSummariesAsync(UpdateRelease release, CancellationToken cancellation)
    {
        var result = new List<ReleaseSummary>();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        foreach (var source in release.Summaries ?? [])
        {
            IReadOnlyDictionary<string, string[]>? notes = null;
            if (source.Asset != null && source.Sha256 != null)
            {
                try
                {
                    using var response = await GetDownloadAsync(source.Asset, timeout.Token).ConfigureAwait(false);
                    await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
                    using var bytes = new MemoryStream();
                    await CopyBoundedAsync(stream, bytes, source.Bytes, null, timeout.Token).ConfigureAwait(false);
                    byte[] data = bytes.ToArray();
                    if (data.Length != source.Bytes || !Convert.ToHexString(SHA256.HashData(data)).Equals(source.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new CryptographicException("Release summary checksum mismatch.");
                    notes = ReleaseSummaryFile.Parse(data, source.Version);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !cancellation.IsCancellationRequested)
                { AppLogger.Error(ex, "Read release summary " + source.Tag); }
            }
            result.Add(new(source.Tag, source.Version, notes));
        }
        return result;
    }
    internal async Task<PreparedUpdate> DownloadAsync(UpdateRelease release, IProgress<double> progress, CancellationToken cancellation)
    {
        if (UpdatePolicy.Signers.Length == 0) throw new InvalidOperationException("Signing policy has not been configured.");
        string installed = UpdatePolicy.InstalledDirectory() ?? throw new InvalidOperationException("Not an installed EXE distribution.");
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nepply", "Updates");
        return await DownloadVerifiedAsync(release, root, installed, path =>
        {
            AuthenticodeVerifier.Verify(path, UpdatePolicy.Signers);
            VerifyVersion(path, release.Version);
        }, progress, cancellation).ConfigureAwait(false);
    }
    // Trust decisions arrive through verify, so the transfer, checksum and cleanup rules
    // run in tests without a signed installer. Production always passes Authenticode.
    internal async Task<PreparedUpdate> DownloadVerifiedAsync(UpdateRelease release, string root, string installed, Action<string> verify, IProgress<double> progress, CancellationToken cancellation)
    {
        if (!UpdatePolicy.IsAssetUri(release.Asset) || release.Bytes <= 0 || release.Bytes > MaximumInstallerBytes) throw new FormatException("Invalid asset.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        var token = timeout.Token;
        Directory.CreateDirectory(root);
        RejectLinks(root);
        CleanStaleDownloads(root);
        string folder = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "Setup.exe");
        FileStream? guard = null;
        try
        {
            using var response = await GetDownloadAsync(release.Asset, token).ConfigureAwait(false);
            if (response.Content.Headers.ContentLength is long length && length != release.Bytes) throw new IOException("Installer size mismatch.");
            await using (var source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
            await using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                long received = await CopyBoundedAsync(source, target, release.Bytes, progress, token).ConfigureAwait(false);
                if (received != release.Bytes) throw new IOException("Incomplete installer.");
            }
            guard = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            string actual = Convert.ToHexString(await SHA256.HashDataAsync(guard, token).ConfigureAwait(false));
            if (!actual.Equals(release.Sha256, StringComparison.OrdinalIgnoreCase)) throw new CryptographicException("Installer checksum mismatch.");
            await Task.Run(() => verify(path), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            var prepared = new PreparedUpdate(path, installed, guard); guard = null;
            return prepared;
        }
        catch { guard?.Dispose(); RemoveDownload(path); throw; }
    }
    private async Task<HttpResponseMessage> GetDownloadAsync(Uri uri, CancellationToken token)
    {
        for (int redirects = 0; redirects <= 3; redirects++)
        {
            if (!UpdatePolicy.IsDownloadUri(uri)) throw new IOException("Untrusted download host.");
            var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                var location = response.Headers.Location; response.Dispose();
                if (location == null) throw new IOException("Invalid redirect.");
                uri = new Uri(uri, location); continue;
            }
            try { response.EnsureSuccessStatusCode(); return response; }
            catch { response.Dispose(); throw; }
        }
        throw new IOException("Too many redirects.");
    }
    internal static async Task<long> CopyBoundedAsync(Stream source, Stream target, long limit, IProgress<double>? progress, CancellationToken token)
    {
        byte[] buffer = new byte[81920]; long total = 0; long last = Environment.TickCount64;
        int count;
        while ((count = await source.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
        {
            total += count;
            if (total > limit) throw new IOException("Download exceeds limit.");
            await target.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
            if (Environment.TickCount64 - last >= 100) { progress?.Report(100d * total / limit); last = Environment.TickCount64; }
        }
        progress?.Report(100); return total;
    }
    internal static void VerifyVersion(string path, ReleaseVersion expected)
    {
        var info = FileVersionInfo.GetVersionInfo(path);
        if (info.ProductName?.Trim() != "Nepply" || ReleaseVersion.Parse(info.ProductVersion?.Trim() ?? "").CompareTo(expected) != 0)
            throw new IOException("Installer identity/version mismatch.");
    }
    private static void RejectLinks(string path)
    {
        for (DirectoryInfo? directory = new(path); directory != null; directory = directory.Parent)
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Update path contains a link.");
    }
    internal static void RemoveDownload(string path)
    {
        try { File.Delete(path); Directory.Delete(Path.GetDirectoryName(path)!); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { AppLogger.Error(ex, "Remove cancelled update"); }
    }
    private static void CleanStaleDownloads(string root)
    {
        // Only updater-owned GUID folders with exactly one old Setup.exe; never
        // recurse into or delete arbitrary content. A locked active Setup is kept.
        foreach (string folder in Directory.EnumerateDirectories(root).Take(100))
        {
            try
            {
                if (!Guid.TryParseExact(Path.GetFileName(folder), "N", out _)) continue;
                if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) continue;
                string path = Path.Combine(folder, "Setup.exe");
                var entries = Directory.EnumerateFileSystemEntries(folder).Take(2).ToArray();
                if (entries.Length != 1 || !entries[0].Equals(path, StringComparison.OrdinalIgnoreCase)) continue;
                if ((File.GetAttributes(path) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0) continue;
                if (File.GetLastWriteTimeUtc(path) < DateTime.UtcNow.AddDays(-7)) RemoveDownload(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { AppLogger.Error(ex, "Clean old update cache"); }
        }
    }
    public void Dispose() => _http.Dispose();
}
