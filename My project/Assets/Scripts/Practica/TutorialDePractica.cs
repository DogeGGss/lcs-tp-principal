using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static ShopUIKit;
using Img = UnityEngine.UI.Image;

// US 166 · Guía de controles en el campo de práctica (F19). Carteles arriba al centro que enseñan los controles de a un
// paso por vez. Lo pone el menú Riftwalker > Mapa de práctica en la escena Mapa_practica.
// - CA1: cada cartel muestra la tecla dibujada y una frase corta ("R · Recargar"). Cuando se completa un paso aparece
//   el siguiente.
// - CA2: cada parte de un paso se completa haciéndola (no hay botón "Siguiente"); la que ya se hizo queda en verde.
// - CA4: Tab saltea el paso actual y mantener Tab saltea todo el tutorial.
// - CA5: las teclas son las que configuró el jugador (KeyBindings, US 155), no las de fábrica, y se redibujan si las
//   cambia desde la pausa.
// - CA6: al terminar (o saltear) el tutorial queda guardado (Guardado, US 162): la próxima vez no aparece solo. Se
//   vuelve a abrir con el botón "Tutorial" del panel del campo de práctica (PanelDePractica).
// Los pasos (en el campo de práctica hay habilidad y granadas infinitas para practicarlas: PlayerAbility, PlayerLoadout):
//   1. Moverse, mirar, saltar, agacharse y correr (en el Táctico, Shift camina despacio en vez de correr: US 198).
//   2. Pegarle a un blanco y recargar.
//   3. Apuntar con la mira del francotirador y pegarle en la cabeza a un blanco. Al llegar a este paso se le da el
//      francotirador (en el campo de práctica las armas son gratis).
//   4. Cambiar de arma (principal, secundaria y cuchillo), sacar una granada y tirarla.
//   5. Usar la habilidad del personaje.
//   6. Abrir la tienda y elegir otra arma. Mientras corre el tutorial se llama "Tienda"; sin tutorial, en el campo de
//      práctica se llama "Cambiar arma" (ShopUI).
// - CA3: al acercarse a un panel de modo (PanelDeModo), su explicación también sale en pantalla, a la derecha.
// Se arma por código con las medidas de una pantalla de 1920 x 1080, como la nota para el PO y la tienda.
public class TutorialDePractica : MonoBehaviour
{
    public const string ClaveCompleto = "Practica.TutorialCompleto";

    [Header("Tipografías (las mismas de la tienda)")]
    public TMP_FontAsset displayFont; // Barlow Condensed Bold
    public TMP_FontAsset labelFont;   // Barlow Condensed SemiBold
    public TMP_FontAsset bodyFont;    // Barlow Regular

    [Header("Sprites")]
    public Sprite rounded;

    public static TutorialDePractica Actual { get; private set; }

    public bool Abierto => paso >= 0;

    /// <summary>El tutorial está mostrando un paso (ShopUI llama "Tienda" a "Cambiar arma" mientras tanto).</summary>
    public static bool EnCurso => Actual != null && Actual.Abierto;

    private const float MantenerParaSaltearTodo = 1f; // s con Tab apretado
    private const float DuracionFinal = 4f;
    private const float MovimientoNecesario = 0.6f, MouseNecesario = 12f, CorrerNecesario = 0.4f;

    // Una parte de un paso: las teclas que se dibujan, la frase y si ya se hizo.
    private class Parte
    {
        public System.Func<string[]> teclas;
        public string frase;
        public bool hecha;
        public Img[] capas;
        public TextMeshProUGUI texto;
    }

    private class Paso
    {
        public string titulo;
        public Parte[] partes;
        public System.Action alEmpezar;
    }

    private Paso[] pasos;
    private int paso = -1;
    private float finHasta = -1f;

    // Lo que se va juntando para las partes que no son de un solo toque.
    private float movido, mouse, corrido;
    private float tabDesde = -1f;
    private bool apunto;
    private ShopItem armaAlEmpezar, secundariaAlEmpezar;

