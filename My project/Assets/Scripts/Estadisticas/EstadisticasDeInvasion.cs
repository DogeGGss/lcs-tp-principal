using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// US 308 (F24): lo propio de la Invasión nueva, además de lo que registra la US 222 (EstadisticasJugador).
// - CA1: Jefes derrotados y el más difícil (la oleada de ese Jefe: 10, 20, 30…). En cooperativo, el Jefe cuenta para
//   todos los que estaban en la partida (sale de OleadasDeInvasion, que es igual para todos).
// - CA2: puestos cargados a los que el jugador les aportó puntos.
// - CA3: veces que se eligió cada carta (la favorita: la más elegida; con empate, la primera del catálogo) e invasores
//   eliminados con cada arma (la favorita: la que más eliminó).
// - CA4: partidas en cooperativo (de 2 a 4), aparte; la mejor oleada en cooperativo por dificultad ya la guarda la
//   US 289 (MatchSettings.ZombieRecordCoop). También suman a las partidas de la US 222.
// - CA6: se suma una vez por partida (con el id de la partida, como la US 222), se guarda con el progreso y va a la nube
//   como contadores (estadisticas.invasion.*), que se juntan con el máximo sin tocar la base.
public static class EstadisticasDeInvasion
{
    public const string Clave = "Estadisticas.Invasion";
    private const int Recordadas = 30;

    [Serializable]
    private class Datos
    {
        public int jefes, jefeMasDificil, puestos, partidasCoop;
        public List<string> cartas = new List<string>(); // "id=veces"
        public List<string> armas = new List<string>();  // "id=bajas"
        public List<string> sumadas = new List<string>(); // ids de las últimas partidas ya sumadas
    }

    private static Datos d;
    private static Datos D
    {
        get
        {
            if (d != null) return d;
            string json = Guardado.GetString(Clave, "");
            if (!string.IsNullOrEmpty(json)) { try { d = JsonUtility.FromJson<Datos>(json); } catch { d = null; } }
            return d ??= new Datos();
        }
    }

    private static void Guardar()
    {
        Guardado.SetString(Clave, JsonUtility.ToJson(D));
        Guardado.Save();
    }

    public static void Recargar() => d = null;

    // ================= Durante la partida =================

