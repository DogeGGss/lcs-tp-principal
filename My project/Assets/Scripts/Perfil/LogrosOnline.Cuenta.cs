using System;
using System.Collections;
using System.IO;
using UnityEngine;

// La cuenta del jugador (F18: US 237, US 269, US 270, US 281 y US 282; diseño en la guía, #cuenta-juego). La cuenta se
// maneja en el launcher, como en un cliente: ahí se crea, se entra y se sale, y el launcher guarda la sesión en
// cuenta-sesion-<host>.json (Launcher/Core/Cuentas.cs). El juego abre con esa sesión: su progreso va en el archivo de
// la cuenta (Guardado.UsarCuenta) y se sube a la nube. Sin una sesión, un build publicado no deja jugar y el menú pide
// iniciarla en el launcher (PantallaSinSesion). Las cuentas arrancan de cero: lo jugado antes queda en la PC.
// En el editor (y en los builds de desarrollo) no hay launcher: el Perfil deja crear la cuenta, entrar y salir para
// probar, con los mismos pedidos que el launcher (Supabase Auth por REST):
// - Crear (US 237): POST /auth/v1/signup; Supabase manda un código de 6 números (plantilla «Confirm signup» con
//   {{ .Token }}) y POST /auth/v1/verify lo confirma y devuelve la sesión.
// - Entrar (US 269): POST /auth/v1/token?grant_type=password. Salir (US 270): POST /auth/v1/logout.
// La contraseña nunca se guarda en la PC: solo la sesión.
public sealed partial class LogrosOnline
{
    [Serializable] private sealed class Meta { public string nombre; }
    [Serializable] private sealed class Identidad { public string id; }
    [Serializable] private sealed class Registro { public string email, password; public Meta data; }
    [Serializable] private sealed class Credenciales { public string email, password; }
    [Serializable] private sealed class Verificar { public string type, email, token; }
    [Serializable] private sealed class Reenvio { public string type, email; }
    [Serializable] private sealed class DatosUsuario { public Meta data; }

    private bool enCuenta;       // hay una cuenta abierta: la sesión es la suya
    private string mailPendiente; // se creó una cuenta con este mail y espera el código

    /// <summary>Cambió el estado de la cuenta (se pidió el código, se entró, se salió): el Perfil se vuelve a dibujar.</summary>
    public event Action CambioCuenta;

    /// <summary>En el editor y en los builds de desarrollo, donde no hay launcher, la cuenta se abre desde el Perfil. En el
    /// editor se puede probar como en el juego publicado: Riftwalker > Cuenta > Como en el juego publicado.</summary>
    public static bool EntraEnElJuego =>
#if UNITY_EDITOR
        !UnityEditor.EditorPrefs.GetBool(ClaveComoPublicado, false);
#else
        Debug.isDebugBuild;
#endif
    public const string ClaveComoPublicado = "Riftwalker.Cuenta.ComoPublicado";

    /// <summary>US 282, CA3: no hay una sesión de cuenta y es un build publicado: no se juega hasta entrar en el launcher.</summary>
    public bool SinSesion { get; private set; }

    /// <summary>La sesión se cerró con el juego abierto (desde el launcher, o se borró la cuenta).</summary>
    public bool SesionCerrada { get; private set; }

    public ReglasCuenta.Estado Cuenta => ReglasCuenta.EstadoDe(enCuenta, mailPendiente);

    /// <summary>El id de la cuenta abierta (el usuario de Supabase), o null sin cuenta.</summary>
    public string IdDeLaCuenta => enCuenta ? sesion?.user?.id : null;

    /// <summary>El mail de la cuenta abierta, o el que espera el código.</summary>
    public string MailDeLaCuenta => enCuenta ? sesion?.user?.email ?? "" : mailPendiente ?? "";

    /// <summary>Cuándo se creó la cuenta abierta, "" si no se sabe.</summary>
    public string CreadaEl =>
        enCuenta && sesion?.user != null && DateTime.TryParse(sesion.user.created_at, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out DateTime d) ? d.ToLocalTime().ToString("dd/MM/yyyy") : "";

