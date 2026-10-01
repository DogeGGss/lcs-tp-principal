using System.Collections.Generic;
using UnityEngine;

public class WeaponSwitcher : MonoBehaviour
{
    [Header("Referencias de Armas")]
    public GameObject mitreObj;        // Slot 1: Fusil Mitre
    [Tooltip("Las demás armas principales (Urquiza, Belgrano Sur, Roca...), cada una con su ArmaDeFuego. Con tienda, en el espacio 1 va la que se compró.")]
    public GameObject[] otrasPrincipales = new GameObject[0];
    public GameObject pistolObj;       // Slot 2: Pistola
    public MeleeWeaponHolder meleeScript; // Slot 3: Cuchillo
    public GameObject grenadeObj;       // Slot 4: Granada

    private bool setupInicialListo = false;

    // Tienda (US 077): con tienda en la escena el arma principal se saca solo si se compró.
    private enum Pendiente { Nada, Principal, Pistola }
    private PlayerLoadout loadout;
    private GrenadeThrower granadas;
    private bool hayTienda;
    private GameObject teniaPrincipal;
    private Pendiente pendiente = Pendiente.Nada;

    // Indica si actualmente está equipada la granada
    public bool GrenadeEquipped { get; private set; }

    void Awake()
    {
        loadout = GetComponentInParent<PlayerLoadout>();

        // Granadas (US 079): el manejo vive en GrenadeThrower; se agrega solo si el jugador no lo tiene.
        granadas = GetComponentInParent<GrenadeThrower>();
        if (granadas == null) granadas = gameObject.AddComponent<GrenadeThrower>();
    }

    void Start()
    {
        hayTienda = FindAnyObjectByType<ShopUI>() != null;
        teniaPrincipal = PrimaryObj;
        if (loadout != null) loadout.Changed += OnLoadoutChanged;
        if (granadas != null) granadas.Emptied += OnGranadasVacias;
    }

    void OnDestroy()
    {
        if (loadout != null) loadout.Changed -= OnLoadoutChanged;
        if (granadas != null) granadas.Emptied -= OnGranadasVacias;
    }

    // Se lanzó o se perdió la última granada que estaba en la mano: se vuelve a la pistola (US 079, CA6).
    void OnGranadasVacias()
    {
        CambiarA(Pendiente.Pistola);
    }

    void OnEnable()
    {
        // La tienda apaga este script mientras está abierta: el cambio de arma se hace al cerrarla.
        if (!setupInicialListo || pendiente == Pendiente.Nada) return;
        if (pendiente == Pendiente.Principal) EquipPrimary(); else EquipPistol();
        pendiente = Pendiente.Nada;
    }

    // Todas las armas principales que tiene el jugador en la mano, empezando por el Mitre.
    public List<GameObject> Principales()
    {
        var lista = new List<GameObject>();
        if (mitreObj != null) lista.Add(mitreObj);
        if (otrasPrincipales != null)
            foreach (GameObject arma in otrasPrincipales)
                if (arma != null) lista.Add(arma);
        return lista;
    }

    // El arma principal que tiene: la que compró en la tienda. Sin tienda en la escena (escenas de prueba de
    // armas), el Mitre está siempre disponible.
    public GameObject PrimaryObj
    {
        get
        {
            if (!hayTienda || loadout == null) return mitreObj;
            if (loadout.Primary == null) return null;
            foreach (GameObject arma in Principales())
                if (FichaDe(arma) == loadout.Primary) return arma;
            return null;
        }
    }

    // El arma principal que está en la mano, o null.
    public GameObject HeldPrimary
    {
        get
        {
            foreach (GameObject arma in Principales())
                if (arma.activeInHierarchy) return arma;
            return null;
        }
    }

    // Ficha de la tienda de un arma principal (Mitre o ArmaDeFuego).
    public static ShopItem FichaDe(GameObject arma)
    {
        if (arma == null) return null;
        ArmaDeFuego fuego = arma.GetComponent<ArmaDeFuego>();
        if (fuego != null) return fuego.shopItem;
        Mitre mitre = arma.GetComponent<Mitre>();
        return mitre != null ? mitre.shopItem : null;
    }

