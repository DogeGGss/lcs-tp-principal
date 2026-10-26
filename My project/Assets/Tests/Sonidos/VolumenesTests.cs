using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;

// Regresión de sonidos (RS-29 a RS-32): con cada volumen de Opciones en 0 (US 154), lo que sale por su canal no se
// escucha. Suena un tono por un canal del mezclador del juego y se mide lo que llega al oyente (AudioListener) con el
// volumen normal y en 0, puesto como lo pone el juego (guardado y aplicado al mezclador). El resto de los sonidos se
// apaga mientras se mide, así lo único que se escucha es el tono.

[PrebuildSetup(typeof(PruebasAisladas)), PostBuildCleanup(typeof(PruebasAisladas))]
public class VolumenesTests
{
    [UnityTest, Category("Regresión de sonidos"), Category("US 154"), Description("RS-29: con Efectos en 0, lo que sale por SFX no se escucha")]
    public IEnumerator RS29_Efectos_en_0() => SeCalla("SFX", "VolumenEfectos");

    [UnityTest, Category("Regresión de sonidos"), Category("US 154"), Description("RS-30: con Interfaz en 0, lo que sale por Interfaz no se escucha")]
    public IEnumerator RS30_Interfaz_en_0() => SeCalla("Interfaz", "VolumenInterfaz");

    [UnityTest, Category("Regresión de sonidos"), Category("US 154"), Description("RS-31: con Música en 0, lo que sale por Musica no se escucha")]
    public IEnumerator RS31_Musica_en_0() => SeCalla("Musica", "VolumenMusica");

    [UnityTest, Category("Regresión de sonidos"), Category("US 154"), Description("RS-32: con General en 0, no se escucha nada (probado con SFX)")]
    public IEnumerator RS32_General_en_0() => SeCalla("SFX", "VolumenGeneral");

    private static readonly float[] muestras = new float[1024];

    private static IEnumerator SeCalla(string canal, string volumen)
    {
#if UNITY_EDITOR
        AudioMixer mezclador = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/NewAudioMixer.mixer");
        bool silenciado = UnityEditor.EditorUtility.audioMasterMute;
        UnityEditor.EditorUtility.audioMasterMute = false; // el botón "Mute Audio" de la ventana Game
#else
        AudioMixer mezclador = null;
        bool silenciado = false;
#endif
        Assert.IsNotNull(mezclador, "Falta el mezclador del juego (Assets/NewAudioMixer.mixer)");
        AudioMixerGroup grupo = System.Array.Find(mezclador.FindMatchingGroups(""), g => g.name == canal);
        Assert.IsNotNull(grupo, "El mezclador no tiene el canal " + canal);

        foreach (AudioSource otra in Object.FindObjectsByType<AudioSource>()) otra.enabled = false;
        foreach (AudioListener otro in Object.FindObjectsByType<AudioListener>()) otro.enabled = false;
        var oyente = new GameObject("Oyente (test)").AddComponent<AudioListener>();
        var tono = new GameObject("Tono (test)").AddComponent<AudioSource>();
        tono.clip = Tono();
        tono.loop = true;
        tono.spatialBlend = 0f;
        tono.outputAudioMixerGroup = grupo;
        float antes = Guardado.GetFloat(volumen, 1f);
        try
        {
            Guardado.SetFloat(volumen, 1f);
            SonidoUIController.AplicarGuardado(mezclador);
            tono.Play();
            // El audio puede tardar en arrancar (con un oyente recién creado): se espera a que llegue el tono.
            float normal = 0f, limite = Time.realtimeSinceStartup + 2f;
            while (normal <= 0.01f && Time.realtimeSinceStartup < limite)
            {
                yield return null;
                normal = Mathf.Max(normal, Nivel());
            }
            Assert.Greater(normal, 0.01f, $"Con {volumen} al máximo el tono por {canal} tendría que escucharse");

            Guardado.SetFloat(volumen, 0f);
            SonidoUIController.AplicarGuardado(mezclador);
            yield return new WaitForSecondsRealtime(0.4f);
            float callado = Nivel();
            Assert.Less(callado, normal * 0.01f, $"Con {volumen} en 0 lo que sale por {canal} se sigue escuchando ({callado:0.0000} contra {normal:0.0000})");
        }
        finally
        {
            Guardado.SetFloat(volumen, antes);
            SonidoUIController.AplicarGuardado(mezclador);
            Object.Destroy(tono.gameObject);
            Object.Destroy(oyente.gameObject);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.audioMasterMute = silenciado;
#endif
        }
    }

    // El pico de lo que llega al oyente en este momento.
    private static float Nivel()
    {
        AudioListener.GetOutputData(muestras, 0);
        float pico = 0f;
        foreach (float m in muestras) pico = Mathf.Max(pico, Mathf.Abs(m));
        return pico;
    }

    private static AudioClip Tono()
    {
        const int hz = 44100;
        var datos = new float[hz];
        for (int i = 0; i < datos.Length; i++) datos[i] = 0.5f * Mathf.Sin(2f * Mathf.PI * 440f * i / hz);
        AudioClip clip = AudioClip.Create("Tono 440 Hz", datos.Length, 1, hz, false);
        clip.SetData(datos, 0);
        return clip;
    }
}
