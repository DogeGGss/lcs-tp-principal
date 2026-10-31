using TMPro;
using UnityEngine;
using static ShopUIKit;
using static PerfilUI;
using Img = UnityEngine.UI.Image;
using Estado = ReglasCuenta.Estado;

// Pestaña Cuenta del Perfil (F18: US 282; diseño en la guía, #cuenta-juego). La cuenta se maneja en el launcher: el
// juego abre con ella y esta pestaña solo la muestra (el mail, desde cuándo, el nombre) y dice que se cambia desde el
// launcher. A la derecha, la tarjeta de privacidad con el aviso completo (US 273, CA1).
// En el editor y en los builds de desarrollo no hay launcher (LogrosOnline.EntraEnElJuego): la tarjeta también deja
// crear la cuenta, confirmar el código, entrar y salir, para probar (US 237, US 269 y US 270):
// - Sin cuenta: tres ventajas, «Crear cuenta» e «Iniciar sesión».
// - Falta confirmar: el código de 6 números que llegó al mail, «Confirmar» y «¿No te llegó?» (otro código a los 60 s).
// - Con cuenta: lo mismo que en el juego publicado, con «Cerrar sesión».
public class PestanaCuenta
{
    private const float SegundosParaReenviar = 60f;
    private static float ultimoEnvio = -999f; // cuándo se mandó el último código, para «¿No te llegó?»

    private readonly MenuRiftwalker menu;
    private readonly RectTransform panel;
    private LogrosOnline suscripto;
    private TMP_InputField codigo;
    private readonly TextMeshProUGUI[] digitos = new TextMeshProUGUI[ReglasCuenta.LargoCodigo];
    private readonly Img[] bordesDigito = new Img[ReglasCuenta.LargoCodigo];
    private TextMeshProUGUI reenviar, avisoCodigo;
    private Boton confirmar;
    private bool esperando; // hay un pedido en camino: los botones no hacen nada
    // Lo escrito en «Crear cuenta», para volver a la ventana después de leer el aviso de privacidad.
    private string mailEscrito = "", claveEscrita = "", repetidaEscrita = "";

    public PestanaCuenta(MenuRiftwalker menu, RectTransform panel)
    {
        this.menu = menu;
        this.panel = panel;
    }

    private static LogrosOnline Online => LogrosOnline.Instancia;
    private static Estado EstadoActual => Online == null ? Estado.SinCuenta : Online.Cuenta;

    /// <summary>El texto a la derecha de las pestañas.</summary>
    public string Info
    {
        get
        {
            Estado e = EstadoActual;
            if (!LogrosOnline.EntraEnElJuego) return $"Cuenta  <color=#F3F4F6>{(e == Estado.ConCuenta ? "Desde el launcher" : "Sin cuenta")}</color>";
            string valor = e == Estado.ConCuenta ? "Con cuenta" : e == Estado.FaltaConfirmar ? "Falta confirmar" : "Sin cuenta";
            return $"Editor  <color=#F3F4F6>{valor}</color>";
        }
    }

    public void Refrescar()
    {
        if (panel == null) { Desuscribir(); return; } // el menú se destruyó (por ejemplo, al empezar una partida)
        if (suscripto != Online) { Desuscribir(); suscripto = Online; if (suscripto != null) suscripto.CambioCuenta += Refrescar; }
        BorrarHijos(panel);
        codigo = null; reenviar = avisoCodigo = null; confirmar = null;

        RectTransform t = Place(Node("Cuenta", panel), 0f, 0f, 1100f, 500f);
        Image(t, Rounded, PanelColor, 8f);
        Borde(t, 8f, White(0.1f));
        Estado e = EstadoActual;
        if (e == Estado.ConCuenta) ConCuenta(t);
        else if (e == Estado.FaltaConfirmar) FaltaConfirmar(t);
        else SinCuenta(t);
        Privacidad();
    }

