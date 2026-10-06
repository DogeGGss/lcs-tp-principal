using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Personaje único por equipo en el Modo Táctico (US 016).
// - CA1: al empezar la partida (fase de selección de RondasTacticas, 20 s) se abre la selección de la US 015 con los
//   equipos ya armados. Mientras dura, el jugador no se mueve ni dispara.
// - CA2: lo que eligió un compañero aparece bloqueado con "Elegido por [nombre]". Los rivales sí pueden repetir.
// - CA3: si dos compañeros confirman el mismo casi a la vez, se lo queda el primero que llegó al servidor: cada
//   personaje de cada equipo es una propiedad de la sala que se cambia solo si sigue libre (comparar y cambiar).
//   El otro ve "Ya lo eligió [nombre]" y elige de nuevo.
// - CA4: si se termina el tiempo, el último personaje usado si está libre en el equipo, o uno libre al azar.
// - CA5: los bloqueos están en la sala, así que los ve todo el equipo en el momento, aunque entre o se reconecte.
// Lo agrega RondasTacticas al empezar una partida del Modo Táctico.
public class PersonajesTacticos : MonoBehaviour
{
    private const string Prefijo = "pj"; // "pj0.Alfa" = actor que eligió a Alfa en el equipo 0 (0 = libre)
    private const float EsperaMaxima = 3f;

    public static PersonajesTacticos Actual { get; private set; }

    private PartidaEnRed partida;
    private CharacterSelectScreen pantalla;
    private bool elegido, abierta;
    private string pedido;          // clave que se pidió y todavía no respondió el servidor
    private CharacterData pedidoDe;
    private float pedidoHasta;
    private readonly List<Behaviour> trabados = new List<Behaviour>();

    public void Iniciar(PartidaEnRed partida)
    {
        this.partida = partida;
        Actual = this;
        // El anfitrión deja todos los personajes libres (valor 0) para poder "comparar y cambiar" después (CA3).
        if (PhotonNetwork.IsMasterClient && Sala != null && !Sala.CustomProperties.ContainsKey(Clave(0, Primero())))
        {
            var libres = new Hashtable();
            for (int equipo = 0; equipo < 2; equipo++)
                foreach (CharacterData p in CharacterRoster.All) libres[Clave(equipo, p)] = 0;
            Sala.SetCustomProperties(libres);
        }
    }

    private void OnDestroy()
    {
        if (Actual == this) Actual = null;
        Destrabar();
    }

    private static Room Sala => PhotonNetwork.CurrentRoom;
    private static CharacterData Primero() => CharacterRoster.All.Count > 0 ? CharacterRoster.All[0] : null;
    private static string Clave(int equipo, CharacterData p) => p != null ? $"{Prefijo}{equipo}.{p.name}" : "";

    // =====================================================================
    // Cuadro a cuadro
    // =====================================================================

    private void Update()
    {
        RondasTacticas rondas = RondasTacticas.Actual;
        if (rondas == null || !rondas.Listo || CharacterRoster.All.Count == 0) return;
        bool seleccion = rondas.FaseActual == RondasTacticas.Fase.Seleccion;

        if (seleccion)
        {
            Trabar();
            if (!elegido && !abierta && LibresListos()) Abrir(rondas.Restante);
            if (elegido) MatchHud.SetHint($"Esperando a los demás   ·   <color=#F29A38>{Reloj(rondas.Restante)}</color>");
        }
        else
        {
            // Terminó la selección (reloj del anfitrión): si todavía no eligió, se elige solo (CA4).
            if (abierta && pantalla != null && pedido == null) pantalla.ForceTimeUp();
            if (!abierta) { Destrabar(); if (elegido) MatchHud.SetHint(null); }
            // US 195, CA3: el que vuelve a la partida después de la selección sigue con el personaje que había elegido.
            if (!abierta && !elegido) Recuperar();
        }

        Responder();
    }

    private static string Reloj(float segundos)
    {
        int s = Mathf.CeilToInt(segundos);
        return $"{s / 60}:{s % 60:00}";
    }

    // Hasta que no llegan los "libres" del anfitrión no se puede pedir ninguno.
    private static bool LibresListos() => Sala != null && Sala.CustomProperties.ContainsKey(Clave(0, Primero()));

    private void Abrir(float segundos)
    {
        MatchHud hud = MatchHud.Instance;
        string lado = EquiposTacticos.LadoLocal == LadoTactico.Atacante ? "Atacantes" : "Defensores";
        pantalla = CharacterSelectScreen.Show(hud != null ? hud.DisplayFont : null, hud != null ? hud.LabelFont : null,
            hud != null ? hud.LabelFont : null, hud != null ? hud.Rounded : null,
            $"Modo Táctico  ·  Tu equipo: {lado}", Mathf.Max(1f, segundos), AlElegir, null);
        if (pantalla == null) { elegido = true; return; } // no hay personajes
        abierta = true;
        pantalla.LockedBy = ElegidoPor;
        pantalla.ConfirmHandler = Pedir;
    }

