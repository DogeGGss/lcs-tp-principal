using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using static ShopUIKit;
using Img = UnityEngine.UI.Image;

// Nota para el PO en el menú principal: antes de probar la entrega, qué ya funciona y qué todavía falta
// implementar, para que no espere cosas que todavía no están.
// - Aparece sola la primera vez que se abre el menú en cada sesión de juego (después de elegir el nombre,
//   si hacía falta) y se cierra con "Entendido", Enter o Esc.
// - Abajo a la derecha del menú principal queda un botón para volver a abrirla.
// - Los textos se editan en el Inspector: cada renglón de las listas es un punto y lo que va antes de ":"
//   sale resaltado. Los controles salen de las teclas actuales del jugador (US 155).
// Se arma por código con las medidas de una pantalla de 1920 x 1080, como la tienda y el nombre de jugador.
public class NotaPO : MonoBehaviour
{
    [Header("Menú principal")]
    [Tooltip("El botón para volver a abrir la nota se ve solo mientras este panel está activo. Si queda vacío, se busca PanelMenuPrincipal.")]
    [SerializeField] private GameObject mainMenuPanel;
    [Tooltip("El fundido negro de MenuUIController. Si queda vacío, se busca OverlayTransicion.")]
    [SerializeField] private CanvasGroup transitionOverlay;

    [Header("Textos")]
    [SerializeField] private string eyebrow = "Nota para el PO · Sprint 1";
    [SerializeField] private string title = "Hola, Amin";
    [TextArea(2, 4)]
    [SerializeField] private string intro =
        "Esta es la entrega del Sprint 1 de Project Riftwalker. Para que sepas qué probar, acá va lo que ya funciona y lo que todavía falta implementar.";
    [SerializeField] private string worksTitle = "Lo que funciona";
    [Tooltip("Un punto por renglón. Lo que va antes de \":\" sale resaltado.")]
    [TextArea(5, 12)]
    [SerializeField] private string works =
        "Movimiento: caminar, correr, saltar y agacharse, con gravedad, colisiones y reaparición si caés del mapa.\n" +
        "Armas: pistola Línea A, fusil Mitre y cuchillo. Daño por zona (cabeza, cuerpo y piernas) y por distancia, dispersión, retroceso, recarga y marcas de bala en las paredes.\n" +
        "Tienda: en la fase de compra y dentro de la zona de compra se puede comprar, vender y deshacer compras. Los escudos protegen de verdad.\n" +
        "HUD: vida, escudo, arma y munición, mira y marcador de impacto. La habilidad muestra su tiempo de recarga.\n" +
        "Menús: principal con música, opciones de video, sonido y controles, nombre de jugador, créditos y pausa (seguir, reiniciar, opciones y salir).";
    [SerializeField] private string missingTitle = "Lo que todavía falta";
    [Tooltip("Un punto por renglón. Lo que va antes de \":\" sale resaltado.")]
    [TextArea(5, 12)]
    [SerializeField] private string missing =
        "Resto de las armas: la tienda muestra todas, pero por ahora solo se usan la pistola Línea A, el Mitre y el cuchillo. Las demás se pueden comprar, pero todavía no se pueden usar.\n" +
        "Granadas: se compran, pero todavía no se pueden tirar.\n" +
        "Habilidad: el Silbato carga y se ve en el HUD, pero todavía no tiene efecto.\n" +
        "Modos y multijugador: Táctico y Deathmatch muestran la sala, pero falta la conexión online. Zombie todavía no tiene zombis: Jugar abre el mapa de la universidad para recorrerlo con las armas, la tienda y el HUD.\n" +
        "Logros: la pantalla está, pero todavía no tiene logros.\n" +
        "Mapas y rivales: Pruebas y Pruebas 2 abren dos mapas de prueba, temporales, para mostrar la jugabilidad. En Pruebas aparecen enemigos que te persiguen y se pueden matar, pero todavía no atacan.";

    [Header("Tipografías (las mismas de la tienda)")]
    [SerializeField] private TMP_FontAsset displayFont; // Barlow Condensed Bold
    [SerializeField] private TMP_FontAsset labelFont;   // Barlow Condensed SemiBold
    [SerializeField] private TMP_FontAsset bodyFont;    // Barlow Regular

    [Header("Sprites")]
    [SerializeField] private Sprite rounded;

    public static bool IsOpen { get; private set; }

    // Una vez por sesión: al volver de una partida al menú no aparece de nuevo (queda el botón).
    private static bool shownThisSession;

