using UnityEngine;

// Dedos de las manos agarrando esta arma en primera persona (US 244, se ajusta con "Ajustar a mano" de la US 173).
// La pose de las manos sale de la animación del cuerpo, igual para todas las armas: con un arma de empuñadura gruesa
// los dedos la atraviesan. Acá cada arma ajusta cada dedo de cada mano, en grados:
// - Abrir: positivo lo estira (se separa del arma), negativo lo cierra más. Se reparte entre las tres falanges.
// - Separar: lo abre hacia el costado, alejándolo del dedo mayor (el mayor, hacia el índice; el pulgar, del índice).
// - Girar: lo gira sobre su propio largo.
// BrazosEnCamara los aplica justo antes de sacar la foto de los brazos con esta arma, y en vivo mientras se ajusta a
// mano en Play; lo que se ajuste en Play queda guardado en el Player.prefab al salir (AjusteDeManos).
public class DedosDelArma : MonoBehaviour
{
    [System.Serializable]
    public class Dedo
    {
        [Tooltip("Positivo: más abierto (estirado). Negativo: más cerrado.")]
        [Range(-60f, 90f)] public float abrir;
        [Tooltip("Positivo: se separa hacia el costado, lejos del dedo mayor.")]
        [Range(-45f, 45f)] public float separar;
        [Tooltip("Gira el dedo sobre su propio largo.")]
        [Range(-60f, 60f)] public float girar;

        public bool Tocado => abrir != 0f || separar != 0f || girar != 0f;
    }

    [System.Serializable]
    public class Mano
    {
        public Dedo pulgar = new Dedo(), indice = new Dedo(), mayor = new Dedo(), anular = new Dedo(), menique = new Dedo();
        public bool Tocada => pulgar.Tocado || indice.Tocado || mayor.Tocado || anular.Tocado || menique.Tocado;
    }

    [Tooltip("La del gatillo.")]
    public Mano manoDerecha = new Mano();
    public Mano manoIzquierda = new Mano();

    // Ajustes de antes (un valor para mayor, anular y meñique juntos, el índice y el pulgar): se pasan a los dedos.
    [SerializeField, HideInInspector] private float dedosDerecha, indiceDerecho, pulgarDerecho;
    [SerializeField, HideInInspector] private float dedosIzquierda, indiceIzquierdo, pulgarIzquierdo;

    // Cuánto de "abrir" va a cada falange (de la base a la punta).
    private static readonly float[] Reparto = { 1f, 0.8f, 0.6f };

    private void OnValidate() => PasarAjusteViejo();
    private void Awake() => PasarAjusteViejo();

    private void PasarAjusteViejo()
    {
        if (dedosDerecha == 0f && indiceDerecho == 0f && pulgarDerecho == 0f &&
            dedosIzquierda == 0f && indiceIzquierdo == 0f && pulgarIzquierdo == 0f) return;
        if (!manoDerecha.Tocada && !manoIzquierda.Tocada)
        {
            Pasar(manoDerecha, dedosDerecha, indiceDerecho, pulgarDerecho);
            Pasar(manoIzquierda, dedosIzquierda, indiceIzquierdo, pulgarIzquierdo);
        }
        dedosDerecha = indiceDerecho = pulgarDerecho = dedosIzquierda = indiceIzquierdo = pulgarIzquierdo = 0f;
    }

    private static void Pasar(Mano m, float dedos, float indice, float pulgar)
    {
        m.mayor.abrir = m.anular.abrir = m.menique.abrir = dedos;
        m.indice.abrir = indice;
        m.pulgar.abrir = pulgar;
    }

    public bool TieneAjuste => manoDerecha.Tocada || manoIzquierda.Tocada;

    /// <summary>Mueve los dedos del esqueleto de ese animador. Hay que llamarlo después de la animación.</summary>
    public void Aplicar(Animator animador)
    {
        if (animador == null || !animador.isHuman) return;
        PonerMano(animador, true, manoDerecha);
        PonerMano(animador, false, manoIzquierda);
    }

