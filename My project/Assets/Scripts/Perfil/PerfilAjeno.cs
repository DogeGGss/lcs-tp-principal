using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using static ShopUIKit;
using static PerfilUI;
using Img = UnityEngine.UI.Image;
using Tipo = PerfilDatos.Tipo;

// El perfil de otro jugador (F34, US 373). Se abre tocando su nombre en el ranking (PantallaLogros), en la tabla con Tab
// (MatchHud) o en la pantalla de resultado (ResultadoPartida y ResultadoDeathmatch) — CA1.
// - Va en su propio canvas, encima de todo: al cerrarlo se vuelve a donde se abrió, sin perder lo que se estaba viendo
//   (CA5). En una partida libera el mouse y deja de mover la cámara y las armas mientras está abierto; no pausa.
// - Muestra lo público que manda la base (rw_public_profile, LogrosOnline.PedirPerfil): avatar, banner, título y marco,
//   el nombre, el nivel, las estadísticas por modo con el KDA, los logros, el puesto en el ranking (CA2) y, en otra
//   pestaña, su historial (US 374, CA5). Nunca el mail, las monedas ni nada de la cuenta, y no se puede cambiar nada (CA3).
// - Si el jugador no tiene cuenta, o no hay conexión, lo dice en lugar de mostrar datos vacíos (CA4).
public class PerfilAjeno : MonoBehaviour
{
    public static bool Abierto => actual != null || Time.frameCount == cuadroCerrado; // el Esc que lo cerró no hace otra cosa
    private static int cuadroCerrado = -1;
    private static PerfilAjeno actual;

    private const float PanelX = 160f, PanelY = 60f, PanelW = 1600f, PanelH = 960f, Pad = 24f;
    private const float CabH = 230f, TabsY = 270f, CuerpoY = 336f;
    private static readonly float CuerpoW = PanelW - 2f * Pad, CuerpoH = PanelH - CuerpoY - Pad;

    private string idJugador, nombreInicial;
    private LogrosOnline.PerfilPublico perfil;
    private string fallo;
    private bool cargando;
    private int pestana;

    private RectTransform panel, cabecera, cuerpo;
    private readonly TextMeshProUGUI[] tabs = new TextMeshProUGUI[2];
    private Img subrayado;
    private System.Action<LogrosOnline.PerfilPublico, string> alLlegar;

    // Lo que se apaga en una partida mientras está abierto (como los trucos de Invasión), y cómo estaban el cursor y la pausa.
    private readonly List<Behaviour> bloqueados = new List<Behaviour>();
    private CursorLockMode cursorAntes;
    private bool cursorVisibleAntes, pausaAntes, conPausa;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar() => actual = null;

