using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;

namespace Riftwalker.Launcher;

public static class SelfUpdate
{
    public static async Task StartAsync(string root, string url, Package package, HttpClient http,
        IProgress<TransferProgress> progress, CancellationToken token)
    {
        var installer = new GameInstaller(root);
        installer.CheckSpace(package with { UnpackedSize = package.Size * 2 });
        string part = installer.DownloadPath(package);
        await new Downloader(http).DownloadAsync(url, package, part, progress, token);
        string helper = Storage.Child(root, ".riftwalker/launcher-update.exe");
        File.Copy(part, helper, true);
        File.Delete(part);
        var start = new ProcessStartInfo(helper) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = root };
        start.ArgumentList.Add("--apply-update"); start.ArgumentList.Add(Environment.ProcessId.ToString());
        start.ArgumentList.Add(root); start.ArgumentList.Add(package.Sha256);
        using var helperProcess = Process.Start(start) ?? throw new IOException("No se pudo iniciar la actualización del launcher.");
    }
    public static async Task ApplyAsync(int parent, string root, string hash)
    {
        string helper = Storage.Child(root, ".riftwalker/launcher-update.exe");
        if (!string.Equals(Path.GetFullPath(Environment.ProcessPath!), helper, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(Storage.Child(root, ".riftwalker/installation.json"))) throw new IOException("Actualización fuera de la instalación.");
        using (var input = File.OpenRead(helper))
            if (!Convert.ToHexString(await SHA256.HashDataAsync(input)).Equals(hash, StringComparison.OrdinalIgnoreCase))
                throw new IOException("La actualización del launcher está dañada.");
        try { using var process = Process.GetProcessById(parent); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)); }
        catch (ArgumentException) { }
        catch (TimeoutException) { return; } // The old launcher is still open: nothing is replaced; it retries next time.
        string target = Storage.Child(root, "Riftwalker-Launcher.exe"), backup = Storage.Child(root, ".riftwalker/launcher.previous.exe");
        string next = Storage.Child(root, ".riftwalker/launcher.next.exe"), health = Storage.Child(root, ".riftwalker/update-ok");
        Process? launched = null;
        bool replaced = false;
        try
        {
            using (var guard = new FileStream(Storage.Child(root, ".riftwalker/launcher.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                File.Copy(helper, next, true);
                if (File.Exists(health)) File.Delete(health);
                File.Replace(next, target, backup, true);
                replaced = true;
            }
            var info = new ProcessStartInfo(target) { WorkingDirectory = root, UseShellExecute = false };
            info.ArgumentList.Add("--updated");
            launched = Process.Start(info) ?? throw new IOException("No se pudo abrir el nuevo launcher.");
            for (int i = 0; i < 150; i++)
            {
                if (File.Exists(health)) return; // MainWindow finished initializing: replacement is healthy.
                if (launched.HasExited) break;
                await Task.Delay(200);
            }
            if (!launched.HasExited) { launched.Kill(); await launched.WaitForExitAsync(); }
            throw new IOException("El nuevo launcher no terminó de iniciar.");
        }
        catch
        {
            if (replaced)
                using (var guard = new FileStream(Storage.Child(root, ".riftwalker/launcher.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                    File.Copy(backup, target, true);
            Storage.Write(Storage.Child(root, ".riftwalker/failed-update.json"), new { hash });
            var retry = new ProcessStartInfo(target) { WorkingDirectory = root, UseShellExecute = true };
            retry.ArgumentList.Add("--skip-self-update");
            Process.Start(retry);
        }
        finally { launched?.Dispose(); }
    }
}
