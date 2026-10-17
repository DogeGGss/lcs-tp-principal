using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Arma lo que aportó la partida que terminó (F24, US 222) con lo que quedó publicado en la sala y lo suma a las
// estadísticas del jugador. Lo llaman las pantallas de resultado, que aparecen una vez por partida (CA4: una partida
// abandonada no llega a la pantalla y no suma). Las asistencias y la racha las anotan RondasTacticas y PartidaDeathmatch.
public static class RegistroDePartida
{
    private const float DuracionMaxima = 4f * 60f * 60f; // más que esto es una hora de inicio que no sirve

    /// <summary>Táctico (US 034): ganó si su equipo llegó a 7 rondas (o los rivales abandonaron).</summary>
    public static void Tactico(RondasTacticas rondas, bool gano)
    {
        Player yo = PhotonNetwork.LocalPlayer;
        Room sala = PhotonNetwork.CurrentRoom;
        if (rondas == null || yo == null || sala == null) return;

        int bajas = RondasTacticas.Propia(RondasTacticas.PropBajas), muertes = RondasTacticas.Propia(RondasTacticas.PropMuertes);
        // MVP: nadie terminó con más bajas, ni con las mismas y menos muertes (los desconectados también cuentan).
        bool mvp = bajas > 0;
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            if (p.IsLocal) continue;
            int b = RondasTacticas.BajasDe(p), m = RondasTacticas.MuertesDe(p);
            if (b > bajas || (b == bajas && m < muertes)) { mvp = false; break; }
        }

        EstadisticasJugador.Registrar(new EstadisticasJugador.Partida
        {
            id = Id(sala, RondasTacticas.Inicio),
            gano = gano,
            mvp = mvp,
            bajas = bajas,
            muertes = muertes,
            asistencias = RondasTacticas.Propia(RondasTacticas.PropAsistencias),
            cabezas = RondasTacticas.Propia(RondasTacticas.PropCabezas),
            rondasGanadas = rondas.RondasDe(Mathf.Max(0, EquiposTacticos.Local)),
            plantados = RondasTacticas.Propia(RondasTacticas.PropPlantadas),
            desactivados = RondasTacticas.Propia(RondasTacticas.PropDesactivaciones),
            segundos = Segundos(RondasTacticas.Inicio),
        });
    }

    /// <summary>Deathmatch (US 142): ganó si quedó en el primer puesto.</summary>
    public static void Deathmatch(bool gano)
    {
        Player yo = PhotonNetwork.LocalPlayer;
        Room sala = PhotonNetwork.CurrentRoom;
        if (yo == null || sala == null) return;
        int actor = yo.ActorNumber;
        EstadisticasJugador.Registrar(new EstadisticasJugador.Partida
        {
            id = Id(sala, PartidaDeathmatch.Inicio),
            deathmatch = true,
            gano = gano,
            bajas = PartidaDeathmatch.Bajas(actor),
            muertes = PartidaDeathmatch.Muertes(actor),
            asistencias = PartidaDeathmatch.Asistencias(actor),
            cabezas = PartidaDeathmatch.Cabezas(actor),
            mejorRacha = PartidaDeathmatch.MejorRacha(actor),
            segundos = Segundos(PartidaDeathmatch.Inicio),
        });
    }

    // CA3: la sala (su código) y la hora del servidor en que empezó la partida.
    private static string Id(Room sala, int? inicio) => sala.Name + ":" + (inicio.HasValue ? inicio.Value.ToString() : "?");

    // Cuánto duró, con la hora del servidor (igual para todos); si no se sabe, desde que se cargó el mapa.
    private static long Segundos(int? inicio)
    {
        if (inicio.HasValue)
        {
            float s = unchecked(PhotonNetwork.ServerTimestamp - inicio.Value) / 1000f;
            if (s > 0f && s < DuracionMaxima) return Mathf.RoundToInt(s);
        }
        return Mathf.RoundToInt(Time.timeSinceLevelLoad);
    }
}
