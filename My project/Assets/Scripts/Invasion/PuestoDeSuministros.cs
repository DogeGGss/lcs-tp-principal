using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using static ShopUIKit;

// F08 (Modo Invasión), US 292: el puesto de suministros. Lo agrega PartidaDeInvasion.
// - CA1: desde la oleada 2, al empezar cada oleada cae un puesto en uno de los lugares del mapa (LugarDeSuministros),
//   nunca en el mismo que el anterior. Cae a cielo abierto cerca de ese lugar (nunca adentro de un edificio ni sobre
//   un techo) y es la caja de los mapas de práctica y Táctico (ConfigInvasion.cajaPuesto).
// - CA2: al caer aparece «Suministros en <lugar>», el puesto se ve en el minimapa (Minimapa) y tiene una marca encima
//   con la distancia, que se dibuja en la pantalla: se ve a través de las paredes.
// - CA3: alrededor hay un anillo verde (15 m de radio) en el piso y en el minimapa.
// - CA4: los puntos que se ganan adentro del anillo (US 147) cargan el puesto; los de afuera solo cuentan para el
//   récord. La carga se ve arriba del reloj del centro (CombatHud).
// - CA5: la meta es el 40 % de los puntos que da la oleada entera (la cuenta: invasores × puntos por invasor).
// - CA6: el de la oleada 2 cae ya cargado.
// - CA7: al superar la oleada, si está cargado se abre (Abrio, para la elección de la US 293); si no, se va sin dar nada.
// US 335: el próximo puesto se elige al empezar el descanso (CA1: «Próximo puesto: <lugar>»; CA2: la marca y el
// minimapa, más tenues, y la caja en el lugar exacto, verde y transparente, con el holograma; CA3: al empezar la
// oleada cae ahí, con la misma vuelta). CA8 y CA9: el camino en el piso hasta el puesto
// (CaminoAlPuesto).
// El radio, la meta y los puntos por invasor están en ConfigInvasion.
// F31 (Invasión cooperativa), US 353: el puesto lo decide el anfitrión y los demás lo repiten (InvasoresEnRed):
// - CA1: dónde cae, con su vuelta, y cuándo se va. CA3: el próximo (la marca, el camino y el holograma).
// - CA2: los puntos que gana cada jugador adentro del anillo se le mandan al anfitrión, que lleva la carga y la
//   reparte: la barra sube igual para todos, y al llegar al 100 % se carga para todos (SeCargo).
// - CA4 y CA6: al superar la oleada, si está cargado se abre para todos (cada uno elige lo suyo y recibe su munición).
// - CA5: los portales cerca del puesto los abre el anfitrión (US 290: los ven todos).
public class PuestoDeSuministros : MonoBehaviour
{
    public const int DesdeLaOleada = 2; // CA1 y CA6
    private const float Caida = 1.2f, AlturaDeCaida = 30f, AlturaDeLaMarca = 2.2f;
    private const float DuracionAviso = 3f, EntradaAviso = 0.25f;

    public static PuestoDeSuministros Actual { get; private set; }

    /// <summary>CA7: el puesto estaba cargado al terminar la oleada (la US 293 abre la elección).</summary>
    public static event System.Action Abrio;
    /// <summary>El puesto llegó al 100 % peleando (no el de la oleada 2, que cae cargado): el puesto (cartas Despeje,
    /// US 333, y Entrega inmediata, US 332).</summary>
    public static event System.Action<PuestoDeSuministros> SeCargo;

    /// <summary>Hay un puesto en el mapa.</summary>
    public bool Hay => puesto != null;
    public Vector3 Lugar { get; private set; }
    public string NombreDelLugar { get; private set; }
    public float Radio => ConfigInvasion.Actual.radioPuesto;
    public int Carga { get; private set; }
    public int Meta { get; private set; }
    public float Progreso => Meta > 0 ? Mathf.Clamp01(Carga / (float)Meta) : 0f;
    public bool Cargado => Meta > 0 && Carga >= Meta;
    /// <summary>CA4: el jugador está adentro del anillo.</summary>
    public bool Adentro { get; private set; }

