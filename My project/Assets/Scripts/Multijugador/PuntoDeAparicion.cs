using UnityEngine;

// Lugar donde puede aparecer o reaparecer un jugador en una partida online (US 030, CA5). Se pone en el mapa a la
// altura del centro del jugador, mirando hacia donde tiene que arrancar. Si el mapa no tiene al menos dos,
// PartidaEnRed busca lugares libres alrededor del punto de inicio del jugador.
public class PuntoDeAparicion : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.95f, 0.6f, 0.22f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.2f);
    }
}
