using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static ShopUIKit;

// Espectar al morir en el Modo Táctico (US 133). El que muere no reaparece hasta la ronda siguiente (US 032):
// - CA1: durante 2 s ve su cámara quieta y no puede moverse, disparar ni comprar.
// - CA2: después la cámara pasa a la vista en primera persona de un compañero vivo, con su nombre en pantalla.
// - CA3: clic izquierdo pasa al siguiente compañero vivo y clic derecho al anterior.
// - CA4: si el compañero que mira muere, la cámara pasa sola a otro compañero vivo.
// - CA5: si no queda ningún compañero vivo, la cámara queda fija donde murió hasta el final de la ronda; si el
//   dispositivo está plantado, queda fija mirando el dispositivo (así se ve si explota o lo desactivan).
// - CA6: al empezar la ronda siguiente reaparece en su base (RondasTacticas) y la cámara vuelve a la normalidad.
// Solo se puede mirar a compañeros: nunca a los rivales ni moverse libre por el mapa.
// Mirando a un compañero se ve en primera persona lo que tiene en la mano: una copia del arma de primera persona
// propia que corresponde (y la mano, si BrazosEnCamara tiene la foto de los brazos con esa arma), que se sacude un
// poco con cada disparo suyo. La dibuja la cámara de las armas, como la propia.
// Lo agrega RondasTacticas al empezar una partida del Modo Táctico.
public class Espectador : MonoBehaviour
{
    public const float EsperaAlMorir = 2f; // CA1
    private const float DistanciaAlDispositivo = 4.5f, AlturaSobreDispositivo = 2.2f;

    private bool viendoDispositivo;
    private Vector3 dispositivoPos;
    private Quaternion dispositivoRot;

    private PartidaEnRed partida;
    private bool muerto;
    private float murioEn;
    private JugadorEnRed mirando;
    private Camera camara;
    private Vector3 camaraPos;
    private Quaternion camaraRot;
    private readonly List<Behaviour> apagados = new List<Behaviour>();
    private readonly List<Renderer> ocultos = new List<Renderer>();
    private readonly List<Renderer> armasOcultas = new List<Renderer>(); // el arma en la mano del que murió

    // El arma en la mano del compañero que se mira, en primera persona.
    private Camera camaraArmas;          // la cámara que dibuja las armas en la mano
    private JugadorEnRed local;
    private GameObject armaMirada;       // el arma de primera persona propia que corresponde a la del compañero
    private Transform vistaArma;         // su copia, hija de la cámara
    private readonly Dictionary<GameObject, Transform> vistas = new Dictionary<GameObject, Transform>();
    private ShopItem granadaMirada;      // la granada que tiene en la mano el compañero (no hay un modelo propio fijo)
    private readonly Dictionary<ShopItem, Transform> vistasGranada = new Dictionary<ShopItem, Transform>();
    private float sacudon;
    private const float SacudonAtras = 0.035f, SacudonArriba = 3f, SacudonVuelta = 8f;

    // Cartel de abajo con el nombre del compañero.
    private GameObject cartel;
    private TextMeshProUGUI cartelTitulo, cartelAyuda;

    public void Iniciar(PartidaEnRed partida)
    {
        this.partida = partida;
    }

    private void OnDestroy() => Terminar();

    // =====================================================================
    // Cuadro a cuadro
    // =====================================================================

    private void Update()
    {
        JugadorEnRed local = partida != null ? partida.Local : null;
        if (local == null) return;

        if (local.Vivo)
        {
            if (muerto) Terminar(); // CA6: arrancó la ronda siguiente y reapareció
            return;
        }

        if (!muerto) Empezar(local);
        if (Time.time - murioEn < EsperaAlMorir) return; // CA1: cámara quieta

        VerDispositivo(local);

        // CA4: si el que miraba murió (o se fue), se pasa a otro.
        if (mirando == null || !mirando.Vivo) Mirar(Siguiente(local, mirando, 1));
        // CA3
        else if (Input.GetMouseButtonDown(0)) Mirar(Siguiente(local, mirando, 1));
        else if (Input.GetMouseButtonDown(1)) Mirar(Siguiente(local, mirando, -1));
    }

