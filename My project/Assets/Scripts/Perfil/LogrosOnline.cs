using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// REST de Supabase sin SDK adicional. Un único pedido a la vez y lecturas solamente bajo demanda del menú.
// Sincroniza los logros con el ranking (rw_sync_comets) y el resto del progreso del jugador (rw_sync_progress,
// ProgresoNube, US 162 y US 163). El guardado en la PC lo hace Guardado.
public sealed class LogrosOnline : MonoBehaviour
{
    [Serializable] public sealed class Jugador
    {
        public string player_id, display_name;
        public int rank, total, blue, green, gold;
        public string[] achievements = Array.Empty<string>();
        public bool Tiene(string id) => achievements != null && Array.IndexOf(achievements, id) >= 0;
    }
    [Serializable] public sealed class Porcentaje { public string achievement_id; public int owners; public float percent; }
    [Serializable] public sealed class Resumen
    {
        public Jugador[] players = Array.Empty<Jugador>();
        public Porcentaje[] stats = Array.Empty<Porcentaje>();
        public Jugador me;
        public int population;
        public bool has_more;
        public int offset;
        public string filter, fetched_at;
    }
    [Serializable] private sealed class Usuario { public string id; }
    [Serializable] private sealed class Sesion
    { public string access_token, refresh_token; public long expires_at; public int expires_in; public Usuario user; }
    [Serializable] private sealed class Renovar { public string refresh_token; }
    [Serializable] private sealed class Sincronizar { public string p_name; public int p_blue, p_green, p_gold; }
    [Serializable] private sealed class Consulta { public int p_offset; public string p_achievement; }

