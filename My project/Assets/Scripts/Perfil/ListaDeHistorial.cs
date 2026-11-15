using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static ShopUIKit;
using static PerfilUI;
using Partida = HistorialDePartidas.Partida;

// La lista del historial de partidas (US 374, CA1 y CA2): una fila por partida, de la más nueva a la más vieja, con el
// modo, el resultado, lo que hizo (bajas, muertes, asistencias y KDA; en Invasión, la dificultad, la oleada y si jugó solo
// o en cooperativo), el mapa, la fecha y hora y la duración. La usan la pestaña Historial del Perfil y el perfil de otro
// jugador (US 373, PerfilAjeno; US 374, CA5).
public static class ListaDeHistorial
{
    public const float FilaH = 66f, Paso = 72f, Encabezado = 34f;

    private static readonly System.Globalization.CultureInfo Cultura = System.Globalization.CultureInfo.InvariantCulture;

    /// <summary>Dibuja las partidas en contenido (que va dentro de un Scroll) y devuelve el alto que ocupan.</summary>
    public static float Dibujar(RectTransform contenido, IReadOnlyList<Partida> partidas, float ancho, string vacio)
    {
        BorrarHijos(contenido);
        if (partidas == null || partidas.Count == 0)
        {
            TextMeshProUGUI t = T(contenido, "SinPartidas", 24f, 24f, ancho - 48f, 80f, FTexto, 24f, Mute, vacio, TextAlignmentOptions.TopLeft);
            t.textWrappingMode = TextWrappingModes.Normal;
            return 120f;
        }

        float[] x = Columnas(ancho);
        string[] rotulos = { "Modo", "Resultado", "Desempeño", "Mapa", "Fecha", "Duración" };
        for (int c = 0; c < rotulos.Length; c++)
            T(contenido, "Columna" + c, x[c], 0f, x[c + 1] - x[c] - 12f, Encabezado - 6f, FRotulo, 14f, Mute, rotulos[c],
                c == rotulos.Length - 1 ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft, 12f, true);
        Image(Place(Node("Separador", contenido), 0f, Encabezado - 2f, ancho, 1f), null, White(0.08f));

        for (int i = 0; i < partidas.Count; i++) Fila(contenido, partidas[i], Encabezado + 8f + i * Paso, ancho, x);
        return Encabezado + 8f + partidas.Count * Paso;
    }

    // Dónde empieza cada columna (y el final), según el ancho disponible.
    private static float[] Columnas(float w) => new[] { 24f, w * 0.13f, w * 0.29f, w * 0.60f, w * 0.75f, w * 0.89f, w - 24f };

    private static void Fila(RectTransform padre, Partida p, float y, float ancho, float[] x)
    {
        var (modo, color) = Modo(p);
        RectTransform fila = Place(Node("Partida", padre), 0f, y, ancho, FilaH);
        Image(fila, Rounded, PanelColor, 6f);
        Image(Place(Node("Color", fila), 0f, 10f, 4f, FilaH - 20f), null, color);

        T(fila, "Modo", x[0], 0f, x[1] - x[0] - 12f, FilaH, FTitulo, 26f, color, modo, TextAlignmentOptions.MidlineLeft, 4f, true);
        var (resultado, gano) = Resultado(p);
        TextMeshProUGUI r = Text(Place(Node("Resultado", fila), x[1], 0f, x[2] - x[1] - 12f, FilaH), FTitulo, 26f, gano ? Accent : Soft,
            TextAlignmentOptions.MidlineLeft, 3f, true);
        r.text = resultado;
        TextMeshProUGUI d = Text(Place(Node("Detalle", fila), x[2], 0f, x[3] - x[2] - 12f, FilaH), FTitulo, 24f, Ink, TextAlignmentOptions.MidlineLeft, 1f);
        d.text = Detalle(p);
        d.overflowMode = TextOverflowModes.Ellipsis;
        TextMeshProUGUI m = Text(Place(Node("Mapa", fila), x[3], 0f, x[4] - x[3] - 12f, FilaH), FTexto, 21f, Soft, TextAlignmentOptions.MidlineLeft);
        m.text = string.IsNullOrEmpty(p.mapa) ? "—" : L10n.Source(p.mapa, "gameplay.");
        m.overflowMode = TextOverflowModes.Ellipsis;
        Text(Place(Node("Fecha", fila), x[4], 0f, x[5] - x[4] - 12f, FilaH), FTexto, 21f, Soft, TextAlignmentOptions.MidlineLeft).text =
            p.Fecha.ToString("dd/MM/yyyy HH:mm", Cultura);
        Text(Place(Node("Duracion", fila), x[5], 0f, x[6] - x[5], FilaH), FTitulo, 24f, Ink, TextAlignmentOptions.MidlineRight).text = Duracion(p.segundos);
    }

    public static (string nombre, Color color) Modo(Partida p) => p.ModoDe switch
    {
        HistorialDePartidas.Modo.Deathmatch => (L10n.Source("Deathmatch", "profile."), Hex(0xFF4B4B)),
        HistorialDePartidas.Modo.Invasion => (L10n.Source("Invasión", "profile."), Hex(0x7DE05A)),
        _ => (L10n.Source("Táctico", "profile."), Hex(0xF29A38)),
    };

    // CA2: ganó o perdió; en Deathmatch, el puesto.
    public static (string texto, bool gano) Resultado(Partida p)
    {
        if (p.ModoDe == HistorialDePartidas.Modo.Deathmatch && p.puesto > 1) return (L10n.T("profile.hist.place_fmt", p.puesto), false);
        if (p.ModoDe == HistorialDePartidas.Modo.Invasion)
            return (p.gano ? L10n.T("profile.stat.world_saved") : L10n.T("gameplay.ui.defeat"), p.gano);
        return (p.gano ? L10n.T("gameplay.ui.victory_1") : L10n.T("gameplay.ui.defeat"), p.gano);
    }

    // CA2: en Táctico y Deathmatch, bajas, muertes, asistencias y KDA; en Invasión, la dificultad, la oleada y solo o cooperativo.
    public static string Detalle(Partida p)
    {
        if (p.ModoDe == HistorialDePartidas.Modo.Invasion)
            return L10n.T("profile.hist.invasion_fmt", Dificultad(p.dificultad), p.oleada,
                p.coop ? L10n.T("profile.hist.coop") : L10n.T("profile.hist.solo"));
        string kda = p.Kda.ToString("0.00", Cultura).Replace(".", ",");
        return L10n.T("profile.hist.kda_fmt", p.bajas, p.muertes, p.asistencias, kda);
    }

    private static string Dificultad(string d) => d switch
    {
        "facil" => L10n.T("gameplay.difficulty.easy"),
        "dificil" => L10n.T("gameplay.difficulty.hard"),
        _ => L10n.T("gameplay.difficulty.normal"),
    };

    public static string Duracion(int segundos)
    {
        segundos = Mathf.Max(0, segundos);
        return segundos >= 3600 ? $"{segundos / 3600}:{segundos % 3600 / 60:00}:{segundos % 60:00}" : $"{segundos / 60}:{segundos % 60:00}";
    }
}
