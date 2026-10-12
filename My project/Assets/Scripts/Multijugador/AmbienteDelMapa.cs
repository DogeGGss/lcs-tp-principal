using UnityEngine;
using UnityEngine.Audio;

// US 083, CA1: el sonido ambiente del mapa, sin música (US 032, CA8). Hay un zumbido de fondo que se escucha igual en
// todo el mapa, y las consolas y máquinas del kit hacen ruido desde donde están: más fuerte al acercarse y nada a unos
// metros. Todo sale por el grupo de efectos (SFX), así lo regulan General y Efectos de Opciones (US 154).
// Va en un objeto de la escena del mapa. Las piezas que suenan se buscan por nombre al empezar, así que no hace falta
// tocarlas una por una: alcanza con que su nombre empiece como dicen las listas de abajo.
public class AmbienteDelMapa : MonoBehaviour
{
    [Tooltip("Grupo SFX del Audio Mixer.")]
    [SerializeField] private AudioMixerGroup efectos;

    [Header("Fondo: se escucha igual en todo el mapa")]
    [SerializeField] private AudioClip fondo;
    [Range(0f, 1f)] [SerializeField] private float volumenFondo = 0.3f;

    [Header("Consolas y máquinas: se escuchan desde donde están")]
    [SerializeField] private AudioClip consola;
    [SerializeField] private AudioClip maquina;
    [Range(0f, 1f)] [SerializeField] private float volumenEquipos = 0.6f;
    [SerializeField] private float distanciaMinima = 1.5f;
    [SerializeField] private float distanciaMaxima = 10f;
    [Tooltip("Las piezas cuyo nombre empieza así suenan como consola.")]
    [SerializeField] private string[] consolas = { "Wall_Console", "Intercom" };
    [Tooltip("Las piezas cuyo nombre empieza así suenan como máquina.")]
    [SerializeField] private string[] maquinas = { "Wall_Gear", "Wall_Pipes", "Pipes_" };

    private void Start()
    {
        // Lo que ya traía su propio sonido (por ejemplo, las puertas del kit) también sale por efectos.
        foreach (AudioSource propia in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (propia.gameObject.scene == gameObject.scene && propia.outputAudioMixerGroup == null)
                propia.outputAudioMixerGroup = efectos;

        if (fondo != null) Fuente(gameObject, fondo, volumenFondo, false);

        foreach (Transform pieza in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (pieza.gameObject.scene != gameObject.scene) continue;
            AudioClip clip = Clip(pieza.name);
            // Solo la pieza entera, no sus partes (que a veces se llaman parecido).
            if (clip == null || (pieza.parent != null && Clip(pieza.parent.name) != null)) continue;
            var go = new GameObject("Sonido ambiente");
            go.transform.SetParent(pieza, false);
            Fuente(go, clip, volumenEquipos, true);
        }
    }

    private AudioClip Clip(string nombre)
    {
        if (consola != null && Empieza(nombre, consolas)) return consola;
        if (maquina != null && Empieza(nombre, maquinas)) return maquina;
        return null;
    }

    private static bool Empieza(string nombre, string[] prefijos)
    {
        foreach (string prefijo in prefijos)
            if (!string.IsNullOrEmpty(prefijo) && nombre.StartsWith(prefijo, System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private void Fuente(GameObject go, AudioClip clip, float volumen, bool enElLugar)
    {
        AudioSource fuente = go.AddComponent<AudioSource>();
        fuente.clip = clip;
        fuente.loop = true;
        fuente.playOnAwake = false;
        fuente.volume = volumen;
        fuente.outputAudioMixerGroup = efectos;
        fuente.dopplerLevel = 0f;
        // El ambiente es lo primero que se deja de escuchar si hay demasiados sonidos a la vez: el combate tiene prioridad.
        fuente.priority = 200;
        if (enElLugar)
        {
            fuente.spatialBlend = 1f;
            fuente.rolloffMode = AudioRolloffMode.Linear;
            fuente.minDistance = distanciaMinima;
            fuente.maxDistance = distanciaMaxima;
        }
        else fuente.spatialBlend = 0f;
        // Cada una arranca en un punto distinto del clip, así las iguales no suenan sincronizadas.
        if (clip.samples > 0) fuente.timeSamples = Random.Range(0, clip.samples);
        fuente.Play();
    }
}
