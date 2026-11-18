using UnityEngine;

// US 257: el cuchillo que ven los demás jugadores, por código, como la granada (ManosPorCodigo): las manos van con el
// IK de los brazos a puntos del ajuste (AgarresTerceraPersona, sección Cuchillo).
// - CA1: con el cuchillo en la mano (ConCuchillo), la derecha lo tiene adelante, a la altura de la cintura, y la
//   izquierda queda adelante, en guardia.
// - CA2: con cada ataque (Atacar, cuando el dueño ataca: RpcCuchillo) la mano derecha va atrás y hacia adentro y cruza
//   en un corte hacia afuera, con los tiempos de primera persona (MeleeAttack: 0,12 s de preparación, 0,12 s de corte
//   y 0,3 s de vuelta). La izquierda acompaña un poco.
// Lo usan las copias de los jugadores remotos (JugadorEnRed) y el muñeco espejo (EspejoTerceraPersona), que puede
// dejarlo quieto en un momento del ataque (Probar) para ajustar los puntos con manijas.
public class CuchilloEnTerceraPersona : ManosPorCodigo
{
    private const float Atras = MeleeAttack.Preparacion;                 // 0,12 s: atrás y hacia adentro
    private const float Corte = Atras + MeleeAttack.Corte;               // 0,24 s: termina el corte, afuera
    private const float Vuelta = MeleeAttack.Total;                      // 0,54 s: de vuelta en guardia

    /// <summary>Lo que dura todo el ataque (s).</summary>
    public const float Dura = Vuelta;

    private float desde = -10f;

    /// <summary>Tiene el cuchillo en la mano (lo pone el que maneja la copia).</summary>
    public bool ConCuchillo { get; set; }

    /// <summary>US 395: la katana (en el lugar del cuchillo) y dónde está en la mano (el mango en el origen): la mano
    /// izquierda la agarra del mango, más abajo que la derecha. Los pone el que maneja la copia.</summary>
    public KatanaData Katana { get; set; }
    public Transform KatanaEnLaMano { get; set; }

    protected override bool IzquierdaEnElArma => Katana != null && KatanaEnLaMano != null;

    public static CuchilloEnTerceraPersona Crear(GameObject personaje, ArmaEnTerceraPersona arma) =>
        Crear<CuchilloEnTerceraPersona>(personaje, arma);

    private float Pasado => Probar >= 0f ? Probar * Dura : Time.time - desde;
    private bool Atacando => Pasado < Dura;

    /// <summary>Un ataque (el dueño atacó).</summary>
    public void Atacar() => desde = Time.time;

    protected override bool Prendida => ConCuchillo;

    protected override void Pose(out Vector3 der, out Vector3 izq, out Quaternion dg, out Quaternion ig)
    {
        AgarresTerceraPersona a = AgarresTerceraPersona.Actual;
        KatanaData k = Katana; // US 395: la katana tiene sus propios puntos (no toca los del cuchillo)
        Vector3 guardia = k != null ? k.manoDerecha : a.cuchilloDerecha;
        Vector3 atras = k != null ? k.manoAtras : a.cuchilloAtras;
        Vector3 corte = k != null ? k.manoCorte : a.cuchilloCorte;
        Quaternion guardiaG = Quaternion.Euler(k != null ? k.giroManoDerecha : a.cuchilloGiroDerecha);
        Quaternion atrasG = Quaternion.Euler(k != null ? k.giroManoAtras : a.cuchilloGiroAtras);
        Quaternion corteG = Quaternion.Euler(k != null ? k.giroManoCorte : a.cuchilloGiroCorte);
        float t = Pasado;
        der = guardia;
        izq = a.cuchilloIzquierda;
        dg = guardiaG;
        ig = Quaternion.Euler(a.cuchilloGiroIzquierda);
        if (!Atacando) { ManoIzquierdaEnLaKatana(ref der, dg, guardiaG, ref izq, ref ig); return; }
        float izqMueve;
        if (t < Atras)
        {
            float f = Suave(t / Atras);
            der = Vector3.Lerp(guardia, atras, f);
            dg = Quaternion.Slerp(guardiaG, atrasG, f);
            izqMueve = f;
        }
        else if (t < Corte)
        {
            // El corte: un arco de adentro hacia afuera, que pasa por adelante (rápido al principio).
            float f = 1f - Mathf.Pow(1f - (t - Atras) / (Corte - Atras), 2f);
            Vector3 medio = (atras + corte) * 0.5f + Vector3.forward * a.cuchilloArco;
            der = Bezier(atras, medio, corte, f);
            dg = Quaternion.Slerp(atrasG, corteG, f);
            izqMueve = 1f;
        }
        else
        {
            float f = Suave((t - Corte) / (Vuelta - Corte));
            der = Vector3.Lerp(corte, guardia, f);
            dg = Quaternion.Slerp(corteG, guardiaG, f);
            izqMueve = 1f - f;
        }
        // La izquierda se recoge un poco hacia el pecho mientras corta.
        izq = Vector3.Lerp(a.cuchilloIzquierda, a.cuchilloIzquierda + new Vector3(0.05f, 0.04f, -0.08f), izqMueve * a.granadaIzquierdaMueve);
        ManoIzquierdaEnLaKatana(ref der, dg, guardiaG, ref izq, ref ig);
    }

