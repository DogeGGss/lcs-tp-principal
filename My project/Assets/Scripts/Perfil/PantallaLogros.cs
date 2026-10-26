using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ShopUIKit;
using static PerfilUI;
using Categoria = CategoriasRanking.Categoria;

// Logros y ranking (US 044 y US 226). Pestaña Logros: los logros de cometas y de combate, con el progreso y el
// porcentaje de jugadores que tiene cada uno. Pestaña Ranking: todos los jugadores ordenados por la categoría elegida (CategoriasRanking): un dato
// de las estadísticas, del minijuego o la cantidad de logros.
public class PantallaLogros : PantallaBase
{
    readonly RectTransform resumen, lista, acciones, filtros;
    readonly ScrollRect scroll;
    readonly TextMeshProUGUI titulo;
    readonly Boton[] tabs = new Boton[2];
    int seccion, pagina, local = -1, online = -1;
    string filtro = "", categoria = "bajas";
    float siguiente;
    LogrosOnline.Jugador seleccionado;
    LogrosOnline Servicio => LogrosOnline.Instancia;

    public PantallaLogros(MenuRiftwalker menu, RectTransform parent) : base(menu, parent, "Logros")
    {
        BarraSuperior("Progreso acumulado · Ranking global", "Logros y ranking");
        resumen = Place(Node("Resumen", rect), 60, 130, 440, 910);
        Image(resumen, Rounded, PanelColor, 8);
        for (int i = 0; i < 2; i++) { int k = i; tabs[i] = CrearBoton(rect, 530 + i * 246, 130, 230, 56, i == 0 ? "Logros" : "Ranking", i == 0, () => Cambiar(k)); }
        titulo = T(rect, "Título lista", 530, 206, 1330, 36, FTitulo, 28, Ink, "");
        titulo.richText = false;
        scroll = Scroll(rect, 530, 252, 1330, 706, out lista);
        acciones = Place(Node("Acciones", rect), 530, 976, 1330, 54);
        filtros = Place(Node("Categorias", rect), 530, 206, 1330, 100);
        Dibujar(true);
    }
    public override void Refrescar() { base.Refrescar(); Consultar(); Dibujar(); }
    public override void Tick()
    {
        if (Time.unscaledTime >= siguiente) Consultar();
        if (local != LogrosCometas.Revision || online != (Servicio == null ? -1 : Servicio.Revision)) Dibujar();
    }
    bool PorEstadistica => seccion == 1 && seleccionado == null && categoria != CategoriasRanking.Logros;
    void Consultar() { siguiente = Time.unscaledTime + 60; Servicio?.Solicitar(pagina, filtro, PorEstadistica ? categoria : ""); }
    void Cambiar(int n) { seccion = n; pagina = 0; filtro = ""; seleccionado = null; Consultar(); Dibujar(true); }
    void Propietarios(string id) { seccion = 1; categoria = CategoriasRanking.Logros; pagina = 0; filtro = id; seleccionado = null; Consultar(); Dibujar(true); }
    void Elegir(string id) { categoria = id; pagina = 0; filtro = ""; seleccionado = null; Consultar(); Dibujar(true); }
    public override bool Escape() { if (seleccionado == null) return false; seleccionado = null; Dibujar(true); return true; }
    void Dibujar(bool alInicio = false)
    {
        float pos = scroll.verticalNormalizedPosition;
        local = LogrosCometas.Revision; online = Servicio == null ? -1 : Servicio.Revision;
        BorrarHijos(resumen); BorrarHijos(lista); BorrarHijos(acciones); BorrarHijos(filtros);
        for (int i = 0; i < 2; i++) { tabs[i].primario = i == seccion; tabs[i].Pintar(); }
        T(resumen, "Tu progreso", 28, 26, 384, 30, FRotulo, 20, Accent, "TU PROGRESO");
        int total = LogrosCometas.Catalogo.Length;
        T(resumen, "Cuenta", 24, 68, 384, 116, FTitulo, 100, Ink, LogrosCometas.Cantidad + "<size=50%> / " + total + "</size>");
        T(resumen, "Obtenidos", 28, 190, 384, 35, FTexto, 24, Mute, "logros obtenidos");
        Barra(resumen, 28, 240, 384, LogrosCometas.Cantidad / (float)total, Accent);
        // Un renglón por color de cometa y uno para los de combate (tipo -1).
        EstadisticasJugador.Totales e = EstadisticasJugador.Datos;
        for (int i = 0; i < 4; i++)
        {
            int tipo = i < 3 ? i : -1, n = 0, de = 0;
            foreach (var d in LogrosCometas.Catalogo) if (d.tipo == tipo) { de++; if (LogrosCometas.Tiene(d)) n++; }
            Color c = i < 3 ? Cometa.ColorDe((TipoCometa)i) : Accent;
            T(resumen, "Grupo", 28, 280 + i * 72, 384, 36, FTitulo, 29, c, new[] { "AZULES", "VERDES", "DORADOS", "COMBATE" }[i] + "   " + n + " / " + de);
            T(resumen, "Detalle", 28, 316 + i * 72, 384, 28, FTexto, 21, Soft, i < 3 ? LogrosCometas.Datos.aciertos[i] + " destruidos"
                : PerfilDatos.Numero(e.Bajas) + " bajas · " + PerfilDatos.Numero(e.Cabezas) + " a la cabeza");
        }
        var datos = Servicio?.Datos;
        T(resumen, "Puesto", 28, 592, 384, 40, FTitulo, 29, Ink, datos?.me == null ? "PUESTO EN LOGROS: —" : "PUESTO EN LOGROS: #" + datos.me.rank);
        string estado = Servicio == null ? "El progreso se guarda en esta PC." : Servicio.Estado;
        if (datos != null) estado += "\n" + (datos.population == 1 ? "1 jugador registrado" : datos.population + " jugadores registrados") + Fecha(datos.fetched_at);
        if (!string.IsNullOrEmpty(LogrosCometas.AvisoGuardado)) estado += "\n" + LogrosCometas.AvisoGuardado;
        var texto = T(resumen, "Conexión", 28, 650, 384, 176, FTexto, 20, Mute, estado);
        texto.textWrappingMode = TextWrappingModes.Normal;
        CrearBoton(resumen, 28, 838, 384, 46, "Actualizar", false, Consultar, 22);

        // En el ranking, arriba van los grupos y las categorías; la lista empieza debajo.
        float arriba = seccion == 1 && seleccionado == null ? DibujarCategorias() : 206;
        Place(titulo.rectTransform, 530, arriba, 1330, 36);
        Place((RectTransform)scroll.transform, 530, arriba + 46, 1330, 958 - (arriba + 46));
        int filas;
        float paso = 112;
        if (PorEstadistica) filas = DibujarEstadistica(out paso);
        else if (seccion == 0 || seleccionado != null)
        {
            titulo.text = seleccionado == null ? "Cometas de cada color y combate en Táctico y Deathmatch" : "Logros de " + seleccionado.display_name;
            filas = LogrosCometas.Catalogo.Length;
            for (int i = 0; i < filas; i++) FilaLogro(LogrosCometas.Catalogo[i], i);
            if (seleccionado != null) CrearBoton(acciones, 0, 0, 300, 48, "Volver al ranking", false, () => { seleccionado = null; Dibujar(true); }, 22);
            else T(acciones, "Nota", 0, 0, 1330, 48, FTexto, 20, Mute, "Los porcentajes corresponden a los jugadores registrados en el servicio online.");
        }
        else
        {
            var definicion = Array.Find(LogrosCometas.Catalogo, d => d.id == filtro);
            titulo.text = definicion == null ? "Logros obtenidos · empates comparten puesto" : "Quiénes consiguieron: " + definicion.nombre;
            bool coincide = datos != null && datos.offset == pagina && (datos.filter ?? "") == filtro;
            filas = coincide && datos.players != null ? datos.players.Length : 0;
            for (int i = 0; i < filas; i++) FilaJugador(datos.players[i], i);
            if (filas == 0) { var t = T(lista, "Vacío", 26, 30, 1200, 140, FTexto, 28, Mute, Servicio == null || !Servicio.Configurado ? "El ranking estará disponible al conectar el servicio online." : Servicio.Ocupado ? "Consultando jugadores…" : coincide ? "Todavía no hay jugadores en esta lista." : "No hay datos disponibles. Probá actualizar."); t.textWrappingMode = TextWrappingModes.Normal; }
            var anterior = CrearBoton(acciones, 0, 0, 210, 48, "Anterior", false, () => { pagina = Mathf.Max(0, pagina - 50); Consultar(); Dibujar(true); }, 22);
            anterior.button.interactable = pagina > 0;
            var proximo = CrearBoton(acciones, 226, 0, 210, 48, "Siguiente", false, () => { pagina += 50; Consultar(); Dibujar(true); }, 22);
            proximo.button.interactable = coincide && datos.has_more;
            if (filtro != "") CrearBoton(acciones, 456, 0, 300, 48, "Todos los jugadores", false, () => Cambiar(1), 22);
            T(acciones, "Página", 1000, 0, 330, 48, FTexto, 23, Soft, "Página " + (pagina / 50 + 1), TextAlignmentOptions.MidlineRight);
        }
        lista.sizeDelta = new Vector2(1330, Mathf.Max(180, filas * paso));
        scroll.verticalNormalizedPosition = alInicio ? 1 : pos;
    }

