using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Tarjeta "Volver a la partida" del menú principal (US 195, CA2).
// Aparece solo si hay una partida online a la que todavía se puede volver: se cortó la conexión o se cerró el juego
// hace menos de 2 minutos (lo anota Reconexion). Muestra la sala, el tiempo que queda y un botón "Volver", que vuelve
// a esa partida en el mismo equipo (Multijugador.VolverALaPartida). Si no se puede, lo explica ahí mismo un rato.
// Va debajo de la columna de botones, con el estilo de las otras tarjetas del menú (fondo oscuro y borde naranja):
// arriba de JUGAR tapaba el título del juego. Las medidas salen del alto de los botones, así acompaña al menú.
// La agrega MenuUIController al abrir el menú.
public class VolverALaPartida : MonoBehaviour
{
    private const float DuracionError = 6f;
    private static readonly Color Naranja = new Color32(242, 154, 56, 255);
    private static readonly Color Tinta = new Color32(243, 244, 246, 255);
    private static readonly Color Apagado = new Color32(142, 150, 163, 255);
    private static readonly Color Rojo = new Color32(255, 92, 92, 255);
    private static readonly Color Fondo = new Color32(10, 12, 17, 240);

    private Button boton;
    private CanvasGroup grupo; // la tarjeta se oculta así: apagada, no correría este Update para volver a mostrarla
    private TextMeshProUGUI etiqueta, titulo, tiempo;
    private float tamanoTitulo;
    private bool volviendo, escuchando;
    private string error;
    private float errorHasta, proximo;

    /// <summary>Agrega la tarjeta al panel del menú principal, debajo de los botones (una sola vez).</summary>
    public static void MostrarEnMenu(Transform panel)
    {
        if (panel == null || panel.GetComponentInChildren<VolverALaPartida>(true) != null) return;
        var jugar = panel.Find("BtnJugar") as RectTransform;
        if (jugar == null)
        {
            Debug.LogWarning("VolverALaPartida: el menú principal no tiene BtnJugar; no se puede mostrar \"Volver a la partida\".");
            return;
        }

        // El botón más bajo de la columna de JUGAR: la tarjeta va debajo de ese.
        RectTransform ultimo = jugar;
        foreach (Transform hijo in jugar.parent)
        {
            var rect = hijo as RectTransform;
            if (rect == null || !rect.gameObject.activeSelf || rect.GetComponent<Button>() == null) continue;
            if (rect.anchorMin != jugar.anchorMin || rect.anchorMax != jugar.anchorMax) continue;
            if (Mathf.Abs(rect.anchoredPosition.x - jugar.anchoredPosition.x) > 1f) continue;
            if (rect.anchoredPosition.y < ultimo.anchoredPosition.y) ultimo = rect;
        }

        var go = new GameObject("TarjetaVolverALaPartida", typeof(RectTransform));
        go.transform.SetParent(jugar.parent, false);
        go.AddComponent<VolverALaPartida>().Armar((RectTransform)go.transform, jugar, ultimo);
    }

    // u = alto de un botón del menú: todas las medidas de la tarjeta salen de ahí.
    private void Armar(RectTransform tarjeta, RectTransform jugar, RectTransform ultimo)
    {
        float u = Mathf.Max(8f, jugar.sizeDelta.y);
        float ancho = 24f * u, alto = 2.9f * u, margen = 0.6f * u;
        TMP_Text muestra = jugar.GetComponentInChildren<TMP_Text>(true);
        TMP_FontAsset fuente = muestra != null ? muestra.font : null;
        Image imagenBoton = jugar.GetComponent<Image>();
        Sprite forma = imagenBoton != null ? imagenBoton.sprite : null;

        tarjeta.anchorMin = jugar.anchorMin;
        tarjeta.anchorMax = jugar.anchorMax;
        tarjeta.pivot = new Vector2(0.5f, 1f);
        tarjeta.sizeDelta = new Vector2(ancho, alto);
        float bajoElUltimo = ultimo.anchoredPosition.y - ultimo.sizeDelta.y * ultimo.pivot.y;
        tarjeta.anchoredPosition = new Vector2(jugar.anchoredPosition.x, bajoElUltimo - 1.1f * u);
        grupo = gameObject.AddComponent<CanvasGroup>();

        // Borde naranja y fondo oscuro, como las otras tarjetas del menú.
        Caja("Borde", tarjeta, Vector2.zero, new Vector2(ancho, alto), new Color(Naranja.r, Naranja.g, Naranja.b, 0.55f), null);
        float linea = Mathf.Max(1f, u * 0.05f);
        Caja("Fondo", tarjeta, new Vector2(linea, 0f), new Vector2(ancho - 2f * linea, alto - 2f * linea), Fondo, null);

        // Ícono redondo.
        float lado = 1.7f * u;
        RectTransform icono = Caja("Icono", tarjeta, new Vector2(margen, 0f), new Vector2(lado, lado), Naranja, forma);
        TextMeshProUGUI flecha = Texto("Flecha", icono, fuente, 1.1f * u, new Color32(26, 17, 6, 255), TextAlignmentOptions.Center);
        Estirar(flecha.rectTransform);
        flecha.text = "«";

        // Botón "Volver": una copia de JUGAR, así tiene su forma, su sonido y su animación.
        float anchoBoton = 4.6f * u;
        var copia = (RectTransform)Instantiate(jugar.gameObject, tarjeta).transform;
        copia.name = "BtnVolver";
        Izquierda(copia, new Vector2(ancho - margen - anchoBoton, 0f), new Vector2(anchoBoton, 1.3f * u));
        boton = copia.GetComponent<Button>();
        if (boton != null)
        {
            boton.onClick = new Button.ButtonClickedEvent(); // sin lo que hacía JUGAR
            boton.onClick.AddListener(Volver);
        }
        Image fondoBoton = copia.GetComponent<Image>();
        if (fondoBoton != null) fondoBoton.color = Naranja;
        TMP_Text textoBoton = copia.GetComponentInChildren<TMP_Text>(true);
        if (textoBoton != null) textoBoton.text = "VOLVER";

        // Tiempo que queda, a la izquierda del botón.
        float anchoTiempo = 2.6f * u;
        tiempo = Texto("Tiempo", tarjeta, fuente, 1.05f * u, Tinta, TextAlignmentOptions.MidlineRight);
        Izquierda(tiempo.rectTransform, new Vector2(ancho - margen - anchoBoton - 0.7f * u - anchoTiempo, 0f), new Vector2(anchoTiempo, alto));

        // Dos renglones: la etiqueta chica y la sala.
        float x = margen + lado + 0.7f * u;
        float anchoTexto = ancho - x - (margen + anchoBoton + 0.7f * u + anchoTiempo + 0.4f * u);
        etiqueta = Texto("Etiqueta", tarjeta, fuente, 0.5f * u, Apagado, TextAlignmentOptions.MidlineLeft);
        etiqueta.characterSpacing = 18f;
        Izquierda(etiqueta.rectTransform, new Vector2(x, 0.62f * u), new Vector2(anchoTexto, 0.7f * u));
        tamanoTitulo = 0.9f * u;
        titulo = Texto("Sala", tarjeta, fuente, tamanoTitulo, Tinta, TextAlignmentOptions.MidlineLeft);
        titulo.characterSpacing = 6f;
        titulo.enableAutoSizing = true; // un error largo se achica para entrar
        titulo.fontSizeMax = tamanoTitulo;
        titulo.fontSizeMin = 0.45f * u;
        Izquierda(titulo.rectTransform, new Vector2(x, -0.42f * u), new Vector2(anchoTexto, 1.2f * u));

        Actualizar();
    }

