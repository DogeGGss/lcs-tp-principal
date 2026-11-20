using UnityEngine;

// US 395, CA9: cómo se mueve la katana en primera persona. Cada ataque va de donde está quieta hasta una pose final y
// vuelve. Las poses son katanas de muestra dentro del modelo de primera persona (Resources/Katana/Katana_ViewModel):
// "Pose Tajo", "Pose Tajo fuerte", "Pose Estocada" y "Pose Guardia". En el juego no se ven; con el ajuste a mano
// (Riftwalker > Manos de las armas > Ajustar a mano en Play) aparecen, y se las mueve y gira con las herramientas de
// la escena hasta donde tiene que terminar la katana en cada ataque. Al salir del Play se guardan en el prefab.
// El brazo: BrazosEnCamara saca una foto del brazo derecho agarrando cada katana de muestra (BrazoConPoses), y acá se
// mezclan con la quieta con el mismo peso que la katana: el brazo se dobla de a poco y la mano la acompaña.
// Mientras esas fotos no están (los primeros cuadros), el brazo derecho se mueve y gira con la katana como una sola
// pieza (Brazo y BrazoGiro, que aplica AnimacionPrimeraPersona; "giroDelBrazo" deja que gire solo una parte).
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
    private BrazosEnCamara brazos;
    private BrazoConPoses brazoConPoses;
    private readonly System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, float>> pesos =
        new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, float>>();

    /// <summary>Dónde está la katana quieta (en el espacio de este modelo), para las fotos de las poses.</summary>
    public bool Iniciado { get; private set; }
    public Vector3 BasePos => basePos;
    public Quaternion BaseGiro => baseGiro;

    /// <summary>Lo que se corre y gira el brazo (con la katana) en este momento, en el espacio de la cámara, alrededor
    /// de la mano derecha (lo aplica AnimacionPrimeraPersona).</summary>
    public Vector3 Brazo { get; private set; }
    public Quaternion BrazoGiro { get; private set; } = Quaternion.identity;

    private bool buscado;

    private void Start()
    {
        ataque = GetComponentInParent<MeleeAttack>();
        Buscar();
        Mostrar(false);
        brazos = GetComponentInParent<BrazosEnCamara>();
        if (ataque == null) enabled = false; // (la copia de otro jugador o la del espectador: queda quieta)
    }

    // Las poses y dónde está la katana quieta. También para el espectador (Pose), que puede usar una copia apagada.
    private void Buscar()
    {
        if (buscado) return;
        buscado = true;
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
        if (modelo != null)
        {
            basePos = modelo.localPosition;
            baseGiro = modelo.localRotation;
            Transform punto = modelo.Find("Agarre"); // dónde la agarra la mano, en el mango
            agarre = punto != null ? punto.localPosition : Quaternion.Inverse(baseGiro) * ((mano != null ? mano.localPosition : basePos) - basePos);
        }
        Iniciado = modelo != null;
    }

    /// <summary>El modelo de la katana (el que se mueve a las poses).</summary>
    public Transform Modelo { get { Buscar(); return modelo; } }

    /// <summary>
    /// Dónde va el modelo de la katana (en el espacio de este objeto) con ese avance del tajo ("corte", de 0 a 1, o -1),
    /// de la estocada ("estocadaAvance", de 0 a 1, o -1) y de la guardia (0 a 1), y cuánto pesa cada pose (para el brazo).
    /// Lo usa también el modo espectador, con lo que hace el compañero que se mira.
    /// </summary>
    public void Pose(float corte, bool fuerte, float estocadaAvance, float guardiaAvance, out Vector3 pos, out Quaternion giro,
                     System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, float>> pesosDeLasPoses)
    {
        Buscar();
        pos = basePos;
        giro = baseGiro;
        pesosDeLasPoses.Clear();
        // Tajo: va hasta la pose (rápido en el corte), se queda un instante y vuelve.
        if (corte >= 0f)
        {
            float t = corte * MeleeAttack.Total, ida = MeleeAttack.Preparacion + MeleeAttack.Corte;
            float f = t < ida ? Mathf.SmoothStep(0f, 1f, t / ida)
                              : 1f - Mathf.SmoothStep(0f, 1f, (t - ida) / MeleeAttack.Vuelta);
            Hacia(fuerte && tajoFuerte != null ? tajoFuerte : tajo, f, ref pos, ref giro, pesosDeLasPoses);
        }
        // Estocada: llega enseguida, se queda mientras avanza y vuelve al final.
        float e = estocadaAvance;
        if (e >= 0f) Hacia(estocada, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, e / 0.25f) * (e > 0.8f ? (1f - e) / 0.2f : 1f)), ref pos, ref giro, pesosDeLasPoses);
        // Guardia: mientras la tiene puesta.
        if (guardiaAvance > 0f) Hacia(guardia, Mathf.SmoothStep(0f, 1f, guardiaAvance), ref pos, ref giro, pesosDeLasPoses);
    }

    // El brazo derecho de la foto de esta katana, con las poses (null hasta que estén las fotos).
    private BrazoConPoses BrazoConPoses()
    {
        if (brazoConPoses != null) return brazoConPoses;
        MeshRenderer foto = brazos != null ? brazos.FotoDe(gameObject) : null;
        Transform derecho = foto != null ? foto.transform.Find("Brazo derecho") : null;
        BrazoConPoses b = derecho != null ? derecho.GetComponent<BrazoConPoses>() : null;
        if (b != null && b.Cuantas > 0) brazoConPoses = b;
        return brazoConPoses;
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

        Pose(ataque.AvanceDelCorte, ataque.TajoFuerte, ataque.AvanceDeLaEstocada, ataque.Guardia, out Vector3 pos, out Quaternion giro, pesos);

        // Con las fotos de las poses: el brazo se dobla (mezclando las fotos) y la katana va derecho a su lugar.
        BrazoConPoses brazo = BrazoConPoses();
        if (brazo != null)
        {
            brazo.Mezclar(pesos);
            modelo.localPosition = pos;
            modelo.localRotation = giro;
            Brazo = Vector3.zero;
            BrazoGiro = Quaternion.identity;
            return;
        }

        // Sin ellas: el brazo hace parte del movimiento (todo, con giroDelBrazo en 1) y la katana lo que falta, en la mano.
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

    // Lleva la katana hacia esa pose (f de 0 a 1) y anota cuánto pesa cada pose (para mezclar las fotos del brazo).
    private static void Hacia(Transform pose, float f, ref Vector3 pos, ref Quaternion giro,
                              System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, float>> pesos)
    {
        if (pose == null || f <= 0f) return;
        pos = Vector3.Lerp(pos, pose.localPosition, f);
        giro = Quaternion.Slerp(giro, pose.localRotation, f);
        for (int i = 0; i < pesos.Count; i++) pesos[i] = new System.Collections.Generic.KeyValuePair<string, float>(pesos[i].Key, pesos[i].Value * (1f - f));
        pesos.Add(new System.Collections.Generic.KeyValuePair<string, float>(pose.name, f));
    }

    private void OnDisable()
    {
        Brazo = Vector3.zero;
        BrazoGiro = Quaternion.identity;
        if (brazoConPoses != null) { pesos.Clear(); brazoConPoses.Mezclar(pesos); }
        if (modelo != null) modelo.SetLocalPositionAndRotation(basePos, baseGiro);
    }
}
