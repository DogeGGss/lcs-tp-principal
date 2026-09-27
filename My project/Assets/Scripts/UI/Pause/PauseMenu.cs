using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UIImage = UnityEngine.UI.Image;
using Slider = UnityEngine.UI.Slider;
using Selectable = UnityEngine.UI.Selectable;
using Navigation = UnityEngine.UI.Navigation;
using CanvasScaler = UnityEngine.UI.CanvasScaler;
using GraphicRaycaster = UnityEngine.UI.GraphicRaycaster;
using static ShopUIKit;

// Menú de pausa (US 051). Se abre con Esc durante la partida y tiene Reanudar, Configuración y Salir.
// En las partidas locales (Zombie) también tiene Reiniciar partida, con confirmación (US 052).
// La interfaz se arma por código, igual que la tienda, con medidas de una pantalla de 1920 x 1080.
//
// Mientras no haya multijugador, la pausa congela el juego (Time.timeScale = 0). Cuando exista el online,
// se marca isMultiplayer en Táctico y Deathmatch: el menú se ve solo en esta pantalla y la partida sigue (CA4).
//
// La configuración usa las mismas claves de PlayerPrefs y los mismos parámetros del AudioMixer que las
// opciones del menú principal (SonidoUIController, SensibilidadUIController y GraficosUIController),
// así lo que se cambia en un lado aparece en el otro.
//
// Corre antes que la tienda: si la tienda está abierta, el Esc la cierra a ella y no abre la pausa.
[DefaultExecutionOrder(-50)]
public class PauseMenu : MonoBehaviour
{
    public enum Panel { None, Main, Settings, Exit, Restart }

    [Header("Partida")]
    [SerializeField] private string mainMenuScene = "MenuPrincipal";
    [Tooltip("Dejalo apagado mientras no haya multijugador: la pausa congela el juego.")]
    [SerializeField] private bool isMultiplayer = false;
    [Tooltip("Si la ventana pierde el foco (Alt+Tab), se abre la pausa.")]
    [SerializeField] private bool pauseOnFocusLoss = true;

    [Header("Audio")]
    [Tooltip("El mismo mixer de las opciones del menú (NewAudioMixer).")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("Tipografías (las mismas de la tienda)")]
    [SerializeField] private TMP_FontAsset displayFont; // Barlow Condensed Bold
    [SerializeField] private TMP_FontAsset labelFont;   // Barlow Condensed SemiBold
    [SerializeField] private TMP_FontAsset bodyFont;    // Barlow Regular

    [Header("Sprites")]
    [Tooltip("El sprite redondeado de la tienda. Si queda vacío, los bordes son rectos.")]
    [SerializeField] private Sprite rounded;

    public static PauseMenu Instance { get; private set; }
    public static bool IsPaused { get; private set; }
    public static event System.Action<bool> PauseChanged;

    // El GameManager lo apaga durante cargas, cuentas regresivas o la pantalla de fin.
    public bool AllowPause { get; set; } = true;
    public Panel Current { get; private set; } = Panel.None;

    // ---------- Claves compartidas con las opciones del menú principal ----------
    private const string KeyGeneral = "VolumenGeneral", KeyMusic = "VolumenMusica", KeyEffects = "VolumenEfectos";
    private const string KeySensitivity = "Sensibilidad";
    private const string KeyFov = "FOV";
    private const string KeyScreenMode = "Graficos_ModoPantalla";
    private const float DefaultVolume = 1f, DefaultSensitivity = 1.5f, DefaultFov = 80f;
    private const int DefaultScreenMode = 2; // 0 ventana, 1 ventana sin bordes, 2 pantalla completa

    // ---------- Medidas (px en 1920 x 1080) ----------
    private const float Col1W = 480f, Col2W = 590f, PadX = 45f, PadTop = 60f;
    private const float OptionH = 84f, OptionGap = 6f, SetRowH = 57f;

    // Se calculan en Awake: ShopUIKit consulta el espacio de color y Unity no deja hacerlo
    // mientras se construye el componente (en un inicializador de campo).
    private Color col2Color, badSelected;
    private static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

    // Opciones del menú. Reiniciar solo aparece en partidas locales (US 052).
    private const int OptResume = 0, OptRestart = 1, OptSettings = 2, OptExit = 3;
    private readonly List<int> visibleOptions = new List<int>();

    private class Option
    {
        public UIImage bg, chip;
        public RectTransform rect;
        public TextMeshProUGUI key, label;
        public bool danger;
    }

    private class SettingRow
    {
        public UIImage bg;
        public Slider slider;
        public TextMeshProUGUI value;
        public float step;
        public UIImage[] segs;
        public TextMeshProUGUI[] segLabels;
    }

    private RectTransform canvasRoot, root, col1, colSettings, colExit;
    private CanvasGroup settingsGroup, exitGroup;
    private UIImage shade, statusChip, statusDot;
    private TextMeshProUGUI metaText, statusText, noteText, exitWarning, savedText;
    private readonly List<Option> options = new List<Option>();
    private readonly List<SettingRow> settingRows = new List<SettingRow>();
    private UIImage exitButton, cancelButton, confirmLine;
    private TextMeshProUGUI exitLabel, cancelLabel, confirmTitle;

