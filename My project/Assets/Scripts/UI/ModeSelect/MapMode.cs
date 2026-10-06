using UnityEngine;

// Modo de juego de este mapa (va en la escena del mapa). Desde el menú el modo ya viene elegido, pero si la
// escena se abre directo (por ejemplo, con Play desde el editor) MatchSettings queda en Táctico y la pausa
// muestra el modo equivocado. Con esto el mapa deja el modo correcto antes de que lo lea nadie.
[DefaultExecutionOrder(-1000)]
public class MapMode : MonoBehaviour
{
    [SerializeField] private GameMode mode = GameMode.Zombie;

    /// <summary>El modo del mapa cargado, o null si la escena no tiene MapMode (por ejemplo, una escena de prueba).</summary>
    public static GameMode? DeEstaEscena { get; private set; }

    private void Awake()
    {
        MatchSettings.Mode = mode;
        DeEstaEscena = mode;
    }

    private void OnDestroy()
    {
        if (DeEstaEscena == mode) DeEstaEscena = null;
    }
}
