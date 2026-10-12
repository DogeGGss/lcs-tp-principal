using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using static ShopUIKit;
using Img = UnityEngine.UI.Image;

// Pantalla de opciones del menú principal (US 048). Va en la raíz del prefab PanelOpciones.
// - CA2: Q y E pasan a la pestaña anterior o siguiente (Gráficos, Controles, Sonido).
// - CA3: los cambios de cada pestaña quedan pendientes hasta apretar "Aplicar". Si el jugador sale con cambios
//   sin aplicar, se le pregunta si quiere guardarlos.
// - CA4: "Restablecer" pide confirmación antes de volver la pestaña a los valores por defecto.
// - CA6: Esc o "Volver" cierran la pantalla y vuelven al menú principal.
// Cada pestaña (GraficosUIController, SonidoUIController y ControlsPanel) se registra como ISeccion.
// La ventana de confirmación se arma por código, con el mismo estilo que la pausa y la tienda.
[DefaultExecutionOrder(-40)] // antes que ControlsPanel: si está esperando una tecla, Esc y Q/E son de él
public class OpcionesPantalla : MonoBehaviour
{
    public interface ISeccion
    {
        bool HayCambios { get; }
        void Aplicar();
        void Descartar();
    }

    private static readonly string[] NombresPestañas = { "BtnGraficos", "BtnControles", "BtnSonido" };
    private static readonly string[] NombresPaneles = { "PanelGraficos", "PanelControles", "PanelSonido" };

    private readonly List<ISeccion> secciones = new List<ISeccion>();
    private readonly List<UnityEngine.UI.Button> pestañas = new List<UnityEngine.UI.Button>();
    private readonly List<GameObject> paneles = new List<GameObject>();
    private TMP_Dropdown[] desplegables;
    private MenuUIController menu;
    private bool iniciado;

    // Ventana de confirmación y aviso de guardado
    private TMP_FontAsset displayFont, labelFont, bodyFont;
    private Sprite rounded;
    private GameObject lienzo;
    private RectTransform ventana, botones;
    private TextMeshProUGUI tituloVentana, textoVentana, aviso;
    private float avisoHasta;
    private System.Action alAceptar, alCancelar;

    public static bool DialogoAbierto { get; private set; }

    /// <summary>La pantalla de opciones que contiene a este objeto, o null si está fuera de ella (por ejemplo, en la pausa).</summary>
    public static OpcionesPantalla De(Component c) => c != null ? c.GetComponentInParent<OpcionesPantalla>(true) : null;

    public void Registrar(ISeccion seccion)
    {
        if (seccion != null && !secciones.Contains(seccion)) secciones.Add(seccion);
    }

    public bool HayCambios
    {
        get
        {
            foreach (ISeccion s in secciones) if (s.HayCambios) return true;
            return false;
        }
    }

    // =====================================================================
    // Ciclo de vida
    // =====================================================================

    private void Awake() => Iniciar();

    private void Iniciar()
    {
        if (iniciado) return;
        iniciado = true;

        menu = FindAnyObjectByType<MenuUIController>(FindObjectsInactive.Include);

        // El panel entero tenía la animación de los botones: toda la pantalla de opciones se agrandaba al pasar
        // el mouse y se achicaba con cada clic. Se apaga acá (los botones siguen teniendo la suya).
        ButtonHoverAnimation animacion = GetComponent<ButtonHoverAnimation>();
        if (animacion != null)
        {
            animacion.enabled = false;
            transform.localScale = Vector3.one;
        }

        for (int i = 0; i < NombresPestañas.Length; i++)
        {
            Transform boton = Buscar(transform, NombresPestañas[i]);
            Transform panel = Buscar(transform, NombresPaneles[i]);
            if (boton == null || panel == null) continue;
            pestañas.Add(boton.GetComponent<UnityEngine.UI.Button>());
            paneles.Add(panel.gameObject);
        }
        CrearPestañaMira();
        desplegables = GetComponentsInChildren<TMP_Dropdown>(true);

        // CA6: "Volver" pasa por acá para revisar si hay cambios sin aplicar.
        Transform volver = Buscar(transform, "BtnVolver");
        UnityEngine.UI.Button botonVolver = volver != null ? volver.GetComponent<UnityEngine.UI.Button>() : null;
        if (botonVolver != null)
        {
            for (int i = 0; i < botonVolver.onClick.GetPersistentEventCount(); i++)
                botonVolver.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
            botonVolver.onClick.AddListener(Volver);
        }

        // Tipografías y sprite: los mismos que ya tiene la pestaña Controles.
        ControlsPanel controles = GetComponentInChildren<ControlsPanel>(true);
        if (controles != null)
        {
            displayFont = controles.DisplayFont;
            labelFont = controles.LabelFont;
            bodyFont = controles.BodyFont;
            rounded = controles.Rounded;
        }
    }

