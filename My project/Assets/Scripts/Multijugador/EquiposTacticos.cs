using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// Lado que tiene un equipo en la ronda actual. "Cualquiera" se usa para lo que no depende del lado
// (Deathmatch, Zombie, mapas sin equipos, o una zona / punto que sirve para los dos lados).
public enum LadoTactico { Cualquiera, Atacante, Defensor }

// Equipos del modo Táctico (US 031).
// - El anfitrión reparte a los jugadores de la sala en dos equipos (0 y 1) al iniciar la partida (Repartir).
//   El reparto se guarda en las propiedades de la sala de Photon, así todos los jugadores lo leen igual.
// - Un equipo ataca y el otro defiende en las rondas 1 a 6; en la ronda 7 se intercambian (LadoDeEquipo).
// - SonAliados dice si dos jugadores son del mismo equipo: lo usan las armas, el cuchillo y las granadas
//   para no dañar a los compañeros (CA6), y el cartel de los jugadores para pintar aliados y rivales (CA5).
// Fuera del Táctico (o sin sala) no hay equipos y todo funciona como antes: HayEquipos es false.
public static class EquiposTacticos
{
    public const int MaxPorEquipo = 4;
    public const int RondaDeCambio = 7;

    // Colores del nombre sobre la cabeza: aliados en verde y rivales en rojo (CA5).
    public const string HexAliado = "#3DDC97";
    public const string HexRival = "#FF5C5C";

    // SOLO PARA PROBAR: con true, todos los jugadores quedan en el mismo equipo. Sirve para probar el
    // nombre de los aliados y el "sin fuego amigo" con dos instancias del juego. Dejalo en false.
    public static bool PruebaTodosAliados = false;

    private const string PropAtacante = "eqAtq"; // equipo (0 o 1) que ataca en las rondas 1 a 6
    private static readonly Dictionary<int, string> claves = new Dictionary<int, string>();

    // Hoy la ronda sale de la fase de compra. Cuando se haga la US 032 (rondas), si el número de ronda
    // pasa a vivir en otro lado, alcanza con cambiar esta línea.
    public static int Ronda => BuyPhase.Current != null ? Mathf.Max(1, BuyPhase.Current.Round) : 1;

    // ---------- Reparto (solo lo hace el anfitrión, al iniciar la partida) ----------

    public static void Repartir()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;

        var actores = new List<int>();
        foreach (Player jugador in PhotonNetwork.PlayerList) actores.Add(jugador.ActorNumber);

        // CA1: se mezclan al azar.
        for (int i = actores.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int guardado = actores[i];
            actores[i] = actores[j];
            actores[j] = guardado;
        }

        // CA2: los primeros van al equipo 0 y el resto al equipo 1. Con cantidad impar, el equipo 0 se queda
        // con uno más (la diferencia es de 1 como mucho) y ninguno pasa de 4.
        int enEquipo0 = Mathf.Min(MaxPorEquipo, (actores.Count + 1) / 2);
        var propiedades = new Hashtable();
        string equipo0 = "", equipo1 = "";
        for (int i = 0; i < actores.Count; i++)
        {
            int equipo = (PruebaTodosAliados || i < enEquipo0) ? 0 : 1;
            propiedades[Clave(actores[i])] = equipo;
            if (equipo == 0) equipo0 += " " + actores[i]; else equipo1 += " " + actores[i];
        }

        // CA3: un equipo al azar ataca en las rondas 1 a 6 y el otro defiende.
        int atacante = Random.Range(0, 2);
        propiedades[PropAtacante] = atacante;

        PhotonNetwork.CurrentRoom.SetCustomProperties(propiedades);
        Debug.Log($"EquiposTacticos: equipo 0 (jugadores:{equipo0}), equipo 1 (jugadores:{equipo1}). Ataca el equipo {atacante}.");
    }

    // ---------- Consultas ----------

    // true si esta partida es Táctica y ya llegó el reparto de equipos.
    public static bool HayEquipos =>
        PhotonNetwork.InRoom && MatchSettings.Mode == GameMode.Tactico &&
        PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(PropAtacante);

    // Equipo (0 o 1) de un jugador, según su número de actor de Photon. -1 si no tiene.
    public static int DeActor(int actor)
    {
        if (!PhotonNetwork.InRoom) return -1;
        return PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(Clave(actor), out object valor) ? (int)valor : -1;
    }

    public static int Local => PhotonNetwork.InRoom ? DeActor(PhotonNetwork.LocalPlayer.ActorNumber) : -1;

    // CA3: lado de un equipo en la ronda actual. Las rondas 1 a 6 ataca el equipo sorteado; desde la 7 se intercambian.
    public static LadoTactico LadoDeEquipo(int equipo)
    {
        if (equipo < 0 || !HayEquipos) return LadoTactico.Cualquiera;
        int atacante = (int)PhotonNetwork.CurrentRoom.CustomProperties[PropAtacante];
        bool intercambiados = Ronda >= RondaDeCambio;
        bool ataca = (equipo == atacante) != intercambiados;
        return ataca ? LadoTactico.Atacante : LadoTactico.Defensor;
    }

    // Lado del jugador de esta computadora (Cualquiera si no hay equipos).
    public static LadoTactico LadoLocal => LadoDeEquipo(Local);

    // ---------- Aliados (CA5 y CA6) ----------

    // Dos jugadores distintos del mismo equipo. Un jugador no es "aliado de sí mismo": así una granada
    // propia sigue haciéndole daño a quien la tiró.
    public static bool SonAliados(int actorA, int actorB)
    {
        if (actorA == actorB) return false;
        int a = DeActor(actorA), b = DeActor(actorB);
        return a >= 0 && a == b;
    }

    // Igual, pero con cualquier componente de cada jugador (su Transform, su HealthSystem, un arma...).
    public static bool SonAliados(Component tirador, Component objetivo)
    {
        if (tirador == null || objetivo == null) return false;
        JugadorEnRed a = tirador.GetComponentInParent<JugadorEnRed>();
        JugadorEnRed b = objetivo.GetComponentInParent<JugadorEnRed>();
        if (a == null || b == null || a == b) return false;
        return SonAliados(a.Actor, b.Actor);
    }

    // CA5: color (en hexadecimal) con que se pinta el nombre de un jugador. null si no hay equipos.
    public static string ColorHexDe(int actor)
    {
        if (!HayEquipos || DeActor(actor) < 0) return null;
        return SonAliados(PhotonNetwork.LocalPlayer.ActorNumber, actor) ? HexAliado : HexRival;
    }

    private static string Clave(int actor)
    {
        if (!claves.TryGetValue(actor, out string clave)) claves[actor] = clave = "eq" + actor;
        return clave;
    }
}
