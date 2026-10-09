using UnityEngine;

// Animación del arma en primera persona, hecha por código (sin Animator): el arma y los brazos (la "foto" de
// BrazosEnCamara) se mueven juntos, como una sola pieza, alrededor de la mano que agarra el arma.
// - Disparo: un golpe hacia atrás y arriba que vuelve con un resorte.
// - Recarga: el arma baja y se inclina, queda abajo mientras se cambia el cargador y vuelve.
// - Sacar el arma: sube desde abajo de la pantalla.
// - Caminar y mirar: balanceo al caminar y un leve retraso del arma al girar la cámara.
// - Detalles (PiezasDeLaPistola): corredera, fogonazo, vaina que sale volando y cambio de cargador.
// Por ahora solo con la pistola inicial (La Porteña). Se apaga con "Activa" en el Inspector, para comparar.
// El movimiento se suma al final del cuadro (después de BrazosEnCamara) y se saca al empezar el siguiente
// (RestaurarPrimeraPersona), así ningún otro script ve el arma corrida.
[DefaultExecutionOrder(300)]
public class AnimacionPrimeraPersona : MonoBehaviour
{
    private const string NombreFoto = "Brazos con {0} (cámara)"; // las que arma BrazosEnCamara

    [Tooltip("Apagado, el arma queda quieta como antes.")]
    public bool activa = true;

    [Tooltip("Solo para revisar: qué está haciendo en este momento (o por qué no anima).")]
    [SerializeField] private string estado = "";

    [Header("Disparo")]
    [Tooltip("Cuánto va hacia atrás el arma en cada disparo (metros).")]
    public float retrocesoAtras = 0.045f;
    [Tooltip("Cuánto se levanta la punta del arma en cada disparo (grados).")]
    public float retrocesoArriba = 7f;
    [Tooltip("Giro al azar hacia los costados en cada disparo (grados).")]
    public float retrocesoCostado = 1.5f;
    [Tooltip("Qué tan rápido vuelve a su lugar (más alto, más rápido).")]
    public float resorte = 160f;
    [Tooltip("Cuánto frena el rebote (1 = sin rebote).")]
    [Range(0.2f, 1.2f)] public float amortiguacion = 0.55f;

    [Header("Recarga")]
    public float recargaAbajo = 0.11f;
    public float recargaInclinacion = 28f;
    public float recargaGiro = 14f;

    [Header("Sacar el arma")]
    public float sacarDuracion = 0.3f;
    public float sacarAbajo = 0.22f;
    public float sacarInclinacion = 35f;

    [Header("Caminar y mirar")]
    public float balanceoAlto = 0.012f;
    public float balanceoAncho = 0.008f;
    [Tooltip("Pasos por segundo del balanceo caminando a velocidad normal.")]
    public float balanceoRitmo = 1.8f;
    [Tooltip("Cuánto se atrasa el arma al girar la cámara (grados por grado girado).")]
    public float retrasoAlMirar = 0.06f;
    public float retrasoMaximo = 4f;

    [Header("Detalles del arma")]
    [Tooltip("Corredera que va y vuelve, fogonazo, vaina y cambio de cargador.")]
    public bool detalles = true;
    [Tooltip("Tamaño del fogonazo en la boca del caño.")]
    public float escalaFogonazo = 0.35f;

    private WeaponSwitcher switcher;
    private Camera camara;
    private CharacterController cuerpo;
    private Transform jugador;
    private Pistola pistola;
    private Transform foto, agarre;
    private PiezasDeLaPistola piezas;

    // Lo que se le sumó en este cuadro, para sacarlo al empezar el siguiente.
    private Transform[] movidos = new Transform[2];
    private Vector3[] lugarBase = new Vector3[2];
    private Quaternion[] giroBase = new Quaternion[2];
    private int cuantos;

    // Estado de cada animación
    private Vector3 golpePos, golpeVel;   // x, y en grados (arriba, costado); z en metros (atrás)
    private float sacarDesde = -10f;
    private bool teniaPistola;
    private float fase;
    private Quaternion giroAnterior;
    private Vector2 retraso;

