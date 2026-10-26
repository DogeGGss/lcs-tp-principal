using System;
using System.Collections.Generic;
using UnityEngine;

// Estadísticas del jugador (F24): lo que hizo en las partidas de Táctico y Deathmatch, acumulado por modo (US 222), y
// los indicadores que muestra el Perfil (US 221), como el KDA (US 223). Reglas acordadas el 17/10/2026:
// - Una partida cuenta cuando aparece su pantalla de resultado (RegistroDePartida); si el jugador abandona, no suma.
//   Cada partida se identifica por el código de la sala y la hora en que empezó, y se suma una sola vez aunque el
//   jugador se corte y vuelva con "Volver a la partida" (US 222, CA3).
// - Asistencia: le hizo daño a un rival que otro jugador eliminó en los 10 s siguientes (JugadorEnRed).
// - KDA = (bajas + asistencias) ÷ muertes; sin muertes, ÷ 1. Se calcula con los totales y solo se redondea al mostrarlo.
// - MVP (Táctico): terminó con más bajas que todos (con las mismas, menos muertes). Mejor racha (Deathmatch): más
//   bajas seguidas sin morir en una partida.
// - El Zombie no cuenta hasta que sus partidas tengan final (US 151); el minijuego tiene las suyas (US 159).
// Se guardan con el progreso del jugador (Guardado, US 222, CA5) y en la nube (ProgresoNube), juntando con el máximo.
public static class EstadisticasJugador
{
    public const string Clave = "Estadisticas.Jugador";
    public const float VentanaAsistencia = 10f;
    private const int Recordadas = 30; // partidas ya sumadas que se recuerdan para no sumarlas dos veces

    [Serializable]
    public sealed class Modo
    {
        public int partidas, victorias, bajas, muertes, asistencias, cabezas;
        public long segundos;
    }

    [Serializable]
    public sealed class Totales
    {
        public int version = 1;
        public Modo tactico = new Modo(), deathmatch = new Modo();
        public int rondasGanadas, plantados, desactivados, mvp; // Táctico
        public int mejorRacha;                                  // Deathmatch
        public List<string> contadas = new List<string>();      // las últimas partidas sumadas (CA3)

        public int Partidas => tactico.partidas + deathmatch.partidas;
        public int Victorias => tactico.victorias + deathmatch.victorias;
        public int Bajas => tactico.bajas + deathmatch.bajas;
        public int Muertes => tactico.muertes + deathmatch.muertes;
        public int Asistencias => tactico.asistencias + deathmatch.asistencias;
        public int Cabezas => tactico.cabezas + deathmatch.cabezas;
        public long Segundos => tactico.segundos + deathmatch.segundos;
        public bool SinDatos => Partidas == 0;
        public float Kda => EstadisticasJugador.Kda(Bajas, Asistencias, Muertes);
    }

    /// <summary>Lo que aportó una partida terminada.</summary>
    public sealed class Partida
    {
        public string id;
        public bool deathmatch, gano, mvp;
        public int bajas, muertes, asistencias, cabezas;
        public int rondasGanadas, plantados, desactivados; // Táctico
        public int mejorRacha;                             // Deathmatch
        public long segundos;
    }

    public static event Action Cambio;
    private static Totales datos;

    public static Totales Datos => datos ??= Leer(Guardado.GetString(Clave, ""));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar()
    {
        datos = null;
        Cambio = null;
    }

    /// <summary>US 223: (bajas + asistencias) ÷ muertes; sin muertes se divide por 1, nunca da infinito.</summary>
    public static float Kda(int bajas, int asistencias, int muertes) => (bajas + asistencias) / (float)Math.Max(1, muertes);

    /// <summary>US 222: suma una partida terminada y la guarda. No hace nada si esa partida ya estaba sumada.</summary>
    public static void Registrar(Partida p)
    {
        if (!Sumar(Datos, p)) return;
        Guardar();
        Guardado.Save(); // una vez por partida: se escribe ya
    }

    /// <summary>Junta lo que vino de la nube: el máximo de cada total.</summary>
    public static void UnirNube(Totales nube)
    {
        if (Unir(Datos, nube)) Guardar();
    }

