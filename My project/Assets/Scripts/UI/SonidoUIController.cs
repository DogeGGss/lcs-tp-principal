using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using Slider = UnityEngine.UI.Slider;

// Pestaña Sonido de las opciones (US 048 y US 154). Se arma por código con el mismo estilo que la pestaña
// Controles (OpcionesKit) y tapa los controles que había en la escena.
// Al mover un slider se escucha el cambio al instante, pero se guarda recién con "Aplicar" (CA3).
// Si el jugador descarta, vuelve el volumen guardado. "Restablecer" pide confirmación (CA4).
public class SonidoUIController : MonoBehaviour, OpcionesPantalla.ISeccion
{
    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;

    // Parámetros del Audio Mixer y claves de PlayerPrefs (se llaman igual)
    private const string CLAVE_GENERAL = "VolumenGeneral";
    private const string CLAVE_MUSICA = "VolumenMusica";
    private const string CLAVE_EFECTOS = "VolumenEfectos";

    private const float VOLUMEN_POR_DEFECTO = 1f;
    private const float VOLUMEN_MINIMO = 0.0001f;

    private OpcionesPantalla pantalla;
    private Slider sliderGeneral, sliderMusica, sliderEfectos;
    private TextMeshProUGUI textoGeneral, textoMusica, textoEfectos;
    private bool listo;

    private void Start()
    {
        pantalla = OpcionesPantalla.De(this);
        if (pantalla != null) pantalla.Registrar(this);

        OpcionesKit kit = OpcionesKit.Armar((RectTransform)transform, "Sonido");
        float y = 114f;
        kit.Grupo("Volumen", ref y);
        sliderGeneral = Fila(kit, "General", CLAVE_GENERAL, ref y, out textoGeneral);
        sliderMusica = Fila(kit, "Música", CLAVE_MUSICA, ref y, out textoMusica);
        sliderEfectos = Fila(kit, "Efectos", CLAVE_EFECTOS, ref y, out textoEfectos);
        y += 10f;
        kit.Ayuda("El volumen se escucha al moverlo y se guarda al apretar Aplicar.", ref y);
        kit.Pie(ref y, AplicarSonido, RestablecerSonido);

        MostrarGuardado();
        AplicarGuardado(audioMixer);
        listo = true;
    }

    // Se escucha al instante, pero no se guarda hasta "Aplicar".
    private Slider Fila(OpcionesKit kit, string titulo, string clave, ref float y, out TextMeshProUGUI valor)
    {
        Slider slider = kit.FilaSlider(titulo, ref y, VOLUMEN_MINIMO, 1f, out valor);
        TextMeshProUGUI texto = valor;
        slider.onValueChanged.AddListener(v =>
        {
            texto.text = Porcentaje(v);
            AplicarAlMixer(audioMixer, clave, v);
        });
        return slider;
    }

    private static string Porcentaje(float v) => Mathf.RoundToInt(v * 100f) + " %";

    private void MostrarGuardado()
    {
        Mostrar(sliderGeneral, textoGeneral, Guardado(CLAVE_GENERAL));
        Mostrar(sliderMusica, textoMusica, Guardado(CLAVE_MUSICA));
        Mostrar(sliderEfectos, textoEfectos, Guardado(CLAVE_EFECTOS));
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
        listo && (Distinto(sliderGeneral, CLAVE_GENERAL) || Distinto(sliderMusica, CLAVE_MUSICA) || Distinto(sliderEfectos, CLAVE_EFECTOS));

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
    }

    public void AplicarGuardado() => AplicarGuardado(audioMixer);

    private static void AplicarAlMixer(AudioMixer mixer, string parametro, float valor)
    {
        if (mixer != null) mixer.SetFloat(parametro, Mathf.Log10(Mathf.Max(valor, VOLUMEN_MINIMO)) * 20f);
    }
}
