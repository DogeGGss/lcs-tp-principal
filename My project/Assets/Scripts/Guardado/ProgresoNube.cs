using System;
using System.Collections.Generic;
using UnityEngine;

// El progreso del jugador que se guarda también en la nube (F18, US 162, CA3, y US 163, CA2), además de los logros, que
// van con el ranking (LogrosOnline, rw_sync_comets): las estadísticas del minijuego (US 159) y las del jugador (F24), los
// récords de Zombie y el Perfil (cosméticos, cápsulas abiertas, pity, compras y últimos premios). Arma el documento que
// sube LogrosOnline (rw_sync_progress) y junta el que vuelve:
// - contadores que solo suben: el máximo (el pity va como «cápsula del último épico», que también solo sube);
// - mejores tiempos: el mínimo; cosméticos, packs de una vez y compras: la unión;
// - equipados y últimos premios: los de la PC, salvo en una PC nueva, que primero trae los de la nube (PerfilDatos.EquipadoPropio).
// Las opciones no se suben. Las monedas tienen su propio saldo en la base (US 272, LogrosOnline.Billetera): juntarlas
// con el máximo devolvería lo gastado.
// Todo va atado al id del jugador en la base, nunca al nombre: así se le puede sumar una cuenta después (US 237).
// También registra en Guardado qué claves se juntan con las de otro juego abierto en esta PC.
public static class ProgresoNube
{
    [Serializable] public sealed class Numero { public string clave; public long valor; }
    [Serializable] public sealed class Conjunto { public string clave; public string[] elementos; }
    [Serializable] public sealed class Texto { public string clave; public string valor; }

    [Serializable]
    public sealed class Documento
    {
        public List<Numero> contadores = new List<Numero>();
        public List<Numero> minimos = new List<Numero>();
        public List<Conjunto> conjuntos = new List<Conjunto>();
        public List<Texto> valores = new List<Texto>();
    }

    private const string Minijuego = "minijuego.", Zombie = "zombie.record.", Perfil = "perfil.";
    private const string Tactico = "estadisticas.tactico.", Deathmatch = "estadisticas.deathmatch.";
    private static readonly string[] Tipos = { "comun", "raro", "epico" };

    // Antes de la primera escena: qué claves del archivo se juntan con las de otro juego abierto, y quién vuelve a
    // leer lo suyo si el archivo trae algo nuevo.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Registrar()
    {
        Guardado.RegistrarUnion(LogrosCometas.Clave, LogrosCometas.JuntarJson);
        Guardado.RegistrarUnion(EstadisticasMinijuego.Clave, EstadisticasMinijuego.Juntar);
        Guardado.RegistrarUnion(EstadisticasJugador.Clave, EstadisticasJugador.Juntar);
        Guardado.RegistrarMaximo(MatchSettings.ZombieRecordPrefix);
        Guardado.Juntado += clave =>
        {
            if (clave == LogrosCometas.Clave) LogrosCometas.Recargar();
            else if (clave == EstadisticasMinijuego.Clave) EstadisticasMinijuego.Recargar();
            else if (clave == EstadisticasJugador.Clave) EstadisticasJugador.Recargar();
            else if (clave == PerfilDatos.Clave) PerfilDatos.Recargar();
        };
        // Se entró o se salió de una cuenta (US 237): todo lo del jugador es otro. Logros antes que estadísticas: al
        // recargarlas, los logros de combate se revisan con lo de la cuenta.
        Guardado.CambioDeJugador += () =>
        {
            LogrosCometas.Recargar();
            EstadisticasMinijuego.Recargar();
            EstadisticasJugador.Recargar();
            PerfilDatos.Recargar();
        };
    }

    // ---------- Subir ----------

