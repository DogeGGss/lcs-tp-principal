using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Riftwalker.Localization;
using UnityEngine;

// F30 (Idiomas), US 349: ningún texto queda sin traducir ni con datos rotos.
// - CA1: cada texto del catálogo en español tiene su versión en cada idioma, y no está vacía.
// - CA2: los datos que se insertan en un texto ({0}, {1:00}...) son los mismos en todos los idiomas.
// - CA3: no hay textos en español escritos en el código que no pasen por el catálogo. Los que no ve el jugador
//   (nombres de objetos, ayudas del inspector, mensajes de depuración) están en TextosQueNoSeTraducen.txt.
public class TraduccionesCompletasTests
{
    private static readonly string Raiz = Path.Combine(Application.dataPath, "Resources", "Localization");
    private static readonly string Lista = Path.Combine(Application.dataPath, "Tests", "EditMode", "TextosQueNoSeTraducen.txt");
    private static readonly Regex Variable = new Regex(@"\{(\d+)(?::[^}]*)?\}");

    private static Dictionary<string, string> Catalogo(string idioma)
    {
        var textos = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string archivo in Directory.GetFiles(Path.Combine(Raiz, idioma), "*.json"))
            foreach (var e in JsonUtility.FromJson<TranslationFile>(File.ReadAllText(archivo)).entries)
                textos[e.key] = e.text;
        return textos;
    }

    private static IEnumerable<string> Idiomas() =>
        JsonUtility.FromJson<LocaleManifest>(File.ReadAllText(Path.Combine(Raiz, "locales.json"))).locales.Select(l => l.code).Where(c => c != "es");

    [Test, Category("US 349"), Description("CA1: todo texto en español tiene su traducción en cada idioma")]
    public void US349_CA1_Todo_texto_tiene_traduccion()
    {
        var es = Catalogo("es");
        var faltan = new List<string>();
        foreach (string idioma in Idiomas())
        {
            var otro = Catalogo(idioma);
            foreach (var e in es)
                if (!otro.TryGetValue(e.Key, out string t) || string.IsNullOrWhiteSpace(t)) faltan.Add($"{idioma}: {e.Key}");
            foreach (string sobra in otro.Keys.Except(es.Keys)) faltan.Add($"{idioma}: sobra {sobra}");
        }
        Assert.IsEmpty(faltan, "Sin traducción: " + string.Join(", ", faltan.Take(20)));
    }

    [Test, Category("US 349"), Description("CA2: los datos insertados ({0}) son los mismos en todos los idiomas")]
    public void US349_CA2_Datos_insertados_iguales()
    {
        var es = Catalogo("es");
        var distintas = new List<string>();
        foreach (string idioma in Idiomas())
            foreach (var e in Catalogo(idioma))
                if (es.TryGetValue(e.Key, out string fuente) && Variables(fuente) != Variables(e.Value)) distintas.Add($"{idioma}: {e.Key}");
        Assert.IsEmpty(distintas, "Datos distintos: " + string.Join(", ", distintas.Take(20)));
    }

    private static string Variables(string texto) =>
        string.Join(",", Variable.Matches(texto).Cast<Match>().Select(m => m.Groups[1].Value).Distinct().OrderBy(v => v));

    [Test, Category("US 349"), Description("CA3: no quedan textos en español en el código sin pasar por el catálogo")]
    public void US349_CA3_Sin_textos_en_espanol_en_el_codigo()
    {
        var permitidos = File.Exists(Lista)
            ? new HashSet<string>(File.ReadAllLines(Lista).Where(l => l.Length > 0 && !l.StartsWith("#")), StringComparer.Ordinal)
            : new HashSet<string>();
        var encontrados = EscanerDeTextos.Buscar(Path.Combine(Application.dataPath, "Scripts"), Catalogo("es").Values);
        var nuevos = encontrados.Where(t => !permitidos.Contains(t)).ToList();
        try
        {
            string carpeta = Environment.GetEnvironmentVariable("RIFTWALKER_EVIDENCIA");
            if (string.IsNullOrEmpty(carpeta)) carpeta = Path.Combine(Path.GetTempPath(), "riftwalker-evidencia-idiomas");
            Directory.CreateDirectory(carpeta);
            var informe = new StringBuilder($"US-349_CA3 · {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
            informe.AppendLine(nuevos.Count == 0 ? "Resultado: sin textos nuevos en español" : $"Resultado: {nuevos.Count} textos en español sin traducir");
            foreach (string t in nuevos) informe.AppendLine(t);
            informe.AppendLine($"Revisados: {encontrados.Count} textos que no ve el jugador (TextosQueNoSeTraducen.txt)");
            File.WriteAllText(Path.Combine(carpeta, "US-349_CA3_Escaner.txt"), informe.ToString(), new UTF8Encoding(false));
        }
        catch (IOException) { }
        Assert.IsEmpty(nuevos, "Textos en español sin pasar por el catálogo (traducirlos con L10n o, si el jugador no los ve, " +
                               "sumarlos a TextosQueNoSeTraducen.txt):\n" + string.Join("\n", nuevos.Take(30)));
    }
}

