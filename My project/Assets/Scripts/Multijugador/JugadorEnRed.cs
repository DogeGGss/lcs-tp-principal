using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.Audio;

// Un jugador de una partida online (US 025, US 028, US 029 y US 030).
// En la computadora de su dueño es el jugador de siempre: manda su estado (posición, mirada, animación, arma,
// vida y escudo) y avisa sus disparos, cuchillazos, muerte y reaparición.
// En las demás es una copia del Player.prefab sin cámara, controles ni pausa, que repite lo que le llega.
// La vida la decide el dueño: si alguien le pega a la copia, el daño se le manda a él (US 029).
public class JugadorEnRed : MonoBehaviourPun, IPunObservable
{
    private const float AlcanceTrazadora = 300f;
    private const byte Agachado = 1, Corriendo = 2, Cayendo = 4, Invulnerable = 8, Despacio = 16;
    // Arma en la mano: las principales van desde PrimeraPrincipal, en el orden de WeaponSwitcher.Principales(), y las
    // demás secundarias (Línea H...) desde PrimeraSecundaria, en el orden de WeaponSwitcher.otrasSecundarias (US 072).
    private const byte SinArma = 0, ArmaPistola = 2, ArmaCuchillo = 3, PrimeraSecundaria = 4, PrimeraPrincipal = 10;
    // El dispositivo del Modo Táctico en la mano (US 130).
    private const byte ArmaDispositivo = 1;
    // La granada en la mano (US 182): PrimeraGranada más su lugar en la tienda.
    private const byte PrimeraGranada = 200;

    private HealthSystem vida;
    private Animator animador;
    private Transform camara;     // "Main Camera": su giro vertical es hacia dónde mira
    private List<GameObject> principales = new List<GameObject>();
    private GameObject pistola;
    private List<GameObject> secundarias = new List<GameObject>(); // las demás secundarias (sin la Línea A)
    private MeleeWeaponHolder cuchillo;
    private WeaponSwitcher cambioLocal; // dueño: para saber qué granada tiene en la mano
    private PartidaEnRed partida;
    private bool muerto;

    // ---------- Dueño ----------
    private PlayerMovement movimiento;
    private byte saltos, aterrizajes;
    private float finInvulnerable;
    private int ultimoAtacante;
    private string ultimaArma = "";   // con qué lo dañaron por última vez, si no fue el arma en la mano (una granada)
    private ShopCatalog catalogo;      // qué granada es (US 073): la tienda está en el mismo orden en todas las computadoras
    private int ultimaGranada;
    private readonly Dictionary<int, Grenade1> granadasRemotas = new Dictionary<int, Grenade1>();
    private readonly List<Behaviour> bloqueados = new List<Behaviour>();

    // ---------- Copia ----------
    private bool recibido;
    private Vector3 posicionRed, velocidadRed;
    private double tiempoRed;
    private float yawRed, pitchRed, velXRed, velZRed;
    private byte banderasRed, armaRed, saltosRed, aterrizajesRed;
    private byte saltosVistos, aterrizajesVistos, armaVista = 255;
    private float alturaParado = 0.75f, alturaAgachado = 0.3f;
    private GameObject cuchilloListo;
    private Transform modelo;
    private Collider[] colisiones;
    private AudioSource sonido;
    private AudioClip sonidoPistola, sonidoCuchillo;
    private readonly List<AudioClip> sonidosPrincipales = new List<AudioClip>();
    private readonly List<AudioClip> sonidosSecundarias = new List<AudioClip>();
    private Coroutine caida;
    private Pasos pasos; // US 001 y US 198: sus pasos se escuchan desde donde está (salvo agachado o despacio)

    // US 182: el arma a tamaño real en la mano derecha, una por código de arma (las de primera persona no se ven).
    private class ModeloEnMano { public GameObject go; public Bounds limites; }
    private readonly Dictionary<byte, ModeloEnMano> enLaMano = new Dictionary<byte, ModeloEnMano>();
    private Transform manoDerecha, soporteMano;

    public bool Vivo => !muerto;

    // US 138, CA5: para saber hasta cuándo se puede cambiar de equipo en Deathmatch.
    public const float TopeConTienda = 10f; // muerto y con la tienda abierta, como mucho se espera esto
    private float reaparecioEn = -100f, finEspera, topeEspera;
    private bool atacoDesdeReaparicion;
    public float ReaparecioEn => reaparecioEn;
    public bool AtacoDesdeReaparicion => atacoDesdeReaparicion;
    /// <summary>Muerto: segundos que faltan para reaparecer (0 si ya solo espera a que cierre la tienda).</summary>
    public float EsperaRestante => Mathf.Max(0f, finEspera - Time.time);
    /// <summary>Muerto: segundos que faltan para reaparecer sí o sí, aunque la tienda siga abierta.</summary>
    public float TopeRestante => Mathf.Max(0f, topeEspera - Time.time);
    /// <summary>Empieza a correr el rato en que todavía se puede cambiar de equipo (al reaparecer o al empezar el combate).</summary>
    public void AbrirVentanaDeEquipo()
    {
        reaparecioEn = Time.time;
        atacoDesdeReaparicion = false;
    }

    /// <summary>US 133: el punto de vista del jugador (en la copia, con la mirada que llega por la red).</summary>
    public Transform Ojos => camara != null ? camara : transform;
    public string Nombre => photonView != null && photonView.Owner != null ? photonView.Owner.NickName : "Jugador";

    // Número de jugador de Photon y equipo táctico (US 031).
    public int Actor => photonView != null && photonView.Owner != null ? photonView.Owner.ActorNumber : -1;
    public int Equipo => EquiposTacticos.DeActor(Actor);

    // =====================================================================
    // Dueño
    // =====================================================================

    public void IniciarLocal(PartidaEnRed partida)
    {
        this.partida = partida;
        vida = GetComponent<HealthSystem>();
        movimiento = GetComponent<PlayerMovement>();
        animador = GetComponentInChildren<Animator>();
        cuchillo = GetComponent<MeleeWeaponHolder>();
        WeaponSwitcher cambio = GetComponentInChildren<WeaponSwitcher>(true);
        camara = cambio != null ? cambio.transform : transform.Find("Main Camera");
        if (cambio != null) { principales = cambio.Principales(); pistola = cambio.pistolObj; secundarias = OtrasSecundarias(cambio); }
        cambioLocal = cambio;
        PlayerLoadout carga = GetComponentInChildren<PlayerLoadout>(true);
        catalogo = carga != null ? carga.Catalog : null;

        // US 184: las armas del piso se mandan a los demás y se levantan con permiso del dueño de la sala.
        SoltarArmas.EnRed = true;
        // US 195: el que vuelve a la partida no repite los ids de las que soltó antes de desconectarse.
        SoltarArmas.IdBase = Actor * 100000 + (Reconexion.Volvio ? 40000 + (PhotonNetwork.ServerTimestamp & 0x3FFF) : 0);
        SoltarArmas.Soltada += AlSoltarArma;
        SoltarArmas.Pedida += AlPedirArma;
        ArmaEnPiso.Quieta += AlQuedarQuieta;

        if (movimiento != null)
        {
            movimiento.Jumped += () => saltos++;
            movimiento.Landed += () => aterrizajes++;
        }
        if (vida != null) vida.Died += AlMorir;
        WeaponFire.Fired += AlDisparar;
        MeleeAttack.Swung += AlAcuchillar;
        GrenadeThrower.Thrown += AlLanzarGranada;
        Grenade1.Exploded += AlExplotarGranada;
    }

