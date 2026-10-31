using NUnit.Framework;

// Cuenta del jugador (F18: US 237 y US 269): las reglas de ReglasCuenta, sin conexión. Los mensajes son los de la guía
// (#cuenta-ventanas) y las respuestas, las que devuelve Supabase Auth.

public class CuentaTests
{
    [Test, Category("US 237"), Description("CA1: el mail tiene que tener forma de mail")]
    [TestCase("ulises@gmail.com", true)]
    [TestCase("  ulises.fonseca@alu.ungs.edu.ar  ", true)]
    [TestCase("ulises", false)]
    [TestCase("ulises@", false)]
    [TestCase("ulises@gmail", false)]
    [TestCase("uli ses@gmail.com", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public void Mail_valido(string mail, bool sirve)
    {
        Assert.AreEqual(sirve ? null : ReglasCuenta.MailInvalido, ReglasCuenta.ValidarMail(mail));
    }

    [Test, Category("US 237"), Description("CA1: contraseña de 8 caracteres o más, repetida igual")]
    public void Contrasena_de_8_y_repetida()
    {
        Assert.AreEqual(ReglasCuenta.ClaveCorta, ReglasCuenta.ValidarClave("1234567", "1234567"), "7 no alcanza");
        Assert.AreEqual(ReglasCuenta.ClaveCorta, ReglasCuenta.ValidarClave(null, null));
        Assert.AreEqual(ReglasCuenta.ClavesDistintas, ReglasCuenta.ValidarClave("riftwalker", "riftwalkr"));
        Assert.IsNull(ReglasCuenta.ValidarClave("riftwalker", "riftwalker"), "8 o más, iguales");
    }

    [Test, Category("US 237"), Description("CA2: sin cuenta, esperando el código o con la sesión de la cuenta abierta")]
    public void Estado_de_la_cuenta()
    {
        Assert.AreEqual(ReglasCuenta.Estado.SinCuenta, ReglasCuenta.EstadoDe(false, null), "sin cuenta: el progreso queda en la PC");
        Assert.AreEqual(ReglasCuenta.Estado.SinCuenta, ReglasCuenta.EstadoDe(false, ""), "sin mail pendiente");
        Assert.AreEqual(ReglasCuenta.Estado.FaltaConfirmar, ReglasCuenta.EstadoDe(false, "a@b.com"), "creó la cuenta, falta el código");
        Assert.AreEqual(ReglasCuenta.Estado.ConCuenta, ReglasCuenta.EstadoDe(true, null), "en la cuenta");
        Assert.AreEqual(ReglasCuenta.Estado.ConCuenta, ReglasCuenta.EstadoDe(true, "c@d.com"), "la sesión de la cuenta manda");
    }

    [Test, Category("US 237"), Description("CA2: el código se puede pegar con espacios")]
    public void Codigo_solo_numeros()
    {
        Assert.AreEqual("481902", ReglasCuenta.SoloNumeros(" 481 902 "));
        Assert.AreEqual("", ReglasCuenta.SoloNumeros(null));
        Assert.AreEqual(6, ReglasCuenta.LargoCodigo);
    }

    [Test, Category("US 237"), Description("CA4: un mensaje claro para cada respuesta de Supabase Auth")]
    [TestCase(0, "", ReglasCuenta.SinInternet)]
    [TestCase(422, "{\"code\":422,\"error_code\":\"email_exists\",\"msg\":\"A user with this email address has already been registered\"}", ReglasCuenta.MailUsado)]
    [TestCase(400, "{\"error_code\":\"email_address_invalid\"}", ReglasCuenta.MailInvalido)]
    [TestCase(422, "{\"error_code\":\"weak_password\",\"msg\":\"Password should be at least 8 characters.\"}", ReglasCuenta.ClaveCorta)]
    [TestCase(403, "{\"error_code\":\"otp_expired\",\"msg\":\"Token has expired or is invalid\"}", ReglasCuenta.CodigoVencido)]
    [TestCase(429, "{\"error_code\":\"over_email_send_rate_limit\"}", ReglasCuenta.Espera)]
    [TestCase(400, "{\"error_code\":\"email_address_not_authorized\"}", ReglasCuenta.SinMail)]
    [TestCase(500, "{\"msg\":\"Error sending confirmation email\"}", ReglasCuenta.SinMail)]
    [TestCase(500, "{\"msg\":\"otra cosa\"}", ReglasCuenta.Generico)]
    public void Mensajes_de_error(long codigo, string cuerpo, string esperado)
    {
        Assert.AreEqual(esperado, ReglasCuenta.Mensaje(codigo, cuerpo));
    }

    [Test, Category("US 269"), Description("CA5: al iniciar sesión no se aclara si está mal el mail o la contraseña")]
    [TestCase(0, "", ReglasCuenta.SinInternet)]
    [TestCase(400, "{\"code\":400,\"error_code\":\"invalid_credentials\",\"msg\":\"Invalid login credentials\"}", ReglasCuenta.NoCoinciden)]
    [TestCase(400, "{\"code\":400,\"error_code\":\"email_not_confirmed\",\"msg\":\"Email not confirmed\"}", ReglasCuenta.SinConfirmar)]
    public void Mensajes_al_entrar(long codigo, string cuerpo, string esperado)
    {
        Assert.AreEqual(esperado, ReglasCuenta.Mensaje(codigo, cuerpo));
    }
}
