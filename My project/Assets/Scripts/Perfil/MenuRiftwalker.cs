using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using static ShopUIKit;
using static PerfilUI;
using Img = UnityEngine.UI.Image;

// Menú principal del juego: JUGAR, la lista (Opciones, Logros, Créditos, Salir), la credencial del jugador con el
// Perfil, la Estación del Rift (cápsulas), la tienda de cristales, el formulario de opinión y "Volver a la partida".
// El Perfil, las monedas, la colección, las estadísticas y los logros todavía son datos fijos (PerfilDatos y
// PantallaLogros): falta guardarlos de verdad.
//
// Va en un objeto de la escena MenuPrincipal y arma dos lienzos por código:
// - MenuRiftwalkerFondo, debajo del Canvas de la escena: las imágenes de fondo. Así Opciones, Créditos y la selección
//   de modo (los paneles de MenuUIController, que se siguen usando) se ven sobre el mismo fondo.
// - MenuRiftwalkerCanvas, encima: el menú, sus pantallas y las ventanas emergentes.
// PanelMenuPrincipal queda vacío: es el estado "en el menú" de MenuUIController (y lo miran la nota para el PO y el
// nombre del jugador), pero sus botones viejos y el fondo viejo de la escena se apagan al arrancar.
public class MenuRiftwalker : MonoBehaviour
{
    [Header("Tipografías (las mismas de la tienda)")]
    [SerializeField] private TMP_FontAsset displayFont; // Barlow Condensed Bold
    [SerializeField] private TMP_FontAsset labelFont;   // Barlow Condensed SemiBold
    [SerializeField] private TMP_FontAsset bodyFont;    // Barlow Regular
    [SerializeField] private TMP_FontAsset monoFont;    // JetBrains FMono

    [Header("Sprites")]
    [SerializeField] private Sprite rounded;
    [Tooltip("Bordes de 1 px para los radios 6 y 8, en ese orden.")]
    [SerializeField] private Sprite[] borders = new Sprite[2];
    [SerializeField] private Sprite icono;

    [Header("Sonido al pasar el mouse por un botón (Kenney, CC0)")]
    [SerializeField] private AudioClip sonidoPasar;
    [Tooltip("Grupo Interfaz del Audio Mixer: así respeta el volumen de Interfaz de las opciones.")]
    [SerializeField] private UnityEngine.Audio.AudioMixerGroup grupoInterfaz;

    private const float Fundido = 0.22f, AnchoTarjeta = 580f;
    private PantallaSinSesion sinSesion;
    private RectTransform rootSinSesion;
    public const float Afuera = 2000f; // cuánto siguen los fondos de las pantallas fuera del 16:9 (pantallas más anchas o más altas)
    private const float DuracionFondo = 9f, FundidoFondo = 2.2f; // cada imagen de fondo y el cruce entre dos

    public PantallaPerfil Perfil { get; private set; }
    public PantallaEstacion Estacion { get; private set; }
    public PantallaTienda Tienda { get; private set; }
    public PantallaLogros Logros { get; private set; }

    private PantallaBase actual, saliendo;
    private RectTransform canvasRoot, root, rootFondo, capaMenu, capaPantallas, capaModal, panelModal;
    private CanvasGroup grupoMenu, grupoModal;
    private GameObject panelPrincipal;
    private CanvasGroup overlay;
    private MenuUIController menuUI;
    private Aviso aviso;
    private VentanaFeedback feedback;
    private bool modalAbierto;
    private Action alCerrarModal;

    // Menú principal
    private Img fondoA, fondoB, fondoFrente;
    private Sprite[] fondos;
    private int fondoActual;
    private float inicioA, inicioB, proximoFondo, mezcla = 1f;
    private Img avatarMenu, marcoAvatar;
    private TextMeshProUGUI nombreMenu, tituloMenu, nuevosMenu, subEstacion;
    private RectTransform chipNuevos;
    private Monedas monedasMenu;

    // Volver a la partida (US 195, CA2)
    private const float DuracionErrorVolver = 6f;
    private RectTransform tarjetaVolver;
    private TextMeshProUGUI rotuloVolver, salaVolver, detalleVolver;
    private Boton botonVolver;
    private bool volviendo, escuchandoRed;
    private string errorVolver;
    private float errorVolverHasta, proximoVolver;

    // =====================================================================
    // Ciclo de vida
    // =====================================================================

    private void Awake()
    {
        FTitulo = displayFont; FRotulo = labelFont; FTexto = bodyFont; FMono = monoFont;
        Rounded = rounded; Borders = borders; Icono = icono;
        SonidoInterfaz.Configurar(sonidoPasar, grupoInterfaz);
        PlayerNameScreen.ChipOculto = true; // la credencial nueva muestra el nombre y abre el Perfil
        Armar();
        ApagarMenuViejo();
    }

