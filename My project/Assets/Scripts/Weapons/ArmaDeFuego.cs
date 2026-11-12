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
// US 287: el Gran Capitán (lanzagranadas) también: su ficha tiene una granada (grenadePrefab) y la velocidad con la que
// sale (velocidadDelProyectil), y en vez de balas dispara esa granada (WeaponFire.FireProjectile).
public class ArmaDeFuego : MonoBehaviour, IHudWeapon
{
    [Tooltip("Ficha de la tienda de esta arma: de acá salen todos sus números.")]
    public ShopItem shopItem;

    // Cartas de Invasión (CartasEnJuego) que cambian la ráfaga:
    /// <summary>US 324, Ráfaga creciente: cuántas balas de más tira la ráfaga de esta arma.</summary>
    public static System.Func<ArmaDeFuego, int> BalasDeMasEnLaRafaga;
    /// <summary>US 312, Sin intervalo: al terminar una ráfaga, true si la próxima puede salir sin la pausa (gasta una).</summary>
    public static System.Func<ArmaDeFuego, bool> SinPausaEntreRafagas;
    /// <summary>US 310, Cargador gemelo: el cargador se acaba de vaciar; true si se recarga al instante desde la reserva.</summary>
    public static System.Func<ArmaDeFuego, bool> RecargaAlVaciarse;
    /// <summary>US 319, Bala compacta: true si esta arma (sin mira telescópica) puede apuntar manteniendo el click
    /// derecho (con un zoom chico).</summary>
    public static System.Func<ArmaDeFuego, bool> ApuntaPorCarta;
    private readonly ZoomPorCarta zoomPorCarta = new ZoomPorCarta();
    private bool apuntandoPorCarta;
    /// <summary>US 319: está apuntando con el click derecho (sin mira telescópica, por la carta).</summary>
    public bool ApuntandoPorCarta => apuntandoPorCarta;

    /// <summary>US 320, Descarga rápida: true si esta arma (La Trochita) tiene el disparo cargado: el disparo sale al
    /// soltar el gatillo; manteniéndolo se carga en 3 etapas (2, 4 y 6 balas, a 0,1 s entre balas).</summary>
    public static System.Func<ArmaDeFuego, bool> DescargaRapida;
    public static readonly float[] EtapasDeCarga = { 0.3f, 0.6f, 0.9f }; // s manteniendo el gatillo
    public static readonly int[] BalasPorEtapa = { 2, 4, 6 };
    public const float EntreBalasDeDescarga = 0.1f;                        // 6 en 0,6 s
    public static readonly Color ColorDeDescarga = new Color(1f, 0.72f, 0.15f); // dorado
    /// <summary>US 320: la carga del arma en la mano (0 a 1 hasta la última etapa; 0 si no está cargando), para el brillo.</summary>
    public static float CargaDeDescarga { get; private set; }
    /// <summary>US 320: la etapa a la que llegó la carga (0: todavía ninguna; 1 a 3).</summary>
    public static int EtapaDeDescarga { get; private set; }
    /// <summary>US 320: está saliendo la descarga (para el tambor).</summary>
    public static bool Descargando { get; private set; }
    private bool cargando;
    private float cargaDesde;

    /// <summary>US 309, Doble cartucho: true si este disparo de la escopeta sale con 2 cartuchos juntos.</summary>
    public static System.Func<ArmaDeFuego, bool> DobleCartucho;
    /// <summary>US 325, Mira abierta: cuántas veces la cadencia de la ficha (según si tiene la mira puesta).</summary>
    public static System.Func<ArmaDeFuego, bool, float> CadenciaPorCarta;
    /// <summary>Terminó de recargar (no si la recarga se cortó; US 324: la ráfaga vuelve a su cantidad de balas).</summary>
    public static event System.Action<ArmaDeFuego> TerminoDeRecargar;
    /// <summary>El arma que está disparando en este momento (mientras sale el tiro; si mata, es la que mató), o null.</summary>
    public static ArmaDeFuego Disparando { get; private set; }

