using UnityEngine;
using UnityEngine.Audio;

// F11 (NPCs): los sonidos de los enemigos, en 3D (se escuchan desde donde está el bicho, más fuerte cerca) y por el grupo
// de efectos del mezclador (el de las armas), así los regula el volumen de efectos de Opciones.
// - Uno: un sonido suelto (el grito de US 036, el ataque, el rugido o el escupitajo del Jefe) que sigue al bicho.
// - En: un sonido suelto en un lugar (la bola de veneno que revienta en el piso).
// - EnLoop: un sonido que se repite mientras dure algo (el zumbido de las alas del Jefe al volar); se corta con Cortar.
// Los clips salen de TipoDeNpc (sección Sonido); sin clip no suena nada.
public static class SonidoDeNpc
{
    private static AudioMixerGroup grupo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar() => grupo = null; // sin recarga de dominio

    /// <summary>El grupo de efectos del mezclador (el de las armas).</summary>
    public static AudioMixerGroup GrupoDeEfectos()
    {
        if (grupo == null)
        {
            Pistola pistola = Object.FindFirstObjectByType<Pistola>(FindObjectsInactive.Include);
            if (pistola != null) grupo = pistola.sfxGroup;
        }
        return grupo;
    }

    /// <summary>Un sonido suelto que sigue al bicho (se escucha hasta a hasta metros).</summary>
    public static AudioSource Uno(AudioClip clip, Transform bicho, float hasta = 30f, float volumen = 1f)
    {
        if (clip == null || bicho == null) return null;
        AudioSource fuente = Fuente(clip, bicho, hasta, volumen, false, "NPC (" + clip.name + ")");
        fuente.Play();
        Object.Destroy(fuente.gameObject, clip.length + 0.1f);
        return fuente;
    }

    /// <summary>Un sonido suelto en un lugar fijo.</summary>
    public static AudioSource En(AudioClip clip, Vector3 lugar, float hasta = 30f, float volumen = 1f)
    {
        if (clip == null) return null;
        var go = new GameObject("NPC (" + clip.name + ")");
        go.transform.position = lugar;
        AudioSource fuente = Configurar(go.AddComponent<AudioSource>(), clip, hasta, volumen, false);
        fuente.Play();
        Object.Destroy(go, clip.length + 0.1f);
        return fuente;
    }

    /// <summary>Un sonido que se repite hasta que se corte (Cortar).</summary>
    public static AudioSource EnLoop(AudioClip clip, Transform bicho, float hasta = 30f, float volumen = 1f)
    {
        if (clip == null || bicho == null) return null;
        AudioSource fuente = Fuente(clip, bicho, hasta, volumen, true, "NPC (" + clip.name + ", loop)");
        fuente.time = Random.Range(0f, clip.length * 0.9f); // no siempre desde el mismo lugar
        fuente.Play();
        return fuente;
    }

    /// <summary>Corta un sonido en loop, bajándolo de a poco (en segundos).</summary>
    public static void Cortar(AudioSource fuente, float bajaEn = 0.4f)
    {
        if (fuente == null) return;
        var baja = fuente.GetComponent<BajarYApagar>();
        if (baja == null) baja = fuente.gameObject.AddComponent<BajarYApagar>();
        baja.En = Mathf.Max(0.01f, bajaEn);
    }

    private static AudioSource Fuente(AudioClip clip, Transform bicho, float hasta, float volumen, bool loop, string nombre)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(bicho, false);
        go.transform.localPosition = Vector3.up;
        return Configurar(go.AddComponent<AudioSource>(), clip, hasta, volumen, loop);
    }

    private static AudioSource Configurar(AudioSource fuente, AudioClip clip, float hasta, float volumen, bool loop)
    {
        fuente.clip = clip;
        fuente.loop = loop;
        fuente.volume = volumen;
        fuente.spatialBlend = 1f;
        fuente.rolloffMode = AudioRolloffMode.Linear;
        fuente.minDistance = 3f;
        fuente.maxDistance = Mathf.Max(5f, hasta);
        fuente.dopplerLevel = 0f;
        fuente.outputAudioMixerGroup = GrupoDeEfectos();
        return fuente;
    }

    // Baja el volumen hasta 0 y borra la fuente.
    private class BajarYApagar : MonoBehaviour
    {
        public float En = 0.4f;
        private AudioSource fuente;
        private float desde;

        private void Start()
        {
            fuente = GetComponent<AudioSource>();
            desde = fuente != null ? fuente.volume : 0f;
        }

        private void Update()
        {
            if (fuente == null) { Destroy(gameObject); return; }
            fuente.volume = Mathf.MoveTowards(fuente.volume, 0f, desde / En * Time.deltaTime);
            if (fuente.volume <= 0f) Destroy(gameObject);
        }
    }
}
