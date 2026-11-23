using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

// F30 (Idiomas), US 350: revisa la pantalla en portugués, francés o italiano.
// - SinTraducir: textos que siguen en español (o en inglés) aunque el idioma elegido tiene su traducción, y frases con
//   letras que solo usa el español (ñ, ¿, ¡). Los nombres propios y los carteles del mapa no cuentan (DetectorDeIdioma).
// - NoEntran (CA5): textos cortados (con "…" o truncados), que se salen de su recuadro o del botón que los contiene.
//   La prueba compara con la misma pantalla en español, así que solo falla lo que se rompe por el idioma nuevo.
// - Guardar: deja una captura y la lista de textos con el nombre del caso en la carpeta de evidencia.
public static class RevisorDeTextos
{
    public struct NoEntra { public string clave, objeto, texto, motivo; } // clave: la posición en la jerarquía (igual en todos los idiomas)

    private static readonly Regex Palabra = new Regex(@"[\p{L}]+");
    private static readonly Dictionary<string, HashSet<string>> textosDe = new Dictionary<string, HashSet<string>>();
    private static readonly Dictionary<string, HashSet<string>> palabrasDe = new Dictionary<string, HashSet<string>>();
    private static readonly Dictionary<string, Dictionary<string, string>> porClave = new Dictionary<string, Dictionary<string, string>>();

    // ================= Textos sin traducir =================

