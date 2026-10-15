using UnityEngine;
using UnityEngine.Audio;

// US 021 · Los sonidos de la Vista aérea (CA7, CA12 y CA13). Salen por el grupo de efectos (SFX) del jugador de esta
// computadora, así los regulan los volúmenes General y Efectos de Opciones. Sin clip en ConfigRed, suena uno generado.
public static class SonidoVistaAerea
{
    private static AudioClip activar, terminar, observado;
    private static AudioMixerGroup grupo;

    /// <summary>CA12: al activarla (lo oye el que la usa).</summary>
    public static void Activar() => Tocar(Clip(c => c != null ? c.sonidoVistaAerea : null, ref activar, GenerarActivar), 1f);

    /// <summary>CA13: al terminar la vista.</summary>
    public static void Terminar() => Tocar(Clip(c => c != null ? c.sonidoVistaAereaFin : null, ref terminar, GenerarTerminar), 0.9f);

    /// <summary>CA7: el aviso a los rivales de que los están observando.</summary>
    public static void Observado() => Tocar(Clip(c => c != null ? c.sonidoObservado : null, ref observado, GenerarObservado), 1f);

    private static void Tocar(AudioClip clip, float volumen)
    {
        if (clip == null) return;
        ConfigRed config = ConfigRed.Actual;
        var go = new GameObject("Vista aérea (sonido)");
        AudioSource fuente = go.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.spatialBlend = 0f;
        fuente.outputAudioMixerGroup = Grupo();
        fuente.clip = clip;
        fuente.volume = volumen * (config != null ? config.volumenVistaAerea : 0.8f);
        fuente.Play();
        Object.Destroy(go, clip.length + 0.1f);
    }

    private static AudioMixerGroup Grupo()
    {
        if (grupo == null)
        {
            Pistola pistola = Object.FindFirstObjectByType<Pistola>(FindObjectsInactive.Include);
            if (pistola != null) grupo = pistola.sfxGroup;
        }
        return grupo;
    }

    private static AudioClip Clip(System.Func<ConfigRed, AudioClip> deConfig, ref AudioClip generado, System.Func<AudioClip> generar)
    {
        AudioClip propio = deConfig(ConfigRed.Actual);
        if (propio != null) return propio;
        if (generado == null) generado = generar();
        return generado;
    }

    private delegate float Muestra(float t, float k, float ruido, ref float fase, ref float fase2);

    private static AudioClip Sintetizar(string nombre, float duracion, int semilla, float filtro, Muestra muestra)
    {
        const int frecuencia = 44100;
        int n = Mathf.CeilToInt(frecuencia * duracion);
        var datos = new float[n];
        var azar = new System.Random(semilla);
        float ruido = 0f, fase = 0f, fase2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)frecuencia, k = t / duracion;
            ruido += ((float)azar.NextDouble() * 2f - 1f - ruido) * filtro;
            datos[i] = Mathf.Clamp(muestra(t, k, ruido, ref fase, ref fase2), -1f, 1f);
        }
        AudioClip clip = AudioClip.Create(nombre, n, 1, frecuencia, false);
        clip.SetData(datos, 0);
        return clip;
    }

    // Un soplido que sube con un brillo agudo: la mirada que se eleva.
    private static AudioClip GenerarActivar() => Sintetizar("Vista aérea (generado)", 0.7f, 31, 0.12f,
        (float t, float k, float ruido, ref float fase, ref float fase2) =>
        {
            fase += 2f * Mathf.PI * Mathf.Lerp(220f, 880f, k * k) / 44100f;
            fase2 += 2f * Mathf.PI * Mathf.Lerp(330f, 1320f, k * k) / 44100f;
            float env = Mathf.Min(1f, t / 0.04f) * Mathf.Pow(1f - k, 1.2f);
            return env * (0.3f * Mathf.Sin(fase) + 0.18f * Mathf.Sin(fase2) + 0.5f * ruido * (1f - k));
        });

    // Dos notas que bajan: se acabó.
    private static AudioClip GenerarTerminar() => Sintetizar("Vista aérea fin (generado)", 0.5f, 32, 0.1f,
        (float t, float k, float ruido, ref float fase, ref float fase2) =>
        {
            float hz = t < 0.18f ? 784f : 523f;
            fase += 2f * Mathf.PI * hz / 44100f;
            float local = t < 0.18f ? t : t - 0.18f;
            float env = Mathf.Min(1f, local / 0.01f) * Mathf.Exp(-local * 9f);
            return env * 0.45f * (Mathf.Sin(fase) + 0.3f * Mathf.Sin(fase * 2f));
        });

    // Un zumbido inquietante con un trino lento: te están mirando.
    private static AudioClip GenerarObservado() => Sintetizar("Observado (generado)", 1.1f, 33, 0.05f,
        (float t, float k, float ruido, ref float fase, ref float fase2) =>
        {
            float vibrato = 1f + 0.03f * Mathf.Sin(2f * Mathf.PI * 6f * t);
            fase += 2f * Mathf.PI * 440f * vibrato / 44100f;
            fase2 += 2f * Mathf.PI * 466f * vibrato / 44100f; // casi la misma nota: suena tenso
            float env = Mathf.Min(1f, t / 0.15f) * Mathf.Min(1f, (1f - k) / 0.3f);
            return env * (0.25f * Mathf.Sin(fase) + 0.25f * Mathf.Sin(fase2) + 0.15f * ruido);
        });
}
