using UnityEngine;

public class GrenadeThrower : MonoBehaviour
{
    public GameObject grenadePrefab;
    public Transform grenadeSpawnPoint;
    public Camera playerCamera;

    void Update()
    {
        // Click izquierdo: lanzar granada
        if (KeyBindings.Down(GameAction.Disparar))
        {
            ThrowGrenade();
        }
    }

    void ThrowGrenade()
    {
        // Creamos la granada en el punto de lanzamiento
        GameObject grenadeObject = Instantiate(
            grenadePrefab,
            grenadeSpawnPoint.position,
            grenadeSpawnPoint.rotation
        );

        // Buscamos el script de la granada
        Grenade1 grenade = grenadeObject.GetComponent<Grenade1>();

        if (grenade != null)
        {
            // La lanzamos hacia donde está mirando la cámara
            grenade.Throw(playerCamera.transform.forward);
        }
    }
}


//Si la granada no esta equipada con el 4 no se lanza
/*
using UnityEngine;

public class GrenadeThrower : MonoBehaviour
{
    public GameObject grenadePrefab;
    public Transform grenadeSpawnPoint;
    public Camera playerCamera;
    public WeaponSwitcher weaponSwitcher;

    void Update()
    {
        // Click izquierdo: lanzar granada
        if (weaponSwitcher != null &&
            weaponSwitcher.GrenadeEquipped &&
            KeyBindings.Down(GameAction.Disparar))
        {
            ThrowGrenade();
        }
    }

    void ThrowGrenade()
    {
        // Creamos la granada en el punto de lanzamiento
        GameObject grenadeObject = Instantiate(
            grenadePrefab,
            grenadeSpawnPoint.position,
            grenadeSpawnPoint.rotation
        );

        // Buscamos el script de la granada
        Grenade1 grenade = grenadeObject.GetComponent<Grenade1>();

        if (grenade != null)
        {
            // La lanzamos hacia donde está mirando la cámara
            grenade.Throw(playerCamera.transform.forward);
        }
    }
}
*/