using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

namespace Riftwalker.Launcher;

public sealed class GameInstaller
{
    public string Root { get; }
    public string Game => Storage.Child(Root, "game");
    private string Next => Storage.Child(Root, ".riftwalker/game.next");
    private string Previous => Storage.Child(Root, ".riftwalker/game.previous");
    public GameInstaller(string root) { Root = Path.GetFullPath(root); }
    public InstalledGame? Installed
    {
        get
        {
            var data = Storage.Read<InstalledGame>(Storage.Child(Game, "installed.json"));
            return data != null && Riftwalker.Releases.ReleaseVersion.TryParse(data.Version, out _) && data.Executable == "Project Riftwalker.exe"
                && File.Exists(Storage.Child(Game, data.Executable)) ? data : null;
        }
    }
    public void Recover()
    {
        // The receipt travels with game/. A crash between directory renames restores the previous game.
        if (!Directory.Exists(Game) && Directory.Exists(Previous)) Directory.Move(Previous, Game);
        if (Directory.Exists(Game) && Installed == null)
            throw new IOException("La carpeta game no es una instalación válida. Revisala antes de continuar.");
    }
    public bool IsRunning()
    {
        foreach (var process in Process.GetProcessesByName("Project Riftwalker"))
            using (process)
            {
                try
                {
                    if (string.Equals(process.MainModule?.FileName, Path.Combine(Game, "Project Riftwalker.exe"), StringComparison.OrdinalIgnoreCase)) return true;
                }
                catch (System.ComponentModel.Win32Exception) { return true; } // inaccessible process: do not risk replacement
                catch (InvalidOperationException) { }
            }
        return false;
    }
    public string DownloadPath(Package p) => Storage.Child(Root, $".riftwalker/{p.Sha256.ToLowerInvariant()}.part");
    public void CheckSpace(Package p)
    {
        long partial = File.Exists(DownloadPath(p)) ? Math.Min(p.Size, new FileInfo(DownloadPath(p)).Length) : 0;
        long required = checked(p.Size - partial + p.UnpackedSize + 128L * 1024 * 1024);
        long free = new DriveInfo(Path.GetPathRoot(Root)!).AvailableFreeSpace;
        if (free < required) throw new IOException($"No hay espacio suficiente. Faltan {Math.Ceiling((required - free) / 1048576d):N0} MB.");
    }
    public async Task InstallAsync(string zipPath, ReleaseManifest manifest, IProgress<string>? stage, CancellationToken token)
    {
        Recover();
        if (IsRunning()) throw new IOException("Cerrá el juego para actualizar.");
        if (!await Downloader.VerifyAsync(zipPath, manifest.Game, token))
            throw new InvalidDataException("El archivo descargado no pasó la verificación.");
        Storage.DeleteDirectory(Root, ".riftwalker/game.next");
        Directory.CreateDirectory(Next);
        try
        {
            stage?.Report("Verificando e instalando…");
            await Task.Run(() => Extract(zipPath, manifest.Game, token), token);
            token.ThrowIfCancellationRequested();
            if (IsRunning()) throw new IOException("Cerrá el juego para actualizar.");
            Storage.Write(Path.Combine(Next, "installed.json"), new InstalledGame(manifest.Version, manifest.Game.Executable));
            // No cancellation inside the short commit. Keep the previous directory until a future update.
            Storage.DeleteDirectory(Root, ".riftwalker/game.previous");
            if (Directory.Exists(Game)) Directory.Move(Game, Previous);
            try { Directory.Move(Next, Game); }
            catch
            {
                if (!Directory.Exists(Game) && Directory.Exists(Previous)) Directory.Move(Previous, Game);
                throw;
            }
        }
        catch
        {
            Storage.DeleteDirectory(Root, ".riftwalker/game.next");
            throw;
        }
        // Cleanup cannot turn a successful commit into an installation failure.
        try { File.Delete(zipPath); } catch (IOException) { }
    }
    private void Extract(string zipPath, Package p, CancellationToken token)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        long size = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (zip.Entries.Count > 100000) throw new InvalidDataException("El ZIP contiene demasiados archivos.");
        foreach (var entry in zip.Entries)
        {
            token.ThrowIfCancellationRequested();
            string relative = entry.FullName.Replace('\\', '/');
            if (relative.StartsWith('/') || relative.Contains(':') || relative.Split('/').Any(p => p == ".." || p.EndsWith(' ') || p.EndsWith('.'))
                || relative.Equals("installed.json", StringComparison.OrdinalIgnoreCase) || !seen.Add(relative)
                || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                throw new InvalidDataException("El ZIP contiene una ruta no permitida.");
            string target = Storage.Child(Next, relative);
            size = checked(size + entry.Length);
            if (size > p.UnpackedSize) throw new InvalidDataException("El ZIP supera el tamaño descomprimido publicado.");
            if (relative.EndsWith('/')) { Directory.CreateDirectory(target); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var input = entry.Open();
            using var output = File.Create(target);
            byte[] buffer = new byte[131072];
            long written = 0;
            int read;
            while ((read = input.Read(buffer)) > 0)
            {
                token.ThrowIfCancellationRequested();
                written += read;
                if (written > entry.Length) throw new InvalidDataException("Contenido ZIP inválido.");
                output.Write(buffer, 0, read);
            }
            if (written != entry.Length) throw new InvalidDataException("Archivo ZIP incompleto.");
        }
        if (size != p.UnpackedSize || !File.Exists(Path.Combine(Next, p.Executable))
            || !Directory.Exists(Path.Combine(Next, "Project Riftwalker_Data")))
            throw new InvalidDataException("El ZIP no contiene el build completo de Project Riftwalker en su raíz.");
        var stamp = Storage.Read<BuildStamp>(Path.Combine(Next, "riftwalker-build.json"));
        if (stamp?.Version != p.Version) throw new InvalidDataException("La versión del build no coincide con el release.");
    }
    public Process? Launch()
    {
        var installed = Installed ?? throw new IOException("Primero instalá el juego.");
        return Process.Start(new ProcessStartInfo(Storage.Child(Game, installed.Executable)) { WorkingDirectory = Game, UseShellExecute = true });
    }
    private sealed record BuildStamp(string Version);
}
