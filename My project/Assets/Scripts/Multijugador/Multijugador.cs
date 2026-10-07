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
// US 195: al iniciar, la sala pasa a guardar 2 minutos a quien se desconecta. VolverALaPartida (el botón del menú
// principal) se conecta con el usuario y la región anotados por Reconexion y vuelve a entrar a esa sala.
public class Multijugador : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public const int MaxJugadores = 8;
    public const int MinJugadores = 2;

    // Un jugador avisa que entró a la partida, con el id de su PhotonView y dónde aparece.
    public const byte EventoJugador = 1;

    private const string PropModo = "modo", PropMapa = "mapa";
    private const string Alfabeto = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"; // sin O, I ni L (US 027, CA2)
    private const string EscenaMenu = "MenuPrincipal";

    private const string ErrorCrear = "No se pudo crear la sala. Revisá tu conexión.";
    private const string ErrorEntrar = "No se pudo entrar a la sala. Revisá tu conexión.";
    private const string ErrorVolver = "No se pudo volver a la partida. Revisá tu conexión.";

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

    private enum Pedido { Nada, Crear, Unirse, Volver }

    private Pedido pedido;
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
        PhotonNetwork.AuthValues = new AuthenticationValues(volviendo ? vuelta.usuario : Reconexion.UsuarioDeEstaVez);
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

    private System.Collections.IEnumerator SeguirConVersion()
    {
        string aviso = null;
        yield return VersionDelJuego.Comprobar(resultado => aviso = resultado);
        if (aviso != null) { Fallar(aviso); yield break; }
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
        // US 196: pantalla de carga (los demás la ven cuando Photon les manda cargar el mapa).
        PantallaDeCarga.MostrarOnline(MapaActual.escena, ModoSala, MapaActual.nombre, MapaActual.imagen, Codigo);
        PhotonNetwork.LoadLevel(MapaActual.escena);
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
        if (returnCode == ErrorCode.GameIdAlreadyExists && ++intentos < 5) { CrearAhora(); return; }
        Debug.LogWarning($"Multijugador: no se pudo crear la sala ({returnCode}: {message}).");
        Fallar(ErrorCrear);
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
        if (habia != Pedido.Nada) Fallar(habia == Pedido.Crear ? ErrorCrear : habia == Pedido.Volver ? ErrorVolver : ErrorEntrar);
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
