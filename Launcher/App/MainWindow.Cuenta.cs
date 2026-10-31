using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Riftwalker.Launcher;

// The player's account (F18: US 237, US 269, US 270 and US 282; design in the guide, #cuenta-launcher). The launcher
// works like a game client: without a session the account panel covers the window (everything but the top bar) and
// PLAY does nothing; with one, the top bar shows the account and its menu, and PLAY opens the game with that session
// (the game reads the same file, see Cuentas). Each step of the panel is built in code, like the game's menus.
public partial class MainWindow
{
    private enum PanelCuenta { Ninguno, Entrar, Crear, Confirmar, Privacidad, Cerrar, CerrarJuego }
    private const int SegundosParaReenviar = 60;
    private Cuentas? cuentas;
    private PanelCuenta panelCuenta;
    private PanelCuenta volverDePrivacidad;
    private bool esperandoCuenta;
    // What the player typed, kept while moving between steps. The passwords are dropped once the account exists.
    private string mailCuenta = "", claveCrear = "", repetidaCrear = "";
    private string? avisoEntrar;           // shown at the top of «Iniciá sesión» (for example, after logging out)
    private DateTime ultimoCodigo = DateTime.MinValue;
    private DispatcherTimer? relojCodigo;

    private static readonly Brush Tinta = Pincel(0xF3F4F6), Gris = Pincel(0x8E96A3), Naranja = Pincel(0xF29A38), Rojo = Pincel(0xFF5C5C);
    private static SolidColorBrush Pincel(int rgb) { var b = new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb)); b.Freeze(); return b; }

    // On start (installed launcher): the saved session, renewed in the background; without one, «Iniciá sesión».
    private void IniciarCuenta()
    {
        cuentas = new Cuentas(http, ServicioCuentas.Juego, Cuentas.CarpetaDelJuego());
        cuentas.Cargar();
        PreviewKeyDown += TeclaCuenta;
        PintarCuenta();
        if (cuentas.Sesion == null) { AbrirCuenta(PanelCuenta.Entrar); return; }
        _ = RenovarCuentaAsync();
    }

    private async Task RenovarCuentaAsync()
    {
        if (cuentas == null || await cuentas.RenovarAsync(CancellationToken.None)) { PintarCuenta(); if (!busy) Idle(); return; }
        // Rejected by Supabase: closed from somewhere else, or the account was deleted.
        avisoEntrar = ReglasCuenta.SesionVencida;
        PintarCuenta();
        AbrirCuenta(PanelCuenta.Entrar);
    }

    private bool ConSesion => cuentas?.Sesion != null;

    // The chip in the top bar and its menu follow the session.
    private void PintarCuenta()
    {
        var usuario = cuentas?.Sesion?.User;
        CuentaChip.Visibility = usuario != null ? Visibility.Visible : Visibility.Collapsed;
        if (usuario == null) { CerrarMenuCuenta(); return; }
        string nombre = usuario.Nombre;
        CuentaInicial.Text = nombre.Length > 0 ? char.ToUpperInvariant(nombre[0]).ToString() : "?";
        CuentaNombre.Text = nombre.ToUpperInvariant();
        CuentaMail.Text = usuario.Email;
        CuentaMenuMail.Text = usuario.Email;
        CuentaMenuDesde.Text = usuario.Desde.Length > 0 ? $"Cuenta confirmada · desde el {usuario.Desde}" : "Cuenta confirmada";
    }

    // The text of the right card when the game is installed and up to date.
    private string EstadoConCuenta(bool conectado) => conectado
        ? $"Entrás como {cuentas!.Sesion!.User!.Nombre}. Tu progreso está en la cuenta."
        : "No se pudo buscar actualizaciones. Podés jugar con tu cuenta: lo que juegues se sube cuando vuelva la conexión.";

    // ---------- Account menu ----------

    private void CuentaChip_Click(object sender, RoutedEventArgs e)
    {
        if (CuentaMenuCapa.Visibility == Visibility.Visible) { CerrarMenuCuenta(); return; }
        Point abajo = CuentaChip.TranslatePoint(new Point(CuentaChip.ActualWidth, CuentaChip.ActualHeight), Shell);
        Canvas.SetLeft(CuentaMenu, abajo.X - CuentaMenu.Width);
        Canvas.SetTop(CuentaMenu, abajo.Y + 8);
        CuentaMenuCapa.Visibility = Visibility.Visible;
        CuentaChip.Tag = "abierto";
    }
    private void CerrarMenuCuenta() { CuentaMenuCapa.Visibility = Visibility.Collapsed; CuentaChip.Tag = null; }
    private void CuentaMenuCapa_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, CuentaMenuCapa)) CerrarMenuCuenta();
    }
    private void MenuPrivacidad_Click(object sender, RoutedEventArgs e) { CerrarMenuCuenta(); volverDePrivacidad = PanelCuenta.Ninguno; AbrirCuenta(PanelCuenta.Privacidad); }
    // US 270, CA3: with the game open, it has to be closed first (it is playing with this session).
    private void MenuCerrar_Click(object sender, RoutedEventArgs e)
    {
        CerrarMenuCuenta();
        AbrirCuenta(!App.Preview && installer.IsRunning() ? PanelCuenta.CerrarJuego : PanelCuenta.Cerrar);
    }

    private void TeclaCuenta(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || SettingsPanel.Visibility == Visibility.Visible) return;
        if (CuentaMenuCapa.Visibility == Visibility.Visible) CerrarMenuCuenta();
        else if (panelCuenta is PanelCuenta.Crear or PanelCuenta.Confirmar) AbrirCuenta(PanelCuenta.Entrar);
        else if (panelCuenta == PanelCuenta.Privacidad) AbrirCuenta(volverDePrivacidad);
        else if (panelCuenta is PanelCuenta.Cerrar or PanelCuenta.CerrarJuego) AbrirCuenta(PanelCuenta.Ninguno);
        else return;
        e.Handled = true;
    }

    // ---------- The panel ----------

    // Shows a step of the account panel (Ninguno hides it). error: what the service said last time, if anything.
    private void AbrirCuenta(PanelCuenta panel, string? error = null)
    {
        if (panel == PanelCuenta.Ninguno && !ConSesion && !App.Preview) panel = PanelCuenta.Entrar; // without a session there is no way around it
        panelCuenta = panel;
        relojCodigo?.Stop();
        CerrarMenuCuenta();
        CuentaContenido.Children.Clear();
        CuentaTarjeta.BorderBrush = Naranja;
        CuentaPanel.Visibility = panel == PanelCuenta.Ninguno ? Visibility.Collapsed : Visibility.Visible;
        switch (panel)
        {
            case PanelCuenta.Entrar: Entrar(error); break;
            case PanelCuenta.Crear: Crear(error); break;
            case PanelCuenta.Confirmar: Confirmar(error); break;
            case PanelCuenta.Privacidad: Privacidad(); break;
            case PanelCuenta.Cerrar: Cerrar(); break;
            case PanelCuenta.CerrarJuego: CerrarJuego(); break;
        }
    }

    // US 269: «Iniciá sesión», the first thing without a session.
    private void Entrar(string? error)
    {
        Encabezado("TU CUENTA", "INICIÁ SESIÓN", "Para jugar hace falta una cuenta. Tu progreso queda en ella y lo seguís en cualquier PC.");
        if (avisoEntrar != null) Franja(avisoEntrar, false);
        Campo mail = new("MAIL", false) { Valor = mailCuenta };
        Campo clave = new("CONTRASEÑA", true);
        if (error == ReglasCuenta.MailInvalido) mail.Marcar(error);
        else if (error != null) Franja(error, true);
        Agregar(mail.Raiz); Agregar(clave.Raiz);
        Button entrar = Principal("ENTRAR  →");
        async void Enviar()
        {
            if (esperandoCuenta) return;
            mailCuenta = mail.Valor.Trim();
            string? problema = ReglasCuenta.ValidarMail(mailCuenta);
            mail.Marcar(problema);
            if (problema != null || cuentas == null) return;
            string? r = await Esperar(entrar, "ENTRANDO…", t => cuentas.EntrarAsync(mailCuenta, clave.Valor, t));
            if (r == null) { Entrado("Entraste a tu cuenta."); return; }
            if (r == ReglasCuenta.SinConfirmar) { ultimoCodigo = DateTime.UtcNow; AbrirCuenta(PanelCuenta.Confirmar, r); return; }
            AbrirCuenta(PanelCuenta.Entrar, r);
        }
        entrar.Click += (_, _) => Enviar();
        mail.Enter += Enviar; clave.Enter += Enviar;
        Pie("¿No tenés cuenta?", "Crear cuenta", () => AbrirCuenta(PanelCuenta.Crear));
        // «¿Te olvidaste la contraseña?» comes with US 271.
        (mailCuenta.Length == 0 ? mail : clave).Foco();
    }

    // US 237, CA1: mail, password (8 or more) and repeat it, with the privacy notice. The account starts from scratch.
    private void Crear(string? error)
    {
        Encabezado("CUENTA NUEVA", "CREAR CUENTA", "La cuenta arranca de cero: con ella guardás tu progreso en la nube y jugás en cualquier PC.");
        Campo mail = new("MAIL", false) { Valor = mailCuenta };
        Campo clave = new("CONTRASEÑA", true, "8 caracteres o más") { Valor = claveCrear };
        Campo repetida = new("REPETIR LA CONTRASEÑA", true) { Valor = repetidaCrear };
        void Recordar() { mailCuenta = mail.Valor.Trim(); claveCrear = clave.Valor; repetidaCrear = repetida.Valor; }
        void Mostrar(string problema)
        {
            if (problema == ReglasCuenta.MailUsado) mail.Marcar(problema, "Iniciar sesión", () => { Recordar(); AbrirCuenta(PanelCuenta.Entrar); });
            else if (problema == ReglasCuenta.MailInvalido) mail.Marcar(problema);
            else if (problema == ReglasCuenta.ClaveCorta) clave.Marcar(problema);
            else if (problema == ReglasCuenta.ClavesDistintas) repetida.Marcar(problema);
            else Franja(problema, true, 0);
        }
        Agregar(mail.Raiz); Agregar(clave.Raiz); Agregar(repetida.Raiz);
        if (error != null) Mostrar(error);
        var legal = new TextBlock { Foreground = Gris, FontSize = 13, Margin = new Thickness(0, -4, 0, 4), TextWrapping = TextWrapping.Wrap };
        legal.Inlines.Add("Al crear la cuenta aceptás el ");
        legal.Inlines.Add(new InlineUIContainer(Enlace("aviso de privacidad", () => { Recordar(); volverDePrivacidad = PanelCuenta.Crear; AbrirCuenta(PanelCuenta.Privacidad); }, 13)) { BaselineAlignment = BaselineAlignment.TextBottom });
        legal.Inlines.Add(".");
        Agregar(legal);
        var (_, crear) = Botones(("Volver", () => { Recordar(); AbrirCuenta(PanelCuenta.Entrar); }), ("CREAR CUENTA", null));
        async void Enviar()
        {
            if (esperandoCuenta) return;
            Recordar();
            string? problemaMail = ReglasCuenta.ValidarMail(mailCuenta), problemaClave = ReglasCuenta.ValidarClave(claveCrear, repetidaCrear);
            mail.Marcar(problemaMail);
            clave.Marcar(problemaClave == ReglasCuenta.ClaveCorta ? problemaClave : null);
            repetida.Marcar(problemaClave == ReglasCuenta.ClavesDistintas ? problemaClave : null);
            if (problemaMail != null || problemaClave != null || cuentas == null) return;
            string? r = await Esperar(crear, "CREANDO…", t => cuentas.CrearAsync(mailCuenta, claveCrear, t));
            if (r != null) { AbrirCuenta(PanelCuenta.Crear, r); return; }
            claveCrear = repetidaCrear = ""; // the password does not stay on screen
            ultimoCodigo = DateTime.UtcNow;
            // Without mail confirmation in the project, Supabase already returned the session.
            if (ConSesion) Entrado("Listo: tu cuenta está activa."); else AbrirCuenta(PanelCuenta.Confirmar);
        }
        crear.Click += (_, _) => Enviar();
        mail.Enter += Enviar; clave.Enter += Enviar; repetida.Enter += Enviar;
        mail.Foco();
    }

    // US 237, CA2: the 6-digit code from the mail. «¿No te llegó?» sends another one after 60 s.
    private void Confirmar(string? mensaje)
    {
        Encabezado("TU CUENTA · FALTA UN PASO", "CONFIRMÁ TU MAIL", null);
        var ayuda = Ayuda("");
        ayuda.Inlines.Add("Te mandamos un código de 6 números a ");
        ayuda.Inlines.Add(new Run(mailCuenta) { Foreground = Tinta, FontWeight = FontWeights.SemiBold });
        ayuda.Inlines.Add(". Si no lo ves, revisá el correo no deseado.");
        if (mensaje == ReglasCuenta.SinConfirmar) Franja(mensaje, false);
        else if (mensaje != null) Franja(mensaje, true);
        var codigo = new Codigo();
        Agregar(codigo.Raiz);
        var reenviar = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
        Agregar(reenviar);
        void PintarReenviar()
        {
            reenviar.Children.Clear();
            int falta = SegundosParaReenviar - (int)(DateTime.UtcNow - ultimoCodigo).TotalSeconds;
            if (falta > 0) { reenviar.Children.Add(Texto($"¿No te llegó? Podés pedir otro en 0:{falta:00}", Gris, 14)); return; }
            reenviar.Children.Add(Texto("¿No te llegó? ", Gris, 14));
            reenviar.Children.Add(Enlace("Pedí otro código", async () =>
            {
                if (esperandoCuenta || cuentas == null) return;
                esperandoCuenta = true;
                string? r = await cuentas.ReenviarAsync(mailCuenta, CancellationToken.None);
                esperandoCuenta = false;
                if (r == null) ultimoCodigo = DateTime.UtcNow;
                if (panelCuenta == PanelCuenta.Confirmar) AbrirCuenta(PanelCuenta.Confirmar, r ?? "Te mandamos otro código.");
            }));
        }
        PintarReenviar();
        relojCodigo ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        relojCodigo.Tick -= TickCodigo; relojCodigo.Tick += TickCodigo;
        reenviarActual = PintarReenviar;
        relojCodigo.Start();
        var cambiar = new WrapPanel { Margin = new Thickness(0, 0, 0, 16) };
        cambiar.Children.Add(Texto("¿Te equivocaste de mail? ", Gris, 14));
        cambiar.Children.Add(Enlace("Cambialo", () => AbrirCuenta(PanelCuenta.Crear)));
        Agregar(cambiar);
        Button confirmar = Principal("CONFIRMAR");
        async void Enviar()
        {
            if (esperandoCuenta || cuentas == null) return;
            if (codigo.Valor.Length != ReglasCuenta.LargoCodigo) { AbrirCuenta(PanelCuenta.Confirmar, ReglasCuenta.CodigoIncompleto); return; }
            string valor = codigo.Valor;
            string? r = await Esperar(confirmar, "CONFIRMANDO…", t => cuentas.ConfirmarAsync(mailCuenta, valor, t));
            if (r == null) Entrado("Listo: tu cuenta está activa."); else AbrirCuenta(PanelCuenta.Confirmar, r);
        }
        confirmar.Click += (_, _) => Enviar();
        codigo.Completo += Enviar;
        codigo.Foco();
    }
    private Action? reenviarActual;
    private void TickCodigo(object? sender, EventArgs e) { if (panelCuenta == PanelCuenta.Confirmar) reenviarActual?.Invoke(); else relojCodigo?.Stop(); }

    // US 273, CA1: what is kept, what for, where, and the player's rights.
    private void Privacidad()
    {
        Encabezado("PRIVACIDAD", "AVISO DE PRIVACIDAD", null);
        (string, string)[] partes =
        [
            ("QUÉ GUARDAMOS", "Tu mail y tu contraseña (cifrada), tu nombre, tu progreso (logros, cosméticos y monedas), tus estadísticas de partida y las opiniones que mandes."),
            ("PARA QUÉ", "Para que recuperes tu progreso en cualquier PC, para el ranking global y para buscar partida. No lo usamos para publicidad ni lo compartimos con nadie."),
            ("DÓNDE", "En la base del juego (Supabase). Las partidas pasan por los servidores de Photon, que reciben tu nombre de jugador, tu id de cuenta y tu conexión (IP)."),
            ("TUS DERECHOS", "Podés ver tus datos en el Perfil del juego y pedir que borremos tu cuenta con todo lo que tiene (Ley 25.326 de Protección de Datos Personales)."),
        ];
        foreach (var (titulo, texto) in partes)
        {
            Agregar(new TextBlock { Text = titulo, Foreground = Naranja, FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 4) });
            Agregar(new TextBlock { Text = texto, Foreground = Pincel(0xAEB5C0), FontSize = 14, LineHeight = 20, TextWrapping = TextWrapping.Wrap });
        }
        Botones((null, null), ("ENTENDIDO", () => AbrirCuenta(volverDePrivacidad)));
    }

    // US 270: confirm, log out, and back to «Iniciá sesión».
    private void Cerrar()
    {
        Encabezado("TU CUENTA", "¿CERRAR SESIÓN?", "En esta PC se deja de usar tu cuenta, y para jugar hay que volver a iniciar sesión. Tu progreso queda en la cuenta y vuelve cuando entres de nuevo.");
        var (_, cerrar) = Botones(("Volver", () => AbrirCuenta(PanelCuenta.Ninguno)), ("CERRAR SESIÓN", null));
        cerrar.Click += async (_, _) =>
        {
            if (esperandoCuenta || cuentas == null) return;
            if (App.Preview) return;
            await Esperar(cerrar, "CERRANDO…", async t => { await cuentas.CerrarAsync(t); return null; });
            avisoEntrar = "Cerraste la sesión. Tu progreso quedó en la cuenta.";
            PintarCuenta();
            AbrirCuenta(PanelCuenta.Entrar);
        };
    }
    private void CerrarJuego()
    {
        Encabezado("TU CUENTA", "CERRÁ EL JUEGO PRIMERO", "Project Riftwalker está abierto con tu cuenta. Cerralo y volvé a tocar «Cerrar sesión».");
        Botones((null, null), ("ENTENDIDO", () => AbrirCuenta(PanelCuenta.Ninguno)));
    }

    // In: the panel closes and the right card says what happened.
    private void Entrado(string mensaje)
    {
        avisoEntrar = null;
        PintarCuenta();
        AbrirCuenta(PanelCuenta.Ninguno);
        if (!busy) Idle(mensaje); else StatusText.Text = mensaje;
    }

    // A request in flight: its button says so and the panel ignores other clicks until it ends.
    private async Task<string?> Esperar(Button boton, string texto, Func<CancellationToken, Task<string?>> pedido)
    {
        esperandoCuenta = true;
        object antes = boton.Content;
        boton.Content = texto; boton.IsEnabled = false;
        try { return await pedido(CancellationToken.None); }
        catch (Exception ex) { Log(ex); return ReglasCuenta.Generico; }
        finally { esperandoCuenta = false; boton.Content = antes; boton.IsEnabled = true; }
    }

    // ---------- Pieces (like Ajustes: 12 px labels, 44 px boxes, the orange button) ----------

    private void Agregar(UIElement e) => CuentaContenido.Children.Add(e);
    private void Encabezado(string ante, string titulo, string? ayuda)
    {
        Agregar(new TextBlock { Text = ante, Foreground = Naranja, FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
        Agregar(new TextBlock { Text = titulo, FontFamily = (FontFamily)FindResource("DisplayFont"), FontWeight = FontWeights.Bold, FontSize = 32 });
        if (ayuda != null) Ayuda(ayuda);
    }
    private TextBlock Ayuda(string texto)
    {
        var t = new TextBlock { Text = texto, Foreground = Gris, TextWrapping = TextWrapping.Wrap, LineHeight = 21, Margin = new Thickness(0, 8, 0, 20) };
        Agregar(t); return t;
    }
    // A strip at the top: red for an error, green for news (logged out, another code was sent). index: where it goes.
    private void Franja(string texto, bool error, int? index = null)
    {
        var franja = new Border
        {
            Background = new SolidColorBrush(error ? Color.FromArgb(0x1F, 0xFF, 0x5C, 0x5C) : Color.FromArgb(0x1F, 0x3D, 0xDC, 0x97)),
            BorderBrush = error ? Rojo : Pincel(0x3DDC97), BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(14, 11, 14, 11), Margin = new Thickness(0, -6, 0, 18),
            Child = new TextBlock { Text = texto, Foreground = error ? Pincel(0xFFB3B3) : Pincel(0x9BEFC8), FontSize = 14, TextWrapping = TextWrapping.Wrap },
        };
        if (index is int i) { int despuesDelEncabezado = Math.Min(3, CuentaContenido.Children.Count); CuentaContenido.Children.Insert(Math.Max(i, despuesDelEncabezado), franja); }
        else Agregar(franja);
    }
    private Button Principal(string texto)
    {
        var b = new Button { Content = texto, Style = (Style)FindResource("BotonPrincipal"), Margin = new Thickness(0, 6, 0, 0) };
        Agregar(b); return b;
    }
    // The two buttons at the bottom right, like Ajustes (the action, orange). A null text leaves that place empty.
    private (Button?, Button) Botones((string? Texto, Action? Accion) secundario, (string Texto, Action? Accion) accion)
    {
        var fila = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 22, 0, 0) };
        Button? volver = null;
        if (secundario.Texto != null)
        {
            volver = new Button { Content = secundario.Texto, Margin = new Thickness(0, 0, 10, 0) };
            if (secundario.Accion is { } a) volver.Click += (_, _) => a();
            fila.Children.Add(volver);
        }
        var principal = new Button
        {
            Content = accion.Texto, Background = Naranja, BorderBrush = Naranja, Foreground = Pincel(0x1A1106),
            FontFamily = (FontFamily)FindResource("DisplayFont"), FontWeight = FontWeights.Bold, FontSize = 17, Padding = new Thickness(20, 10, 20, 10),
        };
        if (accion.Accion is { } b) principal.Click += (_, _) => b();
        fila.Children.Add(principal);
        Agregar(fila);
        return (volver, principal);
    }
    private void Pie(string texto, string enlace, Action accion)
    {
        var pie = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 16, 0, 0) };
        pie.Children.Add(Texto(texto + " ", Gris, 13));
        pie.Children.Add(Enlace(enlace, accion, 13));
        Agregar(pie);
    }
    private static TextBlock Texto(string texto, Brush color, double tam) => new() { Text = texto, Foreground = color, FontSize = tam, VerticalAlignment = VerticalAlignment.Center };
    private static Button Enlace(string texto, Action accion, double tam = 14)
    {
        var b = new Button { Content = texto, Style = (Style)Application.Current.FindResource("BotonEnlace"), FontSize = tam, VerticalAlignment = VerticalAlignment.Center };
        b.Click += (_, _) => accion();
        return b;
    }

    // A field: its name above (and a hint on the right), the box (passwords with «VER») and the error below.
    private sealed class Campo
    {
        public readonly StackPanel Raiz = new() { Margin = new Thickness(0, 0, 0, 16) };
        private readonly TextBox texto = new() { MaxLength = 254 };
        private readonly PasswordBox? clave;
        private readonly WrapPanel error = new() { Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
        public event Action? Enter;

        public Campo(string nombre, bool esClave, string? ayuda = null)
        {
            var rotulo = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            if (ayuda != null) { var a = Texto(ayuda, Gris, 12); DockPanel.SetDock(a, Dock.Right); rotulo.Children.Add(a); }
            rotulo.Children.Add(new TextBlock { Text = nombre, FontSize = 12 });
            Raiz.Children.Add(rotulo);
            var caja = new Grid();
            texto.KeyDown += Tecla;
            AutomationPropertiesName(texto, nombre);
            caja.Children.Add(texto);
            if (esClave)
            {
                clave = new PasswordBox { MaxLength = 72, Padding = new Thickness(12, 12, 64, 12) };
                clave.KeyDown += Tecla;
                AutomationPropertiesName(clave, nombre);
                texto.Padding = new Thickness(12, 12, 64, 12);
                texto.Visibility = Visibility.Collapsed;
                caja.Children.Add(clave);
                var ver = new Button { Content = "VER", Style = (Style)Application.Current.FindResource("BotonEnlace"), FontSize = 12, FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
                // «Ver» swaps the password box for a text box with the same value, and back.
                ver.Click += (_, _) =>
                {
                    bool mostrar = clave.Visibility == Visibility.Visible;
                    if (mostrar) texto.Text = clave.Password; else clave.Password = texto.Text;
                    texto.Visibility = mostrar ? Visibility.Visible : Visibility.Collapsed;
                    clave.Visibility = mostrar ? Visibility.Collapsed : Visibility.Visible;
                    ver.Content = mostrar ? "OCULTAR" : "VER";
                    Foco();
                };
                caja.Children.Add(ver);
            }
            Raiz.Children.Add(caja);
            Raiz.Children.Add(error);
            texto.TextChanged += (_, _) => Marcar(null);
            if (clave != null) clave.PasswordChanged += (_, _) => Marcar(null);
        }

        private static void AutomationPropertiesName(DependencyObject o, string nombre) => System.Windows.Automation.AutomationProperties.SetName(o, nombre);
        private void Tecla(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { e.Handled = true; Enter?.Invoke(); } }

        public string Valor
        {
            get => clave != null && clave.Visibility == Visibility.Visible ? clave.Password : texto.Text;
            set { texto.Text = value; if (clave != null) clave.Password = value; }
        }

        // The error below the box (null clears it), with an optional link (for example, «Iniciar sesión»).
        public void Marcar(string? mensaje, string? enlace = null, Action? accion = null)
        {
            error.Children.Clear();
            error.Visibility = mensaje == null ? Visibility.Collapsed : Visibility.Visible;
            texto.Tag = mensaje == null ? null : "mal";
            if (clave != null) clave.Tag = texto.Tag;
            if (mensaje == null) return;
            error.Children.Add(Texto(mensaje + (enlace != null ? " " : ""), Rojo, 13));
            if (enlace != null && accion != null) error.Children.Add(Enlace(enlace, accion, 13));
        }

        public void Foco()
        {
            Control c = clave != null && clave.Visibility == Visibility.Visible ? clave : texto;
            c.Dispatcher.BeginInvoke(DispatcherPriority.Input, () => { c.Focus(); Keyboard.Focus(c); if (c is TextBox t) t.CaretIndex = t.Text.Length; });
        }
    }

    // The code: six boxes that show what is typed in an invisible box on top of them (the code can be pasted).
    private sealed class Codigo
    {
        public readonly Grid Raiz = new() { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 10) };
        private readonly TextBox entrada = new() { MaxLength = 12, Opacity = 0, Cursor = Cursors.IBeam };
        private readonly Border[] cajas = new Border[ReglasCuenta.LargoCodigo];
        private readonly TextBlock[] numeros = new TextBlock[ReglasCuenta.LargoCodigo];
        public event Action? Completo;
        public string Valor => ReglasCuenta.SoloNumeros(entrada.Text);

        public Codigo()
        {
            var fila = new StackPanel { Orientation = Orientation.Horizontal };
            for (int i = 0; i < cajas.Length; i++)
            {
                numeros[i] = new TextBlock { FontFamily = (FontFamily)Application.Current.FindResource("DisplayFont"), FontWeight = FontWeights.Bold, FontSize = 34,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Tinta };
                cajas[i] = new Border { Width = 60, Height = 70, Background = Pincel(0x0A0C11), BorderBrush = Pincel(0x343B47), BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, i < cajas.Length - 1 ? 12 : 0, 0), Child = numeros[i] };
                fila.Children.Add(cajas[i]);
            }
            Raiz.Children.Add(fila);
            System.Windows.Automation.AutomationProperties.SetName(entrada, "Código de 6 números");
            Raiz.Children.Add(entrada);
            entrada.TextChanged += (_, _) =>
            {
                string n = Valor;
                if (n.Length > ReglasCuenta.LargoCodigo) n = n[..ReglasCuenta.LargoCodigo];
                if (entrada.Text != n) { entrada.Text = n; entrada.CaretIndex = n.Length; return; }
                Pintar();
                if (n.Length == ReglasCuenta.LargoCodigo) Completo?.Invoke();
            };
            entrada.GotKeyboardFocus += (_, _) => Pintar();
            entrada.LostKeyboardFocus += (_, _) => Pintar();
            entrada.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; Completo?.Invoke(); } };
            Pintar();
        }

        // The box where the next digit goes is orange while the code has the focus.
        private void Pintar()
        {
            string n = Valor;
            for (int i = 0; i < cajas.Length; i++)
            {
                numeros[i].Text = i < n.Length ? n[i].ToString() : "";
                cajas[i].BorderBrush = entrada.IsKeyboardFocused && i == Math.Min(n.Length, cajas.Length - 1) ? Naranja : Pincel(0x343B47);
            }
        }

        public void Foco() => entrada.Dispatcher.BeginInvoke(DispatcherPriority.Input, () => { entrada.Focus(); Keyboard.Focus(entrada); });
    }

    // ---------- Preview (--preview --cuenta <step>), to look at the design without Supabase ----------

    private void VistaPreviaCuenta(string paso)
    {
        cuentas = new Cuentas(http, ServicioCuentas.Juego, System.IO.Path.Combine(System.IO.Path.GetTempPath(), "riftwalker-vista-previa"));
        PreviewKeyDown += TeclaCuenta;
        mailCuenta = "ulises.fonseca@gmail.com";
        bool conCuenta = paso is "listo" or "menu" or "cerrar" or "cerrar-juego" or "privacidad";
        if (conCuenta)
        {
            cuentas.UsarVistaPrevia(new SesionCuenta { User = new UsuarioCuenta { Id = "vista-previa", Email = mailCuenta, CreatedAt = "2026-10-29T12:00:00Z", UserMetadata = new MetaCuenta { Nombre = "Ulises" } } });
            StateTitle.Text = "LISTO PARA ENTRAR"; InstalledText.Text = "Versión 0.3.0 instalada";
            StatusText.Text = EstadoConCuenta(true);
        }
        PintarCuenta();
        ultimoCodigo = DateTime.UtcNow.AddSeconds(-18);
        switch (paso)
        {
            case "entrar-error": AbrirCuenta(PanelCuenta.Entrar, ReglasCuenta.NoCoinciden); break;
            case "crear": AbrirCuenta(PanelCuenta.Crear); break;
            case "crear-error": AbrirCuenta(PanelCuenta.Crear, ReglasCuenta.MailUsado); break;
            case "confirmar": AbrirCuenta(PanelCuenta.Confirmar); break;
            case "privacidad": AbrirCuenta(PanelCuenta.Privacidad); break;
            case "cerrar": AbrirCuenta(PanelCuenta.Cerrar); break;
            case "cerrar-juego": AbrirCuenta(PanelCuenta.CerrarJuego); break;
            case "listo": AbrirCuenta(PanelCuenta.Ninguno); break;
            case "menu": UpdateLayout(); CuentaChip_Click(CuentaChip, new RoutedEventArgs()); break;
            default: AbrirCuenta(PanelCuenta.Entrar); break;
        }
    }
}