    /// <summary>Vuelve a leer lo guardado (lo cambió otro juego abierto en esta PC).</summary>
    public static void Recargar()
    {
        datos = null;
        Cambio?.Invoke();
    }

    private static void Guardar()
    {
        Guardado.SetString(Clave, JsonUtility.ToJson(Datos));
        Cambio?.Invoke();
    }

    // ---------- Suma, formato y unión (puros, para las pruebas) ----------

    /// <summary>Suma la partida a los totales. Devuelve false si no vale o si ya estaba sumada (CA3).</summary>
    public static bool Sumar(Totales t, Partida p)
    {
        if (t == null || p == null || string.IsNullOrEmpty(p.id) || t.contadas.Contains(p.id)) return false;
        Modo m = p.deathmatch ? t.deathmatch : t.tactico;
        m.partidas++;
        if (p.gano) m.victorias++;
        m.bajas += Math.Max(0, p.bajas);
        m.muertes += Math.Max(0, p.muertes);
        m.asistencias += Math.Max(0, p.asistencias);
        m.cabezas += Math.Max(0, p.cabezas);
        m.segundos += Math.Max(0, p.segundos);
        if (p.deathmatch) t.mejorRacha = Math.Max(t.mejorRacha, p.mejorRacha);
        else
        {
            t.rondasGanadas += Math.Max(0, p.rondasGanadas);
            t.plantados += Math.Max(0, p.plantados);
            t.desactivados += Math.Max(0, p.desactivados);
            if (p.mvp) t.mvp++;
        }
        t.contadas.Add(p.id);
        if (t.contadas.Count > Recordadas) t.contadas.RemoveRange(0, t.contadas.Count - Recordadas);
        return true;
    }

    public static Totales Leer(string json)
    {
        Totales t = null;
        if (!string.IsNullOrEmpty(json)) { try { t = JsonUtility.FromJson<Totales>(json); } catch { t = null; } }
        if (t == null) return new Totales();
        t.tactico ??= new Modo();
        t.deathmatch ??= new Modo();
        t.contadas ??= new List<string>();
        return t;
    }

    /// <summary>Lo de dos juegos o de la PC y la nube: el máximo de cada total y las partidas sumadas de los dos.</summary>
    public static bool Unir(Totales a, Totales b)
    {
        if (a == null || b == null) return false;
        bool cambio = false;
        void Max(ref int x, int y) { if (y > x) { x = y; cambio = true; } }
        void MaxL(ref long x, long y) { if (y > x) { x = y; cambio = true; } }
        void UnirModo(Modo x, Modo y)
        {
            if (y == null) return;
            Max(ref x.partidas, y.partidas);
            Max(ref x.victorias, y.victorias);
            Max(ref x.bajas, y.bajas);
            Max(ref x.muertes, y.muertes);
            Max(ref x.asistencias, y.asistencias);
            Max(ref x.cabezas, y.cabezas);
            MaxL(ref x.segundos, y.segundos);
        }
        UnirModo(a.tactico, b.tactico);
        UnirModo(a.deathmatch, b.deathmatch);
        Max(ref a.rondasGanadas, b.rondasGanadas);
        Max(ref a.plantados, b.plantados);
        Max(ref a.desactivados, b.desactivados);
        Max(ref a.mvp, b.mvp);
        Max(ref a.mejorRacha, b.mejorRacha);
        if (b.contadas != null)
            foreach (string id in b.contadas)
                if (!string.IsNullOrEmpty(id) && !a.contadas.Contains(id)) { a.contadas.Add(id); cambio = true; }
        if (a.contadas.Count > Recordadas) a.contadas.RemoveRange(0, a.contadas.Count - Recordadas);
        return cambio;
    }

    /// <summary>Para Guardado.RegistrarUnion: junta lo de este juego con lo del archivo.</summary>
    public static string Juntar(string mio, string otro)
    {
        Totales a = Leer(mio);
        return Unir(a, Leer(otro)) ? JsonUtility.ToJson(a) : mio;
    }
}