    private void OnDestroy()
    {
        WeaponFire.Fired -= AlDisparar;
        MeleeAttack.Swung -= AlAcuchillar;
        GrenadeThrower.Thrown -= AlLanzarGranada;
        Grenade1.Exploded -= AlExplotarGranada;
        SoltarArmas.Soltada -= AlSoltarArma;
        SoltarArmas.Pedida -= AlPedirArma;
        ArmaEnPiso.Quieta -= AlQuedarQuieta;
        if (vida != null) vida.Died -= AlMorir;
        if (photonView != null && photonView.IsMine) SoltarArmas.EnRed = false;
    }

    private void AlDisparar(Transform tirador, Vector3 origen, Vector3[] direcciones)
    {
        if (tirador != transform || muerto) return;
        if (vida != null) vida.Invulnerable = false; // US 137, CA5: disparar corta la invulnerabilidad
        atacoDesdeReaparicion = true;

        var plano = new float[direcciones.Length * 3];
        for (int i = 0; i < direcciones.Length; i++)
        {
            plano[i * 3] = direcciones[i].x;
            plano[i * 3 + 1] = direcciones[i].y;
            plano[i * 3 + 2] = direcciones[i].z;
        }
        photonView.RPC(nameof(RpcDisparo), RpcTarget.Others, origen, plano, ArmaEnMano());
    }

    private void AlAcuchillar(MeleeAttack ataque)
    {
        if (ataque.gameObject != gameObject || muerto) return;
        if (vida != null) vida.Invulnerable = false;
        atacoDesdeReaparicion = true;
        photonView.RPC(nameof(RpcCuchillo), RpcTarget.Others);
    }

    // US 073: este jugador tiró una granada; en las demás computadoras se ve una igual (RpcGranada).
    private void AlLanzarGranada(Transform tirador, Grenade1 granada)
    {
        if (tirador != transform || muerto || granada == null) return;
        int indice = catalogo != null ? catalogo.items.IndexOf(granada.Item) : -1;
        if (indice < 0 || indice > byte.MaxValue) return;
        if (vida != null) vida.Invulnerable = false; // como disparar (US 137, CA5)
        atacoDesdeReaparicion = true;
        granada.netId = ++ultimaGranada;
        photonView.RPC(nameof(RpcGranada), RpcTarget.Others, granada.netId, (byte)indice,
            granada.LaunchOrigin, granada.LaunchDirection, granada.LaunchInherited);
    }

    // Explotó una granada de este jugador: en las demás computadoras explota en el mismo lugar.
    private void AlExplotarGranada(Grenade1 granada, Vector3 centro)
    {
        if (granada == null || granada.thrower != transform || granada.netId == 0) return;
        int indice = catalogo != null ? catalogo.items.IndexOf(granada.Item) : -1;
        photonView.RPC(nameof(RpcExplosionGranada), RpcTarget.Others, granada.netId, (byte)Mathf.Clamp(indice, 0, byte.MaxValue), centro);
    }

    // US 184, CA10: este jugador soltó un arma; en las demás computadoras cae igual, en el mismo lugar.
    private void AlSoltarArma(SoltarArmas.Suelta suelta)
    {
        int indice = catalogo != null ? catalogo.items.IndexOf(suelta.item) : -1;
        if (indice < 0 || indice > byte.MaxValue) return;
        photonView.RPC(nameof(RpcArmaSoltada), RpcTarget.Others, suelta.id, (byte)indice, suelta.desde, suelta.velocidad,
            suelta.yaw, suelta.cargador, suelta.reserva);
    }

    [PunRPC]
    private void RpcArmaSoltada(int id, byte indice, Vector3 desde, Vector3 velocidad, float yaw, int cargador, int reserva)
    {
        ShopItem item = FichaDe(indice);
        if (item != null) ArmaEnPiso.Crear(id, item, desde, velocidad, yaw, cargador, reserva, false);
    }

    // CA10: la que soltó este jugador ya se quedó quieta; en las demás computadoras queda en el mismo lugar.
    private void AlQuedarQuieta(ArmaEnPiso arma)
    {
        photonView.RPC(nameof(RpcArmaQuieta), RpcTarget.Others, arma.Id, arma.transform.position, arma.transform.rotation);
    }

    [PunRPC]
    private void RpcArmaQuieta(int id, Vector3 posicion, Quaternion giro)
    {
        ArmaEnPiso arma = ArmaEnPiso.Buscar(id);
        if (arma != null) arma.Asentar(posicion, giro);
    }

    // CA10: para levantar un arma se le pide al dueño de la sala; si dos la piden a la vez, se la da al primero.
    private void AlPedirArma(int id)
    {
        photonView.RPC(nameof(RpcPedirArma), RpcTarget.MasterClient, id);
    }

