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
// Ganador de la ronda (US 033), en DecidirGanador:
// - CA1: si el dispositivo plantado llega a 0, explota y ganan los atacantes (aunque no quede ninguno vivo);
//   la explosión elimina a los que estén a menos de RadioExplosion metros.
// - CA2: si un defensor lo desactiva, ganan los defensores.
// - CA3: si mueren todos los defensores, ganan los atacantes.
// - CA4: si mueren todos los atacantes antes de plantar, ganan los defensores; con el dispositivo plantado la
//   ronda sigue hasta que explote o lo desactiven.
// - CA5: si se termina el tiempo sin plantar, ganan los defensores.
// - CA6: el ganador suma una ronda y se pasa al cartel de fin de ronda; con 7, termina la partida.
// Para las otras US:
// - US 131: RondasTacticas.Plantar(lugar) cuando se termina de plantar: el reloj pasa a los 45 s del dispositivo.
// - US 132: RondasTacticas.Desactivar() cuando un defensor termina de desactivarlo.
//   Los eventos DispositivoPlantado / DispositivoExploto / DispositivoDesactivado llegan a todas las computadoras
//   (para el modelo, los sonidos y la explosión).
// - US 034: evento PartidaTerminada(equipo ganador). US 135: evento RondaTerminada(equipo ganador, motivo).
// Lo agrega PartidaEnRed al empezar una partida del Modo Táctico.
public class RondasTacticas : MonoBehaviour
{
    public const float DuracionCompra = 20f, DuracionCombate = 100f, DuracionCartel = 5f, DuracionAviso = 4f;
    public const int PlataInicial = 800, PlataMuerteSubita = 5000;
    public const float DuracionDispositivo = 45f; // F07: 45 s de dispositivo plantado

    // Economía (US 135). El tope de $ 9.000 lo pone la billetera (CA7).
    public const int PremioBaja = 200, PremioGanada = 3000, PremioPlantar = 300;
    private static readonly int[] PremiosDerrota = { 1900, 2400, 2900 }; // 1.ª, 2.ª y 3.ª derrota seguida o más
    public const float RadioExplosion = 12f;      // US 033, CA1: metros alrededor del dispositivo

    public enum Fase { Compra, Combate, FinDeRonda, Terminada }
    public enum Motivo { Eliminacion, Tiempo, DispositivoExploto, DispositivoDesactivado }

    private const string PropRonda = "rt.ronda", PropFase = "rt.fase", PropFin = "rt.fin", PropGanador = "rt.gan",
        PropMotivo = "rt.mot", PropRondas0 = "rt.e0", PropRondas1 = "rt.e1", PropSubita = "rt.sub",
        PropDispositivo = "rt.disp", PropLugar = "rt.lugar", PropDesactivado = "rt.desact",
        PropPlantador = "rt.plantador", PropDesactivador = "rt.desactivador",
        PropRacha0 = "rt.racha0", PropRacha1 = "rt.racha1"; // derrotas seguidas de cada equipo (US 135, CA4)

    /// <summary>US 034: estadísticas que cada jugador publica de sí mismo (plantadas y desactivaciones).</summary>
    public const string PropPlantadas = "st.pl", PropDesactivaciones = "st.des";

    public static RondasTacticas Actual { get; private set; }

    /// <summary>US 034: terminó la partida (equipo ganador, 0 o 1).</summary>
    public static event System.Action<int> PartidaTerminada;

    /// <summary>US 033 / US 135: terminó una ronda (equipo ganador, motivo). Llega en todas las computadoras.</summary>
    public static event System.Action<int, Motivo> RondaTerminada;

    /// <summary>US 131 / 132: el dispositivo se plantó, explotó o se desactivó (llega en todas las computadoras).</summary>
    public static event System.Action<Vector3> DispositivoPlantado, DispositivoExploto;
    public static event System.Action DispositivoDesactivado;

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

    /// <summary>US 033: el dispositivo está plantado en esta ronda.</summary>
    public bool HayDispositivo => Leer(PropDispositivo, 0) == 1;
    /// <summary>Dónde se plantó el dispositivo de esta ronda.</summary>
    public Vector3 LugarDelDispositivo =>
        Sala != null && Sala.CustomProperties.TryGetValue(PropLugar, out object v) && v is Vector3 lugar ? lugar : Vector3.zero;

