using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using static ShopUIKit;

// Tienda del modo Táctico (US 076). Se abre con B y es un menú desplegable en tres niveles:
// categoría → elemento → ficha. La interfaz se arma por código con las medidas de la maqueta
// (pantalla de referencia de 1920 x 1080) y todo lo que muestra sale del catálogo y del
// equipamiento del jugador (PlayerLoadout), que es el que aplica las reglas de compra.
public class ShopUI : MonoBehaviour
{
    [SerializeField] private KeyCode openKey = KeyCode.B;
    [Tooltip("Lo mínimo que se cobra la próxima ronda (una derrota), para decidir si conviene ahorrar.")]
    [SerializeField] private int minimumNextRound = 1900;

    [Header("Tipografías")]
    [SerializeField] private TMP_FontAsset displayFont; // Barlow Condensed Bold
    [SerializeField] private TMP_FontAsset labelFont;   // Barlow Condensed SemiBold
    [SerializeField] private TMP_FontAsset bodyFont;    // Barlow Regular
    [SerializeField] private TMP_FontAsset monoFont;    // JetBrains Mono

    [Header("Sprites")]
    [SerializeField] private Sprite rounded;
    [Tooltip("Bordes de 1 px para los radios 4, 5, 6, 8, 10 y 24, en ese orden.")]
    [SerializeField] private Sprite[] borders = new Sprite[6];
    [SerializeField] private Sprite circle;
    [SerializeField] private Sprite ring;
    [SerializeField] private Sprite glow;
    [SerializeField] private Sprite fade;
    [SerializeField] private Sprite shieldSprite;
    [Tooltip("Símbolos de las teclas Enter (↵) y Retroceso (⌫), que no están en las tipografías.")]
    [SerializeField] private Sprite enterIcon;
    [SerializeField] private Sprite backIcon;
    [Tooltip("Escala de grises para los íconos de lo que no alcanza a pagarse.")]
    [SerializeField] private Material grayscale;

    public static bool IsOpen { get; private set; }

    private static readonly int[] BorderRadii = { 4, 5, 6, 8, 10, 24 };
    private static readonly int[] Targets = { 100, 125, 150 };
    private static readonly string[] TargetNames = { "Sin escudo", "Liviano", "Completo" };
    private static readonly BodyZone[] Zones = { BodyZone.Head, BodyZone.Body, BodyZone.Legs };
    private static readonly string[] ZoneNames = { "Cabeza", "Cuerpo", "Piernas" };

    // Medidas de la maqueta (px en 1920 x 1080).
    private const float PanelTop = 150f;
    private const float CatsX = 56f, CatsW = 400f;
    private const float ListX = 470f, ListW = 480f;
    private const float DetailX = 964f, DetailW = 560f, DetailH = 754f;
    private const float LoadoutX = 56f, LoadoutY = 938f, LoadoutW = 1808f, LoadoutH = 112f;
    private const float HeadH = 54f, CatRowH = 66f, ItemRowH = 92f, LegendH = 87f;
    private const float Pad = 22f, ContentW = 516f;
    private const float ButtonY = 668f, ButtonH = 66f;
    private const float FxScale = 492f / 500f; // los gráficos de la maqueta son SVG de 500 px de ancho

    private class Row
    {
        public RectTransform root;
        public UnityEngine.UI.Image background, selectionBar, keyChip, icon, badge;
        public TextMeshProUGUI keyText, nameText, subText, rightText, badgeText, chevron;
        public ShopPointerTarget pointer;
        public bool hovered;
    }

    private enum ButtonStyle { Normal, Cant, Owned, Locked }

    private PlayerLoadout loadout;
    private ShopCatalog catalog;
    private BuyPhase phase;
    private readonly List<Behaviour> blocked = new List<Behaviour>();
    private readonly List<ShopCategory> categories = new List<ShopCategory>();
    private readonly List<ShopItem> listItems = new List<ShopItem>();
    private readonly List<Row> categoryRows = new List<Row>();
    private readonly List<Row> itemRows = new List<Row>();

    private bool open;
    private int level;          // 0 = categorías, 1 = lista de una categoría
    private int highlight;
    private ShopCategory? category;
    private ShopItem selected;
    private int band;
    private ShopResult lastAccess = ShopResult.Ok;

    // Interfaz
    private RectTransform canvasRoot, hud, shopRoot, stage, topBar, catsPanel, listPanel, detailPanel, loadoutBar, toast;
    private UnityEngine.UI.RectMask2D catsMask, listMask, detailMask;
    private CanvasGroup shopGroup, detailGroup, toastGroup;
    private GameObject hudClock, hudHint, banner;
    private TextMeshProUGUI hudTime, hudRound, hudMoney, bannerText, toastText;
    private TextMeshProUGUI barSub, barPhase, barTime, barMoney, barNext;
    private RectTransform barProgress, catRowsRoot, legendRoot, listRowsRoot;
    private TextMeshProUGUI listTitle;

    // Ficha
    private TextMeshProUGUI detailName, detailAlias, detailMeta, detailPrice, detailRole;
    private RectTransform detailPicture, detailBody;
    private UnityEngine.UI.Image detailIcon, detailGlow;
    private UnityEngine.UI.Image buyButton;
    private TextMeshProUGUI buyLabel, buySub;

    // Equipamiento
    private RectTransform[] loadoutCells;
    private float[] loadoutWidths;

    private Coroutine toastRoutine, shakeRoutine, openRoutine, catsRoutine, listRoutine, detailRoutine;

    private void Awake()
    {
        BuildCanvas();
    }

    private void Start()
    {
        EnsureEventSystem();
        phase = BuyPhase.Current;
        if (phase != null)
        {
            phase.Started += Render;
            phase.Ended += OnPhaseEnded;
        }
        FindPlayer();
        shopRoot.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (phase != null)
        {
            phase.Started -= Render;
            phase.Ended -= OnPhaseEnded;
        }
        if (loadout != null)
        {
            loadout.Changed -= Render;
            loadout.Wallet.MoneyChanged -= OnMoneyChanged;
        }
        if (open) RestorePlayerControls();
        IsOpen = false;
    }

    private void Update()
    {
        if (loadout == null)
        {
            FindPlayer();
            if (loadout == null) return;
        }

        if (Input.GetKeyDown(openKey)) SetOpen(!open);
        else if (open) HandleKeys();
        UpdateLive();
    }

    // ---------- Jugador y controles ----------

    private void FindPlayer()
    {
        PlayerLoadout found = FindAnyObjectByType<PlayerLoadout>();
        if (found == null || found.Catalog == null) return;

        loadout = found;
        catalog = loadout.Catalog;
        loadout.Changed += Render;
        loadout.Wallet.MoneyChanged += OnMoneyChanged;
        categories.Clear();
        categories.AddRange(catalog.ActiveCategories());
        BuildCategoryRows();
        lastAccess = loadout.CheckAccess();
        Render();
    }

    private void SetOpen(bool value)
    {
        if (value == open) return;
        open = value;
        IsOpen = value;
        level = 0;
        category = null;
        selected = null;
        highlight = 0;
        listItems.Clear();
        BuildItemRows();

        if (value)
        {
            BlockPlayerControls();
            shopRoot.gameObject.SetActive(true);
            Render();
            if (openRoutine != null) StopCoroutine(openRoutine);
            openRoutine = StartCoroutine(OpenAnimation());
            Reveal(ref catsRoutine, catsMask, true, 0.16f);
        }
        else
        {
            RestorePlayerControls();
            shopRoot.gameObject.SetActive(false);
            Render();
        }
    }