    [PunRPC]
    private void RpcPedirArma(int id, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || ArmaEnPiso.Buscar(id) == null || info.Sender == null) return;
        // A todos, también acá mismo y en el momento: si llega otro pedido, el arma ya no está.
        photonView.RPC(nameof(RpcArmaLevantada), RpcTarget.All, id, info.Sender.ActorNumber);
    }

    [PunRPC]
    private void RpcArmaLevantada(int id, int actor)
    {
        ArmaEnPiso arma = ArmaEnPiso.Buscar(id);
        if (arma == null) return;
        if (actor == PhotonNetwork.LocalPlayer.ActorNumber && SoltarArmas.Local != null)
        {
            SoltarArmas.Local.Levantar(arma); // va a su espacio con sus balas, y suena
            return;
        }
        ArmaEnPiso.Sonar(ConfigRed.Actual != null ? ConfigRed.Actual.sonidoLevantarArma : null, arma.Centro, ArmaEnPiso.Grupo);
        ArmaEnPiso.Quitar(id);
    }

    // US 029: el daño que le hicieron a la copia de este jugador en otra computadora. arma: con qué, si no fue el
    // arma que el atacante tiene en la mano (por ejemplo, una granada); va al aviso de baja (US 057).
    [PunRPC]
    private void RpcDanio(int danio, int atacante, bool cabeza, string arma, Vector3 origen)
    {
        if (!photonView.IsMine || muerto || vida == null) return;
        if (EquiposTacticos.SonAliados(atacante, Actor)) return; // US 031, CA6: por las dudas, también acá
        ultimoAtacante = atacante;
        ultimaArma = arma ?? "";
        HealthSystem.DamageOrigin = origen; // US 192, CA7: desde dónde dispararon (o dónde explotó la granada)
        vida.TakeDamage(danio, cabeza);
    }

    // US 030: los demás lo ven caer. En Deathmatch reaparece; en Táctico espera la ronda siguiente (US 032).
    private void AlMorir()
    {
        if (muerto) return;
        muerto = true;
        bool cabeza = vida != null && vida.KilledByHeadshot;
        photonView.RPC(nameof(RpcMurio), RpcTarget.Others, ultimoAtacante, cabeza, ultimaArma);
        // Si murió con la tienda abierta se cierra antes de trabar: al cerrarse después devolvería las armas a un muerto.
        if (MatchSettings.Mode == GameMode.Deathmatch) ShopUI.Cerrar();
        Bloquear(true);
        AvisarBaja(ultimoAtacante, cabeza, ultimaArma); // US 057, CA4

        Player asesino = ultimoAtacante != 0 && PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.GetPlayer(ultimoAtacante) : null;
        string titulo = asesino != null ? $"Te eliminó {asesino.NickName}" : "Te eliminaron";

        if (MatchSettings.Mode == GameMode.Deathmatch)
            StartCoroutine(Reaparecer(titulo, ConfigRed.Actual != null ? ConfigRed.Actual.reaparicion : 3f));
        // US 033: si la ronda ya terminó (por ejemplo, lo eliminó la explosión), el cartel de fin de ronda ya lo explica.
        else if (RondasTacticas.Actual == null || RondasTacticas.Actual.FaseActual == RondasTacticas.Fase.Combate)
            partida.Aviso(titulo, "Reaparecés en la ronda siguiente.");
    }

    private IEnumerator Reaparecer(string titulo, float espera)
    {
        float fin = Time.time + espera;
        finEspera = fin;
        topeEspera = Time.time + Mathf.Max(espera, TopeConTienda);
        string tecla = KeyBindings.Label(GameAction.Tienda);
        while (Time.time < fin)
        {
            partida.Aviso(titulo, $"Reaparecés en {Mathf.CeilToInt(fin - Time.time)}. Apretá {tecla} para cambiar de equipo");
            yield return null;
        }
        // US 138, CA5: si está eligiendo equipo, lo espera hasta que cierre la tienda (con un tope).
        while (ShopUI.IsOpen && Time.time < topeEspera && !PartidaDeathmatch.YaTermino) yield return null;
        ShopUI.Cerrar();
        partida.Aviso(null);
        if (PartidaDeathmatch.YaTermino) yield break; // la partida terminó mientras esperaba: ya no reaparece

        Pose punto = partida.PuntoDeReaparicion();
        Teletransportar(transform, punto.position, punto.rotation);
        vida.Revive();
        foreach (Pistola arma in GetComponentsInChildren<Pistola>(true)) arma.currentAmmo = arma.maxAmmo;
        foreach (Mitre arma in GetComponentsInChildren<Mitre>(true)) arma.Refill();
        foreach (ArmaDeFuego arma in GetComponentsInChildren<ArmaDeFuego>(true)) arma.Refill();
        muerto = false;
        ultimoAtacante = 0;
        ultimaArma = "";
        Bloquear(false);
        AbrirVentanaDeEquipo();

        float segundos = ConfigRed.Actual != null ? ConfigRed.Actual.invulnerabilidad : 2f;
        vida.Invulnerable = segundos > 0f;
        finInvulnerable = Time.time + segundos;

        photonView.RPC(nameof(RpcReaparecio), RpcTarget.Others, punto.position, punto.rotation.eulerAngles.y);
    }

    /// <summary>
    /// US 032, CA4 y CA7: arranca una ronda nueva del Táctico. Todos vuelven a su base; el que estaba muerto
    /// revive (sin escudo y con lo que le quedó después de morir) y el que estaba vivo conserva su escudo.
    /// </summary>
    public void EmpezarRonda(Pose punto)
    {
        if (photonView == null || !photonView.IsMine || vida == null) return;
        partida.Aviso(null);
        Teletransportar(transform, punto.position, punto.rotation);
        if (muerto) vida.Revive();
        else vida.SetState(vida.maxHealth, vida.currentShield);
        foreach (Pistola arma in GetComponentsInChildren<Pistola>(true)) arma.currentAmmo = arma.maxAmmo;
        foreach (Mitre arma in GetComponentsInChildren<Mitre>(true)) arma.Refill();
        foreach (ArmaDeFuego arma in GetComponentsInChildren<ArmaDeFuego>(true)) arma.Refill();
        muerto = false;
        ultimoAtacante = 0;
        ultimaArma = "";
        Bloquear(false);
        vida.Invulnerable = false;
        photonView.RPC(nameof(RpcReaparecio), RpcTarget.Others, punto.position, punto.rotation.eulerAngles.y);
    }

    /// <summary>
    /// US 195, CA3: volvió a la partida después de desconectarse. En el Táctico, si la ronda ya empezó, espera la
    /// siguiente como si hubiera muerto (sin aviso de baja). En Deathmatch reaparece enseguida, como al morir (US 137).
    /// </summary>
    public void AlVolver()
    {
        if (photonView == null || !photonView.IsMine || vida == null) return;
        if (MatchSettings.Mode == GameMode.Tactico && RondasTacticas.RondaEmpezada)
        {
            QuedarAfuera();
            partida.Aviso("Volviste a la partida", "Reaparecés en la ronda siguiente.");
        }
        else if (MatchSettings.Mode == GameMode.Deathmatch && PartidaDeathmatch.EnCombate)
        {
            QuedarAfuera();
            StartCoroutine(Reaparecer("Volviste a la partida", ConfigRed.Actual != null ? ConfigRed.Actual.reaparicion : 3f));
        }
    }

    // Como muerto, pero sin morir: no hay aviso de baja ni suma una muerte, y no se le cae nada. Los demás lo ven
    // afuera porque le llega la vida en 0 (OnPhotonSerializeView).
    private void QuedarAfuera()
    {
        muerto = true;
        vida.SetState(0, 0);
        Bloquear(true);
        StartCoroutine(SinVida());
    }

    // La vida del jugador se llena en su Start, que puede correr después: se deja en 0 de nuevo.
    private IEnumerator SinVida()
    {
        yield return null;
        if (muerto && vida != null) vida.SetState(0, 0);
    }

    /// <summary>
    /// US 195, CA4 (solo el anfitrión): el arma de otro jugador que se fue vivo cae donde estaba, en todas las
    /// computadoras. Es la que cae al morir (US 184, CA8): la principal, o la secundaria si no tenía. Las conoce por lo
    /// que publicó en la tienda; las balas, llenas.
    /// </summary>
    public void SoltarArmaDe(JugadorEnRed otro)
    {
        if (otro == null || catalogo == null || otro.photonView == null || otro.photonView.Owner == null) return;
        Player dueno = otro.photonView.Owner;
        int indice = Indice(dueno, ShopUI.PropPrincipal);
        if (indice < 0) indice = Indice(dueno, ShopUI.PropSecundaria);
        ShopItem item = indice >= 0 && indice <= byte.MaxValue ? FichaDe((byte)indice) : null;
        if (item == null) return;

        var suelta = new SoltarArmas.Suelta
        {
            id = otro.Actor * 100000 + 90000 + (++soltadasPorOtros % 10000), // no se pisa con las que soltó él (SoltarArmas)
            item = item,
            desde = otro.transform.position + Vector3.up * 0.8f,
            velocidad = Vector3.up * 0.5f,
            yaw = otro.transform.eulerAngles.y,
            cargador = item.magazine > 0 ? item.magazine : -1,
            reserva = item.magazine > 0 ? item.reserve : -1
        };
        // Propia: cuando se queda quieta, esta computadora les avisa a las demás dónde quedó (AlQuedarQuieta).
        ArmaEnPiso.Crear(suelta.id, item, suelta.desde, suelta.velocidad, suelta.yaw, suelta.cargador, suelta.reserva, true);
        AlSoltarArma(suelta);
    }

    private int soltadasPorOtros;

    private static int Indice(Player jugador, string clave) =>
        jugador.CustomProperties.TryGetValue(clave, out object v) && v is int n ? n : -1;

    /// <summary>US 033: lo elimina la explosión del dispositivo (también lo usa la prueba solo).</summary>
    public void Eliminar()
    {
        if (photonView == null || !photonView.IsMine || muerto || vida == null) return;
        ultimoAtacante = 0;
        ultimaArma = "";
        vida.Invulnerable = false;
        vida.TakeDamage(vida.currentHealth + vida.currentShield + 1);
    }

    // Muerto no se mueve, no dispara ni cambia de arma; puede seguir mirando alrededor.
    private void Bloquear(bool bloquear)
    {
        if (!bloquear)
        {
            foreach (Behaviour componente in bloqueados)
                if (componente != null) componente.enabled = true;
            bloqueados.Clear();
            return;
        }
        bloqueados.Clear();
        foreach (Behaviour componente in GetComponentsInChildren<Behaviour>(true))
            if (componente.enabled && (componente is PlayerMovement || componente is Pistola || componente is Mitre ||
                componente is ArmaDeFuego || componente is MeleeAttack || componente is WeaponSwitcher || componente is PlayerAbility))
            {
                componente.enabled = false;
                bloqueados.Add(componente);
            }
    }

    private float Pitch()
    {
        if (camara == null) return 0f;
        float x = camara.localEulerAngles.x;
        return x > 180f ? x - 360f : x;
    }

    private byte Banderas()
    {
        byte banderas = 0;
        if (animador != null)
        {
            if (animador.GetBool("isCrouching")) banderas |= Agachado;
            if (animador.GetBool("isSprinting")) banderas |= Corriendo;
            if (animador.GetBool("isFalling")) banderas |= Cayendo;
        }
        if (vida != null && vida.Invulnerable) banderas |= Invulnerable;
        if (movimiento != null && movimiento.CaminandoDespacio) banderas |= Despacio; // US 198
        return banderas;
    }

    // US 057, CA4 y CA7: el aviso "asesino [arma] víctima" con los nombres de cada jugador, igual en todas las computadoras,
    // con la marca de tiro a la cabeza si el golpe que lo mató fue a la cabeza. arma: con qué lo mató, si no fue el arma
    // que el asesino tiene en la mano (por ejemplo, una granada, US 073); vacío, el arma en la mano.
    private void AvisarBaja(int atacante, bool cabeza, string arma)
    {
        RondasTacticas.ContarBaja(atacante, photonView.OwnerActorNr); // US 135, CA2: $ 200 al que mató
        PartidaDeathmatch.ContarBaja(atacante, photonView.OwnerActorNr, cabeza); // US 140, CA1 y CA2
        Player asesino = atacante != 0 && PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.GetPlayer(atacante) : null;
        JugadorEnRed tirador = partida != null ? partida.Buscar(atacante) : null;
        bool otraArma = !string.IsNullOrEmpty(arma);
        string nombreArma = otraArma ? arma : tirador != null ? tirador.NombreArma : "";
        Sprite icono = otraArma ? IconoDe(arma) : tirador != null ? tirador.IconoArma : null;
        MatchHud.ReportKill(asesino != null ? asesino.NickName : "", nombreArma, Nombre,
            ColorDe(atacante), ColorDe(photonView.OwnerActorNr), cabeza, icono);
    }

    // Ícono de la ficha de la tienda que se llama así (por ejemplo, la granada de metralla), o null.
    private Sprite IconoDe(string arma)
    {
        if (catalogo == null) return null;
        foreach (ShopItem item in catalogo.items)
            if (item != null && (item.displayName == arma || item.alias == arma)) return item.icon;
        return null;
    }

    // Azul el propio equipo (y uno mismo), rojo el rival; blanco si no hay equipos.
    public static Color ColorDe(int actor)
    {
        if (actor == 0) return Color.white;
        bool yo = PhotonNetwork.LocalPlayer != null && actor == PhotonNetwork.LocalPlayer.ActorNumber;
        if (!EquiposTacticos.HayEquipos) return yo ? MatchHud.TeamColor : Color.white;
        return yo || EquiposTacticos.SonAliados(PhotonNetwork.LocalPlayer.ActorNumber, actor) ? MatchHud.TeamColor : MatchHud.RivalColor;
    }

    /// <summary>Nombre del arma que este jugador tiene en la mano (para los avisos de bajas).</summary>
    public string NombreArma => NombreDeArma(ArmaActual);

    /// <summary>Ícono (de la ficha de la tienda) del arma en la mano, o null.</summary>
    public Sprite IconoArma
    {
        get
        {
            byte arma = ArmaActual;
            ShopItem ficha = null;
            if (arma >= PrimeraGranada) ficha = FichaDe((byte)(arma - PrimeraGranada));
            else if (photonView != null && photonView.IsMine)
            {
                // Dueño: los scripts de armas siguen estando.
                if (arma == ArmaPistola && pistola != null)
                {
                    Pistola p = pistola.GetComponentInChildren<Pistola>(true);
                    ficha = p != null ? p.shopItem : null;
                }
                else if (arma >= PrimeraPrincipal && arma - PrimeraPrincipal < principales.Count)
                    ficha = WeaponSwitcher.FichaDe(principales[arma - PrimeraPrincipal]);
                else if (EsSecundaria(arma))
                    ficha = WeaponSwitcher.FichaDe(secundarias[arma - PrimeraSecundaria]);
            }
            else if (arma == ArmaPistola) ficha = fichaPistola;
            else if (arma >= PrimeraPrincipal && arma - PrimeraPrincipal < fichasPrincipales.Count)
                ficha = fichasPrincipales[arma - PrimeraPrincipal];
            else if (EsSecundaria(arma) && arma - PrimeraSecundaria < fichasSecundarias.Count)
                ficha = fichasSecundarias[arma - PrimeraSecundaria];
            return ficha != null ? ficha.icon : null;
        }
    }

    // Fichas de la tienda de cada arma, guardadas al armar el jugador (en las copias se quitan los scripts de armas).
    private readonly List<ShopItem> fichasPrincipales = new List<ShopItem>();
    private readonly List<ShopItem> fichasSecundarias = new List<ShopItem>();
    private ShopItem fichaPistola;

    private bool EsSecundaria(byte arma) => arma >= PrimeraSecundaria && arma < PrimeraPrincipal && arma - PrimeraSecundaria < secundarias.Count;

    // Las secundarias que no son la Línea A (US 072), en el orden de WeaponSwitcher.otrasSecundarias.
    private static List<GameObject> OtrasSecundarias(WeaponSwitcher cambio)
    {
        var lista = new List<GameObject>();
        if (cambio != null && cambio.otrasSecundarias != null)
            foreach (GameObject arma in cambio.otrasSecundarias)
                if (arma != null) lista.Add(arma);
        return lista;
    }

    private byte ArmaActual => photonView != null && photonView.IsMine ? ArmaEnMano() : armaRed;

    /// <summary>US 133: lo que tiene en la mano ahora (en la copia, lo que llega por la red), para el espectador.</summary>
    public byte ArmaVisible => muerto ? SinArma : ArmaActual;

    /// <summary>US 133: avisa en la copia cada disparo de su dueño (el espectador mueve el arma).</summary>
    public event System.Action Disparo;

    /// <summary>
    /// US 133: el modelo de primera persona de este jugador (el de esta computadora) para un arma, con el mismo código
    /// que viaja por la red: así el espectador muestra lo que tiene en la mano el compañero que mira. null si no hay.
    /// </summary>
    public GameObject PrimeraPersona(byte arma)
    {
        if (arma == SinArma) return null;
        if (arma >= PrimeraGranada) return cambioLocal != null ? cambioLocal.grenadeObj : null;
        if (arma == ArmaDispositivo) return cambioLocal != null ? cambioLocal.dispositivoObj : null;
        if (arma == ArmaCuchillo) return cuchillo != null ? cuchillo.CurrentViewModel : null;
        if (arma == ArmaPistola) return pistola;
        if (EsSecundaria(arma)) return secundarias[arma - PrimeraSecundaria];
        int i = arma - PrimeraPrincipal;
        return arma >= PrimeraPrincipal && i < principales.Count ? principales[i] : null;
    }

    private string NombreDeArma(byte arma)
    {
        if (arma >= PrimeraGranada) return ModeloReal.NombreDe(FichaDe((byte)(arma - PrimeraGranada)));
        if (arma == ArmaDispositivo) return "Dispositivo";
        if (arma == ArmaCuchillo) return "Cuchillo";
        if (arma == ArmaPistola) return "Pistola";
        if (EsSecundaria(arma))
        {
            GameObject secundaria = secundarias[arma - PrimeraSecundaria];
            IHudWeapon hudSecundaria = secundaria != null ? secundaria.GetComponentInChildren<IHudWeapon>(true) : null;
            if (hudSecundaria != null) return hudSecundaria.HudName;
            ShopItem ficha = arma - PrimeraSecundaria < fichasSecundarias.Count ? fichasSecundarias[arma - PrimeraSecundaria] : null;
            return ficha != null && !string.IsNullOrEmpty(ficha.alias) ? ficha.alias : secundaria != null ? secundaria.name : "";
        }
        int i = arma - PrimeraPrincipal;
        if (arma >= PrimeraPrincipal && i < principales.Count && principales[i] != null)
        {
            IHudWeapon hud = principales[i].GetComponentInChildren<IHudWeapon>(true);
            return hud != null ? hud.HudName : principales[i].name;
        }
        return "";
    }

    private byte ArmaEnMano()
    {
        for (int i = 0; i < principales.Count; i++)
            if (principales[i] != null && principales[i].activeSelf) return (byte)(PrimeraPrincipal + i);
        if (pistola != null && pistola.activeSelf) return ArmaPistola;
        for (int i = 0; i < secundarias.Count; i++)
            if (secundarias[i] != null && secundarias[i].activeSelf) return (byte)(PrimeraSecundaria + i);
        if (cambioLocal != null && cambioLocal.DispositivoEquipado) return ArmaDispositivo;
        if (cuchillo != null && cuchillo.CurrentViewModel != null && cuchillo.CurrentViewModel.activeSelf) return ArmaCuchillo;
        // US 182: la granada que tiene en la mano, por su lugar en la tienda.
        ShopItem granada = cambioLocal != null && cambioLocal.Granadas != null ? cambioLocal.Granadas.Selected : null;
        int indice = granada != null && catalogo != null ? catalogo.items.IndexOf(granada) : -1;
        if (indice >= 0 && PrimeraGranada + indice <= byte.MaxValue) return (byte)(PrimeraGranada + indice);
        return SinArma;
    }

    // =====================================================================
    // Copia
    // =====================================================================

    // Arma la copia de otro jugador a partir del Player.prefab. Se instancia bajo un objeto apagado para sacarle
    // lo que es solo del dueño antes de que corra nada: cámaras, controles, armas que leen el mouse, tienda y pausa.
    public static JugadorEnRed CrearCopia(GameObject molde, Vector3 posicion, Quaternion rotacion, int viewId, Player dueno, PartidaEnRed partida)
    {
        var soporte = new GameObject("Cargando jugador");
        soporte.SetActive(false);
        GameObject go = Instantiate(molde, posicion, rotacion, soporte.transform);
        go.name = "Jugador en red (" + dueno.NickName + ")";
        go.tag = "Untagged";

        WeaponSwitcher cambio = go.GetComponentInChildren<WeaponSwitcher>(true);
        Pistola pistola = go.GetComponentInChildren<Pistola>(true);
        MeleeAttack ataque = go.GetComponent<MeleeAttack>();
        PlayerMovement movimiento = go.GetComponent<PlayerMovement>();

        var jugador = go.AddComponent<JugadorEnRed>();
        jugador.partida = partida;
        jugador.camara = cambio != null ? cambio.transform : go.transform.Find("Main Camera");
        if (cambio != null) jugador.principales = cambio.Principales();
        foreach (GameObject arma in jugador.principales)
        {
            ArmaDeFuego fuego = arma.GetComponent<ArmaDeFuego>();
            Mitre mitre = arma.GetComponent<Mitre>();
            jugador.sonidosPrincipales.Add(fuego != null ? fuego.shootSound : mitre != null ? mitre.shootSound : null);
            jugador.fichasPrincipales.Add(WeaponSwitcher.FichaDe(arma)); // US 057: antes de quitar los componentes
        }
        jugador.fichaPistola = pistola != null ? pistola.shopItem : null;
        jugador.pistola = cambio != null ? cambio.pistolObj : pistola != null ? pistola.gameObject : null;
        jugador.sonidoPistola = pistola != null ? pistola.shootSound : null;
        jugador.secundarias = OtrasSecundarias(cambio); // US 072: la Línea H y las que vengan
        foreach (GameObject arma in jugador.secundarias)
        {
            ArmaDeFuego fuego = arma.GetComponent<ArmaDeFuego>();
            jugador.sonidosSecundarias.Add(fuego != null ? fuego.shootSound : null);
            jugador.fichasSecundarias.Add(WeaponSwitcher.FichaDe(arma));
        }
        jugador.sonidoCuchillo = ataque != null ? ataque.SwingSound : null;
        if (movimiento != null) { jugador.alturaParado = movimiento.standingCameraHeight; jugador.alturaAgachado = movimiento.crouchCameraHeight; }
        AudioMixerGroup efectos = pistola != null ? pistola.sfxGroup : null;

        foreach (PauseMenu pausa in go.GetComponentsInChildren<PauseMenu>(true)) DestroyImmediate(pausa.gameObject);
        Quitar<CameraLook>(go);
        Quitar<WeaponSwitcher>(go);
        Quitar<Pistola>(go);
        Quitar<Mitre>(go);
        Quitar<ArmaDeFuego>(go);
        Quitar<MeleeAttack>(go);
        Quitar<PlayerAbility>(go);
        Quitar<GrenadeThrower>(go); // la copia no tira granadas: las que tiró su dueño llegan por RpcGranada (US 073)
        Quitar<SoltarArmas>(go);    // ni suelta armas: las que soltó su dueño llegan por RpcArmaSoltada (US 184)
        PlayerLoadout carga = go.GetComponentInChildren<PlayerLoadout>(true);
        jugador.catalogo = carga != null ? carga.Catalog : null; // para saber qué granada tiró
        Quitar<PlayerLoadout>(go); // antes que la billetera, que la necesita
        Quitar<PlayerWallet>(go);
        Quitar<PlayerMovement>(go);
        Quitar<HideOwnBody>(go);
        Quitar<AudioListener>(go);
        Quitar<CharacterController>(go); // la copia se mueve con lo que llega; la cápsula sigue chocando
        foreach (Camera c in go.GetComponentsInChildren<Camera>(true))
        {
            c.enabled = false;
            c.gameObject.tag = "Untagged";
        }
        // Las armas en la mano se dibujan con una cámara aparte (capa ArmaEnMano): en la copia van con el resto del mundo.
        foreach (GameObject arma in jugador.principales) PonerCapa(arma, 0);
        PonerCapa(jugador.pistola, 0);
        foreach (GameObject arma in jugador.secundarias) PonerCapa(arma, 0);

        jugador.animador = go.GetComponentInChildren<Animator>(true);
        if (jugador.animador != null)
        {
            jugador.animador.applyRootMotion = false;
            jugador.modelo = jugador.animador.transform;
        }
        jugador.vida = go.GetComponent<HealthSystem>();
        jugador.cuchillo = go.GetComponent<MeleeWeaponHolder>();
        jugador.colisiones = go.GetComponentsInChildren<Collider>(true);

        // US 182: cada arma a tamaño real para la mano derecha; las de primera persona no se ven (CA4).
        for (int i = 0; i < jugador.principales.Count; i++)
            jugador.ArmarEnMano((byte)(PrimeraPrincipal + i), jugador.principales[i],
                i < jugador.fichasPrincipales.Count ? jugador.fichasPrincipales[i] : null, ModeloReal.LargoSinFicha);
        jugador.ArmarEnMano(ArmaPistola, jugador.pistola, jugador.fichaPistola, ModeloReal.LargoSinFicha);
        for (int i = 0; i < jugador.secundarias.Count; i++)
            jugador.ArmarEnMano((byte)(PrimeraSecundaria + i), jugador.secundarias[i],
                i < jugador.fichasSecundarias.Count ? jugador.fichasSecundarias[i] : null, ModeloReal.LargoSinFicha);

        var vista = go.AddComponent<PhotonView>();
        vista.ObservedComponents = new List<Component> { jugador };
        vista.Synchronization = ViewSynchronization.UnreliableOnChange;
        vista.OwnershipTransfer = OwnershipOption.Fixed;

        go.transform.SetParent(null, true);
        Destroy(soporte);
        vista.ViewID = viewId;
        jugador.IniciarCopia(efectos);
        return jugador;
    }

    private static void Quitar<T>(GameObject go) where T : Component
    {
        foreach (T componente in go.GetComponentsInChildren<T>(true)) DestroyImmediate(componente);
    }

    private static void PonerCapa(GameObject go, int capa)
    {
        if (go == null) return;
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = capa;
    }

    private void IniciarCopia(AudioMixerGroup efectos)
    {
        posicionRed = transform.position;
        yawRed = transform.eulerAngles.y;

        sonido = gameObject.AddComponent<AudioSource>();
        sonido.playOnAwake = false;
        sonido.spatialBlend = 1f;
        sonido.minDistance = 3f;
        sonido.maxDistance = 80f;
        sonido.rolloffMode = AudioRolloffMode.Linear;
        sonido.outputAudioMixerGroup = efectos;

        pasos = gameObject.AddComponent<Pasos>();
        pasos.ComoCopia();

        // US 029: el daño a esta copia se le manda al dueño, que es el que sabe su vida. Va con qué se hizo, si no fue
        // el arma en la mano (HealthSystem.DamageSource, por ejemplo la granada), para el aviso de baja.
        if (vida != null)
            vida.DamageRedirect = (danio, cabeza) =>
            {
                if (photonView.Owner != null)
                {
                    // US 192: el lugar del daño; si quien lo hizo no lo avisó, donde está el jugador de esta computadora.
                    JugadorEnRed yo = this.partida != null ? this.partida.Local : null;
                    Vector3 origen = HealthSystem.DamageOrigin ?? (yo != null ? yo.transform.position : transform.position);
                    photonView.RPC(nameof(RpcDanio), photonView.Owner, danio, PhotonNetwork.LocalPlayer.ActorNumber, cabeza,
                        HealthSystem.DamageSource ?? "", origen);
                }
            };

        // US 031, CA5: a los rivales se los reconoce por el contorno rojo, como en Valorant. No se ve su nombre ni su vida.
        ContornoRival.Crear(gameObject, modelo, ConfigRed.Actual != null ? ConfigRed.Actual.contornoRival : null,
            () => Vivo && EquiposTacticos.EsRival(Actor));
    }

    private void Update()
    {
        if (photonView == null) return;
        if (photonView.IsMine)
        {
            if (vida != null && vida.Invulnerable && Time.time >= finInvulnerable) vida.Invulnerable = false;
            return;
        }
        if (!recibido) return;

        // US 025: se acerca a lo último que llegó, adelantándolo según su velocidad lo que tardó en llegar.
        float dt = Time.deltaTime;
        float atraso = Mathf.Clamp((float)(PhotonNetwork.Time - tiempoRed), 0f, 0.2f);
        Vector3 objetivo = posicionRed + (muerto ? Vector3.zero : velocidadRed * atraso);
        if ((transform.position - objetivo).sqrMagnitude > 16f) transform.position = objetivo;
        else transform.position = Vector3.Lerp(transform.position, objetivo, 1f - Mathf.Exp(-15f * dt));
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, yawRed, 0f), 1f - Mathf.Exp(-20f * dt));

        bool agachado = (banderasRed & Agachado) != 0;
        if (camara != null)
        {
            camara.localRotation = Quaternion.Slerp(camara.localRotation, Quaternion.Euler(pitchRed, 0f, 0f), 1f - Mathf.Exp(-20f * dt));
            Vector3 lugar = camara.localPosition;
            lugar.y = Mathf.Lerp(lugar.y, agachado ? alturaAgachado : alturaParado, 1f - Mathf.Exp(-8f * dt));
            camara.localPosition = lugar;
        }

        if (animador != null && !muerto)
        {
            animador.SetFloat("VelX", velXRed, 0.08f, dt);
            animador.SetFloat("VelZ", velZRed, 0.08f, dt);
            animador.SetBool("isCrouching", agachado);
            animador.SetBool("isSprinting", (banderasRed & Corriendo) != 0);
            animador.SetBool("isFalling", (banderasRed & Cayendo) != 0);
            if (saltosVistos != saltosRed)
            {
                saltosVistos = saltosRed;
                animador.SetTrigger("Jump");
                if (pasos != null) pasos.Salto(); // US 003, CA6: se escucha desde donde está
            }
            if (aterrizajesVistos != aterrizajesRed) { aterrizajesVistos = aterrizajesRed; animador.SetTrigger("Land"); }
        }
        if (vida != null) vida.Invulnerable = (banderasRed & Invulnerable) != 0;
        if (pasos != null)
        {
            pasos.Agachado = agachado;
            pasos.Despacio = (banderasRed & Despacio) != 0;
            pasos.EnElAire = (banderasRed & Cayendo) != 0;
            pasos.Muerto = muerto;
        }

        MostrarArma(muerto ? SinArma : armaRed);
    }

    // US 182, CA1 y CA2: en la mano se ve el arma a tamaño real que tiene el jugador (o ninguna).
    private void MostrarArma(byte arma)
    {
        // El cuchillo lo arma MeleeWeaponHolder en su Start: cuando aparece, se arma su modelo para la mano.
        GameObject modeloCuchillo = cuchillo != null ? cuchillo.CurrentViewModel : null;
        if (modeloCuchillo != cuchilloListo)
        {
            cuchilloListo = modeloCuchillo;
            if (modeloCuchillo != null)
            {
                ArmarEnMano(ArmaCuchillo, modeloCuchillo, null, LargoCuchillo);
                modeloCuchillo.SetActive(false); // CA4: no se ve el de primera persona
            }
            armaVista = 255;
        }
        // La granada se arma la primera vez que la saca, con el prefab de su ficha.
        if (arma >= PrimeraGranada && !enLaMano.ContainsKey(arma))
        {
            ShopItem granada = FichaDe((byte)(arma - PrimeraGranada));
            if (granada != null && granada.grenadePrefab != null) ArmarEnMano(arma, granada.grenadePrefab, null, LargoGranada);
        }
        // El dispositivo (US 130), también la primera vez que lo saca.
        if (arma == ArmaDispositivo && !enLaMano.ContainsKey(arma) && ConfigRed.Actual != null && ConfigRed.Actual.modeloDispositivo != null)
            ArmarEnMano(arma, ConfigRed.Actual.modeloDispositivo, null, LargoDispositivo);
        if (arma == armaVista) return;
        armaVista = arma;
        foreach (KeyValuePair<byte, ModeloEnMano> modelo in enLaMano)
            if (modelo.Value.go != null) modelo.Value.go.SetActive(modelo.Key == arma);
    }

    private const float LargoCuchillo = 0.3f, LargoGranada = 0.12f, LargoDispositivo = 0.3f;

    // Arma a tamaño real para la mano derecha (US 182). La de primera persona queda apagada (CA4).
    private void ArmarEnMano(byte codigo, GameObject arma, ShopItem ficha, float largoSinFicha)
    {
        if (arma == null) return;
        if (enLaMano.TryGetValue(codigo, out ModeloEnMano viejo) && viejo.go != null) Destroy(viejo.go);
        float largo = ficha != null && ficha.realLength > 0f ? ficha.realLength : largoSinFicha;
        GameObject modelo = ModeloReal.Crear(arma, largo, out Bounds limites, arma.name + " (en la mano)");
        modelo.transform.SetParent(SoporteMano(), false);
        // La mano agarra la empuñadura: el centro del arma queda un poco más arriba y más adelante.
        modelo.transform.localPosition = new Vector3(0f, limites.extents.y * 0.5f, limites.extents.z * 0.45f);
        modelo.SetActive(false);
        enLaMano[codigo] = new ModeloEnMano { go = modelo, limites = limites };
        if (arma.scene.IsValid()) arma.SetActive(false); // el prefab de la granada no se toca
        armaVista = 255;
    }

    // Sigue a la mano derecha del personaje y apunta hacia donde mira el jugador (LateUpdate).
    private Transform SoporteMano()
    {
        if (soporteMano != null) return soporteMano;
        soporteMano = new GameObject("Arma en la mano").transform;
        soporteMano.SetParent(transform, false);
        return soporteMano;
    }

    private void LateUpdate()
    {
        if (soporteMano == null || photonView == null || photonView.IsMine) return;
        // El hueso se busca ya con la copia prendida (con el personaje apagado, el Animator no lo da).
        if (manoDerecha == null && animador != null && animador.isHuman) manoDerecha = animador.GetBoneTransform(HumanBodyBones.RightHand);
        if (manoDerecha != null) soporteMano.position = manoDerecha.position;
        if (camara != null) soporteMano.rotation = camara.rotation;
    }

    // CA5: la boca del caño del arma que tiene en la mano; si no tiene ninguna, "siNo".
    private Vector3 Boca(byte arma, Vector3 siNo)
    {
        if (!enLaMano.TryGetValue(arma, out ModeloEnMano modelo) || modelo.go == null) return siNo;
        return modelo.go.transform.TransformPoint(new Vector3(0f, 0f, modelo.limites.max.z));
    }

    // US 028: el disparo se ve y se escucha desde el arma de la copia, con la trazadora hasta donde pegó y la
    // marca en el escenario. El daño ya lo calculó el que disparó.
    [PunRPC]
    private void RpcDisparo(Vector3 origen, float[] direcciones, byte arma)
    {
        if (photonView.IsMine) return;
        armaRed = arma;
        MostrarArma(arma);
        Disparo?.Invoke();
        int principal = arma - PrimeraPrincipal;
        bool esPrincipal = principal >= 0 && principal < principales.Count;
        int secundaria = arma - PrimeraSecundaria;
        bool esSecundaria = EsSecundaria(arma);
        AudioClip clip = esPrincipal ? sonidosPrincipales[principal]
            : esSecundaria && secundaria < sonidosSecundarias.Count ? sonidosSecundarias[secundaria] : sonidoPistola;
        if (clip != null && sonido != null) sonido.PlayOneShot(clip);

        Vector3 boca = Boca(arma, origen); // US 182, CA5: del caño del arma en la mano
        global::Trazadora.Fogonazo(boca);
        for (int i = 0; i + 2 < direcciones.Length; i += 3)
        {
            Vector3 direccion = new Vector3(direcciones[i], direcciones[i + 1], direcciones[i + 2]);
            Vector3 fin = WeaponFire.Replay(origen, direccion, AlcanceTrazadora, transform);
            global::Trazadora.Mostrar(boca, fin);
        }
    }

    [PunRPC]
    private void RpcCuchillo()
    {
        if (photonView.IsMine) return;
        armaRed = ArmaCuchillo;
        MostrarArma(ArmaCuchillo);
        if (sonidoCuchillo != null && sonido != null) sonido.PlayOneShot(sonidoCuchillo);
        if (enLaMano.TryGetValue(ArmaCuchillo, out ModeloEnMano hoja) && hoja.go != null) StartCoroutine(Estocada(hoja.go.transform));
    }

    // US 073: la granada que tiró este jugador en su computadora. Acá se ve una de muestra que vuela igual y suena,
    // pero no hace daño: el daño lo calcula la computadora de quien la tiró y llega por RpcDanio.
    [PunRPC]
    private void RpcGranada(int id, byte indice, Vector3 origen, Vector3 direccion, Vector3 heredada)
    {
        if (photonView.IsMine) return;
        ShopItem item = FichaDe(indice);
        if (item == null || item.grenadePrefab == null) return;
        GameObject go = Instantiate(item.grenadePrefab, origen, Quaternion.identity);
        Grenade1 granada = go.GetComponent<Grenade1>();
        if (granada == null) { Destroy(go); return; }
        granada.cosmetic = true;
        granada.netId = id;
        granada.thrower = transform; // que no choque con la copia de quien la tiró
        granada.Configure(item);
        granada.Throw(direccion, heredada);
        granadasRemotas[id] = granada;
    }

    // Explotó la granada de verdad: la de muestra explota en el mismo lugar (si no llegó a verse, solo el efecto).
    [PunRPC]
    private void RpcExplosionGranada(int id, byte indice, Vector3 centro)
    {
        if (photonView.IsMine) return;
        if (granadasRemotas.TryGetValue(id, out Grenade1 granada) && granada != null) granada.ExplodeAt(centro);
        else
        {
            ShopItem item = FichaDe(indice);
            Grenade1.PlayExplosion(item != null && item.grenadePrefab != null ? item.grenadePrefab.GetComponent<Grenade1>() : null, centro, item);
        }
        granadasRemotas.Remove(id);
    }

    private ShopItem FichaDe(byte indice) => catalogo != null && indice < catalogo.items.Count ? catalogo.items[indice] : null;

    private static IEnumerator Estocada(Transform hoja)
    {
        Vector3 inicio = hoja.localPosition, adelante = inicio + Vector3.forward * 0.3f;
        for (float t = 0f; t < 0.2f && hoja != null; t += Time.deltaTime)
        {
            hoja.localPosition = Vector3.Lerp(inicio, adelante, t < 0.08f ? t / 0.08f : 1f - (t - 0.08f) / 0.12f);
            yield return null;
        }
        if (hoja != null) hoja.localPosition = inicio;
    }

    // US 030, CA1 y CA2: el cuerpo cae y ya no recibe disparos.
    [PunRPC]
    private void RpcMurio(int atacante, bool cabeza, string arma)
    {
        if (photonView.IsMine) return;
        muerto = true;
        AvisarBaja(atacante, cabeza, arma); // US 057, CA4
        if (vida != null) vida.SetState(0, 0);
        foreach (Collider c in colisiones) if (c != null) c.enabled = false;
        MostrarArma(SinArma);
        if (caida != null) StopCoroutine(caida);
        caida = StartCoroutine(Caer(true));
    }

    // US 030, CA3 y CA5: aparece en el punto que eligió el dueño, sin deslizarse desde donde murió.
    [PunRPC]
    private void RpcReaparecio(Vector3 posicion, float yaw)
    {
        if (photonView.IsMine) return;
        muerto = false;
        if (afuera) { afuera = false; if (modelo != null) modelo.gameObject.SetActive(true); }
        transform.SetPositionAndRotation(posicion, Quaternion.Euler(0f, yaw, 0f));
        posicionRed = posicion;
        velocidadRed = Vector3.zero;
        yawRed = yaw;
        if (vida != null) vida.SetState(vida.maxHealth, 0);
        foreach (Collider c in colisiones) if (c != null) c.enabled = true;
        if (caida != null) StopCoroutine(caida);
        caida = StartCoroutine(Caer(false));
    }

    // US 195: la copia se armó cuando su dueño ya estaba fuera de juego (murió antes, o volvió a la partida y espera
    // para reaparecer). No se la ve hasta que reaparece, y no recibe disparos.
    private bool afuera;

    private void QuedarAfueraCopia()
    {
        muerto = true;
        afuera = true;
        if (vida != null) vida.SetState(0, 0);
        foreach (Collider c in colisiones) if (c != null) c.enabled = false;
        MostrarArma(SinArma);
        if (modelo != null) modelo.gameObject.SetActive(false);
    }

    private IEnumerator Caer(bool cae)
    {
        if (modelo == null) yield break;
        Quaternion desde = modelo.localRotation, hasta = cae ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.identity;
        float duracion = cae ? 0.45f : 0f;
        for (float t = 0f; t < duracion; t += Time.deltaTime)
        {
            float k = t / duracion;
            modelo.localRotation = Quaternion.Slerp(desde, hasta, k * k);
            yield return null;
        }
        modelo.localRotation = hasta;
    }

    // =====================================================================
    // Red
    // =====================================================================

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.eulerAngles.y);
            stream.SendNext(Pitch());
            stream.SendNext(animador != null ? animador.GetFloat("VelX") : 0f);
            stream.SendNext(animador != null ? animador.GetFloat("VelZ") : 0f);
            stream.SendNext(Banderas());
            stream.SendNext(muerto ? SinArma : ArmaEnMano());
            stream.SendNext(saltos);
            stream.SendNext(aterrizajes);
            stream.SendNext(vida != null && !muerto ? (short)vida.currentHealth : (short)0); // muerto: 0 (US 195)
            stream.SendNext(vida != null ? (short)vida.currentShield : (short)0);
            return;
        }

        Vector3 posicion = (Vector3)stream.ReceiveNext();
        yawRed = (float)stream.ReceiveNext();
        pitchRed = (float)stream.ReceiveNext();
        velXRed = (float)stream.ReceiveNext();
        velZRed = (float)stream.ReceiveNext();
        banderasRed = (byte)stream.ReceiveNext();
        armaRed = (byte)stream.ReceiveNext();
        saltosRed = (byte)stream.ReceiveNext();
        aterrizajesRed = (byte)stream.ReceiveNext();
        short salud = (short)stream.ReceiveNext();
        short blindaje = (short)stream.ReceiveNext();

        double tiempo = info.SentServerTime;
        if (recibido && tiempo > tiempoRed)
            velocidadRed = Vector3.ClampMagnitude((posicion - posicionRed) / (float)(tiempo - tiempoRed), 20f);
        if (!recibido)
        {
            // Los contadores arrancan donde está el dueño: no se repiten saltos viejos.
            saltosVistos = saltosRed;
            aterrizajesVistos = aterrizajesRed;
            // US 195: si su dueño ya estaba fuera de juego cuando se armó esta copia, no se lo ve hasta que reaparece.
            if (salud <= 0 && !muerto) QuedarAfueraCopia();
        }
        posicionRed = posicion;
        tiempoRed = tiempo;
        recibido = true;
        if (!muerto && vida != null) vida.SetState(salud, blindaje);
    }

    // =====================================================================
    // Utilidades
    // =====================================================================

    public static void Teletransportar(Transform jugador, Vector3 posicion, Quaternion rotacion)
    {
        CharacterController cuerpo = jugador.GetComponent<CharacterController>();
        bool prendido = cuerpo != null && cuerpo.enabled;
        if (prendido) cuerpo.enabled = false;
        jugador.SetPositionAndRotation(posicion, rotacion);
        if (prendido) cuerpo.enabled = true;
        Physics.SyncTransforms();
    }
}
