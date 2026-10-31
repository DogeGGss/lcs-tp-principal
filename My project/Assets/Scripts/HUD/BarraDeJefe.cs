using TMPro;
using UnityEngine;
using UnityEngine.UI;

// F11 (NPCs), US 146, CA4: la barra de vida del jefe. Desde que aparece y mientras está vivo, arriba al centro de la
// pantalla (debajo del marcador) se ve su nombre y una barra con su vida, que baja con cada impacto (con un tramo claro
// que la sigue un instante, para ver cuánto le sacó el último golpe). Tiene una marca en la mitad, donde empieza la
// furia (US 268). Al morir el jefe, la barra se apaga y desaparece.
// La crea Npc.Crear para los tipos con esJefe.
public class BarraDeJefe : MonoBehaviour
{
    /// <summary>Dónde empieza la furia (fracción de la vida): la marca de la barra (US 268).</summary>
    public const float Furia = 0.5f;

    private const float Ancho = 640f, Alto = 12f, Abajo = 118f; // px de la pantalla de 1920 × 1080
    // En Invasión el marcador de oleadas (MarcadorInvasion) ocupa el centro de arriba (620 px de ancho): la barra va
    // arriba a la derecha, más angosta, donde en los otros modos van los avisos de bajas (los invasores no los usan).
    private const float AnchoEnInvasion = 480f, DerechaEnInvasion = 24f, ArribaEnInvasion = 40f;
    private const float Sigue = 0.6f;    // s que el tramo claro tarda en alcanzar a la vida
    private const float Apagarse = 0.6f; // s al morir

    private Npc jefe;
    private RectTransform relleno, tramo;
    private TextMeshProUGUI valor, nombreTexto;
    private string nombre;
    private Image imagenRelleno;
    private CanvasGroup grupo;
    private float mostrado = 1f, rastro = 1f, murioEn = -1f;

    /// <summary>La barra de ese jefe (si ya hay una de otro jefe, quedan las dos, una abajo de la otra).</summary>
    public static BarraDeJefe Mostrar(Npc jefe)
    {
        if (jefe == null) return null;
        var go = new GameObject("Barra del jefe · " + jefe.Tipo.nombre);
        var barra = go.AddComponent<BarraDeJefe>();
        barra.Armar(jefe);
        return barra;
    }

    private void Armar(Npc npc)
    {
        jefe = npc;
        var lienzo = gameObject.AddComponent<Canvas>();
        lienzo.renderMode = RenderMode.ScreenSpaceOverlay;
        lienzo.sortingOrder = 15;
        var escala = gameObject.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.matchWidthOrHeight = 0.5f;
        grupo = gameObject.AddComponent<CanvasGroup>();
        grupo.blocksRaycasts = false;
        grupo.interactable = false;

        MatchHud hud = MatchHud.Instance;
        TMP_FontAsset titulo = hud != null ? hud.DisplayFont : null, etiqueta = hud != null ? hud.LabelFont : null;
        Sprite redondo = hud != null ? hud.Rounded : null;

        var caja = Rect("Barra", transform);
        if (PartidaDeInvasion.Actual != null)
        {
            escala.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // igual que el marcador, para no encimarse
            caja.anchorMin = caja.anchorMax = caja.pivot = new Vector2(1f, 1f);
            caja.anchoredPosition = new Vector2(-DerechaEnInvasion, -ArribaEnInvasion);
            caja.sizeDelta = new Vector2(AnchoEnInvasion, 46f);
        }
        else
        {
            caja.anchorMin = caja.anchorMax = new Vector2(0.5f, 1f);
            caja.pivot = new Vector2(0.5f, 1f);
            caja.anchoredPosition = new Vector2(0f, -Abajo);
            caja.sizeDelta = new Vector2(Ancho, 46f);
        }

        // El nombre a la izquierda y la vida a la derecha, arriba de la barra.
        nombre = (string.IsNullOrEmpty(npc.Tipo.titulo) ? npc.Tipo.nombre : npc.Tipo.titulo).ToUpperInvariant();
        TextMeshProUGUI texto = Texto("Nombre", caja, nombre, titulo, 22f, ShopUIKit.Ink, TextAlignmentOptions.BottomLeft);
        nombreTexto = texto;
        texto.characterSpacing = 6f;
        valor = Texto("Vida", caja, "", etiqueta, 16f, ShopUIKit.Mute, TextAlignmentOptions.BottomRight);

        // La barra: fondo, tramo claro, vida y la marca de la furia.
        var fondo = Rect("Fondo", caja);
        fondo.anchorMin = new Vector2(0f, 0f);
        fondo.anchorMax = new Vector2(1f, 0f);
        fondo.pivot = new Vector2(0.5f, 0f);
        fondo.sizeDelta = new Vector2(0f, Alto);
        Imagen(fondo, ShopUIKit.HudColor, redondo);
        tramo = Lleno("Tramo", fondo, ShopUIKit.Hot, redondo);
        relleno = Lleno("Vida", fondo, ShopUIKit.Accent, redondo);
        imagenRelleno = relleno.GetComponent<Image>();

        var marca = Rect("Marca de la furia", fondo);
        marca.anchorMin = new Vector2(Furia, 0f);
        marca.anchorMax = new Vector2(Furia, 1f);
        marca.sizeDelta = new Vector2(2f, 8f);
        Imagen(marca, ShopUIKit.Ink, null);
    }

