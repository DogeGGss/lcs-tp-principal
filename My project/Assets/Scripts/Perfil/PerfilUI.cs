using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using static ShopUIKit;
using Img = UnityEngine.UI.Image;
using Rareza = PerfilDatos.Rareza;

// PRUEBA DEL MENÚ: piezas comunes del menú nuevo, el Perfil, la Estación del Rift y la tienda.
// Se arman por código sobre ShopUIKit, con la pantalla de referencia de 1920 x 1080 (coordenadas desde arriba a la izquierda).
public static class PerfilUI
{
    public static TMP_FontAsset FTitulo, FRotulo, FTexto, FMono;
    public static Sprite Rounded, Icono;
    public static Sprite[] Borders;

    // Colores de rareza: distintos del naranja de selección y del azul de compañeros.
    public static Color ColorRareza(Rareza r) => r switch
    {
        Rareza.Comun => Hex(0x9AA3B0),
        Rareza.Raro => Hex(0x5C7CFF),
        Rareza.Epico => Hex(0xB061FF),
        Rareza.Legendario => Hex(0xFFC93C),
        _ => Hex(0x8E96A3),
    };

    public static string NombreRareza(Rareza r) => r switch
    {
        Rareza.Comun => "Común",
        Rareza.Raro => "Raro",
        Rareza.Epico => "Épico",
        Rareza.Legendario => "Legendario",
        _ => "Inicial",
    };

    public static string NombreTipo(PerfilDatos.Tipo t) => t switch
    {
        PerfilDatos.Tipo.Avatar => "Avatar",
        PerfilDatos.Tipo.Banner => "Banner",
        PerfilDatos.Tipo.Marco => "Marco",
        _ => "Título",
    };

