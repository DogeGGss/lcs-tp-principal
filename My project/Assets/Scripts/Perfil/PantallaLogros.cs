using TMPro;
using UnityEngine;
using static ShopUIKit;
using static PerfilUI;

// Pantalla de logros del menú principal (US 044): PLANTILLA DE PRUEBA. Los logros todavía no se detectan ni se
// guardan (US 041 y US 045), así que la lista, el progreso y las fechas son de ejemplo, sacados de las US 040 y 160.
// A la izquierda, el progreso total y por categoría; a la derecha, la lista con pestañas Todos, Obtenidos y Pendientes.
public class PantallaLogros : PantallaBase
{
    private const float XLista = 530f, AnchoLista = 1330f, AltoFila = 104f, Separacion = 12f;

    private struct Logro
    {
        public string categoria, nombre, descripcion, fecha; // fecha == null: todavía no se obtuvo
        public int progreso, meta;                           // meta > 0: logro con contador
        public bool secreto;
        public bool Obtenido => fecha != null;
    }

    private static Logro L(string categoria, string nombre, string descripcion, string fecha = null, int progreso = 0, int meta = 0,
        bool secreto = false) =>
        new Logro { categoria = categoria, nombre = nombre, descripcion = descripcion, fecha = fecha, progreso = progreso, meta = meta, secreto = secreto };

    private static readonly string[] Categorias = { "General", "Táctico", "Deathmatch", "Zombie", "Minijuego", "Secreto" };

    private static readonly Logro[] Logros =
    {
        L("General", "Bienvenido al andén", "Elegir tu nombre de jugador.", "02/10/2026"),
        L("General", "Primera partida", "Terminar una partida online.", "03/10/2026"),
        L("General", "Coleccionista", "Conseguir 25 cosméticos para el Perfil.", progreso: 12, meta: 25),
        L("Táctico", "Victoria táctica", "Ganar una partida del modo táctico.", "05/10/2026"),
        L("Táctico", "Desactivador", "Desactivar la bomba 5 veces.", progreso: 2, meta: 5),
        L("Deathmatch", "Primera sangre", "Hacer la primera eliminación de una partida.", "07/10/2026"),
        L("Deathmatch", "Imparable", "Conseguir 100 eliminaciones.", progreso: 37, meta: 100),
        L("Deathmatch", "En racha", "Eliminar a 5 rivales seguidos sin morir."),
        L("Zombie", "Exterminador", "Eliminar 10 zombies.", "09/10/2026"),
        L("Zombie", "Última oleada", "Sobrevivir hasta la oleada 20.", progreso: 8, meta: 20),
        L("Minijuego", "Primer vuelo", "Acertarle al primer pájaro.", "10/10/2026"),
        L("Minijuego", "Ojo de halcón", "Acertarle a 10 pájaros en una sola fase de compra."),
        L("Minijuego", "Golondrina veloz", "Acertarle a 25 golondrinas en total.", progreso: 12, meta: 25),
        L("Minijuego", "Sin fallar", "Acertarle a 5 pájaros seguidos sin errar un disparo."),
        L("Minijuego", "Cazador", "Acertarle a 500 pájaros en total.", progreso: 61, meta: 500),
        L("Secreto", "Easter egg", "Seguí explorando los mapas para descubrirlo.", secreto: true),
    };

    private readonly TextMeshProUGUI[] pestanas = new TextMeshProUGUI[3];
    private TextMeshProUGUI infoPestana;
    private UnityEngine.UI.Image subrayado;
    private UnityEngine.UI.ScrollRect scroll;
    private RectTransform lista;
    private int pestana;

    public PantallaLogros(MenuRiftwalker menu, RectTransform parent) : base(menu, parent, "Logros")
    {
        BarraSuperior("Plantilla de prueba · datos de ejemplo", "Logros");
        ArmarResumen();
        ArmarPestanas();
        scroll = Scroll(rect, XLista, 204f, AnchoLista, 836f, out lista);
        ElegirPestana(0);
    }

    // ================= Resumen =================

