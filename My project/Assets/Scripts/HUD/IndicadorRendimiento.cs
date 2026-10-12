using Photon.Pun;
using TMPro;
using UnityEngine;
using static ShopUIKit;

// Indicador de FPS y ping (US 197), para saber si un problema es de la computadora o de la conexión.
// - CA1: se prende desde Opciones > Gráficos (y la misma pestaña en la pausa); está apagado por defecto.
// - CA2: indicador chico en la esquina de arriba a la izquierda, pegado al borde.
// - CA3: cuadros por segundo, actualizados cada 0,5 s.
// - CA4: en las partidas online, el ping al servidor de Photon en ms: verde hasta 80, amarillo hasta 150 y rojo si
//   pasa de 150. En las partidas locales no se muestra.
// - CA5: la opción se guarda en PlayerPrefs y se usa al volver a abrir el juego.
// Se crea solo al arrancar el juego y sigue entre escenas: no hay que ponerlo en ninguna.
public class IndicadorRendimiento : MonoBehaviour
{
    private const string Clave = "Graficos_MostrarFPS";
    private const float Cada = 0.5f; // CA3
    private const int PingBueno = 80, PingRegular = 150; // CA4

    private static IndicadorRendimiento actual;

    private GameObject caja;
    private TextMeshProUGUI texto;
    private RectTransform fondo;
    private float tiempo;
    private int cuadros;
    private bool conFuente;

    /// <summary>Si el jugador eligió ver el indicador (CA1 y CA5).</summary>
    public static bool Activo
    {
        get => PlayerPrefs.GetInt(Clave, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(Clave, value ? 1 : 0);
            if (actual != null) actual.Mostrar(value);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Crear()
    {
        if (actual != null) return;
        var go = new GameObject("IndicadorRendimiento", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        DontDestroyOnLoad(go);
        actual = go.AddComponent<IndicadorRendimiento>();
        actual.Armar();
    }

    private void Armar()
    {
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300; // arriba de todo: se ve también con los menús abiertos
        var escala = GetComponent<UnityEngine.UI.CanvasScaler>();
        escala.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;

        // CA2: chico y pegado a la esquina, para no tapar el marcador, los avisos de bajas ni el minimapa.
        fondo = Place(Node("Caja", transform), 8f, 6f, 120f, 22f);
        caja = fondo.gameObject;
        Image(fondo, null, Rgb(10, 12, 17, 0.55f));
        texto = Text(Place(Node("Texto", fondo), 8f, 0f, 300f, 22f), null, 14f, Ink, TextAlignmentOptions.MidlineLeft, 4f);
        texto.fontStyle = FontStyles.Bold;
        Mostrar(Activo);
    }

    private void Mostrar(bool visible)
    {
        caja.SetActive(visible);
        tiempo = Cada; // se actualiza en el próximo cuadro
        cuadros = 0;
    }

    private void Update()
    {
        if (!caja.activeSelf) return;

        // La tipografía del HUD, cuando hay una partida abierta (si no, la de TextMeshPro por defecto).
        if (!conFuente && MatchHud.Instance != null && MatchHud.Instance.LabelFont != null)
        {
            texto.font = MatchHud.Instance.LabelFont;
            conFuente = true;
        }

        cuadros++;
        tiempo += Time.unscaledDeltaTime;
        if (tiempo < Cada) return;

        int fps = cuadros > 1 ? Mathf.RoundToInt(cuadros / tiempo) : Mathf.RoundToInt(1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime));
        cuadros = 0;
        tiempo = 0f;

        string linea = $"{fps} FPS";
        // CA4: solo en una partida online (en las locales no hay servidor).
        if (PhotonNetwork.InRoom && !PhotonNetwork.OfflineMode)
        {
            int ping = PhotonNetwork.GetPing();
            string color = ping <= PingBueno ? "#3DDC97" : ping <= PingRegular ? "#FFD84A" : "#FF5C5C";
            linea += $"   <color={color}>{ping} ms</color>";
        }
        texto.text = linea;
        fondo.sizeDelta = new Vector2(Width(texto, linea) + 16f, 22f);
    }
}
