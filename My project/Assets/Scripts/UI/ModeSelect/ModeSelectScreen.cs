using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using static ShopUIKit;
using Img = UnityEngine.UI.Image;

// Selección de modo (US 047), diseño "Andenes": la pantalla se parte en tres con dos rayos y cada andén es un modo.
// Al pasar el mouse el andén se agranda; al hacer clic ocupa toda la pantalla y muestra sus opciones:
// Táctico y Deathmatch crean o se unen a una sala (US 026, US 027); Zombie elige la dificultad (US 161) y juega.
// Se arma por código con las medidas de la maqueta (pantalla de referencia de 1920 x 1080), como el HUD y la tienda.
public class ModeSelectScreen : MonoBehaviour
{
    [Serializable] public class ModeEvent : UnityEvent<GameMode> { }
    [Serializable] public class JoinEvent : UnityEvent<GameMode, string> { }
    [Serializable] public class ZombieEvent : UnityEvent<ZombieDifficulty> { }

    [Header("Tipografías")]
    [SerializeField] private TMP_FontAsset displayFont; // Barlow Condensed Bold
    [SerializeField] private TMP_FontAsset labelFont;   // Barlow Condensed SemiBold
    [SerializeField] private TMP_FontAsset bodyFont;    // Barlow Regular
    [SerializeField] private TMP_FontAsset monoFont;    // JetBrains Mono

    [Header("Sprites")]
    [SerializeField] private Sprite rounded;
    [Tooltip("Bordes de 1 px para los radios 6 y 8, en ese orden.")]
    [SerializeField] private Sprite[] borders = new Sprite[2];
    [Tooltip("Captura del mapa de cada modo (Táctico, Deathmatch, Zombie), en gris. Se ve difuminada detrás del andén.")]
    [SerializeField] private Sprite[] mapBackgrounds = new Sprite[3];

    [Header("Partida")]
    [Tooltip("Escena que carga Jugar en Zombie. Vacío: todavía no hay escena.")]
    [SerializeField] private string zombieScene = "";

    [Header("Eventos (para conectar el multijugador y las partidas)")]
    public ModeEvent onCreateRoom = new ModeEvent();
    public JoinEvent onJoinRoom = new JoinEvent();
    public ZombieEvent onPlayZombie = new ZombieEvent();

    // ---------- Datos de la maqueta ----------

    private const float W = 1920f, H = 1080f;
    private const int N = 12;
    private const float LabelTop = 610f, LabelBottom = 900f;

    // Se arman en Awake: ShopUIKit consulta el espacio de color y Unity no deja hacerlo al crear el componente.
    private static Color[] ModeColor, ModeBg;
    private static readonly string[] ModeHex = { "#F29A38", "#FF4B4B", "#7DE05A" };
    private static readonly string[] Titles = { "Táctico", "Deathmatch", "Zombie" };
    private static readonly string[] Blurbs =
    {
        "Plantá o desactivá el dispositivo. Sin reaparición.",
        "Todos contra todos. Reaparecés a los 3 s.",
        "Sobreviví a la horda en la universidad."
    };
    private static readonly string[][] Chips =
    {
        new[] { "4 vs 4", "Primero a 7", "Online" },
        new[] { "Hasta 8", "25 bajas", "Online" },
        new[] { "1 jugador", "10 oleadas", "Local" }
    };
    private static readonly string[] DetailEyebrow = { "Andén 1 · Online", "Andén 2 · Online", "Andén 3 · Local" };
    private static readonly string[] DetailText =
    {
        "Un equipo planta el dispositivo en A o B y el otro lo defiende. Sin reaparición: si morís, mirás a un compañero hasta la ronda siguiente.",
        "Todos contra todos. Elegís tus armas gratis, cada baja te cura 50 y reaparecés a los 3 s lejos de los rivales.",
        "Vos solo contra 10 oleadas en la universidad. Después podés seguir en modo infinito para hacer récord."
    };
    private static readonly string[][] Rules =
    {
        new[] { "4 vs 4", "Primero a 7", "Cambio de lado en la 6", "Compra 20 s" },
        new[] { "Hasta 8", "25 bajas", "8 min", "Armas gratis" },
        new string[0]
    };
    private static readonly string[] DifficultyName = { "Fácil", "Normal", "Difícil" };
    private static readonly string[] DifficultyText =
    {
        "Zombis con 75 % de vida y daño. Regenerás a los 3 s.",
        "Como está pensado. Regenerás a los 4 s.",
        "125 % de vida, 150 % de daño y más zombis. +25 % de puntos."
    };
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"; // sin O, I ni L

    // ---------- Piezas ----------

    private class Bolt
    {
        public float slant;
        public readonly float[] jit = new float[N + 1];
        public UIPolyline glow, mid, core, branch;
        public int steps;
        public float nextStep, branchAt = -10f;
    }

    private class Anden
    {
        public CanvasGroup group;
        public UIPolygon shape;
        public RectTransform motif;
        public Img dim, flash;
        public float flashAt = -10f;
    }

    private class Label
    {
        public RectTransform rect;
        public CanvasGroup group;
        public TextMeshProUGUI title, blurb;
        public readonly List<RectTransform> chips = new List<RectTransform>();
        public float natural, lastWidth = -1f, visible = 1f, shown = 1f;
    }

    private class Detail
    {
        public RectTransform rect;
        public CanvasGroup group;
        public RectTransform codeRow, room;
        public TMP_InputField code;
        public TextMeshProUGUI status, roomCode, roomCount, roomFoot;
        public readonly TextMeshProUGUI[] slots = new TextMeshProUGUI[8];
        public float actionsY;
    }

    private RectTransform root;
    private readonly Bolt[] bolts = { new Bolt { slant = 95f }, new Bolt { slant = 72f } };
    private readonly Anden[] andenes = new Anden[3];
    private readonly Label[] labels = new Label[3];
    private readonly Detail[] details = new Detail[3];
    private CanvasGroup topGroup, titleGroup, shadeGroup;
    private TextMeshProUGUI backText;
    private RectTransform backButton;
    private MenuUIController menu;

    // Táctico
    private readonly List<UIRing> pulses = new List<UIRing>();
    private readonly List<UIPolyline> flows = new List<UIPolyline>();
    private readonly List<Img> leds = new List<Img>();
    private RectTransform spin, spinBack;
    private CanvasGroup holo, core;
    private TextMeshProUGUI lcd;
    // Deathmatch
    private readonly List<UIPolyline> tracers = new List<UIPolyline>();
    private readonly List<float> tracerSpeed = new List<float>(), tracerPhase = new List<float>();
    private RectTransform cross;
    private CanvasGroup hit;
    private Vector2 crossPos = new Vector2(960f, 380f), crossFrom, crossTo = new Vector2(960f, 380f);
    private float crossT = 1f, crossDur = 0.6f, crossWait;
    private float hitAt = -10f;
    // Zombie
    private readonly List<RectTransform> fogs = new List<RectTransform>();
    private readonly List<RectTransform> zombies = new List<RectTransform>();
    private readonly List<float> zombieDelay = new List<float>();
    private readonly List<UIRing> eyes = new List<UIRing>();
    private readonly List<Vector2> zombieBase = new List<Vector2>();
    private readonly UnityEngine.UI.Button[] difficultyButtons = new UnityEngine.UI.Button[3];
    private TextMeshProUGUI record;

    // Estado
    private float[] cur = { W / 3f, 2f * W / 3f }, target = { W / 3f, 2f * W / 3f };
    private readonly float[] motX = { W / 6f, W / 2f, 5f * W / 6f };
    private int hover = -1, open = -1;
    private bool dirty = true;
    private float introAt, nextStrike, openAt;
    private Vector2 lastMouse = new Vector2(-1f, -1f);
    private readonly Vector2[] p0 = new Vector2[N + 1], p1 = new Vector2[N + 1];
    private readonly List<Vector2> scratch = new List<Vector2>();

