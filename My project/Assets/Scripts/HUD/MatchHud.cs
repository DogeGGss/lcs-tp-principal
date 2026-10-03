using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using static ShopUIKit;
using Img = UnityEngine.UI.Image;

// Información de la partida (US 057): la base común de los marcadores de los tres modos.
// - CA1 y CA2: arriba al centro, un reloj o contador en el medio y un lado a cada costado. Cada lado tiene un
//   título arriba, un número (con rayitas debajo si el modo las pide) y, más afuera, una fila de jugadores con
//   su retrato y una cruz cuando mueren. Lo completa cada modo con SetCenter, SetSide y SetRoster:
//   el Táctico (US 134), el Deathmatch (US 141) y el Zombie (US 150). Sin modo, muestra la fase de compra.
// - CA3: el reloj va en minutos y segundos y se pone rojo cuando el modo lo pide; en "alerta" (por ejemplo, el
//   dispositivo plantado) todo el centro se pone rojo y aparece un ícono al lado del reloj.
// - CA4: avisos de bajas arriba a la derecha: asesino, ícono del arma (o su nombre), marca de tiro a la cabeza y
//   víctima. Duran 4 s y se ven como máximo 5.
// - CA5: con Tab, una tabla sobre el juego. Cada modo arma la suya con TableProvider (secciones, columnas,
//   fila propia resaltada, jugadores muertos apagados); si no, se ven jugadores, bajas y muertes.
// - CA6: vive dentro del lienzo del HUD de combate, con sus fuentes y colores, y se oculta con la tienda.
// - CA7: los avisos y la tabla usan el nombre de cada jugador (US 164).
// La crea el HUD de combate; se arma por código con las medidas de una pantalla de 1920 x 1080.
public class MatchHud : MonoBehaviour
{
    public static MatchHud Instance { get; private set; }

    // Colores de equipo de la guía de diseño: el propio en azul y el rival en rojo.
    public static readonly Color TeamColor = new Color(0.345f, 0.651f, 1f);   // #58A6FF
    public static readonly Color RivalColor = new Color(1f, 0.361f, 0.361f);  // #FF5C5C

    /// <summary>Un jugador en la fila de un lado del marcador. Muerto se oscurece con una cruz.</summary>
    public struct RosterEntry
    {
        public string name;
        public string initial;
        public Sprite portrait;
        public Color color;    // color de su personaje (fondo del cuadrado)
        public Color team;     // rayita de abajo
        public bool alive;
        public bool marked;    // marca naranja arriba (por ejemplo, el portador del dispositivo, US 130)
    }

    /// <summary>Tabla con Tab que arma cada modo.</summary>
    public class Table
    {
        public string corner = "";                 // arriba a la derecha (por ejemplo "Ronda 7 · 3 a 3")
        public string[] columns;                   // la primera es la del nombre
        public float[] widths;                     // anchos en píxeles; la suma no debería pasar 800
        public List<Section> sections = new List<Section>();
    }

    public class Section
    {
        public string title = "";                  // vacío: sin título
        public Color color = Color.white;
        public List<Row> rows = new List<Row>();
    }

    public class Row
    {
        public string[] cells;
        public bool highlight;                     // la fila del jugador (resaltada en naranja)
        public bool dim;                           // apagada (por ejemplo, jugador muerto)
    }

    /// <summary>Si un modo lo asigna, la tabla con Tab muestra lo que devuelve.</summary>
    public static System.Func<Table> TableProvider;

    // ---------- Medidas (px en 1920 x 1080) ----------
    private const float Top = 20f, CenterW = 200f, CenterH = 76f, ScoreW = 56f, Gap = 10f;
    private const float ChipS = 44f, ChipGap = 5f, RosterW = 4f * ChipS + 3f * ChipGap;
    private const float MarkerW = 2f * (RosterW + Gap + ScoreW + Gap) + CenterW;
    private const float FeedRowH = 32f, FeedGap = 6f, FeedTime = 4f, FeedFade = 0.5f;
    private const int FeedMax = 5, MaxTicks = 12;
    private const float TableW = 860f, TableRowH = 36f;

    private TMP_FontAsset displayFont, labelFont;
    private Sprite rounded;
    private CombatHud combat;

    // Centro
    private RectTransform marker;
    private Img centerBg, centerEdge, alertIcon, deviceLight;
    private GameObject deviceIcon; // US 134, CA3: ícono del dispositivo plantado
    private TextMeshProUGUI centerLabel, clock, centerSub;
    private bool modeSetCenter;

    // Lados: 0 izquierda, 1 derecha
    private readonly TextMeshProUGUI[] sideTitle = new TextMeshProUGUI[2], sideValue = new TextMeshProUGUI[2];
    private readonly RectTransform[] ticks = new RectTransform[2], rosters = new RectTransform[2];
    private readonly string[] ticksState = new string[2], rosterState = new string[2];

    // Avisos de bajas
    private RectTransform feedRoot;
    private class FeedRow { public RectTransform rect; public CanvasGroup group; public float at; }
    private readonly List<FeedRow> feed = new List<FeedRow>();

    // Tabla
    private RectTransform table;

