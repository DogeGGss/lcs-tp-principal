using UnityEngine;
using TMPro;

public class HealthUI : MonoBehaviour
{
    public HealthSystem playerHealth;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI shieldText;

    void Update()
    {
        healthText.text = "Vida: " + playerHealth.currentHealth;

        if (playerHealth.currentHealth <= playerHealth.maxHealth / 4)
        {
            healthText.color = Color.red;
        }
        else
        {
            healthText.color = Color.green;
        }

        if (shieldText != null)
        {
            shieldText.text = "Escudo: " + playerHealth.currentShield;
            shieldText.color = new Color(0.2f, 0.8f, 1f);
        }
    }
}