using UnityEngine;

public class Pistola : MonoBehaviour
{
    public int damage = 25;
    public int maxAmmo = 20;
    public int currentAmmo;
    public Camera playerCamera;

    public AudioClip shootSound;
    private AudioSource audioSource;

    void Start()
    {
        currentAmmo = maxAmmo;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Shoot();
        }
    }

    void Shoot()
    {
        if (currentAmmo <= 0)
        {
            Debug.Log("Cargador vacío (clic).");
            return;
        }

        currentAmmo--;
        Debug.Log("¡PUM! Balas restantes: " + currentAmmo);

        if (shootSound != null)
        {
            audioSource.PlayOneShot(shootSound);
        }

        RaycastHit hit;
        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out hit, 100f))
        {
            // CA4: Impacto
            HealthSystem targetHealth = hit.transform.GetComponent<HealthSystem>();
            if (targetHealth != null)
            {
                targetHealth.TakeDamage(damage);
            }
        }
    }
}