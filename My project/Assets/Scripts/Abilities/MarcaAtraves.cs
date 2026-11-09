using System.Collections.Generic;
using UnityEngine;

// US 022, CA8: el rival mordido por el perro del Callejero queda marcado 3 s: su silueta se ve a través de las paredes
// para el dueño del perro y sus compañeros. Se dibujan copias de sus mallas (que siguen sus huesos) con el shader
// Efectos/MarcaAtraves, que no respeta la profundidad. Al final se apaga de a poco.
// Con soloDetras (los portales de enemigos de la Invasión, US 269) se usa Efectos/MarcaDetras: se dibuja solo donde
// lo tapa una pared, no donde se ve directo.
public class MarcaAtraves : MonoBehaviour
{
    private const float Apagado = 0.4f;
    private static Material material, materialDetras;
    private static readonly Dictionary<Transform, MarcaAtraves> activas = new Dictionary<Transform, MarcaAtraves>();

    private Transform personaje;
    private readonly List<Renderer> copias = new List<Renderer>();
    private Material propio;
    private float hasta;

    /// <summary>Marca a ese personaje esos segundos (si ya estaba marcado, alarga la marca).</summary>
    public static void Marcar(Transform personaje, float segundos, Color color, bool soloDetras = false)
    {
        if (personaje == null) return;
        if (!activas.TryGetValue(personaje, out MarcaAtraves marca) || marca == null)
        {
            Material base_ = soloDetras ? MaterialDetras() : MaterialMarca();
            if (base_ == null) return;
            marca = personaje.gameObject.AddComponent<MarcaAtraves>();
            marca.Armar(personaje, base_);
            activas[personaje] = marca;
        }
        marca.propio.SetColor("_BaseColor", color);
        marca.hasta = Mathf.Max(marca.hasta, Time.time + segundos);
    }

    private void Armar(Transform personaje, Material base_)
    {
        this.personaje = personaje;
        propio = new Material(base_);
        foreach (Renderer r in personaje.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
            string nombre = r.gameObject.name;
            if (nombre.StartsWith("Contorno") || nombre.StartsWith("Brazos") || nombre.StartsWith("Marca")) continue;
            Renderer copia = Copiar(r);
            if (copia != null) copias.Add(copia);
        }
    }

    private Renderer Copiar(Renderer original)
    {
        var go = new GameObject("Marca (a través)");
        go.layer = original.gameObject.layer;
        go.transform.SetParent(original.transform, false);
        var mats = new Material[Mathf.Max(1, original.sharedMaterials.Length)];
        for (int i = 0; i < mats.Length; i++) mats[i] = propio;
        if (original is SkinnedMeshRenderer piel)
        {
            if (piel.sharedMesh == null) { Destroy(go); return null; }
            var s = go.AddComponent<SkinnedMeshRenderer>();
            s.sharedMesh = piel.sharedMesh;
            s.bones = piel.bones;
            s.rootBone = piel.rootBone;
            s.localBounds = piel.localBounds;
            s.updateWhenOffscreen = true;
            s.sharedMaterials = mats;
            s.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Los huesos están en el padre de la malla original: la copia va al lado, no adentro.
            go.transform.SetParent(original.transform.parent, false);
            go.transform.localPosition = original.transform.localPosition;
            go.transform.localRotation = original.transform.localRotation;
            go.transform.localScale = original.transform.localScale;
            return s;
        }
        MeshFilter filtro = original.GetComponent<MeshFilter>();
        if (filtro == null || filtro.sharedMesh == null) { Destroy(go); return null; }
        go.AddComponent<MeshFilter>().sharedMesh = filtro.sharedMesh;
        MeshRenderer m = go.AddComponent<MeshRenderer>();
        m.sharedMaterials = mats;
        m.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return m;
    }

    private void Update()
    {
        float queda = hasta - Time.time;
        propio.SetFloat("_Alfa", Mathf.Clamp01(queda / Apagado));
        if (queda <= 0f) Destroy(this);
    }

    private void OnDestroy()
    {
        foreach (Renderer r in copias) if (r != null) Destroy(r.gameObject);
        if (propio != null) Destroy(propio);
        if (personaje != null && activas.TryGetValue(personaje, out MarcaAtraves m) && m == this) activas.Remove(personaje);
    }

    /// <summary>Cola de dibujo de lo que va después de la marca "solo detrás" (sigue siendo opaco: hasta 2500).</summary>
    public const int ColaDespuesDeLaMarca = 2460;

    /// <summary>
    /// US 269, CA4: lo opaco de ese objeto se dibuja después de la marca "solo detrás" (Efectos/MarcaDetras), así no la
    /// tapa como si fuera una pared: los enemigos que pasan por delante de un portal y el mismo portal. Usa copias de
    /// sus materiales (las del objeto, no los del proyecto).
    /// </summary>
    public static void DespuesDeLaMarca(GameObject objeto)
    {
        if (objeto == null) return;
        foreach (Renderer r in objeto.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            Material[] mats = r.materials; // copias
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null && mats[i].renderQueue <= 2450) mats[i].renderQueue = ColaDespuesDeLaMarca;
            r.materials = mats;
        }
    }

    private static Material MaterialDetras()
    {
        if (materialDetras == null)
        {
            Shader shader = Resources.Load<Shader>("Efectos/MarcaDetras");
            if (shader == null) shader = Shader.Find("Efectos/MarcaDetras");
            if (shader != null) materialDetras = new Material(shader) { name = "Marca detrás" };
        }
        return materialDetras;
    }

    private static Material MaterialMarca()
    {
        if (material == null)
        {
            Shader shader = Resources.Load<Shader>("Efectos/MarcaAtraves");
            if (shader == null) shader = Shader.Find("Efectos/MarcaAtraves");
            if (shader != null) material = new Material(shader) { name = "Marca a través" };
        }
        return material;
    }
}
