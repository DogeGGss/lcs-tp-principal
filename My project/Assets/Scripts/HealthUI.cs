using UnityEngine;
using TMPro;

public class HealthUI : MonoBehaviour
{
    public HealthSystem playerHealth;
    public TextMeshProUGUI healthText;

    void Update()
    {
        healthText.text = "Vida: " + playerHealth.currentHealth;

        if (playerHealth.currentHealth <= 50)
        {
            healthText.color = Color.red;
        }
        else
        {
            healthText.color = Color.white;
        }
    }
}