    /// <summary>Segundos que le quedan a la fase actual (según la hora del servidor, igual en todas las computadoras).</summary>
    public float Restante => Listo ? Mathf.Max(0f, unchecked(Leer(PropFin, Ahora) - Ahora) / 1000f) : 0f;

    private static Room Sala => PhotonNetwork.CurrentRoom;
    private static int Ahora => PhotonNetwork.ServerTimestamp;

    private PartidaEnRed partida;
    private int rondaVista = -1;
    private Fase faseVista;
    private bool primeraVez = true;
    private float esperaAnfitrion;
    private int dispositivoVisto;
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
                { PropRondas0, 0 }, { PropRondas1, 0 }, { PropGanador, -1 }, { PropDispositivo, 0 }, { PropDesactivado, 0 }
            });

        MarcadorTactico.TiempoDeRonda = TiempoDeCombate;
        ArrancarMusica();
        if (PruebaSolo.Activa)
            Debug.Log("Prueba solo (Táctico): F4 cobrar una baja · F5 morir · F6 plantar acá · F7 desactivar · F9 gana tu equipo · " +
                      "F10 gana el rival · F11 salta la fase · F8 pone 6 a 6.");
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

        VerDispositivo(fase);

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
    /// US 033: decide si la ronda terminó y quién ganó. Un equipo sin jugadores (por ejemplo, probando solo) no
    /// pierde por eliminación.
    /// </summary>
    private bool DecidirGanador(out int ganador, out Motivo motivo)
    {
        int atacante = EquipoDelLado(LadoTactico.Atacante);
        int defensor = EquipoDelLado(LadoTactico.Defensor);
        bool sinDefensores = Eliminado(defensor);

        if (HayDispositivo)
        {
            // CA2: lo desactivaron.
            if (Leer(PropDesactivado, 0) == 1) { ganador = defensor; motivo = Motivo.DispositivoDesactivado; return true; }
            // CA1: llegó a 0 sin que lo desactiven (aunque no quede ningún atacante vivo).
            if (Restante <= 0f) { ganador = atacante; motivo = Motivo.DispositivoExploto; return true; }
            // CA3: murieron todos los defensores.
            if (sinDefensores) { ganador = atacante; motivo = Motivo.Eliminacion; return true; }
            // CA4: si murieron los atacantes, la ronda sigue hasta que explote o lo desactiven.
            ganador = -1; motivo = Motivo.Eliminacion;
            return false;
        }

        // CA3 y CA4 (sin plantar).
        if (sinDefensores) { ganador = atacante; motivo = Motivo.Eliminacion; return true; }
        if (Eliminado(atacante)) { ganador = defensor; motivo = Motivo.Eliminacion; return true; }
        // CA5: se terminó el tiempo sin plantar.
        ganador = defensor;
        motivo = Motivo.Tiempo;
        return Restante <= 0f;
    }

    private bool Eliminado(int equipo) => equipo >= 0 && Jugadores(equipo) > 0 && Vivos(equipo) == 0;

    /// <summary>US 131: se terminó de plantar el dispositivo en "lugar". Lo puede llamar cualquier computadora.</summary>
    /// actor: el jugador que lo plantó (por defecto, el de esta computadora); suma en sus estadísticas (US 034).
    public static void Plantar(Vector3 lugar, int actor = 0)
    {
        RondasTacticas r = Actual;
        if (r == null || r.FaseActual != Fase.Combate || r.HayDispositivo) return;
        if (actor == 0) actor = PhotonNetwork.LocalPlayer.ActorNumber;
        // Solo se aplica si sigue siendo la misma ronda en combate y nadie lo plantó antes.
        Sala.SetCustomProperties(
            new Hashtable
            {
                { PropDispositivo, 1 }, { PropLugar, lugar }, { PropFin, Ahora + Ms(DuracionDispositivo) }, { PropPlantador, actor }
            },
            new Hashtable { { PropRonda, r.Ronda }, { PropFase, (int)Fase.Combate }, { PropDispositivo, 0 } });
    }

    /// <summary>US 132: un defensor terminó de desactivar el dispositivo. Lo puede llamar cualquier computadora.</summary>
    /// actor: el jugador que lo desactivó (por defecto, el de esta computadora); suma en sus estadísticas (US 034).
    public static void Desactivar(int actor = 0)
    {
        RondasTacticas r = Actual;
        if (r == null || r.FaseActual != Fase.Combate || !r.HayDispositivo) return;
        if (actor == 0) actor = PhotonNetwork.LocalPlayer.ActorNumber;
        Sala.SetCustomProperties(new Hashtable { { PropDesactivado, 1 }, { PropDesactivador, actor } },
            new Hashtable { { PropRonda, r.Ronda }, { PropFase, (int)Fase.Combate }, { PropDispositivo, 1 } });
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
        // US 135, CA4: el que pierde suma una derrota seguida; el que gana vuelve a 0.
        int racha0 = ganador == 0 ? 0 : Leer(PropRacha0, 0) + 1;
        int racha1 = ganador == 1 ? 0 : Leer(PropRacha1, 0) + 1;
        Publicar(new Hashtable
        {
            { PropFase, (int)(termina ? Fase.Terminada : Fase.FinDeRonda) }, { PropFin, Ahora + Ms(DuracionCartel) },
            { PropGanador, ganador }, { PropMotivo, (int)motivo }, { PropRondas0, e0 }, { PropRondas1, e1 },
            { PropRacha0, racha0 }, { PropRacha1, racha1 }
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
            { PropRonda, ronda }, { PropFase, (int)Fase.Compra }, { PropFin, Ahora + Ms(DuracionCompra) }, { PropGanador, -1 },
            { PropDispositivo, 0 }, { PropDesactivado, 0 }
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
                if (MotivoActual == Motivo.DispositivoExploto) Explotar();
                CartelFinDeRonda(ronda, CobrarRonda()); // US 135
                RondaTerminada?.Invoke(Ganador, MotivoActual);
                break;

            case Fase.Terminada:
                BloquearArmas(false);
                if (!primeraVez && MotivoActual == Motivo.DispositivoExploto) Explotar();
                ResultadoPartida.Mostrar(this); // US 034
                if (!primeraVez) RondaTerminada?.Invoke(Ganador, MotivoActual);
                PartidaTerminada?.Invoke(Ganador);
                break;
        }
    }

    // =====================================================================
    // Dispositivo (US 033; plantar y desactivar son de las US 131 y 132)
    // =====================================================================

    // Avisa los cambios del dispositivo y le pasa al marcador el tiempo que le queda.
    private void VerDispositivo(Fase fase)
    {
        bool desactivado = Leer(PropDesactivado, 0) == 1;
        int estado = !HayDispositivo ? 0 : desactivado ? 2 : 1;
        if (estado != dispositivoVisto)
        {
            if (estado == 1 && !primeraVez)
            {
                DispositivoPlantado?.Invoke(LugarDelDispositivo);
                if (Leer(PropPlantador, 0) == PhotonNetwork.LocalPlayer.ActorNumber) Sumar(PropPlantadas); // US 034
            }
            if (estado == 2)
            {
                DispositivoDesactivado?.Invoke();
                if (!primeraVez && Leer(PropDesactivador, 0) == PhotonNetwork.LocalPlayer.ActorNumber) Sumar(PropDesactivaciones);
            }
            dispositivoVisto = estado;
        }

        if (estado == 1 && fase == Fase.Combate) MarcadorTactico.SetDispositivo(Restante); // US 134, CA3
        else MarcadorTactico.ClearDispositivo();
    }

    // CA1: la explosión elimina al jugador local si está cerca del dispositivo.
    private void Explotar()
    {
        Vector3 lugar = LugarDelDispositivo;
        DispositivoExploto?.Invoke(lugar);
        JugadorEnRed local = partida != null ? partida.Local : null;
        if (local != null && local.Vivo && Vector3.Distance(local.transform.position, lugar) <= RadioExplosion)
            local.Eliminar();
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

    private void CartelFinDeRonda(int ronda, List<string> plata)
    {
        int mio = Mathf.Max(0, EquiposTacticos.Local);
        bool gane = Ganador == mio;
        MatchHud.ShowBanner($"Ronda {ronda}", gane ? "Ronda ganada" : "Ronda perdida",
            gane ? MatchHud.TeamColor : MatchHud.RivalColor, $"{TextoMotivo(gane)}  ·  {Resultado(mio)}",
            plata, Mathf.Max(0.5f, Restante), "Siguiente ronda en {0}");
    }

    // =====================================================================
    // Economía (US 135)
    // =====================================================================

    /// <summary>
    /// CA2: lo llama JugadorEnRed en cada baja (en todas las computadoras). Cobra solo el que mató, si el muerto
    /// es un rival y la ronda está en combate.
    /// </summary>
    public static void ContarBaja(int atacante, int muerto)
    {
        RondasTacticas r = Actual;
        if (r == null || PhotonNetwork.LocalPlayer == null || atacante != PhotonNetwork.LocalPlayer.ActorNumber) return;
        if (atacante == muerto || r.FaseActual != Fase.Combate || EquiposTacticos.SonAliados(atacante, muerto)) return;
        r.Cobrar(PremioBaja);
    }

    // CA3 a CA6: lo que cobra el jugador local al terminar la ronda. Devuelve el detalle para el cartel.
    private List<string> CobrarRonda()
    {
        var detalle = new List<string>();
        int mio = Mathf.Max(0, EquiposTacticos.Local);
        bool gane = Ganador == mio;
        bool ataca = EquiposTacticos.LadoLocal == LadoTactico.Atacante;
        bool vivo = partida != null && partida.Local != null && partida.Local.Vivo;

        if (gane) Premio(detalle, PremioGanada, "Ronda ganada");                                    // CA3
        else if (MotivoActual == Motivo.Tiempo && ataca && vivo) detalle.Add("Sin premio: no plantaron"); // CA6
        else
        {
            // CA4: 1.900, 2.400 y desde la tercera derrota seguida 2.900.
            int racha = Mathf.Clamp(Leer(mio == 0 ? PropRacha0 : PropRacha1, 1), 1, PremiosDerrota.Length);
            Premio(detalle, PremiosDerrota[racha - 1], racha == 1 ? "Ronda perdida" : $"{racha}.ª derrota seguida");
        }
        if (HayDispositivo && ataca) Premio(detalle, PremioPlantar, "Dispositivo plantado");       // CA5
        return detalle;
    }

    private void Premio(List<string> detalle, int monto, string motivo)
    {
        Cobrar(monto);
        detalle.Add($"+{ShopUIKit.Money(monto)}  {motivo}");
    }

    // CA7: la billetera no pasa de $ 9.000 (lo que sobra se pierde).
    private void Cobrar(int monto)
    {
        PlayerWallet billetera = partida != null && partida.Local != null ? partida.Local.GetComponent<PlayerWallet>() : null;
        if (billetera != null) billetera.Add(monto);
    }

    // US 034: cada uno publica sus propias plantadas y desactivaciones, así todos las ven en el resultado.
    private static void Sumar(string clave)
    {
        Player yo = PhotonNetwork.LocalPlayer;
        if (yo == null) return;
        int actual = yo.CustomProperties.TryGetValue(clave, out object v) && v is int n ? n : 0;
        yo.SetCustomProperties(new Hashtable { { clave, actual + 1 } });
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

        JugadorEnRed local = partida != null ? partida.Local : null;
        if (Input.GetKeyDown(KeyCode.F4)) Cobrar(PremioBaja);                                     // como si matara a un rival
        else if (Input.GetKeyDown(KeyCode.F5) && local != null) local.Eliminar();               // morir
        else if (Input.GetKeyDown(KeyCode.F6) && local != null) Plantar(local.transform.position); // plantar acá
        else if (Input.GetKeyDown(KeyCode.F7)) Desactivar();
        else if (Input.GetKeyDown(KeyCode.F9)) ForzarGanador(mio);
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
