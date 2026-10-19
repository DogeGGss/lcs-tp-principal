using System.Collections.Generic;
using UnityEngine;

// Pared invisible que frena solo a los jugadores (US 240, CA6). Va en las paredes invisibles de las vallas del borde
// del Mapa 1: están en la capa Ignore Raycast, así que los disparos ya no las miran, y con esto las granadas tampoco:
// pasan por encima de la valla y se pueden tirar fuera del mapa. Se puede poner en cualquier otra pared invisible.
[RequireComponent(typeof(Collider))]
public class ParedSoloJugadores : MonoBehaviour
{
    private static readonly List<Collider> paredes = new List<Collider>();

    /// <summary>Que ese objeto (una granada) no choque con ninguna pared de estas.</summary>
    public static void Atravesar(GameObject objeto)
    {
        if (objeto == null || paredes.Count == 0) return;
        Collider[] propios = objeto.GetComponentsInChildren<Collider>();
        foreach (Collider pared in paredes)
            if (pared != null)
                foreach (Collider propio in propios)
                    Physics.IgnoreCollision(propio, pared);
    }

    private void OnEnable()
    {
        foreach (Collider c in GetComponents<Collider>())
            if (!paredes.Contains(c)) paredes.Add(c);
    }

    private void OnDisable()
    {
        foreach (Collider c in GetComponents<Collider>()) paredes.Remove(c);
    }
}
