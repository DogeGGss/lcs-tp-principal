using System.Globalization;
using TMPro;
using UnityEngine;

// Colores, medidas y piezas básicas de la interfaz de la tienda.
// Las posiciones son en píxeles de una pantalla de referencia de 1920 x 1080, desde arriba a la izquierda.
public static class ShopUIKit
{
    // La maqueta mezcla las transparencias como el navegador (en espacio gamma). El proyecto usa espacio
    // lineal, donde el mismo alfa deja ver mucho más el fondo. Por eso las capas oscuras compensan el alfa
    // y lo que va apoyado sobre un panel se precalcula opaco, con el color que da la mezcla de la maqueta.
    private static readonly bool Linear = QualitySettings.activeColorSpace == ColorSpace.Linear;

    // Alfa de una capa oscura sobre algo más claro (paneles, el oscurecido del juego).
    public static float DarkAlpha(float a) => Linear ? 1f - Mathf.Pow(1f - a, 2.2f) : a;

    // Alfa de algo claro que se desvanece sobre un fondo oscuro (brillos, filas deshabilitadas).
    public static float FadeAlpha(float a) => Linear ? Mathf.Pow(a, 2.2f) : a;

    // Color opaco que resulta de poner "top" (con su alfa) encima de "bottom", mezclado como en la maqueta.
    public static Color Over(Color top, Color bottom) => new Color(
        Mathf.Lerp(bottom.r, top.r, top.a), Mathf.Lerp(bottom.g, top.g, top.a), Mathf.Lerp(bottom.b, top.b, top.a), 1f);

    public static Color White(float a) => new Color(1f, 1f, 1f, a);

    // Cómo se ve un panel sobre el juego oscurecido, y el fondo del reloj y del aviso del HUD.
    public static readonly Color PanelBase = Rgb(15, 18, 24);
    public static readonly Color HudBase = Rgb(14, 16, 20);

    public static readonly Color PanelColor = Rgb(10, 12, 17, DarkAlpha(0.88f));
    public static readonly Color BarColor = Rgb(8, 10, 14, DarkAlpha(0.94f));
    public static readonly Color HudColor = Rgb(8, 10, 14, DarkAlpha(0.72f));
    public static readonly Color ShadeColor = new Color(0f, 0f, 0f, DarkAlpha(0.5f));
    public static readonly Color LineColor = Over(White(0.09f), PanelBase);
    public static readonly Color Ink = Hex(0xF3F4F6);
    public static readonly Color Mute = Hex(0x8E96A3);
    public static readonly Color Soft = Hex(0xD6DAE0);
    public static readonly Color Note = Hex(0xAEB5C0);
    public static readonly Color Accent = Hex(0xF29A38);
    public static readonly Color AccentInk = Hex(0x1A1106);
    public static readonly Color Hot = Hex(0xFFD2A1);
    public static readonly Color Bad = Hex(0xFF5C5C);
    public static readonly Color Ok = Hex(0x3DDC97);
    public static readonly Color ShieldColor = Hex(0x4CC3FF);
    public static readonly Color HoverColor = Over(White(0.06f), PanelBase);
    public static readonly Color SelectedColor = Over(Rgb(242, 154, 56, 0.13f), PanelBase);
    public static readonly Color ChipDim = Over(White(0.14f), PanelBase);
    public static readonly Color ChipColor = Over(White(0.07f), PanelBase);
    public static readonly Color BoxColor = Over(White(0.05f), PanelBase);
    public static readonly Color FxColor = Over(White(0.04f), PanelBase);
    public static readonly Color TrackColor = Over(White(0.12f), PanelBase);
    public static readonly Color KeyInk = Rgb(17, 17, 17);

    public static Color Rgb(int r, int g, int b, float a = 1f) => new Color(r / 255f, g / 255f, b / 255f, a);

    public static Color Hex(int rgb, float a = 1f) => Rgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF, a);

    public static Color WithAlpha(Color color, float a) => new Color(color.r, color.g, color.b, a);

    // "$ 4.200" (con espacio, como en la maqueta) o "$4.200" para los rangos de precio.
    public static string Money(int amount, bool spaced = true)
    {
        string digits = Mathf.Abs(amount).ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".");
        return (amount < 0 ? "-" : "") + (spaced ? "$ " : "$") + digits;
    }

    public static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');

    public static RectTransform Node(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5; // UI
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // Ubica un elemento desde la esquina superior izquierda de su padre.
    public static RectTransform Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    public static RectTransform Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0f, 1f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    // radius: radio de las esquinas en píxeles; el sprite redondeado tiene 16 px de borde.
    public static UnityEngine.UI.Image Image(RectTransform rect, Sprite sprite, Color color, float radius = 0f, bool raycast = false)
    {
        UnityEngine.UI.Image image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = raycast;
        if (sprite != null && radius > 0f)
        {
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 16f / radius;
        }
        return image;
    }

    // spacing: espaciado entre letras en centésimas de em (letter-spacing .12em = 12).
    public static TextMeshProUGUI Text(RectTransform rect, TMP_FontAsset font, float size, Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft, float spacing = 0f, bool upper = false)
    {
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.characterSpacing = spacing;
        text.fontStyle = upper ? FontStyles.UpperCase : FontStyles.Normal;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.richText = true;
        text.margin = Vector4.zero;
        return text;
    }

    // Ancho que ocupa un texto con su tipografía y estilo actuales.
    public static float Width(TextMeshProUGUI text, string value)
    {
        return text.GetPreferredValues(value, 4000f, 400f).x;
    }

    // Una línea recta entre dos puntos (coordenadas desde arriba a la izquierda del padre).
    public static UnityEngine.UI.Image Segment(Transform parent, Vector2 from, Vector2 to, float thickness, Color color)
    {
        RectTransform rect = Node("Linea", parent);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(from.x, -from.y);
        Vector2 delta = to - from;
        rect.sizeDelta = new Vector2(delta.magnitude, thickness);
        rect.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        return Image(rect, null, color);
    }

    public static void DashedLine(Transform parent, Vector2 from, Vector2 to, float dash, float gap, Color color)
    {
        float length = Vector2.Distance(from, to);
        Vector2 direction = (to - from).normalized;
        for (float d = 0f; d < length; d += dash + gap)
            Segment(parent, from + direction * d, from + direction * Mathf.Min(length, d + dash), 1f, color);
    }
}
