using UnityEngine;
using TMPro;
using Slider = UnityEngine.UI.Slider;

// Pestaña Gráficos de las opciones (US 048 y US 153). Se arma por código con el mismo estilo que la pestaña
// Controles (OpcionesKit) y tapa los controles que había en la escena.
// Los cambios quedan pendientes hasta apretar "Aplicar" (CA3); "Restablecer" pide confirmación (CA4).
// Lo guardado se aplica al abrir el juego aunque no se entre a las opciones (CA5): ver AplicarGuardado.
// US 153: cada resolución aparece una sola vez (Unity la repite por cada frecuencia del monitor; se usa la más
// alta) y la calidad se elige solo entre Baja, Media y Alta, aunque el proyecto tenga otros niveles.
public class GraficosUIController : MonoBehaviour, OpcionesPantalla.ISeccion
{
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
    private const int MODO_PANTALLA_DEFECTO = 2;   // 0 = Ventana, 1 = Ventana sin bordes, 2 = Pantalla completa
    private const int VSYNC_DEFECTO = 1;           // 0 = Desactivado, 1 = Activado
    private const int FPS_DEFECTO = 60;            // 30 / 60 / 144 / -1 = Sin límite
    private const float FOV_POR_DEFECTO = 80f;
    private const float FOV_MINIMO = 60f;
    private const float FOV_MAXIMO = 100f;
    private const int ALTO_MINIMO = 720;

    private static readonly int[] OpcionesFPS = { 30, 60, 144, -1 };
    private static readonly string[] NivelesCalidad = { "Baja", "Media", "Alta" }; // niveles de Quality Settings

    // Lo que muestran los controles. "guardado" es lo último aplicado; "pendiente", lo que eligió el jugador.
    private struct Estado
    {
        public int resolucion, calidad, modo, vsync, fps;
        public float fov;
        public bool indicador; // US 197: mostrar FPS y ping

        public bool IgualA(Estado o) =>
            resolucion == o.resolucion && calidad == o.calidad && modo == o.modo &&
            vsync == o.vsync && fps == o.fps && Mathf.Approximately(fov, o.fov) && indicador == o.indicador;
    }

    private static readonly string[] NombresModo = { "Ventana", "Sin bordes", "Completa" };
    private static readonly string[] NombresVSync = { "Sí", "No" };
    private static readonly string[] NombresFPS = { "30", "60", "144", "Sin límite" };

    private Resolution[] resoluciones;
    private Estado guardado, pendiente;
    private OpcionesPantalla pantalla;
    private bool listo;

    // Uso en el menú de pausa (Setup)
    private bool enPausa;
    private TMP_FontAsset fuenteDisplay, fuenteLabel, fuenteBody;
    private Sprite redondeado;
    private System.Action alVolver;

    // Controles armados por código
    private TextMeshProUGUI textoResolucion, textoCalidad, textoFOV;
    private OpcionesKit.Segmentos segModo, segVSync, segFPS, segIndicador;
    private static readonly string[] NombresIndicador = { "Ocultar", "Mostrar" };
    private Slider sliderFOV;

    // =========================================================
    // INICIO
    // =========================================================

    private void Start()
    {
        resoluciones = Unicas(Screen.resolutions);
        pantalla = OpcionesPantalla.De(this);
        if (pantalla != null) pantalla.Registrar(this);

        guardado = LeerGuardado();
        pendiente = guardado;

        // En el menú de pausa (Setup) se arma sobre este mismo objeto; en el menú principal, sobre PanelGraficos.
        Transform panel = enPausa ? transform : transform.parent != null ? transform.parent.Find("PanelGraficos") : null;
        if (panel != null) Armar((RectTransform)panel);

        listo = true;
        MostrarEnControles(pendiente);
    }

    /// <summary>
    /// Para usar la pestaña fuera del menú principal (menú de pausa): se arma sobre este mismo objeto, con estas
    /// tipografías, y suma un botón "Volver".
    /// </summary>
    public void Setup(TMP_FontAsset display, TMP_FontAsset label, TMP_FontAsset body, Sprite sprite, System.Action onBack)
    {
        enPausa = true;
        fuenteDisplay = display;
        fuenteLabel = label;
        fuenteBody = body;
        redondeado = sprite;
        alVolver = onBack;
    }