    // Carteles (US 032): el grande del medio, la ayuda de arriba y el aviso rojo de abajo.
    private RectTransform hudRoot, banner, hint, warning, prompt, action;
    private CanvasGroup bannerGroup;
    private Img bannerFill, actionFill;
    private TextMeshProUGUI bannerFooter, hintText, warningText, promptText, actionText;
    private string bannerFooterFormat;
    private float bannerStart, bannerEnd, warningEnd;
    private float nextTableRefresh;

    // Bajas y muertes que pasan por los avisos, por nombre.
    private readonly Dictionary<string, int> kills = new Dictionary<string, int>();
    private readonly Dictionary<string, int> deaths = new Dictionary<string, int>();

    public int KillsOf(string name) => Count(kills, name);

    // US 034: para que otras pantallas usen las mismas tipografías.
    public TMP_FontAsset DisplayFont => displayFont;
    public TMP_FontAsset LabelFont => labelFont;
    public Sprite Rounded => rounded;
    public int DeathsOf(string name) => Count(deaths, name);

    /// <summary>El lienzo del HUD (1920 x 1080 de referencia), para dibujar marcas sobre el juego (US 130).</summary>
    public RectTransform Root => hudRoot;

    // =====================================================================
    // Armado (lo llama CombatHud)
    // =====================================================================

    public void Setup(RectTransform root, TMP_FontAsset display, TMP_FontAsset label, Sprite roundedSprite, CombatHud hud)
    {
        Instance = this;
        displayFont = display;
        labelFont = label;
        rounded = roundedSprite;
        combat = hud;
        BuildMarker(root);
        BuildFeed(root);
        BuildTable(root);
        hudRoot = root;
    }

    private void OnEnable()
    {
        WeaponFire.Killed += OnLocalKill;
        Grenade1.Killed += OnGrenadeKill;
    }

    private void OnDisable()
    {
        WeaponFire.Killed -= OnLocalKill;
        Grenade1.Killed -= OnGrenadeKill;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        TableProvider = null;
    }

    private void BuildMarker(RectTransform root)
    {
        marker = Node("Marcador", root);
        marker.anchorMin = marker.anchorMax = marker.pivot = new Vector2(0.5f, 1f);
        marker.anchoredPosition = new Vector2(0f, -Top);
        marker.sizeDelta = new Vector2(MarkerW, CenterH);

        float cx = RosterW + Gap + ScoreW + Gap;
        RectTransform center = Place(Node("Centro", marker), cx, 0f, CenterW, CenterH);
        centerEdge = Image(center, rounded, RivalColor, 7f);
        centerBg = Image(Place(Node("Fondo", center), 2f, 2f, CenterW - 4f, CenterH - 4f), rounded, Rgb(10, 12, 17, DarkAlpha(0.85f)), 6f);
        centerLabel = Text(Place(Node("Etiqueta", center), 0f, 8f, CenterW, 16f), labelFont, 14f, Accent, TextAlignmentOptions.Center, 14f, true);
        clock = Text(Place(Node("Reloj", center), 0f, 22f, CenterW, 38f), displayFont, 40f, Ink, TextAlignmentOptions.Center, 2f);
        alertIcon = Image(Place(Node("Alerta", center), CenterW / 2f - 64f, 31f, 20f, 20f), rounded, RivalColor, 10f);
        Image(Place(Node("Centro", alertIcon.rectTransform), 6f, 6f, 8f, 8f), rounded, Rgb(10, 12, 17), 4f);

        RectTransform device = Place(DeviceGlyph(center, RivalColor, rounded, out deviceLight), CenterW / 2f - 72f, 27f, 28f, 28f);
        deviceIcon = device.gameObject;
        deviceIcon.SetActive(false);
        centerSub = Text(Place(Node("Detalle", center), 0f, 58f, CenterW, 14f), labelFont, 13f, Mute, TextAlignmentOptions.Center, 14f, true);

        for (int i = 0; i < 2; i++)
        {
            bool left = i == 0;
            float scoreX = left ? RosterW + Gap : cx + CenterW + Gap;
            float rosterX = left ? 0f : scoreX + ScoreW + Gap;

            // Título arriba del lado: a la izquierda alineado a la izquierda del grupo; a la derecha, desde el número.
            sideTitle[i] = Text(Place(Node("Titulo", marker), left ? 0f : scoreX, 0f, RosterW + Gap + ScoreW, 14f),
                labelFont, 12f, Mute, left ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineLeft, 12f, true);
            sideValue[i] = Text(Place(Node("Valor", marker), scoreX, 16f, ScoreW, 44f), displayFont, 42f, Ink, TextAlignmentOptions.Center);
            AddShadowTo(sideValue[i]);
            ticks[i] = Place(Node("Rayitas", marker), scoreX, 62f, ScoreW, 8f);
            rosters[i] = Place(Node("Jugadores", marker), rosterX, 18f, RosterW, ChipS);
        }
        HideSides();
        marker.gameObject.SetActive(false);
    }

    private void BuildFeed(RectTransform root)
    {
        feedRoot = Node("AvisosDeBajas", root);
        feedRoot.anchorMin = feedRoot.anchorMax = feedRoot.pivot = new Vector2(1f, 1f);
        feedRoot.anchoredPosition = new Vector2(-24f, -Top);
        feedRoot.sizeDelta = new Vector2(560f, FeedMax * (FeedRowH + FeedGap));
    }