    private static readonly Dictionary<string, int> cartas = new Dictionary<string, int>(), armas = new Dictionary<string, int>();
    private static readonly HashSet<PuestoDeSuministros> aportados = new HashSet<PuestoDeSuministros>();
    private static int puestos;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReiniciarSesion() { d = null; Limpiar(); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Iniciar()
    {
        Npc.Murio -= AlMorirNpc;
        Npc.Murio += AlMorirNpc;
        PuestoDeSuministros.SeCargo -= AlCargarPuesto;
        PuestoDeSuministros.SeCargo += AlCargarPuesto;
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;
    }

    private static void AlCargarEscena(Scene escena, LoadSceneMode modo) { if (modo == LoadSceneMode.Single) Limpiar(); }

    private static void Limpiar()
    {
        cartas.Clear(); armas.Clear(); aportados.Clear(); puestos = 0;
    }

    /// <summary>CA3: el jugador eligió una carta en el puesto (EleccionDeInvasion).</summary>
    public static void CartaElegida(CartaDeInvasion carta)
    {
        if (carta == null) return;
        string id = Id(carta.nombre);
        cartas.TryGetValue(id, out int n);
        cartas[id] = n + 1;
    }

    /// <summary>CA2: el jugador le aportó puntos a un puesto (PuestoDeSuministros, dentro del anillo).</summary>
    public static void AporteAlPuesto(PuestoDeSuministros puesto)
    {
        if (puesto != null) aportados.Add(puesto);
    }

    private static void AlCargarPuesto(PuestoDeSuministros puesto)
    {
        if (puesto != null && aportados.Remove(puesto)) puestos++;
    }

    // CA3: la baja es del jugador local (en cooperativo, no las de los compañeros); el arma es la que tiene en la mano.
    private static void AlMorirNpc(Npc npc, int puntos, bool cabeza)
    {
        if (npc == null || npc.LoMatoOtro || MatchSettings.Mode != GameMode.Zombie) return;
        string arma = CombatHud.ArmaActual;
        if (string.IsNullOrEmpty(arma)) return;
        string id = IdDeArma(arma);
        armas.TryGetValue(id, out int n);
        armas[id] = n + 1;
    }

    // ================= Al terminar (ResultadoDeInvasion) =================

    /// <summary>CA1, CA4 y CA6: suma lo de la partida (una sola vez por id).</summary>
    public static void Registrar(string idPartida, int jefesDerrotados, bool cooperativo)
    {
        Datos s = D;
        if (!string.IsNullOrEmpty(idPartida))
        {
            if (s.sumadas.Contains(idPartida)) { Limpiar(); return; }
            s.sumadas.Add(idPartida);
            if (s.sumadas.Count > Recordadas) s.sumadas.RemoveAt(0);
        }
        s.jefes += Mathf.Max(0, jefesDerrotados);
        s.jefeMasDificil = Mathf.Max(s.jefeMasDificil, jefesDerrotados * OleadasDeInvasion.Ultima);
        s.puestos += puestos;
        if (cooperativo) s.partidasCoop++;
        foreach (var c in cartas) Poner(s.cartas, c.Key, Leer(s.cartas, c.Key) + c.Value);
        foreach (var a in armas) Poner(s.armas, a.Key, Leer(s.armas, a.Key) + a.Value);
        Limpiar();
        Guardar();
    }

    // ================= Para el Perfil (CA5) =================

    public static int Jefes => D.jefes;
    public static int JefeMasDificil => D.jefeMasDificil;
    public static int Puestos => D.puestos;
    public static int PartidasCoop => D.partidasCoop;

    /// <summary>La carta más elegida (con empate, la primera del catálogo), o null.</summary>
    public static string CartaFavorita
    {
        get
        {
            string mejor = null; int max = 0;
            foreach (CartaDeInvasion c in CartaDeInvasion.Todas)
            {
                int n = Leer(D.cartas, Id(c.nombre));
                if (n > max) { max = n; mejor = c.nombre; }
            }
            return mejor;
        }
    }

    /// <summary>El arma que eliminó más invasores, o null.</summary>
    public static string ArmaFavorita
    {
        get
        {
            string mejor = null; int max = 0;
            foreach (string s in D.armas)
            {
                int igual = s.IndexOf('=');
                if (igual <= 0 || !int.TryParse(s.Substring(igual + 1), out int n) || n <= max) continue;
                max = n; mejor = s.Substring(0, igual);
            }
            if (mejor == null) return null;
            CatalogoDeSkins.Arma a = CatalogoDeSkins.ArmaDe(mejor);
            return a != null ? a.nombre : mejor == "cuchillo" ? "Cuchillo" : mejor;
        }
    }

    // ================= Nube (ProgresoNube) =================

    private const string Prefijo = "estadisticas.invasion.";

    public static IEnumerable<(string clave, long valor)> Contadores
    {
        get
        {
            Datos s = D;
            yield return (Prefijo + "jefes", s.jefes);
            yield return (Prefijo + "jefe_mas_dificil", s.jefeMasDificil);
            yield return (Prefijo + "puestos", s.puestos);
            yield return (Prefijo + "coop.partidas", s.partidasCoop);
            foreach (string c in s.cartas) { var (k, v) = Par(c); if (k != null) yield return (Prefijo + "carta." + k, v); }
            foreach (string a in s.armas) { var (k, v) = Par(a); if (k != null) yield return (Prefijo + "arma." + k, v); }
        }
    }

    /// <summary>Junta con lo de la nube: el máximo de cada contador.</summary>
    public static void FusionarNube(IEnumerable<KeyValuePair<string, long>> contadores)
    {
        Datos s = D;
        bool cambio = false;
        void Max(ref int campo, long v) { if (v > campo) { campo = (int)v; cambio = true; } }
        foreach (var kv in contadores)
        {
            if (!kv.Key.StartsWith(Prefijo, StringComparison.Ordinal)) continue;
            string k = kv.Key.Substring(Prefijo.Length);
            if (k == "jefes") Max(ref s.jefes, kv.Value);
            else if (k == "jefe_mas_dificil") Max(ref s.jefeMasDificil, kv.Value);
            else if (k == "puestos") Max(ref s.puestos, kv.Value);
            else if (k == "coop.partidas") Max(ref s.partidasCoop, kv.Value);
            else if (k.StartsWith("carta.")) { string id = k.Substring(6); if (kv.Value > Leer(s.cartas, id)) { Poner(s.cartas, id, (int)kv.Value); cambio = true; } }
            else if (k.StartsWith("arma.")) { string id = k.Substring(5); if (kv.Value > Leer(s.armas, id)) { Poner(s.armas, id, (int)kv.Value); cambio = true; } }
        }
        if (cambio) Guardar();
    }

    // ================= Ayudas =================

    /// <summary>Un id para las claves de la nube (minúsculas, sin tildes, guiones): "Disparo concentrado" → "disparo-concentrado".</summary>
    public static string Id(string nombre) => PaseDeBatalla.IdDePersonaje(nombre);

    private static string IdDeArma(string nombre)
    {
        string n = (nombre ?? "").Trim().ToLowerInvariant();
        foreach (CatalogoDeSkins.Arma a in CatalogoDeSkins.Armas)
            if (a.nombre.ToLowerInvariant() == n || (!string.IsNullOrEmpty(a.ficha) && a.ficha.ToLowerInvariant() == n)) return a.id;
        if (n == "pistola") return "la-portena";
        return Id(nombre);
    }

    private static (string, int) Par(string s)
    {
        int igual = s.IndexOf('=');
        return igual > 0 && int.TryParse(s.Substring(igual + 1), out int n) ? (s.Substring(0, igual), n) : (null, 0);
    }

    private static int Leer(List<string> lista, string id)
    {
        string prefijo = id + "=";
        foreach (string s in lista)
            if (s.StartsWith(prefijo, StringComparison.Ordinal) && int.TryParse(s.Substring(prefijo.Length), out int n)) return n;
        return 0;
    }

    private static void Poner(List<string> lista, string id, int valor)
    {
        string prefijo = id + "=";
        lista.RemoveAll(s => s.StartsWith(prefijo, StringComparison.Ordinal));
        lista.Add(prefijo + valor);
    }
}
