using UnityEngine;

// US 395, CA9: lo que se ve de la energía de la katana en primera persona. Va en su modelo (lo pone KatanaDeInvasion).
// - El filo brilla más en el tajo fuerte y en la estocada, y destella al frenar un disparo (Destello).
// - Cada corte deja una estela del color de la energía, que sale del medio de la hoja ("Filo" en el modelo).
public class BrilloDeKatana : MonoBehaviour
{
    private const float BrilloNormal = 1f, BrilloFuerte = 2.6f, BrilloDestello = 4f;

    private MeleeAttack ataque;
    private KatanaData katana;
    private Material[] materiales;
    private Color[] emisionBase;
    private TrailRenderer estela;
    private float brillo = BrilloNormal, destello;

    public void Iniciar(MeleeAttack ataque, KatanaData katana)
    {
        this.ataque = ataque;
        this.katana = katana;
        Renderer[] partes = GetComponentsInChildren<Renderer>(true);
        materiales = new Material[0];
        var lista = new System.Collections.Generic.List<Material>();
        foreach (Renderer r in partes)
        {
            if (r is TrailRenderer) continue;
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

    private void CrearEstela()
    {
        Transform filo = Buscar(transform, "Filo");
        var go = new GameObject("Estela");
        go.layer = filo != null ? filo.gameObject.layer : gameObject.layer;
        go.transform.SetParent(filo != null ? filo : transform, false);
        estela = go.AddComponent<TrailRenderer>();
        estela.time = 0.12f;
        estela.minVertexDistance = 0.02f;
        estela.widthMultiplier = 0.42f;
        estela.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f));
        Color c = katana != null ? katana.energia : new Color(0.45f, 0.85f, 1f);
        var colores = new Gradient();
        colores.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                        new[] { new GradientAlphaKey(0.55f, 0f), new GradientAlphaKey(0f, 1f) });
        estela.colorGradient = colores;
        estela.material = Destellos.Material(c);
        estela.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        estela.receiveShadows = false;
        estela.emitting = false;
    }

    private void LateUpdate()
    {
        if (ataque == null) return;
        bool estocada = ataque.AvanceDeLaEstocada >= 0f;
        bool cortando = ataque.AvanceDelCorte >= 0f;
        // La estela solo mientras la hoja cruza (no al prepararse ni al volver), y en toda la estocada.
        float t = ataque.AvanceDelCorte * MeleeAttack.Total;
        bool cruza = cortando && t >= MeleeAttack.Preparacion * 0.6f && t <= MeleeAttack.Preparacion + MeleeAttack.Corte * 1.4f;
        if (estela != null)
        {
            bool emitir = cruza || estocada;
            if (emitir && !estela.emitting) estela.Clear();
            estela.emitting = emitir;
        }

        float objetivo = ataque.TajoFuerte || estocada ? BrilloFuerte : BrilloNormal;
        brillo = Mathf.MoveTowards(brillo, objetivo, Time.deltaTime * 12f);
        destello = Mathf.MoveTowards(destello, 0f, Time.deltaTime * 4f);
        float total = brillo + destello * BrilloDestello;
        for (int i = 0; i < materiales.Length; i++)
            if (materiales[i] != null) materiales[i].SetColor("_EmissionColor", emisionBase[i] * total);
    }

    private void OnDisable()
    {
        if (estela != null) { estela.emitting = false; estela.Clear(); }
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
