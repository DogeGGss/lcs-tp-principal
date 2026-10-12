using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using static ShopUIKit;

// Marcador de la partida táctica (US 134), sobre la base de la US 057 (MatchHud), con la guía de diseño:
// "TU EQUIPO · ATACANTES" a la izquierda y el rival a la derecha, rondas ganadas con rayitas (primero a 7),
// el reloj en el centro y los jugadores de cada equipo con su retrato y una cruz cuando mueren.
// Lo que ya funciona con lo que existe hoy:
// - CA4: los jugadores de cada equipo (US 031); el que muere se oscurece con una cruz.
// - CA6: tu equipo siempre a la izquierda, aunque cambien de lado (cambia el título Atacantes / Defensores).
// - CA2 (en parte): el tiempo de la fase de compra.
// - Tabla con Tab: tu equipo y los rivales, con bajas, muertes y la plata de tus compañeros (US 135).
// - US 195, CA1: el desconectado queda en gris (distinto del muerto) y en la tabla dice "(desconectado)". Las bajas y
//   las muertes salen de lo que publica cada jugador en la sala (RondasTacticas), así no se pierden al volver.
// Queda preparado para que lo completen otras US, llamando a:
// - CA1: SetRondas(equipo0, equipo1) al terminar cada ronda (US 032, US 033).
// - CA2: TiempoDeRonda, el tiempo que queda de la ronda en combate (US 032).
// - CA3: SetDispositivo(segundos) al plantar y ClearDispositivo() al terminar (US 131, US 132).
// - CA5: SetPortador(actor) para marcar al que lleva el dispositivo (US 130).
// Lo agrega PartidaEnRed al empezar una partida del Modo Táctico.
public class MarcadorTactico : MonoBehaviour
{
    public const int RondasParaGanar = 7;
    /// <summary>La plata de cada jugador, publicada en la sala (también la recupera el que vuelve a la partida, US 195).</summary>
    public const string PropPlata = "plata";

    public static MarcadorTactico Instance { get; private set; }

    /// <summary>US 032: el tiempo que le queda a la ronda en combate (en segundos), o null si no hay.</summary>
    public static System.Func<float?> TiempoDeRonda;

    private static readonly int[] rondas = new int[2];
    private static float? dispositivo;
    private static int portador;

    private readonly List<MatchHud.RosterEntry> aliados = new List<MatchHud.RosterEntry>();
    private readonly List<MatchHud.RosterEntry> rivales = new List<MatchHud.RosterEntry>();
    private PartidaEnRed partida;
    private PlayerWallet billetera;
    private int plataPublicada = int.MinValue;

    // ---------- Para las US que faltan ----------

    /// <summary>CA1: rondas ganadas por el equipo 0 y el equipo 1 (US 032, US 033).</summary>
    public static void SetRondas(int equipo0, int equipo1) { rondas[0] = equipo0; rondas[1] = equipo1; }

    /// <summary>CA3: se plantó el dispositivo; segundos que faltan para que explote (US 131).</summary>
    public static void SetDispositivo(float segundos) => dispositivo = segundos;

    /// <summary>CA3: el dispositivo se desactivó, explotó o terminó la ronda (US 132).</summary>
    public static void ClearDispositivo() => dispositivo = null;

    /// <summary>CA5: actor de Photon del que lleva el dispositivo; 0 si nadie (US 130).</summary>
    public static void SetPortador(int actor) => portador = actor;

    /// <summary>El actor que lleva el dispositivo (0 si nadie).</summary>
    public static int Portador => portador;

    // ---------- Ciclo de vida ----------

    public void Iniciar(PartidaEnRed partida)
    {
        this.partida = partida;
        Instance = this;
        rondas[0] = rondas[1] = 0;
        dispositivo = null;
        portador = 0;
        MatchHud.TableProvider = Tabla;
        PlayerMovement local = FindAnyObjectByType<PlayerMovement>();
        billetera = local != null ? local.GetComponent<PlayerWallet>() : null;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        TiempoDeRonda = null;
        MatchHud.TableProvider = null;
        MatchHud.ClearCenter();
    }

