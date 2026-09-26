using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class SonidoUIController : MonoBehaviour
{
    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("Sliders")]
    [SerializeField] private Slider sliderGeneral;
    [SerializeField] private Slider sliderMusica;
    [SerializeField] private Slider sliderEfectos;

    // Parámetros del Audio Mixer
    private const string PARAMETRO_GENERAL = "VolumenGeneral";
    private const string PARAMETRO_MUSICA = "VolumenMusica";
    private const string PARAMETRO_EFECTOS = "VolumenEfectos";

    // Claves de PlayerPrefs
    private const string CLAVE_GENERAL = "VolumenGeneral";
    private const string CLAVE_MUSICA = "VolumenMusica";
    private const string CLAVE_EFECTOS = "VolumenEfectos";

    // Volumen por defecto
    private const float VOLUMEN_POR_DEFECTO = 1f;

    private void Start()
    {
        // Cargar valores guardados
        float volumenGeneral = PlayerPrefs.GetFloat(
            CLAVE_GENERAL,
            VOLUMEN_POR_DEFECTO
        );

        float volumenMusica = PlayerPrefs.GetFloat(
            CLAVE_MUSICA,
            VOLUMEN_POR_DEFECTO
        );

        float volumenEfectos = PlayerPrefs.GetFloat(
            CLAVE_EFECTOS,
            VOLUMEN_POR_DEFECTO
        );

        // Configurar sliders
        ConfigurarSlider(sliderGeneral, volumenGeneral);
        ConfigurarSlider(sliderMusica, volumenMusica);
        ConfigurarSlider(sliderEfectos, volumenEfectos);

        // Escuchar cambios
        sliderGeneral.onValueChanged.AddListener(CambiarVolumenGeneral);
        sliderMusica.onValueChanged.AddListener(CambiarVolumenMusica);
        sliderEfectos.onValueChanged.AddListener(CambiarVolumenEfectos);

        // Aplicar valores al Mixer
        CambiarVolumenGeneral(volumenGeneral);
        CambiarVolumenMusica(volumenMusica);
        CambiarVolumenEfectos(volumenEfectos);
    }

    private void ConfigurarSlider(Slider slider, float valor)
    {
        slider.minValue = 0.0001f;
        slider.maxValue = 1f;
        slider.value = valor;
    }

    private void CambiarVolumenGeneral(float valor)
    {
        float volumenDB = Mathf.Log10(valor) * 20f;

        audioMixer.SetFloat(PARAMETRO_GENERAL, volumenDB);

        PlayerPrefs.SetFloat(CLAVE_GENERAL, valor);
        PlayerPrefs.Save();
    }

    private void CambiarVolumenMusica(float valor)
    {
        float volumenDB = Mathf.Log10(valor) * 20f;

        audioMixer.SetFloat(PARAMETRO_MUSICA, volumenDB);

        PlayerPrefs.SetFloat(CLAVE_MUSICA, valor);
        PlayerPrefs.Save();
    }

    private void CambiarVolumenEfectos(float valor)
    {
        float volumenDB = Mathf.Log10(valor) * 20f;

        audioMixer.SetFloat(PARAMETRO_EFECTOS, volumenDB);

        PlayerPrefs.SetFloat(CLAVE_EFECTOS, valor);
        PlayerPrefs.Save();
    }

    public void RestablecerSonido()
    {
        sliderGeneral.value = VOLUMEN_POR_DEFECTO;
        sliderMusica.value = VOLUMEN_POR_DEFECTO;
        sliderEfectos.value = VOLUMEN_POR_DEFECTO;

        CambiarVolumenGeneral(VOLUMEN_POR_DEFECTO);
        CambiarVolumenMusica(VOLUMEN_POR_DEFECTO);
        CambiarVolumenEfectos(VOLUMEN_POR_DEFECTO);
    }
}