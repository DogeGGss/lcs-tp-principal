using System.Collections;
using UnityEngine;

public class Pistola : MonoBehaviour
{
    [Header("Estadísticas de Arma")]
    public int damage = 25;
    public int maxAmmo = 20;       // Capacidad máxima del cargador
    public int currentAmmo;        // Balas en el cargador actual
    public int reserveAmmo = 60;   // Balas totales en reserva
    public float reloadTime = 1.5f;

    [Header("Referencias")]
    public Camera playerCamera;
    public AudioClip shootSound;
    public AudioClip reloadSound;

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
    }

    void OnDisable()
    {
        // Si el WeaponSwitcher oculta la pistola en plena recarga, se cancela el estado
        isReloading = false;
    }

    void Update()
    {
        // Si está recargando, no procesa disparo ni nueva recarga
        if (isReloading) return;

        // Disparo con clic izquierdo
        if (Input.GetMouseButtonDown(0))
        {
            Shoot();
        }

        // Recarga con la tecla R
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
        Debug.Log("¡PUM! Balas en cargador: " + currentAmmo + " | Reserva: " + reserveAmmo);

        if (shootSound != null)
        {
            audioSource.PlayOneShot(shootSound);
        }

        RaycastHit hit;
        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out hit, 100f))
        {
            HealthSystem targetHealth = hit.transform.GetComponent<HealthSystem>();
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

        if (reloadSound != null)
        {
            audioSource.PlayOneShot(reloadSound);
        }

        yield return new WaitForSeconds(reloadTime);

        // Cuántas balas faltan para llenar el cargador
        int neededAmmo = maxAmmo - currentAmmo;
        // Solo tomamos lo que realmente tenemos en reserva
        int ammoToLoad = Mathf.Min(neededAmmo, reserveAmmo);

        currentAmmo += ammoToLoad;
        reserveAmmo -= ammoToLoad;

        isReloading = false;
        Debug.Log($"Recarga completada. Cargador: {currentAmmo}/{maxAmmo} | Reserva restante: {reserveAmmo}");
    }
}