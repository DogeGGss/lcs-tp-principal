using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using static ShopUIKit;
using Img = UnityEngine.UI.Image;

// Buscar partida en la pantalla de elegir modo (F28: US 274 y US 275; diseño en la guía, #buscar-partida). En Táctico y
// Deathmatch, «Buscar partida» es lo principal: grande, del color del modo, con un pulso y un brillo que lo recorre. La
// sala privada (crear sala y unirse con código) queda abajo, más chica. Mientras busca, el panel de la búsqueda reemplaza
// a los botones (el tiempo, cuántos hay, los 8 lugares, la regla para arrancar y «Cancelar») y a la derecha aparece un
// radar del color del modo, con un punto por jugador. Sin conexión o con una versión vieja, el panel de error. La
// búsqueda y el arranque automático (US 276) están en Multijugador.
public partial class ModeSelectScreen
{
    // Lo que ocupa el lugar de los botones del andén.
    private enum Bloque { Acciones, Sala, Busqueda, Error }

    private class Busqueda
    {
        public Bloque bloque = Bloque.Acciones;
        // Acciones: el botón grande, la cuenta y la sala privada.
        public RectTransform acciones, buscar, pulso, brillo, aguja;
        public UIRing puntoCuenta;
        public TextMeshProUGUI cuenta;
        // El panel de la búsqueda.
        public RectTransform panel, progreso, progresoLleno;
        public TextMeshProUGUI titulo, reloj, grande, datos, pie, salir;
        public readonly TextMeshProUGUI[] lugares = new TextMeshProUGUI[8];
        public readonly UIRing[] puntos = new UIRing[8];
        // El panel de error.
        public RectTransform error, reintentar, launcher;
        public TextMeshProUGUI errorTitulo, errorTexto;
    }

    private static readonly Color Oscuro = new Color(0.067f, 0.067f, 0.067f, 1f); // texto sobre el color del modo (#111)
    private const float AnchoBuscar = 720f, AltoBuscar = 124f, AnchoPanel = 860f;

    // El radar: uno solo, del color del modo que busca. Los puntos son los otros jugadores (vos, en el centro).
    private static readonly Vector2[] LugarRadar =
    {
        new Vector2(-170f, -110f), new Vector2(150f, -160f), new Vector2(200f, 110f), new Vector2(-120f, 150f),
        new Vector2(10f, -230f), new Vector2(-230f, 20f), new Vector2(240f, -30f)
    };
    private RectTransform radar, barrido;
    private CanvasGroup radarGroup;
    private readonly List<UIRing> radarPulsos = new List<UIRing>(), radarColor = new List<UIRing>();
    private readonly List<UIPolyline> radarTrazos = new List<UIPolyline>();
    private readonly List<RectTransform> radarJugadores = new List<RectTransform>();
    private readonly List<TextMeshProUGUI> radarNombres = new List<TextMeshProUGUI>();
    private int radarModo = -1;

    // ================= Armado =================

