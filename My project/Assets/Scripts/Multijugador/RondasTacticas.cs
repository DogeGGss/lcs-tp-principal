using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Rondas del Modo Táctico (US 032). La partida se juega por rondas hasta que un equipo gana 7:
// compra de 20 s (encerrados en la base y sin disparar), combate de 1:40 y un cartel de 5 s con el ganador.
// - CA1: la ronda 1 arranca con los equipos formados (US 031), cada uno en su base con $ 800 y la pistola.
// - CA2: en la compra cada uno queda dentro de la zona de compra de su lado y no puede disparar ni acuchillar.
// - CA3: al terminar la compra se abre la base y corre el reloj del combate (lo muestra MarcadorTactico).
// - CA4: al terminar la ronda, cartel con el ganador 5 s; después todos vuelven a su base (los muertos reviven).
// - CA5: desde la ronda 7 los lados se intercambian, con cartel; la plata vuelve a $ 800 y las armas a la pistola.
// - CA6: con 6 a 6 se juega la muerte súbita: lados sorteados de nuevo y $ 5.000 para todos.
// - CA7: el que muere no reaparece hasta la ronda siguiente (JugadorEnRed ya lo deja esperando).
// - CA8: música y ambiente del modo, por el grupo Musica del mezclador (se configuran en ConfigRed).
// Cómo funciona: el anfitrión decide los cambios de fase y los publica como propiedades de la sala (ronda, fase,
// hora del servidor en que termina la fase, rondas ganadas). Cada computadora lee esas propiedades y hace lo suyo
// (reubicar al jugador local, la tienda, los carteles). Si el anfitrión se va, Photon elige otro y sigue igual.
// Para las otras US:
// - US 033: DecidirGanador() tiene la regla base (eliminación, o gana el que defiende si se acaba el tiempo).
// - US 131 / 132: FijarFinDeCombate(segundos) al plantar y TerminarRonda(equipo, motivo) al explotar o desactivar.
// - US 034: evento PartidaTerminada(equipo ganador). US 135: evento RondaTerminada(equipo ganador, motivo).
// Lo agrega PartidaEnRed al empezar una partida del Modo Táctico.
public class RondasTacticas : MonoBehaviour
{
    public const float DuracionCompra = 20f, DuracionCombate = 100f, DuracionCartel = 5f, DuracionAviso = 4f;
    public const int PlataInicial = 800, PlataMuerteSubita = 5000;

    public enum Fase { Compra, Combate, FinDeRonda, Terminada }
    public enum Motivo { Eliminacion, Tiempo, DispositivoExploto, DispositivoDesactivado }

    private const string PropRonda = "rt.ronda", PropFase = "rt.fase", PropFin = "rt.fin", PropGanador = "rt.gan",
        PropMotivo = "rt.mot", PropRondas0 = "rt.e0", PropRondas1 = "rt.e1", PropSubita = "rt.sub";

    public static RondasTacticas Actual { get; private set; }

    /// <summary>US 034: terminó la partida (equipo ganador, 0 o 1).</summary>
    public static event System.Action<int> PartidaTerminada;

    /// <summary>US 033 / US 135: terminó una ronda (equipo ganador, motivo). Llega en todas las computadoras.</summary>
    public static event System.Action<int, Motivo> RondaTerminada;

    public static int RondasParaGanar => MarcadorTactico.RondasParaGanar;
    /// <summary>La ronda 13: solo se llega con 6 a 6.</summary>
    public static int RondaMuerteSubita => 2 * (RondasParaGanar - 1) + 1;

    // ---------- Estado (lo que publicó el anfitrión) ----------

    public bool Listo => Sala != null && Sala.CustomProperties.ContainsKey(PropFase);
    public int Ronda => Leer(PropRonda, 1);
    public Fase FaseActual => (Fase)Leer(PropFase, 0);
    public int Ganador => Leer(PropGanador, -1);
    public Motivo MotivoActual => (Motivo)Leer(PropMotivo, 0);
    public bool EsMuerteSubita => Ronda >= RondaMuerteSubita;
    public int RondasDe(int equipo) => Leer(equipo == 0 ? PropRondas0 : PropRondas1, 0);

    /// <summary>Segundos que le quedan a la fase actual (según la hora del servidor, igual en todas las computadoras).</summary>
    public float Restante => Listo ? Mathf.Max(0f, unchecked(Leer(PropFin, Ahora) - Ahora) / 1000f) : 0f;

