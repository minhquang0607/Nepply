using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using Nepply.Modules.Updates;

/// <summary>Transfer, checksum, redirect and cleanup rules of the update download, without a signed installer.</summary>
internal static class UpdateDownloadTests
{
    private static readonly Uri Asset = new("https://github.com/minhquang0607/Nepply/releases/download/v9.0.0/Nepply-9.0.0-Setup.exe");
    private static readonly Uri Cdn = new("https://release-assets.githubusercontent.com/github-production-release-asset/1/installer");

    internal static void Run(string output, Action<string, bool> check)
    {
        string root = Path.Combine(output, "update-download-" + Guid.NewGuid().ToString("N"));
        byte[] payload = new byte[256 * 1024];
        new Random(7).NextBytes(payload);
        var release = new UpdateRelease("v9.0.0", ReleaseVersion.Parse("9.0.0"), "", Asset, payload.Length, Convert.ToHexString(SHA256.HashData(payload)));
        bool NoDownloads() => !Directory.Exists(root) || Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length == 0 && Directory.GetDirectories(root).Length == 0;
        bool Rejected<TException>(Func<HttpRequestMessage, HttpResponseMessage> respond, UpdateRelease? candidate = null, Action<string>? verify = null, CancellationToken token = default) where TException : Exception
        {
            using var service = new UpdateService(new Handler(respond));
            try
            {
                service.DownloadVerifiedAsync(candidate ?? release, root, root, verify ?? (_ => { }), new Progress<double>(), token).GetAwaiter().GetResult().Dispose();
                return false;
            }
            catch (TException) { return true; }
            catch (Exception) { return false; } // Report the named check instead of ending the run.
        }

        string? verified = null;
        bool writeBlocked = false, deleteBlocked = false;
        using (var service = new UpdateService(new Handler(_ => Ok(payload))))
        {
            var prepared = service.DownloadVerifiedAsync(release, root, root, path =>
            {
                verified = path;
                try { using var write = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite); }
                catch (IOException) { writeBlocked = true; }
                try { File.Delete(path); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { deleteBlocked = true; }
            }, new Progress<double>(), CancellationToken.None).GetAwaiter().GetResult();
            check("Update download hands the exact verified bytes to the trust check", verified != null && File.Exists(verified) && File.ReadAllBytes(verified).SequenceEqual(payload));
            check("Verified installer stays protected from replacement", writeBlocked && deleteBlocked);
            prepared.Dispose();
        }
        check("Unused update download is removed", NoDownloads());

        check("Update checksum mismatch is refused", Rejected<CryptographicException>(_ => Ok(payload), release with { Sha256 = new string('0', 64) }));
        check("Checksum mismatch leaves no file", NoDownloads());
        bool verifiedTampered = false;
        byte[] tampered = (byte[])payload.Clone(); tampered[100] ^= 1;
        check("Tampered bytes never reach the trust check", Rejected<CryptographicException>(_ => Ok(tampered), verify: _ => verifiedTampered = true) && !verifiedTampered);
        check("Refused trust check is fatal and leaves no file", Rejected<CryptographicException>(_ => Ok(payload), verify: _ => throw new CryptographicException("Untrusted")) && NoDownloads());

        check("Declared size mismatch is refused", Rejected<IOException>(_ => Ok(payload[..^1])) && NoDownloads());
        check("Undeclared short body is refused", Rejected<IOException>(_ => Streamed(payload[..^1])) && NoDownloads());
        check("Undeclared long body stops at the size limit", Rejected<IOException>(_ => Streamed([.. payload, 1])) && NoDownloads());
        check("Failed HTTP status is refused", Rejected<HttpRequestException>(_ => new(HttpStatusCode.NotFound)) && NoDownloads());
        check("Malformed asset size is refused", Rejected<FormatException>(_ => Ok(payload), release with { Bytes = UpdateService.MaximumInstallerBytes + 1 }));
        check("Asset outside the repository is refused", Rejected<FormatException>(_ => Ok(payload), release with { Asset = new("https://github.com/someone/else/releases/download/v9.0.0/Setup.exe") }));

        using (var service = new UpdateService(new Handler(request => request.RequestUri == Asset ? Redirect(Cdn) : Ok(payload))))
        {
            var prepared = service.DownloadVerifiedAsync(release, root, root, _ => { }, new Progress<double>(), CancellationToken.None).GetAwaiter().GetResult();
            check("Update follows the redirect to the GitHub asset host", true);
            prepared.Dispose();
        }
        check("Redirect to another host is refused", Rejected<IOException>(request => request.RequestUri == Asset ? Redirect(new("https://downloads.example.com/Setup.exe")) : Ok(payload)) && NoDownloads());
        check("Redirect to plain HTTP is refused", Rejected<IOException>(request => request.RequestUri == Asset ? Redirect(new("http://release-assets.githubusercontent.com/Setup.exe")) : Ok(payload)) && NoDownloads());
        check("Redirect without a location is refused", Rejected<IOException>(_ => new(HttpStatusCode.Found)) && NoDownloads());
        check("Endless redirects are refused", Rejected<IOException>(_ => Redirect(Cdn)) && NoDownloads());

        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            check("Cancelled download leaves no file", Rejected<OperationCanceledException>(_ => Ok(payload), token: cancelled.Token) && NoDownloads());
        }

        string stale = Path.Combine(root, Guid.NewGuid().ToString("N"));
        string foreign = Path.Combine(root, "keep-me");
        Directory.CreateDirectory(stale); Directory.CreateDirectory(foreign);
        File.WriteAllBytes(Path.Combine(stale, "Setup.exe"), [1]);
        File.SetLastWriteTimeUtc(Path.Combine(stale, "Setup.exe"), DateTime.UtcNow.AddDays(-8));
        File.WriteAllBytes(Path.Combine(foreign, "Setup.exe"), [1]);
        File.SetLastWriteTimeUtc(Path.Combine(foreign, "Setup.exe"), DateTime.UtcNow.AddDays(-8));
        using (var service = new UpdateService(new Handler(_ => Ok(payload))))
            service.DownloadVerifiedAsync(release, root, root, _ => { }, new Progress<double>(), CancellationToken.None).GetAwaiter().GetResult().Dispose();
        check("Next download removes expired updater folders only", !Directory.Exists(stale) && File.Exists(Path.Combine(foreign, "Setup.exe")));
    }

    private static HttpResponseMessage Ok(byte[] body) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };
    private static HttpResponseMessage Streamed(byte[] body) => new(HttpStatusCode.OK) { Content = new UndeclaredLengthContent(body) };
    private static HttpResponseMessage Redirect(Uri location)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = location;
        return response;
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(respond(request));
        }
    }

    // Chunked responses declare no Content-Length, so only the byte count can catch a wrong size.
    private sealed class UndeclaredLengthContent(byte[] body) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(body).AsTask();
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }
}
