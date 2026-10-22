using UnityEngine;

// US 259: la granada que ven los demás jugadores, por código (como las recargas de ArmaEnTerceraPersona): las manos
// van con el IK de los brazos a puntos del ajuste (AgarresTerceraPersona, sección Granadas), respecto del pecho y de
// hacia dónde mira (si mira arriba o abajo, los brazos lo siguen).
// - CA1: con una granada en la mano (ConGranada), la derecha la sostiene adelante del pecho y la izquierda queda
//   adelante, relajada.
// - CA2: al lanzar (Lanzar, cuando el dueño hace clic: RpcLanzandoGranada) el brazo derecho va atrás, por arriba del
//   hombro, y vuelve en un arco hacia adelante y arriba, donde la suelta; la izquierda se estira adelante, apuntando.
//   Los tiempos son los de primera persona (GrenadeThrower): la suelta a los 0,34 s del clic, igual que el dueño, y la
//   granada de muestra que llega por la red sale en ese momento (FaltaParaSoltar). Después baja y saca la siguiente.
// Lo usan las copias de los jugadores remotos (JugadorEnRed) y el muñeco espejo (EspejoTerceraPersona), que puede
// dejarlo quieto en un momento del lanzamiento (Probar) para ajustar los puntos con manijas.
[DefaultExecutionOrder(45)] // después de que se mueven el cuerpo y los ojos, antes de ArmaEnTerceraPersona
public class LanzamientoDeGranada : MonoBehaviour
{
    private const float Atras = GrenadeThrower.Preparacion;              // 0,22 s: el brazo llega atrás
    private const float Suelta = GrenadeThrower.Suelta;                  // 0,34 s: la suelta adelante
    private const float Envion = Suelta + GrenadeThrower.Despues;        // 0,52 s: termina el envión
    private const float Vuelve = Envion + 0.25f;                         // 0,77 s: de nuevo con la siguiente
    private const float Entrar = 0.15f, Salir = 0.25f;

    /// <summary>Lo que dura todo el lanzamiento (s), hasta que vuelve a sostener la siguiente.</summary>
    public const float Dura = Vuelve;

    private ArmaEnTerceraPersona arma;
    private float desde = -10f;
    private float peso;

    /// <summary>Tiene una granada en la mano (lo pone el que maneja la copia).</summary>
    public bool ConGranada { get; set; }

    /// <summary>Muñeco espejo: queda quieto en ese momento del lanzamiento (0 a 1); -1, normal.</summary>
    public float Probar { get; set; } = -1f;

    public static LanzamientoDeGranada Crear(GameObject personaje, ArmaEnTerceraPersona arma)
    {
        if (personaje == null || arma == null) return null;
        LanzamientoDeGranada l = personaje.AddComponent<LanzamientoDeGranada>();
        l.arma = arma;
        return l;
    }

    private float Pasado => Probar >= 0f ? Probar * Dura : Time.time - desde;
    private bool Lanzando => Pasado < Dura;

    /// <summary>Lo que falta para que suelte la granada (0 si ya la soltó o no está lanzando).</summary>
    public float FaltaParaSoltar => Probar < 0f && Lanzando ? Mathf.Max(0f, Suelta - Pasado) : 0f;

    /// <summary>Ya la soltó y todavía no sacó la siguiente: la mano está vacía.</summary>
    public bool ManoVacia => Pasado >= Suelta && Pasado < Envion + 0.1f;

    /// <summary>Empieza a lanzar (el dueño hizo clic).</summary>
    public void Lanzar() => desde = Time.time;

    /// <summary>El marco de los puntos del ajuste: el pecho (más abajo que los ojos) y hacia dónde mira.</summary>
    public bool Marco(out Vector3 origen, out Quaternion giro)
    {
        origen = Vector3.zero;
        giro = Quaternion.identity;
        if (arma == null || arma.Ojos == null) return false;
        giro = arma.Ojos.rotation;
        origen = arma.Ojos.position - transform.up * AgarresTerceraPersona.Actual.granadaAlturaPecho;
        return true;
    }

    private void Update()
    {
        if (arma == null) return;
        float t = Pasado;
        bool lanzando = t < Dura;
        bool prendida = ConGranada || (lanzando && t < Envion);
        peso = Mathf.MoveTowards(peso, prendida ? 1f : 0f, Time.deltaTime / (prendida ? Entrar : Salir));
        if (peso <= 0f || !Marco(out Vector3 origen, out Quaternion giro)) { arma.ManosDeGranada(0f, Vector3.zero, Vector3.zero); return; }

        AgarresTerceraPersona a = AgarresTerceraPersona.Actual;
        Vector3 der = a.granadaDerecha, izq = a.granadaIzquierda;
        if (lanzando)
        {
            if (t < Atras)
            {
                // Lleva el brazo atrás, por arriba del hombro; la izquierda se estira adelante.
                float k = Suave(t / Atras);
                der = Vector3.Lerp(a.granadaDerecha, a.granadaAtras, k);
                izq = Vector3.Lerp(a.granadaIzquierda, a.granadaApunta, k);
            }
            else if (t < Suelta)
            {
                // El arco hacia adelante y arriba, rápido al principio (como un latigazo).
                float k = 1f - Mathf.Pow(1f - (t - Atras) / (Suelta - Atras), 2f);
                Vector3 medio = (a.granadaAtras + a.granadaSuelta) * 0.5f + Vector3.up * a.granadaArco;
                der = Bezier(a.granadaAtras, medio, a.granadaSuelta, k);
                izq = a.granadaApunta;
            }
            else if (t < Envion)
            {
                // Sigue de largo hacia abajo y adelante.
                float k = Suave((t - Suelta) / (Envion - Suelta));
                der = Vector3.Lerp(a.granadaSuelta, a.granadaEnvion, k);
                izq = Vector3.Lerp(a.granadaApunta, a.granadaIzquierda, k);
            }
            else
            {
                // Vuelve a sostener la siguiente.
                float k = Suave((t - Envion) / (Vuelve - Envion));
                der = Vector3.Lerp(a.granadaEnvion, a.granadaDerecha, k);
            }
        }
        arma.ManosDeGranada(Suave(peso), origen + giro * der, origen + giro * izq);
    }

    private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    private static float Suave(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
