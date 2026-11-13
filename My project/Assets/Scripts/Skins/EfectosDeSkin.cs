using UnityEngine;

// F35 (Skins de armas), US 381: el fogonazo y la estela de las skins legendarias (CatalogoDeSkins.Efectos), en lugar de
// los de siempre (Trazadora). No cambian la bala: se ven más (como los de la bronca) y de otro color y forma.
// - Fogonazo: la luz del color del fuego y, si tiene alas, dos alas de fuego que se abren y se cierran en un instante,
//   mirando a la cámara (Art/Skins/EfectoAlas).
// - Estela: la de siempre, del color del fuego, y si tiene brasas, unas brasas que se desprenden y caen (EfectoBrasa).
// Los usan los disparos propios (WeaponFire) y los de los demás jugadores (JugadorEnRed): CA4, lo ven todos.
public static class EfectosDeSkin
{
    private const int BrasasPorMetro = 4, MaxBrasas = 30;

    private static ParticleSystem brasas;

    /// <summary>Los efectos de esa skin, o null si no es legendaria (o no hay skin).</summary>
    public static CatalogoDeSkins.Efectos De(CatalogoDeSkins.Skin skin) => skin?.diseno?.efectos;

    /// <summary>El fogonazo de la legendaria en la boca del arma, mirando hacia "adelante".</summary>
    public static void Fogonazo(Vector3 boca, Vector3 adelante, CatalogoDeSkins.Efectos e)
    {
        Trazadora.Fogonazo(boca, false, e.fuego);
        if (!e.alas) return;
        Material material = ModelosDeArmas.Actual.MaterialDe("efecto-alas");
        if (material == null) return;
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Fogonazo (alas)";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.position = boca;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        go.AddComponent<AlasDeFuego>().Iniciar(adelante, e);
    }

    /// <summary>La estela de la legendaria, de la boca del arma hasta donde pegó.</summary>
    public static void Estela(Vector3 desde, Vector3 hasta, CatalogoDeSkins.Efectos e)
    {
        Trazadora.Mostrar(desde, hasta, false, e.fuego);
        if (e.brasas) Brasas(desde, hasta, e);
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
