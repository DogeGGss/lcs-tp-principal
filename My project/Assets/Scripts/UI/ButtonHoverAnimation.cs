using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonHoverAnimation : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [SerializeField] private float escalaNormal = 1f;
    [SerializeField] private float escalaHover = 1.03f;
    [SerializeField] private float escalaPresionado = 0.98f;
    [SerializeField] private float velocidad = 12f;

    private RectTransform rectTransform;
    private Vector3 escalaObjetivo;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        escalaObjetivo = Vector3.one * escalaNormal;
    }

    private void Update()
    {
        rectTransform.localScale = Vector3.Lerp(
            rectTransform.localScale,
            escalaObjetivo,
            Time.unscaledDeltaTime * velocidad
        );
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        escalaObjetivo = Vector3.one * escalaHover;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        escalaObjetivo = Vector3.one * escalaNormal;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        escalaObjetivo = Vector3.one * escalaPresionado;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        escalaObjetivo = Vector3.one * escalaHover;
    }
}