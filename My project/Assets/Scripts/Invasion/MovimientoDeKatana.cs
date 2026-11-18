using UnityEngine;

// US 395, CA9: cómo se mueve la katana en primera persona. Cada momento del ataque es una pose, y entre una y otra se
// pasa de a poco. Una pose tiene tres partes, todas en el espacio de la cámara (x a la derecha, y arriba, z adelante):
// - brazo: cuánto se corren juntos el brazo derecho y la katana (metros). Lo aplica AnimacionPrimeraPersona.
// - muñeca: cuánto gira la katana alrededor de la mano derecha (Gun_ref_rightHand), en grados: x baja la punta
//   (negativo la levanta), y la lleva a los costados, z la inclina. La mano no la suelta.
// - corrimiento: cuánto se corre la katana sola, sin el brazo (metros; para acomodarla en la mano en esa pose).
// Las poses se ajustan en Play con Riftwalker > Armas > Poses de la katana (se ve cada una quieta, se cambian los
// números y se guardan en Resources/Katana/Katana_ViewModel).
public class MovimientoDeKatana : MonoBehaviour
{
    [System.Serializable]
    public class Pose
    {
        public Vector3 brazo;
        public Vector3 muneca;
        public Vector3 corrimiento;

        public Pose() { }
        public Pose(Vector3 brazo, Vector3 muneca) { this.brazo = brazo; this.muneca = muneca; }
        public Pose Copia() => new Pose { brazo = brazo, muneca = muneca, corrimiento = corrimiento };
    }

    [Header("Tajo (CA3): arriba a la derecha, por el medio, abajo a la izquierda")]
    public Pose tajoArriba = new Pose(new Vector3(0.06f, 0.08f, -0.02f), new Vector3(-20f, 0f, -25f));
    public Pose tajoMedio = new Pose(new Vector3(-0.04f, 0f, 0.04f), new Vector3(35f, 0f, 20f));
    public Pose tajoAbajo = new Pose(new Vector3(-0.16f, -0.10f, 0.06f), new Vector3(60f, -20f, 45f));
    [Header("Tajo fuerte (CA4): el final, más abajo (el principio es el del tajo)")]
    public Pose tajoFuerteAbajo = new Pose(new Vector3(-0.20f, -0.14f, 0.08f), new Vector3(75f, -25f, 55f));
    [Header("Estocada (CA5) y guardia (CA6)")]
    public Pose estocada = new Pose(new Vector3(-0.03f, 0.03f, 0.2f), new Vector3(50f, 0f, 0f));
    public Pose guardia = new Pose(new Vector3(-0.06f, 0.08f, 0.02f), new Vector3(10f, 0f, 80f));

    /// <summary>Los nombres de las poses, en el orden de Poses().</summary>
    public static readonly string[] Nombres = { "Quieta", "Tajo: arriba", "Tajo: medio", "Tajo: abajo", "Tajo fuerte: abajo", "Estocada", "Guardia" };
    /// <summary>Las poses (la 0, quieta, no se cambia: es como quedó en el ajuste a mano).</summary>
    public Pose[] Poses() => new[] { Quieta, tajoArriba, tajoMedio, tajoAbajo, tajoFuerteAbajo, estocada, guardia };
    private static readonly Pose Quieta = new Pose();

    // ---------- Para ajustar en Play (la ventana Poses de la katana) ----------
    /// <summary>Muestra esa pose quieta (-1: la katana se mueve como en el juego).</summary>
    public static int PoseFija = -1;
    /// <summary>Repite un ataque para verlo (0 tajo, 1 tajo fuerte, 2 estocada; -1 nada), a esa velocidad.</summary>
    public static int Probando = -1;
    public static float VelocidadDePrueba = 1f;
    private static float probandoDesde;
    public static void Probar(int que) { Probando = que; probandoDesde = Time.time; }

    private MeleeAttack ataque;
    private Transform modelo, mano;
    private Vector3 basePos;
    private Quaternion baseGiro;

    /// <summary>Lo que se corre el brazo en este momento (lo aplica AnimacionPrimeraPersona).</summary>
    public Vector3 Brazo { get; private set; }

