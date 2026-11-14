using System;
using System.Collections.Generic;
using UnityEngine;
using Cosmetico = PerfilDatos.Cosmetico;
using Rareza = PerfilDatos.Rareza;
using Tipo = PerfilDatos.Tipo;

// F32: el pase de batalla. Datos y reglas; la pantalla es PantallaPase y el resumen al terminar la partida, ResumenDelPase.
// - US 362: experiencia por partida (100, y 100 más si ganó; en Invasión 10 por oleada superada) y por desafío; un nivel
//   cada 1.000, hasta el 40 (después se sigue sumando experiencia, pero no hay más niveles).
// - US 363: 40 recompensas en 4 páginas de 10. Las de los niveles 1 y 6 de cada página son gratis; las otras 8, de pago.
// - US 364: el pase cuesta 950 cristales y se compra una vez por temporada. Las recompensas de pago de los niveles 5, 15,
//   25, 35 y 40 traen además 100 cristales cada una (500 en total).
// - US 365: los desafíos de la temporada (Desafios), que avanzan al terminar cada partida (DesafiosDePartida).
// - US 366: temporadas de 8 semanas desde el lunes 9/11/2026 (hora de Argentina). Al terminar una, lo desbloqueado y no
//   reclamado se reclama solo y la siguiente empieza de cero, con otras recompensas.
// Todo se guarda con el progreso del jugador (Guardado, en la cuenta) y en la nube (ProgresoNube): la experiencia, el
// avance de los desafíos y la compra solo suben dentro de una temporada, así se juntan con el máximo; lo reclamado, con
// la unión.
public static class PaseDeBatalla
{
    public const string Clave = "Pase.Estado";
    public const int Niveles = 40, PorPagina = 10, Paginas = 4, XpPorNivel = 1000, Precio = 950;
    public const int XpPartida = 100, XpVictoria = 100, XpPorOleada = 10;
    public const int DiasPorTemporada = 56;
    private static readonly DateTime Inicio = new DateTime(2026, 11, 9, 3, 0, 0, DateTimeKind.Utc); // 00:00 en Argentina (UTC-3)
    // US 364, CA3 (14/11/2026): no todo es un cosmético. Algunos niveles dan cristales o créditos: 100 cristales en el pase
    // gratis y 550 en total con el pase comprado (450 en los niveles de pago), repartidos a medida que se sube.
    private static readonly Dictionary<int, int> Cristales = new Dictionary<int, int>
    {
        { 16, 50 }, { 31, 50 },                                   // gratis: 100
        { 5, 100 }, { 10, 50 }, { 15, 100 }, { 25, 100 }, { 35, 100 }, // pase: 450
    };
    private static readonly Dictionary<int, int> Creditos = new Dictionary<int, int>
    {
        { 6, 500 }, { 26, 1000 },                                 // gratis
        { 3, 500 }, { 8, 500 }, { 18, 750 }, { 23, 750 }, { 33, 1000 }, { 38, 1000 }, // pase
    };
    public const int CristalesGratis = 100, CristalesConPase = 550;

    public static event Action Cambio;

    [Serializable]
    private class Estado
    {
        public int temporada;
        public int xp;
        public bool comprado;
        public List<string> reclamados = new List<string>(); // "12g" (gratis) o "12p" (pago)
        public List<string> desafios = new List<string>();   // "id=avance"
        public List<string> etapas = new List<string>();     // "id=etapas cobradas"
        public List<string> aviso = new List<string>();      // US 366, CA3: lo que se reclamó solo al terminar la temporada
    }

    private static Estado e;

    private static Estado E
    {
        get
        {
            if (e == null)
            {
                string json = Guardado.GetString(Clave, "");
                if (!string.IsNullOrEmpty(json)) { try { e = JsonUtility.FromJson<Estado>(json); } catch { e = null; } }
                if (e == null) e = new Estado { temporada = TemporadaActual };
                if (e.temporada == 0) e.temporada = TemporadaActual;
                MigrarIds(e.desafios);
                MigrarIds(e.etapas);
            }
            if (e.temporada != TemporadaActual) CerrarTemporada();
            return e;
        }
    }

