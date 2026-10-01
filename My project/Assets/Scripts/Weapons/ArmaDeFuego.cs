using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

// Arma principal que sale entera de su ficha de la tienda (US 067 Urquiza, US 068 Último Tren, US 069 Belgrano Sur y
// US 070 Roca):
// - automática: dispara mientras se mantiene apretado el botón;
// - de ráfaga: cada clic tira la ráfaga, aunque se mantenga apretado;
// - de bombeo o de cerrojo: un tiro por clic; la escopeta recarga de a un cartucho que se corta disparando.
// Cadencia, cargador, reserva, recarga, tiempo para sacarla y velocidad al moverse salen de la ficha; el daño por zona
// y distancia, la dispersión y el retroceso, del núcleo de disparo (WeaponFire y WeaponAim). Solo los francotiradores
// apuntan: llevan además una MiraTelescopica (US 009).
public class ArmaDeFuego : MonoBehaviour, IHudWeapon
{
    [Tooltip("Ficha de la tienda de esta arma: de acá salen todos sus números.")]
    public ShopItem shopItem;
    [Tooltip("Hasta dónde llegan las balas, en metros.")]
    public float alcance = 300f;

    [Header("Referencias y audio")]
    [Tooltip("Cámara del jugador. Vacío: la cámara de la que cuelga el arma.")]
    public Camera playerCamera;
    public AudioClip shootSound;
    public AudioClip reloadSound;
    [Tooltip("Armas que cargan de a un cartucho (escopeta): suena con cada cartucho.")]
    public AudioClip shellSound;
    [Tooltip("Armas de bombeo o cerrojo: suena después de cada disparo, cuando se carga el siguiente.")]
    public AudioClip pumpSound;
    [Tooltip("Segundos entre el disparo y el sonido del bombeo.")]
    public float pumpDelay = 0.35f;
    [Tooltip("Canal del mixer (SFX), así respeta el volumen de efectos de Opciones.")]
    public AudioMixerGroup sfxGroup;

    [Header("Munición")]
    public int currentAmmo;
    public int reserveAmmo;

    private AudioSource audioSource;
    private Transform shooter;
    private MiraTelescopica mira;
    private float nextShot;
    private bool loaded, equipping, reloading, bursting;
    private float reloadStart, reloadDuration = 1f;
    private Coroutine reloadRoutine;

    // Datos para el HUD (US 056).
    public string HudName => shopItem != null && !string.IsNullOrEmpty(shopItem.alias) ? shopItem.alias : name;
    public int Ammo => currentAmmo;
    public int MagazineSize => shopItem != null ? shopItem.magazine : 0;
    public int Reserve => reserveAmmo;
    public float ReloadProgress => reloading ? Mathf.Clamp01((Time.time - reloadStart) / reloadDuration) : -1f;

    // Velocidad del jugador con esta arma en la mano (por ejemplo, 97 % con el Urquiza).
    public float SpeedMultiplier => shopItem != null && shopItem.mobility > 0f ? shopItem.mobility : 1f;

    // La mira telescópica no se pone mientras se recarga o se saca el arma.
    public bool Recargando => reloading;
    public bool Equipando => equipping;

    private void Awake()
    {
        PlayerMovement owner = GetComponentInParent<PlayerMovement>();
        shooter = owner != null ? owner.transform : transform.root; // sus balas nunca le pegan a él mismo (US 165)
        if (playerCamera == null) playerCamera = GetComponentInParent<Camera>();
        if (playerCamera == null) playerCamera = Camera.main;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        if (sfxGroup != null) audioSource.outputAudioMixerGroup = sfxGroup;

        mira = GetComponent<MiraTelescopica>();
        if (!loaded) Refill();
    }

    // Al comprarla queda con el cargador lleno y la reserva completa (US 077, CA1).
    public void Refill()
    {
        if (shopItem == null) return;
        currentAmmo = shopItem.magazine;
        reserveAmmo = shopItem.reserve;
        loaded = true;
    }

    private void OnEnable()
    {
        bursting = false;
        reloading = false;
        StartCoroutine(Equip());
    }

    private void OnDisable()
    {
        // Al guardarla (o al abrir la pausa o la tienda) se corta la recarga, como con la pistola.
        StopAllCoroutines();
        reloading = false;
        bursting = false;
        equipping = false;
    }