    private static void PonerMano(Animator a, bool derecha, Mano m)
    {
        if (m == null || !m.Tocada) return;
        Transform mano = a.GetBoneTransform(derecha ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        Transform baseIndice = Hueso(a, derecha, "IndexProximal"), baseMenique = Hueso(a, derecha, "LittleProximal");
        Transform baseMayor = Hueso(a, derecha, "MiddleProximal");
        if (mano == null || baseIndice == null || baseMenique == null || baseMayor == null) return;

        // Ejes de la mano, antes de mover nada: los nudillos (los dedos se doblan alrededor de ellos), hacia dónde
        // apunta la mano y la palma (los dedos se separan girando alrededor de ella).
        Vector3 nudillos = (baseMenique.position - baseIndice.position).normalized;
        Vector3 adelante = (baseMayor.position - mano.position).normalized;
        Vector3 palma = Vector3.Cross(nudillos, adelante).normalized;
        Vector3 puntaMayor = Punta(Falanges(a, derecha, "Middle"));
        Vector3 muneca = mano.position;

        PonerDedo(a, derecha, "Index", m.indice, nudillos, palma, muneca, puntaMayor);
        PonerDedo(a, derecha, "Middle", m.mayor, nudillos, palma, muneca, puntaMayor * 2f - Punta(Falanges(a, derecha, "Index")));
        PonerDedo(a, derecha, "Ring", m.anular, nudillos, palma, muneca, puntaMayor);
        PonerDedo(a, derecha, "Little", m.menique, nudillos, palma, muneca, puntaMayor);
        // El pulgar se abre girando alrededor de lo largo de la mano (se separa del índice) y se separa en la palma.
        PonerDedo(a, derecha, "Thumb", m.pulgar, adelante, palma, baseIndice.position, Punta(Falanges(a, derecha, "Index")));
    }

    // "lejosDe": el punto del que se aleja al abrir (la muñeca, o el índice para el pulgar). "separarDe": el punto del
    // que se aleja al separar (la punta del mayor). Así los signos valen con cualquier esqueleto y en las dos manos.
    private static void PonerDedo(Animator a, bool derecha, string dedo, Dedo d, Vector3 ejeAbrir, Vector3 ejeSeparar,
                                  Vector3 lejosDe, Vector3 separarDe)
    {
        if (d == null || !d.Tocado) return;
        Transform[] f = Falanges(a, derecha, dedo);
        if (f[0] == null) return;

        // Girar: sobre su propio largo, en el mismo sentido en las dos manos (como en un espejo).
        if (d.girar != 0f)
        {
            Vector3 largo = (Punta(f) - f[0].position).normalized;
            f[0].rotation = Quaternion.AngleAxis(d.girar * (derecha ? 1f : -1f), largo) * f[0].rotation;
        }
        // Separar: toda la base hacia el costado.
        if (d.separar != 0f)
            f[0].rotation = Quaternion.AngleAxis(d.separar * Sentido(f, ejeSeparar, separarDe), ejeSeparar) * f[0].rotation;
        // Abrir: repartido entre las falanges.
        if (d.abrir != 0f)
        {
            float signo = Sentido(f, ejeAbrir, lejosDe);
            for (int i = 0; i < f.Length && f[i] != null; i++)
                f[i].rotation = Quaternion.AngleAxis(d.abrir * Reparto[i] * signo, ejeAbrir) * f[i].rotation;
        }
    }

    // El sentido de giro que aleja la punta del dedo de "desde".
    private static float Sentido(Transform[] f, Vector3 eje, Vector3 desde)
    {
        Vector3 inicio = f[0].position;
        Vector3 punta = Punta(f);
        Quaternion mas = Quaternion.AngleAxis(10f, eje);
        float conMas = (inicio + mas * (punta - inicio) - desde).sqrMagnitude;
        float conMenos = (inicio + Quaternion.Inverse(mas) * (punta - inicio) - desde).sqrMagnitude;
        return conMas >= conMenos ? 1f : -1f;
    }

    private static Transform[] Falanges(Animator a, bool derecha, string dedo) => new[]
    {
        Hueso(a, derecha, dedo + "Proximal"), Hueso(a, derecha, dedo + "Intermediate"), Hueso(a, derecha, dedo + "Distal"),
    };

    // La punta del dedo: un poco más allá de la última falange.
    private static Vector3 Punta(Transform[] f)
    {
        if (f[0] == null) return Vector3.zero;
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
}
