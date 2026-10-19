using UnityEngine;
using UnityEngine.UI;

// US 022, CA7: el rival mordido por el perro del Callejero ve la pantalla casi negra durante 2,5 s y solo distingue lo
// que tiene a 1 o 2 metros. Se hace con niebla oscura muy cerrada (lo lejano desaparece) y una capa oscura sobre la
// pantalla que deja ver un círculo en el centro. Al terminar vuelve de a poco a como estaba.
public class VistaNublada : MonoBehaviour
{
    private const float Entrada = 0.15f, Salida = 0.6f;
    private const float Claro = 1f, Oscuro = 4f; // m: hasta dónde se ve bien y desde dónde ya no se ve nada
    private static readonly Color Niebla = new Color(0.008f, 0.01f, 0.014f);
    private const float Velo = 0.7f; // lo oscura que queda toda la pantalla, además de la viñeta

    private static VistaNublada actual;
    private static Sprite vineta;

    private bool niebla;
    private FogMode modo;
    private Color color;
    private float inicio, fin, densidad;
    private float hasta, empieza;
    private Image capa, centro;

    /// <summary>Nubla la vista del jugador de esta computadora esos segundos (si ya estaba nublada, la alarga).</summary>
    public static void Aplicar(float segundos)
    {
        if (actual == null)
        {
            actual = new GameObject("Vista nublada").AddComponent<VistaNublada>();
            actual.Armar();
        }
        actual.hasta = Mathf.Max(actual.hasta, Time.time + segundos);
    }

    private void Armar()
    {
        empieza = Time.time;
        niebla = RenderSettings.fog;
        modo = RenderSettings.fogMode;
        color = RenderSettings.fogColor;
        inicio = RenderSettings.fogStartDistance;
        fin = RenderSettings.fogEndDistance;
        densidad = RenderSettings.fogDensity;

        Canvas lienzo = gameObject.AddComponent<Canvas>();
        lienzo.renderMode = RenderMode.ScreenSpaceOverlay;
        lienzo.sortingOrder = 450;
        centro = Capa("Viñeta", Vineta());
        capa = Capa("Oscuro", null);
    }

    private Image Capa(string nombre, Sprite sprite)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        Image imagen = go.AddComponent<Image>();
        imagen.sprite = sprite;
        imagen.raycastTarget = false;
        RectTransform r = imagen.rectTransform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        return imagen;
    }

    private void Update()
    {
        // Cuánto está nublada: entra rápido y sale despacio.
        float k = Mathf.Min(Mathf.Clamp01((Time.time - empieza) / Entrada), Mathf.Clamp01((hasta - Time.time) / Salida + 0f));
        if (Time.time >= hasta) { Destroy(gameObject); return; }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Color.Lerp(color, Niebla, k);
        float lejos = niebla && modo == FogMode.Linear ? fin : 300f;
        RenderSettings.fogStartDistance = Mathf.Lerp(niebla && modo == FogMode.Linear ? inicio : 150f, Claro, k);
        RenderSettings.fogEndDistance = Mathf.Lerp(lejos, Oscuro, k);

        // La viñeta se ajusta a la pantalla para que el círculo quede redondo.
        float lado = Mathf.Max(Screen.width, Screen.height) * 1.6f;
        RectTransform r = centro.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(lado, lado);
        centro.color = new Color(1f, 1f, 1f, k);
        capa.color = new Color(Niebla.r, Niebla.g, Niebla.b, Velo * k);
    }

    private void OnDestroy()
    {
        RenderSettings.fog = niebla;
        RenderSettings.fogMode = modo;
        RenderSettings.fogColor = color;
        RenderSettings.fogStartDistance = inicio;
        RenderSettings.fogEndDistance = fin;
        RenderSettings.fogDensity = densidad;
        if (actual == this) actual = null;
    }

    // Transparente en un círculo chico del centro y casi negro alrededor.
    private static Sprite Vineta()
    {
        if (vineta != null) return vineta;
        const int n = 256;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixeles = new Color32[n * n];
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c; // 0 al centro, 1 en el borde
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.06f, 0.32f, d)) * 0.99f;
                pixeles[y * n + x] = new Color32(2, 3, 4, (byte)(a * 255f));
            }
        tex.SetPixels32(pixeles);
        tex.Apply();
        vineta = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return vineta;
    }
}
