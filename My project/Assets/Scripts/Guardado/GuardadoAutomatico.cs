using UnityEngine;

// Escribe el guardado del jugador poco después de cada cambio y al cerrar el juego (F18, US 162, CA1). Lo crea Guardado
// antes de la primera escena y pasa de una escena a otra.
public sealed class GuardadoAutomatico : MonoBehaviour
{
    private void Update() => Guardado.Tick();

    private void OnApplicationPause(bool pausa)
    {
        if (pausa) Guardado.Save();
    }

    private void OnApplicationQuit() => Guardado.Save();
}