    private void Desuscribir()
    {
        if (suscripto != null) suscripto.CambioCuenta -= Refrescar;
        suscripto = null;
    }

    /// <summary>Cada cuadro, con la pestaña a la vista: la cuenta de «¿No te llegó?».</summary>
    public void Tick()
    {
        if (reenviar == null) return;
        float falta = ultimoEnvio + SegundosParaReenviar - Time.unscaledTime;
        reenviar.text = falta > 0f
            ? $"¿No te llegó? Podés pedir otro en 0:{Mathf.CeilToInt(falta):00}"
            : "¿No te llegó? <color=#F29A38><u>Pedí otro código</u></color>";
    }

    // ================= Tarjeta de la cuenta =================

    private void Encabezado(RectTransform t, string rotulo, string titulo)
    {
        T(t, "Rotulo", 44f, 36f, 900f, 22f, FRotulo, 18f, Accent, rotulo, TextAlignmentOptions.MidlineLeft, 16f, true);
        T(t, "Titulo", 42f, 62f, 1000f, 64f, FTitulo, 60f, Ink, titulo, TextAlignmentOptions.MidlineLeft, 3f, true);
    }

    private static TextMeshProUGUI Parrafo(RectTransform parent, string nombre, float x, float y, float w, float h, float tam, Color color, string texto)
    {
        TextMeshProUGUI p = T(parent, nombre, x, y, w, h, FTexto, tam, color, texto, TextAlignmentOptions.TopLeft);
        p.textWrappingMode = TextWrappingModes.Normal;
        return p;
    }

    private void SinCuenta(RectTransform t)
    {
        Encabezado(t, "Tu cuenta", "Jugás sin cuenta");
        bool editor = LogrosOnline.EntraEnElJuego;
        Parrafo(t, "Texto", 44f, 136f, 1000f, 70f, 25f, Soft, editor
            ? "En el juego publicado, la cuenta se maneja desde el launcher. Acá, en el editor, podés crearla o entrar para probar."
            : "La cuenta se maneja desde el launcher: entrá ahí y tocá Jugar.");
        string[] ventajas = { "Tu progreso en la nube, en cualquier PC", "Aparecé en el ranking global", "Buscá partida con otros jugadores" };
        for (int i = 0; i < ventajas.Length; i++)
        {
            Tilde(t, 44f, 230f + i * 48f);
            T(t, "Ventaja" + i, 86f, 226f + i * 48f, 900f, 34f, FTexto, 24f, Ink, ventajas[i]);
        }
        if (!editor) return;
        bool hay = Online != null && Online.Configurado;
        CrearBoton(t, 44f, 396f, 320f, 66f, "Crear cuenta", true, () => AbrirCrear(null), 26f).Habilitar(hay);
        CrearBoton(t, 384f, 396f, 320f, 66f, "Iniciar sesión", false, () => AbrirEntrar(null), 26f).Habilitar(hay);
        if (!hay) Parrafo(t, "SinServicio", 724f, 400f, 340f, 70f, 19f, Mute, ReglasCuenta.SinServicio);
    }

    private void FaltaConfirmar(RectTransform t)
    {
        Encabezado(t, "Tu cuenta · Falta un paso", "Confirmá tu mail");
        Parrafo(t, "Texto", 44f, 136f, 1000f, 70f, 25f, Soft,
            $"Te mandamos un código de 6 números a <color=#F3F4F6>{Escapar(Online.MailDeLaCuenta)}</color>. Escribilo para activar la cuenta.");
        Codigo(t, 44f, 232f);
        confirmar = CrearBoton(t, 526f, 232f, 260f, 76f, "Confirmar", true, Confirmar, 28f);
        avisoCodigo = T(t, "Aviso", 44f, 318f, 1000f, 28f, FTexto, 20f, Bad, "");
        reenviar = T(t, "Reenviar", 44f, 352f, 700f, 30f, FTexto, 21f, Mute, "");
        Enlace(reenviar, Reenviar);
        TextMeshProUGUI otro = T(t, "OtroMail", 44f, 386f, 700f, 30f, FTexto, 21f, Mute, "¿Te equivocaste de mail? <color=#F29A38><u>Cambialo</u></color>");
        Enlace(otro, () => { Online.OlvidarPendiente(); AbrirCrear(null); });
        Parrafo(t, "Mientras", 44f, 440f, 1000f, 30f, 21f, Mute, "Mientras tanto seguís jugando como siempre.");
        Tick();
        PintarCodigo();
        codigo.ActivateInputField();
    }

