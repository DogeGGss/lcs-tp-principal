using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

// Guardado del jugador (F18, US 162 y US 163): un solo archivo JSON en la carpeta de datos del juego con todo lo que
// antes iba a PlayerPrefs y a logros-cometas.json. Tiene los mismos métodos que PlayerPrefs, así cada sistema solo
// cambia la clase que usa.
// - Se guarda solo (US 162, CA1): cada cambio queda pendiente y se escribe al segundo (GuardadoAutomatico), cuando un
//   sistema lo pide (Save) y al cerrar el juego. Escribe en un temporal y reemplaza el archivo, que queda de respaldo
//   (.bak): si el juego se cierra de golpe, lo anterior no se pierde.
// - Se carga antes de la primera escena (US 163, CA1). Si el archivo está dañado usa el respaldo; si tampoco sirve
//   arranca de cero, avisa y deja el dañado aparte (CA3). Un archivo de una versión más nueva no se pisa (CA4).
// - Migración: si un dato no está en el archivo pero sí en PlayerPrefs (versiones anteriores del juego), se trae la
//   primera vez que se pide. PlayerPrefs no se borra (salvo con DeleteKey), así una versión anterior sigue andando.
// - Dos juegos abiertos en esta PC: antes de escribir se junta con el archivo. De lo que este juego no cambió gana lo
//   del archivo, y las claves de progreso registradas (RegistrarUnion, ProgresoNube) se juntan sin perder nada.
// En el editor usa otro archivo (jugador-editor.json): como con PlayerPrefs, las pruebas en el editor no se mezclan
// con lo del juego instalado. Con los tests automáticos (ModoPruebas) usa uno temporal que arranca vacío.
// Los datos temporales de "Volver a la partida" (Reconexion) siguen en PlayerPrefs.
// Cuenta (US 237 y US 281): con una cuenta abierta, el progreso (logros, estadísticas, perfil, monedas, emotes, récords y
// el nombre) va al archivo de la cuenta (jugador-<id>.json), y las opciones de la PC siguen en este. Sin cuenta, todo va
// acá, como siempre. cuenta-activa.txt dice al arrancar qué cuenta está abierta.
public static class Guardado
{
    public const int VersionFormato = 1;
    private const float Demora = 1f; // s desde el último cambio hasta escribir

    public static string Ruta => ModoPruebas.Activo ? RutaPruebas
        : Path.Combine(Application.persistentDataPath, Application.isEditor ? "jugador-editor.json" : "jugador.json");
    /// <summary>El archivo de los tests automáticos: en la carpeta temporal, nunca el progreso real.</summary>
    public static string RutaPruebas => Path.Combine(Application.temporaryCachePath, "jugador-pruebas.json");

    /// <summary>Lo que el jugador tiene que saber del guardado (archivo recuperado, dañado, de otra versión), o null.</summary>
    public static string Aviso => Actual.Aviso ?? cuenta?.Aviso;
    /// <summary>Sube con cada cambio, también los que vienen del archivo de otro juego abierto.</summary>
    public static int Revision { get; private set; }
    /// <summary>Una clave cambió al juntarse con el archivo de otro juego abierto en esta PC.</summary>
    public static event Action<string> Juntado;

    private static Almacen actual;
    private static Almacen cuenta; // el progreso de la cuenta abierta (US 237), o null sin cuenta
    private static string idCuenta;
    private static readonly Dictionary<string, Func<string, string, string>> uniones = new Dictionary<string, Func<string, string, string>>();
    private static readonly List<string> maximos = new List<string>();
    private static GuardadoAutomatico automatico;

