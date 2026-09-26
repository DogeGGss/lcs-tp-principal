using UnityEngine;

public class WeaponSwitcher : MonoBehaviour
{
    [Header("Referencias de Armas")]
    public GameObject mitreObj;        // Slot 1: Fusil Mitre
    public GameObject pistolObj;       // Slot 2: Pistola
    public MeleeWeaponHolder meleeScript; // Slot 3: Cuchillo

    private bool setupInicialListo = false;

    void Update()
    {
        // Configuración inicial al arrancar: arranca con el Mitre equipado (o pistola si no está asignado)
        if (!setupInicialListo && meleeScript != null && meleeScript.CurrentViewModel != null)
        {
            EquipMitre();
            setupInicialListo = true;
        }

        // Tecla 1: Mitre (Arma principal)
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            EquipMitre();
        }

        // Tecla 2: Pistola (Arma secundaria)
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            EquipPistol();
        }

        // Tecla 3: Cuchillo (Melee)
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            EquipKnife();
        }
    }

    void EquipMitre()
    {
        if (mitreObj != null) mitreObj.SetActive(true);
        if (pistolObj != null) pistolObj.SetActive(false);
        if (meleeScript != null && meleeScript.CurrentViewModel != null)
        {
            meleeScript.CurrentViewModel.SetActive(false);
        }

        // El fusil Mitre reduce la velocidad al 92% (0.92f)
        ApplySpeedMultiplier(0.92f);
    }

    void EquipPistol()
    {
        if (mitreObj != null) mitreObj.SetActive(false);
        if (pistolObj != null) pistolObj.SetActive(true);
        if (meleeScript != null && meleeScript.CurrentViewModel != null)
        {
            meleeScript.CurrentViewModel.SetActive(false);
        }

        // Velocidad normal para pistola (100%)
        ApplySpeedMultiplier(1.0f);
    }

    void EquipKnife()
    {
        if (mitreObj != null) mitreObj.SetActive(false);
        if (pistolObj != null) pistolObj.SetActive(false);
        if (meleeScript != null && meleeScript.CurrentViewModel != null)
        {
            meleeScript.CurrentViewModel.SetActive(true);
        }

        // Aplica el multiplicador definido en el cuchillo
        float knifeSpeed = (meleeScript != null && meleeScript.CurrentWeapon != null)
            ? meleeScript.CurrentWeapon.moveSpeedMultiplier
            : 1.0f;

        ApplySpeedMultiplier(knifeSpeed);
    }

    private void ApplySpeedMultiplier(float multiplier)
    {
        PlayerMovement movement = meleeScript != null ? meleeScript.GetComponent<PlayerMovement>() : null;
        if (movement == null)
        {
            movement = GetComponentInParent<PlayerMovement>();
        }

        if (movement != null)
        {
            movement.speedMultiplier = multiplier;
        }
    }
}