    private PlayerLoadout equipo;
    private PlayerAbility habilidad;
    private RectTransform caja, fila, saltear, barraTab;
    private TextMeshProUGUI ceja, titulo, textoSaltear;
    private CanvasGroup grupo;

    // CA3: la explicación del modo en pantalla.
    private RectTransform tarjeta;
    private TextMeshProUGUI tarjetaTitulo, tarjetaTexto;
    private PanelDeModo modoMostrado;

    private void Awake() => Actual = this;

    private void OnDestroy()
    {
        if (Actual == this) Actual = null;
    }

    private void OnEnable()
    {
        WeaponFire.Impacto += AlImpactar;
        GrenadeThrower.Thrown += AlTirarGranada;
        KeyBindings.Changed += Redibujar;
    }

    private void OnDisable()
    {
        WeaponFire.Impacto -= AlImpactar;
        GrenadeThrower.Thrown -= AlTirarGranada;
        if (habilidad != null) habilidad.Used -= AlUsarHabilidad;
        KeyBindings.Changed -= Redibujar;
    }

    private void Start()
    {
        equipo = FindAnyObjectByType<PlayerLoadout>();
        BuscarHabilidad();
        ArmarPasos();
        Armar();
        // CA6: si ya lo terminó, no aparece solo (queda el botón "Tutorial" del panel).
        if (Guardado.GetInt(ClaveCompleto, 0) == 1) Cerrar(false);
        else Empezar();
    }

    /// <summary>Desde el principio (el botón "Tutorial" del panel).</summary>
    public void Empezar()
    {
        finHasta = -1f;
        Ir(0);
    }

    private void ArmarPasos()
    {
        pasos = new[]
        {
            new Paso
            {
                titulo = "Moverse",
                partes = new[]
                {
                    P("Moverse", GameAction.Adelante, GameAction.Izquierda, GameAction.Atras, GameAction.Derecha),
                    new Parte { teclas = () => new[] { "Mouse" }, frase = "Mirar" },
                    P("Saltar", GameAction.Saltar),
                    P("Agacharse", GameAction.Agacharse),
                    P("Correr (en Táctico, caminar despacio)", GameAction.Correr),
                },
            },
            new Paso
            {
                titulo = "Disparar",
                partes = new[] { P("Pegale a un blanco", GameAction.Disparar), P("Recargar", GameAction.Recargar) },
            },
            new Paso
            {
                titulo = "Francotirador",
                partes = new[] { P("Apuntar con la mira", GameAction.Apuntar), P("Pegale en la cabeza", GameAction.Disparar) },
                alEmpezar = DarFrancotirador,
            },
            new Paso
            {
                titulo = "Cambiar de arma",
                partes = new[]
                {
                    P("Principal", GameAction.ArmaPrincipal), P("Secundaria", GameAction.ArmaSecundaria), P("Cuchillo", GameAction.Cuchillo),
                    P("Granada", GameAction.Granadas), P("Tirarla", GameAction.Disparar),
                },
            },
            new Paso
            {
                titulo = "Habilidad",
                partes = new[] { P("Usar la habilidad de tu personaje", GameAction.Habilidad) },
                // Se vuelve a buscar por si el jugador apareció después del tutorial. Si no tiene habilidad, el paso
                // queda igual a la vista (se saltea con Tab) en vez de pasar solo.
                alEmpezar = BuscarHabilidad,
            },
            new Paso
            {
                titulo = "Tienda",
                partes = new[]
                {
                    P("Abrir la tienda", GameAction.Tienda),
                    new Parte { teclas = () => new[] { KeyBindings.Label(GameAction.Disparar) }, frase = "Elegí otra arma" },
                },
                alEmpezar = () =>
                {
                    armaAlEmpezar = equipo != null ? equipo.Primary : null;
                    secundariaAlEmpezar = equipo != null ? equipo.Secondary : null;
                },
            },
        };
    }

