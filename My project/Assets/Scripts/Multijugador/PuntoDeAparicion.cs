using UnityEngine;

// Lugar donde puede aparecer o reaparecer un jugador en una partida online (US 030, CA5). Se pone en el mapa a la
// altura del centro del jugador, mirando hacia donde tiene que arrancar. Si el mapa no tiene al menos dos,
// PartidaEnRed busca lugares libres alrededor del punto de inicio del jugador.
// Táctico (US 031, CA4): "lado" dice en la base de qué equipo está el punto. Atacante = donde aparecen los
// atacantes, Defensor = donde aparecen los defensores (en la ronda 7 los equipos cambian de base porque
// intercambian los lados). Cualquiera = no es de ningún lado (Deathmatch). El punto tiene que quedar
// dentro de la zona de compra (BuyZone) del mismo lado.
public class PuntoDeAparicion : MonoBehaviour
{
    public LadoTactico lado = LadoTactico.Cualquiera;

    private void OnDrawGizmos()
    {
        Gizmos.color = lado == LadoTactico.Atacante ? new Color(1f, 0.35f, 0.3f, 0.9f)
            : lado == LadoTactico.Defensor ? new Color(0.3f, 0.65f, 1f, 0.9f)
            : new Color(0.95f, 0.6f, 0.22f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.2f);
    }
}
