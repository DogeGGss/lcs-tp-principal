using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// Conexión con Photon PUN 2 y salas por código (F06: US 026 y US 027). Vive entre escenas.
// El anfitrión crea la sala y recibe un código de 5 caracteres; los demás entran con ese código. La sala guarda
// el modo y el mapa. Al iniciar, el anfitrión carga el mapa y Photon lo carga en todos a la vez; en la escena
// del mapa, PartidaEnRed arma los jugadores (US 025 a US 030).
// US 195: al iniciar, la sala pasa a guardar 2 minutos a quien se desconecta. "Volver a la partida" (la tarjeta del menú
// principal, en MenuRiftwalker) se conecta con el usuario y la región anotados por Reconexion y vuelve a entrar a esa sala.
// F28 (US 274 a US 276): buscar partida junta con jugadores del mismo modo, la misma versión (AppVersion, US 204) y la
// misma región: entra a una sala pública con lugar o crea una. Las públicas son visibles y llevan "pub" en el lobby; las
// de código siguen invisibles, así nunca se mezclan (US 274, CA4). La partida arranca sola (US 276): con 8 jugadores, o
// con el mínimo del modo si pasan 45 s sin que entre nadie, después de una cuenta regresiva de 10 s. El reloj lo lleva
// el anfitrión con la hora del servidor guardada en la sala, así sigue igual si cambia el anfitrión.
public class Multijugador : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public const int MaxJugadores = 8;
    public const int MinJugadores = 2;
    public const int EsperaConMinimo = 45, SegundosCuentaRegresiva = 10;

    // Un jugador avisa que entró a la partida, con el id de su PhotonView y dónde aparece.
    public const byte EventoJugador = 1;

    private const string PropModo = "modo", PropMapa = "mapa";
    // Buscar partida: la sala es pública, desde cuándo se espera con el mínimo y cuándo empezó la cuenta regresiva
    // (PhotonNetwork.ServerTimestamp; 0, todavía no).
    private const string PropPublica = "pub", PropEspera = "esp", PropInicio = "ini";
    private const string Alfabeto = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"; // sin O, I ni L (US 027, CA2)
    private const string EscenaMenu = "MenuPrincipal";

    private const string ErrorCrear = "No se pudo crear la sala. Revisá tu conexión.";
    private const string ErrorEntrar = "No se pudo entrar a la sala. Revisá tu conexión.";
    private const string ErrorVolver = "No se pudo volver a la partida. Revisá tu conexión.";
    private const string ErrorBuscar = "No se pudo buscar partida. Revisá tu internet y volvé a buscar.";

    public struct Jugador
    {
        public string nombre;
        public bool anfitrion, vos;
    }

    private static Multijugador instancia;

    public static Multijugador Instancia
    {
        get
        {
            if (instancia == null)
            {
                var go = new GameObject("Multijugador");
                DontDestroyOnLoad(go);
                instancia = go.AddComponent<Multijugador>();
            }
            return instancia;
        }
    }

    public static bool Existe => instancia != null;
    public static bool EnSala => instancia != null && PhotonNetwork.InRoom;

    // Entró o salió alguien, cambió el anfitrión, el mapa o un nombre: la sala del menú se vuelve a dibujar.
    public event Action Cambio;
    // No se pudo conectar, crear la sala o entrar: el mensaje es para mostrarlo tal cual.
    public event Action<string> Error;

    private enum Pedido { Nada, Crear, Unirse, Volver, Buscar }

    public enum Fallo { Conexion, Version, CuentaRepetida }

    /// <summary>Por qué falló lo último que se pidió: la búsqueda muestra un aviso para cada caso (US 275, CA3). Con una
    /// versión vieja (US 204) ofrece abrir el launcher.</summary>
    public Fallo UltimoFallo { get; private set; }

    private Pedido pedido;
    private float buscandoDesde, ultimoAjuste = -10f;
    private GameMode modoPedido;
    private string codigoPedido;
    private int intentos;
    private Reconexion.Partida vuelta;  // US 195: la partida a la que se vuelve
    private bool cambiandoUsuario;      // US 195: se desconectó para conectarse de nuevo con el usuario de esa partida
    private readonly List<object[]> pendientes = new List<object[]>();

    private void Awake()
    {
        SceneManager.sceneLoaded += AlCargarEscena;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= AlCargarEscena;
        if (instancia == this) instancia = null;
    }

    // ================= Pedidos del menú =================

    // Se llama al abrir el andén de Táctico o Deathmatch, así al crear o entrar ya está conectado.
    public void Conectar()
    {
        StartCoroutine(ConectarConVersion());
    }

    private System.Collections.IEnumerator ConectarConVersion()
    {
        string aviso = null;
        yield return VersionDelJuego.Comprobar(resultado => aviso = resultado);
        if (aviso != null) { Fallar(aviso); yield break; }
        ConectarAhora();
    }

    private void ConectarAhora()
    {
        if (PhotonNetwork.IsConnected) return;
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.SendRate = 30;
        PhotonNetwork.SerializationRate = 15;
        PhotonNetwork.NickName = NombreBase();
        // US 195: Photon reconoce al que vuelve a una sala por su usuario. Para volver se usan el usuario y la región
        // con los que se jugó esa partida.
        bool volviendo = pedido == Pedido.Volver;
        PhotonNetwork.AuthValues = new AuthenticationValues(volviendo ? vuelta.usuario : UsuarioPhoton());
        // Solo se juntan jugadores con la misma versión del juego (US 204). ConnectUsingSettings pisa GameVersion con la
        // AppVersion de PhotonServerSettings (vacía), así que la versión va en una copia de esa configuración.
        var ajustes = PhotonNetwork.PhotonServerSettings == null ? null : PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());
        if (ajustes != null) ajustes.AppVersion = Application.version;
        if (ajustes != null && volviendo && !string.IsNullOrEmpty(vuelta.region)) ajustes.FixedRegion = vuelta.region;
        if (ajustes == null || !PhotonNetwork.ConnectUsingSettings(ajustes, PhotonNetwork.PhotonServerSettings.StartInOfflineMode))
            Fallar("No se pudo conectar. Revisá tu conexión.");
    }

    public void CrearSala(GameMode modo)
    {
        pedido = Pedido.Crear;
        modoPedido = modo;
        intentos = 0;
        StartCoroutine(SeguirConVersion());
    }

    public void UnirseASala(string codigo, GameMode modo)
    {
        pedido = Pedido.Unirse;
        modoPedido = modo;
        codigoPedido = codigo.ToUpperInvariant();
        StartCoroutine(SeguirConVersion());
    }

    /// <summary>US 274: busca una partida pública de ese modo (o la crea) y espera ahí a que se llene (US 276).</summary>
    public void BuscarPartida(GameMode modo)
    {
        pedido = Pedido.Buscar;
        modoPedido = modo;
        buscandoDesde = Time.unscaledTime;
        StartCoroutine(SeguirConVersion());
        Cambio?.Invoke();
    }

    /// <summary>US 275, CA2: deja de buscar y sale de la sala pública. Se puede hasta que empieza la partida.</summary>
    public void CancelarBusqueda()
    {
        if (pedido == Pedido.Buscar) pedido = Pedido.Nada;
        if (EnPartidaPublica) SalirDeLaSala();
        Cambio?.Invoke();
    }

    private System.Collections.IEnumerator SeguirConVersion()
    {
        UltimoFallo = Fallo.Conexion;
        string aviso = null;
        yield return VersionDelJuego.Comprobar(resultado => aviso = resultado);
        if (aviso != null) { UltimoFallo = Fallo.Version; Fallar(aviso); yield break; }
        Seguir();
    }

    /// <summary>US 195, CA2: vuelve a la partida anotada por Reconexion (botón del menú principal).</summary>
    public void VolverALaPartida()
    {
        if (!Reconexion.HayPartida(out vuelta))
        {
            Fallar("Ya no se puede volver a esa partida.");
            return;
        }
        pedido = Pedido.Volver;
        StartCoroutine(SeguirConVersion());
    }

    public static void SalirDeLaSala()
    {
        // US 195, CA7: el que sale a propósito de la partida no vuelve.
        if (PhotonNetwork.CurrentRoom != null) Reconexion.OlvidarSiEsLaSala(PhotonNetwork.CurrentRoom.Name);
        if (instancia == null) return;
        instancia.pedido = Pedido.Nada;
        instancia.pendientes.Clear();
        // Si no, al cargar el menú mientras sale, Photon intenta avisarle a la sala la escena del anfitrión.
        PhotonNetwork.AutomaticallySyncScene = false;
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(false);
    }

    // El anfitrión pasa al mapa siguiente de los de ese modo (US 027, CA4).
    public void CambiarMapa()
    {
        if (!EsAnfitrion) return;
        int cantidad = ConfigRed.Actual != null ? ConfigRed.Actual.MapasDe(ModoSala).Count : 0;
        if (cantidad < 2) return;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { PropMapa, (IndiceMapa + 1) % cantidad } });
    }

    // US 027, CA7: solo el anfitrión, con al menos 2 jugadores. La sala se cierra y todos pasan al mapa.
    public void Iniciar()
    {
        if (!PuedeIniciar) return;
        // US 031: en Táctico, el anfitrión reparte los equipos antes de cargar el mapa.
        if (ModoSala == GameMode.Tactico) EquiposTacticos.Repartir();
        PhotonNetwork.CurrentRoom.IsOpen = false;
        // US 195: desde ahora, el que se desconecta queda 2 minutos en la sala para poder volver. En el andén no: ahí el
        // que se va deja su lugar libre en el momento. La sala también espera 2 minutos si se desconectan todos a la vez
        // (por ejemplo, se cortó la red de todos).
        PhotonNetwork.CurrentRoom.PlayerTtl = Reconexion.SegundosParaVolver * 1000;
        PhotonNetwork.CurrentRoom.EmptyRoomTtl = Reconexion.SegundosParaVolver * 1000;
        // US 196: pantalla de carga (los demás la ven cuando Photon les manda cargar el mapa). Las públicas no tienen código.
        PantallaDeCarga.MostrarOnline(MapaActual.escena, ModoSala, MapaActual.nombre, MapaActual.imagen, EnPartidaPublica ? "" : Codigo);
        PhotonNetwork.LoadLevel(MapaActual.escena);
    }

    // ================= Buscar partida (US 274 a US 276) =================

    /// <summary>Está conectándose o entrando a una sala pública (todavía no está en ella).</summary>
    public bool Buscando => pedido == Pedido.Buscar;

    /// <summary>Está en una sala de buscar partida, esperando que se llene o en la cuenta regresiva.</summary>
    public bool EnPartidaPublica =>
        PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(PropPublica, out object p) && p is bool b && b;

    public float SegundosBuscando => Time.unscaledTime - buscandoDesde;

    /// <summary>Con cuántos arranca la partida si no entra nadie más: 2 en Deathmatch y 4 en Táctico (2 contra 2).</summary>
    public static int MinimoPara(GameMode modo) => PruebaSolo.Activa ? 1 : modo == GameMode.Tactico ? 4 : MinJugadores;

    /// <summary>US 276, CA2: segundos que faltan para arrancar con el mínimo; -1 si no hay el mínimo o ya empezó la cuenta.</summary>
    public float FaltaConMinimo =>
        !EnPartidaPublica || MarcaSala(PropInicio) != 0 || CantidadJugadores < MinimoPara(ModoSala) ? -1f
            : Mathf.Max(0f, EsperaConMinimo - SegundosDesde(MarcaSala(PropEspera)));

    /// <summary>US 276, CA1: segundos de la cuenta regresiva que faltan; -1 si no empezó.</summary>
    public float FaltaParaEmpezar =>
        !EnPartidaPublica || MarcaSala(PropInicio) == 0 ? -1f : Mathf.Max(0f, SegundosCuentaRegresiva - SegundosDesde(MarcaSala(PropInicio)));

    private static int MarcaSala(string clave) =>
        PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(clave, out object v) && v is int t ? t : 0;

    // La hora del servidor da la vuelta (es un int en milisegundos): la resta sin control de desborde sigue siendo correcta.
    private static float SegundosDesde(int marca) => unchecked(PhotonNetwork.ServerTimestamp - marca) / 1000f;

    private void Update()
    {
        // US 276: el anfitrión de una sala pública arranca la partida. Cada cambio de la sala tarda en volver del servidor:
        // entre uno y otro espera medio segundo, así no los repite.
        if (!EnPartidaPublica || !PhotonNetwork.IsMasterClient || !PhotonNetwork.CurrentRoom.IsOpen) return;
        if (Time.unscaledTime - ultimoAjuste < 0.5f) return;
        int inicio = MarcaSala(PropInicio);
        switch (QueHacer(PhotonNetwork.CurrentRoom.PlayerCount, MinimoPara(ModoSala), inicio != 0, SegundosDesde(MarcaSala(PropEspera)), SegundosDesde(inicio)))
        {
            case Arranque.EmpezarCuenta: Ajustar(new Hashtable { { PropInicio, MarcaAhora() } }); break;
            case Arranque.CancelarCuenta: Ajustar(new Hashtable { { PropInicio, 0 }, { PropEspera, MarcaAhora() } }); break;
            case Arranque.Iniciar:
                if (!PuedeIniciar) break;
                ultimoAjuste = Time.unscaledTime;
                Iniciar();
                break;
        }
    }

    public enum Arranque { Nada, EmpezarCuenta, CancelarCuenta, Iniciar }

    /// <summary>US 276: qué hace el anfitrión de una sala pública. Sin la cuenta regresiva, la empieza con 8 jugadores, o
    /// con el mínimo si pasaron 45 s sin que entre nadie (CA1 y CA2). Durante la cuenta, si quedan menos que el mínimo la
    /// cancela y sigue buscando (CA3); a los 10 s, inicia la partida.</summary>
    public static Arranque QueHacer(int cantidad, int minimo, bool enCuenta, float segundosEsperando, float segundosDeCuenta)
    {
        if (!enCuenta)
            return cantidad >= MaxJugadores || (cantidad >= minimo && segundosEsperando >= EsperaConMinimo) ? Arranque.EmpezarCuenta : Arranque.Nada;
        if (cantidad < minimo) return Arranque.CancelarCuenta;
        return segundosDeCuenta >= SegundosCuentaRegresiva ? Arranque.Iniciar : Arranque.Nada;
    }

    private void Ajustar(Hashtable cambios)
    {
        ultimoAjuste = Time.unscaledTime;
        PhotonNetwork.CurrentRoom.SetCustomProperties(cambios);
    }

    // 0 quiere decir "todavía no": si justo la hora del servidor es 0, se usa 1 (un milisegundo no se nota).
    private static int MarcaAhora() => PhotonNetwork.ServerTimestamp == 0 ? 1 : PhotonNetwork.ServerTimestamp;

    // US 274, CA3: el mismo modo y sala pública (la versión ya la separa Photon, US 204; la región es la de la conexión).
    // Si no hay ninguna con lugar, crea una con un mapa del modo al azar.
    private void BuscarAhora()
    {
        int mapas = ConfigRed.Actual != null ? ConfigRed.Actual.MapasDe(modoPedido).Count : 0;
        var opciones = new RoomOptions
        {
            MaxPlayers = MaxJugadores,
            IsVisible = true,
            IsOpen = true,
            CleanupCacheOnLeave = true,
            PublishUserId = true,
            CustomRoomProperties = new Hashtable
            {
                { PropModo, (int)modoPedido }, { PropMapa, mapas > 0 ? UnityEngine.Random.Range(0, mapas) : 0 },
                { PropPublica, true }, { PropEspera, MarcaAhora() }, { PropInicio, 0 }
            },
            CustomRoomPropertiesForLobby = new[] { PropModo, PropPublica }
        };
        var esperadas = new Hashtable { { PropModo, (int)modoPedido }, { PropPublica, true } };
        PhotonNetwork.NickName = NombreBase();
        if (!PhotonNetwork.JoinRandomOrCreateRoom(esperadas, MaxJugadores, MatchmakingMode.FillRoom, TypedLobby.Default, null, null, opciones))
            Fallar(ErrorBuscar);
    }

    // ================= Estado de la sala =================

    public string Codigo => PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : "";
    public bool EsAnfitrion => PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient;
    public int CantidadJugadores => PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.PlayerCount : 0;

    public GameMode ModoSala =>
        PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(PropModo, out object modo)
            ? (GameMode)(int)modo : MatchSettings.Mode;

    public int IndiceMapa =>
        PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(PropMapa, out object mapa)
            ? (int)mapa : 0;

    public int CantidadMapas => ConfigRed.Actual != null ? ConfigRed.Actual.MapasDe(ModoSala).Count : 0;

    public ConfigRed.Mapa MapaActual
    {
        get
        {
            if (ConfigRed.Actual == null) return null;
            List<ConfigRed.Mapa> mapas = ConfigRed.Actual.MapasDe(ModoSala);
            return mapas.Count > 0 ? mapas[Mathf.Clamp(IndiceMapa, 0, mapas.Count - 1)] : null;
        }
    }

    public bool PuedeIniciar => EsAnfitrion && CantidadJugadores >= (PruebaSolo.Activa ? 1 : MinJugadores) && MapaActual != null;

    // En el orden en que entraron (US 027, CA5).
    public List<Jugador> Jugadores()
    {
        var lista = new List<Jugador>();
        if (PhotonNetwork.CurrentRoom == null) return lista;
        var jugadores = new List<Player>(PhotonNetwork.CurrentRoom.Players.Values);
        jugadores.Sort((a, b) => a.ActorNumber.CompareTo(b.ActorNumber));
        foreach (Player p in jugadores)
            lista.Add(new Jugador { nombre = p.NickName, anfitrion = p.IsMasterClient, vos = p.IsLocal });
        return lista;
    }

    public static string Titulo(GameMode modo) => modo == GameMode.Deathmatch ? "Deathmatch" : modo == GameMode.Zombie ? "Zombie" : "Táctico";

    // ================= Conexión =================

    // Hace lo que se pidió apenas se pueda: si hay que conectarse, sigue en OnConnectedToMaster.
    private void Seguir()
    {
        if (pedido == Pedido.Nada) return;
        if (PhotonNetwork.InRoom) { PhotonNetwork.LeaveRoom(false); return; }
        // US 195: si está conectado con otro usuario o a otra región, se reconecta con los de la partida.
        if (pedido == Pedido.Volver && PhotonNetwork.IsConnected && !ConectadoComoEnLaPartida())
        {
            cambiandoUsuario = true;
            PhotonNetwork.Disconnect();
            return;
        }
        if (!PhotonNetwork.IsConnected) { Conectar(); return; }
        if (PhotonNetwork.NetworkClientState != ClientState.ConnectedToMasterServer) return;

        if (pedido == Pedido.Volver)
        {
            // US 195, CA2: entra a la misma sala con el mismo nombre. Photon le devuelve su número de jugador (y con él,
            // su equipo y sus propiedades). El mapa se carga en OnJoinedRoom: si Photon lo cargara solo, lo haría antes
            // de terminar de entrar a la sala y la partida no se armaría.
            PhotonNetwork.AutomaticallySyncScene = false;
            PhotonNetwork.NickName = string.IsNullOrEmpty(vuelta.nombre) ? NombreBase() : vuelta.nombre;
            if (!PhotonNetwork.RejoinRoom(vuelta.sala)) FallarVuelta(ErrorVolver, false);
            return;
        }
        PhotonNetwork.AutomaticallySyncScene = true; // cuando el anfitrión inicia, todos cargan su mapa
        // Photon conserva las propiedades del jugador local al salir de una sala y las lleva a la siguiente: sin esto,
        // las bajas, muertes, plantadas y desactivaciones de la tabla (y la plata y las armas publicadas) arrancarían
        // con lo de la partida anterior. Al volver a la misma partida (arriba) no se borran: las devuelve la sala.
        PhotonNetwork.LocalPlayer.CustomProperties = new Hashtable();
        if (pedido == Pedido.Crear) CrearAhora();
        else if (pedido == Pedido.Buscar) BuscarAhora();
        else if (!PhotonNetwork.JoinRoom(codigoPedido)) Fallar(ErrorEntrar);
    }

    private void CrearAhora()
    {
        string codigo = "";
        for (int i = 0; i < 5; i++) codigo += Alfabeto[UnityEngine.Random.Range(0, Alfabeto.Length)];

        var opciones = new RoomOptions
        {
            MaxPlayers = MaxJugadores,
            IsVisible = false, // solo se entra con el código
            IsOpen = true,
            CleanupCacheOnLeave = true,
            CustomRoomProperties = new Hashtable { { PropModo, (int)modoPedido }, { PropMapa, 0 } }
        };
        PhotonNetwork.NickName = NombreBase();
        if (!PhotonNetwork.CreateRoom(codigo, opciones)) Fallar(ErrorCrear);
    }

    private static string NombreBase() => PlayerProfile.HasName ? PlayerProfile.Name : "Jugador";

    // US 277, CA3: en el juego publicado, el usuario de Photon es la cuenta, así la misma cuenta no puede estar dos veces en
    // una sala (Photon no deja entrar a un usuario que ya está). En el editor y en los builds de desarrollo es uno nuevo
    // cada vez, para poder probar con dos copias del juego en la misma PC y la misma cuenta.
    private static string UsuarioPhoton()
    {
        string cuenta = LogrosOnline.Instancia != null ? LogrosOnline.Instancia.IdDeLaCuenta : null;
        return !LogrosOnline.EntraEnElJuego && !string.IsNullOrEmpty(cuenta) ? cuenta : Reconexion.UsuarioDeEstaVez;
    }

    private bool ConectadoComoEnLaPartida() =>
        PhotonNetwork.AuthValues != null && PhotonNetwork.AuthValues.UserId == vuelta.usuario &&
        (string.IsNullOrEmpty(vuelta.region) || Reconexion.Region(PhotonNetwork.CloudRegion) == vuelta.region);

    // US 195: no se pudo volver. olvidar: ya no hay a qué volver, así que el menú deja de ofrecerlo.
    private void FallarVuelta(string mensaje, bool olvidar = true)
    {
        if (olvidar) Reconexion.Olvidar();
        Fallar(mensaje);
    }

    private void Fallar(string mensaje)
    {
        pedido = Pedido.Nada;
        Error?.Invoke(mensaje);
    }

    public override void OnConnectedToMaster()
    {
        Seguir();
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        // Otra sala ya usa ese código: se prueba con otro.
        if (pedido == Pedido.Crear && returnCode == ErrorCode.GameIdAlreadyExists && ++intentos < 5) { CrearAhora(); return; }
        Debug.LogWarning($"Multijugador: no se pudo crear la sala ({returnCode}: {message}).");
        if (pedido == Pedido.Buscar) FallarBusqueda(returnCode);
        else Fallar(ErrorCrear);
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.LogWarning($"Multijugador: no se pudo buscar partida ({returnCode}: {message}).");
        if (pedido == Pedido.Buscar) FallarBusqueda(returnCode);
    }

    // US 277, CA3: si la cuenta ya está en esa sala (desde otra PC), Photon no la deja entrar de nuevo.
    private void FallarBusqueda(short returnCode)
    {
        bool repetida = returnCode == ErrorCode.JoinFailedFoundActiveJoiner;
        UltimoFallo = repetida ? Fallo.CuentaRepetida : Fallo.Conexion;
        Fallar(repetida ? "Tu cuenta ya está en esa partida, en otra PC." : ErrorBuscar);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        if (pedido == Pedido.Volver)
        {
            // US 195, CA2.
            Debug.LogWarning($"Multijugador: no se pudo volver a la sala {vuelta.sala} ({returnCode}: {message}).");
            switch (returnCode)
            {
                // Photon todavía no se dio cuenta de que se cortó la conexión: en unos segundos sí.
                case ErrorCode.JoinFailedFoundActiveJoiner:
                    FallarVuelta("Todavía figurás en la partida. Probá de nuevo en unos segundos.", false);
                    break;
                case ErrorCode.GameDoesNotExist: FallarVuelta("La partida ya terminó."); break;
                case ErrorCode.JoinFailedWithRejoinerNotFound: FallarVuelta("Ya no se puede volver a esa partida."); break;
                default: FallarVuelta(ErrorVolver, false); break;
            }
            return;
        }
        // US 026, CA4 y CA5.
        switch (returnCode)
        {
            case ErrorCode.GameDoesNotExist: Fallar("No existe una sala con ese código."); break;
            case ErrorCode.GameFull: Fallar("La sala está llena."); break;
            case ErrorCode.GameClosed: Fallar("La partida ya empezó."); break;
            default:
                Debug.LogWarning($"Multijugador: no se pudo entrar a la sala ({returnCode}: {message}).");
                Fallar(ErrorEntrar);
                break;
        }
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"Multijugador: en la sala {PhotonNetwork.CurrentRoom.Name} ({PhotonNetwork.CurrentRoom.PlayerCount} jugadores).");
        Pedido hecho = pedido;
        pedido = Pedido.Nada;
        pendientes.Clear();

        GameMode modo = ModoSala;
        if (hecho == Pedido.Unirse && modo != modoPedido)
        {
            // US 026, CA5: la sala es de otro modo.
            PhotonNetwork.LeaveRoom(false);
            Error?.Invoke($"Esa sala es de {Titulo(modo)}.");
            return;
        }

        MatchSettings.Mode = modo;
        MatchSettings.RoomCode = PhotonNetwork.CurrentRoom.Name;

        if (hecho == Pedido.Volver)
        {
            // US 195, CA2: ya está en la sala, con el nombre de antes: se carga el mapa de la partida, con la pantalla de
            // carga. Desde acá sigue igual que los demás (si el anfitrión carga otra escena, la carga también).
            ConfigRed.Mapa mapa = MapaActual;
            if (mapa != null) PantallaDeCarga.MostrarOnline(mapa.escena, modo, mapa.nombre, mapa.imagen, Codigo);
            PhotonNetwork.AutomaticallySyncScene = true; // carga la escena que tiene el anfitrión
            // Si quedó como anfitrión (no hay nadie más conectado), Photon no le carga nada: se carga acá.
            if (PhotonNetwork.IsMasterClient && mapa != null) PhotonNetwork.LoadLevel(mapa.escena);
            Cambio?.Invoke();
            return;
        }

        // US 026, CA9: si el nombre ya está en la sala, se le agrega un número solo para esta partida (US 164).
        var otros = new List<string>();
        foreach (Player p in PhotonNetwork.PlayerListOthers) otros.Add(p.NickName);
        PhotonNetwork.NickName = PlayerProfile.UniqueInRoom(NombreBase(), otros);

        Cambio?.Invoke();
    }

    public override void OnLeftRoom()
    {
        pendientes.Clear();
        Cambio?.Invoke();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"Multijugador: {(newPlayer.HasRejoined ? "volvió" : "entró")} {newPlayer.NickName}.");
        if (newPlayer.HasRejoined && PartidaEnRed.Actual != null) PartidaEnRed.Actual.AlVolver(newPlayer); // US 195
        // US 276, CA2: en buscar partida, si entra alguien la espera con el mínimo vuelve a empezar.
        if (EnPartidaPublica && PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom.IsOpen && MarcaSala(PropInicio) == 0)
            Ajustar(new Hashtable { { PropEspera, MarcaAhora() } });
        Cambio?.Invoke();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        // US 195: IsInactive, se desconectó y tiene 2 minutos para volver; si no, se fue de la sala.
        Debug.Log($"Multijugador: {(otherPlayer.IsInactive ? "se desconectó" : "salió")} {otherPlayer.NickName}.");
        if (PartidaEnRed.Actual != null) PartidaEnRed.Actual.AlIrse(otherPlayer);
        Cambio?.Invoke();
    }

    public override void OnMasterClientSwitched(Player newMasterClient) => Cambio?.Invoke();
    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged) => Cambio?.Invoke();
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps) => Cambio?.Invoke();

    public override void OnDisconnected(DisconnectCause cause)
    {
        Pedido habia = pedido;
        pendientes.Clear();
        Reconexion.TerminarCorteDePrueba(); // US 195: si era el corte de prueba (F12), ya se puede volver a conectar
        // US 195: se desconectó a propósito para volver a la partida con su usuario: se conecta de nuevo.
        if (habia == Pedido.Volver && cambiandoUsuario)
        {
            cambiandoUsuario = false;
            Conectar();
            return;
        }
        cambiandoUsuario = false;
        if (habia != Pedido.Nada) Fallar(habia == Pedido.Crear ? ErrorCrear : habia == Pedido.Volver ? ErrorVolver : habia == Pedido.Buscar ? ErrorBuscar : ErrorEntrar);
        else if (cause != DisconnectCause.DisconnectByClientLogic) Error?.Invoke("Se perdió la conexión con el servidor.");
        Cambio?.Invoke();

        // Sin conexión no hay partida: se vuelve al menú.
        if (PartidaEnRed.Actual != null && Application.CanStreamedLevelBeLoaded(EscenaMenu))
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            PantallaDeCarga.Cargar(EscenaMenu, "", "Menú principal"); // US 196
        }
    }

    // ================= Partida =================

    private void AlCargarEscena(Scene escena, LoadSceneMode modo)
    {
        if (!PhotonNetwork.InRoom) return;
        if (escena.name == EscenaMenu) { SalirDeLaSala(); return; }
        if (PartidaEnRed.Actual == null) new GameObject("PartidaEnRed").AddComponent<PartidaEnRed>();
    }

    public void OnEvent(EventData evento)
    {
        if (evento.Code != EventoJugador || !(evento.CustomData is object[] datos)) return;
        // Puede llegar antes de que la partida termine de armarse: se guarda hasta que esté lista.
        if (PartidaEnRed.Actual != null && PartidaEnRed.Actual.Lista) PartidaEnRed.Actual.CrearRemoto(datos);
        else pendientes.Add(datos);
    }

    public List<object[]> TomarPendientes()
    {
        var lista = new List<object[]>(pendientes);
        pendientes.Clear();
        return lista;
    }
}
