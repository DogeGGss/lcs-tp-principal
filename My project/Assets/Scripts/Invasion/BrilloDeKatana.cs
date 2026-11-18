using UnityEngine;

// US 395, CA9: lo que se ve de la energía de la katana en primera persona. Va en su modelo (lo pone KatanaDeInvasion).
// - El filo brilla más en el tajo fuerte y en la estocada, y destella al frenar un disparo (Destello).
// - Cada corte deja una estela del color de la energía: una cinta que va de cerca de la mano a la punta de la hoja.
[DefaultExecutionOrder(400)] // después de la muñeca (MovimientoDeKatana) y del brazo (AnimacionPrimeraPersona)
public class BrilloDeKatana : MonoBehaviour
{
    private const float BrilloNormal = 1f, BrilloFuerte = 2.6f, BrilloDestello = 4f;

    private MeleeAttack ataque;
    private KatanaData katana;
    private Material[] materiales;
    private Color[] emisionBase;
    private float brillo = BrilloNormal, destello;
    private FiloDeEnergia filo;

    public void Iniciar(MeleeAttack ataque, KatanaData katana)
    {
        this.ataque = ataque;
        this.katana = katana;
        Renderer[] partes = GetComponentsInChildren<Renderer>(true);
        materiales = new Material[0];
        var lista = new System.Collections.Generic.List<Material>();
        foreach (Renderer r in partes)
        {
            foreach (Material m in r.materials) // copias propias: el brillo no cambia el del archivo
                if (m != null && m.HasProperty("_EmissionColor")) lista.Add(m);
        }
        materiales = lista.ToArray();
        emisionBase = new Color[materiales.Length];
        for (int i = 0; i < materiales.Length; i++) emisionBase[i] = materiales[i].GetColor("_EmissionColor");
        CrearEstela();
    }

    /// <summary>Un destello del filo (frenó un disparo).</summary>
    public void Destello() => destello = 1f;

    // ---------- La estela: una cinta entre la base y la punta de la hoja, en el espacio de la cámara ----------
    // (con un TrailRenderer se veían manchas: la cámara se mueve con el arma y la estela quedaba atrás, en el mundo).

    private const int Muestras = 24;
    private const float VidaDeLaEstela = 0.13f;
    private Transform modelo, punta, padre;
    private MeshFilter estela;
    private Mesh malla;
    private readonly Vector3[] bases = new Vector3[Muestras], puntas = new Vector3[Muestras];
    private readonly float[] cuando = new float[Muestras];
    private int cuantas;
    private Vector3[] vertices = new Vector3[Muestras * 2];
    private Color[] colores = new Color[Muestras * 2];
    private Vector2[] uvs = new Vector2[Muestras * 2];
    private int[] triangulos = new int[(Muestras - 1) * 6];
    private Color colorEstela;

    private void CrearEstela()
    {
        modelo = transform.Find("Modelo");
        punta = Buscar(transform, "Punta");
        padre = transform.parent;
        if (modelo == null || punta == null || padre == null) return;
        var go = new GameObject("Estela de la katana");
        go.layer = modelo.gameObject.layer;
        go.transform.SetParent(padre, false);
        estela = go.AddComponent<MeshFilter>();
        var r = go.AddComponent<MeshRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        Color c = katana != null ? katana.energia : new Color(0.45f, 0.85f, 1.6f);
        colorEstela = new Color(Mathf.Min(1f, c.r), Mathf.Min(1f, c.g), Mathf.Min(1f, c.b), 1f);
        r.sharedMaterial = Rayo.Material(colorEstela * 0.6f);
        malla = new Mesh { name = "Estela de la katana" };
        malla.MarkDynamic();
        estela.sharedMesh = malla;
    }

