using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static ShopUIKit;
using static PerfilUI;
using Img = UnityEngine.UI.Image;
using EstadoRecompensa = PaseDeBatalla.EstadoRecompensa;

// F32: la pestaña Pase de batalla del menú de la tienda (US 385).
// - US 363: arriba la temporada, los días que faltan, el nivel y la barra de experiencia. Los 40 niveles en 4 páginas de
//   10, con flechas; abre en la página del nivel actual. Cada recompensa con su cosmético, su tipo, sus cristales, si es
//   gratis o del pase, y su estado (bloqueada, lista o reclamada). Al pasar el mouse se ve más grande a la derecha.
//   Clic en una lista la reclama; también "Reclamar todo".
// - US 364: "Comprar pase · 950 cristales", con confirmación; si no alcanzan, lleva a la pestaña Cristales.
// - US 365: la pestaña Desafíos, con el avance y la experiencia de cada uno.
// - US 366: en la última semana, "Termina en N días".
public class PantallaPase : PantallaBase
{
    private const float CartasX = 60f, CartasY = 312f, AnchoCartas = 1310f, CartaH = 300f, Gap = 16f;
    private const float PanelX = 1390f, PanelW = 470f;

    private TextMeshProUGUI temporada, dias, nivel, cuenta, pagina;
    private RectTransform barra, relleno, cartas, panel, vistaDesafios, vistaRecompensas;
    private Boton botonComprar, botonReclamarTodo;
    private TextMeshProUGUI[] pestanasVista = new TextMeshProUGUI[2];
    private Img[] lineasVista = new Img[2];
    private int paginaActual = -1, vista;
    private int elegido = 1;

    public PantallaPase(MenuRiftwalker menu, RectTransform parent) : base(menu, parent, "PaseDeBatalla")
    {
        BarraDeTienda(PestanaTienda.Pase, "Temporada");
        PestanasDeTienda.RecompensasDelPase = () => PaseDeBatalla.ParaReclamar; // US 363, CA5: los avisos del menú
        ArmarCabecera();
        ArmarVistas();
    }

    // ================= Cabecera (US 363, CA1) =================

    private void ArmarCabecera()
    {
        RectTransform cab = Place(Node("Cabecera", rect), 60f, 120f, 1800f, 120f);
        Image(cab, Rounded, PanelColor, 10f);
        Borde(cab, 8f, White(0.08f));
        Image(Place(Node("Linea", cab), 0f, 0f, 4f, 120f), null, Accent);
        temporada = T(cab, "Temporada", 30f, 14f, 700f, 52f, FTitulo, 46f, Ink, "", TextAlignmentOptions.MidlineLeft, 3f, true);
        dias = T(cab, "Dias", 32f, 66f, 700f, 26f, FRotulo, 18f, Mute, "", TextAlignmentOptions.MidlineLeft, 12f, true);
        nivel = T(cab, "Nivel", 560f, 18f, 300f, 40f, FTitulo, 34f, Accent, "", TextAlignmentOptions.MidlineLeft, 3f, true);
        cuenta = T(cab, "Cuenta", 880f, 22f, 300f, 34f, FTitulo, 26f, Soft, "", TextAlignmentOptions.MidlineRight, 2f);
        barra = Place(Node("Barra", cab), 560f, 66f, 620f, 12f);
        Image(barra, Rounded, White(0.1f), 6f);
        relleno = Place(Node("Relleno", barra), 0f, 0f, 0f, 12f);
        Image(relleno, Rounded, Accent, 6f);
        botonReclamarTodo = CrearBoton(cab, 1210f, 28f, 250f, 64f, "Reclamar todo", false, ReclamarTodo, 24f);
        botonComprar = CrearBoton(cab, 1480f, 28f, 300f, 64f, "Comprar pase", true, Comprar, 24f);
    }

    // ================= Vistas: Recompensas y Desafíos =================

