using UnityEngine;

// Una manija del muñeco espejo (EspejoTerceraPersona) para ajustar a mano, en la vista Scene, el arma o una mano en
// tercera persona: se dibuja como una bolita con sus ejes y se mueve, gira (y, la del arma, se escala) con las
// herramientas de siempre de Unity (W, E, R).
public class ManijaDeAjuste : MonoBehaviour
{
    public Color color = Color.yellow;
    public float tamano = 0.04f;

    private void OnDrawGizmos()
    {
        Gizmos.color = color;
        Gizmos.DrawWireSphere(transform.position, tamano);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + transform.right * tamano * 2.5f);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + transform.up * tamano * 2.5f);
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * tamano * 2.5f);
    }
}
