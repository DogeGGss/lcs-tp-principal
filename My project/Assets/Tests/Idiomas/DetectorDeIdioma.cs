using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Riftwalker.Localization;
using TMPro;
using UnityEngine;

// F30 (Idiomas), US 347 y US 348: con el juego en inglés, junta todos los textos que se ven en pantalla y marca los que
// siguen en español. Un texto está en español si es un texto del catálogo en español que en inglés es otro, o si es una
// frase con palabras que solo se usan en español (de, la, que, con, tu...). Los nombres propios (La Porteña, Gran
// Capitán...) no cuentan: son las palabras con tilde que aparecen en el catálogo en inglés, más los de NombresPropios.
// Cada revisión deja como evidencia una captura y la lista de textos en la carpeta de Evidencia (ver pruebas-idiomas).
public static class DetectorDeIdioma
{
    public const string VariableEvidencia = "RIFTWALKER_EVIDENCIA";

    // Nombres que no se traducen y que no salen en el catálogo en inglés.
    private static readonly string[] NombresPropios =
    {
        "Nicolás", "Gauna", "Velázquez", "Gómez", "Huber", "Barreto", "Fonseca", "Ramos", "Lucas", "Luka", "Franco",
        "Nahuel", "Ulises", "David", "Unzué", "Porteña", "Capitán", "UNGS", "Riftwalker",
    };
    private static readonly Regex Etiqueta = new Regex("<[^>]+>");
    private static readonly Regex Espacios = new Regex(@"\s+");
    private static readonly Regex Palabra = new Regex(@"[\p{L}]+");
    private static readonly HashSet<string> SoloEspanol = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "de", "la", "el", "los", "las", "que", "con", "sin", "para", "por", "tu", "tus", "del", "al", "se", "es", "una",
        "un", "y", "en", "más", "ya", "vos", "te", "tenés", "podés", "está", "están", "acá", "hasta", "cuando", "sobre",
    };

    private static HashSet<string> espanol, ingles, palabrasPropias;

    /// <summary>El texto que se ve, sin etiquetas de formato y con los espacios juntos.</summary>
    public static string Visible(string texto) => Espacios.Replace(Etiqueta.Replace(texto ?? "", " "), " ").Trim();

    public static bool EstaEnEspanol(string texto)
    {
        Cargar();
        string v = Visible(texto);
        if (v.Length < 2 || ingles.Contains(v)) return false;
        if (espanol.Contains(v)) return true;
        var palabras = Palabra.Matches(v).Cast<Match>().Select(m => m.Value).Where(p => !palabrasPropias.Contains(p)).ToList();
        if (palabras.Count == 0) return false;
        bool tildes = palabras.Any(p => p.IndexOfAny("áéíóúñÁÉÍÓÚÑ¿¡".ToCharArray()) >= 0);
        int soloEspanol = palabras.Count(p => SoloEspanol.Contains(p));
        return tildes || (soloEspanol >= 1 && palabras.Count >= 2) || (v.Contains("¿") || v.Contains("¡"));
    }

    /// <summary>Los textos activos en pantalla (TextMeshPro y Text de uGUI).</summary>
    public static List<string> TextosVisibles()
    {
        var textos = new List<string>();
        foreach (TMP_Text t in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude))
            if (t.enabled && t.isActiveAndEnabled && t.alpha > 0.01f && !string.IsNullOrWhiteSpace(t.text) && Visible(t.text).Length > 0) textos.Add(t.text);
        foreach (UnityEngine.UI.Text t in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Exclude))
            if (t.isActiveAndEnabled && t.color.a > 0.01f && !string.IsNullOrWhiteSpace(t.text)) textos.Add(t.text);
        return textos;
    }

    public static List<string> TextosEnEspanol() => TextosVisibles().Where(EstaEnEspanol).Select(Visible).Distinct().ToList();

    /// <summary>
    /// Revisa la pantalla, guarda la evidencia (captura y lista de textos) con el nombre del caso y devuelve los
    /// textos que quedaron en español. Usar con yield return dentro de un UnityTest.
    /// </summary>
    public static IEnumerator Revisar(string caso, List<string> enEspanol)
    {
        yield return new WaitForEndOfFrame();
        enEspanol.Clear();
        enEspanol.AddRange(TextosEnEspanol());
        string carpeta = CarpetaEvidencia();
        try
        {
            Directory.CreateDirectory(carpeta);
            var informe = new StringBuilder();
            informe.AppendLine($"{caso} · idioma {L10n.LanguageCode} · {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            informe.AppendLine(enEspanol.Count == 0 ? "Resultado: sin textos en español" : $"Resultado: {enEspanol.Count} textos en español");
            foreach (string t in enEspanol) informe.AppendLine("  EN ESPAÑOL: " + t);
            informe.AppendLine("Textos en pantalla:");
            foreach (string t in TextosVisibles().Select(Visible).Distinct()) informe.AppendLine("  " + t);
            File.WriteAllText(Path.Combine(carpeta, caso + ".txt"), informe.ToString(), new UTF8Encoding(false));
            Texture2D captura = ScreenCapture.CaptureScreenshotAsTexture();
            if (captura != null)
            {
                File.WriteAllBytes(Path.Combine(carpeta, caso + ".png"), captura.EncodeToPNG());
                UnityEngine.Object.Destroy(captura);
            }
        }
        catch (Exception e) { Debug.LogWarning("No se pudo guardar la evidencia de " + caso + ": " + e.Message); }
    }

    public static string CarpetaEvidencia()
    {
        string desdeAfuera = Environment.GetEnvironmentVariable(VariableEvidencia);
        return !string.IsNullOrEmpty(desdeAfuera) ? desdeAfuera : Path.Combine(Path.GetTempPath(), "riftwalker-evidencia-idiomas");
    }

    public static string Mensaje(List<string> enEspanol) =>
        "Quedaron en español: " + string.Join(" | ", enEspanol.Take(15)) + (enEspanol.Count > 15 ? $" (y {enEspanol.Count - 15} más)" : "");

    private static void Cargar()
    {
        if (espanol != null) return;
        var porClave = new Dictionary<string, string>();
        ingles = new HashSet<string>(StringComparer.Ordinal);
        foreach (string dominio in new[] { "ui", "gameplay", "profile", "launcher" })
        {
            var es = Leer("es", dominio); var en = Leer("en", dominio);
            foreach (var e in es) porClave[e.key] = e.text;
            foreach (var e in en)
            {
                ingles.Add(Visible(e.text));
                if (porClave.TryGetValue(e.key, out string texto) && Visible(texto) == Visible(e.text)) porClave.Remove(e.key);
            }
        }
        espanol = new HashSet<string>(porClave.Values.Select(Visible).Where(t => !t.Contains("{")), StringComparer.Ordinal);
        palabrasPropias = new HashSet<string>(NombresPropios, StringComparer.OrdinalIgnoreCase);
        foreach (string t in ingles)
            foreach (Match m in Palabra.Matches(t))
                if (m.Value.IndexOfAny("áéíóúñÁÉÍÓÚÑ".ToCharArray()) >= 0) palabrasPropias.Add(m.Value);
    }

    private static TranslationEntry[] Leer(string idioma, string dominio)
    {
        var archivo = Resources.Load<TextAsset>($"Localization/{idioma}/{dominio}");
        if (archivo == null) return Array.Empty<TranslationEntry>();
        var datos = JsonUtility.FromJson<TranslationFile>(archivo.text);
        return datos?.entries ?? Array.Empty<TranslationEntry>();
    }
}
