using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static ShopUIKit;
using Img = UnityEngine.UI.Image;
using Slider = UnityEngine.UI.Slider;

// Piezas de las pestañas Gráficos y Sonido de las opciones (US 048, US 153 y US 154), con el mismo estilo que
// la pestaña Controles (ControlsPanel): panel oscuro, título con la línea naranja, grupos, filas de 44 unidades,
// sliders con forma de vía y botones al pie. Las medidas están en unidades del panel (600 de ancho).
public class OpcionesKit
{
    public const float Pad = 40f, RowH = 44f, LabelW = 190f;

    public readonly RectTransform root;
    public readonly float width;
    private readonly TMP_FontAsset displayFont, labelFont, bodyFont;
    private readonly Sprite rounded;

    public float Inner => width - Pad * 2f;
    public float ControlX => Pad + LabelW;
    public float ControlW => width - Pad * 2f - LabelW;

    /// <summary>Filas de opciones con segmentos, para pintarlas cuando cambia el valor.</summary>
    public class Segmentos
    {
        public Img[] fondos;
        public TextMeshProUGUI[] textos;

        public void Marcar(int elegido)
        {
            for (int i = 0; i < fondos.Length; i++)
            {
                bool on = i == elegido;
                fondos[i].color = on ? Ink : ChipColor;
                textos[i].color = on ? KeyInk : Mute;
            }
        }
    }

    // ---------------------------------------------------------------------
    // Panel
    // ---------------------------------------------------------------------

    /// <summary>
    /// Arma la pestaña sobre "panel": le copia la ubicación y la escala de PanelControles (así las tres pestañas
    /// quedan en el mismo lugar y del mismo tamaño), apaga lo que tenía antes y crea el fondo y el título.
    /// </summary>
    public static OpcionesKit Armar(RectTransform panel, string titulo)
    {
        OpcionesPantalla pantalla = OpcionesPantalla.De(panel);
        ControlsPanel controles = pantalla != null ? pantalla.GetComponentInChildren<ControlsPanel>(true) : null;

        RectTransform modelo = controles != null ? controles.transform.parent as RectTransform : null;
        if (modelo != null && modelo != panel)
        {
            panel.anchorMin = modelo.anchorMin;
            panel.anchorMax = modelo.anchorMax;
            panel.pivot = modelo.pivot;
            panel.anchoredPosition = modelo.anchoredPosition;
            panel.sizeDelta = modelo.sizeDelta;
            panel.localScale = modelo.localScale;
        }

        // Lo que se había armado a mano en la escena queda apagado (no se borra).
        for (int i = 0; i < panel.childCount; i++) panel.GetChild(i).gameObject.SetActive(false);
        Img fondoViejo = panel.GetComponent<Img>();
        if (fondoViejo != null) fondoViejo.enabled = false;

        return new OpcionesKit(panel, titulo,
            controles != null ? controles.DisplayFont : null,
            controles != null ? controles.LabelFont : null,
            controles != null ? controles.BodyFont : null,
            controles != null ? controles.Rounded : null);
    }

    private OpcionesKit(RectTransform panel, string titulo, TMP_FontAsset display, TMP_FontAsset label, TMP_FontAsset body, Sprite sprite)
    {
        displayFont = display;
        labelFont = label;
        bodyFont = body;
        rounded = sprite;

        root = Stretch(Node("Pestaña", panel));
        Image(root, rounded, Rgb(10, 12, 17, DarkAlpha(0.97f)), 10f, true);
        width = panel.rect.width > 0f ? panel.rect.width : 600f;

        Text(Place(Node("Titulo", root), Pad, 36f, Inner, 50f), displayFont, 46f, Ink, TextAlignmentOptions.MidlineLeft, 2f, true).text = titulo;
        Image(Place(Node("LineaAnden", root), Pad, 92f, 120f, 3f), null, Accent);
    }

    // ---------------------------------------------------------------------
    // Filas
    // ---------------------------------------------------------------------

    public void Grupo(string titulo, ref float y)
    {
        Text(Place(Node("Grupo", root), Pad, y, 300f, 20f), labelFont, 15f, Mute, TextAlignmentOptions.MidlineLeft, 16f, true).text = titulo;
        y += 24f;
    }

    public void Ayuda(string texto, ref float y)
    {
        Text(Place(Node("Ayuda", root), Pad, y, Inner, 22f), bodyFont, 16f, Mute, TextAlignmentOptions.MidlineLeft).text = texto;
        y += 32f;
    }

    private void Etiqueta(string titulo, float y)
    {
        Text(Place(Node(titulo, root), Pad, y, LabelW, RowH), labelFont, 19f, Ink, TextAlignmentOptions.MidlineLeft, 5f, true).text = titulo;
    }

    /// <summary>Slider con forma de vía y el valor a la derecha, como Sensibilidad en Controles.</summary>
    public Slider FilaSlider(string titulo, ref float y, float min, float max, out TextMeshProUGUI valor)
    {
        Etiqueta(titulo, y);
        valor = Text(Place(Node("Valor", root), width - Pad - 60f, y, 60f, RowH), labelFont, 18f, Ink, TextAlignmentOptions.MidlineRight);
        Slider slider = CrearSlider(root, ControlX, y + (RowH - 26f) / 2f, ControlW - 76f, min, max);
        y += RowH;
        return slider;
    }

