using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// Soltar y levantar armas (US 184), en el jugador de esta computadora. WeaponSwitcher lo agrega solo.
//   CA1: solo en el modo Táctico.
//   CA2 a CA4: con G suelta el arma de fuego que tiene en la mano, con sus balas, y saca la siguiente.
//   CA5 y CA6: pasando por encima de un arma la levanta si tiene vacío ese espacio; si no, mirándola de cerca
//              ve el aviso para soltar la suya.
//   CA7: la compra del arma soltada ya no se vende ni se deshace (PlayerLoadout.Drop).
//   CA8: al morir cae la principal, o la secundaria si no tenía.
//   CA11: al terminar la ronda se van todas las del piso.
// Multijugador: JugadorEnRed manda cada arma soltada a los demás y le pide al dueño de la sala levantar una, para
// que si dos llegan a la vez se la quede uno solo (CA10).
public class SoltarArmas : MonoBehaviour
{
    private const float TiroAdelante = 3f, TiroArriba = 1.5f; // m/s con que sale el arma al soltarla
    private const float AlcanceLevantar = 0.9f; // a cuánto de los pies se levanta (en horizontal)
    private const float AlcanceAviso = 2f, AnguloAviso = 20f;
    private const float SinVolverALevantar = 1f; // la que uno soltó no se levanta enseguida

    // Una arma que se soltó: lo que hace falta para que las demás computadoras la hagan caer igual.
    public struct Suelta
    {
        public int id;
        public ShopItem item;
        public Vector3 desde, velocidad;
        public float yaw;
        public int cargador, reserva;
    }

    // Jugador de esta computadora.
    public static SoltarArmas Local { get; private set; }
    // Online: lo prende JugadorEnRed. Las armas se levantan con permiso del dueño de la sala.
    public static bool EnRed;
    // Los ids de cada computadora no se pisan: JugadorEnRed pone uno distinto por jugador.
    public static int IdBase;
    private static int ultimoId;

    public static event System.Action<Suelta> Soltada;
    public static event System.Action<int> Pedida;

    public static bool Activo => MatchSettings.Mode == GameMode.Tactico;

    private PlayerLoadout loadout;
    private WeaponSwitcher switcher;
    private HealthSystem vida;
    private CharacterController cuerpo;
    private Transform duenio, camara;
    private AudioMixerGroup grupo;
    private int propia;
    private float propiaHasta;
    private string aviso;

    private void Awake()
    {
        loadout = GetComponentInParent<PlayerLoadout>();
        switcher = GetComponentInParent<WeaponSwitcher>();
        vida = GetComponentInParent<HealthSystem>();
        cuerpo = GetComponentInParent<CharacterController>();
        duenio = cuerpo != null ? cuerpo.transform : transform.root;
        camara = switcher != null ? switcher.transform : transform;
    }

    private void Start()
    {
        Local = this;
        // El grupo SFX del mezclador, el mismo de las armas (CA12 y CA13).
        Pistola pistola = duenio.GetComponentInChildren<Pistola>(true);
        grupo = pistola != null ? pistola.sfxGroup : null;
        ArmaEnPiso.Grupo = grupo;
        if (switcher != null)
        {
            foreach (GameObject arma in switcher.Principales()) ModeloReal.Registrar(FichaDe(arma), arma);
            foreach (GameObject arma in switcher.Secundarias()) ModeloReal.Registrar(FichaDe(arma), arma);
        }
        if (loadout != null) loadout.LosingEquipment += AlMorir;
        RondasTacticas.RondaTerminada += AlTerminarRonda;
        if (BuyPhase.Current != null) BuyPhase.Current.Started += ArmaEnPiso.QuitarTodas;
    }

    private void OnDestroy()
    {
        if (Local == this) Local = null;
        if (loadout != null) loadout.LosingEquipment -= AlMorir;
        RondasTacticas.RondaTerminada -= AlTerminarRonda;
        if (BuyPhase.Current != null) BuyPhase.Current.Started -= ArmaEnPiso.QuitarTodas;
        MostrarAviso(null);
    }

    private void AlTerminarRonda(int ganador, RondasTacticas.Motivo motivo) => ArmaEnPiso.QuitarTodas();