    // «Buscar partida», la cuenta con la que se busca y, abajo, la sala privada (US 274, CA1 y CA2).
    private void BuildBuscar(Detail d, int mode, Color c, float y)
    {
        Busqueda b = d.busqueda;
        b.acciones = Place(Node("Acciones", d.rect), 0f, y, 900f, 420f);

        // El pulso, detrás del botón: un borde del color del modo que se agranda y se apaga.
        b.pulso = Place(Node("Pulso", b.acciones), 0f, 0f, AnchoBuscar, AltoBuscar);
        Image(b.pulso, rounded, WithAlpha(c, 0f), 14f);
        CenterPivot(b.pulso);

        b.buscar = Place(Node("BotonBuscarPartida", b.acciones), 0f, 0f, AnchoBuscar, AltoBuscar);
        Image(b.buscar, rounded, c, 12f, true);
        b.buscar.gameObject.AddComponent<UnityEngine.UI.RectMask2D>(); // el brillo no se sale del botón
        b.brillo = Place(Node("Brillo", b.buscar), -160f, 0f, 120f, AltoBuscar);
        Image(b.brillo, BrilloSprite(), White(FadeAlpha(0.45f))).raycastTarget = false;

        // Ícono de radar con la aguja que gira.
        RectTransform icono = Group("Icono", b.buscar, 62f, 62f);
        Circle(icono, 0f, 0f, 30f, Oscuro, 4f);
        Circle(icono, 0f, 0f, 17f, WithAlpha(Oscuro, 0.55f), 3f);
        b.aguja = Group("Aguja", icono, 0f, 0f);
        Line(b.aguja, Oscuro, 4f, Vector2.zero, new Vector2(24f, 0f));
        Text(Place(Node("Titulo", b.buscar), 118f, 20f, 520f, 64f), displayFont, 60f, Oscuro, TextAlignmentOptions.MidlineLeft, 5f, true).text = "Buscar partida";
        Text(Place(Node("Detalle", b.buscar), 120f, 84f, 520f, 24f), labelFont, 21f, WithAlpha(Oscuro, 0.72f), TextAlignmentOptions.MidlineLeft, 12f, true).text =
            Titles[mode] + " · con jugadores de tu región";
        UIPolyline flecha = Line(b.buscar, Oscuro, 5f, new Vector2(646f, 62f), new Vector2(690f, 62f));
        flecha.roundJoins = true;
        UIPolyline punta = Line(b.buscar, Oscuro, 5f, new Vector2(673f, 45f), new Vector2(690f, 62f), new Vector2(673f, 79f));
        punta.roundJoins = true;
        var boton = b.buscar.gameObject.AddComponent<UnityEngine.UI.Button>();
        boton.transition = UnityEngine.UI.Selectable.Transition.None;
        boton.onClick.AddListener(() => Buscar(mode));
        b.buscar.gameObject.AddComponent<ButtonHoverAnimation>();
        CenterPivot(b.buscar);

        b.puntoCuenta = Circle(b.acciones, 6f, 158f, 6f, Hex(0x3DDC97));
        b.cuenta = Text(Place(Node("Cuenta", b.acciones), 24f, 143f, 860f, 30f), bodyFont, 22f, White(0.7f), TextAlignmentOptions.MidlineLeft);

        Image(Place(Node("Linea", b.acciones), 0f, 204f, AnchoBuscar, 1f), null, White(FadeAlpha(0.12f)));
        Text(Place(Node("Privada", b.acciones), 0f, 226f, AnchoBuscar, 22f), labelFont, 20f, White(0.55f), TextAlignmentOptions.MidlineLeft, 30f, true).text =
            "Sala privada · con amigos";
        float x = ButtonAt(b.acciones, 0f, 262f, "Crear sala", false, c, () => CreateRoom(mode), 26f, 60f).x;
        ButtonAt(b.acciones, x + 14f, 262f, "Unirse con código", false, c, () => ShowJoin(mode), 26f, 60f);
    }

    // El panel de la búsqueda (US 275): reemplaza a los botones mientras busca.
    private void BuildPanelBusqueda(Detail d, int mode, Color c, float y)
    {
        Busqueda b = d.busqueda;
        b.panel = Place(Node("Busqueda", d.rect), 0f, y, AnchoPanel, 460f);
        Image(b.panel, rounded, new Color(0f, 0f, 0f, DarkAlpha(0.62f)), 12f, true);
        Border(b.panel, 8f, White(0.18f));
        Image(Place(Node("Barra", b.panel), 0f, 0f, AnchoPanel, 5f), null, c);
        b.titulo = Text(Place(Node("Titulo", b.panel), 30f, 26f, 520f, 26f), labelFont, 24f, c, TextAlignmentOptions.MidlineLeft, 24f, true);
        b.reloj = Text(Place(Node("Reloj", b.panel), 560f, 18f, 270f, 50f), monoFont, 46f, Ink, TextAlignmentOptions.MidlineRight);
        b.grande = Text(Place(Node("Grande", b.panel), 30f, 62f, 800f, 76f), displayFont, 72f, Ink, TextAlignmentOptions.MidlineLeft, 2f, true);
        b.datos = Text(Place(Node("Datos", b.panel), 30f, 142f, 800f, 24f), monoFont, 20f, White(0.62f), TextAlignmentOptions.MidlineLeft);
        for (int s = 0; s < 8; s++)
        {
            float lx = 30f + (s % 2) * 415f, ly = 182f + (s / 2) * 46f;
            Segment(b.panel, new Vector2(lx, ly + 45f), new Vector2(lx + 385f, ly + 45f), 1f, White(FadeAlpha(0.08f)));
            b.puntos[s] = Circle(b.panel, lx + 6f, ly + 23f, 6f, c);
            b.lugares[s] = Text(Place(Node("Lugar", b.panel), lx + 26f, ly, 360f, 46f), bodyFont, 24f, Ink, TextAlignmentOptions.MidlineLeft);
        }
        b.pie = Text(Place(Node("Pie", b.panel), 30f, 376f, 540f, 54f), bodyFont, 21f, White(0.72f), TextAlignmentOptions.TopLeft);
        b.pie.textWrappingMode = TextWrappingModes.Normal;
        b.progreso = Place(Node("Progreso", b.panel), 30f, 432f, 520f, 6f);
        Image(b.progreso, rounded, White(FadeAlpha(0.12f)), 3f);
        b.progresoLleno = Place(Node("Lleno", b.progreso), 0f, 0f, 0f, 6f);
        Image(b.progresoLleno, rounded, c, 3f);
        RectTransform salir = ButtonRect(b.panel, 0f, 0f, "Cancelar", false, c, () => CancelarBusqueda(mode), 26f, 60f);
        salir.anchoredPosition = new Vector2(AnchoPanel - 30f - salir.sizeDelta.x / 2f, -(378f + 30f));
        b.salir = salir.GetComponentInChildren<TextMeshProUGUI>();
        b.panel.gameObject.SetActive(false);
    }