    private void OnEnable()
    {
        if (!listo) return;
        // Si no hay nada pendiente, se vuelve a leer lo guardado (pudo cambiar desde otro lado, por ejemplo el
        // campo de visión de la pausa). Si no, los controles muestran lo pendiente.
        if (!HayCambios)
        {
            guardado = LeerGuardado();
            pendiente = guardado;
        }
        MostrarEnControles(pendiente);
    }

    private void Armar(RectTransform panel)
    {
        OpcionesKit kit = enPausa
            ? OpcionesKit.ArmarCon(panel, "Gráficos", fuenteDisplay, fuenteLabel, fuenteBody, redondeado)
            : OpcionesKit.Armar(panel, "Gráficos");
        float y = 114f;

        kit.Grupo("Pantalla", ref y);
        textoResolucion = kit.FilaSelector("Resolución", ref y,
            () => Cambiar(ref pendiente.resolucion, -1, resoluciones.Length),
            () => Cambiar(ref pendiente.resolucion, 1, resoluciones.Length));
        segModo = kit.FilaSegmentos("Modo", ref y, NombresModo, i => { pendiente.modo = i; MostrarEnControles(pendiente); });

        y += 10f;
        kit.Grupo("Imagen", ref y);
        textoCalidad = kit.FilaSelector("Calidad", ref y,
            () => Cambiar(ref pendiente.calidad, -1, NivelesCalidad.Length),
            () => Cambiar(ref pendiente.calidad, 1, NivelesCalidad.Length));
        segVSync = kit.FilaSegmentos("VSync", ref y, NombresVSync, i => { pendiente.vsync = i == 0 ? 1 : 0; MostrarEnControles(pendiente); });
        segFPS = kit.FilaSegmentos("Límite de FPS", ref y, NombresFPS, i => { pendiente.fps = i; MostrarEnControles(pendiente); });
        // US 197, CA1: "Mostrar FPS y ping", apagado por defecto.
        segIndicador = kit.FilaSegmentos("FPS y ping", ref y, NombresIndicador, i => { pendiente.indicador = i == 1; MostrarEnControles(pendiente); });
        sliderFOV = kit.FilaSlider("Campo de visión", ref y, FOV_MINIMO, FOV_MAXIMO, out textoFOV);
        sliderFOV.wholeNumbers = true;
        sliderFOV.onValueChanged.AddListener(v => { pendiente.fov = v; textoFOV.text = Mathf.RoundToInt(v) + "°"; });

        y += 10f;
        kit.Ayuda("Los cambios se usan al apretar Aplicar.", ref y);
        float yPie = y + 12f;
        kit.Pie(ref y, AplicarConfiguracion, RestablecerConfiguracion);
        if (alVolver != null) kit.Boton(OpcionesKit.Pad + 352f, yPie, 130f, 40f, "Volver", false, alVolver);
    }

    private void Cambiar(ref int indice, int paso, int cantidad)
    {
        if (cantidad <= 0) return;
        indice = (indice + paso + cantidad) % cantidad;
        MostrarEnControles(pendiente);
    }

    private void MostrarEnControles(Estado e)
    {
        if (textoResolucion == null) return;
        if (resoluciones.Length > 0)
        {
            Resolution r = resoluciones[Mathf.Clamp(e.resolucion, 0, resoluciones.Length - 1)];
            textoResolucion.text = r.width + " x " + r.height;
        }
        textoCalidad.text = NivelesCalidad[Mathf.Clamp(e.calidad, 0, NivelesCalidad.Length - 1)];
        segModo.Marcar(e.modo);
        segVSync.Marcar(e.vsync == 1 ? 0 : 1);
        segFPS.Marcar(e.fps);
        segIndicador.Marcar(e.indicador ? 1 : 0);
        sliderFOV.SetValueWithoutNotify(e.fov);
        textoFOV.text = Mathf.RoundToInt(e.fov) + "°";
    }

    // =========================================================
    // CA3: APLICAR / DESCARTAR
    // =========================================================

    public bool HayCambios => listo && !pendiente.IgualA(guardado);

    // Botón "Aplicar" de la pestaña.
    public void AplicarConfiguracion()
    {
        Aplicar();
        if (pantalla != null) pantalla.AvisarGuardado();
    }

