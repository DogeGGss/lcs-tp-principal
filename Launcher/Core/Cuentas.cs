using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Riftwalker.Launcher;

// The player's account in the launcher (F18: US 237, US 269, US 270 and US 282). The launcher works like a game client:
// the player logs in here and the game opens with that session. Supabase Auth over REST, like the game (LogrosOnline):
// - Create (US 237): POST /auth/v1/signup; Supabase mails a 6-digit code (template «Confirm signup» with {{ .Token }})
//   and POST /auth/v1/verify confirms it and returns the session.
// - Log in (US 269): POST /auth/v1/token?grant_type=password.
// - Forgot the password (US 271): POST /auth/v1/recover mails a code (template «Reset Password» with {{ .Token }}),
//   POST /auth/v1/verify with type recovery logs in and PUT /auth/v1/user sets the new password.
// - Log out (US 270): POST /auth/v1/logout, and the session file is deleted.
// - Delete the account (US 273): POST /functions/v1/borrar-cuenta with the password (Backend/Supabase/functions).
// The session (never the password) is saved in the game's save folder, in the format of LogrosOnline.Sesion: the game
// reads it when it opens (US 282). Supabase rotates the refresh token on every use, so the file is read again before
// renewing, in case the game already did it.
public sealed class Cuentas(HttpClient http, ServicioCuentas servicio, string carpetaJuego)
{
    public static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public SesionCuenta? Sesion { get; private set; }
    public string RutaSesion => Path.Combine(carpetaJuego, $"cuenta-sesion-{servicio.Host}.json");

