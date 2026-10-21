using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

// Lo que "escucha" un test de regresión de sonidos: las fuentes de audio que empiezan a sonar después de una acción del
// jugador, con el canal del mezclador por el que salen y su alcance. Una fuente suena si tiene un clip en Play o si su
// salida tiene muestras (PlayOneShot no cambia el clip de la fuente). Los datos se guardan en el momento en que suena,
// porque muchas fuentes se destruyen apenas termina su sonido.

public sealed class Sonido
{
    public AudioSource fuente;
    public string nombre, canal;
    public bool es3D;
    public float alcance;

    public Sonido(AudioSource f)
    {
        fuente = f;
        nombre = f.name + (f.clip != null ? " · " + f.clip.name : "");
        canal = f.outputAudioMixerGroup != null ? f.outputAudioMixerGroup.name : "(sin canal)";
        es3D = f.spatialBlend > 0.5f;
        alcance = f.maxDistance;
    }

    public override string ToString() => $"{nombre} [{canal}{(es3D ? $", 3D hasta {alcance:0.#} m" : ", 2D")}]";
}

public static class Oido
{
    private static readonly float[] muestras = new float[256];

    public static bool Suena(AudioSource fuente)
    {
        if (fuente == null || !fuente.isActiveAndEnabled) return false;
        if (fuente.isPlaying) return true;
        fuente.GetOutputData(muestras, 0);
        foreach (float m in muestras) if (Mathf.Abs(m) > 1e-4f) return true;
        return false;
    }

    /// <summary>
    /// Hace la acción y junta, durante esos segundos, las fuentes que empezaron a sonar. Las que ya sonaban antes (la
    /// música, el ambiente) no cuentan. filtro: solo las fuentes que interesan (por ejemplo, las del jugador).
    /// </summary>
    public static IEnumerator Escuchar(Action accion, List<Sonido> oidos, Func<AudioSource, bool> filtro = null, float segundos = 0.5f)
    {
        var antes = new HashSet<AudioSource>();
        foreach (AudioSource f in Object.FindObjectsByType<AudioSource>()) if (Suena(f)) antes.Add(f);
        accion();
        float fin = Time.realtimeSinceStartup + segundos;
        do
        {
            yield return null;
            foreach (AudioSource f in Object.FindObjectsByType<AudioSource>())
            {
                if (antes.Contains(f) || (filtro != null && !filtro(f)) || !Suena(f)) continue;
                if (!oidos.Exists(o => o.fuente == f)) oidos.Add(new Sonido(f));
            }
        } while (Time.realtimeSinceStartup < fin);
    }

    public static string Lista(List<Sonido> oidos) => oidos.Count == 0 ? "no sonó nada" : string.Join(", ", oidos);
}