    public static LogrosOnline Instancia { get; private set; }
    public Resumen Datos { get; private set; }
    public string Estado { get; private set; } = "Ranking online sin configurar. El progreso se guarda en esta PC.";
    public bool Ocupado { get; private set; }
    public int Revision { get; private set; }
    public bool Configurado => config != null && config.Valido;
    private LogrosOnlineConfig config;
    private Sesion sesion;
    private bool pedir, identidadDanada;
    // Resto del progreso (ProgresoNube): si la base todavía no tiene rw_sync_progress (falta correr el logros.sql
    // actualizado), se sigue solo con los logros.
    private bool progresoDisponible = true, progresoCambio = true;
    private string progresoEnviado;
    private int revisionProgreso = -1;
    private int offset, enviado = -1, fallos;
    private string filtro = "", nombreEnviado, respuesta, error;
    private long codigo;
    private float intentar;
    private string RutaSesion => Path.Combine(Application.persistentDataPath, "logros-sesion-" + new Uri(config.url).Host + ".json");
    private string RutaCache => Path.Combine(Application.persistentDataPath, "logros-ranking-" + new Uri(config.url).Host + ".json");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => Instancia = null;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Arrancar()
    { if (Instancia == null) new GameObject("Logros online").AddComponent<LogrosOnline>(); }
    private void Awake()
    {
        if (Instancia != null) { Destroy(gameObject); return; }
        Instancia = this; DontDestroyOnLoad(gameObject);
        config = Resources.Load<LogrosOnlineConfig>("LogrosOnline");
        LogrosCometas.Desbloqueado += Avisar;
        if (!Configurado) return;
        Estado = "Pendiente de conexión.";
        if (File.Exists(RutaSesion))
        {
            try
            {
                sesion = JsonUtility.FromJson<Sesion>(File.ReadAllText(RutaSesion));
                identidadDanada = sesion == null || string.IsNullOrEmpty(sesion.refresh_token);
            }
            catch { identidadDanada = true; }
        }
        try { if (File.Exists(RutaCache)) Datos = JsonUtility.FromJson<Resumen>(File.ReadAllText(RutaCache)); } catch { }
    }
    private void Update()
    {
        if (!Configurado || Ocupado || Time.unscaledTime < intentar || !PlayerProfile.HasName) return;
        // El documento del progreso se rearma solo cuando cambia algo guardado.
        if (progresoDisponible && revisionProgreso != Guardado.Revision)
        {
            revisionProgreso = Guardado.Revision;
            progresoCambio = ProgresoNube.Json() != progresoEnviado;
        }
        if (pedir || enviado != LogrosCometas.Revision || nombreEnviado != PlayerProfile.Name || (progresoDisponible && progresoCambio))
            StartCoroutine(Trabajar());
    }
    public void Solicitar(int pagina = 0, string logro = "")
    {
        offset = Mathf.Max(0, pagina); filtro = logro ?? ""; pedir = true;
        // Los errores respetan el backoff aunque se pulse repetidamente.
        if (fallos == 0) intentar = Time.unscaledTime;
    }
    private IEnumerator Trabajar()
    {
        Ocupado = true; Estado = "Sincronizando…"; Revision++;
        try
        {
            if (identidadDanada) { Fallar("No se pudo recuperar la identidad online. El progreso local está conservado."); yield break; }
            // Supabase cambia el token de renovación cada vez que se usa y, si alguien reusa uno viejo, anula la sesión.
            // Con dos juegos abiertos en esta PC (el editor y un build comparten la carpeta) el otro pudo renovarla:
            // antes de renovar se mira el archivo. Si la sesión quedó anulada, se crea otra identidad anónima; la
            // anterior queda en el ranking con lo que tenía.
            string anulado = null, aviso = null;
            for (int intento = 0; intento < 4 && Vencida(); intento++)
            {
                Sesion enArchivo = LeerSesion();
                if (enArchivo != null && enArchivo.refresh_token != anulado && (sesion == null || enArchivo.refresh_token != sesion.refresh_token))
                { sesion = enArchivo; continue; }
                string usado = sesion?.refresh_token;
                yield return Post(usado == null ? "/auth/v1/signup" : "/auth/v1/token?grant_type=refresh_token",
                    usado == null ? "{}" : JsonUtility.ToJson(new Renovar { refresh_token = usado }), false);
                if (error != null)
                {
                    if (usado != null && (codigo == 400 || codigo == 401))
                    { anulado = usado; sesion = null; aviso = " · la sesión anterior se había anulado: se creó una nueva"; continue; }
                    Fallar(codigo == 400 || codigo == 401 ? "No se pudo autenticar. Revisá el acceso anónimo del proyecto." : error);
                    yield break;
                }
                Sesion nueva = null;
                try { nueva = JsonUtility.FromJson<Sesion>(respuesta); } catch { }
                if (nueva == null || string.IsNullOrEmpty(nueva.access_token) || string.IsNullOrEmpty(nueva.refresh_token))
                { Fallar("Respuesta de autenticación inválida."); yield break; }
                nueva.expires_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Math.Max(60, nueva.expires_in);
                sesion = nueva;
                try { Guardado.EscribirAtomico(RutaSesion, JsonUtility.ToJson(sesion)); }
                catch { identidadDanada = true; Fallar("No se pudo guardar la identidad online. Revisá los permisos de la carpeta de progreso."); yield break; }
            }
            if (Vencida()) { Fallar("No se pudo autenticar."); yield break; }
            int revision = LogrosCometas.Revision;
            string nombre = PlayerProfile.Name;
            var p = LogrosCometas.Datos;
            yield return Post("/rest/v1/rpc/rw_sync_comets", JsonUtility.ToJson(new Sincronizar {
                p_name = nombre, p_blue = p.aciertos[0], p_green = p.aciertos[1], p_gold = p.aciertos[2] }), true);
            if (error != null) { Fallar(error); yield break; }
            Jugador propio = null;
            try { propio = JsonUtility.FromJson<Jugador>(respuesta); } catch { }
            if (propio == null || string.IsNullOrEmpty(propio.player_id)) { Fallar("Respuesta de progreso inválida."); yield break; }
            LogrosCometas.Fusionar(propio.blue, propio.green, propio.gold);
            enviado = revision; nombreEnviado = nombre;

            // US 162 (CA3) y US 163 (CA2): el resto del progreso. La base junta (máximo, mínimo, unión) y devuelve lo
            // que tiene, que se junta con lo de la PC.
            if (progresoDisponible && progresoCambio)
            {
                string documento = ProgresoNube.Json();
                yield return Post("/rest/v1/rpc/rw_sync_progress", "{\"p\":" + documento + "}", true);
                if (error != null)
                {
                    if (codigo != 404) { Fallar(error); yield break; }
                    progresoDisponible = false; // falta actualizar la base: se sigue solo con los logros
                }
                else
                {
                    ProgresoNube.Documento nube = null;
                    try { nube = JsonUtility.FromJson<ProgresoNube.Documento>(respuesta); } catch { }
                    if (nube == null) { Fallar("Respuesta de progreso inválida."); yield break; }
                    ProgresoNube.Fusionar(nube);
                    progresoEnviado = ProgresoNube.Json();
                    revisionProgreso = Guardado.Revision;
                    progresoCambio = false;
                }
            }
            if (pedir)
            {
                int pagina = offset; string logro = filtro; pedir = false;
                yield return Post("/rest/v1/rpc/rw_achievement_board", JsonUtility.ToJson(new Consulta { p_offset = pagina, p_achievement = logro }), true);
                if (error != null) { Fallar(error); yield break; }
                Resumen nuevo = null;
                try { nuevo = JsonUtility.FromJson<Resumen>(respuesta); } catch { }
                if (nuevo == null || nuevo.players == null || nuevo.stats == null) { Fallar("Respuesta del ranking inválida."); yield break; }
                nuevo.offset = pagina; nuevo.filter = logro; nuevo.fetched_at = DateTime.UtcNow.ToString("O");
                Datos = nuevo;
                try { Guardado.EscribirAtomico(RutaCache, JsonUtility.ToJson(nuevo)); } catch { }
            }
            fallos = 0; intentar = Time.unscaledTime + 15f;
            if (!progresoDisponible) aviso += " · falta actualizar la base para guardar el resto del progreso en la nube";
            Estado = "Conectado · actualizado " + DateTime.Now.ToString("HH:mm") + aviso; Revision++;
        }
        finally { Ocupado = false; }
    }
    private bool Vencida() => sesion == null || sesion.expires_at <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60;