    private void Start()
    {
        menuUI = FindAnyObjectByType<MenuUIController>();
        GameObject o = BuscarEnEscena("OverlayTransicion");
        if (o != null) overlay = o.GetComponent<CanvasGroup>();
        PerfilDatos.Cambio += RefrescarTodo;
        EstadisticasJugador.Cambio += RefrescarTodo; // F24: lo que trae la nube
        PlayerProfile.NameChanged += NombreCambiado;
        Guardado.CambioDeJugador += RefrescarTodo; // se entró o se salió de una cuenta
        RefrescarTodo();
        // US 163, CA3 y CA4: si el guardado se recuperó del respaldo, estaba dañado o es de una versión más nueva.
        if (Guardado.Aviso != null) Avisar(Guardado.Aviso);
    }

    private void OnDestroy()
    {
        PerfilDatos.Cambio -= RefrescarTodo;
        EstadisticasJugador.Cambio -= RefrescarTodo;
        PlayerProfile.NameChanged -= NombreCambiado;
        Guardado.CambioDeJugador -= RefrescarTodo;
        PlayerNameScreen.ChipOculto = false;
        if (escuchandoRed && Multijugador.Existe) Multijugador.Instancia.Error -= AlFallarVolver;
    }

    private void NombreCambiado(string _) => RefrescarTodo();

    // Del menú viejo queda PanelMenuPrincipal, vacío y transparente (ver el comentario de la clase). Se apaga en Awake,
    // antes del primer cuadro, así sus botones no llegan a verse. También se apaga el fondo viejo de la escena: el
    // nuevo está en el lienzo de abajo.
    private void ApagarMenuViejo()
    {
        panelPrincipal = BuscarEnEscena("PanelMenuPrincipal");
        if (panelPrincipal != null)
        {
            foreach (UnityEngine.UI.Graphic g in panelPrincipal.GetComponents<UnityEngine.UI.Graphic>()) g.enabled = false;
            foreach (Transform hijo in panelPrincipal.transform) hijo.gameObject.SetActive(false);
        }
        GameObject imagen = BuscarEnEscena("ImagFondo");
        if (imagen == null) return;
        imagen.SetActive(false);
        foreach (UnityEngine.UI.Graphic g in imagen.transform.parent.GetComponents<UnityEngine.UI.Graphic>()) g.enabled = false;
    }

    private void Update()
    {
        Encajar();
        AnimarFondos(); // se ven en el menú y detrás de Opciones (y de los otros paneles transparentes)
        if (BloqueadoSinSesion()) return;
        bool enMenu = panelPrincipal != null && panelPrincipal.activeInHierarchy && actual == null;
        if (capaMenu.gameObject.activeSelf != enMenu) capaMenu.gameObject.SetActive(enMenu);
        if (enMenu)
        {
            grupoMenu.alpha = overlay != null ? 1f - overlay.alpha : 1f;
            grupoMenu.blocksRaycasts = overlay == null || overlay.alpha < 0.5f;
            ActualizarVolver();
        }

        AnimarPantallas();
        actual?.Tick();
        aviso.Actualizar();
        grupoModal.alpha = Mathf.MoveTowards(grupoModal.alpha, modalAbierto ? 1f : 0f, Time.unscaledDeltaTime / Fundido);
        if (!modalAbierto && grupoModal.alpha <= 0f && capaModal.gameObject.activeSelf) capaModal.gameObject.SetActive(false);

        Keyboard teclado = Keyboard.current;
        if (teclado != null && teclado.f9Key.wasPressedThisFrame) SumarDebug();
        if (teclado != null && teclado.escapeKey.wasPressedThisFrame && !PlayerNameScreen.IsOpen && !NotaPO.IsOpen)
        {
            if (modalAbierto) CerrarModal();
            else if (actual != null && !actual.Escape()) VolverAlMenu();
        }
    }

    private void Encajar()
    {
        Vector2 size = canvasRoot.rect.size;
        if (size.x <= 0f || size.y <= 0f) return;
        root.localScale = Vector3.one * Mathf.Min(size.x / 1920f, size.y / 1080f);
        if (rootSinSesion != null) rootSinSesion.localScale = root.localScale;
        rootFondo.localScale = Vector3.one * Mathf.Max(size.x / 1920f, size.y / 1080f); // el fondo cubre toda la pantalla
    }

    // US 282, CA3: sin una sesión de cuenta, un build publicado no deja jugar: la pantalla tapa todo (también el nombre
    // de jugador y la nota para el PO) hasta entrar en el launcher.
    private bool BloqueadoSinSesion()
    {
        LogrosOnline online = LogrosOnline.Instancia;
        bool bloqueado = online != null && online.SinSesion;
        if (bloqueado && sinSesion == null)
        {
            rootSinSesion = Lienzo("MenuRiftwalkerSinSesion", 120, true, out RectTransform lienzo);
            sinSesion = new PantallaSinSesion(lienzo, rootSinSesion);
            Encajar();
        }
        sinSesion?.Mostrar(bloqueado, online != null && online.SesionCerrada);
        return bloqueado;
    }

