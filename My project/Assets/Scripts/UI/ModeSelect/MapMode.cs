using UnityEngine;

// Modo de juego de este mapa (va en la escena del mapa). Desde el menú el modo ya viene elegido, pero si la
// escena se abre directo (por ejemplo, con Play desde el editor) MatchSettings queda en Táctico y la pausa
// muestra el modo equivocado. Con esto el mapa deja el modo correcto antes de que lo lea nadie.
[DefaultExecutionOrder(-1000)]
public class MapMode : MonoBehaviour
{
    [SerializeField] private GameMode mode = GameMode.Zombie;

    private void Awake()
    {
        MatchSettings.Mode = mode;
    }
}
