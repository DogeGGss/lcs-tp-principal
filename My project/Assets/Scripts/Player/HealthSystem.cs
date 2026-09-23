using UnityEngine;

public class HealthSystem : MonoBehaviour
{
    public int maxHealth = 200;
    public int currentHealth;

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int damageAmount)
    {
        if (currentHealth <= 0) return;

        currentHealth -= damageAmount;
        Debug.Log("El jugador recibio " + damageAmount + " de danio. Vida restante: " + currentHealth);

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("El personaje ha muerto (0 HP).");
        gameObject.SetActive(false);
    }
}