    private void Awake()
    {
        ModeColor ??= new[] { Hex(0xF29A38), Hex(0xFF4B4B), Hex(0x7DE05A) };
        ModeBg ??= new[] { Hex(0x0D0A07), Hex(0x0C0606), Hex(0x060906) };
        menu = GetComponentInParent<MenuUIController>();
        foreach (Bolt b in bolts) NewJitter(b);
        Build();
    }

    private void OnEnable()
    {
        open = -1;
        hover = -1;
        cur = new[] { W / 2f, W / 2f };
        target = new[] { W / 3f, 2f * W / 3f };
        introAt = Time.unscaledTime;
        nextStrike = introAt + 1.6f;
        dirty = true;
        if (details[0] != null) for (int i = 0; i < 3; i++) ResetDetail(i);
    }

    // ================= Armado =================

    private void Build()
    {
        Img backdrop = Image(Stretch(Node("Fondo", transform)), null, Color.black);
        backdrop.raycastTarget = false;

        root = Node("Andenes", transform);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(W, H);

        for (int i = 0; i < 3; i++) andenes[i] = BuildAnden(i);
        BuildTactico(andenes[0].motif);
        BuildDeathmatch(andenes[1]);
        BuildZombie(andenes[2].motif);
        foreach (Anden a in andenes) { a.dim.transform.SetAsLastSibling(); a.flash.transform.SetAsLastSibling(); }

        for (int k = 0; k < 2; k++) BuildBolt(k);
        // Zona invisible que recibe el clic sobre los andenes (por el sistema de eventos, como los botones).
        Img catcher = Image(Place(Node("Clic", root), 0f, 0f, W, H), null, White(0f), 0f, true);
        catcher.gameObject.AddComponent<ClickCatcher>().Clicked = OnStageClick;

        for (int i = 0; i < 3; i++) labels[i] = BuildLabel(i);

        RectTransform shade = Place(Node("Sombra", root), 0f, 0f, W, H);
        Img shadeImg = Image(shade, GradientSprite(), Color.white);
        shadeGroup = shade.gameObject.AddComponent<CanvasGroup>();
        shadeGroup.alpha = 0f;
        shadeGroup.blocksRaycasts = false;
        shadeImg.raycastTarget = false;

        BuildTop();
        for (int i = 0; i < 3; i++) details[i] = BuildDetail(i);
    }

    private T Shape<T>(string name, Transform parent, float x, float y, Color color) where T : UIShape
    {
        RectTransform rect = Node(name, parent);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(1f, 1f);
        rect.gameObject.AddComponent<CanvasRenderer>(); // las figuras viven en un archivo con otro nombre y Unity no lo agrega solo
        T shape = rect.gameObject.AddComponent<T>();
        shape.color = color;
        shape.raycastTarget = false;
        return shape;
    }

