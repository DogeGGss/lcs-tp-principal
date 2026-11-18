using UnityEngine;

// US 395, CA9: cómo se mueve la katana en primera persona. Cada ataque va de donde está quieta hasta una pose final y
// vuelve. Las poses son katanas de muestra dentro del modelo de primera persona (Resources/Katana/Katana_ViewModel):
// "Pose Tajo", "Pose Tajo fuerte", "Pose Estocada" y "Pose Guardia". En el juego no se ven; con el ajuste a mano
// (Riftwalker > Manos de las armas > Ajustar a mano en Play) aparecen, y se las mueve y gira con las herramientas de
// la escena hasta donde tiene que terminar la katana en cada ataque. Al salir del Play se guardan en el prefab.
// La mano no suelta la katana: el brazo derecho (la foto) se mueve y gira con ella, como una sola pieza (lo aplica
// AnimacionPrimeraPersona con Brazo y BrazoGiro). Si el brazo gira demasiado y se ve raro, "giroDelBrazo" deja que
// gire solo una parte y el resto lo gira la katana sola en la mano (alrededor del "Agarre"), aunque así el agarre se
// nota menos firme.
public class MovimientoDeKatana : MonoBehaviour
{
    [Tooltip("Cuánto del giro de la katana gira también el brazo (1: todo, la mano nunca la suelta; 0: el brazo solo se corre y la katana gira sola en la mano).")]
    [Range(0f, 1f)] public float giroDelBrazo = 1f;

    public const string PoseTajo = "Pose Tajo", PoseTajoFuerte = "Pose Tajo fuerte", PoseEstocada = "Pose Estocada", PoseGuardia = "Pose Guardia";
    /// <summary>Las poses que guarda el ajuste a mano (AjusteDeManos), por su nombre.</summary>
    public static bool EsPose(string nombre) => nombre != null && nombre.StartsWith("Pose ");

    private MeleeAttack ataque;
    private Transform modelo, mano, tajo, tajoFuerte, estocada, guardia;
    private Renderer[] muestras;
    private Vector3 basePos, agarre; // agarre: dónde agarra la mano, en el espacio de la katana
    private Quaternion baseGiro;
    private bool mostrando;

    /// <summary>Lo que se corre y gira el brazo (con la katana) en este momento, en el espacio de la cámara, alrededor
    /// de la mano derecha (lo aplica AnimacionPrimeraPersona).</summary>
    public Vector3 Brazo { get; private set; }
    public Quaternion BrazoGiro { get; private set; } = Quaternion.identity;

    private void Start()
    {
        ataque = GetComponentInParent<MeleeAttack>();
        modelo = transform.Find("Modelo");
        mano = transform.Find(BrazosEnCamara.AgarreDerecho);
        tajo = transform.Find(PoseTajo);
        tajoFuerte = transform.Find(PoseTajoFuerte);
        estocada = transform.Find(PoseEstocada);
        guardia = transform.Find(PoseGuardia);
        var lista = new System.Collections.Generic.List<Renderer>();
        foreach (Transform t in transform)
            if (EsPose(t.name)) lista.AddRange(t.GetComponentsInChildren<Renderer>(true));
        muestras = lista.ToArray();
        Mostrar(false);
        if (modelo != null)
        {
            basePos = modelo.localPosition;
            baseGiro = modelo.localRotation;
            Transform punto = modelo.Find("Agarre"); // dónde la agarra la mano, en el mango
            agarre = punto != null ? punto.localPosition : Quaternion.Inverse(baseGiro) * ((mano != null ? mano.localPosition : basePos) - basePos);
        }
        if (ataque == null) enabled = false; // (la copia de otro jugador o la del espectador: queda quieta)
    }

    // Las katanas de muestra se ven solo mientras se ajusta a mano.
    private void Mostrar(bool ver)
    {
        mostrando = ver;
        foreach (Renderer r in muestras) if (r != null) r.enabled = ver;
    }

    private void LateUpdate()
    {
        bool ajustando = BrazosEnCamara.AjustandoAMano;
        if (ajustando != mostrando) Mostrar(ajustando);
        if (modelo == null || ataque == null || ajustando) { Brazo = Vector3.zero; BrazoGiro = Quaternion.identity; return; } // (ajustando: quieta)

        Vector3 pos = basePos;
        Quaternion giro = baseGiro;

        // Tajo: va hasta la pose (rápido en el corte), se queda un instante y vuelve.
        float corte = ataque.AvanceDelCorte;
        if (corte >= 0f)
        {
            float t = corte * MeleeAttack.Total, ida = MeleeAttack.Preparacion + MeleeAttack.Corte;
            float f = t < ida ? Mathf.SmoothStep(0f, 1f, t / ida)
                              : 1f - Mathf.SmoothStep(0f, 1f, (t - ida) / MeleeAttack.Vuelta);
            Hacia(ataque.TajoFuerte && tajoFuerte != null ? tajoFuerte : tajo, f, ref pos, ref giro);
        }
        // Estocada: llega enseguida, se queda mientras avanza y vuelve al final.
        float e = ataque.AvanceDeLaEstocada;
        if (e >= 0f) Hacia(estocada, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, e / 0.25f) * (e > 0.8f ? (1f - e) / 0.2f : 1f)), ref pos, ref giro);
        // Guardia: mientras la tiene puesta.
        if (ataque.Guardia > 0f) Hacia(guardia, Mathf.SmoothStep(0f, 1f, ataque.Guardia), ref pos, ref giro);

        // El brazo hace parte del movimiento (todo, con giroDelBrazo en 1) y la katana lo que falta, en la mano.
        // En el espacio de este modelo: el mango va de g0 (quieta) a g1 (la pose); el brazo gira "parte" y lleva g0 a g1.
        Vector3 g0 = basePos + baseGiro * agarre, g1 = pos + giro * agarre;
        Quaternion parte = Quaternion.Slerp(Quaternion.identity, giro * Quaternion.Inverse(baseGiro), giroDelBrazo);
        Quaternion sinParte = Quaternion.Inverse(parte);
        modelo.localPosition = g0 + sinParte * (pos - g1);
        modelo.localRotation = sinParte * giro;

        // Lo mismo, en el espacio de la cámara y alrededor de la mano derecha (el pivote de AnimacionPrimeraPersona).
        Quaternion rb = transform.localRotation;
        Vector3 tb = transform.localPosition, esc = transform.localScale;
        Vector3 ACamara(Vector3 p) => tb + rb * Vector3.Scale(p, esc);
        Vector3 h = ACamara(mano != null ? mano.localPosition : g0), g0c = ACamara(g0), g1c = ACamara(g1);
        BrazoGiro = rb * parte * Quaternion.Inverse(rb);
        Brazo = g1c - h + BrazoGiro * (h - g0c);
    }

    private static void Hacia(Transform pose, float f, ref Vector3 pos, ref Quaternion giro)
    {
        if (pose == null || f <= 0f) return;
        pos = Vector3.Lerp(pos, pose.localPosition, f);
        giro = Quaternion.Slerp(giro, pose.localRotation, f);
    }

    private void OnDisable()
    {
        Brazo = Vector3.zero;
        BrazoGiro = Quaternion.identity;
        if (modelo != null) modelo.SetLocalPositionAndRotation(basePos, baseGiro);
    }
}
