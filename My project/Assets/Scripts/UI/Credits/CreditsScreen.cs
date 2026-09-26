using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using static ShopUIKit;
using Img = UnityEngine.UI.Image;

// Créditos (US 085) al estilo Star Wars: fondo de estrellas, un prólogo, el título que se aleja y los créditos que
// suben inclinados hacia el fondo. Espacio o el clic sostenido aceleran; Esc vuelve al menú. Al terminar, vuelve solo.
// El texto inclinado se logra proyectando en perspectiva la malla de cada letra, así funciona en el Canvas del menú.
public class CreditsScreen : MonoBehaviour
{
    [Serializable] public class Role { public string role; [TextArea] public string names; }
    [Serializable] public class Credit { public string item; public string source; }

    [Header("Tipografías")]
    [SerializeField] private TMP_FontAsset displayFont; // Barlow Condensed Bold
    [SerializeField] private TMP_FontAsset labelFont;   // Barlow Condensed SemiBold
    [SerializeField] private TMP_FontAsset bodyFont;    // Barlow Regular
    [SerializeField] private TMP_FontAsset monoFont;    // JetBrains Mono
    [SerializeField] private Sprite rounded;

    [Header("Contenido")]
    [SerializeField] private string gameTitle = "Shooter Legends";
    [SerializeField, TextArea] private string prologue = "Hace no tanto tiempo, en una universidad\nmuy, muy cercana…";
    [SerializeField, TextArea] private string lede = "Un shooter táctico, un deathmatch y una horda de zombis en la universidad. Hecho por siete estudiantes en diez semanas, con más café que horas de sueño.";
    [SerializeField] private List<Role> team = new List<Role>
    {
        new Role { role = "Scrum Master", names = "Ulises Fonseca" },
        new Role { role = "Desarrollo y mecánicas", names = "Nicolás Gauna\nFranco Barreto\nNahuel Velázquez\nUlises Fonseca" },
        new Role { role = "Arte 2D/3D y diseño de niveles", names = "Ulises Fonseca\nNahuel Velázquez" },
        new Role { role = "Interfaz (UI/UX)", names = "Lucas Huber\nNicolás Gauna\nLuka Gómez" },
        new Role { role = "Sonido y música", names = "David Ramos" },
        new Role { role = "Testing y QA", names = "Lucas Huber\nDavid Ramos" },
        new Role { role = "Documentación y backlog", names = "Luka Gómez\nFranco Barreto" },
        new Role { role = "Repositorio y Git", names = "Ulises Fonseca" }
    };
    [SerializeField, TextArea] private string tools = "Unity 6 · Universal Render Pipeline\nProBuilder · TextMesh Pro · Input System · AI Navigation\nPhoton PUN 2 (multijugador)\nGitHub y GitHub Projects";
    [SerializeField] private List<Credit> assets = new List<Credit>
    {
        new Credit { item = "Personaje y animaciones", source = "Mixamo, de Adobe" },
        new Credit { item = "Escenario sci-fi", source = "3D Scifi Kit Starter Kit, de Creepy Cat (Unity Asset Store)" },
        new Credit { item = "Cuchillo", source = "Sci-fi Weapon - Game Ready Knife - FREE SAMPLE, de AF Creations (Unity Asset Store)" },
        new Credit { item = "Texturas", source = "Cartoon Texture Pack" }
    };
    [SerializeField] private List<Credit> fonts = new List<Credit>
    {
        new Credit { item = "Barlow", source = "Jeremy Tribby · SIL Open Font License 1.1" },
        new Credit { item = "JetBrains Mono", source = "JetBrains · SIL Open Font License 1.1" }
    };
    [SerializeField, TextArea] private string assignment = "Trabajo práctico \"Último Tren a Retiro\"\nLaboratorio de Construcción de Software\nTecnicatura Universitaria en Informática";
    [SerializeField, TextArea] private string thanks = "A la cátedra, a los que probaron las builds rotas\ny a vos, por jugar.";
    [SerializeField] private string ending = "Gracias por jugar";

    // Maqueta: pantalla de 1920 x 1080; el texto va en un plano inclinado 26° con perspectiva de 520 px.
    private const float W = 1920f, H = 1080f, PageW = 1180f;
    private const float Tilt = 26f, Perspective = 520f, OriginY = 237.6f;
    private const float ProIn = 0.6f, ProOut = 4.6f, ProEnd = 5.4f, LogoStart = 5.6f, LogoEnd = 12f, CrawlStart = 8.5f;
    private const float Speed = 62f, FastFactor = 5f;