    private void Awake()
    {
        switcher = GetComponent<WeaponSwitcher>();
        camara = GetComponent<Camera>();
        // El mismo "tirador" que manda la pistola al disparar (Pistola usa el PlayerMovement).
        PlayerMovement movimiento = GetComponentInParent<PlayerMovement>();
        jugador = movimiento != null ? movimiento.transform : transform.root;
        cuerpo = jugador.GetComponentInChildren<CharacterController>();
        giroAnterior = transform.rotation;
        if (GetComponent<RestaurarPrimeraPersona>() == null) gameObject.AddComponent<RestaurarPrimeraPersona>().animacion = this;
    }

    private void OnEnable() => WeaponFire.Fired += AlDisparar;
    private void OnDisable()
    {
        WeaponFire.Fired -= AlDisparar;
        Restaurar();
    }

    private void AlDisparar(Transform tirador, Vector3 origen, Vector3[] direcciones)
    {
        if (!activa || tirador != jugador || !ConPistola()) return;
        golpeVel += new Vector3(-retrocesoArriba, Random.Range(-retrocesoCostado, retrocesoCostado), retrocesoAtras) * 22f;
        if (detalles)
        {
            if (pistola == null) pistola = switcher.pistolObj.GetComponent<Pistola>();
            Piezas().escalaFogonazo = escalaFogonazo;
            Piezas().Disparo(pistola != null ? pistola.Ammo : 1, ConfigRed.Actual != null ? ConfigRed.Actual.fogonazoPrimeraPersona : null);
        }
    }

    private PiezasDeLaPistola Piezas()
    {
        if (piezas == null) piezas = new PiezasDeLaPistola(switcher.pistolObj, transform);
        return piezas;
    }

    private bool ConPistola() => switcher != null && switcher.pistolObj != null && switcher.HeldSecondary == switcher.pistolObj;

    /// <summary>Saca lo que se sumó en el cuadro anterior (lo llama RestaurarPrimeraPersona, antes que todo).</summary>
    public void Restaurar()
    {
        for (int i = 0; i < cuantos; i++)
            if (movidos[i] != null) movidos[i].SetLocalPositionAndRotation(lugarBase[i], giroBase[i]);
        cuantos = 0;
    }

    private void LateUpdate()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        Quaternion giro = transform.rotation;
        bool conPistola = activa && camara != null && camara.enabled && ConPistola();

        // Retraso al mirar: el arma se queda un poco atrás del giro de la cámara y vuelve sola.
        Vector3 delta = (Quaternion.Inverse(giroAnterior) * giro).eulerAngles;
        giroAnterior = giro;
        Vector2 giroCuadro = new Vector2(Mathf.DeltaAngle(0f, delta.x), Mathf.DeltaAngle(0f, delta.y));
        retraso = Vector2.ClampMagnitude(retraso - giroCuadro * retrasoAlMirar, retrasoMaximo);
        retraso = Vector2.Lerp(retraso, Vector2.zero, 1f - Mathf.Exp(-10f * dt));

        // Resorte del disparo (amortiguado): vuelve a cero.
        float w = Mathf.Sqrt(resorte);
        golpeVel += (-resorte * golpePos - 2f * amortiguacion * w * golpeVel) * dt;
        golpePos += golpeVel * dt;

        piezas?.ActualizarSueltas(dt);

        if (!conPistola)
        {
            piezas?.Quieta();
            estado = !activa ? "Apagada (Activa sin tildar)"
                : camara == null ? "No está en la cámara del jugador"
                : !camara.enabled ? "La cámara está apagada (no es el jugador local)"
                : switcher == null || switcher.pistolObj == null ? "No encuentra la pistola (WeaponSwitcher.pistolObj)"
                : "Sin la pistola en la mano (en la mano: " + (switcher.HeldSecondary != null ? switcher.HeldSecondary.name : switcher.HeldPrimary != null ? switcher.HeldPrimary.name : "nada") + ")";
            teniaPistola = false;
            return;
        }
        if (!teniaPistola) sacarDesde = Time.time; // la acaba de sacar
        teniaPistola = true;

