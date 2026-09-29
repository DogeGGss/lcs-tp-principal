using UnityEngine;

public class Grenade1 : MonoBehaviour
{
    public float delay = 2f;
    public float explosionForce = 70f;
    public float radius = 5f;

    public float maxDamage = 100f;
    public float minDamage = 25f;
    public float fullDamageRadius = 1.5f;

    // Fuerza con la que se lanza la granada
    public float throwForce = 15f;

    private float countdown;
    private bool exploded = false;

    void Start()
    {
        countdown = delay;
    }

    void Update()
    {
        countdown -= Time.deltaTime;

        if (countdown <= 0 && exploded == false)
        {
            Explode();
            exploded = true;
        }
    }

    // Lanza la granada en la dirección indicada
    public void Throw(Vector3 direction)
    {
        Rigidbody rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.AddForce(
                direction * throwForce,
                ForceMode.Impulse
            );
        }
    }

    void Explode()
    {
        Collider[] colliders = Physics.OverlapSphere(
            transform.position,
            radius
        );

        foreach (var rangeObjects in colliders)
        {
            // Buscamos el sistema de vida del objetivo
            HealthSystem health =
                rangeObjects.GetComponent<HealthSystem>();

            if (health != null)
            {
                // Distancia entre la granada y el objetivo
                float distance = Vector3.Distance(
                    transform.position,
                    rangeObjects.transform.position
                );

                float damage;

                // Hasta 1.5 metros = 100 de daño
                if (distance <= fullDamageRadius)
                {
                    damage = maxDamage;
                }
                // Entre 1.5 y 5 metros = daño lineal
                else if (distance <= radius)
                {
                    float t = (distance - fullDamageRadius) /
                              (radius - fullDamageRadius);

                    damage = Mathf.Lerp(
                        maxDamage,
                        minDamage,
                        t
                    );
                }
                // Fuera del radio = 0 daño
                else
                {
                    damage = 0;
                }

                // Aplicamos el daño
                if (damage > 0)
                {
                    health.TakeDamage(
                        Mathf.RoundToInt(damage)
                    );
                }
            }

            // Fuerza de explosión
            Rigidbody rb = rangeObjects.GetComponent<Rigidbody>();

            if (rb != null)
            {
                rb.AddExplosionForce(
                    explosionForce * 10,
                    transform.position,
                    radius
                );
            }
        }

        Destroy(gameObject);
    }
}
