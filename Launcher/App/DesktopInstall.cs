using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Riftwalker.Launcher;

public static class DesktopInstall
{
    public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Project Riftwalker");
    public static string Install(string requested)
    {
        string root = Path.GetFullPath(requested.Trim());
        if (root == Path.GetPathRoot(root)) throw new IOException("Elegí una carpeta propia para el juego, no la raíz del disco.");
        Storage.Child(root, ".riftwalker/installation.json");
        bool existing = File.Exists(Path.Combine(root, ".riftwalker", "installation.json"));
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any() && !existing)
            throw new IOException("Elegí una carpeta vacía para instalar Riftwalker.");
        Directory.CreateDirectory(Path.Combine(root, ".riftwalker"));
        string destination = Path.Combine(root, "Riftwalker-Launcher.exe");
        if (!File.Exists(destination)) File.Copy(Environment.ProcessPath!, destination);
        Storage.Write(Path.Combine(root, ".riftwalker", "installation.json"), new { schema = 1 });
        Shortcut(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), destination, root);
        Shortcut(Environment.GetFolderPath(Environment.SpecialFolder.Programs), destination, root);
        return root;
    }
    private static void Shortcut(string folder, string target, string root)
    {
        if (string.IsNullOrWhiteSpace(folder)) return;
        Directory.CreateDirectory(folder);
        object? shell = null, shortcut = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
            dynamic wsh = shell!;
            shortcut = wsh.CreateShortcut(Path.Combine(folder, "Project Riftwalker.lnk"));
            dynamic link = shortcut!;
            link.TargetPath = target; link.WorkingDirectory = root; link.Description = "Project Riftwalker"; link.Save();
        }
        finally
        {
            if (shortcut != null) Marshal.FinalReleaseComObject(shortcut);
            if (shell != null) Marshal.FinalReleaseComObject(shell);
        }
    }
}
