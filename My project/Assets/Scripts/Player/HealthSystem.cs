using UnityEngine;

public class HealthSystem : MonoBehaviour
{
    public int maxHealth = 200;
    public int currentHealth;

    public int maxShield = 100;
    public int currentShield;

    void Start()
    {
        currentHealth = maxHealth;
        currentShield = 0;
    }

    void Update()
    {
        // Botón temporal de prueba para sumar escudo en el editor (ELIMINAR MAS ADELANTE ESTA FUNCION)
        if (Input.GetKeyDown(KeyCode.E))
        {
            AddShield(50);
        }
    }

    public void AddShield(int shieldAmount)
    {
        currentShield += shieldAmount;
        if (currentShield > maxShield)
        {
            currentShield = maxShield;
        }
        Debug.Log("Escudo adquirido. Escudo actual: " + currentShield);
    }

    public void TakeDamage(int damageAmount)
    {
        if (currentHealth <= 0) return;

        if (currentShield > 0)
        {
            if (damageAmount <= currentShield)
            {
                currentShield -= damageAmount;
                damageAmount = 0;
            }
            else
            {
                damageAmount -= currentShield;
                currentShield = 0;
            }
        }

        if (damageAmount > 0)
        {
            currentHealth -= damageAmount;
        }

        Debug.Log("Recibes danio. Escudo: " + currentShield + " | Vida: " + currentHealth);

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("El personaje ha muerto (0 HP).");
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.enabled = false;
        }
    }
}