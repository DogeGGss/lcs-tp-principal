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

    [Tooltip("Ficha de la tienda de esta arma. Si falta, usa la pistola inicial del catálogo (Línea A).")]
    public ShopItem shopItem;

    public AudioClip shootSound;
    public AudioClip reloadSound;

    [Header("Audio")]
    public AudioMixerGroup sfxGroup;

    private AudioSource audioSource;
    private Transform shooter;
    private ShopItem data;
    private bool isReloading = false;
    private float reloadStartTime;

    // Datos para el HUD (US 056): el HUD le pone el nombre de la tienda (Línea A o Línea H).
    public string HudName => "Pistola";
    public int Ammo => currentAmmo;
    public int MagazineSize => maxAmmo;
    public int Reserve => reserveAmmo;
    public float ReloadProgress => isReloading ? Mathf.Clamp01((Time.time - reloadStartTime) / reloadTime) : -1f;

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

    void OnDisable()
    {
        // Si el WeaponSwitcher oculta la pistola en plena recarga,
        // se cancela el estado.
        isReloading = false;
    }

    void Update()
    {
        // Si está recargando, no procesa disparo ni nueva recarga.
        if (isReloading) return;

        // Disparo con clic izquierdo.
        if (KeyBindings.Down(GameAction.Disparar))
        {
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

        currentAmmo--;

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
        item.alias = "Pistola";
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

        yield return new WaitForSeconds(reloadTime);

        // Cuántas balas faltan para llenar el cargador.
        int neededAmmo = maxAmmo - currentAmmo;

        // Solo tomamos lo que realmente tenemos en reserva.
        int ammoToLoad = Mathf.Min(neededAmmo, reserveAmmo);

        currentAmmo += ammoToLoad;
        reserveAmmo -= ammoToLoad;

        isReloading = false;

        Debug.Log(
            $"Recarga completada. Cargador: {currentAmmo}/{maxAmmo} | " +
            $"Reserva restante: {reserveAmmo}"
        );
    }
}