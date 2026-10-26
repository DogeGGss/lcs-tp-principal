using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Mapa 1: las puertas verticales del kit sci-fi (Door_Vert_01) bajan y se meten en el piso cuando alguien se acerca y
// vuelven a subir cuando ya no hay nadie, junto con las consolas que tienen pegadas, con el sonido neumático que trae el prefab. Cada computadora las mueve sola con las posiciones de los
// personajes que ve, así que en red se abren igual para todos sin mandar nada.
// Se arman solas al cargar las escenas de la lista (el prefab también se usa en el mapa táctico, donde se quedan quietas).
public class PuertaAutomatica : MonoBehaviour
{
    private static readonly string[] Escenas = { "Mapa_deathmatch" };
    private const string Malla = "Door_Vert";

    private const float Radio = 3.5f;         // m alrededor de la puerta (sin contar la altura) para que se abra
    private const float Alto = 3f;            // m de diferencia de altura que todavía cuenta
    private const float Apertura = 0.45f;     // s en abrirse
    private const float Cierre = 0.6f;        // s en cerrarse
    private const float Espera = 0.8f;        // s sin nadie cerca antes de cerrarse

    private static readonly List<PuertaAutomatica> puertas = new List<PuertaAutomatica>();
    private static readonly List<Transform> cuerpos = new List<Transform>();
    private static float proximaLista;

    private Transform hoja;          // la hoja de la puerta (malla y colisión)
    private Transform[] piezas;      // lo que se mueve: la hoja y lo que trae pegado (consolas y sus luces)
    private Vector3[] cerradas;      // dónde está cada pieza con la puerta cerrada
    private Vector3 medio;           // el centro de la puerta cerrada, en el espacio de la puerta
    private float altura;            // cuánto baja (en el espacio de la puerta)
    private AudioSource sonido;
    private float abierto;           // 0 cerrada, 1 abierta
    private bool abriendo;
    private float ultimoCerca = -10f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Iniciar()
    {
        SceneManager.sceneLoaded -= AlCargar;
        SceneManager.sceneLoaded += AlCargar;
        for (int i = 0; i < SceneManager.sceneCount; i++) AlCargar(SceneManager.GetSceneAt(i), LoadSceneMode.Additive);
    }

    private static void AlCargar(Scene escena, LoadSceneMode modo)
    {
        if (!escena.isLoaded || System.Array.IndexOf(Escenas, escena.name) < 0) return;
        foreach (GameObject raiz in escena.GetRootGameObjects())
            foreach (MeshFilter filtro in raiz.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filtro.sharedMesh == null || !(filtro.sharedMesh.name.StartsWith(Malla) || filtro.name.StartsWith(Malla))) continue;
                Transform puerta = filtro.transform.parent != null ? filtro.transform.parent : filtro.transform;
                if (puerta.GetComponent<PuertaAutomatica>() == null)
                    puerta.gameObject.AddComponent<PuertaAutomatica>().Armar(filtro);
            }
    }

    private void Armar(MeshFilter filtro)
    {
        hoja = filtro.transform;
        // Todo lo que cuelga de la puerta se mueve junto (antes las consolas quedaban flotando).
        piezas = new Transform[transform.childCount];
        cerradas = new Vector3[piezas.Length];
        for (int i = 0; i < piezas.Length; i++)
        {
            piezas[i] = transform.GetChild(i);
            cerradas[i] = piezas[i].localPosition;
        }
        // Baja lo que mide la hoja (un poco menos, así el borde de arriba queda a ras del piso).
        Renderer dibujo = filtro.GetComponent<Renderer>();
        float mundo = dibujo != null ? dibujo.bounds.size.y : 3f;
        medio = transform.InverseTransformPoint(dibujo != null ? dibujo.bounds.center : hoja.position);
        altura = mundo * 0.97f / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y));

        sonido = GetComponent<AudioSource>();
        if (sonido != null)
        {
            sonido.playOnAwake = false;
            sonido.loop = false;
            sonido.spatialBlend = 1f;
            sonido.rolloffMode = AudioRolloffMode.Linear;
            sonido.minDistance = 2f;
            sonido.maxDistance = 20f;
            sonido.dopplerLevel = 0f;
            if (sonido.outputAudioMixerGroup == null)
            {
                // Por el grupo de efectos, así lo regulan los volúmenes de Opciones.
                Pistola pistola = FindAnyObjectByType<Pistola>(FindObjectsInactive.Include);
                if (pistola != null) sonido.outputAudioMixerGroup = pistola.sfxGroup;
            }
        }
    }

    private void OnEnable() => puertas.Add(this);
    private void OnDisable() => puertas.Remove(this);

    /// <summary>Si esa colisión es de una de estas puertas (el NavMesh del perro no las cuenta como pared).</summary>
    public static bool EsPuerta(Collider c) => c != null && c.GetComponentInParent<PuertaAutomatica>() != null;

    private void Update()
    {
        if (hoja == null) return;
        if (Time.time >= proximaLista) ArmarLista();

        Vector3 centro = transform.TransformPoint(medio);
        bool cerca = false;
        foreach (Transform t in cuerpos)
        {
            if (t == null) continue;
            Vector3 d = t.position - centro;
            if (Mathf.Abs(d.y) <= Alto && new Vector2(d.x, d.z).sqrMagnitude <= Radio * Radio) { cerca = true; break; }
        }
        if (cerca) ultimoCerca = Time.time;
        bool quiere = Time.time - ultimoCerca < Espera;

        if (quiere != abriendo)
        {
            abriendo = quiere;
            if (sonido != null) sonido.Play();
        }
        abierto = Mathf.MoveTowards(abierto, abriendo ? 1f : 0f, Time.deltaTime / (abriendo ? Apertura : Cierre));
        float k = Mathf.SmoothStep(0f, 1f, abierto);
        // Las piezas son hijas de la puerta: bajan en su eje vertical.
        for (int i = 0; i < piezas.Length; i++)
            if (piezas[i] != null) piezas[i].localPosition = cerradas[i] + Vector3.down * altura * k;
    }

    // Personajes (jugadores, rivales en red, zombis) y perros: lo que puede pasar por la puerta.
    private static void ArmarLista()
    {
        proximaLista = Time.time + 0.25f;
        cuerpos.Clear();
        foreach (HealthSystem h in FindObjectsByType<HealthSystem>(FindObjectsSortMode.None))
            if (h.currentHealth > 0) cuerpos.Add(h.transform);
        foreach (CharacterController c in FindObjectsByType<CharacterController>(FindObjectsSortMode.None))
            cuerpos.Add(c.transform);
        foreach (Perro p in FindObjectsByType<Perro>(FindObjectsSortMode.None))
            if (p.Activo) cuerpos.Add(p.transform);
    }
}