    private static Room Sala => PhotonNetwork.CurrentRoom;
    private static int Ahora => PhotonNetwork.ServerTimestamp;

    private PartidaEnRed partida;
    private int rondaVista = -1;
    private Fase faseVista;
    private bool primeraVez = true;
    private float esperaAnfitrion;
    private Vector3? ultimoDentro;
    private readonly List<Behaviour> armasBloqueadas = new List<Behaviour>();

    // =====================================================================
    // Inicio
    // =====================================================================

    public void Iniciar(PartidaEnRed partida)
    {
        this.partida = partida;
        Actual = this;

        // CA1: el anfitrión arranca la ronda 1. La sala se cierra al iniciar, así que hay una sola partida por sala.
        if (PhotonNetwork.IsMasterClient && Sala != null && !Sala.CustomProperties.ContainsKey(PropFase))
            Publicar(new Hashtable
            {
                { PropRonda, 1 }, { PropFase, (int)Fase.Compra }, { PropFin, Ahora + Ms(DuracionCompra) },
                { PropRondas0, 0 }, { PropRondas1, 0 }, { PropGanador, -1 }
            });

        MarcadorTactico.TiempoDeRonda = TiempoDeCombate;
        ArrancarMusica();
        if (PruebaSolo.Activa)
            Debug.Log("Prueba solo (Táctico): F9 gana tu equipo · F10 gana el rival · F11 salta la fase · F8 pone 6 a 6.");
    }

    private void OnDestroy()
    {
        if (Actual != this) return;
        Actual = null;
        MarcadorTactico.TiempoDeRonda = null;
        BloquearArmas(false);
        MatchHud.SetHint(null);
    }

    // =====================================================================
    // Cuadro a cuadro
    // =====================================================================

    private void Update()
    {
        if (!Listo) return;
        if (PhotonNetwork.IsMasterClient) { Atajos(); Anfitrion(); }

        int ronda = Ronda;
        Fase fase = FaseActual;
        SincronizarCompra(ronda, fase);
        if (ronda != rondaVista || fase != faseVista)
        {
            AlCambiar(ronda, fase, primeraVez);
            rondaVista = ronda;
            faseVista = fase;
            primeraVez = false;
        }

        if (fase == Fase.Compra) { EncerrarEnBase(); Ayuda(); }
        else MatchHud.SetHint(null);

        MarcadorTactico.SetRondas(RondasDe(0), RondasDe(1));
    }

    // Lo que ve el marcador en el centro después de la compra.
    private float? TiempoDeCombate()
    {
        Fase fase = FaseActual;
        if (fase == Fase.Combate) return Restante;
        if (fase == Fase.FinDeRonda || fase == Fase.Terminada) return 0f;
        return null;
    }

    // =====================================================================
    // Anfitrión: decide cuándo cambia la fase
    // =====================================================================

    private void Anfitrion()
    {
        // Lo que publica tarda un momento en volver: mientras tanto no decide de nuevo.
        if (Time.unscaledTime < esperaAnfitrion) return;
        switch (FaseActual)
        {
            case Fase.Compra:
                if (Restante <= 0f) CambiarFase(Fase.Combate, DuracionCombate); // CA3
                break;
            case Fase.Combate:
                if (DecidirGanador(out int ganador, out Motivo motivo)) TerminarRonda(ganador, motivo); // CA4
                break;
            case Fase.FinDeRonda:
                if (Restante <= 0f) SiguienteRonda();
                break;
        }
    }

    /// <summary>
    /// US 033 (regla base): gana el equipo que deja al otro sin jugadores vivos; si se acaba el tiempo, gana el que
    /// defiende. Un equipo sin jugadores (por ejemplo, probando solo) no pierde por eliminación.
    /// </summary>
    private bool DecidirGanador(out int ganador, out Motivo motivo)
    {
        motivo = Motivo.Eliminacion;
        for (int equipo = 0; equipo < 2; equipo++)
        {
            if (Jugadores(equipo) > 0 && Vivos(equipo) == 0)
            {
                ganador = 1 - equipo;
                return true;
            }
        }
        motivo = Motivo.Tiempo;
        ganador = EquipoDelLado(LadoTactico.Defensor);
        return Restante <= 0f;
    }

    /// <summary>US 131 (solo el anfitrión): el combate pasa a terminar en "segundos" (por ejemplo, 45 s al plantar).</summary>
    public void FijarFinDeCombate(float segundos)
    {
        if (!PhotonNetwork.IsMasterClient || FaseActual != Fase.Combate) return;
        Publicar(new Hashtable { { PropFin, Ahora + Ms(segundos) } });
    }

