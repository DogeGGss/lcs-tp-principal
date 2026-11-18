using System.Collections.Generic;
using UnityEngine;

// US 395, CA9: el filo de energía de la katana. Una cinta que brilla (Efectos/LineaBrillante, con el bloom) pegada al
// filo de la hoja, de la guarda a la punta, que late despacio. Se arma al crear el modelo, sacando el filo de la malla
// de la katana ("Modelo"): en cada tramo de la hoja, el punto más hacia el filo (+y). Es una malla común, así que
// también la copian el modelo en la mano de los compañeros (ModeloReal) y el espectador.
// Va en el modelo de primera persona de la katana (Resources/Katana/Katana_ViewModel). BrilloDeKatana la hace brillar
// más en el tajo fuerte y la estocada (Intensidad).
public class FiloDeEnergia : MonoBehaviour
{
    [Tooltip("Color de la energía.")]
    public Color color = new Color(0.3f, 0.78f, 1f);
    [Tooltip("Dónde arranca y termina la hoja, a lo largo de la katana (metros del modelo).")]
    public float desde = 0.035f, hasta = 0.61f;
    [Tooltip("Cuánto sobresale la cinta del filo hacia afuera y hacia adentro (metros).")]
    public float afuera = 0.022f, adentro = 0.006f;
    [Tooltip("Cuánto brilla (con el bloom: más de 1 resplandece).")]
    public float fuerza = 3f;
    [Tooltip("Cuánto late (0: fijo).")]
    [Range(0f, 1f)] public float latido = 0.25f;

    /// <summary>Cuánto brilla, además del latido (1 normal; más en el tajo fuerte).</summary>
    [System.NonSerialized] public float Intensidad = 1f;

    private Material material;
    private Color colorBase;

    private void Awake()
    {
        Transform modelo = transform.Find("Modelo");
        MeshFilter filtro = modelo != null ? modelo.GetComponent<MeshFilter>() : null;
        if (filtro == null || filtro.sharedMesh == null || !filtro.sharedMesh.isReadable) return;
        List<Vector3> filo = Filo(filtro.sharedMesh);
        if (filo.Count < 2) return;

        var go = new GameObject("Filo de energía");
        go.layer = modelo.gameObject.layer;
        go.transform.SetParent(modelo, false);
        go.AddComponent<MeshFilter>().sharedMesh = Cinta(filo);
        var r = go.AddComponent<MeshRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        material = Rayo.Material(color);
        colorBase = material.GetColor("_Color") * fuerza;
        r.sharedMaterial = material;
    }

    private void Update()
    {
        if (material == null) return;
        float l = 1f + latido * Mathf.Sin(Time.time * 3.2f);
        material.SetColor("_Color", colorBase * (l * Intensidad));
    }

    // El filo: en cada tramo de la hoja, el vértice que más sale hacia +y (el filo; el lomo es -y).
    private List<Vector3> Filo(Mesh malla)
    {
        Vector3[] v = malla.vertices;
        const float paso = 0.012f, medio = 0.007f;
        var puntos = new List<Vector3>();
        for (float z = desde; z <= hasta + 0.0001f; z += paso)
        {
            bool hay = false;
            Vector3 mejor = Vector3.zero;
            foreach (Vector3 p in v)
                if (Mathf.Abs(p.z - z) <= medio && (!hay || p.y > mejor.y)) { mejor = p; hay = true; }
            if (hay) puntos.Add(new Vector3(mejor.x, mejor.y, z));
        }
        // Suavizado (que no tiemble entre tramos).
        for (int vuelta = 0; vuelta < 2; vuelta++)
            for (int i = 1; i < puntos.Count - 1; i++)
                puntos[i] = (puntos[i - 1] + puntos[i] * 2f + puntos[i + 1]) * 0.25f;
        return puntos;
    }

    // Una cinta en el plano de la hoja, del filo hacia afuera y un poco hacia adentro; se afina en la punta.
    private Mesh Cinta(List<Vector3> filo)
    {
        int n = filo.Count;
        var vertices = new Vector3[n * 2];
        var uvs = new Vector2[n * 2];
        var colores = new Color[n * 2];
        var triangulos = new int[(n - 1) * 6];
        for (int i = 0; i < n; i++)
        {
            float k = i / (float)(n - 1);
            float ancho = Mathf.Lerp(1f, 0.35f, Mathf.InverseLerp(0.8f, 1f, k)); // más fina en la punta
            Vector3 normal = Vector3.up;
            if (i > 0 && i < n - 1)
            {
                Vector3 a = (filo[i + 1] - filo[i - 1]).normalized; // a lo largo
                normal = Vector3.ProjectOnPlane(Vector3.up, a).normalized;
            }
            vertices[i * 2] = filo[i] - normal * adentro * ancho;
            vertices[i * 2 + 1] = filo[i] + normal * afuera * ancho;
            uvs[i * 2] = new Vector2(k, 0f);
            uvs[i * 2 + 1] = new Vector2(k, 1f);
            float a0 = Mathf.Clamp01(Mathf.Min(k / 0.06f, 1f)); // se apaga junto a la guarda
            colores[i * 2] = colores[i * 2 + 1] = new Color(1f, 1f, 1f, a0);
        }
        for (int i = 0, t = 0; i < n - 1; i++, t += 6)
        {
            int v = i * 2;
            triangulos[t] = v; triangulos[t + 1] = v + 1; triangulos[t + 2] = v + 2;
            triangulos[t + 3] = v + 1; triangulos[t + 4] = v + 3; triangulos[t + 5] = v + 2;
        }
        var malla = new Mesh { name = "Filo de energía" };
        malla.vertices = vertices;
        malla.uv = uvs;
        malla.colors = colores;
        malla.triangles = triangulos;
        malla.RecalculateBounds();
        return malla;
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