    private string RutaPendiente => Path.Combine(Application.persistentDataPath, "cuenta-pendiente.txt");

    /// <summary>US 237: crea la cuenta y manda el código. listo recibe null si se mandó, o el mensaje de error.</summary>
    public void CrearCuenta(string mail, string clave, Action<string> listo) => StartCoroutine(EnTurno(CrearAhora(mail.Trim(), clave, listo)));

    /// <summary>US 237, CA2: confirma el mail con el código y entra a la cuenta.</summary>
    public void ConfirmarMail(string codigoMail, Action<string> listo) => StartCoroutine(EnTurno(ConfirmarAhora(codigoMail, listo)));

    /// <summary>«¿No te llegó?»: manda otro código al mail que espera confirmación.</summary>
    public void ReenviarCodigo(Action<string> listo) => StartCoroutine(EnTurno(ReenviarAhora(listo)));

    /// <summary>US 269: entra a una cuenta que ya existe.</summary>
    public void IniciarSesion(string mail, string clave, Action<string> listo) => StartCoroutine(EnTurno(EntrarAhora(mail.Trim(), clave, listo)));

    /// <summary>US 270: sale de la cuenta y vuelve al progreso de la PC.</summary>
    public void CerrarSesion(Action listo) => StartCoroutine(EnTurno(SalirAhora(listo)));

    /// <summary>«¿Te equivocaste de mail?»: se olvida del mail que esperaba el código.</summary>
    public void OlvidarPendiente() { GuardarPendiente(null); CambioCuenta?.Invoke(); }

    // Al arrancar (Awake): la cuenta es la de la sesión que guardó el launcher (US 282) o, en el editor, la que se abrió
    // desde el Perfil. Su progreso se abre aunque no haya conexión (CA4); sin sesión, un build publicado no deja jugar.
    private void AbrirCuentaGuardada()
    {
        try { if (File.Exists(RutaPendiente)) mailPendiente = File.ReadAllText(RutaPendiente).Trim(); } catch { }
        if (string.IsNullOrEmpty(mailPendiente)) mailPendiente = null;
        Sesion s = null;
        try { if (File.Exists(RutaSesionCuenta)) s = JsonUtility.FromJson<Sesion>(File.ReadAllText(RutaSesionCuenta)); } catch { }
        if (s == null || string.IsNullOrEmpty(s.refresh_token) || s.user == null || string.IsNullOrEmpty(s.user.id))
        {
            Guardado.UsarCuenta(null);
            SinSesion = !EntraEnElJuego;
            return;
        }
        enCuenta = true;
        Guardado.UsarCuenta(s.user.id); // la del launcher, aunque la marca de la PC dijera otra cuenta
        // En una PC nueva el archivo de la cuenta está vacío: el nombre es el de la cuenta (US 269, CA2). Sin nombre (una
        // cuenta recién creada), el menú lo pide como siempre.
        string nombre = s.user.user_metadata?.nombre;
        if (!PlayerProfile.HasName && !string.IsNullOrEmpty(nombre)) PlayerProfile.TrySetName(nombre, out _);
    }

    // Las operaciones de la cuenta usan los mismos campos que Trabajar (respuesta, error...): esperan a que termine.
    private IEnumerator EnTurno(IEnumerator operacion)
    {
        while (Ocupado) yield return null;
        Ocupado = true;
        try { yield return operacion; }
        finally { Ocupado = false; Revision++; }
    }