    public static Documento Armar()
    {
        var d = new Documento();
        EstadisticasMinijuego.Totales m = EstadisticasMinijuego.Datos;
        Sumar(d.contadores, Minijuego + "fases", m.fases);
        Sumar(d.contadores, Minijuego + "disparos", m.disparos);
        Sumar(d.contadores, Minijuego + "puntos", m.puntos);
        for (int i = 0; i < 3; i++) Sumar(d.contadores, Minijuego + "aciertos." + Tipos[i], m.aciertos[i]);
        Sumar(d.contadores, Minijuego + "reaccion_ms", m.reaccionMs);
        Sumar(d.contadores, Minijuego + "record_cometas", m.recordCometas);
        Sumar(d.contadores, Minijuego + "record_puntos", m.recordPuntos);
        if (m.mejorReaccionMs > 0) d.minimos.Add(new Numero { clave = Minijuego + "mejor_reaccion_ms", valor = m.mejorReaccionMs });

        EstadisticasJugador.Totales e = EstadisticasJugador.Datos;
        SumarModo(d.contadores, Tactico, e.tactico);
        SumarModo(d.contadores, Deathmatch, e.deathmatch);
        Sumar(d.contadores, Tactico + "rondas_ganadas", e.rondasGanadas);
        Sumar(d.contadores, Tactico + "plantados", e.plantados);
        Sumar(d.contadores, Tactico + "desactivados", e.desactivados);
        Sumar(d.contadores, Tactico + "mvp", e.mvp);
        Sumar(d.contadores, Deathmatch + "mejor_racha", e.mejorRacha);

        foreach (ZombieDifficulty z in Enum.GetValues(typeof(ZombieDifficulty)))
            Sumar(d.contadores, Zombie + z.ToString().ToLowerInvariant(), MatchSettings.ZombieRecord(z));

        d.conjuntos.Add(Ordenado(Perfil + "obtenidos", PerfilDatos.ObtenidosIds));
        d.conjuntos.Add(Ordenado(Perfil + "packs_usados", PerfilDatos.PacksUsados));
        d.conjuntos.Add(Ordenado(Perfil + "compras", PerfilDatos.ComprasNube));
        Sumar(d.contadores, Perfil + "aperturas", PerfilDatos.Aperturas);
        Sumar(d.contadores, Perfil + "ultimo_epico", PerfilDatos.UltimoEpico);
        Sumar(d.contadores, Perfil + "gastado", PerfilDatos.Gastado);
        if (PerfilDatos.EquipadoPropio) // en una PC nueva, lo de entrada no pisa lo de la nube
        {
            foreach (var (tipo, id) in PerfilDatos.Equipados) d.valores.Add(new Texto { clave = Perfil + tipo, valor = id });
            IReadOnlyList<string> recientes = PerfilDatos.Recientes;
            for (int i = 0; i < recientes.Count; i++) d.valores.Add(new Texto { clave = Reciente + i, valor = recientes[i] });
        }
        return d;
    }

    private const string Reciente = Perfil + "reciente.";

    private static Conjunto Ordenado(string clave, IEnumerable<string> elementos)
    {
        var lista = new List<string>(elementos);
        lista.Sort(string.CompareOrdinal);
        return new Conjunto { clave = clave, elementos = lista.ToArray() };
    }

    public static string Json() => JsonUtility.ToJson(Armar());

    private static void Sumar(List<Numero> lista, string clave, long valor)
    {
        if (valor > 0) lista.Add(new Numero { clave = clave, valor = valor });
    }

    private static void SumarModo(List<Numero> lista, string prefijo, EstadisticasJugador.Modo m)
    {
        Sumar(lista, prefijo + "partidas", m.partidas);
        Sumar(lista, prefijo + "victorias", m.victorias);
        Sumar(lista, prefijo + "bajas", m.bajas);
        Sumar(lista, prefijo + "muertes", m.muertes);
        Sumar(lista, prefijo + "asistencias", m.asistencias);
        Sumar(lista, prefijo + "cabezas", m.cabezas);
        Sumar(lista, prefijo + "segundos", m.segundos);
    }

    // ---------- Juntar lo que vuelve ----------

