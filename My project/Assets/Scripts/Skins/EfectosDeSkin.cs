using UnityEngine;

// F35 (Skins de armas), US 381: el fogonazo y la estela de las skins legendarias (CatalogoDeSkins.Efectos), en lugar de
// los de siempre (Trazadora). No cambian la bala: se ven más (como los de la bronca) y de otro color y forma.
// - Fogonazo: la luz del color del fuego, tres lenguas de fuego que se abren en abanico y se enroscan (LlamaradaDeSkin),
//   chispas que saltan hacia adelante y, después de disparar, humo que sale de la boca (HumoDeSkin).
// - Estela: la de siempre, del color del fuego, y si tiene brasas, unas brasas que se desprenden y caen (EfectoBrasa).
// Las chispas y el humo del arma propia se mueven con la cámara (como el arma en la mano); los de los demás, en el mundo.
// Los usan los disparos propios (WeaponFire) y los de los demás jugadores (JugadorEnRed): CA4, lo ven todos.
public static class EfectosDeSkin
{
    private const int BrasasPorMetro = 4, MaxBrasas = 30;

    private static ParticleSystem brasas;

    /// <summary>Los efectos de esa skin, o null si no es legendaria (o no hay skin).</summary>
    public static CatalogoDeSkins.Efectos De(CatalogoDeSkins.Skin skin) => skin?.diseno?.efectos;

    private const float DistanciaBoca = 1f;                       // m: donde se dibuja la boca del arma propia
    private const float LlamaPropia = 0.13f, LlamaDeOtro = 0.45f; // m: el largo de la llamarada

    /// <summary>El objeto vacío del arma de donde salen el fogonazo y la estela de las legendarias: se mueve (y se gira,
    /// para la dirección de la llama) en el prefab del jugador. Lo crea Riftwalker > Armas > Crear bocas de skin.</summary>
    public const string NombreBoca = "BocaSkin";

    /// <summary>La boca del arma (el vacío BocaSkin), o null si esa arma no la tiene.</summary>
    public static Transform BocaDe(GameObject arma)
    {
        if (arma == null) return null;
        foreach (Transform t in arma.GetComponentsInChildren<Transform>(true))
            if (t.name == NombreBoca) return t;
        return null;
    }

    /// <summary>Dónde se ve la boca del arma propia (primera persona) y hacia dónde sale la llama. El arma la dibuja otra
    /// cámara (con otro campo de visión), así que la boca "de verdad" no coincide con lo que se ve; se toma el punto de la
    /// pantalla donde está la boca y se lo pone a un metro de la cámara. La boca es el vacío BocaSkin del arma; si no lo
    /// tiene, la punta del modelo más el ajuste de Skins de prueba.</summary>
    public static bool BocaPropia(GameObject arma, ShopItem ficha, out Vector3 boca, out Vector3 haciaDonde)
    {
        boca = Vector3.zero;
        haciaDonde = Vector3.zero;
        Camera cam = Camera.main;
        if (cam == null || arma == null) return false;
        Transform vacio = BocaDe(arma);
        Vector3 pantalla = Vector3.zero;
        Vector2 direccion = Vector2.zero; // hacia dónde sale la llama, en la pantalla
        bool hay;
        if (vacio != null)
        {
            Camera camArma = CamaraDelArma(cam, vacio.gameObject.layer, arma);
            pantalla = camArma.WorldToScreenPoint(vacio.position);
            Vector3 adelante = camArma.WorldToScreenPoint(vacio.position + vacio.forward * 0.1f);
            direccion = (Vector2)(adelante - pantalla);
            hay = pantalla.z > 0f;
        }
        else
        {
            hay = PuntaEnPantalla(arma, cam, out pantalla);
            if (hay)
            {
                // El ajuste a ojo de esa arma (Skins de prueba).
                CatalogoDeSkins.Arma a = ficha != null ? CatalogoDeSkins.ArmaDeFicha(ficha.name) : null;
                if (a != null) pantalla += (Vector3)(ModelosDeArmas.Actual.CorrimientoDeBoca(a.id) * Screen.height);
            }
        }
        if (!hay) return false;
        boca = cam.ScreenToWorldPoint(new Vector3(pantalla.x, pantalla.y, DistanciaBoca));
        // Sin vacío, o si apunta casi derecho hacia adentro de la pantalla, la llama va hacia el centro.
        if (direccion.sqrMagnitude < 1f) direccion = new Vector2(Screen.width * 0.5f - pantalla.x, Screen.height * 0.5f - pantalla.y);
        haciaDonde = cam.transform.right * direccion.x + cam.transform.up * direccion.y;
        return true;
    }

