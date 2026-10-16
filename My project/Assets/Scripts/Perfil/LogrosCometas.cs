using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Catálogo y progreso real. Independiente de la UI y del servicio online.
// Se guarda con el progreso del jugador (Guardado, clave "Logros.Cometas", US 045 y US 162). La primera vez se trae lo
// que haya en el archivo anterior, logros-cometas.json, que no se borra.
public static class LogrosCometas
{
    public sealed class Definicion
    {
        public readonly string id, nombre, color;
        public readonly int tipo, meta;
        public Definicion(string id, string nombre, string color, int tipo, int meta)
        { this.id = id; this.nombre = nombre; this.color = color; this.tipo = tipo; this.meta = meta; }
    }
    // Los logros nuevos se agregan siempre AL FINAL: las fechas del guardado van en este mismo orden.
    public static readonly Definicion[] Catalogo = {
        new Definicion("azul_10", "Primer rastro azul", "azules", 0, 10),
        new Definicion("azul_50", "Cazador celeste", "azules", 0, 50),
        new Definicion("azul_100", "Dominio azul", "azules", 0, 100),
        new Definicion("verde_10", "Destello esmeralda", "verdes", 1, 10),
        new Definicion("verde_50", "Órbita esmeralda", "verdes", 1, 50),
        new Definicion("verde_100", "Dominio verde", "verdes", 1, 100),
        new Definicion("dorado_10", "Polvo de oro", "dorados", 2, 10),
        new Definicion("dorado_50", "Fiebre del oro", "dorados", 2, 50),
        new Definicion("dorado_100", "Dominio dorado", "dorados", 2, 100)
    };
    [Serializable] public sealed class Progreso
    {
        public int version = 1;
        public int[] aciertos = new int[3];
        public string[] fechas = new string[Catalogo.Length];
    }
    public const string Clave = "Logros.Cometas";
    private static Progreso datos;
    private static bool soloLectura;
    private static string aviso;
    public static int Revision { get; private set; }
    /// <summary>Lo que el jugador tiene que saber del guardado de los logros (o del archivo del jugador), o null.</summary>
    public static string AvisoGuardado => aviso ?? Guardado.Aviso;
    public static event Action<Definicion> Desbloqueado;
    /// <summary>Donde se guardaba antes (hasta la US 162): solo se lee para migrar.</summary>
    public static string RutaAnterior => Path.Combine(Application.persistentDataPath, "logros-cometas.json");
    public static Progreso Datos { get { if (datos == null) Cargar(); return datos; } }
    public static int Cantidad { get { int n = 0; foreach (var d in Catalogo) if (Datos.aciertos[d.tipo] >= d.meta) n++; return n; } }

    // Función pura: permite probar saltos de metas, tipos y ausencia de duplicados sin tocar el guardado real.
    public static List<Definicion> Aplicar(Progreso p, int tipo, int total, string fecha)
    {
        if (tipo < 0 || tipo > 2) throw new ArgumentOutOfRangeException(nameof(tipo));
        p.aciertos[tipo] = Math.Max(p.aciertos[tipo], Math.Max(0, total));
        var nuevos = new List<Definicion>();
        for (int i = 0; i < Catalogo.Length; i++)
            if (p.aciertos[Catalogo[i].tipo] >= Catalogo[i].meta && string.IsNullOrEmpty(p.fechas[i]))
            { p.fechas[i] = fecha; nuevos.Add(Catalogo[i]); }
        return nuevos;
    }
    public static void Registrar(TipoCometa tipo)
    {
        var p = Datos;
        if (soloLectura) return;
        int i = (int)tipo;
        var nuevos = Aplicar(p, i, p.aciertos[i] < int.MaxValue ? p.aciertos[i] + 1 : int.MaxValue, DateTime.UtcNow.ToString("O"));
        Revision++;
        Guardar();
        if (nuevos.Count > 0) Guardado.Save(); // un logro nuevo se escribe ya, sin esperar
        foreach (var logro in nuevos) Desbloqueado?.Invoke(logro);
    }
    public static void Fusionar(int azul, int verde, int dorado)
    {
        var p = Datos; if (soloLectura) return;
        int[] valores = { azul, verde, dorado }; bool cambio = false;
        for (int i = 0; i < 3; i++) if (valores[i] > p.aciertos[i])
        { Aplicar(p, i, valores[i], DateTime.UtcNow.ToString("O")); cambio = true; }
        if (cambio) { Revision++; Guardar(); }
    }
    private static Progreso Leer(string ruta) => Validar(JsonUtility.FromJson<Progreso>(File.ReadAllText(ruta)));