    // Con la tienda abierta el mouse maneja la tienda: no se mira, no se dispara ni se cambia de arma.
    // WASD sigue funcionando (US 076, CA1).
    private void BlockPlayerControls()
    {
        blocked.Clear();
        Block(FindObjectsByType<CameraLook>());
        Block(FindObjectsByType<MeleeAttack>());
        Block(FindObjectsByType<Pistola>());
        Block(FindObjectsByType<WeaponSwitcher>());
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
        foreach (Behaviour component in blocked)
            if (component != null) component.enabled = true;
        blocked.Clear();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnPhaseEnded()
    {
        // Si la tienda está abierta cuando termina la fase, se cierra sola (US 076, CA9).
        if (open) SetOpen(false);
        Render();
    }

    private void OnMoneyChanged(int money) => Render();

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    // ---------- Teclado y acciones ----------

    private void HandleKeys()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) { SetOpen(false); return; }
        if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.LeftArrow)) { Back(); return; }

        int count = level == 0 ? categories.Count : listItems.Count;
        for (int i = 0; i < 9; i++)
        {
            if (!Input.GetKeyDown(KeyCode.Alpha1 + i) && !Input.GetKeyDown(KeyCode.Keypad1 + i)) continue;
            if (i >= count) return;
            if (level == 0) OpenCategory(categories[i]);
            else
            {
                // En la lista, el número compra directamente (US 076, CA7).
                Select(listItems[i]);
                TryBuy(listItems[i]);
            }
            return;
        }

        if (count == 0) return;
        if (Input.GetKeyDown(KeyCode.DownArrow)) MoveHighlight(1);
        else if (Input.GetKeyDown(KeyCode.UpArrow)) MoveHighlight(-1);
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (level == 0) OpenCategory(categories[highlight]);
            else if (selected != null) TryBuy(selected);
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow) && level == 0) OpenCategory(categories[highlight]);
    }

    private void OpenCategory(ShopCategory value)
    {
        bool wasHidden = level == 0;
        category = value;
        level = 1;
        highlight = 0;
        band = 0;
        listItems.Clear();
        listItems.AddRange(catalog.ItemsIn(value));
        selected = listItems.Count > 0 ? listItems[0] : null;
        BuildItemRows();
        Render();
        if (wasHidden) Reveal(ref listRoutine, listMask, false, 0.13f);
        if (selected != null) Reveal(ref detailRoutine, detailMask, true, 0.13f, detailGroup);
    }

    private void Select(ShopItem item)
    {
        if (item == null || selected == item) return;
        selected = item;
        band = 0;
        highlight = Mathf.Max(0, listItems.IndexOf(item));
        Render();
    }

    private void Back()
    {
        if (level == 0) { SetOpen(false); return; }
        highlight = category.HasValue ? Mathf.Max(0, categories.IndexOf(category.Value)) : 0;
        level = 0;
        category = null;
        selected = null;
        listItems.Clear();
        BuildItemRows();
        Render();
    }

    private void MoveHighlight(int step)
    {
        int count = level == 0 ? categories.Count : listItems.Count;
        highlight = (highlight + step + count) % count;
        if (level == 1)
        {
            selected = listItems[highlight];
            band = 0;
        }
        Render();
    }

    private void TryBuy(ShopItem item)
    {
        ShopResult result = loadout.Buy(item);
        if (result == ShopResult.Ok)
        {
            ShowToast(item.price > 0 ? $"{ItemName(item)} <color=#F29A38>−{Money(item.price)}</color>" : $"Elegiste {ItemName(item)}");
            return;
        }
        ShowToast($"<color=#FF5C5C>{ErrorText(result, item)}</color>");
        Shake();
    }

    private void TrySell(ShopItem item)
    {
        if (loadout.CheckAccess() != ShopResult.Ok) return;
        ShopResult result = loadout.Sell(item);
        if (result == ShopResult.Ok)
        {
            ShowToast($"Vendiste {ItemName(item)} <color=#3DDC97>+{Money(item.price)}</color>");
            return;
        }
        ShowToast($"<color=#FF5C5C>{ErrorText(result, item)}</color>");
        Shake();
    }

    private string ErrorText(ShopResult result, ShopItem item)
    {
        switch (result)
        {
            case ShopResult.NotEnoughMoney: return $"Te faltan {Money(loadout.CostOf(item) - loadout.Wallet.Money)}";
            case ShopResult.AlreadyEquipped: return $"Ya tenés {ItemName(item)}";
            case ShopResult.MaxReached: return $"Máximo de {item.displayName.ToLowerInvariant()}";
            case ShopResult.ShieldFull: return "Tu escudo ya está lleno";
            case ShopResult.BuyPhaseOver: return "Terminó la fase de compra";
            case ShopResult.OutsideBuyZone: return "Fuera de la zona de compra";
            case ShopResult.NotSellable:
                bool owned = loadout.IsEquipped(item) || loadout.Count(item) > 0 || (item.kind == ShopItemKind.Shield && loadout.Shield > 0);
                return owned ? "Solo se vende lo que compraste esta ronda" : "No lo tenés";
            default: return "";
        }
    }

    private void UndoAll()
    {
        if (!loadout.HasPurchases) return;
        loadout.UndoPurchases();
        ShowToast("Compras deshechas");
    }

    // ---------- Actualización por cuadro (tiempo, zona) ----------

    private void UpdateLive()
    {
        bool phaseActive = phase == null || phase.IsActive;
        ShopResult access = loadout.CheckAccess();

        hudClock.SetActive(!open && phase != null && phase.IsActive);
        hudHint.SetActive(!open && phaseActive && access == ShopResult.Ok);
        hudMoney.gameObject.SetActive(!open);
        if (phase != null)
        {
            hudTime.text = Clock(phase.TimeLeft);
            hudRound.text = $"Ronda {phase.Round}";
        }

        if (open)
        {
            if (phase == null)
            {
                barPhase.text = "Compra libre";
                barPhase.color = Accent;
                barTime.text = "—";
                SetProgress(1f);
            }
            else if (phase.IsActive)
            {
                barPhase.text = "Fase de compra";
                barPhase.color = Accent;
                barTime.text = Clock(phase.TimeLeft);
                SetProgress(phase.TimeLeft / Mathf.Max(0.01f, phase.Duration));
            }
            else
            {
                barPhase.text = "Terminó la compra";
                barPhase.color = Mute;
                barTime.text = "0:00";
                SetProgress(0f);
            }
        }

        if (access != lastAccess)
        {
            lastAccess = access;
            Render();
        }
    }

    private void SetProgress(float ratio)
    {
        barProgress.sizeDelta = new Vector2(420f * Mathf.Clamp01(ratio), barProgress.sizeDelta.y);
    }

    private static string Clock(float seconds)
    {
        int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        return $"{total / 60}:{total % 60:00}";
    }

    // ---------- Render ----------

    private void Render()
    {
        if (loadout == null || shopRoot == null) return;
        hudMoney.text = Money(loadout.Wallet.Money);
        if (!open) return;

        RenderBar();
        RenderCategories();
        RenderItems();
        RenderDetail();
        RenderLoadout();
    }

    private void RenderBar()
    {
        barSub.text = phase != null ? $"Táctico · Ronda {phase.Round}" : "Táctico";
        barMoney.text = Money(loadout.Wallet.Money);
        barNext.text = $"Próxima ronda: mínimo {Money(minimumNextRound)}";

        ShopResult access = loadout.CheckAccess();
        banner.SetActive(access != ShopResult.Ok);
        if (access == ShopResult.OutsideBuyZone)
            bannerText.text = "Estás fuera de la zona de compra. Volvé al punto de aparición para comprar.";
        else if (access == ShopResult.BuyPhaseOver)
            bannerText.text = "La fase de compra terminó. Podés mirar, pero no comprar.";
    }

    private void RenderCategories()
    {
        for (int i = 0; i < categoryRows.Count; i++)
        {
            Row row = categoryRows[i];
            ShopCategory value = categories[i];
            bool isSelected = category.HasValue && category.Value == value;
            List<ShopItem> items = catalog.ItemsIn(value);

            // Una categoría sin nada que se pueda comprar se ve al 38 %, como el ".row.off" de la maqueta.
            float opacity = items.Exists(IsReachable) ? 1f : 0.38f;
            PaintRow(row, isSelected, (level == 0 && highlight == i) || row.hovered, opacity);
            row.nameText.color = Faded(Ink, opacity);
            row.rightText.color = Faded(Mute, opacity);
            row.chevron.color = Faded(Mute, opacity);
            row.icon.color = value == ShopCategory.Shields
                ? WithAlpha(ShieldColor, FadeAlpha(opacity))
                : new Color(1f, 1f, 1f, FadeAlpha(0.9f * opacity));

            // Nombre, rango de precios y flecha, uno detrás del otro como en la maqueta.
            row.rightText.text = PriceRange(items);
            float nameWidth = Width(row.nameText, row.nameText.text.ToUpperInvariant());
            float rangeWidth = Width(row.rightText, row.rightText.text);
            float rangeX = Mathf.Max(361f - rangeWidth, 155f + nameWidth + 14f);
            Place(row.rightText.rectTransform, rangeX, 0f, rangeWidth + 2f, CatRowH);
            row.chevron.rectTransform.anchoredPosition = new Vector2(Mathf.Max(375f, rangeX + rangeWidth + 14f), 0f);
        }
    }

    private void RenderItems()
    {
        listTitle.text = category.HasValue ? CategoryName(category.Value) : "";
        for (int i = 0; i < itemRows.Count; i++)
        {
            Row row = itemRows[i];
            ShopItem item = listItems[i];
            bool cant = IsCant(item);

            // Estado a la derecha: "Equipada", "Lleno", "Máximo" o el precio.
            float stateWidth;
            string badge = StateBadge(item, out Color badgeInk, out Color badgeFill);
            row.badge.gameObject.SetActive(badge != null);
            row.rightText.gameObject.SetActive(badge == null);
            if (badge != null)
            {
                row.badgeText.text = badge;
                row.badgeText.color = badgeInk;
                row.badge.color = badgeFill;
                stateWidth = Width(row.badgeText, badge.ToUpperInvariant()) + 16f;
                Place(row.badge.rectTransform, 461f - stateWidth, (ItemRowH - 23f) / 2f, stateWidth, 23f);
            }
            else
            {
                row.rightText.text = item.price <= 0 ? "Gratis" : Money(item.price);
                row.rightText.color = cant ? Bad : Ink;
                stateWidth = Mathf.Max(84f, Width(row.rightText, row.rightText.text));
                Place(row.rightText.rectTransform, 461f - stateWidth, 0f, stateWidth, ItemRowH);
            }

            // Nombre (hasta dos líneas) y detalle, centrados en la fila.
            float textWidth = 461f - stateWidth - 14f - 203f;
            string name = ListName(item);
            bool longName = name.Length > 13;
            row.nameText.fontSize = longName ? 19f : 22f;
            row.nameText.characterSpacing = longName ? 3f : 7f;
            row.nameText.text = name;
            float single = row.nameText.GetPreferredValues("A", textWidth, 400f).y;
            float full = row.nameText.GetPreferredValues(name.ToUpperInvariant(), textWidth, 400f).y;
            int lines = full > single * 1.5f ? 2 : 1;
            float nameHeight = lines * row.nameText.fontSize * 1.05f;
            row.subText.text = ListSubtitle(item);
            float top = (ItemRowH - (nameHeight + 3f + 17f)) / 2f;
            Place(row.nameText.rectTransform, 203f, top, textWidth, nameHeight);
            Place(row.subText.rectTransform, 203f, top + nameHeight + 3f, textWidth, 17f);

            row.icon.material = cant && item.IsWeapon ? grayscale : null;
            row.icon.color = IconColor(item, cant);
            PaintRow(row, item == selected, level == 1 && highlight == i);
        }
    }

    // opacity: la opacidad de toda la fila. En la maqueta se aplica a la fila ya armada, así que cada
    // elemento se mezcla con el panel (y no con lo que tiene abajo dentro de la fila).
    private void PaintRow(Row row, bool isSelected, bool isHighlighted, float opacity = 1f)
    {
        row.background.color = isSelected ? Faded(SelectedColor, opacity) : isHighlighted ? Faded(HoverColor, opacity) : Color.clear;
        row.selectionBar.gameObject.SetActive(isSelected);
        row.selectionBar.color = Faded(Accent, opacity);
        row.keyChip.color = Faded(isSelected ? Ink : ChipDim, opacity);
        row.keyText.color = Faded(isSelected ? KeyInk : Ink, opacity);
    }

    private static Color Faded(Color color, float opacity) => opacity >= 1f ? color : Over(WithAlpha(color, opacity), PanelBase);

    private void RenderDetail()
    {
        bool show = level == 1 && selected != null;
        detailPanel.gameObject.SetActive(show);
        if (!show) return;

        ShopItem item = selected;
        bool cant = IsCant(item);
        string title = ItemName(item);
        float nameSize = title.Length > 15 ? 36f : 46f;

        detailPrice.text = item.price <= 0 ? "Gratis" : Money(item.price);
        detailPrice.color = cant ? Bad : Ink;
        float priceWidth = Width(detailPrice, detailPrice.text);
        detailName.text = title;
        detailName.fontSize = nameSize;
        Place(detailName.rectTransform, Pad, 20f, ContentW - priceWidth - 12f, nameSize);
        detailAlias.gameObject.SetActive(item.IsWeapon);
        detailAlias.text = item.displayName;
        detailMeta.text = MetaText(item);
        float headBottom = 20f + nameSize + (item.IsWeapon ? 21f : 0f) + 4f + 19.5f;
        Place(detailMeta.rectTransform, Pad, headBottom - 19.5f, 380f, 19.5f);

        // Imagen: las armas con su brillo naranja; escudos y granadas, el ícono de la lista agrandado 1,9 veces.
        detailIcon.sprite = item.icon;
        detailIcon.color = IconColor(item, false);
        detailGlow.gameObject.SetActive(true);
        float roleTop;
        if (item.IsWeapon)
        {
            // Las armas llenan la ficha y la maqueta las comprime un poco: se usan las medidas de la captura.
            Place(detailPicture, Pad, 104f, ContentW, 132f);
            Place(detailGlow.rectTransform, 0f, 0f, ContentW, 132f);
            detailGlow.color = WithAlpha(Accent, FadeAlpha(0.12f));
            Place(detailIcon.rectTransform, 43f, 4f, 430f, 124f);
            roleTop = 225f;
        }
        else
        {
            Place(detailPicture, Pad, headBottom + 8f, ContentW, 132f);
            float size = IconSize(item) * 1.9f, width = size * IconAspect(item);
            Place(detailIcon.rectTransform, (ContentW - width) / 2f, (132f - size) / 2f, width, size);
            if (item.kind == ShopItemKind.Grenade)
            {
                float glowSize = size * 1.5f;
                Place(detailGlow.rectTransform, (ContentW - glowSize) / 2f, (132f - glowSize) / 2f, glowSize, glowSize);
                detailGlow.color = WithAlpha(item.tint, FadeAlpha(0.55f));
            }
            else detailGlow.gameObject.SetActive(false);
            roleTop = headBottom + 8f + 132f + 2f + 6f;
        }

        string role = item.kind == ShopItemKind.Shield
            ? $"Absorbe los primeros {item.shieldPoints} puntos de daño, antes que la vida. Si sobrevivís, te lo llevás a la próxima ronda con lo que le quede."
            : item.description;
        detailRole.text = role;
        float roleHeight = Mathf.Max(46f, detailRole.GetPreferredValues(role, ContentW, 400f).y);
        Place(detailRole.rectTransform, Pad, roleTop, ContentW, roleHeight);

        Clear(detailBody);
        if (item.IsWeapon) BuildWeaponBody(item);
        else if (item.kind == ShopItemKind.Shield) BuildShieldBody(item, roleTop + roleHeight);
        else BuildGrenadeBody(item, roleTop + roleHeight);
        RenderBuyButton(item);
    }

    // ---------- Ficha de un arma ----------

    private void BuildWeaponBody(ShopItem item)
    {
        if (item.bands == null || item.bands.Length == 0) return;
        int index = Mathf.Clamp(band, 0, item.bands.Length - 1);
        DamageBand damage = item.bands[index];
        const float top = 281f;

        // Figura con el daño por zona.
        RectTransform figure = Place(Node("Figura", detailBody), Pad, top, 132f, 250f);
        Color torso = Hex(0x5B6878), legs = Hex(0x3A4350);
        Image(Place(Node("Torso", figure), 40f, 54f, 52f, 86f), rounded, torso, 12f);
        Image(Place(Node("BrazoIzquierdo", figure), 24f, 58f, 13f, 70f), rounded, torso, 6f);
        Image(Place(Node("BrazoDerecho", figure), 95f, 58f, 13f, 70f), rounded, torso, 6f);
        Image(Place(Node("PiernaIzquierda", figure), 42f, 143f, 22f, 100f), rounded, legs, 9f);
        Image(Place(Node("PiernaDerecha", figure), 68f, 143f, 22f, 100f), rounded, legs, 9f);
        Image(Place(Node("Cabeza", figure), 46f, 10f, 40f, 40f), circle, Accent);
        FigureNumber(figure, 30f, item.ShotDamage(damage, BodyZone.Head), AccentInk);
        FigureNumber(figure, 97f, item.ShotDamage(damage, BodyZone.Body), Ink);
        FigureNumber(figure, 195f, item.ShotDamage(damage, BodyZone.Legs), Ink);
        if (item.pellets > 1)
        {
            TextMeshProUGUI pellets = Text(Place(Node("Perdigones", figure), 0f, 238f, 132f, 14f), labelFont, 11f, Mute, TextAlignmentOptions.Midline, 10f, true);
            pellets.text = $"×{item.pellets} perdigones";
        }

        // Tiros para matar según el escudo del rival.
        RectTransform table = Place(Node("TirosParaMatar", detailBody), 170f, top, 368f, 133f);
        float[] columns = { 0f, 134f, 212f, 290f, 368f };
        TextMeshProUGUI corner = Text(Place(Node("Titulo", table), 6f, 4f, 128f, 14f), labelFont, 13f, Mute, TextAlignmentOptions.MidlineLeft, 8f, true);
        corner.text = "Tiros para matar";
        for (int c = 0; c < 3; c++)
        {
            float x = columns[c + 1], width = columns[c + 2] - x;
            TextMeshProUGUI header = Text(Place(Node("Escudo", table), x, 4f, width, 14f), labelFont, 13f, Mute, TextAlignmentOptions.Midline, 8f, true);
            header.text = TargetNames[c];
            TextMeshProUGUI health = Text(Place(Node("Vida", table), x, 18f, width, 14f), monoFont, 13f, Mute, TextAlignmentOptions.Midline);
            health.text = Targets[c].ToString();
        }
        Line(table, 0f, 36f, 368f);
        for (int r = 0; r < 3; r++)
        {
            float y = 37f + r * 32f;
            int shot = item.ShotDamage(damage, Zones[r]);
            TextMeshProUGUI zone = Text(Place(Node("Zona", table), 6f, y, 128f, 31f), displayFont, 16f, Ink, TextAlignmentOptions.MidlineLeft, 8f, true);
            zone.text = ZoneNames[r];
            for (int c = 0; c < 3; c++)
                ShotsCell(table, columns[c + 1], y, columns[c + 2] - columns[c + 1], DamageMath.ShotsToKill(shot, Targets[c]));
            Line(table, 0f, y + 31f, 368f);
        }

        // Tramos de distancia (se eligen con clic) y lo que mata de un tiro.
        float bandX = 170f;
        for (int i = 0; i < item.bands.Length; i++)
        {
            int bandIndex = i;
            bool pressed = i == index;
            RectTransform chip = Tag(detailBody, bandX, 424f, item.BandLabel(i), pressed ? Accent : ChipColor, pressed ? AccentInk : Mute,
                pressed ? Accent : LineColor, 14f, 8f, 9f, 28f, 5f);
            if (item.bands.Length > 1)
                AddPointer(chip).Clicked = () => { band = bandIndex; Render(); };
            bandX += chip.sizeDelta.x + 6f;
        }
        Flow(detailBody, 170f, 462f, 368f, Breakpoints(item));

        // Datos.
        string[] values =
        {
            item.burstCount > 1 ? $"{Number(item.fireRate)}×{item.burstCount}" : Number(item.fireRate),
            $"{item.magazine}<color=#8E96A3><size=16> / {item.reserve}</size></color>",
            item.reloadTime > 0f ? $"{Number(item.reloadTime)} s" : "—",
            $"{Mathf.RoundToInt(item.mobility * 100f)}%",
            item.zoom > 0f ? $"{Number(item.zoom)}×" + (item.zoom2 > 0f ? $"/{Number(item.zoom2)}×" : "") : "—",
        };
        string[] labels = { item.burstCount > 1 ? "ráfagas/s" : "disparos/s", "cargador", item.reloadPerShell ? "por cartucho" : "recarga", "movilidad", "mira" };
        Stats(detailBody, 543f, values, labels);

        TextMeshProUGUI note = Text(Place(Node("Manejo", detailBody), Pad, 606f, ContentW, 60f), bodyFont, 15f, Note, TextAlignmentOptions.TopLeft);
        note.textWrappingMode = TextWrappingModes.Normal;
        note.lineSpacing = 16f;
        note.text = $"<color=#D6DAE0><b>Retroceso:</b></color> {item.recoil}\n<color=#D6DAE0><b>Dispersión:</b></color> {Number(item.spread.x)}° quieto · {Number(item.spread.y)}° en movimiento · penetración {item.penetration.ToLowerInvariant()}";
    }

    private void FigureNumber(RectTransform figure, float centerY, int value, Color color)
    {
        TextMeshProUGUI text = Text(Place(Node("Danio", figure), 30f, centerY - 10f, 72f, 20f), monoFont, 15f, color, TextAlignmentOptions.Midline);
        text.fontStyle = FontStyles.Bold;
        text.text = value.ToString();
    }

    private void ShotsCell(RectTransform table, float x, float y, float width, int shots)
    {
        RectTransform cell = Place(Node("Celda", table), x, y, width, 31f);
        if (shots == 1) Image(cell, null, Accent);
        TextMeshProUGUI text = Text(Stretch(Node("Tiros", cell)), monoFont, 17f, shots == 1 ? AccentInk : Ink, TextAlignmentOptions.Midline);
        text.fontStyle = FontStyles.Bold;
        text.text = shots.ToString();
    }

    private void Stats(RectTransform parent, float y, string[] values, string[] labels)
    {
        int count = values.Length;
        float width = (ContentW - (count - 1) * 8f) / count;
        for (int i = 0; i < count; i++)
        {
            RectTransform box = Place(Node("Dato", parent), Pad + i * (width + 8f), y, width, 55f);
            Image(box, rounded, BoxColor, 6f);
            TextMeshProUGUI value = Text(Place(Node("Valor", box), 8f, 8f, width - 12f, 22f), displayFont, 22f, Ink);
            value.text = values[i];
            TextMeshProUGUI label = Text(Place(Node("Nombre", box), 8f, 33f, width - 12f, 14f), labelFont, 12f, Mute, TextAlignmentOptions.MidlineLeft, 10f, true);
            label.text = labels[i];
        }
    }

    // Claves en etiquetas que bajan de renglón cuando no entran (el ".bp" de la maqueta).
    private void Flow(RectTransform parent, float x, float y, float width, List<KeyValuePair<string, bool>> tags)
    {
        float cursorX = 0f, cursorY = 0f;
        foreach (KeyValuePair<string, bool> tag in tags)
        {
            RectTransform chip = Tag(parent, x + cursorX, y + cursorY, tag.Key, ChipColor, tag.Value ? Hot : Ink,
                tag.Value ? Over(Rgb(242, 154, 56, 0.7f), ChipColor) : LineColor);
            if (cursorX > 0f && cursorX + chip.sizeDelta.x > width)
            {
                cursorX = 0f;
                cursorY += 34f;
                chip.anchoredPosition = new Vector2(x, -(y + cursorY));
            }
            cursorX += chip.sizeDelta.x + 6f;
        }
    }

    // ---------- Ficha de un escudo ----------

    private void BuildShieldBody(ShopItem item, float y)
    {
        ShopItem rifle = MostExpensive(ShopCategory.Rifles);
        ShopItem smg = MostExpensive(ShopCategory.SMGs);
        ShopItem frag = catalog.items.Find(i => i != null && i.kind == ShopItemKind.Grenade && i.grenadeType == GrenadeType.Frag);
        int withShield = 100 + item.shieldPoints;

        // Cuántos tiros aguanta, sin escudo y con este.
        y += 12f;
        RectTransform table = Place(Node("Tabla", detailBody), Pad, y, ContentW, 120f);
        float[] columns = { 0f, 271f, 402f, ContentW };
        string[] headers = { "Contra", "Sin escudo", "Con este" };
        for (int c = 0; c < 3; c++)
        {
            TextMeshProUGUI header = Text(Place(Node("Columna", table), columns[c] + (c == 0 ? 6f : 0f), 4f, columns[c + 1] - columns[c], 14f),
                labelFont, 13f, Mute, c == 0 ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Midline, 8f, true);
            header.text = headers[c];
        }
        Line(table, 0f, 22f, ContentW);
        List<(string label, int damage)> rows = new List<(string label, int damage)>();
        if (rifle != null) rows.Add(($"{ItemName(rifle)} al cuerpo", rifle.ShotDamage(rifle.bands[0], BodyZone.Body)));
        if (smg != null) rows.Add(($"{ItemName(smg)} al cuerpo", smg.ShotDamage(smg.bands[0], BodyZone.Body)));
        if (frag != null && frag.bands != null && frag.bands.Length > 0) rows.Add(("Metralla (centro)", frag.bands[0].body));
        for (int r = 0; r < rows.Count; r++)
        {
            float rowY = 23f + r * 32f;
            TextMeshProUGUI label = Text(Place(Node("Arma", table), 6f, rowY, columns[1] - 6f, 31f), displayFont, 16f, Ink, TextAlignmentOptions.MidlineLeft, 8f, true);
            label.text = rows[r].label;
            ShotsCell(table, columns[1], rowY, columns[2] - columns[1], DamageMath.ShotsToKill(rows[r].damage, 100));
            ShotsCell(table, columns[2], rowY, columns[3] - columns[2], DamageMath.ShotsToKill(rows[r].damage, withShield));
            if (r < rows.Count - 1) Line(table, 0f, rowY + 31f, ContentW);
        }
        y += 23f + rows.Count * 32f;

        // Vida efectiva: vida + escudo sobre 150.
        y += 12f;
        float height = 64f * FxScale + 20f;
        RectTransform fx = FxBox(y, height);
        float unit = (500f - 2f) / 150f * FxScale, left = 12f, top = 10f;
        SvgText(fx, left, top + 11f * FxScale, "Vida 100", labelFont, 13f, Mute, -1);
        SvgText(fx, left + 100f * unit + 6f, top + 11f * FxScale, $"+{item.shieldPoints} escudo", labelFont, 13f, ShieldColor, -1);
        Image(Place(Node("Vida", fx), left, top + 18f * FxScale, 100f * unit, 18f * FxScale), rounded, Ink, 3f);
        Image(Place(Node("Escudo", fx), left + 100f * unit + 3f, top + 18f * FxScale, item.shieldPoints * unit - 3f, 18f * FxScale), rounded, ShieldColor, 3f);
        if (item.shieldPoints < 50)
        {
            // Lo que le falta para ser escudo completo, punteado.
            float x0 = left + (100f + item.shieldPoints) * unit + 3f, x1 = x0 + (50 - item.shieldPoints) * unit - 4f;
            float y0 = top + 18.5f * FxScale, y1 = y0 + 17f * FxScale;
            Color dash = Over(White(0.25f), FxColor);
            DashedLine(fx, new Vector2(x0, y0), new Vector2(x1, y0), 4f, 4f, dash);
            DashedLine(fx, new Vector2(x0, y1), new Vector2(x1, y1), 4f, 4f, dash);
            DashedLine(fx, new Vector2(x0, y0), new Vector2(x0, y1), 4f, 4f, dash);
            DashedLine(fx, new Vector2(x1, y0), new Vector2(x1, y1), 4f, 4f, dash);
        }
        SvgText(fx, left + 1f, top + 56f * FxScale, "0", monoFont, 13f, Ink, -1, 0f, false);
        SvgText(fx, left + 100f * unit, top + 56f * FxScale, "100", monoFont, 13f, Ink, 0, 0f, false);
        SvgText(fx, left + 125f * unit, top + 56f * FxScale, "125", monoFont, 13f, Ink, 0, 0f, false);
        SvgText(fx, left + 499f * FxScale, top + 56f * FxScale, "150", monoFont, 13f, Ink, 1, 0f, false);
        y += height + 10f;

        Flow(detailBody, Pad, y, ContentW, new List<KeyValuePair<string, bool>>
        {
            new KeyValuePair<string, bool>($"Vida efectiva {withShield}", true),
            new KeyValuePair<string, bool>($"Tenés {loadout.Shield}", false),
        });
    }

    // ---------- Ficha de una granada ----------

    private void BuildGrenadeBody(ShopItem item, float y)
    {
        y += 12f;
        float chartHeight = item.grenadeType == GrenadeType.Frag ? 134f : item.grenadeType == GrenadeType.Smoke ? 120f : 118f;
        float height = chartHeight * FxScale + 20f;
        RectTransform fx = FxBox(y, height);
        switch (item.grenadeType)
        {
            case GrenadeType.Frag: FragChart(fx, item); break;
            case GrenadeType.Smoke: SmokeChart(fx, item); break;
            default: FlashChart(fx, item); break;
        }
        y += height + 12f;

        int totalMax = 0;
        foreach (ShopItem other in catalog.ItemsIn(ShopCategory.Grenades)) totalMax += other.maxCarry;
        Stats(detailBody, y, new[] { $"{loadout.Count(item)}/{item.maxCarry}", $"{Number(item.fuse)} s", totalMax.ToString() },
            new[] { "llevás", "mecha", "máx. en total" });
        y += 55f + 10f;

        Flow(detailBody, Pad, y, ContentW, new List<KeyValuePair<string, bool>>
        {
            new KeyValuePair<string, bool>("Espacio 4 · apretá 4 de nuevo para cambiar", false),
        });
    }

    private RectTransform FxBox(float y, float height)
    {
        RectTransform box = Place(Node("Efecto", detailBody), Pad, y, ContentW, height);
        Image(box, rounded, FxColor, 8f);
        return box;
    }

    // Daño de la metralla según la distancia: 100 hasta 1,5 m y baja en línea recta hasta 25 a 5 m.
    private void FragChart(RectTransform fx, ShopItem item)
    {
        if (item.bands == null || item.bands.Length < 2) return;
        DamageBand center = item.bands[0], edge = item.bands[1];
        const float left = 12f, top = 10f;
        Vector2 P(float meters, float damage) => new Vector2(left + (34f + meters / 6f * 456f) * FxScale, top + (104f - damage * 0.84f) * FxScale);
        float right = left + 490f * FxScale;

        Segment(fx, P(0f, 0f), new Vector2(right, P(0f, 0f).y), 1f, Over(White(0.18f), FxColor));
        DashedLine(fx, P(0f, center.body), new Vector2(right, P(0f, center.body).y), 4f, 4f, Over(White(0.12f), FxColor));
        Vector2[] points = { P(0f, center.body), P(center.upTo, center.body), P(edge.upTo, edge.body), P(edge.upTo, 0f), P(6f, 0f) };
        for (int i = 0; i < points.Length - 1; i++) Segment(fx, points[i], points[i + 1], 3f, item.tint);
        for (int i = 1; i < points.Length - 1; i++) Image(Place(Node("Union", fx), points[i].x - 1.5f, points[i].y - 1.5f, 3f, 3f), circle, item.tint);

        SvgText(fx, left + 28f * FxScale, P(0f, center.body).y + 4f * FxScale, center.body.ToString(), monoFont, 13f, Ink, 1, 0f, false);
        SvgText(fx, left + 28f * FxScale, P(0f, edge.body).y + 4f * FxScale, edge.body.ToString(), monoFont, 13f, Ink, 1, 0f, false);
        SvgText(fx, P(center.upTo, 0f).x, top + 128f * FxScale, $"{Number(center.upTo)} m", monoFont, 13f, Ink, 0, 0f, false);
        SvgText(fx, P(edge.upTo, 0f).x, top + 128f * FxScale, $"{Number(edge.upTo)} m", monoFont, 13f, Ink, 0, 0f, false);
        SvgText(fx, P(2.4f, 0f).x, P(0f, center.body).y - 6f * FxScale, "Mata solo sin escudo, y de muy cerca", labelFont, 13f, Mute, -1);
    }

    // Radio y duración del humo.
    private void SmokeChart(RectTransform fx, ShopItem item)
    {
        const float left = 12f, top = 10f;
        Vector2 P(float x, float y) => new Vector2(left + x * FxScale, top + y * FxScale);

        Vector2 center = P(60f, 58f);
        float radius = 46f * FxScale;
        Image(Place(Node("Humo", fx), center.x - radius, center.y - radius, radius * 2f, radius * 2f), circle, Over(WithAlpha(item.tint, 0.35f), FxColor));
        float ringHalf = radius * 48f / 46f; // el anillo del sprite está a 46 px del centro, en 48
        Image(Place(Node("Borde", fx), center.x - ringHalf, center.y - ringHalf, ringHalf * 2f, ringHalf * 2f), ring, item.tint);
        Segment(fx, center, P(106f, 58f), 2f, Ink);
        SvgText(fx, P(70f, 0f).x, P(0f, 52f).y, $"{Number(item.effectRadius)} m", monoFont, 13f, Ink, -1, 0f, false);
        SvgText(fx, P(140f, 0f).x, P(0f, 36f).y, $"Dura {Number(item.effectDuration)} s", labelFont, 13f, Mute, -1);
        Image(Place(Node("Duracion", fx), P(140f, 0f).x, P(0f, 46f).y, 340f * FxScale, 18f * FxScale), rounded, Over(WithAlpha(item.tint, 0.55f), FxColor), 3f);
        SvgText(fx, P(140f, 0f).x, P(0f, 84f).y, "0", monoFont, 13f, Ink, -1, 0f, false);
        SvgText(fx, P(480f, 0f).x, P(0f, 84f).y, $"{Number(item.effectDuration)} s", monoFont, 13f, Ink, 1, 0f, false);
        SvgText(fx, P(140f, 0f).x, P(0f, 108f).y, "Tapa la visión de adentro hacia afuera y al revés", labelFont, 13f, Mute, -1);
    }

    // Ceguera de la flash según hacia dónde mirás: total (barra llena) y recuperación (degradé).
    private void FlashChart(RectTransform fx, ShopItem item)
    {
        const float left = 12f, top = 10f;
        float full = item.effectDuration, back = item.recovery;
        FlashBar(fx, left, top + 4f * FxScale, "De frente (< 15 m)", full, back, item.tint);
        FlashBar(fx, left, top + 40f * FxScale, "De costado", full / 2f, back / 2f, item.tint);
        FlashBar(fx, left, top + 76f * FxScale, "De espaldas", 0.5f, 0f, item.tint);
    }

    private void FlashBar(RectTransform fx, float left, float y, string label, float full, float back, Color color)
    {
        const float perSecond = 330f / 4f;
        SvgText(fx, left, y + 13f * FxScale, label, labelFont, 13f, Mute, -1);
        Image(Place(Node("Ceguera", fx), left + 150f * FxScale, y, full * perSecond * FxScale, 18f * FxScale), rounded, color, 3f);
        if (back > 0f) Image(Place(Node("Recuperacion", fx), left + (150f + full * perSecond) * FxScale, y, back * perSecond * FxScale, 18f * FxScale), fade, color);
        SvgText(fx, left + (150f + (full + back) * perSecond + 8f) * FxScale, y + 13f * FxScale, $"{Number(full + back)} s", monoFont, 13f, Ink, -1, 0f, false);
    }

    // Texto ubicado como en un SVG: x con su anclaje (-1 inicio, 0 centro, 1 fin) y la línea de base.
    private TextMeshProUGUI SvgText(RectTransform parent, float x, float baseline, string value, TMP_FontAsset font, float size, Color color,
        int anchor, float spacing = 6f, bool upper = true)
    {
        const float width = 420f;
        float height = size * 1.4f;
        float left = anchor < 0 ? x : anchor == 0 ? x - width / 2f : x - width;
        TextAlignmentOptions alignment = anchor < 0 ? TextAlignmentOptions.MidlineLeft : anchor == 0 ? TextAlignmentOptions.Midline : TextAlignmentOptions.MidlineRight;
        TextMeshProUGUI text = Text(Place(Node("Texto", parent), left, baseline - size * 0.36f - height / 2f, width, height), font, size, color, alignment, spacing, upper);
        text.text = value;
        return text;
    }

    // ---------- Botón de compra ----------

    private void RenderBuyButton(ShopItem item)
    {
        ShopResult access = loadout.CheckAccess();
        int money = loadout.Wallet.Money;
        int cost = loadout.CostOf(item);

        if (access != ShopResult.Ok)
            PaintButton(ButtonStyle.Locked, access == ShopResult.BuyPhaseOver ? "Terminó la compra" : "Fuera de la zona", "solo mirar");
        else if (item.IsWeapon && loadout.IsEquipped(item))
            PaintButton(ButtonStyle.Owned, "Ya la tenés", loadout.BoughtThisPhase(item)
                ? $"clic derecho: vender +{Money(item.price)}"
                : item.price <= 0 ? "la tenés siempre" : "la trajiste de la ronda anterior");
        else if (item.kind == ShopItemKind.Shield && loadout.Shield >= item.shieldPoints)
            PaintButton(ButtonStyle.Owned, "Escudo lleno", loadout.BoughtThisPhase(item) ? $"clic derecho: vender +{Money(item.price)}" : "—");
        else if (item.kind == ShopItemKind.Grenade && loadout.Count(item) >= item.maxCarry)
            PaintButton(ButtonStyle.Locked, "Máximo alcanzado", $"{item.maxCarry} por jugador");
        else if (cost > money)
            PaintButton(ButtonStyle.Cant, $"Te faltan {Money(cost - money)}", Money(item.price));
        else
            PaintButton(ButtonStyle.Normal, item.price <= 0 ? "Elegir · Gratis" : $"Comprar · {Money(item.price)}",
                cost == item.price ? "Enter · clic" : cost < 0 ? $"te devuelven {Money(-cost)}" : $"pagás {Money(cost)}");
    }

    private void PaintButton(ButtonStyle style, string label, string sub)
    {
        buyLabel.text = label;
        buySub.text = sub;
        Color fill, ink;
        switch (style)
        {
            case ButtonStyle.Normal: fill = Accent; ink = AccentInk; break;
            case ButtonStyle.Cant: fill = Over(Rgb(255, 92, 92, 0.16f), PanelBase); ink = Bad; break;
            case ButtonStyle.Owned: fill = Over(Rgb(61, 220, 151, 0.14f), PanelBase); ink = Ok; break;
            default: fill = Over(White(0.08f), PanelBase); ink = Mute; break;
        }
        buyButton.color = fill;
        buyLabel.color = ink;
        buySub.color = Over(WithAlpha(ink, 0.75f), fill);
    }

    // ---------- Equipamiento ----------

    private void RenderLoadout()
    {
        for (int i = 0; i < loadoutCells.Length; i++) Clear(loadoutCells[i]);

        SlotHeader(0, "1", "Principal");
        SlotWeapon(0, loadout.Primary);
        SlotHeader(1, "2", "Secundaria");
        SlotWeapon(1, loadout.Secondary);
        SlotHeader(2, "3", "Cuchillo");
        SlotLabel(2, 18f, "Cuchillo", Ink);

        SlotHeader(3, "4", "Granadas");
        float x = 18f;
        foreach (ShopItem grenade in catalog.ItemsIn(ShopCategory.Grenades))
        {
            int count = loadout.Count(grenade);
            RectTransform chip = Place(Node("Granada", loadoutCells[3]), x, 58.5f, 10f, 25f);
            Image(chip, rounded, ChipColor, 4f);
            chip.gameObject.AddComponent<CanvasGroup>().alpha = count > 0 ? 1f : FadeAlpha(0.35f);
            Image(Place(Node("Color", chip), 7f, 7.5f, 10f, 10f), circle, grenade.tint);
            TextMeshProUGUI text = Text(Place(Node("Cantidad", chip), 23f, 0f, 60f, 25f), monoFont, 15f, Ink);
            text.fontStyle = FontStyles.Bold;
            text.text = $"{count}/{grenade.maxCarry}";
            float width = 23f + Width(text, text.text) + 7f;
            chip.sizeDelta = new Vector2(width, 25f);
            x += width + 14f;
        }

        SlotHeader(4, null, "Escudo");
        int maxShield = 0;
        foreach (ShopItem shield in catalog.ItemsIn(ShopCategory.Shields)) maxShield = Mathf.Max(maxShield, shield.shieldPoints);
        RectTransform track = Place(Node("Barra", loadoutCells[4]), 18f, 65f, 150f, 12f);
        Image(track, rounded, TrackColor, 6f);
        float ratio = Mathf.Clamp01(loadout.Shield / (float)Mathf.Max(1, maxShield));
        if (ratio > 0f) Image(Place(Node("Relleno", track), 0f, 0f, 150f * ratio, 12f), rounded, ShieldColor, 6f);
        TextMeshProUGUI shieldValue = Text(Place(Node("Valor", loadoutCells[4]), 178f, 51f, 80f, 40f), monoFont, 20f, ShieldColor);
        shieldValue.text = loadout.Shield.ToString();

        // Gastado esta ronda y "Deshacer" (US 076, CA10), alineados a la derecha.
        float right = loadoutWidths[5] - 18f;
        TextMeshProUGUI title = Text(Place(Node("Titulo", loadoutCells[5]), 0f, 21f, right, 24f), labelFont, 14f, Mute, TextAlignmentOptions.MidlineRight, 12f, true);
        title.text = "Gastado esta ronda";
        if (loadout.HasPurchases && loadout.CheckAccess() == ShopResult.Ok)
        {
            RectTransform undo = Place(Node("Deshacer", loadoutCells[5]), 0f, 54f, 10f, 34f);
            Image(undo, rounded, Over(White(0.08f), PanelBase), 6f, true);
            Border(undo, 6f, LineColor);
            TextMeshProUGUI undoText = Text(Stretch(Node("Texto", undo)), displayFont, 16f, Ink, TextAlignmentOptions.Midline, 10f, true);
            undoText.text = "Deshacer";
            float undoWidth = Width(undoText, "DESHACER") + 26f;
            undo.sizeDelta = new Vector2(undoWidth, 34f);
            undo.anchoredPosition = new Vector2(right - undoWidth, -54f);
            AddPointer(undo).Clicked = UndoAll;
            right -= undoWidth + 10f;
        }
        TextMeshProUGUI spent = Text(Place(Node("Gastado", loadoutCells[5]), 0f, 51f, right, 40f), displayFont, 30f, Ink, TextAlignmentOptions.MidlineRight, 5f);
        spent.text = Money(loadout.SpentThisPhase);
    }

    private void SlotHeader(int cell, string key, string label)
    {
        float x = 18f;
        if (key != null) x += Key(loadoutCells[cell], x, 21f, key, false, true).width + 8f;
        TextMeshProUGUI text = Text(Place(Node("Titulo", loadoutCells[cell]), x, 21f, 220f, 24f), labelFont, 14f, Mute, TextAlignmentOptions.MidlineLeft, 12f, true);
        text.text = label;
    }

    private void SlotWeapon(int cell, ShopItem item)
    {
        if (item == null)
        {
            SlotLabel(cell, 18f, "Vacío", Mute);
            return;
        }
        float width = 0f;
        if (item.icon != null)
        {
            width = Mathf.Min(130f, 40f * IconAspect(item));
            UnityEngine.UI.Image icon = Image(Place(Node("Icono", loadoutCells[cell]), 18f, 51f, width, 40f), item.icon, Color.white);
            icon.preserveAspect = true;
            width += 10f;
        }
        SlotLabel(cell, 18f + width, ItemName(item), Ink);
    }

    private void SlotLabel(int cell, float x, string label, Color color)
    {
        TextMeshProUGUI text = Text(Place(Node("Nombre", loadoutCells[cell]), x, 51f, loadoutWidths[cell] - x - 12f, 40f), displayFont, 21f, color, TextAlignmentOptions.MidlineLeft, 5f, true);
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.text = label;
    }

    // ---------- Textos y estados derivados ----------

    private bool IsReachable(ShopItem item)
    {
        if (item.IsWeapon && loadout.IsEquipped(item)) return false;
        if (item.kind == ShopItemKind.Shield && loadout.Shield >= item.shieldPoints) return false;
        if (item.kind == ShopItemKind.Grenade && loadout.Count(item) >= item.maxCarry) return false;
        return loadout.CostOf(item) <= loadout.Wallet.Money;
    }

    private bool IsCant(ShopItem item)
    {
        if (item.IsWeapon && loadout.IsEquipped(item)) return false;
        if (item.kind == ShopItemKind.Shield && loadout.Shield >= item.shieldPoints) return false;
        return loadout.CostOf(item) > loadout.Wallet.Money;
    }

    private string StateBadge(ShopItem item, out Color ink, out Color fill)
    {
        ink = Ok;
        fill = Over(Rgb(61, 220, 151, 0.16f), PanelBase);
        if (item.IsWeapon && loadout.IsEquipped(item)) return "Equipada";
        if (item.kind == ShopItemKind.Shield && loadout.Shield >= item.shieldPoints) return "Lleno";
        if (item.kind == ShopItemKind.Grenade && loadout.Count(item) >= item.maxCarry)
        {
            ink = Mute;
            fill = Over(White(0.1f), PanelBase);
            return "Máximo";
        }
        return null;
    }

    private static Color IconColor(ShopItem item, bool cant)
    {
        Color color = item.kind == ShopItemKind.Shield ? WithAlpha(item.tint, FadeAlpha(item.tint.a)) : Color.white;
        if (cant && item.IsWeapon) color = new Color(0.6f, 0.6f, 0.6f, color.a);
        return color;
    }

    // Alto del ícono en la lista: las armas ocupan su caja; granadas y escudos, como en la maqueta.
    private static float IconSize(ShopItem item)
    {
        if (item.kind == ShopItemKind.Grenade) return 54f;
        if (item.kind == ShopItemKind.Shield) return item.shieldPoints >= 50 ? 54f : 40f;
        return 56f;
    }

    private static float IconAspect(ShopItem item)
    {
        if (item.icon == null || item.icon.rect.height <= 0f) return 1f;
        return item.icon.rect.width / item.icon.rect.height;
    }

    private static string CategoryName(ShopCategory value)
    {
        switch (value)
        {
            case ShopCategory.Pistols: return "Pistolas";
            case ShopCategory.SMGs: return "Subfusiles";
            case ShopCategory.Shotguns: return "Escopetas";
            case ShopCategory.Rifles: return "Fusiles";
            case ShopCategory.Snipers: return "Francotiradores";
            case ShopCategory.Shields: return "Escudos";
            default: return "Granadas";
        }
    }

    private static string PriceRange(List<ShopItem> items)
    {
        if (items.Count == 0) return "";
        int low = int.MaxValue, high = int.MinValue;
        foreach (ShopItem item in items)
        {
            low = Mathf.Min(low, item.price);
            high = Mathf.Max(high, item.price);
        }
        return low == high ? Money(low, false) : $"{Money(low, false)}+";
    }

    private static string ListName(ShopItem item) => string.IsNullOrEmpty(item.alias) ? item.displayName : item.alias;

    // Las armas se llaman por su nombre (Mitre, Urquiza), como en Valorant; el tipo (Fusil, Subfusil) queda de dato.
    private static string ItemName(ShopItem item) => item.IsWeapon && !string.IsNullOrEmpty(item.alias) ? item.alias : item.displayName;

    private string ListSubtitle(ShopItem item)
    {
        if (item.IsWeapon) return $"{item.displayName} · {item.fireMode}";
        if (item.kind == ShopItemKind.Shield) return item.shortEffect;
        return $"{loadout.Count(item)}/{item.maxCarry} · {item.shortEffect}";
    }

    private static string MetaText(ShopItem item)
    {
        switch (item.kind)
        {
            case ShopItemKind.PrimaryWeapon: return $"Principal · {item.fireMode}";
            case ShopItemKind.SecondaryWeapon: return $"Secundaria · {item.fireMode}";
            case ShopItemKind.Shield: return $"Escudo · {item.shieldPoints} puntos";
            default: return "Granada · espacio 4";
        }
    }

    private ShopItem MostExpensive(ShopCategory value)
    {
        ShopItem best = null;
        foreach (ShopItem item in catalog.ItemsIn(value))
            if (item.bands != null && item.bands.Length > 0 && (best == null || item.price > best.price)) best = item;
        return best;
    }

    // Qué mata de un tiro, en palabras (los "breakpoints" del diseño de armas).
    private static List<KeyValuePair<string, bool>> Breakpoints(ShopItem item)
    {
        List<KeyValuePair<string, bool>> result = new List<KeyValuePair<string, bool>>();
        DamageBand first = item.bands[0];

        if (item.pellets > 1)
        {
            int all = item.ShotDamage(first, BodyZone.Body);
            result.Add(new KeyValuePair<string, bool>($"Todo al cuerpo a menos de {Number(first.upTo)} m: {all}", all >= 100));
            result.Add(new KeyValuePair<string, bool>(all >= 125 ? "Mata de 1 sin escudo o con liviano" : all >= 100 ? "Mata de 1 sin escudo" : "Nunca mata de 1", all >= 100));
            return result;
        }

        int head = first.head;
        bool headEverywhere = System.Array.TrueForAll(item.bands, b => b.head >= 150);
        if (head >= 150) result.Add(new KeyValuePair<string, bool>(headEverywhere ? "1 a la cabeza mata a cualquiera" : $"1 a la cabeza mata a cualquiera ({item.BandLabel(0)})", true));
        else if (head >= 125) result.Add(new KeyValuePair<string, bool>("1 a la cabeza mata con escudo liviano", true));
        else if (head >= 100) result.Add(new KeyValuePair<string, bool>("1 a la cabeza mata sin escudo", true));
        else result.Add(new KeyValuePair<string, bool>($"{DamageMath.ShotsToKill(head, 150)} a la cabeza con escudo completo", false));

        int body = first.body;
        if (body >= 150) result.Add(new KeyValuePair<string, bool>("1 al cuerpo mata a cualquiera", true));
        else if (body >= 100) result.Add(new KeyValuePair<string, bool>("1 al cuerpo mata sin escudo", true));
        else result.Add(new KeyValuePair<string, bool>($"{DamageMath.ShotsToKill(body, 150)} al cuerpo con escudo completo", false));
        return result;
    }

    // ---------- Construcción de la interfaz ----------

    private void BuildCanvas()
    {
        GameObject canvasObject = new GameObject("TiendaCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        canvasObject.layer = 5;
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
        UnityEngine.UI.CanvasScaler scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        // Expand: el lienzo mide al menos 1920 x 1080, así el diseño entra entero en cualquier proporción.
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        canvasRoot = (RectTransform)canvasObject.transform;

        hud = Centered(Node("HUD", canvasRoot));
        BuildHud();

        shopRoot = Stretch(Node("Tienda", canvasRoot));
        shopGroup = shopRoot.gameObject.AddComponent<CanvasGroup>();
        Image(Stretch(Node("Oscurecer", shopRoot)), null, ShadeColor, 0f, true);
        BuildTopBar();
        stage = Centered(Node("Escenario", shopRoot));
        BuildBanner();
        BuildCategoryPanel();
        BuildListPanel();
        BuildDetailPanel();
        BuildLoadoutBar();
        BuildToast();
    }

    private static RectTransform Centered(RectTransform rect)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(1920f, 1080f);
        return rect;
    }

    private Sprite BorderFor(float radius)
    {
        int best = 0;
        for (int i = 1; i < BorderRadii.Length; i++)
            if (Mathf.Abs(BorderRadii[i] - radius) < Mathf.Abs(BorderRadii[best] - radius)) best = i;
        return borders != null && best < borders.Length ? borders[best] : null;
    }

    private void Border(RectTransform rect, float radius, Color color)
    {
        Sprite sprite = BorderFor(radius);
        if (sprite == null) return;
        UnityEngine.UI.Image image = Image(Stretch(Node("Borde", rect)), sprite, color);
        image.type = UnityEngine.UI.Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
    }

    // Panel con fondo y recorte (para la animación de aparición). El borde se agrega al final, arriba de todo.
    private RectTransform Panel(string name, float x, float y, float width, float height, out UnityEngine.UI.RectMask2D mask)
    {
        RectTransform root = Place(Node(name, stage), x, y, width, height);
        mask = root.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        Image(Stretch(Node("Fondo", root)), rounded, PanelColor, 10f, true);
        return root;
    }

    private void Line(RectTransform parent, float x, float y, float width, float height = 1f)
    {
        Image(Place(Node("Linea", parent), x, y, width, height), null, LineColor);
    }

    private (UnityEngine.UI.Image chip, TextMeshProUGUI text, float width) Key(Transform parent, float x, float y, string key, bool big, bool dim)
    {
        float height = big ? 30f : 24f;
        RectTransform rect = Place(Node("Tecla", parent), x, y, height, height);
        UnityEngine.UI.Image chip = Image(rect, rounded, dim ? ChipDim : Ink, big ? 6f : 5f);
        TextMeshProUGUI text = Text(Stretch(Node("Texto", rect)), monoFont, big ? 18f : 14f, dim ? Ink : KeyInk, TextAlignmentOptions.Midline);
        text.fontStyle = FontStyles.Bold;
        float width;
        Sprite glyph = key == "↵" ? enterIcon : key == "⌫" ? backIcon : null;
        if (glyph != null)
        {
            float glyphWidth = key == "↵" ? 11f : 14f;
            Glyph(rect, glyph, glyphWidth, text.color);
            width = Mathf.Max(height, glyphWidth + 14f);
        }
        else
        {
            text.text = key;
            width = Mathf.Max(height, Width(text, key) + 14f);
        }
        rect.sizeDelta = new Vector2(width, height);
        return (chip, text, width);
    }

    // Símbolo dibujado (↵, ⌫) centrado en su padre, con el ancho pedido y su proporción.
    private UnityEngine.UI.Image Glyph(RectTransform parent, Sprite sprite, float width, Color color)
    {
        RectTransform rect = Node("Simbolo", parent);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, width * sprite.rect.height / sprite.rect.width);
        return Image(rect, sprite, color);
    }

    // Etiqueta con fondo y borde: tramos de distancia y claves de la ficha.
    private RectTransform Tag(RectTransform parent, float x, float y, string label, Color fill, Color ink, Color border,
        float size = 14f, float spacing = 5f, float padX = 8f, float height = 28f, float radius = 4f)
    {
        RectTransform rect = Place(Node("Etiqueta", parent), x, y, 10f, height);
        Image(rect, rounded, fill, radius, true);
        Border(rect, radius, border);
        TextMeshProUGUI text = Text(Stretch(Node("Texto", rect)), labelFont, size, ink, TextAlignmentOptions.Midline, spacing, true);
        text.text = label;
        rect.sizeDelta = new Vector2(Width(text, label.ToUpperInvariant()) + padX * 2f + 2f, height);
        return rect;
    }

    private static ShopPointerTarget AddPointer(RectTransform rect)
    {
        ShopPointerTarget pointer = rect.GetComponent<ShopPointerTarget>();
        return pointer != null ? pointer : rect.gameObject.AddComponent<ShopPointerTarget>();
    }

    private static void Clear(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
        parent.DetachChildren();
    }

    private void BuildHud()
    {
        RectTransform clock = Place(Node("RelojFase", hud), 865f, 22f, 190f, 96f);
        Image(clock, rounded, HudColor, 8f);
        Border(clock, 8f, LineColor);
        TextMeshProUGUI phaseText = Text(Place(Node("Fase", clock), 0f, 9f, 190f, 16f), displayFont, 16f, Accent, TextAlignmentOptions.Midline, 16f, true);
        phaseText.text = "Fase de compra";
        hudTime = Text(Place(Node("Tiempo", clock), 0f, 24f, 190f, 48f), displayFont, 46f, Ink, TextAlignmentOptions.Midline);
        hudRound = Text(Place(Node("Ronda", clock), 0f, 72f, 190f, 14f), labelFont, 14f, Mute, TextAlignmentOptions.Midline, 14f, true);
        hudClock = clock.gameObject;

        RectTransform hint = Place(Node("AvisoTienda", hud), 0f, 150f, 10f, 48f);
        Image(hint, rounded, HudColor, 24f);
        Border(hint, 24f, Over(Rgb(242, 154, 56, 0.6f), HudBase));
        Key(hint, 19f, 9f, "B", true, false);
        TextMeshProUGUI hintText = Text(Place(Node("Texto", hint), 59f, 0f, 200f, 48f), labelFont, 20f, Ink, TextAlignmentOptions.MidlineLeft, 10f, true);
        hintText.text = "Tienda";
        float hintWidth = 59f + Width(hintText, "TIENDA") + 19f;
        hint.sizeDelta = new Vector2(hintWidth, 48f);
        hint.anchoredPosition = new Vector2(960f - hintWidth / 2f, -150f);
        hudHint = hint.gameObject;

        hudMoney = Text(Place(Node("Plata", hud), 34f, 866f, 600f, 40f), displayFont, 34f, Ink);
        Material shadow = hudMoney.fontMaterial;
        shadow.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        shadow.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.6f));
        shadow.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
        shadow.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.6f);
    }

    private void BuildTopBar()
    {
        topBar = Node("BarraSuperior", shopRoot);
        topBar.anchorMin = new Vector2(0f, 1f);
        topBar.anchorMax = Vector2.one;
        topBar.pivot = new Vector2(0.5f, 1f);
        topBar.anchoredPosition = Vector2.zero;
        topBar.sizeDelta = new Vector2(0f, 92f);
        Image(topBar, null, BarColor, 0f, true);
        RectTransform lineRect = Node("Linea", topBar);
        lineRect.anchorMin = Vector2.zero;
        lineRect.anchorMax = new Vector2(1f, 0f);
        lineRect.pivot = new Vector2(0.5f, 0f);
        lineRect.anchoredPosition = Vector2.zero;
        lineRect.sizeDelta = new Vector2(0f, 1f);
        Image(lineRect, null, LineColor);

        RectTransform content = Node("Contenido", topBar);
        content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(1920f, 92f);

        Key(content, 56f, 31f, "B", true, false);
        TextMeshProUGUI title = Text(Place(Node("Titulo", content), 102f, 18f, 500f, 40f), displayFont, 40f, Ink, TextAlignmentOptions.MidlineLeft, 10f, true);
        title.text = "Tienda";
        barSub = Text(Place(Node("Modo", content), 102f, 58f, 500f, 16f), labelFont, 16f, Mute, TextAlignmentOptions.MidlineLeft, 14f, true);

        barPhase = Text(Place(Node("Fase", content), 750f, 12f, 420f, 17f), displayFont, 16f, Accent, TextAlignmentOptions.Midline, 18f, true);
        barTime = Text(Place(Node("Tiempo", content), 750f, 28f, 420f, 42f), displayFont, 38f, Ink, TextAlignmentOptions.Midline);
        RectTransform track = Place(Node("Progreso", content), 750f, 74f, 420f, 5f);
        Image(track, rounded, Over(White(0.12f), Rgb(8, 10, 14)), 2.5f);
        barProgress = Place(Node("Relleno", track), 0f, 0f, 420f, 5f);
        Image(barProgress, rounded, Accent, 2.5f);

        barMoney = Text(Place(Node("Plata", content), 1364f, 11f, 500f, 50f), displayFont, 50f, Ink, TextAlignmentOptions.MidlineRight, 2f);
        barNext = Text(Place(Node("ProximaRonda", content), 1364f, 62f, 500f, 19f), labelFont, 16f, Mute, TextAlignmentOptions.MidlineRight, 8f, true);
    }

    private void BuildBanner()
    {
        RectTransform rect = Place(Node("Aviso", stage), 56f, 100f, 1808f, 46f);
        Image(rect, rounded, WithAlpha(Over(Rgb(255, 92, 92, 0.14f), Rgb(20, 23, 28)), DarkAlpha(0.9f)), 8f);
        Border(rect, 8f, Over(Rgb(255, 92, 92, 0.55f), Rgb(45, 30, 35)));
        bannerText = Text(Place(Node("Texto", rect), 17f, 0f, 1774f, 46f), labelFont, 20f, Hex(0xFFD0D0), TextAlignmentOptions.MidlineLeft, 6f, true);
        banner = rect.gameObject;
    }

    private TextMeshProUGUI ColumnHeader(RectTransform panel, float width, string hint, Sprite hintIcon = null)
    {
        TextMeshProUGUI title = Text(Place(Node("Titulo", panel), 21f, 16f, width - 200f, 26f), displayFont, 24f, Ink, TextAlignmentOptions.MidlineLeft, 12f, true);
        TextMeshProUGUI right = Text(Place(Node("Ayuda", panel), width - 221f, 22f, 200f, 18f), labelFont, 14f, Mute, TextAlignmentOptions.MidlineRight, 10f, true);
        right.text = hint;
        if (hintIcon != null)
        {
            float x = width - 21f - Width(right, hint.ToUpperInvariant()) - 6f - 14f;
            Glyph(Place(Node("Icono", panel), x, 22f, 14f, 18f), hintIcon, 14f, Mute);
        }
        Line(panel, 0f, HeadH - 1f, width);
        return title;
    }

    private void BuildCategoryPanel()
    {
        catsPanel = Panel("Categorias", CatsX, PanelTop, CatsW, HeadH + LegendH, out catsMask);
        ColumnHeader(catsPanel, CatsW, "Número o clic").text = "Categoría";
        catRowsRoot = Place(Node("Filas", catsPanel), 0f, HeadH, CatsW, 10f);
        legendRoot = Place(Node("Atajos", catsPanel), 0f, HeadH, CatsW, LegendH);
        Line(legendRoot, 0f, 0f, CatsW);
        Border(catsPanel, 10f, LineColor);

        // Atajos en renglones que se acomodan solos, como el ".legend" de la maqueta.
        (string key, string label)[] shortcuts = { ("B", "Cerrar"), ("1–9", "Elegir"), ("↵", "Comprar"), ("⌫", "Volver"), (null, "Clic der. vender") };
        float x = 19f, y = 15f;
        foreach ((string key, string label) in shortcuts)
        {
            RectTransform item = Place(Node("Atajo", legendRoot), x, y, 10f, 24f);
            float width = 0f;
            if (key != null) width = Key(item, 0f, 0f, key, false, true).width + 6f;
            TextMeshProUGUI text = Text(Place(Node("Texto", item), width, 0f, 200f, 24f), labelFont, 15f, Mute, TextAlignmentOptions.MidlineLeft, 6f, true);
            text.text = label;
            width += Width(text, label.ToUpperInvariant());
            if (x > 19f && x + width > CatsW - 19f)
            {
                x = 19f;
                y += 34f;
                item.anchoredPosition = new Vector2(x, -y);
            }
            item.sizeDelta = new Vector2(width, 24f);
            x += width + 16f;
        }
    }

    private void BuildCategoryRows()
    {
        Clear(catRowsRoot);
        categoryRows.Clear();
        for (int i = 0; i < categories.Count; i++)
        {
            int index = i;
            ShopCategory value = categories[i];
            Row row = NewRow(catRowsRoot, i * CatRowH, CatRowH, CatsW, i < categories.Count - 1);
            row.keyText.text = (i + 1).ToString();

            List<ShopItem> items = catalog.ItemsIn(value);
            if (value == ShopCategory.Shields)
            {
                row.icon.sprite = shieldSprite;
                row.icon.color = ShieldColor;
                Place(row.icon.rectTransform, 57f, 19f, 84f, 28f);
            }
            else
            {
                row.icon.sprite = items.Count > 0 ? items[0].icon : null;
                row.icon.color = new Color(1f, 1f, 1f, FadeAlpha(0.9f));
                Place(row.icon.rectTransform, 57f, 16f, 84f, 34f);
            }

            row.nameText = Text(Place(Node("Nombre", row.root), 155f, 0f, 240f, CatRowH), displayFont, 23f, Ink, TextAlignmentOptions.MidlineLeft, 7f, true);
            row.nameText.text = CategoryName(value);
            row.rightText = Text(Place(Node("Precio", row.root), 300f, 0f, 60f, CatRowH), labelFont, 15f, Mute, TextAlignmentOptions.MidlineLeft, 6f);
            row.chevron = Text(Place(Node("Flecha", row.root), 375f, 0f, 12f, CatRowH), displayFont, 24f, Mute, TextAlignmentOptions.MidlineLeft);
            row.chevron.text = "›";

            row.pointer.Hovered = () => { row.hovered = true; if (open) Render(); };
            row.pointer.Exited = () => { row.hovered = false; if (open) Render(); };
            row.pointer.Clicked = () => OpenCategory(categories[index]);
            categoryRows.Add(row);
        }

        float rowsHeight = categories.Count * CatRowH;
        catRowsRoot.sizeDelta = new Vector2(CatsW, rowsHeight);
        legendRoot.anchoredPosition = new Vector2(0f, -(HeadH + rowsHeight));
        catsPanel.sizeDelta = new Vector2(CatsW, HeadH + rowsHeight + LegendH);
    }

    private Row NewRow(RectTransform parent, float y, float height, float width, bool divider)
    {
        Row row = new Row();
        row.root = Place(Node("Fila", parent), 0f, y, width, height);
        row.background = Image(row.root, null, Color.clear, 0f, true);
        row.pointer = row.root.gameObject.AddComponent<ShopPointerTarget>();
        if (divider) Line(row.root, 0f, height - 1f, width);
        row.selectionBar = Image(Place(Node("Seleccion", row.root), 0f, 0f, 4f, height), null, Accent);

        var key = Key(row.root, 19f, (height - 24f) / 2f, "0", false, true);
        row.keyChip = key.chip;
        row.keyText = key.text;
        row.icon = Image(Place(Node("Icono", row.root), 57f, 18f, 132f, 56f), null, Color.white);
        row.icon.preserveAspect = true;
        return row;
    }

    private void BuildListPanel()
    {
        listPanel = Panel("Lista", ListX, PanelTop, ListW, HeadH, out listMask);
        listTitle = ColumnHeader(listPanel, ListW, "Volver", backIcon);
        listRowsRoot = Place(Node("Filas", listPanel), 0f, HeadH, ListW, 10f);
        Border(listPanel, 10f, LineColor);
    }

    private void BuildItemRows()
    {
        Clear(listRowsRoot);
        itemRows.Clear();
        listPanel.gameObject.SetActive(listItems.Count > 0);
        for (int i = 0; i < listItems.Count; i++)
        {
            ShopItem item = listItems[i];
            Row row = NewRow(listRowsRoot, i * ItemRowH, ItemRowH, ListW, i < listItems.Count - 1);
            row.keyText.text = (i + 1).ToString();

            // Las granadas llevan un brillo de su color detrás; escudos y granadas se centran en la caja del ícono.
            float size = IconSize(item);
            if (item.kind == ShopItemKind.Grenade && glow != null)
            {
                UnityEngine.UI.Image halo = Image(Place(Node("Brillo", row.root), 57f + 66f - size * 0.75f, 46f - size * 0.75f, size * 1.5f, size * 1.5f), glow, WithAlpha(item.tint, FadeAlpha(0.55f)));
                halo.transform.SetSiblingIndex(row.icon.transform.GetSiblingIndex());
            }
            row.icon.sprite = item.icon;
            if (!item.IsWeapon)
            {
                float width = size * IconAspect(item);
                Place(row.icon.rectTransform, 57f + (132f - width) / 2f, 46f - size / 2f, width, size);
            }

            row.nameText = Text(Place(Node("Nombre", row.root), 203f, 24f, 160f, 23f), displayFont, 22f, Ink, TextAlignmentOptions.MidlineLeft, 7f, true);
            // Sin recorte: la caja mide justo lo que ocupan las letras y TMP escondería la línea (su alto de renglón es mayor).
            row.nameText.textWrappingMode = TextWrappingModes.Normal;
            row.nameText.maxVisibleLines = 2;
            row.nameText.lineSpacing = -15f;
            row.subText = Text(Place(Node("Detalle", row.root), 203f, 50f, 160f, 17f), labelFont, 14f, Mute, TextAlignmentOptions.MidlineLeft, 10f, true);
            row.subText.overflowMode = TextOverflowModes.Ellipsis;
            row.rightText = Text(Place(Node("Precio", row.root), 377f, 0f, 84f, ItemRowH), displayFont, 26f, Ink, TextAlignmentOptions.MidlineRight);
            row.badge = Image(Place(Node("Estado", row.root), 380f, 34f, 80f, 23f), rounded, Color.clear, 4f);
            row.badgeText = Text(Stretch(Node("Texto", row.badge.rectTransform)), displayFont, 13f, Ok, TextAlignmentOptions.Midline, 12f, true);

            row.pointer.Hovered = () => Select(item);
            row.pointer.Clicked = () => { Select(item); TryBuy(item); };
            row.pointer.RightClicked = () => { Select(item); TrySell(item); };
            itemRows.Add(row);
        }

        float rowsHeight = listItems.Count * ItemRowH;
        listRowsRoot.sizeDelta = new Vector2(ListW, rowsHeight);
        listPanel.sizeDelta = new Vector2(ListW, HeadH + rowsHeight);
    }

    private void BuildDetailPanel()
    {
        detailPanel = Panel("Ficha", DetailX, PanelTop, DetailW, DetailH, out detailMask);
        detailGroup = detailPanel.gameObject.AddComponent<CanvasGroup>();

        detailName = Text(Place(Node("Nombre", detailPanel), Pad, 20f, 380f, 46f), displayFont, 46f, Ink, TextAlignmentOptions.MidlineLeft, 5f, true);
        detailAlias = Text(Place(Node("Alias", detailPanel), Pad, 66f, 380f, 21f), labelFont, 16f, Accent, TextAlignmentOptions.MidlineLeft, 16f, true);
        detailMeta = Text(Place(Node("Tipo", detailPanel), Pad, 91f, 380f, 19.5f), labelFont, 15f, Mute, TextAlignmentOptions.MidlineLeft, 10f, true);
        detailPrice = Text(Place(Node("Precio", detailPanel), 338f, 20f, 200f, 40f), displayFont, 40f, Ink, TextAlignmentOptions.MidlineRight);

        detailPicture = Place(Node("Imagen", detailPanel), Pad, 104f, ContentW, 132f);
        detailGlow = Image(Place(Node("Brillo", detailPicture), 0f, 0f, ContentW, 132f), glow, WithAlpha(Accent, FadeAlpha(0.12f)));
        detailIcon = Image(Place(Node("Icono", detailPicture), 43f, 4f, 430f, 124f), null, Color.white);
        detailIcon.preserveAspect = true;

        detailRole = Text(Place(Node("Rol", detailPanel), Pad, 225f, ContentW, 46f), bodyFont, 17f, Soft, TextAlignmentOptions.TopLeft);
        detailRole.textWrappingMode = TextWrappingModes.Normal;
        detailRole.lineSpacing = 15f;

        detailBody = Stretch(Node("Contenido", detailPanel));

        RectTransform button = Place(Node("Comprar", detailPanel), Pad, ButtonY, ContentW, ButtonH);
        buyButton = Image(button, rounded, Accent, 8f, true);
        buyLabel = Text(Place(Node("Texto", button), 20f, 0f, 340f, ButtonH), displayFont, 24f, AccentInk, TextAlignmentOptions.MidlineLeft, 12f, true);
        buySub = Text(Place(Node("Ayuda", button), 256f, 0f, 240f, ButtonH), labelFont, 14f, AccentInk, TextAlignmentOptions.MidlineRight, 8f, true);
        ShopPointerTarget pointer = AddPointer(button);
        pointer.Clicked = () => { if (selected != null) TryBuy(selected); };
        pointer.RightClicked = () => { if (selected != null) TrySell(selected); };

        Border(detailPanel, 10f, LineColor);
    }

    private void BuildLoadoutBar()
    {
        loadoutBar = Place(Node("Equipamiento", stage), LoadoutX, LoadoutY, LoadoutW, LoadoutH);
        Image(loadoutBar, rounded, PanelColor, 10f, true);

        float[] weights = { 1f, 1f, 0.7f, 1f, 1f, 1.25f };
        float unit = (LoadoutW - 2f) / 5.95f, x = 1f;
        loadoutCells = new RectTransform[weights.Length];
        loadoutWidths = new float[weights.Length];
        for (int i = 0; i < weights.Length; i++)
        {
            loadoutWidths[i] = unit * weights[i];
            loadoutCells[i] = Place(Node("Espacio", loadoutBar), x, 0f, loadoutWidths[i], LoadoutH);
            x += loadoutWidths[i];
            if (i < weights.Length - 1) Line(loadoutBar, x - 1f, 1f, 1f, LoadoutH - 2f);
        }
        Border(loadoutBar, 10f, LineColor);
    }

    private void BuildToast()
    {
        toast = Place(Node("Mensaje", stage), 760f, 860f, 400f, 50f);
        toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0f;
        toastGroup.blocksRaycasts = false;
        Image(toast, rounded, Rgb(10, 12, 17, DarkAlpha(0.94f)), 8f);
        Border(toast, 8f, LineColor);
        toastText = Text(Stretch(Node("Texto", toast)), displayFont, 24f, Ink, TextAlignmentOptions.Midline, 8f, true);
    }

    // ---------- Animaciones (tiempo sin escala) ----------

    private void ShowToast(string message)
    {
        toastText.text = message;
        float width = Width(toastText, message) + 42f;
        Place(toast, 960f - width / 2f, 860f, width, 50f);
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(ToastAnimation());
    }

    private IEnumerator ToastAnimation()
    {
        float x = toast.anchoredPosition.x;
        for (float t = 0f; t < 0.18f; t += Time.unscaledDeltaTime)
        {
            float k = Ease(t / 0.18f);
            toastGroup.alpha = k;
            toast.anchoredPosition = new Vector2(x, -860f - 20f * (1f - k));
            yield return null;
        }
        toastGroup.alpha = 1f;
        toast.anchoredPosition = new Vector2(x, -860f);
        yield return new WaitForSecondsRealtime(1.5f);
        for (float t = 0f; t < 0.18f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.18f;
            toastGroup.alpha = 1f - k;
            toast.anchoredPosition = new Vector2(x, -860f - 20f * k);
            yield return null;
        }
        toastGroup.alpha = 0f;
    }

    // La barra superior baja, el equipamiento sube y el fondo se oscurece (0,16 s).
    private IEnumerator OpenAnimation()
    {
        const float duration = 0.16f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = Ease(t / duration);
            shopGroup.alpha = k;
            topBar.anchoredPosition = new Vector2(0f, 92f * (1f - k));
            loadoutBar.anchoredPosition = new Vector2(LoadoutX, -LoadoutY - 180f * (1f - k));
            yield return null;
        }
        shopGroup.alpha = 1f;
        topBar.anchoredPosition = Vector2.zero;
        loadoutBar.anchoredPosition = new Vector2(LoadoutX, -LoadoutY);
    }

    // Aparición con recorte, como el clip-path de la maqueta: de arriba hacia abajo o de izquierda a derecha.
    private void Reveal(ref Coroutine routine, UnityEngine.UI.RectMask2D mask, bool downwards, float duration, CanvasGroup fadeGroup = null)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(RevealAnimation(mask, downwards, duration, fadeGroup));
    }

    private static IEnumerator RevealAnimation(UnityEngine.UI.RectMask2D mask, bool downwards, float duration, CanvasGroup fadeGroup)
    {
        RectTransform rect = mask.rectTransform;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float hidden = 1f - Ease(t / duration);
            mask.padding = downwards ? new Vector4(0f, rect.rect.height * hidden, 0f, 0f) : new Vector4(0f, 0f, rect.rect.width * hidden, 0f);
            if (fadeGroup != null) fadeGroup.alpha = 1f - hidden;
            yield return null;
        }
        mask.padding = Vector4.zero;
        if (fadeGroup != null) fadeGroup.alpha = 1f;
    }

    private static float Ease(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);

    // Si no se puede comprar, la ficha tiembla 5 px.
    private void Shake()
    {
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeAnimation());
    }

    private IEnumerator ShakeAnimation()
    {
        const float duration = 0.22f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float offset = -Mathf.Sin(t / duration * Mathf.PI * 2f) * 5f;
            detailPanel.anchoredPosition = new Vector2(DetailX + offset, -PanelTop);
            yield return null;
        }
        detailPanel.anchoredPosition = new Vector2(DetailX, -PanelTop);
    }
}
