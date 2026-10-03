using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static ShopUIKit;
using Img = UnityEngine.UI.Image;
using Slider = UnityEngine.UI.Slider;

// Pestaña Controles de las opciones (US 155). Va como hijo de PanelControles, en la escena MenuPrincipal:
// se estira al tamaño del panel y tapa lo que había antes.
// - CA1: sensibilidad del mouse (0,1 a 5) y sensibilidad al apuntar con mira.
// - CA2: invertir el eje vertical.
// - CA3: todas las acciones con su tecla actual.
// - CA4: clic en una acción y se aprieta la tecla nueva; Esc cancela.
// - CA5: si la tecla ya la usa otra acción, avisa y pregunta si se intercambian.
// - CA6: se guarda en PlayerPrefs y el juego lee las teclas desde KeyBindings.
// Dentro de las opciones del menú principal (US 048) los cambios quedan pendientes hasta "Aplicar", y
// "Restablecer" pide confirmación. En la pausa se guardan al instante, como antes.
public class ControlsPanel : MonoBehaviour, OpcionesPantalla.ISeccion
{
    [Header("Tipografías (las mismas de la tienda)")]
    [SerializeField] private TMP_FontAsset displayFont; // Barlow Condensed Bold
    [SerializeField] private TMP_FontAsset labelFont;   // Barlow Condensed SemiBold
    [SerializeField] private TMP_FontAsset bodyFont;    // Barlow Regular

    [Header("Sprites")]
    [SerializeField] private Sprite rounded;

    // Medidas en unidades del panel (PanelControles mide 600 de ancho).
    private const float Pad = 40f, RowH = 44f, KeyRowH = 40f, KeyGap = 4f, ColGap = 16f;

    private class KeyRow
    {
        public GameAction action;
        public Img bg, chip;
        public TextMeshProUGUI label, key;
        public bool hovered;
    }

    private readonly List<KeyRow> rows = new List<KeyRow>();
    private static KeyCode[] assignable;

    // Se calculan en Awake: ShopUIKit consulta el espacio de color y Unity no deja hacerlo al crear el componente.
    private Color panelColor, rowColor, rowHover, clear;

    private RectTransform root, conflictBar;
    private Slider sensitivity, aimSensitivity;
    private TextMeshProUGUI sensitivityValue, aimValue, conflictText, savedText;
    private Img[] invertSegs = new Img[2];
    private TextMeshProUGUI[] invertLabels = new TextMeshProUGUI[2];

    private GameAction? waiting;
    private int waitingSince;
    private GameAction pendingAction, pendingOther;
    private bool conflictOpen;
    private float savedUntil;

    public static bool IsCapturing { get; private set; }

    /// <summary>Esperando una tecla o preguntando si se intercambian: Esc y Retroceso son de este panel.</summary>
    public static bool IsBusy => IsCapturing || conflictShown;
    private static bool conflictShown;

    private System.Action onBack;

    // Opciones del menú principal (US 048): null en la pausa.
    private OpcionesPantalla pantalla;

    // Teclas y mouse como estaban la última vez que se aplicó, para descartar cambios.
    private class Foto
    {
        public readonly Dictionary<GameAction, KeyCode> teclas = new Dictionary<GameAction, KeyCode>();
        public float sensibilidad, apuntar;
        public bool invertir;

        public static Foto Actual()
        {
            Foto f = new Foto { sensibilidad = KeyBindings.Sensitivity, apuntar = KeyBindings.AimSensitivity, invertir = KeyBindings.InvertY };
            foreach (GameAction a in KeyBindings.All) f.teclas[a] = KeyBindings.Key(a);
            return f;
        }

        public bool IgualA(Foto o)
        {
            if (!Mathf.Approximately(sensibilidad, o.sensibilidad) || !Mathf.Approximately(apuntar, o.apuntar) || invertir != o.invertir) return false;
            foreach (var par in teclas) if (!o.teclas.TryGetValue(par.Key, out KeyCode k) || k != par.Value) return false;
            return true;
        }

        public void Restaurar()
        {
            foreach (var par in teclas) if (KeyBindings.Key(par.Key) != par.Value) KeyBindings.Set(par.Key, par.Value);
            KeyBindings.Sensitivity = sensibilidad;
            KeyBindings.AimSensitivity = apuntar;
            KeyBindings.InvertY = invertir;
        }
    }
    private Foto aplicado;