    private void ArmarVistas()
    {
        string[] nombres = { "Recompensas", "Desafíos" };
        for (int i = 0; i < 2; i++)
        {
            int indice = i;
            RectTransform r = Place(Node("Vista " + nombres[i], rect), 60f + i * 220f, 252f, 210f, 44f);
            pestanasVista[i] = T(r, "Texto", 0f, 0f, 210f, 40f, FTitulo, 28f, Soft, nombres[i], TextAlignmentOptions.MidlineLeft, 4f, true);
            lineasVista[i] = Image(Place(Node("Linea", r), 0f, 41f, 120f, 3f), null, Accent);
            var p = Clickeable(r, () => { vista = indice; Refrescar(); });
            p.Hovered = () => { if (vista != indice) pestanasVista[indice].color = Accent; };
            p.Exited = () => { if (vista != indice) pestanasVista[indice].color = Soft; };
        }

        vistaRecompensas = Place(Node("Recompensas", rect), 0f, 0f, 1920f, 1080f);
        pagina = T(vistaRecompensas, "Pagina", 520f, 256f, 600f, 36f, FRotulo, 20f, Mute, "", TextAlignmentOptions.MidlineLeft, 12f, true);
        Flecha(vistaRecompensas, 1250f, "‹", -1);
        Flecha(vistaRecompensas, 1316f, "›", +1);
        cartas = Place(Node("Cartas", vistaRecompensas), CartasX, CartasY, AnchoCartas, 2f * CartaH + Gap);
        RectTransform marco = Place(Node("Detalle", vistaRecompensas), PanelX, CartasY, PanelW, 2f * CartaH + Gap);
        Image(marco, Rounded, PanelColor, 10f);
        panel = Place(Node("Contenido", marco), 0f, 0f, PanelW, 2f * CartaH + Gap);

        vistaDesafios = Place(Node("Desafios", rect), 60f, CartasY, 1800f, 2f * CartaH + Gap);
    }

    private void Flecha(RectTransform padre, float x, string simbolo, int paso)
    {
        RectTransform r = Place(Node("Flecha" + paso, padre), x, 252f, 54f, 44f);
        Image(r, Rounded, PanelColor, 6f, true);
        Img borde = Borde(r, 6f, White(0.12f));
        TextMeshProUGUI t = T(r, "T", 0f, -3f, 54f, 44f, FTitulo, 40f, Soft, simbolo, TextAlignmentOptions.Center);
        var p = Clickeable(r, () => { paginaActual = Mathf.Clamp(paginaActual + paso, 0, PaseDeBatalla.Paginas - 1); Refrescar(); });
        p.Hovered = () => { t.color = Accent; if (borde != null) borde.color = Accent; };
        p.Exited = () => { t.color = Soft; if (borde != null) borde.color = White(0.12f); };
    }

    // ================= Datos =================

    public override void Refrescar()
    {
        base.Refrescar();
        int nivelActual = PaseDeBatalla.Nivel;
        if (paginaActual < 0) // CA2: abre en la página del nivel actual
        {
            paginaActual = Mathf.Clamp(Mathf.Max(0, nivelActual) / PaseDeBatalla.PorPagina, 0, PaseDeBatalla.Paginas - 1);
            if (nivelActual >= PaseDeBatalla.Niveles) paginaActual = PaseDeBatalla.Paginas - 1;
            elegido = Mathf.Clamp(nivelActual + 1, 1, PaseDeBatalla.Niveles);
        }

        temporada.text = PaseDeBatalla.NombreTemporada;
        int faltan = PaseDeBatalla.DiasQueFaltan;
        dias.text = PaseDeBatalla.UltimaSemana
            ? $"<color=#F29A38>{L10n.Source("Termina en", "profile.")} {faltan} {(faltan == 1 ? L10n.Source("día", "profile.") : L10n.Source("días", "profile."))}</color>"
            : $"{L10n.Source("Faltan", "profile.")} {faltan} {L10n.Source("días", "profile.")} · {L10n.Source("40 niveles", "profile.")} · {PaseDeBatalla.CristalesGratis} {L10n.Source("cristales gratis", "profile.")}, {PaseDeBatalla.CristalesConPase} {L10n.Source("con el pase", "profile.")}";
        bool tope = nivelActual >= PaseDeBatalla.Niveles;
        nivel.text = $"{L10n.Source("Nivel", "profile.")} {nivelActual}";
        cuenta.text = tope ? L10n.Source("Nivel máximo", "profile.") : $"{PerfilDatos.Numero(PaseDeBatalla.XpDelNivel)} / {PerfilDatos.Numero(PaseDeBatalla.XpPorNivel)}";
        relleno.sizeDelta = new Vector2(620f * PaseDeBatalla.XpDelNivel / PaseDeBatalla.XpPorNivel, 12f);

        // US 364, CA1 y CA4
        bool comprado = PaseDeBatalla.Comprado;
        int faltanCristales = PaseDeBatalla.Precio - PerfilDatos.Cristales;
        botonComprar.texto.text = comprado ? "Pase comprado" : $"Comprar pase · {PerfilDatos.Numero(PaseDeBatalla.Precio)}";
        botonComprar.Habilitar(!comprado);
        int listas = PaseDeBatalla.ParaReclamar;
        botonReclamarTodo.texto.text = listas > 0 ? $"Reclamar todo ({listas})" : "Reclamar todo";
        botonReclamarTodo.Habilitar(listas > 0);

        for (int i = 0; i < 2; i++)
        {
            pestanasVista[i].color = vista == i ? Ink : Soft;
            lineasVista[i].enabled = vista == i;
        }
        vistaRecompensas.gameObject.SetActive(vista == 0);
        vistaDesafios.gameObject.SetActive(vista == 1);
        if (vista == 0) DibujarRecompensas(); else DibujarDesafios();
    }