    // Suma la posición de la hoja de este cuadro (si corta), saca las viejas y arma la cinta.
    private void Estela(bool emitir)
    {
        if (estela == null) return;
        float ahora = Time.time;
        int quedan = 0;
        for (int i = 0; i < cuantas; i++)
            if (ahora - cuando[i] < VidaDeLaEstela) { bases[quedan] = bases[i]; puntas[quedan] = puntas[i]; cuando[quedan] = cuando[i]; quedan++; }
        cuantas = quedan;
        if (emitir)
        {
            if (cuantas == Muestras) { System.Array.Copy(bases, 1, bases, 0, Muestras - 1); System.Array.Copy(puntas, 1, puntas, 0, Muestras - 1); System.Array.Copy(cuando, 1, cuando, 0, Muestras - 1); cuantas--; }
            Vector3 p = padre.InverseTransformPoint(punta.position);
            Vector3 b = padre.InverseTransformPoint(modelo.TransformPoint(new Vector3(punta.localPosition.x, 0f, 0.1f)));
            bases[cuantas] = b; puntas[cuantas] = p; cuando[cuantas] = ahora; cuantas++;
        }
        malla.Clear();
        estela.gameObject.SetActive(cuantas >= 2);
        if (cuantas < 2) return;
        for (int i = 0; i < cuantas; i++)
        {
            float a = Mathf.Clamp01(1f - (ahora - cuando[i]) / VidaDeLaEstela);
            a *= a * 0.9f;
            vertices[i * 2] = bases[i]; vertices[i * 2 + 1] = puntas[i];
            colores[i * 2] = new Color(colorEstela.r, colorEstela.g, colorEstela.b, a * 0.25f); // cerca de la mano, apenas
            colores[i * 2 + 1] = new Color(colorEstela.r, colorEstela.g, colorEstela.b, a);
            float x = i / (float)(cuantas - 1);
            uvs[i * 2] = new Vector2(x, 0.1f); uvs[i * 2 + 1] = new Vector2(x, 0.65f); // más fuerte hacia la punta
        }
        int t = 0;
        for (int i = 0; i < cuantas - 1; i++)
        {
            int v = i * 2;
            triangulos[t++] = v; triangulos[t++] = v + 1; triangulos[t++] = v + 2;
            triangulos[t++] = v + 1; triangulos[t++] = v + 3; triangulos[t++] = v + 2;
        }
        malla.SetVertices(vertices, 0, cuantas * 2);
        malla.SetColors(colores, 0, cuantas * 2);
        malla.SetUVs(0, uvs, 0, cuantas * 2);
        malla.SetTriangles(triangulos, 0, (cuantas - 1) * 6, 0, false);
        malla.RecalculateBounds();
    }

    private void LateUpdate()
    {
        if (ataque == null) return;
        bool estocada = ataque.AvanceDeLaEstocada >= 0f;
        bool cortando = ataque.AvanceDelCorte >= 0f;
        // La estela solo mientras la hoja cruza (no al prepararse ni al volver), y en toda la estocada.
        float t = ataque.AvanceDelCorte * MeleeAttack.Total;
        bool cruza = cortando && t >= MeleeAttack.Preparacion * 0.6f && t <= MeleeAttack.Preparacion + MeleeAttack.Corte * 1.4f;
        Estela(cruza || estocada);

        float objetivo = ataque.TajoFuerte || estocada ? BrilloFuerte : BrilloNormal;
        brillo = Mathf.MoveTowards(brillo, objetivo, Time.deltaTime * 12f);
        destello = Mathf.MoveTowards(destello, 0f, Time.deltaTime * 4f);
        float total = brillo + destello * BrilloDestello;
        for (int i = 0; i < materiales.Length; i++)
            if (materiales[i] != null) materiales[i].SetColor("_EmissionColor", emisionBase[i] * total);
        if (filo == null) filo = GetComponent<FiloDeEnergia>();
        if (filo != null) filo.Intensidad = total; // el filo de energía también
    }

    private void OnDisable()
    {
        cuantas = 0;
        if (estela != null) estela.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (estela != null) Destroy(estela.gameObject);
        if (malla != null) Destroy(malla);
    }

    private static Transform Buscar(Transform t, string nombre)
    {
        if (t.name == nombre) return t;
        foreach (Transform h in t)
        {
            Transform r = Buscar(h, nombre);
            if (r != null) return r;
        }
        return null;
    }
}