    // Para que las opciones usen las mismas tipografías en su ventana de confirmación.
    public TMP_FontAsset DisplayFont => displayFont;
    public TMP_FontAsset LabelFont => labelFont;
    public TMP_FontAsset BodyFont => bodyFont;
    public Sprite Rounded => rounded;

    /// <summary>
    /// Para usarlo fuera del menú principal (la pausa): se llama con el objeto apagado, antes de que arranque.
    /// onBack agrega un botón Volver al pie.
    /// </summary>
    public void Setup(TMP_FontAsset display, TMP_FontAsset label, TMP_FontAsset body, Sprite roundedSprite, System.Action back)
    {
        displayFont = display;
        labelFont = label;
        bodyFont = body;
        rounded = roundedSprite;
        onBack = back;
    }

    // =====================================================================
    // Ciclo de vida
    // =====================================================================

    private void Awake()
    {
        panelColor = Rgb(10, 12, 17, DarkAlpha(0.97f));
        rowColor = Over(White(0.04f), PanelBase);
        rowHover = Over(White(0.08f), PanelBase);
        clear = new Color(0f, 0f, 0f, 0f);

        if (assignable == null)
        {
            var keys = new List<KeyCode>();
            foreach (KeyCode k in System.Enum.GetValues(typeof(KeyCode)))
                if (KeyBindings.CanAssign(k) && !keys.Contains(k)) keys.Add(k);
            assignable = keys.ToArray();
        }

        pantalla = OpcionesPantalla.De(this);
        if (pantalla != null)
        {
            pantalla.Registrar(this);
            aplicado = Foto.Actual();
        }

        // En las opciones del menú, PanelControles viene estirado distinto a lo ancho y a lo alto: se corrige para
        // que las letras no salgan deformadas (igual que Gráficos y Sonido).
        if (pantalla != null) OpcionesKit.SinDeformar(transform.parent as RectTransform, OpcionesKit.AltoControles);

        // Estirado al tamaño de PanelControles y dibujado encima de lo que tenga.
        RectTransform self = transform as RectTransform;
        if (self == null) self = gameObject.AddComponent<RectTransform>();
        Stretch(self);
        transform.SetAsLastSibling();
        Build(self);
    }

    private void OnEnable()
    {
        if (root != null) RefreshAll();
    }

    private void OnDisable()
    {
        CancelCapture();
        CloseConflict();
        Guardar();
    }

