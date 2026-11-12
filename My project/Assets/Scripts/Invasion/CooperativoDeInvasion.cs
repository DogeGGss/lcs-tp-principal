using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

// F31 (Invasión cooperativa), US 289: lo que cambia cuando Invasión se juega de a varios, según cuántos jugadores hay.
// - CA4: cada jugador de más suma un 50 % de invasores a cada oleada y 5 al tope de invasores a la vez (US 299), y el
//   Jefe tiene un 50 % más de vida por cada uno (con 4 jugadores, 2,5 veces).
// - El personaje (solo el aspecto, US 297) se elige al entrar a la partida (PersonajesInvasion); viaja como propiedad del
//   jugador en la sala ("inv.pj"), así los demás lo ven con su cuerpo (JugadorEnRed.PersonajeDe).
// Jugando solo (sin sala) todo queda como siempre. Cuenta a los que están en la partida: el que se desconectó (y todavía
// puede volver, US 195) no suma.
public static class CooperativoDeInvasion
{
    public const float InvasoresPorJugador = 0.5f, VidaDelJefePorJugador = 0.5f;
    public const int TopePorJugador = 5;

    /// <summary>Se está jugando Invasión en una sala (cooperativo), aunque sea uno solo.</summary>
    public static bool Activo => PhotonNetwork.InRoom && MatchSettings.Mode == GameMode.Zombie;

    /// <summary>Cuántos jugadores hay en la partida (1 jugando solo).</summary>
    public static int Jugadores
    {
        get
        {
            if (!Activo) return 1;
            int n = 0;
            foreach (Player p in PhotonNetwork.PlayerList)
                if (!p.IsInactive) n++;
            return n < 1 ? 1 : n;
        }
    }

    private static int DeMas => Jugadores - 1;

    /// <summary>CA4: multiplica la cantidad de invasores de cada oleada.</summary>
    public static float MasInvasores => 1f + InvasoresPorJugador * DeMas;

    /// <summary>CA4: se suma al tope de invasores vivos a la vez.</summary>
    public static int MasTope => TopePorJugador * DeMas;

    /// <summary>CA4: multiplica la vida del Jefe.</summary>
    public static float VidaDelJefe => 1f + VidaDelJefePorJugador * DeMas;

    private const string PropPersonaje = "inv.pj";

    /// <summary>Publica el personaje elegido (lo llama PersonajesInvasion al empezar y al elegir).</summary>
    public static void AnunciarPersonaje()
    {
        CharacterData p = CharacterRoster.Selected;
        if (p == null || !PhotonNetwork.InRoom) return;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { PropPersonaje, p.name } });
    }

    /// <summary>El personaje (aspecto) de ese jugador, o null si todavía no se sabe.</summary>
    public static CharacterData PersonajeDe(int actor)
    {
        Room sala = PhotonNetwork.CurrentRoom;
        Player jugador = sala != null ? sala.GetPlayer(actor) : null;
        if (jugador == null || !jugador.CustomProperties.TryGetValue(PropPersonaje, out object v) || !(v is string nombre)) return null;
        foreach (CharacterData p in CharacterRoster.All) if (p.name == nombre) return p;
        return null;
    }
}
