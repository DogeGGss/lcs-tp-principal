using UnityEngine;
using UnityEngine.EventSystems;

// Mouse sobre una fila o un botón de la tienda: pasar por encima, clic y clic derecho (vender).
public class ShopPointerTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public System.Action Hovered;
    public System.Action Exited;
    public System.Action Clicked;
    public System.Action RightClicked;

    public void OnPointerEnter(PointerEventData eventData) => Hovered?.Invoke();

    public void OnPointerExit(PointerEventData eventData) => Exited?.Invoke();

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) Clicked?.Invoke();
        else if (eventData.button == PointerEventData.InputButton.Right) RightClicked?.Invoke();
    }
}
