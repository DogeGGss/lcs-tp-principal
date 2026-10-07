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

    [Header("Inventario (US 055)")]
    [Tooltip("Ícono del cuchillo en el espacio 3. Si queda vacío, el espacio muestra solo el nombre.")]
    [SerializeField] private Sprite knifeIcon;

    [Header("Marcador de impacto (US 165)")]
    [Tooltip("Canal del mixer para el sonido del marcador (SFX), así respeta el volumen de efectos.")]
    [SerializeField] private UnityEngine.Audio.AudioMixerGroup sfxGroup;

    // Mira (US 171): por defecto, cuatro rayitas blancas con borde oscuro y un hueco en el centro (px en 1920 x 1080).
    // El jugador la cambia desde Opciones > Mira (US 172): forma, color y si se abre con la dispersión del arma.
    private RectTransform crosshair;
    private MiraDibujo crosshairDrawing;
    private float crosshairOpen; // px que está abierta por la dispersión (mira dinámica)

    // Cuatro rayitas en la mira durante 0,15 s: blancas al acertar, amarillas a la cabeza, rojas y más grandes si mata.
    private const float HitMarkerTime = 0.15f;
    private static Color HitHeadColor => new Color(1f, 0.824f, 0.247f); // #FFD23F
    private RectTransform hitMarker;
    private readonly UnityEngine.UI.Image[] hitLines = new UnityEngine.UI.Image[4];
    private float hitAt = -10f;
    private HitMarkerKind hitKind;
    private AudioSource hitAudio;
    private AudioClip hitBodyClip, hitHeadClip, hitKillClip, hitSample;

    // Medidas de la maqueta (px en 1920 x 1080).
    private const float BarW = 934f, BarH = 170f, BlockW = 240f, SkillW = 150f;
    private const float RailLeftX = 256f, SkillX = 392f, RailRightX = 558f, AmmoX = 694f;

    // Inventario (US 055): los 4 espacios del equipo, uno arriba del otro, a la derecha de la munición.
    // El 5, el dispositivo del Modo Táctico (US 130), va arriba de todo y solo se ve mientras lo lleva.
    private const float InvX = BarW + 24f, InvW = 170f, InvRowH = 36f, InvGap = 6f;
    private const int DeviceSlot = 4;
    private class InvSlot
    {
        public RectTransform rect;
        public UnityEngine.UI.Image bg, icon, keyBg;
        public TextMeshProUGUI name, key, dash;
    }
    private readonly InvSlot[] invSlots = new InvSlot[5];
    private readonly string[] invState = new string[5];
    private bool sceneHasShop;

    /// <summary>Nombre del arma en la mano, para los avisos de bajas (US 057).</summary>
    public string WeaponName { get; private set; }

    /// <summary>Ícono del arma en la mano (de su ficha de la tienda), para los avisos de bajas (US 057).</summary>
    public Sprite WeaponIcon { get; private set; }

    private HealthSystem health;
    private IndicadorDeDano damageIndicator; // US 192
    private Minimapa minimap; // US 194
    private PlayerAbility ability;
    private WeaponSwitcher switcher;
    private MeleeWeaponHolder melee;
    private PlayerLoadout loadout;

    private CanvasGroup group;
    private GameObject crit, hint, skill;
    private CanvasGroup[] hiddenWhenScoped;
    private TextMeshProUGUI hintText;
    private TextMeshProUGUI hpText, shieldText;

    // Espectador (US 133): mientras se mira a un compañero, la vida y el minimapa son los suyos.
    private HealthSystem watched;
    private JugadorEnRed watchedMate;
    private Transform playerRoot;

    /// <summary>Muestra la vida y el minimapa de un compañero (espectador). null vuelve al jugador propio.</summary>
    public static void Spectate(JugadorEnRed mate)
    {
        CombatHud hud = current;
        if (hud == null) return;
        hud.watched = mate != null ? mate.GetComponent<HealthSystem>() : null;
        hud.watchedMate = mate;
        hud.lastHp = int.MinValue; // que se vuelva a dibujar
        hud.lastAmmoState = null;
        // El inventario de abajo es el propio (no se conoce el del compañero): no se ve mientras se especta.
        foreach (InvSlot slot in hud.invSlots)
            if (slot != null && slot.rect != null) slot.rect.localScale = mate != null ? Vector3.zero : Vector3.one;
        if (hud.minimap == null) return;
        if (mate != null) hud.minimap.Seguir(mate.transform, mate.Ojos);
        else if (hud.playerRoot != null)
        {
            Camera own = hud.playerRoot.GetComponentInChildren<Camera>();
            hud.minimap.Seguir(hud.playerRoot, own != null ? own.transform : null);
        }
    }

    // US 139, CA3: aviso verde "+50" al lado de la vida durante 1 s.
    private static CombatHud current;
    private TextMeshProUGUI healText;
    private float healUntil = -1f;
    private const float HealSeconds = 1f;

    /// <summary>Muestra "+N" en verde al lado de la vida durante 1 s (US 139).</summary>
    public static void ShowHeal(int amount)
    {
        if (current == null || current.healText == null || amount <= 0) return;
        current.healText.text = $"+{amount}";
        current.healUntil = Time.time + HealSeconds;
    }
    private UnityEngine.UI.Image hpFill, shieldFill;
    private RectTransform markHealth, markHalfShield;
    private UnityEngine.UI.Image ringFill, icon, keyChip;
    private readonly UnityEngine.UI.Image[] ticks = new UnityEngine.UI.Image[12];
    // US 019, CA7: una marca por carga, debajo del aro (solo si la habilidad tiene más de una).
    private RectTransform chargesRoot;
    private UnityEngine.UI.Image[] charges = new UnityEngine.UI.Image[0];
    private AbilityData shownSkill;
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
        current = this;
        WeaponFire.Hit += OnHit;
    }

    private void OnDisable()
    {
        WeaponFire.Hit -= OnHit;
    }

    private void OnDestroy()
    {
        if (ability != null) ability.Used -= OnAbilityUsed;
        KeyBindings.Changed -= RefreshSkillKey;
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
        UpdateHeal();
        UpdateSkill();
        UpdateAmmo();
        UpdateInventory();
        UpdateHitMarker();

        // La mira se ve con cualquier arma menos los francotiradores: sin la mira telescópica se tira a ojo, como el AWP
        // de CS (US 068), y con ella puesta la lente tiene su propia retícula (US 009). Con la pausa abierta se oculta
        // (con la tienda ya se oculta todo el HUD).
        crosshair.gameObject.SetActive(!PauseMenu.IsPaused && MiraTelescopica.EnMano == null);
        UpdateCrosshair();
        float center = MiraTelescopica.Puesta ? 0f : 1f;
        foreach (CanvasGroup part in hiddenWhenScoped) part.alpha = center;
    }

    // ---------- Mira ----------

    private void BuildCrosshair(RectTransform root)
    {
        crosshairDrawing = new MiraDibujo(root);
        crosshair = crosshairDrawing.raiz;
        crosshairDrawing.Aplicar(MiraConfig.Actual);
    }

    // US 172: la mira se dibuja con lo guardado en Opciones > Mira. Con "Se abre con la dispersión" (CA4), las
    // rayitas se separan según la dispersión actual del arma en la mano (US 167) y se cierran al frenar.
    private void UpdateCrosshair()
    {
        MiraConfig.Mira config = MiraConfig.Actual;
        float target = config.dinamica ? SpreadPixels() : 0f;
        crosshairOpen = Mathf.Lerp(crosshairOpen, target, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
        if (Mathf.Abs(crosshairOpen - target) < 0.05f) crosshairOpen = target;
        crosshairDrawing.Aplicar(config, crosshairOpen);
    }

    // La dispersión del arma en la mano, pasada de grados a px del HUD: hasta dónde puede desviarse una bala.
    private float SpreadPixels()
    {
        Camera view = Camera.main;
        if (view == null || switcher == null) return 0f;

        ShopItem weapon = null;
        GameObject held = switcher.HeldPrimary;
        if (held != null) weapon = WeaponSwitcher.FichaDe(held);
        else if (switcher.pistolObj != null && switcher.pistolObj.activeInHierarchy && loadout != null) weapon = loadout.Secondary;
        if (weapon == null) return 0f;

        float degrees = WeaponAim.For(view).CurrentSpread(weapon, false);
        RectTransform canvas = crosshair.parent as RectTransform;
        float half = canvas != null && canvas.rect.height > 0f ? canvas.rect.height * 0.5f : 540f;
        return Mathf.Tan(degrees * Mathf.Deg2Rad) / Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad) * half;
    }

    // ---------- Marcador de impacto ----------

    private void OnHit(HitMarkerKind kind)
    {
        hitKind = kind;
        hitAt = Time.unscaledTime;
        if (hitAudio == null) return;
        if (hitSample != null)
        {
            hitAudio.pitch = kind == HitMarkerKind.Kill ? 0.8f : kind == HitMarkerKind.Head ? 1.35f : 1f;
            hitAudio.PlayOneShot(hitSample, kind == HitMarkerKind.Kill ? 1f : 0.8f);
            return;
        }
        AudioClip clip = kind == HitMarkerKind.Kill ? hitKillClip : kind == HitMarkerKind.Head ? hitHeadClip : hitBodyClip;
        if (clip != null) hitAudio.PlayOneShot(clip);
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
        // El sonido del marcador está en Resources/SonidosJugador: a la cabeza suena más agudo y en una baja, más grave
        // (los tres tienen que distinguirse, US 165). Si no está, se usan los tonos de antes.
        SonidosJugador sonidos = SonidosJugador.Actual;
        hitSample = sonidos != null ? sonidos.marcadorImpacto : null;
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
        playerRoot = player.transform;
        if (damageIndicator != null) damageIndicator.Seguir(health);
        if (minimap != null) minimap.Seguir(player.transform);
        switcher = player.GetComponentInChildren<WeaponSwitcher>(true);
        melee = player.GetComponent<MeleeWeaponHolder>();
        loadout = player.GetComponent<PlayerLoadout>();
        sceneHasShop = FindAnyObjectByType<ShopUI>() != null;
        for (int i = 0; i < invState.Length; i++) invState[i] = null;
        ability = player.GetComponent<PlayerAbility>();
        if (ability != null) ability.Used += OnAbilityUsed;

        bool hasAbility = ability != null && ability.Ability != null;
        skill.SetActive(hasAbility);
        if (hasAbility) SetupSkill(ability.Ability);

        // Si se cambia la tecla desde la pausa (US 155), el chip de la habilidad muestra la nueva.
        KeyBindings.Changed -= RefreshSkillKey;
        KeyBindings.Changed += RefreshSkillKey;
    }

    private void RefreshSkillKey()
    {
        if (ability != null && ability.Ability != null) SetupSkill(ability.Ability);
    }

    private void OnAbilityUsed()
    {
        usedAt = Time.time;
    }

    // ---------- Vida y escudo: una sola vía con la vida efectiva ----------

    private void UpdateHealth()
    {
        HealthSystem shown = watched != null ? watched : health; // espectador: la del compañero que se mira
        int hp = Mathf.Max(0, shown.currentHealth), shield = Mathf.Max(0, shown.currentShield);
        int maxHp = Mathf.Max(1, shown.maxHealth), maxShield = Mathf.Max(0, shown.maxShield);
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

    // US 139, CA3: el "+50" aparece a la derecha del número, sube un poco y se apaga.
    private void UpdateHeal()
    {
        if (healText == null) return;
        float left = healUntil - Time.time;
        bool show = left > 0f;
        if (healText.gameObject.activeSelf != show) healText.gameObject.SetActive(show);
        if (!show) return;
        float t = 1f - left / HealSeconds; // 0 a 1
        float x = Width(hpText, hpText.text) + 12f + (shieldText.text.Length > 0 ? Width(shieldText, shieldText.text) + 10f : 0f);
        healText.rectTransform.anchoredPosition = new Vector2(x, -(104f - 14f * t));
        healText.alpha = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
    }

    // ---------- La habilidad: un reloj de andén que se enciende mientras recarga ----------

    private void SetupSkill(AbilityData data)
    {
        shownSkill = data;
        lastSkillState = int.MinValue;
        BuildCharges(ability != null ? ability.MaxCargas : 1);
        icon.sprite = data.icon;
        icon.enabled = data.icon != null;
        keyText.text = KeyBindings.Label(GameAction.Habilidad);
        nameText.text = data.displayName;
        float nameW = Width(nameText, data.displayName.ToUpperInvariant());
        float x = (SkillW - (24f + 8f + nameW)) / 2f;
        keyChip.rectTransform.anchoredPosition = new Vector2(x, 0f);
        nameText.rectTransform.anchoredPosition = new Vector2(x + 32f, 0f);
    }

    private void UpdateSkill()
    {
        if (ability == null || ability.Ability == null) return;
        // US 016: en el Táctico el personaje (y su habilidad) se elige con la partida ya cargada.
        if (ability.Ability != shownSkill) SetupSkill(ability.Ability);

        float progress = ability.Progress;
        bool ready = ability.IsReady, used = Time.time - usedAt < 0.4f;
        int lit = Mathf.FloorToInt(progress * 12f + 0.0001f);
        // US 019, CA7: con cargas, los segundos son los que faltan para la próxima aunque quede alguna para usar.
        int seconds = ability.Recargando ? Mathf.CeilToInt(ability.CooldownLeft) : 0;
        int left = ability.Cargas;
        ringFill.fillAmount = progress;

        int state = lit + seconds * 16 + (ready ? 1 << 20 : 0) + (used ? 1 << 21 : 0) + (left << 22);
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
        for (int i = 0; i < charges.Length; i++) charges[i].color = i < left ? (used && i == left - 1 ? Accent : Ink) : dim;
        if (charges.Length > 0) cooldownText.color = ready ? Mute : Color.white;
    }

    // Marcas cortas centradas debajo del aro, como los vagones de una formación.
    private void BuildCharges(int count)
    {
        if (chargesRoot == null) return;
        int shown = count > 1 ? count : 0;
        if (charges.Length == shown) return;
        foreach (UnityEngine.UI.Image old in charges) if (old != null) Destroy(old.gameObject);
        charges = new UnityEngine.UI.Image[shown];
        const float w = 16f, gap = 5f;
        float x = (SkillW - (shown * w + (shown - 1) * gap)) / 2f;
        for (int i = 0; i < shown; i++)
            charges[i] = Image(Place(Node("Carga" + (i + 1), chargesRoot), x + i * (w + gap), 0f, w, 4f), rounded, Ink, 2f);
    }

    // ---------- Munición: un durmiente por bala ----------

    private void UpdateAmmo()
    {
        ReadWeapon(out string name, out int ammo, out int size, out int reserve, out float reload);
        WeaponName = name;
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
        if (empty) hintText.text = reserve >= 0 ? KeyBindings.Label(GameAction.Recargar) + " · Recargar" : "Sin balas";
    }

    // El arma en la mano: el arma principal o la secundaria (las dos implementan IHudWeapon); si no, el cuchillo.
    private void ReadWeapon(out string name, out int ammo, out int size, out int reserve, out float reload)
    {
        name = ""; ammo = 0; size = 0; reserve = -1; reload = -1f;
        WeaponIcon = null;

        // Espectador (US 133): el arma y las balas del compañero que se mira, que llegan por la red.
        if (watchedMate != null)
        {
            if (!watchedMate.Vivo) return;
            name = watchedMate.NombreArma;
            watchedMate.Balas(out ammo, out size, out reserve, out reload);
            return;
        }

        // Granada en la mano (US 073): su nombre y cuántas de esas lleva.
        ShopItem grenadeInHand = HeldGrenade();
        if (grenadeInHand != null)
        {
            name = ItemName(grenadeInHand);
            WeaponIcon = grenadeInHand.icon;
            ammo = size = loadout != null ? loadout.Count(grenadeInHand) : 1;
            return;
        }

        // El dispositivo en la mano (US 130): sin balas.
        if (switcher != null && switcher.DispositivoEquipado)
        {
            name = "Dispositivo";
            return;
        }

        GameObject held = null;
        if (switcher != null)
        {
            held = switcher.HeldPrimary;
            if (held == null) held = switcher.HeldSecondary; // la Línea A o la que compró (US 072)
        }
        IHudWeapon weapon = held != null ? held.GetComponentInChildren<IHudWeapon>() : null;
        if (weapon != null)
        {
            // La secundaria se muestra con su nombre de la tienda (Línea A o Línea H).
            bool secondary = held == switcher.HeldSecondary;
            name = secondary ? SecondaryName() : weapon.HudName;
            ShopItem ficha = secondary ? (loadout != null ? loadout.Secondary : null) : WeaponSwitcher.FichaDe(held);
            WeaponIcon = ficha != null ? ficha.icon : null;
            ammo = weapon.Ammo; size = weapon.MagazineSize; reserve = weapon.Reserve; reload = weapon.ReloadProgress;
            return;
        }
        if (melee != null && melee.CurrentViewModel != null && melee.CurrentViewModel.activeInHierarchy)
        {
            name = melee.CurrentWeapon != null ? melee.CurrentWeapon.weaponName : "Cuchillo";
            WeaponIcon = knifeIcon; // para el aviso de baja con cuchillo
        }
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
        GameObject railsLeft = Image(Place(Node("Rieles", bar), RailLeftX, 160f, 120f, 12f), rails, Color.white).gameObject;
        BuildSkill(bar);
        GameObject railsRight = Image(Place(Node("Rieles", bar), RailRightX, 160f, 120f, 12f), rails, Color.white).gameObject;
        // Con la mira telescópica puesta (US 009) se oculta el centro del andén: la habilidad y sus rieles tapan la lente.
        hiddenWhenScoped = new[] { railsLeft.AddComponent<CanvasGroup>(), skill.AddComponent<CanvasGroup>(), railsRight.AddComponent<CanvasGroup>() };
        BuildAmmo(bar);
        BuildInventory(bar);
        BuildCrosshair(root);
        BuildHitMarker(root);
        damageIndicator = Node("IndicadorDeDano", root).gameObject.AddComponent<IndicadorDeDano>(); // US 192
        minimap = gameObject.AddComponent<Minimapa>(); // US 194
        minimap.Iniciar(root, labelFont, rounded);
        gameObject.AddComponent<MatchHud>().Setup(root, displayFont, labelFont, rounded, this); // US 057
    }

    private void BuildHealth(RectTransform bar)
    {
        RectTransform block = Place(Node("Vida", bar), 0f, 0f, BlockW, BarH);
        hpText = Text(Place(Node("Numero", block), 0f, 88f, 170f, 64f), displayFont, 72f, Ink);
        AddShadow(hpText);
        shieldText = Text(Place(Node("Escudo", block), 100f, 126f, 140f, 21f), labelFont, 24f, ShieldColor);
        AddShadow(shieldText);
        healText = Text(Place(Node("Curacion", block), 100f, 104f, 160f, 40f), displayFont, 40f, Ok);
        AddShadow(healText);
        healText.gameObject.SetActive(false);

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

        chargesRoot = Place(Node("Cargas", block), 0f, 125f, SkillW, 4f);

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

    // ---------- Inventario (US 055) ----------

    private void BuildInventory(RectTransform bar)
    {
        for (int i = 0; i < invSlots.Length; i++)
        {
            InvSlot slot = new InvSlot();
            // Pivote a la derecha: el espacio en la mano crece hacia la izquierda, sin salirse de la pantalla.
            // El del dispositivo, arriba del espacio 1.
            float y = 6f + (i == DeviceSlot ? -1 : i) * (InvRowH + InvGap);
            slot.rect = Place(Node("Espacio" + (i + 1), bar), InvX, y, InvW, InvRowH);
            slot.rect.pivot = new Vector2(1f, 0.5f);
            slot.rect.anchoredPosition = new Vector2(InvX + InvW, -(y + InvRowH / 2f));
            slot.bg = Image(slot.rect, rounded, Rgb(10, 12, 17, DarkAlpha(0.55f)), 5f);

            slot.icon = Image(Place(Node("Icono", slot.rect), 8f, 6f, 52f, 24f), null, Mute);
            slot.icon.preserveAspect = true;
            slot.dash = Text(Place(Node("Vacio", slot.rect), 8f, 0f, 52f, InvRowH), displayFont, 18f, Mute, TextAlignmentOptions.Center);
            slot.dash.text = "—";
            slot.name = Text(Place(Node("Nombre", slot.rect), 66f, 0f, InvW - 100f, InvRowH), labelFont, 15f, Mute, TextAlignmentOptions.MidlineLeft, 8f, true);
            slot.name.overflowMode = TextOverflowModes.Ellipsis;

            RectTransform keyRect = Place(Node("Tecla", slot.rect), InvW - 28f, 8f, 20f, 20f);
            slot.keyBg = Image(keyRect, rounded, new Color(0f, 0f, 0f, DarkAlpha(0.35f)), 3f);
            slot.key = Text(Stretch(Node("Texto", keyRect)), displayFont, 13f, Mute, TextAlignmentOptions.Center);
            invSlots[i] = slot;
        }

        // El dispositivo no tiene ficha en la tienda: se dibuja su ícono (el mismo del reloj del marcador), en naranja.
        Place(MatchHud.DeviceGlyph(invSlots[DeviceSlot].rect, Accent, rounded, out _), 20f, 4f, 28f, 28f);
        invSlots[DeviceSlot].rect.gameObject.SetActive(false);
    }

    private void UpdateInventory()
    {
        // Qué tiene en la mano (CA4): 0 principal, 1 secundaria, 2 cuchillo; -1 si nada.
        int held = -1;
        if (switcher != null && switcher.HeldPrimary != null) held = 0;
        else if (switcher != null && switcher.HeldSecondary != null) held = 1;
        else if (melee != null && melee.CurrentViewModel != null && melee.CurrentViewModel.activeInHierarchy) held = 2;
        else if (HeldGrenade() != null) held = 3;
        else if (switcher != null && switcher.DispositivoEquipado) held = DeviceSlot;

        // Principal (CA2, CA3): lo que compró; en escenas sin tienda el Mitre está siempre.
        ShopItem primary = loadout != null ? loadout.Primary : null;
        GameObject always = !sceneHasShop && switcher != null ? switcher.PrimaryObj : null;
        IHudWeapon alwaysHud = always != null ? always.GetComponent<IHudWeapon>() : null;
        ShopItem alwaysItem = WeaponSwitcher.FichaDe(always);
        bool hasPrimary = primary != null || alwaysHud != null;
        string primaryName = primary != null ? ItemName(primary) : hasPrimary ? alwaysHud.HudName : "Vacío";
        Sprite primaryIcon = primary != null ? primary.icon : alwaysItem != null ? alwaysItem.icon : null;

        ShopItem secondary = loadout != null ? loadout.Secondary : null;
        // Con tienda, la que tiene en la tienda: si soltó la Línea A (US 184, CA4), el espacio queda vacío.
        bool hasSecondary = secondary != null || (!sceneHasShop && switcher != null && switcher.pistolObj != null);
        string secondaryName = hasSecondary ? SecondaryName() : "Vacío";

        bool hasKnife = melee != null && melee.CurrentWeapon != null;
        string knifeName = hasKnife ? melee.CurrentWeapon.weaponName : "Vacío";

        // Granadas (CA5): la primera que tenga y cuántas lleva en total.
        ShopItem grenade = null;
        int grenadeTotal = 0;
        if (loadout != null && loadout.Catalog != null)
            foreach (ShopItem item in loadout.Catalog.items)
            {
                if (item == null || item.kind != ShopItemKind.Grenade) continue;
                int n = loadout.Count(item);
                if (n <= 0) continue;
                if (grenade == null) grenade = item;
                grenadeTotal += n;
            }
        string grenadeName = grenade != null ? $"{ItemName(grenade)} ×{grenadeTotal}" : "Vacío";

        SetSlot(0, hasPrimary, held == 0, primaryName, primaryIcon, KeyBindings.Label(GameAction.ArmaPrincipal));
        SetSlot(1, hasSecondary, held == 1, secondaryName, secondary != null ? secondary.icon : null, KeyBindings.Label(GameAction.ArmaSecundaria));
        SetSlot(2, hasKnife, held == 2, knifeName, knifeIcon, KeyBindings.Label(GameAction.Cuchillo));
        SetSlot(3, grenade != null, held == 3, grenadeName, grenade != null ? grenade.icon : null, KeyBindings.Label(GameAction.Granadas));

        // US 130: el dispositivo ocupa su propio espacio mientras lo lleva.
        bool device = switcher != null && switcher.LlevaDispositivo;
        if (invSlots[DeviceSlot].rect.gameObject.activeSelf != device) invSlots[DeviceSlot].rect.gameObject.SetActive(device);
        if (device) SetSlot(DeviceSlot, true, held == DeviceSlot, "Dispositivo", null, KeyBindings.Label(GameAction.Dispositivo));
    }

    // La granada que tiene en la mano (US 073), o null.
    private ShopItem HeldGrenade()
    {
        if (switcher == null || !switcher.GrenadeEquipped || switcher.Granadas == null) return null;
        return switcher.Granadas.Selected;
    }

    // CA6: solo se redibuja cuando algo cambió.
    private void SetSlot(int i, bool has, bool inHand, string name, Sprite icon, string key)
    {
        string state = $"{has}|{inHand}|{name}|{(icon != null ? icon.name : "")}|{key}";
        if (invState[i] == state) return;
        invState[i] = state;

        InvSlot slot = invSlots[i];
        Color ink = inHand ? KeyInk : has ? Mute : Rgb(107, 113, 122);
        slot.bg.color = inHand ? Ink : Rgb(10, 12, 17, DarkAlpha(has ? 0.55f : 0.35f));
        slot.rect.localScale = Vector3.one * (inHand ? 1.08f : 1f);

        bool showIcon = has && icon != null;
        slot.icon.enabled = showIcon;
        slot.icon.sprite = icon;
        slot.icon.color = inHand ? KeyInk : Rgb(154, 161, 171);
        slot.dash.gameObject.SetActive(!has);
        slot.dash.color = ink;

        slot.name.text = name;
        slot.name.color = ink;
        slot.key.text = key;
        slot.key.color = ink;
        slot.keyBg.color = inHand ? new Color(0f, 0f, 0f, 0.12f) : new Color(0f, 0f, 0f, DarkAlpha(0.35f));
    }

    private static string ItemName(ShopItem item) => string.IsNullOrEmpty(item.alias) ? item.displayName : item.alias;

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
