using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using static ShopUIKit;
using Img = UnityEngine.UI.Image;

// Elegir nombre de jugador (US 164). Va en la escena MenuPrincipal.
// - CA1: si no hay nombre guardado, al abrir el juego aparece "¿Cómo te llamás?" antes del menú.
// - CA2: de 3 a 16 caracteres (letras, números, espacio, guion y guion bajo); si no cumple, dice qué falta.
// - CA3: arriba a la derecha del menú principal se ve el nombre; al hacer clic se cambia con la misma ventana.
// - CA4: se guarda con PlayerProfile (hoy PlayerPrefs; después, el guardado de la US 162).
// Se arma por código con las medidas de una pantalla de 1920 x 1080, como la tienda y la pausa.
public class PlayerNameScreen : MonoBehaviour
{
    [Header("Menú principal")]
    [Tooltip("El nombre se ve solo mientras este panel está activo. Si queda vacío, se busca PanelMenuPrincipal.")]
    [SerializeField] private GameObject mainMenuPanel;
    [Tooltip("El fundido negro de MenuUIController, para que el nombre se oscurezca con el menú. Si queda vacío, se busca OverlayTransicion.")]
    [SerializeField] private CanvasGroup transitionOverlay;

    [Header("Tipografías (las mismas de la tienda)")]
    [SerializeField] private TMP_FontAsset displayFont; // Barlow Condensed Bold
    [SerializeField] private TMP_FontAsset labelFont;   // Barlow Condensed SemiBold
    [SerializeField] private TMP_FontAsset bodyFont;    // Barlow Regular

    [Header("Sprites")]
    [SerializeField] private Sprite rounded;

    public static bool IsOpen { get; private set; }

    private const float PanelW = 760f, PanelH = 470f, Pad = 48f;
    private const string Hint = "De 3 a 16 caracteres: letras, números, espacio, guion y guion bajo.";

    // Se calculan en Awake: ShopUIKit consulta el espacio de color y Unity no deja hacerlo al crear el componente.
    private Color fieldColor, okColor, chipHover;

    private RectTransform canvasRoot, chip, modal, panel, cancelRect, keysRect;
    private CanvasGroup chipGroup;
    private Img chipBg, acceptButton;
    private TextMeshProUGUI chipLabel, chipName, avatarLetter, eyebrow, message, counter, acceptLabel, keysText;
    private TMP_InputField input;

    private bool firstTime, attempted;
    private Coroutine shakeRoutine;

    // =====================================================================
    // Ciclo de vida
    // =====================================================================

    private void Awake()
    {
        fieldColor = new Color(0f, 0f, 0f, DarkAlpha(0.5f));
        okColor = Ok;
        chipHover = Over(White(0.08f), PanelBase);
        BuildCanvas();
        modal.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

        if (mainMenuPanel == null) mainMenuPanel = FindInScene("PanelMenuPrincipal");
        if (transitionOverlay == null)
        {
            GameObject overlay = FindInScene("OverlayTransicion");
            if (overlay != null) transitionOverlay = overlay.GetComponent<CanvasGroup>();
        }

        PlayerProfile.NameChanged += RefreshChip;
        RefreshChip(PlayerProfile.Name);

        // CA1: la primera vez se pide el nombre antes de usar el menú.
        if (!PlayerProfile.HasName) Open(true);
    }

    private void OnDestroy()
    {
        PlayerProfile.NameChanged -= RefreshChip;
        IsOpen = false;
    }

    private void Update()
    {
        bool showChip = !IsOpen && PlayerProfile.HasName && (mainMenuPanel == null || mainMenuPanel.activeInHierarchy);
        if (chip.gameObject.activeSelf != showChip) chip.gameObject.SetActive(showChip);
        if (showChip && transitionOverlay != null) chipGroup.alpha = 1f - transitionOverlay.alpha;

        if (!IsOpen) return;

        if (Input.GetKeyDown(KeyCode.Escape) && !firstTime) { Close(); return; }

        // Si se hizo clic afuera del campo, cualquier tecla vuelve a escribir en él.
        if (!input.isFocused && Input.anyKeyDown && !Input.GetMouseButtonDown(0)) input.ActivateInputField();
    }

    // =====================================================================
    // Abrir, aceptar, cerrar
    // =====================================================================