    private static GameObject BuscarEnEscena(string nombre)
    {
        foreach (GameObject r in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (r.name == nombre) return r;
            foreach (Transform t in r.GetComponentsInChildren<Transform>(true))
                if (t.name == nombre) return t.gameObject;
        }
        return null;
    }

    // =====================================================================
    // Navegación
    // =====================================================================

    public void Abrir(PantallaBase pantalla)
    {
        if (pantalla == null || pantalla == actual) return;
        if (actual != null) actual.rect.gameObject.SetActive(false);
        saliendo = null;
        actual = pantalla;
        actual.rect.gameObject.SetActive(true);
        actual.rect.SetAsLastSibling();
        actual.grupo.alpha = 0f;
        actual.Refrescar();
    }

    public void VolverAlMenu()
    {
        if (actual == null) return;
        if (panelPrincipal != null) panelPrincipal.SetActive(true);
        saliendo = actual;
        actual = null;
        RefrescarTodo();
    }

    private void AnimarPantallas()
    {
        if (actual != null && actual.grupo.alpha < 1f)
        {
            actual.grupo.alpha = Mathf.MoveTowards(actual.grupo.alpha, 1f, Time.unscaledDeltaTime / Fundido);
            // Ya tapada la pantalla, se apaga el menú de abajo (con la credencial del nombre y la nota para el PO).
            if (actual.grupo.alpha >= 1f && panelPrincipal != null) panelPrincipal.SetActive(false);
        }
        if (saliendo != null)
        {
            saliendo.grupo.alpha = Mathf.MoveTowards(saliendo.grupo.alpha, 0f, Time.unscaledDeltaTime / Fundido);
            if (saliendo.grupo.alpha <= 0f) { saliendo.rect.gameObject.SetActive(false); saliendo = null; }
        }
    }

    public void Avisar(string mensaje) => aviso.Mostrar(mensaje);

    /// <summary>DEBUG (F9, en el menú y en sus pantallas): suma de las tres monedas. No tiene botón a la vista.</summary>
    public void SumarDebug()
    {
        PerfilDatos.SumarDebug();
        Avisar($"Debug: +{PerfilDatos.Numero(PerfilDatos.DebugCreditos)} créditos, +{PerfilDatos.DebugBoletos} boletos y +{PerfilDatos.DebugCristales} cristales");
    }

    private void RefrescarTodo()
    {
        RefrescarMenu();
        actual?.Refrescar();
    }

    // =====================================================================
    // Ventanas emergentes
    // =====================================================================

    /// <summary>Abre una ventana centrada y devuelve el panel vacío para llenarlo.</summary>
    public RectTransform AbrirModal(float w, float h, Action alCerrar = null)
    {
        BorrarHijos(panelModal);
        panelModal.sizeDelta = new Vector2(w, h);
        panelModal.anchoredPosition = new Vector2((1920f - w) / 2f, -(1080f - h) / 2f);
        capaModal.gameObject.SetActive(true);
        capaModal.SetAsLastSibling();
        modalAbierto = true;
        alCerrarModal = alCerrar;
        Image(Stretch(Node("Fondo", panelModal)), Rounded, BarColor, 10f, true);
        Borde(Stretch(Node("Marco", panelModal)), 8f, White(0.12f));
        return panelModal;
    }

    public void CerrarModal()
    {
        if (!modalAbierto) return;
        modalAbierto = false;
        Action a = alCerrarModal;
        alCerrarModal = null;
        a?.Invoke();
    }

    /// <summary>Ventana de confirmación con dos botones.</summary>
    public void Confirmar(string rotulo, string titulo, string cuerpo, string aceptar, Action alAceptar)
    {
        RectTransform p = AbrirModal(720f, 400f);
        T(p, "Rotulo", 48f, 40f, 620f, 22f, FRotulo, 18f, Accent, rotulo, TextAlignmentOptions.MidlineLeft, 16f, true);
        T(p, "Titulo", 46f, 66f, 620f, 56f, FTitulo, 46f, Ink, titulo, TextAlignmentOptions.MidlineLeft, 3f, true);
        TextMeshProUGUI c = T(p, "Cuerpo", 48f, 136f, 624f, 150f, FTexto, 23f, Soft, cuerpo, TextAlignmentOptions.TopLeft);
        c.textWrappingMode = TextWrappingModes.Normal;
        CrearBoton(p, 720f - 48f - 290f - 16f - 190f, 400f - 48f - 64f, 190f, 64f, "Cancelar", false, CerrarModal, 26f);
        CrearBoton(p, 720f - 48f - 290f, 400f - 48f - 64f, 290f, 64f, aceptar, true, () => { CerrarModal(); alAceptar?.Invoke(); }, 26f);
    }