    // El proyecto entra a Play sin recargar el dominio: sin esto, en el editor aparecería solo la primera vez.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        shownThisSession = false;
        IsOpen = false;
    }

    private const float PanelW = 1320f, Pad = 48f, ColumnGap = 48f, BulletIndent = 22f;

    // Controles que se muestran, con las teclas que tenga configuradas el jugador.
    private static readonly (string label, GameAction[] keys, string join)[] Controls =
    {
        ("Mover", new[] { GameAction.Adelante, GameAction.Izquierda, GameAction.Atras, GameAction.Derecha }, ""),
        ("Correr", new[] { GameAction.Correr }, ""),
        ("Saltar", new[] { GameAction.Saltar }, ""),
        ("Agacharse", new[] { GameAction.Agacharse }, ""),
        ("Disparar", new[] { GameAction.Disparar }, ""),
        ("Apuntar", new[] { GameAction.Apuntar }, ""),
        ("Recargar", new[] { GameAction.Recargar }, ""),
        ("Mitre / pistola / cuchillo", new[] { GameAction.ArmaPrincipal, GameAction.ArmaSecundaria, GameAction.Cuchillo }, " / "),
        ("Tienda", new[] { GameAction.Tienda }, ""),
        ("Habilidad", new[] { GameAction.Habilidad }, ""),
    };

    private RectTransform canvasRoot, chip, modal, panel;
    private CanvasGroup chipGroup, modalGroup;
    private Img chipBg;
    private TextMeshProUGUI chipLabel, eyebrowText, titleText, introText, worksHeader, missingHeader, worksText, missingText, controlsText;
    private RectTransform accentLine, columns, worksDot, missingDot, divider, controlsLabel, button, keysHint;
    private Color chipHover;
    private Coroutine fadeRoutine;

    // =====================================================================
    // Ciclo de vida
    // =====================================================================

    private void Awake()
    {
        chipHover = Over(White(0.08f), PanelBase);
        BuildCanvas();
        modal.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        if (mainMenuPanel == null) mainMenuPanel = FindInScene("PanelMenuPrincipal");
        if (transitionOverlay == null)
        {
            GameObject overlay = FindInScene("OverlayTransicion");
            if (overlay != null) transitionOverlay = overlay.GetComponent<CanvasGroup>();
        }

        if (!shownThisSession) StartCoroutine(OpenWhenReady());
    }

    private void OnDestroy()
    {
        IsOpen = false;
    }

    private void Update()
    {
        bool showChip = !IsOpen && !PlayerNameScreen.IsOpen && (mainMenuPanel == null || mainMenuPanel.activeInHierarchy);
        if (chip.gameObject.activeSelf != showChip) chip.gameObject.SetActive(showChip);
        if (showChip && transitionOverlay != null) chipGroup.alpha = 1f - transitionOverlay.alpha;

        if (!IsOpen) return;
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            Close();
    }

    // Espera a que se elija el nombre (US 164) y a que el menú termine de aparecer.
    private IEnumerator OpenWhenReady()
    {
        yield return null;
        while (PlayerNameScreen.IsOpen || (transitionOverlay != null && transitionOverlay.alpha > 0.05f))
            yield return null;
        shownThisSession = true;
        Open();
    }

    // =====================================================================
    // Abrir y cerrar
    // =====================================================================

    public void Open()
    {
        IsOpen = true;
        Fill();
        modal.gameObject.SetActive(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeIn());
    }

    public void Close()
    {
        IsOpen = false;
        modal.gameObject.SetActive(false);
    }

    private IEnumerator FadeIn()
    {
        const float duration = 0.18f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = t / duration;
            modalGroup.alpha = k;
            panel.anchoredPosition = new Vector2(0f, -14f * (1f - k) * (1f - k));
            yield return null;
        }
        modalGroup.alpha = 1f;
        panel.anchoredPosition = Vector2.zero;
    }

    // =====================================================================
    // Contenido y medidas
    // =====================================================================

    // Pone los textos y acomoda todo según lo que ocupen (las listas se pueden editar en el Inspector).
    private void Fill()
    {
        float w = PanelW - Pad * 2f;
        float columnW = (w - ColumnGap) * 0.5f;

        eyebrowText.text = eyebrow;
        titleText.text = title;
        introText.text = intro;
        worksHeader.text = worksTitle;
        missingHeader.text = missingTitle;
        worksText.text = Bullets(works, Ok);
        missingText.text = Bullets(missing, Accent);
        controlsText.text = ControlsLine();

        float y = 166f;
        float introH = Height(introText, w);
        Place(introText.rectTransform, Pad, y, w, introH);
        y += introH + 34f;

        // Dos columnas: título con un puntito de color y la lista abajo.
        Place(columns, Pad, y, w, 0f);
        float listW = columnW;
        float listH = Mathf.Max(Height(worksText, listW), Height(missingText, listW));
        Place(worksDot, 0f, 7f, 10f, 10f);
        Place(worksHeader.rectTransform, 20f, 0f, columnW - 20f, 24f);
        Place(worksText.rectTransform, 0f, 40f, listW, listH);
        Place(missingDot, columnW + ColumnGap, 7f, 10f, 10f);
        Place(missingHeader.rectTransform, columnW + ColumnGap + 20f, 0f, columnW - 20f, 24f);
        Place(missingText.rectTransform, columnW + ColumnGap, 40f, listW, listH);
        y += 40f + listH + 30f;

        Place(divider, Pad, y, w, 1f);
        y += 24f;
        Place(controlsLabel, Pad, y, w, 20f);
        y += 28f;
        float controlsH = Height(controlsText, w);
        Place(controlsText.rectTransform, Pad, y, w, controlsH);
        y += controlsH + 30f;

        Place(button, PanelW - Pad - 220f, y, 220f, 56f);
        Place(keysHint, Pad, y, 400f, 56f);
        y += 56f + Pad;

        panel.sizeDelta = new Vector2(PanelW, y);
    }

    // Cada renglón es un punto: viñeta de color, sangría colgante y lo que va antes de ":" resaltado.
    private static string Bullets(string text, Color bulletColor)
    {
        string bullet = ColorUtility.ToHtmlStringRGB(bulletColor);
        string ink = ColorUtility.ToHtmlStringRGB(Ink);
        var sb = new StringBuilder();
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            int colon = line.IndexOf(':');
            if (colon > 0 && colon < 40)
                line = $"<color=#{ink}>{line.Substring(0, colon + 1)}</color>{line.Substring(colon + 1)}";
            if (sb.Length > 0) sb.Append('\n');
            sb.Append($"<color=#{bullet}>•</color><indent={BulletIndent}>{line}</indent>");
        }
        return sb.ToString();
    }

    private static string ControlsLine()
    {
        string ink = ColorUtility.ToHtmlStringRGB(Ink);
        var sb = new StringBuilder();
        foreach (var control in Controls)
        {
            var keys = new string[control.keys.Length];
            for (int i = 0; i < keys.Length; i++) keys[i] = KeyBindings.Label(control.keys[i]);
            // <nobr>: la tecla y lo que hace no se separan al cortar el renglón.
            sb.Append($"<nobr><color=#{ink}>{string.Join(control.join, keys)}</color> {control.label}</nobr>     ");
        }
        sb.Append($"<nobr><color=#{ink}>Esc</color> Pausa</nobr>");
        sb.Append($"     <color=#{ColorUtility.ToHtmlStringRGB(Mute)}>(se cambian en Opciones, pestaña Controles)</color>");
        return sb.ToString();
    }

    private static float Height(TextMeshProUGUI text, float width)
    {
        return Mathf.Ceil(text.GetPreferredValues(text.text, width, 0f).y);
    }

    private GameObject FindInScene(string objectName)
    {
        foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t.name == objectName && t.gameObject.scene == gameObject.scene) return t.gameObject;
        return null;
    }

    // =====================================================================
    // Armado de la interfaz
    // =====================================================================

    private void BuildCanvas()
    {
        GameObject canvasObject = new GameObject("NotaPOCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        canvasObject.layer = 5;
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110; // por encima del menú y del nombre de jugador (100)
        var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        canvasRoot = (RectTransform)canvasObject.transform;

        BuildChip();
        BuildModal();
    }

    // Botón para volver a abrir la nota, abajo a la derecha del menú principal.
    private void BuildChip()
    {
        chip = Node("AbrirNota", canvasRoot);
        chip.anchorMin = chip.anchorMax = chip.pivot = new Vector2(1f, 0f);
        chip.anchoredPosition = new Vector2(-48f, 40f);
        chip.sizeDelta = new Vector2(262f, 76f);
        chipGroup = chip.gameObject.AddComponent<CanvasGroup>();
        chipBg = Image(chip, rounded, PanelColor, 8f, true);

        Img icon = Image(Place(Node("Icono", chip), 14f, 14f, 48f, 48f), rounded, Accent, 24f);
        Text(Stretch(Node("Letra", icon.rectTransform)), displayFont, 30f, KeyInk, TextAlignmentOptions.Center).text = "i";

        chipLabel = Text(Place(Node("Rotulo", chip), 80f, 14f, 300f, 18f), labelFont, 14f, Mute, TextAlignmentOptions.MidlineLeft, 16f, true);
        chipLabel.text = "Antes de jugar";
        TextMeshProUGUI chipName = Text(Place(Node("Nombre", chip), 80f, 30f, 300f, 34f), displayFont, 30f, Ink, TextAlignmentOptions.MidlineLeft, 3f, true);
        chipName.text = "Nota para el PO";
        float textW = Mathf.Max(Width(chipName, chipName.text), Width(chipLabel, chipLabel.text));
        chip.sizeDelta = new Vector2(80f + textW + 26f, 76f);

        ShopPointerTarget pointer = chip.gameObject.AddComponent<ShopPointerTarget>();
        pointer.Hovered = () => { chipBg.color = chipHover; chipLabel.color = Accent; };
        pointer.Exited = () => { chipBg.color = PanelColor; chipLabel.color = Mute; };
        pointer.Clicked = () => { pointer.Exited(); Open(); };
    }

    private void BuildModal()
    {
        modal = Stretch(Node("Ventana", canvasRoot));
        modalGroup = modal.gameObject.AddComponent<CanvasGroup>();
        Image(Stretch(Node("Oscurecido", modal)), null, new Color(0f, 0f, 0f, DarkAlpha(0.78f)), 0f, true);

        panel = Node("Panel", modal);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(PanelW, 800f);
        Image(panel, rounded, BarColor, 10f, true);

        float w = PanelW - Pad * 2f;
        eyebrowText = Text(Place(Node("Rotulo", panel), Pad, 44f, w, 20f), labelFont, 16f, Accent, TextAlignmentOptions.MidlineLeft, 16f, true);
        titleText = Text(Place(Node("Titulo", panel), Pad, 70f, w, 66f), displayFont, 62f, Ink, TextAlignmentOptions.MidlineLeft, 2f, true);
        accentLine = Place(Node("LineaAnden", panel), Pad, 146f, 150f, 3f);
        Image(accentLine, null, Accent);

        introText = Paragraph(Node("Bajada", panel), 21f, Soft);

        columns = Node("Columnas", panel);
        worksDot = Node("PuntoFunciona", columns);
        Image(worksDot, rounded, Ok, 5f);
        worksHeader = Text(Node("Funciona", columns), labelFont, 20f, Ok, TextAlignmentOptions.MidlineLeft, 12f, true);
        worksText = Paragraph(Node("ListaFunciona", columns), 19f, Soft);
        missingDot = Node("PuntoFalta", columns);
        Image(missingDot, rounded, Accent, 5f);
        missingHeader = Text(Node("Falta", columns), labelFont, 20f, Accent, TextAlignmentOptions.MidlineLeft, 12f, true);
        missingText = Paragraph(Node("ListaFalta", columns), 19f, Soft);

        divider = Node("Division", panel);
        Image(divider, null, LineColor);
        controlsLabel = Node("RotuloControles", panel);
        Text(controlsLabel, labelFont, 15f, Mute, TextAlignmentOptions.MidlineLeft, 16f, true).text = "Controles";
        controlsText = Paragraph(Node("Controles", panel), 18f, Note);
        controlsText.font = labelFont;
        controlsText.characterSpacing = 4f;

        // Entendido
        button = Node("Entendido", panel);
        Img buttonBg = Image(button, rounded, Accent, 6f, true);
        Text(Stretch(Node("Texto", button)), displayFont, 22f, KeyInk, TextAlignmentOptions.Center, 10f, true).text = "Entendido";
        ShopPointerTarget pointer = button.gameObject.AddComponent<ShopPointerTarget>();
        pointer.Hovered = () => buttonBg.color = Hot;
        pointer.Exited = () => buttonBg.color = Accent;
        pointer.Clicked = () => { pointer.Exited(); Close(); };

        keysHint = Node("Atajos", panel);
        Text(keysHint, labelFont, 16f, Mute, TextAlignmentOptions.MidlineLeft, 10f, true)
            .text = "<color=#F3F4F6>Enter</color>  o  <color=#F3F4F6>Esc</color>  Cerrar";
    }

    private TextMeshProUGUI Paragraph(RectTransform rect, float size, Color color)
    {
        TextMeshProUGUI text = Text(rect, bodyFont, size, color, TextAlignmentOptions.TopLeft);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.paragraphSpacing = 12f;
        text.lineSpacing = 4f;
        return text;
    }
}
