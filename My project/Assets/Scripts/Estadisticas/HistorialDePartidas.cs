using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Historial de partidas (F34, US 374): las últimas 20 partidas terminadas del jugador, de la más nueva a la más vieja.
// - Cuenta la partida que llega a su pantalla de resultado, como las estadísticas (US 222, CA3): la anotan
//   RegistroDePartida (Táctico y Deathmatch) y ResultadoDeInvasion, con el mismo id que usan las estadísticas, así una
//   partida a la que se vuelve después de un corte no aparece dos veces. Una abandonada no llega y no aparece.
// - Se guarda con el progreso de la cuenta (Guardado) y la sube LogrosOnline a la nube (rw_add_matches, perfiles.sql),
//   que devuelve las 20 de la cuenta: así el historial es el mismo en cualquier computadora (CA4). Las que todavía no
//   subieron quedan marcadas y salen en la próxima sincronización.
// - El perfil de otro jugador (US 373) muestra el suyo, que manda la base (rw_public_profile).
public static class HistorialDePartidas
{
    public const string Clave = "Historial.Partidas";
    public const int Maximo = 20;

    public enum Modo { Tactico, Deathmatch, Invasion }

    /// <summary>Una partida terminada. Los nombres cortos son los del JSON que sube a la nube.</summary>
    [Serializable]
    public sealed class Partida
    {
        public string id;          // el de las estadísticas: la sala y la hora en que empezó (o el de la Invasión)
        public string modo;        // "tactico", "deathmatch" o "invasion"
        public string mapa;        // el nombre del mapa, como en la sala ("Mapa 1"); se traduce al mostrarlo
        public long fecha;         // cuándo terminó, en segundos desde 1970 (UTC)
        public int segundos;       // cuánto duró
        public bool gano;          // Táctico: ganó su equipo; Deathmatch: primer puesto; Invasión: salvó al mundo
        public int puesto;         // Deathmatch: el puesto (1, 2, 3…); 0 en los demás modos
        public int bajas, muertes, asistencias;
        public string dificultad;  // Invasión: "facil", "normal" o "dificil"
        public int oleada;         // Invasión: hasta qué oleada llegó
        public bool coop;          // Invasión: jugó de a varios (US 289)
        public bool subida;        // ya está en la nube (no se manda a la base)

        public Modo ModoDe => modo == "deathmatch" ? Modo.Deathmatch : modo == "invasion" ? Modo.Invasion : Modo.Tactico;
        public float Kda => EstadisticasJugador.Kda(bajas, asistencias, muertes);
        public DateTime Fecha => DateTimeOffset.FromUnixTimeSeconds(Math.Max(0, fecha)).LocalDateTime;
    }

    [Serializable]
    public sealed class Lista
    {
        public List<Partida> partidas = new List<Partida>();
    }

    public static event Action Cambio;
    private static Lista datos;

    /// <summary>Las partidas guardadas, de la más nueva a la más vieja (hasta 20).</summary>
    public static IReadOnlyList<Partida> Partidas => Datos.partidas;

    private static Lista Datos => datos ??= Leer(Guardado.GetString(Clave, ""));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar()
    {
        datos = null;
        Cambio = null;
    }

    // Antes de la primera escena: dos juegos abiertos en la PC juntan sus historiales, y al entrar o salir de una cuenta
    // se lee el de la otra.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Preparar()
    {
        Guardado.RegistrarUnion(Clave, Juntar);
        Guardado.Juntado += clave => { if (clave == Clave) Recargar(); };
        Guardado.CambioDeJugador += Recargar;
    }

    public static void Recargar()
    {
        datos = null;
        Cambio?.Invoke();
    }

    /// <summary>Anota una partida terminada (CA3). No hace nada si esa partida ya estaba.</summary>
    public static void Registrar(Partida p)
    {
        if (p == null || string.IsNullOrEmpty(p.id)) return;
        p.id = IdValido(p.id);
        if (p.fecha <= 0) p.fecha = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (string.IsNullOrEmpty(p.mapa)) p.mapa = MapaActual();
        if (!Agregar(Datos, p)) return;
        Guardar();
        Guardado.Save();
    }

    /// <summary>Las que todavía no están en la nube, para que LogrosOnline las suba.</summary>
    public static List<Partida> PorSubir()
    {
        var lista = new List<Partida>();
        foreach (Partida p in Datos.partidas) if (!p.subida) lista.Add(p);
        return lista;
    }

