using UnityEngine;

public class WeaponSwitcher : MonoBehaviour
{
    public GameObject pistolObj;
    public MeleeWeaponHolder meleeScript;

    private bool setupInicialListo = false;

    void Update()
    {
        if (!setupInicialListo && meleeScript != null && meleeScript.CurrentViewModel != null)
        {
            meleeScript.CurrentViewModel.SetActive(false);

            if (pistolObj != null)
            {
                pistolObj.SetActive(true);
            }
            SetSpeedMultiplier(false);

            setupInicialListo = true;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            if (meleeScript != null && meleeScript.CurrentViewModel != null)
            {
                meleeScript.CurrentViewModel.SetActive(true);
            }
            if (pistolObj != null) pistolObj.SetActive(false);
            SetSpeedMultiplier(true);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            if (meleeScript != null && meleeScript.CurrentViewModel != null)
            {
                meleeScript.CurrentViewModel.SetActive(false);
            }
            if (pistolObj != null) pistolObj.SetActive(true);
            SetSpeedMultiplier(false);
        }
    }

    // La velocidad depende del arma en mano: el cuchillo aplica su multiplicador y la pistola ninguno.
    private void SetSpeedMultiplier(bool knifeInHand)
    {
        PlayerMovement movement = meleeScript != null ? meleeScript.GetComponent<PlayerMovement>() : null;
        if (movement == null) return;
        movement.speedMultiplier = knifeInHand && meleeScript.CurrentWeapon != null
            ? meleeScript.CurrentWeapon.moveSpeedMultiplier
            : 1f;
    }
}