    private void BuildTable(RectTransform root)
    {
        table = Node("TablaDePartida", root);
        table.anchorMin = table.anchorMax = table.pivot = new Vector2(0.5f, 0.5f);
        table.anchoredPosition = new Vector2(0f, 30f);
        table.sizeDelta = new Vector2(TableW, 200f);
        table.gameObject.SetActive(false);
    }

    /// <summary>
    /// El ícono del dispositivo (28 x 28): una cajita con antena y una luz que titila. Lo usan el reloj del centro,
    /// el espacio 5 del inventario y las marcas en pantalla (US 130). Va en (0, 0); se mueve con Place.
    /// </summary>
    public static RectTransform DeviceGlyph(RectTransform parent, Color color, Sprite sprite, out Img light)
    {
        RectTransform device = Place(Node("Dispositivo", parent), 0f, 0f, 28f, 28f);
        Image(Place(Node("Antena", device), 6f, 0f, 3f, 8f), null, color);
        Image(Place(Node("Caja", device), 0f, 7f, 28f, 20f), sprite, color, 4f);
        Image(Place(Node("Frente", device), 2f, 9f, 24f, 16f), sprite, Over(WithAlpha(color, 0.12f), Rgb(14, 10, 12)), 3f);
        Image(Place(Node("Cable", device), 5f, 15f, 10f, 3f), null, WithAlpha(color, 0.6f));
        light = Image(Place(Node("Luz", device), 17f, 13f, 7f, 7f), sprite, color, 3.5f);
        return device;
    }

    private static void AddShadowTo(TextMeshProUGUI text)
    {
        UnityEngine.UI.Shadow shadow = text.gameObject.AddComponent<UnityEngine.UI.Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(0f, -2f);
    }

    // =====================================================================
    // Para los modos
    // =====================================================================

    /// <summary>
    /// CA2 y CA3: completa el centro. label arriba, seconds el reloj (null para usar counter en su lugar), sub abajo.
    /// red pone el reloj en rojo; alert pone todo el centro en rojo con un ícono al lado del reloj.
    /// </summary>
    public static void SetCenter(string label, float? seconds, string sub = "", bool red = false, string counter = null, bool alert = false)
    {
        if (Instance == null) return;
        Instance.modeSetCenter = true;
        Instance.ShowCenter(label, seconds, sub, red, counter, alert);
    }