    public void Aplicar()
    {
        if (!listo) return;

        if (resoluciones.Length > 0)
        {
            Resolution r = resoluciones[Mathf.Clamp(pendiente.resolucion, 0, resoluciones.Length - 1)];
            PlayerPrefs.SetInt(CLAVE_RESOLUCION_ANCHO, r.width);
            PlayerPrefs.SetInt(CLAVE_RESOLUCION_ALTO, r.height);
        }
        PlayerPrefs.SetString(CLAVE_CALIDAD, NivelesCalidad[Mathf.Clamp(pendiente.calidad, 0, NivelesCalidad.Length - 1)]);
        PlayerPrefs.SetInt(CLAVE_MODO_PANTALLA, pendiente.modo);
        PlayerPrefs.SetInt(CLAVE_VSYNC, pendiente.vsync);
        PlayerPrefs.SetInt(CLAVE_FPS, OpcionesFPS[Mathf.Clamp(pendiente.fps, 0, OpcionesFPS.Length - 1)]);
        PlayerPrefs.SetFloat(CLAVE_FOV, pendiente.fov);
        IndicadorRendimiento.Activo = pendiente.indicador; // US 197, CA5
        PlayerPrefs.Save();

        AplicarGuardado();
        // En partida, el campo de visión se ve al instante (CameraLook lo lee solo al empezar).
        foreach (CameraLook mirada in FindObjectsByType<CameraLook>())
        {
            Camera camara = mirada.GetComponent<Camera>();
            if (camara != null) camara.fieldOfView = pendiente.fov;
        }
        guardado = pendiente;
        Debug.Log("Configuración gráfica aplicada y guardada.");
    }

    public void Descartar()
    {
        if (!listo) return;
        pendiente = guardado;
        MostrarEnControles(pendiente);
    }

    // =========================================================
    // CA4: RESTABLECER
    // =========================================================

    // Botón "Restablecer" de la pestaña.
    public void RestablecerConfiguracion()
    {
        if (pantalla != null) pantalla.ConfirmarRestablecer("los gráficos", RestablecerAhora);
        else RestablecerAhora();
    }

    private void RestablecerAhora()
    {
        int calidad = System.Array.IndexOf(NivelesCalidad, CALIDAD_DEFECTO);
        pendiente = new Estado
        {
            resolucion = BuscarResolucion(resoluciones, RESOLUCION_ANCHO_DEFECTO, RESOLUCION_ALTO_DEFECTO),
            calidad = calidad < 0 ? 0 : calidad,
            modo = MODO_PANTALLA_DEFECTO,
            vsync = VSYNC_DEFECTO,
            fps = System.Array.IndexOf(OpcionesFPS, FPS_DEFECTO),
            fov = FOV_POR_DEFECTO,
            indicador = false
        };
        MostrarEnControles(pendiente);
        Aplicar();
        Debug.Log("Configuración gráfica restablecida a los valores por defecto.");
    }

    // =========================================================
    // LECTURA Y APLICACIÓN DE LO GUARDADO (CA5)
    // =========================================================

    private Estado LeerGuardado()
    {
        // Si había guardado otro nivel (por ejemplo "PC"), se muestra Media.
        int calidad = System.Array.IndexOf(NivelesCalidad, PlayerPrefs.GetString(CLAVE_CALIDAD, CALIDAD_DEFECTO));
        if (calidad < 0) calidad = System.Array.IndexOf(NivelesCalidad, CALIDAD_DEFECTO);
        int fps = System.Array.IndexOf(OpcionesFPS, PlayerPrefs.GetInt(CLAVE_FPS, FPS_DEFECTO));

        return new Estado
        {
            resolucion = BuscarResolucion(resoluciones,
                PlayerPrefs.GetInt(CLAVE_RESOLUCION_ANCHO, RESOLUCION_ANCHO_DEFECTO),
                PlayerPrefs.GetInt(CLAVE_RESOLUCION_ALTO, RESOLUCION_ALTO_DEFECTO)),
            calidad = calidad < 0 ? 0 : calidad,
            modo = Mathf.Clamp(PlayerPrefs.GetInt(CLAVE_MODO_PANTALLA, MODO_PANTALLA_DEFECTO), 0, 2),
            vsync = PlayerPrefs.GetInt(CLAVE_VSYNC, VSYNC_DEFECTO) == 1 ? 1 : 0,
            fps = fps < 0 ? System.Array.IndexOf(OpcionesFPS, FPS_DEFECTO) : fps,
            fov = Mathf.Clamp(PlayerPrefs.GetFloat(CLAVE_FOV, FOV_POR_DEFECTO), FOV_MINIMO, FOV_MAXIMO),
            indicador = IndicadorRendimiento.Activo
        };
    }