    private void ConCuenta(RectTransform t)
    {
        T(t, "Rotulo", 44f, 36f, 900f, 22f, FRotulo, 18f, Accent, "Tu cuenta", TextAlignmentOptions.MidlineLeft, 16f, true);
        string mail = Online.MailDeLaCuenta;
        // 64 de alto: con menos, el renglón (48 px de Barlow Condensed) no entra y TMP, con puntos suspensivos, no lo muestra.
        TextMeshProUGUI m = T(t, "Mail", 42f, 60f, 760f, 64f, FTitulo, 48f, Ink, mail, TextAlignmentOptions.MidlineLeft, 2f);
        m.overflowMode = TextOverflowModes.Ellipsis;
        float x = Mathf.Min(42f + Width(m, mail) + 24f, 830f);
        RectTransform chip = Place(Node("Confirmada", t), x, 75f, 150f, 34f);
        Image(chip, Rounded, WithAlpha(Ok, 0.16f), 17f);
        T(chip, "Texto", 0f, 0f, 150f, 34f, FRotulo, 17f, Ok, "Confirmada", TextAlignmentOptions.Center, 12f, true);
        bool editor = LogrosOnline.EntraEnElJuego;
        Parrafo(t, "Texto", 44f, 136f, 1000f, 70f, 25f, Soft, (editor ? "En el editor se usa la sesión del launcher o la que se abrió acá." : "Entraste desde el launcher.")
            + " Tu progreso, tus monedas y tu lugar en el ranking están en la cuenta, y los seguís en cualquier PC.");
        string desde = Online.CreadaEl;
        string[,] datos = { { "Desde", desde == "" ? "—" : desde }, { "Nombre", PlayerProfile.HasName ? PlayerProfile.Name : "Jugador" }, { "Progreso", "En la nube" } };
        for (int i = 0; i < 3; i++)
        {
            RectTransform c = Place(Node("Dato" + i, t), 44f + i * 300f, 236f, 280f, 84f);
            Image(c, Rounded, new Color(0f, 0f, 0f, DarkAlpha(0.35f)), 6f);
            T(c, "Rotulo", 18f, 10f, 260f, 18f, FRotulo, 15f, Mute, datos[i, 0], TextAlignmentOptions.MidlineLeft, 14f, true);
            T(c, "Valor", 18f, 32f, 250f, 40f, FTitulo, 32f, Ink, datos[i, 1], TextAlignmentOptions.MidlineLeft, 3f, true).overflowMode = TextOverflowModes.Ellipsis;
        }
        if (!editor)
        {
            // US 282, CA2: la cuenta se cambia desde el launcher, no desde el juego.
            Image(Place(Node("Linea", t), 44f, 360f, 1012f, 1f), null, White(0.08f));
            Parrafo(t, "Launcher", 44f, 390f, 1000f, 70f, 23f, Mute,
                "Para cambiar de cuenta o cerrar sesión, usá el <color=#F3F4F6>launcher</color>: el menú de la cuenta está arriba, al lado de Ajustes.");
            return;
        }
        // Solo en el editor (US 270): se vuelve al progreso de la PC; lo de la cuenta queda en ella.
        CrearBoton(t, 44f, 396f, 300f, 66f, "Cerrar sesión", false, () => menu.Confirmar("Tu cuenta", "Cerrar sesión",
            "En el editor volvés al progreso sin cuenta. Lo de la cuenta queda guardado en ella y vuelve cuando inicies sesión (en el juego publicado, desde el launcher).",
            "Cerrar sesión", () => { esperando = true; Online.CerrarSesion(() => { esperando = false; menu.Avisar("Cerraste la sesión"); Refrescar(); }); }), 26f);
        Parrafo(t, "Editor", 364f, 404f, 700f, 60f, 19f, Mute, "En el juego publicado, la cuenta se cambia desde el launcher.");
    }