    /// <summary>Disparo propio (primera persona): el fogonazo y las estelas salen de la boca del arma tal como se ve en
    /// la pantalla (BocaPropia).</summary>
    public static void DisparoPropio(GameObject arma, ShopItem ficha, Vector3 muzzle, Vector3[] finales, CatalogoDeSkins.Efectos e)
    {
        if (!BocaPropia(arma, ficha, out Vector3 boca, out Vector3 haciaDonde)) boca = muzzle;
        Camera cam = Camera.main;
        Trazadora.Fogonazo(boca, false, e.fuego);
        if (e.llamarada) Lenguas(boca, haciaDonde, LlamaPropia, e);
        if (e.chispas && cam != null) Chispas(boca, haciaDonde, cam.transform.forward, LlamaPropia, e, cam.transform);
        if (e.arcos) Arcos(boca, haciaDonde, LlamaPropia, e, 2);
        if (e.humo || e.arcos) HumoDeSkin.DisparoPropio(arma, ficha, e);
        foreach (Vector3 fin in finales) Estela(boca, fin, e);
    }

    // Las tres lenguas de fuego: la del medio, larga y derecha; las de los costados, más cortas, abiertas en abanico (en
    // el plano de la pantalla) y enroscándose hacia afuera.
    private static void Lenguas(Vector3 boca, Vector3 haciaDonde, float largo, CatalogoDeSkins.Efectos e)
    {
        Camera cam = Camera.main;
        Vector3 mira = cam != null ? (boca - cam.transform.position).normalized : Vector3.forward;
        if (haciaDonde.sqrMagnitude < 0.0001f) haciaDonde = cam != null ? cam.transform.right : Vector3.right;
        LlamaradaDeSkin.Crear(boca, haciaDonde, largo, e, 0.45f, 0f);
        foreach (float lado in new[] { -1f, 1f })
        {
            Vector3 abierta = Quaternion.AngleAxis(AperturaLenguas * lado, mira) * haciaDonde;
            LlamaradaDeSkin.Crear(boca, abierta, largo * 0.75f, e, 0.4f, -lado);
        }
    }

    private const float AperturaLenguas = 26f; // grados entre la del medio y las de los costados

    // La cámara que dibuja el arma de primera persona (la de las armas, hija de la principal), o la principal.
    private static Camera CamaraDelArma(Camera cam, int capa, GameObject arma)
    {
        foreach (Camera c in cam.GetComponentsInChildren<Camera>(true))
            if (c != cam && c.enabled && (c.cullingMask & (1 << capa)) != 0) return c;
        // El vacío puede estar en otra capa: la del modelo del arma.
        foreach (Renderer r in arma.GetComponentsInChildren<Renderer>())
            foreach (Camera c in cam.GetComponentsInChildren<Camera>(true))
                if (c != cam && c.enabled && (c.cullingMask & (1 << r.gameObject.layer)) != 0) return c;
        return cam;
    }

    /// <summary>Para ajustar la boca (Skins de prueba): el fogonazo y una estela hacia adelante, sin disparar.</summary>
    public static void Prueba(GameObject arma, ShopItem ficha, CatalogoDeSkins.Efectos e)
    {
        Camera cam = Camera.main;
        if (cam == null || e == null) return;
        DisparoPropio(arma, ficha, Trazadora.BocaLocal(cam), new[] { cam.transform.position + cam.transform.forward * 25f }, e);
    }

    /// <summary>Disparo de otro jugador (su arma en la mano, US 380): desde la boca de ese modelo. modelo: el arma en
    /// su mano, para que el humo siga saliendo de ahí aunque se mueva.</summary>
    public static void DisparoDeOtro(Vector3 boca, Vector3 adelante, CatalogoDeSkins.Efectos e, GameObject modelo = null)
    {
        Trazadora.Fogonazo(boca, false, e.fuego);
        if (e.llamarada) Lenguas(boca, adelante, LlamaDeOtro, e);
        if (e.chispas) Chispas(boca, adelante, adelante, LlamaDeOtro, e, null);
        if (e.arcos) Arcos(boca, adelante, LlamaDeOtro, e, 2);
        if ((e.humo || e.arcos) && modelo != null) HumoDeSkin.DisparoDeOtro(modelo, boca, e);
    }