    // ================= Recompensas (US 363) =================

    private void DibujarRecompensas()
    {
        int desde = paginaActual * PaseDeBatalla.PorPagina + 1;
        pagina.text = $"{L10n.Source("Página", "profile.")} {paginaActual + 1} / {PaseDeBatalla.Paginas} · {L10n.Source("Niveles", "profile.")} {desde}–{desde + PaseDeBatalla.PorPagina - 1}";
        BorrarHijos(cartas);
        const int columnas = 5;
        float w = (AnchoCartas - (columnas - 1) * Gap) / columnas;
        for (int i = 0; i < PaseDeBatalla.PorPagina; i++)
            Carta(desde + i, (i % columnas) * (w + Gap), (i / columnas) * (CartaH + Gap), w);
        DibujarPanel(elegido);
    }

    private void Carta(int n, float x, float y, float w)
    {
        List<Premio> premios = PaseDeBatalla.Premios(n);
        Premio p = premios.Count > 0 ? premios[0] : null;
        EstadoRecompensa estado = PaseDeBatalla.EstadoDe(n);
        bool gratis = PaseDeBatalla.EsGratis(n);
        Color colorR = p != null ? ColorRareza(p.Rareza) : Mute;
        RectTransform carta = Place(Node("Nivel " + n, cartas), x, y, w, CartaH);
        Image(carta, Rounded, estado == EstadoRecompensa.Lista ? Over(WithAlpha(Accent, 0.14f), PanelColor) : PanelColor, 10f, true);

        float artH = 156f;
        if (p != null) p.DibujarArte(carta, 0f, 0f, w, artH, estado == EstadoRecompensa.Bloqueada || estado == EstadoRecompensa.SinPase);
        Image(Place(Node("Rareza", carta), 0f, artH, w, 4f), null, colorR);
        // El nivel arriba a la izquierda y "Gratis" o "Pase" arriba a la derecha.
        RectTransform chipNivel = Place(Node("ChipNivel", carta), 10f, 10f, 56f, 30f);
        Image(chipNivel, Rounded, new Color(0f, 0f, 0f, 0.75f), 6f);
        T(chipNivel, "T", 0f, 0f, 56f, 30f, FTitulo, 24f, Ink, n.ToString(), TextAlignmentOptions.Center);
        RectTransform chipTipo = Place(Node("ChipTipo", carta), w - 86f, 10f, 76f, 26f);
        Image(chipTipo, Rounded, gratis ? Ok : Accent, 6f);
        T(chipTipo, "T", 0f, 0f, 76f, 26f, FRotulo, 14f, AccentInk, gratis ? "Gratis" : "Pase", TextAlignmentOptions.Center, 10f, true);
        if (estado == EstadoRecompensa.Bloqueada || estado == EstadoRecompensa.SinPase) Candado(carta, w / 2f, artH / 2f, 34f, Soft);

        if (p != null)
        {
            TextMeshProUGUI nombre = T(carta, "Nombre", 14f, artH + 10f, w - 28f, 30f, FTitulo, 24f, Ink, p.Nombre, TextAlignmentOptions.MidlineLeft, 2f);
            nombre.overflowMode = TextOverflowModes.Ellipsis;
            T(carta, "Tipo", 14f, artH + 40f, w - 28f, 20f, FRotulo, 14f, colorR, p.NombreTipo, TextAlignmentOptions.MidlineLeft, 10f, true);
        }

        string pie = estado == EstadoRecompensa.Reclamada ? "Reclamada" : estado == EstadoRecompensa.Lista ? "Reclamar"
            : estado == EstadoRecompensa.SinPase ? "Con el pase" : $"Nivel {n}";
        Color tinta = estado == EstadoRecompensa.Reclamada ? Ok : estado == EstadoRecompensa.Lista ? Accent : Mute;
        T(carta, "Estado", 14f, CartaH - 40f, w - 28f, 30f, FTitulo, 22f, tinta, pie, TextAlignmentOptions.MidlineLeft, 4f, true);

        Img borde = Borde(carta, 8f, elegido == n ? Ink : estado == EstadoRecompensa.Lista ? Accent : White(0.08f));
        var c = Clickeable(carta, () =>
        {
            elegido = n;
            if (PaseDeBatalla.EstadoDe(n) == EstadoRecompensa.Lista) Reclamar(n); else Refrescar();
        });
        c.Hovered = () => DibujarPanel(n); // CA4: con el mouse encima se ve más grande
        c.Exited = () => DibujarPanel(elegido);
    }