    public static string Rich(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

    public static readonly Color ColorCreditos = Hex(0xD6DAE0);
    public static readonly Color ColorBoletos = Hex(0xF29A38);
    public static readonly Color ColorCristales = Hex(0x9B7BFF);

    // ================= Básicos =================

    public static TextMeshProUGUI T(RectTransform parent, string name, float x, float y, float w, float h, TMP_FontAsset font, float size,
        Color color, string text, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, float spacing = 0f, bool upper = false)
    {
        TextMeshProUGUI t = Text(Place(Node(name, parent), x, y, w, h), font, size, color, align, spacing, upper);
        t.text = text;
        return t;
    }

    public static Img Panel(RectTransform parent, string name, float x, float y, float w, float h, Color color, float radius = 8f, bool raycast = false)
    {
        return Image(Place(Node(name, parent), x, y, w, h), radius > 0f ? Rounded : null, color, radius, raycast);
    }

    public static Img Borde(RectTransform rect, float radius, Color color)
    {
        Sprite sprite = Borders != null && Borders.Length > 0 ? Borders[radius > 7f && Borders.Length > 1 ? 1 : 0] : null;
        if (sprite == null) return null;
        Img image = Image(Stretch(Node("Borde", rect)), sprite, color);
        image.type = Img.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
        return image;
    }

    /// <summary>Imagen que llena el contenedor sin deformarse (recorta lo que sobra), como object-fit: cover.</summary>
    public static Img Cubrir(RectTransform contenedor, Sprite sprite, float anclaX = 0.5f, float anclaY = 0.5f)
    {
        if (contenedor.GetComponent<UnityEngine.UI.RectMask2D>() == null) contenedor.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        RectTransform rect = Node("Imagen", contenedor);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(anclaX, anclaY);
        Img img = Image(rect, sprite, Color.white);
        AjustarCubrir(img, contenedor);
        return img;
    }

    public static void AjustarCubrir(Img img, RectTransform contenedor)
    {
        Vector2 caja = contenedor.sizeDelta;
        if (img.sprite == null) { img.rectTransform.sizeDelta = caja; return; }
        float aspecto = img.sprite.rect.width / img.sprite.rect.height;
        float w = caja.x, h = caja.x / aspecto;
        if (h < caja.y) { h = caja.y; w = h * aspecto; }
        img.rectTransform.sizeDelta = new Vector2(w, h);
        img.rectTransform.anchoredPosition = Vector2.zero;
    }

    private static readonly Dictionary<string, Sprite[]> mitadesMarco = new Dictionary<string, Sprite[]>();

    /// <summary>
    /// Pone (o saca) el marco alrededor de un recuadro (x, y, w, h) de su padre. Va en dos mitades estiradas por partes:
    /// las esquinas y el adorno del centro no se deforman; solo se alargan (o se repiten) los tramos del riel.
    /// El centro del riel queda sobre el borde del recuadro; con adentro, el marco entero entra en el recuadro (para las cartas).
    /// encimaDe: el marco va justo encima de ese hermano (debajo de lo que sigue); si no, arriba de todo.
    /// </summary>
    public static RectTransform Enmarcar(ref RectTransform marco, RectTransform parent, float x, float y, float w, float h,
        PerfilDatos.Cosmetico c, bool adentro = false, Transform encimaDe = null)
    {
        Sprite sprite = PerfilDatos.Imagen(c);
        if (sprite == null || !PerfilDatos.RielesMarco.TryGetValue(c.id, out Vector4 riel))
        {
            if (marco != null) marco.gameObject.SetActive(false);
            return marco;
        }
        if (marco == null)
        {
            marco = Node("Marco", parent);
            Image(Node("Izquierda", marco), null, Color.white);
            Image(Node("Derecha", marco), null, Color.white);
        }
        marco.gameObject.SetActive(true);
        marco.SetAsLastSibling();
        if (encimaDe != null) marco.SetSiblingIndex(encimaDe.GetSiblingIndex() + 1);
        // Escala: el alto del recuadro manda (el riel queda del mismo grosor arriba y a los costados).
        if (adentro) riel = Vector4.zero;
        float s = h / (sprite.rect.height - riel.y - riel.w);
        float ancho = w + (riel.x + riel.z) * s, alto = h + (riel.y + riel.w) * s;
        Place(marco, x - riel.x * s, y - riel.y * s, ancho, alto);
        Sprite[] mitades = Mitades(c.id, sprite);
        bool repetir = PerfilDatos.Cortes(c.id).repetir;
        for (int i = 0; i < 2; i++)
        {
            Img mitad = marco.GetChild(i).GetComponent<Img>();
            mitad.sprite = mitades[i];
            mitad.type = repetir ? Img.Type.Tiled : Img.Type.Sliced;
            mitad.pixelsPerUnitMultiplier = 1f / s;
            Place(mitad.rectTransform, i * ancho / 2f, 0f, ancho / 2f, alto);
        }
        return marco;
    }

    /// <summary>Las dos mitades del marco, con sus bordes: la esquina afuera y medio adorno del centro adentro.</summary>
    private static Sprite[] Mitades(string id, Sprite sprite)
    {
        if (mitadesMarco.TryGetValue(id, out Sprite[] mitades) && mitades[0] != null) return mitades;
        var (esquina, centro, _) = PerfilDatos.Cortes(id);
        Rect r = sprite.rect;
        float medio = Mathf.Floor(r.width / 2f), arriba = sprite.border.w, abajo = sprite.border.y;
        Vector2 pivote = new Vector2(0.5f, 0.5f);
        mitades = new[]
        {
            Sprite.Create(sprite.texture, new Rect(r.x, r.y, medio, r.height), pivote, sprite.pixelsPerUnit, 0,
                SpriteMeshType.FullRect, new Vector4(esquina, abajo, centro, arriba)),
            Sprite.Create(sprite.texture, new Rect(r.x + medio, r.y, r.width - medio, r.height), pivote, sprite.pixelsPerUnit, 0,
                SpriteMeshType.FullRect, new Vector4(centro, abajo, esquina, arriba)),
        };
        mitadesMarco[id] = mitades;
        return mitades;
    }

    private static readonly Dictionary<string, Sprite> degrades = new Dictionary<string, Sprite>();

    /// <summary>Degradé negro: horizontal (de izquierda a derecha) o vertical (de abajo hacia arriba).</summary>
    public static Sprite Degrade(bool horizontal, float desde, float hasta, float corte = 1f)
    {
        string key = $"{horizontal}{desde}{hasta}{corte}";
        if (degrades.TryGetValue(key, out Sprite s) && s != null) return s;
        var tex = horizontal ? new Texture2D(256, 1, TextureFormat.RGBA32, false) : new Texture2D(1, 256, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int i = 0; i < 256; i++)
        {
            float t = Mathf.Clamp01(i / 255f / corte);
            float a = DarkAlpha(Mathf.Lerp(desde, hasta, Mathf.SmoothStep(0f, 1f, t)));
            if (horizontal) tex.SetPixel(i, 0, new Color(0f, 0f, 0f, a)); else tex.SetPixel(0, i, new Color(0f, 0f, 0f, a));
        }
        tex.Apply();
        s = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        degrades[key] = s;
        return s;
    }

    public static T Figura<T>(RectTransform parent, string name, float x, float y, Color color) where T : UIShape
    {
        RectTransform rect = Node(name, parent);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = Vector2.one;
        rect.gameObject.AddComponent<CanvasRenderer>();
        T shape = rect.gameObject.AddComponent<T>();
        shape.color = color;
        shape.raycastTarget = false;
        return shape;
    }

    public static UIRing Circulo(RectTransform parent, float cx, float cy, float radio, Color color, float grosor = 0f)
    {
        UIRing ring = Figura<UIRing>(parent, "Circulo", cx, cy, color);
        ring.radius = radio;
        ring.thickness = grosor;
        return ring;
    }

    public static UIPolygon Poligono(RectTransform parent, string name, Color color, params Vector2[] puntos)
    {
        UIPolygon poly = Figura<UIPolygon>(parent, name, 0f, 0f, color);
        poly.SetPoints(puntos);
        return poly;
    }

    /// <summary>Íconos de las monedas, centrados en (cx, cy).</summary>
    public static void IconoMoneda(RectTransform parent, int moneda, float cx, float cy, float s)
    {
        if (moneda == 0)
        {
            Circulo(parent, cx, cy, s * 0.5f, ColorCreditos);
            Circulo(parent, cx, cy, s * 0.3f, Hex(0x0A0C11), s * 0.1f);
        }
        else if (moneda == 1)
        {
            float w = s * 1.15f, h = s * 0.72f;
            Poligono(parent, "Boleto", ColorBoletos,
                new Vector2(cx - w / 2f, cy - h / 2f), new Vector2(cx + w / 2f, cy - h / 2f), new Vector2(cx + w / 2f, cy - h * 0.15f),
                new Vector2(cx + w * 0.38f, cy), new Vector2(cx + w / 2f, cy + h * 0.15f), new Vector2(cx + w / 2f, cy + h / 2f),
                new Vector2(cx - w / 2f, cy + h / 2f), new Vector2(cx - w / 2f, cy + h * 0.15f), new Vector2(cx - w * 0.38f, cy),
                new Vector2(cx - w / 2f, cy - h * 0.15f));
        }
        else
        {
            float r = s * 0.55f;
            Poligono(parent, "Cristal", ColorCristales,
                new Vector2(cx, cy - r), new Vector2(cx + r * 0.72f, cy - r * 0.2f), new Vector2(cx, cy + r), new Vector2(cx - r * 0.72f, cy - r * 0.2f));
            Poligono(parent, "Brillo", WithAlpha(Color.white, 0.35f),
                new Vector2(cx, cy - r), new Vector2(cx + r * 0.72f, cy - r * 0.2f), new Vector2(cx, cy - r * 0.05f));
        }
    }

    /// <summary>Candado chico, centrado en (cx, cy).</summary>
    public static void Candado(RectTransform parent, float cx, float cy, float s, Color color)
    {
        Circulo(parent, cx, cy - s * 0.18f, s * 0.26f, color, s * 0.1f);
        Image(Place(Node("Cuerpo", parent), cx - s * 0.36f, cy - s * 0.12f, s * 0.72f, s * 0.56f), Rounded, color, 3f);
        Image(Place(Node("Ojo", parent), cx - s * 0.05f, cy + s * 0.04f, s * 0.1f, s * 0.2f), null, Hex(0x0A0C11));
    }

    // ================= Botones =================

    public class Boton
    {
        public RectTransform rect;
        public Img fondo, borde;
        public TextMeshProUGUI texto;
        public UnityEngine.UI.Button button;
        public bool primario, activo = true, encima;
        public Color color;

        public void Habilitar(bool on)
        {
            activo = on;
            button.interactable = on;
            Pintar();
        }

        public void Pintar()
        {
            if (primario)
            {
                fondo.color = !activo ? ChipColor : encima ? Color.Lerp(color, Color.white, 0.22f) : color;
                texto.color = activo ? AccentInk : Mute;
            }
            else
            {
                fondo.color = !activo ? WithAlpha(Color.black, 0.2f) : encima ? Over(White(0.12f), PanelBase) : Over(White(0.04f), PanelBase);
                if (borde != null) borde.color = activo && encima ? color : White(activo ? 0.3f : 0.12f);
                texto.color = activo ? Ink : Mute;
            }
        }
    }

    public static Boton CrearBoton(RectTransform parent, float x, float y, float w, float h, string label, bool primary, UnityAction onClick,
        float size = 26f, Color? color = null)
    {
        var b = new Boton { primario = primary, color = color ?? Accent };
        b.rect = Place(Node("Boton " + label, parent), x, y, w, h);
        b.fondo = Image(b.rect, Rounded, b.color, 6f, true);
        if (!primary) b.borde = Borde(b.rect, 6f, White(0.3f));
        b.texto = Text(Stretch(Node("Texto", b.rect)), FTitulo, size, Ink, TextAlignmentOptions.Center, 6f, true);
        b.texto.text = label;
        b.button = b.rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        b.button.transition = UnityEngine.UI.Selectable.Transition.None;
        if (onClick != null) b.button.onClick.AddListener(onClick);
        var hover = b.rect.gameObject.AddComponent<ShopPointerTarget>();
        hover.Hovered = () => { b.encima = true; b.Pintar(); };
        hover.Exited = () => { b.encima = false; b.Pintar(); };
        b.rect.gameObject.AddComponent<ButtonHoverAnimation>();
        // ButtonHoverAnimation escala desde el pivote: al centro, así crece parejo.
        Vector2 size2 = b.rect.sizeDelta;
        b.rect.pivot = new Vector2(0.5f, 0.5f);
        b.rect.anchoredPosition += new Vector2(size2.x / 2f, -size2.y / 2f);
        b.Pintar();
        return b;
    }

    /// <summary>Zona clickeable invisible (tarjetas, filas, la credencial del jugador).</summary>
    public static ShopPointerTarget Clickeable(RectTransform rect, Action alClic)
    {
        if (rect.GetComponent<Img>() == null) Image(rect, null, White(0f), 0f, true);
        else rect.GetComponent<Img>().raycastTarget = true;
        var p = rect.gameObject.AddComponent<ShopPointerTarget>();
        p.Clicked = alClic;
        rect.gameObject.AddComponent<SonidoAlPasar>();
        return p;
    }

    // ================= Scroll =================

    public static UnityEngine.UI.ScrollRect Scroll(RectTransform parent, float x, float y, float w, float h, out RectTransform contenido)
    {
        RectTransform vista = Place(Node("Scroll", parent), x, y, w, h);
        Image(vista, null, White(0f), 0f, true);
        vista.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        contenido = Node("Contenido", vista);
        contenido.anchorMin = contenido.anchorMax = contenido.pivot = new Vector2(0f, 1f);
        contenido.anchoredPosition = Vector2.zero;
        contenido.sizeDelta = new Vector2(w, h);
        var sr = vista.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        sr.content = contenido;
        sr.viewport = vista;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 60f;
        sr.inertia = true;
        return sr;
    }

    public static void BorrarHijos(Transform t)
    {
        // Se sacan del padre antes de destruirlos: así childCount y el armado de ese mismo cuadro ya no los ven.
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            Transform hijo = t.GetChild(i);
            hijo.SetParent(null, false);
            UnityEngine.Object.Destroy(hijo.gameObject);
        }
    }