    // Los grupos (General, Táctico, Deathmatch, Minijuego, Logros) y las categorías del grupo elegido, que bajan de
    // renglón si no entran. Devuelve dónde empieza la lista.
    float DibujarCategorias()
    {
        Categoria actual = CategoriasRanking.Buscar(categoria) ?? CategoriasRanking.Todas[0];
        string[] grupos = CategoriasRanking.Grupos;
        for (int i = 0; i < grupos.Length; i++)
        {
            string g = grupos[i];
            CrearBoton(filtros, i * 212, 0, 200, 44, g, g == actual.grupo, () => Elegir(CategoriasRanking.Primera(g).id), 22);
        }
        float alto = 44;
        if (actual.grupo != "Logros")
        {
            float x = 0, y = 56;
            foreach (Categoria c in CategoriasRanking.Todas)
            {
                if (c.grupo != actual.grupo) continue;
                string id = c.id;
                Boton b = CrearBoton(filtros, x, y, 100, 38, c.nombre, id == categoria, () => Elegir(id), 18);
                float w = Mathf.Ceil(b.texto.GetPreferredValues(b.texto.text).x) + 36;
                if (x > 0 && x + w > 1330) { x = 0; y += 48; }
                b.rect.sizeDelta = new Vector2(w, 38);
                b.rect.anchoredPosition = new Vector2(x + w / 2, -(y + 19)); // el botón crece desde el centro
                x += w + 10;
            }
            alto = y + 38;
        }
        Place(filtros, 530, 206, 1330, alto);
        return 206 + alto + 22;
    }