    /// <summary>El texto visible de cada objeto de texto en pantalla, por su ruta en la jerarquía.</summary>
    public static Dictionary<string, string> TextosPorRuta()
    {
        var textos = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (TMP_Text t in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude))
        {
            if (!t.isActiveAndEnabled || t.alpha <= 0.01f || EnElMundo(t)) continue;
            string ruta = Clave(t.transform), v = DetectorDeIdioma.Visible(t.text);
            if (v.Length > 0 && !textos.ContainsKey(ruta)) textos.Add(ruta, v);
        }
        return textos;
    }

    /// <param name="enEspanol">Los textos de la misma pantalla en español (TextosPorRuta). Un texto que quedó igual que en
    /// español, que no está en el catálogo del idioma y tiene palabras que ese idioma no usa, quedó sin pasar por el
    /// catálogo (por ejemplo, escrito a mano en el código).</param>
    public static List<string> SinTraducir(string idioma, Dictionary<string, string> enEspanol = null)
    {
        Cargar(idioma);
        var propios = new HashSet<string>(textosDe[idioma], StringComparer.Ordinal);
        var otros = new HashSet<string>(StringComparer.Ordinal);
        foreach (string fuente in new[] { "es", "en" })
        {
            Cargar(fuente);
            foreach (var kv in porClave[fuente])
                if (porClave[idioma].TryGetValue(kv.Key, out string propio) && DetectorDeIdioma.Visible(propio) != DetectorDeIdioma.Visible(kv.Value)
                    && !kv.Value.Contains("{"))
                    otros.Add(DetectorDeIdioma.Visible(kv.Value));
        }
        var nombres = Nombres();
        var resultado = new List<string>();
        foreach (string t in DetectorDeIdioma.TextosVisibles().Select(DetectorDeIdioma.Visible).Distinct())
        {
            if (t.Length < 2 || propios.Contains(t) || DetectorDeIdioma.CartelesDelMapa.Contains(t)) continue;
            bool letrasDelEspanol = t.IndexOfAny("¿¡".ToCharArray()) >= 0 || Palabra.Matches(t).Cast<Match>().Any(m =>
                m.Value.IndexOfAny("ñÑ".ToCharArray()) >= 0 && !nombres.Contains(m.Value) && !palabrasDe[idioma].Contains(m.Value));
            bool soloNombres = Palabra.Matches(t).Cast<Match>().All(m => nombres.Contains(m.Value)); // por ejemplo, la tecla "Mouse"
            if ((otros.Contains(t) && !soloNombres) || letrasDelEspanol) resultado.Add(t);
        }
        if (enEspanol != null)
            foreach (var kv in TextosPorRuta())
            {
                if (resultado.Contains(kv.Value) || propios.Contains(kv.Value) || DetectorDeIdioma.CartelesDelMapa.Contains(kv.Value)) continue;
                if (!enEspanol.TryGetValue(kv.Key, out string es) || es != kv.Value) continue;
                bool ajenas = Palabra.Matches(kv.Value).Cast<Match>().Any(m => m.Value.Length >= 4 && !nombres.Contains(m.Value) && !palabrasDe[idioma].Contains(m.Value));
                if (ajenas) resultado.Add(kv.Value);
            }
        return resultado;
    }

    // Teclas y bailes con nombre en inglés: son iguales en todos los idiomas.
    private static readonly string[] Universales =
    {
        "Ctrl", "Shift", "Alt", "Enter", "Esc", "Tab", "Mouse", "VSync", "Twist", "Twerk", "Rumba", "Floss", "Shuffle",
        "Breakdance", "Freestyle", "Flair", "Old", "School", "Round", "Hip", "Hop",
        // Los nombres de los idiomas en el selector van en su propio idioma.
        "Español", "English", "Português", "Brasil", "Français", "Italiano",
        // Créditos: nombres de fuentes, recursos, licencias y roles que se dejan como los publica su autor.
        "Scrum", "Master", "JetBrains", "Mono", "Barlow", "Jeremy", "Tribby", "SIL", "Open", "Font", "License", "Cartoon",
        "Texture", "Pack",
    };

    // Nombres propios (DetectorDeIdioma), los nombres de las armas (El Marplatense, Roca, Urquiza...) y los universales.
    private static HashSet<string> Nombres()
    {
        var nombres = new HashSet<string>(DetectorDeIdioma.NombresPropios.Concat(Universales), StringComparer.OrdinalIgnoreCase);
        foreach (var arma in CatalogoDeSkins.Armas)
            foreach (Match m in Palabra.Matches(arma.nombre ?? "")) nombres.Add(m.Value);
        foreach (Match m in Palabra.Matches(PlayerProfile.Name ?? "")) nombres.Add(m.Value); // el nombre del jugador
        return nombres;
    }

    // Los textos en el mundo (TextMeshPro sin Canvas) son carteles del mapa: no se traducen ni tienen recuadro de UI.
    private static bool EnElMundo(TMP_Text t) => !(t is TextMeshProUGUI);

    // ================= Textos que no entran (CA5) =================

    public static List<NoEntra> NoEntran()
    {
        var resultado = new List<NoEntra>();
        foreach (TMP_Text t in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude))
        {
            if (!t.isActiveAndEnabled || t.alpha <= 0.01f || EnElMundo(t) || DetectorDeIdioma.Visible(t.text).Length == 0) continue;
            Rect rect = t.rectTransform.rect;
            if (rect.width < 4f || rect.height < 4f) continue;
            t.ForceMeshUpdate();
            string motivo = null;
            if (t.isTextTruncated) motivo = "cortado";
            else if (t.overflowMode != TextOverflowModes.Overflow && t.overflowMode != TextOverflowModes.ScrollRect && t.isTextOverflowing) motivo = "no entra";
            else
            {
                Bounds b = t.textBounds;
                float margen = Mathf.Max(3f, t.fontSize * 0.2f);
                if (b.size.x > 0f && (b.min.x < rect.xMin - margen || b.max.x > rect.xMax + margen)) motivo = "se sale a lo ancho";
                else if (b.size.y > 0f && (b.min.y < rect.yMin - margen * 2f || b.max.y > rect.yMax + margen * 2f)) motivo = "se sale a lo alto";
                else if (FueraDelBoton(t, b, margen)) motivo = "se sale del botón";
            }
            if (motivo != null) resultado.Add(new NoEntra { clave = Clave(t.transform), objeto = Ruta(t.transform), texto = DetectorDeIdioma.Visible(t.text), motivo = motivo });
        }
        return resultado;
    }

    // Si el texto está dentro de un botón (hasta dos niveles arriba), no se tiene que salir de él.
    private static bool FueraDelBoton(TMP_Text t, Bounds b, float margen)
    {
        Transform p = t.transform.parent;
        for (int i = 0; i < 2 && p != null; i++, p = p.parent)
        {
            if (p.GetComponent<UnityEngine.UI.Selectable>() == null || !(p is RectTransform boton)) continue;
            Rect r = boton.rect;
            if (r.width < 4f || r.height < 4f) return false;
            foreach (Vector3 esquina in new[] { new Vector3(b.min.x, b.min.y), new Vector3(b.max.x, b.max.y) })
            {
                Vector3 local = boton.InverseTransformPoint(t.transform.TransformPoint(esquina));
                if (local.x < r.xMin - margen || local.x > r.xMax + margen) return true;
            }
            return false;
        }
        return false;
    }

    // La posición de cada objeto entre sus hermanos, de la raíz al texto: no cambia con el idioma (los nombres sí pueden,
    // por ejemplo las cartas que se llaman como su cosmético).
    private static string Clave(Transform t)
    {
        var partes = new List<string>();
        for (; t.parent != null; t = t.parent) partes.Add(t.GetSiblingIndex().ToString());
        partes.Add(t.name);
        partes.Reverse();
        return string.Join("/", partes);
    }

    private static string Ruta(Transform t)
    {
        var partes = new List<string>();
        for (; t != null; t = t.parent) partes.Add(t.name);
        partes.Reverse();
        return string.Join("/", partes);
    }

    // ================= Evidencia =================

    public static void Guardar(string caso, string idioma, List<string> sinTraducir, List<NoEntra> noEntran, List<NoEntra> yaEnEspanol)
    {
        string carpeta = DetectorDeIdioma.CarpetaEvidencia();
        try
        {
            Directory.CreateDirectory(carpeta);
            var informe = new StringBuilder();
            informe.AppendLine($"{caso} · idioma {idioma} · {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            bool ok = sinTraducir.Count == 0 && noEntran.Count == 0;
            informe.AppendLine(idioma == "es" ? "Referencia en español" : ok ? "Resultado: todo traducido y todos los textos entran" :
                $"Resultado: {sinTraducir.Count} sin traducir · {noEntran.Count} que no entran");
            foreach (string s in sinTraducir) informe.AppendLine("  SIN TRADUCIR: " + s);
            foreach (NoEntra n in noEntran) informe.AppendLine($"  NO ENTRA ({n.motivo}): {n.texto}  [{n.objeto}]");
            if (yaEnEspanol != null && yaEnEspanol.Count > 0)
            {
                informe.AppendLine("Ya pasaba en español (no cuenta para este idioma):");
                foreach (NoEntra n in yaEnEspanol) informe.AppendLine($"  ({n.motivo}) {n.texto}  [{n.objeto}]");
            }
            informe.AppendLine("Textos en pantalla:");
            foreach (string t in DetectorDeIdioma.TextosVisibles().Select(DetectorDeIdioma.Visible).Distinct()) informe.AppendLine("  " + t);
            File.WriteAllText(Path.Combine(carpeta, caso + ".txt"), informe.ToString(), new UTF8Encoding(false));
            Texture2D captura = ScreenCapture.CaptureScreenshotAsTexture();
            if (captura != null)
            {
                File.WriteAllBytes(Path.Combine(carpeta, caso + ".png"), captura.EncodeToPNG());
                UnityEngine.Object.Destroy(captura);
            }
            if (idioma != "es")
                File.AppendAllText(Path.Combine(carpeta, "US-350_resumen.txt"),
                    $"{caso,-48} {(ok ? "OK   " : "FALLÓ")}  sin traducir {sinTraducir.Count} · no entran {noEntran.Count}\n", new UTF8Encoding(false));
        }
        catch (Exception e) { Debug.LogWarning("No se pudo guardar la evidencia de " + caso + ": " + e.Message); }
    }

    // ================= Catálogos =================

    private static void Cargar(string idioma)
    {
        if (porClave.ContainsKey(idioma)) return;
        var textos = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string dominio in new[] { "ui", "gameplay", "profile", "launcher" })
            foreach (var e in DetectorDeIdioma.Leer(idioma, dominio)) textos[e.key] = e.text;
        porClave[idioma] = textos;
        textosDe[idioma] = new HashSet<string>(textos.Values.Select(DetectorDeIdioma.Visible), StringComparer.Ordinal);
        var palabras = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string t in textosDe[idioma]) foreach (Match m in Palabra.Matches(t)) palabras.Add(m.Value);
        palabrasDe[idioma] = palabras;
    }
}
