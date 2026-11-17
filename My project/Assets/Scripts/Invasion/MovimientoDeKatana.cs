using UnityEngine;

// US 395, CA9: la muñeca de la katana en primera persona. El brazo y la katana se corren juntos (AnimacionPrimeraPersona);
// el giro lo hace solo la katana ("Modelo"), alrededor de la mano derecha (Gun_ref_rightHand), así la mano nunca la
// suelta y el brazo de la foto no se sale de lugar.
// Cada pose dice hacia dónde apunta la hoja, en el espacio de la cámara (x a la derecha, y arriba, z adelante): así
// sirve aunque se cambie a mano dónde está la katana quieta (el giro sale de comparar con esa).
// - El tajo: levantada arriba a la derecha, pasa por adelante y termina abajo a la izquierda (el fuerte, más abajo).
// - La estocada: la punta al frente.
// - La guardia: acostada delante, apuntando a la izquierda.
// Va en el modelo de primera persona de la katana (Resources/Katana/Katana_ViewModel). Se ajusta en Play.
public class MovimientoDeKatana : MonoBehaviour
{
    [Header("Tajo (CA3 y CA4): hacia dónde apunta la hoja")]
    public Vector3 tajoArriba = new Vector3(0.45f, 0.85f, -0.25f);
    public Vector3 tajoMedio = new Vector3(-0.2f, 0.35f, 0.9f);
    public Vector3 tajoAbajo = new Vector3(-0.75f, -0.35f, 0.55f);
    [Tooltip("El final del tajo fuerte (más abajo).")]
    public Vector3 tajoFuerteAbajo = new Vector3(-0.6f, -0.65f, 0.45f);

    [Header("Estocada (CA5)")]
    public Vector3 estocada = new Vector3(0f, 0.08f, 1f);

    [Header("Guardia (CA6)")]
    public Vector3 guardia = new Vector3(-0.95f, 0.15f, 0.25f);

    private MeleeAttack ataque;
    private Transform modelo, mano;
    private Vector3 basePos;
    private Quaternion baseGiro;

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
        if (modelo == null || ataque == null || BrazosEnCamara.AjustandoAMano) return; // (ajustando a mano: quieta)

        Quaternion giro = Quaternion.identity;
        float avance = ataque.AvanceDelCorte;
        if (avance >= 0f)
        {
            float t = avance * MeleeAttack.Total;
            Quaternion arriba = Hacia(tajoArriba), medio = Hacia(tajoMedio), abajo = Hacia(ataque.TajoFuerte ? tajoFuerteAbajo : tajoAbajo);
            if (t < MeleeAttack.Preparacion)
                giro = Quaternion.Slerp(Quaternion.identity, arriba, Mathf.SmoothStep(0f, 1f, t / MeleeAttack.Preparacion));
            else if (t < MeleeAttack.Preparacion + MeleeAttack.Corte)
            {
                float f = (t - MeleeAttack.Preparacion) / MeleeAttack.Corte;
                f = 1f - (1f - f) * (1f - f); // rápido al principio
                giro = f < 0.5f ? Quaternion.Slerp(arriba, medio, f * 2f) : Quaternion.Slerp(medio, abajo, f * 2f - 1f);
            }
            else
                giro = Quaternion.Slerp(abajo, Quaternion.identity,
                    Mathf.SmoothStep(0f, 1f, (t - MeleeAttack.Preparacion - MeleeAttack.Corte) / MeleeAttack.Vuelta));
        }

        float e = ataque.AvanceDeLaEstocada;
        if (e >= 0f)
            giro = Quaternion.Slerp(giro, Hacia(estocada), Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, e / 0.25f) * (e > 0.8f ? (1f - e) / 0.2f : 1f)));

        float g = ataque.Guardia;
        if (g > 0f) giro = Quaternion.Slerp(giro, Hacia(guardia), Mathf.SmoothStep(0f, 1f, g));

        // Girar alrededor de la mano: la mano queda donde está y la hoja da la vuelta.
        Vector3 pivote = mano != null ? mano.localPosition : basePos;
        modelo.localPosition = pivote + giro * (basePos - pivote);
        modelo.localRotation = giro * baseGiro;
    }

    // El giro (en el espacio de este modelo) que lleva la hoja de donde apunta quieta a esa dirección de la cámara.
    private Quaternion Hacia(Vector3 enLaCamara)
    {
        Vector3 quieta = baseGiro * Vector3.forward;
        Vector3 destino = Quaternion.Inverse(transform.localRotation) * enLaCamara.normalized;
        return Quaternion.FromToRotation(quieta, destino);
    }

    private void OnDisable()
    {
        if (modelo != null) modelo.SetLocalPositionAndRotation(basePos, baseGiro);
    }
}
