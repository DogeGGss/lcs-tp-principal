using UnityEngine;
using UnityEngine.EventSystems;

// Para las zonas clickeables del menú (filas, tarjetas, la credencial): suenan al pasar el mouse.
// Si además tienen ButtonHoverAnimation, suena una sola vez igual.
public class SonidoAlPasar : MonoBehaviour, IPointerEnterHandler
{
    public void OnPointerEnter(PointerEventData eventData) => SonidoInterfaz.Pasar();
}