    // ---------- Rayos ----------

    /// <summary>Unos arquitos eléctricos que saltan de la boca (o de donde pegó un rayo), hacia cualquier lado pero más
    /// hacia adelante. largo: el de la llamarada del arma (el de la propia o el de los demás).
    /// grueso: cuánto más gruesos y duraderos (los de después de disparar, que tienen que notarse solos).</summary>
    public static void Arcos(Vector3 desde, Vector3 adelante, float largo, CatalogoDeSkins.Efectos e, int cuantos, float grueso = 1f)
    {
        Vector3 hacia = adelante.sqrMagnitude > 0.0001f ? adelante.normalized : Vector3.forward;
        // El grosor, según lo lejos que está de la cámara (que se vea igual de cerca y de lejos).
        Camera cam = Camera.main;
        float distancia = cam != null ? Vector3.Distance(cam.transform.position, desde) : 5f;
        float grosor = Mathf.Clamp(0.004f * distancia, 0.003f, 0.06f) * grueso;
        for (int i = 0; i < cuantos; i++)
        {
            Vector3 dir = (hacia * 0.6f + Random.onUnitSphere).normalized;
            float l = largo * Random.Range(0.3f, 0.6f);
            RayoDeSkin.Crear(desde, desde + dir * l, e, grosor, grosor * 0.6f, Random.value < 0.5f ? 1 : 0,
                             Random.Range(0.06f, 0.1f) * Mathf.Sqrt(grueso));
        }
    }

    // ---------- Chispas y humo ----------

    // Escala de las chispas y el humo: 1 en el arma propia (a un metro de la cámara); más grandes en la de los demás.
    private static float Escala(float largoDeLlama) => largoDeLlama / LlamaPropia;

    private static ParticleSystem chispasMundo, chispasPropias, humoMundo, humoPropio;

    /// <summary>Chispas que saltan de la boca en cono hacia adelante, caen un poco y se apagan. propias: la cámara (se
    /// mueven con ella, como el arma en la mano) o null (en el mundo).</summary>
    private static void Chispas(Vector3 boca, Vector3 haciaDonde, Vector3 adelante, float largo, CatalogoDeSkins.Efectos e, Transform propias)
    {
        ParticleSystem ps = propias != null ? Particulas(ref chispasPropias, "efecto-brasa", "Chispas de las skins (propias)", propias, true)
                                            : Particulas(ref chispasMundo, "efecto-brasa", "Chispas de las skins", null, true);
        if (ps == null) return;
        float escala = Escala(largo);
        Vector3 hacia = (haciaDonde.normalized * 0.5f + adelante.normalized).normalized;
        var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        for (int i = 0; i < ChispasPorDisparo; i++)
        {
            Vector3 dir = (hacia + Random.insideUnitSphere * 0.45f).normalized;
            Vector3 velocidad = dir * Random.Range(1.2f, 3.5f) * escala;
            p.position = propias != null ? propias.InverseTransformPoint(boca) : boca;
            p.velocity = propias != null ? propias.InverseTransformDirection(velocidad) : velocidad;
            p.startSize = Random.Range(0.006f, 0.012f) * escala;
            p.startLifetime = Random.Range(0.15f, 0.4f);
            p.startColor = Color.Lerp(e.fuego, e.nucleo, Random.Range(0.3f, 1f));
            ps.Emit(p, 1);
        }
    }

    private const int ChispasPorDisparo = 14;