    private bool Vivo => vida == null || vida.currentHealth > 0;

    private void Update()
    {
        if (!Activo || loadout == null || switcher == null || !Vivo) { MostrarAviso(null); return; }

        if (!PauseMenu.IsPaused && !ShopUI.IsOpen && KeyBindings.Down(GameAction.Soltar)) SoltarLaDeLaMano();

        BuscarArmas();
    }

    // CA2: suelta el arma de fuego que tiene en la mano. El cuchillo y las granadas no se sueltan.
    public void SoltarLaDeLaMano()
    {
        if (!Activo || switcher == null || !Vivo) return;
        GameObject enMano = switcher.HeldPrimary != null ? switcher.HeldPrimary : switcher.HeldSecondary;
        if (enMano != null) Soltar(enMano, false);
    }

    // CA5 y CA6: levanta la que tiene debajo si su espacio está vacío; si no, avisa cuál está mirando.
    private void BuscarArmas()
    {
        Vector3 pies = Pies();
        Vector3 ojo = camara.position;
        ArmaEnPiso mirada = null;
        float mejorAngulo = AnguloAviso;
        foreach (ArmaEnPiso arma in new List<ArmaEnPiso>(ArmaEnPiso.Todas))
        {
            if (arma == null || arma.Pedida || arma.Item == null) continue;
            if (arma.Id == propia && Time.time < propiaHasta) continue;

            Vector3 hasta = arma.Centro - pies;
            float vertical = Mathf.Abs(hasta.y);
            hasta.y = 0f;
            if (hasta.magnitude <= AlcanceLevantar && vertical <= 1.2f && loadout.CanPickUp(arma.Item))
            {
                PedirLevantar(arma);
                continue;
            }

            Vector3 hacia = arma.Centro - ojo;
            float angulo = Vector3.Angle(camara.forward, hacia);
            if (hacia.magnitude <= AlcanceAviso && angulo < mejorAngulo && !loadout.CanPickUp(arma.Item))
            {
                mejorAngulo = angulo;
                mirada = arma;
            }
        }

        if (mirada == null) { MostrarAviso(null); return; }
        ShopItem tengo = mirada.Item.kind == ShopItemKind.PrimaryWeapon ? loadout.Primary : loadout.Secondary;
        MostrarAviso($"{ModeloReal.NombreDe(mirada.Item)}   ·   Soltá tu {ModeloReal.NombreDe(tengo)} con " +
                     $"<color=#F29A38>{KeyBindings.Label(GameAction.Soltar)}</color> para levantarla");
    }

    private void MostrarAviso(string texto)
    {
        if (texto == aviso) return;
        aviso = texto;
        MatchHud.SetPrompt(texto);
    }

    private void PedirLevantar(ArmaEnPiso arma)
    {
        if (!EnRed) { Levantar(arma); return; }
        arma.PedidaHasta = Time.time + 2f;
        Pedida?.Invoke(arma.Id);
    }

    // La levanta (sin red, o porque el dueño de la sala dijo que es suya): va a su espacio con sus balas, sin
    // cambiar lo que tiene en la mano (CA5), y suena como al sacar un arma (CA13).
    public void Levantar(ArmaEnPiso arma)
    {
        if (arma == null) return;
        Vector3 donde = arma.Centro;
        ShopItem item = arma.Item;
        int cargador = arma.Cargador, reserva = arma.Reserva;
        ArmaEnPiso.Quitar(arma.Id);

        // Si mientras tanto llenó ese espacio (por ejemplo, compró), el arma vuelve al piso.
        if (!loadout.PickUp(item))
        {
            Tirar(item, donde + Vector3.up * 0.3f, Vector3.zero, duenio.eulerAngles.y, cargador, reserva);
            return;
        }
        GameObject nueva = item.kind == ShopItemKind.PrimaryWeapon ? switcher.PrimaryObj : switcher.SecondaryObj;
        PonerBalas(nueva, cargador, reserva);
        ArmaEnPiso.Sonar(ConfigRed.Actual != null ? ConfigRed.Actual.sonidoLevantarArma : null, donde, grupo);
    }

