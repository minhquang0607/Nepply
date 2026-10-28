using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Nepply.Modules.Updates;

internal static class UpdateTests
{
    internal static void Run(Action<string, bool> check)
    {
        check("Update stable supersedes beta", ReleaseVersion.Parse("2.5.0").CompareTo(ReleaseVersion.Parse("2.5.0-beta.3")) > 0);
        check("Update prerelease uses numeric ordering", ReleaseVersion.Parse("2.5.0-beta.10").CompareTo(ReleaseVersion.Parse("2.5.0-beta.9")) > 0);
        check("Update ignores build metadata", ReleaseVersion.Parse("v2.5.0+a").CompareTo(ReleaseVersion.Parse("2.5.0+b")) == 0);
        var dated = ReleaseVersion.Parse("1.0.0-17.10.26");
        check("Dated release retains prerelease label", dated.Pre == "17.10.26");
        check("Stable 1.0.0 supersedes dated release", ReleaseVersion.Parse("1.0.0").CompareTo(dated) > 0);
        check("First signed 1.0.0 supersedes public beta", ReleaseVersion.Parse("1.0.0").CompareTo(ReleaseVersion.Parse("1.0.0-beta.1")) > 0
            && ReleaseVersion.Parse("1.0.0-beta.2").CompareTo(ReleaseVersion.Parse("1.0.0-beta.1")) > 0);
        check("Beta build accepts the first signed stable release", UpdateService.ParseReleases(Feed("v1.0.0"), ReleaseVersion.Parse("1.0.0-beta.1")) != null);
        check("Release page is built locally for the official repository",
            UpdatePolicy.ReleasePageUri("v1.0.0").AbsoluteUri == "https://github.com/minhquang0607/Nepply/releases/tag/v1.0.0"
            && UpdatePolicy.ReleasePageUri("v1.0.0-beta.2").AbsoluteUri == "https://github.com/minhquang0607/Nepply/releases/tag/v1.0.0-beta.2");
        bool rejectedPage = false;
        try { UpdatePolicy.ReleasePageUri("v1.0.0/../../evil"); } catch (FormatException) { rejectedPage = true; }
        check("Release page refuses a tag that is not a release version", rejectedPage);
        string notes = ReleaseNotesText.ToPlain(
            "> ⚠️ **Beta, unsigned build.** Do not disable protection.\r\n\r\n[English](#english) · [Tiếng Việt](#vi)\r\n\r\n## English\r\n\r\n"
            + "### 📦 Downloads\r\n\r\n| Choose | Asset |\r\n| --- | --- |\r\n| Recommended | `Nepply-1.0.0-Setup.exe` |\r\n\r\n\r\n\r\n"
            + "- Open from the edge or *Ctrl + Space*.\r\n  - Nested __detail__\r\n---\r\n<!-- hidden -->![logo](x.png) <script>kept</script>");
        check("Release notes drop Markdown punctuation for the update window", notes ==
            "⚠️ Beta, unsigned build. Do not disable protection.\n\nEnglish · Tiếng Việt\n\nEnglish\n\n📦 Downloads\n\n"
            + "Choose  ·  Asset\nRecommended  ·  Nepply-1.0.0-Setup.exe\n\n• Open from the edge or Ctrl + Space.\n  • Nested detail\nlogo <script>kept</script>");
        check("Empty release notes stay empty", ReleaseNotesText.ToPlain(" \n ") == "");
        check("Next patch supersedes dated release across month boundary", ReleaseVersion.Parse("1.0.1-1.11.26").CompareTo(dated) > 0);
        foreach (string invalid in new[] { "../1.0.0", "01.0.0", "1.0.0-beta.01", "1.0", "v1.0.0/evil" })
        {
            bool rejected = false; try { ReleaseVersion.Parse(invalid); } catch (FormatException) { rejected = true; }
            check("Update rejects invalid version " + invalid, rejected);
        }
        foreach (string invalid in new[] { "http://github.com/minhquang0607/Nepply/releases/download/v3.0.0/x.exe", "https://github.com/other/Nepply/releases/download/v3.0.0/x.exe", "https://github.com.evil.test/minhquang0607/Nepply/releases/download/v3.0.0/x.exe", "https://user@github.com/minhquang0607/Nepply/releases/download/v3.0.0/x.exe", "https://github.com:444/minhquang0607/Nepply/releases/download/v3.0.0/x.exe" })
            check("Update refuses foreign asset " + new Uri(invalid).Host, !UpdatePolicy.IsAssetUri(new Uri(invalid)));
        check("Update refuses redirect to LAN", !UpdatePolicy.IsDownloadUri(new Uri("https://127.0.0.1/a")));
        check("Update accepts GitHub asset CDN", UpdatePolicy.IsDownloadUri(new Uri("https://release-assets.githubusercontent.com/a?signature=test")));
        byte[] Feed(string tag = "v3.0.0", bool draft = false, bool prerelease = false, string? digest = null, long size = 100, string? url = null) => JsonSerializer.SerializeToUtf8Bytes(new[] { new {
            tag_name = tag, draft, prerelease, body = "<script>test</script>\nRelease notes", assets = new[] { new {
                name = "Nepply-" + tag.TrimStart('v') + "-Setup.exe", size,
                digest = digest ?? "sha256:" + new string('a', 64),
                browser_download_url = url ?? $"https://github.com/{UpdatePolicy.Repository}/releases/download/{tag}/Nepply-{tag.TrimStart('v')}-Setup.exe"
            } }
        } });
        var current = ReleaseVersion.Parse("2.5.0");
        var release = UpdateService.ParseReleases(Feed(), current);
        check("Update finds exact installer and keeps plain notes", release?.Tag == "v3.0.0" && release.Notes.StartsWith("<script>"));
        check("Update never downgrades", UpdateService.ParseReleases(Feed("v2.4.0"), current) == null);
        check("Dated release is not a downgrade path from old internal builds", UpdateService.ParseReleases(Feed("v1.0.0-17.10.26", prerelease: true), ReleaseVersion.Parse("2.5.0-beta.3")) == null);
        check("Dated release accepts subsequent stable release", UpdateService.ParseReleases(Feed("v1.0.0"), dated) != null);
        check("Update never reinstalls same version", UpdateService.ParseReleases(Feed("v2.5.0"), current) == null);
        check("Update excludes drafts", UpdateService.ParseReleases(Feed(draft: true), current) == null);
        check("Stable users exclude prerelease", UpdateService.ParseReleases(Feed("v3.0.0-beta.1", prerelease: true), current) == null);
        check("Beta users accept newer beta", UpdateService.ParseReleases(Feed("v3.0.0-beta.1", prerelease: true), ReleaseVersion.Parse("2.5.0-beta.3")) != null);
        check("Update requires SHA256", UpdateService.ParseReleases(Feed(digest: ""), current) == null);
        check("Update rejects oversized installer", UpdateService.ParseReleases(Feed(size: UpdateService.MaximumInstallerBytes + 1), current) == null);
        check("Update binds URL to exact version and filename", UpdateService.ParseReleases(Feed(url: $"https://github.com/{UpdatePolicy.Repository}/releases/download/v4.0.0/other.exe"), current) == null);
        bool bounded = false;
        try { UpdateService.CopyBoundedAsync(new MemoryStream(new byte[11]), new MemoryStream(), 10, null, CancellationToken.None).GetAwaiter().GetResult(); }
        catch (IOException) { bounded = true; }
        check("Update streaming enforces byte limit without ContentLength", bounded);
        bool cancelled = false;
        try { UpdateService.CopyBoundedAsync(new MemoryStream(new byte[1]), new MemoryStream(), 10, null, new CancellationToken(true)).GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { cancelled = true; }
        check("Update streaming respects cancellation", cancelled);
        bool policy = false;
        try { AuthenticodeVerifier.Verify("not-used.exe", []); } catch (InvalidOperationException) { policy = true; }
        check("Update fails closed without signer policy", policy);
        using var client = new UpdateService(new FixtureHandler(Feed()));
        check("Update check works with bounded HTTP fixture", client.CheckAsync(CancellationToken.None).GetAwaiter().GetResult()?.Tag == "v3.0.0");
        using var denied = new UpdateService(new FixtureHandler([], HttpStatusCode.Forbidden));
        bool rejectedResponse = false;
        try { denied.CheckAsync(CancellationToken.None).GetAwaiter().GetResult(); } catch (HttpRequestException) { rejectedResponse = true; }
        check("Update rate limiting is an error not up-to-date", rejectedResponse);
    }
    private sealed class FixtureHandler(byte[] content, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent(content) });
    }
}
