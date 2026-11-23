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
//   para no dañar a los compañeros (CA6), y EsRival dice a quién se le dibuja el contorno rojo (CA5).
// Fuera del Táctico (o sin sala) no hay equipos y todo funciona como antes: HayEquipos es false.
public static class EquiposTacticos
{
    public const int MaxPorEquipo = 4;
    public const int RondaDeCambio = 7;

    // SOLO PARA PROBAR: con true, todos los jugadores quedan en el mismo equipo. Sirve para probar que los
    // aliados no tienen contorno y el "sin fuego amigo" con dos instancias del juego. Dejalo en false.
    public static bool PruebaTodosAliados = false;

    private const string PropAtacante = "eqAtq"; // equipo (0 o 1) que ataca en las rondas 1 a 6
    private static readonly Dictionary<int, string> claves = new Dictionary<int, string>();

    // Hoy la ronda sale de la fase de compra. Cuando se haga la US 032 (rondas), si el número de ronda
    // pasa a vivir en otro lado, alcanza con cambiar esta línea.
    public static int Ronda =>
        RondasTacticas.Actual != null ? RondasTacticas.Actual.Ronda : // US 032: la ronda que lleva el anfitrión
        BuyPhase.Current != null ? Mathf.Max(1, BuyPhase.Current.Round) : 1;

    // ---------- Reparto (solo lo hace el anfitrión, al iniciar la partida) ----------

    public static void Repartir()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;

        var jugadores = new List<Player>(PhotonNetwork.PlayerList);
        // CA1: se mezclan al azar (así, si hay empate, cualquiera de los repartos parejos puede salir).
        for (int i = jugadores.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (jugadores[i], jugadores[j]) = (jugadores[j], jugadores[i]);
        }

        bool publica = Multijugador.Existe && Multijugador.Instancia.EnPartidaPublica;
        bool[] enEquipo0;
        if (PruebaTodosAliados) enEquipo0 = jugadores.ConvertAll(_ => true).ToArray();
        else if (!publica && EquiposElegidosListos) enEquipo0 = jugadores.ConvertAll(p => ElegidoPor(p) == 1).ToArray(); // US 377, CA6
        else if (publica) enEquipo0 = Parejos(jugadores.ConvertAll(NivelDeJugador.De)); // US 280, CA1
        else
        {
            // CA2: los primeros van al equipo 0 y el resto al equipo 1. Con cantidad impar, el equipo 0 se queda
            // con uno más (la diferencia es de 1 como mucho) y ninguno pasa de 4.
            int cuantos = Mathf.Min(MaxPorEquipo, (jugadores.Count + 1) / 2);
            enEquipo0 = new bool[jugadores.Count];
            for (int i = 0; i < jugadores.Count; i++) enEquipo0[i] = i < cuantos;
        }

        var propiedades = new Hashtable();
        string equipo0 = "", equipo1 = "";
        for (int i = 0; i < jugadores.Count; i++)
        {
            int equipo = enEquipo0[i] ? 0 : 1;
            propiedades[Clave(jugadores[i].ActorNumber)] = equipo;
            if (equipo == 0) equipo0 += " " + jugadores[i].ActorNumber; else equipo1 += " " + jugadores[i].ActorNumber;
        }

        // CA3 (y US 280, CA4; US 377, CA6): un equipo al azar ataca en las rondas 1 a 6 y el otro defiende.
        int atacante = Random.Range(0, 2);
        propiedades[PropAtacante] = atacante;

