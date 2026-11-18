using UnityEngine;

// US 395, CA9: cómo se mueve la katana en primera persona. Cada ataque va de donde está quieta hasta una pose final y
// vuelve. Las poses son katanas de muestra dentro del modelo de primera persona (Resources/Katana/Katana_ViewModel):
// "Pose Tajo", "Pose Tajo fuerte", "Pose Estocada" y "Pose Guardia". En el juego no se ven; con el ajuste a mano
// (Riftwalker > Manos de las armas > Ajustar a mano en Play) aparecen, y se las mueve y gira con las herramientas de
// la escena hasta donde tiene que terminar la katana en cada ataque. Al salir del Play se guardan en el prefab.
// La mano no suelta la katana: el brazo derecho se corre lo mismo que se corre el mango (lo aplica
// AnimacionPrimeraPersona con Brazo), y la katana gira alrededor de la mano.
public class MovimientoDeKatana : MonoBehaviour
{
    public const string PoseTajo = "Pose Tajo", PoseTajoFuerte = "Pose Tajo fuerte", PoseEstocada = "Pose Estocada", PoseGuardia = "Pose Guardia";
    /// <summary>Las poses que guarda el ajuste a mano (AjusteDeManos), por su nombre.</summary>
    public static bool EsPose(string nombre) => nombre != null && nombre.StartsWith("Pose ");

    private MeleeAttack ataque;
    private Transform modelo, mano, tajo, tajoFuerte, estocada, guardia;
    private Renderer[] muestras;
    private Vector3 basePos, agarre; // agarre: dónde agarra la mano, en el espacio de la katana
    private Quaternion baseGiro;
    private bool mostrando;

    /// <summary>Lo que se corre el brazo en este momento, en el espacio de la cámara (lo aplica AnimacionPrimeraPersona).</summary>
    public Vector3 Brazo { get; private set; }

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
            agarre = Quaternion.Inverse(baseGiro) * ((mano != null ? mano.localPosition : basePos) - basePos);
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
        if (modelo == null || ataque == null || ajustando) { Brazo = Vector3.zero; return; } // (ajustando: quieta)

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

        // El mango, en esa pose, ¿cuánto se corrió de la mano? Eso se corre el brazo (y la katana se corre al revés,
        // porque el brazo la lleva): así la mano sigue en el mango.
        Vector3 manoQuieta = basePos + baseGiro * agarre;
        Vector3 corrido = pos + giro * agarre - manoQuieta;
        Brazo = transform.localRotation * Vector3.Scale(corrido, transform.localScale);
        modelo.localPosition = pos - corrido;
        modelo.localRotation = giro;
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
        if (modelo != null) modelo.SetLocalPositionAndRotation(basePos, baseGiro);
    }
}