    // Ranking por estadística: puesto, nombre y valor; el jugador local, resaltado.
    int DibujarEstadistica(out float paso)
    {
        paso = 80;
        Categoria cat = CategoriasRanking.Buscar(categoria);
        LogrosOnline.Tabla tabla = Servicio?.Estadistica;
        titulo.text = cat.grupo + ": " + cat.nombre + " · " + (cat.requisito ?? "empates comparten puesto");
        bool coincide = tabla != null && tabla.stat == categoria && tabla.offset == pagina && tabla.players != null;
        int filas = coincide ? tabla.players.Length : 0;
        string yo = coincide ? tabla.Propia?.player_id : null;
        for (int i = 0; i < filas; i++) FilaEstadistica(tabla.players[i], i, cat, yo);
        if (filas == 0)
        {
            string vacio = Servicio == null || !Servicio.Configurado ? "El ranking estará disponible al conectar el servicio online."
                : !Servicio.RankingDisponible ? "Este ranking todavía no está disponible: falta actualizar el servicio online."
                : Servicio.Ocupado ? "Consultando jugadores…"
                : coincide ? "Todavía no hay jugadores en esta categoría." : "No hay datos disponibles. Probá actualizar.";
            var t = T(lista, "Vacío", 26, 30, 1200, 140, FTexto, 28, Mute, vacio);
            t.textWrappingMode = TextWrappingModes.Normal;
        }
        var anterior = CrearBoton(acciones, 0, 0, 210, 48, "Anterior", false, () => { pagina = Mathf.Max(0, pagina - 50); Consultar(); Dibujar(true); }, 22);
        anterior.button.interactable = pagina > 0;
        var proximo = CrearBoton(acciones, 226, 0, 210, 48, "Siguiente", false, () => { pagina += 50; Consultar(); Dibujar(true); }, 22);
        proximo.button.interactable = coincide && tabla.has_more;
        LogrosOnline.Fila mio = coincide ? tabla.Propia : null;
        string puesto = !coincide ? "" : mio != null ? $"Tu puesto: <color=#F3F4F6>#{mio.rank}</color> · {CategoriasRanking.Mostrar(cat, mio.value)}"
            : "Todavía no aparecés en esta categoría.";
        T(acciones, "Puesto", 456, 0, 520, 48, FTexto, 23, Soft, puesto + (coincide ? Fecha(tabla.fetched_at) : ""));
        T(acciones, "Página", 1000, 0, 330, 48, FTexto, 23, Soft, "Página " + (pagina / 50 + 1), TextAlignmentOptions.MidlineRight);
        return filas;
    }