    private void Privacidad()
    {
        RectTransform t = Place(Node("Privacidad", panel), 1130f, 0f, 670f, 500f);
        Image(t, Rounded, PanelColor, 8f);
        Borde(t, 8f, White(0.1f));
        T(t, "Rotulo", 40f, 36f, 560f, 22f, FRotulo, 18f, Accent, "Privacidad", TextAlignmentOptions.MidlineLeft, 16f, true);
        T(t, "Titulo", 38f, 62f, 600f, 64f, FTitulo, 48f, Ink, "Qué guardamos", TextAlignmentOptions.MidlineLeft, 3f, true);
        Parrafo(t, "Texto", 40f, 132f, 590f, 230f, 23f, Soft, "Tu mail, tu nombre, tu progreso, tus estadísticas y tus opiniones. Los usamos para guardar tu progreso, armar el ranking y buscar partida. No los compartimos con nadie.");
        CrearBoton(t, 40f, 396f, 330f, 66f, "Ver el aviso completo", false, () => AbrirPrivacidad(null), 24f);
    }

    // Texto que se puede tocar, como las pestañas: un link dentro de la tarjeta o de la ventana.
    private static void Enlace(TextMeshProUGUI texto, System.Action alClic)
    {
        texto.raycastTarget = true;
        texto.gameObject.AddComponent<ShopPointerTarget>().Clicked = alClic;
        texto.gameObject.AddComponent<SonidoAlPasar>();
    }

    // Tilde dibujada con dos trazos (ninguna fuente del juego tiene el ✓), dentro de un círculo verde.
    private static void Tilde(RectTransform parent, float x, float y)
    {
        RectTransform caja = Place(Node("Tilde", parent), x, y, 26f, 26f);
        Circulo(caja, 13f, 13f, 13f, WithAlpha(Ok, 0.16f));
        Segment(caja, new Vector2(7f, 13.5f), new Vector2(11f, 17.5f), 3f, Ok);
        Segment(caja, new Vector2(10.2f, 18f), new Vector2(19f, 8.5f), 3f, Ok);
    }

    // ================= Código de 6 números =================

    // Seis casillas que muestran lo que se escribe en un campo invisible que las tapa (se puede pegar el código).
    private void Codigo(RectTransform parent, float x, float y)
    {
        for (int i = 0; i < digitos.Length; i++)
        {
            RectTransform d = Place(Node("Digito" + i, parent), x + i * 76f, y, 64f, 76f);
            Image(d, Rounded, ChipColor, 8f);
            bordesDigito[i] = Borde(d, 8f, White(0.14f));
            digitos[i] = T(d, "Numero", 0f, 0f, 64f, 76f, FMono, 40f, Ink, "", TextAlignmentOptions.Center);
        }
        RectTransform caja = Place(Node("Campo", parent), x, y, digitos.Length * 76f - 12f, 76f);
        caja.gameObject.SetActive(false);
        Image(caja, null, White(0f), 0f, true);
        RectTransform area = Stretch(Node("Area", caja));
        TextMeshProUGUI valor = Text(Stretch(Node("Texto", area)), FMono, 40f, White(0f), TextAlignmentOptions.MidlineLeft);
        codigo = caja.gameObject.AddComponent<TMP_InputField>();
        codigo.textViewport = area;
        codigo.textComponent = valor;
        codigo.fontAsset = FMono;
        codigo.pointSize = 40f;
        codigo.lineType = TMP_InputField.LineType.SingleLine;
        codigo.contentType = TMP_InputField.ContentType.IntegerNumber;
        codigo.characterLimit = ReglasCuenta.LargoCodigo;
        codigo.customCaretColor = true;
        codigo.caretColor = White(0f);
        codigo.selectionColor = White(0f);
        codigo.onValueChanged.AddListener(_ => { if (avisoCodigo != null) avisoCodigo.text = ""; PintarCodigo(); });
        codigo.onSubmit.AddListener(_ => Confirmar());
        caja.gameObject.SetActive(true);
    }

