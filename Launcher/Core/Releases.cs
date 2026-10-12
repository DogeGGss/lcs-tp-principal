using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Riftwalker.Releases;

namespace Riftwalker.Launcher;

public sealed record ReleaseAsset
{
    public long Id { get; init; }
    public string Name { get; init; } = "";
    public long Size { get; init; }
    [JsonPropertyName("browser_download_url")] public string Url { get; init; } = "";
}
public sealed record GitHubRelease
{
    [JsonPropertyName("tag_name")] public string Tag { get; init; } = "";
    public string Name { get; init; } = "";
    public string Body { get; init; } = "";
    public bool Draft { get; init; }
    public bool Prerelease { get; init; }
    [JsonPropertyName("published_at")] public DateTimeOffset Published { get; init; }
    public List<ReleaseAsset> Assets { get; init; } = [];
    [JsonIgnore] public ReleaseVersion Version => ReleaseVersion.Parse(Tag);
    [JsonIgnore] public bool Compatible => !Draft && ReleaseVersion.TryParse(Tag, out var v)
        && v.IsExperimental == Prerelease && Assets.Any(a => a.Name == "release.json")
        && Assets.Any(a => a.Name == $"Project-Riftwalker-v{v}.zip");
}
public sealed record Package
{
    public string Asset { get; init; } = "";
    public string Sha256 { get; init; } = "";
    public long Size { get; init; }
    public long UnpackedSize { get; init; }
    public string Executable { get; init; } = "Project Riftwalker.exe";
    public string Version { get; init; } = "";
}
public sealed record ReleaseManifest
{
    public int Schema { get; init; } = 1;
    public string Version { get; init; } = "";
    public Package Game { get; init; } = new();
    public Package? Launcher { get; init; }
    public void Validate(GitHubRelease release)
    {
        if (Schema != 1 || Version != release.Version.ToString() || Game.Version != Version
            || Game.Asset != $"Project-Riftwalker-v{Version}.zip" || Game.Executable != "Project Riftwalker.exe"
            || Game.UnpackedSize <= 0) throw new InvalidDataException("El release no tiene un formato compatible.");
        ValidatePackage(Game, release);
        if (Launcher != null)
        {
            if (Launcher.Asset != "Riftwalker-Launcher.exe" || !ReleaseVersion.TryParse(Launcher.Version, out _))
                throw new InvalidDataException("Versión del launcher inválida.");
            ValidatePackage(Launcher, release);
        }
    }
    private static void ValidatePackage(Package p, GitHubRelease r)
    {
        if (p.Size <= 0 || !System.Text.RegularExpressions.Regex.IsMatch(p.Sha256, "^[a-fA-F0-9]{64}$")
            || r.Assets.Count(a => a.Name == p.Asset && a.Size == p.Size) != 1)
            throw new InvalidDataException("Tamaño o verificación del archivo inválidos.");
    }
}
// cacheDirectory: where the API answers are kept with their ETag (the launcher's .riftwalker). null = no cache.
public sealed class ReleaseClient(HttpClient http, string? cacheDirectory = null)
{
    public const string Repository = "DogeGGss/lcs-tp-principal";
    public const string ReleasesUrl = "https://github.com/" + Repository + "/releases";
    // A plain download, not the API: it does not count against the 60 requests per hour per IP of the anonymous API.
    public const string LatestManifestUrl = ReleasesUrl + "/latest/download/release.json";
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        string version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.1.0";
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Riftwalker-Launcher/" + version);
        return client;
    }
    public async Task<List<GitHubRelease>> FetchAsync(CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var result = new List<GitHubRelease>();
        for (int page = 1; ; page++)
        {
            var batch = await PageAsync(page, timeout.Token);
            result.AddRange(batch.Where(r => r.Compatible));
            if (batch.Count < 100) break;
        }
        return result.OrderByDescending(r => r.Version).ToList();
    }

    // One page of the API, revalidated with its ETag: an unchanged list answers 304 and is read from the cache.
    // GitHub does not count those 304 against the rate limit, so reopening the launcher costs nothing.
    private async Task<List<GitHubRelease>> PageAsync(int page, CancellationToken token)
    {
        string? cachePath = cacheDirectory == null ? null : Path.Combine(cacheDirectory, $"releases-page{page}.json");
        var cached = cachePath == null ? null : Storage.Read<CachedPage>(cachePath);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repository}/releases?per_page=100&page={page}");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        if (cached != null) request.Headers.TryAddWithoutValidation("If-None-Match", cached.ETag);
        using var response = await http.SendAsync(request, token);
        string json;
        if (response.StatusCode == System.Net.HttpStatusCode.NotModified && cached != null) json = cached.Body;
        else
        {
            response.EnsureSuccessStatusCode();
            json = await response.Content.ReadAsStringAsync(token);
            if (cachePath != null && response.Headers.ETag != null) Storage.Write(cachePath, new CachedPage(response.Headers.ETag.ToString(), json));
        }
        return JsonSerializer.Deserialize<List<GitHubRelease>>(json, Json) ?? [];
    }
    private sealed record CachedPage(string ETag, string Body);

    // Without the API (offline API, or the rate limit reached in a shared network like the university's): the latest
    // stable release still comes from its release.json, a plain download. Returns null if the latest stable release
    // does not have the launcher format yet (404).
    public async Task<(GitHubRelease Release, ReleaseManifest Manifest)?> LatestStableAsync(CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var response = await http.GetAsync(LatestManifestUrl, timeout.Token);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var manifest = await response.Content.ReadFromJsonAsync<ReleaseManifest>(Json, timeout.Token)
            ?? throw new InvalidDataException("Falta el manifiesto del release.");
        var release = FromManifest(manifest);
        manifest.Validate(release);
        return (release, manifest);
    }

    // ---------- The launcher's own release ----------
    // The launcher is published apart from the game versions, in one fixed release (tag "launcher") with only its EXE and
    // launcher.json: players download it once from a link that never changes, and it updates itself from there. The
    // game versions carry only the game. The tag is not a version, so that release never counts as one.
    public const string LauncherTag = "launcher";
    public const string LauncherAsset = "Riftwalker-Launcher.exe";
    public const string LauncherDownloadUrl = ReleasesUrl + "/download/" + LauncherTag + "/" + LauncherAsset;
    public const string LauncherManifestUrl = ReleasesUrl + "/download/" + LauncherTag + "/launcher.json";

    // The published launcher (version, size and SHA-256), or null if its release does not exist yet. A plain download,
    // not the API.
    public async Task<Package?> LauncherAsync(CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var response = await http.GetAsync(LauncherManifestUrl, timeout.Token);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var launcher = await response.Content.ReadFromJsonAsync<Package>(Json, timeout.Token)
            ?? throw new InvalidDataException("Falta el manifiesto del launcher.");
        if (launcher.Asset != LauncherAsset || !ReleaseVersion.TryParse(launcher.Version, out var version) || version.IsExperimental
            || launcher.Size <= 0 || !System.Text.RegularExpressions.Regex.IsMatch(launcher.Sha256, "^[a-fA-F0-9]{64}$"))
            throw new InvalidDataException("El manifiesto del launcher no es válido.");
        return launcher;
    }

    // The publisher ends each version's notes with a footer for whoever opens the release page ("se instala desde el
    // launcher"). The launcher shows the notes without it.
    public const string NotesFooterMark = "<!-- riftwalker:pie -->";
    public static string NotesForLauncher(string? body)
    {
        body ??= "";
        int at = body.IndexOf(NotesFooterMark, StringComparison.Ordinal);
        return (at >= 0 ? body[..at] : body).TrimEnd();
    }

    // A release built only from its manifest: the asset addresses follow from the tag. Each file is still checked
    // against the manifest's size and SHA-256 when it is downloaded.
    public static GitHubRelease FromManifest(ReleaseManifest manifest, string body = "")
    {
        if (!ReleaseVersion.TryParse(manifest.Version, out var version) || version.IsExperimental)
            throw new InvalidDataException("El release no tiene un formato compatible.");
        string tag = "v" + version;
        ReleaseAsset Asset(string name, long size) => new() { Name = name, Size = size, Url = $"{ReleasesUrl}/download/{tag}/{name}" };
        var assets = new List<ReleaseAsset> { Asset("release.json", 0), Asset(manifest.Game.Asset, manifest.Game.Size) };
        if (manifest.Launcher != null) assets.Add(Asset(manifest.Launcher.Asset, manifest.Launcher.Size));
        return new GitHubRelease { Tag = tag, Name = "Project Riftwalker " + tag, Body = body, Assets = assets };
    }
    public static GitHubRelease? Select(IEnumerable<GitHubRelease> releases, bool experimental) =>
        releases.Where(r => r.Compatible && (experimental || !r.Prerelease)).OrderByDescending(r => r.Version).FirstOrDefault();
    public async Task<ReleaseManifest> ManifestAsync(GitHubRelease release, CancellationToken token)
    {
        var asset = release.Assets.Single(a => a.Name == "release.json");
        ValidateUrl(asset.Url);
        if (asset.Size > 1024 * 1024) throw new InvalidDataException("Manifiesto demasiado grande.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var manifest = await http.GetFromJsonAsync<ReleaseManifest>(asset.Url, Json, timeout.Token)
            ?? throw new InvalidDataException("Falta el manifiesto del release.");
        manifest.Validate(release);
        return manifest;
    }
    public static void ValidateUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "github.com"
            || !uri.AbsolutePath.StartsWith("/" + Repository + "/releases/download/", StringComparison.Ordinal))
            throw new InvalidDataException("La descarga no pertenece a los releases de Riftwalker.");
    }
}
