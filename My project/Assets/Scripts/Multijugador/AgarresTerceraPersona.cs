using System.Collections.Generic;
using UnityEngine;

// Cómo tiene cada arma el personaje que ven los demás jugadores (tercera persona, F26: US 243 y las que siguen).
// El arma a tamaño real (ModeloReal) va en un lugar fijo respecto del pecho del personaje (y gira alrededor del
// pecho con hacia dónde mira, así no se mete en el cuerpo al mirar abajo), y las manos la agarran con el IK de los brazos en los mismos agarres que en primera persona
// (Gun_ref_rightHand y Gun_ref_leftHand, los de "Ajustar a mano", US 173): si el agarre se ajusta en primera persona,
// se ve igual desde afuera. Acá se ajusta lo demás, por arma.
// Es un asset (Resources/AgarresTerceraPersona): lo que se cambie en el Inspector con el juego andando queda guardado.
// Para verlo sin otro jugador: Riftwalker > Muñeco espejo (tercera persona).
[CreateAssetMenu(menuName = "Riftwalker/Agarres en tercera persona")]
public class AgarresTerceraPersona : ScriptableObject
{
    public enum TipoDeRecarga
    {
        [Tooltip("Cambia el cargador: el arma se inclina y la mano izquierda va al cargador, baja a buscar otro y lo pone.")]
        Cargador,
        [Tooltip("Revólver (La Trochita, US 245): el arma sube de costado mostrando el tambor y la mano izquierda la suelta y baja; la sostiene la derecha.")]
        Tambor,
    }

    [System.Serializable]
    public class Agarre
    {
        [Tooltip("El nombre del arma de primera persona (el objeto hijo de la cámara del Player, por ejemplo Pistol_2).")]
        public string arma = "";
        [Tooltip("Solo para reconocerla en la lista.")]
        public string nombre = "";

        [Header("Dónde va el arma")]
        [Tooltip("Cuánto más abajo de los ojos está el pecho, el punto alrededor del que gira el arma al mirar arriba o abajo (metros).")]
        public float alturaPecho = 0.3f;
        [Tooltip("Respecto del pecho, mirando derecho (metros): x a la derecha, y arriba, z adelante.")]
        public Vector3 lugar = new Vector3(0.04f, 0.02f, 0.45f);
        [Tooltip("Lo mínimo que el arma queda adelante del pecho, mire donde mire (metros): así no se mete en el cuerpo.")]
        public float minimoAdelante = 0.3f;
        [Tooltip("Cuánto acompaña el arma el movimiento del cuerpo de la animación (respirar quieto, el balanceo al caminar): 0 queda fija, 1 se mueve con el pecho.")]
        [Range(0f, 1.5f)] public float seguirCuerpo = 1f;
        [Tooltip("Giro del arma respecto de hacia dónde mira (grados).")]
        public Vector3 giro = Vector3.zero;
        [Tooltip("Tamaño del arma (1 = el largo real de su ficha).")]
        [Range(0.5f, 1.5f)] public float escala = 1f;

        [Header("Manos (se suma al agarre de primera persona)")]
        [Tooltip("Corre la mano derecha, en el espacio del arma (metros: x derecha, y arriba, z hacia el caño).")]
        public Vector3 manoDerecha = Vector3.zero;
        public Vector3 giroManoDerecha = Vector3.zero;
        public Vector3 manoIzquierda = Vector3.zero;
        public Vector3 giroManoIzquierda = Vector3.zero;

        [Header("Recarga")]
        public TipoDeRecarga recarga = TipoDeRecarga.Cargador;
        [Tooltip("Tambor: cuánto sube el arma (metros).")]
        public float tamborArriba = 0.15f;
        [Tooltip("Tambor: giro del arma al subir (grados: x negativo levanta la punta, y la gira hacia el centro, z la inclina).")]
        public Vector3 tamborGiro = new Vector3(-50f, -50f, -5f);
        [Tooltip("Cargador: dónde va la mano izquierda a agarrar el cargador, respecto de la mano derecha, en el espacio del arma (metros: x derecha, y arriba, z hacia el caño). Con el muñeco espejo: \"Probar la recarga\" y la manija \"Cargador\".")]
        public Vector3 cargador = new Vector3(-0.03f, -0.12f, 0f);
        [Tooltip("Cargador: giro de la mano izquierda ahí, respecto de cómo agarra el arma (grados).")]
        public Vector3 giroCargador = Vector3.zero;
        [Tooltip("Cargador: al final la mano izquierda tira de la palanca de carga (fusiles, US 251 y 253).")]
        public bool palanca;
        [Tooltip("Dónde va la mano izquierda a agarrar la palanca de carga, respecto de la mano derecha, en el espacio del arma (metros). Manija \"Palanca\" del muñeco espejo.")]
        public Vector3 palancaLugar = new Vector3(-0.07f, 0.08f, 0f);
        [Tooltip("Giro de la mano izquierda en la palanca (grados).")]
        public Vector3 giroPalanca = Vector3.zero;

