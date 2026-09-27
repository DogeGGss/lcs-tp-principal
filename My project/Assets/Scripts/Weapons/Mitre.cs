using System.Collections;
using UnityEngine;

public class Mitre : MonoBehaviour, IHudWeapon
{
    [Header("Identificación")]
    public string weaponName = "Mitre";

    [Header("Tienda")]
    [Tooltip("Ficha de la tienda de esta arma. Con tienda en la escena, sin comprarla no se puede sacar (US 077).")]
    public ShopItem shopItem;

    [Header("Debug Visual")]
    [Tooltip("Activa o desactiva los marcadores de impacto para visualizar el patrón de retroceso")]
    public bool debugVisualRecoil = false;

    [Header("Estadísticas de Daño")]
    public int headDamage = 160;
    public int bodyDamage = 40;
    public int legsDamage = 34;
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

    [Header("Retroceso (Recoil CA5)")]
    public Transform cameraTransform;       // Transform de la cámara para aplicar rotación de recoil
    public float verticalRecoil = 1.2f;     // Subida vertical por bala a partir del tiro 3
    public float horizontalRecoil = 0.8f;   // Fuerza del zigzagueo
    public float recoilRecoverySpeed = 6f;  // Velocidad para volver al centro
    private int consecutiveShots = 0;
    private Vector2 currentRecoilRotation = Vector2.zero;

    [Header("Zoom al apuntar (1.25x)")]
    public float zoomFactor = 1.25f;
    private float defaultFOV;
    private Camera camComponent;

    [Header("Referencias y Audio")]
    public Camera playerCamera;
    public AudioClip shootSound;
    public AudioClip reloadSound;

    private AudioSource audioSource;
    private bool isReloading = false;
    private float reloadStartTime;
    private int fullReserve = -1;
    private PlayerMovement playerMovement;
    private HealthSystem ownHealth;

    // Datos para el HUD (US 056): nombre de la tienda, cargador, reserva y recarga.
    public string HudName => shopItem != null && !string.IsNullOrEmpty(shopItem.alias) ? shopItem.alias : weaponName;
    public int Ammo => currentAmmo;
    public int MagazineSize => maxAmmo;
    public int Reserve => reserveAmmo;
    public float ReloadProgress => isReloading ? Mathf.Clamp01((Time.time - reloadStartTime) / reloadTime) : -1f;