    /// <summary>Suena por el audio del arma (como sus disparos).</summary>
    public void Sonar(AudioClip clip, float volumen = 1f)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip, volumen);
    }

    /// <summary>US 311, Cerrojo suelto: se puede volver a disparar dentro de esos segundos (si faltaba más).</summary>
    public void AcortarCerrojo(float segundos)
    {
        nextShot = Mathf.Min(nextShot, Time.time + segundos);
    }

    /// <summary>Es de ráfaga (el Belgrano Sur, US 069).</summary>
    public bool DeRafaga => shopItem != null && shopItem.burstCount > 1;
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
    [Tooltip("Segundos del principio del sonido del bombeo que se saltean (si el archivo empieza con silencio). Así el " +
             "ruido coincide con el guardamanos en la animación (US 248).")]
    public float pumpSkip = 0f;
    [Tooltip("Canal del mixer (SFX), así respeta el volumen de efectos de Opciones.")]
    public AudioMixerGroup sfxGroup;

    [Header("Munición")]
    public int currentAmmo;
    public int reserveAmmo;

    private AudioSource audioSource;
    private Transform shooter;
    private MiraTelescopica mira;
    private float nextShot;
    private bool encadenar; // US 312: la ráfaga que terminó deja salir la próxima sin pausa, manteniendo el gatillo
    private bool loaded, equipping, reloading, bursting;
    private float reloadStart, reloadDuration = 1f;
    private Coroutine reloadRoutine;

    // Datos para el HUD (US 056).
    public string HudName => shopItem != null && !string.IsNullOrEmpty(shopItem.alias) ? shopItem.alias : name;
    public int Ammo => currentAmmo;
    public int MagazineSize => shopItem != null ? Municion.Cargador(shopItem.magazine, shopItem.kind == ShopItemKind.PrimaryWeapon) : 0; // US 331
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

        // Las piezas del modelo que vinieron en la capa por defecto van en la del arma (la cámara del arma las dibuja).
        foreach (Transform parte in GetComponentsInChildren<Transform>(true))
            if (parte.gameObject.layer == 0) parte.gameObject.layer = gameObject.layer;
    }

    // Al comprarla queda con el cargador lleno y la reserva completa (US 077, CA1).
    /// <summary>US 148: el cargador y la reserva están como al comprarla.</summary>
    public bool Llena => shopItem == null || (currentAmmo >= MagazineSize && reserveAmmo >= ReservaLlena);

    /// <summary>La reserva llena (la de la ficha; con la Mochila, más: US 315).</summary>
    public int ReservaLlena => shopItem != null ? Municion.Reserva(shopItem.reserve) : 0;

    public void Refill()
    {
        if (shopItem == null) return;
        currentAmmo = MagazineSize;
        reserveAmmo = ReservaLlena;
        loaded = true;
    }

    /// <summary>US 293, CA2: la munición máxima del arma (cargador y reserva llenos).</summary>
    public int MunicionMaxima => shopItem != null ? MagazineSize + ReservaLlena : 0;

    /// <summary>US 315: la reserva llena creció (la Mochila): suma esas balas a la reserva.</summary>
    public void AgrandarReserva(float antes)
    {
        if (shopItem != null) reserveAmmo += Municion.Agrandada(shopItem.reserve, antes);
    }

    /// <summary>US 360, CA6: al descartar una carta que agrandaba el cargador o la reserva (Sin respaldo, Mochila), lo que
    /// no entra en el cargador vuelve a la reserva y lo que sobra de la reserva se pierde.</summary>
    public void RecortarMunicion()
    {
        if (shopItem == null) return;
        if (currentAmmo > MagazineSize) { reserveAmmo += currentAmmo - MagazineSize; currentAmmo = MagazineSize; }
        if (ReservaLlena >= 0) reserveAmmo = Mathf.Min(reserveAmmo, ReservaLlena);
    }

    /// <summary>US 293, CA2: suma balas a la reserva sin pasarse de la llena; lo que sobra va al cargador.</summary>
    public void SumarBalas(int balas)
    {
        if (shopItem == null) return;
        int aReserva = Mathf.Clamp(ReservaLlena - reserveAmmo, 0, balas);
        reserveAmmo += aReserva;
        currentAmmo = Mathf.Min(MagazineSize, currentAmmo + balas - aReserva);
    }

    private void OnEnable()
    {
        bursting = false;
        encadenar = false;
        reloading = false;
        StartCoroutine(Equip());
    }

    private void OnDisable()
    {
        // Al guardarla (o al abrir la pausa o la tienda) se corta la recarga, como con la pistola.
        StopAllCoroutines();
        if (pumpSource != null) pumpSource.Stop();
        reloading = false;
        bursting = false;
        equipping = false;
        apuntandoPorCarta = false;
        zoomPorCarta.Soltar(); // US 319: vuelve el campo de visión
        cargando = false;      // US 320
        CargaDeDescarga = 0f;
        EtapaDeDescarga = 0;
        Descargando = false;
        Trazadora.Tinte = null;
    }

    // US 319: apuntar con el click derecho (armas sin mira telescópica, con la carta): un zoom chico.
    private void ApuntarPorCarta()
    {
        bool puede = mira == null && !reloading && !equipping && ApuntaPorCarta != null && ApuntaPorCarta(this);
        apuntandoPorCarta = zoomPorCarta.Actualizar(puede, playerCamera);
    }

    private void Update()
    {
        if (shopItem == null) return;
        ApuntarPorCarta(); // US 319
        if (equipping || bursting) return;

        bool press = KeyBindings.Down(GameAction.Disparar);
        bool trigger = shopItem.automatic ? KeyBindings.Held(GameAction.Disparar) : press;
        if (encadenar && !KeyBindings.Held(GameAction.Disparar)) encadenar = false;
        if (encadenar) trigger = true; // US 312: manteniendo el gatillo, la próxima ráfaga sale enseguida

        // Escopeta: disparar con cartuchos cargados corta la recarga (US 070, CA4).
        if (reloading && shopItem.reloadPerShell && press && currentAmmo > 0) StopReload();
        if (reloading) return;

        // US 322, Tiro de reserva: el subfusil con el cargador vacío sigue desde la reserva, a la mitad de la cadencia.
        if (KeyBindings.Held(GameAction.Disparar) && currentAmmo == 0 && reserveAmmo > 0 && Time.time >= nextShot
            && shopItem.category == ShopCategory.SMGs && Municion.TiroDeReserva != null && Municion.TiroDeReserva())
        {
            nextShot = Time.time + 1f / Mathf.Max(0.01f, shopItem.fireRate * Municion.CadenciaDeReserva);
            FireOne(true);
            return;
        }

        // US 320, Descarga rápida: el disparo sale al soltar el gatillo. Un toque: un tiro normal. Manteniéndolo se carga
        // en 3 etapas (a los 0,3, 0,6 y 0,9 s): al soltar salen 2, 4 o 6 balas seguidas, doradas.
        if (DescargaRapida != null && DescargaRapida(this))
        {
            if (press && currentAmmo > 0 && Time.time >= nextShot) { cargando = true; cargaDesde = Time.time; }
            if (cargando)
            {
                float tiempo = Time.time - cargaDesde;
                int etapa = 0;
                while (etapa < EtapasDeCarga.Length && tiempo >= EtapasDeCarga[etapa]) etapa++;
                EtapaDeDescarga = etapa;
                CargaDeDescarga = Mathf.Clamp01(tiempo / EtapasDeCarga[EtapasDeCarga.Length - 1]);
                if (!KeyBindings.Held(GameAction.Disparar)) // soltó: sale
                {
                    cargando = false;
                    CargaDeDescarga = 0f;
                    EtapaDeDescarga = 0;
                    if (etapa == 0)
                    {
                        nextShot = Time.time + 1f / Mathf.Max(0.01f, shopItem.fireRate);
                        FireOne();
                    }
                    else StartCoroutine(Descarga(BalasPorEtapa[etapa - 1]));
                }
            }
            if (KeyBindings.Down(GameAction.Recargar) && !cargando && currentAmmo < MagazineSize && reserveAmmo > 0) StartReload();
            return;
        }
        CargaDeDescarga = 0f;
        EtapaDeDescarga = 0;

        if (trigger && currentAmmo > 0 && Time.time >= nextShot)
        {
            float cadencia = shopItem.fireRate * (CadenciaPorCarta?.Invoke(this, mira != null && mira.ConLente) ?? 1f); // US 325
            nextShot = Time.time + 1f / Mathf.Max(0.01f, cadencia);
            if (shopItem.burstCount > 1) StartCoroutine(Burst());
            else FireOne();
        }

        if (KeyBindings.Down(GameAction.Recargar) && currentAmmo < MagazineSize && reserveAmmo > 0) StartReload();
    }

    /// <summary>US 321, Acople: guardada, dispara sola un tiro hacia la mira, con sus balas y su cadencia. false si no
    /// pudo (vacía, recargando o todavía no le toca): ese tiro se pierde. No cuenta para las cartas de esta arma.</summary>
    public bool DispararGuardada()
    {
        if (shopItem == null || currentAmmo <= 0 || reloading || Time.time < nextShot) return false;
        Camera cam = playerCamera != null ? playerCamera : Camera.main;
        if (cam == null) return false;
        nextShot = Time.time + 1f / Mathf.Max(0.01f, shopItem.fireRate);
        if (!MinijuegoCometas.Activo) currentAmmo--;
        if (shootSound != null) AudioSource.PlayClipAtPoint(shootSound, cam.transform.position, 0.7f); // (guardada no suena)
        if (shopItem.velocidadDelProyectil > 0f && shopItem.grenadePrefab != null)
            WeaponFire.FireProjectile(shopItem, cam, shooter, false);
        else WeaponFire.Fire(shopItem, cam, shooter, false, alcance);
        return true;
    }

    /// <param name="deLaReserva">US 322: la bala sale de la reserva (Tiro de reserva).</param>
    private void FireOne(bool deLaReserva = false)
    {
        if (MinijuegoCometas.Activo) { } // US 157, CA2: en la compra los disparos no gastan balas
        else if (deLaReserva) { if (!Municion.ReservaInfinita) reserveAmmo--; }
        else currentAmmo--;
        // US 309: con la carta, sale también el cartucho de al lado (si queda uno).
        int cartuchos = 1;
        if (!deLaReserva && currentAmmo > 0 && DobleCartucho != null && DobleCartucho(this))
        {
            cartuchos = 2;
            if (!MinijuegoCometas.Activo) currentAmmo--;
        }
        if (shootSound != null) audioSource.PlayOneShot(shootSound);
        // Cada perdigón hace su daño por separado; los perdigones y el cono salen de la ficha (US 070, CA1).
        // Con la mira telescópica puesta la dispersión es la de apuntar (US 167, CA5).
        bool conMira = (mira != null && mira.ConLente) || apuntandoPorCarta; // US 319: apuntando con el click derecho
        Disparando = this;
        try
        {
            if (shopItem.velocidadDelProyectil > 0f && shopItem.grenadePrefab != null)
                WeaponFire.FireProjectile(shopItem, playerCamera, shooter, conMira); // US 287: una granada
            else WeaponFire.Fire(shopItem, playerCamera, shooter, conMira, alcance, cartuchos);
        }
        finally { Disparando = null; }
        // US 310: con el cargador recién vaciado, la carta puede recargarlo al instante.
        if (!deLaReserva && currentAmmo == 0 && reserveAmmo > 0 && RecargaAlVaciarse != null && RecargaAlVaciarse(this))
            MunicionDelArma.LlenarCargador(gameObject);
        if (pumpSound != null && currentAmmo > 0) StartCoroutine(PlayLater(pumpSound, pumpDelay, pumpSkip));
        // Cerrojo: sale de la mira y vuelve sola cuando se puede volver a disparar (US 068, CA3).
        // (Lo que falta para el próximo: con Mira abierta, US 325, o Cerrojo suelto, US 311, es menos que la ficha.)
        if (mira != null) mira.AlDisparar(Mathf.Max(0f, nextShot - Time.time));
    }

    private AudioSource pumpSource;

    private IEnumerator PlayLater(AudioClip clip, float delay, float skip = 0f)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (skip <= 0f || skip >= clip.length) { audioSource.PlayOneShot(clip); yield break; }
        // Desde la mitad del archivo: con una fuente aparte (PlayOneShot siempre empieza del principio).
        if (pumpSource == null)
        {
            pumpSource = gameObject.AddComponent<AudioSource>();
            pumpSource.playOnAwake = false;
            pumpSource.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
            pumpSource.spatialBlend = audioSource.spatialBlend;
        }
        pumpSource.clip = clip;
        pumpSource.time = skip;
        pumpSource.Play();
    }

    // US 320: la descarga del disparo cargado: las balas de la etapa, a 0,1 s, doradas.
    private IEnumerator Descarga(int balas)
    {
        bursting = true;
        Descargando = true;
        for (int i = 0; i < balas && currentAmmo > 0; i++)
        {
            if (i > 0) yield return new WaitForSeconds(EntreBalasDeDescarga);
            Trazadora.Tinte = ColorDeDescarga;
            try { FireOne(); }
            finally { Trazadora.Tinte = null; }
        }
        nextShot = Time.time + 1f / Mathf.Max(0.01f, shopItem.fireRate);
        Descargando = false;
        bursting = false;
    }

    // US 069, CA1 y CA3: la ráfaga tira sus balas seguidas; si quedan menos, las que queden. Con Ráfaga creciente
    // (US 324) tira más balas; con Sin intervalo (US 312) la próxima sale sin la pausa entre ráfagas, con la misma
    // separación que entre las balas.
    private IEnumerator Burst()
    {
        bursting = true;
        float gap = shopItem.burstRate > 0f ? 1f / shopItem.burstRate : 1f / 15f;
        int balas = shopItem.burstCount + Mathf.Max(0, BalasDeMasEnLaRafaga?.Invoke(this) ?? 0);
        for (int i = 0; i < balas && currentAmmo > 0; i++)
        {
            if (i > 0) yield return new WaitForSeconds(gap);
            FireOne();
        }
        encadenar = false;
        if (SinPausaEntreRafagas != null && SinPausaEntreRafagas(this)) { nextShot = Time.time + gap; encadenar = true; }
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
        if (!Municion.ReservaInfinita) reserveAmmo -= load; // US 121, CA1 y Deathmatch: la reserva no se gasta
        reloading = false;
        TerminoDeRecargar?.Invoke(this);
    }

    // US 070, CA4: de a un cartucho, cada uno con su tiempo; la barra del HUD muestra el cartucho que entra.
    private IEnumerator ReloadShells()
    {
        while (currentAmmo < MagazineSize && reserveAmmo > 0)
        {
            reloadStart = Time.time;
            reloadDuration = Mathf.Max(0.01f, shopItem.reloadTime);
            // El sonido arranca con el cartucho: su golpe (a los 0,3 s del archivo) cae cuando la mano lo mete (US 248).
            AudioClip shell = shellSound != null ? shellSound : reloadSound;
            if (shell != null) audioSource.PlayOneShot(shell);
            yield return new WaitForSeconds(shopItem.reloadTime);
            currentAmmo++;
            if (!Municion.ReservaInfinita) reserveAmmo--; // US 121, CA1 y Deathmatch: la reserva no se gasta
        }
        reloading = false;
        TerminoDeRecargar?.Invoke(this);
    }
}