    /// <summary>Abre la ventana. firstTime: no se puede cancelar (CA1).</summary>
    public void Open(bool isFirstTime = false)
    {
        firstTime = isFirstTime || !PlayerProfile.HasName;
        attempted = false;
        IsOpen = true;

        eyebrow.text = firstTime ? "Project Riftwalker · Jugador" : "Cambiar nombre";
        cancelRect.gameObject.SetActive(!firstTime);
        keysText.text = firstTime ? "<color=#F3F4F6>Enter</color>  Aceptar" : "<color=#F3F4F6>Enter</color>  Aceptar     <color=#F3F4F6>Esc</color>  Cancelar";

        input.SetTextWithoutNotify(PlayerProfile.Name);
        UpdateValidation(input.text);
        modal.gameObject.SetActive(true);
        StartCoroutine(FocusNextFrame());
    }

    public void Close()
    {
        IsOpen = false;
        modal.gameObject.SetActive(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private void Submit()
    {
        attempted = true;
        if (PlayerProfile.TrySetName(input.text, out string error))
        {
            Close();
            return;
        }
        ShowMessage(error, Bad);
        Shake();
        input.ActivateInputField();
    }

    private IEnumerator FocusNextFrame()
    {
        yield return null;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(input.gameObject);
        input.ActivateInputField();
    }

    // CA2: mientras escribe se ve cuántos caracteres lleva y, si algo no sirve, qué falta.
    private void UpdateValidation(string text)
    {
        string clean = PlayerProfile.Clean(text);
        counter.text = clean.Length + "/" + PlayerProfile.MaxLength;
        counter.color = clean.Length > PlayerProfile.MaxLength ? Bad : Mute;

        string error = PlayerProfile.Validate(text);
        bool valid = error == null;

        if (clean.Length == 0 && !attempted) ShowMessage(Hint, Mute);
        else if (valid) ShowMessage("Así te van a ver los demás jugadores.", okColor);
        else
        {
            // Un carácter no permitido se marca en rojo enseguida; lo que falta, en gris hasta que intente aceptar.
            bool badChar = error.StartsWith("No se puede");
            ShowMessage(error, badChar || attempted ? Bad : Mute);
        }

        acceptButton.color = valid ? Accent : ChipColor;
        acceptLabel.color = valid ? KeyInk : Mute;
    }

    private void ShowMessage(string text, Color color)
    {
        message.text = text;
        message.color = color;
    }

    private void Shake()
    {
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeAnimation());
    }

    private IEnumerator ShakeAnimation()
    {
        Vector2 basePos = panel.anchoredPosition;
        for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
        {
            panel.anchoredPosition = basePos + new Vector2(Mathf.Sin(t * 60f) * 8f * (1f - t / 0.3f), 0f);
            yield return null;
        }
        panel.anchoredPosition = basePos;
    }

    // CA3: el nombre actual arriba a la derecha del menú principal.
    private void RefreshChip(string name)
    {
        chipName.text = name;
        avatarLetter.text = string.IsNullOrEmpty(name) ? "" : name.Substring(0, 1).ToUpperInvariant();
        float textW = Mathf.Max(Width(chipName, name), Width(chipLabel, "Cambiar nombre"));
        chip.sizeDelta = new Vector2(80f + textW + 26f, 76f);
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
        GameObject canvasObject = new GameObject("NombreJugadorCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        canvasObject.layer = 5;
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // por encima del menú
        var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        canvasRoot = (RectTransform)canvasObject.transform;

        BuildChip();
        BuildModal();
    }

    private void BuildChip()
    {
        chip = Node("NombreActual", canvasRoot);
        chip.anchorMin = chip.anchorMax = chip.pivot = new Vector2(1f, 1f);
        chip.anchoredPosition = new Vector2(-48f, -40f);
        chip.sizeDelta = new Vector2(260f, 76f);
        chipGroup = chip.gameObject.AddComponent<CanvasGroup>();
        chipBg = Image(chip, rounded, PanelColor, 8f, true);

        Img avatar = Image(Place(Node("Inicial", chip), 14f, 14f, 48f, 48f), rounded, Accent, 24f);
        avatarLetter = Text(Stretch(Node("Letra", avatar.rectTransform)), displayFont, 28f, KeyInk, TextAlignmentOptions.Center);

        chipLabel = Text(Place(Node("Rotulo", chip), 80f, 14f, 300f, 18f), labelFont, 14f, Mute, TextAlignmentOptions.MidlineLeft, 16f, true);
        chipLabel.text = "Jugador";
        chipName = Text(Place(Node("Nombre", chip), 80f, 30f, 300f, 34f), displayFont, 30f, Ink, TextAlignmentOptions.MidlineLeft, 3f, true);

        ShopPointerTarget pointer = chip.gameObject.AddComponent<ShopPointerTarget>();
        pointer.Hovered = () => { chipBg.color = chipHover; chipLabel.text = "Cambiar nombre"; chipLabel.color = Accent; };
        pointer.Exited = () => { chipBg.color = PanelColor; chipLabel.text = "Jugador"; chipLabel.color = Mute; };
        pointer.Clicked = () => { pointer.Exited(); Open(false); };
    }

    private void BuildModal()
    {
        modal = Stretch(Node("Ventana", canvasRoot));
        Image(Stretch(Node("Oscurecido", modal)), null, new Color(0f, 0f, 0f, DarkAlpha(0.72f)), 0f, true);

        panel = Node("Panel", modal);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(PanelW, PanelH);
        Image(panel, rounded, BarColor, 10f, true).gameObject.AddComponent<ShopPointerTarget>().Clicked = () => input.ActivateInputField();

        float w = PanelW - Pad * 2f;
        eyebrow = Text(Place(Node("Rotulo", panel), Pad, 44f, w, 20f), labelFont, 16f, Accent, TextAlignmentOptions.MidlineLeft, 16f, true);
        Text(Place(Node("Titulo", panel), Pad, 70f, w, 66f), displayFont, 62f, Ink, TextAlignmentOptions.MidlineLeft, 2f, true).text = "¿Cómo te llamás?";
        Image(Place(Node("LineaAnden", panel), Pad, 146f, 150f, 3f), null, Accent);
        Text(Place(Node("Bajada", panel), Pad, 166f, w, 28f), bodyFont, 20f, Soft, TextAlignmentOptions.MidlineLeft)
            .text = "Así te van a ver en las salas, la tabla y los avisos de bajas.";

        BuildInput(w);

        message = Text(Place(Node("Mensaje", panel), Pad, 318f, w, 52f), bodyFont, 19f, Mute, TextAlignmentOptions.TopLeft);
        message.textWrappingMode = TextWrappingModes.Normal;

        // Botones
        RectTransform acceptRect = Place(Node("Aceptar", panel), PanelW - Pad - 200f, 386f, 200f, 56f);
        acceptButton = Image(acceptRect, rounded, Accent, 6f, true);
        acceptLabel = Text(Stretch(Node("Texto", acceptRect)), displayFont, 22f, KeyInk, TextAlignmentOptions.Center, 10f, true);
        acceptLabel.text = "Aceptar";
        acceptRect.gameObject.AddComponent<ShopPointerTarget>().Clicked = Submit;

        cancelRect = Place(Node("Cancelar", panel), PanelW - Pad - 200f - 16f - 170f, 386f, 170f, 56f);
        Img cancelBg = Image(cancelRect, rounded, ChipColor, 6f, true);
        Text(Stretch(Node("Texto", cancelRect)), displayFont, 22f, Ink, TextAlignmentOptions.Center, 10f, true).text = "Cancelar";
        ShopPointerTarget cancelPointer = cancelRect.gameObject.AddComponent<ShopPointerTarget>();
        cancelPointer.Hovered = () => cancelBg.color = ChipDim;
        cancelPointer.Exited = () => cancelBg.color = ChipColor;
        cancelPointer.Clicked = Close;

        keysRect = Place(Node("Atajos", panel), Pad, 386f, 220f, 56f);
        keysText = Text(keysRect, labelFont, 16f, Mute, TextAlignmentOptions.MidlineLeft, 10f, true);
    }

    private void BuildInput(float w)
    {
        RectTransform box = Place(Node("Campo", panel), Pad, 224f, w, 78f);
        box.gameObject.SetActive(false); // TMP_InputField se configura con el objeto apagado
        Image(box, rounded, fieldColor, 8f, true);

        RectTransform area = Stretch(Node("Area", box));
        area.offsetMin = new Vector2(22f, 0f);
        area.offsetMax = new Vector2(-96f, 0f);
        area.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();

        TextMeshProUGUI placeholder = Text(Stretch(Node("Ejemplo", area)), displayFont, 40f, White(0.25f), TextAlignmentOptions.MidlineLeft, 3f);
        placeholder.text = "Tu nombre";
        TextMeshProUGUI value = Text(Stretch(Node("Texto", area)), displayFont, 40f, Ink, TextAlignmentOptions.MidlineLeft, 3f);

        counter = Text(Place(Node("Contador", box), w - 86f, 0f, 66f, 78f), labelFont, 18f, Mute, TextAlignmentOptions.MidlineRight, 6f);

        input = box.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = area;
        input.textComponent = value;
        input.placeholder = placeholder;
        input.fontAsset = displayFont;
        input.pointSize = 40f;
        input.characterLimit = PlayerProfile.MaxLength;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.customCaretColor = true;
        input.caretColor = Accent;
        input.caretWidth = 3;
        input.selectionColor = WithAlpha(Accent, 0.35f);
        input.onValueChanged.AddListener(UpdateValidation);
        input.onSubmit.AddListener(_ => Submit());
        box.gameObject.SetActive(true);
    }
}