    private void ArmarResumen()
    {
        int total = Logros.Length, obtenidos = Contar(null, true);
        RectTransform r = Place(Node("Resumen", rect), 60f, 130f, 440f, 910f);
        Image(r, Rounded, PanelColor, 8f);
        Borde(r, 8f, White(0.06f));
        T(r, "Rotulo", 28f, 26f, 384f, 22f, FRotulo, 17f, Accent, "Progreso", TextAlignmentOptions.MidlineLeft, 16f, true);
        T(r, "Cuenta", 24f, 50f, 384f, 120f, FTitulo, 116f, Ink, $"{obtenidos}<size=50%><color={Rich(Mute)}> / {total}</color></size>",
            TextAlignmentOptions.MidlineLeft, 2f);
        T(r, "Obtenidos", 28f, 168f, 250f, 28f, FTexto, 22f, Mute, "logros obtenidos");
        T(r, "Porcentaje", 212f, 168f, 200f, 28f, FTitulo, 26f, Soft, Mathf.RoundToInt(100f * obtenidos / total) + " %", TextAlignmentOptions.MidlineRight);
        Barra(r, 28f, 206f, 384f, 10f, (float)obtenidos / total);

        T(r, "PorCategoria", 28f, 258f, 384f, 22f, FRotulo, 17f, Accent, "Por categoría", TextAlignmentOptions.MidlineLeft, 16f, true);
        for (int i = 0; i < Categorias.Length; i++)
        {
            float y = 292f + i * 62f;
            int tiene = Contar(Categorias[i], true), de = Contar(Categorias[i], null);
            T(r, Categorias[i], 28f, y, 250f, 40f, FTitulo, 30f, Soft, Categorias[i], TextAlignmentOptions.MidlineLeft, 3f, true);
            T(r, "Cuenta " + Categorias[i], 232f, y, 180f, 40f, FTitulo, 30f, Ink, $"{tiene}<color={Rich(Mute)}> / {de}</color>",
                TextAlignmentOptions.MidlineRight, 2f);
            Barra(r, 28f, y + 42f, 384f, 4f, (float)tiene / de);
        }

        TextMeshProUGUI nota = T(r, "Nota", 28f, 690f, 384f, 190f, FTexto, 19f, Mute,
            "Plantilla de prueba: los logros todavía no se detectan ni se guardan (US 041 y US 045). La lista y el progreso " +
            "son de ejemplo, para ver cómo se van a consultar (US 044).", TextAlignmentOptions.BottomLeft);
        nota.textWrappingMode = TextWrappingModes.Normal;
    }

    private static void Barra(RectTransform parent, float x, float y, float w, float h, float parte)
    {
        Image(Place(Node("Barra", parent), x, y, w, h), Rounded, TrackColor, h / 2f);
        if (parte > 0f) Image(Place(Node("Lleno", parent), x, y, Mathf.Max(h, w * Mathf.Clamp01(parte)), h), Rounded, Accent, h / 2f);
    }

    /// <summary>Cuántos logros hay de una categoría (null: todas) que estén obtenidos (true), pendientes (false) o ambos (null).</summary>
    private static int Contar(string categoria, bool? obtenido)
    {
        int n = 0;
        foreach (Logro l in Logros)
            if ((categoria == null || l.categoria == categoria) && (obtenido == null || l.Obtenido == obtenido.Value)) n++;
        return n;
    }

    // ================= Pestañas y lista =================

    private void ArmarPestanas()
    {
        string[] nombres = { "Todos", "Obtenidos", "Pendientes" };
        float x = XLista;
        for (int i = 0; i < nombres.Length; i++)
        {
            int k = i;
            pestanas[i] = T(rect, nombres[i], x, 128f, 300f, 50f, FTitulo, 36f, Mute, nombres[i], TextAlignmentOptions.MidlineLeft, 6f, true);
            float w = Width(pestanas[i], nombres[i].ToUpperInvariant());
            pestanas[i].rectTransform.sizeDelta = new Vector2(w, 50f);
            pestanas[i].raycastTarget = true;
            var p = pestanas[i].gameObject.AddComponent<ShopPointerTarget>();
            p.Clicked = () => ElegirPestana(k);
            p.Hovered = () => { if (pestana != k) pestanas[k].color = Soft; };
            p.Exited = () => { if (pestana != k) pestanas[k].color = Mute; };
            pestanas[i].gameObject.AddComponent<SonidoAlPasar>();
            x += w + 56f;
        }
        Image(Place(Node("Linea", rect), XLista, 184f, AnchoLista, 1f), null, White(0.1f));
        subrayado = Image(Place(Node("Subrayado", rect), XLista, 180f, 100f, 4f), null, Accent);
        infoPestana = T(rect, "Info", XLista + AnchoLista - 600f, 138f, 600f, 34f, FRotulo, 20f, Mute, "", TextAlignmentOptions.MidlineRight, 10f, true);
    }

