#nullable disable
using System.Text;
using System.Text.RegularExpressions;

// Reglas de la cuenta (F18: US 237 y US 269): cuándo un mail o una contraseña sirven, en qué estado está la cuenta y
// qué mensaje se le muestra al jugador para cada respuesta de Supabase. Funciones puras, para probarlas sin conexión.
// Las comparten el juego y el launcher (que incluye este archivo). Los textos son los del diseño (guía, #cuenta-launcher).
public static class ReglasCuenta
{
    public enum Estado { SinCuenta, FaltaConfirmar, ConCuenta }

    public const int LargoMinimo = 8;
    public const int LargoCodigo = 6;

    public const string MailInvalido = "Escribí un mail válido.";
    public const string ClaveCorta = "La contraseña tiene que tener 8 caracteres o más.";
    public const string ClavesDistintas = "Las contraseñas no coinciden.";
    public const string MailUsado = "Ese mail ya tiene una cuenta.";
    public const string NoCoinciden = "El mail o la contraseña no coinciden.";
    public const string SinConfirmar = "Tu mail todavía no está confirmado. Te mandamos otro código.";
    public const string SesionVencida = "Tu sesión venció. Volvé a iniciar sesión.";
    public const string CodigoIncompleto = "Escribí los 6 números del código.";
    public const string CodigoVencido = "El código venció o no es correcto. Pedí otro.";
    public const string SinInternet = "Hace falta internet para esto.";
    public const string Espera = "Ya te mandamos un código hace poco. Esperá un minuto y pedí otro.";
    public const string SinServicio = "Las cuentas necesitan el servicio online, que no está configurado en esta versión.";
    public const string SinMail = "No se pudo mandar el mail. Probá de nuevo en un rato.";
    public const string Generico = "No se pudo completar. Probá de nuevo en un rato.";

    private static readonly Regex FormaDeMail = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$");

    /// <summary>null si el mail sirve; si no, el mensaje para mostrar debajo del campo.</summary>
    public static string ValidarMail(string mail)
    {
        string m = (mail ?? "").Trim();
        return m.Length <= 254 && FormaDeMail.IsMatch(m) ? null : MailInvalido;
    }

    /// <summary>null si la contraseña sirve y coincide con la repetida; si no, el mensaje.</summary>
    public static string ValidarClave(string clave, string repetida)
    {
        if ((clave ?? "").Length < LargoMinimo) return ClaveCorta;
        return clave == repetida ? null : ClavesDistintas;
    }

    /// <summary>
    /// El estado de la cuenta en esta PC: con la sesión de una cuenta abierta (con cuenta), con un mail que espera el
    /// código (falta confirmar) o sin nada (sin cuenta: el progreso queda en la PC, US 281).
    /// </summary>
    public static Estado EstadoDe(bool enCuenta, string mailPendiente)
    {
        if (enCuenta) return Estado.ConCuenta;
        return string.IsNullOrEmpty(mailPendiente) ? Estado.SinCuenta : Estado.FaltaConfirmar;
    }

    /// <summary>Los números del código, sin espacios ni otros caracteres (se puede pegar «481 902»).</summary>
    public static string SoloNumeros(string texto)
    {
        var sb = new StringBuilder();
        foreach (char c in texto ?? "") if (c >= '0' && c <= '9') sb.Append(c);
        return sb.ToString();
    }

    /// <summary>
    /// El mensaje para el jugador según la respuesta de Supabase Auth (código HTTP y cuerpo con <c>error_code</c>).
    /// Sin conexión, Unity devuelve el código 0.
    /// </summary>
    public static string Mensaje(long codigo, string cuerpo)
    {
        if (codigo == 0) return SinInternet;
        string c = cuerpo ?? "";
        if (Tiene(c, "email_exists", "user_already_exists", "already been registered", "already registered", "email_address_already")) return MailUsado;
        if (Tiene(c, "invalid_credentials", "Invalid login credentials")) return NoCoinciden;
        if (Tiene(c, "email_not_confirmed", "Email not confirmed")) return SinConfirmar;
        if (Tiene(c, "email_address_invalid", "invalid format", "Unable to validate email")) return MailInvalido;
        if (Tiene(c, "weak_password", "Password should be")) return ClaveCorta;
        if (Tiene(c, "otp_expired", "otp_disabled", "Token has expired", "invalid otp", "Invalid token")) return CodigoVencido;
        if (codigo == 429 || Tiene(c, "over_email_send_rate_limit", "rate limit")) return Espera;
        if (Tiene(c, "email_address_not_authorized", "Error sending")) return SinMail;
        return Generico;
    }

    private static bool Tiene(string texto, params string[] partes)
    {
        foreach (string p in partes)
            if (texto.IndexOf(p, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }
}