    private static Parte P(string frase, params GameAction[] acciones) => new Parte
    {
        frase = frase,
        teclas = () =>
        {
            string[] t = new string[acciones.Length];
            for (int i = 0; i < acciones.Length; i++) t[i] = KeyBindings.Label(acciones[i]);
            return t;
        },
    };

    // Paso 3: el francotirador como arma principal (gratis en el campo de práctica), si no lo tiene ya.
    private void DarFrancotirador()
    {
        if (equipo == null || equipo.Catalog == null) return;
        if (equipo.Primary != null && equipo.Primary.zoom > 1f) return;
        ShopItem fran = equipo.Catalog.items.Find(i => i != null && i.kind == ShopItemKind.PrimaryWeapon && i.zoom > 1f);
        if (fran != null) equipo.Buy(fran);
    }

    // ---------- Cuadro a cuadro ----------

    private void Update()
    {
        if (grupo == null) return;
        bool pausa = PauseMenu.IsPaused;
        grupo.alpha = !pausa && !ShopUI.IsOpen ? 1f : 0f;

        if (finHasta > 0f && Time.time >= finHasta) { finHasta = -1f; caja.gameObject.SetActive(false); }
        if (!Abierto || pausa) { tabDesde = -1f; return; }

        Detectar();
        Tab();
    }

    // CA2: cada parte se completa haciéndola.
    private void Detectar()
    {
        Parte[] partes = pasos[paso].partes;
        bool libre = !ShopUI.IsOpen;
        switch (paso)
        {
            case 0:
                if (!libre) break;
                bool mueve = KeyBindings.Held(GameAction.Adelante) || KeyBindings.Held(GameAction.Atras) ||
                             KeyBindings.Held(GameAction.Izquierda) || KeyBindings.Held(GameAction.Derecha);
                if (mueve) movido += Time.deltaTime;
                if (mueve && KeyBindings.Held(GameAction.Correr)) corrido += Time.deltaTime;
                mouse += Mathf.Abs(Input.GetAxis("Mouse X")) + Mathf.Abs(Input.GetAxis("Mouse Y"));
                if (movido >= MovimientoNecesario) Hecha(0);
                if (mouse >= MouseNecesario) Hecha(1);
                if (KeyBindings.Down(GameAction.Saltar)) Hecha(2);
                if (KeyBindings.Down(GameAction.Agacharse)) Hecha(3);
                if (corrido >= CorrerNecesario) Hecha(4);
                break;
            case 1:
                if (libre && KeyBindings.Down(GameAction.Recargar)) Hecha(1);
                break;
            case 2:
                if (MiraTelescopica.Puesta) { apunto = true; Hecha(0); }
                break;
            case 3:
                if (!libre) break;
                if (KeyBindings.Down(GameAction.ArmaPrincipal)) Hecha(0);
                if (KeyBindings.Down(GameAction.ArmaSecundaria)) Hecha(1);
                if (KeyBindings.Down(GameAction.Cuchillo)) Hecha(2);
                if (KeyBindings.Down(GameAction.Granadas)) Hecha(3);
                break;
            case 4:
                if (libre && KeyBindings.Down(GameAction.Habilidad)) Hecha(0);
                break;
            case 5:
                if (ShopUI.IsOpen) Hecha(0);
                if (equipo != null && (equipo.Primary != armaAlEmpezar || equipo.Secondary != secundariaAlEmpezar)) Hecha(1);
                break;
        }
        if (Abierto && System.Array.TrueForAll(pasos[paso].partes, p => p.hecha)) Siguiente();
        else Pintar(partes);
    }

    // Pasos 2 y 3: los tiros que les pegan a los bots del campo.
    private void AlImpactar(HealthSystem vida, int danio, BodyZone zona)
    {
        if (!Abierto || vida == null || vida.GetComponent<BotDePractica>() == null) return;
        if (paso == 1) Hecha(0);
        // Con la mira: el francotirador sale de la mira al disparar (el cerrojo), así que vale si ya apuntó antes.
        if (paso == 2 && zona == BodyZone.Head && apunto) Hecha(1);
    }

