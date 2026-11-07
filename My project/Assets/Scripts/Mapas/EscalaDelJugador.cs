using UnityEngine;

// Tamaño del jugador en este mapa. El mapa de la uni está a escala real (puertas de 2,1 m) y el jugador mide 2 m con
// los ojos a 1,8 m, así que todo se veía chico. En vez de reescalar el mapa, en esa escena el jugador es más chico: se
// escala la raíz del jugador local y la de las copias de los demás (JugadorEnRed.CrearCopia). Con ella se achican la
// cápsula, la cámara, las armas en primera persona y el cuerpo. Las velocidades y el salto quedan en metros, como en
// los otros mapas. Va en la escena del mapa; sin este componente el jugador tiene el tamaño de siempre.
[DefaultExecutionOrder(-1000)]
public class EscalaDelJugador : MonoBehaviour
{
    [Tooltip("Tamaño del jugador en este mapa (1 = el de siempre: 2 m de alto, ojos a 1,8 m).")]
    [Range(0.5f, 1.5f)] public float escala = 0.85f;

    /// <summary>La escala del jugador en el mapa cargado (1 si la escena no tiene EscalaDelJugador).</summary>
    public static float Actual { get; private set; } = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Limpiar() => Actual = 1f;

    private void Awake()
    {
        Actual = escala;
        foreach (PlayerMovement jugador in FindObjectsByType<PlayerMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Aplicar(jugador.transform);
    }

    private void OnDestroy()
    {
        if (Mathf.Approximately(Actual, escala)) Actual = 1f;
    }

    /// <summary>Le da a la raíz de un jugador (el local o la copia de otro) el tamaño de este mapa.</summary>
    public static void Aplicar(Transform jugador)
    {
        if (jugador != null && !Mathf.Approximately(Actual, 1f)) jugador.localScale = Vector3.one * Actual;
    }
}