    /// <summary>Hay partidas que todavía no están en la nube.</summary>
    public static bool HayPorSubir
    {
        get
        {
            foreach (Partida p in Datos.partidas) if (!p.subida) return true;
            return false;
        }
    }

    /// <summary>Las partidas que ya llegaron a la nube (rw_add_matches respondió bien).</summary>
    public static void MarcarSubidas(IEnumerable<Partida> subidas)
    {
        bool cambio = false;
        foreach (Partida s in subidas)
        {
            Partida mia = s == null ? null : Datos.partidas.Find(x => x.id == s.id);
            if (mia != null && !mia.subida) { mia.subida = true; cambio = true; }
        }
        if (cambio) Guardar();
    }

    /// <summary>Junta lo que devolvió la nube (las 20 de la cuenta) con lo de esta PC: lo de la nube queda como subido.</summary>
    public static void UnirNube(IEnumerable<Partida> nube)
    {
        if (nube == null) return;
        bool cambio = false;
        foreach (Partida p in nube)
        {
            if (p == null || string.IsNullOrEmpty(p.id)) continue;
            Partida mia = Datos.partidas.Find(x => x.id == p.id);
            if (mia != null) { if (!mia.subida) { mia.subida = true; cambio = true; } continue; }
            p.subida = true;
            cambio |= Agregar(Datos, p);
        }
        if (cambio) Guardar();
    }

    private static void Guardar()
    {
        Guardado.SetString(Clave, JsonUtility.ToJson(Datos));
        Cambio?.Invoke();
    }

    // El nombre del mapa de esta escena, como figura en la sala (ConfigRed); si no está, el de la escena.
    private static string MapaActual()
    {
        string escena = SceneManager.GetActiveScene().name;
        ConfigRed config = ConfigRed.Actual;
        if (config != null && config.mapas != null)
            foreach (ConfigRed.Mapa m in config.mapas)
                if (m != null && m.escena == escena && !string.IsNullOrEmpty(m.nombre)) return m.nombre;
        return escena;
    }

    // ---------- Puros (para las pruebas) ----------

    /// <summary>El id con los caracteres que acepta la base (rw_add_matches): letras, números y _ . : # -; hasta 96.</summary>
    public static string IdValido(string id)
    {
        var sb = new System.Text.StringBuilder(id.Length);
        foreach (char c in id)
            sb.Append(c < 128 && (char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == ':' || c == '#' || c == '-') ? c : '_');
        return sb.Length > 96 ? sb.ToString(0, 96) : sb.ToString();
    }

    /// <summary>Agrega la partida en su lugar por fecha (la más nueva primero) y deja las 20 más nuevas.
    /// Devuelve false si no vale o si ya estaba.</summary>
    public static bool Agregar(Lista l, Partida p)
    {
        if (l == null || p == null || string.IsNullOrEmpty(p.id) || l.partidas.Exists(x => x.id == p.id)) return false;
        int i = l.partidas.FindIndex(x => x.fecha < p.fecha);
        if (i < 0) l.partidas.Add(p); else l.partidas.Insert(i, p);
        if (l.partidas.Count > Maximo) l.partidas.RemoveRange(Maximo, l.partidas.Count - Maximo);
        return l.partidas.Contains(p); // una más vieja que las 20 guardadas no entra
    }

    public static Lista Leer(string json)
    {
        Lista l = null;
        if (!string.IsNullOrEmpty(json)) { try { l = JsonUtility.FromJson<Lista>(json); } catch { l = null; } }
        l ??= new Lista();
        l.partidas ??= new List<Partida>();
        l.partidas.RemoveAll(p => p == null || string.IsNullOrEmpty(p.id));
        l.partidas.Sort((a, b) => b.fecha.CompareTo(a.fecha));
        if (l.partidas.Count > Maximo) l.partidas.RemoveRange(Maximo, l.partidas.Count - Maximo);
        return l;
    }

    /// <summary>Para Guardado.RegistrarUnion: junta el historial de este juego con el del archivo (otro juego abierto).</summary>
    public static string Juntar(string mio, string otro)
    {
        Lista a = Leer(mio), b = Leer(otro);
        foreach (Partida p in b.partidas)
        {
            Partida igual = a.partidas.Find(x => x.id == p.id);
            if (igual != null) igual.subida |= p.subida;
            else Agregar(a, p);
        }
        return JsonUtility.ToJson(a);
    }
}
