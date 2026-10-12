using System.Text.Json;

namespace Riftwalker.Launcher;

public static class Storage
{
    public static T? Read<T>(string path)
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), ReleaseClient.Json) : default; }
        catch (JsonException) { return default; }
    }
    public static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, ReleaseClient.Json));
        File.Move(temp, path, true);
    }
    public static string Child(string root, string relative)
    {
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(Path.Combine(fullRoot, relative));
        if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Ruta fuera de la instalación.");
        for (string? part = full; part != null && part.Length >= fullRoot.Length - 1; part = Path.GetDirectoryName(part))
            if ((File.Exists(part) || Directory.Exists(part)) && (File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("La carpeta contiene un enlace; elegí una carpeta normal.");
        return full;
    }
    public static void DeleteDirectory(string root, string relative)
    {
        string path = Child(root, relative);
        if (!Directory.Exists(path)) return;
        // Never follow junctions or symlinks, including ones inside a previous installation.
        foreach (string entry in Directory.EnumerateFileSystemEntries(path))
        {
            Child(root, Path.GetRelativePath(root, entry));
            if (Directory.Exists(entry)) DeleteDirectory(root, Path.GetRelativePath(root, entry));
            else File.Delete(entry);
        }
        Directory.Delete(path);
    }
}
// Size: window size in Ajustes, "chica", "normal" or "grande" (16:9, almost full screen).
public sealed record LauncherSettings(bool Experimental = false, string Size = "normal");
public sealed record InstalledGame(string Version, string Executable);
