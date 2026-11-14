using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

public class Pistola : MonoBehaviour, IHudWeapon
{
    [Header("Estadísticas de Arma")]
    public int damage = 25;
    public int maxAmmo = 20;
    public int currentAmmo;
    public int reserveAmmo = 60;
    public float reloadTime = 1.5f;

    [Header("Referencias")]
    public Camera playerCamera;

    [Tooltip("Ficha de la tienda de esta arma. Si falta, usa la pistola inicial del catálogo (La Porteña).")]
    public ShopItem shopItem;

    public AudioClip shootSound;
    public AudioClip reloadSound;

    [Header("Audio")]
    public AudioMixerGroup sfxGroup;

    private AudioSource audioSource;
    private Transform shooter;
    private ShopItem data;
    private bool isReloading = false;
    private int fullReserve = -1; // la reserva con la que viene el arma, para volver a llenarla
    private float reloadStartTime;
    private bool sacandola;   // US 242, CA2: mientras la saca no dispara
    private float nextShot;   // US 242, CA3: la cadencia de la ficha (6,75 disparos por segundo)

    // US 242: los tiempos salen de la ficha de la tienda, como en las demás armas; si falta, los del prefab.
    private float TiempoDeRecarga => Data.reloadTime > 0f ? Data.reloadTime : reloadTime;
    private float TiempoDeSacarla => Data.equipTime > 0f ? Data.equipTime : 0f;

    // Datos para el HUD (US 056): el HUD le pone el nombre de la tienda (Línea A o Línea H).
    public string HudName => L10n.T("gameplay.ui.pistol");
    public int Ammo => currentAmmo;
    public int MagazineSize => maxAmmo;
    public int Reserve => reserveAmmo;
    public float ReloadProgress => isReloading ? Mathf.Clamp01((Time.time - reloadStartTime) / Mathf.Max(0.01f, TiempoDeRecarga)) : -1f;

    void Awake()
    {
        if (fullReserve < 0) fullReserve = reserveAmmo;
    }

    /// <summary>Cargador lleno y la reserva con la que viene el arma. Lo usan el inicio de cada ronda del Táctico
    /// (antes La Porteña se quedaba con la reserva gastada de la ronda anterior), la reaparición y la tienda.</summary>
    /// <summary>US 148: el cargador y la reserva están como al comprarla.</summary>
    public bool Llena => currentAmmo >= maxAmmo && (fullReserve < 0 || reserveAmmo >= Municion.Reserva(fullReserve));

    public void Refill()
    {
        if (fullReserve < 0) fullReserve = reserveAmmo;
        currentAmmo = maxAmmo;
        reserveAmmo = Municion.Reserva(fullReserve); // con la Mochila, más (US 315)
    }

    /// <summary>US 293, CA2: la munición máxima del arma (cargador y reserva llenos).</summary>
    public int MunicionMaxima => maxAmmo + Mathf.Max(Municion.Reserva(fullReserve), reserveAmmo);

    /// <summary>La reserva llena (con la Mochila, más: US 315).</summary>
    public int ReservaLlena => Municion.Reserva(fullReserve < 0 ? reserveAmmo : fullReserve);

    /// <summary>US 315: la reserva llena creció (la Mochila): suma esas balas a la reserva.</summary>
    public void AgrandarReserva(float antes)
    {
        if (fullReserve < 0) fullReserve = reserveAmmo;
        reserveAmmo += Municion.Agrandada(fullReserve, antes);
    }

    /// <summary>US 360, CA6: al descartar Mochila, lo que sobra de la reserva se pierde.</summary>
    public void RecortarMunicion()
    {
        if (fullReserve >= 0) reserveAmmo = Mathf.Min(reserveAmmo, Municion.Reserva(fullReserve));
    }

    /// <summary>US 293, CA2: suma balas a la reserva sin pasarse de la llena; lo que sobra va al cargador.</summary>
    public void SumarBalas(int balas)
    {
        if (fullReserve < 0) fullReserve = reserveAmmo;
        int aReserva = Mathf.Clamp(Municion.Reserva(fullReserve) - reserveAmmo, 0, balas);
        reserveAmmo += aReserva;
        currentAmmo = Mathf.Min(maxAmmo, currentAmmo + balas - aReserva);
    }