    void FilaEstadistica(LogrosOnline.Fila p, int i, Categoria cat, string yo)
    {
        bool mio = yo != null && p.player_id == yo;
        var r = Place(Node("Jugador", lista), 0, i * 80, 1330, 72);
        Image(r, Rounded, mio ? Over(WithAlpha(Accent, .16f), PanelColor) : PanelColor, 6);
        if (mio) Borde(r, 6, WithAlpha(Accent, .6f));
        T(r, "Puesto", 22, 0, 110, 72, FTitulo, 34, p.rank <= 3 ? Accent : Soft, "#" + p.rank);
        T(r, "Nombre", 150, 0, 800, 72, FTitulo, 30, Ink, mio ? p.display_name + " (vos)" : p.display_name).richText = false;
        T(r, "Valor", 960, 0, 346, 72, FTitulo, 36, Ink, CategoriasRanking.Mostrar(cat, p.value), TextAlignmentOptions.MidlineRight);
    }
    void FilaLogro(LogrosCometas.Definicion d, int i)
    {
        bool obtenido = seleccionado == null ? LogrosCometas.Tiene(d) : seleccionado.Tiene(d.id);
        Color c = ColorDe(d);
        var r = Place(Node(d.id, lista), 0, i * 112, 1330, 100);
        Image(r, Rounded, PanelColor, 6); Borde(r, 6, obtenido ? WithAlpha(c, .6f) : White(.08f));
        Image(Place(Node("Color", r), 0, 16, 5, 68), Rounded, c, 0);
        T(r, "Nombre", 22, 12, 630, 36, FTitulo, 29, obtenido ? Ink : Soft, d.nombre);
        T(r, "Descripción", 24, 53, 630, 30, FTexto, 21, Mute, d.descripcion);
        int cuenta = LogrosCometas.Valor(d);
        T(r, "Progreso", 670, 12, 190, 34, FTitulo, 26, obtenido ? c : Soft, seleccionado == null ? Mathf.Min(cuenta, d.meta) + " / " + d.meta : obtenido ? "Obtenido" : "Pendiente");
        Barra(r, 670, 64, 190, seleccionado == null ? (float)cuenta / d.meta : obtenido ? 1 : 0, c);
        var stats = Servicio?.Datos?.stats;
        var p = stats == null ? null : Array.Find(stats, s => s.achievement_id == d.id);
        T(r, "Porcentaje", 885, 18, 190, 30, FTitulo, 25, Ink, p == null ? "— %" : p.percent.ToString("0.#") + " %");
        T(r, "Global", 885, 52, 190, 25, FTexto, 18, Mute, "de jugadores");
        CrearBoton(r, 1100, 27, 210, 46, "Ver jugadores", false, () => Propietarios(d.id), 20);
    }
    void FilaJugador(LogrosOnline.Jugador p, int i)
    {
        var r = Place(Node("Jugador", lista), 0, i * 112, 1330, 100); Image(r, Rounded, PanelColor, 6);
        T(r, "Puesto", 20, 20, 110, 50, FTitulo, 38, Accent, "#" + p.rank);
        T(r, "Nombre", 150, 15, 490, 38, FTitulo, 30, Ink, p.display_name).richText = false;
        var catalogo = LogrosCometas.Catalogo;
        T(r, "Cantidad", 150, 57, 490, 28, FTexto, 22, Mute, p.total + " / " + catalogo.Length + " logros obtenidos");
        // Un cuadradito por logro, en el orden del catálogo: entran entre el nombre y el botón.
        float paso = Mathf.Min(40, 425f / catalogo.Length), lado = Mathf.Round(paso * 0.75f);
        for (int k = 0; k < catalogo.Length; k++)
        { var d = catalogo[k]; Image(Place(Node(d.id, r), 670 + k * paso, 50 - lado / 2, lado, lado), Rounded, p.Tiene(d.id) ? ColorDe(d) : White(.08f), 3); }
        CrearBoton(r, 1100, 27, 210, 46, "Ver logros", false, () => { seleccionado = p; Dibujar(true); }, 20);
    }
    // El color de cada logro: el de su cometa o, los de combate, el del juego.
    static Color ColorDe(LogrosCometas.Definicion d) => d.EsCometa ? Cometa.ColorDe((TipoCometa)d.tipo) : Accent;

    // La fecha del último ranking, en la hora de la PC ("16/10 07:03").
    static string Fecha(string iso) =>
        DateTime.TryParse(iso, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime d)
            ? " · " + d.ToLocalTime().ToString("dd/MM HH:mm") : "";

    static void Barra(RectTransform parent, float x, float y, float w, float parte, Color c)
    {
        Image(Place(Node("Barra", parent), x, y, w, 6), Rounded, TrackColor, 3);
        if (parte > 0) Image(Place(Node("Lleno", parent), x, y, Mathf.Max(6, w * Mathf.Clamp01(parte)), 6), Rounded, c, 3);
    }
}