    private void DibujarPanel(int n)
    {
        BorrarHijos(panel);
        List<Premio> premios = PaseDeBatalla.Premios(n);
        if (premios.Count == 0) return;
        Premio p = premios[0];
        EstadoRecompensa estado = PaseDeBatalla.EstadoDe(n);
        float h = panel.sizeDelta.y;
        Borde(panel, 8f, WithAlpha(ColorRareza(p.Rareza), 0.6f));
        T(panel, "Nivel", 24f, 20f, PanelW - 48f, 26f, FRotulo, 18f, Accent,
            $"{L10n.Source("Nivel", "profile.")} {n} · {(PaseDeBatalla.EsGratis(n) ? L10n.Source("Gratis", "profile.") : L10n.Source("Pase de batalla", "profile."))}",
            TextAlignmentOptions.MidlineLeft, 12f, true);
        float artH = p.cosmetico != null && p.cosmetico.tipo == PerfilDatos.Tipo.Avatar ? 300f : 220f;
        p.DibujarArte(panel, 24f, 54f, PanelW - 48f, artH);
        float y = 54f + artH + 16f;
        T(panel, "Tipo", 24f, y, PanelW - 48f, 22f, FRotulo, 17f, ColorRareza(p.Rareza), $"{p.NombreTipo} · {NombreRareza(p.Rareza)}", TextAlignmentOptions.MidlineLeft, 12f, true);
        TextMeshProUGUI nombre = T(panel, "Nombre", 24f, y + 24f, PanelW - 48f, 80f, FTitulo, 36f, Ink, p.Nombre, TextAlignmentOptions.TopLeft, 2f, true);
        nombre.textWrappingMode = TextWrappingModes.Normal;
        string ayuda = estado == EstadoRecompensa.Reclamada ? "Ya está en tu colección del Perfil."
            : estado == EstadoRecompensa.Lista ? "Hacé clic en la carta para reclamarla."
            : estado == EstadoRecompensa.SinPase ? "Se desbloquea con el pase de batalla."
            : $"Se desbloquea al llegar al nivel {n}.";
        T(panel, "Ayuda", 24f, h - 56f, PanelW - 48f, 30f, FTexto, 20f, estado == EstadoRecompensa.Lista ? Accent : Mute, ayuda, TextAlignmentOptions.MidlineLeft);
    }

    private void Reclamar(int n)
    {
        List<string> nombres = PaseDeBatalla.Reclamar(n);
        if (nombres != null && nombres.Count > 0) menu.Avisar($"{L10n.Source("Reclamaste", "profile.")}: {string.Join(" y ", nombres)}");
        Refrescar();
    }

    private void ReclamarTodo()
    {
        List<string> nombres = PaseDeBatalla.ReclamarTodo();
        if (nombres.Count > 0) menu.Avisar($"{L10n.Source("Reclamaste", "profile.")} {nombres.Count} {(nombres.Count == 1 ? L10n.Source("recompensa", "profile.") : L10n.Source("recompensas", "profile."))}");
        Refrescar();
    }