    void Start()
    {
        currentAmmo = maxAmmo;

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        // Quien dispara: sus balas nunca le pegan a él mismo (US 165)
        PlayerMovement owner = GetComponentInParent<PlayerMovement>();
        shooter = owner != null ? owner.transform : transform.root;

        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Enviamos los sonidos de la pistola al grupo SFX
        if (sfxGroup != null)
        {
            audioSource.outputAudioMixerGroup = sfxGroup;
        }
    }

    void OnEnable()
    {
        // US 242, CA2: al sacarla tarda lo que dice la ficha (1 s) en estar lista, igual que la animación.
        StartCoroutine(Sacarla());
    }

    void OnDisable()
    {
        // Si el WeaponSwitcher oculta la pistola en plena recarga,
        // se cancela el estado.
        isReloading = false;
        sacandola = false;
    }

    private IEnumerator Sacarla()
    {
        sacandola = true;
        float espera = TiempoDeSacarla;
        if (espera > 0f) yield return new WaitForSeconds(espera);
        sacandola = false;
    }

    void Update()
    {
        // Si está recargando o sacándola, no procesa disparo ni nueva recarga.
        if (isReloading || sacandola) return;

        // Disparo con clic izquierdo, como mucho a la cadencia de la ficha (US 242, CA3).
        if (KeyBindings.Down(GameAction.Disparar) && Time.time >= nextShot)
        {
            if (currentAmmo > 0) nextShot = Time.time + 1f / Mathf.Max(0.01f, Data.fireRate > 0f ? Data.fireRate : 6.75f);
            Shoot();
        }

        // Recarga con la tecla R.
        if (KeyBindings.Down(GameAction.Recargar))
        {
            if (currentAmmo < maxAmmo && reserveAmmo > 0)
            {
                StartCoroutine(ReloadRoutine());
            }
        }
    }

    void Shoot()
    {
        if (currentAmmo <= 0)
        {
            Debug.Log("Cargador vacío (clic). Presiona R para recargar.");
            return;
        }

        if (!MinijuegoCometas.Activo) currentAmmo--; // US 157, CA2: en la compra los disparos no gastan balas

        Debug.Log(
            "¡PUM! Balas en cargador: " +
            currentAmmo +
            " | Reserva: " +
            reserveAmmo
        );

        // Sonido del disparo
        if (shootSound != null)
        {
            audioSource.PlayOneShot(shootSound);
        }

        // Retroceso, dispersión, zonas, daño por distancia, marcas y marcador de impacto (núcleo de disparo).
        WeaponFire.Fire(Data, playerCamera, shooter, false, 100f);
    }

    /// <summary>F35 (US 381): la ficha del arma (la asignada o la pistola inicial), para saber qué skin lleva.</summary>
    public ShopItem Ficha => Data;

    // Ficha del arma: la asignada o, si falta, la pistola inicial del catálogo de la tienda.
    private ShopItem Data
    {
        get
        {
            if (shopItem != null) return shopItem;
            if (data == null)
            {
                PlayerLoadout loadout = GetComponentInParent<PlayerLoadout>();
                if (loadout != null && loadout.Catalog != null) data = loadout.Catalog.starterSecondary;
                if (data == null) data = FallbackData();
            }
            return data;
        }
    }

    // Sin tienda ni ficha: el daño de siempre a cualquier zona, sin dispersión ni retroceso.
    private ShopItem FallbackData()
    {
        ShopItem item = ScriptableObject.CreateInstance<ShopItem>();
        item.alias = L10n.T("gameplay.ui.pistol");
        item.bands = new[] { new DamageBand { upTo = 0f, head = damage, body = damage, legs = damage } };
        return item;
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;
        reloadStartTime = Time.time;

        Debug.Log("Recargando...");

        // Sonido de recarga
        if (reloadSound != null)
        {
            audioSource.PlayOneShot(reloadSound);
        }

        yield return new WaitForSeconds(TiempoDeRecarga); // US 242, CA4: 1,75 s, lo que dice la ficha

        // Cuántas balas faltan para llenar el cargador.
        int neededAmmo = maxAmmo - currentAmmo;

        // Solo tomamos lo que realmente tenemos en reserva.
        int ammoToLoad = Mathf.Min(neededAmmo, reserveAmmo);

        currentAmmo += ammoToLoad;
        if (!Municion.ReservaInfinita) reserveAmmo -= ammoToLoad; // US 121, CA1 y Deathmatch: la reserva no se gasta

        isReloading = false;

        Debug.Log(
            $"Recarga completada. Cargador: {currentAmmo}/{maxAmmo} | " +
            $"Reserva restante: {reserveAmmo}"
        );
    }
}