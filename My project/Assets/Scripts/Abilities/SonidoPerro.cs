using UnityEngine;
using UnityEngine.Audio;

// US 022 · Los sonidos del perro del Callejero (CA11, CA16 y CA17). Salen por el grupo de efectos (SFX), así los
// regulan los volúmenes General y Efectos de Opciones. Sin clip en ConfigRed, suena uno generado.
public static class SonidoPerro
{
    public const float Alcance = 20f; // CA11
    public enum Tipo { Soltar, Ladrido, Grunido, Mordida, Muerte, Desaparece }

    private static readonly AudioClip[] generados = new AudioClip[6];
    private static int ultimoLadrido = -1;

    /// <summary>Si hay ladridos cargados en ConfigRed para alternar mientras corre (entonces no gruñe de fondo).</summary>
    public static bool HayLadridos => Ladridos() > 0;

    private static int Ladridos()
    {
        ConfigRed c = ConfigRed.Actual;
        return c != null && c.ladridosPerro != null ? c.ladridosPerro.Length : 0;
    }

    /// <summary>Un ladrido de la lista, sin repetir el anterior; sin lista, el de siempre.</summary>
    public static AudioClip Ladrido()
    {
        int n = Ladridos();
        if (n > 0)
        {
            int i = Random.Range(0, n);
            if (n > 1 && i == ultimoLadrido) i = (i + 1 + Random.Range(0, n - 1)) % n;
            ultimoLadrido = i;
            AudioClip clip = ConfigRed.Actual.ladridosPerro[i];
            if (clip != null) return clip;
        }
        return Clip(Tipo.Ladrido);
    }

    public static AudioClip Clip(Tipo tipo)
    {
        ConfigRed c = ConfigRed.Actual;
        AudioClip propio = null;
        if (c != null)
            switch (tipo)
            {
                case Tipo.Soltar: propio = c.sonidoPerroSoltar; break;
                case Tipo.Ladrido: propio = c.sonidoPerroLadrido; break;
                case Tipo.Grunido: propio = c.sonidoPerroGrunido; break;
                case Tipo.Mordida: propio = c.sonidoPerroMordida; break;
                case Tipo.Muerte: propio = c.sonidoPerroMuerte; break;
                case Tipo.Desaparece: propio = c.sonidoPerroDesaparece; break;
            }
        if (propio != null) return propio;
        int i = (int)tipo;
        if (generados[i] == null) generados[i] = Generar(tipo);
        return generados[i];
    }

    /// <summary>Un sonido suelto en ese lugar, que se oye hasta 20 m (o sin distancia, para el que lo soltó).</summary>
    public static void Tocar(Tipo tipo, Vector3 lugar, AudioMixerGroup grupo, bool cerca = false, float volumen = 1f)
    {
        AudioClip clip = Clip(tipo);
        if (clip == null) return;
        var go = new GameObject("Perro (sonido)");
        go.transform.position = lugar;
        AudioSource fuente = Fuente(go, grupo, cerca);
        fuente.clip = clip;
        fuente.volume *= volumen;
        fuente.Play();
        Object.Destroy(go, clip.length + 0.1f);
    }

    public static AudioSource Fuente(GameObject go, AudioMixerGroup grupo, bool cerca)
    {
        ConfigRed config = ConfigRed.Actual;
        AudioSource fuente = go.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.outputAudioMixerGroup = grupo;
        fuente.spatialBlend = cerca ? 0f : 1f;
        fuente.rolloffMode = AudioRolloffMode.Linear;
        fuente.minDistance = 2f;
        fuente.maxDistance = Alcance;
        fuente.dopplerLevel = 0f;
        fuente.volume = config != null ? config.volumenPerro : 0.8f;
        return fuente;
    }

    // ---------- Generados ----------

    private delegate float Muestra(float t, float k, float ruido, ref float fase);