    private int selected, settingSelected, exitSelected = 1, screenMode;
    private float savedTimeScale = 1f;
    private readonly List<Behaviour> blocked = new List<Behaviour>();
    private Coroutine savedRoutine;

    // =====================================================================
    // Ciclo de vida
    // =====================================================================

    private void Awake()
    {
        Instance = this;
        IsPaused = false;
        col2Color = Rgb(18, 21, 28, DarkAlpha(0.92f));
        badSelected = Over(Rgb(255, 92, 92, 0.13f), PanelBase);
        BuildCanvas();
        root.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // El AudioMixer solo acepta SetFloat desde Start: se aplica el volumen guardado desde el primer frame.
        ApplyVolume(KeyGeneral, PlayerPrefs.GetFloat(KeyGeneral, DefaultVolume));
        ApplyVolume(KeyMusic, PlayerPrefs.GetFloat(KeyMusic, DefaultVolume));
        ApplyVolume(KeyEffects, PlayerPrefs.GetFloat(KeyEffects, DefaultVolume));
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        if (IsPaused) RestorePlayerControls();
        Instance = null;
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && pauseOnFocusLoss && !IsPaused && AllowPause && !ShopUI.IsOpen) Pause();
    }

    // =====================================================================
    // Teclado
    // =====================================================================

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!IsPaused)
            {
                // La tienda corre después y se cierra sola con este mismo Esc.
                if (!ShopUI.IsOpen && AllowPause) Pause();
            }
            else if (Current == Panel.Main) Resume();
            else Back();
            return;
        }

        if (!IsPaused) return;

        switch (Current)
        {
            case Panel.Main: MainKeys(); break;
            case Panel.Settings: SettingsKeys(); break;
            case Panel.Exit:
            case Panel.Restart: ExitKeys(); break;
        }
    }

    private void MainKeys()
    {
        int count = visibleOptions.Count;
        for (int i = 0; i < count; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) { Activate(i); return; }

        if (Input.GetKeyDown(KeyCode.DownArrow)) { selected = (selected + 1) % count; Render(); }
        else if (Input.GetKeyDown(KeyCode.UpArrow)) { selected = (selected + count - 1) % count; Render(); }
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.RightArrow))
            Activate(selected);
    }

    private void SettingsKeys()
    {
        if (Input.GetKeyDown(KeyCode.Backspace)) { Back(); return; }
        if (Input.GetKeyDown(KeyCode.DownArrow)) { settingSelected = Mathf.Min(settingRows.Count - 1, settingSelected + 1); Render(); }
        else if (Input.GetKeyDown(KeyCode.UpArrow)) { settingSelected = Mathf.Max(0, settingSelected - 1); Render(); }
        else if (Input.GetKeyDown(KeyCode.RightArrow)) Adjust(settingRows[settingSelected], 1);
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) Adjust(settingRows[settingSelected], -1);
    }

    private void ExitKeys()
    {
        if (Input.GetKeyDown(KeyCode.Backspace)) { Back(); return; }
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.Tab))
        {
            exitSelected = 1 - exitSelected;
            Render();
        }
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (exitSelected == 0) Confirm(); else Back();
        }
    }

    // =====================================================================
    // Acciones
    // =====================================================================

    // CA1: abre el menú durante una partida en curso.
    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;

        if (!isMultiplayer)
        {
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        BlockPlayerControls();
        selected = 0;
        LayoutOptions();
        RefreshTexts();
        root.gameObject.SetActive(true);
        Show(Panel.Main);
        PauseChanged?.Invoke(true);
    }

    // CA3: vuelve a la partida.
    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;

        if (!isMultiplayer)
        {
            Time.timeScale = savedTimeScale;
            AudioListener.pause = false;
        }

        PlayerPrefs.Save();
        Show(Panel.None);
        root.gameObject.SetActive(false);
        RestorePlayerControls();
        PauseChanged?.Invoke(false);
    }

    public void Back()
    {
        if (Current == Panel.Settings) PlayerPrefs.Save();
        Show(Panel.Main);
    }

    public void LeaveMatch()
    {
        IsPaused = false;
        Time.timeScale = 1f;          // si no, el menú principal arranca congelado
        AudioListener.pause = false;
        PlayerPrefs.Save();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (!Application.CanStreamedLevelBeLoaded(mainMenuScene))
        {
            Debug.LogError($"PauseMenu: la escena \"{mainMenuScene}\" no está en File > Build Profiles (Scene List).");
            return;
        }
        SceneManager.LoadScene(mainMenuScene);
    }

    // CA3 (US 052): vuelve a cargar la escena desde cero, así se descarta todo el progreso
    // (vida, plata, balas, oleada) sin que cada sistema tenga que saber reiniciarse.
    public void RestartMatch()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        PlayerPrefs.Save();

        Scene scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0)
        {
            SceneManager.LoadScene(scene.buildIndex);
            return;
        }