    private void Update()
    {
        if (shopItem == null) return;
        if (equipping || bursting) return;

        bool press = KeyBindings.Down(GameAction.Disparar);
        bool trigger = shopItem.automatic ? KeyBindings.Held(GameAction.Disparar) : press;

        // Escopeta: disparar con cartuchos cargados corta la recarga (US 070, CA4).
        if (reloading && shopItem.reloadPerShell && press && currentAmmo > 0) StopReload();
        if (reloading) return;

        if (trigger && currentAmmo > 0 && Time.time >= nextShot)
        {
            nextShot = Time.time + 1f / Mathf.Max(0.01f, shopItem.fireRate);
            if (shopItem.burstCount > 1) StartCoroutine(Burst());
            else FireOne();
        }

        if (KeyBindings.Down(GameAction.Recargar) && currentAmmo < MagazineSize && reserveAmmo > 0) StartReload();
    }

    private void FireOne()
    {
        currentAmmo--;
        if (shootSound != null) audioSource.PlayOneShot(shootSound);
        // Cada perdigón hace su daño por separado; los perdigones y el cono salen de la ficha (US 070, CA1).
        // Con la mira telescópica puesta la dispersión es la de apuntar (US 167, CA5).
        bool conMira = mira != null && mira.Nivel > 0;
        WeaponFire.Fire(shopItem, playerCamera, shooter, conMira, alcance);
        if (pumpSound != null && currentAmmo > 0) StartCoroutine(PlayLater(pumpSound, pumpDelay));
        // Cerrojo: sale de la mira y vuelve sola cuando se puede volver a disparar (US 068, CA3).
        if (mira != null) mira.AlDisparar(1f / Mathf.Max(0.01f, shopItem.fireRate));
    }

    private IEnumerator PlayLater(AudioClip clip, float delay)
    {
        yield return new WaitForSeconds(delay);
        audioSource.PlayOneShot(clip);
    }

    // US 069, CA1 y CA3: la ráfaga tira sus balas seguidas; si quedan menos, las que queden.
    private IEnumerator Burst()
    {
        bursting = true;
        float gap = shopItem.burstRate > 0f ? 1f / shopItem.burstRate : 1f / 15f;
        for (int i = 0; i < shopItem.burstCount && currentAmmo > 0; i++)
        {
            if (i > 0) yield return new WaitForSeconds(gap);
            FireOne();
        }
        bursting = false;
    }

    private IEnumerator Equip()
    {
        equipping = true;
        yield return new WaitForSeconds(shopItem != null ? shopItem.equipTime : 1f);
        equipping = false;
    }

    private void StartReload()
    {
        reloading = true;
        if (mira != null) mira.AlRecargar();
        reloadRoutine = StartCoroutine(shopItem.reloadPerShell ? ReloadShells() : ReloadMagazine());
    }

    private void StopReload()
    {
        if (reloadRoutine != null) StopCoroutine(reloadRoutine);
        reloadRoutine = null;
        reloading = false;
    }

    // US 011: todo el cargador de una vez.
    private IEnumerator ReloadMagazine()
    {
        reloadStart = Time.time;
        reloadDuration = Mathf.Max(0.01f, shopItem.reloadTime);
        if (reloadSound != null) audioSource.PlayOneShot(reloadSound);
        yield return new WaitForSeconds(shopItem.reloadTime);

        int load = Mathf.Min(MagazineSize - currentAmmo, reserveAmmo);
        currentAmmo += load;
        reserveAmmo -= load;
        reloading = false;
    }

    // US 070, CA4: de a un cartucho, cada uno con su tiempo; la barra del HUD muestra el cartucho que entra.
    private IEnumerator ReloadShells()
    {
        while (currentAmmo < MagazineSize && reserveAmmo > 0)
        {
            reloadStart = Time.time;
            reloadDuration = Mathf.Max(0.01f, shopItem.reloadTime);
            yield return new WaitForSeconds(shopItem.reloadTime);
            currentAmmo++;
            reserveAmmo--;
            AudioClip shell = shellSound != null ? shellSound : reloadSound;
            if (shell != null) audioSource.PlayOneShot(shell);
        }
        reloading = false;
    }
}