    /// <summary>The game's save folder (Application.persistentDataPath): AppData/LocalLow/Equipo 4/Project Riftwalker.</summary>
    public static string CarpetaDelJuego()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(Path.GetDirectoryName(local) ?? local, "LocalLow", "Equipo 4", "Project Riftwalker");
    }

    /// <summary>The session saved on the PC (the launcher's, or the one the game renewed), or null.</summary>
    public SesionCuenta? LeerSesion()
    {
        try
        {
            if (!File.Exists(RutaSesion)) return null;
            var s = JsonSerializer.Deserialize<SesionCuenta>(File.ReadAllText(RutaSesion), Json);
            return s is { RefreshToken.Length: > 0, User.Id.Length: > 0 } ? s : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException) { return null; }
    }

    public void Cargar() => Sesion = LeerSesion();

    /// <summary>The design preview (--preview --cuenta): a made-up session, nothing saved.</summary>
    public void UsarVistaPrevia(SesionCuenta sesion) => Sesion = sesion;

    /// <summary>US 237: creates the account and mails the code. null if it worked (Sesion is set when the project does not
    /// ask to confirm the mail); otherwise, the message for the player.</summary>
    public async Task<string?> CrearAsync(string mail, string clave, CancellationToken token)
    {
        mail = mail.Trim();
        string? problema = ReglasCuenta.ValidarMail(mail) ?? ReglasCuenta.ValidarClave(clave, clave);
        if (problema != null) return problema;
        var r = await PedirAsync(HttpMethod.Post, "/auth/v1/signup", new Credenciales(mail, clave), null, token);
        if (!r.Bien) return ReglasCuenta.Mensaje(r.Codigo, r.Cuerpo);
        // Without mail confirmation in the project, Supabase returns the session: straight in.
        if (Leer<SesionCuenta>(r.Cuerpo) is { AccessToken.Length: > 0, RefreshToken.Length: > 0 } directa) { Guardar(directa); return null; }
        // A mail that already has an account is not an error (so as not to reveal which mails exist): the user comes back
        // without identities.
        var u = Leer<UsuarioCuenta>(r.Cuerpo);
        if (u == null || u.Id.Length == 0) return ReglasCuenta.Generico;
        return u.Identities is { Length: 0 } ? ReglasCuenta.MailUsado : null;
    }

    /// <summary>US 237, CA2: confirms the mail with the code and logs in.</summary>
    public async Task<string?> ConfirmarAsync(string mail, string codigo, CancellationToken token)
    {
        string numeros = ReglasCuenta.SoloNumeros(codigo);
        if (numeros.Length != ReglasCuenta.LargoCodigo) return ReglasCuenta.CodigoIncompleto;
        var r = await PedirAsync(HttpMethod.Post, "/auth/v1/verify", new Verificacion("signup", mail.Trim(), numeros), null, token);
        if (!r.Bien && r.Codigo != 0) // newer Supabase projects confirm the sign-up with the type «email»
            r = await PedirAsync(HttpMethod.Post, "/auth/v1/verify", new Verificacion("email", mail.Trim(), numeros), null, token);
        return Entrar(r);
    }

    /// <summary>«¿No te llegó?»: mails another code.</summary>
    public async Task<string?> ReenviarAsync(string mail, CancellationToken token)
    {
        var r = await PedirAsync(HttpMethod.Post, "/auth/v1/resend", new Reenvio("signup", mail.Trim()), null, token);
        return r.Bien ? null : ReglasCuenta.Mensaje(r.Codigo, r.Cuerpo);
    }

    /// <summary>US 269: logs in to an existing account. With the mail not confirmed yet it mails another code and returns
    /// ReglasCuenta.SinConfirmar, to ask for it.</summary>
    public async Task<string?> EntrarAsync(string mail, string clave, CancellationToken token)
    {
        mail = mail.Trim();
        string? problema = ReglasCuenta.ValidarMail(mail);
        if (problema != null) return problema;
        if (string.IsNullOrEmpty(clave)) return ReglasCuenta.NoCoinciden;
        var r = await PedirAsync(HttpMethod.Post, "/auth/v1/token?grant_type=password", new Credenciales(mail, clave), null, token);
        if (!r.Bien && ReglasCuenta.Mensaje(r.Codigo, r.Cuerpo) == ReglasCuenta.SinConfirmar)
        {
            await PedirAsync(HttpMethod.Post, "/auth/v1/resend", new Reenvio("signup", mail), null, token);
            return ReglasCuenta.SinConfirmar;
        }
        return Entrar(r);
    }

    /// <summary>US 271, CA1: mails a code to choose a new password. Supabase answers the same whether the mail has an
    /// account or not (so it does not reveal which mails exist), and so does this: null unless it could not ask.</summary>
    public async Task<string?> RecuperarAsync(string mail, CancellationToken token)
    {
        mail = mail.Trim();
        string? problema = ReglasCuenta.ValidarMail(mail);
        if (problema != null) return problema;
        var r = await PedirAsync(HttpMethod.Post, "/auth/v1/recover", new Recuperacion(mail), null, token);
        return r.Bien ? null : ReglasCuenta.Mensaje(r.Codigo, r.Cuerpo);
    }

    /// <summary>
    /// US 271, CA2 and CA3: the code from the mail logs in (POST /auth/v1/verify, type recovery) and then the new
    /// password is set (PUT /auth/v1/user). null if it worked and the session is saved; otherwise, the message.
    /// </summary>
    public async Task<string?> CambiarClaveAsync(string mail, string codigo, string nueva, CancellationToken token)
    {
        string numeros = ReglasCuenta.SoloNumeros(codigo);
        if (numeros.Length != ReglasCuenta.LargoCodigo) return ReglasCuenta.CodigoIncompleto;
        string? problema = ReglasCuenta.ValidarClave(nueva, nueva);
        if (problema != null) return problema;
        var r = await PedirAsync(HttpMethod.Post, "/auth/v1/verify", new Verificacion("recovery", mail.Trim(), numeros), null, token);
        if (!r.Bien) return ReglasCuenta.Mensaje(r.Codigo, r.Cuerpo);
        var sesion = Leer<SesionCuenta>(r.Cuerpo);
        if (sesion is not { AccessToken.Length: > 0, RefreshToken.Length: > 0, User.Id.Length: > 0 }) return ReglasCuenta.Generico;
        var cambio = await PedirAsync(HttpMethod.Put, "/auth/v1/user", new ClaveNueva(nueva), sesion.AccessToken, token);
        // The same password as before is not an error for the player: the account is theirs and they are in.
        if (!cambio.Bien && !cambio.Cuerpo.Contains("same_password", StringComparison.OrdinalIgnoreCase))
            return ReglasCuenta.Mensaje(cambio.Codigo, cambio.Cuerpo);
        Guardar(sesion);
        return null;
    }

    /// <summary>US 270: tells Supabase (if online) and deletes the session from the PC.</summary>
    public async Task CerrarAsync(CancellationToken token)
    {
        var s = LeerSesion() ?? Sesion;
        if (s != null && !Vencida(s)) await PedirAsync(HttpMethod.Post, "/auth/v1/logout?scope=local", new { }, s.AccessToken, token);
        try { File.Delete(RutaSesion); } catch (IOException) { }
        Sesion = null;
    }

    /// <summary>
    /// US 273, CA2 and CA3: deletes the account with everything it has (Edge Function borrar-cuenta, which checks the
    /// password again and has the service key), then the session and the account's save files leave the PC. null if it
    /// was deleted; otherwise, the message for the player.
    /// </summary>
    public async Task<string?> BorrarAsync(string clave, CancellationToken token)
    {
        if (string.IsNullOrEmpty(clave)) return ReglasCuenta.ClaveIncorrecta;
        if (!await RenovarAsync(token) || Sesion is not { User: { } usuario } s) return ReglasCuenta.SesionVencida;
        var r = await PedirAsync(HttpMethod.Post, "/functions/v1/borrar-cuenta", new Borrado(clave), s.AccessToken, token);
        if (r.Codigo == 0) return ReglasCuenta.SinInternet;
        if (r.Codigo == 401) return r.Cuerpo.Contains("\"clave\"") ? ReglasCuenta.ClaveIncorrecta : ReglasCuenta.SesionVencida;
        if (!r.Bien) return ReglasCuenta.Generico;
        string id = usuario.Id;
        string[] archivos = [RutaSesion, .. new[] { "jugador-", "jugador-editor-" }.SelectMany(p => new[] { $"{p}{id}.json", $"{p}{id}.json.bak" }).Select(a => Path.Combine(carpetaJuego, a))];
        foreach (string archivo in archivos) try { File.Delete(archivo); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        // The game's mark of the last account used on this PC: if it was this one, it goes too.
        string marca = Path.Combine(carpetaJuego, "cuenta-activa.txt");
        try { if (File.Exists(marca) && File.ReadAllText(marca).Trim() == id) File.Delete(marca); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        Sesion = null;
        return null;
    }

    /// <summary>When the launcher opens: renews the session if it expired. false if Supabase rejected it (closed somewhere
    /// else, or the account was deleted): it is deleted and the player logs in again. Offline, the saved one is kept.</summary>
    public async Task<bool> RenovarAsync(CancellationToken token)
    {
        Sesion = LeerSesion() ?? Sesion;
        if (Sesion == null) return false;
        if (!Vencida(Sesion)) return true;
        var r = await PedirAsync(HttpMethod.Post, "/auth/v1/token?grant_type=refresh_token", new Renovacion(Sesion.RefreshToken), null, token);
        if (r.Codigo is 400 or 401)
        {
            // The game may have renewed it meanwhile: if the file changed, the new one counts.
            var enArchivo = LeerSesion();
            if (enArchivo != null && enArchivo.RefreshToken != Sesion.RefreshToken) { Sesion = enArchivo; return true; }
            try { File.Delete(RutaSesion); } catch (IOException) { }
            Sesion = null; return false;
        }
        if (r.Bien && Leer<SesionCuenta>(r.Cuerpo) is { AccessToken.Length: > 0, RefreshToken.Length: > 0 } nueva) Guardar(nueva);
        return true;
    }

    public static bool Vencida(SesionCuenta s) => s.ExpiresAt <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60;

    private string? Entrar(Respuesta r)
    {
        if (!r.Bien) return ReglasCuenta.Mensaje(r.Codigo, r.Cuerpo);
        var nueva = Leer<SesionCuenta>(r.Cuerpo);
        if (nueva is not { AccessToken.Length: > 0, RefreshToken.Length: > 0, User.Id.Length: > 0 }) return ReglasCuenta.Generico;
        Guardar(nueva);
        return null;
    }

    private void Guardar(SesionCuenta nueva)
    {
        nueva.ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Math.Max(60, nueva.ExpiresIn);
        Directory.CreateDirectory(carpetaJuego);
        string temp = RutaSesion + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(nueva, Json));
        File.Move(temp, RutaSesion, true);
        Sesion = nueva;
    }

    private static T? Leer<T>(string json) where T : class
    {
        try { return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<T>(json, Json); }
        catch (JsonException) { return null; }
    }

    private sealed record Respuesta(int Codigo, string Cuerpo) { public bool Bien => Codigo is >= 200 and < 300; }

    // Offline (or 15 s without an answer) the code is 0, like UnityWebRequest in the game.
    private async Task<Respuesta> PedirAsync(HttpMethod metodo, string ruta, object cuerpo, string? acceso, CancellationToken token)
    {
        using var pedido = new HttpRequestMessage(metodo, servicio.Url.TrimEnd('/') + ruta)
        { Content = new StringContent(JsonSerializer.Serialize(cuerpo, Json), Encoding.UTF8, "application/json") };
        pedido.Headers.Add("apikey", servicio.ClavePublica);
        if (acceso != null) pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", acceso);
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(token);
        limite.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            using var respuesta = await http.SendAsync(pedido, limite.Token);
            return new((int)respuesta.StatusCode, await respuesta.Content.ReadAsStringAsync(limite.Token));
        }
        catch (HttpRequestException) { return new(0, ""); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return new(0, ""); }
    }

    private sealed record Credenciales([property: JsonPropertyName("email")] string Mail, [property: JsonPropertyName("password")] string Clave);
    private sealed record Verificacion([property: JsonPropertyName("type")] string Tipo, [property: JsonPropertyName("email")] string Mail, [property: JsonPropertyName("token")] string Codigo);
    private sealed record Reenvio([property: JsonPropertyName("type")] string Tipo, [property: JsonPropertyName("email")] string Mail);
    private sealed record Renovacion([property: JsonPropertyName("refresh_token")] string Token);
    private sealed record Borrado([property: JsonPropertyName("clave")] string Clave);
    private sealed record Recuperacion([property: JsonPropertyName("email")] string Mail);
    private sealed record ClaveNueva([property: JsonPropertyName("password")] string Clave);
}

