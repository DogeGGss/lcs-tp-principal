using TMPro;
using UnityEngine;
using static ShopUIKit;

// Un arma que quiera mostrar su reserva y la recarga en el HUD implementa esta interfaz (US 011, US 056).
public interface IHudWeapon
{
    string HudName { get; }
    int Ammo { get; }
    int MagazineSize { get; }
    int Reserve { get; }          // -1 si el arma no tiene reserva
    float ReloadProgress { get; } // -1 si no está recargando; de 0 a 1 mientras recarga
}

// HUD de combate, diseño Andén (F15): la vida a la izquierda, la habilidad en el medio y la munición a la
// derecha, abajo al centro y unidas por una vía. Se arma por código con las medidas de la maqueta
// (pantalla de referencia de 1920 x 1080) y lee todo del jugador: HealthSystem, PlayerAbility y el arma en la mano.
public class CombatHud : MonoBehaviour
{
    [Header("Tipografías")]
    [SerializeField] private TMP_FontAsset displayFont; // Barlow Condensed Bold
    [SerializeField] private TMP_FontAsset labelFont;   // Barlow Condensed SemiBold
    [SerializeField] private TMP_FontAsset monoFont;    // JetBrains Mono

    [Header("Sprites")]
    [SerializeField] private Sprite rounded;
    [SerializeField] private Sprite circle;
    [Tooltip("Aro de la habilidad: circunferencia a 60 px del centro, en un sprite de 128.")]
    [SerializeField] private Sprite ring;
    [SerializeField] private Sprite rails;
    [SerializeField] private Sprite shade;
    [SerializeField] private Sprite vignette;
    [SerializeField] private Sprite glow;

    [Tooltip("Con la vida por debajo de esta parte del máximo, se ve en rojo y se tiñen los bordes de la pantalla.")]
    [SerializeField, Range(0f, 1f)] private float criticalHealth = 0.25f;

    [Header("Marcador de impacto (US 165)")]
    [Tooltip("Canal del mixer para el sonido del marcador (SFX), así respeta el volumen de efectos.")]
    [SerializeField] private UnityEngine.Audio.AudioMixerGroup sfxGroup;

    // Mira fija (US 171): cuatro rayitas blancas con borde oscuro y un hueco en el centro (px en 1920 x 1080).
    private const float CrosshairLength = 8f, CrosshairThickness = 2f, CrosshairGap = 5f;
    private RectTransform crosshair;

    // Cuatro rayitas en la mira durante 0,15 s: blancas al acertar, amarillas a la cabeza, rojas y más grandes si mata.
    private const float HitMarkerTime = 0.15f;
    private static Color HitHeadColor => new Color(1f, 0.824f, 0.247f); // #FFD23F
    private RectTransform hitMarker;
    private readonly UnityEngine.UI.Image[] hitLines = new UnityEngine.UI.Image[4];
    private float hitAt = -10f;
    private HitMarkerKind hitKind;
    private AudioSource hitAudio;
    private AudioClip hitBodyClip, hitHeadClip, hitKillClip;

    // Medidas de la maqueta (px en 1920 x 1080).
    private const float BarW = 934f, BarH = 170f, BlockW = 240f, SkillW = 150f;
    private const float RailLeftX = 256f, SkillX = 392f, RailRightX = 558f, AmmoX = 694f;

    private HealthSystem health;
    private PlayerAbility ability;
    private WeaponSwitcher switcher;
    private MeleeWeaponHolder melee;
    private PlayerLoadout loadout;

    private CanvasGroup group;
    private GameObject crit, hint, skill;
    private TextMeshProUGUI hintText;
    private TextMeshProUGUI hpText, shieldText;
    private UnityEngine.UI.Image hpFill, shieldFill;
    private RectTransform markHealth, markHalfShield;
    private UnityEngine.UI.Image ringFill, icon, keyChip;
    private readonly UnityEngine.UI.Image[] ticks = new UnityEngine.UI.Image[12];
    private TextMeshProUGUI cooldownText, keyText, nameText;
    private TextMeshProUGUI weaponText, magText, reserveText;
    private RectTransform sleepersRoot;
    private UnityEngine.UI.Image[] sleepers = new UnityEngine.UI.Image[0];
    private float usedAt = -10f;

    // Lo último que se dibujó, para tocar la interfaz solo cuando algo cambia.
    private int lastHp = int.MinValue, lastShield, lastMaxHp, lastMaxShield;
    private int lastSkillState = int.MinValue;
    private string lastAmmoState;

