using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Lente de la mira telescópica (US 009), con los colores de la guía de diseño: todo oscuro menos un círculo que ocupa
// el alto de la pantalla, la retícula en naranja (#F29A38), el zoom a la izquierda y la distancia al blanco a la
// derecha. Va debajo del HUD de combate (40), así la vida y la munición se siguen viendo.
public class MiraTelescopicaHud : MonoBehaviour
{
    private static readonly Color Naranja = new Color32(0xF2, 0x9A, 0x38, 0xFF);
    private static readonly Color Tinta = new Color32(0xF3, 0xF4, 0xF6, 0xFF);
    private static readonly Color Apagado = new Color32(0x8E, 0x96, 0xA3, 0xFF);
    private static readonly Color32 Fondo = new Color32(0x07, 0x09, 0x0C, 0xFF);
    private static readonly Color Sombra = new Color(0f, 0f, 0f, 0.55f);

    private const float Alto = 1080f;           // la lente ocupa todo el alto de la pantalla
    private const float RadioLente = 0.47f;     // radio del círculo, respecto del alto
    private const float Hueco = 16f;            // hueco de la retícula alrededor del centro
    private const float TiempoAparecer = 0.06f;

    private static MiraTelescopicaHud instancia;
    private static Texture2D mascara;

    private MiraTelescopica mira;
    private CanvasGroup grupo;
    private TextMeshProUGUI distancia, zoom;
    private float abiertaEn;

    public static void Mostrar(MiraTelescopica mira)
    {
        if (instancia == null)
        {
            instancia = new GameObject("MiraTelescopicaHud").AddComponent<MiraTelescopicaHud>();
            instancia.Construir(mira.fuenteNumeros, mira.fuenteTextos);
        }
        instancia.mira = mira;
        if (!instancia.gameObject.activeSelf)
        {
            instancia.gameObject.SetActive(true);
            instancia.abiertaEn = Time.unscaledTime;
            instancia.grupo.alpha = 0f;
        }
        instancia.Refrescar();
    }

    public static void Ocultar()
    {
        if (instancia != null) instancia.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (mira == null || !MiraTelescopica.Puesta)
        {
            gameObject.SetActive(false);
            return;
        }
        grupo.alpha = Mathf.Clamp01((Time.unscaledTime - abiertaEn) / TiempoAparecer);
        Refrescar();
    }