    /// <summary>F31 (US 353, CA6): hay al menos un jugador vivo adentro del anillo (este o un compañero).</summary>
    public bool AlguienAdentro()
    {
        if (!Hay) return false;
        if (Adentro) return true;
        PartidaEnRed partida = PartidaEnRed.Actual;
        if (partida == null) return false;
        foreach (JugadorEnRed j in partida.Jugadores)
        {
            if (j == null || !j.Vivo || j.EstaCaido) continue;
            Vector3 d = j.transform.position - Lugar;
            d.y = 0f;
            if (d.sqrMagnitude <= Radio * Radio) return true;
        }
        return false;
    }
    /// <summary>US 335: ya se sabe dónde cae el próximo (en el descanso).</summary>
    public bool HayProximo { get; private set; }
    public Vector3 LugarProximo { get; private set; }
    public string NombreProximo { get; private set; }
    private float giroProximo;     // US 335: la vuelta de la caja (la misma con la que cae)
    private GameObject fantasma;   // US 335, CA2: la caja verde transparente donde va a caer
    /// <summary>US 335: adonde hay que ir: el puesto que está, o el próximo.</summary>
    public bool HayDestino => Hay || HayProximo;
    public Vector3 Destino => Hay ? Lugar : LugarProximo;
    /// <summary>US 335: el jugador está adentro del anillo del destino (el puesto o el próximo).</summary>
    public bool AdentroDelDestino { get; private set; }

    private OleadasDeInvasion oleadas;
    private List<(string nombre, Vector3 lugar)> lugares;
    private string anterior;
    private GameObject puesto;
    private Transform cuerpo;
    private float cayoEn;
    private Transform jugador;
    private static Material linea;

    // La marca y el aviso, en la pantalla.
    private RectTransform lienzo, marca, aviso;
    private CanvasGroup marcaGrupo, avisoGrupo;
    private TextMeshProUGUI distancia, avisoTexto;
    private float avisoDesde = -10f;

    private void Awake() => Actual = this;

    private void OnEnable()
    {
        OleadasDeInvasion.Empezo += AlEmpezar;
        OleadasDeInvasion.Supero += AlSuperar;
        OleadasDeInvasion.EmpezoElDescanso += AlEmpezarElDescanso;
        PuntosDeInvasion.Sumo += AlSumar;
    }

    private void OnDisable()
    {
        OleadasDeInvasion.Empezo -= AlEmpezar;
        OleadasDeInvasion.Supero -= AlSuperar;
        OleadasDeInvasion.EmpezoElDescanso -= AlEmpezarElDescanso;
        PuntosDeInvasion.Sumo -= AlSumar;
    }

    private void OnDestroy()
    {
        if (Actual == this) Actual = null;
        Quitar();
        QuitarFantasma();
        if (lienzo != null) Destroy(lienzo.gameObject);
    }

    // ---------- CA1 y CA6: cae ----------

    private void AlEmpezar(int oleada)
    {
        if (InvasoresEnRed.EsCopia) return; // US 353: cae donde dice el anfitrión (CaeEnElAnfitrion)
        Quitar(); // (Ir a la oleada 10 corta una oleada a la mitad)
        if (oleada < DesdeLaOleada) return;
        if (oleadas == null) oleadas = GetComponent<OleadasDeInvasion>();
        string nombre;
        Vector3 lugar;
        float giro = Random.Range(0f, 360f);
        if (HayProximo) { nombre = NombreProximo; lugar = LugarProximo; giro = giroProximo; } // US 335, CA3: donde se avisó
        else if (!Elegir(out nombre, out lugar)) return;
        HayProximo = false;
        QuitarFantasma();

        ConfigInvasion config = ConfigInvasion.Actual;
        int total = oleadas != null ? oleadas.Total : 1;
        Meta = Mathf.Max(1, Mathf.CeilToInt(config.metaPuesto * total * config.puntosPorInvasor * DificultadDeInvasion.Puntos)); // CA5
        Carga = oleada == DesdeLaOleada ? Meta : 0; // CA6
        NombreDelLugar = nombre;
        Lugar = lugar;
        anterior = nombre;
        Armar(giro);
        Avisar(L10n.T("gameplay.station.supplies_in_fmt", nombre)); // CA2
        InvasoresEnRed.AvisarPuesto(nombre, lugar, giro, Meta, Carga); // US 353, CA1
    }