    // Rectángulo pegado a la izquierda de la tarjeta y centrado en alto; "lugar" es desde ese borde y desde el medio.
    private static void Izquierda(RectTransform rect, Vector2 lugar, Vector2 medida)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = medida;
        rect.anchoredPosition = lugar;
    }

    private static void Estirar(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static RectTransform Caja(string nombre, RectTransform padre, Vector2 lugar, Vector2 medida, Color color, Sprite forma)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var rect = (RectTransform)go.transform;
        Izquierda(rect, lugar, medida);
        Image imagen = go.AddComponent<Image>();
        imagen.color = color;
        imagen.raycastTarget = false;
        if (forma != null)
        {
            imagen.sprite = forma;
            imagen.type = forma.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        }
        return rect;
    }

    private static TextMeshProUGUI Texto(string nombre, RectTransform padre, TMP_FontAsset fuente, float tamano, Color color, TextAlignmentOptions alineacion)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        TextMeshProUGUI texto = go.AddComponent<TextMeshProUGUI>();
        if (fuente != null) texto.font = fuente;
        texto.fontSize = tamano;
        texto.color = color;
        texto.alignment = alineacion;
        texto.textWrappingMode = TextWrappingModes.NoWrap;
        texto.overflowMode = TextOverflowModes.Overflow;
        texto.raycastTarget = false;
        return texto;
    }

    private void OnDestroy()
    {
        if (escuchando && Multijugador.Existe) Multijugador.Instancia.Error -= AlFallar;
    }

    private void Update()
    {
        if (Time.unscaledTime < proximo) return;
        proximo = Time.unscaledTime + 0.2f;
        Actualizar();
    }

    private void Actualizar()
    {
        bool hay = Reconexion.HayPartida(out Reconexion.Partida partida);
        bool conError = error != null && Time.unscaledTime < errorHasta;
        if (!conError) error = null;

        bool visible = hay || volviendo || conError;
        grupo.alpha = visible ? 1f : 0f;
        grupo.interactable = visible && !volviendo;
        grupo.blocksRaycasts = visible;
        if (!visible) return;
        if (boton != null) boton.gameObject.SetActive(hay && !volviendo);

        if (conError)
        {
            etiqueta.text = "NO SE PUDO VOLVER";
            etiqueta.color = Rojo;
            titulo.text = error;
            titulo.color = Rojo;
            tiempo.text = "";
            return;
        }
        etiqueta.text = "PARTIDA EN CURSO";
        etiqueta.color = Apagado;
        titulo.color = Tinta;
        if (volviendo)
        {
            titulo.text = "VOLVIENDO A LA PARTIDA…";
            tiempo.text = "";
            return;
        }
        int s = Mathf.CeilToInt(partida.restante);
        titulo.text = $"SALA <color=#F29A38>{partida.sala}</color>  ·  PODÉS VOLVER";
        tiempo.text = $"{s / 60}:{s % 60:00}";
    }

    private void Volver()
    {
        if (volviendo) return;
        volviendo = true;
        error = null;
        Multijugador red = Multijugador.Instancia;
        if (!escuchando) { red.Error += AlFallar; escuchando = true; }
        red.VolverALaPartida();
        Actualizar();
    }

    private void AlFallar(string mensaje)
    {
        if (!volviendo) return;
        volviendo = false;
        error = mensaje;
        errorHasta = Time.unscaledTime + DuracionError;
        Actualizar();
    }
}