    private IEnumerator CrearAhora(string mail, string clave, Action<string> listo)
    {
        string problema = ReglasCuenta.ValidarMail(mail) ?? ReglasCuenta.ValidarClave(clave, clave);
        if (problema != null) { listo?.Invoke(problema); yield break; }
        if (!Configurado) { listo?.Invoke(ReglasCuenta.SinServicio); yield break; }
        if (enCuenta) { listo?.Invoke("Ya estás en una cuenta. Cerrá la sesión para crear otra."); yield break; }
        string nombre = PlayerProfile.HasName ? PlayerProfile.Name : "";
        yield return Post("/auth/v1/signup", JsonUtility.ToJson(new Registro { email = mail, password = clave, data = new Meta { nombre = nombre } }), false);
        if (error != null) { listo?.Invoke(ReglasCuenta.Mensaje(codigo, cuerpoError)); yield break; }
        // Sin confirmación de mail en el proyecto, Supabase devuelve la sesión: se entra directo.
        Sesion directa = Leer<Sesion>(respuesta);
        if (directa != null && !string.IsNullOrEmpty(directa.access_token)) { Entrar(directa, nombre); listo?.Invoke(null); yield break; }
        // Con un mail que ya tiene cuenta, Supabase no da error (para no revelar qué mails existen): devuelve un usuario
        // sin identidades.
        Usuario u = Leer<Usuario>(respuesta);
        if (u == null || string.IsNullOrEmpty(u.id)) { listo?.Invoke(ReglasCuenta.Generico); yield break; }
        if (u.identities != null && u.identities.Length == 0) { listo?.Invoke(ReglasCuenta.MailUsado); yield break; }
        GuardarPendiente(mail);
        listo?.Invoke(null);
        CambioCuenta?.Invoke();
    }

    private IEnumerator ConfirmarAhora(string codigoMail, Action<string> listo)
    {
        string numeros = ReglasCuenta.SoloNumeros(codigoMail);
        if (numeros.Length != ReglasCuenta.LargoCodigo) { listo?.Invoke(ReglasCuenta.CodigoIncompleto); yield break; }
        if (!Configurado || string.IsNullOrEmpty(mailPendiente)) { listo?.Invoke(ReglasCuenta.SinServicio); yield break; }
        string nombrePC = PlayerProfile.HasName ? PlayerProfile.Name : "";
        yield return Post("/auth/v1/verify", JsonUtility.ToJson(new Verificar { type = "signup", email = mailPendiente, token = numeros }), false);
        if (error != null && codigo != 0) // proyectos nuevos de Supabase confirman el alta con el tipo «email»
            yield return Post("/auth/v1/verify", JsonUtility.ToJson(new Verificar { type = "email", email = mailPendiente, token = numeros }), false);
        if (error != null) { listo?.Invoke(ReglasCuenta.Mensaje(codigo, cuerpoError)); yield break; }
        Sesion nueva = Leer<Sesion>(respuesta);
        if (nueva == null || string.IsNullOrEmpty(nueva.access_token) || string.IsNullOrEmpty(nueva.refresh_token)) { listo?.Invoke(ReglasCuenta.Generico); yield break; }
        Entrar(nueva, nombrePC);
        listo?.Invoke(null);
    }

    private IEnumerator ReenviarAhora(Action<string> listo)
    {
        if (!Configurado || string.IsNullOrEmpty(mailPendiente)) { listo?.Invoke(ReglasCuenta.SinServicio); yield break; }
        yield return Post("/auth/v1/resend", JsonUtility.ToJson(new Reenvio { type = "signup", email = mailPendiente }), false);
        listo?.Invoke(error == null ? null : ReglasCuenta.Mensaje(codigo, cuerpoError));
    }

    private IEnumerator EntrarAhora(string mail, string clave, Action<string> listo)
    {
        string problema = ReglasCuenta.ValidarMail(mail);
        if (problema != null) { listo?.Invoke(problema); yield break; }
        if (string.IsNullOrEmpty(clave)) { listo?.Invoke(ReglasCuenta.NoCoinciden); yield break; }
        if (!Configurado) { listo?.Invoke(ReglasCuenta.SinServicio); yield break; }
        string nombrePC = PlayerProfile.HasName ? PlayerProfile.Name : "";
        yield return Post("/auth/v1/token?grant_type=password", JsonUtility.ToJson(new Credenciales { email = mail, password = clave }), false);
        if (error != null)
        {
            string mensaje = ReglasCuenta.Mensaje(codigo, cuerpoError);
            if (mensaje == ReglasCuenta.SinConfirmar)
            {
                // CA5: la cuenta existe pero falta el código: se manda otro y el Perfil pasa a «Confirmá tu mail».
                GuardarPendiente(mail);
                yield return Post("/auth/v1/resend", JsonUtility.ToJson(new Reenvio { type = "signup", email = mail }), false);
                CambioCuenta?.Invoke();
            }
            listo?.Invoke(mensaje);
            yield break;
        }
        Sesion nueva = Leer<Sesion>(respuesta);
        if (nueva == null || string.IsNullOrEmpty(nueva.access_token) || string.IsNullOrEmpty(nueva.refresh_token)) { listo?.Invoke(ReglasCuenta.Generico); yield break; }
        Entrar(nueva, nombrePC);
        listo?.Invoke(null);
    }

