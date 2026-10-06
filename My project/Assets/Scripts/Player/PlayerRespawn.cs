using UnityEngine;

// Zona de fuera del mapa (US 006, CA1): se pone debajo de un vacío o en una zona no transitable. El jugador que entra
// se cayó del mapa: según el modo, cuenta como muerte o reaparece (FueraDelMapa). spawnPoint es donde reaparece en las
// escenas de prueba (si hay varios, se usa el más cercano).
public class PlayerRespawn : MonoBehaviour
{

    public Transform spawnPoint;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        FueraDelMapa fuera = other.GetComponent<FueraDelMapa>();
        if (fuera != null)
        {
            fuera.Caer(spawnPoint);
            return;
        }

        // Un jugador sin FueraDelMapa (no debería pasar: PlayerMovement lo agrega): se lo devuelve al punto.
        if (spawnPoint == null) return;
        CharacterController controller = other.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        other.transform.position = spawnPoint.position;
        other.transform.rotation = spawnPoint.rotation;
        if (controller != null) controller.enabled = true;
    }
}
