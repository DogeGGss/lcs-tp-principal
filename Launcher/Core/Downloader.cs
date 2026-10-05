using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace Riftwalker.Launcher;

public sealed record TransferProgress(long Downloaded, long Total, double BytesPerSecond)
{
    public double Percent => Total > 0 ? 100d * Downloaded / Total : 0;
    public double SecondsLeft => BytesPerSecond > 0 ? (Total - Downloaded) / BytesPerSecond : double.NaN;
}

public sealed class Downloader(HttpClient http)
{
    public async Task DownloadAsync(string url, Package package, string path, IProgress<TransferProgress>? progress, CancellationToken token)
    {
        ReleaseClient.ValidateUrl(url);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // File name is content-addressed by the caller; a partial file can never belong to another release.
        long offset = File.Exists(path) ? new FileInfo(path).Length : 0;
        if (offset > package.Size) { File.Delete(path); offset = 0; }
        if (offset == package.Size)
        {
            if (await VerifyAsync(path, package, token)) return;
            File.Delete(path); offset = 0;
        }
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (offset > 0) request.Headers.Range = new RangeHeaderValue(offset, null);
        using var headerTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        headerTimeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, headerTimeout.Token);
        response.EnsureSuccessStatusCode();
        if (response.StatusCode == HttpStatusCode.PartialContent)
        {
            var range = response.Content.Headers.ContentRange;
            if (range?.From != offset || range.Length != package.Size || range.To != package.Size - 1)
                throw new InvalidDataException("El servidor devolvió un fragmento incorrecto.");
        }
        else offset = 0; // Server ignored Range: restart rather than append a second full ZIP.
        if (response.Content.Headers.ContentLength is long length && length != package.Size - offset)
            throw new InvalidDataException("El tamaño de la descarga no coincide con el release.");

        await using (var file = new FileStream(path, offset > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 131072, true))
        await using (var stream = await response.Content.ReadAsStreamAsync(token))
        {
            var watch = Stopwatch.StartNew();
            long total = offset, lastReport = 0;
            byte[] buffer = new byte[131072];
            while (true)
            {
                using var idle = CancellationTokenSource.CreateLinkedTokenSource(token);
                idle.CancelAfter(TimeSpan.FromSeconds(30));
                int read = await stream.ReadAsync(buffer, idle.Token);
                if (read == 0) break;
                if (total + read > package.Size) throw new InvalidDataException("La descarga supera el tamaño publicado.");
                await file.WriteAsync(buffer.AsMemory(0, read), token);
                total += read;
                if (watch.ElapsedMilliseconds - lastReport >= 100 || total == package.Size)
                {
                    progress?.Report(new(total, package.Size, (total - offset) / Math.Max(.01, watch.Elapsed.TotalSeconds)));
                    lastReport = watch.ElapsedMilliseconds;
                }
            }
        }
        if (new FileInfo(path).Length != package.Size) throw new IOException("Se interrumpió la descarga. Podés reintentar desde donde quedó.");
        if (!await VerifyAsync(path, package, token))
        {
            File.Delete(path);
            throw new InvalidDataException("El archivo está dañado. Reintentá para descargarlo de nuevo.");
        }
    }
    public static async Task<bool> VerifyAsync(string path, Package package, CancellationToken token)
    {
        if (!File.Exists(path) || new FileInfo(path).Length != package.Size) return false;
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, token);
        return Convert.ToHexString(hash).Equals(package.Sha256, StringComparison.OrdinalIgnoreCase);
    }
}