    private void Update()
    {
        HealthSystem vida = jefe != null ? jefe.Vida : null;
        float fraccion = vida != null && vida.maxHealth > 0 ? Mathf.Clamp01((float)vida.currentHealth / vida.maxHealth) : 0f;

        // Murió (o ya no está): se apaga y se borra.
        if (vida == null || vida.currentHealth <= 0)
        {
            if (murioEn < 0f) murioEn = Time.unscaledTime;
            grupo.alpha = 1f - Mathf.Clamp01((Time.unscaledTime - murioEn) / Apagarse);
            if (grupo.alpha <= 0f) Destroy(gameObject);
        }

        mostrado = fraccion;
        rastro = rastro > mostrado ? Mathf.MoveTowards(rastro, mostrado, Time.deltaTime / Sigue) : mostrado;
        relleno.anchorMax = new Vector2(mostrado, 1f);
        tramo.anchorMax = new Vector2(rastro, 1f);
        if (vida != null) valor.text = $"{Mathf.Max(0, vida.currentHealth):N0} / {vida.maxHealth:N0}".Replace(',', '.');
    }

    /// <summary>El color de la vida (la furia de la US 268 la pone roja).</summary>
    /// <summary>La furia (US 268): la vida en rojo y "FURIA" al lado del nombre.</summary>
    public void Enfurecer()
    {
        PonerColor(ShopUIKit.Bad);
        if (nombreTexto != null) nombreTexto.text = $"{nombre}  <color=#FF5C5C>FURIA</color>";
    }

    public void PonerColor(Color color)
    {
        if (imagenRelleno != null) imagenRelleno.color = color;
    }

    // ---------- Piezas ----------

    private static RectTransform Rect(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        return (RectTransform)go.transform;
    }

    private static Image Imagen(RectTransform rect, Color color, Sprite sprite)
    {
        Image img = rect.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        if (sprite != null) { img.sprite = sprite; img.type = Image.Type.Sliced; }
        return img;
    }

    private static RectTransform Lleno(string nombre, RectTransform padre, Color color, Sprite sprite)
    {
        RectTransform r = Rect(nombre, padre);
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.pivot = new Vector2(0f, 0.5f);
        r.offsetMin = r.offsetMax = Vector2.zero;
        Imagen(r, color, sprite);
        return r;
    }

    private static TextMeshProUGUI Texto(string nombre, RectTransform padre, string texto, TMP_FontAsset fuente, float tam, Color color, TextAlignmentOptions alineado)
    {
        RectTransform r = Rect(nombre, padre);
        r.anchorMin = new Vector2(0f, 0f);
        r.anchorMax = new Vector2(1f, 1f);
        r.offsetMin = new Vector2(0f, Alto + 6f);
        r.offsetMax = Vector2.zero;
        TextMeshProUGUI t = r.gameObject.AddComponent<TextMeshProUGUI>();
        if (fuente != null) t.font = fuente;
        t.text = texto;
        t.fontSize = tam;
        t.color = color;
        t.alignment = alineado;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }
}