    private void OnEnable()
    {
        Iniciar();
        // Al abrir la pantalla se muestra la primera pestaña si no hay ninguna activa.
        if (PestañaActual() < 0 && pestañas.Count > 0) MostrarPestaña(0);
    }

    private void OnDisable()
    {
        if (panelMira != null) panelMira.SetActive(false); // el menú solo apaga las tres pestañas de la escena
        CerrarDialogo();
        if (lienzo != null) lienzo.SetActive(false);
    }

    private void OnDestroy()
    {
        DialogoAbierto = false;
        if (lienzo != null) Destroy(lienzo);
    }

    private void Update()
    {
        if (aviso != null)
        {
            aviso.alpha = Time.unscaledTime < avisoHasta ? 1f : 0f;
            if (!DialogoAbierto && Time.unscaledTime >= avisoHasta && lienzo != null && lienzo.activeSelf) lienzo.SetActive(false);
        }

        if (DialogoAbierto)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) Cancelar();
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Aceptar();
            return;
        }

        if (ControlsPanel.IsBusy || DesplegableAbierto()) return;

        if (Input.GetKeyDown(KeyCode.Q)) CambiarPestaña(-1);      // CA2
        else if (Input.GetKeyDown(KeyCode.E)) CambiarPestaña(1);  // CA2
        else if (Input.GetKeyDown(KeyCode.Escape)) Volver();      // CA6
    }

    // =====================================================================
    // Pestañas (CA2)
    // =====================================================================

    // Pestaña "Mira" (US 172): se crea por código al lado de Sonido, copiando su botón, sin tocar la escena.
    private GameObject panelMira;

    private void CrearPestañaMira()
    {
        if (panelMira != null || pestañas.Count < 2 || pestañas[pestañas.Count - 1] == null) return;
        UnityEngine.UI.Button ultimo = pestañas[pestañas.Count - 1], anterior = pestañas[pestañas.Count - 2];
        GameObject ultimoPanel = paneles[paneles.Count - 1];

        GameObject clon = Instantiate(ultimo.gameObject, ultimo.transform.parent);
        clon.name = "BtnMira";
        RectTransform rect = (RectTransform)clon.transform, rectUltimo = (RectTransform)ultimo.transform;
        Vector2 paso = anterior != null ? rectUltimo.anchoredPosition - ((RectTransform)anterior.transform).anchoredPosition : new Vector2(163f, 0f);
        rect.anchoredPosition = rectUltimo.anchoredPosition + paso;
        rect.SetSiblingIndex(rectUltimo.GetSiblingIndex() + 1);
        TMP_Text texto = clon.GetComponentInChildren<TMP_Text>(true);
        if (texto != null) texto.text = texto.text == texto.text.ToUpperInvariant() ? "MIRA" : "Mira";

        panelMira = new GameObject("PanelMira", typeof(RectTransform));
        panelMira.layer = ultimoPanel.layer;
        panelMira.SetActive(false);
        panelMira.transform.SetParent(ultimoPanel.transform.parent, false);
        panelMira.transform.SetSiblingIndex(ultimoPanel.transform.GetSiblingIndex() + 1);
        panelMira.AddComponent<MiraUIController>();

        // Las otras pestañas apagan la de la mira; la de la mira apaga las otras.
        foreach (UnityEngine.UI.Button b in pestañas)
            if (b != null) b.onClick.AddListener(() => panelMira.SetActive(false));
        List<GameObject> otros = new List<GameObject>(paneles);
        UnityEngine.UI.Button boton = clon.GetComponent<UnityEngine.UI.Button>();
        boton.onClick = new UnityEngine.UI.Button.ButtonClickedEvent(); // sin lo que traía la copia (mostrar Sonido)
        boton.onClick.AddListener(() =>
        {
            foreach (GameObject p in otros) p.SetActive(false);
            panelMira.SetActive(true);
        });

        pestañas.Add(boton);
        paneles.Add(panelMira);
    }

    private int PestañaActual()
    {
        for (int i = 0; i < paneles.Count; i++) if (paneles[i].activeSelf) return i;
        return -1;
    }

    private void CambiarPestaña(int paso)
    {
        if (pestañas.Count == 0) return;
        int actual = Mathf.Max(PestañaActual(), 0);
        MostrarPestaña((actual + paso + pestañas.Count) % pestañas.Count);
    }

    // Se "aprieta" el botón de la pestaña: así se usa lo mismo que ya hace el clic (MenuUIController).
    private void MostrarPestaña(int indice)
    {
        if (pestañas[indice] != null) pestañas[indice].onClick.Invoke();
    }

    private bool DesplegableAbierto()
    {
        if (desplegables == null) return false;
        foreach (TMP_Dropdown d in desplegables) if (d != null && d.IsExpanded) return true;
        return false;
    }

    // =====================================================================
    // Salir (CA3, CA6)
    // =====================================================================

    public void Volver()
    {
        if (!HayCambios) { Salir(); return; }

        Preguntar("¿Guardar los cambios?",
            "Hay cambios sin aplicar. Si salís sin guardarlos, se pierden.",
            new[] { ("Guardar", true), ("Descartar", false), ("Cancelar", false) },
            new System.Action[]
            {
                () => { AplicarTodo(); Salir(); },
                () => { DescartarTodo(); Salir(); },
                null
            });
    }

    private void Salir()
    {
        if (menu != null) menu.MostrarMenuPrincipal();
        else gameObject.SetActive(false);
    }

    private void AplicarTodo()
    {
        foreach (ISeccion s in secciones) if (s.HayCambios) s.Aplicar();
        AvisarGuardado();
    }

    private void DescartarTodo()
    {
        foreach (ISeccion s in secciones) s.Descartar();
    }

    // =====================================================================
    // Para las pestañas
    // =====================================================================

    /// <summary>CA4: pide confirmación antes de restablecer una pestaña.</summary>
    public void ConfirmarRestablecer(string pestaña, System.Action restablecer)
    {
        Preguntar("¿Restablecer " + pestaña + "?",
            "Los valores de esta pestaña vuelven a los de fábrica y se guardan.",
            new[] { ("Restablecer", true), ("Cancelar", false) },
            new System.Action[] { () => { restablecer?.Invoke(); AvisarGuardado(); }, null });
    }

    /// <summary>Muestra "Cambios guardados" unos instantes, abajo al centro.</summary>
    public void AvisarGuardado()
    {
        ArmarLienzo();
        lienzo.SetActive(true);
        avisoHasta = Time.unscaledTime + 1.6f;
    }

    // =====================================================================
    // Ventana de confirmación
    // =====================================================================

    private void Preguntar(string titulo, string texto, (string etiqueta, bool principal)[] opciones, System.Action[] acciones)
    {
        ArmarLienzo();
        lienzo.SetActive(true);
        ventana.gameObject.SetActive(true);
        tituloVentana.text = titulo;
        textoVentana.text = texto;

        for (int i = botones.childCount - 1; i >= 0; i--) Destroy(botones.GetChild(i).gameObject);

        alAceptar = null;
        alCancelar = null;
        float x = 0f;
        for (int i = 0; i < opciones.Length; i++)
        {
            System.Action accion = acciones[i];
            System.Action alElegir = () => { CerrarDialogo(); accion?.Invoke(); };
            if (opciones[i].principal && alAceptar == null) alAceptar = alElegir;
            if (accion == null) alCancelar = alElegir;
            float w = opciones[i].principal ? 190f : 170f;
            Boton(botones, x, 0f, w, 56f, opciones[i].etiqueta, opciones[i].principal, alElegir);
            x += w + 16f;
        }
        if (alCancelar == null) alCancelar = CerrarDialogo;
        DialogoAbierto = true;
    }

    private void Aceptar() => (alAceptar ?? CerrarDialogo).Invoke();

    private void Cancelar() => (alCancelar ?? CerrarDialogo).Invoke();

    private void CerrarDialogo()
    {
        DialogoAbierto = false;
        if (ventana != null) ventana.gameObject.SetActive(false);
    }

    private void ArmarLienzo()
    {
        if (lienzo != null) return;

        // Lienzo propio a 1920 x 1080, como la pausa y el nombre de jugador (el menú usa otra escala).
        lienzo = new GameObject("OpcionesDialogo", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        lienzo.layer = 5;
        Canvas canvas = lienzo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        var scaler = lienzo.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        RectTransform raiz = (RectTransform)lienzo.transform;

        // Aviso de guardado
        RectTransform avisoRect = Node("Aviso", raiz);
        avisoRect.anchorMin = avisoRect.anchorMax = avisoRect.pivot = new Vector2(0.5f, 0f);
        avisoRect.anchoredPosition = new Vector2(0f, 60f);
        avisoRect.sizeDelta = new Vector2(600f, 40f);
        aviso = Text(avisoRect, labelFont, 22f, Ok, TextAlignmentOptions.Center, 12f, true);
        aviso.text = "Cambios guardados";
        aviso.alpha = 0f;

        // Ventana
        ventana = Stretch(Node("Ventana", raiz));
        Image(Stretch(Node("Oscurecido", ventana)), null, new Color(0f, 0f, 0f, DarkAlpha(0.72f)), 0f, true);

        const float W = 720f, H = 330f;
        RectTransform panel = Node("Panel", ventana);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(W, H);
        Image(panel, rounded, Rgb(8, 10, 14, DarkAlpha(0.97f)), 10f, true);

        tituloVentana = Text(Place(Node("Titulo", panel), 48f, 44f, W - 96f, 56f), displayFont, 48f, Ink, TextAlignmentOptions.MidlineLeft, 2f, true);
        Image(Place(Node("Linea", panel), 48f, 108f, 150f, 3f), null, Accent);
        textoVentana = Text(Place(Node("Texto", panel), 48f, 128f, W - 96f, 70f), bodyFont, 21f, Soft, TextAlignmentOptions.TopLeft);
        textoVentana.textWrappingMode = TextWrappingModes.Normal;

        botones = Place(Node("Botones", panel), 48f, H - 48f - 56f, W - 96f, 56f);
        TextMeshProUGUI ayuda = Text(Place(Node("Ayuda", panel), 48f, H - 30f, W - 96f, 20f), labelFont, 14f, Mute, TextAlignmentOptions.MidlineLeft, 10f, true);
        ayuda.text = "Enter  Aceptar     Esc  Cancelar";
        ayuda.rectTransform.anchoredPosition += new Vector2(0f, 8f);

        ventana.gameObject.SetActive(false);
        lienzo.SetActive(false);
    }

    private void Boton(RectTransform padre, float x, float y, float w, float h, string etiqueta, bool principal, System.Action alHacerClic)
    {
        RectTransform rect = Place(Node(etiqueta, padre), x, y, w, h);
        Color normal = principal ? Accent : ChipColor;
        Img fondo = Image(rect, rounded, normal, 6f, true);
        Text(Stretch(Node("Texto", rect)), displayFont, 22f, principal ? KeyInk : Ink, TextAlignmentOptions.Center, 10f, true).text = etiqueta;
        ShopPointerTarget puntero = rect.gameObject.AddComponent<ShopPointerTarget>();
        puntero.Hovered = () => fondo.color = principal ? Hot : ChipDim;
        puntero.Exited = () => fondo.color = normal;
        puntero.Clicked = alHacerClic;
    }

    private static Transform Buscar(Transform raiz, string nombre)
    {
        if (raiz.name == nombre) return raiz;
        for (int i = 0; i < raiz.childCount; i++)
        {
            Transform t = Buscar(raiz.GetChild(i), nombre);
            if (t != null) return t;
        }
        return null;
    }
}
