using System.Collections.Generic;
using UnityEngine;

// F08 (Modo Invasión), US 187, CA2: un punto de aparición de enemigos en el mapa (los de los jugadores en las partidas
// online son PuntoDeAparicion). Se ponen en la escena y se mueven a
// mano (se ven como un círculo verde con una flecha en la vista de escena). Por ahora son provisorios: se crean con
// Riftwalker > Invasión > Crear 3 puntos de aparición, hasta definir bien dónde van.
// Las oleadas (OleadasDeInvasion) eligen entre los que están conectados con el jugador por el NavMesh.
public class AparicionDeEnemigos : MonoBehaviour
{
    private static readonly List<AparicionDeEnemigos> todos = new List<AparicionDeEnemigos>();

    /// <summary>Los puntos activos de la escena.</summary>
    public static IReadOnlyList<AparicionDeEnemigos> Todos => todos;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Limpiar() => todos.Clear();

    private void OnEnable() => todos.Add(this);
    private void OnDisable() => todos.Remove(this);

    private void OnDrawGizmos()
    {
        Vector3 p = transform.position;
        Gizmos.color = new Color(0.49f, 0.88f, 0.35f, 0.9f);
        const int lados = 24;
        for (int i = 0; i < lados; i++)
        {
            float a = i * Mathf.PI * 2f / lados, b = (i + 1) * Mathf.PI * 2f / lados;
            Gizmos.DrawLine(p + new Vector3(Mathf.Cos(a), 0.05f, Mathf.Sin(a)) * 0.8f, p + new Vector3(Mathf.Cos(b), 0.05f, Mathf.Sin(b)) * 0.8f);
        }
        Gizmos.DrawLine(p, p + Vector3.up * 2f);
        Gizmos.DrawLine(p + Vector3.up * 1f, p + Vector3.up * 1f + transform.forward * 1.2f);
    }
}