    private void Awake()
    {
        Build();
        BuildHitSounds();
    }

    private void Start()
    {
        FindPlayer();
    }

    private void OnEnable()
    {
        WeaponFire.Hit += OnHit;
    }

    private void OnDisable()
    {
        WeaponFire.Hit -= OnHit;
    }

    private void OnDestroy()
    {
        if (ability != null) ability.Used -= OnAbilityUsed;
    }

    private void Update()
    {
        if (health == null)
        {
            FindPlayer();
            if (health == null) { group.alpha = 0f; return; }
        }

        // Con la tienda abierta, su barra de equipo ocupa este lugar.
        group.alpha = ShopUI.IsOpen ? 0f : 1f;
        UpdateHealth();
        UpdateSkill();
        UpdateAmmo();
        UpdateHitMarker();

        // La mira se ve con cualquier arma; con la pausa abierta se oculta (con la tienda ya se oculta todo el HUD).
        crosshair.gameObject.SetActive(!PauseMenu.IsPaused);
    }

    // ---------- Mira ----------

    private void BuildCrosshair(RectTransform root)
    {
        crosshair = Node("Mira", root);
        crosshair.anchorMin = crosshair.anchorMax = crosshair.pivot = new Vector2(0.5f, 0.5f);
        crosshair.anchoredPosition = Vector2.zero;
        crosshair.sizeDelta = Vector2.one * 2f * (CrosshairGap + CrosshairLength);

        float offset = CrosshairGap + CrosshairLength * 0.5f;
        CrosshairLine("Arriba", new Vector2(0f, offset), new Vector2(CrosshairThickness, CrosshairLength));
        CrosshairLine("Abajo", new Vector2(0f, -offset), new Vector2(CrosshairThickness, CrosshairLength));
        CrosshairLine("Izquierda", new Vector2(-offset, 0f), new Vector2(CrosshairLength, CrosshairThickness));
        CrosshairLine("Derecha", new Vector2(offset, 0f), new Vector2(CrosshairLength, CrosshairThickness));
    }

    private void CrosshairLine(string name, Vector2 position, Vector2 size)
    {
        RectTransform line = Node(name, crosshair);
        line.anchorMin = line.anchorMax = line.pivot = new Vector2(0.5f, 0.5f);
        line.anchoredPosition = position;
        line.sizeDelta = size;
        Image(line, null, Color.white);
        // Borde oscuro de 1 px para que se lea sobre el cielo y sobre paredes oscuras.
        UnityEngine.UI.Outline border = line.gameObject.AddComponent<UnityEngine.UI.Outline>();
        border.effectColor = new Color(0f, 0f, 0f, 0.85f);
        border.effectDistance = new Vector2(1f, 1f);
    }

    // ---------- Marcador de impacto ----------

    private void OnHit(HitMarkerKind kind)
    {
        hitKind = kind;
        hitAt = Time.unscaledTime;
        AudioClip clip = kind == HitMarkerKind.Kill ? hitKillClip : kind == HitMarkerKind.Head ? hitHeadClip : hitBodyClip;
        if (hitAudio != null && clip != null) hitAudio.PlayOneShot(clip);
    }

    private void UpdateHitMarker()
    {
        float t = (Time.unscaledTime - hitAt) / HitMarkerTime;
        bool visible = t >= 0f && t < 1f;
        hitMarker.gameObject.SetActive(visible);
        if (!visible) return;

        Color color = hitKind == HitMarkerKind.Kill ? Bad : hitKind == HitMarkerKind.Head ? HitHeadColor : Color.white;
        color.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
        foreach (UnityEngine.UI.Image line in hitLines) line.color = color;
        hitMarker.localScale = Vector3.one * (hitKind == HitMarkerKind.Kill ? 1.35f : 1f);
    }