    // La sesión como está en el archivo (la puede haber renovado otro juego abierto en esta PC).
    private Sesion LeerSesion()
    {
        try
        {
            if (!File.Exists(RutaSesion)) return null;
            Sesion s = JsonUtility.FromJson<Sesion>(File.ReadAllText(RutaSesion));
            return s != null && !string.IsNullOrEmpty(s.refresh_token) ? s : null;
        }
        catch { return null; }
    }

    private IEnumerator Post(string ruta, string json, bool autenticado)
    {
        respuesta = error = null; codigo = 0;
        using (var req = new UnityWebRequest(config.url.TrimEnd('/') + ruta, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer(); req.timeout = 15;
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("apikey", config.clavePublica.Trim());
            if (autenticado) req.SetRequestHeader("Authorization", "Bearer " + sesion.access_token);
            yield return req.SendWebRequest(); codigo = req.responseCode;
            if (req.result != UnityWebRequest.Result.Success)
            {
                if (autenticado && codigo == 401) sesion.expires_at = 0;
                string cuerpo = req.downloadHandler != null ? req.downloadHandler.text : null;
                error = codigo == 0 ? "Sin conexión. Se conserva el progreso local y el último ranking disponible."
                    : cuerpo != null && cuerpo.Contains("anonymous_provider_disabled")
                        ? "El proyecto de Supabase no permite jugadores anónimos: hay que activar Anonymous Sign-Ins (ver Backend/Supabase/README.md)."
                    : cuerpo != null && cuerpo.Contains("Invalid name")
                        ? "El ranking online no aceptó tu nombre. Probá con otro (letras, números, espacio, - o _)."
                        : "Servicio no disponible (HTTP " + codigo + "). Se reintentará sin duplicar progreso.";
            }
            else respuesta = req.downloadHandler.text;
        }
    }
    private void Fallar(string mensaje)
    { Estado = mensaje; fallos++; intentar = Time.unscaledTime + Mathf.Min(120, 10 * Mathf.Pow(2, Mathf.Min(fallos - 1, 4))); Revision++; }
    private void Avisar(LogrosCometas.Definicion logro)
    {
        var menu = FindAnyObjectByType<MenuRiftwalker>();
        if (menu != null) menu.Avisar("Logro desbloqueado: " + logro.nombre);
        else MatchHud.Warn("Logro desbloqueado: " + logro.nombre);
        SonidoInterfaz.Logro();
    }

    private void OnDestroy()
    { if (Instancia == this) { LogrosCometas.Desbloqueado -= Avisar; Instancia = null; } }
}
