using UnityEngine;

// F35 (Skins de armas), US 381: el fogonazo y la estela de las skins legendarias (CatalogoDeSkins.Efectos), en lugar de
// los de siempre (Trazadora). No cambian la bala: se ven más (como los de la bronca) y de otro color y forma.
// - Fogonazo: la luz del color del fuego y una llamarada que sale de la boca (LlamaradaDeSkin).
// - Estela: la de siempre, del color del fuego, y si tiene brasas, unas brasas que se desprenden y caen (EfectoBrasa).
// Los usan los disparos propios (WeaponFire) y los de los demás jugadores (JugadorEnRed): CA4, lo ven todos.
public static class EfectosDeSkin
{
    private const int BrasasPorMetro = 4, MaxBrasas = 30;

    private static ParticleSystem brasas;

    /// <summary>Los efectos de esa skin, o null si no es legendaria (o no hay skin).</summary>
    public static CatalogoDeSkins.Efectos De(CatalogoDeSkins.Skin skin) => skin?.diseno?.efectos;

    private const float DistanciaBoca = 1f;                       // m: donde se dibuja la boca del arma propia
    private const float LlamaPropia = 0.13f, LlamaDeOtro = 0.45f; // m: el largo de la llamarada

    /// <summary>Disparo propio (primera persona): el fogonazo y las estelas salen de la punta del arma tal como se ve en
    /// la pantalla. El arma la dibuja otra cámara (con otro campo de visión), así que la boca "de verdad" no coincide con
    /// lo que se ve; se toma el punto de la pantalla donde está la punta y se lo pone a un metro de la cámara.</summary>
    public static void DisparoPropio(GameObject arma, Vector3 muzzle, Vector3[] finales, CatalogoDeSkins.Efectos e)
    {
        Vector3 boca = muzzle;
        Vector3 haciaDonde = Vector3.zero;
        Camera cam = Camera.main;
        if (cam != null && PuntaEnPantalla(arma, cam, out Vector3 pantalla))
        {
            boca = cam.ScreenToWorldPoint(new Vector3(pantalla.x, pantalla.y, DistanciaBoca));
            // La llama sale hacia el centro de la pantalla (hacia donde apunta el caño en la imagen), corta: no tapa la mira.
            Vector2 alCentro = new Vector2(Screen.width * 0.5f - pantalla.x, Screen.height * 0.5f - pantalla.y);
            haciaDonde = cam.transform.right * alCentro.x + cam.transform.up * alCentro.y;
        }
        Trazadora.Fogonazo(boca, false, e.fuego);
        if (e.llamarada) LlamaradaDeSkin.Crear(boca, haciaDonde, LlamaPropia, e);
        foreach (Vector3 fin in finales) Estela(boca, fin, e);
    }

    /// <summary>Disparo de otro jugador (su arma en la mano, US 380): desde la boca de ese modelo.</summary>
    public static void DisparoDeOtro(Vector3 boca, Vector3 adelante, CatalogoDeSkins.Efectos e)
    {
        Trazadora.Fogonazo(boca, false, e.fuego);
        if (e.llamarada) LlamaradaDeSkin.Crear(boca, adelante, LlamaDeOtro, e);
    }

    /// <summary>La estela de la legendaria, de la boca del arma hasta donde pegó.</summary>
    public static void Estela(Vector3 desde, Vector3 hasta, CatalogoDeSkins.Efectos e)
    {
        Trazadora.Mostrar(desde, hasta, false, e.fuego);
        if (e.brasas) Brasas(desde, hasta, e);
    }

    // Dónde se ve en la pantalla la punta del arma de primera persona: la del modelo (sus límites) más lejos hacia
    // adelante, vista por la cámara que la dibuja (la de las armas, hija de la principal).
    private static bool PuntaEnPantalla(GameObject arma, Camera cam, out Vector3 pantalla)
    {
        pantalla = Vector3.zero;
        if (arma == null) return false;
        Renderer[] rs = arma.GetComponentsInChildren<Renderer>();
        Bounds b = default;
        bool hay = false;
        int capa = arma.layer;
        foreach (Renderer r in rs)
        {
            if (!r.enabled || r is LineRenderer || r is TrailRenderer || r is ParticleSystemRenderer) continue;
            if (!hay) { b = r.bounds; capa = r.gameObject.layer; hay = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!hay) return false;
        Camera camArma = cam;
        foreach (Camera c in cam.GetComponentsInChildren<Camera>(true))
            if (c != cam && c.enabled && (c.cullingMask & (1 << capa)) != 0) { camArma = c; break; }
        Vector3 f = camArma.transform.forward;
        float hasta = Mathf.Abs(f.x) * b.extents.x + Mathf.Abs(f.y) * b.extents.y + Mathf.Abs(f.z) * b.extents.z;
        pantalla = camArma.WorldToScreenPoint(b.center + f * hasta);
        return pantalla.z > 0f;
    }

    // Unas brasas a lo largo de la estela que caen y se apagan (un solo sistema de partículas para todas).
    private static void Brasas(Vector3 desde, Vector3 hasta, CatalogoDeSkins.Efectos e)
    {
        ParticleSystem ps = Sistema();
        if (ps == null) return;
        float largo = Vector3.Distance(desde, hasta);
        int cuantas = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(largo, 30f) * BrasasPorMetro), 2, MaxBrasas);
        var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        for (int i = 0; i < cuantas; i++)
        {
            float t = Random.Range(0.05f, 1f) * Mathf.Min(1f, 30f / Mathf.Max(0.01f, largo)); // las de cerca, que se ven
            p.position = Vector3.Lerp(desde, hasta, t) + Random.insideUnitSphere * 0.04f;
            p.velocity = Random.insideUnitSphere * 0.4f + Vector3.down * Random.Range(0.3f, 0.9f);
            p.startSize = Random.Range(0.04f, 0.08f);
            p.startLifetime = Random.Range(0.5f, 1f);
            p.startColor = Color.Lerp(e.fuego, e.nucleo, Random.value);
            ps.Emit(p, 1);
        }
    }

    private static ParticleSystem Sistema()
    {
        if (brasas != null) return brasas;
        Material material = ModelosDeArmas.Actual.MaterialDe("efecto-brasa");
        if (material == null) return null;
        var go = new GameObject("Brasas de las skins");
        brasas = go.AddComponent<ParticleSystem>();
        brasas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = brasas.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.25f;
        main.maxParticles = 600;
        ParticleSystem.EmissionModule emision = brasas.emission;
        emision.enabled = false;
        ParticleSystem.ShapeModule forma = brasas.shape;
        forma.enabled = false;
        ParticleSystem.ColorOverLifetimeModule apagar = brasas.colorOverLifetime; // se apagan al caer
        apagar.enabled = true;
        var degrade = new Gradient();
        degrade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                        new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        apagar.color = degrade;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        brasas.Play();
        return brasas;
    }
}