    private void BuildHitMarker(RectTransform root)
    {
        hitMarker = Node("MarcadorImpacto", root);
        hitMarker.anchorMin = hitMarker.anchorMax = hitMarker.pivot = new Vector2(0.5f, 0.5f);
        hitMarker.anchoredPosition = Vector2.zero;
        hitMarker.sizeDelta = new Vector2(40f, 40f);
        for (int i = 0; i < 4; i++)
        {
            // Una rayita en cada diagonal, a 10 px del centro, apuntando hacia afuera.
            float angle = 45f + 90f * i;
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            RectTransform line = Node("Raya", hitMarker);
            line.anchorMin = line.anchorMax = line.pivot = new Vector2(0.5f, 0.5f);
            line.anchoredPosition = dir * 13f;
            line.sizeDelta = new Vector2(3f, 11f);
            line.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
            hitLines[i] = Image(line, null, Color.white);
            line.gameObject.AddComponent<UnityEngine.UI.Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);
        }
        hitMarker.gameObject.SetActive(false);
    }

    // Sonidos cortos generados, para no depender de archivos: un clic al cuerpo, un "ding" a la cabeza
    // y un golpe más grave con "ding" al matar.
    private void BuildHitSounds()
    {
        hitAudio = gameObject.AddComponent<AudioSource>();
        hitAudio.playOnAwake = false;
        hitAudio.spatialBlend = 0f;
        hitAudio.volume = 0.35f;
        hitAudio.outputAudioMixerGroup = sfxGroup;
        hitBodyClip = Tone("ImpactoCuerpo", 0.05f, 1900f, 0f, 60f);
        hitHeadClip = Tone("ImpactoCabeza", 0.10f, 2600f, 3900f, 28f);
        hitKillClip = Tone("ImpactoBaja", 0.16f, 900f, 2600f, 18f);
    }

    private static AudioClip Tone(string name, float seconds, float freqA, float freqB, float decay)
    {
        const int rate = 44100;
        int samples = Mathf.CeilToInt(seconds * rate);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float time = i / (float)rate;
            float envelope = Mathf.Exp(-decay * time) * Mathf.Clamp01(time * 400f);
            float wave = Mathf.Sin(2f * Mathf.PI * freqA * time);
            if (freqB > 0f) wave = wave * 0.6f + Mathf.Sin(2f * Mathf.PI * freqB * time) * 0.4f;
            data[i] = wave * envelope * 0.8f;
        }
        AudioClip clip = AudioClip.Create(name, samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // ---------- Jugador ----------

    private void FindPlayer()
    {
        PlayerMovement player = FindAnyObjectByType<PlayerMovement>();
        if (player == null) return;

        health = player.GetComponent<HealthSystem>();
        switcher = player.GetComponentInChildren<WeaponSwitcher>(true);
        melee = player.GetComponent<MeleeWeaponHolder>();
        loadout = player.GetComponent<PlayerLoadout>();
        ability = player.GetComponent<PlayerAbility>();
        if (ability != null) ability.Used += OnAbilityUsed;

        bool hasAbility = ability != null && ability.Ability != null;
        skill.SetActive(hasAbility);
        if (hasAbility) SetupSkill(ability.Ability);
    }

    private void OnAbilityUsed()
    {
        usedAt = Time.time;
    }

    // ---------- Vida y escudo: una sola vía con la vida efectiva ----------

    private void UpdateHealth()
    {
        int hp = Mathf.Max(0, health.currentHealth), shield = Mathf.Max(0, health.currentShield);
        int maxHp = Mathf.Max(1, health.maxHealth), maxShield = Mathf.Max(0, health.maxShield);
        if (hp == lastHp && shield == lastShield && maxHp == lastMaxHp && maxShield == lastMaxShield) return;
        lastHp = hp; lastShield = shield; lastMaxHp = maxHp; lastMaxShield = maxShield;

        bool low = hp <= maxHp * criticalHealth;
        hpText.text = hp.ToString();
        hpText.color = low ? Bad : Ink;
        shieldText.text = shield > 0 ? $"+{shield}" : "";
        shieldText.rectTransform.anchoredPosition = new Vector2(Width(hpText, hpText.text) + 8f, shieldText.rectTransform.anchoredPosition.y);

        float total = maxHp + maxShield, hpW = BlockW * hp / total, shieldW = BlockW * shield / total;
        Place(hpFill.rectTransform, 0f, 0f, hpW, 8f);
        hpFill.color = low ? Bad : Ink;
        Place(shieldFill.rectTransform, hpW, 0f, shieldW, 8f);
        markHealth.anchoredPosition = new Vector2(BlockW * maxHp / total - 1f, 4f);
        markHalfShield.gameObject.SetActive(maxShield > 0);
        markHalfShield.anchoredPosition = new Vector2(BlockW * (maxHp + maxShield / 2f) / total - 1f, 4f);
        crit.SetActive(low);
    }

    // ---------- La habilidad: un reloj de andén que se enciende mientras recarga ----------

    private void SetupSkill(AbilityData data)
    {
        icon.sprite = data.icon;
        icon.enabled = data.icon != null;
        keyText.text = data.key.ToString();
        nameText.text = data.displayName;
        float nameW = Width(nameText, data.displayName.ToUpperInvariant());
        float x = (SkillW - (24f + 8f + nameW)) / 2f;
        keyChip.rectTransform.anchoredPosition = new Vector2(x, 0f);
        nameText.rectTransform.anchoredPosition = new Vector2(x + 32f, 0f);
    }

    private void UpdateSkill()
    {
        if (ability == null || ability.Ability == null) return;

        float progress = ability.Progress;
        bool ready = ability.IsReady, used = Time.time - usedAt < 0.4f;
        int lit = Mathf.FloorToInt(progress * 12f + 0.0001f);
        int seconds = ready ? 0 : Mathf.CeilToInt(ability.CooldownLeft);
        ringFill.fillAmount = progress;

        int state = lit + seconds * 16 + (ready ? 1 << 20 : 0) + (used ? 1 << 21 : 0);
        if (state == lastSkillState) return;
        lastSkillState = state;

        Color line = used ? Accent : Ink, dim = White(FadeAlpha(0.22f));
        ringFill.color = line;
        for (int i = 0; i < ticks.Length; i++) ticks[i].color = i < lit ? line : dim;
        icon.color = used ? Accent : ready ? Ink : dim;
        cooldownText.text = seconds > 0 ? seconds.ToString() : "";
        keyChip.color = ready ? Ink : White(FadeAlpha(0.14f));
        keyText.color = ready ? KeyInk : Ink;
        nameText.color = ready ? Ink : Mute;
    }

    // ---------- Munición: un durmiente por bala ----------

    private void UpdateAmmo()
    {
        ReadWeapon(out string name, out int ammo, out int size, out int reserve, out float reload);
        bool reloading = reload >= 0f;
        int filled = reloading ? Mathf.RoundToInt(reload * size) : ammo;
        string state = $"{name}|{ammo}|{size}|{reserve}|{(reloading ? filled : -1)}";
        if (state == lastAmmoState) return;
        lastAmmoState = state;

        bool hasAmmo = size > 0, low = hasAmmo && !reloading && ammo <= size / 4;
        weaponText.text = reloading ? "Recargando" : name;
        weaponText.color = reloading ? Accent : Mute;
        magText.text = hasAmmo ? ammo.ToString() : "—";
        magText.color = low ? Bad : Ink;
        reserveText.text = hasAmmo && reserve >= 0 ? $"/ {reserve}" : "";
        float reserveW = reserveText.text.Length > 0 ? Width(reserveText, reserveText.text) + 8f : 0f;
        Place(magText.rectTransform, 0f, 88f, BlockW - reserveW, 64f);

        int count = hasAmmo ? Mathf.Min(size, 40) : 0;
        if (count != sleepers.Length) BuildSleepers(count);
        Color on = reloading ? Accent : low ? Bad : Ink, off = White(FadeAlpha(0.16f));
        int shown = count == size ? filled : Mathf.RoundToInt(filled * (float)count / Mathf.Max(1, size));
        for (int i = 0; i < sleepers.Length; i++) sleepers[i].color = i < shown ? on : off;

        bool empty = hasAmmo && ammo == 0 && !reloading;
        hint.SetActive(empty);
        if (empty) hintText.text = reserve >= 0 ? "R · Recargar" : "Sin balas";
    }

    // El arma en la mano: el arma principal o la secundaria (las dos implementan IHudWeapon); si no, el cuchillo.
    private void ReadWeapon(out string name, out int ammo, out int size, out int reserve, out float reload)
    {
        name = ""; ammo = 0; size = 0; reserve = -1; reload = -1f;

        GameObject held = null;
        if (switcher != null)
        {
            if (switcher.mitreObj != null && switcher.mitreObj.activeInHierarchy) held = switcher.mitreObj;
            else if (switcher.pistolObj != null && switcher.pistolObj.activeInHierarchy) held = switcher.pistolObj;
        }
        IHudWeapon weapon = held != null ? held.GetComponentInChildren<IHudWeapon>() : null;
        if (weapon != null)
        {
            // La secundaria se muestra con su nombre de la tienda (Línea A o Línea H).
            name = held == switcher.pistolObj ? SecondaryName() : weapon.HudName;
            ammo = weapon.Ammo; size = weapon.MagazineSize; reserve = weapon.Reserve; reload = weapon.ReloadProgress;
            return;
        }
        if (melee != null && melee.CurrentViewModel != null && melee.CurrentViewModel.activeInHierarchy)
            name = melee.CurrentWeapon != null ? melee.CurrentWeapon.weaponName : "Cuchillo";
    }

    // La pistola de hoy es el arma secundaria: se muestra con su nombre de la tienda (Línea A), como en Valorant.
    private string SecondaryName()
    {
        ShopItem secondary = loadout != null ? loadout.Secondary : null;
        if (secondary == null) return "Pistola";
        return string.IsNullOrEmpty(secondary.alias) ? secondary.displayName : secondary.alias;
    }

    // ---------- Construcción ----------

    private void Build()
    {
        GameObject canvasObject = new GameObject("HUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        canvasObject.layer = 5;
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40; // debajo de la tienda (50)
        canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
        UnityEngine.UI.CanvasScaler scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        RectTransform root = (RectTransform)canvasObject.transform;
        group = canvasObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        crit = Image(Stretch(Node("VidaCritica", root)), vignette, new Color(1f, 0.24f, 0.24f, 0.6f)).gameObject;
        crit.SetActive(false);

        RectTransform shadeRect = Node("Sombra", root);
        shadeRect.anchorMin = Vector2.zero;
        shadeRect.anchorMax = new Vector2(1f, 0f);
        shadeRect.pivot = new Vector2(0.5f, 0f);
        shadeRect.anchoredPosition = Vector2.zero;
        shadeRect.sizeDelta = new Vector2(0f, 240f);
        Image(shadeRect, shade, Color.black);

        RectTransform hintRect = Node("SinBalas", root);
        hintRect.anchorMin = hintRect.anchorMax = hintRect.pivot = new Vector2(0.5f, 0.5f);
        hintRect.anchoredPosition = new Vector2(0f, -50f);
        hintRect.sizeDelta = new Vector2(400f, 24f);
        hintText = Text(hintRect, labelFont, 18f, Bad, TextAlignmentOptions.Midline, 14f, true);
        AddShadow(hintText);
        hint = hintRect.gameObject;
        hint.SetActive(false);

        RectTransform bar = Node("Anden", root);
        bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0.5f, 0f);
        bar.anchoredPosition = new Vector2(0f, 26f);
        bar.sizeDelta = new Vector2(BarW, BarH);

        BuildHealth(bar);
        Image(Place(Node("Rieles", bar), RailLeftX, 160f, 120f, 12f), rails, Color.white);
        BuildSkill(bar);
        Image(Place(Node("Rieles", bar), RailRightX, 160f, 120f, 12f), rails, Color.white);
        BuildAmmo(bar);
        BuildCrosshair(root);
        BuildHitMarker(root);
    }

    private void BuildHealth(RectTransform bar)
    {
        RectTransform block = Place(Node("Vida", bar), 0f, 0f, BlockW, BarH);
        hpText = Text(Place(Node("Numero", block), 0f, 88f, 170f, 64f), displayFont, 72f, Ink);
        AddShadow(hpText);
        shieldText = Text(Place(Node("Escudo", block), 100f, 126f, 140f, 21f), labelFont, 24f, ShieldColor);
        AddShadow(shieldText);

        RectTransform track = Place(Node("ViaDeVida", block), 0f, 162f, BlockW, 8f);
        Image(track, null, White(FadeAlpha(0.14f)));
        hpFill = Image(Place(Node("VidaRelleno", track), 0f, 0f, 0f, 8f), null, Ink);
        shieldFill = Image(Place(Node("EscudoRelleno", track), 0f, 0f, 0f, 8f), null, ShieldColor);
        markHealth = Place(Node("MarcaVida", track), 0f, -4f, 2f, 16f);
        Image(markHealth, null, Rgb(10, 12, 17, 0.9f));
        markHalfShield = Place(Node("MarcaEscudo", track), 0f, -4f, 2f, 16f);
        Image(markHalfShield, null, Rgb(10, 12, 17, 0.9f));
    }

    private void BuildSkill(RectTransform bar)
    {
        RectTransform block = Place(Node("Habilidad", bar), SkillX, 0f, SkillW, BarH);
        skill = block.gameObject;

        // El aro mide 112 px: 12 marcas alrededor, el recorrido y el relleno de la recarga.
        RectTransform ringRect = Place(Node("Aro", block), 19f, 11f, 112f, 112f);
        Vector2 center = new Vector2(56f, 56f);
        for (int i = 0; i < ticks.Length; i++)
        {
            float angle = i * Mathf.PI / 6f;
            Vector2 direction = new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle));
            ticks[i] = Segment(ringRect, center + direction * 51.5f, center + direction * 44.8f, 2.7f, White(FadeAlpha(0.22f)));
        }
        const float radius = 37f;
        float disc = 2f * (radius - 1.7f);
        Image(Place(Node("Fondo", ringRect), 56f - disc / 2f, 56f - disc / 2f, disc, disc), circle, Rgb(8, 10, 14, DarkAlpha(0.35f)));
        float ringSize = 2f * radius * 64f / 60f;
        Image(Place(Node("Recorrido", ringRect), 56f - ringSize / 2f, 56f - ringSize / 2f, ringSize, ringSize), ring, White(FadeAlpha(0.14f)));
        ringFill = Image(Place(Node("Recarga", ringRect), 56f - ringSize / 2f, 56f - ringSize / 2f, ringSize, ringSize), ring, Ink);
        ringFill.type = UnityEngine.UI.Image.Type.Filled;
        ringFill.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
        ringFill.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
        ringFill.fillClockwise = true;
        icon = Image(Place(Node("Icono", ringRect), 33f, 33f, 46f, 46f), null, Ink);
        icon.preserveAspect = true;
        cooldownText = Text(Stretch(Node("Segundos", ringRect)), displayFont, 34f, Color.white, TextAlignmentOptions.Midline);
        AddShadow(cooldownText);

        RectTransform keyRow = Place(Node("Tecla", block), 0f, 133f, SkillW, 24f);
        keyChip = Image(Place(Node("Chip", keyRow), 0f, 0f, 24f, 24f), rounded, Ink, 5f);
        keyText = Text(Stretch(Node("Letra", keyChip.rectTransform)), monoFont, 14f, KeyInk, TextAlignmentOptions.Midline);
        keyText.fontStyle = FontStyles.Bold;
        nameText = Text(Place(Node("Nombre", keyRow), 32f, 0f, 200f, 24f), labelFont, 15f, Ink, TextAlignmentOptions.MidlineLeft, 14f, true);

        // La línea de seguridad del borde del andén.
        if (glow != null) Image(Place(Node("Brillo", block), -20f, 157f, SkillW + 40f, 23f), glow, WithAlpha(Accent, FadeAlpha(0.5f)));
        Image(Place(Node("LineaDelAnden", block), 0f, 167f, SkillW, 3f), null, Accent);
    }

    private void BuildAmmo(RectTransform bar)
    {
        RectTransform block = Place(Node("Municion", bar), AmmoX, 0f, BlockW, BarH);
        weaponText = Text(Place(Node("Arma", block), 0f, 71f, BlockW, 14f), labelFont, 13f, Mute, TextAlignmentOptions.MidlineRight, 18f, true);
        magText = Text(Place(Node("Cargador", block), 0f, 88f, BlockW, 64f), displayFont, 72f, Ink, TextAlignmentOptions.MidlineRight);
        AddShadow(magText);
        reserveText = Text(Place(Node("Reserva", block), 0f, 126f, BlockW, 21f), labelFont, 24f, Mute, TextAlignmentOptions.MidlineRight);
        AddShadow(reserveText);
        sleepersRoot = Place(Node("Durmientes", block), 0f, 160f, BlockW, 10f);
    }

    private void BuildSleepers(int count)
    {
        for (int i = sleepersRoot.childCount - 1; i >= 0; i--) Destroy(sleepersRoot.GetChild(i).gameObject);
        sleepers = new UnityEngine.UI.Image[count];
        float x = BlockW - (count * 6f - 3f);
        for (int i = 0; i < count; i++)
            sleepers[i] = Image(Place(Node("Durmiente", sleepersRoot), x + i * 6f, 0f, 3f, 10f), null, Ink);
    }

    // Sombra suave debajo de los números, para leerlos sobre el cielo o una pared clara.
    private static void AddShadow(TextMeshProUGUI text)
    {
        Material material = text.fontMaterial;
        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.55f));
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.5f);
        material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.6f);
    }
}
