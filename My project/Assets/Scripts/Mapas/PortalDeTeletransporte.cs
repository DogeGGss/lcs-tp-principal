using Photon.Pun;
using UnityEngine;
using UnityEngine.Audio;

// Mapa 1: los portales (The Portal Collection). El que entra por el remolino de un portal sale por el otro, en su
// punto de salida (los objetos TP_portal de la escena), mirando hacia afuera del portal de salida.
// - Va en cada portal, con un trigger que tapa el remolino (en la capa Ignore Raycast: las balas lo atraviesan).
// - Lo hace el jugador de esta computadora (el dueño de su cuerpo). Los demás lo ven aparecer de golpe en el otro
//   portal y escuchan el paso (JugadorEnRed, con Paso).
// - Después de pasar, un rato sin volver a pasar, por si sale encima del otro portal.
// - Sonidos (por el grupo de efectos del mezclador, así respetan el volumen de Opciones): cada portal zumba todo el
//   tiempo (sonidoLoop, en 3D: se escucha al acercarse) y al pasar suena sonidoPasar en los dos portales. Si no se
//   ponen, se usan los de Sonidos/Resources/Mapas (portal_loop y portal_tp).
// Se arma desde el editor con Riftwalker > Mapas > Conectar portales (PortalesDelMapa).
[RequireComponent(typeof(BoxCollider))]
public class PortalDeTeletransporte : MonoBehaviour
{
    private const float Espera = 0.75f;                 // s sin volver a pasar
    private const float VolumenLoop = 0.55f, VolumenPaso = 0.9f;
    private const float LoopCerca = 2f, LoopLejos = 18f; // m: a todo volumen y hasta dónde se escucha
    private const float PasoCerca = 3f, PasoLejos = 30f;

    [Tooltip("Dónde sale el que entra por este portal: el punto de salida del otro portal (TP_portal).")]
    public Transform destino;
    [Tooltip("El portal por el que sale (para mirar hacia afuera de él). Si no está, sale mirando como el punto de salida.")]
    public Transform portalDeSalida;
    [Tooltip("Sonido al pasar, en los dos portales. Vacío: Sonidos/Resources/Mapas/portal_tp.")]
    public AudioClip sonidoPasar;
    [Tooltip("Zumbido del portal, todo el tiempo. Vacío: Sonidos/Resources/Mapas/portal_loop.")]
    public AudioClip sonidoLoop;

    /// <summary>El jugador de esta computadora pasó por un portal (jugador, desde dónde, adónde): para avisar a los demás.</summary>
    public static event System.Action<Transform, Vector3, Vector3> Paso;

    private static float pasoEn = -10f;
    private static AudioMixerGroup grupo;
    private static AudioClip pasarPorDefecto, loopPorDefecto;
    private AudioSource zumbido;

    private void Reset()
    {
        BoxCollider area = GetComponent<BoxCollider>();
        area.isTrigger = true;
        gameObject.layer = 2; // Ignore Raycast
    }

    private void Start()
    {
        AudioClip clip = sonidoLoop != null ? sonidoLoop : LoopPorDefecto();
        if (clip == null) return;
        // Desde el centro del remolino, un poco desfasado en cada portal para que no suenen idénticos.
        zumbido = gameObject.AddComponent<AudioSource>();
        zumbido.clip = clip;
        zumbido.loop = true;
        zumbido.playOnAwake = false;
        zumbido.spatialBlend = 1f;
        zumbido.rolloffMode = AudioRolloffMode.Linear;
        zumbido.minDistance = LoopCerca;
        zumbido.maxDistance = LoopLejos;
        zumbido.dopplerLevel = 0f;
        zumbido.volume = VolumenLoop;
        zumbido.time = Random.Range(0f, clip.length);
        zumbido.Play();
    }