    // ================= Barra de monedas =================

    public class Monedas
    {
        private readonly TextMeshProUGUI[] cantidades = new TextMeshProUGUI[3];

        public Monedas(RectTransform parent, float derecha, float y, Action alSumarBoletos, Action alSumarCristales)
        {
            // Se arma de derecha a izquierda: cristales, boletos, créditos.
            string[] nombres = { "Créditos", "Boletos", "Cristales" };
            string[] ayudas = { "Se ganan jugando", "Abren cápsulas", "Moneda de pago" };
            Action[] sumar = { null, alSumarBoletos, alSumarCristales };
            float x = derecha;
            for (int i = 2; i >= 0; i--)
            {
                float w = sumar[i] != null ? 214f : 186f;
                x -= w;
                RectTransform chip = Place(Node("Moneda " + nombres[i], parent), x, y, w, 56f);
                Image(chip, Rounded, PanelColor, 8f, true);
                Borde(chip, 8f, White(0.08f));
                IconoMoneda(chip, i, 28f, 28f, 22f);
                cantidades[i] = T(chip, "Cantidad", 50f, 6f, 120f, 30f, FTitulo, 28f, Ink, "0");
                T(chip, "Nombre", 50f, 33f, 140f, 16f, FRotulo, 13f, Mute, ayudas[i], TextAlignmentOptions.MidlineLeft, 8f, true);
                if (sumar[i] != null)
                {
                    Action accion = sumar[i];
                    RectTransform mas = Place(Node("Sumar", chip), w - 40f, 12f, 30f, 30f);
                    Img fondo = Image(mas, Rounded, i == 1 ? ColorBoletos : ColorCristales, 6f, true);
                    T(mas, "Mas", 0f, -1f, 30f, 30f, FTitulo, 26f, AccentInk, "+", TextAlignmentOptions.Center);
                    var p = Clickeable(mas, accion);
                    Color baseColor = fondo.color;
                    p.Hovered = () => fondo.color = Color.Lerp(baseColor, Color.white, 0.3f);
                    p.Exited = () => fondo.color = baseColor;
                }
                x -= 10f;
            }
            Refrescar();
        }

