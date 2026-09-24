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

            setupInicialListo = true; 
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            if (meleeScript != null && meleeScript.CurrentViewModel != null)
            {
                meleeScript.CurrentViewModel.SetActive(true);
            }
            if (pistolObj != null) pistolObj.SetActive(false);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            if (meleeScript != null && meleeScript.CurrentViewModel != null)
            {
                meleeScript.CurrentViewModel.SetActive(false);
            }
            if (pistolObj != null) pistolObj.SetActive(true);
        }
    }
}