    // El grupo de efectos se conoce cuando aparece el jugador (la pistola lo tiene): hasta entonces sale sin grupo.
    private void Update()
    {
        if (zumbido != null && zumbido.outputAudioMixerGroup == null && Grupo() != null) zumbido.outputAudioMixerGroup = Grupo();
    }

    private void OnTriggerEnter(Collider otro) => Pasar(otro);
    private void OnTriggerStay(Collider otro) => Pasar(otro); // si se quedó parado adentro durante la espera

    private void Pasar(Collider otro)
    {
        if (Time.time < pasoEn) pasoEn = -10f; // otra partida (el reloj volvió a empezar)
        if (destino == null || Time.time - pasoEn < Espera) return;
        CharacterController cuerpo = otro.GetComponentInParent<CharacterController>();
        if (cuerpo == null || cuerpo.GetComponent<PlayerMovement>() == null) return; // solo jugadores que se manejan acá
        PhotonView vista = cuerpo.GetComponent<PhotonView>();
        if (vista != null && PhotonNetwork.InRoom && !vista.IsMine) return;
        HealthSystem vida = cuerpo.GetComponent<HealthSystem>();
        if (vida != null && vida.currentHealth <= 0) return;

        pasoEn = Time.time;
        Vector3 desde = cuerpo.transform.position;
        // Sale mirando hacia afuera del portal de salida (del portal hacia el punto), derecho.
        Vector3 afuera = portalDeSalida != null ? destino.position - portalDeSalida.position : destino.forward;
        afuera.y = 0f;
        Quaternion giro = afuera.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(afuera.normalized, Vector3.up)
                                                      : Quaternion.Euler(0f, destino.eulerAngles.y, 0f);
        JugadorEnRed.Teletransportar(cuerpo.transform, destino.position, giro);
        SonarPaso(desde, destino.position, true, sonidoPasar);
        Paso?.Invoke(cuerpo.transform, desde, destino.position);
    }

    /// <summary>
    /// El sonido de pasar, en el portal de entrada y en el de salida. propio: el que pasó (lo escucha fuerte y sin
    /// ubicar, en el de salida); los demás lo escuchan desde cada portal, más fuerte cuanto más cerca.
    /// </summary>
    public static void SonarPaso(Vector3 desde, Vector3 hasta, bool propio, AudioClip clip = null)
    {
        if (clip == null) clip = PasarPorDefecto();
        if (clip == null) return;
        if (propio) Sonar(clip, hasta, 0f);
        else
        {
            Sonar(clip, desde, 1f);
            Sonar(clip, hasta, 1f);
        }
    }

    private static void Sonar(AudioClip clip, Vector3 lugar, float espacial)
    {
        var go = new GameObject("Portal (sonido)");
        go.transform.position = lugar;
        AudioSource fuente = go.AddComponent<AudioSource>();
        fuente.clip = clip;
        fuente.playOnAwake = false;
        fuente.outputAudioMixerGroup = Grupo();
        fuente.spatialBlend = espacial;
        fuente.rolloffMode = AudioRolloffMode.Linear;
        fuente.minDistance = PasoCerca;
        fuente.maxDistance = PasoLejos;
        fuente.dopplerLevel = 0f;
        fuente.volume = VolumenPaso;
        fuente.Play();
        Destroy(go, clip.length + 0.1f);
    }

    private static AudioMixerGroup Grupo()
    {
        if (grupo == null)
        {
            Pistola pistola = FindFirstObjectByType<Pistola>(FindObjectsInactive.Include);
            if (pistola != null) grupo = pistola.sfxGroup;
        }
        return grupo;
    }

    private static AudioClip PasarPorDefecto()
    {
        if (pasarPorDefecto == null) pasarPorDefecto = Resources.Load<AudioClip>("Mapas/portal_tp");
        return pasarPorDefecto;
    }

    private static AudioClip LoopPorDefecto()
    {
        if (loopPorDefecto == null) loopPorDefecto = Resources.Load<AudioClip>("Mapas/portal_loop");
        return loopPorDefecto;
    }
}
