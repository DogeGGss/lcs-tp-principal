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