    private void PintarCodigo()
    {
        if (codigo == null) return;
        string n = ReglasCuenta.SoloNumeros(codigo.text);
        for (int i = 0; i < digitos.Length; i++)
        {
            digitos[i].text = i < n.Length ? n[i].ToString() : "";
            if (bordesDigito[i] != null) bordesDigito[i].color = i == Mathf.Min(n.Length, digitos.Length - 1) ? Accent : White(0.14f);
        }
        confirmar?.Habilitar(!esperando && n.Length == ReglasCuenta.LargoCodigo);
    }

    private void Confirmar()
    {
        if (esperando || codigo == null || Online == null) return;
        string n = ReglasCuenta.SoloNumeros(codigo.text);
        if (n.Length != ReglasCuenta.LargoCodigo) { avisoCodigo.text = ReglasCuenta.CodigoIncompleto; return; }
        esperando = true;
        PintarCodigo();
        Online.ConfirmarMail(n, error =>
        {
            esperando = false;
            if (error == null)
            {
                menu.CerrarModal();
                menu.Avisar("Listo: tu cuenta está activa");
                Refrescar();
                return;
            }
            if (avisoCodigo != null) avisoCodigo.text = error;
            else menu.Avisar(error);
            PintarCodigo();
        });
    }

    private void Reenviar()
    {
        if (esperando || Online == null || Time.unscaledTime < ultimoEnvio + SegundosParaReenviar) return;
        esperando = true;
        Online.ReenviarCodigo(error =>
        {
            esperando = false;
            if (error == null) { ultimoEnvio = Time.unscaledTime; menu.Avisar("Te mandamos otro código"); }
            else menu.Avisar(error);
            Tick();
            PintarCodigo();
        });
    }

    // ================= Ventanas =================

    private class Campo
    {
        public TMP_InputField input;
        public Img borde;
        public TextMeshProUGUI aviso;
    }

