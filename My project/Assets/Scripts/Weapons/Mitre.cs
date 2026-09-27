using System.Collections;
using UnityEngine;

public class Mitre : MonoBehaviour, IHudWeapon
{
    [Header("Identificación")]
    public string weaponName = "Mitre";

    [Header("Tienda")]
    [Tooltip("Ficha de la tienda de esta arma. Con tienda en la escena, sin comprarla no se puede sacar (US 077).")]
    public ShopItem shopItem;

    [Header("Alcance")]
    // El daño por zona y distancia, la dispersión y el retroceso salen de la ficha (US 165, US 167 y US 168).
    public float maxRange = 300f;

    [Header("Munición")]
    public int maxAmmo = 30;
    public int currentAmmo;
    public int reserveAmmo = 60;
    public float reloadTime = 2.5f;

    [Header("Cadencia y Disparo")]
    public float fireRate = 9.75f; // disparos por segundo
    private float nextTimeToFire = 0f;

    [Header("Equipamiento")]
    public float equipTime = 1.0f;
    public float speedMultiplier = 0.92f;
    private bool isEquipping = false;

    [Header("Zoom al apuntar (1.25x)")]
    public float zoomFactor = 1.25f;

    /// <summary>Verdadero mientras se apunta con la mira del Mitre.</summary>
    public static bool Aiming { get; private set; }
    private float defaultFOV;
    private Camera camComponent;

    [Header("Referencias y Audio")]
    public Camera playerCamera;
    public AudioClip shootSound;
    public AudioClip reloadSound;
    [Tooltip("Canal del mixer (SFX), así respeta el volumen de efectos de Opciones (US 086, CA4).")]
    public UnityEngine.Audio.AudioMixerGroup sfxGroup;

    private AudioSource audioSource;
    private bool isReloading = false;
    private float reloadStartTime;
    private int fullReserve = -1;
    private PlayerMovement playerMovement;
    private Transform shooter;
    private ShopItem data;

    // Ficha del arma: la asignada o, si falta, la del catálogo de la tienda con este nombre.
    private ShopItem Data
    {
        get
        {
            if (shopItem != null) return shopItem;
            if (data == null) data = WeaponFire.FindInCatalog(this, weaponName);
            return data;
        }
    }

    // Datos para el HUD (US 056): nombre de la tienda, cargador, reserva y recarga.
    public string HudName => Data != null && !string.IsNullOrEmpty(Data.alias) ? Data.alias : weaponName;
    public int Ammo => currentAmmo;
    public int MagazineSize => maxAmmo;
    public int Reserve => reserveAmmo;
    public float ReloadProgress => isReloading ? Mathf.Clamp01((Time.time - reloadStartTime) / reloadTime) : -1f;

    void Awake()
    {
        currentAmmo = maxAmmo;
        if (fullReserve < 0) fullReserve = reserveAmmo;
        // Quien dispara: sus balas nunca le pegan a él mismo (US 165)
        PlayerMovement owner = GetComponentInParent<PlayerMovement>();
        shooter = owner != null ? owner.transform : transform.root;

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (playerCamera != null)
        {
            camComponent = playerCamera.GetComponent<Camera>();
            if (camComponent != null) defaultFOV = camComponent.fieldOfView;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        if (sfxGroup != null) audioSource.outputAudioMixerGroup = sfxGroup;

        // Buscar el controlador de movimiento del jugador
        playerMovement = GetComponentInParent<PlayerMovement>();
    }

    void OnEnable()
    {
        // CA6: Tarda 1 segundo en estar lista al sacarla
        StartCoroutine(EquipRoutine());

        // CA6: Reducción de velocidad al 92% mientras la lleva en mano
        if (playerMovement != null)
        {
            // playerMovement.speedMultiplier = speedMultiplier;
        }
    }

    void OnDisable()
    {
        Aiming = false;
        isReloading = false;
        isEquipping = false;

        // Restaurar velocidad base al guardar el arma
        if (playerMovement != null)
        {
            // playerMovement.speedMultiplier = 1.0f;
        }

        // Restaurar Zoom
        if (camComponent != null)
        {
            camComponent.fieldOfView = defaultFOV;
        }
    }

    // Al comprarla en la tienda queda con el cargador lleno y la reserva completa (US 077, CA1).
    public void Refill()
    {
        if (fullReserve < 0) fullReserve = reserveAmmo;
        currentAmmo = maxAmmo;
        reserveAmmo = fullReserve;
    }

    void Update()
    {
        // Control de zoom con clic derecho
        HandleZoom();

        // Bloqueos de estado
        if (isReloading || isEquipping) return;

        // CA2: Disparo automático manteniendo presionado el clic izquierdo
        if (KeyBindings.Held(GameAction.Disparar) && Time.time >= nextTimeToFire)
        {
            nextTimeToFire = Time.time + (1f / fireRate);
            Shoot();
        }

        // CA4: Recarga con R
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
            Debug.Log("Mitre: Cargador vacío.");
            return;
        }

        currentAmmo--;

        if (shootSound != null)
        {
            audioSource.PlayOneShot(shootSound);
        }

        // Retroceso, dispersión, zonas, daño por distancia, marcas y marcador de impacto (núcleo de disparo).
        // Apuntar con el zoom (clic derecho) reduce la dispersión (US 167, CA5).
        bool aiming = KeyBindings.Held(GameAction.Apuntar) && zoomFactor > 1f;
        WeaponFire.Fire(Data, playerCamera, shooter, aiming, maxRange);
    }

    void HandleZoom()
    {
        if (camComponent == null) return;

        // Apuntando con la mira: CameraLook usa la sensibilidad al apuntar (US 155).
        Aiming = KeyBindings.Held(GameAction.Apuntar) && zoomFactor > 1f;

        if (KeyBindings.Held(GameAction.Apuntar)) // Clic derecho mantenido (tecla reasignable)
        {
            camComponent.fieldOfView = Mathf.Lerp(camComponent.fieldOfView, defaultFOV / zoomFactor, Time.deltaTime * 10f);
        }
        else
        {
            camComponent.fieldOfView = Mathf.Lerp(camComponent.fieldOfView, defaultFOV, Time.deltaTime * 10f);
        }
    }

    private IEnumerator EquipRoutine()
    {
        isEquipping = true;
        yield return new WaitForSeconds(equipTime); // Espera de 1 segundo
        isEquipping = false;
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;
        reloadStartTime = Time.time;

        if (reloadSound != null)
        {
            audioSource.PlayOneShot(reloadSound);
        }

        yield return new WaitForSeconds(reloadTime); // Espera de 2.5 segundos

        int neededAmmo = maxAmmo - currentAmmo;
        int ammoToLoad = Mathf.Min(neededAmmo, reserveAmmo);

        currentAmmo += ammoToLoad;
        reserveAmmo -= ammoToLoad;

        isReloading = false;
        Debug.Log($"Mitre recargado: {currentAmmo}/{maxAmmo} | Reserva restante: {reserveAmmo}");
    }
}