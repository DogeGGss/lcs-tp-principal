using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Riftwalker.Launcher;
using Riftwalker.Releases;

var tests = new List<(string Name, Func<Task> Run)>();
void Test(string name, Func<Task> run) => tests.Add((name, run));
void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}; got {actual}"); }
async Task Reject<T>(Func<Task> action) where T : Exception
{ try { await action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
string url = "https://github.com/DogeGGss/lcs-tp-principal/releases/download/v0.2.0/test.zip";
Package P(byte[] data) => new() { Size = data.Length, Sha256 = Convert.ToHexString(SHA256.HashData(data)), Asset = "test.zip" };
GitHubRelease R(string version, bool pre = false) => new()
{
    Tag = "v" + version, Prerelease = pre, Assets = [new() { Name = "release.json" }, new() { Name = $"Project-Riftwalker-v{version}.zip" }]
};
Test("SemVer numeric order and prerelease boundaries", () =>
{
    Equal(true, ReleaseVersion.Parse("0.1.10").CompareTo(ReleaseVersion.Parse("0.1.9")) > 0);
    Equal(true, ReleaseVersion.Parse("0.2.0-exp.10").CompareTo(ReleaseVersion.Parse("0.2.0-exp.2")) > 0);
    Equal(true, ReleaseVersion.Parse("0.2.0").CompareTo(ReleaseVersion.Parse("0.2.0-exp.10")) > 0);
    foreach (var invalid in new[] { "sprint-1", "0.2", "01.2.0", "0.2.0-exp.01", "0.2.0/../../x" }) Equal(false, ReleaseVersion.TryParse(invalid, out _));
    return Task.CompletedTask;
});
Test("Stable / experimental selection ignores drafts and legacy releases", () =>
{
    var releases = new[] { R("0.2.0"), R("0.3.0-exp.2", true), R("0.3.0-exp.10", true), R("9.0.0") with { Draft = true }, new GitHubRelease { Tag = "sprint-1" } };
    Equal("0.2.0", ReleaseClient.Select(releases, false)!.Version.ToString());
    Equal("0.3.0-exp.10", ReleaseClient.Select(releases, true)!.Version.ToString());
    return Task.CompletedTask;
});
Test("Resume sends Range and appends only the remaining bytes", async () =>
{
    using var temp = new TestFolder(); byte[] data = Encoding.UTF8.GetBytes("complete archive bytes");
    string file = Path.Combine(temp.Path, "resume.part"); File.WriteAllBytes(file, data[..5]);
    using var client = new HttpClient(new Handler(req =>
    {
        Equal(5L, req.Headers.Range!.Ranges.Single().From!.Value);
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(data[5..]) };
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(5, data.Length - 1, data.Length); return response;
    }));
    await new Downloader(client).DownloadAsync(url, P(data), file, null, default);
    Equal(Convert.ToHexString(data), Convert.ToHexString(File.ReadAllBytes(file)));
});
Test("Server ignoring Range restarts cleanly", async () =>
{
    using var temp = new TestFolder(); byte[] data = Encoding.UTF8.GetBytes("valid archive"); string file = Path.Combine(temp.Path, "a.part"); File.WriteAllBytes(file, data[..3]);
    using var client = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(data) }));
    await new Downloader(client).DownloadAsync(url, P(data), file, null, default);
    Equal(data.Length, File.ReadAllBytes(file).Length);
});
Test("Wrong Content-Range is rejected without touching the partial download", async () =>
{
    using var temp = new TestFolder(); byte[] data = Encoding.UTF8.GetBytes("valid archive"); string file = Path.Combine(temp.Path, "a.part"); File.WriteAllBytes(file, data[..3]);
    using var client = new HttpClient(new Handler(_ =>
    { var response = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(data[3..]) }; response.Content.Headers.ContentRange = new ContentRangeHeaderValue(2, data.Length - 2, data.Length); return response; }));
    await Reject<InvalidDataException>(() => new Downloader(client).DownloadAsync(url, P(data), file, null, default));
    Equal(3L, new FileInfo(file).Length);
});
Test("Corrupt full download is removed", async () =>
{
    using var temp = new TestFolder(); byte[] data = [1, 2, 3]; string file = Path.Combine(temp.Path, "a.part");
    using var client = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 3, 2, 1 }) }));
    await Reject<InvalidDataException>(() => new Downloader(client).DownloadAsync(url, P(data), file, null, default)); Equal(false, File.Exists(file));
});
Test("Cancelled transfer leaves the previous installation intact", async () =>
{
    using var temp = new TestFolder(); var installer = new GameInstaller(temp.Path); Seed(installer, "0.1.0");
    using var cancel = new CancellationTokenSource(); cancel.Cancel();
    using var client = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent([1]) }));
    await Reject<OperationCanceledException>(() => new Downloader(client).DownloadAsync(url, P([1]), installer.DownloadPath(P([1])), null, cancel.Token));
    Equal("0.1.0", installer.Installed!.Version);
});
Test("Interrupted streaming download resumes its durable prefix", async () =>
{
    using var temp = new TestFolder(); byte[] data = Encoding.UTF8.GetBytes("some long download"); string file = Path.Combine(temp.Path, "a.part");
    using var client = new HttpClient(new Handler(_ =>
    { var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new BrokenStream(data[..5])) }; response.Content.Headers.ContentLength = data.Length; return response; }));
    await Reject<IOException>(() => new Downloader(client).DownloadAsync(url, P(data), file, null, default)); Equal(5L, new FileInfo(file).Length);
});
Test("Install and update replace game only, preserving launcher settings and backup", async () =>
{
    using var temp = new TestFolder(); var installer = new GameInstaller(temp.Path);
    Storage.Write(Path.Combine(temp.Path, ".riftwalker", "settings.json"), new LauncherSettings(true));
    var (file, manifest) = Archive(temp.Path, "0.2.0");
    await installer.InstallAsync(file, manifest, null, default); Equal("0.2.0", installer.Installed!.Version); Equal(false, File.Exists(file));
    (file, manifest) = Archive(temp.Path, "0.3.0");
    await installer.InstallAsync(file, manifest, null, default); Equal("0.3.0", installer.Installed!.Version);
    Equal(true, Storage.Read<LauncherSettings>(Path.Combine(temp.Path, ".riftwalker", "settings.json"))!.Experimental);
    Equal("0.2.0", Storage.Read<InstalledGame>(Path.Combine(temp.Path, ".riftwalker", "game.previous", "installed.json"))!.Version);
});
foreach (var entry in new[] { "../escape.txt", "/absolute.txt", "file:stream", "folder/../escape.txt", "installed.json" })
    Test("ZIP rejects unsafe path " + entry, async () =>
    {
        using var temp = new TestFolder(); var installer = new GameInstaller(temp.Path); Seed(installer, "0.1.0");
        var (file, manifest) = Archive(temp.Path, "0.2.0", entry);
        await Reject<InvalidDataException>(() => installer.InstallAsync(file, manifest, null, default)); Equal("0.1.0", installer.Installed!.Version);
    });