    // Campo de una línea con su nombre arriba y el aviso en rojo debajo (como en el diseño: 664 × 60). El texto va arriba
    // del área y el área tiene margen para quedar centrada: con el texto centrado en alto, el cursor de TMP sale arriba.
    // Las contraseñas tienen «Ver» a la derecha, que las muestra.
    private static Campo NuevoCampo(RectTransform parent, float y, string nombre, string ejemplo, string valor, bool clave, string ayuda = null)
    {
        var c = new Campo();
        T(parent, "Nombre " + nombre, 48f, y, 500f, 22f, FRotulo, 17f, Mute, nombre, TextAlignmentOptions.MidlineLeft, 12f, true);
        if (ayuda != null) T(parent, "Ayuda " + nombre, 300f, y, 412f, 22f, FTexto, 18f, Mute, ayuda, TextAlignmentOptions.MidlineRight);
        RectTransform caja = Place(Node("Campo " + nombre, parent), 48f, y + 28f, 664f, 60f);
        caja.gameObject.SetActive(false);
        Image(caja, Rounded, ChipColor, 8f, true);
        c.borde = Borde(caja, 8f, White(0.14f));
        RectTransform area = Stretch(Node("Area", caja));
        area.offsetMin = new Vector2(20f, 14f);
        area.offsetMax = new Vector2(clave ? -118f : -20f, -14f);
        area.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        TextMeshProUGUI ph = Text(Stretch(Node("Ejemplo", area)), FTexto, 26f, White(0.3f), TextAlignmentOptions.TopLeft);
        ph.text = ejemplo;
        TextMeshProUGUI texto = Text(Stretch(Node("Texto", area)), FTexto, 26f, Ink, TextAlignmentOptions.TopLeft);
        c.input = caja.gameObject.AddComponent<TMP_InputField>();
        c.input.textViewport = area;
        c.input.textComponent = texto;
        c.input.placeholder = ph;
        c.input.fontAsset = FTexto;
        c.input.pointSize = 26f;
        c.input.lineType = TMP_InputField.LineType.SingleLine;
        c.input.contentType = clave ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.EmailAddress;
        c.input.characterLimit = clave ? 72 : 254;
        c.input.customCaretColor = true;
        c.input.caretColor = Accent;
        c.input.caretWidth = 2;
        c.input.selectionColor = WithAlpha(Accent, 0.35f);
        c.input.text = valor ?? "";
        c.aviso = T(parent, "Aviso " + nombre, 48f, y + 94f, 664f, 26f, FTexto, 20f, Bad, "");
        c.input.onValueChanged.AddListener(_ => Marcar(c, null));
        if (clave)
        {
            TextMeshProUGUI ver = T(caja, "Ver", 664f - 110f, 0f, 92f, 60f, FRotulo, 18f, Accent, "Ver", TextAlignmentOptions.MidlineRight, 12f, true);
            Enlace(ver, () =>
            {
                bool oculta = c.input.contentType == TMP_InputField.ContentType.Password;
                c.input.contentType = oculta ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
                c.input.ForceLabelUpdate();
                ver.text = oculta ? "Ocultar" : "Ver";
            });
        }
        caja.gameObject.SetActive(true);
        return c;
    }

    private static void Marcar(Campo c, string aviso)
    {
        c.aviso.text = aviso ?? "";
        if (c.borde != null) c.borde.color = aviso != null ? Bad : White(0.14f);
    }

    private static void Titulos(RectTransform p, string rotulo, string titulo, Color? colorRotulo = null)
    {
        T(p, "Rotulo", 48f, 36f, 664f, 22f, FRotulo, 18f, colorRotulo ?? Accent, rotulo, TextAlignmentOptions.MidlineLeft, 16f, true);
        T(p, "Titulo", 46f, 60f, 664f, 54f, FTitulo, 48f, Ink, titulo, TextAlignmentOptions.MidlineLeft, 3f, true);
    }

