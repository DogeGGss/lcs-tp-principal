using UnityEngine;

// Dedos de las manos agarrando esta arma en primera persona (US 244, se ajusta con "Ajustar a mano" de la US 173).
// La pose de las manos sale de la animación del cuerpo, igual para todas las armas: con un arma de empuñadura gruesa
// los dedos la atraviesan. Acá cada arma dice cuánto abrir los dedos de cada mano, en grados: positivo los abre
// (los estira, se separan del arma), negativo los cierra más. El índice va aparte (el del gatillo) y el pulgar también.
// BrazosEnCamara los aplica justo antes de sacar la foto de los brazos con esta arma, y en vivo mientras se ajusta a
// mano en Play; lo que se ajuste en Play queda guardado en el Player.prefab al salir (AjusteDeManos).
public class DedosDelArma : MonoBehaviour
{
    [Header("Mano derecha (la del gatillo)")]
    [Tooltip("Mayor, anular y meñique (grados). Positivo: más abiertos.")]
    [Range(-40f, 80f)] public float dedosDerecha;
    [Range(-40f, 80f)] public float indiceDerecho;
    [Range(-40f, 80f)] public float pulgarDerecho;

    [Header("Mano izquierda")]
    [Range(-40f, 80f)] public float dedosIzquierda;
    [Range(-40f, 80f)] public float indiceIzquierdo;
    [Range(-40f, 80f)] public float pulgarIzquierdo;

    // Cuánto del ángulo va a cada falange (de la base a la punta).
    private static readonly float[] Reparto = { 1f, 0.8f, 0.6f };

    /// <summary>Abre o cierra los dedos del esqueleto de ese animador. Hay que llamarlo después de la animación.</summary>
    public void Aplicar(Animator animador)
    {
        if (animador == null || !animador.isHuman) return;
        Mano(animador, true, dedosDerecha, indiceDerecho, pulgarDerecho);
        Mano(animador, false, dedosIzquierda, indiceIzquierdo, pulgarIzquierdo);
    }

    private static void Mano(Animator a, bool derecha, float dedos, float indice, float pulgar)
    {
        if (dedos == 0f && indice == 0f && pulgar == 0f) return;
        Transform mano = a.GetBoneTransform(derecha ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        Transform baseIndice = Hueso(a, derecha, "IndexProximal"), baseMenique = Hueso(a, derecha, "LittleProximal");
        Transform baseMayor = Hueso(a, derecha, "MiddleProximal");
        if (mano == null || baseIndice == null || baseMenique == null || baseMayor == null) return;

        // Eje de los nudillos (los dedos se doblan alrededor de él) y hacia dónde apunta la mano.
        Vector3 nudillos = (baseMenique.position - baseIndice.position).normalized;
        Vector3 adelante = (baseMayor.position - mano.position).normalized;

        Dedo(a, derecha, "Index", indice, nudillos, mano.position);
        Dedo(a, derecha, "Middle", dedos, nudillos, mano.position);
        Dedo(a, derecha, "Ring", dedos, nudillos, mano.position);
        Dedo(a, derecha, "Little", dedos, nudillos, mano.position);
        // El pulgar se abre girando alrededor de lo largo de la mano: se separa del índice.
        Dedo(a, derecha, "Thumb", pulgar, adelante, baseIndice.position);
    }

    // Gira las tres falanges del dedo. El sentido es el que aleja la punta de "desde" (la muñeca, o el índice para el
    // pulgar): así "abrir" es abrir con cualquier esqueleto, sin saber para dónde apuntan sus ejes.
    private static void Dedo(Animator a, bool derecha, string dedo, float grados, Vector3 eje, Vector3 desde)
    {
        if (grados == 0f) return;
        Transform[] falanges =
        {
            Hueso(a, derecha, dedo + "Proximal"), Hueso(a, derecha, dedo + "Intermediate"), Hueso(a, derecha, dedo + "Distal"),
        };
        if (falanges[0] == null) return;
        float signo = Sentido(falanges, eje, desde);
        for (int i = 0; i < falanges.Length; i++)
        {
            if (falanges[i] == null) break;
            falanges[i].rotation = Quaternion.AngleAxis(grados * Reparto[i] * signo, eje) * falanges[i].rotation;
        }
    }

    private static float Sentido(Transform[] f, Vector3 eje, Vector3 desde)
    {
        Vector3 inicio = f[0].position;
        Vector3 punta = Punta(f);
        Quaternion mas = Quaternion.AngleAxis(10f, eje);
        float conMas = (inicio + mas * (punta - inicio) - desde).sqrMagnitude;
        float conMenos = (inicio + Quaternion.Inverse(mas) * (punta - inicio) - desde).sqrMagnitude;
        return conMas >= conMenos ? 1f : -1f;
    }

    // La punta del dedo: un poco más allá de la última falange.
    private static Vector3 Punta(Transform[] f)
    {
        Transform ultima = f[0], anterior = null;
        for (int i = 1; i < f.Length && f[i] != null; i++) { anterior = ultima; ultima = f[i]; }
        if (anterior == null) return ultima.position + (ultima.childCount > 0 ? ultima.GetChild(0).position - ultima.position : Vector3.zero);
        return ultima.position + (ultima.position - anterior.position) * 0.8f;
    }

    private static Transform Hueso(Animator a, bool derecha, string nombre)
    {
        if (!System.Enum.TryParse((derecha ? "Right" : "Left") + nombre, out HumanBodyBones hueso)) return null;
        return a.GetBoneTransform(hueso);
    }

    public bool TieneAjuste =>
        dedosDerecha != 0f || indiceDerecho != 0f || pulgarDerecho != 0f ||
        dedosIzquierda != 0f || indiceIzquierdo != 0f || pulgarIzquierdo != 0f;
}
