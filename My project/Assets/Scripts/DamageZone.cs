using UnityEngine;

public class DamageZone : MonoBehaviour
{
    public int damageToGive = 50;

    // Cuando el tipito entra al objeto
    private void OnTriggerEnter(Collider other)
    {
        // Esto verifica si el objeto con el que choca tiene el coso de la vida
        HealthSystem health = other.GetComponent<HealthSystem>();

        if (health != null)
        {
            health.TakeDamage(damageToGive);
        }
    }
}