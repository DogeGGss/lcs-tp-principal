using UnityEngine;

// US 395, CA9: la muñeca de la katana en primera persona. El brazo y la katana se mueven juntos poco
// (AnimacionPrimeraPersona: si giran mucho, el brazo de la foto se sale de lugar); el giro grande lo hace solo la katana
// ("Modelo"), alrededor de donde la agarra la mano ("Agarre"):
// - el tajo cruza en diagonal, de arriba a la derecha hacia abajo a la izquierda (el fuerte, más amplio);
// - la estocada baja la punta para que apunte adelante;
// - la guardia la acuesta delante, casi horizontal.
// Va en el modelo de primera persona de la katana (Resources/Katana/Katana_ViewModel). Los giros son en el espacio de
// la cámara (grados) y se ajustan en Play.
public class MovimientoDeKatana : MonoBehaviour
{
    [Header("Tajo (CA3 y CA4)")]
    [Tooltip("Giro de la katana al levantarla para el tajo (x adelante, y a los costados, z inclina: positivo hacia la izquierda).")]
    public Vector3 tajoArriba = new Vector3(-10f, 0f, -30f);
    [Tooltip("Giro al terminar el tajo, cruzada abajo a la izquierda.")]
    public Vector3 tajoAbajo = new Vector3(25f, -20f, 80f);
    [Tooltip("Cuánto más amplio es el tajo fuerte.")]
    public float fuerte = 1.3f;

    [Header("Estocada (CA5)")]
    [Tooltip("Giro para que la punta apunte adelante.")]
    public Vector3 estocada = new Vector3(50f, 0f, 0f);

    [Header("Guardia (CA6)")]
    [Tooltip("Giro con la hoja acostada delante.")]
    public Vector3 guardia = new Vector3(10f, 0f, 80f);

    private MeleeAttack ataque;
    private Transform modelo, agarre;
    private Vector3 basePos;
    private Quaternion baseGiro;

    private void Start()
    {
        ataque = GetComponentInParent<MeleeAttack>();
        modelo = transform.Find("Modelo");
        agarre = modelo != null ? modelo.Find("Agarre") : null;
        if (modelo != null) { basePos = modelo.localPosition; baseGiro = modelo.localRotation; }
        if (ataque == null) enabled = false; // (la copia de otro jugador o la del espectador: queda quieta)
    }

    private void LateUpdate()
    {
        if (modelo == null || ataque == null || BrazosEnCamara.AjustandoAMano) return; // (ajustando a mano: quieta)
        Vector3 giro = Vector3.zero;

        float avance = ataque.AvanceDelCorte;
        if (avance >= 0f)
        {
            float t = avance * MeleeAttack.Total, k = ataque.TajoFuerte ? fuerte : 1f;
            Vector3 a = tajoArriba * k, b = tajoAbajo * k;
            if (t < MeleeAttack.Preparacion) giro = a * Mathf.SmoothStep(0f, 1f, t / MeleeAttack.Preparacion);
            else if (t < MeleeAttack.Preparacion + MeleeAttack.Corte)
            {
                float f = (t - MeleeAttack.Preparacion) / MeleeAttack.Corte;
                giro = Vector3.Lerp(a, b, 1f - (1f - f) * (1f - f)); // rápido al principio
            }
            else giro = b * (1f - Mathf.SmoothStep(0f, 1f, (t - MeleeAttack.Preparacion - MeleeAttack.Corte) / MeleeAttack.Vuelta));
        }

        float e = ataque.AvanceDeLaEstocada;
        if (e >= 0f) giro += estocada * Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, e / 0.25f) * (e > 0.8f ? (1f - e) / 0.2f : 1f));

        giro += guardia * Mathf.SmoothStep(0f, 1f, ataque.Guardia);

        // Girar alrededor del agarre: la mano queda donde está y la hoja da la vuelta.
        Quaternion q = Quaternion.Euler(giro);
        Vector3 pivote = agarre != null ? basePos + baseGiro * agarre.localPosition : basePos;
        modelo.localPosition = pivote + q * (basePos - pivote);
        modelo.localRotation = q * baseGiro;
    }

    private void OnDisable()
    {
        if (modelo != null) modelo.SetLocalPositionAndRotation(basePos, baseGiro);
    }
}