    // US 237, CA1: mail, contraseña (8 o más) y repetirla, con el aviso de privacidad. La cuenta arranca de cero (CA3).
    // error: lo que dijo el servicio la vez anterior.
    private void AbrirCrear(string error)
    {
        RectTransform p = menu.AbrirModal(760f, 700f);
        Titulos(p, "Cuenta nueva", "Crear cuenta");
        Parrafo(p, "DeCero", 48f, 122f, 664f, 30f, 21f, Mute, "La cuenta arranca de cero: con ella guardás tu progreso en la nube y jugás en cualquier PC.");
        Campo mail = NuevoCampo(p, 170f, "Mail", "tu@mail.com", mailEscrito, false);
        Campo clave = NuevoCampo(p, 300f, "Contraseña", "", claveEscrita, true, "8 caracteres o más");
        Campo repetida = NuevoCampo(p, 410f, "Repetir la contraseña", "", repetidaEscrita, true);
        // CA4: si el mail ya tiene cuenta, el link lleva a «Iniciar sesión» con el mail escrito.
        TextMeshProUGUI entrar = T(p, "IrAEntrar", 412f, 170f + 94f, 300f, 26f, FTexto, 20f, Accent, "<u>Iniciar sesión</u>", TextAlignmentOptions.MidlineRight);
        Enlace(entrar, () => { Recordar(mail, clave, repetida); AbrirEntrar(mail.input.text); });
        entrar.gameObject.SetActive(false);
        mail.input.onValueChanged.AddListener(_ => entrar.gameObject.SetActive(false));
        System.Action<string> mostrar = problema =>
        {
            Marcar(problema == ReglasCuenta.ClaveCorta ? clave : problema == ReglasCuenta.ClavesDistintas ? repetida : mail, problema);
            entrar.gameObject.SetActive(problema == ReglasCuenta.MailUsado);
        };
        if (error != null) mostrar(error);
        TextMeshProUGUI aviso = T(p, "Privacidad", 48f, 538f, 664f, 30f, FTexto, 20f, Mute,
            "Al crear la cuenta aceptás el <color=#F29A38><u>aviso de privacidad</u></color>.");
        Enlace(aviso, () => { Recordar(mail, clave, repetida); AbrirPrivacidad(() => AbrirCrear(null)); });
        CrearBoton(p, 48f, 596f, 320f, 64f, "Cancelar", false, menu.CerrarModal, 24f);
        Boton crear = null;
        crear = CrearBoton(p, 392f, 596f, 320f, 64f, "Crear cuenta", true, () =>
        {
            if (esperando) return;
            Recordar(mail, clave, repetida);
            string problemaMail = ReglasCuenta.ValidarMail(mail.input.text);
            string problemaClave = ReglasCuenta.ValidarClave(clave.input.text, repetida.input.text);
            Marcar(mail, problemaMail);
            Marcar(problemaClave == ReglasCuenta.ClavesDistintas ? repetida : clave, problemaClave);
            if (problemaMail != null || problemaClave != null) return;
            esperando = true;
            crear.texto.text = "Creando…";
            crear.Habilitar(false);
            Online.CrearCuenta(mail.input.text, clave.input.text, resultado =>
            {
                esperando = false;
                if (resultado == null)
                {
                    ultimoEnvio = Time.unscaledTime;
                    claveEscrita = repetidaEscrita = ""; // la contraseña no queda en la pantalla
                    menu.CerrarModal();
                    // Si Supabase no pide confirmar el mail, la cuenta ya quedó abierta.
                    menu.Avisar(EstadoActual == Estado.ConCuenta ? "Listo: tu cuenta está activa" : "Te mandamos un código a tu mail");
                    Refrescar();
                    return;
                }
                crear.texto.text = "Crear cuenta";
                crear.Habilitar(true);
                mostrar(resultado);
            });
        }, 24f);
        mail.input.ActivateInputField();
    }

    private void Recordar(Campo mail, Campo clave, Campo repetida)
    {
        mailEscrito = mail.input.text;
        claveEscrita = clave.input.text;
        repetidaEscrita = repetida.input.text;
    }