    private class Block
    {
        public TextMeshProUGUI text;
        public float top;
        public Vector3[][] original;
    }

    private RectTransform root;
    private StarField stars;
    private TextMeshProUGUI prologueText, logo;
    private readonly List<Block> blocks = new List<Block>();
    private float pageHeight, clock, lastScroll;
    private bool leaving;
    private MenuUIController menu;
    private Color gold, pale, orange;

    private void Awake()
    {
        menu = GetComponentInParent<MenuUIController>();
        gold = Hex(0xF2B544); pale = Hex(0xFFE6B3); orange = Hex(0xF29A38);
        Build();
    }

    private void OnEnable()
    {
        clock = 0f;
        leaving = false;
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextRegenerated);
    }

    private void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextRegenerated);
    }

    // Si TextMeshPro vuelve a generar la malla de un renglón (al activarse, al reconstruir el Canvas), se reproyecta enseguida.
    private void OnTextRegenerated(UnityEngine.Object obj)
    {
        foreach (Block b in blocks)
            if (b.text == obj) { Project(b, lastScroll); return; }
    }

    // ================= Armado =================

    private void Build()
    {
        Image(Stretch(Node("Fondo", transform)), null, Color.black);

        root = Node("Creditos", transform);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(W, H);

        RectTransform starRect = Place(Node("Estrellas", root), 0f, 0f, W, H);
        starRect.gameObject.AddComponent<CanvasRenderer>();
        stars = starRect.gameObject.AddComponent<StarField>();
        stars.raycastTarget = false;

        prologueText = Text(Place(Node("Prologo", root), 0f, 400f, W, 160f), bodyFont, 44f, Hex(0x4BD5EE), TextAlignmentOptions.Center);
        prologueText.text = prologue;
        prologueText.lineSpacing = 20f;

        logo = Text(Node("Titulo", root), displayFont, 240f, gold, TextAlignmentOptions.Center, 4f, true);
        RectTransform logoRect = logo.rectTransform;
        logoRect.anchorMin = logoRect.anchorMax = logoRect.pivot = new Vector2(0.5f, 0.5f);
        logoRect.anchoredPosition = Vector2.zero;
        logoRect.sizeDelta = new Vector2(W, 500f);
        logo.text = gameTitle.Replace(" ", "\n");
        logo.lineSpacing = -30f;
        logo.faceColor = new Color32(0, 0, 0, 0);
        logo.outlineColor = gold;
        logo.outlineWidth = 0.14f;
        logo.fontMaterial.EnableKeyword("OUTLINE_ON"); // el shader Mobile solo dibuja el contorno con esta opción

        BuildCrawl();

        // Oscurece arriba, donde el texto se pierde a lo lejos.
        Image(Place(Node("Niebla", root), 0f, 0f, W, 420f), FadeSprite(), Color.black);

        BuildHint();
    }

    private void BuildCrawl()
    {
        RectTransform crawl = Place(Node("Texto", root), 0f, 0f, W, H);
        float y = 0f;
        Add(crawl, ref y, 0f, "Créditos", labelFont, 40f, gold, 30f, true);
        Add(crawl, ref y, 10f, gameTitle, displayFont, 120f, gold, 4f, true);
        Add(crawl, ref y, 50f, lede, bodyFont, 46f, gold, 0f, false);
        y += 40f;
        foreach (Role r in team)
        {
            Add(crawl, ref y, 70f, r.role, labelFont, 38f, orange, 22f, true);
            Add(crawl, ref y, 18f, r.names, displayFont, 64f, pale, 3f, true);
        }
        Add(crawl, ref y, 110f, "· · ·", displayFont, 44f, orange, 0f, false);
        Add(crawl, ref y, 20f, "Motor y herramientas", labelFont, 38f, orange, 22f, true);
        Add(crawl, ref y, 18f, tools, bodyFont, 42f, gold, 0f, false);
        Add(crawl, ref y, 70f, "Recursos externos", labelFont, 38f, orange, 22f, true);
        foreach (Credit c in assets)
        {
            Add(crawl, ref y, 18f, c.item, displayFont, 44f, pale, 4f, true);
            Add(crawl, ref y, 4f, c.source, bodyFont, 36f, gold, 0f, false);
        }
        Add(crawl, ref y, 70f, "Tipografías", labelFont, 38f, orange, 22f, true);
        foreach (Credit c in fonts)
        {
            Add(crawl, ref y, 18f, c.item, displayFont, 44f, pale, 4f, true);
            Add(crawl, ref y, 4f, c.source, bodyFont, 36f, gold, 0f, false);
        }
        Add(crawl, ref y, 110f, "· · ·", displayFont, 44f, orange, 0f, false);
        Add(crawl, ref y, 20f, "Trabajo práctico", labelFont, 38f, orange, 22f, true);
        Add(crawl, ref y, 18f, assignment, bodyFont, 42f, gold, 0f, false);
        Add(crawl, ref y, 70f, "Agradecimientos", labelFont, 38f, orange, 22f, true);
        Add(crawl, ref y, 18f, thanks, bodyFont, 42f, gold, 0f, false);
        Add(crawl, ref y, 200f, ending, displayFont, 110f, pale, 6f, true);
        pageHeight = y;
    }

    private void Add(RectTransform parent, ref float y, float gap, string value, TMP_FontAsset font, float size, Color color, float spacing, bool upper)
    {
        y += gap;
        TextMeshProUGUI text = Text(Place(Node("Linea", parent), 0f, 0f, PageW, 10f), font, size, color, TextAlignmentOptions.Top, spacing, upper);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.text = value;
        float height = text.GetPreferredValues(upper ? value.ToUpperInvariant() : value, PageW, 0f).y;
        text.rectTransform.sizeDelta = new Vector2(PageW, height);
        text.ForceMeshUpdate();

        var block = new Block { text = text, top = y, original = new Vector3[text.textInfo.meshInfo.Length][] };
        for (int m = 0; m < block.original.Length; m++)
            block.original[m] = (Vector3[])text.textInfo.meshInfo[m].vertices.Clone();
        blocks.Add(block);
        y += height;
    }

    private void BuildHint()
    {
        RectTransform hint = Place(Node("Ayuda", root), W - 64f - 420f, H - 44f - 36f, 420f, 36f);
        var group = hint.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0.75f;
        group.blocksRaycasts = false;
        float x = 420f;
        foreach (var (key, label) in new[] { ("Esc", "Volver"), ("Espacio", "Acelerar") })
        {
            TextMeshProUGUI text = Text(Node("Accion", hint), labelFont, 22f, White(0.85f), TextAlignmentOptions.MidlineLeft, 8f, true);
            text.text = label;
            float tw = Width(text, label.ToUpperInvariant());
            RectTransform keyRect = Node("Tecla", hint);
            TextMeshProUGUI keyText = Text(Stretch(Node("Letra", keyRect)), monoFont, 18f, Hex(0x111111), TextAlignmentOptions.Center, 4f, true);
            keyText.text = key;
            float kw = Width(keyText, key.ToUpperInvariant()) + 22f;
            x -= tw;
            Place(text.rectTransform, x, 0f, tw, 36f);
            x -= 12f + kw;
            Place(keyRect, x, 0f, kw, 36f);
            Image(keyRect, rounded, Color.white, 6f);
            keyText.transform.SetAsLastSibling();
            x -= 30f;
        }
    }

    // Degradé vertical: negro arriba y transparente abajo.
    private static Sprite FadeSprite()
    {
        var tex = new Texture2D(1, 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 256; y++)
        {
            float t = 1f - y / 255f; // 0 arriba, 1 abajo
            float a = t < 0.35f ? Mathf.Lerp(1f, 0.85f, t / 0.35f) : Mathf.Lerp(0.85f, 0f, (t - 0.35f) / 0.65f);
            tex.SetPixel(0, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, 1f, 256f), new Vector2(0.5f, 0.5f));
    }

    // ================= Cuadro a cuadro =================

    private void Update()
    {
        float dt = Mathf.Min(0.05f, Time.unscaledDeltaTime);
        if (leaving) return; // ya pidió volver al menú: la transición del menú necesita unos cuadros para terminar
        Vector2 size = ((RectTransform)transform).rect.size;
        if (size.x > 0f && size.y > 0f) root.localScale = Vector3.one * Mathf.Max(size.x / W, size.y / H);

        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) { Back(); return; }
        bool fast = (kb != null && kb.spaceKey.isPressed) || (mouse != null && mouse.leftButton.isPressed);
        clock += dt * (fast ? FastFactor : 1f);

        stars.time = Time.unscaledTime;
        stars.SetVerticesDirty();

        float pa = clock < ProIn ? clock / ProIn : clock < ProOut ? 1f : Mathf.Max(0f, 1f - (clock - ProOut) / (ProEnd - ProOut));
        prologueText.alpha = pa;

        if (clock >= LogoStart && clock < LogoEnd)
        {
            float t = (clock - LogoStart) / (LogoEnd - LogoStart);
            logo.rectTransform.localScale = Vector3.one * (1.25f * Mathf.Pow(0.03f, t));
            logo.alpha = t < 0.04f ? t / 0.04f : t > 0.85f ? Mathf.Max(0f, (1f - t) / 0.15f) : 1f;
        }
        else logo.alpha = 0f;

        float scroll = Mathf.Max(0f, clock - CrawlStart) * Speed - 80f; // arranca apenas por debajo del borde
        lastScroll = scroll;
        foreach (Block b in blocks) Project(b, scroll);

        // Cuando el último renglón ya se perdió a lo lejos, vuelve al menú.
        if (scroll > pageHeight + 1500f) Back();
    }

    private void Project(Block b, float scroll)
    {
        TMP_TextInfo info = b.text.textInfo;
        float sin = Mathf.Sin(Tilt * Mathf.Deg2Rad), cos = Mathf.Cos(Tilt * Mathf.Deg2Rad);
        float pageLeft = (W - PageW) / 2f;

        for (int c = 0; c < info.characterCount; c++)
        {
            TMP_CharacterInfo ch = info.characterInfo[c];
            if (!ch.isVisible) continue;
            int m = ch.materialReferenceIndex, v = ch.vertexIndex;
            Vector3[] src = b.original[m], dst = info.meshInfo[m].vertices;
            if (src == null || v + 3 >= src.Length) continue;

            // Letras que todavía no entraron (por debajo de la pantalla) no se dibujan: la perspectiva las agrandaría.
            float charTop = b.top - src[v + 1].y - scroll;
            if (charTop > 120f || charTop < -9000f)
            {
                for (int k = 0; k < 4; k++) dst[v + k] = Vector3.zero;
                continue;
            }
            for (int k = 0; k < 4; k++)
            {
                Vector3 p = src[v + k];
                float u = pageLeft + p.x;
                float depth = b.top - p.y - scroll; // distancia sobre el plano desde el borde de abajo (negativo = más lejos)
                float y = H + depth * cos, z = depth * sin;
                float scale = Perspective / (Perspective - z);
                float sx = W / 2f + (u - W / 2f) * scale;
                float sy = OriginY + (y - OriginY) * scale;
                dst[v + k] = new Vector3(sx, -sy, 0f);
            }
        }

        // Se reescribe siempre: TextMeshPro regenera la malla original al activarse o al cambiar de panel.
        b.text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }

    private void Back()
    {
        if (leaving) return;
        leaving = true;
        if (menu != null) menu.MostrarMenuPrincipal();
        else gameObject.SetActive(false);
    }
}