    private static void Guardar()
    {
        Guardado.SetString(Clave, JsonUtility.ToJson(e));
        Guardado.Save();
        Cambio?.Invoke();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReiniciarSesion() => e = null;

    /// <summary>Vuelve a leer lo guardado (otra cuenta, o lo cambió otro juego abierto en esta PC).</summary>
    public static void Recargar() { e = null; Cambio?.Invoke(); }

    // ================= Temporadas (US 366) =================

    private static DateTime Ahora => DateTime.UtcNow.AddDays(DiasDePrueba);

    public static int TemporadaActual => Mathf.Max(1, (int)Math.Floor((Ahora - Inicio).TotalDays / DiasPorTemporada) + 1);
    public static DateTime FinDeTemporada => Inicio.AddDays(TemporadaActual * DiasPorTemporada);
    public static int DiasQueFaltan => Mathf.Max(1, (int)Math.Ceiling((FinDeTemporada - Ahora).TotalDays));
    public static bool UltimaSemana => DiasQueFaltan <= 7;
    public static string NombreTemporada => L10n.Source("Temporada", "profile.") + " " + TemporadaActual;

    // US 366, CA3 y CA4: lo desbloqueado y no reclamado de la temporada que terminó se reclama solo, y la nueva empieza de cero.
    private static void CerrarTemporada()
    {
        var aviso = new List<string>();
        int vieja = e.temporada;
        int nivel = NivelDe(e.xp);
        for (int n = 1; n <= nivel; n++)
        {
            bool pago = !EsGratis(n);
            if ((pago && !e.comprado) || e.reclamados.Contains(n + (pago ? "p" : "g"))) continue;
            foreach (Premio p in PremiosDe(vieja, n, pago))
            {
                p.Dar("pase");
                aviso.Add(p.Nombre);
            }
        }
        e = new Estado { temporada = TemporadaActual, aviso = aviso };
        Guardar();
    }

    /// <summary>US 366, CA3: lo que se reclamó solo al terminar la temporada anterior (una vez: al leerlo se borra).</summary>
    public static List<string> TomarAviso()
    {
        var lista = new List<string>(E.aviso);
        if (lista.Count > 0) { e.aviso.Clear(); Guardar(); }
        return lista;
    }

    // ================= Experiencia y niveles (US 362) =================

    public static int Xp => E.xp;
    public static int Nivel => NivelDe(E.xp);
    public static int NivelDe(int xp) => Mathf.Clamp(xp / XpPorNivel, 0, Niveles);
    /// <summary>La experiencia dentro del nivel actual (en el 40, 1.000 / 1.000).</summary>
    public static int XpDelNivel => Nivel >= Niveles ? XpPorNivel : E.xp % XpPorNivel;
    public static bool Comprado => E.comprado;

    /// <summary>Lo que pasó con el pase al terminar una partida: para el resumen de la pantalla de resultado.</summary>
    public sealed class Resumen
    {
        public int xpPartida, xpDesafios, nivelAntes, nivelDespues, xpAntes;
        public readonly List<string> desafios = new List<string>(); // "Ganar 1 partida (+300)"
        public readonly List<Premio> desbloqueados = new List<Premio>();
        public int XpTotal => xpPartida + xpDesafios;
    }

    /// <summary>US 362, CA1: suma la experiencia de la partida y la de los desafíos que avanzaron (US 365, CA3 y CA4).</summary>
    public static Resumen Sumar(int xpPartida, Dictionary<string, int> avances, Dictionary<string, int> maximos)
    {
        Estado s = E;
        var r = new Resumen { xpPartida = Mathf.Max(0, xpPartida), xpAntes = s.xp, nivelAntes = NivelDe(s.xp) };
        foreach (Desafio d in Desafios)
        {
            int antes = Avance(d.id);
            int ahora = antes;
            if (avances != null && avances.TryGetValue(d.id, out int mas)) ahora = antes + mas;
            if (maximos != null && maximos.TryGetValue(d.id, out int max)) ahora = Mathf.Max(ahora, max);
            if (ahora == antes) continue;
            Poner(s.desafios, d.id, ahora);
            int cobradas = Leer(s.etapas, d.id);
            int completas = d.EtapasCompletas(ahora);
            for (int i = cobradas; i < completas; i++)
            {
                r.xpDesafios += d.xp[i];
                r.desafios.Add($"{d.Nombre(i)} (+{PerfilDatos.Numero(d.xp[i])})");
            }
            if (completas > cobradas) Poner(s.etapas, d.id, completas);
        }
        s.xp += r.XpTotal;
        r.nivelDespues = NivelDe(s.xp);
        for (int n = r.nivelAntes + 1; n <= r.nivelDespues; n++)
        {
            if (EsGratis(n)) r.desbloqueados.AddRange(PremiosDe(s.temporada, n, false));
            else if (s.comprado) r.desbloqueados.AddRange(PremiosDe(s.temporada, n, true));
        }
        Guardar();
        return r;
    }

    // ================= Recompensas (US 363 y US 364) =================

    /// <summary>Las 2 recompensas gratis de cada página son las de los niveles 1 y 6 de la página.</summary>
    public static bool EsGratis(int nivel) => (nivel - 1) % PorPagina == 0 || (nivel - 1) % PorPagina == 5;

    public enum EstadoRecompensa { Bloqueada, SinPase, Lista, Reclamada }

    public static EstadoRecompensa EstadoDe(int nivel)
    {
        bool pago = !EsGratis(nivel);
        if (E.reclamados.Contains(nivel + (pago ? "p" : "g"))) return EstadoRecompensa.Reclamada;
        if (nivel > Nivel) return pago && !E.comprado ? EstadoRecompensa.SinPase : EstadoRecompensa.Bloqueada;
        if (pago && !E.comprado) return EstadoRecompensa.SinPase;
        return EstadoRecompensa.Lista;
    }

    /// <summary>US 363, CA5: cuántas recompensas hay para reclamar (para los avisos del menú).</summary>
    public static int ParaReclamar
    {
        get
        {
            int n = 0;
            for (int i = 1; i <= Niveles; i++) if (EstadoDe(i) == EstadoRecompensa.Lista) n++;
            return n;
        }
    }

    /// <summary>El cosmético del nivel y, en los niveles con cristales del pase de pago, también los cristales.</summary>
    public static List<Premio> Premios(int nivel) => PremiosDe(E.temporada, nivel, !EsGratis(nivel));

    private static List<Premio> PremiosDe(int temporada, int nivel, bool pago)
    {
        var lista = new List<Premio>();
        Premio c = CosmeticoDe(temporada, nivel);
        if (c != null) lista.Add(c);
        return lista;
    }

    /// <summary>US 363, CA5: reclama una recompensa lista. Devuelve los nombres de lo que se sumó (null si no estaba lista).</summary>
    public static List<string> Reclamar(int nivel)
    {
        if (EstadoDe(nivel) != EstadoRecompensa.Lista) return null;
        var nombres = new List<string>();
        foreach (Premio p in Premios(nivel))
        {
            int creditos = p.Dar("pase");
            nombres.Add(creditos > 0 ? $"{p.Nombre} ({L10n.Source("repetido", "profile.")}: +{PerfilDatos.Numero(creditos)} {L10n.Source("créditos", "profile.")})" : p.Nombre);
        }
        e.reclamados.Add(nivel + (EsGratis(nivel) ? "g" : "p"));
        Guardar();
        return nombres;
    }

    public static List<string> ReclamarTodo()
    {
        var nombres = new List<string>();
        for (int n = 1; n <= Niveles; n++)
        {
            List<string> r = Reclamar(n);
            if (r != null) nombres.AddRange(r);
        }
        return nombres;
    }

    /// <summary>US 364: compra el pase (una vez por temporada). listo recibe null si se pudo, o el motivo.</summary>
    public static void Comprar(Action<string> listo)
    {
        if (E.comprado) { listo?.Invoke(L10n.Source("Ya compraste el pase de esta temporada", "profile.")); return; }
        int temporada = e.temporada;
        PerfilDatos.PagarCristales("pase:" + temporada, Precio, error =>
        {
            if (error == null && E.temporada == temporada) { e.comprado = true; Guardar(); }
            listo?.Invoke(error);
        });
    }

    // Qué cosmético da cada nivel: sale del catálogo, con la rareza subiendo a lo largo de las páginas y los tipos
    // alternados. Cada temporada sortea otros (con la temporada como semilla), siempre los mismos para todos.
    private static readonly Dictionary<int, Premio[]> recompensas = new Dictionary<int, Premio[]>();

    private static Premio CosmeticoDe(int temporada, int nivel)
    {
        if (!recompensas.TryGetValue(temporada, out Premio[] lista))
        {
            lista = ArmarRecompensas(temporada);
            recompensas[temporada] = lista;
        }
        return nivel >= 1 && nivel <= Niveles ? lista[nivel - 1] : null;
    }

    private static Premio[] ArmarRecompensas(int temporada)
    {
        var rng = new System.Random(7919 * temporada + 17);
        var usados = new HashSet<string>();
        var lista = new Premio[Niveles];
        // Avatar, skin, tarjeta de presentación (banner), título, marco: en ese orden, una y otra vez.
        int[] tipos = { 0, 4, 1, 2, 3 };
        int cosmeticos = 0;
        for (int n = 1; n <= Niveles; n++)
        {
            if (Cristales.TryGetValue(n, out int cr)) { lista[n - 1] = new Premio { cristales = cr }; continue; }
            if (Creditos.TryGetValue(n, out int cd)) { lista[n - 1] = new Premio { creditos = cd }; continue; }
            Rareza r = n == Niveles ? Rareza.Legendario : n % 10 == 0 ? Rareza.Epico : n <= 10 ? (n % 3 == 0 ? Rareza.Raro : Rareza.Comun)
                : n <= 20 ? Rareza.Raro : n <= 32 ? Rareza.Epico : (n % 4 == 0 ? Rareza.Legendario : Rareza.Epico);
            // Corrido uno por página de 5: así las gratis (niveles 1 y 6 de cada página) no son siempre del mismo tipo.
            int t = n == Niveles ? 4 : tipos[cosmeticos++ % tipos.Length];
            // Si no hay de ese tipo con esa rareza (todavía no hay skins legendarias), primero otro tipo de la misma rareza.
            Premio p = Elegir(rng, usados, t, r) ?? Elegir(rng, usados, -1, r) ?? Elegir(rng, usados, t, null) ?? Elegir(rng, usados, -1, null);
            if (p != null) { usados.Add(p.Clave); usados.Add("n:" + p.Nombre); } // sin dos del mismo conjunto (avatar y banner)
            lista[n - 1] = p;
        }
        return lista;
    }

    // tipo: 0 avatar, 1 banner, 2 título, 3 marco, 4 skin; -1 cualquiera. rareza null: cualquiera.
    private static Premio Elegir(System.Random rng, HashSet<string> usados, int tipo, Rareza? rareza)
    {
        var posibles = new List<Premio>();
        if (tipo == 4 || tipo < 0)
            foreach (CatalogoDeSkins.Skin s in CatalogoDeSkins.Skins)
                if ((rareza == null || s.rareza == rareza) && !usados.Contains("sk:" + s.id) && !usados.Contains("n:" + s.Texto)) posibles.Add(Premio.De(s));
        if (tipo != 4)
            foreach (Cosmetico c in PerfilDatos.Catalogo)
            {
                if (c.rareza == Rareza.Inicial || (tipo >= 0 && (int)c.tipo != tipo)) continue;
                if ((rareza == null || c.rareza == rareza) && !usados.Contains(c.Clave) && !usados.Contains("n:" + L10n.Source(c.nombre, "profile."))) posibles.Add(Premio.De(c));
            }
        return posibles.Count > 0 ? posibles[rng.Next(posibles.Count)] : null;
    }

    // ================= Desafíos (US 365) =================

    public sealed class Desafio
    {
        public string id, nombre, modo;
        public int[] metas, xp;
        public bool porMaximo; // "Llegar a la oleada": cuenta la mejor partida, no la suma

        public string Nombre(int etapa) => metas.Length > 1 ? nombre.Replace("{n}", PerfilDatos.Numero(metas[etapa])) : nombre;
        public int EtapasCompletas(int avance)
        {
            int n = 0;
            while (n < metas.Length && avance >= metas[n]) n++;
            return n;
        }
    }

    public static readonly List<Desafio> Desafios = ArmarDesafios();

    private static List<Desafio> ArmarDesafios()
    {
        var l = new List<Desafio>();
        void D(string id, string nombre, string modo, int meta, int xp) => l.Add(new Desafio { id = id, nombre = nombre, modo = modo, metas = new[] { meta }, xp = new[] { xp } });
        foreach (string p in new[] { "Soledad", "Unzué", "Goya", "Necochea" })
            D("personaje." + IdDePersonaje(p), $"Jugar 3 partidas con {p}", "Táctico", 3, 300);
        D("cabezas", "Eliminar 3 enemigos con disparos a la cabeza", "Deathmatch o Táctico", 3, 300);
        l.Add(new Desafio { id = "invasion.bajas", nombre = "Eliminar {n} enemigos", modo = "Invasión",
            metas = new[] { 10, 20, 40, 80, 160, 320 }, xp = new[] { 300, 300, 500, 500, 750, 1000 } });
        D("ganar.tactico", "Ganar 2 partidas", "Táctico", 2, 300);
        D("ganar.deathmatch", "Ganar 1 partida", "Deathmatch", 1, 300);
        l.Add(new Desafio { id = "invasion.oleada", nombre = "Llegar a la oleada {n}", modo = "Invasión", porMaximo = true,
            metas = new[] { 10, 20, 30, 40 }, xp = new[] { 300, 500, 750, 1000 } });
        D("sin-mira", "Eliminar 5 enemigos sin apuntar con el Último Tren", "Deathmatch o Táctico", 5, 500);
        foreach (CatalogoDeSkins.Arma a in CatalogoDeSkins.Armas)
            D("arma." + a.id, $"Eliminar 15 enemigos con {a.nombre}", "Deathmatch o Táctico", 15, 300);
        D("invasion.puestos", "Cargar 3 puestos de suministros", "Invasión", 3, 300);
        D("invasion.jefe", "Eliminar al Jefe", "Invasión", 1, 750);
        D("invasion.alfas", "Eliminar 10 alfas", "Invasión", 10, 500);
        return l;
    }

    /// <summary>
    /// El id del personaje para las claves de la nube: en minúsculas y sin tildes ("Unzué" → "unzue"). La base solo acepta
    /// claves con a-z, 0-9, punto, guion y guion bajo (rw_sync_progress): con una mayúscula o una tilde rechazaba todo el
    /// progreso del jugador.
    /// </summary>
    public static string IdDePersonaje(string nombre)
    {
        if (string.IsNullOrEmpty(nombre)) return "";
        var sb = new System.Text.StringBuilder();
        foreach (char c in nombre.Normalize(System.Text.NormalizationForm.FormD).ToLowerInvariant())
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            sb.Append((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_' ? c : '-');
        }
        return sb.ToString();
    }

    // Los desafíos por personaje se guardaban con el nombre tal cual ("personaje.Unzué"): se pasan al id nuevo.
    private static void MigrarIds(List<string> lista)
    {
        for (int i = 0; i < lista.Count; i++)
        {
            string s = lista[i];
            if (!s.StartsWith("personaje.", StringComparison.Ordinal)) continue;
            int igual = s.IndexOf('=');
            if (igual < 0) continue;
            string nuevo = "personaje." + IdDePersonaje(s.Substring(10, igual - 10)) + s.Substring(igual);
            if (nuevo != s) lista[i] = nuevo;
        }
    }

    public static int Avance(string id) => Leer(E.desafios, id);
    public static int EtapasCobradas(string id) => Leer(E.etapas, id);

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

    // ================= Nube (ProgresoNube) =================

    /// <summary>Contadores que solo suben dentro de la temporada: la experiencia, la compra y el avance de los desafíos.</summary>
    public static IEnumerable<(string clave, long valor)> Contadores
    {
        get
        {
            Estado s = E;
            string t = "pase." + s.temporada + ".";
            yield return (t + "xp", s.xp);
            if (s.comprado) yield return (t + "comprado", 1);
            foreach (Desafio d in Desafios)
            {
                int a = Leer(s.desafios, d.id), c = Leer(s.etapas, d.id);
                if (a > 0) yield return (t + "d." + d.id, a);
                if (c > 0) yield return (t + "e." + d.id, c);
            }
        }
    }

    public static string ClaveReclamados => "pase." + E.temporada + ".reclamados";
    public static IEnumerable<string> Reclamados => E.reclamados;

    /// <summary>Junta lo que volvió de la nube: el máximo de cada contador y la unión de lo reclamado.</summary>
    public static void FusionarNube(Func<string, long> leer, IEnumerable<string> reclamados)
    {
        Estado s = E;
        string t = "pase." + s.temporada + ".";
        bool cambio = false;
        int xp = (int)leer(t + "xp");
        if (xp > s.xp) { s.xp = xp; cambio = true; }
        if (!s.comprado && leer(t + "comprado") > 0) { s.comprado = true; cambio = true; }
        foreach (Desafio d in Desafios)
        {
            int a = (int)leer(t + "d." + d.id), c = (int)leer(t + "e." + d.id);
            if (a > Leer(s.desafios, d.id)) { Poner(s.desafios, d.id, a); cambio = true; }
            if (c > Leer(s.etapas, d.id)) { Poner(s.etapas, d.id, c); cambio = true; }
        }
        if (reclamados != null)
            foreach (string r in reclamados)
                if (!string.IsNullOrEmpty(r) && !s.reclamados.Contains(r)) { s.reclamados.Add(r); cambio = true; }
        if (cambio) Guardar();
    }

    // ================= Pruebas en el editor =================

    private static int DiasDePrueba
    {
        get
        {
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetInt("Riftwalker.Pase.Dias", 0);
#else
            return 0;
#endif
        }
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Riftwalker/Pase de batalla/Sumar 1.000 de experiencia")]
    private static void SumarXpDePrueba()
    {
        Resumen r = Sumar(XpPorNivel, null, null);
        Debug.Log($"Pase: nivel {r.nivelAntes} → {r.nivelDespues} ({Xp} de experiencia).");
    }

    [UnityEditor.MenuItem("Riftwalker/Pase de batalla/Adelantar 7 días")]
    private static void AdelantarDias()
    {
        UnityEditor.EditorPrefs.SetInt("Riftwalker.Pase.Dias", DiasDePrueba + 7);
        Debug.Log($"Pase: {NombreTemporada}, faltan {DiasQueFaltan} días.");
        Cambio?.Invoke();
    }

    [UnityEditor.MenuItem("Riftwalker/Pase de batalla/Volver a la fecha real")]
    private static void FechaReal()
    {
        UnityEditor.EditorPrefs.DeleteKey("Riftwalker.Pase.Dias");
        Debug.Log($"Pase: {NombreTemporada}, faltan {DiasQueFaltan} días.");
        Cambio?.Invoke();
    }

    [UnityEditor.MenuItem("Riftwalker/Pase de batalla/Reiniciar el pase")]
    private static void ReiniciarDePrueba()
    {
        Guardado.DeleteKey(Clave);
        Guardado.Save();
        Recargar();
        Debug.Log("Pase: reiniciado.");
    }
#endif
}