    // US 269: mail y contraseña de una cuenta que ya existe. Si no coinciden, el aviso no dice cuál de los dos está mal
    // (CA5). «¿Te olvidaste la contraseña?» llega con la US 271.
    private void AbrirEntrar(string mailInicial)
    {
        RectTransform p = menu.AbrirModal(760f, 600f);
        Titulos(p, "Tu cuenta", "Iniciar sesión");
        RectTransform banda = Place(Node("Error", p), 48f, 124f, 664f, 50f);
        Image(banda, Rounded, WithAlpha(Bad, 0.14f), 8f);
        TextMeshProUGUI textoBanda = T(banda, "Texto", 18f, 0f, 628f, 50f, FTexto, 21f, Bad, "");
        banda.gameObject.SetActive(false);
        Campo mail = NuevoCampo(p, 190f, "Mail", "tu@mail.com", mailInicial ?? mailEscrito, false);
        Campo clave = NuevoCampo(p, 300f, "Contraseña", "", "", true);
        System.Action<string> mostrar = problema =>
        {
            bool delMail = problema == ReglasCuenta.MailInvalido;
            Marcar(mail, delMail ? problema : null);
            textoBanda.text = delMail ? "" : problema ?? "";
            banda.gameObject.SetActive(!delMail && problema != null);
        };
        CrearBoton(p, 48f, 430f, 320f, 64f, "Cancelar", false, menu.CerrarModal, 24f);
        Boton entrar = null;
        entrar = CrearBoton(p, 392f, 430f, 320f, 64f, "Entrar", true, () =>
        {
            if (esperando) return;
            mailEscrito = mail.input.text;
            string problemaMail = ReglasCuenta.ValidarMail(mail.input.text);
            if (problemaMail != null) { mostrar(problemaMail); return; }
            mostrar(null);
            esperando = true;
            entrar.texto.text = "Entrando…";
            entrar.Habilitar(false);
            Online.IniciarSesion(mail.input.text, clave.input.text, resultado =>
            {
                esperando = false;
                if (resultado == null)
                {
                    menu.CerrarModal();
                    menu.Avisar("Entraste a tu cuenta");
                    Refrescar();
                    return;
                }
                if (resultado == ReglasCuenta.SinConfirmar)
                {
                    // La cuenta existe pero falta el código: el Perfil pasa a «Confirmá tu mail» con un código nuevo.
                    ultimoEnvio = Time.unscaledTime;
                    menu.CerrarModal();
                    menu.Avisar(resultado);
                    Refrescar();
                    return;
                }
                entrar.texto.text = "Entrar";
                entrar.Habilitar(true);
                mostrar(resultado);
            });
        }, 24f);
        clave.input.onSubmit.AddListener(_ => entrar.button.onClick.Invoke());
        TextMeshProUGUI crear = T(p, "IrACrear", 48f, 522f, 664f, 30f, FTexto, 21f, Mute, "¿No tenés cuenta? <color=#F29A38><u>Crear cuenta</u></color>");
        Enlace(crear, () => { mailEscrito = mail.input.text; AbrirCrear(null); });
        if (string.IsNullOrEmpty(mail.input.text)) mail.input.ActivateInputField();
        else clave.input.ActivateInputField();
    }

    // US 273, CA1: qué se guarda, para qué, dónde y los derechos del jugador. alVolver: a dónde vuelve (Crear cuenta).
    private void AbrirPrivacidad(System.Action alVolver)
    {
        RectTransform p = menu.AbrirModal(980f, 780f);
        Titulos(p, "Privacidad", "Aviso de privacidad");
        string[,] partes =
        {
            { "Qué guardamos", "Tu mail y tu contraseña (cifrada), tu nombre, tu progreso (logros, cosméticos, monedas), tus estadísticas de partida y las opiniones que mandes." },
            { "Para qué", "Para que recuperes tu progreso en cualquier PC, para el ranking global y para buscar partida. No lo usamos para publicidad ni lo compartimos con nadie." },
            { "Dónde", "En la base del juego (Supabase). Las partidas pasan por los servidores de Photon, que reciben tu nombre de jugador, tu id de cuenta y tu conexión (IP), como cualquier juego en línea." },
            { "Tus derechos", "Podés ver tus datos en el Perfil y pedir que borremos tu cuenta con todo lo que tiene (Ley 25.326 de Protección de Datos Personales)." },
        };
        for (int i = 0; i < 4; i++)
        {
            T(p, "Parte" + i, 48f, 136f + i * 136f, 880f, 26f, FRotulo, 20f, Accent, partes[i, 0], TextAlignmentOptions.MidlineLeft, 12f, true);
            Parrafo(p, "Detalle" + i, 48f, 166f + i * 136f, 880f, 100f, 22f, Soft, partes[i, 1]);
        }
        CrearBoton(p, 980f - 48f - 272f, 690f, 272f, 60f, "Entendido", true, () => { if (alVolver != null) alVolver(); else menu.CerrarModal(); }, 24f);
    }

    private static string Escapar(string s) => (s ?? "").Replace("<", "&lt;").Replace(">", "&gt;");
}
