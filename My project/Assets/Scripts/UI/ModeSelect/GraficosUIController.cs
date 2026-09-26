using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class GraficosUIController : MonoBehaviour
{
    [Header("Controles")]
    [SerializeField] private TMP_Dropdown dropdownResolucion;
    [SerializeField] private TMP_Dropdown dropdownCalidad;
    [SerializeField] private Toggle togglePantallaCompleta;

    private Resolution[] resoluciones;

    private void Start()
{
    CargarResoluciones();
    CargarCalidades();

    togglePantallaCompleta.isOn = Screen.fullScreen;

    togglePantallaCompleta.onValueChanged.AddListener(CambiarPantallaCompleta);
}

    private void CargarResoluciones()
    {
        resoluciones = Screen.resolutions;

        dropdownResolucion.ClearOptions();

        int resolucionActual = 0;

        for (int i = 0; i < resoluciones.Length; i++)
        {
            Resolution resolucion = resoluciones[i];

            string texto =
                resolucion.width + " x " + resolucion.height;

            dropdownResolucion.options.Add(
                new TMP_Dropdown.OptionData(texto)
            );

            if (resolucion.width == Screen.currentResolution.width &&
                resolucion.height == Screen.currentResolution.height)
            {
                resolucionActual = i;
            }
        }

        dropdownResolucion.value = resolucionActual;
        dropdownResolucion.RefreshShownValue();

        dropdownResolucion.onValueChanged.AddListener(CambiarResolucion);
    }

private void CargarCalidades()
{
    dropdownCalidad.ClearOptions();

    dropdownCalidad.AddOptions(
        new System.Collections.Generic.List<string>
        {
            "Baja",
            "Media",
            "Alta"
        }
    );

    string calidadActual = QualitySettings.names[QualitySettings.GetQualityLevel()];

    int indiceDropdown = 0;

    if (calidadActual == "Media")
        indiceDropdown = 1;
    else if (calidadActual == "Alta")
        indiceDropdown = 2;

    dropdownCalidad.value = indiceDropdown;
    dropdownCalidad.RefreshShownValue();

    dropdownCalidad.onValueChanged.AddListener(CambiarCalidad);
}

    private void CambiarResolucion(int indice)
{
    Resolution resolucion = resoluciones[indice];

    Screen.SetResolution(
        resolucion.width,
        resolucion.height,
        FullScreenMode.Windowed
    );

    Debug.Log("Resolución cambiada a: " +
        resolucion.width + " x " + resolucion.height);
}

   private void CambiarCalidad(int indice)
{
    string nombreBuscado = "";

    switch (indice)
    {
        case 0:
            nombreBuscado = "Baja";
            break;

        case 1:
            nombreBuscado = "Media";
            break;

        case 2:
            nombreBuscado = "Alta";
            break;
    }

    int nivel = System.Array.IndexOf(
        QualitySettings.names,
        nombreBuscado
    );

    if (nivel >= 0)
    {
        QualitySettings.SetQualityLevel(nivel);

        Debug.Log(
            "Calidad gráfica: " +
            QualitySettings.names[nivel]
        );
    }
    else
    {
        Debug.LogWarning(
            "No se encontró el nivel de calidad: " +
            nombreBuscado
        );
    }
}

    public void CambiarPantallaCompleta(bool pantallaCompleta)
{
    Screen.fullScreen = pantallaCompleta;

    Debug.Log("Pantalla completa: " + pantallaCompleta);
}
}