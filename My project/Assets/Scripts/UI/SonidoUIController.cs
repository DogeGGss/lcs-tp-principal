using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using Slider = UnityEngine.UI.Slider;

// Pestaña Sonido de las opciones (US 048 y US 154). Se arma por código con el mismo estilo que la pestaña
// Controles (OpcionesKit) y tapa los controles que había en la escena.
// US 154: General, Música, Efectos e Interfaz, de 0 a 100 (CA1 y CA2). Al mover un control suena una muestra
// por ese grupo del Audio Mixer, así se escucha el volumen nuevo (CA3); en 0 el grupo queda en -80 dB (CA4).
// US 048: se guarda recién con "Aplicar"; si el jugador descarta, vuelve el volumen guardado.
// "Restablecer" pide confirmación.
public class SonidoUIController : MonoBehaviour, OpcionesPantalla.ISeccion
{
    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("Muestras al mover los controles (US 154, CA3)")]
    [Tooltip("Suena por el grupo SFX (Efectos y General).")]
    [SerializeField] private AudioClip muestraEfectos;
    [Tooltip("Suena por el grupo Interfaz.")]
    [SerializeField] private AudioClip muestraInterfaz;
    [Tooltip("Suena por el grupo Musica unos segundos, si no hay otra música sonando.")]
    [SerializeField] private AudioClip muestraMusica;

    // Parámetros del Audio Mixer y claves de PlayerPrefs (se llaman igual)
    private const string CLAVE_GENERAL = "VolumenGeneral";
    private const string CLAVE_MUSICA = "VolumenMusica";
    private const string CLAVE_EFECTOS = "VolumenEfectos";
    private const string CLAVE_INTERFAZ = "VolumenInterfaz";

    private const float PAUSA_MUESTRA = 0.18f;   // mientras se arrastra, una muestra cada tanto
    private const float DURACION_MUSICA = 1.6f;  // la música de muestra suena un rato y se apaga

    private const float VOLUMEN_POR_DEFECTO = 1f;
    private const float VOLUMEN_MINIMO = 0.0001f;

    private OpcionesPantalla pantalla;
    private Slider sliderGeneral, sliderMusica, sliderEfectos, sliderInterfaz;
    private TextMeshProUGUI textoGeneral, textoMusica, textoEfectos, textoInterfaz;
    private bool listo;

    private AudioMixerGroup grupoMusica;
    private AudioSource fuenteEfectos, fuenteInterfaz, fuenteMusica;
    private float proximaMuestra, finMusica;

    private void Start()
    {
        pantalla = OpcionesPantalla.De(this);
        if (pantalla != null) pantalla.Registrar(this);

        OpcionesKit kit = OpcionesKit.Armar((RectTransform)transform, "Sonido");
        float y = 114f;
        kit.Grupo("Volumen", ref y);
        CrearFuentes();
        sliderGeneral = Fila(kit, "General", CLAVE_GENERAL, ref y, out textoGeneral, () => Probar(fuenteEfectos, muestraEfectos));
        sliderMusica = Fila(kit, "Música", CLAVE_MUSICA, ref y, out textoMusica, ProbarMusica);
        sliderEfectos = Fila(kit, "Efectos", CLAVE_EFECTOS, ref y, out textoEfectos, () => Probar(fuenteEfectos, muestraEfectos));
        sliderInterfaz = Fila(kit, "Interfaz", CLAVE_INTERFAZ, ref y, out textoInterfaz, () => Probar(fuenteInterfaz, muestraInterfaz));
        y += 10f;
        kit.Ayuda("Al mover un control suena una muestra. Se guarda al apretar Aplicar.", ref y);
        kit.Pie(ref y, AplicarSonido, RestablecerSonido);

        MostrarGuardado();
        AplicarGuardado(audioMixer);
        listo = true;
    }

    // Se escucha al instante (con una muestra, CA3), pero no se guarda hasta "Aplicar".
    private Slider Fila(OpcionesKit kit, string titulo, string clave, ref float y, out TextMeshProUGUI valor, System.Action muestra)
    {
        Slider slider = kit.FilaSlider(titulo, ref y, VOLUMEN_MINIMO, 1f, out valor);
        TextMeshProUGUI texto = valor;
        slider.onValueChanged.AddListener(v =>
        {
            texto.text = Porcentaje(v);
            AplicarAlMixer(audioMixer, clave, v);
            if (listo) muestra();
        });
        return slider;
    }

    // =========================================================
    // US 154, CA3: MUESTRAS
    // =========================================================

    private void CrearFuentes()
    {
        fuenteEfectos = CrearFuente(Grupo("SFX"));
        fuenteInterfaz = CrearFuente(Grupo("Interfaz"));
        grupoMusica = Grupo("Musica");
        fuenteMusica = CrearFuente(grupoMusica);
        fuenteMusica.loop = true;
    }

    private AudioMixerGroup Grupo(string nombre)
    {
        if (audioMixer == null) return null;
        foreach (AudioMixerGroup g in audioMixer.FindMatchingGroups(nombre))
            if (g.name == nombre) return g;
        return null;
    }

    private AudioSource CrearFuente(AudioMixerGroup grupo)
    {
        AudioSource fuente = gameObject.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.spatialBlend = 0f;
        fuente.ignoreListenerPause = true; // se escucha aunque el juego esté en pausa
        fuente.outputAudioMixerGroup = grupo;
        return fuente;
    }

    // Mientras se arrastra, suena de nuevo cada PAUSA_MUESTRA segundos (no en cada cuadro).
    private void Probar(AudioSource fuente, AudioClip clip)
    {
        if (fuente == null || clip == null || Time.unscaledTime < proximaMuestra) return;
        proximaMuestra = Time.unscaledTime + PAUSA_MUESTRA;
        fuente.Stop();
        fuente.clip = clip;
        fuente.Play();
    }

    // Si ya suena música por ese grupo (la del menú), alcanza con esa. Si no, suena la muestra un rato.
    private void ProbarMusica()
    {
        if (fuenteMusica == null || muestraMusica == null || HayOtraMusica()) return;
        finMusica = Time.unscaledTime + DURACION_MUSICA;
        fuenteMusica.volume = 1f;
        if (fuenteMusica.isPlaying) return;
        fuenteMusica.clip = muestraMusica;
        fuenteMusica.Play();
    }

    private bool HayOtraMusica()
    {
        if (grupoMusica == null) return false;
        foreach (AudioSource fuente in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            if (fuente != fuenteMusica && fuente.isPlaying && fuente.outputAudioMixerGroup == grupoMusica) return true;
        return false;
    }

    // La música de muestra se apaga de a poco al terminar su tiempo.
    private void Update()
    {
        if (fuenteMusica == null || !fuenteMusica.isPlaying) return;
        float resta = finMusica - Time.unscaledTime;
        if (resta <= 0f) fuenteMusica.Stop();
        else if (resta < 0.4f) fuenteMusica.volume = resta / 0.4f;
    }

    private void OnDisable()
    {
        if (fuenteMusica != null) fuenteMusica.Stop();
    }

    private static string Porcentaje(float v) => Mathf.RoundToInt(v * 100f) + " %";

    private void MostrarGuardado()
    {
        Mostrar(sliderGeneral, textoGeneral, Guardado(CLAVE_GENERAL));
        Mostrar(sliderMusica, textoMusica, Guardado(CLAVE_MUSICA));
        Mostrar(sliderEfectos, textoEfectos, Guardado(CLAVE_EFECTOS));
        Mostrar(sliderInterfaz, textoInterfaz, Guardado(CLAVE_INTERFAZ));
    }

    private static void Mostrar(Slider slider, TextMeshProUGUI texto, float valor)
    {
        slider.SetValueWithoutNotify(valor);
        texto.text = Porcentaje(valor);
    }

    private static float Guardado(string clave) => PlayerPrefs.GetFloat(clave, VOLUMEN_POR_DEFECTO);

    // =========================================================
    // CA3: APLICAR / DESCARTAR
    // =========================================================

    public bool HayCambios =>
        listo && (Distinto(sliderGeneral, CLAVE_GENERAL) || Distinto(sliderMusica, CLAVE_MUSICA) ||
                  Distinto(sliderEfectos, CLAVE_EFECTOS) || Distinto(sliderInterfaz, CLAVE_INTERFAZ));

    private static bool Distinto(Slider slider, string clave) => Mathf.Abs(slider.value - Guardado(clave)) > 0.001f;

    // Botón "Aplicar" de la pestaña.
    public void AplicarSonido()
    {
        Aplicar();
        if (pantalla != null) pantalla.AvisarGuardado();
    }

    public void Aplicar()
    {
        if (!listo) return;
        PlayerPrefs.SetFloat(CLAVE_GENERAL, sliderGeneral.value);
        PlayerPrefs.SetFloat(CLAVE_MUSICA, sliderMusica.value);
        PlayerPrefs.SetFloat(CLAVE_EFECTOS, sliderEfectos.value);
        PlayerPrefs.SetFloat(CLAVE_INTERFAZ, sliderInterfaz.value);
        PlayerPrefs.Save();
        AplicarGuardado(audioMixer);
    }

    public void Descartar()
    {
        if (!listo) return;
        MostrarGuardado();
        AplicarGuardado(audioMixer);
    }

    // =========================================================
    // CA4: RESTABLECER
    // =========================================================

    // Botón "Restablecer" de la pestaña.
    public void RestablecerSonido()
    {
        if (pantalla != null) pantalla.ConfirmarRestablecer("el sonido", RestablecerAhora);
        else RestablecerAhora();
    }

    private void RestablecerAhora()
    {
        Mostrar(sliderGeneral, textoGeneral, VOLUMEN_POR_DEFECTO);
        Mostrar(sliderMusica, textoMusica, VOLUMEN_POR_DEFECTO);
        Mostrar(sliderEfectos, textoEfectos, VOLUMEN_POR_DEFECTO);
        Mostrar(sliderInterfaz, textoInterfaz, VOLUMEN_POR_DEFECTO);
        Aplicar();
    }

    // =========================================================
    // CA5: LO GUARDADO SE USA DESDE QUE ARRANCA EL JUEGO
    // =========================================================

    /// <summary>Pasa al mixer los volúmenes guardados. Lo llama el menú principal al arrancar.</summary>
    public static void AplicarGuardado(AudioMixer mixer)
    {
        AplicarAlMixer(mixer, CLAVE_GENERAL, Guardado(CLAVE_GENERAL));
        AplicarAlMixer(mixer, CLAVE_MUSICA, Guardado(CLAVE_MUSICA));
        AplicarAlMixer(mixer, CLAVE_EFECTOS, Guardado(CLAVE_EFECTOS));
        AplicarAlMixer(mixer, CLAVE_INTERFAZ, Guardado(CLAVE_INTERFAZ));
    }

    public void AplicarGuardado() => AplicarGuardado(audioMixer);

    private static void AplicarAlMixer(AudioMixer mixer, string parametro, float valor)
    {
        if (mixer != null) mixer.SetFloat(parametro, Mathf.Log10(Mathf.Max(valor, VOLUMEN_MINIMO)) * 20f);
    }
}