    public static void Fusionar(Documento nube)
    {
        if (nube == null) return;
        var contadores = new Dictionary<string, long>();
        if (nube.contadores != null) foreach (Numero n in nube.contadores) if (n != null && n.clave != null) contadores[n.clave] = n.valor;
        long Leer(string clave) => contadores.TryGetValue(clave, out long v) ? v : 0;

        // Minijuego (US 159)
        var m = new EstadisticasMinijuego.Totales
        {
            fases = (int)Leer(Minijuego + "fases"),
            disparos = (int)Leer(Minijuego + "disparos"),
            puntos = (int)Leer(Minijuego + "puntos"),
            reaccionMs = Leer(Minijuego + "reaccion_ms"),
            recordCometas = (int)Leer(Minijuego + "record_cometas"),
            recordPuntos = (int)Leer(Minijuego + "record_puntos"),
        };
        for (int i = 0; i < 3; i++) m.aciertos[i] = (int)Leer(Minijuego + "aciertos." + Tipos[i]);
        if (nube.minimos != null)
            foreach (Numero n in nube.minimos)
                if (n != null && n.clave == Minijuego + "mejor_reaccion_ms") m.mejorReaccionMs = n.valor;
        EstadisticasMinijuego.UnirNube(m);

        // Estadísticas del jugador (F24)
        EstadisticasJugador.Modo LeerModo(string prefijo) => new EstadisticasJugador.Modo
        {
            partidas = (int)Leer(prefijo + "partidas"),
            victorias = (int)Leer(prefijo + "victorias"),
            bajas = (int)Leer(prefijo + "bajas"),
            muertes = (int)Leer(prefijo + "muertes"),
            asistencias = (int)Leer(prefijo + "asistencias"),
            cabezas = (int)Leer(prefijo + "cabezas"),
            segundos = Leer(prefijo + "segundos"),
        };
        EstadisticasJugador.UnirNube(new EstadisticasJugador.Totales
        {
            tactico = LeerModo(Tactico),
            deathmatch = LeerModo(Deathmatch),
            rondasGanadas = (int)Leer(Tactico + "rondas_ganadas"),
            plantados = (int)Leer(Tactico + "plantados"),
            desactivados = (int)Leer(Tactico + "desactivados"),
            mvp = (int)Leer(Tactico + "mvp"),
            mejorRacha = (int)Leer(Deathmatch + "mejor_racha"),
        });

        // Récords de Zombie
        foreach (ZombieDifficulty z in Enum.GetValues(typeof(ZombieDifficulty)))
            MatchSettings.SubirZombieRecord(z, (int)Leer(Zombie + z.ToString().ToLowerInvariant()));

        // Perfil: lo que solo suma (unión y máximo) y lo equipado y los últimos premios (solo en una PC nueva)
        var perfil = new PerfilDatos.Nube
        {
            aperturas = (int)Leer(Perfil + "aperturas"),
            ultimoEpico = (int)Leer(Perfil + "ultimo_epico"),
            gastado = (int)Leer(Perfil + "gastado"),
        };
        if (nube.conjuntos != null)
            foreach (Conjunto c in nube.conjuntos)
            {
                if (c == null || c.elementos == null) continue;
                if (c.clave == Perfil + "obtenidos") perfil.obtenidos.AddRange(c.elementos);
                else if (c.clave == Perfil + "packs_usados") perfil.packs.AddRange(c.elementos);
                else if (c.clave == Perfil + "compras") perfil.compras.AddRange(c.elementos);
            }
        var recientes = new SortedDictionary<int, string>();
        if (nube.valores != null)
            foreach (Texto t in nube.valores)
            {
                if (t == null || t.clave == null) continue;
                if (t.clave.StartsWith(Reciente, StringComparison.Ordinal))
                {
                    if (int.TryParse(t.clave.Substring(Reciente.Length), out int i) && !string.IsNullOrEmpty(t.valor)) recientes[i] = t.valor;
                }
                else if (t.clave.StartsWith(Perfil, StringComparison.Ordinal)) perfil.equipados[t.clave.Substring(Perfil.Length)] = t.valor ?? "";
            }
        perfil.recientes.AddRange(recientes.Values);
        PerfilDatos.FusionarNube(perfil);
    }
}