    // ================= Comprar (US 364) =================

    private void Comprar()
    {
        if (PaseDeBatalla.Comprado) return;
        if (PerfilDatos.Cristales < PaseDeBatalla.Precio)
        {
            menu.Avisar($"Te faltan {PerfilDatos.Numero(PaseDeBatalla.Precio - PerfilDatos.Cristales)} cristales para el pase");
            menu.AbrirTienda(PestanaTienda.Cristales); // CA1
            return;
        }
        menu.Confirmar("Pase de batalla", $"¿Comprar el pase de la {PaseDeBatalla.NombreTemporada.ToLowerInvariant()}?",
            $"Cuesta {PerfilDatos.Numero(PaseDeBatalla.Precio)} cristales y desbloquea las 32 recompensas de pago (también las de los niveles que ya pasaste). A lo largo de los niveles te devuelve cristales: 550 en total con el pase, contra 100 sin él. Compra simulada: no se cobra dinero real.",
            "Comprar", () => PaseDeBatalla.Comprar(error =>
            {
                menu.Avisar(error ?? "¡Pase comprado! Las recompensas de pago de tus niveles ya están para reclamar.");
                Refrescar();
            }));
    }

    // ================= Desafíos (US 365) =================

    private void DibujarDesafios()
    {
        BorrarHijos(vistaDesafios);
        var lista = PaseDeBatalla.Desafios;
        const int columnas = 2;
        float w = (1800f - Gap) / columnas, h = 54f;
        int filas = (lista.Count + columnas - 1) / columnas;
        float paso = Mathf.Min(h + 6f, (vistaDesafios.sizeDelta.y) / filas);
        for (int i = 0; i < lista.Count; i++)
            Desafio(lista[i], (i / filas) * (w + Gap), (i % filas) * paso, w, paso - 6f);
    }

    private void Desafio(PaseDeBatalla.Desafio d, float x, float y, float w, float h)
    {
        int avance = PaseDeBatalla.Avance(d.id), cobradas = PaseDeBatalla.EtapasCobradas(d.id);
        bool completo = cobradas >= d.metas.Length;
        int etapa = Mathf.Min(cobradas, d.metas.Length - 1);
        int meta = d.metas[etapa];
        RectTransform fila = Place(Node(d.id, vistaDesafios), x, y, w, h);
        Image(fila, Rounded, completo ? Over(WithAlpha(Ok, 0.1f), PanelColor) : PanelColor, 6f);
        TextMeshProUGUI nombre = T(fila, "Nombre", 16f, 2f, w - 420f, h * 0.58f, FTitulo, 22f, completo ? Ok : Ink,
            d.Nombre(etapa), TextAlignmentOptions.BottomLeft, 2f);
        nombre.overflowMode = TextOverflowModes.Ellipsis;
        string etapas = d.metas.Length > 1 ? $" · {L10n.Source("Etapa", "profile.")} {Mathf.Min(cobradas + 1, d.metas.Length)} / {d.metas.Length}" : "";
        T(fila, "Modo", 16f, h * 0.58f, w - 420f, h * 0.4f, FRotulo, 13f, Mute, d.modo + etapas, TextAlignmentOptions.TopLeft, 10f, true);
        float bx = w - 390f, bw = 230f;
        RectTransform barraD = Place(Node("Barra", fila), bx, h / 2f - 4f, bw, 8f);
        Image(barraD, Rounded, White(0.1f), 4f);
        float k = completo ? 1f : Mathf.Clamp01(avance / (float)meta);
        Image(Place(Node("Relleno", barraD), 0f, 0f, bw * k, 8f), Rounded, completo ? Ok : Accent, 4f);
        T(fila, "Avance", bx, 0f, bw, h / 2f - 4f, FRotulo, 13f, Soft,
            completo ? L10n.Source("Completado", "profile.") : $"{PerfilDatos.Numero(Mathf.Min(avance, meta))} / {PerfilDatos.Numero(meta)}", TextAlignmentOptions.BottomRight, 4f);
        T(fila, "Xp", w - 150f, 0f, 134f, h, FTitulo, 24f, completo ? Mute : Accent, $"+{PerfilDatos.Numero(d.xp[etapa])} XP", TextAlignmentOptions.MidlineRight, 2f);
    }
}