    // La cámara se mueve después de que la copia del compañero se acomodó en este cuadro.
    private void LateUpdate()
    {
        if (!muerto || camara == null) return;
        if (mirando != null)
        {
            Transform ojos = mirando.Ojos;
            camara.transform.SetPositionAndRotation(ojos.position, ojos.rotation);
            VerArmaDelCompanero();
        }
        else if (viendoDispositivo)
            camara.transform.SetPositionAndRotation(dispositivoPos, dispositivoRot); // CA5, con dispositivo plantado
        else
        {
            // CA1 y CA5: quieta donde murió.
            camara.transform.localPosition = camaraPos;
            camara.transform.localRotation = camaraRot;
        }
    }

    // =====================================================================
    // Muerte y vuelta
    // =====================================================================

    // CA5: sin compañeros vivos y con el dispositivo plantado, la cámara se para cerca y lo mira.
    private void VerDispositivo(JugadorEnRed local)
    {
        RondasTacticas rondas = RondasTacticas.Actual;
        bool ver = rondas != null && rondas.HayDispositivo && Companeros(local).Count == 0;
        if (ver == viendoDispositivo) return;
        viendoDispositivo = ver;
        if (ver)
        {
            Vector3 bomba = rondas.LugarDelDispositivo;
            // Desde el lado donde murió el jugador, a unos metros y un poco arriba.
            Vector3 hacia = local.transform.position - bomba;
            hacia.y = 0f;
            if (hacia.sqrMagnitude < 0.25f) hacia = -local.transform.forward;
            Vector3 centro = bomba + Vector3.up * 0.4f;
            Vector3 lugar = centro + hacia.normalized * DistanciaAlDispositivo + Vector3.up * AlturaSobreDispositivo;
            // Si hay una pared en el medio, la cámara se queda de este lado.
            if (Physics.Linecast(centro, lugar, out RaycastHit pared, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                lugar = Vector3.Lerp(centro, pared.point, 0.85f);
            dispositivoPos = lugar;
            dispositivoRot = Quaternion.LookRotation(centro - lugar, Vector3.up);
        }
        MostrarCartel();
    }

    private void Empezar(JugadorEnRed local)
    {
        muerto = true;
        viendoDispositivo = false;
        murioEn = Time.time;
        mirando = null;
        camara = Camera.main;
        if (camara != null)
        {
            camaraPos = camara.transform.localPosition;
            camaraRot = camara.transform.localRotation;
        }

        // CA1: ni mirar alrededor (el movimiento y las armas ya los traba JugadorEnRed al morir).
        this.local = local;
        foreach (Behaviour c in local.GetComponentsInChildren<Behaviour>(true))
        {
            bool camaraDeArmas = c is Camera cam && cam != camara; // la que dibuja el arma en la mano
            if (camaraDeArmas) camaraArmas = (Camera)c;
            if (c.enabled && (c is CameraLook || c is PlayerMovement || c is WeaponSwitcher || camaraDeArmas))
            {
                c.enabled = false;
                apagados.Add(c);
            }
        }
        // El arma en la mano cuelga de la cámara: si no se oculta, se ve al mirar a un compañero o al dispositivo.
        OcultarArmas(local.Ojos);
        if (camara != null) OcultarArmas(camara.transform);
        // Comprar: la tienda solo abre en la fase de compra, y ahí no se puede morir (no se dispara).
    }

    private void OcultarArmas(Transform raiz)
    {
        if (raiz == null) return;
        foreach (Renderer r in raiz.GetComponentsInChildren<Renderer>(true))
            if (r.enabled) { r.enabled = false; armasOcultas.Add(r); }
    }

    private void Terminar()
    {
        if (!muerto) return;
        muerto = false;
        Mirar(null);
        if (camara != null)
        {
            camara.transform.localPosition = camaraPos;
            camara.transform.localRotation = camaraRot;
        }
        foreach (Transform vista in vistas.Values) if (vista != null) Destroy(vista.gameObject);
        vistas.Clear();
        foreach (Transform vista in vistasGranada.Values) if (vista != null) Destroy(vista.gameObject);
        vistasGranada.Clear();
        vistaArma = null;
        armaMirada = null;
        granadaMirada = null;
        // Por si no pasó por Mirar(null): el HUD vuelve a mostrar lo propio.
        CombatHud.Spectate(null);
        ShopUI.Spectated = null;
        foreach (Behaviour c in apagados) if (c != null) c.enabled = true;
        apagados.Clear();
        foreach (Renderer r in armasOcultas) if (r != null) r.enabled = true;
        armasOcultas.Clear();
        if (cartel != null) cartel.SetActive(false);
    }

    // =====================================================================
    // El arma en la mano del compañero
    // =====================================================================

    // Muestra la copia del arma que tiene en la mano el compañero que se mira (la cambia si cambió de arma).
    private void VerArmaDelCompanero()
    {
        // La granada se arma aparte: el jugador no tiene un modelo fijo de granada en primera persona para copiar.
        ShopItem granada = mirando.GranadaVisible;
        GameObject arma = granada == null && local != null ? local.PrimeraPersona(mirando.ArmaVisible) : null;
        if (arma != armaMirada || granada != granadaMirada)
        {
            armaMirada = arma;
            granadaMirada = granada;
            if (vistaArma != null) vistaArma.gameObject.SetActive(false);
            vistaArma = granada != null ? VistaDeGranada(granada) : arma != null ? VistaDe(arma) : null;
            if (vistaArma != null) vistaArma.gameObject.SetActive(true);
            sacudon = 0f;
        }
        if (camaraArmas != null) camaraArmas.enabled = vistaArma != null;
        if (vistaArma == null) return;

        sacudon = Mathf.MoveTowards(sacudon, 0f, SacudonVuelta * Time.deltaTime);
        vistaArma.localPosition = new Vector3(0f, 0f, -SacudonAtras * sacudon);
        vistaArma.localRotation = Quaternion.Euler(-SacudonArriba * sacudon, 0f, 0f);
    }

    // La copia (en primera persona) de un arma propia, con la mano si hay foto de los brazos con esa arma.
    private Transform VistaDe(GameObject arma)
    {
        if (vistas.TryGetValue(arma, out Transform vista) && vista != null) return vista;
        // También las partes que se ocultaron al morir (el arma que tenía en la mano).
        System.Func<Renderer, bool> visible = r => r.enabled || armasOcultas.Contains(r);
        GameObject copia = ModeloReal.CopiaEnPrimeraPersona(arma, camara.transform, visible, "Espectador: " + arma.name);
        BrazosEnCamara brazos = local.GetComponentInChildren<BrazosEnCamara>(true);
        MeshRenderer foto = brazos != null ? brazos.FotoDe(arma) : null;
        if (foto != null)
        {
            GameObject mano = ModeloReal.CopiaEnPrimeraPersona(foto.gameObject, camara.transform, r => true, "Brazos");
            mano.transform.SetParent(copia.transform, false);
        }

        int capa = camaraArmas != null ? CapaVisible(camaraArmas) : copia.layer;
        foreach (Transform parte in copia.GetComponentsInChildren<Transform>(true)) parte.gameObject.layer = capa;
        foreach (Renderer r in copia.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        copia.transform.SetParent(camara.transform, false);
        vistas[arma] = copia.transform;
        return copia.transform;
    }

    // La granada en la mano, como la ve el que la sostiene. Va dentro de una caja hija de la cámara, que es la que
    // se sacude.
    private Transform VistaDeGranada(ShopItem granada)
    {
        if (vistasGranada.TryGetValue(granada, out Transform vista) && vista != null)
        {
            PonerManoDeGranada(vista);
            return vista;
        }
        GrenadeThrower lanzador = local != null ? local.GetComponentInChildren<GrenadeThrower>(true) : null;
        GameObject modelo = lanzador != null ? lanzador.BuildPreview(granada) : null;
        if (modelo == null) return null;

        var caja = new GameObject("Espectador: " + granada.name).transform;
        caja.SetParent(camara.transform, false);
        modelo.transform.SetParent(caja, true); // queda donde la armó el lanzador
        int capa = camaraArmas != null ? CapaVisible(camaraArmas) : modelo.layer;
        foreach (Transform parte in caja.GetComponentsInChildren<Transform>(true)) parte.gameObject.layer = capa;
        foreach (Renderer r in caja.GetComponentsInChildren<Renderer>(true))
        {
            r.enabled = true;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        vistasGranada[granada] = caja;
        PonerManoDeGranada(caja);
        return caja;
    }

    // La mano que sostiene la granada: la foto de los brazos que BrazosEnCamara saca para la "Mano con granada".
    // Si todavía no la sacó, se intenta de nuevo la próxima vez que se muestre.
    private void PonerManoDeGranada(Transform caja)
    {
        const string nombre = "Brazos";
        if (caja.Find(nombre) != null || local == null) return;
        Transform mano = camara.transform.Find(GrenadeThrower.NombreMano);
        BrazosEnCamara brazos = local.GetComponentInChildren<BrazosEnCamara>(true);
        MeshRenderer foto = brazos != null && mano != null ? brazos.FotoDe(mano.gameObject) : null;
        if (foto == null) return;

        GameObject copia = ModeloReal.CopiaEnPrimeraPersona(foto.gameObject, camara.transform, r => true, nombre);
        if (copia == null) return;
        copia.transform.SetParent(caja, false);
        int capa = camaraArmas != null ? CapaVisible(camaraArmas) : copia.layer;
        foreach (Transform parte in copia.GetComponentsInChildren<Transform>(true)) parte.gameObject.layer = capa;
        foreach (Renderer r in copia.GetComponentsInChildren<Renderer>(true))
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // La capa que dibuja la cámara de las armas (ArmaEnMano).
    private static int CapaVisible(Camera camaraDeArmas)
    {
        int capa = LayerMask.NameToLayer("ArmaEnMano");
        if (capa >= 0 && (camaraDeArmas.cullingMask & (1 << capa)) != 0) return capa;
        for (int i = 0; i < 32; i++) if ((camaraDeArmas.cullingMask & (1 << i)) != 0) return i;
        return 0;
    }

    private void AlDispararElCompanero() => sacudon = 1f;

    // =====================================================================
    // A quién mirar
    // =====================================================================

    // Compañeros vivos, siempre en el mismo orden (por número de jugador).
    private List<JugadorEnRed> Companeros(JugadorEnRed local)
    {
        var lista = new List<JugadorEnRed>();
        foreach (JugadorEnRed j in partida.Jugadores)
            if (j != null && j != local && j.Vivo && EquiposTacticos.SonAliados(local.Actor, j.Actor)) lista.Add(j);
        lista.Sort((a, b) => a.Actor.CompareTo(b.Actor));
        return lista;
    }

    private JugadorEnRed Siguiente(JugadorEnRed local, JugadorEnRed actual, int paso)
    {
        List<JugadorEnRed> vivos = Companeros(local);
        if (vivos.Count == 0) return null;
        int i = actual != null ? vivos.IndexOf(actual) : -1;
        if (i < 0) return vivos[paso > 0 ? 0 : vivos.Count - 1];
        return vivos[(i + paso + vivos.Count) % vivos.Count];
    }

    private void Mirar(JugadorEnRed jugador)
    {
        if (jugador == mirando && (jugador != null || !muerto)) { MostrarCartel(); return; }

        // El cuerpo del que se mira no se dibuja (si no, la cámara queda adentro de su cabeza).
        foreach (Renderer r in ocultos) if (r != null) r.enabled = true;
        ocultos.Clear();
        if (mirando != null) mirando.Disparo -= AlDispararElCompanero;
        mirando = jugador;
        if (mirando != null) mirando.Disparo += AlDispararElCompanero;
        // Su arma se arma de nuevo en el próximo LateUpdate; sin nadie a quien mirar, no se ve ninguna.
        armaMirada = null;
        granadaMirada = null;
        if (vistaArma != null) vistaArma.gameObject.SetActive(false);
        vistaArma = null;
        if (camaraArmas != null && muerto) camaraArmas.enabled = false;
        // La vida, el minimapa y la plata del HUD pasan a ser los del compañero que se mira (o vuelven a los propios).
        CombatHud.Spectate(mirando);
        ShopUI.Spectated = mirando != null ? mirando.Dueno : null;
        if (mirando != null)
            foreach (Renderer r in mirando.GetComponentsInChildren<Renderer>(true))
                if (r.enabled) { r.enabled = false; ocultos.Add(r); }

        if (muerto && partida != null) partida.Aviso(null); // el "Te eliminaron" ya no hace falta
        MostrarCartel();
    }

    // =====================================================================
    // Cartel (CA2 y CA5)
    // =====================================================================

    private void MostrarCartel()
    {
        if (!muerto) { if (cartel != null) cartel.SetActive(false); return; }
        if (cartel == null) ArmarCartel();
        cartel.SetActive(true);
        if (mirando != null)
        {
            cartelTitulo.text = $"Mirando a <color=#58A6FF>{mirando.Nombre}</color>";
            cartelAyuda.text = "Clic izq.  Siguiente     Clic der.  Anterior";
        }
        else
        {
            cartelTitulo.text = viendoDispositivo ? "Mirando el dispositivo" : "No quedan compañeros vivos";
            cartelAyuda.text = "Reaparecés en la ronda siguiente";
        }
    }

    private void ArmarCartel()
    {
        MatchHud hud = MatchHud.Instance;
        TMP_FontAsset display = hud != null ? hud.DisplayFont : null, label = hud != null ? hud.LabelFont : null;
        Sprite rounded = hud != null ? hud.Rounded : null;

        cartel = new GameObject("Espectador", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        cartel.transform.SetParent(transform, false);
        var canvas = cartel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        var escala = cartel.GetComponent<UnityEngine.UI.CanvasScaler>();
        escala.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;

        RectTransform caja = Node("Caja", cartel.transform);
        caja.anchorMin = caja.anchorMax = caja.pivot = new Vector2(0.5f, 0f);
        caja.anchoredPosition = new Vector2(0f, 190f);
        caja.sizeDelta = new Vector2(620f, 84f);
        Image(caja, rounded, Rgb(10, 12, 17, DarkAlpha(0.85f)), 8f);
        Image(Place(Node("Linea", caja), 0f, 0f, 620f, 3f), null, MatchHud.TeamColor);
        Text(Place(Node("Antetitulo", caja), 0f, 10f, 620f, 16f), label, 13f, Mute, TMPro.TextAlignmentOptions.Center, 14f, true).text = "Espectando";
        cartelTitulo = Text(Place(Node("Nombre", caja), 0f, 26f, 620f, 32f), display, 30f, Ink, TMPro.TextAlignmentOptions.Center, 3f, true);
        cartelAyuda = Text(Place(Node("Ayuda", caja), 0f, 58f, 620f, 20f), label, 15f, Mute, TMPro.TextAlignmentOptions.Center, 8f, true);
    }
}
