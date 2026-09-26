using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GraficosUIController : MonoBehaviour
{
    [Header("Controles")]
    [SerializeField] private TMP_Dropdown dropdownResolucion;
    [SerializeField] private TMP_Dropdown dropdownCalidad;
    [SerializeField] private TMP_Dropdown dropdownModoPantalla;
    [SerializeField] private TMP_Dropdown dropdownVSync;
    [SerializeField] private TMP_Dropdown dropdownLimiteFPS;
    [SerializeField] private Slider sliderFOV;

    private Resolution[] resoluciones;

    // =========================
    // CLAVES PLAYERPREFS
    // =========================

    private const string CLAVE_RESOLUCION_ANCHO = "Graficos_ResolucionAncho";
    private const string CLAVE_RESOLUCION_ALTO = "Graficos_ResolucionAlto";
    private const string CLAVE_CALIDAD = "Graficos_Calidad";
    private const string CLAVE_MODO_PANTALLA = "Graficos_ModoPantalla";
    private const string CLAVE_VSYNC = "Graficos_VSync";
    private const string CLAVE_FPS = "Graficos_FPS";
    private const string CLAVE_FOV = "FOV";

    // =========================
    // VALORES POR DEFECTO
    // =========================

    private const int RESOLUCION_ANCHO_DEFECTO = 1920;
    private const int RESOLUCION_ALTO_DEFECTO = 1080;

    private const string CALIDAD_DEFECTO = "Media";

    // 0 = Ventana
    // 1 = Ventana sin bordes
    // 2 = Pantalla completa
    private const int MODO_PANTALLA_DEFECTO = 2;

    // 0 = Desactivado
    // 1 = Activado
    private const int VSYNC_DEFECTO = 1;

    // 30 / 60 / 144 / -1 = Sin límite
    private const int FPS_DEFECTO = 60;

    private const float FOV_POR_DEFECTO = 80f;
    private const float FOV_MINIMO = 60f;
    private const float FOV_MAXIMO = 100f;

    // =========================
    // INICIO
    // =========================

    private void Start()
    {
        CargarResoluciones();
        CargarCalidades();
        CargarModosPantalla();
        CargarVSync();
        CargarLimiteFPS();
        CargarFOV();
    }

    // =========================================================
    // RESOLUCIÓN
    // =========================================================

    private void CargarResoluciones()
    {
        resoluciones = Screen.resolutions;

        dropdownResolucion.ClearOptions();

        var opciones = new System.Collections.Generic.List<string>();

        foreach (Resolution resolucion in resoluciones)
        {
            opciones.Add(resolucion.width + " x " + resolucion.height);
        }

        dropdownResolucion.AddOptions(opciones);

        int anchoGuardado = PlayerPrefs.GetInt(
            CLAVE_RESOLUCION_ANCHO,
            RESOLUCION_ANCHO_DEFECTO
        );

        int altoGuardado = PlayerPrefs.GetInt(
            CLAVE_RESOLUCION_ALTO,
            RESOLUCION_ALTO_DEFECTO
        );

        int indice = BuscarResolucion(anchoGuardado, altoGuardado);

        dropdownResolucion.SetValueWithoutNotify(indice);

        dropdownResolucion.onValueChanged.AddListener(CambiarResolucion);

        // Aplicar resolución guardada
        Resolution resolucionGuardada = resoluciones[indice];

        Screen.SetResolution(
            resolucionGuardada.width,
            resolucionGuardada.height,
            Screen.fullScreenMode
        );

        Debug.Log(
            "Resolución cargada: " +
            resolucionGuardada.width + "x" +
            resolucionGuardada.height
        );
    }

    private int BuscarResolucion(int ancho, int alto)
    {
        for (int i = 0; i < resoluciones.Length; i++)
        {
            if (
                resoluciones[i].width == ancho &&
                resoluciones[i].height == alto
            )
            {
                return i;
            }
        }

        // Si no existe la resolución guardada,
        // buscar 1920x1080.
        for (int i = 0; i < resoluciones.Length; i++)
        {
            if (
                resoluciones[i].width == RESOLUCION_ANCHO_DEFECTO &&
                resoluciones[i].height == RESOLUCION_ALTO_DEFECTO
            )
            {
                return i;
            }
        }

        return resoluciones.Length - 1;
    }

    private void CambiarResolucion(int indice)
    {
        Resolution resolucion = resoluciones[indice];

        Screen.SetResolution(
            resolucion.width,
            resolucion.height,
            Screen.fullScreenMode
        );

        PlayerPrefs.SetInt(
            CLAVE_RESOLUCION_ANCHO,
            resolucion.width
        );

        PlayerPrefs.SetInt(
            CLAVE_RESOLUCION_ALTO,
            resolucion.height
        );

        PlayerPrefs.Save();

        Debug.Log(
            "Resolución: " +
            resolucion.width + "x" +
            resolucion.height
        );
    }

    // =========================================================
    // CALIDAD
    // =========================================================

    private void CargarCalidades()
    {
        dropdownCalidad.ClearOptions();

        string[] nombresCalidad = QualitySettings.names;

        var opciones = new System.Collections.Generic.List<string>();

        foreach (string nombre in nombresCalidad)
        {
            opciones.Add(nombre);
        }

        dropdownCalidad.AddOptions(opciones);

        string calidadGuardada = PlayerPrefs.GetString(
            CLAVE_CALIDAD,
            CALIDAD_DEFECTO
        );

        int indice = System.Array.IndexOf(
            nombresCalidad,
            calidadGuardada
        );

        // Si no existe "Media" o la guardada,
        // buscar Media.
        if (indice < 0)
        {
            indice = System.Array.IndexOf(
                nombresCalidad,
                CALIDAD_DEFECTO
            );
        }

        // Si tampoco existe, usar el nivel 0.
        if (indice < 0)
        {
            indice = 0;
        }

        dropdownCalidad.SetValueWithoutNotify(indice);

        QualitySettings.SetQualityLevel(
            indice,
            true
        );

        dropdownCalidad.onValueChanged.AddListener(CambiarCalidad);

        Debug.Log(
            "Calidad cargada: " +
            QualitySettings.names[indice]
        );
    }

    private void CambiarCalidad(int indice)
    {
        QualitySettings.SetQualityLevel(
            indice,
            true
        );

        PlayerPrefs.SetString(
            CLAVE_CALIDAD,
            QualitySettings.names[indice]
        );

        PlayerPrefs.Save();

        Debug.Log(
            "Calidad: " +
            QualitySettings.names[indice]
        );
    }

    // =========================================================
    // MODO DE PANTALLA
    // =========================================================

    private void CargarModosPantalla()
    {
        dropdownModoPantalla.ClearOptions();

        var opciones = new System.Collections.Generic.List<string>();

        opciones.Add("Ventana");
        opciones.Add("Ventana sin bordes");
        opciones.Add("Pantalla completa");

        dropdownModoPantalla.AddOptions(opciones);

        int modoGuardado = PlayerPrefs.GetInt(
            CLAVE_MODO_PANTALLA,
            MODO_PANTALLA_DEFECTO
        );

        dropdownModoPantalla.SetValueWithoutNotify(
            modoGuardado
        );

        AplicarModoPantalla(modoGuardado);

        dropdownModoPantalla.onValueChanged.AddListener(
            CambiarModoPantalla
        );

        Debug.Log(
            "Modo pantalla cargado: " +
            opciones[modoGuardado]
        );
    }

    private void CambiarModoPantalla(int indice)
    {
        AplicarModoPantalla(indice);

        PlayerPrefs.SetInt(
            CLAVE_MODO_PANTALLA,
            indice
        );

        PlayerPrefs.Save();

        Debug.Log(
            "Modo pantalla: " +
            dropdownModoPantalla.options[indice].text
        );
    }

    private void AplicarModoPantalla(int indice)
    {
        FullScreenMode modo;

        switch (indice)
        {
            case 0:
                modo = FullScreenMode.Windowed;
                break;

            case 1:
                modo = FullScreenMode.FullScreenWindow;
                break;

            case 2:
                modo = FullScreenMode.ExclusiveFullScreen;
                break;

            default:
                modo = FullScreenMode.ExclusiveFullScreen;
                break;
        }

        Screen.fullScreenMode = modo;
    }

    // =========================================================
    // VSYNC
    // =========================================================

    private void CargarVSync()
    {
        dropdownVSync.ClearOptions();

        var opciones = new System.Collections.Generic.List<string>();

        opciones.Add("Desactivado");
        opciones.Add("Activado");

        dropdownVSync.AddOptions(opciones);

        int vsyncGuardado = PlayerPrefs.GetInt(
            CLAVE_VSYNC,
            VSYNC_DEFECTO
        );

        dropdownVSync.SetValueWithoutNotify(
            vsyncGuardado
        );

        AplicarVSync(vsyncGuardado);

        dropdownVSync.onValueChanged.AddListener(
            CambiarVSync
        );

        Debug.Log(
            "VSync cargado: " +
            (vsyncGuardado == 1 ? "Activado" : "Desactivado")
        );
    }

    private void CambiarVSync(int indice)
    {
        AplicarVSync(indice);

        PlayerPrefs.SetInt(
            CLAVE_VSYNC,
            indice
        );

        PlayerPrefs.Save();

        Debug.Log(
            "VSync: " +
            (indice == 1 ? "Activado" : "Desactivado")
        );
    }

    private void AplicarVSync(int indice)
    {
        QualitySettings.vSyncCount =
            indice == 1 ? 1 : 0;
    }

    // =========================================================
    // LÍMITE DE FPS
    // =========================================================

    private void CargarLimiteFPS()
    {
        dropdownLimiteFPS.ClearOptions();

        var opciones = new System.Collections.Generic.List<string>();

        opciones.Add("30 FPS");
        opciones.Add("60 FPS");
        opciones.Add("144 FPS");
        opciones.Add("Sin límite");

        dropdownLimiteFPS.AddOptions(opciones);

        int fpsGuardado = PlayerPrefs.GetInt(
            CLAVE_FPS,
            FPS_DEFECTO
        );

        int indice;

        switch (fpsGuardado)
        {
            case 30:
                indice = 0;
                break;

            case 60:
                indice = 1;
                break;

            case 144:
                indice = 2;
                break;

            case -1:
                indice = 3;
                break;

            default:
                indice = 1;
                fpsGuardado = FPS_DEFECTO;
                break;
        }

        dropdownLimiteFPS.SetValueWithoutNotify(
            indice
        );

        AplicarFPS(fpsGuardado);

        dropdownLimiteFPS.onValueChanged.AddListener(
            CambiarLimiteFPS
        );

        Debug.Log(
            "FPS cargados: " +
            (fpsGuardado == -1 ? "Sin límite" : fpsGuardado.ToString())
        );
    }

    private void CambiarLimiteFPS(int indice)
    {
        int fps;

        switch (indice)
        {
            case 0:
                fps = 30;
                break;

            case 1:
                fps = 60;
                break;

            case 2:
                fps = 144;
                break;

            case 3:
                fps = -1;
                break;

            default:
                fps = FPS_DEFECTO;
                break;
        }

        AplicarFPS(fps);

        PlayerPrefs.SetInt(
            CLAVE_FPS,
            fps
        );

        PlayerPrefs.Save();

        Debug.Log(
            "Límite FPS: " +
            (fps == -1 ? "Sin límite" : fps.ToString())
        );
    }

    private void AplicarFPS(int fps)
    {
        Application.targetFrameRate = fps;
    }

    // =========================================================
    // FOV
    // =========================================================

    private void CargarFOV()
    {
        float fovGuardado = PlayerPrefs.GetFloat(
            CLAVE_FOV,
            FOV_POR_DEFECTO
        );

        sliderFOV.minValue = FOV_MINIMO;
        sliderFOV.maxValue = FOV_MAXIMO;
        sliderFOV.wholeNumbers = true;

        sliderFOV.SetValueWithoutNotify(
            fovGuardado
        );

        sliderFOV.onValueChanged.AddListener(
            CambiarFOV
        );

        Debug.Log(
            "FOV cargado: " +
            fovGuardado
        );
    }

    private void CambiarFOV(float valor)
    {
        PlayerPrefs.SetFloat(
            CLAVE_FOV,
            valor
        );

        PlayerPrefs.Save();

        Debug.Log(
            "FOV: " +
            valor
        );
    }

    // =========================================================
    // APLICAR
    // =========================================================

    public void AplicarConfiguracion()
    {
        PlayerPrefs.Save();

        Debug.Log(
            "Configuración gráfica guardada."
        );
    }

    // =========================================================
    // RESTABLECER
    // =========================================================

    public void RestablecerConfiguracion()
    {
        // Resolución
        int indiceResolucion = BuscarResolucion(
            RESOLUCION_ANCHO_DEFECTO,
            RESOLUCION_ALTO_DEFECTO
        );

        dropdownResolucion.SetValueWithoutNotify(
            indiceResolucion
        );

        Resolution resolucionDefault =
            resoluciones[indiceResolucion];

        Screen.SetResolution(
            resolucionDefault.width,
            resolucionDefault.height,
            Screen.fullScreenMode
        );

        PlayerPrefs.SetInt(
            CLAVE_RESOLUCION_ANCHO,
            resolucionDefault.width
        );

        PlayerPrefs.SetInt(
            CLAVE_RESOLUCION_ALTO,
            resolucionDefault.height
        );

        // Calidad
        int indiceCalidad = System.Array.IndexOf(
            QualitySettings.names,
            CALIDAD_DEFECTO
        );

        if (indiceCalidad < 0)
            indiceCalidad = 0;

        dropdownCalidad.SetValueWithoutNotify(
            indiceCalidad
        );

        QualitySettings.SetQualityLevel(
            indiceCalidad,
            true
        );

        PlayerPrefs.SetString(
            CLAVE_CALIDAD,
            QualitySettings.names[indiceCalidad]
        );

        // Modo pantalla
        dropdownModoPantalla.SetValueWithoutNotify(
            MODO_PANTALLA_DEFECTO
        );

        AplicarModoPantalla(
            MODO_PANTALLA_DEFECTO
        );

        PlayerPrefs.SetInt(
            CLAVE_MODO_PANTALLA,
            MODO_PANTALLA_DEFECTO
        );

        // VSync
        dropdownVSync.SetValueWithoutNotify(
            VSYNC_DEFECTO
        );

        AplicarVSync(
            VSYNC_DEFECTO
        );

        PlayerPrefs.SetInt(
            CLAVE_VSYNC,
            VSYNC_DEFECTO
        );

        // FPS
        dropdownLimiteFPS.SetValueWithoutNotify(1);

        AplicarFPS(FPS_DEFECTO);

        PlayerPrefs.SetInt(
            CLAVE_FPS,
            FPS_DEFECTO
        );

        // FOV
        sliderFOV.SetValueWithoutNotify(
            FOV_POR_DEFECTO
        );

        PlayerPrefs.SetFloat(
            CLAVE_FOV,
            FOV_POR_DEFECTO
        );

        PlayerPrefs.Save();

        Debug.Log(
            "Configuración restablecida a los valores por defecto."
        );
    }
}