    void OnLoadoutChanged()
    {
        GameObject principal = PrimaryObj;
        if (principal != null && principal != teniaPrincipal)
        {
            // Recién comprada: cargador lleno y reserva completa, y se saca al cerrar la tienda (US 077, CA1).
            Rellenar(principal);
            CambiarA(Pendiente.Principal);
        }
        else if (principal == null && teniaPrincipal != null)
        {
            // Vendida, deshecha o perdida al morir: si estaba en la mano, se vuelve a la pistola.
            if (pendiente == Pendiente.Principal) pendiente = Pendiente.Nada;
            if (teniaPrincipal.activeSelf) CambiarA(Pendiente.Pistola);
        }
        teniaPrincipal = principal;
    }

    static void Rellenar(GameObject arma)
    {
        ArmaDeFuego fuego = arma.GetComponent<ArmaDeFuego>();
        if (fuego != null) fuego.Refill();
        Mitre mitre = arma.GetComponent<Mitre>();
        if (mitre != null) mitre.Refill();
    }

    void CambiarA(Pendiente arma)
    {
        if (!enabled) { pendiente = arma; return; }
        if (arma == Pendiente.Principal) EquipPrimary(); else EquipPistol();
    }

    void Update()
    {
        // Configuración inicial al arrancar: con el arma principal si la tiene; si no, con la pistola
        if (!setupInicialListo && meleeScript != null && meleeScript.CurrentViewModel != null)
        {
            if (PrimaryObj != null) EquipPrimary(); else EquipPistol();
            setupInicialListo = true;
        }

        // Tecla 1: arma principal, solo si tiene una
        if (KeyBindings.Down(GameAction.ArmaPrincipal) && PrimaryObj != null)
        {
            EquipPrimary();
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

        // Tecla 4: Granadas. Cada vez que se aprieta pasa a la siguiente que tenga; sin granadas no hace nada (US 079, CA1 y CA6)
        if (KeyBindings.Down(GameAction.Granadas) && granadas != null && granadas.SelectNext())
        {
            EquipGrenade();
        }
    }

    void EquipPrimary()
    {
        GuardarGranada();

        GameObject principal = PrimaryObj;
        GuardarPrincipales(principal);
        if (principal != null) principal.SetActive(true);
        if (pistolObj != null) pistolObj.SetActive(false);
        if (meleeScript != null && meleeScript.CurrentViewModel != null)
        {
            meleeScript.CurrentViewModel.SetActive(false);
        }

        // Cada arma tiene su velocidad: el Mitre al 92 %, el Urquiza al 97 %...
        ApplySpeedMultiplier(VelocidadCon(principal));
    }

    void EquipPistol()
    {
        GuardarGranada();

        GuardarPrincipales(null);
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
        GuardarGranada();

        GuardarPrincipales(null);
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

    // Guarda la granada que tenía en la mano (si tenía) al sacar otra arma.
    void GuardarGranada()
    {
        GrenadeEquipped = false;
        if (grenadeObj != null) grenadeObj.SetActive(false);
        if (granadas != null) granadas.Deselect();
    }

    void EquipGrenade()
    {
        GuardarPrincipales(null);
        if (pistolObj != null) pistolObj.SetActive(false);

        if (meleeScript != null && meleeScript.CurrentViewModel != null)
        {
            meleeScript.CurrentViewModel.SetActive(false);
        }

        if (grenadeObj != null)
        {
            grenadeObj.SetActive(true);
        }

        // Indica que la granada está equipada
        GrenadeEquipped = true;

        // Velocidad normal mientras se sostiene la granada
        ApplySpeedMultiplier(1.0f);
    }

    // Guarda todas las armas principales menos la que se va a sacar.
    void GuardarPrincipales(GameObject salvo)
    {
        foreach (GameObject arma in Principales())
            if (arma != salvo) arma.SetActive(false);
    }

    static float VelocidadCon(GameObject arma)
    {
        if (arma == null) return 1f;
        ArmaDeFuego fuego = arma.GetComponent<ArmaDeFuego>();
        if (fuego != null) return fuego.SpeedMultiplier;
        Mitre mitre = arma.GetComponent<Mitre>();
        return mitre != null ? mitre.speedMultiplier : 0.92f;
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
