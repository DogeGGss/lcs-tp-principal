using System;
using System.IO;
using System.Linq;
using Riftwalker.Releases;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// US 205. Generates a stamped Windows build; release packaging/publication is in Launcher/tools.
public class PublicarVersion : EditorWindow
{
    private string version = "0.2.0";
    [MenuItem("Riftwalker/Publicar versión")]
    public static void Abrir() => GetWindow<PublicarVersion>(true, "Publicar versión");
    private void OnGUI()
    {
        GUILayout.Label("PROJECT RIFTWALKER", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Genera un build Windows con su versión real. Luego ejecutá el publicador de Launcher/tools para preparar el release de GitHub.", MessageType.Info);
        version = EditorGUILayout.TextField("Versión", version);
        EditorGUILayout.LabelField("Formato", "0.2.0 o 0.2.0-exp.1");
        using (new EditorGUI.DisabledScope(EditorApplication.isCompiling || EditorApplication.isPlaying))
            if (GUILayout.Button("Generar build para publicar"))
            {
                string parent = EditorUtility.OpenFolderPanel("Carpeta para los builds", Path.GetFullPath(Path.Combine(Application.dataPath, "../../Builds")), "");
                if (string.IsNullOrEmpty(parent)) return;
                try
                {
                    Generar(version, parent);
                    EditorUtility.DisplayDialog("Build preparado", "Build listo. Seguí Launcher/README.md para empaquetarlo y publicarlo.\n\n" +
                        "La versión del proyecto quedó en " + PlayerSettings.bundleVersion + ": subí ProjectSettings/ProjectSettings.asset con ese cambio, " +
                        "así el editor y los builds del equipo tienen la misma versión que el release (Photon solo junta versiones iguales).", "Aceptar");
                }
                catch (Exception ex) { Debug.LogException(ex); EditorUtility.DisplayDialog("No se pudo generar", ex.Message, "Aceptar"); }
            }
    }
    public static void Generar(string requested, string parent)
    {
        string normalized = ReleaseVersion.Parse(requested).ToString();
        string output = Path.Combine(parent, "Project-Riftwalker-v" + normalized);
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new IOException("La carpeta de esta versión ya contiene archivos. Elegí otra carpeta o versión.");
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0) throw new InvalidOperationException("No hay escenas habilitadas en Build Settings.");
        Directory.CreateDirectory(output);
        string previousVersion = PlayerSettings.bundleVersion;
        bool success = false;
        try
        {
            PlayerSettings.bundleVersion = normalized;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes, locationPathName = Path.Combine(output, "Project Riftwalker.exe"),
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("El build falló. Revisá la consola de Unity.");
            File.WriteAllText(Path.Combine(output, "riftwalker-build.json"), JsonUtility.ToJson(new Stamp { version = normalized }, true));
            success = true;
        }
        finally { if (!success) PlayerSettings.bundleVersion = previousVersion; }
    }
    [Serializable] private class Stamp { public string version; }
}