/// <summary>Busca en los scripts los textos entre comillas que parecen español y no están en el catálogo.</summary>
public static class EscanerDeTextos
{
    private static readonly Regex Literal = new Regex("\\$?@?\"((?:[^\"\\\\\\n]|\\\\.)*)\"");
    private static readonly Regex Palabras = new Regex(@"\b(de|la|el|los|las|que|tu|tus|con|sin|para|por|en|un|una|del|al|se|no|es|más|ya|vos|te)\b", RegexOptions.IgnoreCase);
    private static readonly Regex Tilde = new Regex("[áéíóúñÁÉÍÓÚÑ¿¡]");
    private static readonly Regex Formato = new Regex(@"<[^>]+>|\{[^}]*\}");
    private static readonly Regex Comentario = new Regex(@"\s//\s.*$");
    // Lugares donde un texto no lo ve el jugador, o ya pasa por L10n.
    private static readonly Regex NoVisible = new Regex(
        @"Debug\.|Log(Warning|Error)?\(|Exception\(|new GameObject\(|Node\(|Hijo\(|Rect\(|Pieza<|Marca\(|Fuente\(|Crear\(|Find\(|" +
        @"\.name\s*=|name\s*=\s*""|Category\(|Description\(|Tooltip\(|Header\(|MenuItem|CreateAssetMenu|L10n\.|case\s+""|==\s*""|" +
        @"!=\s*""|Veces\(|Tiene\(|StartsWith\(|Contains\(|Equals\(|Resources\.Load|Shader\.|PlayerPrefs");

    /// <summary>Cada resultado es "Ruta/Archivo.cs: texto".</summary>
    public static List<string> Buscar(string carpeta, IEnumerable<string> catalogo)
    {
        var conocidos = new HashSet<string>(catalogo, StringComparer.Ordinal);
        var resultado = new List<string>();
        foreach (string archivo in Directory.GetFiles(carpeta, "*.cs", SearchOption.AllDirectories).OrderBy(a => a, StringComparer.Ordinal))
        {
            string relativo = archivo.Substring(carpeta.Length + 1).Replace('\\', '/');
            if (relativo.StartsWith("Localization/")) continue;
            foreach (string linea in File.ReadAllLines(archivo))
            {
                string s = linea.Trim();
                if (s.StartsWith("//") || s.StartsWith("*") || s.StartsWith("/*") || s.StartsWith("[")) continue;
                string codigo = Comentario.Replace(linea, "");
                if (NoVisible.IsMatch(codigo)) continue;
                foreach (Match m in Literal.Matches(codigo))
                {
                    string texto = m.Groups[1].Value.Replace("\\n", "\n");
                    string visible = Formato.Replace(texto, " ");
                    if (!(Tilde.IsMatch(visible) || (visible.Trim().Contains(" ") && Palabras.IsMatch(visible)))) continue;
                    if (conocidos.Contains(texto)) continue;
                    string fila = relativo + ": " + m.Groups[1].Value;
                    if (!resultado.Contains(fila)) resultado.Add(fila);
                }
            }
        }
        return resultado;
    }
}