    /// <summary>US 134, CA3: el centro en rojo con el ícono del dispositivo y la cuenta que le queda.</summary>
    public static void SetCenterDevice(string label, float seconds, string sub = "")
    {
        if (Instance == null) return;
        SetCenter(label, seconds, sub, true, null, true);
        Instance.alertIcon.gameObject.SetActive(false);
        Instance.deviceIcon.SetActive(true);
        // La luz titila más rápido en los últimos 10 segundos.
        float speed = seconds <= 10f ? 14f : 6f;
        Instance.deviceLight.color = WithAlpha(RivalColor, 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * speed)));
    }

    /// <summary>Vuelve al comportamiento por defecto (fase de compra, si la hay).</summary>
    public static void ClearCenter()
    {
        if (Instance == null) return;
        Instance.modeSetCenter = false;
        Instance.HideSides();
    }

    /// <summary>
    /// CA2: completa un lado. title va arriba (admite colores con &lt;color&gt;), value es el número grande y
    /// ticks/filled dibujan rayitas debajo (por ejemplo, 7 rondas y 3 ganadas). value null oculta el lado.
    /// </summary>
    public static void SetSide(bool left, string value, string title, Color color, int ticks = 0, int filled = 0)
    {
        if (Instance == null) return;
        Instance.ShowSide(left ? 0 : 1, value, title, color, ticks, filled);
    }

    /// <summary>CA2: fila de hasta 4 jugadores al costado de un lado. null o vacío la saca.</summary>
    public static void SetRoster(bool left, IList<RosterEntry> entries)
    {
        if (Instance == null) return;
        Instance.DrawRoster(left ? 0 : 1, entries);
    }

    /// <summary>CA4 y CA7: agrega un aviso de baja y lo cuenta para la tabla.</summary>
    public static void ReportKill(string killer, string weapon, string victim, Color killerColor, Color victimColor,
        bool headshot = false, Sprite weaponIcon = null)
    {
        if (Instance == null) return;
        Instance.AddFeed(killer, weapon, victim, killerColor, victimColor, headshot, weaponIcon);
        if (!string.IsNullOrEmpty(killer)) Instance.kills[killer] = Instance.Count(Instance.kills, killer) + 1;
        if (!string.IsNullOrEmpty(victim)) Instance.deaths[victim] = Instance.Count(Instance.deaths, victim) + 1;
    }

    // =====================================================================
    // Cuadro a cuadro
    // =====================================================================

    private void Update()
    {
        if (!modeSetCenter) DefaultCenter();
        UpdateFeed();
        UpdateTable();
        UpdateNotices();
    }

    // Sin modo: la fase de compra (US 076), si la hay.
    private void DefaultCenter()
    {
        BuyPhase phase = BuyPhase.Current;
        if (phase == null || !phase.IsActive) { marker.gameObject.SetActive(false); return; }
        ShowCenter("Fase de compra", phase.TimeLeft, phase.Round > 0 ? $"Ronda {phase.Round}" : "", phase.TimeLeft <= 5f, null, false);
    }

    private void ShowCenter(string label, float? seconds, string sub, bool red, string counter, bool alert)
    {
        marker.gameObject.SetActive(true);
        centerLabel.text = label;
        centerLabel.color = alert ? RivalColor : Accent;
        centerSub.text = sub;
        if (seconds.HasValue)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds.Value));
            clock.text = $"{s / 60}:{s % 60:00}";
        }
        else clock.text = counter ?? "";
        clock.color = red || alert ? RivalColor : Ink;
        centerEdge.enabled = alert;
        centerBg.color = alert ? Over(WithAlpha(RivalColor, 0.18f), Rgb(10, 12, 17)) : Rgb(10, 12, 17, DarkAlpha(0.85f));
        alertIcon.gameObject.SetActive(alert);
        deviceIcon.SetActive(false);
    }

    private void ShowSide(int i, string value, string title, Color color, int count, int filled)
    {
        bool show = value != null;
        sideValue[i].gameObject.SetActive(show);
        sideTitle[i].gameObject.SetActive(show);
        ticks[i].gameObject.SetActive(show && count > 0);
        if (!show) return;
        sideValue[i].text = value;
        sideValue[i].color = color;
        sideTitle[i].text = title;

        string state = $"{count}|{filled}|{ColorUtility.ToHtmlStringRGB(color)}";
        if (ticksState[i] == state) return;
        ticksState[i] = state;
        RectTransform row = ticks[i];
        for (int k = row.childCount - 1; k >= 0; k--) Destroy(row.GetChild(k).gameObject);
        count = Mathf.Min(count, MaxTicks);
        const float w = 4f, g = 2f;
        float x0 = (ScoreW - (count * w + (count - 1) * g)) / 2f;
        for (int k = 0; k < count; k++)
        {
            // Las ganadas se llenan desde el lado del centro.
            int pos = i == 0 ? count - 1 - k : k;
            Image(Place(Node("Rayita", row), x0 + pos * (w + g), 0f, w, 8f), null, k < filled ? color : White(FadeAlpha(0.2f)));
        }
    }

    private void HideSides()
    {
        for (int i = 0; i < 2; i++)
        {
            sideValue[i].gameObject.SetActive(false);
            sideTitle[i].gameObject.SetActive(false);
            ticks[i].gameObject.SetActive(false);
            DrawRoster(i, null);
        }
    }

    // ---------- Filas de jugadores ----------

    private void DrawRoster(int side, IList<RosterEntry> entries)
    {
        var sb = new System.Text.StringBuilder();
        if (entries != null)
            foreach (RosterEntry e in entries)
                sb.Append(e.name).Append(e.alive).Append(e.marked).Append(e.initial).Append(e.portrait != null ? e.portrait.name : "")
                  .Append(ColorUtility.ToHtmlStringRGB(e.color)).Append(ColorUtility.ToHtmlStringRGB(e.team)).Append('|');
        string state = sb.ToString();
        if (rosterState[side] == state) return;
        rosterState[side] = state;

        RectTransform roster = rosters[side];
        for (int i = roster.childCount - 1; i >= 0; i--) Destroy(roster.GetChild(i).gameObject);
        if (entries == null) return;

        int n = Mathf.Min(entries.Count, 4);
        for (int k = 0; k < n; k++)
        {
            RosterEntry e = entries[k];
            // El primero queda pegado al número, de los dos lados.
            float x = side == 0 ? RosterW - (k + 1) * ChipS - k * ChipGap : k * (ChipS + ChipGap);
            RectTransform chip = Place(Node(e.name, roster), x, 0f, ChipS, ChipS);
            Image(chip, rounded, Over(WithAlpha(e.color, 0.3f), Rgb(14, 16, 20)), 5f);
            if (e.portrait != null)
            {
                Img pic = Image(Place(Node("Retrato", chip), 2f, 2f, ChipS - 4f, ChipS - 6f), e.portrait, Color.white);
                pic.preserveAspect = true;
            }
            else
                Text(Stretch(Node("Inicial", chip)), displayFont, 26f, e.color, TextAlignmentOptions.Center, 0f, true).text = e.initial;
            Image(Place(Node("Equipo", chip), 0f, ChipS - 4f, ChipS, 4f), null, e.team);

            if (!e.alive)
            {
                Image(Stretch(Node("Muerto", chip)), rounded, new Color(0f, 0f, 0f, DarkAlpha(0.6f)), 5f);
                for (int d = 0; d < 2; d++)
                {
                    RectTransform bar = Node("Cruz", chip);
                    bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0.5f, 0.5f);
                    bar.sizeDelta = new Vector2(ChipS * 0.72f, 4f);
                    bar.localRotation = Quaternion.Euler(0f, 0f, d == 0 ? 45f : -45f);
                    Image(bar, null, RivalColor);
                }
            }
            else if (e.marked)
            {
                // Rombo naranja arriba del cuadrado.
                RectTransform mark = Node("Marca", chip);
                mark.anchorMin = mark.anchorMax = mark.pivot = new Vector2(0.5f, 1f);
                mark.anchoredPosition = new Vector2(0f, 7f);
                mark.sizeDelta = new Vector2(10f, 10f);
                mark.localRotation = Quaternion.Euler(0f, 0f, 45f);
                Image(mark, null, Accent);
            }
        }
    }

    // ---------- Avisos de bajas (CA4) ----------

    private void AddFeed(string killer, string weapon, string victim, Color killerColor, Color victimColor, bool headshot, Sprite weaponIcon)
    {
        if (feed.Count >= FeedMax) RemoveFeed(0);

        RectTransform row = Node("Aviso", feedRoot);
        row.anchorMin = row.anchorMax = row.pivot = new Vector2(1f, 1f);
        Image(row, rounded, Rgb(10, 12, 17, DarkAlpha(0.8f)), 5f);

        float x = 12f;
        if (!string.IsNullOrEmpty(killer)) x = FeedText(row, killer, killerColor, x) + 10f;

        if (weaponIcon != null)
        {
            Img icon = Image(Place(Node("Arma", row), x, 6f, 58f, FeedRowH - 12f), weaponIcon, Soft);
            icon.preserveAspect = true;
            x += 58f + 8f;
        }
        else x = FeedText(row, string.IsNullOrEmpty(weapon) ? "—" : weapon, Mute, x) + 8f;

        if (headshot)
        {
            // Mira naranja: aro con un punto oscuro en el centro.
            Img ring = Image(Place(Node("Cabeza", row), x, 9f, 14f, 14f), rounded, Accent, 7f);
            Image(Place(Node("Centro", ring.rectTransform), 3f, 3f, 8f, 8f), rounded, Rgb(10, 12, 17), 4f);
            x += 14f + 8f;
        }

        x = FeedText(row, victim, victimColor, x) + 12f;
        row.sizeDelta = new Vector2(x, FeedRowH);

        feed.Add(new FeedRow { rect = row, group = row.gameObject.AddComponent<CanvasGroup>(), at = Time.unscaledTime });
        LayoutFeed();
    }

    private float FeedText(RectTransform row, string value, Color color, float x)
    {
        TextMeshProUGUI t = Text(Place(Node("Texto", row), x, 0f, 300f, FeedRowH), labelFont, 17f, color, TextAlignmentOptions.MidlineLeft, 4f, false);
        t.text = value;
        float w = Width(t, value);
        t.rectTransform.sizeDelta = new Vector2(w + 2f, FeedRowH);
        return x + w;
    }

    private void UpdateFeed()
    {
        for (int i = feed.Count - 1; i >= 0; i--)
        {
            float age = Time.unscaledTime - feed[i].at;
            if (age >= FeedTime) { RemoveFeed(i); continue; }
            feed[i].group.alpha = Mathf.Clamp01((FeedTime - age) / FeedFade);
        }
    }

    private void RemoveFeed(int i)
    {
        if (feed[i].rect != null) Destroy(feed[i].rect.gameObject);
        feed.RemoveAt(i);
        LayoutFeed();
    }

    private void LayoutFeed()
    {
        for (int i = 0; i < feed.Count; i++)
            feed[i].rect.anchoredPosition = new Vector2(0f, -i * (FeedRowH + FeedGap));
    }

    // Sin conexión, las bajas del jugador salen de sus disparos (zombis y enemigos de prueba).
    // En el multijugador las avisa JugadorEnRed, para que todos vean las mismas.
    private void OnLocalKill(bool headshot)
    {
        if (PhotonNetwork.InRoom) return;
        ReportKill(LocalName(), combat != null ? combat.WeaponName : "", "Enemigo", TeamColor, RivalColor, headshot,
            combat != null ? combat.WeaponIcon : null);
    }

    // Sin conexión, una granada que eliminó a un enemigo (US 073): el aviso lleva la granada, no el arma en la mano.
    private void OnGrenadeKill(Grenade1 grenade, HealthSystem victim)
    {
        if (PhotonNetwork.InRoom || victim == null || victim.GetComponent<PlayerMovement>() != null) return;
        ReportKill(LocalName(), grenade.WeaponName, "Enemigo", TeamColor, RivalColor, false,
            grenade.Item != null ? grenade.Item.icon : null);
    }

    // ---------- Carteles (US 032) ----------

    /// <summary>
    /// Cartel grande en el medio de la pantalla (por ejemplo "Ronda ganada"). seconds &lt;= 0 lo deja hasta que se
    /// llame HideBanner. footer admite {0} para los segundos que faltan (por ejemplo "Siguiente ronda en {0}");
    /// con seconds &gt; 0 también muestra una barrita que se vacía.
    /// </summary>
    public static void ShowBanner(string eyebrow, string title, Color color, string sub, IList<string> chips = null,
        float seconds = 5f, string footer = null)
    {
        if (Instance == null) return;
        Instance.BuildBanner(eyebrow, title, color, sub, chips, seconds, footer);
    }

    public static void HideBanner()
    {
        if (Instance != null && Instance.banner != null) Instance.banner.gameObject.SetActive(false);
    }

    /// <summary>Ayuda chica arriba, debajo del marcador (admite &lt;color&gt;). null o vacío la saca.</summary>
    public static void SetHint(string text)
    {
        if (Instance == null) return;
        Instance.ShowHint(text);
    }

    /// <summary>
    /// US 184, CA6: aviso debajo de la mira mientras se mira algo que se puede agarrar (admite &lt;color&gt;),
    /// por ejemplo "Soltá tu Mitre con G para levantarla". null o vacío lo saca.
    /// </summary>
    public static void SetPrompt(string text)
    {
        if (Instance == null) return;
        Instance.ShowPrompt(text);
    }

    /// <summary>
    /// US 131 y US 132: una acción que se hace manteniendo una tecla, debajo de la mira (admite &lt;color&gt;).
    /// Con progress (0 a 1) muestra la barra que se llena, por ejemplo "Plantando el dispositivo"; sin progress, solo
    /// el texto ("Mantené E para plantar"). null o vacío la saca.
    /// </summary>
    public static void SetAction(string text, float? progress = null)
    {
        if (Instance == null) return;
        Instance.ShowAction(text, progress);
    }

    /// <summary>Aviso rojo abajo del centro durante unos segundos (por ejemplo "No podés salir de la base").</summary>
    public static void Warn(string text, float seconds = 1.5f)
    {
        if (Instance == null) return;
        Instance.ShowWarning(text, seconds);
    }

    private void BuildBanner(string eyebrow, string title, Color color, string sub, IList<string> chips, float seconds, string footer)
    {
        if (banner != null) Destroy(banner.gameObject);
        banner = Node("Cartel", hudRoot);
        banner.anchorMin = banner.anchorMax = banner.pivot = new Vector2(0.5f, 1f);
        banner.anchoredPosition = new Vector2(0f, -220f);
        const float W = 1300f;
        bool hasChips = chips != null && chips.Count > 0;
        bool timed = seconds > 0f;
        float h = 40f + 26f + 112f + 18f + 36f + (hasChips ? 52f : 0f) + (footer != null ? 30f : 0f) + (timed ? 18f : 0f) + 26f;
        banner.sizeDelta = new Vector2(W, h);
        bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
        bannerGroup.blocksRaycasts = false;

        // Franja oscura con una línea de color arriba y abajo.
        Image(Stretch(Node("Franja", banner)), null, Rgb(8, 10, 14, DarkAlpha(0.82f)));
        Image(Place(Node("LineaArriba", banner), W * 0.2f, 0f, W * 0.6f, 2f), null, WithAlpha(color, 0.9f));
        Image(Place(Node("LineaAbajo", banner), W * 0.2f, h - 2f, W * 0.6f, 2f), null, WithAlpha(color, 0.9f));

        float y = 40f;
        Text(Place(Node("Antetitulo", banner), 0f, y, W, 24f), labelFont, 22f, Mute, TextAlignmentOptions.Center, 24f, true).text = eyebrow ?? "";
        y += 26f;
        TextMeshProUGUI t = Text(Place(Node("Titulo", banner), 0f, y, W, 112f), displayFont, 110f, color, TextAlignmentOptions.Center, 4f, true);
        t.text = title;
        AddShadowTo(t);
        y += 112f;
        Image(Place(Node("Linea", banner), W / 2f - 70f, y + 4f, 140f, 4f), null, color);
        y += 18f;
        Text(Place(Node("Detalle", banner), 0f, y, W, 34f), labelFont, 28f, Ink, TextAlignmentOptions.Center).text = sub ?? "";
        y += 36f;

        if (hasChips)
        {
            var widths = new float[chips.Count];
            float total = 0f;
            var texts = new TextMeshProUGUI[chips.Count];
            for (int i = 0; i < chips.Count; i++)
            {
                texts[i] = Text(Node("Dato", banner), labelFont, 20f, Ink, TextAlignmentOptions.Center, 8f, true);
                texts[i].text = chips[i];
                widths[i] = Width(texts[i], chips[i].ToUpperInvariant()) + 36f;
                total += widths[i] + (i > 0 ? 12f : 0f);
            }
            float x = (W - total) / 2f;
            for (int i = 0; i < chips.Count; i++)
            {
                RectTransform chip = Place(Node("Chip", banner), x, y + 12f, widths[i], 36f);
                Image(chip, rounded, White(0.08f), 4f);
                texts[i].rectTransform.SetParent(chip, false);
                Stretch(texts[i].rectTransform);
                x += widths[i] + 12f;
            }
            y += 52f;
        }

        bannerFooter = null;
        bannerFooterFormat = footer;
        if (footer != null)
        {
            bannerFooter = Text(Place(Node("Pie", banner), 0f, y + 6f, W, 24f), labelFont, 20f, Mute, TextAlignmentOptions.Center, 12f, true);
            y += 30f;
        }

        bannerFill = null;
        if (timed)
        {
            Image(Place(Node("Barra", banner), W / 2f - 180f, y + 10f, 360f, 4f), null, White(0.15f));
            bannerFill = Image(Place(Node("Relleno", banner), W / 2f - 180f, y + 10f, 360f, 4f), null, color);
        }

        bannerStart = Time.unscaledTime;
        bannerEnd = timed ? Time.unscaledTime + seconds : float.MaxValue;
        UpdateBanner();
    }

    private void ShowHint(string text)
    {
        if (string.IsNullOrEmpty(text)) { if (hint != null) hint.gameObject.SetActive(false); return; }
        if (hint == null)
        {
            hint = Node("Ayuda", hudRoot);
            hint.anchorMin = hint.anchorMax = hint.pivot = new Vector2(0.5f, 1f);
            // Debajo del aviso "B Tienda" de la tienda (que ocupa de 150 a 198), así no se superponen.
            hint.anchoredPosition = new Vector2(0f, -212f);
            Image(Stretch(Node("Fondo", hint)), rounded, Rgb(10, 12, 17, DarkAlpha(0.85f)), 6f);
            hintText = Text(Stretch(Node("Texto", hint)), labelFont, 22f, Ink, TextAlignmentOptions.Center);
        }
        hint.gameObject.SetActive(true);
        if (hintText.text == text) return;
        hintText.text = text;
        hint.sizeDelta = new Vector2(hintText.GetPreferredValues(text).x + 44f, 44f);
    }

    // Como la ayuda de arriba, pero debajo de la mira.
    private void ShowPrompt(string text)
    {
        if (string.IsNullOrEmpty(text)) { if (prompt != null) prompt.gameObject.SetActive(false); return; }
        if (prompt == null)
        {
            prompt = Node("AvisoMira", hudRoot);
            prompt.anchorMin = prompt.anchorMax = prompt.pivot = new Vector2(0.5f, 0.5f);
            prompt.anchoredPosition = new Vector2(0f, -110f);
            Image(Stretch(Node("Fondo", prompt)), rounded, Rgb(10, 12, 17, DarkAlpha(0.85f)), 6f);
            promptText = Text(Stretch(Node("Texto", prompt)), labelFont, 22f, Ink, TextAlignmentOptions.Center);
        }
        prompt.gameObject.SetActive(true);
        if (promptText.text == text) return;
        promptText.text = text;
        prompt.sizeDelta = new Vector2(promptText.GetPreferredValues(text).x + 44f, 44f);
    }

    // Más abajo que el aviso de la mira: un texto y, si hay progreso, una barra naranja que se llena.
    private void ShowAction(string text, float? progress)
    {
        if (string.IsNullOrEmpty(text)) { if (action != null) action.gameObject.SetActive(false); return; }
        const float BarW = 300f;
        if (action == null)
        {
            action = Node("Accion", hudRoot);
            action.anchorMin = action.anchorMax = action.pivot = new Vector2(0.5f, 0.5f);
            action.anchoredPosition = new Vector2(0f, -190f);
            Image(Stretch(Node("Fondo", action)), rounded, Rgb(10, 12, 17, DarkAlpha(0.85f)), 6f);
            actionText = Text(Place(Node("Texto", action), 0f, 0f, 10f, 44f), labelFont, 22f, Ink, TextAlignmentOptions.Center);
            RectTransform track = Node("Barra", action);
            track.anchorMin = track.anchorMax = track.pivot = new Vector2(0.5f, 0f);
            track.anchoredPosition = new Vector2(0f, 12f);
            track.sizeDelta = new Vector2(BarW, 6f);
            Image(track, rounded, White(FadeAlpha(0.16f)), 3f);
            actionFill = Image(Place(Node("Relleno", track), 0f, 0f, 0f, 6f), rounded, Accent, 3f);
        }
        action.gameObject.SetActive(true);
        bool bar = progress.HasValue;
        actionFill.transform.parent.gameObject.SetActive(bar);
        if (bar) actionFill.rectTransform.sizeDelta = new Vector2(BarW * Mathf.Clamp01(progress.Value), 6f);
        float h = bar ? 70f : 44f;
        if (actionText.text != text) actionText.text = text;
        float w = Mathf.Max(bar ? BarW + 44f : 0f, actionText.GetPreferredValues(text).x + 44f);
        action.sizeDelta = new Vector2(w, h);
        actionText.rectTransform.sizeDelta = new Vector2(w, 44f);
    }

    private void ShowWarning(string text, float seconds)
    {
        if (warning == null)
        {
            warning = Node("AvisoRojo", hudRoot);
            warning.anchorMin = warning.anchorMax = warning.pivot = new Vector2(0.5f, 0.5f);
            warning.anchoredPosition = new Vector2(0f, -120f);
            Image(Stretch(Node("Borde", warning)), rounded, RivalColor, 6f);
            Image(Place(Node("Fondo", warning), 2f, 2f, 10f, 10f), rounded, Over(WithAlpha(RivalColor, 0.18f), Rgb(10, 12, 17)), 5f);
            warningText = Text(Stretch(Node("Texto", warning)), labelFont, 24f, Ink, TextAlignmentOptions.Center, 10f, true);
        }
        warning.gameObject.SetActive(true);
        warningText.text = text;
        float w = Width(warningText, text.ToUpperInvariant()) + 56f;
        warning.sizeDelta = new Vector2(w, 50f);
        ((RectTransform)warning.GetChild(1)).sizeDelta = new Vector2(w - 4f, 46f);
        warningEnd = Time.unscaledTime + seconds;
    }

    private void UpdateNotices()
    {
        UpdateBanner();
        if (warning != null && warning.gameObject.activeSelf && Time.unscaledTime >= warningEnd) warning.gameObject.SetActive(false);
    }

    private void UpdateBanner()
    {
        if (banner == null || !banner.gameObject.activeSelf) return;
        float now = Time.unscaledTime;
        if (now >= bannerEnd) { banner.gameObject.SetActive(false); return; }

        // Aparece y se va con un fundido corto.
        float fadeIn = Mathf.Clamp01((now - bannerStart) / 0.2f);
        float fadeOut = bannerEnd == float.MaxValue ? 1f : Mathf.Clamp01((bannerEnd - now) / 0.3f);
        bannerGroup.alpha = Mathf.Min(fadeIn, fadeOut);

        if (bannerEnd == float.MaxValue) return;
        float left = bannerEnd - now;
        if (bannerFooter != null) bannerFooter.text = string.Format(bannerFooterFormat, Mathf.CeilToInt(left));
        if (bannerFill != null)
            bannerFill.rectTransform.sizeDelta = new Vector2(360f * Mathf.Clamp01(left / (bannerEnd - bannerStart)), 4f);
    }

    // ---------- Tabla con Tab (CA5) ----------

    private void UpdateTable()
    {
        bool held = Input.GetKey(KeyCode.Tab) && !PauseMenu.IsPaused;
        if (table.gameObject.activeSelf != held) { table.gameObject.SetActive(held); nextTableRefresh = 0f; }
        if (!held || Time.unscaledTime < nextTableRefresh) return;
        nextTableRefresh = Time.unscaledTime + 0.25f;
        DrawTable(TableProvider != null ? TableProvider() : DefaultTable());
    }

    private void DrawTable(Table data)
    {
        for (int i = table.childCount - 1; i >= 0; i--) Destroy(table.GetChild(i).gameObject);
        if (data == null || data.columns == null) return;

        float h = 72f;
        foreach (Section s in data.sections) h += (string.IsNullOrEmpty(s.title) ? 0f : 26f) + TableRowH * (s.rows.Count + 1) + 10f;
        h += 10f;
        table.sizeDelta = new Vector2(TableW, h);

        Image(Stretch(Node("Fondo", table)), rounded, Rgb(10, 12, 17, DarkAlpha(0.9f)), 10f);
        Text(Place(Node("Titulo", table), 24f, 16f, 400f, 34f), displayFont, 30f, Ink, TextAlignmentOptions.MidlineLeft, 3f, true).text = "Partida";
        Image(Place(Node("Linea", table), 24f, 52f, 64f, 3f), null, Accent);
        Text(Place(Node("Esquina", table), TableW - 324f, 16f, 300f, 34f), labelFont, 14f, Mute, TextAlignmentOptions.MidlineRight, 12f, true).text = data.corner;

        float y = 72f;
        foreach (Section s in data.sections)
        {
            if (!string.IsNullOrEmpty(s.title))
            {
                Text(Place(Node("Seccion", table), 24f, y, 400f, 22f), labelFont, 14f, s.color, TextAlignmentOptions.MidlineLeft, 14f, true).text = s.title;
                y += 26f;
            }
            DrawRow(data, new Row { cells = data.columns }, y, true, 0);
            y += TableRowH;
            for (int r = 0; r < s.rows.Count; r++, y += TableRowH) DrawRow(data, s.rows[r], y, false, r);
            y += 10f;
        }
    }

    private void DrawRow(Table data, Row row, float y, bool header, int index)
    {
        if (!header)
        {
            if (row.highlight)
            {
                Image(Place(Node("Resaltada", table), 16f, y, TableW - 32f, TableRowH), rounded, Over(WithAlpha(Accent, 0.16f), Rgb(10, 12, 17)), 3f);
                Image(Place(Node("Borde", table), 16f, y, 3f, TableRowH), null, Accent);
            }
            else if (index % 2 == 0)
                Image(Place(Node("Franja", table), 16f, y, TableW - 32f, TableRowH), rounded, White(0.035f), 3f);
        }
        else Image(Place(Node("Separador", table), 24f, y + TableRowH - 1f, TableW - 48f, 1f), null, White(0.08f));

        float x = 24f;
        for (int c = 0; c < row.cells.Length && c < data.columns.Length; c++)
        {
            float w = data.widths != null && c < data.widths.Length ? data.widths[c] : (c == 0 ? 420f : 130f);
            var align = c == 0 ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;
            TextMeshProUGUI t = header
                ? Text(Place(Node("Columna", table), x, y, w, TableRowH), labelFont, 12f, Mute, align, 12f, true)
                : Text(Place(Node("Celda", table), x, y, w, TableRowH), displayFont, c == 0 ? 19f : 20f, Ink, align, c == 0 ? 2f : 0f);
            t.text = row.cells[c];
            if (row.dim) t.alpha = 0.45f;
            x += w;
        }
    }

    // Tabla por defecto: jugadores con las bajas y muertes que pasaron por los avisos.
    private Table DefaultTable()
    {
        var data = new Table { columns = new[] { "Jugador", "Bajas", "Muertes" }, widths = new[] { 552f, 130f, 130f } };
        var section = new Section();
        var players = new List<(string name, bool local)>();
        if (PhotonNetwork.InRoom)
            foreach (Player p in PhotonNetwork.PlayerList) players.Add((p.NickName, p.IsLocal));
        else players.Add((LocalName(), true));

        players.Sort((a, b) => Count(kills, b.name).CompareTo(Count(kills, a.name)));
        foreach (var (name, local) in players)
            section.rows.Add(new Row
            {
                cells = new[] { local ? $"{name} <color=#8E96A3>(vos)</color>" : name, Count(kills, name).ToString(), Count(deaths, name).ToString() },
                highlight = local
            });
        data.sections.Add(section);
        return data;
    }

    private int Count(Dictionary<string, int> dict, string key) => key != null && dict.TryGetValue(key, out int n) ? n : 0;

    public static string LocalName()
    {
        if (PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer != null) return PhotonNetwork.LocalPlayer.NickName;
        return PlayerProfile.HasName ? PlayerProfile.Name : "Vos";
    }
}
