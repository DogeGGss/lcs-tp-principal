using UnityEngine;
using UnityEngine.Audio;

// Sonido corto al pasar el mouse por un botón de los menús. Sale por el grupo Interfaz del Audio Mixer, así respeta
// el volumen de Interfaz de las opciones. Lo configura el menú principal al arrancar (con el clip y el grupo que tiene
// en la escena) y queda vivo entre escenas, para los menús de la partida.
public static class SonidoInterfaz
{
    private static AudioSource fuente;
    private static AudioClip clipPasar;
    private static int ultimoCuadro = -1;
    private static AudioClip clipLogro;

    public static void Logro()
    {
        if (fuente == null) return;
        if (clipLogro == null)
        {
            const int hz = 22050;
            var muestras = new float[hz / 2];
            for (int i = 0; i < muestras.Length; i++)
            {
                float t = (float)i / hz;
                float frecuencia = t < .16f ? 659f : t < .32f ? 831f : 988f;
                muestras[i] = .18f * Mathf.Sin(2f * Mathf.PI * frecuencia * t) * Mathf.Sin(Mathf.PI * i / muestras.Length);
            }
            clipLogro = AudioClip.Create("Logro desbloqueado", muestras.Length, 1, hz, false);
            clipLogro.SetData(muestras, 0);
        }
        fuente.PlayOneShot(clipLogro);
    }

    public static void Configurar(AudioClip pasar, AudioMixerGroup grupo)
    {
        if (fuente == null)
        {
            var go = new GameObject("SonidoInterfaz");
            Object.DontDestroyOnLoad(go);
            fuente = go.AddComponent<AudioSource>();
            fuente.playOnAwake = false;
            fuente.spatialBlend = 0f;
            fuente.ignoreListenerPause = true; // también suena con el juego en pausa
        }
        fuente.outputAudioMixerGroup = grupo;
        clipPasar = pasar;
    }

    /// <summary>Al pasar el mouse por un botón. Suena una vez por cuadro aunque varios componentes del botón lo pidan.</summary>
    public static void Pasar()
    {
        if (fuente == null || clipPasar == null || Time.frameCount == ultimoCuadro) return;
        ultimoCuadro = Time.frameCount;
        fuente.PlayOneShot(clipPasar);
    }
}
