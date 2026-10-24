using Photon.Pun;
using UnityEngine;

// Mapa 1: los portales (The Portal Collection). El que entra por el remolino de un portal sale por el otro, en su
// punto de salida (los objetos TP_portal de la escena), mirando hacia afuera del portal de salida.
// - Va en cada portal, con un trigger que tapa el remolino (en la capa Ignore Raycast: las balas lo atraviesan).
// - Lo hace el jugador de esta computadora (el dueño de su cuerpo): su posición nueva llega sola a los demás, que lo
//   ven saltar de un portal al otro (JugadorEnRed acomoda de golpe los saltos grandes).
// - Después de pasar, un rato sin volver a pasar, por si sale encima del otro portal.
// Se arma desde el editor con Riftwalker > Mapas > Conectar portales (PortalesDelMapa).
[RequireComponent(typeof(BoxCollider))]
public class PortalDeTeletransporte : MonoBehaviour
{
    private const float Espera = 0.75f; // s sin volver a pasar

    [Tooltip("Dónde sale el que entra por este portal: el punto de salida del otro portal (TP_portal).")]
    public Transform destino;
    [Tooltip("El portal por el que sale (para mirar hacia afuera de él). Si no está, sale mirando como el punto de salida.")]
    public Transform portalDeSalida;
    [Tooltip("Sonido al pasar (opcional).")]
    public AudioClip sonido;

    private static float pasoEn = -10f;

    private void Reset()
    {
        BoxCollider area = GetComponent<BoxCollider>();
        area.isTrigger = true;
        gameObject.layer = 2; // Ignore Raycast
    }

    private void OnTriggerEnter(Collider otro) => Pasar(otro);
    private void OnTriggerStay(Collider otro) => Pasar(otro); // si se quedó parado adentro durante la espera

    private void Pasar(Collider otro)
    {
        if (destino == null || Time.time - pasoEn < Espera) return;
        CharacterController cuerpo = otro.GetComponentInParent<CharacterController>();
        if (cuerpo == null || cuerpo.GetComponent<PlayerMovement>() == null) return; // solo jugadores que se manejan acá
        PhotonView vista = cuerpo.GetComponent<PhotonView>();
        if (vista != null && PhotonNetwork.InRoom && !vista.IsMine) return;
        HealthSystem vida = cuerpo.GetComponent<HealthSystem>();
        if (vida != null && vida.currentHealth <= 0) return;

        pasoEn = Time.time;
        // Sale mirando hacia afuera del portal de salida (del portal hacia el punto), derecho.
        Vector3 afuera = portalDeSalida != null ? destino.position - portalDeSalida.position : destino.forward;
        afuera.y = 0f;
        Quaternion giro = afuera.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(afuera.normalized, Vector3.up)
                                                      : Quaternion.Euler(0f, destino.eulerAngles.y, 0f);
        JugadorEnRed.Teletransportar(cuerpo.transform, destino.position, giro);
        if (sonido != null) AudioSource.PlayClipAtPoint(sonido, destino.position);
    }
}