    // Paso 4: la granada que tiró el jugador.
    private void AlTirarGranada(Transform quien, Grenade1 granada)
    {
        if (Abierto && paso == 3) Hecha(4);
    }

    private void BuscarHabilidad()
    {
        PlayerAbility nueva = equipo != null ? equipo.GetComponentInChildren<PlayerAbility>() : null;
        if (nueva == null) nueva = FindAnyObjectByType<PlayerAbility>();
        if (nueva == habilidad) return;
        if (habilidad != null) habilidad.Used -= AlUsarHabilidad;
        habilidad = nueva;
        if (habilidad != null) habilidad.Used += AlUsarHabilidad;
    }

    // Paso 5: también vale apretar la tecla (por si el efecto no se pudo hacer ahí, por ejemplo contra una pared).
    private void AlUsarHabilidad()
    {
        if (Abierto && paso == 4) Hecha(0);
    }

    // CA4: Tab saltea el paso; mantenido, saltea todo el tutorial.
    private void Tab()
    {
        if (Input.GetKeyDown(KeyCode.Tab)) tabDesde = Time.unscaledTime;
        float tiempo = tabDesde >= 0f ? Time.unscaledTime - tabDesde : 0f;
        barraTab.sizeDelta = new Vector2(saltear.sizeDelta.x * Mathf.Clamp01(tiempo / MantenerParaSaltearTodo), 3f);
        if (tabDesde < 0f) return;
        if (tiempo >= MantenerParaSaltearTodo)
        {
            tabDesde = -1f;
            Cerrar(true);
        }
        else if (Input.GetKeyUp(KeyCode.Tab))
        {
            tabDesde = -1f;
            Siguiente();
        }
    }

    private void Hecha(int parte)
    {
        Parte[] partes = pasos[paso].partes;
        if (parte < partes.Length) partes[parte].hecha = true;
    }

    private void Siguiente()
    {
        if (paso + 1 < pasos.Length) Ir(paso + 1);
        else Terminar();
    }

    private void Ir(int nuevo)
    {
        paso = nuevo;
        movido = mouse = corrido = 0f;
        apunto = false;
        tabDesde = -1f;
        foreach (Parte p in pasos[paso].partes) p.hecha = false;
        pasos[paso].alEmpezar?.Invoke();
        caja.gameObject.SetActive(true);
        saltear.gameObject.SetActive(true);
        Redibujar();
    }

    // CA6: terminó todos los pasos.
    private void Terminar()
    {
        Cerrar(true);
        caja.gameObject.SetActive(true);
        saltear.gameObject.SetActive(false);
        ceja.text = "TUTORIAL COMPLETO";
        titulo.text = "¡Listo! Ya sabés lo básico";
        Limpiar();
        TextMeshProUGUI t = Text(Node("Final", fila), bodyFont, 22f, Soft, TextAlignmentOptions.Center);
        t.text = "Mirá los paneles de los modos para saber cómo se gana. El tutorial se vuelve a abrir desde el panel.";
        float ancho = Width(t, t.text) + 4f;
        Place(t.rectTransform, 0f, 0f, ancho, AltoTecla);
        Ajustar(ancho);
        finHasta = Time.time + DuracionFinal;
    }

    // Lo oculta y, si "guardar", lo marca como hecho para que no aparezca solo la próxima vez.
    private void Cerrar(bool guardar)
    {
        paso = -1;
        tabDesde = -1f;
        if (caja != null) caja.gameObject.SetActive(false);
        if (!guardar) return;
        Guardado.SetInt(ClaveCompleto, 1);
        Guardado.Save();
    }

    // =====================================================================
    // Armado de la interfaz
    // =====================================================================

    private const float AltoTecla = 38f, Separacion = 28f, Margen = 28f;

