using System.Collections.Generic;
using UnityEngine;

// F35 (Skins de armas): le pone una skin a un arma (el modelo en la mano, en primera o en tercera persona) y la vuelve
// atrás al sacarla. Cambia los materiales del modelo, por su nombre (las armas de fuego traen Main, Grey, Black y White):
// - una skin del catálogo (CatalogoDeSkins): las comunes, cada material con su color (US 378);
// - un material (la prueba de código binario, Art/Skins): en las partes elegidas, o en toda el arma.
// No toca las estelas, las líneas ni las partículas. Las piezas que el arma arma después (el cargador del Mitre que se
// separa al recargar) copian el material de la malla, así que también salen con la skin.
public class SkinDeArma : MonoBehaviour
{
    // Un material por cada original y color: así no se crea uno nuevo cada vez que se pone una skin.
    private static readonly Dictionary<(Material, Color), Material> pintados = new Dictionary<(Material, Color), Material>();
    // De cada pintado, su original: una copia de un arma con skin (el arma en el piso, la de la mano de los demás) sale
    // con los materiales pintados, y otra skin (o ninguna) tiene que partir de los originales (US 380).
    private static readonly Dictionary<Material, Material> originalDe = new Dictionary<Material, Material>();

    private readonly Dictionary<Renderer, Material[]> originales = new Dictionary<Renderer, Material[]>();

    /// <summary>El material puesto (la prueba), o null.</summary>
    public Material Skin { get; private set; }

    /// <summary>La skin del catálogo puesta, o null.</summary>
    public CatalogoDeSkins.Skin Puesta { get; private set; }

    /// <summary>Le pone la skin al arma, o se la saca si ya la tenía.</summary>
    public static void Alternar(GameObject arma, Material skin, params string[] partes)
    {
        if (arma == null || skin == null) return;
        SkinDeArma s = De(arma);
        if (s.Skin == skin) s.Sacar();
        else s.Poner(skin, partes);
    }

    /// <summary>Le pone esa skin del catálogo (null: la original).</summary>
    public static void Poner(GameObject arma, CatalogoDeSkins.Skin skin)
    {
        if (arma == null) return;
        De(arma).Poner(skin);
    }

    private static SkinDeArma De(GameObject arma)
    {
        SkinDeArma s = arma.GetComponent<SkinDeArma>();
        return s != null ? s : arma.AddComponent<SkinDeArma>();
    }

    // Si el material es de una de las partes elegidas (o no se eligió ninguna).
    private static bool EsParte(Material m, string[] partes)
    {
        if (partes == null || partes.Length == 0) return true;
        if (m == null) return false;
        foreach (string p in partes)
            if (m.name == p || m.name.StartsWith(p + " ")) return true;
        return false;
    }

    public void Poner(Material skin, params string[] partes)
    {
        Cambiar(m => EsParte(m, partes) ? skin : m);
        Skin = skin;
    }

    public void Poner(CatalogoDeSkins.Skin skin)
    {
        if (skin == Puesta && (skin != null || pintada)) return;
        pintada = true;
        Material diseno = skin?.diseno != null ? ModelosDeArmas.Actual.MaterialDe(skin.diseno.id) : null;
        Cambiar(m =>
        {
            if (diseno != null)
            {
                if (m == null) return m;
                // La mira que se le agrega al Último Tren (PiezasDelTren): en las legendarias, como las franjas.
                string parte = m.name == PiezasDelTren.NombreMira && skin.rareza == PerfilDatos.Rareza.Legendario
                    ? CatalogoDeSkins.Main : m.name;
                if (CatalogoDeSkins.EsParteDelDiseno(skin, parte)) return ConDiseno(m, diseno);
                // Otras partes del diseño: con otro material (el cuerpo de madera del cobre) o un color (las franjas).
                CatalogoDeSkins.Extra extra = CatalogoDeSkins.ExtraPara(skin, parte);
                if (extra == null) return m;
                Material otro = !string.IsNullOrEmpty(extra.material) ? ModelosDeArmas.Actual.MaterialDe(extra.material) : null;
                return otro != null ? ConDiseno(m, otro) : string.IsNullOrEmpty(extra.material) ? Pintado(m, extra.color) : m;
            }
            Color? color = m != null ? CatalogoDeSkins.ColorPara(skin, m.name) : null;
            return color.HasValue ? Pintado(m, color.Value) : m;
        });
        Puesta = skin;
    }

    private bool pintada; // ya se le puso una skin del catálogo (o la original) alguna vez

    /// <summary>Vuelve a poner la skin puesta (se le agregaron piezas al arma, como la mira del Último Tren).</summary>
    public void Reaplicar()
    {
        CatalogoDeSkins.Skin s = Puesta;
        if (s == null) return;
        Puesta = null;
        Poner(s);
    }

    /// <summary>El material original del que salió una copia pintada (o el mismo, si no es una copia).</summary>
    public static Material OriginalDe(Material m) => Original(m);

    // Cambia cada material (de los originales: si ya venía pintado, el suyo) por el que devuelve cambio.
    private void Cambiar(System.Func<Material, Material> cambio)
    {
        Sacar();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is LineRenderer || r is TrailRenderer || r is ParticleSystemRenderer) continue;
            Material[] actuales = r.sharedMaterials;
            originales[r] = actuales;
            var nuevos = new Material[actuales.Length];
            for (int i = 0; i < nuevos.Length; i++) nuevos[i] = cambio(Original(actuales[i]));
            r.sharedMaterials = nuevos;
        }
    }

    // Una copia del material del diseño por cada original (y no el mismo para todas): así se sabe de qué original salió.
    private static readonly Dictionary<(Material, Material), Material> conDiseno = new Dictionary<(Material, Material), Material>();

    private static Material ConDiseno(Material original, Material diseno)
    {
        var clave = (original, diseno);
        if (conDiseno.TryGetValue(clave, out Material m) && m != null) return m;
        m = new Material(diseno) { name = original.name + " (skin)" };
        conDiseno[clave] = m;
        originalDe[m] = original;
        return m;
    }

    private static Material Original(Material m) => m != null && originalDe.TryGetValue(m, out Material o) && o != null ? o : m;

    // El mismo material con otro color (URP: _BaseColor; si no, _Color). Con textura, el color la tiñe.
    private static Material Pintado(Material original, Color color)
    {
        var clave = (original, color);
        if (pintados.TryGetValue(clave, out Material m) && m != null) return m;
        m = new Material(original) { name = original.name + " (skin)" };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        pintados[clave] = m;
        originalDe[m] = original;
        return m;
    }

    public void Sacar()
    {
        foreach (KeyValuePair<Renderer, Material[]> o in originales)
            if (o.Key != null) o.Key.sharedMaterials = o.Value;
        originales.Clear();
        Skin = null;
        Puesta = null;
    }
}