/// <summary>The game's Supabase project: the same as Assets/Resources/LogrosOnline.asset. The publishable key is public on
/// purpose (it ships in the game); never put the service_role or a secret key here.</summary>
public sealed record ServicioCuentas(string Url, string ClavePublica)
{
    public static readonly ServicioCuentas Juego = new("https://linroadyovelwuaxjjhk.supabase.co", "sb_publishable_Am1iW4MJpTWmHlNQyRwrWQ_CEjDV4d0");
    public string Host => new Uri(Url).Host;
}

// The session with Supabase's names, which are those of LogrosOnline.Sesion in the game (JsonUtility).
public sealed class SesionCuenta
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
    [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; } = "";
    [JsonPropertyName("expires_at")] public long ExpiresAt { get; set; }
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    [JsonPropertyName("user")] public UsuarioCuenta? User { get; set; }
}
public sealed class UsuarioCuenta
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("email")] public string Email { get; set; } = "";
    [JsonPropertyName("created_at")] public string CreatedAt { get; set; } = "";
    [JsonPropertyName("is_anonymous")] public bool IsAnonymous { get; set; }
    [JsonPropertyName("user_metadata")] public MetaCuenta? UserMetadata { get; set; }
    [JsonPropertyName("identities")] public IdentidadCuenta[]? Identities { get; set; }

    /// <summary>The account's player name (the game sets it); without one yet, the part of the mail before the @.</summary>
    [JsonIgnore] public string Nombre => !string.IsNullOrWhiteSpace(UserMetadata?.Nombre) ? UserMetadata!.Nombre! : Email.Split('@')[0];

    /// <summary>When it was created, «dd/MM/yyyy», or "".</summary>
    [JsonIgnore] public string Desde => DateTimeOffset.TryParse(CreatedAt, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d)
        ? d.ToLocalTime().ToString("dd/MM/yyyy") : "";
}
public sealed class MetaCuenta { [JsonPropertyName("nombre")] public string? Nombre { get; set; } }
public sealed class IdentidadCuenta { [JsonPropertyName("id")] public string Id { get; set; } = ""; }