    void Awake()
    {
        currentAmmo = maxAmmo;
        if (fullReserve < 0) fullReserve = reserveAmmo;
        // Vida de quien dispara, para no pegarse a sí mismo
        ownHealth = GetComponentInParent<HealthSystem>();

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (playerCamera != null)
        {
            camComponent = playerCamera.GetComponent<Camera>();
            if (camComponent != null) defaultFOV = camComponent.fieldOfView;
            if (cameraTransform == null) cameraTransform = playerCamera.transform;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

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
        isReloading = false;
        isEquipping = false;
        consecutiveShots = 0;

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
        // Recuperación gradual del retroceso hacia el centro
        HandleRecoilRecovery();

        // Control de zoom con clic derecho
        HandleZoom();

        // Bloqueos de estado
        if (isReloading || isEquipping) return;

        // CA2: Disparo automático manteniendo presionado el clic izquierdo
        if (Input.GetMouseButton(0) && Time.time >= nextTimeToFire)
        {
            nextTimeToFire = Time.time + (1f / fireRate);
            Shoot();
        }

        // Al soltar el botón de disparo se corta la ráfaga continua
        if (Input.GetMouseButtonUp(0))
        {
            consecutiveShots = 0;
        }

        // CA4: Recarga con R
        if (Input.GetKeyDown(KeyCode.R))
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
            consecutiveShots = 0;
            return;
        }

        currentAmmo--;
        consecutiveShots++;

        if (shootSound != null)
        {
            audioSource.PlayOneShot(shootSound);
        }

        // CA5: Calcular dirección con retroceso
        Vector3 shootDirection = CalculateRecoilDirection();

        // CA1: Raycast y cálculo de impacto
        // Ignora las zonas invisibles (triggers) como la zona de compra, que frenaban la bala.
        RaycastHit hit;
        if (Physics.Raycast(playerCamera.transform.position, shootDirection, out hit, maxRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            // Debug visual opcional: dibuja trayectoria y crea marca esférica
            if (debugVisualRecoil)
            {
                Debug.DrawLine(playerCamera.transform.position, hit.point, Color.red, 2.0f);

                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.transform.position = hit.point;
                marker.transform.localScale = Vector3.one * 0.08f;
                Destroy(marker.GetComponent<Collider>());
                Destroy(marker, 3.0f);
            }

            ApplyDamageByZone(hit);
            ImpactMarks.Spawn(hit);
        }
        else if (debugVisualRecoil)
        {
            // Si no colisiona con nada, dibuja el rayo hacia el alcance máximo
            Debug.DrawLine(playerCamera.transform.position, playerCamera.transform.position + (shootDirection * maxRange), Color.red, 2.0f);
        }
    }

    Vector3 CalculateRecoilDirection()
    {
        Vector3 baseDirection = playerCamera.transform.forward;

        // 1) Balas 1 y 2: Centro perfecto
        if (consecutiveShots <= 2)
        {
            return baseDirection;
        }

        // 2) Componente Vertical:
        // Sube linealmente hasta la bala 6; desde la 7 se amortigua (crece cada vez menos).
        float upwardOffset;
        if (consecutiveShots <= 6)
        {
            upwardOffset = (consecutiveShots - 2) * (verticalRecoil * 0.015f);
        }
        else
        {
            float baseVertical = 4f * (verticalRecoil * 0.015f);
            // Crecimiento desacelerado mediante raíz cuadrada a partir del 7° disparo
            upwardOffset = baseVertical + Mathf.Sqrt(consecutiveShots - 6) * (verticalRecoil * 0.007f);
        }

        // 3) Componente Horizontal (Asimétrico a partir de la bala 7):
        float horizontalOffset = 0f;
        if (consecutiveShots >= 7)
        {
            int shotIndex = consecutiveShots - 7;
            // Bloques de 3 balas para alternar el lado
            int side = ((shotIndex / 3) % 2 == 0) ? -1 : 1;

            // Amplitud base progresiva: crece a medida que se sostiene la ráfaga
            float spreadMultiplier = 1.0f + ((consecutiveShots - 6) * 0.08f);

            // ASIMETRÍA:
            // Si va a la izquierda (-1) aplica un 75% de fuerza; si va a la derecha (+1) aplica un 125%
            float sideAsymmetry = (side < 0) ? 0.75f : 1.25f;

            // Leve variación determinista basada en el número de bala para romper líneas perfectamente rectas
            float microJitter = ((consecutiveShots * 17) % 5 - 2) * 0.003f;

            horizontalOffset = (side * horizontalRecoil * 0.022f * spreadMultiplier * sideAsymmetry) + microJitter;
        }

        // Guardar para la recuperación al soltar el gatillo
        currentRecoilRotation.x = upwardOffset;
        currentRecoilRotation.y = horizontalOffset;

        // Desviación aplicada a la dirección relativa de la cámara
        Vector3 spreadDirection = baseDirection
                                  + (playerCamera.transform.up * upwardOffset)
                                  + (playerCamera.transform.right * horizontalOffset);

        return spreadDirection.normalized;
    }

    void HandleRecoilRecovery()
    {
        // Si no se está disparando, recupera suavemente el desplazamiento hacia el centro
        if (!Input.GetMouseButton(0) && currentRecoilRotation != Vector2.zero)
        {
            currentRecoilRotation = Vector2.Lerp(currentRecoilRotation, Vector2.zero, Time.deltaTime * recoilRecoverySpeed);
        }
    }

    void ApplyDamageByZone(RaycastHit hit)
    {
        HealthSystem targetHealth = hit.collider.GetComponentInParent<HealthSystem>();
        if (targetHealth == null || targetHealth == ownHealth) return;

        int finalDamage = bodyDamage; // Base 40

        // Diferenciación de zona por Tag o nombre de Collider
        string hitTag = hit.collider.tag;
        string hitName = hit.collider.gameObject.name.ToLower();

        if (hitTag == "Head" || hitName.Contains("head") || hitName.Contains("cabeza"))
        {
            finalDamage = headDamage; // 160
            Debug.Log($"Mitre: ¡HEADSHOT! {finalDamage} de daño.");
        }
        else if (hitTag == "Legs" || hitName.Contains("leg") || hitName.Contains("pierna"))
        {
            finalDamage = legsDamage; // 34
            Debug.Log($"Mitre: Impacto en pierna. {finalDamage} de daño.");
        }
        else
        {
            Debug.Log($"Mitre: Impacto en cuerpo. {finalDamage} de daño.");
        }

        targetHealth.TakeDamage(finalDamage);
    }

    void HandleZoom()
    {
        if (camComponent == null) return;

        if (Input.GetMouseButton(1)) // Clic derecho mantenido
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
        consecutiveShots = 0;

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