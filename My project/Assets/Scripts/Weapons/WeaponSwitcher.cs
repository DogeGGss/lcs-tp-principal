using System.Collections.Generic;
using UnityEngine;

public class WeaponSwitcher : MonoBehaviour
{
    [Header("Referencias de Armas")]
    public GameObject mitreObj;        // Slot 1: Fusil Mitre
    [Tooltip("Las demás armas principales (Urquiza, Belgrano Sur, Roca...), cada una con su ArmaDeFuego. Con tienda, en el espacio 1 va la que se compró.")]
    public GameObject[] otrasPrincipales = new GameObject[0];
    public GameObject pistolObj;       // Slot 2: Pistola
    [Tooltip("Las demás armas secundarias (La Trochita...), cada una con su ArmaDeFuego. Con tienda, en el espacio 2 va la que se compró; si no compró ninguna, La Porteña (pistolObj).")]
    public GameObject[] otrasSecundarias = new GameObject[0];
    public MeleeWeaponHolder meleeScript; // Slot 3: Cuchillo
    public GameObject grenadeObj;       // Slot 4: Granada
    // Slot 5: el dispositivo del Modo Táctico (US 130). Lo crea y lo pone DispositivoTactico; se saca con la
    // tecla 5 solo mientras LlevaDispositivo.
    [System.NonSerialized] public GameObject dispositivoObj;
    [System.NonSerialized] public bool LlevaDispositivo;
    // Mientras planta o desactiva (US 131 y US 132) no se cambia de arma.
    [System.NonSerialized] public bool Ocupado;

    private bool setupInicialListo = false;

    // Tienda (US 077): con tienda en la escena el arma principal se saca solo si se compró.
    private enum Pendiente { Nada, Principal, Pistola }
    private PlayerLoadout loadout;
    private GrenadeThrower granadas;
    private bool hayTienda;
    private GameObject teniaPrincipal;
    private GameObject teniaSecundaria;
    private Pendiente pendiente = Pendiente.Nada;

    // Indica si actualmente está equipada la granada
    public bool GrenadeEquipped { get; private set; }

    // El dispositivo está en la mano (US 130).
    public bool DispositivoEquipado => dispositivoObj != null && dispositivoObj.activeSelf;

    // Las granadas del jugador (cuál tiene en la mano): el HUD la muestra (US 073).
    public GrenadeThrower Granadas => granadas;

    void Awake()
    {
        loadout = GetComponentInParent<PlayerLoadout>();

        // Granadas (US 079): el manejo vive en GrenadeThrower; se agrega solo si el jugador no lo tiene.
        granadas = GetComponentInParent<GrenadeThrower>();
        if (granadas == null) granadas = gameObject.AddComponent<GrenadeThrower>();

        // Soltar y levantar armas (US 184): igual, se agrega solo.
        if (GetComponentInParent<SoltarArmas>() == null) gameObject.AddComponent<SoltarArmas>();
        // Brazos de primera persona pegados a la cámara con la Línea A (si el jugador tiene brazos de primera persona).
        if (GetComponentInParent<BrazosEnCamara>() == null) gameObject.AddComponent<BrazosEnCamara>();
    }