    /// <summary>US 033 / 131 / 132 (solo el anfitrión): termina la ronda con ese ganador.</summary>
    public void TerminarRonda(int ganador, Motivo motivo)
    {
        if (!PhotonNetwork.IsMasterClient || FaseActual != Fase.Combate || ganador < 0) return;
        int e0 = RondasDe(0) + (ganador == 0 ? 1 : 0);
        int e1 = RondasDe(1) + (ganador == 1 ? 1 : 0);
        bool termina = e0 >= RondasParaGanar || e1 >= RondasParaGanar;
        Publicar(new Hashtable
        {
            { PropFase, (int)(termina ? Fase.Terminada : Fase.FinDeRonda) }, { PropFin, Ahora + Ms(DuracionCartel) },
            { PropGanador, ganador }, { PropMotivo, (int)motivo }, { PropRondas0, e0 }, { PropRondas1, e1 }
        }, true);
    }

    private void CambiarFase(Fase fase, float segundos)
    {
        Publicar(new Hashtable { { PropFase, (int)fase }, { PropFin, Ahora + Ms(segundos) } }, true);
    }

    private void SiguienteRonda()
    {
        int ronda = Ronda + 1;
        var datos = new Hashtable
        {
            { PropRonda, ronda }, { PropFase, (int)Fase.Compra }, { PropFin, Ahora + Ms(DuracionCompra) }, { PropGanador, -1 }
        };
        if (ronda == RondaMuerteSubita) datos[PropSubita] = Random.Range(0, 2); // CA6: nuevo sorteo de lados
        Publicar(datos, true);
    }

    // conControl: solo se aplica si la ronda y la fase siguen siendo las que vio (por si dos deciden a la vez).
    private void Publicar(Hashtable datos, bool conControl = false)
    {
        if (Sala == null) return;
        Hashtable esperado = conControl ? new Hashtable { { PropRonda, Ronda }, { PropFase, (int)FaseActual } } : null;
        Sala.SetCustomProperties(datos, esperado);
        esperaAnfitrion = Time.unscaledTime + 0.5f;
    }

    private int Jugadores(int equipo)
    {
        int n = 0;
        foreach (Player p in PhotonNetwork.PlayerList) if (EquiposTacticos.DeActor(p.ActorNumber) == equipo) n++;
        return n;
    }