    // CA8: al morir cae la principal, o la secundaria si no tenía. Las granadas y el escudo no.
    // Al cambiar de lado (US 032, CA5) también se pierde el equipo, pero vivo: ahí no cae nada.
    private void AlMorir()
    {
        if (!Activo || switcher == null || Vivo) return;
        GameObject arma = switcher.PrimaryObj != null ? switcher.PrimaryObj : switcher.SecondaryObj;
        if (arma != null) Soltar(arma, true);
    }

    // Suelta un arma: sale de su espacio y se tira hacia adelante (o cae a los pies, si murió) con las balas que
    // tenía. Cae con física y queda apoyada en lo que encuentre (ArmaEnPiso).
    private void Soltar(GameObject arma, bool alMorir)
    {
        ShopItem item = FichaDe(arma);
        if (item == null) return;
        IHudWeapon datos = arma.GetComponent<IHudWeapon>();
        int cargador = datos != null ? datos.Ammo : 0;
        int reserva = datos != null ? Mathf.Max(0, datos.Reserve) : 0;
        if (!loadout.Drop(item)) return; // CA4: WeaponSwitcher saca la siguiente

        Vector3 frente = camara.forward;
        frente.y = 0f;
        frente = frente.sqrMagnitude > 0.001f ? frente.normalized : duenio.forward;
        Vector3 desde = alMorir ? Pies() + Vector3.up * 0.8f : camara.position - Vector3.up * 0.3f + frente * 0.35f;
        // Con una pared pegada adelante, sale antes de atravesarla.
        if (!alMorir && Physics.Raycast(camara.position, frente, out RaycastHit pared, 0.6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && !pared.collider.transform.IsChildOf(duenio))
            desde = camara.position - Vector3.up * 0.3f + frente * Mathf.Max(0.05f, pared.distance - 0.25f);
        Vector3 velocidad = alMorir ? Vector3.up * 0.5f : frente * TiroAdelante + Vector3.up * TiroArriba;
        if (!alMorir && cuerpo != null) velocidad += cuerpo.velocity; // si va corriendo, sale con su velocidad

        int id = Tirar(item, desde, velocidad, Mathf.Atan2(frente.x, frente.z) * Mathf.Rad2Deg, cargador, reserva);
        propia = id;
        propiaHasta = Time.time + SinVolverALevantar;
    }

    // Ficha del arma. La Línea A del jugador puede no tenerla cargada: es la secundaria inicial de la tienda.
    private ShopItem FichaDe(GameObject arma)
    {
        ShopItem item = WeaponSwitcher.FichaDe(arma);
        if (item == null && switcher != null && arma == switcher.pistolObj && loadout.Catalog != null)
            item = loadout.Catalog.starterSecondary;
        return item;
    }

    // Crea el arma en el piso acá y se la avisa a los demás.
    private int Tirar(ShopItem item, Vector3 desde, Vector3 velocidad, float yaw, int cargador, int reserva)
    {
        var suelta = new Suelta
        {
            id = IdBase + (++ultimoId),
            item = item,
            desde = desde,
            velocidad = velocidad,
            yaw = yaw,
            cargador = cargador,
            reserva = reserva
        };
        ArmaEnPiso.Crear(suelta.id, item, desde, velocidad, yaw, cargador, reserva, true);
        Soltada?.Invoke(suelta);
        return suelta.id;
    }

    private Vector3 Pies()
    {
        if (cuerpo == null) return duenio.position;
        Vector3 centro = cuerpo.transform.TransformPoint(cuerpo.center);
        return centro - Vector3.up * (cuerpo.height * 0.5f * Mathf.Abs(cuerpo.transform.lossyScale.y));
    }

    // Las balas del arma levantada (CA3).
    private static void PonerBalas(GameObject arma, int cargador, int reserva)
    {
        if (arma == null) return;
        ArmaDeFuego fuego = arma.GetComponent<ArmaDeFuego>();
        if (fuego != null) { fuego.currentAmmo = cargador; fuego.reserveAmmo = reserva; }
        Mitre mitre = arma.GetComponent<Mitre>();
        if (mitre != null) { mitre.currentAmmo = cargador; mitre.reserveAmmo = reserva; }
        Pistola pistola = arma.GetComponent<Pistola>();
        if (pistola != null) { pistola.currentAmmo = cargador; pistola.reserveAmmo = reserva; }
    }
}
