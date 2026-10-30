using System.Collections.Generic;
using UnityEngine;

// F08 (Modo Invasión), US 269: un portal de donde salen enemigos. Lo abren las oleadas (OleadasDeInvasion), que le dan
// su grupo de enemigos (Fila) y los van sacando por la Salida de a uno.
// - CA2: se abre creciendo en medio segundo, con el sonido de los portales, y zumba mientras está abierto; al cerrarse
//   se achica y desaparece.
// - CA4: mientras está abierto, tapado por una pared se ve en rojo (MarcaAtraves con soloDetras, la marca del perro del
//   Callejero); visto directo se ve normal. Ni los enemigos que pasan por delante ni el propio portal lo tiñen: se
//   dibujan después de la marca (MarcaAtraves.DespuesDeLaMarca).
// El modelo, el tamaño y los sonidos salen de ConfigInvasion. Es sólido: el marco y el centro tienen colisión (los
// jugadores no lo atraviesan y las balas pegan en él); los enemigos salen por delante (Salida), así que no lo cruzan.
public class PortalDeEnemigos : MonoBehaviour
{
    private const float AbreEn = 0.5f, CierraEn = 0.5f; // CA2
    private const float VolumenAbrir = 0.9f, VolumenZumbido = 0.5f;

    /// <summary>Los enemigos que le quedan por largar (CA3).</summary>
    public readonly Queue<TipoDeNpc> Fila = new Queue<TipoDeNpc>();
    /// <summary>Cuándo puede largar el próximo (lo maneja OleadasDeInvasion).</summary>
    public float Proximo { get; set; }
    /// <summary>Ya terminó de abrirse y todavía no se está cerrando: puede largar enemigos.</summary>
    public bool Listo => abierto >= 1f && !cerrando;
    /// <summary>Se está cerrando (o ya se cerró).</summary>
    public bool Cerrando => cerrando;
    /// <summary>Por dónde salen los enemigos: adelante del portal, sobre el piso.</summary>
    public Vector3 Salida => transform.position + transform.forward * (ConfigInvasion.Actual.salida * escala);
    /// <summary>Hacia dónde salen (el frente del portal).</summary>
    public Quaternion Frente => transform.rotation;

    private float escala, abierto;
    private bool cerrando;
    private Transform modelo;
    private AudioSource zumbido;

    /// <summary>Abre un portal en ese lugar del piso, mirando hacia "mira" (CA1), de ese tamaño.</summary>
    public static PortalDeEnemigos Abrir(Vector3 piso, Vector3 mira, float escala)
    {
        Vector3 hacia = mira - piso;
        hacia.y = 0f;
        var go = new GameObject("Portal de enemigos");
        go.transform.SetPositionAndRotation(piso, hacia.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(hacia) : Quaternion.identity);
        var portal = go.AddComponent<PortalDeEnemigos>();
        portal.escala = escala;
        portal.Armar();
        return portal;
    }

    private void Armar()
    {
        ConfigInvasion config = ConfigInvasion.Actual;
        GameObject m = config.portal != null ? Instantiate(config.portal, transform) : Disco();
        m.name = "Modelo";
        m.transform.localPosition = Vector3.zero;
        m.transform.localRotation = Quaternion.Euler(0f, config.giro, 0f);
        m.transform.localScale = Vector3.zero; // crece al abrirse
        modelo = m.transform;
        Solido(m);

        // CA4: rojo detrás de las paredes, mientras esté (se borra con el portal). El portal se dibuja después de la
        // marca, así no se tapa a sí mismo.
        MarcaAtraves.DespuesDeLaMarca(m);
        MarcaAtraves.Marcar(transform, 100000f, MatchHud.RivalColor, true);

        // CA2: el sonido de los portales.
        Sonar(config.sonidoAbrir != null ? config.sonidoAbrir : Resources.Load<AudioClip>("Mapas/portal_tp"), false, VolumenAbrir);
        zumbido = Sonar(config.sonidoAbierto != null ? config.sonidoAbierto : Resources.Load<AudioClip>("Mapas/portal_loop"), true, VolumenZumbido);
    }

    // Colisión en todas sus partes: las que ya traen una la usan (el marco), y las que no (el centro del portal) llevan
    // una caja de su tamaño (una MeshCollider pediría el modelo con Read/Write en el juego compilado). Crece y se achica
    // con el modelo.
    private static void Solido(GameObject m)
    {
        foreach (MeshFilter f in m.GetComponentsInChildren<MeshFilter>(true))
        {
            if (f.sharedMesh == null) continue;
            Collider c = f.GetComponent<Collider>();
            if (c == null)
            {
                var caja = f.gameObject.AddComponent<BoxCollider>();
                caja.center = f.sharedMesh.bounds.center;
                caja.size = f.sharedMesh.bounds.size;
                c = caja;
            }
            c.enabled = true;
            c.isTrigger = false;
        }
    }

    // Sin modelo cargado: un disco rojo parado.
    private GameObject Disco()
    {
        GameObject d = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        d.transform.SetParent(transform, false);
        var hijo = d.transform;
        var raiz = new GameObject("Disco");
        raiz.transform.SetParent(transform, false);
        hijo.SetParent(raiz.transform, false);
        hijo.localPosition = Vector3.up * 1.6f;
        hijo.localRotation = Quaternion.Euler(90f, 0f, 0f);
        hijo.localScale = new Vector3(2.6f, 0.05f, 3.2f);
        var r = d.GetComponent<MeshRenderer>();
        r.sharedMaterial = Veneno.Material(new Color(0.9f, 0.15f, 0.15f, 0.85f));
        return raiz;
    }

    /// <summary>CA2: se cierra achicándose y desaparece.</summary>
    public void Cerrar()
    {
        if (cerrando) return;
        cerrando = true;
        Fila.Clear();
    }

    private void Update()
    {
        if (!cerrando)
        {
            abierto = Mathf.Min(1f, abierto + Time.deltaTime / AbreEn);
            modelo.localScale = Vector3.one * escala * Mathf.SmoothStep(0f, 1f, abierto);
            return;
        }
        abierto = Mathf.Max(0f, Mathf.Min(abierto, 1f) - Time.deltaTime / CierraEn);
        modelo.localScale = Vector3.one * escala * Mathf.SmoothStep(0f, 1f, abierto);
        if (zumbido != null) zumbido.volume = VolumenZumbido * abierto;
        if (abierto <= 0f) Destroy(gameObject);
    }

    private AudioSource Sonar(AudioClip clip, bool loop, float volumen)
    {
        if (clip == null) return null;
        AudioSource fuente = gameObject.AddComponent<AudioSource>();
        fuente.clip = clip;
        fuente.loop = loop;
        fuente.playOnAwake = false;
        fuente.spatialBlend = 1f;
        fuente.rolloffMode = AudioRolloffMode.Linear;
        fuente.minDistance = 4f;
        fuente.maxDistance = 45f; // se escucha desde lejos: avisa de dónde vienen
        fuente.dopplerLevel = 0f;
        fuente.volume = volumen;
        fuente.outputAudioMixerGroup = Grupo();
        if (loop) fuente.time = Random.Range(0f, clip.length);
        fuente.Play();
        return fuente;
    }

    private static UnityEngine.Audio.AudioMixerGroup grupo;
    private static UnityEngine.Audio.AudioMixerGroup Grupo()
    {
        if (grupo == null)
        {
            Pistola pistola = FindFirstObjectByType<Pistola>(FindObjectsInactive.Include);
            if (pistola != null) grupo = pistola.sfxGroup;
        }
        return grupo;
    }
}
