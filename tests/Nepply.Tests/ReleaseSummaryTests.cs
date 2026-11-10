using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nepply.Modules.Updates;

/// <summary>The update window's per-language summaries: file rules, feed collection, download checks.</summary>
internal static class ReleaseSummaryTests
{
    private const string Repo = "https://github.com/minhquang0607/Nepply/releases/download/";

    internal static void Run(Action<string, bool> check)
    {
        byte[] File(string version, string vi = "Một", string en = "One", string? extra = null) => Encoding.UTF8.GetBytes(
            $$$"""{"version":"{{{version}}}","notes":{"vi":["{{{vi}}}"],"en":["{{{en}}}"]{{{extra}}}}}""");
        var v300 = ReleaseVersion.Parse("3.0.0");

        // File rules.
        var parsed = ReleaseSummaryFile.Parse(File("3.0.0", extra: ""","xx":["ignored"]"""), v300);
        check("Summary file reads each language and ignores unknown ones", parsed["vi"][0] == "Một" && parsed["en"][0] == "One" && !parsed.ContainsKey("xx"));
        foreach (var (name, bytes) in new (string, byte[])[] {
            ("another version", File("3.0.1")),
            ("a prerelease of the version", File("3.0.0-rc.1")),
            ("a control character", File("3.0.0", vi: "a\\nb")),
            ("an empty item", File("3.0.0", vi: " ")),
            ("an overlong item", File("3.0.0", vi: new string('x', ReleaseSummaryFile.MaximumItemCharacters + 1))),
            ("a non-list language", Encoding.UTF8.GetBytes("""{"version":"3.0.0","notes":{"vi":"text"}}""")),
            ("a non-object root", Encoding.UTF8.GetBytes("[]")),
            ("an oversized file", new byte[ReleaseSummaryFile.MaximumBytes + 1]) })
        {
            bool rejected = false;
            try { ReleaseSummaryFile.Parse(bytes, v300); } catch (Exception ex) when (ex is FormatException or JsonException) { rejected = true; }
            check("Summary file rejects " + name, rejected);
        }

        // Text for the window.
        var notes = new Dictionary<string, string[]> { ["vi"] = ["Sửa A"], ["en"] = ["Fix A"] };
        var one = new[] { new ReleaseSummary("v3.0.0", v300, notes) };
        check("Summary uses the app language without a heading for one version", ReleaseSummaryFile.Build(one, "vi", "missing") == "• Sửa A");
        check("Summary falls back to English", ReleaseSummaryFile.Build(one, "ja", "missing") == "• Fix A");
        var two = new[] { new ReleaseSummary("v2.9.0", ReleaseVersion.Parse("2.9.0"), null), new ReleaseSummary("v3.0.0", v300, notes) };
        check("Summary lists every newer version newest first and marks one without a file",
            ReleaseSummaryFile.Build(two, "vi", "missing") == "3.0.0\n• Sửa A\n\n2.9.0\nmissing");
        check("Summary is absent when no version has one, so the release notes are used",
            ReleaseSummaryFile.Build([new ReleaseSummary("v3.0.0", v300, null)], "vi", "missing") == null);

        // Feed: summaries of every version between the running one and the newest.
        byte[] summary300 = File("3.0.0"), summary290 = File("2.9.0", vi: "Hai", en: "Two");
        string Digest(byte[] data) => "sha256:" + Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
        object Release(string tag, bool prerelease, params object[] assets) => new { tag_name = tag, draft = false, prerelease, body = "Body " + tag, assets };
        object Setup(string tag) => new { name = $"Nepply-{tag.TrimStart('v')}-Setup.exe", size = 100, digest = "sha256:" + new string('a', 64), browser_download_url = $"{Repo}{tag}/Nepply-{tag.TrimStart('v')}-Setup.exe" };
        object Notes(string tag, byte[] data, string? digest = null, string? url = null) => new { name = ReleaseSummaryFile.AssetName, size = data.Length, digest = digest ?? Digest(data), browser_download_url = url ?? $"{Repo}{tag}/{ReleaseSummaryFile.AssetName}" };
        byte[] feed = JsonSerializer.SerializeToUtf8Bytes(new[] {
            Release("v3.0.0", false, Setup("v3.0.0"), Notes("v3.0.0", summary300)),
            Release("v3.0.0-rc.1", true, Setup("v3.0.0-rc.1"), Notes("v3.0.0-rc.1", File("3.0.0-rc.1"))),
            Release("v2.9.0", false, Setup("v2.9.0"), Notes("v2.9.0", summary290)),
            Release("v2.8.0", false, Setup("v2.8.0")),
            Release("v2.7.0", false, Setup("v2.7.0"), Notes("v2.7.0", File("2.7.0"), url: "https://example.invalid/release-notes.json")),
            Release("v2.6.0", false, Setup("v2.6.0"), Notes("v2.6.0", File("2.6.0"))) });
        var found = UpdateService.ParseReleases(feed, ReleaseVersion.Parse("2.6.0"));
        string[] tags = found?.Summaries?.Select(source => source.Tag).ToArray() ?? [];
        check("Feed collects every newer stable version, newest first, not the running one or prereleases",
            tags.SequenceEqual(["v3.0.0", "v2.9.0", "v2.8.0", "v2.7.0"]));
        check("Feed keeps a version without a summary file and drops a summary from another host",
            found!.Summaries![2].Asset == null && found.Summaries[3].Asset == null && found.Summaries[0].Asset != null);
        var rcUser = UpdateService.ParseReleases(feed, ReleaseVersion.Parse("2.9.0"));
        check("A release candidate user also sees prerelease summaries",
            UpdateService.ParseReleases(feed, ReleaseVersion.Parse("3.0.0-beta.1"))?.Summaries?.Select(s => s.Tag).SequenceEqual(["v3.0.0", "v3.0.0-rc.1"]) == true
            && rcUser?.Summaries?.Select(s => s.Tag).SequenceEqual(["v3.0.0"]) == true);

        // Download: through the asset CDN, checked against the feed's SHA-256.
        var cdn = new Uri("https://release-assets.githubusercontent.com/summary");
        using (var service = new UpdateService(new Handler(request => request.RequestUri!.Host == "github.com"
            ? Redirect(cdn) : Ok(request.RequestUri == cdn ? summary300 : []))))
        {
            var read = service.GetSummariesAsync(found with { Summaries = [found.Summaries[0]] }, CancellationToken.None).GetAwaiter().GetResult();
            check("Summary download follows the CDN redirect and checks the hash", read.Single().Notes?["en"][0] == "One");
        }
        using (var service = new UpdateService(new Handler(request => Ok(request.RequestUri!.AbsolutePath.Contains("v3.0.0/") ? summary290 : summary290))))
        {
            var read = service.GetSummariesAsync(found, CancellationToken.None).GetAwaiter().GetResult();
            check("An altered summary loses only its own text", read.Count == 4 && read[0].Notes == null && read[1].Notes?["vi"][0] == "Hai" && read[2].Notes == null);
        }
        using (var service = new UpdateService(new Handler(_ => Redirect(new Uri("https://example.invalid/x")))))
        {
            var read = service.GetSummariesAsync(found with { Summaries = [found.Summaries[0]] }, CancellationToken.None).GetAwaiter().GetResult();
            check("Summary download refuses a redirect to another host", read.Single().Notes == null);
        }

        // The repository's own files follow the app's rules.
        string? root = AppContext.BaseDirectory;
        while (root != null && !System.IO.File.Exists(Path.Combine(root, "Nepply.slnx"))) root = Path.GetDirectoryName(root);
        var files = root == null ? [] : Directory.GetFiles(Path.Combine(root, "packaging", "release-notes"), "*.json");
        check("Repository summaries load in all eight languages", files.Length > 0 && files.All(path =>
        {
            var read = ReleaseSummaryFile.Parse(System.IO.File.ReadAllBytes(path), ReleaseVersion.Parse(Path.GetFileNameWithoutExtension(path)));
            return Nepply.Services.LocalizationService.Languages.All(language => read.ContainsKey(language.Id));
        }));
    }

    private static HttpResponseMessage Ok(byte[] data) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(data) };
    private static HttpResponseMessage Redirect(Uri location) { var response = new HttpResponseMessage(HttpStatusCode.Found); response.Headers.Location = location; return response; }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
}