    // "Salir" pregunta con una ventana de este menú: el panel PanelDeseaSalir de la escena queda debajo de este lienzo.
    private void ConfirmarSalir() =>
        Confirmar("Salir", "¿Salir del juego?", "Se cierra Project Riftwalker.", "Salir", () => menuUI?.ConfirmarSalida());

    // =====================================================================
    // Armado
    // =====================================================================

    private void Armar()
    {
        // Debajo del Canvas de la escena (orden 0), con los paneles de MenuUIController.
        rootFondo = Lienzo("MenuRiftwalkerFondo", -10, false, out _);
        ArmarFondo();
        // Arriba del Canvas de la escena; abajo del nombre de jugador (100) y la nota para el PO (110).
        root = Lienzo("MenuRiftwalkerCanvas", 95, true, out canvasRoot);

        capaMenu = Place(Node("MenuPrincipal", root), 0f, 0f, 1920f, 1080f);
        grupoMenu = capaMenu.gameObject.AddComponent<CanvasGroup>();
        ArmarMenu();

        capaPantallas = Place(Node("Pantallas", root), 0f, 0f, 1920f, 1080f);
        Perfil = new PantallaPerfil(this, capaPantallas);
        Estacion = new PantallaEstacion(this, capaPantallas);
        Tienda = new PantallaTienda(this, capaPantallas);
        Logros = new PantallaLogros(this, capaPantallas);
        foreach (PantallaBase p in new PantallaBase[] { Perfil, Estacion, Tienda, Logros }) p.rect.gameObject.SetActive(false);

        capaModal = Place(Node("Ventana", root), 0f, 0f, 1920f, 1080f);
        grupoModal = capaModal.gameObject.AddComponent<CanvasGroup>();
        Image(Place(Node("Oscurecido", capaModal), -Afuera, -Afuera, 1920f + 2f * Afuera, 1080f + 2f * Afuera), null,
            new Color(0f, 0f, 0f, DarkAlpha(0.72f)), 0f, true);
        panelModal = Place(Node("Panel", capaModal), 0f, 0f, 600f, 400f);
        capaModal.gameObject.SetActive(false);
        grupoModal.alpha = 0f;

        feedback = new VentanaFeedback(this);
        aviso = new Aviso(root);
    }

    /// <summary>Lienzo de pantalla completa con su raíz de 1920 x 1080 al centro (Encajar le da la escala).</summary>
    private RectTransform Lienzo(string nombre, int orden, bool clics, out RectTransform lienzo)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        if (clics) go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        go.layer = 5;
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = orden;
        var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        lienzo = (RectTransform)go.transform;