Test("Wrong build stamp does not replace the installed game", async () =>
{
    using var temp = new TestFolder(); var installer = new GameInstaller(temp.Path); Seed(installer, "0.1.0");
    var (file, m) = Archive(temp.Path, "0.2.0");
    await Reject<InvalidDataException>(() => installer.InstallAsync(file, m with { Game = m.Game with { Version = "0.3.0" } }, null, default));
    Equal("0.1.0", installer.Installed!.Version);
});
Test("Declared unpacked size limits ZIP extraction", async () =>
{
    using var temp = new TestFolder(); var installer = new GameInstaller(temp.Path);
    var (file, m) = Archive(temp.Path, "0.2.0");
    await Reject<InvalidDataException>(() => installer.InstallAsync(file, m with { Game = m.Game with { UnpackedSize = 1 } }, null, default)); Equal(null, installer.Installed);
});
Test("Startup recovers interrupted directory swap", () =>
{
    using var temp = new TestFolder(); var installer = new GameInstaller(temp.Path); Seed(installer, "0.1.0");
    Directory.CreateDirectory(Path.Combine(temp.Path, ".riftwalker"));
    Directory.Move(installer.Game, Path.Combine(temp.Path, ".riftwalker", "game.previous"));
    installer.Recover(); Equal("0.1.0", installer.Installed!.Version); return Task.CompletedTask;
});
Test("Unmanaged game folder is never replaced", async () =>
{
    using var temp = new TestFolder(); var installer = new GameInstaller(temp.Path); Directory.CreateDirectory(installer.Game);
    File.WriteAllText(Path.Combine(installer.Game, "personal.txt"), "keep"); var (file, m) = Archive(temp.Path, "0.2.0");
    await Reject<IOException>(() => installer.InstallAsync(file, m, null, default)); Equal("keep", File.ReadAllText(Path.Combine(installer.Game, "personal.txt")));
});
Test("Manifest requires matching version, asset size and hash", () =>
{
    var r = R("0.2.0") with { Assets = [new() { Name = "Project-Riftwalker-v0.2.0.zip", Size = 9 }] };
    var m = new ReleaseManifest { Version = "0.2.0", Game = new() { Asset = "Project-Riftwalker-v0.2.0.zip", Version = "0.2.0", Size = 9, UnpackedSize = 20, Sha256 = new string('A', 64) } };
    m.Validate(r);
    return Reject<InvalidDataException>(() => { (m with { Version = "0.3.0" }).Validate(r); return Task.CompletedTask; });
});
Test("Download URLs cannot point to another repository or scheme", async () =>
{
    foreach (string bad in new[] { "http://github.com/DogeGGss/lcs-tp-principal/releases/download/x/a", "https://evil.example/a", "https://github.com/someone/other/releases/download/x/a" })
        await Reject<InvalidDataException>(() => { ReleaseClient.ValidateUrl(bad); return Task.CompletedTask; });
});
Test("API list is reused with its ETag (304 does not count against GitHub's limit)", async () =>
{
    using var temp = new TestFolder();
    string list = "[{\"tag_name\":\"v0.2.0\",\"prerelease\":false,\"assets\":[{\"name\":\"release.json\"},{\"name\":\"Project-Riftwalker-v0.2.0.zip\"}]}]";
    int calls = 0;
    using var client = new HttpClient(new Handler(req =>
    {
        calls++;
        if (calls == 1)
        {
            Equal(false, req.Headers.Contains("If-None-Match"));
            var ok = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(list) };
            ok.Headers.ETag = new EntityTagHeaderValue("\"lista-1\""); return ok;
        }
        Equal("\"lista-1\"", req.Headers.GetValues("If-None-Match").Single());
        return new HttpResponseMessage(HttpStatusCode.NotModified);
    }));
    Equal("0.2.0", (await new ReleaseClient(client, temp.Path).FetchAsync(default)).Single().Version.ToString());
    Equal("0.2.0", (await new ReleaseClient(client, temp.Path).FetchAsync(default)).Single().Version.ToString());
    Equal(2, calls);
});
Test("Latest stable release is found without the API, from its release.json", async () =>
{
    var manifest = new ReleaseManifest
    {
        Version = "0.2.0",
        Game = new() { Asset = "Project-Riftwalker-v0.2.0.zip", Version = "0.2.0", Size = 9, UnpackedSize = 20, Sha256 = new string('A', 64) },
        Launcher = new() { Asset = "Riftwalker-Launcher.exe", Version = "0.1.1", Size = 7, Sha256 = new string('B', 64) }
    };
    // Windows PowerShell writes UTF-8 with BOM: it must still be read.
    byte[] body = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(manifest, ReleaseClient.Json))];
    using var client = new HttpClient(new Handler(req =>
    {
        Equal(ReleaseClient.LatestManifestUrl, req.RequestUri!.ToString());
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };
    }));
    var latest = (await new ReleaseClient(client).LatestStableAsync(default))!.Value;
    Equal(true, latest.Release.Compatible);
    Equal("0.2.0", ReleaseClient.Select([latest.Release], false)!.Version.ToString());
    foreach (var asset in latest.Release.Assets) ReleaseClient.ValidateUrl(asset.Url);
    Equal("https://github.com/DogeGGss/lcs-tp-principal/releases/download/v0.2.0/Project-Riftwalker-v0.2.0.zip",
        latest.Release.Assets.Single(a => a.Name == manifest.Game.Asset).Url);
});
Test("Latest stable release without the launcher format is not offered", async () =>
{
    using var client = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
    Equal(false, (await new ReleaseClient(client).LatestStableAsync(default)).HasValue);
});
Test("The launcher's own release: launcher.json is read and checked", async () =>
{
    var launcher = new Package { Asset = "Riftwalker-Launcher.exe", Version = "0.2.0", Size = 7, Sha256 = new string('C', 64) };
    using var ok = new HttpClient(new Handler(req =>
    {
        Equal(ReleaseClient.LauncherManifestUrl, req.RequestUri!.ToString());
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(launcher, ReleaseClient.Json)) };
    }));
    Equal("0.2.0", (await new ReleaseClient(ok).LauncherAsync(default))!.Version);
    using var missing = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
    Equal(true, await new ReleaseClient(missing).LauncherAsync(default) == null);
    using var wrong = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK)
    { Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(launcher with { Asset = "otro.exe" }, ReleaseClient.Json)) }));
    await Reject<InvalidDataException>(() => new ReleaseClient(wrong).LauncherAsync(default));
    ReleaseClient.ValidateUrl(ReleaseClient.LauncherDownloadUrl);
    // The launcher's tag is not a version: its release never counts as a game version.
    Equal(false, new GitHubRelease { Tag = ReleaseClient.LauncherTag, Assets = [new() { Name = "release.json" }] }.Compatible);
});
Test("Notes in the launcher stop at the publisher's footer", () =>
{
    Equal("## Qué trae\n- Algo", ReleaseClient.NotesForLauncher("## Qué trae\n- Algo\n\n<!-- riftwalker:pie -->\n---\n**Esta versión se instala desde el launcher.**"));
    Equal("Sin pie", ReleaseClient.NotesForLauncher("Sin pie\n"));
    Equal("", ReleaseClient.NotesForLauncher(null));
    return Task.CompletedTask;
});
int packageIndex = Array.IndexOf(args, "--package-root");
if (packageIndex >= 0)
    Test("Publisher manifest and ZIP install together", async () =>
    {
        string folder = Path.GetFullPath(args[packageIndex + 1]);
        var manifest = Storage.Read<ReleaseManifest>(Path.Combine(folder, "release.json"))!;
        var release = R(manifest.Version, manifest.Version.Contains('-')) with
        { Assets = Directory.GetFiles(folder).Select(p => new ReleaseAsset { Name = Path.GetFileName(p), Size = new FileInfo(p).Length }).ToList() };
        manifest.Validate(release);
        using var temp = new TestFolder();
        string source = Path.Combine(folder, manifest.Game.Asset), copy = Path.Combine(temp.Path, "game.zip");
        File.Copy(source, copy);
        await new GameInstaller(temp.Path).InstallAsync(copy, manifest, null, default);
        Equal(manifest.Version, new GameInstaller(temp.Path).Installed!.Version);
    });