        PhotonNetwork.CurrentRoom.SetCustomProperties(propiedades);
        Debug.Log($"EquiposTacticos: equipo 0 (jugadores:{equipo0}), equipo 1 (jugadores:{equipo1}). Ataca el equipo {atacante}.");
    }

    // ---------- Equipos parejos (US 280) ----------

    /// <summary>
    /// CA1 y CA2: de todos los repartos con la misma cantidad de jugadores (o uno de diferencia, ninguno de más de 4), el
    /// que deja la suma de niveles más pareja. Con 8 jugadores son 70 repartos: se prueban todos. Si hay varios iguales,
    /// queda el primero (la lista ya viene mezclada al azar). Devuelve, para cada jugador, si va al equipo 0.
    /// </summary>
    public static bool[] Parejos(List<int> niveles)
    {
        int n = niveles.Count;
        var mejor = new bool[n];
        if (n == 0) return mejor;
        int total = 0;
        foreach (int x in niveles) total += x;
        int tamano = Mathf.Min(MaxPorEquipo, (n + 1) / 2);
        long mejorDiferencia = long.MaxValue;
        for (int mascara = 0; mascara < (1 << n); mascara++)
        {
            int cuenta = 0, suma = 0;
            for (int i = 0; i < n; i++) if ((mascara & (1 << i)) != 0) { cuenta++; suma += niveles[i]; }
            if (cuenta != tamano || n - cuenta > MaxPorEquipo) continue;
            long diferencia = System.Math.Abs((long)suma - (total - suma));
            if (diferencia >= mejorDiferencia) continue;
            mejorDiferencia = diferencia;
            for (int i = 0; i < n; i++) mejor[i] = (mascara & (1 << i)) != 0;
        }
        return mejor;
    }

    // ---------- Elegir equipo en las salas con código (US 377) ----------

    /// <summary>El equipo que eligió cada jugador en la sala: 0 sin equipo (en el medio), 1 o 2.</summary>
    public const string PropElegido = "eqElegido";

    public static int ElegidoPor(Player p) => p != null && p.CustomProperties.TryGetValue(PropElegido, out object v) && v is int n ? n : 0;

    public static int CuantosEligieron(int equipo)
    {
        int n = 0;
        if (PhotonNetwork.InRoom) foreach (Player p in PhotonNetwork.PlayerList) if (ElegidoPor(p) == equipo) n++;
        return n;
    }

    /// <summary>CA3 y CA4: el jugador local se mueve a ese lugar (0 el medio, 1 o 2), si hay lugar.</summary>
    public static bool Elegir(int equipo)
    {
        if (!PhotonNetwork.InRoom || equipo < 0 || equipo > 2) return false;
        if (equipo != 0 && CuantosEligieron(equipo) >= MaxPorEquipo && ElegidoPor(PhotonNetwork.LocalPlayer) != equipo) return false;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { PropElegido, equipo } });
        return true;
    }

    /// <summary>CA5: se puede iniciar si nadie quedó en el medio y cada equipo tiene al menos uno (y ninguno más de 4).</summary>
    public static bool EquiposElegidosListos
    {
        get
        {
            int uno = CuantosEligieron(1), dos = CuantosEligieron(2);
            return CuantosEligieron(0) == 0 && uno >= 1 && dos >= 1 && uno <= MaxPorEquipo && dos <= MaxPorEquipo;
        }
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

    // US 195, CA6: cuántos jugadores le tocaron a ese equipo en el reparto (aunque después se hayan ido de la sala).
    public static int Asignados(int equipo)
    {
        if (!PhotonNetwork.InRoom) return 0;
        int n = 0;
        foreach (System.Collections.DictionaryEntry propiedad in PhotonNetwork.CurrentRoom.CustomProperties)
            if (propiedad.Key is string clave && clave.Length > 2 && clave.StartsWith("eq") && int.TryParse(clave.Substring(2), out _) &&
                propiedad.Value is int valor && valor == equipo)
                n++;
        return n;
    }

    // CA3: lado de un equipo en la ronda actual. Las rondas 1 a 6 ataca el equipo sorteado; desde la 7 se intercambian.
    public static LadoTactico LadoDeEquipo(int equipo)
    {
        if (equipo < 0 || !HayEquipos) return LadoTactico.Cualquiera;
        // US 032, CA6: en la muerte súbita los lados se sortean de nuevo.
        if (RondasTacticas.AtacanteMuerteSubita(out int atacanteSubita))
            return equipo == atacanteSubita ? LadoTactico.Atacante : LadoTactico.Defensor;
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
        // F31 (US 289, CA10): en Invasión cooperativa todos son compañeros: sin fuego amigo (balas, cuchillo, granadas).
        if (PhotonNetwork.InRoom && MatchSettings.Mode == GameMode.Zombie) return true;
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

    // CA5: si el jugador de esta computadora tiene que ver a ese jugador como rival (con el contorno rojo, ContornoRival).
    // Sin equipos (Deathmatch, o el Táctico antes del reparto) todos son rivales; en Zombie se juega juntos.
    public static bool EsRival(int actor)
    {
        if (MatchSettings.Mode == GameMode.Zombie) return false;
        if (!HayEquipos || DeActor(actor) < 0) return true;
        return !SonAliados(PhotonNetwork.LocalPlayer.ActorNumber, actor);
    }

    private static string Clave(int actor)
    {
        if (!claves.TryGetValue(actor, out string clave)) claves[actor] = clave = "eq" + actor;
        return clave;
    }
}