    private void ElegirPestana(int i)
    {
        pestana = i;
        for (int k = 0; k < pestanas.Length; k++) pestanas[k].color = k == i ? Ink : Mute;
        RectTransform t = pestanas[i].rectTransform;
        subrayado.rectTransform.anchoredPosition = new Vector2(t.anchoredPosition.x, -180f);
        subrayado.rectTransform.sizeDelta = new Vector2(t.sizeDelta.x, 4f);

        BorrarHijos(lista);
        int n = 0;
        foreach (Logro l in Logros)
        {
            if ((i == 1 && !l.Obtenido) || (i == 2 && l.Obtenido)) continue;
            Fila(l, n * (AltoFila + Separacion));
            n++;
        }
        lista.sizeDelta = new Vector2(AnchoLista, Mathf.Max(0f, n * (AltoFila + Separacion) - Separacion));
        scroll.verticalNormalizedPosition = 1f;
        infoPestana.text = n == 1 ? "1 logro" : $"{n} logros";
    }

    private void Fila(Logro l, float y)
    {
        bool ok = l.Obtenido;
        RectTransform f = Place(Node(l.nombre, lista), 0f, y, AnchoLista, AltoFila);
        Image(f, Rounded, ok ? PanelColor : WithAlpha(PanelBase, 0.55f), 8f);
        Borde(f, 8f, ok ? WithAlpha(Accent, 0.45f) : White(0.06f));
        if (ok) Image(Place(Node("Marca", f), 0f, 18f, 4f, AltoFila - 36f), null, Accent);

        // Ícono: obtenido, una medalla con tilde; pendiente, un candado (o un signo de pregunta si es secreto).
        float cx = 66f, cy = AltoFila / 2f;
        if (ok)
        {
            Circulo(f, cx, cy, 30f, Accent);
            Poligono(f, "Tilde", AccentInk,
                new Vector2(cx - 14f, cy + 1f), new Vector2(cx - 9f, cy - 4f), new Vector2(cx - 4f, cy + 1f),
                new Vector2(cx + 9f, cy - 12f), new Vector2(cx + 14f, cy - 7f), new Vector2(cx - 4f, cy + 11f));
        }
        else
        {
            Circulo(f, cx, cy, 30f, White(0.16f), 3f);
            if (l.secreto) T(f, "Signo", cx - 30f, cy - 30f, 60f, 60f, FTitulo, 40f, Mute, "?", TextAlignmentOptions.Center);
            else Candado(f, cx, cy, 30f, Mute);
        }

        T(f, "Categoria", 124f, 16f, 600f, 20f, FRotulo, 15f, ok ? Accent : Mute, l.categoria, TextAlignmentOptions.MidlineLeft, 14f, true);
        T(f, "Nombre", 122f, 34f, 760f, 38f, FTitulo, 34f, ok ? Ink : Soft, l.secreto && !ok ? "???" : l.nombre, TextAlignmentOptions.MidlineLeft, 3f, true);
        T(f, "Descripcion", 124f, 70f, 800f, 24f, FTexto, 20f, Mute, l.descripcion);

        // A la derecha: la fecha si se obtuvo; si no, el progreso (los que tienen contador) o "Pendiente".
        float xd = AnchoLista - 32f;
        if (ok)
        {
            T(f, "Estado", xd - 300f, 26f, 300f, 22f, FRotulo, 17f, Accent, "Obtenido", TextAlignmentOptions.MidlineRight, 14f, true);
            T(f, "Fecha", xd - 300f, 52f, 300f, 26f, FMono, 18f, Mute, l.fecha, TextAlignmentOptions.MidlineRight);
        }
        else if (l.meta > 0)
        {
            T(f, "Progreso", xd - 300f, 18f, 300f, 36f, FTitulo, 32f, Ink, $"{l.progreso}<color={Rich(Mute)}> / {l.meta}</color>",
                TextAlignmentOptions.MidlineRight, 2f);
            Barra(f, xd - 300f, 64f, 300f, 8f, (float)l.progreso / l.meta);
        }
        else T(f, "Estado", xd - 300f, 40f, 300f, 24f, FRotulo, 17f, Mute, "Pendiente", TextAlignmentOptions.MidlineRight, 14f, true);
    }
}
