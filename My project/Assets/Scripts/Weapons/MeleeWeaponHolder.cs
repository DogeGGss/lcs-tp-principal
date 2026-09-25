using UnityEngine;

public class MeleeWeaponHolder : MonoBehaviour
{
    [SerializeField] private MeleeWeaponData defaultWeapon;
    [SerializeField] private Transform viewModelParent;

    public MeleeWeaponData CurrentWeapon { get; private set; }
    public GameObject CurrentViewModel { get; private set; }

    private PlayerMovement movement;

    private void Start()
    {
        movement = GetComponent<PlayerMovement>();
        Equip(defaultWeapon);
    }

    public void Equip(MeleeWeaponData weapon)
    {
        Unequip();
        if (weapon == null) return;

        CurrentWeapon = weapon;
        if (weapon.viewModelPrefab != null)
            CurrentViewModel = Instantiate(weapon.viewModelPrefab, viewModelParent, false);
        if (movement != null)
            movement.speedMultiplier = weapon.moveSpeedMultiplier;
    }

    public void Unequip()
    {
        if (CurrentViewModel != null)
            Destroy(CurrentViewModel);
        CurrentViewModel = null;
        CurrentWeapon = null;
        if (movement != null)
            movement.speedMultiplier = 1f;
    }
}