    // CA2: el nombre del compañero que lo tiene, o null si está libre (o si es mío).
    private static string ElegidoPor(CharacterData p)
    {
        int actor = Dueno(EquiposTacticos.Local, p);
        if (actor == 0 || actor == PhotonNetwork.LocalPlayer.ActorNumber) return null;
        Player jugador = Sala != null ? Sala.GetPlayer(actor) : null;
        return jugador != null ? jugador.NickName : "un compañero";
    }

    /// <summary>El personaje que eligió ese jugador en su equipo, o null si todavía no eligió (para el marcador).</summary>
    public static CharacterData De(int actor)
    {
        int equipo = EquiposTacticos.DeActor(actor);
        if (equipo < 0 || actor == 0) return null;
        foreach (CharacterData p in CharacterRoster.All)
            if (Dueno(equipo, p) == actor) return p;
        return null;
    }

    private static int Dueno(int equipo, CharacterData p) =>
        Sala != null && Sala.CustomProperties.TryGetValue(Clave(equipo, p), out object v) && v is int actor ? actor : 0;

    // =====================================================================
    // Pedido al servidor (CA3)
    // =====================================================================

    private void Pedir(CharacterData p)
    {
        int equipo = EquiposTacticos.Local, yo = PhotonNetwork.LocalPlayer.ActorNumber;
        string clave = Clave(equipo, p);
        int dueno = Dueno(equipo, p);
        if (dueno == yo) { pantalla.Accept(); return; }
        if (dueno != 0) { pantalla.Reject($"Ya lo eligió {ElegidoPor(p)}"); return; }

        // Se cambia solo si en el servidor sigue libre (0): si otro llegó antes, no pasa nada y se ve su número.
        pedido = clave;
        pedidoDe = p;
        pedidoHasta = Time.unscaledTime + EsperaMaxima;
        Sala.SetCustomProperties(new Hashtable { { clave, yo } }, new Hashtable { { clave, 0 } });
    }

    private void Responder()
    {
        if (pedido == null || pantalla == null) return;
        int yo = PhotonNetwork.LocalPlayer.ActorNumber;
        int dueno = Dueno(EquiposTacticos.Local, pedidoDe);
        if (dueno == yo) { pedido = null; pantalla.Accept(); return; }
        if (dueno != 0) { string quien = ElegidoPor(pedidoDe); pedido = null; pantalla.Reject($"Ya lo eligió {quien}"); return; }
        // Sin respuesta (por ejemplo, se cortó la conexión un momento): se deja elegir de nuevo.
        if (Time.unscaledTime >= pedidoHasta) { pedido = null; pantalla.Reject("No se pudo confirmar, probá de nuevo"); }
    }

    // US 195, CA3: el personaje que tiene en la sala (lo eligió antes de desconectarse), con su habilidad.
    private void Recuperar()
    {
        elegido = true;
        CharacterData mio = De(PhotonNetwork.LocalPlayer.ActorNumber);
        PlayerAbility habilidad = partida != null && partida.Local != null ? partida.Local.GetComponent<PlayerAbility>() : null;
        if (mio != null && habilidad != null) habilidad.UsarPersonaje(mio);
    }

    // Se quedó con el personaje: se usa su habilidad desde ya.
    private void AlElegir()
    {
        elegido = true;
        abierta = false;
        pantalla = null;
        CharacterData p = CharacterRoster.Selected;
        PlayerAbility habilidad = partida != null && partida.Local != null ? partida.Local.GetComponent<PlayerAbility>() : null;
        if (habilidad != null) habilidad.UsarPersonaje(p);
    }

    // =====================================================================
    // Controles trabados durante la selección (CA1)
    // =====================================================================

    private void Trabar()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = abierta;
        if (PauseMenu.Instance != null) PauseMenu.Instance.AllowPause = false;
        if (trabados.Count > 0) return;
        JugadorEnRed local = partida != null ? partida.Local : null;
        if (local == null) return;
        foreach (Behaviour c in local.GetComponentsInChildren<Behaviour>(true))
            if (c.enabled && (c is PlayerMovement || c is CameraLook || c is WeaponSwitcher || c is PlayerAbility))
            {
                c.enabled = false;
                trabados.Add(c);
            }
    }

    private void Destrabar()
    {
        if (trabados.Count == 0) return;
        foreach (Behaviour c in trabados) if (c != null) c.enabled = true;
        trabados.Clear();
        if (PauseMenu.Instance != null) PauseMenu.Instance.AllowPause = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