    private static AudioClip Sintetizar(string nombre, float duracion, int semilla, float filtro, Muestra muestra)
    {
        const int frecuencia = 44100;
        int n = Mathf.CeilToInt(frecuencia * duracion);
        var datos = new float[n];
        var azar = new System.Random(semilla);
        float ruido = 0f, fase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)frecuencia, k = t / duracion;
            ruido += ((float)azar.NextDouble() * 2f - 1f - ruido) * filtro;
            datos[i] = Mathf.Clamp(muestra(t, k, ruido, ref fase), -1f, 1f);
        }
        AudioClip clip = AudioClip.Create(nombre, n, 1, frecuencia, false);
        clip.SetData(datos, 0);
        return clip;
    }

    // Un ladrido: un golpe de voz que baja de tono, con ruido áspero.
    private static float Ladrido(float t, float inicio, float hz, float ruido, ref float fase)
    {
        float l = t - inicio;
        if (l < 0f || l > 0.22f) return 0f;
        float tono = Mathf.Lerp(hz, hz * 0.55f, l / 0.22f);
        fase += 2f * Mathf.PI * tono / 44100f;
        float env = Mathf.Min(1f, l / 0.012f) * Mathf.Exp(-l * 14f);
        float voz = Mathf.Sin(fase) + 0.5f * Mathf.Sin(fase * 2f) + 0.3f * Mathf.Sin(fase * 3f);
        return env * (0.35f * voz + 0.6f * ruido);
    }

    private static AudioClip Generar(Tipo tipo)
    {
        switch (tipo)
        {
            case Tipo.Soltar: // dos ladridos
                return Sintetizar("Perro soltar (generado)", 0.6f, 41, 0.45f,
                    (float t, float k, float r, ref float f) => Ladrido(t, 0f, 520f, r, ref f) + Ladrido(t, 0.26f, 470f, r, ref f));
            case Tipo.Ladrido:
                return Sintetizar("Perro ladrido (generado)", 0.3f, 42, 0.45f,
                    (float t, float k, float r, ref float f) => Ladrido(t, 0f, 560f, r, ref f));
            case Tipo.Grunido: // grave y rasposo, para repetir mientras corre
                return Sintetizar("Perro gruñido (generado)", 1.2f, 43, 0.12f,
                    (float t, float k, float r, ref float f) =>
                    {
                        f += 2f * Mathf.PI * (95f + 12f * Mathf.Sin(t * 7f)) / 44100f;
                        float aspero = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 28f * t);
                        float env = Mathf.Min(1f, Mathf.Min(t, 1.2f - t) / 0.08f);
                        return env * aspero * (0.4f * Mathf.Sin(f) + 0.25f * Mathf.Sin(f * 2.03f) + 0.5f * r);
                    });
            case Tipo.Mordida: // un chasquido seco y un gruñido corto
                return Sintetizar("Perro mordida (generado)", 0.45f, 44, 0.6f,
                    (float t, float k, float r, ref float f) =>
                    {
                        float golpe = t < 0.03f ? (1f - t / 0.03f) * r * 1.6f : 0f;
                        f += 2f * Mathf.PI * 120f / 44100f;
                        float gru = t > 0.04f ? Mathf.Exp(-(t - 0.04f) * 6f) * (0.35f * Mathf.Sin(f) + 0.3f * r) : 0f;
                        return golpe + gru;
                    });
            case Tipo.Muerte: // un gemido agudo que baja
                return Sintetizar("Perro muerte (generado)", 0.7f, 45, 0.2f,
                    (float t, float k, float r, ref float f) =>
                    {
                        f += 2f * Mathf.PI * Mathf.Lerp(1100f, 420f, k) / 44100f;
                        float env = Mathf.Min(1f, t / 0.02f) * Mathf.Pow(1f - k, 1.5f);
                        return env * (0.4f * Mathf.Sin(f) + 0.15f * Mathf.Sin(f * 2f) + 0.15f * r);
                    });
            default: // Desaparece: un soplido que se apaga
                return Sintetizar("Perro desaparece (generado)", 0.6f, 46, 0.08f,
                    (float t, float k, float r, ref float f) =>
                    {
                        f += 2f * Mathf.PI * Mathf.Lerp(600f, 200f, k) / 44100f;
                        float env = Mathf.Min(1f, t / 0.05f) * (1f - k);
                        return env * (0.15f * Mathf.Sin(f) + 0.7f * r);
                    });
        }
    }
}