// Cielo de estrellas que titilan, dibujado en una sola malla.
public class StarField : UnityEngine.UI.MaskableGraphic
{
    public float time;
    private struct Star { public Vector2 pos; public float radius, phase, speed; }
    private Star[] stars;

    protected override void Awake()
    {
        base.Awake();
        var random = new System.Random(7);
        stars = new Star[520];
        for (int i = 0; i < stars.Length; i++)
        {
            bool big = random.NextDouble() < 0.06;
            stars[i] = new Star
            {
                pos = new Vector2((float)random.NextDouble() * 1920f, (float)random.NextDouble() * 1080f),
                radius = big ? 1.8f + (float)random.NextDouble() : 0.5f + (float)random.NextDouble() * 0.9f,
                phase = (float)random.NextDouble() * 6.28f,
                speed = 0.6f + (float)random.NextDouble() * 1.6f
            };
        }
    }

    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
    {
        vh.Clear();
        if (stars == null) return;
        foreach (Star s in stars)
        {
            float a = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(time * s.speed + s.phase));
            Color32 c = new Color(1f, 1f, 1f, a);
            float r = s.radius;
            int i = vh.currentVertCount;
            vh.AddVert(new Vector3(s.pos.x - r, -s.pos.y - r), c, Vector2.zero);
            vh.AddVert(new Vector3(s.pos.x - r, -s.pos.y + r), c, Vector2.zero);
            vh.AddVert(new Vector3(s.pos.x + r, -s.pos.y + r), c, Vector2.zero);
            vh.AddVert(new Vector3(s.pos.x + r, -s.pos.y - r), c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