    /// <summary>
    /// Abre el perfil del jugador con ese id de cuenta. nombre se muestra mientras llega lo de la base. Un id que no es de
    /// una cuenta (un jugador sin cuenta, o una versión vieja) muestra que el perfil no está disponible (CA4).
    /// </summary>
    public static void Abrir(string idCuenta, string nombre)
    {
        if (actual != null) actual.Cerrar();
        var go = new GameObject("PerfilAjeno", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        actual = go.AddComponent<PerfilAjeno>();
        actual.Armar(idCuenta, nombre);
    }

    /// <summary>Si es el id de una cuenta (un uuid), como lo usa la base.</summary>
    public static bool EsCuenta(string id) => !string.IsNullOrEmpty(id) && System.Guid.TryParse(id, out _);

    private void Armar(string idCuenta, string nombre)
    {
        idJugador = idCuenta;
        nombreInicial = string.IsNullOrEmpty(nombre) ? "" : nombre;
        AsegurarRecursos();

        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900; // arriba del HUD, la tabla, el resultado y el menú
        canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
        var escala = GetComponent<UnityEngine.UI.CanvasScaler>();
        escala.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // Lo de abajo queda tapado y sin clics.
        Image(Stretch(Node("Sombra", transform)), null, Rgb(4, 6, 10, DarkAlpha(0.86f)), 0f, true);
        RectTransform raiz = Node("Contenido", transform);
        raiz.anchorMin = raiz.anchorMax = raiz.pivot = new Vector2(0.5f, 0.5f);
        raiz.sizeDelta = new Vector2(1920f, 1080f);

        panel = Place(Node("Panel", raiz), PanelX, PanelY, PanelW, PanelH);
        Image(panel, Rounded, Hex(0x0B0E14), 12f, true);
        Borde(panel, 12f, White(0.1f));
        cabecera = Place(Node("Cabecera", panel), Pad, Pad, CuerpoW, CabH);

        string[] nombres = { "Estadísticas", "Historial" };
        float x = Pad;
        for (int i = 0; i < 2; i++)
        {
            int k = i;
            tabs[i] = T(panel, nombres[i], x, TabsY, 400f, 46f, FTitulo, 34f, Mute, nombres[i], TextAlignmentOptions.MidlineLeft, 6f, true);
            float w = Width(tabs[i], tabs[i].text.ToUpperInvariant());
            tabs[i].rectTransform.sizeDelta = new Vector2(w, 46f);
            tabs[i].raycastTarget = true;
            var p = tabs[i].gameObject.AddComponent<ShopPointerTarget>();
            p.Clicked = () => ElegirPestana(k);
            p.Hovered = () => { if (pestana != k) tabs[k].color = Soft; };
            p.Exited = () => { if (pestana != k) tabs[k].color = Mute; };
            x += w + 48f;
        }
        Image(Place(Node("Linea", panel), Pad, TabsY + 52f, CuerpoW, 1f), null, White(0.1f));
        subrayado = Image(Place(Node("Subrayado", panel), Pad, TabsY + 48f, 100f, 4f), null, Accent);
        CrearBoton(panel, PanelW - Pad - 220f, TabsY - 2f, 220f, 48f, "« Volver", false, Cerrar, 22f);
        cuerpo = Place(Node("Cuerpo", panel), Pad, CuerpoY, CuerpoW, CuerpoH);

        BloquearPartida();


        // CA4: sin cuenta no se pregunta a la base.
        if (!EsCuenta(idJugador)) fallo = L10n.T("profile.other.no_account");
        else if (LogrosOnline.Instancia == null) fallo = L10n.T("profile.other.offline");
        else
        {
            cargando = true;
            alLlegar = Llego;
            LogrosOnline.Instancia.PedirPerfil(idJugador, alLlegar);
        }
        pestana = 0;
        Dibujar();
    }

    private void Llego(LogrosOnline.PerfilPublico p, string motivo)
    {
        if (this == null) return;
        cargando = false;
        perfil = p;
        fallo = motivo ?? (p == null || !p.found ? L10n.T("profile.other.no_account") : null);
        Dibujar();
    }

    // En la escena de una partida, si nunca se pasó por el menú, las letras y las piezas salen del HUD.
    private static void AsegurarRecursos()
    {
        if (FTitulo != null && Rounded != null) return;
        MatchHud hud = FindAnyObjectByType<MatchHud>();
        if (hud == null) return;
        FTitulo ??= hud.DisplayFont;
        FRotulo ??= hud.LabelFont;
        FTexto ??= hud.LabelFont;
        if (Rounded == null) Rounded = hud.Rounded;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Cerrar();
        // En una partida, otro sistema (la tienda, el resultado) puede volver a trabar el mouse: mientras esto está abierto, no.
        if (Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }

    private void ElegirPestana(int i)
    {
        pestana = i;
        Dibujar();
    }

    // ================= Dibujo =================

    private void Dibujar()
    {
        for (int k = 0; k < tabs.Length; k++) tabs[k].color = k == pestana ? Ink : Mute;
        RectTransform t = tabs[pestana].rectTransform;
        subrayado.rectTransform.anchoredPosition = new Vector2(t.anchoredPosition.x, -(TabsY + 48f));
        subrayado.rectTransform.sizeDelta = new Vector2(t.sizeDelta.x, 4f);

        DibujarCabecera();
        BorrarHijos(cuerpo);
        if (cargando || fallo != null)
        {
            TextMeshProUGUI m = T(cuerpo, "Mensaje", 0f, 40f, CuerpoW, 200f, FTexto, 28f, cargando ? Mute : Soft,
                cargando ? L10n.T("profile.other.loading") : fallo, TextAlignmentOptions.Top);
            m.textWrappingMode = TextWrappingModes.Normal;
            return;
        }
        if (pestana == 0) DibujarEstadisticas();
        else DibujarHistorial();
    }

    private void DibujarCabecera()
    {
        BorrarHijos(cabecera);
        bool hay = perfil != null && perfil.found;
        PerfilDatos.Cosmetico avatar = Equipado(Tipo.Avatar, "perfil.avatar", "riftwalker");
        PerfilDatos.Cosmetico banner = Equipado(Tipo.Banner, "perfil.banner", "anden-riftwalker");
        PerfilDatos.Cosmetico marco = Equipado(Tipo.Marco, "perfil.marco", "anden-riftwalker");
        PerfilDatos.Cosmetico titulo = Equipado(Tipo.Titulo, "perfil.titulo", "");

        // Como la cabecera del Perfil: el banner de fondo con sombras, el marco, el avatar con borde blanco, el nombre y el título.
        Vector4 d = hay ? InteriorMarco(marco, CabH) : Vector4.zero;
        RectTransform caja = Place(Node("Banner", cabecera), d.x, d.y, CuerpoW - d.x - d.z, CabH - d.y - d.w);
        Image(Stretch(Node("Fondo", caja)), Rounded, PanelColor, 10f);
        if (hay) Cubrir(caja, PerfilDatos.Imagen(banner));
        Image(Place(Node("Sombra", caja), 0f, 0f, 1000f, CabH), Degrade(true, 0.9f, 0f), Color.white);
        if (hay)
        {
            RectTransform marcoRect = null;
            Enmarcar(ref marcoRect, cabecera, 0f, 0f, CuerpoW, CabH, marco, false, caja);
        }

        float lado = 160f, ax = d.x + 40f, ay = (CabH - lado) / 2f;
        Image(Place(Node("BordeAvatar", cabecera), ax - 4f, ay - 4f, lado + 8f, lado + 8f), Rounded, Ink, 8f);
        RectTransform cajaAvatar = Place(Node("Avatar", cabecera), ax, ay, lado, lado);
        if (hay) Cubrir(cajaAvatar, PerfilDatos.Imagen(avatar));
        else Image(cajaAvatar, Rounded, PanelColor, 6f);

        float tx = ax + lado + 36f;
        string nombre = hay && !string.IsNullOrEmpty(perfil.display_name) ? perfil.display_name : nombreInicial;
        TextMeshProUGUI n = Text(Place(Node("Nombre", cabecera), tx, 26f, 760f, 104f), FTitulo, 72f, Ink, TextAlignmentOptions.MidlineLeft, 3f, true);
        n.text = nombre;
        n.richText = false;
        n.overflowMode = TextOverflowModes.Ellipsis;
        bool sinTitulo = !hay || titulo == null || string.IsNullOrEmpty(titulo.id);
        T(cabecera, "Titulo", tx + 4f, 124f, 760f, 36f, FRotulo, 28f, sinTitulo ? Mute : Accent,
            !hay ? "" : sinTitulo ? L10n.T("profile.title.none") : L10n.Source(titulo.nombre, "profile."), TextAlignmentOptions.MidlineLeft, 6f);
        T(cabecera, "Ayuda", tx + 4f, 166f, 760f, 26f, FTexto, 18f, Mute, "Perfil de otro jugador · solo para mirar", TextAlignmentOptions.MidlineLeft);
        if (!hay) return;

        // El nivel y el puesto en el ranking, a la derecha (CA2).
        int nivel = Nivel();
        int rango = EmblemaDeNivel.RangoDe(nivel);
        Color color = ColorRareza((PerfilDatos.Rareza)(rango + 1));
        RectTransform caja2 = Place(Node("Nivel", cabecera), CuerpoW - d.z - 24f - 600f, 32f, 600f, 166f);
        Image(caja2, Rounded, new Color(0f, 0f, 0f, DarkAlpha(0.62f)), 10f);
        Borde(caja2, 10f, WithAlpha(color, 0.6f));
        RectTransform logo = Place(Node("Logo", caja2), 0f, 0f, 140f, 166f);
        EmblemaDeNivel.Dibujar(logo, 70f, 83f, rango, color);
        T(caja2, "Rotulo", 140f, 16f, 200f, 22f, FRotulo, 16f, color, "Nivel de jugador", TextAlignmentOptions.MidlineLeft, 14f, true);
        Text(Place(Node("Valor", caja2), 136f, 38f, 200f, 76f), FTitulo, 72f, Ink, TextAlignmentOptions.MidlineLeft, 2f).text = PerfilDatos.Numero(nivel);
        T(caja2, "Rango", 140f, 114f, 200f, 28f, FTitulo, 24f, color, NombreRango(rango), TextAlignmentOptions.MidlineLeft, 6f, true);

        Image(Place(Node("Separador", caja2), 350f, 24f, 1f, 118f), null, White(0.12f));
        T(caja2, "RotuloRanking", 374f, 16f, 210f, 22f, FRotulo, 16f, Accent, "Puesto en el ranking", TextAlignmentOptions.MidlineLeft, 14f, true);
        Puesto(caja2, 374f, 46f, perfil.rank_bajas, L10n.Source("Bajas", "profile."));
        Puesto(caja2, 374f, 98f, perfil.rank_logros, L10n.Source("Logros", "profile."));
    }

    private static void Puesto(RectTransform padre, float x, float y, int puesto, string categoria)
    {
        Text(Place(Node("Puesto", padre), x, y, 100f, 46f), FTitulo, 40f, puesto > 0 ? Ink : Mute, TextAlignmentOptions.MidlineLeft).text =
            puesto > 0 ? "#" + PerfilDatos.Numero(puesto) : "—";
        TextMeshProUGUI c = Text(Place(Node("Categoria", padre), x + 104f, y, 120f, 46f), FRotulo, 16f, Soft, TextAlignmentOptions.MidlineLeft, 10f, true);
        c.text = categoria;
    }

    private static string NombreRango(int r) => r switch { 1 => "Guarda", 2 => "Maquinista", 3 => "Jefe de estación", _ => "Pasajero" };

    private PerfilDatos.Cosmetico Equipado(Tipo tipo, string clave, string inicial)
    {
        string id = perfil?.Valor(clave);
        return (id != null ? PerfilDatos.Buscar(tipo, id) : null) ?? PerfilDatos.Buscar(tipo, inicial);
    }

    private int Nivel()
    {
        string v = perfil?.Valor("nivel.jugador");
        int i = v != null ? v.IndexOf('|') : -1;
        return v != null && int.TryParse(i >= 0 ? v.Substring(0, i) : v, out int n) ? n : NivelDeJugador.Inicial;
    }

    // ---------- Estadísticas (CA2) ----------

    private long C(string clave) => perfil.Contador(clave);

    private void DibujarEstadisticas()
    {
        const string T0 = "estadisticas.tactico.", D0 = "estadisticas.deathmatch.", I0 = "estadisticas.invasion.";
        long partidas = C(T0 + "partidas") + C(D0 + "partidas"), victorias = C(T0 + "victorias") + C(D0 + "victorias");
        long bajas = C(T0 + "bajas") + C(D0 + "bajas"), muertes = C(T0 + "muertes") + C(D0 + "muertes");
        long asistencias = C(T0 + "asistencias") + C(D0 + "asistencias");

        // General: Táctico y Deathmatch juntos, como en el Perfil propio (US 221).
        RectTransform general = Place(Node("General", cuerpo), 0f, 0f, CuerpoW, 150f);
        Image(general, Rounded, PanelColor, 8f);
        Borde(general, 8f, White(0.06f));
        T(general, "Rotulo", 28f, 14f, 700f, 22f, FRotulo, 18f, Accent, "General · Táctico y Deathmatch", TextAlignmentOptions.MidlineLeft, 16f, true);
        string sinDatos = L10n.Source("Sin datos", "profile.");
        (string nombre, string valor)[] datos =
        {
            ("Partidas", N(partidas)),
            ("Victorias", N(victorias)),
            ("% de victorias", partidas > 0 ? Mathf.RoundToInt(100f * victorias / partidas) + " %" : sinDatos),
            ("Bajas", N(bajas)),
            ("Muertes", N(muertes)),
            ("Asistencias", N(asistencias)),
            ("KDA*", partidas > 0 ? Kda(bajas, asistencias, muertes) : sinDatos),
        };
        float paso = (CuerpoW - 56f) / datos.Length;
        for (int i = 0; i < datos.Length; i++)
        {
            bool vacio = datos[i].valor == sinDatos;
            Text(Place(Node("Valor" + i, general), 28f + i * paso, 44f, paso - 10f, 56f), FTitulo, vacio ? 30f : 52f, vacio ? Mute : Ink,
                TextAlignmentOptions.MidlineLeft).text = datos[i].valor;
            T(general, "Nombre" + i, 30f + i * paso, 104f, paso - 10f, 22f, FRotulo, 15f, Mute, datos[i].nombre, TextAlignmentOptions.MidlineLeft, 12f, true);
        }

        float w = (CuerpoW - 3f * 18f) / 4f, y = 168f, h = CuerpoH - y - 36f;
        Tarjeta(0, w, y, h, "Táctico", Hex(0xF29A38), C(T0 + "partidas") == 0, new[]
        {
            ("Partidas", N(C(T0 + "partidas"))),
            ("Victorias", N(C(T0 + "victorias"))),
            ("KDA*", C(T0 + "partidas") > 0 ? Kda(C(T0 + "bajas"), C(T0 + "asistencias"), C(T0 + "muertes")) : "—"),
            ("Rondas ganadas", N(C(T0 + "rondas_ganadas"))),
            ("Plantados", N(C(T0 + "plantados"))),
            ("MVP", N(C(T0 + "mvp"))),
        });
        Tarjeta(1, w, y, h, "Deathmatch", Hex(0xFF4B4B), C(D0 + "partidas") == 0, new[]
        {
            ("Partidas", N(C(D0 + "partidas"))),
            ("Primer puesto", N(C(D0 + "victorias"))),
            ("Bajas", N(C(D0 + "bajas"))),
            ("KDA*", C(D0 + "partidas") > 0 ? Kda(C(D0 + "bajas"), C(D0 + "asistencias"), C(D0 + "muertes")) : "—"),
            ("Mejor racha", N(C(D0 + "mejor_racha"))),
        });
        long facil = C("zombie.record.facil"), normal = C("zombie.record.normal"), dificil = C("zombie.record.dificil");
        Tarjeta(2, w, y, h, "Invasión", Hex(0x7DE05A), C(I0 + "partidas") == 0 && facil + normal + dificil == 0, new[]
        {
            ("Partidas", N(C(I0 + "partidas"))),
            ("Mundo salvado", N(C(I0 + "victorias"))),
            ("Invasores eliminados", N(C(I0 + "bajas"))),
            ("Récord F · N · D", $"{Oleada(facil)} · {Oleada(normal)} · {Oleada(dificil)}"),
            ("Jefes derrotados", N(C(I0 + "jefes"))),
            ("En cooperativo", N(C(I0 + "coop.partidas"))),
        });
        Logros(3, w, y, h);

        TextMeshProUGUI nota = T(cuerpo, "Nota", 0f, CuerpoH - 28f, CuerpoW, 26f, FTexto, 18f, Mute,
            "* KDA = (bajas + asistencias) ÷ muertes; sin muertes, ÷ 1. Cuentan las partidas terminadas.");
        nota.textWrappingMode = TextWrappingModes.Normal;
    }

    private RectTransform Tarjeta(int col, float w, float y, float h, string nombre, Color color, bool vacia, (string nombre, string valor)[] filas)
    {
        RectTransform t = Place(Node(nombre, cuerpo), col * (w + 18f), y, w, h);
        Image(t, Rounded, PanelColor, 8f);
        Borde(t, 8f, White(0.06f));
        Image(Place(Node("Color", t), 0f, 0f, w, 4f), null, color);
        T(t, "Titulo", 22f, 14f, w - 44f, 40f, FTitulo, 32f, color, nombre, TextAlignmentOptions.MidlineLeft, 4f, true);
        if (vacia)
        {
            TextMeshProUGUI m = T(t, "SinDatos", 22f, 72f, w - 44f, 120f, FTexto, 21f, Mute, "Todavía no terminó ninguna partida de este modo.", TextAlignmentOptions.TopLeft);
            m.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }
        float paso = Mathf.Min(40f, (h - 76f) / filas.Length);
        for (int j = 0; j < filas.Length; j++)
        {
            float fy = 64f + j * paso;
            T(t, "Dato" + j, 22f, fy, w * 0.55f, paso - 4f, FTexto, 19f, Soft, filas[j].nombre);
            TextMeshProUGUI v = Text(Place(Node("Valor" + j, t), w * 0.5f, fy, w * 0.5f - 22f, paso - 4f), FTitulo, 26f, Ink, TextAlignmentOptions.MidlineRight);
            v.text = filas[j].valor;
            if (j < filas.Length - 1) Image(Place(Node("Linea" + j, t), 22f, fy + paso - 3f, w - 44f, 1f), null, White(0.06f));
        }
        return t;
    }

    // Los logros que tiene: la cantidad y un cuadradito por logro, en el orden del catálogo (como en el ranking de logros).
    private void Logros(int col, float w, float y, float h)
    {
        RectTransform t = Place(Node("Logros", cuerpo), col * (w + 18f), y, w, h);
        Image(t, Rounded, PanelColor, 8f);
        Borde(t, 8f, White(0.06f));
        Image(Place(Node("Color", t), 0f, 0f, w, 4f), null, Accent);
        T(t, "Titulo", 22f, 14f, w - 44f, 40f, FTitulo, 32f, Accent, "Logros", TextAlignmentOptions.MidlineLeft, 4f, true);
        var catalogo = LogrosCometas.Catalogo;
        int tiene = 0;
        foreach (var l in catalogo) if (perfil.Tiene(l.id)) tiene++;
        Text(Place(Node("Cantidad", t), 22f, 60f, w - 44f, 60f), FTitulo, 56f, Ink, TextAlignmentOptions.MidlineLeft).text =
            tiene + "<size=50%> / " + catalogo.Length + "</size>";
        T(t, "Obtenidos", 24f, 120f, w - 44f, 26f, FTexto, 19f, Mute, "logros obtenidos");
        int porFila = 7;
        float lado = Mathf.Floor((w - 44f - (porFila - 1) * 8f) / porFila);
        for (int k = 0; k < catalogo.Length; k++)
        {
            var l = catalogo[k];
            Color c = l.EsCometa ? Cometa.ColorDe((TipoCometa)l.tipo) : Accent;
            Img cuadro = Image(Place(Node(l.id, t), 22f + (k % porFila) * (lado + 8f), 160f + (k / porFila) * (lado + 8f), lado, lado),
                Rounded, perfil.Tiene(l.id) ? c : White(0.08f), 4f, true);
            // El nombre del logro al pasar el mouse, en el rótulo de abajo.
            var p = cuadro.gameObject.AddComponent<ShopPointerTarget>();
            string texto = L10n.Source(l.nombre, "profile.") + (perfil.Tiene(l.id) ? "" : " · " + L10n.T("profile.ach.pending"));
            p.Hovered = () => { var r = t.Find("Nombre del logro"); if (r != null) r.GetComponent<TextMeshProUGUI>().text = texto; };
        }
        TextMeshProUGUI pie = Text(Place(Node("Nombre del logro", t), 22f, h - 46f, w - 44f, 30f), FTexto, 18f, Soft, TextAlignmentOptions.MidlineLeft);
        pie.text = "";
        pie.overflowMode = TextOverflowModes.Ellipsis;
    }

    // ---------- Historial (US 374, CA5) ----------

    private void DibujarHistorial()
    {
        var sr = Scroll(cuerpo, 0f, 0f, CuerpoW, CuerpoH, out RectTransform lista);
        var partidas = new List<HistorialDePartidas.Partida>(perfil.partidas ?? new HistorialDePartidas.Partida[0]);
        float alto = ListaDeHistorial.Dibujar(lista, partidas, CuerpoW, L10n.T("profile.other.no_matches"));
        lista.sizeDelta = new Vector2(CuerpoW, Mathf.Max(CuerpoH, alto));
        sr.verticalNormalizedPosition = 1f;
    }

    private static string N(long n) => PerfilDatos.Numero((int)System.Math.Min(n, int.MaxValue));
    private static string Oleada(long o) => o > 0 ? o.ToString() : "—";
    private static string Kda(long bajas, long asistencias, long muertes) =>
        EstadisticasJugador.Kda((int)bajas, (int)asistencias, (int)muertes).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).Replace(".", ",");

    // ================= Abrir y cerrar en una partida =================

    private void BloquearPartida()
    {
        cursorAntes = Cursor.lockState;
        cursorVisibleAntes = Cursor.visible;
        conPausa = PauseMenu.Instance != null;
        if (conPausa) { pausaAntes = PauseMenu.Instance.AllowPause; PauseMenu.Instance.AllowPause = false; } // Esc cierra el perfil
        Bloquear(FindObjectsByType<CameraLook>(FindObjectsSortMode.None));
        Bloquear(FindObjectsByType<MeleeAttack>(FindObjectsSortMode.None));
        Bloquear(FindObjectsByType<Pistola>(FindObjectsSortMode.None));
        Bloquear(FindObjectsByType<Mitre>(FindObjectsSortMode.None));
        Bloquear(FindObjectsByType<ArmaDeFuego>(FindObjectsSortMode.None));
        Bloquear(FindObjectsByType<WeaponSwitcher>(FindObjectsSortMode.None));
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Bloquear<T>(T[] componentes) where T : Behaviour
    {
        foreach (T c in componentes)
            if (c != null && c.enabled) { c.enabled = false; bloqueados.Add(c); }
    }

    public void Cerrar()
    {
        if (actual == this) actual = null;
        cuadroCerrado = Time.frameCount;
        if (LogrosOnline.Instancia != null && alLlegar != null) LogrosOnline.Instancia.CancelarPerfil(alLlegar);
        foreach (Behaviour c in bloqueados) if (c != null) c.enabled = true;
        bloqueados.Clear();
        Cursor.lockState = cursorAntes;
        Cursor.visible = cursorVisibleAntes;
        // La pausa vuelve a como estaba, un cuadro después: así el mismo Esc no la abre.
        if (conPausa && PauseMenu.Instance != null) PerfilAjenoPausa.Devolver(pausaAntes);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (actual == this) actual = null;
    }
}

// Devuelve la pausa al cuadro siguiente de cerrar el perfil (el Esc que lo cerró no tiene que abrirla).
internal sealed class PerfilAjenoPausa : MonoBehaviour
{
    private bool valor;
    private int cuadro;

    public static void Devolver(bool valor)
    {
        var go = new GameObject("DevolverPausa");
        var d = go.AddComponent<PerfilAjenoPausa>();
        d.valor = valor;
        d.cuadro = Time.frameCount;
    }

    private void Update()
    {
        if (Time.frameCount == cuadro) return;
        if (PauseMenu.Instance != null) PauseMenu.Instance.AllowPause = valor;
        Destroy(gameObject);
    }
}
