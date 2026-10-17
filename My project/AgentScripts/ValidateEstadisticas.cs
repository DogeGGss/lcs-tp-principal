using System;
using UnityEngine;

// Pruebas de las estadísticas del jugador (F24: US 222 y US 223) y de las categorías del ranking (US 226) sin tocar el
// progreso real: trabajan sobre totales sueltos, no sobre EstadisticasJugador.Datos. Se corre con: unity command run_script
// --file AgentScripts/ValidateEstadisticas.cs --entry ValidateEstadisticas.Main
public static class ValidateEstadisticas
{
    static int checks;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }

    static EstadisticasJugador.Partida Tactico(string id, bool gano, int bajas, int muertes, int asistencias) =>
        new EstadisticasJugador.Partida
        {
            id = id, gano = gano, bajas = bajas, muertes = muertes, asistencias = asistencias, cabezas = 1,
            rondasGanadas = gano ? 7 : 3, plantados = 1, desactivados = 0, mvp = gano, segundos = 900,
        };

    public static string Main()
    {
        // US 223, CA3: cero partidas, cero muertes: nunca NaN ni infinito.
        var t = new EstadisticasJugador.Totales();
        Check(t.SinDatos && t.Kda == 0f, "empty totals");
        Check(EstadisticasJugador.Kda(5, 3, 0) == 8f, "no deaths divides by 1");
        Check(Mathf.Approximately(EstadisticasJugador.Kda(10, 2, 4), 3f), "kda formula");
        Check(!float.IsNaN(EstadisticasJugador.Kda(0, 0, 0)), "kda never NaN");

        // US 222, CA1: por modo, y el General es la suma.
        Check(EstadisticasJugador.Sumar(t, Tactico("ABCDE:100", true, 12, 4, 3)), "first tactico match");
        Check(EstadisticasJugador.Sumar(t, new EstadisticasJugador.Partida { id = "FGHIJ:200", deathmatch = true, bajas = 20, muertes = 10, asistencias = 5, cabezas = 6, mejorRacha = 4, segundos = 480 }), "first deathmatch match");
        Check(t.tactico.partidas == 1 && t.tactico.victorias == 1 && t.mvp == 1 && t.rondasGanadas == 7, "tactico totals");
        Check(t.deathmatch.partidas == 1 && t.deathmatch.victorias == 0 && t.mejorRacha == 4, "deathmatch totals");
        Check(t.Partidas == 2 && t.Bajas == 32 && t.Muertes == 14 && t.Asistencias == 8 && t.Cabezas == 7 && t.Segundos == 1380, "general is the sum");
        Check(Mathf.Approximately(t.Kda, 40f / 14f), "kda over totals, not averaged per match");

        // US 222, CA3: la misma partida no se suma dos veces (pantalla repetida o volver a la partida).
        Check(!EstadisticasJugador.Sumar(t, Tactico("ABCDE:100", true, 12, 4, 3)), "same match rejected");
        Check(t.tactico.partidas == 1 && t.tactico.bajas == 12, "same match did not add");
        Check(!EstadisticasJugador.Sumar(t, Tactico("", true, 1, 1, 1)) && !EstadisticasJugador.Sumar(t, null), "match without id rejected");

        // Mejor racha: el máximo, no la suma.
        EstadisticasJugador.Sumar(t, new EstadisticasJugador.Partida { id = "FGHIJ:300", deathmatch = true, gano = true, bajas = 25, mejorRacha = 3 });
        Check(t.mejorRacha == 4 && t.deathmatch.victorias == 1, "best streak keeps maximum");

        // Solo se recuerdan las últimas 30 partidas; las nuevas se siguen sumando.
        for (int i = 0; i < 40; i++) EstadisticasJugador.Sumar(t, Tactico("SALA" + i + ":1", false, 0, 1, 0));
        Check(t.contadas.Count == 30 && t.tactico.partidas == 41, "remembered matches are capped");

        // Dos juegos o la PC y la nube: el máximo de cada total y las partidas sumadas de los dos.
        var a = new EstadisticasJugador.Totales();
        var b = new EstadisticasJugador.Totales();
        EstadisticasJugador.Sumar(a, Tactico("A:1", true, 10, 2, 1));
        EstadisticasJugador.Sumar(b, Tactico("B:1", false, 3, 9, 0));
        EstadisticasJugador.Sumar(b, new EstadisticasJugador.Partida { id = "B:2", deathmatch = true, bajas = 7, mejorRacha = 5 });
        Check(EstadisticasJugador.Unir(a, b), "merge changes");
        Check(a.tactico.bajas == 10 && a.tactico.muertes == 9 && a.deathmatch.bajas == 7 && a.mejorRacha == 5, "merge keeps maximum");
        Check(a.contadas.Contains("A:1") && a.contadas.Contains("B:1") && a.contadas.Contains("B:2"), "merge joins counted matches");
        Check(!EstadisticasJugador.Unir(a, b), "merge idempotent");

        // Guardado: ida y vuelta por JSON; datos rotos o viejos arrancan en cero sin romper (US 222, CA6).
        var back = EstadisticasJugador.Leer(JsonUtility.ToJson(a));
        Check(back.tactico.bajas == 10 && back.mejorRacha == 5 && back.contadas.Count == 3, "json roundtrip");
        Check(EstadisticasJugador.Leer("basura").SinDatos && EstadisticasJugador.Leer("").SinDatos, "bad data starts empty");
        var viejo = EstadisticasJugador.Leer("{\"version\":1}");
        Check(viejo.tactico != null && viejo.deathmatch != null && viejo.contadas != null, "missing fields are created");
        string mio = JsonUtility.ToJson(a);
        Check(EstadisticasJugador.Juntar(mio, mio) == mio, "join without changes keeps the text");

        // US 226: cada categoría del juego existe en la base (rw_stat_board) y se muestra con su formato.
        string sql = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../../Backend/Supabase/logros.sql"));
        foreach (CategoriasRanking.Categoria c in CategoriasRanking.Todas)
        {
            Check(Array.IndexOf(CategoriasRanking.Grupos, c.grupo) >= 0, "unknown group " + c.grupo);
            if (c.id != CategoriasRanking.Logros) Check(sql.Contains("'" + c.id + "'"), "category missing in logros.sql: " + c.id);
        }
        foreach (string g in CategoriasRanking.Grupos) Check(CategoriasRanking.Primera(g) != null, "empty group " + g);
        Check(CategoriasRanking.Mostrar(CategoriasRanking.Buscar("bajas"), 1204) == "1.204", "integer format");
        Check(CategoriasRanking.Mostrar(CategoriasRanking.Buscar("kda"), 1.789) == "1,79", "decimal format");
        Check(CategoriasRanking.Mostrar(CategoriasRanking.Buscar("precision"), 55.55) == "55,6 %", "percent format");
        Check(CategoriasRanking.Mostrar(CategoriasRanking.Buscar("mejor_reaccion"), 650) == "0,65 s", "reaction format");

        return checks + " checks passed. Real player data untouched.";
    }
}
