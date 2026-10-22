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
// Las muñecas: cada punto tiene además un giro de la mano (los "granadaGiro..." del ajuste), que se aplica sobre la
// mano de la animación después del IK (Munecas), así la granada (que va respecto de la mano) gira con ella.
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
    private float peso, pesoVisto;
    private Quaternion giroDer = Quaternion.identity, giroIzq = Quaternion.identity; // en el marco (hacia dónde mira)
    private int munecasEn = -1;

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
        AgarresTerceraPersona a = AgarresTerceraPersona.Actual;
        // Gira con el cuerpo (a los costados) y sigue solo una parte de la mira arriba o abajo: con las manos abajo del
        // pecho, girar todo con la mira las llevaba al revés (mirando arriba, las manos se iban adelante y abajo).
        Vector3 adelante = Vector3.ProjectOnPlane(arma.Ojos.forward, transform.up);
        Quaternion cuerpo = adelante.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(adelante, transform.up) : transform.rotation;
        giro = Quaternion.Slerp(cuerpo, arma.Ojos.rotation, a.granadaSigueMira);
        origen = arma.Ojos.position - transform.up * a.granadaAlturaPecho;
        return true;
    }

    private void Update()
    {
        if (arma == null) return;
        float t = Pasado;
        bool lanzando = t < Dura;
        // Mientras guarda el arma anterior (la sigue manejando ArmaEnTerceraPersona), todavía no.
        bool prendida = !arma.Manejando && (ConGranada || (lanzando && t < Envion));
        peso = Mathf.MoveTowards(peso, prendida ? 1f : 0f, Time.deltaTime / (prendida ? Entrar : Salir));
        if (arma.Manejando) peso = 0f; // un arma con ajuste en la mano: las manos (y las muñecas) son de ella
        pesoVisto = Suave(peso);
        if (peso <= 0f || !Marco(out Vector3 origen, out Quaternion giro)) { arma.ManosDeGranada(0f, Vector3.zero, Vector3.zero); return; }

        AgarresTerceraPersona a = AgarresTerceraPersona.Actual;
        Vector3 der = a.granadaDerecha, izq = a.granadaIzquierda;
        Quaternion dg = Quaternion.Euler(a.granadaGiroDerecha), ig = Quaternion.Euler(a.granadaGiroIzquierda);
        Quaternion atrasG = Quaternion.Euler(a.granadaGiroAtras), sueltaG = Quaternion.Euler(a.granadaGiroSuelta);
        Quaternion envionG = Quaternion.Euler(a.granadaGiroEnvion), apuntaG = Quaternion.Euler(a.granadaGiroApunta);
        if (lanzando)
        {
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
        giroDer = dg;
        giroIzq = ig;
        arma.ManosDeGranada(pesoVisto, FueraDelCuerpo(origen + giro * der, origen, a), FueraDelCuerpo(origen + giro * izq, origen, a),
            Codo(HumanBodyBones.RightUpperArm, 1f, a), Codo(HumanBodyBones.LeftUpperArm, -1f, a));
    }

    // Una mano nunca adentro del cuerpo: si quedó más cerca del eje del cuerpo (la vertical del pecho) que
    // granadaLejosDelCuerpo, sale hacia afuera (de costado o adelante, por donde esté más cerca).
    private Vector3 FueraDelCuerpo(Vector3 mano, Vector3 pecho, AgarresTerceraPersona a)
    {
        Vector3 desdeEje = Vector3.ProjectOnPlane(mano - pecho, transform.up);
        float lejos = desdeEje.magnitude;
        if (lejos >= a.granadaLejosDelCuerpo) return mano;
        Vector3 hacia = lejos > 1e-3f ? desdeEje / lejos : transform.forward;
        return mano + hacia * (a.granadaLejosDelCuerpo - lejos);
    }

    // Adónde apunta el codo: respecto del hombro, hacia afuera, abajo y atrás (lado: 1 derecho, -1 izquierdo).
    private Vector3 Codo(HumanBodyBones hombro, float lado, AgarresTerceraPersona a)
    {
        Transform hueso = arma.Animador != null ? arma.Animador.GetBoneTransform(hombro) : null;
        Vector3 desde = hueso != null ? hueso.position : transform.position + transform.up * 1.4f;
        Vector3 c = a.granadaCodo;
        return desde + transform.right * (c.x * lado) + transform.up * c.y + transform.forward * c.z;
    }

    private void LateUpdate() => Munecas();

    /// <summary>
    /// Gira las muñecas (una vez por cuadro, después de la animación y del IK). Lo llama también el que pone la granada
    /// en la mano, antes de ponerla, así la granada gira con la mano.
    /// </summary>
    public void Munecas()
    {
        if (munecasEn == Time.frameCount) return;
        munecasEn = Time.frameCount;
        if (pesoVisto <= 0f || arma == null || arma.Animador == null || !Marco(out _, out Quaternion giro)) return;
        Girar(arma.Animador.GetBoneTransform(HumanBodyBones.RightHand), giro, giroDer);
        Girar(arma.Animador.GetBoneTransform(HumanBodyBones.LeftHand), giro, giroIzq);
    }

    // La muñeca derecha, como en la pose de base del personaje (la T: la mano en línea con el antebrazo), y encima el
    // giro "enMarco" (respecto de hacia dónde mira), por el peso. La de la animación de base no sirve: es la de agarrar
    // un rifle, y con el brazo en otra posición queda doblada como rota.
    private void Girar(Transform mano, Quaternion marco, Quaternion enMarco)
    {
        if (mano == null) return;
        if (Reposo(mano, out Quaternion reposo))
            mano.localRotation = Quaternion.Slerp(mano.localRotation, reposo, pesoVisto);
        Quaternion mundo = marco * enMarco * Quaternion.Inverse(marco);
        mano.rotation = Quaternion.Slerp(Quaternion.identity, mundo, pesoVisto) * mano.rotation;
    }

    // El giro de ese hueso en la pose de base del avatar (cambia si cambia el cuerpo del personaje).
    private Avatar avatarVisto;
    private readonly System.Collections.Generic.Dictionary<string, Quaternion> reposos =
        new System.Collections.Generic.Dictionary<string, Quaternion>();

    private bool Reposo(Transform hueso, out Quaternion giro)
    {
        Avatar avatar = arma.Animador.avatar;
        if (avatar != avatarVisto)
        {
            avatarVisto = avatar;
            reposos.Clear();
            if (avatar != null && avatar.isHuman)
                foreach (SkeletonBone b in avatar.humanDescription.skeleton)
                    if (!string.IsNullOrEmpty(b.name)) reposos[b.name] = b.rotation;
        }
        return reposos.TryGetValue(hueso.name, out giro);
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