    private IEnumerator SalirAhora(Action listo)
    {
        // Avisa a Supabase que esta sesión ya no sirve; si no hay conexión, igual se sale en la PC.
        if (enCuenta && sesion != null && !Vencida()) yield return Post("/auth/v1/logout?scope=local", "{}", true);
        SalirDeLaCuentaLocal();
        listo?.Invoke();
    }

    // Abre la cuenta de esa sesión: su archivo de progreso (vacío si es nueva) y, en la próxima vuelta de Trabajar, lo
    // de la nube. El nombre es el de la cuenta (el que se eligió al crearla) o, si no tiene, el de la PC.
    private void Entrar(Sesion nueva, string nombrePC)
    {
        nueva.expires_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Math.Max(60, nueva.expires_in);
        sesion = nueva;
        enCuenta = true;
        SinSesion = SesionCerrada = false;
        GuardarSesion();
        GuardarPendiente(null);
        string nombreCuenta = nueva.user?.user_metadata?.nombre;
        Guardado.UsarCuenta(nueva.user != null ? nueva.user.id : null);
        string nombre = !string.IsNullOrEmpty(nombreCuenta) ? nombreCuenta : PlayerProfile.HasName ? PlayerProfile.Name : nombrePC;
        if (!string.IsNullOrEmpty(nombre) && nombre != PlayerProfile.Name) PlayerProfile.TrySetName(nombre, out _);
        ReiniciarSincronizacion();
        CambioCuenta?.Invoke();
    }

    // Vuelve al progreso de la PC y a la sesión anónima (la que había, o una nueva cuando haga falta). En un build
    // publicado no se juega sin cuenta: el menú pide volver a entrar en el launcher (US 282, CA3).
    private void SalirDeLaCuentaLocal()
    {
        try { File.Delete(RutaSesionCuenta); } catch { }
        enCuenta = false;
        sesion = LeerSesion();
        Guardado.UsarCuenta(null);
        if (!EntraEnElJuego) SinSesion = SesionCerrada = true;
        ReiniciarSincronizacion();
        CambioCuenta?.Invoke();
    }

    // Lo del jugador cambió: la próxima vuelta sube y baja todo de nuevo.
    private void ReiniciarSincronizacion()
    {
        enviado = -1; nombreEnviado = null;
        progresoEnviado = null; progresoCambio = true; revisionProgreso = -1;
        pedir = true; fallos = 0; intentar = Time.unscaledTime;
        Revision++;
    }

    private void GuardarPendiente(string mail)
    {
        mailPendiente = string.IsNullOrEmpty(mail) ? null : mail;
        try
        {
            if (mailPendiente == null) File.Delete(RutaPendiente);
            else Guardado.EscribirAtomico(RutaPendiente, mailPendiente);
        }
        catch { }
    }

    // El nombre de la cuenta sigue al del jugador: así aparece al entrar desde otra PC (US 269) y en el launcher.
    private void NombreCambiado(string nombre)
    {
        if (enCuenta && Configurado && nombre != sesion?.user?.user_metadata?.nombre) StartCoroutine(EnTurno(GuardarNombre(nombre)));
    }

    private IEnumerator GuardarNombre(string nombre)
    {
        yield return AsegurarSesion();
        if (falloSesion != null || !enCuenta) yield break;
        yield return Pedir("PUT", "/auth/v1/user", JsonUtility.ToJson(new DatosUsuario { data = new Meta { nombre = nombre } }), true);
    }

    private static T Leer<T>(string json) where T : class
    {
        try { return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<T>(json); } catch { return null; }
    }
}
