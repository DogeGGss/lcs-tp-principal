using UnityEngine;

public class MeleeWeaponHolder : MonoBehaviour
{
    [SerializeField] private MeleeWeaponData defaultWeapon;
    [SerializeField] private Transform viewModelParent;
    [Tooltip("Canal del mixer (SFX), así respeta el volumen de efectos de Opciones (US 114, CA3).")]
    [SerializeField] private UnityEngine.Audio.AudioMixerGroup sfxGroup;

    public MeleeWeaponData CurrentWeapon { get; private set; }
    public GameObject CurrentViewModel { get; private set; }
    /// <summary>El arma con la que arranca (el cuchillo).</summary>
    public MeleeWeaponData DefaultWeapon => defaultWeapon;
    /// <summary>Donde va el modelo de primera persona (la cámara).</summary>
    public Transform ViewModelParent => viewModelParent;

    private PlayerMovement movement;
    private AudioSource sounds;

    private void Start()
    {
        movement = GetComponent<PlayerMovement>();
        sounds = gameObject.AddComponent<AudioSource>();
        sounds.playOnAwake = false;
        sounds.spatialBlend = 0f;
        sounds.outputAudioMixerGroup = sfxGroup;
        Equip(defaultWeapon);
    }

    // Sonido de sacar el arma (US 114). Lo llama el WeaponSwitcher al cambiar al cuchillo.
    public void PlayDrawSound()
    {
        if (sounds != null && CurrentWeapon != null && CurrentWeapon.drawSound != null)
            sounds.PlayOneShot(CurrentWeapon.drawSound);
    }

    public void Equip(MeleeWeaponData weapon)
    {
        Unequip();
        if (weapon == null) return;

        CurrentWeapon = weapon;
        if (weapon.viewModelPrefab != null)
            CurrentViewModel = Instantiate(weapon.viewModelPrefab, viewModelParent, false);
        if (movement != null)
            movement.speedMultiplier = weapon.MoveSpeedMultiplier;
    }

    /// <summary>US 395: cambia el arma de este lugar en medio de la partida (el cuchillo por la katana) sin cambiar lo
    /// que tiene en la mano: si tenía el cuchillo, queda con la nueva; si no, la nueva queda guardada.</summary>
    public void Cambiar(MeleeWeaponData weapon)
    {
        if (weapon == null || weapon == CurrentWeapon) return;
        bool enMano = CurrentViewModel != null && CurrentViewModel.activeSelf;
        float velocidad = movement != null ? movement.speedMultiplier : 1f;
        Equip(weapon);
        if (CurrentViewModel != null && !enMano) CurrentViewModel.SetActive(false);
        if (movement != null && !enMano) movement.speedMultiplier = velocidad; // la velocidad sigue siendo la del arma en la mano
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