    private void Update()
    {
        if (savedText != null) savedText.alpha = Time.unscaledTime < savedUntil ? 1f : 0f;

        if (conflictOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) CloseConflict();
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) ConfirmSwap();
            return;
        }

        if (waiting == null || Time.frameCount <= waitingSince) return;

        // CA4: Esc cancela el cambio.
        if (Input.GetKeyDown(KeyCode.Escape)) { CancelCapture(); return; }

        foreach (KeyCode key in assignable)
        {
            if (!Input.GetKeyDown(key)) continue;
            AssignKey(waiting.Value, key);
            return;
        }
    }

    // =====================================================================
    // Teclas
    // =====================================================================

    private void StartCapture(GameAction action)
    {
        if (conflictOpen) return;
        waiting = action;
        waitingSince = Time.frameCount;
        IsCapturing = true;
        RefreshRows();
    }

    private void CancelCapture()
    {
        waiting = null;
        IsCapturing = false;
        if (root != null) RefreshRows();
    }

    private void AssignKey(GameAction action, KeyCode key)
    {
        waiting = null;
        IsCapturing = false;

        if (KeyBindings.Key(action) == key) { RefreshRows(); return; }

        // CA5: la tecla ya la usa otra acción.
        GameAction? other = KeyBindings.ActionUsing(key, action);
        if (other != null)
        {
            pendingAction = action;
            pendingOther = other.Value;
            conflictText.text = $"<color=#F3F4F6>{KeyBindings.Label(key)}</color> ya está en " +
                                $"<color=#F3F4F6>{KeyBindings.Name(other.Value)}</color>. ¿Querés intercambiarlas? " +
                                $"{KeyBindings.Name(other.Value)} pasaría a {KeyBindings.Label(action)}.";
            conflictOpen = true;
            conflictShown = true;
            conflictBar.gameObject.SetActive(true);
            RefreshRows();
            return;
        }

        KeyBindings.Set(action, key);
        Saved();
    }

    private void ConfirmSwap()
    {
        KeyBindings.Swap(pendingAction, pendingOther);
        CloseConflict();
        Saved();
    }

    private void CloseConflict()
    {
        conflictOpen = false;
        conflictShown = false;
        if (conflictBar != null) conflictBar.gameObject.SetActive(false);
        if (root != null) RefreshRows();
    }

    // US 048, CA4: en las opciones del menú, Restablecer pide confirmación antes.
    private void PedirRestablecer()
    {
        if (pantalla == null) { ResetAll(); return; }
        CancelCapture();
        CloseConflict();
        pantalla.ConfirmarRestablecer("los controles", () => { ResetAll(); aplicado = Foto.Actual(); });
    }

    // =====================================================================
    // US 048, CA3: aplicar o descartar (solo en las opciones del menú)
    // =====================================================================

    public bool HayCambios => pantalla != null && aplicado != null && !Foto.Actual().IgualA(aplicado);

    public void Aplicar()
    {
        CancelCapture();
        CloseConflict();
        KeyBindings.Save();
        aplicado = Foto.Actual();
    }

    public void Descartar()
    {
        if (aplicado == null) return;
        CancelCapture();
        CloseConflict();
        aplicado.Restaurar();
        KeyBindings.Save();
        if (root != null) RefreshAll();
    }

    private void ResetAll()
    {
        CancelCapture();
        CloseConflict();
        KeyBindings.ResetDefaults();
        RefreshAll();
        Saved();
    }

    // En la pausa se guarda al instante; en las opciones del menú, recién con "Aplicar" (US 048, CA3).
    private void Guardar()
    {
        if (pantalla == null) KeyBindings.Save();
    }

    private void Saved()
    {
        if (pantalla != null) { RefreshAll(); return; }
        KeyBindings.Save();
        savedUntil = Time.unscaledTime + 1.2f;
        RefreshAll();
    }

    // =====================================================================
    // Mostrar
    // =====================================================================

    private void RefreshAll()
    {
        sensitivity.SetValueWithoutNotify(KeyBindings.Sensitivity);
        aimSensitivity.SetValueWithoutNotify(KeyBindings.AimSensitivity);
        sensitivityValue.text = Number(KeyBindings.Sensitivity);
        aimValue.text = "×" + KeyBindings.AimSensitivity.ToString("0.00").Replace('.', ',');
        RefreshInvert();
        RefreshRows();
    }

    private void RefreshInvert()
    {
        bool invert = KeyBindings.InvertY;
        for (int i = 0; i < 2; i++)
        {
            bool on = (i == 0) == invert;
            invertSegs[i].color = on ? Ink : ChipColor;
            invertLabels[i].color = on ? KeyInk : Mute;
        }
    }

    private void RefreshRows()
    {
        foreach (KeyRow row in rows)
        {
            bool isWaiting = waiting == row.action;
            bool isConflict = conflictOpen && (row.action == pendingAction || row.action == pendingOther);
            row.bg.color = isWaiting || isConflict ? SelectedColor : row.hovered ? rowHover : rowColor;
            row.key.text = isWaiting ? "Apretá una tecla" : KeyBindings.Label(row.action);
            row.key.color = isWaiting ? Accent : Ink;
            row.chip.color = isWaiting ? clear : isConflict ? Accent : ChipDim;
            if (isConflict && !isWaiting) row.key.color = KeyInk;
        }
    }

    // =====================================================================
    // Armado
    // =====================================================================

    private void Build(RectTransform parent)
    {
        root = Stretch(Node("Controles", parent));
        Image(root, rounded, panelColor, 10f, true);
        float w = parent.rect.width > 0f ? parent.rect.width : 600f;
        float inner = w - Pad * 2f;

        Text(Place(Node("Titulo", root), Pad, 36f, inner, 50f), displayFont, 46f, Ink, TextAlignmentOptions.MidlineLeft, 2f, true).text = "Controles";
        Image(Place(Node("LineaAnden", root), Pad, 92f, 120f, 3f), null, Accent);

        // ---- Mouse (CA1, CA2) ----
        float y = 114f;
        Group("Mouse", ref y);
        sensitivity = SliderRow("Sensibilidad", ref y, w, KeyBindings.MinSensitivity, KeyBindings.MaxSensitivity, out sensitivityValue);
        sensitivity.onValueChanged.AddListener(v => { KeyBindings.Sensitivity = v; sensitivityValue.text = Number(v); Guardar(); });
        aimSensitivity = SliderRow("Al apuntar", ref y, w, KeyBindings.MinAimSensitivity, KeyBindings.MaxAimSensitivity, out aimValue);
        aimSensitivity.onValueChanged.AddListener(v => { KeyBindings.AimSensitivity = v; aimValue.text = "×" + v.ToString("0.00").Replace('.', ','); Guardar(); });
        InvertRow(ref y);

        // ---- Teclas (CA3, CA4) ----
        y += 10f;
        Group("Teclas", ref y);
        TextMeshProUGUI hint = Text(Place(Node("Ayuda", root), Pad, y, inner, 22f), bodyFont, 16f, Mute, TextAlignmentOptions.MidlineLeft);
        hint.text = "Hacé clic en una acción y apretá la tecla nueva. Esc cancela.";
        y += 32f;

        List<GameAction> actions = new List<GameAction>(KeyBindings.All);
        int perColumn = Mathf.CeilToInt((actions.Count + 1) / 2f); // +1: Pausa, fija
        float colW = (inner - ColGap) / 2f;
        for (int i = 0; i <= actions.Count; i++)
        {
            int col = i / perColumn, line = i % perColumn;
            float x = Pad + col * (colW + ColGap);
            float ry = y + line * (KeyRowH + KeyGap);
            if (i < actions.Count) rows.Add(BuildKeyRow(actions[i], x, ry, colW));
            else BuildFixedRow("Pausa", "Esc", x, ry, colW);
        }
        y += perColumn * (KeyRowH + KeyGap) + 12f;

        // ---- Tecla repetida (CA5) ----
        conflictBar = Place(Node("TeclaRepetida", root), Pad, y, inner, 96f);
        Image(conflictBar, rounded, Over(Rgb(242, 154, 56, 0.12f), PanelBase), 6f, true);
        conflictText = Text(Place(Node("Texto", conflictBar), 16f, 10f, inner - 32f, 44f), bodyFont, 16f, Soft, TextAlignmentOptions.TopLeft);
        conflictText.textWrappingMode = TextWrappingModes.Normal;
        Button(conflictBar, 16f, 56f, 140f, 32f, "Intercambiar", true, ConfirmSwap);
        Button(conflictBar, 166f, 56f, 120f, 32f, "Cancelar", false, CloseConflict);
        conflictBar.gameObject.SetActive(false);
        y += 108f;

        // ---- Pie ----
        float footX = Pad;
        if (onBack != null)
        {
            Button(root, footX, y, 130f, 40f, "Volver", false, onBack);
            footX += 146f;
        }
        if (pantalla != null)
        {
            Button(root, footX, y, 150f, 40f, "Aplicar", true, () => { Aplicar(); pantalla.AvisarGuardado(); });
            footX += 166f;
        }
        Button(root, footX, y, 170f, 40f, "Restablecer", false, PedirRestablecer);
        savedText = Text(Place(Node("Guardado", root), footX + 186f, y, 140f, 40f), labelFont, 16f, Ok, TextAlignmentOptions.MidlineLeft, 12f, true);
        savedText.text = "Guardado";
        savedText.alpha = 0f;

        RefreshAll();
    }

    private void Group(string title, ref float y)
    {
        Text(Place(Node("Grupo", root), Pad, y, 300f, 20f), labelFont, 15f, Mute, TextAlignmentOptions.MidlineLeft, 16f, true).text = title;
        y += 24f;
    }

    private Slider SliderRow(string title, ref float y, float w, float min, float max, out TextMeshProUGUI value)
    {
        Text(Place(Node(title, root), Pad, y, 190f, RowH), labelFont, 19f, Ink, TextAlignmentOptions.MidlineLeft, 5f, true).text = title;
        value = Text(Place(Node("Valor", root), w - Pad - 60f, y, 60f, RowH), labelFont, 18f, Ink, TextAlignmentOptions.MidlineRight);
        Slider slider = BuildSlider(root, Pad + 190f, y + (RowH - 26f) / 2f, w - Pad * 2f - 190f - 76f, min, max);
        y += RowH;
        return slider;
    }

    private void InvertRow(ref float y)
    {
        Text(Place(Node("InvertirY", root), Pad, y, 190f, RowH), labelFont, 19f, Ink, TextAlignmentOptions.MidlineLeft, 5f, true).text = "Invertir eje Y";
        string[] names = { "Sí", "No" };
        for (int i = 0; i < 2; i++)
        {
            bool value = i == 0;
            RectTransform seg = Place(Node(names[i], root), Pad + 190f + i * 64f, y + (RowH - 30f) / 2f, 60f, 30f);
            invertSegs[i] = Image(seg, rounded, ChipColor, 3f, true);
            invertLabels[i] = Text(Stretch(Node("Texto", seg)), labelFont, 16f, Mute, TextAlignmentOptions.Center, 8f, true);
            invertLabels[i].text = names[i];
            seg.gameObject.AddComponent<ShopPointerTarget>().Clicked = () => { KeyBindings.InvertY = value; Guardar(); RefreshInvert(); };
        }
        y += RowH;
    }

    private KeyRow BuildKeyRow(GameAction action, float x, float y, float w)
    {
        RectTransform rect = Place(Node(action.ToString(), root), x, y, w, KeyRowH);
        KeyRow row = new KeyRow { action = action };
        row.bg = Image(rect, rounded, rowColor, 4f, true);
        row.label = Text(Place(Node("Accion", rect), 12f, 0f, w - 120f, KeyRowH), labelFont, 17f, Ink, TextAlignmentOptions.MidlineLeft, 4f, true);
        row.label.text = KeyBindings.Name(action);
        // Los nombres largos ("Correr / caminar despacio") se achican para no taparse con la tecla.
        row.label.enableAutoSizing = true;
        row.label.fontSizeMin = 12f;
        row.label.fontSizeMax = 17f;
        row.chip =Image(Place(Node("Tecla", rect), w - 104f, (KeyRowH - 28f) / 2f, 96f, 28f), rounded, ChipDim, 4f);
        row.key = Text(Stretch(Node("Texto", row.chip.rectTransform)), displayFont, 16f, Ink, TextAlignmentOptions.Center, 2f, true);

        ShopPointerTarget pointer = rect.gameObject.AddComponent<ShopPointerTarget>();
        pointer.Hovered = () => { row.hovered = true; RefreshRows(); };
        pointer.Exited = () => { row.hovered = false; RefreshRows(); };
        pointer.Clicked = () => { if (waiting == null) StartCapture(action); };
        return row;
    }

    private void BuildFixedRow(string name, string key, float x, float y, float w)
    {
        RectTransform rect = Place(Node(name, root), x, y, w, KeyRowH);
        Image(rect, rounded, rowColor, 4f);
        Text(Place(Node("Accion", rect), 12f, 0f, w - 120f, KeyRowH), labelFont, 17f, Mute, TextAlignmentOptions.MidlineLeft, 4f, true).text = name;
        Img chip = Image(Place(Node("Tecla", rect), w - 104f, (KeyRowH - 28f) / 2f, 96f, 28f), rounded, ChipColor, 4f);
        Text(Stretch(Node("Texto", chip.rectTransform)), displayFont, 16f, Mute, TextAlignmentOptions.Center, 2f, true).text = key;
    }

    private void Button(RectTransform parent, float x, float y, float w, float h, string label, bool primary, System.Action onClick)
    {
        RectTransform rect = Place(Node(label, parent), x, y, w, h);
        Color normal = primary ? Accent : ChipColor;
        Img bg = Image(rect, rounded, normal, 4f, true);
        Text(Stretch(Node("Texto", rect)), displayFont, 17f, primary ? KeyInk : Ink, TextAlignmentOptions.Center, 8f, true).text = label;
        ShopPointerTarget pointer = rect.gameObject.AddComponent<ShopPointerTarget>();
        pointer.Hovered = () => bg.color = primary ? Hot : ChipDim;
        pointer.Exited = () => bg.color = normal;
        pointer.Clicked = onClick;
    }

    // Slider con forma de vía, como en la pausa: tramo blanco y un durmiente naranja como cursor.
    private static Slider BuildSlider(RectTransform parent, float x, float y, float width, float min, float max)
    {
        RectTransform rect = Place(Node("Slider", parent), x, y, width, 26f);

        RectTransform track = Node("Via", rect);
        track.anchorMin = new Vector2(0f, 0.5f);
        track.anchorMax = new Vector2(1f, 0.5f);
        track.sizeDelta = new Vector2(0f, 6f);
        Image(track, null, TrackColor, 0f, true);

        RectTransform fillArea = Node("Relleno", rect);
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.sizeDelta = new Vector2(0f, 6f);
        RectTransform fill = Node("Tramo", fillArea);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.sizeDelta = Vector2.zero;
        Image(fill, null, Ink);

        RectTransform handleArea = Stretch(Node("AreaCursor", rect));
        RectTransform handle = Node("Durmiente", handleArea);
        handle.anchorMin = handle.anchorMax = new Vector2(0f, 0.5f);
        handle.sizeDelta = new Vector2(7f, 20f);
        Img handleImage = Image(handle, null, Accent, 0f, true);

        Slider slider = rect.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.transition = UnityEngine.UI.Selectable.Transition.None;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        return slider;
    }
}
