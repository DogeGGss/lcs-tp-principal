using UnityEngine;

// Saca el movimiento del cuadro anterior antes que corra cualquier otro script, así el resto ve el arma en su lugar.
[DefaultExecutionOrder(-300)]
public class RestaurarPrimeraPersona : MonoBehaviour
{
    [HideInInspector] public AnimacionPrimeraPersona animacion;

    private void Update()
    {
        if (animacion != null) animacion.Restaurar();
    }
}
