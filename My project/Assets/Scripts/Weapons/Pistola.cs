using UnityEngine;

public class Pistola : MonoBehaviour
{
    public int damage = 25;
    public int maxAmmo = 20;
    public int currentAmmo;

    public Camera playerCamera;

    void Start()
    {
        currentAmmo = maxAmmo;
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

        // Esto dibuja un láser rojo en la pestaña "Scene" que dura 2 segundos
        Debug.DrawRay(playerCamera.transform.position, playerCamera.transform.forward * 100f, Color.red, 2f);

        RaycastHit hit;
        // Le pasamos un 100f al final para garantizar que el rayo viaje 100 metros
        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out hit, 100f))
        {
            Debug.Log("Impacto en: " + hit.transform.name);

            HealthSystem targetHealth = hit.transform.GetComponent<HealthSystem>();
            if (targetHealth != null)
            {
                targetHealth.TakeDamage(damage);
            }
        }
    }
}