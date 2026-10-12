using System.IO;
using System.Windows;

namespace Riftwalker.Launcher;

public partial class App : Application
{
    private FileStream? installationLock;
    public static string LauncherVersion => typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
    public static bool Preview { get; private set; }
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            if (e.Args.Length == 4 && e.Args[0] == "--apply-update")
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                await SelfUpdate.ApplyAsync(int.Parse(e.Args[1]), e.Args[2], e.Args[3]);
                Shutdown(); return;
            }
            Preview = e.Args.Contains("--preview") || e.Args.Contains("--render");
            string root = AppContext.BaseDirectory;
            if (!Preview && File.Exists(Path.Combine(root, ".riftwalker", "installation.json")))
            {
                try { AcquireLock(root); }
                catch (IOException ex) { throw new IOException("Puede que el launcher ya esté abierto en esta instalación.\n\n" + ex.Message, ex); }
            }
            var window = new MainWindow(root, e.Args);
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show("No se pudo abrir el launcher. " + ex.Message, "Project Riftwalker", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown(1);
        }
    }
    public void AcquireLock(string root)
    {
        installationLock ??= new FileStream(Storage.Child(root, ".riftwalker/launcher.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    public void ReleaseLock() { installationLock?.Dispose(); installationLock = null; }
    protected override void OnExit(ExitEventArgs e) { ReleaseLock(); base.OnExit(e); }
}