    private RectTransform Group(string name, Transform parent, float x, float y)
    {
        RectTransform rect = Node(name, parent);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    private UIPolyline Line(Transform parent, Color color, float thickness, params Vector2[] points)
    {
        UIPolyline line = Shape<UIPolyline>("Linea", parent, 0f, 0f, color);
        line.thickness = thickness;
        line.SetPoints(points);
        return line;
    }

    private UIRing Circle(Transform parent, float x, float y, float radius, Color color, float thickness = 0f)
    {
        UIRing ring = Shape<UIRing>("Circulo", parent, x, y, color);
        ring.radius = radius;
        ring.thickness = thickness;
        return ring;
    }

    // Brillo redondo: una textura con caída suave y un poco de ruido, así no se ven escalones en los tonos oscuros.
    private RectTransform Glow(Transform parent, float x, float y, float radius, Color color, float mid = 0.25f)
    {
        RectTransform rect = Node("Brillo", parent);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(radius * 2f, radius * 2f);
        Image(rect, GlowSprite(mid), color);
        return rect;
    }

    private static readonly Dictionary<int, Sprite> glowSprites = new Dictionary<int, Sprite>();

    private static Sprite GlowSprite(float mid)
    {
        int key = Mathf.RoundToInt(mid * 100f);
        if (glowSprites.TryGetValue(key, out Sprite cached) && cached != null) return cached;
        const int size = 256;
        float power = Mathf.Log(Mathf.Clamp(mid, 0.01f, 0.99f)) / Mathf.Log(0.5f);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var random = new System.Random(key);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float t = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                float a = Mathf.Pow(1f - t, power) * 255f + (float)(random.NextDouble() * 8.0 - 4.0) * Mathf.Min(1f, (1f - t) * 4f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(a), 0, 255));
            }
        tex.SetPixels32(pixels);
        tex.Apply();
        Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        glowSprites[key] = sprite;
        return sprite;
    }

    private UIPolygon Poly(Transform parent, Color color, params Vector2[] points)
    {
        UIPolygon poly = Shape<UIPolygon>("Forma", parent, 0f, 0f, color);
        poly.SetPoints(points);
        return poly;
    }

    private static Vector2[] RectPts(float x, float y, float w, float h) => UIPolygon.Rect(x, y, w, h);

    private Anden BuildAnden(int i)
    {
        var a = new Anden();
        a.shape = Shape<UIPolygon>("Anden" + (i + 1), root, 0f, 0f, ModeBg[i]);
        var mask = a.shape.gameObject.AddComponent<UnityEngine.UI.Mask>();
        mask.showMaskGraphic = true;
        a.group = a.shape.gameObject.AddComponent<CanvasGroup>();
        a.group.blocksRaycasts = false;

        a.motif = Group("Motivo", a.shape.transform, motX[i], 540f);
        if (mapBackgrounds != null && i < mapBackgrounds.Length && mapBackgrounds[i] != null)
            Image(Place(Node("Mapa", a.motif), -1400f, -787f, 2800f, 1575f), mapBackgrounds[i], White(FadeAlpha(i == 2 ? 0.14f : 0.17f)));
        float light = i == 2 ? 0.26f : 0.34f;
        Glow(a.motif, 0f, 0f, 720f, WithAlpha(ModeColor[i], FadeAlpha(light)));

        a.dim = Image(Place(Node("Oscuro", a.shape.transform), 0f, 0f, W, H), null, new Color(0f, 0f, 0f, 0f));
        a.flash = Image(Place(Node("Destello", a.shape.transform), 0f, 0f, W, H), null, White(0f));
        return a;
    }

    private void BuildTactico(RectTransform m)
    {
        Color acc = ModeColor[0], hot = Hot;

        // Grilla de plano y las letras de los puntos de plantado.
        Color grid = WithAlpha(acc, FadeAlpha(0.09f));
        for (float x = -1408f; x <= 1408f; x += 64f) Segment(m, new Vector2(x, -540f), new Vector2(x, 540f), 1.5f, grid);
        for (float y = -512f; y <= 540f; y += 64f) Segment(m, new Vector2(-1400f, y), new Vector2(1400f, y), 1.5f, grid);
        foreach (var (letter, x) in new[] { ("A", -260f), ("B", 270f) })
        {
            TextMeshProUGUI t = Text(Place(Node("Punto" + letter, m), x - 200f, -420f, 400f, 340f), displayFont, 320f,
                WithAlpha(acc, FadeAlpha(0.06f)), TextAlignmentOptions.Center);
            t.text = letter;
        }

        RectTransform dev = Group("Dispositivo", m, 0f, -110f);
        for (int k = 0; k < 3; k++)
        {
            UIRing pulse = Circle(dev, 0f, 0f, 70f, acc, 3f);
            pulses.Add(pulse);
        }

        // Proyección del holograma con la cuenta regresiva.
        Line(dev, WithAlpha(acc, FadeAlpha(0.35f)), 1.5f, new Vector2(-46f, -78f), new Vector2(-96f, -168f));
        Line(dev, WithAlpha(acc, FadeAlpha(0.35f)), 1.5f, new Vector2(46f, -78f), new Vector2(96f, -168f));
        Poly(dev, WithAlpha(acc, FadeAlpha(0.05f)), new Vector2(-96f, -168f), new Vector2(96f, -168f), new Vector2(46f, -78f), new Vector2(-46f, -78f));
        RectTransform holoRect = Group("Holograma", dev, 0f, 0f);
        holo = holoRect.gameObject.AddComponent<CanvasGroup>();
        Poly(holoRect, WithAlpha(acc, FadeAlpha(0.10f)), RectPts(-104f, -232f, 208f, 64f));
        UIPolyline frame = Line(holoRect, WithAlpha(acc, FadeAlpha(0.7f)), 2f, RectPts(-104f, -232f, 208f, 64f));
        frame.closed = true;
        foreach (var (sx, sy) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
        {
            float cx = 104f * sx, cy = -200f + 32f * sy;
            Line(holoRect, hot, 3f, new Vector2(cx, cy - 14f * sy), new Vector2(cx, cy), new Vector2(cx - 14f * sx, cy));
        }
        lcd = Text(Place(Node("Cuenta", holoRect), -104f, -232f, 208f, 64f), monoFont, 30f, acc, TextAlignmentOptions.Center, 6f);
        lcd.text = "0:45";

        // Cuerpo hexagonal con las líneas de energía.
        Vector2[] hex = { new Vector2(-150f, 0f), new Vector2(-84f, -86f), new Vector2(84f, -86f), new Vector2(150f, 0f), new Vector2(84f, 86f), new Vector2(-84f, 86f) };
        Vector2[] inner = { new Vector2(-122f, 0f), new Vector2(-68f, -70f), new Vector2(68f, -70f), new Vector2(122f, 0f), new Vector2(68f, 70f), new Vector2(-68f, 70f) };
        Poly(dev, Hex(0x120E0A), hex);
        UIPolyline outline = Line(dev, acc, 3f, hex); outline.closed = true;
        UIPolyline innerLine = Line(dev, WithAlpha(acc, FadeAlpha(0.28f)), 2f, inner); innerLine.closed = true;
        foreach (Vector2 corner in inner)
        {
            UIPolyline flow = Line(dev, WithAlpha(acc, FadeAlpha(0.75f)), 3f, Vector2.zero, corner);
            flow.dash = 6f; flow.gap = 10f;
            flows.Add(flow);
            Circle(dev, corner.x, corner.y, Mathf.Abs(corner.y) < 1f ? 6f : 5f, acc);
        }
        foreach (float sx in new[] { -1f, 1f })
        {
            float x = sx < 0f ? -176f : 150f;
            Poly(dev, Hex(0x1D1611), RectPts(x, -22f, 26f, 44f));
            UIPolyline clamp = Line(dev, acc, 2f, RectPts(x, -22f, 26f, 44f)); clamp.closed = true;
            leds.Add(Image(Place(Node("Luz", dev), x + 8f, -8f, 10f, 16f), rounded, Bad, 2f));
        }

        // Núcleo con dos anillos que giran.
        Glow(dev, 0f, 0f, 70f, WithAlpha(acc, FadeAlpha(0.45f)), 0.35f);
        UIRing ringA = Circle(dev, 0f, 0f, 52f, acc, 4f); ringA.dashCount = 12; ringA.dashFill = 0.66f;
        spin = ringA.rectTransform;
        UIRing ringB = Circle(dev, 0f, 0f, 42f, WithAlpha(hot, FadeAlpha(0.6f)), 2f); ringB.dashCount = 22; ringB.dashFill = 0.33f;
        spinBack = ringB.rectTransform;
        Circle(dev, 0f, 0f, 32f, Hex(0x1A1109));
        Circle(dev, 0f, 0f, 32f, acc, 2f);
        RectTransform coreRect = Group("Nucleo", dev, 0f, 0f);
        core = coreRect.gameObject.AddComponent<CanvasGroup>();
        Circle(coreRect, 0f, 0f, 20f, acc);
        Glow(coreRect, 0f, 0f, 17f, Hex(0xFFF4E6), 0.8f);
    }

    private void BuildDeathmatch(Anden a)
    {
        Color red = ModeColor[1];
        // Trazadoras que cruzan en diagonal (en coordenadas de pantalla, no se mueven con el andén).
        RectTransform tr = Group("Trazadoras", a.shape.transform, 0f, 0f);
        for (int i = 0; i < 7; i++)
        {
            float y = 250f + i * 150f;
            UIPolyline t = Line(tr, WithAlpha(Hex(0xFFB3A8), FadeAlpha(0.9f)), 3f, new Vector2(-300f, y + 500f), new Vector2(2300f, y - 900f));
            t.dash = 160f; t.gap = 3000f;
            tracers.Add(t);
            tracerSpeed.Add(1f / UnityEngine.Random.Range(1.8f, 3f));
            tracerPhase.Add(UnityEngine.Random.value);
        }

        cross = Group("Mira", a.shape.transform, 960f, 380f);
        UIRing outer = Circle(cross, 0f, 0f, 96f, WithAlpha(red, FadeAlpha(0.35f)), 2f); outer.dashCount = 26; outer.dashFill = 0.45f;
        Circle(cross, 0f, 0f, 56f, red, 4f);
        Segment(cross, new Vector2(-84f, 0f), new Vector2(-34f, 0f), 4f, red);
        Segment(cross, new Vector2(34f, 0f), new Vector2(84f, 0f), 4f, red);
        Segment(cross, new Vector2(0f, -84f), new Vector2(0f, -34f), 4f, red);
        Segment(cross, new Vector2(0f, 34f), new Vector2(0f, 84f), 4f, red);
        Circle(cross, 0f, 0f, 5f, Color.white);
        RectTransform hitRect = Group("Impacto", cross, 0f, 0f);
        hit = hitRect.gameObject.AddComponent<CanvasGroup>();
        hit.alpha = 0f;
        foreach (var (sx, sy) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
            Segment(hitRect, new Vector2(30f * sx, 30f * sy), new Vector2(14f * sx, 14f * sy), 5f, Color.white);
    }

    private void BuildZombie(RectTransform m)
    {
        Color green = ModeColor[2], moon = Hex(0xD8EFCB), fog = WithAlpha(Hex(0xB8E6A6), FadeAlpha(0.30f));
        Circle(m, 170f, -330f, 88f, WithAlpha(moon, FadeAlpha(0.16f)));
        Glow(m, 170f, -330f, 170f, WithAlpha(moon, FadeAlpha(0.14f)), 0.4f);

        fogs.Add(FogBank(m, -160f, 420f, 520f, 120f, fog));
        var figures = new[]
        {
            (x: -230f, y: 560f, s: 0.8f, delay: 0.4f, arms: 1),
            (x: -40f, y: 540f, s: 1.05f, delay: 2.1f, arms: 0),
            (x: 170f, y: 570f, s: 0.7f, delay: 3.6f, arms: 2),
            (x: 330f, y: 560f, s: 0.9f, delay: 1.2f, arms: 0)
        };
        foreach (var f in figures) BuildZombieFigure(m, f.x, f.y, f.s, f.delay, f.arms, green);
        fogs.Add(FogBank(m, 120f, 470f, 620f, 130f, fog));
    }

    private RectTransform FogBank(RectTransform parent, float x, float y, float rx, float ry, Color color)
    {
        RectTransform bank = Glow(parent, x, y, rx, color, 0.35f);
        bank.localScale = new Vector3(1f, ry / rx, 1f);
        return bank;
    }

    private void BuildZombieFigure(RectTransform parent, float x, float y, float scale, float delay, int arms, Color eye)
    {
        Color ink = Hex(0x020402);
        RectTransform figure = Group("Zombi", parent, x, y);
        figure.localScale = Vector3.one * scale;
        zombies.Add(figure);
        zombieBase.Add(new Vector2(x, y));
        zombieDelay.Add(delay);

        // Torso con hombros (sigue por debajo de la pantalla, así nunca se ve cortado).
        scratch.Clear();
        scratch.Add(new Vector2(-86f, 320f));
        Bezier(scratch, new Vector2(-86f, 0f), new Vector2(-86f, -66f), new Vector2(-74f, -108f), new Vector2(-34f, -118f));
        Bezier(scratch, new Vector2(34f, -118f), new Vector2(74f, -108f), new Vector2(86f, -66f), new Vector2(86f, 0f));
        scratch.Add(new Vector2(86f, 320f));
        Poly(figure, ink, scratch.ToArray());
        Poly(figure, ink, RectPts(-16f, -138f, 32f, 30f));
        Circle(figure, 0f, -170f, 44f, ink);
        foreach (float ex in new[] { -15f, 15f }) eyes.Add(Circle(figure, ex, -176f, 5.5f, eye));

        if (arms >= 1) Arm(figure, ink, new Vector2(60f, -96f), new Vector2(120f, -196f), new Vector2(150f, -214f));
        if (arms >= 2)
        {
            Arm(figure, ink, new Vector2(-60f, -96f), new Vector2(-120f, -196f), new Vector2(-150f, -214f));
        }
    }

    private void Arm(RectTransform parent, Color ink, params Vector2[] points)
    {
        UIPolyline arm = Line(parent, ink, 26f, points);
        arm.roundJoins = true;
    }

    private static void Bezier(List<Vector2> into, Vector2 a, Vector2 b, Vector2 c, Vector2 d, int steps = 8)
    {
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps, u = 1f - t;
            into.Add(u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d);
        }
    }

    private void BuildBolt(int k)
    {
        Bolt b = bolts[k];
        Color left = ModeColor[k], right = ModeColor[k + 1];
        b.glow = BoltLine("Brillo", left, right, 30f, 18f, FadeAlpha(0.5f));
        b.mid = BoltLine("Rayo", left, right, 9f, 2f, FadeAlpha(0.9f));
        b.core = BoltLine("Nucleo", Color.white, Color.white, 3.5f, 1f, 1f);
        b.branch = Shape<UIPolyline>("Rama", root, 0f, 0f, White(0f));
        b.branch.thickness = 2.5f;
    }

    private UIPolyline BoltLine(string name, Color left, Color right, float thickness, float feather, float alpha)
    {
        UIPolyline line = Shape<UIPolyline>(name, root, 0f, 0f, WithAlpha(left, alpha));
        line.colorB = WithAlpha(right, alpha);
        line.useGradient = true;
        line.thickness = thickness;
        line.feather = feather;
        return line;
    }

    private Label BuildLabel(int i)
    {
        var l = new Label();
        l.rect = Place(Node("Titulo" + Titles[i], root), 0f, LabelTop, 600f, 320f);
        l.group = l.rect.gameObject.AddComponent<CanvasGroup>();
        l.group.blocksRaycasts = false;

        TextMeshProUGUI eyebrow = Text(Place(Node("Anden", l.rect), 0f, 0f, 600f, 24f), labelFont, 22f, ModeColor[i], TextAlignmentOptions.Center, 32f, true);
        eyebrow.text = "Andén " + (i + 1);
        eyebrow.rectTransform.anchorMax = new Vector2(1f, 1f);
        eyebrow.rectTransform.sizeDelta = new Vector2(0f, 24f);

        l.title = Text(Place(Node("Nombre", l.rect), 0f, 34f, 600f, 116f), displayFont, 124f, Ink, TextAlignmentOptions.Center, 1f, true);
        l.title.text = Titles[i];
        l.title.rectTransform.anchorMax = new Vector2(1f, 1f);
        l.title.rectTransform.sizeDelta = new Vector2(0f, 116f);
        l.natural = Width(l.title, Titles[i].ToUpperInvariant());

        l.blurb = Text(Place(Node("Texto", l.rect), 0f, 162f, 600f, 80f), bodyFont, 26f, White(0.82f), TextAlignmentOptions.Top);
        l.blurb.textWrappingMode = TextWrappingModes.Normal;
        l.blurb.text = Blurbs[i];

        for (int c = 0; c < Chips[i].Length; c++)
            l.chips.Add(Chip(l.rect, Chips[i][c], 19f, c == 0 ? ModeColor[i] : Ink, c == 0 ? ModeColor[i] : White(0.22f), 0.4f));
        return l;
    }

    private RectTransform Chip(RectTransform parent, string value, float size, Color ink, Color border, float darkness)
    {
        RectTransform chip = Node("Etiqueta", parent);
        TextMeshProUGUI text = Text(Stretch(Node("Texto", chip)), monoFont, size, ink, TextAlignmentOptions.Center);
        text.text = value;
        float w = Width(text, value) + 28f, h = size + 20f;
        Place(chip, 0f, 0f, w, h);
        Img bg = Image(chip, rounded, new Color(0f, 0f, 0f, DarkAlpha(darkness)), 6f);
        Border(chip, 6f, border);
        text.transform.SetAsLastSibling();
        return chip;
    }

    private void Border(RectTransform rect, float radius, Color color)
    {
        Sprite sprite = borders != null && borders.Length > 0 ? borders[radius > 7f && borders.Length > 1 ? 1 : 0] : null;
        if (sprite == null) return;
        Img image = Image(Stretch(Node("Borde", rect)), sprite, color);
        image.type = Img.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
    }

    private void BuildTop()
    {
        RectTransform top = Place(Node("Arriba", root), 0f, 0f, W, 140f);
        topGroup = top.gameObject.AddComponent<CanvasGroup>();

        RectTransform titleRect = Place(Node("Titulo", top), 64f, 50f, 700f, 90f);
        titleGroup = titleRect.gameObject.AddComponent<CanvasGroup>();
        titleGroup.blocksRaycasts = false;
        Text(Place(Node("Juego", titleRect), 0f, 0f, 700f, 22f), labelFont, 20f, White(0.6f), TextAlignmentOptions.MidlineLeft, 30f, true).text = "Shooter Legends";
        Text(Place(Node("Pantalla", titleRect), 0f, 26f, 700f, 60f), displayFont, 54f, Ink, TextAlignmentOptions.MidlineLeft, 2f, true).text = "Elegí el modo";

        // Esc: vuelve a los modos o al menú principal.
        RectTransform back = Place(Node("Volver", top), W - 64f - 330f, 50f, 330f, 36f);
        backButton = back;
        Image(back, null, White(0f), 0f, true);
        backText = Text(Place(Node("Texto", back), 0f, 0f, 330f, 36f), labelFont, 22f, White(0.85f), TextAlignmentOptions.MidlineRight, 8f, true);
        backText.text = "Menú principal";
        RectTransform key = Place(Node("Tecla", back), 0f, 0f, 56f, 36f);
        Image(key, rounded, Ink, 6f);
        Text(Stretch(Node("Esc", key)), monoFont, 18f, Hex(0x111111), TextAlignmentOptions.Center, 4f, true).text = "Esc";
        key.GetComponent<Img>().color = Color.white;
        var button = back.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.transition = UnityEngine.UI.Selectable.Transition.None;
        button.onClick.AddListener(Back);
        PlaceBackKey();
    }

    private void PlaceBackKey()
    {
        RectTransform key = (RectTransform)backText.transform.parent.Find("Tecla");
        float w = Width(backText, backText.text.ToUpperInvariant());
        key.anchoredPosition = new Vector2(330f - w - 12f - 56f, 0f);
    }

    private Detail BuildDetail(int i)
    {
        var d = new Detail();
        d.rect = Place(Node("Detalle" + Titles[i], root), 150f, 150f, 900f, 860f);
        d.group = d.rect.gameObject.AddComponent<CanvasGroup>();
        Color c = ModeColor[i];

        Text(Place(Node("Anden", d.rect), 0f, 0f, 900f, 26f), labelFont, 24f, c, TextAlignmentOptions.MidlineLeft, 32f, true).text = DetailEyebrow[i];
        Text(Place(Node("Nombre", d.rect), 0f, 36f, 900f, 118f), displayFont, 128f, Ink, TextAlignmentOptions.MidlineLeft, 1f, true).text = Titles[i];
        TextMeshProUGUI body = Text(Place(Node("Texto", d.rect), 0f, 166f, 760f, 120f), bodyFont, 28f, White(0.85f), TextAlignmentOptions.TopLeft);
        body.textWrappingMode = TextWrappingModes.Normal;
        body.text = DetailText[i];
        float y = 166f + body.GetPreferredValues(DetailText[i], 760f, 0f).y + 24f;

        if (i < 2)
        {
            float x = 0f;
            foreach (string rule in Rules[i])
            {
                RectTransform chip = Chip(d.rect, rule, 21f, Ink, White(0.14f), 0.25f);
                chip.anchoredPosition = new Vector2(x, -y);
                x += chip.sizeDelta.x + 12f;
            }
            y += 41f + 30f;
            d.actionsY = y;
            int mode = i;
            float bx = ButtonAt(d.rect, 0f, y, "Crear sala", true, c, () => CreateRoom(mode)).x;
            ButtonAt(d.rect, bx + 18f, y, "Unirse con código", false, c, () => ShowJoin(mode));

            d.codeRow = Place(Node("Codigo", d.rect), 0f, y + 78f + 24f, 900f, 72f);
            d.code = CodeInput(d.codeRow, c);
            ButtonAt(d.codeRow, 318f, 0f, "Entrar", false, c, () => Join(mode), 30f, 72f);
            d.codeRow.gameObject.SetActive(false);

            d.room = BuildRoom(d, y + 78f + 22f, c);
            d.room.gameObject.SetActive(false);
            d.status = Text(Place(Node("Aviso", d.rect), 0f, y + 78f + 24f + 72f + 16f, 900f, 30f), bodyFont, 22f, White(0.7f), TextAlignmentOptions.MidlineLeft);
        }
        else
        {
            float bw = 280f;
            for (int k = 0; k < 3; k++)
            {
                int level = k;
                RectTransform card = Place(Node("Dificultad" + DifficultyName[k], d.rect), k * (bw + 14f), y + 6f, bw, 150f);
                Image(card, rounded, new Color(0f, 0f, 0f, DarkAlpha(0.45f)), 10f, true);
                Border(card, 8f, White(0.16f));
                Text(Place(Node("Nombre", card), 20f, 18f, bw - 40f, 36f), displayFont, 34f, Ink, TextAlignmentOptions.MidlineLeft, 4f, true).text = DifficultyName[k];
                TextMeshProUGUI detail = Text(Place(Node("Texto", card), 20f, 60f, bw - 40f, 80f), bodyFont, 18f, White(0.7f), TextAlignmentOptions.TopLeft);
                detail.textWrappingMode = TextWrappingModes.Normal;
                detail.text = DifficultyText[k];
                var button = card.gameObject.AddComponent<UnityEngine.UI.Button>();
                button.transition = UnityEngine.UI.Selectable.Transition.None;
                button.onClick.AddListener(() => SelectDifficulty((ZombieDifficulty)level));
                card.gameObject.AddComponent<ButtonHoverAnimation>();
                CenterPivot(card);
                difficultyButtons[k] = button;
            }
            y += 6f + 150f + 30f;
            record = Text(Place(Node("Record", d.rect), 0f, y, 900f, 30f), monoFont, 24f, White(0.8f), TextAlignmentOptions.MidlineLeft);
            y += 30f + 28f;
            d.actionsY = y;
            ButtonAt(d.rect, 0f, y, "Jugar", true, c, PlayZombie);
            d.status = Text(Place(Node("Aviso", d.rect), 0f, y + 78f + 20f, 900f, 30f), bodyFont, 22f, White(0.7f), TextAlignmentOptions.MidlineLeft);
            SelectDifficulty(MatchSettings.Difficulty);
        }
        return d;
    }

    private RectTransform BuildRoom(Detail d, float y, Color c)
    {
        RectTransform room = Place(Node("Sala", d.rect), 0f, y, 720f, 330f);
        Image(room, rounded, new Color(0f, 0f, 0f, DarkAlpha(0.55f)), 10f);
        Border(room, 8f, White(0.18f));
        Text(Place(Node("Sala", room), 26f, 22f, 200f, 40f), labelFont, 22f, White(0.7f), TextAlignmentOptions.MidlineLeft, 10f, true).text = "Sala";
        d.roomCode = Text(Place(Node("Codigo", room), 160f, 22f, 400f, 40f), monoFont, 40f, c, TextAlignmentOptions.Center, 30f);
        d.roomCount = Text(Place(Node("Jugadores", room), 494f, 22f, 200f, 40f), labelFont, 22f, White(0.7f), TextAlignmentOptions.MidlineRight, 10f, true);
        for (int s = 0; s < 8; s++)
        {
            float x = 26f + (s % 2) * 344f, sy = 80f + (s / 2) * 40f;
            Segment(room, new Vector2(x, sy + 38f), new Vector2(x + 324f, sy + 38f), 1f, White(FadeAlpha(0.08f)));
            d.slots[s] = Text(Place(Node("Lugar", room), x, sy, 324f, 36f), bodyFont, 21f, Ink, TextAlignmentOptions.MidlineLeft);
        }
        d.roomFoot = Text(Place(Node("Pie", room), 26f, 260f, 420f, 50f), bodyFont, 20f, White(0.7f), TextAlignmentOptions.MidlineLeft);
        UnityEngine.UI.Button start = ButtonRect(room, 0f, 0f, "Iniciar partida", true, c, null, 26f, 54f).GetComponent<UnityEngine.UI.Button>();
        RectTransform startRect = (RectTransform)start.transform;
        startRect.anchoredPosition = new Vector2(720f - 26f - startRect.sizeDelta.x / 2f, -(260f + 27f));
        start.interactable = false;
        start.gameObject.AddComponent<CanvasGroup>().alpha = 0.4f;
        return room;
    }

    // Botón con el estilo del artifact. Devuelve la esquina derecha (x) para ubicar el siguiente.
    private Vector2 ButtonAt(RectTransform parent, float x, float y, string label, bool primary, Color c, UnityAction onClick, float size = 34f, float height = 78f)
    {
        RectTransform rect = ButtonRect(parent, x, y, label, primary, c, onClick, size, height);
        return new Vector2(x + rect.sizeDelta.x, y);
    }

    private RectTransform ButtonRect(RectTransform parent, float x, float y, string label, bool primary, Color c, UnityAction onClick, float size, float height)
    {
        RectTransform rect = Node("Boton" + label, parent);
        TextMeshProUGUI text = Text(Stretch(Node("Texto", rect)), displayFont, size, primary ? Hex(0x111111) : Ink, TextAlignmentOptions.Center, 6f, true);
        text.text = label;
        float w = Width(text, label.ToUpperInvariant()) + (size > 30f ? 76f : 48f);
        Place(rect, x, y, w, height);
        Image(rect, rounded, primary ? c : new Color(0f, 0f, 0f, DarkAlpha(0.25f)), 8f, true);
        if (!primary) Border(rect, 8f, White(0.4f));
        text.transform.SetAsLastSibling();
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.transition = UnityEngine.UI.Selectable.Transition.None;
        if (onClick != null) button.onClick.AddListener(onClick);
        rect.gameObject.AddComponent<ButtonHoverAnimation>();
        CenterPivot(rect);
        return rect;
    }

    private static void CenterPivot(RectTransform rect)
    {
        Vector2 size = rect.sizeDelta;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition += new Vector2(size.x / 2f, -size.y / 2f);
    }

    private TMP_InputField CodeInput(RectTransform row, Color c)
    {
        RectTransform box = Place(Node("Campo", row), 0f, 0f, 300f, 72f);
        box.gameObject.SetActive(false);
        Image(box, rounded, new Color(0f, 0f, 0f, DarkAlpha(0.5f)), 8f, true);
        Border(box, 8f, White(0.35f));
        RectTransform area = Stretch(Node("Area", box));
        area.offsetMin = new Vector2(20f, 0f);
        area.offsetMax = new Vector2(-12f, 0f);
        area.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        TextMeshProUGUI placeholder = Text(Stretch(Node("Ejemplo", area)), monoFont, 44f, White(0.25f), TextAlignmentOptions.MidlineLeft, 35f);
        placeholder.text = "KX7QM";
        TextMeshProUGUI value = Text(Stretch(Node("Texto", area)), monoFont, 44f, Ink, TextAlignmentOptions.MidlineLeft, 35f);

        TMP_InputField input = box.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = area;
        input.textComponent = value;
        input.placeholder = placeholder;
        input.fontAsset = monoFont;
        input.pointSize = 44f;
        input.characterLimit = 5;
        input.customCaretColor = true;
        input.caretColor = c;
        input.caretWidth = 3;
        input.selectionColor = WithAlpha(c, 0.35f);
        input.onValidateInput = (s, index, ch) => char.IsLetterOrDigit(ch) && ch < 128 ? char.ToUpperInvariant(ch) : '\0';
        input.onSubmit.AddListener(_ => Join(open));
        box.gameObject.SetActive(true);
        return input;
    }

    // Degradé de la sombra que deja leer el detalle: negro a la izquierda, transparente a partir del 72 %.
    private static Sprite GradientSprite()
    {
        var tex = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int x = 0; x < 256; x++)
        {
            float t = x / 255f;
            float a = t < 0.45f ? Mathf.Lerp(0.82f, 0.55f, t / 0.45f) : Mathf.Lerp(0.55f, 0f, Mathf.InverseLerp(0.45f, 0.72f, t));
            tex.SetPixel(x, 0, new Color(0f, 0f, 0f, DarkAlpha(a)));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, 256f, 1f), new Vector2(0.5f, 0.5f));
    }

    // ================= Acciones =================

    private void OpenMode(int i)
    {
        if (i < 0 || i > 2) return;
        open = i;
        hover = -1;
        openAt = Time.unscaledTime;
        MatchSettings.Mode = (GameMode)i;
        ResetDetail(i);
        backText.text = "Volver a los modos";
        PlaceBackKey();
        Strike(0, true);
        Strike(1, true);
    }

    private void CloseMode()
    {
        if (open < 0) return;
        open = -1;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        backText.text = "Menú principal";
        PlaceBackKey();
        Strike(0, true);
        Strike(1, true);
    }

    private void Back()
    {
        if (open >= 0) CloseMode();
        else if (menu != null) menu.MostrarMenuPrincipal();
    }

    private void ResetDetail(int i)
    {
        Detail d = details[i];
        if (d.codeRow != null) d.codeRow.gameObject.SetActive(false);
        if (d.room != null) d.room.gameObject.SetActive(false);
        if (d.code != null) d.code.text = "";
        d.status.text = "";
    }

    private void CreateRoom(int mode)
    {
        Detail d = details[mode];
        string code = "";
        for (int k = 0; k < 5; k++) code += CodeAlphabet[UnityEngine.Random.Range(0, CodeAlphabet.Length)];
        MatchSettings.Mode = (GameMode)mode;
        MatchSettings.RoomCode = code;
        onCreateRoom.Invoke((GameMode)mode);

        d.codeRow.gameObject.SetActive(false);
        d.status.text = "";
        ShowRoom(d, code, new[] { "Vos  <color=#FFD2A1>· anfitrión</color>" },
            onCreateRoom.GetPersistentEventCount() == 0 ? "Falta conectar el multijugador (SPK 01)." : "Pasale el código a tus amigos.");
    }

    private void ShowJoin(int mode)
    {
        Detail d = details[mode];
        d.room.gameObject.SetActive(false);
        d.codeRow.gameObject.SetActive(true);
        d.status.text = "";
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(d.code.gameObject);
        d.code.ActivateInputField();
    }

    private void Join(int mode)
    {
        if (mode < 0 || mode > 1) return;
        Detail d = details[mode];
        string code = d.code.text.ToUpperInvariant();
        if (code.Length != 5) { d.status.text = "El código tiene 5 letras."; d.code.ActivateInputField(); return; }
        MatchSettings.Mode = (GameMode)mode;
        MatchSettings.RoomCode = code;
        onJoinRoom.Invoke((GameMode)mode, code);
        if (onJoinRoom.GetPersistentEventCount() == 0)
            d.status.text = "Todavía no hay multijugador: la conexión con Photon está en el SPK 01.";
    }

    private void ShowRoom(Detail d, string code, string[] players, string foot)
    {
        d.roomCode.text = code;
        d.roomCount.text = $"{players.Length} / 8";
        for (int s = 0; s < 8; s++)
        {
            bool taken = s < players.Length;
            d.slots[s].text = taken ? players[s] : "Libre";
            d.slots[s].color = taken ? Ink : White(FadeAlpha(0.35f));
        }
        d.roomFoot.text = foot;
        d.room.gameObject.SetActive(true);
    }

    private void SelectDifficulty(ZombieDifficulty difficulty)
    {
        MatchSettings.Difficulty = difficulty;
        for (int k = 0; k < 3; k++)
        {
            bool on = k == (int)difficulty;
            Transform card = difficultyButtons[k].transform;
            card.GetComponent<Img>().color = on ? Over(WithAlpha(ModeColor[2], 0.12f), Hex(0x0B0F0B)) : new Color(0f, 0f, 0f, DarkAlpha(0.45f));
            Transform edge = card.Find("Borde");
            if (edge != null) edge.GetComponent<Img>().color = on ? ModeColor[2] : White(0.16f);
        }
        int best = MatchSettings.ZombieRecord(difficulty);
        string name = DifficultyName[(int)difficulty];
        record.text = best > 0
            ? $"Récord en {name}: <color={ModeHex[2]}>oleada {best}</color>"
            : $"Récord en {name}: <color={ModeHex[2]}>todavía no jugaste</color>";
    }

    private void PlayZombie()
    {
        MatchSettings.Mode = GameMode.Zombie;
        onPlayZombie.Invoke(MatchSettings.Difficulty);
        if (!string.IsNullOrEmpty(zombieScene) && Application.CanStreamedLevelBeLoaded(zombieScene))
            SceneManager.LoadScene(zombieScene);
        else if (onPlayZombie.GetPersistentEventCount() == 0)
            details[2].status.text = "El modo Zombie todavía está en desarrollo (F08).";
    }

    // ================= Cuadro a cuadro =================

    private void Update()
    {
        float now = Time.unscaledTime, dt = Mathf.Min(0.05f, Time.unscaledDeltaTime);
        Fit();
        HandleInput();

        ComputeTargets();
        float k = 1f - Mathf.Exp(-dt * 9f);
        bool moving = false;
        for (int i = 0; i < 2; i++)
        {
            cur[i] += (target[i] - cur[i]) * k;
            moving |= Mathf.Abs(target[i] - cur[i]) > 0.3f;
        }
        float[] centers = { cur[0] / 2f, (cur[0] + cur[1]) / 2f, (cur[1] + W) / 2f };
        for (int i = 0; i < 3; i++)
        {
            float goal = open == i ? 1320f + 20f * i : centers[i];
            motX[i] += (goal - motX[i]) * k;
            moving |= Mathf.Abs(goal - motX[i]) > 0.3f;
        }

        UpdateBolts(now);
        if (dirty || moving) { Layout(); dirty = false; }

        float intro = now - introAt;
        float boltDraw = EaseOut(Mathf.Clamp01(intro / 0.45f));
        foreach (Bolt b in bolts)
            if (b.core.drawFraction < 1f || boltDraw < 1f)
            {
                b.glow.drawFraction = b.mid.drawFraction = b.core.drawFraction = boltDraw;
                b.glow.Refresh(); b.mid.Refresh(); b.core.Refresh();
            }
        float panelsIn = Mathf.Clamp01((intro - 0.22f) / 0.55f);
        for (int i = 0; i < 3; i++)
        {
            Anden a = andenes[i];
            a.group.alpha = panelsIn;
            float dimGoal = open < 0 && hover >= 0 && hover != i ? DarkAlpha(0.52f) : 0f;
            a.dim.color = new Color(0f, 0f, 0f, Mathf.MoveTowards(a.dim.color.a, dimGoal, dt / 0.28f));
            a.flash.color = White(0.10f * Mathf.Exp(-(now - a.flashAt) / 0.15f));
        }

        UpdateLabels(intro, dt);
        UpdateTop(intro, dt);
        UpdateDetails(now, dt);
        AnimateTactico(now, dt);
        AnimateDeathmatch(now, dt, centers);
        AnimateZombie(now);

        if (now > nextStrike) { Strike(UnityEngine.Random.value < 0.5f ? 0 : 1, false); nextStrike = now + UnityEngine.Random.Range(2.6f, 5.6f); }
    }

    private void Fit()
    {
        Vector2 size = ((RectTransform)transform).rect.size;
        if (size.x <= 0f || size.y <= 0f) return;
        root.localScale = Vector3.one * Mathf.Max(size.x / W, size.y / H);
    }

    private void ComputeTargets()
    {
        if (open == 0) { target[0] = W + 260f; target[1] = W + 560f; }
        else if (open == 1) { target[0] = -260f; target[1] = W + 260f; }
        else if (open == 2) { target[0] = -560f; target[1] = -260f; }
        else if (hover >= 0)
        {
            float w0 = hover == 0 ? 0.46f : 0.27f, w1 = hover == 1 ? 0.46f : 0.27f;
            target[0] = w0 * W; target[1] = (w0 + w1) * W;
        }
        else { target[0] = W / 3f; target[1] = 2f * W / 3f; }
    }

    private float XAt(int k, float y) => cur[k] + bolts[k].slant * (1f - 2f * y / H);
    private int PanelAt(Vector2 p) => p.x < XAt(0, p.y) ? 0 : p.x < XAt(1, p.y) ? 1 : 2;

    private void BoltPoints(int k, Vector2[] into)
    {
        Bolt b = bolts[k];
        for (int j = 0; j <= N; j++)
        {
            float t = j / (float)N;
            into[j] = new Vector2(cur[k] + b.slant * (1f - 2f * t) + b.jit[j], t * H);
        }
        into[0] = new Vector2(into[0].x + b.slant * 0.04f, -24f);
        into[N] = new Vector2(into[N].x - b.slant * 0.04f, H + 24f);
    }

    private void Layout()
    {
        BoltPoints(0, p0);
        BoltPoints(1, p1);

        scratch.Clear();
        scratch.Add(new Vector2(-60f, -60f)); scratch.AddRange(p0); scratch.Add(new Vector2(-60f, H + 60f));
        andenes[0].shape.SetPoints(scratch);
        scratch.Clear();
        scratch.AddRange(p0); for (int j = N; j >= 0; j--) scratch.Add(p1[j]);
        andenes[1].shape.SetPoints(scratch);
        scratch.Clear();
        scratch.AddRange(p1); scratch.Add(new Vector2(W + 60f, H + 60f)); scratch.Add(new Vector2(W + 60f, -60f));
        andenes[2].shape.SetPoints(scratch);

        for (int k = 0; k < 2; k++)
        {
            Vector2[] pts = k == 0 ? p0 : p1;
            Bolt b = bolts[k];
            foreach (UIPolyline line in new[] { b.glow, b.mid, b.core })
            {
                line.gradientX0 = cur[k] - 170f;
                line.gradientX1 = cur[k] + 170f;
                line.SetPoints(pts);
            }
        }
        for (int i = 0; i < 3; i++) andenes[i].motif.anchoredPosition = new Vector2(motX[i], -540f);

        // Cada título va entre los rayos reales (con su zigzag) en la franja donde está escrito, y se achica si no entra.
        float left0 = 0f, right2 = W;
        float min0 = float.MaxValue, max0 = float.MinValue, min1 = float.MaxValue, max1 = float.MinValue;
        for (int j = 0; j <= N; j++)
        {
            if (p0[j].y < LabelTop - 100f || p0[j].y > LabelBottom + 100f) continue;
            min0 = Mathf.Min(min0, p0[j].x); max0 = Mathf.Max(max0, p0[j].x);
            min1 = Mathf.Min(min1, p1[j].x); max1 = Mathf.Max(max1, p1[j].x);
        }
        float[,] lim = { { left0 + 40f, min0 - 70f }, { max0 + 70f, min1 - 70f }, { max1 + 70f, right2 - 40f } };
        for (int i = 0; i < 3; i++)
        {
            Label l = labels[i];
            float w = Mathf.Min(640f, lim[i, 1] - lim[i, 0]);
            float x = (lim[i, 0] + lim[i, 1]) / 2f - w / 2f;
            l.rect.anchoredPosition = new Vector2(x, l.rect.anchoredPosition.y);
            l.visible = open < 0 && w > 330f ? 1f : 0f;
            if (Mathf.Abs(w - l.lastWidth) > 0.5f && w > 0f) RelayoutLabel(l, w);
        }
    }

    private void RelayoutLabel(Label l, float w)
    {
        l.lastWidth = w;
        l.rect.sizeDelta = new Vector2(w, l.rect.sizeDelta.y);
        l.title.fontSize = Mathf.Min(124f, 124f * w / Mathf.Max(1f, l.natural));
        float blurbH = l.blurb.GetPreferredValues(l.blurb.text, w, 0f).y;
        l.blurb.rectTransform.sizeDelta = new Vector2(w, blurbH);
        l.blurb.rectTransform.anchoredPosition = new Vector2(0f, -162f);

        // Etiquetas centradas, en una o dos filas según entren.
        float y = 162f + blurbH + 22f, rowW = 0f;
        var row = new List<RectTransform>();
        void Flush()
        {
            float x = (w - rowW + 10f) / 2f;
            foreach (RectTransform c in row) { c.anchoredPosition = new Vector2(x, -y); x += c.sizeDelta.x + 10f; }
            row.Clear();
            rowW = 0f;
        }
        foreach (RectTransform chip in l.chips)
        {
            float cw = chip.sizeDelta.x + 10f;
            if (row.Count > 0 && rowW + cw > w) { Flush(); y += chip.sizeDelta.y + 10f; }
            row.Add(chip);
            rowW += cw;
        }
        Flush();
    }

    private void UpdateLabels(float intro, float dt)
    {
        float inT = Mathf.Clamp01((intro - 0.45f) / 0.55f);
        foreach (Label l in labels)
        {
            l.shown = Mathf.MoveTowards(l.shown, l.visible, dt / 0.22f);
            l.group.alpha = l.shown * inT;
            l.rect.anchoredPosition = new Vector2(l.rect.anchoredPosition.x, -(LabelTop + 24f * (1f - inT)));
        }
    }

    private void UpdateTop(float intro, float dt)
    {
        topGroup.alpha = Mathf.Clamp01((intro - 0.6f) / 0.5f);
        titleGroup.alpha = Mathf.MoveTowards(titleGroup.alpha, open < 0 ? 1f : 0f, dt / 0.25f);
        shadeGroup.alpha = Mathf.MoveTowards(shadeGroup.alpha, open >= 0 ? 1f : 0f, dt / 0.4f);
    }

    private void UpdateDetails(float now, float dt)
    {
        for (int i = 0; i < 3; i++)
        {
            Detail d = details[i];
            bool on = open == i && now - openAt > 0.18f;
            d.group.alpha = Mathf.MoveTowards(d.group.alpha, on ? 1f : 0f, dt / 0.35f);
            d.group.interactable = d.group.blocksRaycasts = open == i;
            d.rect.anchoredPosition = new Vector2(150f - 40f * (1f - d.group.alpha), -150f);
        }
    }

    // ---------- Rayos ----------

    private void NewJitter(Bolt b)
    {
        for (int j = 0; j <= N; j++) b.jit[j] = j == 0 || j == N ? UnityEngine.Random.Range(-8f, 8f) : UnityEngine.Random.Range(-36f, 36f);
        dirty = true;
    }

    private void Strike(int k, bool strong)
    {
        Bolt b = bolts[k];
        b.steps = strong ? 4 : 3;
        b.nextStep = Time.unscaledTime;
        andenes[k].flashAt = andenes[k + 1].flashAt = Time.unscaledTime;

        // Rama lateral que sale del rayo.
        float y0 = UnityEngine.Random.Range(180f, 820f), x0 = XAt(k, y0), dir = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        scratch.Clear();
        scratch.Add(new Vector2(x0, y0));
        for (int s = 1; s <= 4; s++) scratch.Add(new Vector2(x0 + dir * s * UnityEngine.Random.Range(28f, 46f), y0 + s * UnityEngine.Random.Range(22f, 40f)));
        b.branch.SetPoints(scratch);
        b.branchAt = Time.unscaledTime;
    }

    private void UpdateBolts(float now)
    {
        foreach (Bolt b in bolts)
        {
            if (b.steps > 0 && now >= b.nextStep)
            {
                NewJitter(b);
                b.steps--;
                b.nextStep = now + UnityEngine.Random.Range(0.055f, 0.095f);
            }
            bool flashing = b.steps > 0 || now - b.nextStep < 0.09f;
            float glowA = flashing ? FadeAlpha(0.95f) : FadeAlpha(0.5f), glowW = flashing ? 46f : 30f;
            if (!Mathf.Approximately(b.glow.thickness, glowW))
            {
                b.glow.thickness = glowW;
                b.glow.color = WithAlpha(b.glow.color, glowA);
                b.glow.colorB = WithAlpha(b.glow.colorB, glowA);
                b.glow.Refresh();
            }
            float age = now - b.branchAt;
            float branchA = age < 0.11f ? 0.9f : Mathf.Max(0f, 0.9f * (1f - (age - 0.11f) / 0.5f));
            if (!Mathf.Approximately(b.branch.color.a, branchA)) b.branch.color = White(branchA);
        }
    }

    // ---------- Motivos ----------

    private void AnimateTactico(float now, float dt)
    {
        bool hot = hover == 0 || open == 0;
        float period = hot ? 1.2f : 2.6f;
        for (int k = 0; k < pulses.Count; k++)
        {
            float p = Mathf.Repeat(now / period + k / 3f, 1f);
            pulses[k].rectTransform.localScale = Vector3.one * (1f + 4.2f * EaseOut(p));
            pulses[k].color = WithAlpha(ModeColor[0], FadeAlpha(0.75f * (1f - p)));
        }
        float blink = hot ? 0.45f : 1.3f;
        float led = 0.25f + 0.75f * (0.5f + 0.5f * Mathf.Cos(now * Mathf.PI * 2f / blink));
        core.alpha = led;
        for (int k = 0; k < leds.Count; k++)
            leds[k].color = WithAlpha(Bad, 0.25f + 0.75f * (0.5f + 0.5f * Mathf.Cos((now + k * 0.6f) * Mathf.PI * 2f / blink)));
        spin.localEulerAngles = new Vector3(0f, 0f, -360f * now / (hot ? 3f : 9f));
        spinBack.localEulerAngles = new Vector3(0f, 0f, 360f * now / (hot ? 2f : 6f));
        foreach (UIPolyline flow in flows) { flow.dashOffset += dt * 16f / (hot ? 0.45f : 1.1f); flow.Refresh(); }
        float h = Mathf.Repeat(now / 4f, 1f);
        holo.alpha = h > 0.91f && h < 0.93f ? 0.55f : h > 0.95f && h < 0.97f ? 0.7f : 1f;
        int secs = 45 - Mathf.FloorToInt(Mathf.Repeat(now, 46f));
        string value = "0:" + secs.ToString("00");
        if (lcd.text != value) { lcd.text = value; lcd.color = secs <= 10 ? Bad : ModeColor[0]; }
    }

    private void AnimateDeathmatch(float now, float dt, float[] centers)
    {
        bool hot = hover == 1 || open == 1;
        for (int i = 0; i < tracers.Count; i++)
        {
            tracerPhase[i] += dt * tracerSpeed[i] * (hot ? 2.1f : 1f);
            tracers[i].dashOffset = tracerPhase[i] * 3160f;
            tracers[i].Refresh();
        }

        if (crossT < 1f)
        {
            crossT = Mathf.Min(1f, crossT + dt / crossDur);
            float e = 1f - Mathf.Pow(1f - crossT, 3f);
            crossPos = Vector2.Lerp(crossFrom, crossTo, e);
            if (crossT >= 1f) { hitAt = now; crossWait = hot ? 0.18f : 0.52f; }
        }
        else
        {
            crossWait -= dt;
            if (crossWait <= 0f)
            {
                float c = centers[1], half = Mathf.Max(80f, (cur[1] - cur[0]) / 2f - 150f);
                if (open == 1) { c = 1340f; half = 360f; }
                crossFrom = crossPos;
                crossTo = new Vector2(c + UnityEngine.Random.Range(-half, half), UnityEngine.Random.Range(200f, 520f));
                crossT = 0f;
                crossDur = UnityEngine.Random.Range(0.42f, 0.76f);
            }
        }
        cross.anchoredPosition = new Vector2(crossPos.x, -crossPos.y);
        hit.alpha = now - hitAt < 0.12f ? 1f : Mathf.Max(0f, 1f - (now - hitAt - 0.12f) / 0.35f);
    }

    private void AnimateZombie(float now)
    {
        bool hot = hover == 2 || open == 2;
        for (int f = 0; f < fogs.Count; f++)
        {
            float periodFog = f == 0 ? 11f : 7.5f;
            float x = (f == 0 ? -160f : 120f) + 140f * Mathf.Sin(now * Mathf.PI * 2f / periodFog * (f == 0 ? 1f : -1f));
            fogs[f].anchoredPosition = new Vector2(x, fogs[f].anchoredPosition.y);
        }
        for (int z = 0; z < zombies.Count; z++)
        {
            float period = hot ? 3.2f : 5.5f;
            float p = Mathf.Repeat((now + zombieDelay[z]) / period, 1f);
            float low = hot ? 130f : 200f, high = hot ? 10f : 40f;
            float a = hot ? 0.4f : 0.45f, b = hot ? 0.7f : 0.6f;
            float off = p < a ? Mathf.Lerp(low, high, Smooth(p / a)) : p < b ? high : Mathf.Lerp(high, low, Smooth((p - b) / (1f - b)));
            zombies[z].anchoredPosition = new Vector2(zombieBase[z].x, -(zombieBase[z].y + off));
        }
        float eye = 0.25f + 0.75f * (0.5f + 0.5f * Mathf.Cos(now * Mathf.PI * 2f / 2.2f));
        foreach (UIRing e in eyes) e.color = WithAlpha(ModeColor[2], eye);
    }

    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
    private static float Smooth(float t) => t * t * (3f - 2f * t);

    // ---------- Entrada ----------

    private void HandleInput()
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        TMP_InputField typing = selected != null ? selected.GetComponent<TMP_InputField>() : null;
        bool isTyping = typing != null && typing.isFocused;

        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.escapeKey.wasPressedThisFrame) { Back(); return; }
            if (!isTyping && open < 0)
            {
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) OpenMode(0);
                else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) OpenMode(1);
                else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) OpenMode(2);
                else if (kb.rightArrowKey.wasPressedThisFrame) hover = Mathf.Min(2, hover + 1);
                else if (kb.leftArrowKey.wasPressedThisFrame) hover = hover < 0 ? 2 : Mathf.Max(0, hover - 1);
                else if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && hover >= 0) OpenMode(hover);
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse == null || open >= 0) return;
        if (!MouseOnStage(mouse, out Vector2 p)) { if (lastMouse.x >= 0f) { lastMouse = new Vector2(-1f, -1f); SetHover(-1); } return; }
        if ((p - lastMouse).sqrMagnitude > 0.5f) { lastMouse = p; SetHover(PanelAt(p)); }
    }

    private void OnStageClick(PointerEventData data)
    {
        if (open >= 0 || data.button != PointerEventData.InputButton.Left) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, data.position, UiCamera(), out Vector2 local)) return;
        OpenMode(PanelAt(new Vector2(local.x + W / 2f, H / 2f - local.y)));
    }

    private void SetHover(int i)
    {
        if (i == hover) return;
        hover = i;
        if (i >= 0) { Strike(i == 2 ? 1 : 0, false); if (i == 1) Strike(1, false); }
    }

    private Camera UiCamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }

    private bool MouseOnStage(Mouse mouse, out Vector2 p)
    {
        p = default;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, mouse.position.ReadValue(), UiCamera(), out Vector2 local)) return false;
        p = new Vector2(local.x + W / 2f, H / 2f - local.y);
        return p.x >= 0f && p.x <= W && p.y >= 0f && p.y <= H;
    }
}

// Pasa el clic de una imagen invisible a la pantalla de modos.
public class ClickCatcher : MonoBehaviour, IPointerClickHandler
{
    public Action<PointerEventData> Clicked;
    public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(eventData);
}