    private int Vivos(int equipo)
    {
        int n = 0;
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            if (EquiposTacticos.DeActor(p.ActorNumber) != equipo) continue;
            JugadorEnRed j = partida != null ? partida.Buscar(p.ActorNumber) : null;
            if (j == null || j.Vivo) n++; // si todavía no cargó, cuenta como vivo
        }
        return n;
    }

    private static int EquipoDelLado(LadoTactico lado)
    {
        for (int equipo = 0; equipo < 2; equipo++)
            if (EquiposTacticos.LadoDeEquipo(equipo) == lado) return equipo;
        return -1;
    }

    // =====================================================================
    // Todas las computadoras: lo que pasa al cambiar de fase
    // =====================================================================

    private void AlCambiar(int ronda, Fase fase, bool primeraVez)
    {
        switch (fase)
        {
            case Fase.Compra:
                ultimoDentro = null;
                // La ronda 1 ya arranca en la base con $ 800 y la pistola (CA1). Las siguientes, todos vuelven (CA4).
                if (!primeraVez && ronda > 1) EmpezarRonda(ronda);
                BloquearArmas(true);
                break;

            case Fase.Combate:
                BloquearArmas(false);
                break;

            case Fase.FinDeRonda:
                BloquearArmas(false);
                if (primeraVez) break;
                CartelFinDeRonda(ronda);
                RondaTerminada?.Invoke(Ganador, MotivoActual);
                break;

            case Fase.Terminada:
                BloquearArmas(false);
                CartelFinDePartida();
                if (!primeraVez) RondaTerminada?.Invoke(Ganador, MotivoActual);
                PartidaTerminada?.Invoke(Ganador);
                break;
        }
    }

    private void EmpezarRonda(int ronda)
    {
        JugadorEnRed local = partida != null ? partida.Local : null;
        if (local == null) return;
        local.EmpezarRonda(partida.PuntoDeBase());

        PlayerWallet billetera = local.GetComponent<PlayerWallet>();
        PlayerLoadout equipo = local.GetComponent<PlayerLoadout>();
        bool ataca = EquiposTacticos.LadoLocal == LadoTactico.Atacante;

        if (ronda == RondaMuerteSubita)
        {
            // CA6
            if (billetera != null) billetera.Set(PlataMuerteSubita);
            MatchHud.ShowBanner($"{RondasDe(0)} a {RondasDe(1)}", "Muerte súbita", MatchHud.RivalColor,
                "El equipo que gane esta ronda gana la partida",
                new[] { $"Lados sorteados: {(ataca ? "atacás" : "defendés")}", $"Todos reciben {ShopUIKit.Money(PlataMuerteSubita)}" },
                DuracionAviso);
        }
        else if (ronda == EquiposTacticos.RondaDeCambio)
        {
            // CA5
            if (billetera != null) billetera.Set(PlataInicial);
            if (equipo != null) equipo.LoseEquipment();
            MatchHud.ShowBanner("Mitad de la partida", "Cambio de lado", ShopUIKit.Accent,
                $"Ahora tu equipo <color=#F3F4F6>{(ataca ? "ataca" : "defiende")}</color>",
                new[] { $"Plata: {ShopUIKit.Money(PlataInicial)}", "Armas: pistola" }, DuracionAviso);
        }
    }

    private void CartelFinDeRonda(int ronda)
    {
        int mio = Mathf.Max(0, EquiposTacticos.Local);
        bool gane = Ganador == mio;
        MatchHud.ShowBanner($"Ronda {ronda}", gane ? "Ronda ganada" : "Ronda perdida",
            gane ? MatchHud.TeamColor : MatchHud.RivalColor, $"{TextoMotivo(gane)}  ·  {Resultado(mio)}",
            null, Mathf.Max(0.5f, Restante), "Siguiente ronda en {0}");
    }

    // US 034 arma la pantalla final; mientras tanto queda este cartel.
    private void CartelFinDePartida()
    {
        int mio = Mathf.Max(0, EquiposTacticos.Local);
        bool gane = Ganador == mio;
        MatchHud.ShowBanner("Fin de la partida", gane ? "Victoria" : "Derrota",
            gane ? MatchHud.TeamColor : MatchHud.RivalColor, $"{TextoMotivo(gane)}  ·  {Resultado(mio)}", null, 0f);
    }

    private string TextoMotivo(bool gane)
    {
        switch (MotivoActual)
        {
            case Motivo.Tiempo:
                return gane ? "Se terminó el tiempo y tu equipo defendió" : "Se terminó el tiempo y los rivales defendieron";
            case Motivo.DispositivoExploto: return "Explotó el dispositivo";
            case Motivo.DispositivoDesactivado: return "Se desactivó el dispositivo";
            default: return gane ? "Tu equipo eliminó a todos los rivales" : "Los rivales eliminaron a tu equipo";
        }
    }

    private string Resultado(int mio) =>
        $"<color=#58A6FF>{RondasDe(mio)}</color> a <color=#FF5C5C>{RondasDe(1 - mio)}</color>";

    // =====================================================================
    // Fase de compra (CA2)
    // =====================================================================

    // La tienda (BuyPhase) copia lo que publicó el anfitrión. Si el mapa no tiene, se crea una.
    private void SincronizarCompra(int ronda, Fase fase)
    {
        BuyPhase compra = BuyPhase.Current;
        if (compra == null) compra = gameObject.AddComponent<BuyPhase>();
        float restante = Restante;
        if (fase == Fase.Compra && restante > 0f) compra.Sincronizar(ronda, restante);
        else
        {
            compra.Terminar();
            compra.PonerRonda(ronda);
        }
    }

    // Mientras dura la compra, el jugador no puede salir de la zona de compra de su lado.
    private void EncerrarEnBase()
    {
        Transform jugador = partida != null && partida.Local != null ? partida.Local.transform : null;
        if (jugador == null) return;
        LadoTactico lado = EquiposTacticos.LadoLocal;
        if (BuyZone.Contains(jugador.position, lado, true))
        {
            ultimoDentro = jugador.position;
            return;
        }
        // Si arrancó afuera (el mapa no tiene la zona donde aparece), no se lo traba.
        if (!ultimoDentro.HasValue) return;
        JugadorEnRed.Teletransportar(jugador, ultimoDentro.Value, jugador.rotation);
        MatchHud.Warn("No podés salir de la base durante la compra");
    }

    // No se dispara ni se acuchilla durante la compra (los pájaros del minijuego son de la US 157).
    private void BloquearArmas(bool bloquear)
    {
        if (!bloquear)
        {
            foreach (Behaviour arma in armasBloqueadas) if (arma != null) arma.enabled = true;
            armasBloqueadas.Clear();
            return;
        }
        JugadorEnRed local = partida != null ? partida.Local : null;
        if (local == null || armasBloqueadas.Count > 0) return;
        foreach (Behaviour componente in local.GetComponentsInChildren<Behaviour>(true))
            if (componente.enabled && (componente is Pistola || componente is Mitre || componente is ArmaDeFuego || componente is MeleeAttack))
            {
                componente.enabled = false;
                armasBloqueadas.Add(componente);
            }
    }

    private void Ayuda()
    {
        int s = Mathf.CeilToInt(Restante);
        // La tecla de la tienda ya la muestra la tienda arriba ("B Tienda").
        MatchHud.SetHint($"La salida de la base se abre en <color=#F29A38>{s / 60}:{s % 60:00}</color>");
    }

    // =====================================================================
    // Música y ambiente (CA8)
    // =====================================================================

    private void ArrancarMusica()
    {
        ConfigRed config = ConfigRed.Actual;
        if (config == null) return;
        Fuente(config.musicaTactico, config.volumenMusica, config.grupoMusica);
        Fuente(config.ambienteTactico, config.volumenAmbiente, config.grupoMusica);
    }

    private void Fuente(AudioClip clip, float volumen, UnityEngine.Audio.AudioMixerGroup grupo)
    {
        if (clip == null) return;
        AudioSource fuente = gameObject.AddComponent<AudioSource>();
        fuente.clip = clip;
        fuente.loop = true;
        fuente.playOnAwake = false;
        fuente.spatialBlend = 0f;
        fuente.volume = volumen;
        fuente.outputAudioMixerGroup = grupo;
        fuente.Play();
    }

    // =====================================================================
    // Prueba solo (PruebaSolo, solo en el editor)
    // =====================================================================

    private void Atajos()
    {
        if (!PruebaSolo.Activa || Time.unscaledTime < esperaAnfitrion) return;
        int mio = Mathf.Max(0, EquiposTacticos.Local);
        Fase fase = FaseActual;
        if (fase == Fase.Terminada) return;

        if (Input.GetKeyDown(KeyCode.F9)) ForzarGanador(mio);
        else if (Input.GetKeyDown(KeyCode.F10)) ForzarGanador(1 - mio);
        else if (Input.GetKeyDown(KeyCode.F11))
        {
            // La fase actual termina ya; en el combate, como si se acabara el tiempo.
            Publicar(new Hashtable { { PropFin, Ahora } });
        }
        else if (Input.GetKeyDown(KeyCode.F8))
        {
            // Termina la ronda 12 con 6 a 6: la siguiente es la muerte súbita.
            Publicar(new Hashtable
            {
                { PropRonda, RondaMuerteSubita - 1 }, { PropFase, (int)Fase.FinDeRonda }, { PropFin, Ahora + Ms(1f) },
                { PropGanador, mio }, { PropMotivo, (int)Motivo.Eliminacion },
                { PropRondas0, RondasParaGanar - 1 }, { PropRondas1, RondasParaGanar - 1 }
            });
        }
    }

    // En la compra primero pasa al combate, así la ronda termina como en una partida de verdad.
    private void ForzarGanador(int equipo)
    {
        if (FaseActual == Fase.Compra) { CambiarFase(Fase.Combate, DuracionCombate); return; }
        if (FaseActual == Fase.Combate) TerminarRonda(equipo, Motivo.Eliminacion);
    }

    // =====================================================================
    // Para EquiposTacticos
    // =====================================================================

    /// <summary>CA6: en la muerte súbita, el equipo que ataca según el nuevo sorteo.</summary>
    public static bool AtacanteMuerteSubita(out int equipo)
    {
        equipo = -1;
        if (Actual == null || !Actual.EsMuerteSubita || Sala == null) return false;
        if (!Sala.CustomProperties.TryGetValue(PropSubita, out object valor) || !(valor is int e)) return false;
        equipo = e;
        return true;
    }

    // ---------- Ayudas ----------

    private static int Leer(string clave, int porDefecto)
    {
        Room sala = Sala;
        return sala != null && sala.CustomProperties.TryGetValue(clave, out object valor) && valor is int n ? n : porDefecto;
    }

    private static int Ms(float segundos) => Mathf.RoundToInt(segundos * 1000f);
}