    // Sin conexión o con una versión vieja (US 275, CA3): el panel en rojo, sin perder nada.
    private void BuildPanelError(Detail d, int mode, Color c, float y)
    {
        Busqueda b = d.busqueda;
        b.error = Place(Node("Error", d.rect), 0f, y, AnchoPanel, 280f);
        Image(b.error, rounded, new Color(0f, 0f, 0f, DarkAlpha(0.62f)), 12f, true);
        Border(b.error, 8f, WithAlpha(Bad, 0.5f));
        Image(Place(Node("Barra", b.error), 0f, 0f, AnchoPanel, 5f), null, Bad);
        Text(Place(Node("Rotulo", b.error), 30f, 26f, 520f, 26f), labelFont, 24f, Bad, TextAlignmentOptions.MidlineLeft, 24f, true).text = "Buscar partida";
        b.errorTitulo = Text(Place(Node("Titulo", b.error), 30f, 58f, 800f, 72f), displayFont, 64f, Ink, TextAlignmentOptions.MidlineLeft, 2f, true);
        b.errorTexto = Text(Place(Node("Texto", b.error), 30f, 134f, 780f, 64f), bodyFont, 24f, White(0.82f), TextAlignmentOptions.TopLeft);
        b.errorTexto.textWrappingMode = TextWrappingModes.Normal;
        b.reintentar = ButtonRect(b.error, 30f, 198f, "Volver a buscar", true, c, () => Buscar(mode), 26f, 60f);
        b.launcher = ButtonRect(b.error, 30f, 198f, "Abrir el launcher", true, c, () => AbrirLauncher(mode), 26f, 60f);
        float x = 30f + Mathf.Max(b.reintentar.sizeDelta.x, b.launcher.sizeDelta.x) + 14f;
        ButtonRect(b.error, x, 198f, "Volver", false, c, () => MostrarBloque(details[mode], Bloque.Acciones), 26f, 60f);
        b.error.gameObject.SetActive(false);
    }

    // El radar de la derecha, sobre el andén abierto (va debajo del detalle y arriba de la sombra).
    private void BuildRadar()
    {
        radar = Group("Radar", root, 1500f, 520f);
        radarGroup = radar.gameObject.AddComponent<CanvasGroup>();
        radarGroup.alpha = 0f;
        radarGroup.blocksRaycasts = false;
        foreach (float r in new[] { 320f, 220f, 120f }) radarColor.Add(Circle(radar, 0f, 0f, r, Color.white, 1f));
        for (int k = 0; k < 3; k++) radarPulsos.Add(Circle(radar, 0f, 0f, 320f, Color.white, 2f));
        barrido = Group("Barrido", radar, 0f, 0f);
        for (int k = 0; k < 6; k++)
        {
            float a = -k * 5f * Mathf.Deg2Rad;
            radarTrazos.Add(Line(barrido, Color.white, 4f, Vector2.zero, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 320f));
        }
        radarColor.Add(Circle(radar, 0f, 0f, 16f, Color.white));
        for (int k = 0; k < LugarRadar.Length; k++)
        {
            RectTransform j = Group("Jugador", radar, LugarRadar[k].x, LugarRadar[k].y);
            radarColor.Add(Circle(j, 0f, 0f, 13f, Color.white, 4f));
            Circle(j, 0f, 0f, 9f, Color.white);
            TextMeshProUGUI nombre = Text(Place(Node("Nombre", j), 24f, -14f, 260f, 28f), labelFont, 20f, White(0.8f), TextAlignmentOptions.MidlineLeft, 8f, true);
            radarJugadores.Add(j);
            radarNombres.Add(nombre);
            j.gameObject.SetActive(false);
        }
    }