    // ---------- F31 (US 353): el puesto del anfitrión ----------

    /// <summary>US 353, CA1: el anfitrión largó el puesto de esta oleada: cae acá igual.</summary>
    public void CaeEnElAnfitrion(string nombre, Vector3 lugar, float giro, int meta, int carga)
    {
        Quitar();
        HayProximo = false;
        QuitarFantasma();
        Meta = meta;
        Carga = Mathf.Min(meta, carga);
        NombreDelLugar = nombre;
        Lugar = lugar;
        anterior = nombre;
        Armar(giro);
        Avisar(L10n.T("gameplay.station.supplies_in_fmt", nombre));
    }

    /// <summary>US 353, CA3: el anfitrión eligió dónde cae el próximo.</summary>
    public void ProximoEnElAnfitrion(string nombre, Vector3 lugar, float giro)
    {
        QuitarFantasma();
        NombreProximo = nombre;
        LugarProximo = lugar;
        giroProximo = giro;
        HayProximo = true;
        ArmarFantasma();
        Avisar(L10n.T("gameplay.station.next_fmt", nombre));
    }

    /// <summary>US 353, CA2: la carga que lleva el anfitrión (seCargo: llegó al 100 % peleando).</summary>
    public void CargaDelAnfitrion(int carga, int meta, bool seCargo)
    {
        if (!Hay) return;
        bool antes = Cargado;
        Meta = meta;
        Carga = Mathf.Min(meta, carga);
        if (seCargo && !antes && Cargado) { Avisar(L10n.T("gameplay.station.charged")); SeCargo?.Invoke(this); }
    }

    /// <summary>US 353, CA2: en el anfitrión, los puntos que un compañero ganó adentro del anillo.</summary>
    public void SumarDeUnCompanero(int puntos) => Cargar(puntos);

    // CA4: suma a la carga (en cooperativo, solo el anfitrión, que se la manda a todos).
    private void Cargar(int puntos)
    {
        if (!Hay || Cargado || puntos <= 0) return;
        Carga = Mathf.Min(Meta, Carga + puntos);
        bool seCargo = Cargado;
        if (seCargo) { Avisar(L10n.T("gameplay.station.charged")); SeCargo?.Invoke(this); }
        InvasoresEnRed.AvisarCargaDelPuesto(Carga, Meta, seCargo);
    }

    // US 335, CA1: al empezar el descanso se elige dónde cae el próximo y se avisa.
    private void AlEmpezarElDescanso(int proxima)
    {
        if (InvasoresEnRed.EsCopia) return; // US 353, CA3: el próximo lo elige el anfitrión (ProximoEnElAnfitrion)
        HayProximo = false;
        QuitarFantasma();
        if (proxima < DesdeLaOleada || !Elegir(out string nombre, out Vector3 lugar)) return;
        NombreProximo = nombre;
        LugarProximo = lugar;
        giroProximo = Random.Range(0f, 360f);
        HayProximo = true;
        ArmarFantasma();
        Avisar(L10n.T("gameplay.station.next_fmt", nombre));
        InvasoresEnRed.AvisarProximoPuesto(nombre, lugar, giroProximo); // US 353, CA3
    }

