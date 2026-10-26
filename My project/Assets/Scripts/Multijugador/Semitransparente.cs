using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// US 137 (CA3): mientras un jugador recién reaparecido es invulnerable, los demás lo ven un poco transparente y
// amarillo (y con el contorno amarillo en vez del rojo, ContornoRival), así se nota que todavía no se le puede hacer daño.
// Cambia los materiales del modelo por copias transparentes y teñidas, y después devuelve los originales.
public class Semitransparente : MonoBehaviour
{
    private const float Opacidad = 0.6f;
    public static readonly Color Amarillo = new Color(1f, 0.85f, 0.15f);
    private const float Tinte = 0.55f;   // cuánto del color propio pasa a amarillo
    private const float Brillo = 0.45f;  // el amarillo que brilla solo (se ve también a la sombra)

    private Transform raiz;
    private bool activo;
    private readonly List<Renderer> partes = new List<Renderer>();
    private readonly List<Material[]> originales = new List<Material[]>();
    private readonly Dictionary<Material, Material> copias = new Dictionary<Material, Material>();

    public static Semitransparente Crear(GameObject personaje, Transform modelo)
    {
        if (personaje == null || modelo == null) return null;
        Semitransparente s = personaje.AddComponent<Semitransparente>();
        s.raiz = modelo;
        return s;
    }

    public void Poner(bool valor)
    {
        if (valor == activo) return;
        activo = valor;
        if (valor) Aplicar(); else Restaurar();
    }

    /// <summary>Cambió el cuerpo (CuerpoDePersonaje): si estaba transparente, lo vuelve a poner sobre las mallas nuevas.</summary>
    public void Rehacer()
    {
        if (!activo) return;
        Restaurar();
        Aplicar();
    }

    private void Aplicar()
    {
        partes.Clear();
        originales.Clear();
        if (raiz == null) return;
        foreach (Renderer r in raiz.GetComponentsInChildren<Renderer>(true))
        {
            // El contorno rojo de los rivales y los efectos quedan como están.
            if (r.name == "Contorno" || r.name == "ContornoMascara") continue;
            if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;
            Material[] propios = r.sharedMaterials;
            var nuevos = new Material[propios.Length];
            for (int i = 0; i < propios.Length; i++) nuevos[i] = Copia(propios[i]);
            partes.Add(r);
            originales.Add(propios);
            r.sharedMaterials = nuevos;
        }
    }

    private void Restaurar()
    {
        for (int i = 0; i < partes.Count; i++)
            if (partes[i] != null) partes[i].sharedMaterials = originales[i];
        partes.Clear();
        originales.Clear();
    }

    private Material Copia(Material original)
    {
        if (original == null) return null;
        if (copias.TryGetValue(original, out Material hecha) && hecha != null) return hecha;

        var m = new Material(original) { name = original.name + " (semitransparente)" };
        // Materiales de URP (Lit, Simple Lit, Unlit): se pasan a superficie transparente.
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHATEST_ON");
        m.renderQueue = (int)RenderQueue.Transparent;

        string color = m.HasProperty("_BaseColor") ? "_BaseColor" : m.HasProperty("_Color") ? "_Color" : null;
        if (color != null)
        {
            Color c = m.GetColor(color);
            float a = c.a * Opacidad;
            c = Color.Lerp(c, Amarillo, Tinte);
            c.a = a;
            m.SetColor(color, c);
        }
        if (m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", null); // parejo en todo el cuerpo
            m.SetColor("_EmissionColor", Amarillo * Brillo);
        }
        copias[original] = m;
        return m;
    }

    private void OnDestroy()
    {
        foreach (Material m in copias.Values) if (m != null) Destroy(m);
    }
}
