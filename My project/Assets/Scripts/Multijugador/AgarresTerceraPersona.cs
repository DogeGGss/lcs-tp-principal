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

        [Header("Disparo")]
        [Tooltip("Cuánto se levanta la punta con cada disparo (grados).")]
        public float retrocesoArriba = 10f;
        [Tooltip("Cuánto va hacia atrás con cada disparo (metros).")]
        public float retrocesoAtras = 0.06f;
    }

    public List<Agarre> armas = new List<Agarre>();

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