    private void Start()
    {
        ataque = GetComponentInParent<MeleeAttack>();
        modelo = transform.Find("Modelo");
        mano = transform.Find(BrazosEnCamara.AgarreDerecho);
        if (modelo != null) { basePos = modelo.localPosition; baseGiro = modelo.localRotation; }
        if (ataque == null) enabled = false; // (la copia de otro jugador o la del espectador: queda quieta)
    }

    private void LateUpdate()
    {
        if (modelo == null || ataque == null || BrazosEnCamara.AjustandoAMano) { Brazo = Vector3.zero; return; } // (ajustando a mano: quieta)
        Pose p = Ahora();
        Brazo = p.brazo;

        // La muñeca y el corrimiento, del espacio de la cámara al de este modelo (que puede estar girado).
        Quaternion aCamara = transform.localRotation, aModelo = Quaternion.Inverse(aCamara);
        Quaternion giro = aModelo * Quaternion.Euler(p.muneca) * aCamara;
        Vector3 corrido = aModelo * p.corrimiento;
        Vector3 escala = transform.localScale;
        corrido = new Vector3(corrido.x / Mathf.Max(0.0001f, escala.x), corrido.y / Mathf.Max(0.0001f, escala.y), corrido.z / Mathf.Max(0.0001f, escala.z));
        Vector3 pivote = mano != null ? mano.localPosition : basePos;
        modelo.localPosition = pivote + giro * (basePos - pivote) + corrido;
        modelo.localRotation = giro * baseGiro;
    }

    // La pose de este momento, según el ataque (o la que se está ajustando o probando).
    private Pose Ahora()
    {
        Pose[] poses = Poses();
        if (PoseFija >= 0 && PoseFija < poses.Length && Probando < 0) return poses[PoseFija];

        float corte = ataque.AvanceDelCorte, estoc = ataque.AvanceDeLaEstocada, guard = ataque.Guardia;
        bool fuerte = ataque.TajoFuerte;
        if (Probando >= 0)
        {
            float t = (Time.time - probandoDesde) * VelocidadDePrueba;
            corte = estoc = -1f; guard = 0f;
            if (Probando == 2) estoc = t / 0.25f;
            else { corte = t / MeleeAttack.Total; fuerte = Probando == 1; }
            if (Mathf.Max(corte, estoc) > 1f) { Probando = -1; corte = estoc = -1f; }
        }

        Pose p = Quieta;
        if (corte >= 0f)
        {
            float t = corte * MeleeAttack.Total;
            Pose abajo = fuerte ? tajoFuerteAbajo : tajoAbajo;
            if (t < MeleeAttack.Preparacion) p = Mezclar(Quieta, tajoArriba, Mathf.SmoothStep(0f, 1f, t / MeleeAttack.Preparacion));
            else if (t < MeleeAttack.Preparacion + MeleeAttack.Corte)
            {
                float f = (t - MeleeAttack.Preparacion) / MeleeAttack.Corte;
                f = 1f - (1f - f) * (1f - f); // rápido al principio
                p = f < 0.5f ? Mezclar(tajoArriba, tajoMedio, f * 2f) : Mezclar(tajoMedio, abajo, f * 2f - 1f);
            }
            else p = Mezclar(abajo, Quieta, Mathf.SmoothStep(0f, 1f, (t - MeleeAttack.Preparacion - MeleeAttack.Corte) / MeleeAttack.Vuelta));
        }
        if (estoc >= 0f)
            p = Mezclar(p, estocada, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, estoc / 0.25f) * (estoc > 0.8f ? (1f - estoc) / 0.2f : 1f)));
        if (guard > 0f) p = Mezclar(p, guardia, Mathf.SmoothStep(0f, 1f, guard));
        return p;
    }

    private static Pose Mezclar(Pose a, Pose b, float f)
    {
        Quaternion q = Quaternion.Slerp(Quaternion.Euler(a.muneca), Quaternion.Euler(b.muneca), f);
        return new Pose { brazo = Vector3.Lerp(a.brazo, b.brazo, f), muneca = q.eulerAngles, corrimiento = Vector3.Lerp(a.corrimiento, b.corrimiento, f) };
    }

    private void OnDisable()
    {
        Brazo = Vector3.zero;
        if (modelo != null) modelo.SetLocalPositionAndRotation(basePos, baseGiro);
    }
}