        GameObject arma = switcher.pistolObj;
        if (pistola == null) pistola = arma.GetComponent<Pistola>();
        if (agarre == null) agarre = arma.transform.Find(BrazosEnCamara.AgarreDerecho);
        if (foto == null) foto = transform.Find(string.Format(NombreFoto, arma.name));

        // ---------- Pose de este cuadro, en el espacio de la cámara ----------
        Vector3 mover = Vector3.zero;
        Vector3 girar = Vector3.zero; // grados: x arriba/abajo (negativo = punta arriba), y costado, z inclinación

        // Disparo
        girar += new Vector3(golpePos.x, golpePos.y, golpePos.y * 0.5f);
        mover += new Vector3(0f, -golpePos.x * 0.002f, -golpePos.z);

        // Recarga: baja (0 a 0,2), queda abajo y vuelve (0,8 a 1); un empujón al meter el cargador (0,6).
        float recarga = pistola != null ? pistola.ReloadProgress : -1f;
        if (recarga >= 0f)
        {
            float bajada = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.2f, recarga)) *
                           (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 1f, recarga)));
            float empujon = Mathf.Exp(-Mathf.Pow((recarga - 0.6f) / 0.05f, 2f));
            mover += new Vector3(0f, -recargaAbajo * bajada + 0.015f * empujon, 0f);
            girar += new Vector3(recargaInclinacion * bajada * 0.4f, recargaGiro * bajada, recargaInclinacion * bajada);
        }

        // Sacar el arma: sube desde abajo.
        float sacar = Mathf.Clamp01((Time.time - sacarDesde) / Mathf.Max(0.01f, sacarDuracion));
        float falta = 1f - Mathf.SmoothStep(0f, 1f, sacar);
        mover += new Vector3(0f, -sacarAbajo * falta, 0f);
        girar += new Vector3(sacarInclinacion * falta, 0f, -sacarInclinacion * 0.3f * falta);

        // Caminar
        Vector3 vel = cuerpo != null ? cuerpo.velocity : Vector3.zero;
        float rapidez = new Vector2(vel.x, vel.z).magnitude;
        bool apoyado = cuerpo == null || cuerpo.isGrounded;
        float intensidad = apoyado ? Mathf.Clamp01(rapidez / 5f) : 0f;
        fase += dt * balanceoRitmo * Mathf.PI * 2f * Mathf.Clamp(rapidez / 5f, 0.5f, 1.6f);
        mover += new Vector3(Mathf.Sin(fase) * balanceoAncho, -Mathf.Abs(Mathf.Cos(fase)) * balanceoAlto, 0f) * intensidad;

        // Mirar
        girar += new Vector3(retraso.x, retraso.y, -retraso.y * 0.5f);

        if (detalles) Piezas().Actualizar(recarga, pistola != null ? pistola.Ammo : 1);
        else piezas?.Quieta();

        Aplicar(arma.transform, mover, Quaternion.Euler(girar));
        estado = "Animando " + arma.name + (foto != null ? " con sus brazos" : " (sin encontrar la foto de los brazos)") +
                 $" | corrida {mover.magnitude * 100f:0.0} cm, girada {girar.magnitude:0.0}°";
    }

    // Mueve el arma y la foto de los brazos como una sola pieza, girando alrededor de la mano derecha.
    private void Aplicar(Transform arma, Vector3 mover, Quaternion girar)
    {
        Vector3 pivote = agarre != null ? transform.InverseTransformPoint(agarre.position) : arma.localPosition;
        cuantos = 0;
        Mover(arma, pivote, mover, girar);
        if (foto != null) Mover(foto, pivote, mover, girar);
    }

    private void Mover(Transform t, Vector3 pivote, Vector3 mover, Quaternion girar)
    {
        movidos[cuantos] = t;
        lugarBase[cuantos] = t.localPosition;
        giroBase[cuantos] = t.localRotation;
        cuantos++;
        t.SetLocalPositionAndRotation(pivote + girar * (t.localPosition - pivote) + mover, girar * t.localRotation);
    }
}