    /// <summary>Una bocanada de humo (o, si brasa, una brasa que sube con el humo) en la boca. propio: el arma en la mano
    /// (se mueve con la cámara). cuanto: de 0 a 1, cuánto humo hay (lo más oscuro).</summary>
    public static void Bocanada(Vector3 boca, bool propio, float escala, float cuanto, CatalogoDeSkins.Efectos e, bool brasa)
    {
        Camera cam = Camera.main;
        Transform propia = propio && cam != null ? cam.transform : null;
        if (propio && propia == null) return;
        ParticleSystem ps = propia != null ? Particulas(ref humoPropio, "efecto-humo", "Humo de las skins (propio)", propia, false)
                                           : Particulas(ref humoMundo, "efecto-humo", "Humo de las skins", null, false);
        if (ps == null) return;
        Vector3 arriba = Vector3.up, costado = propia != null ? propia.right : Vector3.right;
        Vector3 velocidad = brasa
            ? arriba * Random.Range(0.15f, 0.3f) * escala + Random.insideUnitSphere * 0.04f * escala
            : arriba * Random.Range(0.08f, 0.14f) * escala + costado * Random.Range(-0.02f, 0.03f) * escala;
        var p = new ParticleSystem.EmitParams
        {
            applyShapeToPosition = false,
            position = propia != null ? propia.InverseTransformPoint(boca) : boca,
            velocity = propia != null ? propia.InverseTransformDirection(velocidad) : velocidad,
            startSize = (brasa ? Random.Range(0.0018f, 0.0028f) : Random.Range(0.02f, 0.034f)) * escala,
            startLifetime = brasa ? Random.Range(0.6f, 1f) : Random.Range(1.2f, 1.8f),
            rotation = Random.Range(0f, 360f),
            // El humo, negro (el material le pone el color) y más tupido cuanto más se disparó; la brasa, del fuego.
            startColor = brasa ? (Color32)Color.Lerp(e.fuego, e.nucleo, Random.value)
                               : (Color32)new Color(0f, 0f, 0f, Mathf.Lerp(0.6f, 1f, cuanto)),
        };
        ps.Emit(p, 1);
    }

    /// <summary>Borra el humo del arma propia (recargó o guardó el arma: el humo quedaría flotando solo).</summary>
    public static void BorrarHumoPropio()
    {
        if (humoPropio != null) humoPropio.Clear();
    }

    // Un sistema de partículas para emitir a mano (sin emisión propia): en el mundo, o hijo de la cámara y moviéndose
    // con ella. Las chispas se estiran según su velocidad; el humo crece y se deshace al subir.
    private static ParticleSystem Particulas(ref ParticleSystem ps, string material, string nombre, Transform padre, bool chispas)
    {
        if (ps != null)
        {
            if (padre != null && ps.transform.parent != padre) ps.transform.SetParent(padre, false); // otra cámara
            return ps;
        }
        Material m = ModelosDeArmas.Actual.MaterialDe(material);
        if (m == null) return null;
        var go = new GameObject(nombre);
        if (padre != null) go.transform.SetParent(padre, false);
        ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = padre != null ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
        main.gravityModifier = chispas ? (padre != null ? 0.15f : 0.5f) : 0f;
        main.maxParticles = chispas ? 400 : 500;
        ParticleSystem.EmissionModule emision = ps.emission;
        emision.enabled = false;
        ParticleSystem.ShapeModule forma = ps.shape;
        forma.enabled = false;
        ParticleSystem.ColorOverLifetimeModule apagar = ps.colorOverLifetime;
        apagar.enabled = true;
        var degrade = new Gradient();
        if (chispas)
            degrade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        else // el humo aparece de a poco y se deshace al subir
            degrade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        apagar.color = degrade;
        if (!chispas)
        {
            ParticleSystem.SizeOverLifetimeModule crece = ps.sizeOverLifetime;
            crece.enabled = true;
            crece.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 4f));
        }
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        if (chispas)
        {
            r.renderMode = ParticleSystemRenderMode.Stretch; // rayitas en la dirección en que vuelan
            r.velocityScale = 0.035f;
            r.lengthScale = 1.5f;
        }
        ps.Play();
        return ps;
    }

    /// <summary>La estela de la legendaria, de la boca del arma hasta donde pegó.</summary>
    public static void Estela(Vector3 desde, Vector3 hasta, CatalogoDeSkins.Efectos e)
    {
        if (e.rayo)
        {
            // Un rayo de la boca hasta donde pegó (más fino junto a la boca, que está cerca de la cámara) y unos
            // arquitos donde pegó, del tamaño de lo que se ve a esa distancia.
            Camera cam = Camera.main;
            float lejos = cam != null ? Vector3.Distance(cam.transform.position, hasta) : 20f;
            float cerca = cam != null ? Vector3.Distance(cam.transform.position, desde) : 1f;
            RayoDeSkin.Crear(desde, hasta, e, Mathf.Clamp(0.012f * cerca, 0.01f, 0.06f), Mathf.Clamp(0.004f * lejos, 0.03f, 0.12f), 2, 0.13f);
            Arcos(hasta, desde - hasta, Mathf.Clamp(lejos * 0.02f, 0.1f, 0.5f), e, 3);
            return;
        }
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
        Camera camArma = CamaraDelArma(cam, capa, arma);
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
