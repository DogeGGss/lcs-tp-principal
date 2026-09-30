using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

// Partida online en la escena del mapa (US 025 a US 030). La crea Multijugador cuando se carga un mapa
// estando en una sala. El jugador que ya tiene la escena queda como jugador local (con su HUD, tienda y pausa):
// se le agrega un PhotonView y se avisa a los demás, que arman una copia de él. Cada uno arranca en un punto
// distinto del mapa.
// Táctico (US 031, CA4): cada equipo arranca en los puntos de aparición de su lado (atacante o defensor).
public class PartidaEnRed : MonoBehaviour
{
    public static PartidaEnRed Actual { get; private set; }

    public bool Lista { get; private set; }
    public JugadorEnRed Local { get; private set; }

    private readonly Dictionary<int, JugadorEnRed> jugadores = new Dictionary<int, JugadorEnRed>();
    private readonly List<Pose> puntos = new List<Pose>();          // todos los puntos del mapa
    private readonly List<Pose> puntosAtacante = new List<Pose>();  // los marcados como lado Atacante (US 031)
    private readonly List<Pose> puntosDefensor = new List<Pose>();  // los marcados como lado Defensor (US 031)
    private GameObject molde;

    private GameObject aviso;
    private TextMeshProUGUI avisoTitulo, avisoTexto;

    private void Awake()
    {
        Actual = this;
    }

    private void OnDestroy()
    {
        if (Actual == this) Actual = null;
    }

    // Start como corrutina: en Táctico espera (unos segundos como mucho) a que llegue el reparto de equipos
    // que hizo el anfitrión, para que cada jugador arranque en la base que le toca (US 031).
    private IEnumerator Start()
    {
        molde = ConfigRed.Actual != null ? ConfigRed.Actual.jugador : null;
        if (molde == null) Debug.LogError("PartidaEnRed: falta Resources/ConfigRed con el Player.prefab; no se van a ver los demás jugadores.");

        PlayerMovement movimiento = FindAnyObjectByType<PlayerMovement>();
        if (movimiento == null)
        {
            Debug.LogError("PartidaEnRed: el mapa no tiene jugador.");
            yield break;
        }

        float limite = Time.time + 3f;
        while (MatchSettings.Mode == GameMode.Tactico && !EquiposTacticos.HayEquipos && Time.time < limite)
            yield return null;
        if (MatchSettings.Mode == GameMode.Tactico && !EquiposTacticos.HayEquipos)
            Debug.LogWarning("PartidaEnRed: no llegó el reparto de equipos; esta partida táctica sigue sin equipos.");

        // En online la pausa no congela el juego (US 051, CA4).
        if (PauseMenu.Instance != null) PauseMenu.Instance.SetMultiplayer(true);
        ApagarEnemigosDePrueba();

        BuscarPuntos(movimiento.transform);
        Pose inicio = PuntoInicial();
        JugadorEnRed.Teletransportar(movimiento.transform, inicio.position, inicio.rotation);

        PhotonView vista = movimiento.gameObject.AddComponent<PhotonView>();
        Local = movimiento.gameObject.AddComponent<JugadorEnRed>();
        vista.ObservedComponents = new List<Component> { Local };
        vista.Synchronization = ViewSynchronization.UnreliableOnChange;
        vista.OwnershipTransfer = OwnershipOption.Fixed;
        if (!PhotonNetwork.AllocateViewID(vista))
        {
            Debug.LogError("PartidaEnRed: Photon no dio un id para el jugador.");
            yield break;
        }
        Local.IniciarLocal(this);
        jugadores[PhotonNetwork.LocalPlayer.ActorNumber] = Local;

        // Queda guardado en la sala: los que terminan de cargar el mapa después también lo reciben.
        PhotonNetwork.RaiseEvent(Multijugador.EventoJugador,
            new object[] { vista.ViewID, inicio.position, inicio.rotation.eulerAngles.y },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others, CachingOption = EventCaching.AddToRoomCache },
            SendOptions.SendReliable);