    void Start()
    {
        hayTienda = FindAnyObjectByType<ShopUI>() != null;
        teniaPrincipal = PrimaryObj;
        teniaSecundaria = SecondaryObj;
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

    // Todas las armas secundarias que tiene el jugador en la mano, empezando por la Línea A (US 072).
    public List<GameObject> Secundarias()
    {
        var lista = new List<GameObject>();
        if (pistolObj != null) lista.Add(pistolObj);
        if (otrasSecundarias != null)
            foreach (GameObject arma in otrasSecundarias)
                if (arma != null) lista.Add(arma);
        return lista;
    }

    // El arma secundaria que tiene: la que compró en la tienda (por ejemplo, la Línea H) o, si no compró
    // ninguna, la Línea A. Si la soltó (US 184), ninguna.
    public GameObject SecondaryObj
    {
        get
        {
            if (hayTienda && loadout != null && loadout.Secondary == null) return null;
            if (hayTienda && loadout != null && otrasSecundarias != null)
                foreach (GameObject arma in otrasSecundarias)
                    if (arma != null && FichaDe(arma) == loadout.Secondary) return arma;
            return pistolObj;
        }
    }

    // El arma secundaria que está en la mano, o null.
    public GameObject HeldSecondary
    {
        get
        {
            foreach (GameObject arma in Secundarias())
                if (arma.activeInHierarchy) return arma;
            return null;
        }
    }

    // Ficha de la tienda de un arma (Mitre, ArmaDeFuego o la Línea A).
    public static ShopItem FichaDe(GameObject arma)
    {
        if (arma == null) return null;
        ArmaDeFuego fuego = arma.GetComponent<ArmaDeFuego>();
        if (fuego != null) return fuego.shopItem;
        Mitre mitre = arma.GetComponent<Mitre>();
        if (mitre != null) return mitre.shopItem;
        Pistola pistola = arma.GetComponent<Pistola>();
        return pistola != null ? pistola.shopItem : null;
    }

    void OnLoadoutChanged()
    {
        // Levantada del piso (US 184, CA5): queda con las balas que tenía y se sigue con lo que hay en la mano.
        if (loadout.PickingUp)
        {
            teniaPrincipal = PrimaryObj;
            teniaSecundaria = SecondaryObj;
            return;
        }

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

        // Secundaria (US 072): al comprar la Línea H reemplaza a la Línea A y se saca; al venderla o perderla
        // vuelve la Línea A, y si estaba en la mano se cambia en el momento. Si la soltó, se pasa al cuchillo.
        GameObject secundaria = SecondaryObj;
        if (secundaria != teniaSecundaria)
        {
            bool estabaEnMano = teniaSecundaria != null && teniaSecundaria.activeSelf;
            bool nueva = secundaria != null && secundaria != pistolObj;
            if (nueva) Rellenar(secundaria);
            if (nueva || estabaEnMano) CambiarA(Pendiente.Pistola);
            teniaSecundaria = secundaria;
        }
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

        if (Ocupado) return;

        // Tecla 1: arma principal, solo si tiene una
        if (KeyBindings.Down(GameAction.ArmaPrincipal) && PrimaryObj != null)
        {
            EquipPrimary();
        }

        // Tecla 2: Pistola (Arma secundaria), solo si tiene una (la puede haber soltado, US 184)
        if (KeyBindings.Down(GameAction.ArmaSecundaria) && SecondaryObj != null)
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

        // Tecla 5: el dispositivo, solo si lo lleva (US 130)
        if (KeyBindings.Down(GameAction.Dispositivo) && LlevaDispositivo && dispositivoObj != null)
        {
            SacarDispositivo();
        }
    }

    // US 130: el dispositivo en la mano (con la tecla 5, o al plantar, US 131).
    public void SacarDispositivo()
    {
        if (dispositivoObj == null) return;
        GuardarGranada();
        GuardarPrincipales(null);
        GuardarSecundarias(null);
        if (meleeScript != null && meleeScript.CurrentViewModel != null) meleeScript.CurrentViewModel.SetActive(false);
        dispositivoObj.SetActive(true);
        ApplySpeedMultiplier(1.0f);
    }

    // Guarda todo lo que tiene en la mano (al desactivar el dispositivo, US 132, no se dispara).
    public void GuardarTodo()
    {
        GuardarGranada();
        GuardarDispositivo();
        GuardarPrincipales(null);
        GuardarSecundarias(null);
        if (meleeScript != null && meleeScript.CurrentViewModel != null) meleeScript.CurrentViewModel.SetActive(false);
    }

    // Vuelve al arma principal si la tiene; si no, a la secundaria (o al cuchillo).
    public void VolverAlArma()
    {
        if (PrimaryObj != null) EquipPrimary(); else EquipPistol();
    }

    void GuardarDispositivo()
    {
        if (dispositivoObj != null) dispositivoObj.SetActive(false);
    }

    void EquipPrimary()
    {
        GuardarGranada();
        GuardarDispositivo();

        GameObject principal = PrimaryObj;
        GuardarPrincipales(principal);
        if (principal != null) principal.SetActive(true);
        GuardarSecundarias(null);
        if (meleeScript != null && meleeScript.CurrentViewModel != null)
        {
            meleeScript.CurrentViewModel.SetActive(false);
        }

        // Cada arma tiene su velocidad: el Mitre al 92 %, el Urquiza al 97 %...
        ApplySpeedMultiplier(VelocidadCon(principal));
    }

    void EquipPistol()
    {
        GameObject secundaria = SecondaryObj; // la Línea A o la que compró (US 072)
        // Sin secundaria (la soltó, US 184, CA4), la siguiente arma es el cuchillo.
        if (secundaria == null && meleeScript != null) { EquipKnife(); return; }

        GuardarGranada();
        GuardarDispositivo();

        GuardarPrincipales(null);
        GuardarSecundarias(secundaria);
        if (secundaria != null) secundaria.SetActive(true);
        if (meleeScript != null && meleeScript.CurrentViewModel != null)
        {
            meleeScript.CurrentViewModel.SetActive(false);
        }

        // Cada secundaria tiene su velocidad (la Línea A y la Línea H, al 100 %)
        ApplySpeedMultiplier(secundaria != null && secundaria != pistolObj ? VelocidadCon(secundaria) : 1.0f);
    }

    void EquipKnife()
    {
        GuardarGranada();
        GuardarDispositivo();

        GuardarPrincipales(null);
        GuardarSecundarias(null);
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
        GuardarDispositivo();
        GuardarPrincipales(null);
        GuardarSecundarias(null);

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

    // Guarda todas las armas secundarias menos la que se va a sacar.
    void GuardarSecundarias(GameObject salvo)
    {
        foreach (GameObject arma in Secundarias())
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
