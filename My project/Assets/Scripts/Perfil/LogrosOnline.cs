using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// REST de Supabase sin SDK adicional. Un único pedido a la vez y lecturas solamente bajo demanda del menú.
// Sincroniza los logros con el ranking (rw_sync_comets) y el resto del progreso del jugador (rw_sync_progress,
// ProgresoNube, US 162 y US 163). El guardado en la PC lo hace Guardado. El ranking (US 226) es por logros
// (rw_achievement_board) o por cualquier otra estadística (rw_stat_board, CategoriasRanking). También manda las
// opiniones de "Danos tu opinión" que esperan en Opiniones (rw_send_feedback, que avisa a Discord). La cuenta del jugador
// (US 237), con la misma sesión, está en LogrosOnline.Cuenta.cs; las monedas de la cuenta (US 272) y lo que se sube
// antes de cerrar el juego, en LogrosOnline.Billetera.cs.
public sealed partial class LogrosOnline : MonoBehaviour
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
    // Ranking por estadística (US 226): el puesto y el valor de cada jugador en la categoría pedida.
    [Serializable] public sealed class Fila { public string player_id, display_name; public double value; public int rank; }
    [Serializable] public sealed class Tabla
    {
        public string stat, fetched_at;
        public Fila[] players = Array.Empty<Fila>();
        public Fila me;
        public int population, offset;
        public bool has_more;
        /// <summary>El jugador local, si aparece en esta categoría (JsonUtility no deja el null: se mira el id).</summary>
        public Fila Propia => me != null && !string.IsNullOrEmpty(me.player_id) ? me : null;
    }
    // El usuario de la sesión: su mail, el nombre de la cuenta (user_metadata) y, al crearla, sus identidades (US 237).
    [Serializable] private sealed class Usuario { public string id, email, created_at; public bool is_anonymous; public Meta user_metadata; public Identidad[] identities; }
    [Serializable] private sealed class Sesion
    { public string access_token, refresh_token; public long expires_at; public int expires_in; public Usuario user; }
    [Serializable] private sealed class Renovar { public string refresh_token; }
    [Serializable] private sealed class Sincronizar { public string p_name; public int p_blue, p_green, p_gold; }
    [Serializable] private sealed class Consulta { public int p_offset; public string p_achievement; }
    [Serializable] private sealed class ConsultaEstadistica { public string p_stat; public int p_offset; }
    [Serializable] private sealed class OpinionRpc { public string p_id, p_kind, p_message, p_version, p_name; public int p_rating; }

    public static LogrosOnline Instancia { get; private set; }
    public Resumen Datos { get; private set; }
    /// <summary>El último ranking por estadística que llegó (o el de la caché, sin internet).</summary>
    public Tabla Estadistica { get; private set; }
    /// <summary>false si la base todavía no tiene rw_stat_board (falta volver a correr logros.sql).</summary>
    public bool RankingDisponible { get; private set; } = true;
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
    // Opiniones: si la base todavía no tiene rw_send_feedback (falta correr opiniones.sql), quedan guardadas en la PC.
    private bool opinionesDisponible = true;
    private string opinionPorAvisar; // la recién enviada desde la ventana: se avisa si llegó o si quedó pendiente
    private int offset, enviado = -1, fallos;
    private string filtro = "", categoria = "", nombreEnviado, respuesta, error, cuerpoError;
    private long codigo;
    private float intentar;
    // Avisos de logros en fila: si una partida o un cometa desbloquea varios a la vez, cada aviso tapaba al anterior
    // y solo se veía el último. Ahora salen de a uno, cada uno con su sonido.
    private const float SegundosPorAviso = 2.6f;
    private readonly Queue<string> avisosPendientes = new Queue<string>();
    private float proximoAviso;
    // Dos sesiones (US 237 y US 281): la de la cuenta abierta o, sin cuenta, la anónima (solo para opiniones y para ver el
    // ranking). El editor y los builds las comparten.
    private string RutaSesion => enCuenta ? RutaSesionCuenta : RutaSesionAnonima;
    private string RutaSesionAnonima => Path.Combine(Application.persistentDataPath, "logros-sesion-" + new Uri(config.url).Host + ".json");
    private string RutaSesionCuenta => Path.Combine(Application.persistentDataPath, "cuenta-sesion-" + new Uri(config.url).Host + ".json");
    private string RutaCache => Path.Combine(Application.persistentDataPath, "logros-ranking-" + new Uri(config.url).Host + ".json");
    private string RutaCacheEstadistica => Path.Combine(Application.persistentDataPath, "logros-estadistica-" + new Uri(config.url).Host + ".json");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => Instancia = null;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Arrancar()
    {
        // Con los tests automáticos no se conecta: no suben progreso ni opiniones de prueba (ModoPruebas).
        if (Instancia == null && !ModoPruebas.Activo) new GameObject("Logros online").AddComponent<LogrosOnline>();
    }
    private void Awake()
    {
        if (Instancia != null) { Destroy(gameObject); return; }
        Instancia = this; DontDestroyOnLoad(gameObject);
        config = Resources.Load<LogrosOnlineConfig>("LogrosOnline");
        LogrosCometas.Desbloqueado += Avisar;
        PlayerProfile.NameChanged += NombreCambiado;
        Application.wantsToQuit += AlCerrar;
        if (!Configurado) return;
        Estado = "Pendiente de conexión.";
        AbrirCuentaGuardada();
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
        try { if (File.Exists(RutaCacheEstadistica)) Estadistica = JsonUtility.FromJson<Tabla>(File.ReadAllText(RutaCacheEstadistica)); } catch { }
    }
    private void Update()
    {
        MostrarAvisoPendiente();
        // Sin sesión, un build publicado espera que se entre en el launcher: no se conecta (US 282).
        if (!Configurado || SinSesion || Ocupado || Time.unscaledTime < intentar || !PlayerProfile.HasName) return;
        // El documento del progreso se rearma solo cuando cambia algo guardado.
        if (progresoDisponible && revisionProgreso != Guardado.Revision)
        {
            revisionProgreso = Guardado.Revision;
            progresoCambio = ProgresoNube.Json() != progresoEnviado;
        }
        // Sin cuenta no se sube nada (US 281): la sesión anónima solo manda opiniones y pide el ranking.
        bool subir = enCuenta && (enviado != LogrosCometas.Revision || nombreEnviado != PlayerProfile.Name || (progresoDisponible && progresoCambio)
            || BilleteraPendiente || NombreDeCuentaPendiente);
        if (pedir || subir || (opinionesDisponible && Opiniones.Pendientes > 0))
            StartCoroutine(Trabajar());
    }
    /// <summary>Manda enseguida la opinión que acaba de guardar la ventana y avisa en el menú cómo le fue.</summary>
    public void EnviarOpinion(string id)
    {
        opinionPorAvisar = id; opinionesDisponible = true;
        intentar = Time.unscaledTime;
    }
    /// <summary>Pide una página del ranking: por logros (estadistica vacía) o por una categoría de CategoriasRanking.</summary>
    public void Solicitar(int pagina = 0, string logro = "", string estadistica = "")
    {
        offset = Mathf.Max(0, pagina); filtro = logro ?? ""; categoria = estadistica ?? ""; pedir = true;
        // Los errores respetan el backoff aunque se pulse repetidamente.
        if (fallos == 0) intentar = Time.unscaledTime;
    }
    private IEnumerator Trabajar()
    {
        Ocupado = true; Estado = "Sincronizando…"; Revision++;
        try
        {
            yield return AsegurarSesion();
            if (falloSesion != null) { Fallar(falloSesion); yield break; }
            string aviso = avisoSesion;

            // Opiniones: una por vuelta y antes que lo demás, así llegan aunque falle otra cosa. Cada una lleva su id:
            // si se reenvía (por un corte), la base no la duplica ni vuelve a avisar a Discord.
            Opiniones.Opinion op = opinionesDisponible ? Opiniones.Primera : null;
            if (op != null)
            {
                yield return Post("/rest/v1/rpc/rw_send_feedback", JsonUtility.ToJson(new OpinionRpc {
                    p_id = op.id, p_kind = op.tipo, p_rating = op.valoracion, p_message = op.texto, p_version = op.version,
                    p_name = PlayerProfile.Name }), true);
                bool rechazada = cuerpoError != null && cuerpoError.Contains("Invalid feedback");
                bool muchas = cuerpoError != null && cuerpoError.Contains("Too many feedback");
                if (error == null || rechazada)
                {
                    Opiniones.Quitar(op.id);
                    if (op.id == opinionPorAvisar)
                        AvisarEnMenu(rechazada ? "No se pudo enviar tu opinión." : "¡Gracias! Tu opinión le llegó al equipo.");
                }
                else if (codigo == 404)
                {
                    opinionesDisponible = false; // falta correr opiniones.sql: queda en la PC
                    if (op.id == opinionPorAvisar)
                        AvisarEnMenu("Tu opinión quedó en la PC: falta actualizar el servicio online.");
                    opinionPorAvisar = null;
                }
                else if (muchas)
                {
                    // Mandó varias seguidas: la base pide esperar. Queda en la cola y sale en la próxima vuelta.
                    if (op.id == opinionPorAvisar) AvisarEnMenu("¡Gracias! Tu opinión se envía en un momento.");
                }
                else { Fallar(error); yield break; }
                if (op.id == opinionPorAvisar && (error == null || rechazada || muchas)) opinionPorAvisar = null;
            }

            // Sin cuenta no se sube nada (US 281): la sesión anónima solo manda opiniones y pide el ranking.
            if (enCuenta)
            {
                if (NombreDeCuentaPendiente) yield return SubirNombreDeCuenta();
                yield return SubirProgreso();
                if (falloSubida != null) { Fallar(falloSubida); yield break; }
                if (BilleteraPendiente)
                {
                    yield return SubirBilletera(null);
                    if (falloBilletera != null) { Fallar(falloBilletera); yield break; }
                }
            }
            if (pedir && categoria != "")
            {
                int pagina = offset; string stat = categoria; pedir = false;
                yield return Post("/rest/v1/rpc/rw_stat_board", JsonUtility.ToJson(new ConsultaEstadistica { p_stat = stat, p_offset = pagina }), true);
                if (error != null)
                {
                    if (codigo != 404) { Fallar(error); yield break; }
                    RankingDisponible = false; // falta actualizar la base: los logros y el progreso siguen igual
                }
                else
                {
                    Tabla nueva = null;
                    try { nueva = JsonUtility.FromJson<Tabla>(respuesta); } catch { }
                    if (nueva == null || nueva.players == null) { Fallar("Respuesta del ranking inválida."); yield break; }
                    nueva.stat = stat; nueva.offset = pagina; nueva.fetched_at = DateTime.UtcNow.ToString("O");
                    Estadistica = nueva; RankingDisponible = true;
                    try { Guardado.EscribirAtomico(RutaCacheEstadistica, JsonUtility.ToJson(nueva)); } catch { }
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
            fallos = 0; intentar = Time.unscaledTime + 2f; // lo nuevo sale enseguida (antes esperaba 15 s y se perdía si se cerraba el juego)
            if (!progresoDisponible) aviso += " · falta actualizar la base para guardar el resto del progreso en la nube";
            else if (!RankingDisponible) aviso += " · falta actualizar la base para el ranking por estadísticas";
            Estado = "Conectado · actualizado " + DateTime.Now.ToString("HH:mm") + aviso; Revision++;
        }
        finally { Ocupado = false; }
    }
    // Logros (rw_sync_comets) y el resto del progreso (rw_sync_progress) de la cuenta. Si falla, deja el motivo en falloSubida.
    private string falloSubida;
    private IEnumerator SubirProgreso()
    {
        falloSubida = null;
        int revision = LogrosCometas.Revision;
        string nombre = PlayerProfile.Name;
        var p = LogrosCometas.Datos;
        yield return Post("/rest/v1/rpc/rw_sync_comets", JsonUtility.ToJson(new Sincronizar {
            p_name = nombre, p_blue = p.aciertos[0], p_green = p.aciertos[1], p_gold = p.aciertos[2] }), true);
        if (error != null) { falloSubida = error; yield break; }
        Jugador propio = null;
        try { propio = JsonUtility.FromJson<Jugador>(respuesta); } catch { }
        if (propio == null || string.IsNullOrEmpty(propio.player_id)) { falloSubida = "Respuesta de progreso inválida."; yield break; }
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
                if (codigo != 404) { falloSubida = error; yield break; }
                progresoDisponible = false; // falta actualizar la base: se sigue solo con los logros
            }
            else
            {
                ProgresoNube.Documento nube = null;
                try { nube = JsonUtility.FromJson<ProgresoNube.Documento>(respuesta); } catch { }
                if (nube == null) { falloSubida = "Respuesta de progreso inválida."; yield break; }
                ProgresoNube.Fusionar(nube);
                progresoEnviado = ProgresoNube.Json();
                revisionProgreso = Guardado.Revision;
                progresoCambio = false;
            }
        }
    }

    private bool Vencida() => sesion == null || sesion.expires_at <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60;

    // La sesión vigente, para la sincronización (Trabajar) y para la cuenta (US 237). Si no pudo, deja el motivo en
    // falloSesion; avisoSesion cuenta si hubo que crear otra identidad.
    private string falloSesion, avisoSesion;
    private IEnumerator AsegurarSesion()
    {
        falloSesion = avisoSesion = null;
        if (identidadDanada) { falloSesion = "No se pudo recuperar la identidad online. El progreso local está conservado."; yield break; }
        // Supabase cambia el token de renovación cada vez que se usa y, si alguien reusa uno viejo, anula la sesión.
        // Con dos juegos abiertos en esta PC (el editor y un build comparten la carpeta) el otro pudo renovarla:
        // antes de renovar se mira el archivo. Si la sesión quedó anulada, se crea otra identidad anónima; la
        // anterior queda en la base con sus opiniones.
        string anulado = null;
        for (int intento = 0; intento < 4 && Vencida(); intento++)
        {
            Sesion enArchivo = LeerSesion();
            if (enArchivo != null && enArchivo.refresh_token != anulado && (sesion == null || enArchivo.refresh_token != sesion.refresh_token))
            { sesion = enArchivo; continue; }
            // La sesión de una cuenta no se reemplaza por una anónima: si se perdió, se sale de la cuenta (US 237).
            if (enCuenta && sesion == null) { SalirDeLaCuentaLocal(); falloSesion = ReglasCuenta.SesionVencida; yield break; }
            string usado = sesion?.refresh_token;
            yield return Post(usado == null ? "/auth/v1/signup" : "/auth/v1/token?grant_type=refresh_token",
                usado == null ? "{}" : JsonUtility.ToJson(new Renovar { refresh_token = usado }), false);
            if (error != null)
            {
                if (enCuenta && (codigo == 400 || codigo == 401)) { SalirDeLaCuentaLocal(); falloSesion = ReglasCuenta.SesionVencida; yield break; }
                if (usado != null && (codigo == 400 || codigo == 401))
                { anulado = usado; sesion = null; avisoSesion = " · la sesión anterior se había anulado: se creó una nueva"; continue; }
                falloSesion = codigo == 400 || codigo == 401 ? "No se pudo autenticar. Revisá el acceso anónimo del proyecto." : error;
                yield break;
            }
            Sesion nueva = null;
            try { nueva = JsonUtility.FromJson<Sesion>(respuesta); } catch { }
            if (nueva == null || string.IsNullOrEmpty(nueva.access_token) || string.IsNullOrEmpty(nueva.refresh_token))
            { falloSesion = "Respuesta de autenticación inválida."; yield break; }
            nueva.expires_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Math.Max(60, nueva.expires_in);
            sesion = nueva;
            if (!GuardarSesion())
            { identidadDanada = true; falloSesion = "No se pudo guardar la identidad online. Revisá los permisos de la carpeta de progreso."; yield break; }
        }
        if (Vencida()) falloSesion = "No se pudo autenticar.";
    }

    private bool GuardarSesion()
    {
        try { Guardado.EscribirAtomico(RutaSesion, JsonUtility.ToJson(sesion)); return true; }
        catch { return false; }
    }

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

    private IEnumerator Post(string ruta, string json, bool autenticado) => Pedir("POST", ruta, json, autenticado);

    private IEnumerator Pedir(string metodo, string ruta, string json, bool autenticado)
    {
        respuesta = error = cuerpoError = null; codigo = 0;
        using (var req = new UnityWebRequest(config.url.TrimEnd('/') + ruta, metodo))
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
                string cuerpo = cuerpoError = req.downloadHandler != null ? req.downloadHandler.text : null;
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
    {
        Estado = mensaje; fallos++; intentar = Time.unscaledTime + Mathf.Min(120, 10 * Mathf.Pow(2, Mathf.Min(fallos - 1, 4))); Revision++;
        // La opinión recién enviada no salió (sin conexión, por ejemplo): queda en la PC y se manda después.
        if (opinionPorAvisar != null && opinionesDisponible)
        {
            opinionPorAvisar = null;
            AvisarEnMenu(codigo == 0 ? "Sin conexión: tu opinión se envía cuando vuelva." : "No se pudo enviar ahora: tu opinión se manda más tarde.");
        }
    }
    private static void AvisarEnMenu(string mensaje)
    {
        var menu = FindAnyObjectByType<MenuRiftwalker>();
        if (menu != null) menu.Avisar(mensaje);
    }
    private void Avisar(LogrosCometas.Definicion logro)
    {
        avisosPendientes.Enqueue("Logro desbloqueado: " + logro.nombre);
        MostrarAvisoPendiente();
    }
    private void MostrarAvisoPendiente()
    {
        if (avisosPendientes.Count == 0 || Time.unscaledTime < proximoAviso) return;
        string mensaje = avisosPendientes.Dequeue();
        var menu = FindAnyObjectByType<MenuRiftwalker>();
        if (menu != null) menu.Avisar(mensaje);
        else MatchHud.Warn(mensaje, SegundosPorAviso);
        SonidoInterfaz.Logro();
        proximoAviso = Time.unscaledTime + SegundosPorAviso;
    }

    private void OnDestroy()
    {
        if (Instancia != this) return;
        LogrosCometas.Desbloqueado -= Avisar; PlayerProfile.NameChanged -= NombreCambiado; Application.wantsToQuit -= AlCerrar;
        Instancia = null;
    }
}