        [Header("Disparo")]
        [Tooltip("Cuánto se levanta la punta con cada disparo (grados).")]
        public float retrocesoArriba = 10f;
        [Tooltip("Cuánto va hacia atrás con cada disparo (metros).")]
        public float retrocesoAtras = 0.06f;
    }

    public List<Agarre> armas = new List<Agarre>();

    [Header("Granadas (US 259): las manos")]
    [Tooltip("Los puntos de las manos van respecto del pecho (este tanto más abajo que los ojos, metros) y de hacia dónde mira: x a la derecha, y arriba, z adelante.")]
    public float granadaAlturaPecho = 0.3f;
    [Tooltip("Sosteniéndola: la mano derecha (con la granada). Manija \"Granada: Mano derecha\" del muñeco espejo.")]
    public Vector3 granadaDerecha = new Vector3(0.2f, 0.02f, 0.3f);
    [Tooltip("Sosteniéndola: la mano izquierda. Manija \"Granada: Mano izquierda\".")]
    public Vector3 granadaIzquierda = new Vector3(-0.12f, -0.08f, 0.3f);
    [Tooltip("Lanzando: la mano derecha atrás, por arriba del hombro (a los 0,22 s). Con \"Probar el lanzamiento\": manija \"Granada: Atrás\".")]
    public Vector3 granadaAtras = new Vector3(0.25f, 0.2f, -0.1f);
    [Tooltip("Lanzando: donde la suelta, adelante y arriba (a los 0,34 s). Manija \"Granada: Suelta\".")]
    public Vector3 granadaSuelta = new Vector3(0.15f, 0.28f, 0.48f);
    [Tooltip("Lanzando: cuánto sube el arco de la mano entre atrás y la suelta (metros).")]
    public float granadaArco = 0.12f;
    [Tooltip("Lanzando: hasta dónde sigue la mano después de soltarla (a los 0,52 s). Manija \"Granada: Envión\".")]
    public Vector3 granadaEnvion = new Vector3(0.05f, -0.1f, 0.42f);
    [Tooltip("Lanzando: la mano izquierda estirada adelante, apuntando. Manija \"Granada: Izquierda apuntando\".")]
    public Vector3 granadaApunta = new Vector3(-0.15f, 0.12f, 0.45f);

    [Header("Granadas (US 259): la granada")]
    [Tooltip("Dónde va la granada respecto de la mano derecha, en el espacio del hueso de la mano (metros). Con el muñeco espejo: la manija \"Granada\".")]
    public Vector3 granadaLugar = new Vector3(0f, 0.08f, 0.03f);
    [Tooltip("Giro de la granada respecto de la mano (grados).")]
    public Vector3 granadaGiro = Vector3.zero;
    [Tooltip("Tamaño de la granada (1 = el de siempre, 12 cm).")]
    [Range(0.5f, 2f)] public float granadaEscala = 1f;

    /// <summary>US 259: dónde va la granada con la mano derecha ahí (sigue a la mano también al lanzar).</summary>
    public void PoseDeGranada(Transform mano, out Vector3 lugar, out Quaternion giro)
    {
        lugar = mano.position + mano.rotation * granadaLugar;
        giro = mano.rotation * Quaternion.Euler(granadaGiro);
    }

    /// <summary>Pasa al ajuste dónde quedó la manija de la granada (muñeco espejo).</summary>
    public void GranadaDesde(Transform mano, Transform manija)
    {
        Quaternion inversa = Quaternion.Inverse(mano.rotation);
        granadaLugar = inversa * (manija.position - mano.position);
        Vector3 e = (inversa * manija.rotation).eulerAngles;
        granadaGiro = new Vector3(Mathf.DeltaAngle(0f, e.x), Mathf.DeltaAngle(0f, e.y), Mathf.DeltaAngle(0f, e.z));
        granadaEscala = Mathf.Clamp(manija.localScale.x, 0.5f, 2f);
    }

    private static AgarresTerceraPersona actual;

    /// <summary>El asset de Resources (o uno con los valores de siempre, si falta).</summary>
    public static AgarresTerceraPersona Actual
    {
        get
        {
            if (actual != null) return actual;
            actual = Resources.Load<AgarresTerceraPersona>("AgarresTerceraPersona");
            if (actual == null)
            {
                actual = CreateInstance<AgarresTerceraPersona>();
                actual.armas.Add(new Agarre { arma = "Pistol_2", nombre = "La Porteña" });
            }
            return actual;
        }
    }

    /// <summary>El ajuste de esa arma de primera persona, o null si todavía no tiene (queda como antes).</summary>
    public Agarre Buscar(GameObject primeraPersona)
    {
        if (primeraPersona == null) return null;
        string nombreArma = primeraPersona.name;
        foreach (Agarre a in armas)
            if (a != null && a.arma == nombreArma) return a;
        return null;
    }
}