if (args.Contains("--live"))
    Test("Read-only live GitHub release discovery", async () =>
    {
        using var http = ReleaseClient.CreateHttpClient();
        var releases = await new ReleaseClient(http).FetchAsync(default);
        Console.WriteLine($"Compatible published releases: {releases.Count}");
        Equal(true, releases.All(r => r.Compatible));
        var latest = await new ReleaseClient(http).LatestStableAsync(default);
        Console.WriteLine("Latest stable from release.json: " + (latest.HasValue ? latest.Value.Release.Version.ToString() : "none with the launcher format yet"));
    });
if (args.Contains("--live-install"))
    Test("Live: the newest published version installs the way the launcher does it", async () =>
    {
        // Same path as the launcher's Install button (experimental channel, so it also sees pre-releases), into a
        // temporary folder: no shortcuts and nothing left behind.
        using var http = ReleaseClient.CreateHttpClient();
        var client = new ReleaseClient(http);
        var target = ReleaseClient.Select(await client.FetchAsync(default), true) ?? throw new Exception("No compatible release is published.");
        var manifest = await client.ManifestAsync(target, default);
        using var temp = new TestFolder();
        var installer = new GameInstaller(temp.Path);
        installer.CheckSpace(manifest.Game);
        string zip = installer.DownloadPath(manifest.Game);
        await new Downloader(http).DownloadAsync(target.Assets.Single(a => a.Name == manifest.Game.Asset).Url, manifest.Game, zip, null, default);
        await installer.InstallAsync(zip, manifest, null, default);
        Equal(manifest.Version, installer.Installed!.Version);
        Console.WriteLine($"Installed {manifest.Version}: {manifest.Game.Size / 1048576d:N0} MB ZIP, {manifest.Game.UnpackedSize / 1048576d:N0} MB unpacked, SHA-256 verified");
        Console.WriteLine("Notes shown in the launcher end with: " + ReleaseClient.NotesForLauncher(target.Body).Split('\n').Last());
    });