    // Banda de luz para el brillo del botón: transparente a los costados y blanca en el medio.
    private static Sprite brilloSprite;
    private static Sprite BrilloSprite()
    {
        if (brilloSprite != null) return brilloSprite;
        var tex = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int x = 0; x < 64; x++)
        {
            float t = Mathf.Abs(x / 63f * 2f - 1f);
            tex.SetPixel(x, 0, new Color(1f, 1f, 1f, Mathf.SmoothStep(1f, 0f, t)));
        }
        tex.Apply();
        brilloSprite = Sprite.Create(tex, new Rect(0f, 0f, 64f, 1f), new Vector2(0.5f, 0.5f));
        return brilloSprite;
    }

    // ================= Acciones =================

    // US 274: busca partida del modo del andén.
    private void Buscar(int mode)
    {
        Detail d = details[mode];
        d.status.text = "";
        MatchSettings.Mode = (GameMode)mode;
        Multijugador.Instancia.BuscarPartida((GameMode)mode);
        MostrarBloque(d, Bloque.Busqueda);
    }

    // US 275, CA2: con «Cancelar» o Esc, hasta que empieza la partida.
    private void CancelarBusqueda(int mode)
    {
        Multijugador.Instancia.CancelarBusqueda();
        MostrarBloque(details[mode], Bloque.Acciones);
        details[mode].status.text = "Búsqueda cancelada.";
    }

    private void AbrirLauncher(int mode)
    {
        if (PantallaSinSesion.AbrirLauncher()) return;
        MostrarBloque(details[mode], Bloque.Acciones);
        details[mode].status.text = "No encontramos el launcher en esta PC: te abrimos la guía del juego para bajarlo.";
        Application.OpenURL(PantallaSinSesion.GuiaLauncher);
    }

    // Qué va en el lugar de los botones según cómo está Photon. El error queda hasta que el jugador elija.
    private void Refrescar(Detail d, int mode)
    {
        if (d.busqueda == null || d.busqueda.bloque == Bloque.Error) return;
        Multijugador net = Multijugador.Instancia;
        bool suya = Multijugador.EnSala && net.ModoSala == (GameMode)mode;
        if (net.Buscando || (suya && net.EnPartidaPublica)) MostrarBloque(d, Bloque.Busqueda);
        else if (suya) { MostrarBloque(d, Bloque.Sala); RefreshRoom(d, mode); }
        else MostrarBloque(d, Bloque.Acciones);
    }

    private void MostrarBloque(Detail d, Bloque bloque)
    {
        Busqueda b = d.busqueda;
        if (b == null) return;
        b.bloque = bloque;
        b.acciones.gameObject.SetActive(bloque == Bloque.Acciones);
        d.room.gameObject.SetActive(bloque == Bloque.Sala);
        b.panel.gameObject.SetActive(bloque == Bloque.Busqueda);
        b.error.gameObject.SetActive(bloque == Bloque.Error);
        if (bloque == Bloque.Acciones) PintarCuenta(d);
        else d.status.text = "";
        // La tecla de arriba dice qué hace Esc (Back).
        if (open < 0 || details[open] != d) return;
        string atras = bloque == Bloque.Busqueda ? "Cancelar búsqueda" : bloque == Bloque.Sala ? "Salir de la sala" : bloque == Bloque.Error ? "Volver" : "Volver a los modos";
        if (backText.text != atras) { backText.text = atras; PlaceBackKey(); }
    }

    private void MostrarError(Detail d, Multijugador.Fallo fallo)
    {
        Busqueda b = d.busqueda;
        bool version = fallo == Multijugador.Fallo.Version;
        b.errorTitulo.text = version ? "Hay una versión nueva" : fallo == Multijugador.Fallo.CuentaRepetida ? "Tu cuenta ya está jugando" : "Se cortó la conexión";
        b.errorTexto.text = version ? "Solo se juntan jugadores con la misma versión del juego. Actualizá desde el launcher y volvé a buscar."
            : fallo == Multijugador.Fallo.CuentaRepetida ? "Tu cuenta está en esa partida desde otra PC. Cerrá el juego ahí y volvé a buscar."
            : "Revisá tu internet y volvé a buscar. No perdiste nada: tu progreso está guardado.";
        b.reintentar.gameObject.SetActive(!version);
        b.launcher.gameObject.SetActive(version);
        MostrarBloque(d, Bloque.Error);
    }

    // Con qué cuenta, en qué región y con qué versión se busca (US 274, CA2 y CA3).
    private void PintarCuenta(Detail d)
    {
        LogrosOnline online = LogrosOnline.Instancia;
        bool cuenta = online != null && online.Cuenta == ReglasCuenta.Estado.ConCuenta;
        string nombre = PlayerProfile.HasName ? PlayerProfile.Name : "Jugador";
        d.busqueda.puntoCuenta.gameObject.SetActive(cuenta);
        d.busqueda.cuenta.text = (cuenta ? $"Con tu cuenta <color=#FFFFFF>{nombre}</color>" : $"Como <color=#FFFFFF>{nombre}</color>")
            + $" · {Region()} · misma versión (v{Application.version})";
    }

    private static string Region()
    {
        string r = PhotonNetwork.CloudRegion;
        if (string.IsNullOrEmpty(r)) return "tu región";
        r = r.Split('/')[0].ToLowerInvariant();
        return r == "sa" ? "Sudamérica" : r == "us" || r == "usw" ? "Estados Unidos" : r == "eu" ? "Europa" : r == "asia" ? "Asia" : r.ToUpperInvariant();
    }

    private static string Reloj(float segundos)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(segundos));
        return $"{s / 60}:{s % 60:00}";
    }

    // ================= Cuadro a cuadro =================

    private void AnimarBusqueda(float now, float dt)
    {
        // El botón de los dos andenes online: el pulso cada 2,2 s, el brillo cada 3,4 s y la aguja del radar.
        float p = Mathf.Repeat(now / 2.2f, 1f), e = EaseOut(p);
        float t = Mathf.Repeat(now / 3.4f, 1f);
        float brilloX = t < 0.55f ? -160f : Mathf.Lerp(-160f, AnchoBuscar + 40f, (t - 0.55f) / 0.45f);
        for (int i = 0; i < 2; i++)
        {
            Busqueda b = details[i].busqueda;
            if (b == null || !b.acciones.gameObject.activeInHierarchy) continue;
            b.pulso.sizeDelta = new Vector2(AnchoBuscar + 44f * e, AltoBuscar + 44f * e);
            b.pulso.GetComponent<Img>().color = WithAlpha(ModeColor[i], FadeAlpha(0.5f * (1f - p)));
            b.brillo.anchoredPosition = new Vector2(brilloX, b.brillo.anchoredPosition.y);
            b.aguja.localEulerAngles = new Vector3(0f, 0f, -360f * now / 2.4f);
        }

        int modo = open == 0 || open == 1 ? open : -1;
        bool ver = modo >= 0 && details[modo].busqueda.bloque == Bloque.Busqueda;
        radarGroup.alpha = Mathf.MoveTowards(radarGroup.alpha, ver ? 1f : 0f, dt / 0.3f);
        if (!ver) return;
        PintarBusqueda(details[modo], modo, now);
    }

    // El panel y el radar con lo que hay en Photon (US 275 y US 276).
    private void PintarBusqueda(Detail d, int mode, float now)
    {
        Busqueda b = d.busqueda;
        Multijugador net = Multijugador.Instancia;
        Color c = ModeColor[mode];
        bool enSala = net.EnPartidaPublica;
        var jugadores = enSala ? net.Jugadores() : new List<Multijugador.Jugador>
            { new Multijugador.Jugador { nombre = PlayerProfile.HasName ? PlayerProfile.Name : "Jugador", vos = true } };
        int n = jugadores.Count;
        float empezar = net.FaltaParaEmpezar, conMinimo = net.FaltaConMinimo;
        bool encontrada = empezar >= 0f;

        b.titulo.text = encontrada ? "Partida encontrada" : "Buscando partida";
        b.reloj.text = encontrada ? $"{n} / {Multijugador.MaxJugadores}" : Reloj(net.SegundosBuscando);
        int segundos = Mathf.CeilToInt(empezar);
        b.grande.text = encontrada
            ? segundos > 0 ? $"Empieza en <color={ModeHex[mode]}>{segundos}</color>" : "Cargando el mapa…"
            : $"<color={ModeHex[mode]}>{n}</color> / {Multijugador.MaxJugadores} jugadores";
        ConfigRed.Mapa mapa = enSala ? net.MapaActual : null;
        b.datos.text = !enSala ? "Conectando con el servidor…"
            : encontrada && mapa != null ? $"{Titles[mode]} · {mapa.nombre} · {Region()}"
            : $"{Titles[mode]} · {Region()} · misma versión (v{Application.version})";

        float latido = 0.3f + 0.7f * (0.5f + 0.5f * Mathf.Cos(now * Mathf.PI * 2f / 1.6f));
        for (int s = 0; s < 8; s++)
        {
            bool ocupado = s < n;
            string nombre = ocupado ? jugadores[s].nombre + (jugadores[s].vos ? " <color=#FFFFFF80>(vos)</color>" : "") : "Buscando…";
            b.lugares[s].text = nombre;
            b.lugares[s].color = ocupado ? Ink : White(FadeAlpha(0.35f));
            b.puntos[s].color = ocupado ? c : White(FadeAlpha(0.28f * latido));
        }

        bool barra = !encontrada && conMinimo >= 0f;
        b.pie.text = encontrada ? "Si entra alguien más, se suma. Si sale alguien y quedan menos que el mínimo, sigue buscando."
            : barra ? $"Ya está el mínimo para {Titles[mode]}. Si no entra nadie más, arranca en {Reloj(Mathf.Ceil(conMinimo))}."
            : $"Arranca sola con {Multijugador.MaxJugadores} jugadores, o con {Multijugador.MinimoPara((GameMode)mode)} si pasan {Multijugador.EsperaConMinimo} s sin que entre nadie.";
        b.progreso.gameObject.SetActive(barra);
        if (barra) b.progresoLleno.sizeDelta = new Vector2(520f * (1f - conMinimo / Multijugador.EsperaConMinimo), 6f);
        b.salir.text = encontrada ? "Salir" : "Cancelar";

        // El radar: del color del modo, más rápido con la partida encontrada.
        if (radarModo != mode)
        {
            radarModo = mode;
            for (int k = 0; k < radarColor.Count; k++)
                radarColor[k].color = k < 3 ? WithAlpha(c, FadeAlpha(0.18f)) : k == 3 ? c : WithAlpha(c, FadeAlpha(0.6f));
        }
        float periodo = encontrada ? 1.1f : 2.6f;
        for (int k = 0; k < radarPulsos.Count; k++)
        {
            float q = Mathf.Repeat(now / periodo + k / 3f, 1f);
            radarPulsos[k].rectTransform.localScale = Vector3.one * Mathf.Lerp(0.12f, 1f, q);
            radarPulsos[k].color = WithAlpha(c, FadeAlpha(0.9f * (1f - q)));
        }
        barrido.localEulerAngles = new Vector3(0f, 0f, -360f * now / (encontrada ? 1.4f : 3.2f));
        for (int k = 0; k < radarTrazos.Count; k++) radarTrazos[k].color = WithAlpha(c, FadeAlpha(0.45f * (1f - k / (float)radarTrazos.Count)));
        int otros = 0;
        for (int s = 0; s < n && otros < radarJugadores.Count; s++)
        {
            if (jugadores[s].vos) continue;
            radarNombres[otros].text = jugadores[s].nombre;
            radarJugadores[otros].gameObject.SetActive(true);
            otros++;
        }
        for (int k = otros; k < radarJugadores.Count; k++) radarJugadores[k].gameObject.SetActive(false);
    }
}