#if UNITY_EDITOR
        // Las escenas de prueba no están en la lista del build: en el editor se cargan por su ruta.
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        Debug.LogError($"PauseMenu: la escena \"{scene.name}\" no está en File > Build Profiles (Scene List).");
#endif
    }

    // Botón de la confirmación: salir o reiniciar, según qué se eligió.
    private void Confirm()
    {
        if (Current == Panel.Restart) RestartMatch();
        else LeaveMatch();
    }

    // Reiniciar solo tiene sentido si la partida es local (Zombie). En online se oculta.
    private void LayoutOptions()
    {
        visibleOptions.Clear();
        visibleOptions.Add(OptResume);
        if (!isMultiplayer) visibleOptions.Add(OptRestart);
        visibleOptions.Add(OptSettings);
        visibleOptions.Add(OptExit);

        for (int i = 0; i < options.Count; i++) options[i].rect.gameObject.SetActive(false);
        for (int i = 0; i < visibleOptions.Count; i++)
        {
            Option option = options[visibleOptions[i]];
            option.rect.gameObject.SetActive(true);
            option.rect.anchoredPosition = new Vector2(option.rect.anchoredPosition.x, -(390f + i * (OptionH + OptionGap)));
            option.key.text = (i + 1).ToString();
        }
    }

    // Para cuando exista el online: true en Táctico y Deathmatch.
    public void SetMultiplayer(bool value)
    {
        if (IsPaused) Resume();
        isMultiplayer = value;
    }

    // index: posición en el menú (1, 2, 3...), que depende de si se ve Reiniciar.
    private void Activate(int index)
    {
        if (index < 0 || index >= visibleOptions.Count) return;
        switch (visibleOptions[index])
        {
            case OptResume: Resume(); break;
            case OptRestart: exitSelected = 1; Show(Panel.Restart); break;
            case OptSettings: settingSelected = 0; Show(Panel.Settings); break;
            case OptExit: exitSelected = 1; Show(Panel.Exit); break;
        }
    }

    // Igual que la tienda: con el menú abierto no se mira, no se mueve, no se dispara ni se cambia de arma.
    // ShopUI también se apaga, así la B no abre la tienda encima de la pausa.
    private void BlockPlayerControls()
    {
        blocked.Clear();
        Block(FindObjectsByType<CameraLook>());
        Block(FindObjectsByType<PlayerMovement>());
        Block(FindObjectsByType<MeleeAttack>());
        Block(FindObjectsByType<Pistola>());
        Block(FindObjectsByType<Mitre>());
        Block(FindObjectsByType<WeaponSwitcher>());
        Block(FindObjectsByType<PlayerAbility>());
        Block(FindObjectsByType<ShopUI>());
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Block<T>(T[] components) where T : Behaviour
    {
        foreach (T component in components)
        {
            if (!component.enabled) continue;
            component.enabled = false;
            blocked.Add(component);
        }
    }

    private void RestorePlayerControls()
    {
        // CameraLook vuelve a leer la sensibilidad y el FOV en OnEnable, así se aplica lo que se cambió acá.
        foreach (Behaviour component in blocked)
            if (component != null) component.enabled = true;
        blocked.Clear();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // =====================================================================
    // Configuración (mismas claves que el menú principal)
    // =====================================================================

    private void Adjust(SettingRow row, int direction)
    {
        if (row.slider != null) row.slider.value += direction * row.step;
        else SetScreenMode(Mathf.Clamp(screenMode + direction, 0, 2));
    }

    private void ApplyVolume(string key, float value)
    {
        if (audioMixer != null) audioMixer.SetFloat(key, Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20f);
    }

    private void SetVolume(string key, float value, TextMeshProUGUI label)
    {
        ApplyVolume(key, value);
        PlayerPrefs.SetFloat(key, value);
        label.text = Mathf.RoundToInt(value * 100f) + " %";
    }

    private void SetSensitivity(float value, TextMeshProUGUI label)
    {
        PlayerPrefs.SetFloat(KeySensitivity, value);
        label.text = Number(value);
    }

    private void SetFov(float value, TextMeshProUGUI label)
    {
        PlayerPrefs.SetFloat(KeyFov, value);
        label.text = Mathf.RoundToInt(value) + "°";
        // Se ve el cambio al instante aunque CameraLook esté apagada durante la pausa.
        foreach (CameraLook look in FindObjectsByType<CameraLook>())
        {
            Camera cam = look.GetComponent<Camera>();
            if (cam != null) cam.fieldOfView = value;
        }
    }

    private void SetScreenMode(int mode)
    {
        screenMode = mode;
        PlayerPrefs.SetInt(KeyScreenMode, mode);
        Screen.fullScreenMode = mode == 0 ? FullScreenMode.Windowed
            : mode == 1 ? FullScreenMode.FullScreenWindow
            : FullScreenMode.ExclusiveFullScreen;
        Render();
    }

    private void LoadSettingsIntoUI()
    {
        settingRows[0].slider.SetValueWithoutNotify(PlayerPrefs.GetFloat(KeyGeneral, DefaultVolume));
        settingRows[1].slider.SetValueWithoutNotify(PlayerPrefs.GetFloat(KeyMusic, DefaultVolume));
        settingRows[2].slider.SetValueWithoutNotify(PlayerPrefs.GetFloat(KeyEffects, DefaultVolume));
        settingRows[3].slider.SetValueWithoutNotify(PlayerPrefs.GetFloat(KeySensitivity, DefaultSensitivity));
        settingRows[4].slider.SetValueWithoutNotify(PlayerPrefs.GetFloat(KeyFov, DefaultFov));
        screenMode = PlayerPrefs.GetInt(KeyScreenMode, DefaultScreenMode);

        for (int i = 0; i < 3; i++) settingRows[i].value.text = Mathf.RoundToInt(settingRows[i].slider.value * 100f) + " %";
        settingRows[3].value.text = Number(settingRows[3].slider.value);
        settingRows[4].value.text = Mathf.RoundToInt(settingRows[4].slider.value) + "°";
    }

    private void ResetSettings()
    {
        // Los mismos valores por defecto que los botones "Restablecer" del menú principal.
        settingRows[0].slider.value = DefaultVolume;
        settingRows[1].slider.value = DefaultVolume;
        settingRows[2].slider.value = DefaultVolume;
        settingRows[3].slider.value = DefaultSensitivity;
        settingRows[4].slider.value = DefaultFov;
        SetScreenMode(DefaultScreenMode);
        PlayerPrefs.Save();
        FlashSaved();
    }

    private void FlashSaved()
    {
        if (savedRoutine != null) StopCoroutine(savedRoutine);
        savedRoutine = StartCoroutine(SavedAnimation());
    }

    private IEnumerator SavedAnimation()
    {
        savedText.alpha = 1f;
        yield return new WaitForSecondsRealtime(1.1f);
        savedText.alpha = 0f;
    }

    // =====================================================================
    // Mostrar
    // =====================================================================

    private void Show(Panel panel)
    {
        Panel previous = Current;
        Current = panel;

        colSettings.gameObject.SetActive(panel == Panel.Settings);
        colExit.gameObject.SetActive(panel == Panel.Exit || panel == Panel.Restart);

        if (panel == Panel.Settings) LoadSettingsIntoUI();
        if (panel == Panel.Exit || panel == Panel.Restart) SetConfirmTexts(panel == Panel.Restart);
        if (panel == Panel.Settings && previous != Panel.Settings) Reveal(settingsGroup);
        if ((panel == Panel.Exit || panel == Panel.Restart) && previous != panel) Reveal(exitGroup);
        if (panel == Panel.Main && previous == Panel.None) Reveal(col1.GetComponent<CanvasGroup>());

        Render();
    }

    private void RefreshTexts()
    {
        string mode = MatchSettings.Mode == GameMode.Zombie ? "Zombie"
            : MatchSettings.Mode == GameMode.Deathmatch ? "Deathmatch" : "Táctico";
        metaText.text = mode + (isMultiplayer ? " · Online" : "");

        statusText.text = isMultiplayer ? "La partida sigue" : "Partida en pausa";
        statusText.color = isMultiplayer ? Accent : Ink;
        statusChip.color = isMultiplayer ? Over(Rgb(242, 154, 56, 0.14f), PanelBase) : ChipColor;
        statusDot.color = isMultiplayer ? Accent : Mute;
        noteText.text = isMultiplayer
            ? "El menú se ve solo en tu pantalla. Tu personaje queda quieto y puede recibir daño."
            : "El tiempo, los enemigos y el audio están detenidos hasta que vuelvas.";

        exitWarning.text = isMultiplayer
            ? "Tu equipo se queda <color=#FF5C5C>con un jugador menos</color>."
            : MatchSettings.Mode == GameMode.Zombie
                ? "Se pierde <color=#FF5C5C>el progreso de esta partida</color>. El récord guardado no cambia."
                : "Se pierde <color=#FF5C5C>el progreso de esta partida</color>.";

        shade.color = new Color(0f, 0f, 0f, DarkAlpha(isMultiplayer ? 0.45f : 0.6f));
    }

    // La misma columna sirve para confirmar Salir (rojo) y Reiniciar (naranja, US 052 CA2).
    private void SetConfirmTexts(bool restart)
    {
        if (restart)
        {
            confirmTitle.text = "¿Reiniciar la partida?";
            exitLabel.text = "Reiniciar";
            confirmLine.color = Accent;
            exitWarning.text = MatchSettings.Mode == GameMode.Zombie
                ? "Volvés a la primera oleada y se pierde <color=#F29A38>el progreso actual</color>."
                : "Volvés al inicio de la partida y se pierde <color=#F29A38>el progreso actual</color>.";
        }
        else
        {
            confirmTitle.text = "¿Salir de la partida?";
            exitLabel.text = "Salir";
            confirmLine.color = Bad;
            RefreshTexts();
        }
    }

    private void Render()
    {
        for (int i = 0; i < visibleOptions.Count; i++)
        {
            int id = visibleOptions[i];
            Option o = options[id];
            bool isSelected = Current == Panel.Main && i == selected;
            bool isOpen = (id == OptRestart && Current == Panel.Restart) || (id == OptSettings && Current == Panel.Settings)
                       || (id == OptExit && Current == Panel.Exit);
            o.bg.color = isSelected ? (o.danger ? badSelected : SelectedColor) : isOpen ? HoverColor : Clear;
            o.chip.color = isSelected ? (o.danger ? Bad : Accent) : ChipDim;
            o.key.color = isSelected ? KeyInk : Ink;
        }

        for (int i = 0; i < settingRows.Count; i++)
        {
            SettingRow row = settingRows[i];
            row.bg.color = Current == Panel.Settings && i == settingSelected ? SelectedColor : Clear;
            if (row.segs == null) continue;
            for (int s = 0; s < row.segs.Length; s++)
            {
                row.segs[s].color = s == screenMode ? Ink : ChipColor;
                row.segLabels[s].color = s == screenMode ? KeyInk : Mute;
            }
        }

        exitButton.color = Current == Panel.Restart ? Accent : Bad;
        exitLabel.color = KeyInk;
        cancelButton.color = exitSelected == 1 ? ChipDim : ChipColor;
        exitButton.transform.localScale = cancelButton.transform.localScale = Vector3.one;
        (exitSelected == 0 ? exitButton : cancelButton).transform.localScale = new Vector3(1.04f, 1.04f, 1f);
    }

    private void Reveal(CanvasGroup group)
    {
        if (group != null) StartCoroutine(RevealAnimation(group));
    }

    // Entra desde la izquierda en 140 ms. Usa tiempo real porque el juego puede estar congelado.
    private static IEnumerator RevealAnimation(CanvasGroup group)
    {
        RectTransform rect = (RectTransform)group.transform;
        float targetX = rect.name == "Columna1" ? 0f : Col1W;
        for (float t = 0f; t < 0.14f; t += Time.unscaledDeltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / 0.14f, 3f);
            group.alpha = k;
            rect.anchoredPosition = new Vector2(targetX - 24f * (1f - k), rect.anchoredPosition.y);
            yield return null;
        }
        group.alpha = 1f;
        rect.anchoredPosition = new Vector2(targetX, rect.anchoredPosition.y);
    }

    // =====================================================================
    // Armado de la interfaz
    // =====================================================================

    private void BuildCanvas()
    {
        GameObject canvasObject = new GameObject("PausaCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.layer = 5;
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60; // por encima de la tienda (50) y del HUD
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        canvasRoot = (RectTransform)canvasObject.transform;

        root = Stretch(Node("Pausa", canvasRoot));
        shade = Image(Stretch(Node("Oscurecido", root)), null, new Color(0f, 0f, 0f, DarkAlpha(0.6f)), 0f, true);

        BuildMainColumn();
        BuildSettingsColumn();
        BuildExitColumn();
    }

    // Una columna pegada al borde izquierdo, de alto completo.
    private RectTransform Column(string name, float x, float width, Color color)
    {
        RectTransform rect = Node(name, root);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(width, 0f);
        Image(rect, null, color, 0f, true);
        rect.gameObject.AddComponent<CanvasGroup>();
        // Borde derecho de 1 px
        RectTransform edge = Node("Borde", rect);
        edge.anchorMin = new Vector2(1f, 0f);
        edge.anchorMax = new Vector2(1f, 1f);
        edge.pivot = new Vector2(1f, 1f);
        edge.sizeDelta = new Vector2(1f, 0f);
        Image(edge, null, LineColor);
        return rect;
    }

    // Una fila anclada abajo (para los atajos y los botones de pie).
    private static RectTransform Bottom(RectTransform rect, float x, float fromBottom, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(x, fromBottom);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private void BuildMainColumn()
    {
        col1 = Column("Columna1", 0f, Col1W, PanelColor);
        float w = Col1W - PadX * 2f;

        Text(Place(Node("Titulo", col1), PadX, PadTop, w, 84f), displayFont, 84f, Ink, TextAlignmentOptions.MidlineLeft, 2f, true).text = "Pausa";
        metaText = Text(Place(Node("Modo", col1), PadX, 150f, w, 26f), labelFont, 20f, Mute, TextAlignmentOptions.MidlineLeft, 16f, true);
        Image(Place(Node("LineaAnden", col1), PadX, 192f, 150f, 3f), null, Accent);

        statusChip = Image(Place(Node("Estado", col1), PadX, 216f, 250f, 40f), rounded, ChipColor, 4f);
        statusDot = Image(Place(Node("Punto", statusChip.rectTransform), 15f, 15f, 10f, 10f), rounded, Mute, 5f);
        statusText = Text(Place(Node("Texto", statusChip.rectTransform), 36f, 0f, 210f, 40f), labelFont, 20f, Ink, TextAlignmentOptions.MidlineLeft, 10f, true);

        noteText = Text(Place(Node("Nota", col1), PadX, 268f, w, 60f), bodyFont, 19f, Mute, TextAlignmentOptions.TopLeft);
        noteText.textWrappingMode = TextWrappingModes.Normal;

        BuildRails(col1, PadX, 346f, w);

        string[] labels = { "Reanudar", "Reiniciar partida", "Configuración", "Salir de la partida" };
        for (int i = 0; i < labels.Length; i++)
        {
            int id = i;
            float y = 390f + i * (OptionH + OptionGap);
            RectTransform row = Place(Node("Opcion" + (i + 1), col1), PadX - 16f, y, w + 32f, OptionH);
            Option option = new Option { danger = i == OptExit, rect = row };
            option.bg = Image(row, rounded, Clear, 6f, true);
            option.chip = Image(Place(Node("Tecla", row), 20f, (OptionH - 39f) / 2f, 39f, 39f), rounded, ChipDim, 4f);
            option.key = Text(Stretch(Node("Numero", option.chip.rectTransform)), displayFont, 20f, Ink, TextAlignmentOptions.Center);
            option.key.text = (i + 1).ToString();
            option.label = Text(Place(Node("Texto", row), 80f, 0f, w - 60f, OptionH), displayFont, 33f, Ink, TextAlignmentOptions.MidlineLeft, 6f, true);
            option.label.text = labels[i];
            options.Add(option);

            ShopPointerTarget pointer = row.gameObject.AddComponent<ShopPointerTarget>();
            pointer.Hovered = () => { if (Current == Panel.Main) { selected = visibleOptions.IndexOf(id); Render(); } };
            pointer.Clicked = () => Activate(visibleOptions.IndexOf(id));
        }

        BuildShortcuts(col1, new[] { ("Esc", "Reanudar"), ("Números", "Elegir"), ("Enter", "Aceptar"), ("Retroceso", "Volver") });
    }

    private void BuildSettingsColumn()
    {
        colSettings = Column("Configuracion", Col1W, Col2W, col2Color);
        settingsGroup = colSettings.GetComponent<CanvasGroup>();
        float w = Col2W - PadX * 2f;

        Text(Place(Node("Titulo", colSettings), PadX, PadTop, w, 56f), displayFont, 50f, Ink, TextAlignmentOptions.MidlineLeft, 2f, true).text = "Configuración";
        Image(Place(Node("LineaAnden", colSettings), PadX, 130f, 150f, 3f), null, Accent);

        float y = 160f;
        Group("Audio", ref y);
        SliderRow("General", ref y, 0.0001f, 1f, 0.05f, (v, label) => SetVolume(KeyGeneral, v, label));
        SliderRow("Música", ref y, 0.0001f, 1f, 0.05f, (v, label) => SetVolume(KeyMusic, v, label));
        SliderRow("Efectos", ref y, 0.0001f, 1f, 0.05f, (v, label) => SetVolume(KeyEffects, v, label));
        Group("Mouse", ref y);
        SliderRow("Sensibilidad", ref y, 0.5f, 3f, 0.1f, SetSensitivity);        // mismo rango que el menú
        Group("Video", ref y);
        SliderRow("Campo de visión", ref y, 60f, 100f, 1f, SetFov, true);        // mismo rango que el menú
        ScreenModeRow(ref y);

        RectTransform back = Bottom(Node("Volver", colSettings), PadX, 90f, 150f, 52f);
        ButtonBox(back, "Volver", () => Back());
        RectTransform reset = Bottom(Node("Restablecer", colSettings), PadX + 166f, 90f, 190f, 52f);
        ButtonBox(reset, "Restablecer", ResetSettings);
        savedText = Text(Bottom(Node("Guardado", colSettings), PadX + 372f, 90f, 120f, 52f), labelFont, 17f, Ok, TextAlignmentOptions.MidlineLeft, 12f, true);
        savedText.text = "Guardado";
        savedText.alpha = 0f;

        BuildShortcuts(colSettings, new[] { ("Flechas", "Elegir y cambiar"), ("Retroceso", "Volver") });
        colSettings.gameObject.SetActive(false);
    }

    private void Group(string title, ref float y)
    {
        y += 14f;
        Text(Place(Node("Grupo", colSettings), PadX, y, 300f, 24f), labelFont, 17f, Mute, TextAlignmentOptions.MidlineLeft, 16f, true).text = title;
        y += 30f;
    }

    private void SliderRow(string title, ref float y, float min, float max, float step,
        System.Action<float, TextMeshProUGUI> onChange, bool whole = false)
    {
        int index = settingRows.Count;
        RectTransform row = Place(Node(title, colSettings), PadX - 12f, y, Col2W - PadX * 2f + 24f, SetRowH);
        SettingRow setting = new SettingRow { step = step };
        setting.bg = Image(row, rounded, Clear, 4f, true);
        Text(Place(Node("Nombre", row), 12f, 0f, 190f, SetRowH), labelFont, 23f, Ink, TextAlignmentOptions.MidlineLeft, 5f, true).text = title;
        setting.value = Text(Place(Node("Valor", row), row.sizeDelta.x - 82f, 0f, 70f, SetRowH), labelFont, 21f, Ink, TextAlignmentOptions.MidlineRight);
        setting.slider = BuildSlider(row, 210f, 250f, min, max, whole);
        setting.slider.onValueChanged.AddListener(v => onChange(v, setting.value));
        settingRows.Add(setting);

        ShopPointerTarget pointer = row.gameObject.AddComponent<ShopPointerTarget>();
        pointer.Hovered = () => { settingSelected = index; Render(); };
        y += SetRowH;
    }

    private void ScreenModeRow(ref float y)
    {
        int index = settingRows.Count;
        RectTransform row = Place(Node("ModoPantalla", colSettings), PadX - 12f, y, Col2W - PadX * 2f + 24f, SetRowH);
        SettingRow setting = new SettingRow { segs = new UIImage[3], segLabels = new TextMeshProUGUI[3] };
        setting.bg = Image(row, rounded, Clear, 4f, true);
        Text(Place(Node("Nombre", row), 12f, 0f, 190f, SetRowH), labelFont, 23f, Ink, TextAlignmentOptions.MidlineLeft, 5f, true).text = "Pantalla";

        string[] names = { "Ventana", "Sin bordes", "Completa" };
        float[] widths = { 96f, 116f, 106f };
        float x = 210f;
        for (int i = 0; i < 3; i++)
        {
            int mode = i;
            RectTransform seg = Place(Node(names[i], row), x, (SetRowH - 34f) / 2f, widths[i], 34f);
            setting.segs[i] = Image(seg, rounded, ChipColor, 3f, true);
            setting.segLabels[i] = Text(Stretch(Node("Texto", seg)), labelFont, 17f, Mute, TextAlignmentOptions.Center, 8f, true);
            setting.segLabels[i].text = names[i];
            seg.gameObject.AddComponent<ShopPointerTarget>().Clicked = () => SetScreenMode(mode);
            x += widths[i] + 4f;
        }
        settingRows.Add(setting);

        ShopPointerTarget pointer = row.gameObject.AddComponent<ShopPointerTarget>();
        pointer.Hovered = () => { settingSelected = index; Render(); };
        y += SetRowH;
    }

    // Slider con forma de vía: tramo blanco hasta el valor y un durmiente naranja como cursor.
    private Slider BuildSlider(RectTransform parent, float x, float width, float min, float max, bool whole)
    {
        RectTransform rect = Place(Node("Slider", parent), x, (SetRowH - 28f) / 2f, width, 28f);

        RectTransform track = Node("Via", rect);
        track.anchorMin = new Vector2(0f, 0.5f);
        track.anchorMax = new Vector2(1f, 0.5f);
        track.sizeDelta = new Vector2(0f, 9f);
        Image(track, null, TrackColor, 0f, true);

        RectTransform fillArea = Node("Relleno", rect);
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.sizeDelta = new Vector2(0f, 9f);
        RectTransform fill = Node("Tramo", fillArea);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.sizeDelta = Vector2.zero;
        Image(fill, null, Ink);

        RectTransform handleArea = Stretch(Node("AreaCursor", rect));
        RectTransform handle = Node("Durmiente", handleArea);
        handle.anchorMin = handle.anchorMax = new Vector2(0f, 0.5f);
        handle.sizeDelta = new Vector2(9f, 27f);
        UIImage handleImage = Image(handle, null, Accent, 0f, true);

        Slider slider = rect.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.transition = Selectable.Transition.None;
        slider.navigation = new Navigation { mode = Navigation.Mode.None }; // las flechas las maneja el menú
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = whole;
        return slider;
    }

    private void BuildExitColumn()
    {
        colExit = Column("Salir", Col1W, Col2W, col2Color);
        exitGroup = colExit.GetComponent<CanvasGroup>();
        float w = Col2W - PadX * 2f;

        confirmTitle = Text(Place(Node("Titulo", colExit), PadX, PadTop, w, 56f), displayFont, 46f, Ink, TextAlignmentOptions.MidlineLeft, 2f, true);
        confirmTitle.text = "¿Salir de la partida?";
        confirmLine = Image(Place(Node("LineaAnden", colExit), PadX, 130f, 150f, 3f), null, Bad);

        exitWarning = Text(Place(Node("Aviso", colExit), PadX, 160f, w, 70f), bodyFont, 23f, Ink, TextAlignmentOptions.TopLeft);
        exitWarning.textWrappingMode = TextWrappingModes.Normal;

        RectTransform exitRect = Place(Node("BotonSalir", colExit), PadX, 250f, 150f, 56f);
        exitButton = Image(exitRect, rounded, Bad, 4f, true);
        exitLabel = Text(Stretch(Node("Texto", exitRect)), displayFont, 22f, KeyInk, TextAlignmentOptions.Center, 10f, true);
        exitLabel.text = "Salir";
        ShopPointerTarget exitPointer = exitRect.gameObject.AddComponent<ShopPointerTarget>();
        exitPointer.Hovered = () => { exitSelected = 0; Render(); };
        exitPointer.Clicked = Confirm;

        RectTransform cancelRect = Place(Node("BotonCancelar", colExit), PadX + 166f, 250f, 180f, 56f);
        cancelButton = Image(cancelRect, rounded, ChipDim, 4f, true);
        cancelLabel = Text(Stretch(Node("Texto", cancelRect)), displayFont, 22f, Ink, TextAlignmentOptions.Center, 10f, true);
        cancelLabel.text = "Cancelar";
        ShopPointerTarget cancelPointer = cancelRect.gameObject.AddComponent<ShopPointerTarget>();
        cancelPointer.Hovered = () => { exitSelected = 1; Render(); };
        cancelPointer.Clicked = () => Back();

        BuildShortcuts(colExit, new[] { ("Flechas", "Elegir"), ("Enter", "Aceptar"), ("Esc", "Cancelar") });
        colExit.gameObject.SetActive(false);
    }

    private UIImage ButtonBox(RectTransform rect, string label, System.Action onClick)
    {
        UIImage image = Image(rect, rounded, ChipColor, 4f, true);
        Text(Stretch(Node("Texto", rect)), displayFont, 20f, Ink, TextAlignmentOptions.Center, 10f, true).text = label;
        ShopPointerTarget pointer = rect.gameObject.AddComponent<ShopPointerTarget>();
        pointer.Hovered = () => image.color = ChipDim;
        pointer.Exited = () => image.color = ChipColor;
        pointer.Clicked = onClick;
        return image;
    }

    // Atajos siempre visibles al pie de la columna. Si no entran en una línea, pasan a dos.
    private void BuildShortcuts(RectTransform column, (string key, string label)[] items)
    {
        float maxX = column.sizeDelta.x - PadX;
        var chips = new List<(RectTransform chip, TextMeshProUGUI label, float keyW, float labelW)>();
        foreach (var (key, label) in items)
        {
            RectTransform chip = Node("Atajo", column);
            Image(chip, rounded, ChipDim, 3f);
            TextMeshProUGUI keyText = Text(Stretch(Node("Tecla", chip)), displayFont, 15f, Ink, TextAlignmentOptions.Center);
            keyText.text = key;
            TextMeshProUGUI labelText = Text(Node("Nombre", column), labelFont, 15f, Mute, TextAlignmentOptions.MidlineLeft, 12f, true);
            labelText.text = label;
            chips.Add((chip, labelText, Mathf.Max(26f, Width(keyText, key) + 14f), Width(labelText, label)));
        }

        // Primero se reparte en líneas, después se ubica de arriba hacia abajo.
        var lines = new List<int>();
        float x = PadX;
        int line = 0;
        foreach (var c in chips)
        {
            float w = c.keyW + 7f + c.labelW;
            if (x > PadX && x + w > maxX) { line++; x = PadX; }
            lines.Add(line);
            x += w + 20f;
        }

        x = PadX;
        for (int i = 0; i < chips.Count; i++)
        {
            if (i > 0 && lines[i] != lines[i - 1]) x = PadX;
            float fromBottom = 20f + (line - lines[i]) * 34f;
            var c = chips[i];
            Bottom(c.chip, x, fromBottom, c.keyW, 26f);
            Bottom(c.label.rectTransform, x + c.keyW + 7f, fromBottom, c.labelW, 26f);
            x += c.keyW + 7f + c.labelW + 20f;
        }
    }

    // Los rieles del HUD: dos líneas de 1 px con durmientes, que se desvanecen en las puntas.
    private static void BuildRails(RectTransform parent, float x, float y, float width)
    {
        RectTransform rails = Place(Node("Rieles", parent), x, y, width, 14f);
        for (float d = 0f; d < width; d += 13f)
        {
            float edge = Mathf.Min(d, width - d) / (width * 0.18f);
            float a = 0.55f * Mathf.Clamp01(edge);
            Image(Place(Node("Durmiente", rails), d, 1f, 3f, 12f), null, WithAlpha(Mute, FadeAlpha(a)));
        }
        for (int i = 0; i < 2; i++)
        {
            float lineY = i * 13f;
            for (float d = 0f; d < width; d += 20f)
            {
                float edge = Mathf.Min(d, width - d) / (width * 0.18f);
                float a = 0.7f * Mathf.Clamp01(edge);
                Image(Place(Node("Riel", rails), d, lineY, 20f, 1f), null, WithAlpha(Mute, FadeAlpha(a)));
            }
        }
    }
}