    private void Armar()
    {
        GameObject lienzo = new GameObject("Tutorial (carteles)", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(CanvasGroup));
        lienzo.layer = 5;
        lienzo.transform.SetParent(transform, false);
        Canvas canvas = lienzo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 15; // arriba del HUD, debajo de la tienda y la pausa
        var escala = lienzo.GetComponent<UnityEngine.UI.CanvasScaler>();
        escala.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        grupo = lienzo.GetComponent<CanvasGroup>();
        grupo.blocksRaycasts = false;
        grupo.interactable = false;

        // CA1: arriba al centro, debajo del aviso de la tienda ("B · Cambiar arma", que ShopUI pone a 150 px del borde).
        caja = Node("Cartel", lienzo.transform);
        caja.anchorMin = caja.anchorMax = new Vector2(0.5f, 1f);
        caja.pivot = new Vector2(0.5f, 1f);
        caja.anchoredPosition = new Vector2(0f, -216f);
        Image(caja, rounded, HudColor, 10f);
        Img borde = Image(Place(Node("Borde", caja), 0f, 0f, 4f, 0f), null, Accent);
        borde.rectTransform.anchorMax = new Vector2(0f, 1f);
        borde.rectTransform.anchorMin = Vector2.zero;
        borde.rectTransform.offsetMin = borde.rectTransform.offsetMax = Vector2.zero;
        borde.rectTransform.sizeDelta = new Vector2(4f, 0f);

        ceja = Text(Node("Paso", caja), labelFont, 17f, Accent, TextAlignmentOptions.Center, 12f, true);
        titulo = Text(Node("Título", caja), displayFont, 32f, Ink, TextAlignmentOptions.Center, 0f, true);
        fila = Node("Teclas", caja);

        saltear = Node("Saltear", caja);
        textoSaltear = Text(Stretch(Node("Texto", saltear)), labelFont, 16f, Mute, TextAlignmentOptions.Center);
        barraTab = Place(Node("Mantener Tab", saltear), 0f, 22f, 0f, 3f);
        Image(barraTab, null, Accent);
        caja.gameObject.SetActive(false);

        // CA3: a la derecha, a media altura.
        tarjeta = Node("Modo", lienzo.transform);
        tarjeta.anchorMin = tarjeta.anchorMax = new Vector2(1f, 0.5f);
        tarjeta.pivot = new Vector2(1f, 0.5f);
        tarjeta.anchoredPosition = new Vector2(-48f, 40f);
        Image(tarjeta, rounded, HudColor, 10f);
        Img bordeModo = Image(Node("Borde", tarjeta), null, Accent);
        bordeModo.rectTransform.anchorMin = Vector2.zero;
        bordeModo.rectTransform.anchorMax = new Vector2(0f, 1f);
        bordeModo.rectTransform.pivot = new Vector2(0f, 0.5f);
        bordeModo.rectTransform.offsetMin = bordeModo.rectTransform.offsetMax = Vector2.zero;
        bordeModo.rectTransform.sizeDelta = new Vector2(4f, 0f);
        Text(Place(Node("Ceja", tarjeta), Margen, 22f, AnchoTarjeta - Margen * 2f, 20f), labelFont, 17f, Accent,
            TextAlignmentOptions.MidlineLeft, 12f, true).text = "Modo de juego";
        tarjetaTitulo = Text(Place(Node("Título", tarjeta), Margen, 44f, AnchoTarjeta - Margen * 2f, 40f), displayFont, 36f, Ink,
            TextAlignmentOptions.MidlineLeft, 0f, true);
        tarjetaTexto = Text(Place(Node("Texto", tarjeta), Margen, 96f, AnchoTarjeta - Margen * 2f, 100f), bodyFont, 20f, Soft,
            TextAlignmentOptions.TopLeft);
        tarjetaTexto.textWrappingMode = TextWrappingModes.Normal;
        tarjetaTexto.lineSpacing = 4f;
        tarjeta.gameObject.SetActive(false);
    }

    private const float AnchoTarjeta = 540f;

