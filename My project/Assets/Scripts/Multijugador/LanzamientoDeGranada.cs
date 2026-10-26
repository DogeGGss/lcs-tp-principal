using UnityEngine;

// US 259: la granada que ven los demás jugadores, por código (como las recargas de ArmaEnTerceraPersona): las manos
// van con el IK de los brazos a puntos del ajuste (AgarresTerceraPersona, sección Granadas). Lo común (el marco, los
// codos, las muñecas) está en ManosPorCodigo.
// - CA1: con una granada en la mano (ConGranada), la derecha la sostiene adelante del pecho y la izquierda queda
//   adelante, relajada.
// - CA2: al lanzar (Lanzar, cuando el dueño hace clic: RpcLanzandoGranada) el brazo derecho va atrás, por arriba del
//   hombro, y vuelve en un arco hacia adelante y arriba, donde la suelta; la izquierda se estira adelante, apuntando.
//   Los tiempos son los de primera persona (GrenadeThrower): la suelta a los 0,34 s del clic, igual que el dueño, y la
//   granada de muestra que llega por la red sale en ese momento (FaltaParaSoltar). Después baja y saca la siguiente.
// Lo usan las copias de los jugadores remotos (JugadorEnRed) y el muñeco espejo (EspejoTerceraPersona), que puede
// dejarlo quieto en un momento del lanzamiento (Probar) para ajustar los puntos con manijas.
public class LanzamientoDeGranada : ManosPorCodigo
{
    private const float Atras = GrenadeThrower.Preparacion;              // 0,22 s: el brazo llega atrás
    private const float Suelta = GrenadeThrower.Suelta;                  // 0,34 s: la suelta adelante
    private const float Envion = Suelta + GrenadeThrower.Despues;        // 0,52 s: termina el envión
    private const float Vuelve = Envion + 0.25f;                         // 0,77 s: de nuevo con la siguiente

    /// <summary>Lo que dura todo el lanzamiento (s), hasta que vuelve a sostener la siguiente.</summary>
    public const float Dura = Vuelve;

    private float desde = -10f;

    /// <summary>Tiene una granada en la mano (lo pone el que maneja la copia).</summary>
    public bool ConGranada { get; set; }

    public static LanzamientoDeGranada Crear(GameObject personaje, ArmaEnTerceraPersona arma) =>
        Crear<LanzamientoDeGranada>(personaje, arma);

    private float Pasado => Probar >= 0f ? Probar * Dura : Time.time - desde;
    private bool Lanzando => Pasado < Dura;

    /// <summary>Lo que falta para que suelte la granada (0 si ya la soltó o no está lanzando).</summary>
    public float FaltaParaSoltar => Probar < 0f && Lanzando ? Mathf.Max(0f, Suelta - Pasado) : 0f;

    /// <summary>Ya la soltó y todavía no sacó la siguiente: la mano está vacía.</summary>
    public bool ManoVacia => Pasado >= Suelta && Pasado < Envion + 0.1f;

    /// <summary>Empieza a lanzar (el dueño hizo clic).</summary>
    public void Lanzar() => desde = Time.time;

    protected override bool Prendida => ConGranada || (Lanzando && Pasado < Envion);

    protected override void Pose(out Vector3 der, out Vector3 izq, out Quaternion dg, out Quaternion ig)
    {
        AgarresTerceraPersona a = AgarresTerceraPersona.Actual;
        float t = Pasado;
        der = a.granadaDerecha;
        izq = a.granadaIzquierda;
        dg = Quaternion.Euler(a.granadaGiroDerecha);
        ig = Quaternion.Euler(a.granadaGiroIzquierda);
        if (!Lanzando) return;
        Quaternion atrasG = Quaternion.Euler(a.granadaGiroAtras), sueltaG = Quaternion.Euler(a.granadaGiroSuelta);
        Quaternion envionG = Quaternion.Euler(a.granadaGiroEnvion), apuntaG = Quaternion.Euler(a.granadaGiroApunta);
        if (t < Atras)
        {
            // Lleva el brazo atrás, por arriba del hombro; la izquierda se estira adelante.
            float k = Suave(t / Atras);
            der = Vector3.Lerp(a.granadaDerecha, a.granadaAtras, k);
            dg = Quaternion.Slerp(dg, atrasG, k);
            izq = Vector3.Lerp(a.granadaIzquierda, a.granadaApunta, k);
            ig = Quaternion.Slerp(ig, apuntaG, k);
        }
        else if (t < Suelta)
        {
            // El arco hacia adelante y arriba, rápido al principio (como un latigazo).
            float k = 1f - Mathf.Pow(1f - (t - Atras) / (Suelta - Atras), 2f);
            Vector3 medio = (a.granadaAtras + a.granadaSuelta) * 0.5f + Vector3.up * a.granadaArco;
            der = Bezier(a.granadaAtras, medio, a.granadaSuelta, k);
            dg = Quaternion.Slerp(atrasG, sueltaG, k);
            izq = a.granadaApunta;
            ig = apuntaG;
        }
        else if (t < Envion)
        {
            // Sigue de largo hacia abajo y adelante.
            float k = Suave((t - Suelta) / (Envion - Suelta));
            der = Vector3.Lerp(a.granadaSuelta, a.granadaEnvion, k);
            dg = Quaternion.Slerp(sueltaG, envionG, k);
            izq = Vector3.Lerp(a.granadaApunta, a.granadaIzquierda, k);
            ig = Quaternion.Slerp(apuntaG, ig, k);
        }
        else
        {
            // Vuelve a sostener la siguiente.
            float k = Suave((t - Envion) / (Vuelve - Envion));
            der = Vector3.Lerp(a.granadaEnvion, a.granadaDerecha, k);
            dg = Quaternion.Slerp(envionG, dg, k);
        }
        // La izquierda se mueve menos: solo una parte del camino desde donde la sostiene.
        izq = Vector3.Lerp(a.granadaIzquierda, izq, a.granadaIzquierdaMueve);
        ig = Quaternion.Slerp(Quaternion.Euler(a.granadaGiroIzquierda), ig, a.granadaIzquierdaMueve);
    }
}