    private void Update()
    {
        PublicarPlata();
        if (MatchHud.Instance == null || !EquiposTacticos.HayEquipos) return;

        int mio = Mathf.Max(0, EquiposTacticos.Local);
        int rival = mio == 0 ? 1 : 0;
        int ronda = EquiposTacticos.Ronda;

        // CA1 y CA6: tus rondas a la izquierda con "TU EQUIPO · Atacantes/Defensores", las del rival a la derecha.
        MatchHud.SetSide(true, rondas[mio].ToString(), $"<color=#F3F4F6>Tu equipo</color>  ·  {Lado(mio)}", MatchHud.TeamColor, RondasParaGanar, rondas[mio]);
        MatchHud.SetSide(false, rondas[rival].ToString(), Lado(rival), MatchHud.RivalColor, RondasParaGanar, rondas[rival]);

        // CA2 y CA3: el centro.
        string detalle = $"Ronda {ronda} · a {RondasParaGanar}";
        BuyPhase compra = BuyPhase.Current;
        float? tiempo = TiempoDeRonda != null ? TiempoDeRonda() : null;
        if (dispositivo.HasValue)
            MatchHud.SetCenterDevice("Dispositivo plantado", dispositivo.Value, detalle); // CA3
        else if (RondasTacticas.Actual != null && RondasTacticas.Actual.FaseActual == RondasTacticas.Fase.Seleccion)
            // US 016: antes de la ronda 1, el tiempo para elegir personaje.
            MatchHud.SetCenter("Selección", RondasTacticas.Actual.Restante, "Elegí tu personaje");
        else if (RondasTacticas.Actual != null && RondasTacticas.Actual.EsMuerteSubita &&
                 RondasTacticas.Actual.FaseActual != RondasTacticas.Fase.Terminada)
            // US 032, CA6: en la muerte súbita el centro queda en rojo.
            MatchHud.SetCenter("Muerte súbita", compra != null && compra.IsActive ? compra.TimeLeft : tiempo,
                $"Ronda {ronda} · {rondas[mio]} a {rondas[rival]}", true, "—", true);
        else if (compra != null && compra.IsActive)
            MatchHud.SetCenter("Fase de compra", compra.TimeLeft, detalle, compra.TimeLeft <= 5f);
        else if (tiempo.HasValue)
            MatchHud.SetCenter("Ronda en curso", tiempo.Value, detalle, tiempo.Value <= 10f);
        else
            MatchHud.SetCenter("Ronda en curso", null, detalle, false, "—");

        // CA4 y CA5: los jugadores de cada equipo.
        aliados.Clear();
        rivales.Clear();
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            int equipo = EquiposTacticos.DeActor(p.ActorNumber);
            if (equipo < 0) continue;
            bool esAliado = equipo == mio;
            Color color = esAliado ? MatchHud.TeamColor : MatchHud.RivalColor;
            // US 016: el retrato (o la inicial y el color) del personaje que eligió; si todavía no eligió, su inicial.
            CharacterData pj = PersonajesTacticos.De(p.ActorNumber);
            string nombre = pj != null ? pj.displayName : p.NickName;
            (esAliado ? aliados : rivales).Add(new MatchHud.RosterEntry
            {
                name = p.NickName,
                initial = string.IsNullOrEmpty(nombre) ? "?" : nombre.Substring(0, 1).ToUpperInvariant(),
                portrait = pj != null ? pj.portrait : null,
                color = pj != null ? pj.color : color,
                team = color,
                alive = EstaVivo(p.ActorNumber),
                offline = p.IsInactive, // US 195, CA1
                marked = esAliado && portador != 0 && p.ActorNumber == portador // el portador solo lo ven sus compañeros
            });
        }
        MatchHud.SetRoster(true, aliados);
        MatchHud.SetRoster(false, rivales);
    }

    private static string Lado(int equipo) =>
        EquiposTacticos.LadoDeEquipo(equipo) == LadoTactico.Atacante ? "Atacantes" : "Defensores";

    private bool EstaVivo(int actor)
    {
        JugadorEnRed j = partida != null ? partida.Buscar(actor) : null;
        return j == null || j.Vivo; // si todavía no cargó el mapa, se lo muestra vivo
    }

    // La plata de cada uno viaja como propiedad del jugador, así la ven sus compañeros en la tabla.
    private void PublicarPlata()
    {
        if (billetera == null || !PhotonNetwork.InRoom || billetera.Money == plataPublicada) return;
        plataPublicada = billetera.Money;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { PropPlata, plataPublicada } });
    }

    // ---------- Tabla con Tab ----------

    private MatchHud.Table Tabla()
    {
        int mio = Mathf.Max(0, EquiposTacticos.Local);
        int rival = mio == 0 ? 1 : 0;
        var tabla = new MatchHud.Table
        {
            corner = $"Ronda {EquiposTacticos.Ronda}  ·  {rondas[mio]} a {rondas[rival]}",
            columns = new[] { "", "Jugador", "Bajas", "Muertes", "Plata" },
            widths = new[] { 30f, 452f, 110f, 110f, 110f }
        };
        tabla.sections.Add(Seccion("Tu equipo", MatchHud.TeamColor, mio, true));
        tabla.sections.Add(Seccion("Rivales", MatchHud.RivalColor, rival, false));
        return tabla;
    }

    private MatchHud.Section Seccion(string titulo, Color color, int equipo, bool propio)
    {
        var jugadores = new List<Player>();
        foreach (Player p in PhotonNetwork.PlayerList)
            if (EquiposTacticos.DeActor(p.ActorNumber) == equipo) jugadores.Add(p);
        jugadores.Sort((a, b) => RondasTacticas.BajasDe(b).CompareTo(RondasTacticas.BajasDe(a)));

        var seccion = new MatchHud.Section { title = titulo, color = color };
        for (int i = 0; i < jugadores.Count; i++)
        {
            Player p = jugadores[i];
            string plata = "—";
            if (propio && p.CustomProperties.TryGetValue(PropPlata, out object valor) && valor is int monto) plata = Money(monto);
            seccion.rows.Add(new MatchHud.Row
            {
                cells = new[]
                {
                    (i + 1).ToString(),
                    p.IsLocal ? $"{p.NickName} <color=#8E96A3>(vos)</color>"
                        : p.IsInactive ? $"{p.NickName} <color=#8E96A3>(desconectado)</color>" : p.NickName,
                    RondasTacticas.BajasDe(p).ToString(),
                    RondasTacticas.MuertesDe(p).ToString(),
                    plata
                },
                highlight = p.IsLocal,
                dim = p.IsInactive || !EstaVivo(p.ActorNumber)
            });
        }
        return seccion;
    }
}