    // US 335, CA2: la caja del puesto en el lugar exacto donde va a caer, verde y transparente (el holograma del perro
    // del Callejero), sin colisión.
    private static Material holograma;

    private void ArmarFantasma()
    {
        GameObject modelo = ConfigInvasion.Actual.cajaPuesto;
        if (holograma == null)
        {
            Shader shader = Resources.Load<Shader>("Efectos/Holograma");
            if (shader == null) shader = Shader.Find("Efectos/Holograma");
            if (shader == null) return;
            holograma = new Material(shader) { name = "Próximo puesto" };
            Color verde = MarcadorInvasion.Verde;
            holograma.SetColor("_BaseColor", new Color(verde.r, verde.g, verde.b, 0.8f));
        }
        fantasma = new GameObject("Próximo puesto");
        fantasma.transform.position = LugarProximo;
        Transform caja;
        if (modelo != null)
        {
            caja = Instantiate(modelo, fantasma.transform).transform;
            caja.localScale = Vector3.one * ConfigInvasion.Actual.escalaCajaPuesto;
        }
        else
        {
            caja = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            caja.SetParent(fantasma.transform, false);
            caja.localPosition = new Vector3(0f, 0.45f, 0f);
            caja.localScale = new Vector3(1.4f, 0.9f, 1f);
        }
        caja.localRotation = Quaternion.Euler(0f, giroProximo, 0f);
        foreach (Collider c in fantasma.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (Renderer r in fantasma.GetComponentsInChildren<Renderer>(true))
        {
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = holograma;
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    private void QuitarFantasma()
    {
        if (fantasma != null) Destroy(fantasma);
        fantasma = null;
    }

    // Un lugar al azar que no sea el anterior. Cerca de ese lugar se busca un punto a cielo abierto (nada encima: ni
    // techo ni edificio) y al que se llegue caminando desde el jugador (así no cae sobre un techo). Si ninguno sirve,
    // cerca del jugador con la misma regla.
    private bool Elegir(out string nombre, out Vector3 lugar)
    {
        if (lugares == null) BuscarLugares();
        Transform yo = Jugador();
        Vector3 desde = yo != null ? yo.position : Vector3.zero;
        var opciones = new List<(string nombre, Vector3 lugar)>();
        foreach (var l in lugares) if (l.nombre != anterior) opciones.Add(l);
        if (opciones.Count == 0) opciones.AddRange(lugares);
        while (opciones.Count > 0)
        {
            int i = Random.Range(0, opciones.Count);
            if (Afuera(opciones[i].lugar, desde, BuscarHasta, out lugar)) { nombre = opciones[i].nombre; return true; }
            opciones.RemoveAt(i);
        }
        nombre = L10n.T("gameplay.station.surroundings");
        return Afuera(desde, desde, BuscarHasta, out lugar);
    }

    private const float BuscarHasta = 40f, PasoDeBusqueda = 4f;
    private NavMeshPath camino;

    // En anillos de 4 m alrededor de "cerca": un punto del NavMesh con el cielo libre y conectado con "desde".
    private bool Afuera(Vector3 cerca, Vector3 desde, float hasta, out Vector3 lugar)
    {
        bool hayDesde = NavMesh.SamplePosition(desde, out NavMeshHit origen, 4f, NavMesh.AllAreas);
        for (float r = 0f; r <= hasta; r += PasoDeBusqueda)
        {
            int pasos = r <= 0f ? 1 : Mathf.CeilToInt(2f * Mathf.PI * r / PasoDeBusqueda);
            float giro = Random.value * Mathf.PI * 2f;
            for (int k = 0; k < pasos; k++)
            {
                float a = giro + k * Mathf.PI * 2f / pasos;
                Vector3 p = cerca + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;
                if (!CieloAbierto(hit.position)) continue;
                if (hayDesde)
                {
                    if (camino == null) camino = new NavMeshPath();
                    if (!NavMesh.CalculatePath(origen.position, hit.position, NavMesh.AllAreas, camino) || camino.status != NavMeshPathStatus.PathComplete) continue;
                }
                lugar = hit.position;
                return true;
            }
        }
        lugar = cerca;
        return false;
    }

    // Nada encima en 80 m (con un poco de ancho, para que la caja no roce un alero al caer). También se mira desde
    // arriba hacia abajo: los techos de malla no se ven desde adentro del edificio, así que solo con el rayo hacia
    // arriba el puesto podía caer adentro de un edificio. Desde arriba, lo primero que se toca tiene que ser el piso.
    private static bool CieloAbierto(Vector3 piso)
    {
        if (Physics.SphereCast(piso + Vector3.up * 1.2f, 0.9f, Vector3.up, out RaycastHit _, 80f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return false;
        return !Physics.SphereCast(piso + Vector3.up * 80f, 0.9f, Vector3.down, out RaycastHit arriba, 78.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            || arriba.point.y <= piso.y + 1.5f;
    }

    private void BuscarLugares()
    {
        var crudos = new List<(string nombre, Vector3 lugar)>();
        foreach (LugarDeSuministros l in LugarDeSuministros.Todos)
            crudos.Add((string.IsNullOrWhiteSpace(l.nombre) ? l.name : l.nombre, l.transform.position));
        if (crudos.Count == 0) crudos = LugarDeSuministros.Automaticos();

        lugares = new List<(string, Vector3)>();
        foreach (var c in crudos)
            if (NavMesh.SamplePosition(c.lugar, out NavMeshHit hit, 20f, NavMesh.AllAreas)) lugares.Add((c.nombre, hit.position));
            else if (LugarDeSuministros.Todos.Count > 0) lugares.Add(c); // los puestos a mano se respetan
    }

    // ---------- CA4: la carga ----------

    private void AlSumar(int puntos, bool cabeza)
    {
        if (!Hay || Cargado) return;
        ActualizarAdentro();
        if (!Adentro) return; // afuera cuenta para el récord, no para el puesto
        // US 353, CA2: en cooperativo la carga la lleva el anfitrión (los puntos de cada uno se le mandan).
        if (InvasoresEnRed.EsCopia) { InvasoresEnRed.MandarPuntosAlPuesto(puntos); return; }
        Cargar(puntos);
    }

    /// <summary>Para probar (Riftwalker > Invasión > Cargar el puesto).</summary>
    public void CargarDePrueba()
    {
        if (!Hay || Cargado) return;
        if (InvasoresEnRed.EsCopia) { InvasoresEnRed.MandarPuntosAlPuesto(Meta); return; } // US 353: lo carga el anfitrión
        Carga = Meta;
        Avisar("Puesto cargado");
        SeCargo?.Invoke(this);
        InvasoresEnRed.AvisarCargaDelPuesto(Carga, Meta, true);
    }

    private void ActualizarAdentro()
    {
        Transform yo = Jugador();
        if (!Hay || yo == null) { Adentro = false; return; }
        Vector3 d = yo.position - Lugar;
        Adentro = new Vector2(d.x, d.z).sqrMagnitude <= Radio * Radio && Mathf.Abs(d.y) < 6f;
    }

    private void ActualizarAdentroDelDestino()
    {
        Transform yo = Jugador();
        if (!HayDestino || yo == null) { AdentroDelDestino = false; return; }
        Vector3 d = yo.position - Destino;
        AdentroDelDestino = new Vector2(d.x, d.z).sqrMagnitude <= Radio * Radio && Mathf.Abs(d.y) < 6f;
    }

    /// <summary>US 335: el jugador (para el camino).</summary>
    public Transform ElJugador => Jugador();

    private Transform Jugador()
    {
        if (jugador == null)
        {
            PlayerMovement p = FindFirstObjectByType<PlayerMovement>();
            if (p != null) jugador = p.transform;
        }
        return jugador;
    }

    // ---------- CA7: se abre o se va ----------

    /// <summary>US 332, Entrega inmediata: la elección de este puesto ya se hizo; al terminar la oleada se abre igual,
    /// pero solo da la munición.</summary>
    public bool Entregado { get; private set; }

    public void Entregar()
    {
        if (Hay) Entregado = true;
    }

    private void AlSuperar(int oleada)
    {
        if (!Hay) return;
        if (Cargado)
        {
            Avisar(L10n.T("gameplay.station.open"));
            Abrio?.Invoke();
        }
        else Avisar(L10n.T("gameplay.station.left"));
        Quitar();
    }

    private void Quitar()
    {
        if (puesto != null) Destroy(puesto);
        puesto = null;
        cuerpo = null;
        Adentro = false;
        Entregado = false;
        Carga = Meta = 0;
    }

    // ---------- El puesto en el mapa ----------

    private void Armar(float giro)
    {
        Color verde = MarcadorInvasion.Verde;
        puesto = new GameObject("Puesto de suministros");
        puesto.transform.position = Lugar;

        cuerpo = new GameObject("Cajón").transform;
        cuerpo.SetParent(puesto.transform, false);
        GameObject modelo = ConfigInvasion.Actual.cajaPuesto;
        if (modelo != null) // la caja de los mapas de práctica y Táctico (Crate_01)
        {
            Transform caja = Instantiate(modelo, cuerpo).transform;
            caja.localPosition = Vector3.zero;
            caja.localRotation = Quaternion.Euler(0f, giro, 0f);
            caja.localScale = Vector3.one * ConfigInvasion.Actual.escalaCajaPuesto;
        }
        else
        {
            Caja(cuerpo, new Vector3(0f, 0.45f, 0f), new Vector3(1.4f, 0.9f, 1f), new Color(0.16f, 0.2f, 0.14f), false);
            Caja(cuerpo, new Vector3(0f, 0.92f, 0f), new Vector3(1.46f, 0.08f, 1.06f), verde, true);
        }
        var luz = new GameObject("Luz").AddComponent<Light>();
        luz.transform.SetParent(cuerpo, false);
        luz.transform.localPosition = Vector3.up * 2f;
        luz.type = LightType.Point;
        luz.color = verde;
        luz.range = 8f;
        luz.intensity = 2f;
        luz.shadows = LightShadows.None;

        // La columna de luz, para encontrarlo de lejos.
        LineRenderer columna = Linea("Columna", 0.35f, WithAlpha(verde, 0.45f));
        columna.positionCount = 2;
        columna.SetPosition(0, Lugar);
        columna.SetPosition(1, Lugar + Vector3.up * 30f);
        columna.endColor = WithAlpha(verde, 0f);

        // CA3: el anillo en el piso.
        LineRenderer anillo = Linea("Anillo", 0.18f, WithAlpha(verde, 0.85f));
        const int puntos = 96;
        anillo.positionCount = puntos;
        anillo.loop = true;
        for (int i = 0; i < puntos; i++)
        {
            float a = i * Mathf.PI * 2f / puntos;
            Vector3 p = Lugar + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Radio;
            if (Physics.Raycast(p + Vector3.up * 4f, Vector3.down, out RaycastHit piso, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                p.y = piso.point.y;
            anillo.SetPosition(i, p + Vector3.up * 0.08f);
        }

        cayoEn = Time.time;
        cuerpo.localPosition = Vector3.up * AlturaDeCaida;
    }

    private void Caja(Transform padre, Vector3 lugar, Vector3 medida, Color color, bool brilla)
    {
        GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(c.GetComponent<Collider>()); // no tapa el paso de los invasores
        c.transform.SetParent(padre, false);
        c.transform.localPosition = lugar;
        c.transform.localScale = medida;
        Material m = c.GetComponent<Renderer>().material;
        m.color = color;
        if (brilla)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * 1.5f);
        }
    }

    private LineRenderer Linea(string nombre, float ancho, Color color)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(puesto.transform, false);
        LineRenderer l = go.AddComponent<LineRenderer>();
        if (linea == null) linea = new Material(Shader.Find("Sprites/Default")) { name = "Puesto de suministros" };
        l.sharedMaterial = linea;
        l.useWorldSpace = true;
        l.widthMultiplier = ancho;
        l.startColor = l.endColor = color;
        l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        l.receiveShadows = false;
        return l;
    }

    // ---------- Cada cuadro ----------

    private void Update()
    {
        ActualizarAdentro();
        ActualizarAdentroDelDestino();
        if (GetComponent<CaminoAlPuesto>() == null) gameObject.AddComponent<CaminoAlPuesto>(); // US 335, CA8
        if (cuerpo != null)
        {
            float t = Mathf.Clamp01((Time.time - cayoEn) / Caida);
            cuerpo.localPosition = Vector3.up * AlturaDeCaida * (1f - t * t); // cae acelerando
        }
        if (lienzo == null && MatchHud.Instance != null) ArmarPantalla();
        if (lienzo == null) return;
        ActualizarMarca();
        ActualizarAviso();
    }

    private void ArmarPantalla()
    {
        var go = new GameObject("Puesto de suministros (pantalla)", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        go.layer = 5;
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 36; // como las marcas de compañeros: debajo del HUD (40)
        var escala = go.GetComponent<UnityEngine.UI.CanvasScaler>();
        escala.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        lienzo = (RectTransform)go.transform;
        TMP_FontAsset display = MatchHud.Instance.DisplayFont, label = MatchHud.Instance.LabelFont;
        Sprite redondeado = MatchHud.Instance.Rounded;
        Color verde = MarcadorInvasion.Verde;

        // CA2: la marca, un rombo verde con la distancia abajo.
        marca = Node("Marca", lienzo);
        marca.anchorMin = marca.anchorMax = marca.pivot = new Vector2(0.5f, 0.5f);
        marca.sizeDelta = new Vector2(120f, 60f);
        marcaGrupo = marca.gameObject.AddComponent<CanvasGroup>();
        marcaGrupo.blocksRaycasts = false;
        RectTransform rombo = Node("Rombo", marca);
        rombo.anchorMin = rombo.anchorMax = rombo.pivot = new Vector2(0.5f, 1f);
        rombo.sizeDelta = new Vector2(20f, 20f);
        rombo.anchoredPosition = new Vector2(0f, -6f);
        rombo.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Image(rombo, redondeado, Rgb(6, 10, 6, 0.85f), 3f);
        RectTransform adentro = Stretch(Node("Color", rombo));
        adentro.offsetMin = new Vector2(3f, 3f);
        adentro.offsetMax = new Vector2(-3f, -3f);
        Image(adentro, redondeado, verde, 2f);
        distancia = Text(Place(Node("Distancia", marca), -30f, 32f, 180f, 22f), label, 15f, verde, TextAlignmentOptions.Center, 8f);
        distancia.rectTransform.anchorMin = distancia.rectTransform.anchorMax = new Vector2(0f, 1f);
        marca.gameObject.SetActive(false);

        // El aviso: una píldora verde arriba al centro, como el del récord (US 150).
        aviso = Node("Aviso", lienzo);
        aviso.anchorMin = aviso.anchorMax = aviso.pivot = new Vector2(0.5f, 1f);
        avisoGrupo = aviso.gameObject.AddComponent<CanvasGroup>();
        avisoGrupo.blocksRaycasts = false;
        MarcadorInvasion.Figura<FormaRedondeada>(Stretch(Node("Fondo", aviso))).Pintar(Rgb(10, 24, 8, 0.9f), verde, 22f, 1f);
        avisoTexto = Text(Stretch(Node("Texto", aviso)), display, 24f, verde, TextAlignmentOptions.Center, 10f, true);
        aviso.gameObject.SetActive(false);
    }

    private void ActualizarMarca()
    {
        Camera camara = Camera.main;
        bool ver = HayDestino && camara != null && !ShopUI.IsOpen && ResultadoDeInvasion.Actual == null;
        if (marca.gameObject.activeSelf != ver) marca.gameObject.SetActive(ver);
        if (!ver) return;

        Vector3 arriba = Destino + Vector3.up * AlturaDeLaMarca;
        Vector3 pantalla = camara.WorldToScreenPoint(arriba);
        if (pantalla.z < 0f) pantalla = new Vector3(Screen.width - pantalla.x, Screen.height - pantalla.y, 0f); // atrás: del lado opuesto
        bool afuera = pantalla.z <= 0f || pantalla.x < 0f || pantalla.x > Screen.width || pantalla.y < 0f || pantalla.y > Screen.height;
        if (afuera) // pegada al borde, del lado donde está
        {
            Vector2 medio = new Vector2(Screen.width, Screen.height) * 0.5f;
            Vector2 dir = (Vector2)pantalla - medio;
            if (pantalla.z <= 0f && dir.y > 0f) dir.y = -dir.y;
            if (dir.sqrMagnitude < 1f) dir = Vector2.down;
            float margen = 60f * Screen.height / 1080f;
            float k = Mathf.Min((medio.x - margen) / Mathf.Max(Mathf.Abs(dir.x), 0.001f), (medio.y - margen) / Mathf.Max(Mathf.Abs(dir.y), 0.001f));
            pantalla = medio + dir * k;
        }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(lienzo, pantalla, null, out Vector2 local);
        marca.anchoredPosition = local;
        float metros = Vector3.Distance(camara.transform.position, Destino);
        if (!Hay) // US 335, CA2: el próximo, más tenue
        {
            distancia.text = AdentroDelDestino ? L10n.T("gameplay.station.next_here") : L10n.T("gameplay.station.next_m_fmt", metros);
            marcaGrupo.alpha = 0.6f;
            return;
        }
        distancia.text = Adentro ? L10n.T("gameplay.station.inside") : $"{metros:0} m";
        marcaGrupo.alpha = Adentro ? 0.55f : 1f;
    }

    // Los avisos esperan en fila y salen recién cuando no hay un cartel grande en pantalla ("Oleada 3", "Oleada 2
    // superada"): el cartel los taparía. Si aparece un cartel mientras se ve uno, se esconde y el tiempo no corre.
    private readonly Queue<string> pendientes = new Queue<string>();

    private void Avisar(string texto) => pendientes.Enqueue(texto);

    private void Mostrar(string texto)
    {
        avisoTexto.text = texto;
        aviso.sizeDelta = new Vector2(Width(avisoTexto, texto) + 44f, 44f);
        aviso.gameObject.SetActive(true);
        avisoDesde = Time.unscaledTime;
    }

    private void ActualizarAviso()
    {
        bool tapado = MatchHud.BannerVisible;
        if (!aviso.gameObject.activeSelf)
        {
            if (pendientes.Count == 0 || tapado) return;
            Mostrar(pendientes.Dequeue());
        }
        if (tapado) { avisoDesde += Time.unscaledDeltaTime; avisoGrupo.alpha = 0f; return; }
        float t = Time.unscaledTime - avisoDesde;
        if (t >= DuracionAviso) { aviso.gameObject.SetActive(false); return; }
        float entra = Mathf.Clamp01(t / EntradaAviso), sale = Mathf.Clamp01((DuracionAviso - t) / EntradaAviso);
        avisoGrupo.alpha = Mathf.Min(entra, sale) * (ShopUI.IsOpen ? 0f : 1f);
        aviso.anchoredPosition = new Vector2(0f, -320f - 10f * (1f - entra));
    }
}