        public void Refrescar()
        {
            cantidades[0].text = PerfilDatos.Numero(PerfilDatos.Creditos);
            cantidades[1].text = PerfilDatos.Numero(PerfilDatos.Boletos);
            cantidades[2].text = PerfilDatos.Numero(PerfilDatos.Cristales);
        }
    }

    // ================= Avisos =================

    /// <summary>Cartel que aparece abajo al centro y se va solo (MenuRiftwalker lo anima).</summary>
    public class Aviso
    {
        public RectTransform rect;
        public TextMeshProUGUI texto;
        public CanvasGroup grupo;
        public float hasta;

        public Aviso(RectTransform parent)
        {
            rect = Place(Node("Aviso", parent), 560f, 960f, 800f, 64f);
            grupo = rect.gameObject.AddComponent<CanvasGroup>();
            grupo.blocksRaycasts = false;
            Image(rect, Rounded, BarColor, 8f);
            Borde(rect, 8f, WithAlpha(Accent, 0.6f));
            texto = Text(Stretch(Node("Texto", rect)), FRotulo, 24f, Ink, TextAlignmentOptions.Center, 4f);
            grupo.alpha = 0f;
        }

        public void Mostrar(string mensaje, float segundos = 2.6f)
        {
            texto.text = mensaje;
            rect.SetAsLastSibling();
            hasta = Time.unscaledTime + segundos;
        }

        public void Actualizar()
        {
            float objetivo = Time.unscaledTime < hasta ? 1f : 0f;
            grupo.alpha = Mathf.MoveTowards(grupo.alpha, objetivo, Time.unscaledDeltaTime * 5f);
        }
    }
}