    /// <summary>Opciones cortas una al lado de la otra, como Invertir eje Y en Controles.</summary>
    public Segmentos FilaSegmentos(string titulo, ref float y, string[] opciones, System.Action<int> alElegir)
    {
        Etiqueta(titulo, y);
        const float gap = 4f;
        float w = Mathf.Min(110f, (ControlW - gap * (opciones.Length - 1)) / opciones.Length);
        float letra = opciones.Length >= 4 ? 14f : 16f;
        Segmentos s = new Segmentos { fondos = new Img[opciones.Length], textos = new TextMeshProUGUI[opciones.Length] };
        for (int i = 0; i < opciones.Length; i++)
        {
            int indice = i;
            RectTransform seg = Place(Node(opciones[i], root), ControlX + i * (w + gap), y + (RowH - 30f) / 2f, w, 30f);
            s.fondos[i] = Image(seg, rounded, ChipColor, 3f, true);
            s.textos[i] = Text(Stretch(Node("Texto", seg)), labelFont, letra, Mute, TextAlignmentOptions.Center, 6f, true);
            s.textos[i].text = opciones[i];
            seg.gameObject.AddComponent<ShopPointerTarget>().Clicked = () => alElegir(indice);
        }
        y += RowH;
        return s;
    }

    /// <summary>Valor con flechas a los costados, para listas largas (resolución, calidad).</summary>
    public TextMeshProUGUI FilaSelector(string titulo, ref float y, System.Action anterior, System.Action siguiente)
    {
        Etiqueta(titulo, y);
        float h = 30f, top = y + (RowH - h) / 2f, flecha = 34f;
        Flecha(ControlX, top, flecha, h, "<", anterior);
        RectTransform caja = Place(Node("Valor", root), ControlX + flecha + 4f, top, ControlW - (flecha + 4f) * 2f, h);
        Image(caja, rounded, ChipColor, 3f, true);
        TextMeshProUGUI valor = Text(Stretch(Node("Texto", caja)), labelFont, 16f, Ink, TextAlignmentOptions.Center, 6f, true);
        caja.gameObject.AddComponent<ShopPointerTarget>().Clicked = siguiente;
        Flecha(ControlX + ControlW - flecha, top, flecha, h, ">", siguiente);
        y += RowH;
        return valor;
    }

    private void Flecha(float x, float y, float w, float h, string signo, System.Action alHacerClic)
    {
        RectTransform rect = Place(Node(signo == "<" ? "Anterior" : "Siguiente", root), x, y, w, h);
        Img fondo = Image(rect, rounded, ChipDim, 3f, true);
        TextMeshProUGUI texto = Text(Stretch(Node("Texto", rect)), displayFont, 22f, Ink, TextAlignmentOptions.Center);
        texto.richText = false; // "<" sin cerrar no se toma como etiqueta
        texto.text = signo;
        ShopPointerTarget puntero = rect.gameObject.AddComponent<ShopPointerTarget>();
        puntero.Hovered = () => fondo.color = Accent;
        puntero.Exited = () => fondo.color = ChipDim;
        puntero.Clicked = alHacerClic;
    }

    // ---------------------------------------------------------------------
    // Pie
    // ---------------------------------------------------------------------

    /// <summary>Aplicar (naranja) y Restablecer, alineados como en Controles.</summary>
    public void Pie(ref float y, System.Action aplicar, System.Action restablecer)
    {
        y += 12f;
        Boton(Pad, y, 150f, 40f, "Aplicar", true, aplicar);
        Boton(Pad + 166f, y, 170f, 40f, "Restablecer", false, restablecer);
        y += 40f;
    }

    public void Boton(float x, float y, float w, float h, string etiqueta, bool principal, System.Action alHacerClic)
    {
        RectTransform rect = Place(Node(etiqueta, root), x, y, w, h);
        Color normal = principal ? Accent : ChipColor;
        Img fondo = Image(rect, rounded, normal, 4f, true);
        Text(Stretch(Node("Texto", rect)), displayFont, 17f, principal ? KeyInk : Ink, TextAlignmentOptions.Center, 8f, true).text = etiqueta;
        ShopPointerTarget puntero = rect.gameObject.AddComponent<ShopPointerTarget>();
        puntero.Hovered = () => fondo.color = principal ? Hot : ChipDim;
        puntero.Exited = () => fondo.color = normal;
        puntero.Clicked = alHacerClic;
    }

    // Slider con forma de vía: tramo blanco y un durmiente naranja como cursor (igual que en Controles y la pausa).
    private static Slider CrearSlider(RectTransform parent, float x, float y, float width, float min, float max)
    {
        RectTransform rect = Place(Node("Slider", parent), x, y, width, 26f);

        RectTransform track = Node("Via", rect);
        track.anchorMin = new Vector2(0f, 0.5f);
        track.anchorMax = new Vector2(1f, 0.5f);
        track.sizeDelta = new Vector2(0f, 6f);
        Image(track, null, TrackColor, 0f, true);

        RectTransform fillArea = Node("Relleno", rect);
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.sizeDelta = new Vector2(0f, 6f);
        RectTransform fill = Node("Tramo", fillArea);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.sizeDelta = Vector2.zero;
        Image(fill, null, Ink);

        RectTransform handleArea = Stretch(Node("AreaCursor", rect));
        RectTransform handle = Node("Durmiente", handleArea);
        handle.anchorMin = handle.anchorMax = new Vector2(0f, 0.5f);
        handle.sizeDelta = new Vector2(7f, 20f);
        Img handleImage = Image(handle, null, Accent, 0f, true);

        Slider slider = rect.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.transition = UnityEngine.UI.Selectable.Transition.None;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        return slider;
    }
}