        Lista = true;
        if (MatchSettings.Mode == GameMode.Tactico) gameObject.AddComponent<MarcadorTactico>().Iniciar(this); // US 134
        foreach (object[] datos in Multijugador.Instancia.TomarPendientes()) CrearRemoto(datos);
    }

    // ================= Jugadores =================

    public IEnumerable<JugadorEnRed> Jugadores => jugadores.Values;

    /// <summary>US 057: el arma que tiene en la mano ese jugador, para los avisos de bajas.</summary>
    public string ArmaDe(int actor) => jugadores.TryGetValue(actor, out JugadorEnRed j) && j != null ? j.NombreArma : "";

    /// <summary>El jugador con ese número de actor, o null.</summary>
    public JugadorEnRed Buscar(int actor) => jugadores.TryGetValue(actor, out JugadorEnRed j) ? j : null;

    public void CrearRemoto(object[] datos)
    {
        if (datos.Length < 3 || !(datos[0] is int viewId) || !(datos[1] is Vector3 posicion) || !(datos[2] is float yaw)) return;
        int actor = viewId / PhotonNetwork.MAX_VIEW_IDS;
        Player dueno = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.GetPlayer(actor) : null;
        if (dueno == null || dueno.IsLocal || jugadores.ContainsKey(actor) || molde == null) return;

        jugadores[actor] = JugadorEnRed.CrearCopia(molde, posicion, Quaternion.Euler(0f, yaw, 0f), viewId, dueno, this);
        Debug.Log($"PartidaEnRed: aparece {dueno.NickName}.");
    }

    public void QuitarJugador(int actor)
    {
        if (!jugadores.TryGetValue(actor, out JugadorEnRed jugador)) return;
        jugadores.Remove(actor);
        if (jugador != null && jugador != Local) Destroy(jugador.gameObject);
    }

    // El mapa de pruebas genera enemigos: en online cada computadora tendría los suyos, así que se apagan.
    private static void ApagarEnemigosDePrueba()
    {
        foreach (EnemySpawner spawner in FindObjectsByType<EnemySpawner>())
        {
            spawner.StopSpawning();
            spawner.enabled = false;
        }
        foreach (EnemyChaser enemigo in FindObjectsByType<EnemyChaser>())
            Destroy(enemigo.gameObject);
    }

    // ================= Puntos de aparición =================

    // Los PuntoDeAparicion del mapa; si no tiene, lugares libres con piso alrededor de donde arranca el jugador.
    private void BuscarPuntos(Transform inicio)
    {
        puntos.Clear();
        puntosAtacante.Clear();
        puntosDefensor.Clear();

        PuntoDeAparicion[] marcados = FindObjectsByType<PuntoDeAparicion>();
        System.Array.Sort(marcados, (a, b) => string.CompareOrdinal(a.name, b.name));
        foreach (PuntoDeAparicion punto in marcados)
        {
            Pose lugar = new Pose(punto.transform.position, Quaternion.Euler(0f, punto.transform.eulerAngles.y, 0f));
            puntos.Add(lugar);
            if (punto.lado == LadoTactico.Atacante) puntosAtacante.Add(lugar);
            else if (punto.lado == LadoTactico.Defensor) puntosDefensor.Add(lugar);
        }
        if (puntos.Count >= 2) return;

        puntos.Clear();
        puntosAtacante.Clear();
        puntosDefensor.Clear();
        Quaternion mirada = Quaternion.Euler(0f, inicio.eulerAngles.y, 0f);
        puntos.Add(new Pose(inicio.position, mirada));

        const int capas = Physics.DefaultRaycastLayers;
        const QueryTriggerInteraction sinZonas = QueryTriggerInteraction.Ignore;
        if (!Physics.Raycast(inicio.position, Vector3.down, out RaycastHit piso, 5f, capas, sinZonas)) return;
        float sobrePiso = inicio.position.y - piso.point.y;

        CharacterController cuerpo = inicio.GetComponent<CharacterController>();
        float alto = cuerpo != null ? cuerpo.height : 2f, radio = cuerpo != null ? cuerpo.radius : 0.5f;
        float medio = alto / 2f - radio;

        for (int i = 0; i < 8 && puntos.Count < Multijugador.MaxJugadores; i++)
        {
            Vector3 direccion = Quaternion.Euler(0f, inicio.eulerAngles.y + 45f * (i + 1), 0f) * Vector3.forward;
            foreach (float distancia in new[] { 3f, 2f, 4.5f })
            {
                Vector3 arriba = inicio.position + direccion * distancia + Vector3.up * 1.5f;
                if (!Physics.Raycast(arriba, Vector3.down, out RaycastHit suelo, 6f, capas, sinZonas)) continue;
                Vector3 centro = suelo.point + Vector3.up * sobrePiso;
                if (Physics.Linecast(inicio.position, centro, capas, sinZonas)) continue; // pared en el medio
                if (Physics.CheckCapsule(centro + Vector3.up * medio, centro - Vector3.up * (medio - 0.1f), radio * 0.9f, capas, sinZonas)) continue;
                puntos.Add(new Pose(centro, mirada));
                break;
            }
        }
    }

    // Puntos marcados para un lado (vacío si el mapa no los tiene).
    private List<Pose> PuntosDelLado(LadoTactico lado)
    {
        return lado == LadoTactico.Atacante ? puntosAtacante : lado == LadoTactico.Defensor ? puntosDefensor : new List<Pose>();
    }

    // Cada jugador en un punto distinto. En Táctico (US 031, CA4) cada equipo usa los puntos de su lado, uno
    // por compañero; si no, según el orden en que entró a la sala.
    private Pose PuntoInicial()
    {
        var actores = new List<int>();
        foreach (Player p in PhotonNetwork.PlayerList) actores.Add(p.ActorNumber);
        actores.Sort();
        int local = PhotonNetwork.LocalPlayer.ActorNumber;

        if (EquiposTacticos.HayEquipos)
        {
            int equipo = EquiposTacticos.DeActor(local);
            LadoTactico lado = EquiposTacticos.LadoDeEquipo(equipo);
            List<Pose> propios = PuntosDelLado(lado);
            if (propios.Count > 0)
            {
                // Puesto dentro del equipo: cuántos compañeros entraron antes que yo.
                int puesto = 0;
                foreach (int actor in actores)
                {
                    if (actor == local) break;
                    if (EquiposTacticos.DeActor(actor) == equipo) puesto++;
                }
                return propios[puesto % propios.Count];
            }
            Debug.LogWarning($"PartidaEnRed: el mapa no tiene PuntoDeAparicion con lado {lado}; se reparte como si no hubiera equipos.");
        }

        int indice = Mathf.Max(0, actores.IndexOf(local));
        return puntos[indice % puntos.Count];
    }

    // US 030, CA5 (y US 137, CA2): el punto más lejos del rival vivo más cercano.
    public Pose PuntoDeReaparicion()
    {
        Pose mejor = puntos[0];
        float mejorDistancia = -1f;
        foreach (Pose punto in puntos)
        {
            float cercano = float.MaxValue;
            foreach (JugadorEnRed jugador in jugadores.Values)
                if (jugador != null && jugador != Local && jugador.Vivo)
                    cercano = Mathf.Min(cercano, Vector3.Distance(punto.position, jugador.transform.position));
            if (cercano > mejorDistancia)
            {
                mejorDistancia = cercano;
                mejor = punto;
            }
        }
        return mejor;
    }

    // ================= Aviso en pantalla =================

    // Cartel al medio de la pantalla (por ejemplo, "Te eliminó Luka" y la cuenta para reaparecer). Null lo oculta.
    public void Aviso(string titulo, string texto = "")
    {
        if (string.IsNullOrEmpty(titulo))
        {
            if (aviso != null) aviso.SetActive(false);
            return;
        }
        if (aviso == null) ArmarAviso();
        avisoTitulo.text = titulo;
        avisoTexto.text = texto;
        aviso.SetActive(true);
    }

    private void ArmarAviso()
    {
        aviso = new GameObject("AvisoRed", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        aviso.transform.SetParent(transform, false);
        var canvas = aviso.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var escala = aviso.GetComponent<UnityEngine.UI.CanvasScaler>();
        escala.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.matchWidthOrHeight = 0.5f;

        avisoTitulo = Texto("Titulo", 64f, new Vector2(0f, 90f));
        avisoTitulo.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        avisoTexto = Texto("Texto", 34f, new Vector2(0f, 20f));
    }

    private TextMeshProUGUI Texto(string nombre, float tamano, Vector2 posicion)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(aviso.transform, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1400f, tamano * 1.4f);
        rect.anchoredPosition = posicion;
        var texto = go.AddComponent<TextMeshProUGUI>();
        texto.fontSize = tamano;
        texto.alignment = TextAlignmentOptions.Center;
        texto.color = Color.white;
        texto.outlineWidth = 0.2f;
        texto.outlineColor = new Color32(0, 0, 0, 200);
        texto.raycastTarget = false;
        return texto;
    }
}
