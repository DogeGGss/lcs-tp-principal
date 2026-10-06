using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Botón "Volver a la partida" del menú principal (US 195, CA2).
// Aparece solo si hay una partida online a la que todavía se puede volver: se cortó la conexión o se cerró el juego
// hace menos de 2 minutos (lo anota Reconexion). Abajo dice la sala y cuánto tiempo queda. Al tocarlo vuelve a esa
// partida, en el mismo equipo (Multijugador.VolverALaPartida). Si no se puede, lo explica abajo un rato.
// Es una copia del botón JUGAR, arriba de él y con el mismo estilo. Lo agrega MenuUIController al abrir el menú.
public class VolverALaPartida : MonoBehaviour
{
    private const float DuracionError = 6f;
    private static readonly Color ColorDetalle = new Color32(142, 150, 163, 255);
    private static readonly Color ColorError = new Color32(255, 92, 92, 255);

    private Button boton;
    private CanvasGroup grupo; // el botón se oculta así: apagado, no correría este Update para volver a mostrarlo
    private TMP_Text texto;
    private TextMeshProUGUI detalle;
    private bool volviendo, escuchando;
    private string error;
    private float errorHasta, proximo;

    /// <summary>Agrega el botón al panel del menú principal, arriba de JUGAR (una sola vez).</summary>
    public static void MostrarEnMenu(Transform panel)
    {
        if (panel == null || panel.GetComponentInChildren<VolverALaPartida>(true) != null) return;
        var jugar = panel.Find("BtnJugar") as RectTransform;
        if (jugar == null)
        {
            Debug.LogWarning("VolverALaPartida: el menú principal no tiene BtnJugar; no se puede mostrar \"Volver a la partida\".");
            return;
        }

        var copia = (RectTransform)Instantiate(jugar.gameObject, jugar.parent).transform;
        copia.name = "BtnVolverALaPartida";
        copia.SetSiblingIndex(jugar.GetSiblingIndex());
        // Arriba de JUGAR, con la misma separación que hay entre JUGAR y el botón de abajo.
        var abajo = panel.Find("BtnLogros") as RectTransform;
        float paso = abajo != null ? Mathf.Abs(jugar.anchoredPosition.y - abajo.anchoredPosition.y) : jugar.sizeDelta.y * 2f;
        copia.anchoredPosition = jugar.anchoredPosition + new Vector2(0f, paso);

        VolverALaPartida volver = copia.gameObject.AddComponent<VolverALaPartida>();
        volver.Armar(copia, paso - copia.sizeDelta.y);
    }

    private void Armar(RectTransform rect, float espacio)
    {
        grupo = GetComponent<CanvasGroup>();
        if (grupo == null) grupo = gameObject.AddComponent<CanvasGroup>();
        boton = GetComponent<Button>();
        if (boton != null)
        {
            boton.onClick = new Button.ButtonClickedEvent(); // sin lo que hacía JUGAR
            boton.onClick.AddListener(Volver);
        }
        texto = GetComponentInChildren<TMP_Text>(true);
        if (texto != null)
        {
            texto.text = "VOLVER A LA PARTIDA";
            // Más ancho que JUGAR, para que entre el texto.
            rect.sizeDelta = new Vector2(Mathf.Max(rect.sizeDelta.x, texto.GetPreferredValues(texto.text).x + 40f), rect.sizeDelta.y);
        }

        // La sala y el tiempo que queda, chiquito, entre este botón y JUGAR.
        var go = new GameObject("Detalle", typeof(RectTransform));
        go.transform.SetParent(rect.parent, false);
        var linea = (RectTransform)go.transform;
        linea.anchorMin = rect.anchorMin;
        linea.anchorMax = rect.anchorMax;
        linea.pivot = new Vector2(0.5f, 1f);
        float alto = Mathf.Clamp(espacio - 4f, 14f, 24f);
        linea.sizeDelta = new Vector2(Mathf.Max(rect.sizeDelta.x, 420f), alto);
        linea.anchoredPosition = rect.anchoredPosition + new Vector2(0f, -rect.sizeDelta.y * (1f - rect.pivot.y) - 2f);
        detalle = go.AddComponent<TextMeshProUGUI>();
        if (texto != null) detalle.font = texto.font;
        detalle.fontSize = alto * 0.8f;
        detalle.alignment = TextAlignmentOptions.Center;
        detalle.raycastTarget = false;
        detalle.color = ColorDetalle;

        Actualizar();
    }

    private void OnDestroy()
    {
        if (escuchando && Multijugador.Existe) Multijugador.Instancia.Error -= AlFallar;
        if (detalle != null) Destroy(detalle.gameObject);
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

        bool visible = hay || volviendo;
        grupo.alpha = visible ? 1f : 0f;
        grupo.interactable = visible && !volviendo;
        grupo.blocksRaycasts = visible;
        if (detalle == null) return;
        detalle.gameObject.SetActive(hay || volviendo || conError);
        if (conError) { detalle.color = ColorError; detalle.text = error; return; }
        detalle.color = ColorDetalle;
        if (volviendo) { detalle.text = "Volviendo a la partida…"; return; }
        if (!hay) return;
        int s = Mathf.CeilToInt(partida.restante);
        detalle.text = $"Te desconectaste de la sala {partida.sala}  ·  podés volver durante {s / 60}:{s % 60:00}";
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
