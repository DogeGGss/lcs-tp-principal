using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

public class Pistola : MonoBehaviour
{
    [Header("Estadísticas de Arma")]
    public int damage = 25;
    public int maxAmmo = 20;
    public int currentAmmo;
    public int reserveAmmo = 60;
    public float reloadTime = 1.5f;

    [Header("Referencias")]
    public Camera playerCamera;

    public AudioClip shootSound;
    public AudioClip reloadSound;

    [Header("Audio")]
    public AudioMixerGroup sfxGroup;

    private AudioSource audioSource;
    private bool isReloading = false;

    void Start()
    {
        currentAmmo = maxAmmo;

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

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
        if (Input.GetMouseButtonDown(0))
        {
            Shoot();
        }

        // Recarga con la tecla R.
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

        RaycastHit hit;

        if (Physics.Raycast(
            playerCamera.transform.position,
            playerCamera.transform.forward,
            out hit,
            100f))
        {
            HealthSystem targetHealth =
                hit.transform.GetComponent<HealthSystem>();

            if (targetHealth != null)
            {
                targetHealth.TakeDamage(damage);
            }
        }
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;

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