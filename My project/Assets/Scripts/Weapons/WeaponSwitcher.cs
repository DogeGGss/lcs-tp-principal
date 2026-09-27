using UnityEngine;

public class WeaponSwitcher : MonoBehaviour
{
    [Header("Referencias de Armas")]
    public GameObject mitreObj;        // Slot 1: Fusil Mitre
    public GameObject pistolObj;       // Slot 2: Pistola
    public MeleeWeaponHolder meleeScript; // Slot 3: Cuchillo

    private bool setupInicialListo = false;

    // Tienda (US 077): con tienda en la escena el Mitre se saca solo si se compró.
    private enum Pendiente { Nada, Mitre, Pistola }
    private PlayerLoadout loadout;
    private Mitre mitre;
    private bool hayTienda;
    private bool teniaMitre;
    private Pendiente pendiente = Pendiente.Nada;

    void Awake()
    {
        loadout = GetComponentInParent<PlayerLoadout>();
        mitre = mitreObj != null ? mitreObj.GetComponent<Mitre>() : null;
    }

    void Start()
    {
        hayTienda = FindAnyObjectByType<ShopUI>() != null;
        teniaMitre = TieneMitre();
        if (loadout != null) loadout.Changed += OnLoadoutChanged;
    }

    void OnDestroy()
    {
        if (loadout != null) loadout.Changed -= OnLoadoutChanged;
    }

    void OnEnable()
    {
        // La tienda apaga este script mientras está abierta: el cambio de arma se hace al cerrarla.
        if (!setupInicialListo || pendiente == Pendiente.Nada) return;
        if (pendiente == Pendiente.Mitre) EquipMitre(); else EquipPistol();
        pendiente = Pendiente.Nada;
    }

    // Sin tienda en la escena (escenas de prueba de armas) el Mitre está siempre disponible.
    bool TieneMitre()
    {
        if (mitreObj == null) return false;
        if (!hayTienda || loadout == null) return true;
        return mitre != null && mitre.shopItem != null && loadout.Primary == mitre.shopItem;
    }

    void OnLoadoutChanged()
    {
        bool tieneMitre = TieneMitre();
        if (tieneMitre && !teniaMitre)
        {
            // Recién comprado: cargador lleno y reserva completa, y se saca al cerrar la tienda (US 077, CA1).
            if (mitre != null) mitre.Refill();
            CambiarA(Pendiente.Mitre);
        }
        else if (!tieneMitre && teniaMitre)
        {
            // Vendido, deshecho o perdido al morir: si estaba en la mano, se vuelve a la pistola.
            if (pendiente == Pendiente.Mitre) pendiente = Pendiente.Nada;
            if (mitreObj.activeSelf) CambiarA(Pendiente.Pistola);
        }
        teniaMitre = tieneMitre;
    }

    void CambiarA(Pendiente arma)
    {
        if (!enabled) { pendiente = arma; return; }
        if (arma == Pendiente.Mitre) EquipMitre(); else EquipPistol();
    }

    void Update()
    {
        // Configuración inicial al arrancar: con el Mitre si lo tiene; si no, con la pistola
        if (!setupInicialListo && meleeScript != null && meleeScript.CurrentViewModel != null)
        {
            if (TieneMitre()) EquipMitre(); else EquipPistol();
            setupInicialListo = true;
        }

        // Tecla 1: Mitre (Arma principal), solo si lo tiene
        if (KeyBindings.Down(GameAction.ArmaPrincipal) && TieneMitre())
        {
            EquipMitre();
        }

        // Tecla 2: Pistola (Arma secundaria)
        if (KeyBindings.Down(GameAction.ArmaSecundaria))
        {
            EquipPistol();
        }

        // Tecla 3: Cuchillo (Melee)
        if (KeyBindings.Down(GameAction.Cuchillo))
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
        ApplySpeedMultiplier(mitre != null ? mitre.speedMultiplier : 0.92f);
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
            // Suena solo si se cambia al cuchillo, no al volver a apretar el 3 con el cuchillo en la mano (US 114).
            bool yaEnMano = meleeScript.CurrentViewModel.activeSelf;
            meleeScript.CurrentViewModel.SetActive(true);
            if (!yaEnMano) meleeScript.PlayDrawSound();
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