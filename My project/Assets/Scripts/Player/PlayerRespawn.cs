using UnityEngine;

public class PlayerRespawn : MonoBehaviour
{

    public Transform spawnPoint;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            CharacterController controller = other.GetComponent<CharacterController>();

            if (controller != null)
            {
                controller.enabled = false;
            }

            //Mueve al jugador al punto de respawn
            other.transform.position = spawnPoint.position;
            other.transform.rotation = spawnPoint.rotation;

            if(controller != null)
            {
                controller.enabled = true;
            }
        }
    }
}
