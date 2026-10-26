using System.Collections.Generic;
using UnityEngine;

// Desde dónde se mira el minijuego de los cometas (F21, US 156): un punto de la base de cada equipo que mira hacia
// afuera de la estación. Los cometas pasan frente a él, a lo lejos (MinijuegoCometas). La flecha azul (adelante) tiene
// que apuntar al espacio abierto, y la altura del objeto es la del piso desde donde se mira.
public class MiradorCometas : MonoBehaviour
{
    private static readonly List<MiradorCometas> miradores = new List<MiradorCometas>();

    [Tooltip("De qué equipo es este mirador. Cualquiera: lo usan los dos.")]
    [SerializeField] private LadoTactico lado = LadoTactico.Cualquiera;

    private void OnEnable() => miradores.Add(this);
    private void OnDisable() => miradores.Remove(this);

    /// <summary>El mirador de un lado (si no hay, uno que sirva para todos).</summary>
    public static MiradorCometas De(LadoTactico lado)
    {
        MiradorCometas comun = null;
        foreach (MiradorCometas m in miradores)
        {
            if (m.lado == lado && lado != LadoTactico.Cualquiera) return m;
            if (m.lado == LadoTactico.Cualquiera && comun == null) comun = m;
        }
        return comun;
    }

    /// <summary>Hacia dónde se mira, sin inclinación.</summary>
    public Vector3 Frente
    {
        get
        {
            Vector3 f = transform.forward;
            f.y = 0f;
            return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
        }
    }

#if UNITY_EDITOR
    // En la escena: la dirección en la que pasan los cometas y la franja de distancia (150-300 m).
    private void OnDrawGizmos()
    {
        Vector3 f = Frente, o = transform.position;
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
        Gizmos.DrawLine(o, o + f * 150f);
        Gizmos.DrawWireSphere(o, 0.4f);
        Vector3 costado = Vector3.Cross(Vector3.up, f);
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.35f);
        Gizmos.DrawLine(o + f * 150f - costado * 60f, o + f * 150f + costado * 60f);
        Gizmos.DrawLine(o + f * 300f - costado * 120f, o + f * 300f + costado * 120f);
    }
#endif
}