    // US 395: con la katana, la izquierda va al mango. La katana se pone después (con la mano derecha ya movida), así
    // que lo que se sabe es dónde quedó el cuadro anterior respecto del punto de la derecha de ese cuadro: se lleva eso
    // al punto de la derecha de ahora (si no, en el tajo, que es rápido, la izquierda queda atrás y se sale del mango).
    // La muñeca izquierda gira con la derecha. Y si el mango queda más lejos de lo que llega el brazo izquierdo (en el
    // corte, la derecha se va bien al costado), las dos manos se acercan a ese hombro lo que haga falta: si no, el brazo
    // izquierdo se estira todo lo que puede y la mano queda corrida, fuera del mango.
    private int cuadroAnterior = -10;
    private Vector3 derAnterior, origenAnterior;
    private Quaternion dgAnterior, giroAnterior;
    private Vector3 desdeSuave;
    private Quaternion giroDesdeSuave = Quaternion.identity;

    private void ManoIzquierdaEnLaKatana(ref Vector3 der, Quaternion dg, Quaternion guardiaG, ref Vector3 izq, ref Quaternion ig)
    {
        KatanaData k = Katana;
        if (k == null || KatanaEnLaMano == null || !KatanaEnLaMano.gameObject.activeInHierarchy || !Marco(out Vector3 origen, out Quaternion giro))
            return;
        bool seguido = cuadroAnterior == Time.frameCount - 1; // (si se cortó, desde donde está ahora)
        Quaternion inversa = Quaternion.Inverse(seguido ? giroAnterior : giro);
        Vector3 lugarAntes = inversa * (KatanaEnLaMano.position - (seguido ? origenAnterior : origen));
        Quaternion giroAntes = inversa * KatanaEnLaMano.rotation;
        Quaternion respecto = Quaternion.Inverse(seguido ? dgAnterior : dg);
        Vector3 desdeLaMano = respecto * (lugarAntes - (seguido ? derAnterior : der));
        Quaternion giroDesdeLaMano = respecto * giroAntes;
        // Suavizado: así no se arma un ida y vuelta entre la mano izquierda y la derecha (temblaba).
        float f = seguido ? 1f - Mathf.Exp(-Time.deltaTime / 0.12f) : 1f;
        desdeSuave = Vector3.Lerp(desdeSuave, desdeLaMano, f);
        giroDesdeSuave = Quaternion.Slerp(giroDesdeSuave, giroDesdeLaMano, f);
        Vector3 katanaLugar = der + dg * desdeSuave;
        Quaternion katanaGiro = dg * giroDesdeSuave;
        izq = katanaLugar + katanaGiro * Vector3.Scale(KatanaEnLaMano.lossyScale, k.manoIzquierdaEnElMango);
        if (AlcanceIzquierdo(origen, giro, out Vector3 hombro, out float largo))
        {
            Vector3 desdeElHombro = izq - hombro;
            if (desdeElHombro.magnitude > largo)
            {
                Vector3 corrido = desdeElHombro.normalized * largo - desdeElHombro;
                izq += corrido;
                der += corrido; // la katana va con la derecha: se corre igual
            }
        }
        cuadroAnterior = Time.frameCount;
        derAnterior = der;
        dgAnterior = dg;
        origenAnterior = origen;
        giroAnterior = giro;
        ig = dg * Quaternion.Inverse(guardiaG) * Quaternion.Euler(k.giroManoIzquierda);
    }

    // El hombro izquierdo (en el marco) y hasta dónde llega la muñeca desde él (un poco menos que el brazo estirado).
    private bool AlcanceIzquierdo(Vector3 origen, Quaternion giro, out Vector3 hombro, out float largo)
    {
        hombro = Vector3.zero;
        largo = 0f;
        Animator animador = arma != null ? arma.Animador : null;
        Transform h = animador != null ? animador.GetBoneTransform(HumanBodyBones.LeftUpperArm) : null;
        Transform c = animador != null ? animador.GetBoneTransform(HumanBodyBones.LeftLowerArm) : null;
        Transform m = animador != null ? animador.GetBoneTransform(HumanBodyBones.LeftHand) : null;
        if (h == null || c == null || m == null) return false;
        hombro = Quaternion.Inverse(giro) * (h.position - origen);
        largo = (Vector3.Distance(h.position, c.position) + Vector3.Distance(c.position, m.position)) * 0.95f;
        return largo > 0.05f;
    }
}
