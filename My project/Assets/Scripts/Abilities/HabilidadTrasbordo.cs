using UnityEngine;

// US 019 · Trasbordo: el jugador desaparece y aparece 7 m más allá, hacia donde camina (o hacia adelante si está quieto).
// Es instantáneo: en un mismo cuadro se lo mueve de a tramos cortos con el CharacterController, así sube escalones y
// rampas como caminando (US 008), no atraviesa paredes, puertas, muros de la base ni jugadores (CA3), y si pasa por
// encima de un vacío sigue derecho y después cae (CA4, US 007). La cámara no se toca: puede disparar enseguida (CA2).
[RequireComponent(typeof(CharacterController))]
public class HabilidadTrasbordo : EfectoHabilidad
{
    public const float Distancia = 7f;   // CA1
    public const float Minimo = 1f;      // CA3: con un obstáculo más cerca, no se hace ni se gasta la carga
    private const float Tramo = 0.5f;

    /// <summary>US 019, CA9: el jugador que lo usó, de dónde salió y a dónde llegó (lo manda a los demás JugadorEnRed).</summary>
    public static event System.Action<Transform, Vector3, Vector3> Usado;

    private CharacterController cuerpo;
    private PlayerMovement movimiento;

    private void Awake()
    {
        cuerpo = GetComponent<CharacterController>();
        movimiento = GetComponent<PlayerMovement>();
    }

    public override bool Aplicar()
    {
        if (cuerpo == null || !cuerpo.enabled) return false;

        Vector3 direccion = Direccion();
        Vector3 origen = transform.position;
        bool apoyado = cuerpo.isGrounded;
        float recorrido = 0f;

        while (recorrido < Distancia - 0.001f)
        {
            float tramo = Mathf.Min(Tramo, Distancia - recorrido);
            Vector3 antes = transform.position;
            cuerpo.Move(direccion * tramo);
            // Bajando una rampa o un escalón se lo vuelve a apoyar; si abajo no hay piso (un vacío), sigue derecho.
            if (apoyado && movimiento != null) apoyado = movimiento.PegarAlPiso(tramo);

            Vector3 avance = transform.position - antes;
            float adelante = Vector3.Dot(new Vector3(avance.x, 0f, avance.z), direccion);
            recorrido += Mathf.Max(0f, adelante);
            // Chocó (una pared, una puerta, otro jugador): se queda justo antes.
            if (adelante < tramo * 0.5f) break;
        }

        Vector3 llegada = transform.position;
        Vector3 plano = llegada - origen;
        plano.y = 0f;
        if (plano.magnitude < Minimo)
        {
            // CA3: el obstáculo estaba a menos de 1 m. Vuelve exactamente a donde estaba y la carga no se gasta.
            cuerpo.enabled = false;
            transform.position = origen;
            cuerpo.enabled = true;
            Physics.SyncTransforms();
            return false;
        }

        EfectosTrasbordo.Reproducir(origen, llegada, transform, true);
        Usado?.Invoke(transform, origen, llegada);
        return true;
    }

    // CA1: hacia donde aprieta (WASD, relativo a hacia dónde mira) o, si no aprieta nada, hacia adelante. Siempre horizontal.
    private Vector3 Direccion()
    {
        float x = KeyBindings.Axis(GameAction.Izquierda, GameAction.Derecha);
        float z = KeyBindings.Axis(GameAction.Atras, GameAction.Adelante);
        Vector3 adelante = transform.forward; adelante.y = 0f;
        Vector3 derecha = transform.right; derecha.y = 0f;
        Vector3 direccion = derecha.normalized * x + adelante.normalized * z;
        if (direccion.sqrMagnitude < 0.01f) direccion = adelante;
        return direccion.sqrMagnitude < 1e-6f ? Vector3.forward : direccion.normalized;
    }
}