    /// <summary>Para Guardado.RegistrarUnion: junta lo de este juego con lo del archivo (el máximo de cada color).</summary>
    public static string JuntarJson(string mio, string otro)
    {
        Progreso a, b;
        try { a = Validar(JsonUtility.FromJson<Progreso>(mio)); } catch { return otro; }
        try { b = Validar(JsonUtility.FromJson<Progreso>(otro)); } catch { return mio; }
        return Juntar(a, b) ? JsonUtility.ToJson(a) : mio;
    }

    /// <summary>Vuelve a leer lo guardado (lo cambió otro juego abierto en esta PC).</summary>
    public static void Recargar()
    {
        datos = null;
        Revision++;
    }

    /// <summary>
    /// Revisa un progreso leído. Acepta otra cantidad de fechas que la del catálogo (de una versión con menos o más
    /// logros): se ajustan al catálogo actual, así un guardado viejo sigue sirviendo cuando se agregan logros.
    /// </summary>
    public static Progreso Validar(Progreso p)
    {
        if (p == null || p.version < 1 || p.aciertos == null || p.aciertos.Length != 3)
            throw new InvalidDataException("Formato de logros inválido");
        foreach (int n in p.aciertos) if (n < 0) throw new InvalidDataException("Contador inválido");
        if (p.fechas == null) p.fechas = new string[Catalogo.Length];
        else if (p.fechas.Length != Catalogo.Length) Array.Resize(ref p.fechas, Catalogo.Length);
        return p;
    }

    /// <summary>
    /// Suma a "p" lo que tiene "otro": el máximo de cada color y las fechas de logro que le falten. Devuelve si cambió.
    /// </summary>
    public static bool Juntar(Progreso p, Progreso otro)
    {
        bool cambio = false;
        for (int i = 0; i < 3; i++)
            if (otro.aciertos[i] > p.aciertos[i]) { p.aciertos[i] = otro.aciertos[i]; cambio = true; }
        for (int i = 0; i < Catalogo.Length && i < otro.fechas.Length; i++)
            if (string.IsNullOrEmpty(p.fechas[i]) && !string.IsNullOrEmpty(otro.fechas[i])) { p.fechas[i] = otro.fechas[i]; cambio = true; }
        return cambio;
    }
    private static void Cargar()
    {
        string json = Guardado.GetString(Clave, "");
        if (!string.IsNullOrEmpty(json))
        {
            try { datos = Validar(JsonUtility.FromJson<Progreso>(json)); }
            catch
            {
                // Una parte dañada del archivo no se pisa: se juega sin contar logros hasta que se arregle.
                datos = new Progreso(); soloLectura = true;
                aviso = "No se pudo leer el progreso de los logros. Se conservó tal como estaba.";
            }
            if (datos.version > 1) { soloLectura = true; aviso = "Este progreso de logros requiere una versión más reciente del juego."; }
            return;
        }
        // Migración (US 162): lo que había en logros-cometas.json (o en su respaldo) pasa al archivo del jugador.
        datos = null;
        foreach (string ruta in new[] { RutaAnterior, RutaAnterior + ".bak" })
        {
            if (!File.Exists(ruta)) continue;
            try { datos = Leer(ruta); break; } catch { }
        }
        if (datos == null) { datos = new Progreso(); return; }
        if (datos.version > 1) { soloLectura = true; aviso = "Este progreso de logros requiere una versión más reciente del juego."; return; }
        Guardar();
    }

    private static void Guardar()
    {
        if (soloLectura || datos == null) return;
        Guardado.SetString(Clave, JsonUtility.ToJson(datos));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReiniciarSesion()
    { datos = null; soloLectura = false; Revision = 0; aviso = null; Desbloqueado = null; }
}
