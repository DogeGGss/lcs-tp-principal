using UnityEngine;

// Lo común de lo que tienen en la mano los demás jugadores sin un arma con ajuste, movido por código (F26): la granada
// (LanzamientoDeGranada, US 259) y el cuchillo (CuchilloEnTerceraPersona, US 257). Las manos van con el IK de los brazos
// de ArmaEnTerceraPersona a puntos del ajuste (AgarresTerceraPersona), respecto del pecho y de hacia dónde mira:
// - el marco gira con el cuerpo y sigue solo una parte de la mira arriba o abajo (granadaSigueMira);
// - las manos nunca entran en el cuerpo (granadaLejosDelCuerpo) y los codos apuntan afuera y atrás (granadaCodo);
// - cada punto tiene un giro de la muñeca, que se aplica después del IK sobre la muñeca recta (la pose de base del
//   avatar), así lo que va en la mano gira con ella (Munecas).
// Cada uno dice si está prendido y dónde van las manos en cada momento (Pose).
[DefaultExecutionOrder(45)] // después de que se mueven el cuerpo y los ojos, antes de ArmaEnTerceraPersona
public abstract class ManosPorCodigo : MonoBehaviour
{
    private const float Entrar = 0.15f, Salir = 0.25f;

    protected ArmaEnTerceraPersona arma;
    private float peso, pesoVisto;
    private Quaternion giroDer = Quaternion.identity, giroIzq = Quaternion.identity; // en el marco (hacia dónde mira)
    private int munecasEn = -1;

    /// <summary>Muñeco espejo: queda quieto en ese momento del movimiento (0 a 1); -1, normal.</summary>
    public float Probar { get; set; } = -1f;

    protected static T Crear<T>(GameObject personaje, ArmaEnTerceraPersona arma) where T : ManosPorCodigo
    {
        if (personaje == null || arma == null) return null;
        T m = personaje.AddComponent<T>();
        m.arma = arma;
        return m;
    }

    /// <summary>Si tiene que mover las manos ahora (por ejemplo, tiene la granada en la mano o la está lanzando).</summary>
    protected abstract bool Prendida { get; }

    /// <summary>Dónde van las manos ahora, en el marco (metros), y el giro de cada muñeca.</summary>
    protected abstract void Pose(out Vector3 der, out Vector3 izq, out Quaternion giroDerecha, out Quaternion giroIzquierda);

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
        // Mientras guarda el arma anterior (la sigue manejando ArmaEnTerceraPersona), todavía no.
        bool prendida = !arma.Manejando && Prendida;
        peso = Mathf.MoveTowards(peso, prendida ? 1f : 0f, Time.deltaTime / (prendida ? Entrar : Salir));
        if (arma.Manejando) peso = 0f; // un arma con ajuste en la mano: las manos (y las muñecas) son de ella
        pesoVisto = Suave(peso);
        if (peso <= 0f || !Marco(out Vector3 origen, out Quaternion giro)) { arma.ManosDeGranada(0f, Vector3.zero, Vector3.zero); return; }

        AgarresTerceraPersona a = AgarresTerceraPersona.Actual;
        Pose(out Vector3 der, out Vector3 izq, out giroDer, out giroIzq);
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
    /// Gira las muñecas (una vez por cuadro, después de la animación y del IK). Lo llama también el que pone lo que va en
    /// la mano, antes de ponerlo, así gira con la mano.
    /// </summary>
    public void Munecas()
    {
        if (munecasEn == Time.frameCount) return;
        munecasEn = Time.frameCount;
        if (pesoVisto <= 0f || arma == null || arma.Animador == null || !Marco(out _, out Quaternion giro)) return;
        Girar(arma.Animador.GetBoneTransform(HumanBodyBones.RightHand), giro, giroDer);
        Girar(arma.Animador.GetBoneTransform(HumanBodyBones.LeftHand), giro, giroIzq);
    }

    // La muñeca, como en la pose de base del personaje (la T: la mano en línea con el antebrazo), y encima el giro
    // "enMarco" (respecto de hacia dónde mira), por el peso. La de la animación de base no sirve: es la de agarrar un
    // rifle, y con el brazo en otra posición queda doblada como rota.
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

    protected static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    protected static float Suave(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