    private void Refrescar()
    {
        zoom.text = mira.ZoomActual.ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("es-AR")) + "×";
        float metros = mira.DistanciaAlBlanco();
        distancia.text = metros < 0f ? "—" : $"{Mathf.RoundToInt(metros)} m";
    }

    // ---------- Armado ----------

    private void Construir(TMP_FontAsset fuenteNumeros, TMP_FontAsset fuenteTextos)
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 39; // debajo del HUD de combate (40)
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, Alto);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f; // el círculo siempre ocupa el alto
        grupo = gameObject.AddComponent<CanvasGroup>();
        grupo.interactable = false;
        grupo.blocksRaycasts = false;
        RectTransform raiz = (RectTransform)transform;

        // Lente: la textura es oscura afuera del círculo; a los costados, dos franjas del mismo color.
        RawImage lente = Nodo("Lente", raiz).gameObject.AddComponent<RawImage>();
        lente.texture = Mascara();
        lente.raycastTarget = false;
        Centrado(lente.rectTransform, Vector2.zero, new Vector2(Alto, Alto));
        Franja("Izquierda", raiz, new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(-Alto * 0.5f + 2f, 0f));
        Franja("Derecha", raiz, new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(Alto * 0.5f - 2f, 0f), Vector2.zero);

        // Retícula: líneas finas cerca del centro y postes gruesos hacia el borde (izquierda, derecha y abajo).
        RectTransform reticula = Nodo("Reticula", raiz);
        Centrado(reticula, Vector2.zero, Vector2.zero);
        float radio = Alto * RadioLente, fina = radio * 0.55f;
        Linea(reticula, "Derecha", new Vector2((Hueco + fina) * 0.5f, 0f), new Vector2(fina - Hueco, 2f));
        Linea(reticula, "Izquierda", new Vector2(-(Hueco + fina) * 0.5f, 0f), new Vector2(fina - Hueco, 2f));
        Linea(reticula, "Abajo", new Vector2(0f, -(Hueco + fina) * 0.5f), new Vector2(2f, fina - Hueco));
        Linea(reticula, "Arriba", new Vector2(0f, (Hueco + radio) * 0.5f), new Vector2(2f, radio - Hueco));
        Linea(reticula, "PosteDerecha", new Vector2((fina + radio) * 0.5f, 0f), new Vector2(radio - fina, 7f));
        Linea(reticula, "PosteIzquierda", new Vector2(-(fina + radio) * 0.5f, 0f), new Vector2(radio - fina, 7f));
        Linea(reticula, "PosteAbajo", new Vector2(0f, -(fina + radio) * 0.5f), new Vector2(7f, radio - fina));
        Linea(reticula, "Centro", Vector2.zero, new Vector2(4f, 4f));
        for (int i = 1; i * 40f < fina; i++)
        {
            float largo = i % 3 == 0 ? 16f : 9f;
            Linea(reticula, "MarcaDerecha", new Vector2(i * 40f, 0f), new Vector2(2f, largo));
            Linea(reticula, "MarcaIzquierda", new Vector2(-i * 40f, 0f), new Vector2(2f, largo));
            Linea(reticula, "MarcaAbajo", new Vector2(0f, -i * 40f), new Vector2(largo, 2f));
        }

        // Datos: zoom a la izquierda y distancia a la derecha, arriba de los postes, sobre una placa oscura para que se
        // lean sobre el cielo o una pared clara.
        float x = radio * 0.74f;
        Placa(reticula, "PlacaZoom", new Vector2(-x, 50f));
        Placa(reticula, "PlacaDistancia", new Vector2(x, 50f));
        Texto(reticula, "ZoomTitulo", fuenteTextos, "ZOOM", 15f, Apagado, new Vector2(-x, 64f));
        zoom = Texto(reticula, "Zoom", fuenteNumeros, "", 30f, Tinta, new Vector2(-x, 34f));
        Texto(reticula, "DistanciaTitulo", fuenteTextos, "DISTANCIA", 15f, Apagado, new Vector2(x, 64f));
        distancia = Texto(reticula, "Distancia", fuenteNumeros, "", 30f, Tinta, new Vector2(x, 34f));

        gameObject.SetActive(false);
    }

    private static RectTransform Nodo(string nombre, Transform padre)
    {
        RectTransform nodo = new GameObject(nombre, typeof(RectTransform)).GetComponent<RectTransform>();
        nodo.SetParent(padre, false);
        return nodo;
    }

    private static void Centrado(RectTransform nodo, Vector2 posicion, Vector2 tamano)
    {
        nodo.anchorMin = nodo.anchorMax = nodo.pivot = new Vector2(0.5f, 0.5f);
        nodo.anchoredPosition = posicion;
        nodo.sizeDelta = tamano;
    }

    private static void Franja(string nombre, Transform padre, Vector2 anclaMin, Vector2 anclaMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        Image franja = Nodo(nombre, padre).gameObject.AddComponent<Image>();
        franja.color = Fondo;
        franja.raycastTarget = false;
        franja.rectTransform.anchorMin = anclaMin;
        franja.rectTransform.anchorMax = anclaMax;
        franja.rectTransform.offsetMin = offsetMin;
        franja.rectTransform.offsetMax = offsetMax;
    }

    private static void Linea(Transform padre, string nombre, Vector2 posicion, Vector2 tamano)
    {
        Image linea = Nodo(nombre, padre).gameObject.AddComponent<Image>();
        linea.color = Naranja;
        linea.raycastTarget = false;
        Centrado(linea.rectTransform, posicion, tamano);
        Outline borde = linea.gameObject.AddComponent<Outline>(); // se lee sobre el cielo y sobre paredes claras
        borde.effectColor = Sombra;
        borde.effectDistance = new Vector2(1f, -1f);
    }

    private static void Placa(Transform padre, string nombre, Vector2 posicion)
    {
        Image placa = Nodo(nombre, padre).gameObject.AddComponent<Image>();
        Color color = Fondo;
        color.a = 0.6f;
        placa.color = color;
        placa.raycastTarget = false;
        Centrado(placa.rectTransform, posicion, new Vector2(150f, 70f));
    }

    private static TextMeshProUGUI Texto(Transform padre, string nombre, TMP_FontAsset fuente, string texto, float tamano, Color color, Vector2 posicion)
    {
        TextMeshProUGUI tmp = Nodo(nombre, padre).gameObject.AddComponent<TextMeshProUGUI>();
        if (fuente != null) tmp.font = fuente;
        tmp.text = texto;
        tmp.fontSize = tamano;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.characterSpacing = tamano < 20f ? 8f : 0f;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        Centrado(tmp.rectTransform, posicion, new Vector2(220f, 40f));
        return tmp;
    }

    // Oscuro afuera del círculo, con el borde de la lente suave, un poco de viñeta adentro y un aro naranja fino.
    private static Texture2D Mascara()
    {
        if (mascara != null) return mascara;
        const int lado = 1024;
        float centro = (lado - 1) * 0.5f, radio = lado * RadioLente, borde = 2.5f;
        Color32[] pixeles = new Color32[lado * lado];
        for (int y = 0; y < lado; y++)
        {
            for (int x = 0; x < lado; x++)
            {
                float r = Mathf.Sqrt((x - centro) * (x - centro) + (y - centro) * (y - centro));
                float afuera = Mathf.Clamp01((r - radio) / borde + 0.5f);
                float vineta = Mathf.SmoothStep(0f, 0.55f, Mathf.InverseLerp(radio * 0.82f, radio, r));
                float aro = Mathf.Clamp01(1f - Mathf.Abs(r - (radio - 3f)) / 1.5f) * 0.6f;
                float alfa = Mathf.Max(afuera, Mathf.Max(vineta, aro));
                Color color = Color.Lerp((Color)Fondo, Naranja, afuera >= 1f ? 0f : aro / Mathf.Max(alfa, 0.001f));
                color.a = alfa;
                pixeles[y * lado + x] = color;
            }
        }
        mascara = new Texture2D(lado, lado, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "MascaraMiraTelescopica" };
        mascara.SetPixels32(pixeles);
        mascara.Apply(false, true);
        return mascara;
    }
}