        RectTransform contenido = Node("Contenido", lienzo);
        contenido.anchorMin = contenido.anchorMax = contenido.pivot = new Vector2(0.5f, 0.5f);
        contenido.sizeDelta = new Vector2(1920f, 1080f);
        return contenido;
    }

    // Las imágenes de Resources/Perfil/Fondos, por orden de nombre. Cubren toda la pantalla (los personajes están a la
    // derecha y el lado izquierdo queda oscuro para el menú). Cambian cada tantos segundos con un fundido lento, y cada
    // una se acerca y se corre un poco mientras se ve.
    private void ArmarFondo()
    {
        fondos = Resources.LoadAll<Sprite>("Perfil/Fondos");
        Array.Sort(fondos, (a, b) => string.CompareOrdinal(a.name, b.name));
        if (fondos.Length == 0) return;
        fondoA = Cubrir(rootFondo, fondos[0]);
        fondoB = Cubrir(rootFondo, fondos[0]);
        fondoB.color = new Color(1f, 1f, 1f, 0f);
        fondoFrente = fondoA;
        inicioA = Time.unscaledTime;
        proximoFondo = Time.unscaledTime + DuracionFondo;
    }

    private void ArmarMenu()
    {
        // Sombras para leer el menú: a la izquierda (JUGAR y la lista), abajo (las tarjetas) y arriba (las monedas).
        // Siguen afuera del 16:9 (pantallas más anchas o más altas), como el fondo.
        Color negro = Color.black;
        Image(Place(Node("SombraIzquierda", capaMenu), 0f, -Afuera, 1150f, 1080f + 2f * Afuera), Degrade(true, 0.9f, 0f), Color.white);
        Image(Place(Node("SombraIzquierdaAfuera", capaMenu), -Afuera, -Afuera, Afuera, 1080f + 2f * Afuera), null, WithAlpha(negro, 0.9f));
        Image(Place(Node("SombraAbajo", capaMenu), -Afuera, 690f, 1920f + 2f * Afuera, 390f), Degrade(false, 0.88f, 0f), Color.white);
        Image(Place(Node("SombraAbajoAfuera", capaMenu), -Afuera, 1080f, 1920f + 2f * Afuera, Afuera), null, WithAlpha(negro, 0.88f));
        Image(Place(Node("SombraArriba", capaMenu), -Afuera, 0f, 1920f + 2f * Afuera, 190f), Degrade(false, 0f, 0.65f), Color.white);
        Image(Place(Node("SombraArribaAfuera", capaMenu), -Afuera, -Afuera, 1920f + 2f * Afuera, Afuera), null, WithAlpha(negro, 0.65f));


        // ----- Marca -----
        if (Icono != null) Image(Place(Node("Icono", capaMenu), 60f, 32f, 68f, 68f), Icono, Color.white);
        T(capaMenu, "Project", 142f, 38f, 300f, 20f, FRotulo, 17f, Accent, "Project", TextAlignmentOptions.MidlineLeft, 18f, true);
        T(capaMenu, "Riftwalker", 140f, 56f, 400f, 44f, FTitulo, 44f, Ink, "Riftwalker", TextAlignmentOptions.MidlineLeft, 4f, true);

        // ----- Opinión -----
        RectTransform opinion = Place(Node("Opinion", capaMenu), 470f, 44f, 230f, 48f);
        Img fondoOpinion = Image(opinion, Rounded, PanelColor, 8f, true);
        Img bordeOpinion = Borde(opinion, 8f, White(0.1f));
        Image(Place(Node("Globo", opinion), 16f, 13f, 26f, 18f), Rounded, Accent, 4f);
        Poligono(opinion, "Cola", Accent, new Vector2(21f, 30f), new Vector2(29f, 30f), new Vector2(21f, 37f));
        T(opinion, "Texto", 54f, 0f, 170f, 48f, FRotulo, 19f, Soft, "Danos tu opinión", TextAlignmentOptions.MidlineLeft, 6f, true);
        var po = Clickeable(opinion, () => feedback.Abrir());
        po.Hovered = () => bordeOpinion.color = Accent;
        po.Exited = () => bordeOpinion.color = White(0.1f);

        // ----- Monedas y credencial -----
        monedasMenu = new Monedas(capaMenu, 1478f, 44f, () => Abrir(Estacion), () => Abrir(Tienda));
        RectTransform cred = Place(Node("Credencial", capaMenu), 1490f, 30f, 370f, 84f);
        Image(cred, Rounded, PanelColor, 8f, true);
        Img bordeCred = Borde(cred, 8f, White(0.1f));
        marcoAvatar = Image(Place(Node("Marco", cred), 10f, 10f, 64f, 64f), Rounded, Ink, 6f);
        RectTransform caja = Place(Node("Avatar", cred), 12f, 12f, 60f, 60f);
        avatarMenu = Cubrir(caja, null);
        nombreMenu = T(cred, "Nombre", 88f, 6f, 236f, 44f, FTitulo, 30f, Ink, "", TextAlignmentOptions.MidlineLeft, 3f, true);
        nombreMenu.overflowMode = TextOverflowModes.Ellipsis;
        tituloMenu = T(cred, "Titulo", 88f, 46f, 236f, 24f, FRotulo, 18f, Accent, "", TextAlignmentOptions.MidlineLeft, 4f);
        tituloMenu.overflowMode = TextOverflowModes.Ellipsis;
        T(cred, "Ir", 330f, 22f, 28f, 40f, FTitulo, 34f, Mute, "»", TextAlignmentOptions.Center);
        chipNuevos = Place(Node("Nuevos", cred), 268f, -12f, 96f, 26f);
        Image(chipNuevos, Rounded, Accent, 6f);
        nuevosMenu = T(chipNuevos, "Texto", 0f, 0f, 96f, 26f, FRotulo, 15f, AccentInk, "", TextAlignmentOptions.Center, 10f, true);
        var pc = Clickeable(cred, () => Abrir(Perfil));
        pc.Hovered = () => bordeCred.color = Accent;
        pc.Exited = () => bordeCred.color = White(0.1f);

        // ----- Columna de la izquierda -----
        T(capaMenu, "Anden", 120f, 250f, 700f, 24f, FRotulo, 20f, Accent, "Andén 01 · Punto de encuentro", TextAlignmentOptions.MidlineLeft, 16f, true);
        RectTransform jugar = Place(Node("Jugar", capaMenu), 112f, 270f, 560f, 168f);
        TextMeshProUGUI textoJugar = T(jugar, "Texto", 0f, 0f, 460f, 168f, FTitulo, 176f, Ink, "Jugar", TextAlignmentOptions.MidlineLeft, 2f, true);
        float anchoJugar = Width(textoJugar, "JUGAR");
        UIPolygon flecha = Poligono(jugar, "Flecha", Accent,
            new Vector2(anchoJugar + 26f, 64f), new Vector2(anchoJugar + 76f, 64f), new Vector2(anchoJugar + 76f, 114f),
            new Vector2(anchoJugar + 64f, 114f), new Vector2(anchoJugar + 64f, 84f), new Vector2(anchoJugar + 34f, 114f),
            new Vector2(anchoJugar + 26f, 106f), new Vector2(anchoJugar + 56f, 76f), new Vector2(anchoJugar + 26f, 76f));
        var pj = Clickeable(jugar, () => menuUI?.MostrarSeleccionModo());
        pj.Hovered = () => { textoJugar.color = Accent; flecha.color = Ink; };
        pj.Exited = () => { textoJugar.color = Ink; flecha.color = Accent; };
        jugar.gameObject.AddComponent<ButtonHoverAnimation>();
        T(capaMenu, "Modos", 122f, 440f, 600f, 30f, FTexto, 25f, Mute, "Táctico  ·  Deathmatch  ·  Zombie");
        Image(Place(Node("Linea", capaMenu), 122f, 486f, 440f, 3f), Degrade(true, 0f, 1f), Accent);

        string[] opciones = { "Opciones", "Logros", "Créditos", "Salir" };
        Action[] acciones =
        {
            () => menuUI?.MostrarOpciones(), () => Abrir(Logros), () => menuUI?.MostrarCreditos(), ConfirmarSalir,
        };
        for (int i = 0; i < opciones.Length; i++)
        {
            RectTransform fila = Place(Node(opciones[i], capaMenu), 120f, 508f + i * 50f, 340f, 46f);
            Img marca = Image(Place(Node("Marca", fila), 0f, 12f, 4f, 22f), null, Accent);
            marca.color = WithAlpha(Accent, 0f);
            TextMeshProUGUI t = T(fila, "Texto", 0f, 0f, 340f, 46f, FTitulo, 36f, Soft, opciones[i], TextAlignmentOptions.MidlineLeft, 6f, true);
            var p = Clickeable(fila, acciones[i]);
            p.Hovered = () => { t.color = Accent; marca.color = Accent; t.rectTransform.anchoredPosition = new Vector2(16f, 0f); };
            p.Exited = () => { t.color = Soft; marca.color = WithAlpha(Accent, 0f); t.rectTransform.anchoredPosition = Vector2.zero; };
        }
        // US 122: el campo de práctica (Shooting Range, US 118) se abre desde acá. Reemplaza a las escenas de prueba
        // viejas (Pruebas y Pruebas 2).
        T(capaMenu, "CamposRotulo", 122f, 718f, 200f, 24f, FRotulo, 17f, Mute, "Campos de prueba:", TextAlignmentOptions.MidlineLeft, 12f, true);
        {
            TextMeshProUGUI t = T(capaMenu, "Campo de práctica", 300f, 718f, 240f, 24f, FRotulo, 17f, Soft, "Campo de práctica", TextAlignmentOptions.MidlineLeft, 12f, true);
            t.raycastTarget = true;
            var p = t.gameObject.AddComponent<ShopPointerTarget>();
            p.Clicked = () => menuUI?.IrAlCampoDePractica();
            p.Hovered = () => t.color = Accent;
            p.Exited = () => t.color = Soft;
        }

        // ----- Tarjetas de abajo -----
        // El Perfil se abre desde la credencial de arriba a la derecha y la Tienda desde el + de los cristales.
        subEstacion = Tarjeta(80f, "01 / Cápsulas", "Estación del Rift", () => Abrir(Estacion), out RectTransform tEstacion);
        DibujarCapsula(tEstacion, 64f, 66f, 0.34f);
        ArmarVolver(80f + AnchoTarjeta + 24f);

        // Versión del juego abajo a la derecha, chiquita y en gris (US 204, CA1). Va debajo de la tarjeta "Nota para el PO".
        T(capaMenu, "Version", 1920f - 28f - 300f, 1080f - 12f - 24f, 300f, 24f, FTexto, 16f, Mute, "v" + Application.version,
            TextAlignmentOptions.BottomRight);
    }

    private TextMeshProUGUI Tarjeta(float x, string rotulo, string titulo, Action alClic, out RectTransform tarjeta)
    {
        tarjeta = Place(Node("Tarjeta " + titulo, capaMenu), x, 900f, AnchoTarjeta, 132f);
        Image(tarjeta, Rounded, PanelColor, 8f, true);
        Img borde = Borde(tarjeta, 8f, White(0.1f));
        Image(Place(Node("Linea", tarjeta), 0f, 0f, AnchoTarjeta, 3f), null, WithAlpha(Accent, 0.8f));
        T(tarjeta, "Rotulo", 128f, 18f, 420f, 20f, FRotulo, 16f, Accent, rotulo, TextAlignmentOptions.MidlineLeft, 12f, true);
        T(tarjeta, "Titulo", 126f, 38f, 430f, 46f, FTitulo, 42f, Ink, titulo, TextAlignmentOptions.MidlineLeft, 3f, true);
        TextMeshProUGUI sub = T(tarjeta, "Detalle", 128f, 86f, 420f, 26f, FTexto, 19f, Mute, "");
        var p = Clickeable(tarjeta, alClic);
        p.Hovered = () => borde.color = Accent;
        p.Exited = () => borde.color = White(0.1f);
        tarjeta.gameObject.AddComponent<ButtonHoverAnimation>();
        // ButtonHoverAnimation escala desde el pivote: al centro.
        tarjeta.pivot = new Vector2(0.5f, 0.5f);
        tarjeta.anchoredPosition += new Vector2(AnchoTarjeta / 2f, -66f);
        return sub;
    }

    // =====================================================================
    // Volver a la partida (US 195, CA2)
    // =====================================================================

    // Tarjeta al lado de la Estación del Rift. Aparece solo si hay una partida online a la que todavía se puede volver:
    // se cortó la conexión o se cerró el juego hace menos de 2 minutos (lo anota Reconexion). Muestra la sala, el tiempo
    // que queda y "Volver", que entra a esa partida en el mismo equipo. Si no se puede, lo explica ahí mismo un rato.
    private void ArmarVolver(float x)
    {
        tarjetaVolver = Place(Node("Tarjeta Volver a la partida", capaMenu), x, 900f, AnchoTarjeta, 132f);
        Image(tarjetaVolver, Rounded, PanelColor, 8f, true);
        Borde(tarjetaVolver, 8f, WithAlpha(Accent, 0.6f));
        Image(Place(Node("Linea", tarjetaVolver), 0f, 0f, AnchoTarjeta, 3f), null, WithAlpha(Accent, 0.8f));
        Circulo(tarjetaVolver, 64f, 66f, 34f, Accent);
        T(tarjetaVolver, "Flecha", 30f, 30f, 68f, 68f, FTitulo, 46f, AccentInk, "«", TextAlignmentOptions.Center);
        rotuloVolver = T(tarjetaVolver, "Rotulo", 128f, 18f, 420f, 20f, FRotulo, 16f, Accent, "", TextAlignmentOptions.MidlineLeft, 12f, true);
        salaVolver = T(tarjetaVolver, "Sala", 126f, 38f, 260f, 46f, FTitulo, 42f, Ink, "", TextAlignmentOptions.MidlineLeft, 3f, true);
        salaVolver.enableAutoSizing = true; // un error largo se achica para entrar
        salaVolver.fontSizeMin = 20f;
        salaVolver.fontSizeMax = 42f;
        detalleVolver = T(tarjetaVolver, "Detalle", 128f, 86f, 420f, 26f, FTexto, 19f, Mute, "");
        botonVolver = CrearBoton(tarjetaVolver, AnchoTarjeta - 28f - 150f, 34f, 150f, 64f, "Volver", true, VolverALaPartida, 28f);
        tarjetaVolver.gameObject.SetActive(false);
    }

    private void ActualizarVolver()
    {
        if (Time.unscaledTime < proximoVolver) return;
        proximoVolver = Time.unscaledTime + 0.2f;
        bool hay = Reconexion.HayPartida(out Reconexion.Partida partida);
        bool conError = errorVolver != null && Time.unscaledTime < errorVolverHasta;
        if (!conError) errorVolver = null;

        bool visible = hay || volviendo || conError;
        if (tarjetaVolver.gameObject.activeSelf != visible) tarjetaVolver.gameObject.SetActive(visible);
        if (!visible) return;
        bool boton = hay && !volviendo;
        botonVolver.rect.gameObject.SetActive(boton);
        salaVolver.rectTransform.sizeDelta = new Vector2(boton ? 260f : 420f, 46f);

        if (conError)
        {
            rotuloVolver.text = "No se pudo volver";
            rotuloVolver.color = Bad;
            salaVolver.text = errorVolver;
            salaVolver.color = Bad;
            detalleVolver.text = "";
            return;
        }
        rotuloVolver.text = "Partida en curso";
        rotuloVolver.color = Accent;
        salaVolver.color = Ink;
        if (volviendo)
        {
            salaVolver.text = "Volviendo…";
            detalleVolver.text = "";
            return;
        }
        int s = Mathf.CeilToInt(partida.restante);
        salaVolver.text = "Sala " + partida.sala;
        detalleVolver.text = $"Podés volver durante {s / 60}:{s % 60:00}";
    }

    private void VolverALaPartida()
    {
        if (volviendo) return;
        volviendo = true;
        errorVolver = null;
        Multijugador red = Multijugador.Instancia;
        if (!escuchandoRed) { red.Error += AlFallarVolver; escuchandoRed = true; }
        red.VolverALaPartida();
        proximoVolver = 0f;
    }

    private void AlFallarVolver(string mensaje)
    {
        if (!volviendo) return;
        volviendo = false;
        errorVolver = mensaje;
        errorVolverHasta = Time.unscaledTime + DuracionErrorVolver;
        proximoVolver = 0f;
    }

    /// <summary>Cápsula chica (para la tarjeta del menú); la grande de la estación se arma allá.</summary>
    public static void DibujarCapsula(RectTransform parent, float cx, float cy, float s)
    {
        float w = 200f * s, h = 300f * s;
        Image(Place(Node("Capsula", parent), cx - w / 2f, cy - h / 2f, w, h), Rounded, Hex(0x323A4A), 8f);
        Image(Place(Node("Ventana", parent), cx - w * 0.3f, cy - h * 0.28f, w * 0.6f, h * 0.56f), Rounded, Hex(0x0E1117), 6f);
        Image(Place(Node("Tapa", parent), cx - w / 2f, cy - h / 2f, w, h * 0.16f), Rounded, Accent, 6f);
        Image(Place(Node("Base", parent), cx - w / 2f, cy + h / 2f - h * 0.16f, w, h * 0.16f), Rounded, Accent, 6f);
        Image(Place(Node("Luz", parent), cx - w * 0.09f, cy - h * 0.22f, w * 0.18f, h * 0.44f), Rounded, Hot, 3f);
    }

    // =====================================================================
    // Menú: datos y animación
    // =====================================================================

    private void RefrescarMenu()
    {
        if (nombreMenu == null) return;
        PerfilDatos.Cosmetico av = PerfilDatos.Avatar, ti = PerfilDatos.Titulo;
        avatarMenu.sprite = PerfilDatos.Imagen(av);
        AjustarCubrir(avatarMenu, (RectTransform)avatarMenu.rectTransform.parent);
        marcoAvatar.color = Ink; // como en el Perfil: la rareza no cambia el color
        nombreMenu.text = PlayerProfile.HasName ? PlayerProfile.Name : "Jugador";
        tituloMenu.text = string.IsNullOrEmpty(ti.id) ? "Sin título" : ti.nombre;
        tituloMenu.color = string.IsNullOrEmpty(ti.id) ? Mute : Ink;
        int nuevos = PerfilDatos.CantidadNuevos(PerfilDatos.Tipo.Avatar) + PerfilDatos.CantidadNuevos(PerfilDatos.Tipo.Banner) +
                     PerfilDatos.CantidadNuevos(PerfilDatos.Tipo.Titulo) + PerfilDatos.CantidadNuevos(PerfilDatos.Tipo.Marco);
        chipNuevos.gameObject.SetActive(nuevos > 0);
        nuevosMenu.text = nuevos == 1 ? "1 nuevo" : $"{nuevos} nuevos";
        subEstacion.text = PerfilDatos.Boletos == 1 ? "Tenés 1 boleto para abrir" : $"Tenés {PerfilDatos.Boletos} boletos para abrir";
        monedasMenu.Refrescar();
    }

    private void SiguienteFondo()
    {
        fondoActual = (fondoActual + 1) % fondos.Length;
        Img entra = fondoFrente == fondoA ? fondoB : fondoA;
        entra.sprite = fondos[fondoActual];
        AjustarCubrir(entra, (RectTransform)entra.rectTransform.parent);
        entra.transform.SetSiblingIndex(1); // arriba de la otra imagen y abajo de las sombras
        entra.color = new Color(1f, 1f, 1f, 0f);
        if (entra == fondoA) inicioA = Time.unscaledTime; else inicioB = Time.unscaledTime;
        fondoFrente = entra;
        mezcla = 0f;
        proximoFondo = Time.unscaledTime + DuracionFondo;
    }

    private void AnimarFondos()
    {
        if (fondoA == null) return;
        if (fondos.Length > 1 && Time.unscaledTime >= proximoFondo) SiguienteFondo();
        if (mezcla < 1f)
        {
            // La nueva aparece encima de la anterior, que sigue entera hasta el final: así no se oscurece en el medio.
            mezcla = Mathf.MoveTowards(mezcla, 1f, Time.unscaledDeltaTime / FundidoFondo);
            fondoFrente.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, mezcla));
            if (mezcla >= 1f) (fondoFrente == fondoA ? fondoB : fondoA).color = new Color(1f, 1f, 1f, 0f);
        }
        Acercar(fondoA, inicioA);
        Acercar(fondoB, inicioB);
    }

    // Mientras se ve, la imagen se acerca y se corre despacio hacia la izquierda.
    private static void Acercar(Img img, float inicio)
    {
        float t = Mathf.Clamp01((Time.unscaledTime - inicio) / (DuracionFondo + FundidoFondo));
        float s = 1.02f + 0.06f * t;
        img.rectTransform.localScale = new Vector3(s, s, 1f);
        img.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(20f, -20f, t), Mathf.Lerp(-6f, 6f, t));
    }
}