    /// <summary>
    /// Aplica al juego la configuración gráfica guardada. Lo llama el menú principal al arrancar (CA5),
    /// así no hace falta abrir las opciones para que se use.
    /// </summary>
    public static void AplicarGuardado()
    {
        Resolution[] lista = Unicas(Screen.resolutions);
        int indiceModo = Mathf.Clamp(PlayerPrefs.GetInt(CLAVE_MODO_PANTALLA, MODO_PANTALLA_DEFECTO), 0, 2);
        FullScreenMode modo = ModoPantalla(indiceModo);
        if (lista.Length > 0)
        {
            Resolution r = lista[BuscarResolucion(lista,
                PlayerPrefs.GetInt(CLAVE_RESOLUCION_ANCHO, RESOLUCION_ANCHO_DEFECTO),
                PlayerPrefs.GetInt(CLAVE_RESOLUCION_ALTO, RESOLUCION_ALTO_DEFECTO))];
            // CA1: "Sin bordes" es una ventana de ese tamaño sin marco (no ocupa toda la pantalla, salvo que se
            // elija la resolución del monitor). En los otros modos se le devuelve el marco si se lo habían sacado.
            if (indiceModo != 1) VentanaSinBordes.Restaurar();
            Screen.SetResolution(r.width, r.height, modo, r.refreshRateRatio);
            if (indiceModo == 1) VentanaSinBordes.Quitar(r.width, r.height);
        }
        else Screen.fullScreenMode = modo;

        int calidad = System.Array.IndexOf(QualitySettings.names, PlayerPrefs.GetString(CLAVE_CALIDAD, CALIDAD_DEFECTO));
        if (calidad < 0) calidad = System.Array.IndexOf(QualitySettings.names, CALIDAD_DEFECTO);
        if (calidad < 0) calidad = 0;
        QualitySettings.SetQualityLevel(calidad, true);
        // CA3: los tres niveles usan el mismo asset de URP; acá se ajusta lo que hace que se note la diferencia.
        CalidadGrafica.Aplicar(QualitySettings.names.Length > 0 ? QualitySettings.names[calidad] : CALIDAD_DEFECTO);

        // Después de la calidad: cada nivel de calidad trae su propio VSync.
        QualitySettings.vSyncCount = PlayerPrefs.GetInt(CLAVE_VSYNC, VSYNC_DEFECTO) == 1 ? 1 : 0;
        Application.targetFrameRate = PlayerPrefs.GetInt(CLAVE_FPS, FPS_DEFECTO);
    }

    private static FullScreenMode ModoPantalla(int indice)
    {
        switch (indice)
        {
            case 0: return FullScreenMode.Windowed;
            case 1: return FullScreenMode.Windowed; // sin bordes: una ventana a la que VentanaSinBordes le saca el marco
            default: return FullScreenMode.ExclusiveFullScreen;
        }
    }

    // CA2: una entrada por tamaño, con la frecuencia más alta que admite el monitor para ese tamaño.
    // Solo las que tienen la misma forma que el monitor (por ejemplo 16:9) y al menos 720 de alto: las demás se
    // ven con barras negras a los costados y borrosas en pantalla completa.
    private static Resolution[] Unicas(Resolution[] lista)
    {
        float forma = Display.main.systemHeight > 0 ? (float)Display.main.systemWidth / Display.main.systemHeight : 16f / 9f;
        var unicas = new System.Collections.Generic.List<Resolution>();
        var todas = new System.Collections.Generic.List<Resolution>(lista);
        var buenas = todas.FindAll(r => r.height >= ALTO_MINIMO && Mathf.Abs((float)r.width / r.height - forma) < 0.02f);
        if (buenas.Count == 0) buenas = todas; // monitor raro: se muestran todas
        foreach (Resolution r in buenas)
        {
            int i = unicas.FindIndex(u => u.width == r.width && u.height == r.height);
            if (i < 0) unicas.Add(r);
            else if (r.refreshRateRatio.value > unicas[i].refreshRateRatio.value) unicas[i] = r;
        }
        return unicas.ToArray();
    }

    private static int BuscarResolucion(Resolution[] lista, int ancho, int alto)
    {
        for (int i = 0; i < lista.Length; i++)
            if (lista[i].width == ancho && lista[i].height == alto) return i;

        // Si no existe la resolución guardada, se busca 1920 x 1080.
        for (int i = 0; i < lista.Length; i++)
            if (lista[i].width == RESOLUCION_ANCHO_DEFECTO && lista[i].height == RESOLUCION_ALTO_DEFECTO) return i;

        return Mathf.Max(lista.Length - 1, 0);
    }
}