    /// <summary>CA3: el jugador está cerca de un panel de modo: su explicación sale en pantalla.</summary>
    public void MostrarModo(PanelDeModo panel, string nombre, string texto)
    {
        if (tarjeta == null) return;
        modoMostrado = panel;
        tarjetaTitulo.text = nombre;
        tarjetaTexto.text = texto;
        float alto = Mathf.Ceil(tarjetaTexto.GetPreferredValues(texto, AnchoTarjeta - Margen * 2f, 0f).y);
        tarjetaTexto.rectTransform.sizeDelta = new Vector2(AnchoTarjeta - Margen * 2f, alto);
        tarjeta.sizeDelta = new Vector2(AnchoTarjeta, 96f + alto + Margen);
        tarjeta.gameObject.SetActive(true);
    }

    public void OcultarModo(PanelDeModo panel)
    {
        if (tarjeta == null || modoMostrado != panel) return;
        modoMostrado = null;
        tarjeta.gameObject.SetActive(false);
    }

    private void Limpiar()
    {
        for (int i = fila.childCount - 1; i >= 0; i--) Destroy(fila.GetChild(i).gameObject);
    }

    // Arma la fila del paso actual con las teclas que tenga configuradas el jugador (CA5).
    private void Redibujar()
    {
        if (!Abierto || caja == null) return;
        Paso p = pasos[paso];
        ceja.text = $"Tutorial · Paso {paso + 1} de {pasos.Length}";
        titulo.text = p.titulo;
        Limpiar();

        float x = 0f;
        foreach (Parte parte in p.partes)
        {
            string[] teclas = parte.teclas();
            parte.capas = new Img[teclas.Length];
            for (int i = 0; i < teclas.Length; i++)
            {
                RectTransform tecla = Node("Tecla", fila);
                TextMeshProUGUI letra = Text(Stretch(Node("Letra", tecla)), displayFont, 22f, KeyInk, TextAlignmentOptions.Center);
                letra.text = teclas[i];
                float ancho = Mathf.Max(AltoTecla, Width(letra, teclas[i]) + 20f);
                Place(tecla, x, 0f, ancho, AltoTecla);
                parte.capas[i] = Image(tecla, rounded, Ink, 6f);
                x += ancho + 6f;
            }
            parte.texto = Text(Node("Frase", fila), bodyFont, 22f, Soft);
            parte.texto.text = "· " + parte.frase;
            float anchoFrase = Width(parte.texto, "✓ " + parte.frase) + 4f;
            Place(parte.texto.rectTransform, x + 2f, 0f, anchoFrase, AltoTecla);
            x += anchoFrase + Separacion;
        }
        textoSaltear.text = $"<color=#{ColorUtility.ToHtmlStringRGB(Soft)}>Tab</color>  Saltear paso     " +
                            $"<color=#{ColorUtility.ToHtmlStringRGB(Soft)}>Mantené Tab</color>  Saltear tutorial";
        Ajustar(Mathf.Max(0f, x - Separacion));
        Pintar(p.partes);
    }

    // Las partes hechas quedan en verde, con una tilde.
    private void Pintar(Parte[] partes)
    {
        foreach (Parte parte in partes)
        {
            if (parte.capas == null || parte.texto == null) continue;
            foreach (Img capa in parte.capas) capa.color = parte.hecha ? Ok : Ink;
            parte.texto.color = parte.hecha ? Ok : Soft;
            parte.texto.text = (parte.hecha ? "✓ " : "· ") + parte.frase;
        }
    }

    // El cartel toma el ancho de la fila.
    private void Ajustar(float anchoFila)
    {
        float ancho = Mathf.Max(anchoFila, Width(titulo, titulo.text), 420f) + Margen * 2f;
        bool conSaltear = saltear.gameObject.activeSelf;
        caja.sizeDelta = new Vector2(ancho, conSaltear ? 176f : 150f);
        Place(ceja.rectTransform, 0f, 16f, ancho, 20f);
        Place(titulo.rectTransform, 0f, 38f, ancho, 38f);
        Place(fila, (ancho - anchoFila) * 0.5f, 88f, anchoFila, AltoTecla);
        Place(saltear, Margen, 140f, ancho - Margen * 2f, 22f);
    }
}