int failures = 0;
foreach (var test in tests)
{
    try { await test.Run(); Console.WriteLine("PASS " + test.Name); }
    catch (Exception e) { failures++; Console.WriteLine("FAIL " + test.Name + "\n" + e); }
}
Console.WriteLine($"{tests.Count - failures}/{tests.Count} passed");
return failures == 0 ? 0 : 1;

static void Seed(GameInstaller installer, string version)
{
    Directory.CreateDirectory(installer.Game); File.WriteAllText(Path.Combine(installer.Game, "Project Riftwalker.exe"), "old");
    Storage.Write(Path.Combine(installer.Game, "installed.json"), new InstalledGame(version, "Project Riftwalker.exe"));
}
static (string, ReleaseManifest) Archive(string root, string version, string? extra = null)
{
    string file = Path.Combine(root, Guid.NewGuid() + ".zip"); long total = 0;
    using (var zip = ZipFile.Open(file, ZipArchiveMode.Create))
    {
        void Add(string name, string content)
        { byte[] bytes = Encoding.UTF8.GetBytes(content); var entry = zip.CreateEntry(name); using var stream = entry.Open(); stream.Write(bytes); total += bytes.Length; }
        Add("Project Riftwalker.exe", "new"); Add("Project Riftwalker_Data/data", "data"); Add("riftwalker-build.json", "{\"version\":\"" + version + "\"}");
        if (extra != null) Add(extra, "bad");
    }
    return (file, new ReleaseManifest { Version = version, Game = new() { Version = version, Asset = $"Project-Riftwalker-v{version}.zip", Size = new FileInfo(file).Length, UnpackedSize = total, Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))) } });
}
sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(handle(request)); }
}
sealed class TestFolder : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "riftwalker-tests-" + Guid.NewGuid());
    public TestFolder() => Directory.CreateDirectory(Path);
    public void Dispose() => Storage.DeleteDirectory(System.IO.Path.GetDirectoryName(Path)!, System.IO.Path.GetFileName(Path));
}
sealed class BrokenStream(byte[] prefix) : MemoryStream(prefix)
{
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    { if (Position >= Length) throw new IOException("Connection lost"); return base.ReadAsync(buffer, cancellationToken); }
}