    /// <summary>El archivo del jugador, cargado la primera vez que se usa.</summary>
    public static Almacen Actual
    {
        get
        {
            if (actual != null) return actual;
            // En los tests no se migra nada de PlayerPrefs: el jugador de prueba arranca de cero.
            actual = new Almacen(Ruta, !ModoPruebas.Activo);
            actual.Cargar();
            return actual;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar()
    {
        actual = null;
        cuenta = null;
        idCuenta = null;
        CambioDeJugador = null;
        automatico = null;
        Revision = 0;
        Juntado = null;
        uniones.Clear();
        maximos.Clear();
    }

    // US 163, CA1: antes de la primera escena, así el menú arranca con todo cargado.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AlIniciar()
    {
        _ = Actual;
        AbrirCuentaGuardada();
        if (automatico != null) return;
        var go = new GameObject("Guardado del jugador");
        UnityEngine.Object.DontDestroyOnLoad(go);
        automatico = go.AddComponent<GuardadoAutomatico>();
    }

    // ---------- Los mismos métodos que PlayerPrefs ----------

    public static int GetInt(string clave, int porDefecto = 0) => Para(clave).GetInt(clave, porDefecto);
    public static float GetFloat(string clave, float porDefecto = 0f) => Para(clave).GetFloat(clave, porDefecto);
    public static string GetString(string clave, string porDefecto = "") => Para(clave).GetString(clave, porDefecto);
    public static bool HasKey(string clave) => Para(clave).HasKey(clave);
    public static void SetInt(string clave, int valor) => Para(clave).SetInt(clave, valor);
    public static void SetFloat(string clave, float valor) => Para(clave).SetFloat(clave, valor);
    public static void SetString(string clave, string valor) => Para(clave).SetString(clave, valor);
    public static void DeleteKey(string clave) => Para(clave).DeleteKey(clave);
    /// <summary>Escribe ya lo pendiente (como PlayerPrefs.Save).</summary>
    public static void Save()
    {
        Actual.Guardar();
        cuenta?.Guardar();
    }

    /// <summary>Lo llama GuardadoAutomatico cada cuadro: escribe un segundo después del último cambio.</summary>
    public static void Tick()
    {
        if (actual != null && actual.Pendiente && Time.unscaledTime - actual.UltimoCambio >= Demora) actual.Guardar();
        if (cuenta != null && cuenta.Pendiente && Time.unscaledTime - cuenta.UltimoCambio >= Demora) cuenta.Guardar();
    }

    // ---------- Cuenta (US 237 y US 281) ----------

    // Lo que es del jugador y no de la PC: con una cuenta abierta va a su archivo. Las opciones (controles, gráficos,
    // sonido, mira, dificultad, personaje elegido) quedan en el de la PC.
    private static readonly string[] DeLaCuenta =
    {
        LogrosCometas.Clave, EstadisticasMinijuego.Clave, EstadisticasJugador.Clave, PerfilDatos.Clave,
        "Riftwalker.Emote", MatchSettings.ZombieRecordPrefix, "Jugador.Nombre"
    };

    /// <summary>La clave es progreso del jugador (va a la cuenta) y no una opción de la PC.</summary>
    public static bool EsDeLaCuenta(string clave)
    {
        foreach (string prefijo in DeLaCuenta) if (clave.StartsWith(prefijo, StringComparison.Ordinal)) return true;
        return false;
    }

    private static Almacen Para(string clave) => cuenta != null && EsDeLaCuenta(clave) ? cuenta : Actual;

    /// <summary>El id de la cuenta abierta, o null sin cuenta.</summary>
    public static string CuentaActiva => idCuenta;

    /// <summary>Cambió el jugador (se entró o se salió de una cuenta): cada sistema vuelve a leer lo suyo.</summary>
    public static event Action CambioDeJugador;

    private static string RutaMarcaCuenta => Path.Combine(Application.persistentDataPath, "cuenta-activa.txt");
    private static string RutaCuenta(string id) =>
        Path.Combine(Application.persistentDataPath, (Application.isEditor ? "jugador-editor-" : "jugador-") + id + ".json");

    /// <summary>Abre el progreso de la cuenta id (o vuelve al de la PC con null) y avisa a todos.</summary>
    public static void UsarCuenta(string id)
    {
        if (ModoPruebas.Activo || id == idCuenta) return;
        cuenta?.Guardar();
        idCuenta = string.IsNullOrEmpty(id) ? null : id;
        cuenta = idCuenta == null ? null : new Almacen(RutaCuenta(idCuenta), false);
        cuenta?.Cargar();
        try
        {
            if (idCuenta == null) File.Delete(RutaMarcaCuenta);
            else EscribirAtomico(RutaMarcaCuenta, idCuenta);
        }
        catch { } // sin la marca, al volver a abrir el juego arranca sin cuenta: se vuelve a iniciar sesión
        Revision++;
        CambioDeJugador?.Invoke();
    }

    // Antes de la primera escena: si quedó una cuenta abierta, su progreso.
    private static void AbrirCuentaGuardada()
    {
        if (ModoPruebas.Activo || cuenta != null) return;
        string id = null;
        try { if (File.Exists(RutaMarcaCuenta)) id = File.ReadAllText(RutaMarcaCuenta).Trim(); } catch { }
        if (string.IsNullOrEmpty(id) || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return;
        idCuenta = id;
        cuenta = new Almacen(RutaCuenta(id), false);
        cuenta.Cargar();
    }

    // ---------- Progreso que se junta con el de otro juego abierto ----------

    /// <summary>Al juntarse con el archivo, esta clave se arma con juntar(lo de este juego, lo del archivo).</summary>
    public static void RegistrarUnion(string clave, Func<string, string, string> juntar) => uniones[clave] = juntar;

    /// <summary>Las claves enteras que empiezan así (por ejemplo, récords) se quedan con el máximo.</summary>
    public static void RegistrarMaximo(string prefijo)
    {
        if (!maximos.Contains(prefijo)) maximos.Add(prefijo);
    }

    internal static Func<string, string, string> UnionDe(string clave)
    {
        if (uniones.TryGetValue(clave, out var juntar)) return juntar;
        foreach (string prefijo in maximos)
            if (clave.StartsWith(prefijo, StringComparison.Ordinal))
                return (a, b) => Math.Max(Entero(a), Entero(b)).ToString(CultureInfo.InvariantCulture);
        return null;
    }

    internal static void Cambio() => Revision++;

    internal static void AvisarJuntado(string clave)
    {
        Revision++;
        Juntado?.Invoke(clave);
    }

    private static int Entero(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;

    /// <summary>Escribe en un temporal y reemplaza el archivo, que queda como respaldo .bak.</summary>
    public static void EscribirAtomico(string ruta, string contenido)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ruta));
        File.WriteAllText(ruta + ".tmp", contenido);
        if (File.Exists(ruta)) File.Replace(ruta + ".tmp", ruta, ruta + ".bak");
        else File.Move(ruta + ".tmp", ruta);
    }

    // =====================================================================
    // El archivo
    // =====================================================================

    [Serializable]
    public sealed class Entrada
    {
        public string clave;
        public int tipo; // 0: entero, 1: decimal, 2: texto
        public string valor;
    }

    [Serializable]
    public sealed class Archivo
    {
        public int version = VersionFormato;
        public List<Entrada> valores = new List<Entrada>();
    }

    /// <summary>Un archivo de guardado. Guardado usa uno (Actual); las pruebas arman otros con su propia ruta.</summary>
    public sealed class Almacen
    {
        public const string NoSePudoGuardar = "No se pudo guardar tu progreso; se va a volver a intentar.";
        private const int Entero = 0, Decimal = 1, Texto = 2;

        public readonly string ruta;
        private readonly bool migrar;
        private Dictionary<string, Entrada> datos = new Dictionary<string, Entrada>();
        private readonly HashSet<string> cambiadas = new HashSet<string>(), borradas = new HashSet<string>();

        public string Aviso { get; private set; }
        public bool SoloLectura { get; private set; }
        public bool Pendiente { get; private set; }
        public float UltimoCambio { get; private set; }
        /// <summary>No había archivo: es la primera vez que se juega (o se perdió todo).</summary>
        public bool Nuevo { get; private set; }

        /// <param name="migrarPlayerPrefs">Traer de PlayerPrefs lo que no esté en el archivo.</param>
        public Almacen(string ruta, bool migrarPlayerPrefs)
        {
            this.ruta = ruta;
            migrar = migrarPlayerPrefs;
        }

        // ---------- Cargar (US 163) ----------

        public void Cargar()
        {
            datos = new Dictionary<string, Entrada>();
            cambiadas.Clear();
            borradas.Clear();
            bool hay = File.Exists(ruta), hayRespaldo = File.Exists(ruta + ".bak");
            if (!hay && !hayRespaldo) { Nuevo = true; return; }

            Archivo archivo = null;
            try { archivo = Leer(ruta); }
            catch
            {
                // CA3: el dañado queda aparte, sin borrarlo; se sigue con el respaldo o de cero.
                if (hay) Apartar(ruta);
                try
                {
                    archivo = Leer(ruta + ".bak");
                    Aviso = "Tu progreso se recuperó de la copia de respaldo.";
                    Pendiente = true; // se vuelve a escribir el archivo principal
                }
                catch
                {
                    Aviso = "No se pudo cargar tu progreso: el juego arrancó de cero y el archivo dañado quedó guardado aparte.";
                    Nuevo = true;
                    return;
                }
            }

            // CA4: de una versión más nueva no se pisa.
            if (archivo.version > VersionFormato)
            {
                SoloLectura = true;
                Aviso = "Tu progreso es de una versión más nueva del juego: actualizalo para seguir guardando. Mientras tanto no se guarda nada.";
            }
            foreach (Entrada e in archivo.valores)
                if (e != null && !string.IsNullOrEmpty(e.clave)) datos[e.clave] = e;
        }

        public static Archivo Leer(string ruta)
        {
            Archivo archivo = JsonUtility.FromJson<Archivo>(File.ReadAllText(ruta));
            if (archivo == null || archivo.version < 1 || archivo.valores == null) throw new InvalidDataException("Formato de guardado inválido");
            return archivo;
        }

        private static void Apartar(string ruta)
        {
            try
            {
                string carpeta = Path.GetDirectoryName(ruta), nombre = Path.GetFileNameWithoutExtension(ruta);
                File.Copy(ruta, Path.Combine(carpeta, $"{nombre}.danado-{DateTime.Now:yyyyMMdd-HHmmss}.json"), false);
            }
            catch { }
        }

        // ---------- Leer ----------

        public bool HasKey(string clave) =>
            datos.ContainsKey(clave) || (migrar && !borradas.Contains(clave) && PlayerPrefs.HasKey(clave));

        public int GetInt(string clave, int porDefecto) =>
            Buscar(clave, Entero) is Entrada e && int.TryParse(e.valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : porDefecto;

        public float GetFloat(string clave, float porDefecto) =>
            Buscar(clave, Decimal) is Entrada e && float.TryParse(e.valor, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : porDefecto;

        public string GetString(string clave, string porDefecto) =>
            Buscar(clave, Texto) is Entrada e ? e.valor : porDefecto;

        // Como PlayerPrefs: si la clave es de otro tipo, se usa el valor por defecto.
        private Entrada Buscar(string clave, int tipo)
        {
            if (datos.TryGetValue(clave, out Entrada e)) return e.tipo == tipo ? e : null;
            if (!migrar || borradas.Contains(clave) || !PlayerPrefs.HasKey(clave)) return null;
            // Migración desde PlayerPrefs: valores imposibles para saber si la clave es de este tipo.
            string valor = null;
            if (tipo == Entero) { int n = PlayerPrefs.GetInt(clave, int.MinValue); if (n != int.MinValue) valor = n.ToString(CultureInfo.InvariantCulture); }
            else if (tipo == Decimal) { float f = PlayerPrefs.GetFloat(clave, float.NaN); if (!float.IsNaN(f)) valor = f.ToString("R", CultureInfo.InvariantCulture); }
            else { const string Ninguno = "\u0001"; string s = PlayerPrefs.GetString(clave, Ninguno); if (s != Ninguno) valor = s; }
            if (valor == null) return null;
            e = new Entrada { clave = clave, tipo = tipo, valor = valor };
            datos[clave] = e;
            Marcar(clave);
            return e;
        }

        // ---------- Escribir ----------

        public void SetInt(string clave, int valor) => Poner(clave, Entero, valor.ToString(CultureInfo.InvariantCulture));
        public void SetFloat(string clave, float valor) => Poner(clave, Decimal, valor.ToString("R", CultureInfo.InvariantCulture));
        public void SetString(string clave, string valor) => Poner(clave, Texto, valor ?? "");

        public void DeleteKey(string clave)
        {
            bool estaba = datos.Remove(clave);
            if (migrar && PlayerPrefs.HasKey(clave)) { PlayerPrefs.DeleteKey(clave); estaba = true; }
            if (!estaba) return;
            borradas.Add(clave);
            cambiadas.Remove(clave);
            Pendiente = true;
            UltimoCambio = Time.unscaledTime;
            Guardado.Cambio();
        }

        private void Poner(string clave, int tipo, string valor)
        {
            if (datos.TryGetValue(clave, out Entrada e) && e.tipo == tipo && e.valor == valor) return;
            datos[clave] = new Entrada { clave = clave, tipo = tipo, valor = valor };
            borradas.Remove(clave);
            Marcar(clave);
        }

        private void Marcar(string clave)
        {
            cambiadas.Add(clave);
            Pendiente = true;
            UltimoCambio = Time.unscaledTime;
            Guardado.Cambio();
        }

        /// <summary>Escribe lo pendiente, juntándolo antes con el archivo. Devuelve si escribió.</summary>
        public bool Guardar()
        {
            if (!Pendiente || SoloLectura) return false;
            try
            {
                Archivo enDisco = null;
                try { if (File.Exists(ruta)) enDisco = Leer(ruta); } catch { }
                if (enDisco != null && enDisco.version <= VersionFormato) Juntar(enDisco);
                var archivo = new Archivo();
                foreach (Entrada e in datos.Values) archivo.valores.Add(e);
                archivo.valores.Sort((a, b) => string.CompareOrdinal(a.clave, b.clave));
                EscribirAtomico(ruta, JsonUtility.ToJson(archivo, true));
                cambiadas.Clear();
                borradas.Clear();
                Pendiente = false;
                Nuevo = false;
                if (Aviso == NoSePudoGuardar) Aviso = null;
                return true;
            }
            catch (Exception)
            {
                Aviso = NoSePudoGuardar;
                return false;
            }
        }

        // Otro juego abierto en esta PC pudo guardar después de que este cargó el archivo.
        private void Juntar(Archivo disco)
        {
            foreach (Entrada otra in disco.valores)
            {
                if (otra == null || string.IsNullOrEmpty(otra.clave) || borradas.Contains(otra.clave)) continue;
                datos.TryGetValue(otra.clave, out Entrada mia);
                Func<string, string, string> juntar = Guardado.UnionDe(otra.clave);
                if (juntar != null && mia != null && mia.tipo == otra.tipo)
                {
                    string junto;
                    try { junto = juntar(mia.valor, otra.valor); } catch { junto = mia.valor; }
                    if (junto != mia.valor)
                    {
                        mia.valor = junto;
                        Guardado.AvisarJuntado(otra.clave); // quien lo tenga en memoria lo vuelve a leer
                    }
                    continue;
                }
                if (cambiadas.Contains(otra.clave)) continue; // lo que cambió este juego gana
                if (mia == null || mia.tipo != otra.tipo || mia.valor != otra.valor)
                {
                    datos[otra.clave] = new Entrada { clave = otra.clave, tipo = otra.tipo, valor = otra.valor };
                    Guardado.AvisarJuntado(otra.clave);
                }
            }
        }
    }
}
