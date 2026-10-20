using System.Collections.Generic;
using UnityEngine;

// Animación del arma en primera persona, hecha por código (sin Animator): el arma y los brazos (la "foto" de
// BrazosEnCamara) se mueven juntos, como una sola pieza, alrededor de la mano que agarra el arma.
// - Disparo: un golpe hacia atrás y arriba que vuelve con un resorte.
// - Recarga: el arma baja y se inclina, queda abajo mientras se cambia el cargador y vuelve.
// - Sacar el arma: sube desde abajo de la pantalla.
// - Quieto, caminar, correr y saltar: respira apenas quieto, se balancea al caminar (más al correr) y baja un poco al
//   despegar y al caer de un salto.
// - Mirar: un leve retraso del arma al girar la cámara.
// - Detalles: La Porteña mueve la corredera y cambia el cargador (PiezasDeLaPistola); La Trochita gira el tambor y
//   cambia el cargador (PiezasDeLaTrochita, US 244).
// Por ahora con las dos pistolas: La Porteña y La Trochita (la secundaria de categoría Pistolas con ArmaDeFuego).
// Cada una tiene su fuerza de retroceso y su tiempo para sacarla. Se apaga con "Activa" en el Inspector, para comparar.
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

    [Header("Disparo (La Porteña)")]
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

    [Header("Disparo (La Trochita): más pesada, golpea más")]
    public float trochitaAtras = 0.065f;
    public float trochitaArriba = 12f;
    public float trochitaCostado = 2.5f;

    [Header("Recarga")]
    public float recargaAbajo = 0.11f;
    public float recargaInclinacion = 28f;
    public float recargaGiro = 14f;

    [Header("Sacar el arma")]
    [Tooltip("La Porteña. Las demás tardan lo que dice su ficha (tiempo para estar lista).")]
    public float sacarDuracion = 0.3f;
    public float sacarAbajo = 0.22f;
    public float sacarInclinacion = 35f;

    [Header("Quieto, caminar, correr y saltar")]
    [Tooltip("Cuánto sube y baja el arma quieta, como respirando (metros).")]
    public float respiracion = 0.0025f;
    [Tooltip("Respiraciones por segundo.")]
    public float respiracionRitmo = 0.35f;
    public float balanceoAlto = 0.012f;
    public float balanceoAncho = 0.008f;
    [Tooltip("Pasos por segundo del balanceo caminando a velocidad normal.")]
    public float balanceoRitmo = 1.8f;
    [Tooltip("Velocidad de caminar (m/s): corriendo más rápido el balanceo es más grande.")]
    public float velocidadCaminando = 5f;
    [Tooltip("Cuánto baja el arma al despegar de un salto (metros).")]
    public float saltoDespegue = 0.025f;
    [Tooltip("Cuánto baja el arma al caer, por cada m/s de caída (metros).")]
    public float saltoCaida = 0.006f;

    [Header("Mirar")]
    [Tooltip("Cuánto se atrasa el arma al girar la cámara (grados por grado girado).")]
    public float retrasoAlMirar = 0.06f;
    public float retrasoMaximo = 4f;

    [Header("Detalles del arma")]
    [Tooltip("Corredera, tambor y cambio de cargador.")]
    public bool detalles = true;

    private WeaponSwitcher switcher;
    private Camera camara;
    private CharacterController cuerpo;
    private Transform jugador;
    private GameObject actual;           // el arma que se está animando
    private IHudWeapon municion;
    private Transform foto, agarre;
    private readonly Dictionary<GameObject, IPiezasDelArma> piezas = new Dictionary<GameObject, IPiezasDelArma>();

    // Lo que se le sumó en este cuadro, para sacarlo al empezar el siguiente.
    private Transform[] movidos = new Transform[2];
    private Vector3[] lugarBase = new Vector3[2];
    private Quaternion[] giroBase = new Quaternion[2];
    private int cuantos;

    // Estado de cada animación
    private Vector3 golpePos, golpeVel;   // x, y en grados (arriba, costado); z en metros (atrás)
    private float saltoPos, saltoVel;     // metros hacia abajo
    private bool apoyadoAntes = true;
    private float caidaAntes;
    private float sacarDesde = -10f;
    private float fase, respira;
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

    // ---------- Qué arma se anima ----------

    private bool EsPortena(GameObject arma) => switcher != null && arma != null && arma == switcher.pistolObj;

    private static bool EsTrochita(GameObject arma)
    {
        if (arma == null) return false;
        ArmaDeFuego fuego = arma.GetComponent<ArmaDeFuego>();
        return fuego != null && fuego.shopItem != null && fuego.shopItem.category == ShopCategory.Pistols;
    }

    // El arma de la mano, si es una de las que tienen animación.
    private GameObject Animable()
    {
        if (switcher == null) return null;
        GameObject arma = switcher.HeldSecondary;
        return EsPortena(arma) || EsTrochita(arma) ? arma : null;
    }

    private IPiezasDelArma Piezas(GameObject arma)
    {
        if (!piezas.TryGetValue(arma, out IPiezasDelArma p))
        {
            p = EsTrochita(arma) ? new PiezasDeLaTrochita(arma, transform) : (IPiezasDelArma)new PiezasDeLaPistola(arma, transform);
            piezas[arma] = p;
        }
        return p;
    }

    private float Sacar(GameObject arma)
    {
        if (EsPortena(arma)) return sacarDuracion;
        ShopItem ficha = WeaponSwitcher.FichaDe(arma);
        return ficha != null && ficha.equipTime > 0f ? ficha.equipTime : sacarDuracion;
    }

    private void AlDisparar(Transform tirador, Vector3 origen, Vector3[] direcciones)
    {
        if (!activa || tirador != jugador) return;
        GameObject arma = Animable();
        if (arma == null) return;
        bool trochita = EsTrochita(arma);
        float arriba = trochita ? trochitaArriba : retrocesoArriba;
        float costado = trochita ? trochitaCostado : retrocesoCostado;
        float atras = trochita ? trochitaAtras : retrocesoAtras;
        golpeVel += new Vector3(-arriba, Random.Range(-costado, costado), atras) * 22f;
        if (detalles)
        {
            IHudWeapon hud = arma.GetComponent<IHudWeapon>();
            Piezas(arma).Disparo(hud != null ? hud.Ammo : 1);
        }
    }

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
        GameObject arma = activa && camara != null && camara.enabled ? Animable() : null;

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

        // Salto: al despegar y al caer el arma baja un poco y vuelve con un resorte más blando.
        Vector3 vel = cuerpo != null ? cuerpo.velocity : Vector3.zero;
        bool apoyado = cuerpo == null || cuerpo.isGrounded;
        if (apoyadoAntes && !apoyado && vel.y > 0.5f) saltoVel += saltoDespegue * 18f;
        if (!apoyadoAntes && apoyado) saltoVel += Mathf.Min(-caidaAntes, 15f) * saltoCaida * 18f;
        apoyadoAntes = apoyado;
        caidaAntes = vel.y;
        const float resorteSalto = 90f;
        saltoVel += (-resorteSalto * saltoPos - 2f * 0.7f * Mathf.Sqrt(resorteSalto) * saltoVel) * dt;
        saltoPos += saltoVel * dt;

        foreach (IPiezasDelArma p in piezas.Values) p.ActualizarSueltas(dt);

        if (arma == null)
        {
            foreach (IPiezasDelArma p in piezas.Values) p.Quieta();
            GameObject mano = switcher != null ? (switcher.HeldSecondary != null ? switcher.HeldSecondary : switcher.HeldPrimary) : null;
            estado = !activa ? "Apagada (Activa sin tildar)"
                : camara == null ? "No está en la cámara del jugador"
                : !camara.enabled ? "La cámara está apagada (no es el jugador local)"
                : switcher == null ? "No encuentra el WeaponSwitcher"
                : "Sin un arma con animación en la mano (en la mano: " + (mano != null ? mano.name : "nada") + ")";
            actual = null;
            return;
        }
        if (arma != actual)
        {
            // La acaba de sacar (o cambió de una pistola a la otra).
            if (actual != null && piezas.TryGetValue(actual, out IPiezasDelArma anterior)) anterior.Quieta();
            actual = arma;
            municion = arma.GetComponent<IHudWeapon>();
            agarre = arma.transform.Find(BrazosEnCamara.AgarreDerecho);
            foto = transform.Find(string.Format(NombreFoto, arma.name));
            sacarDesde = Time.time;
        }
        if (foto == null) foto = transform.Find(string.Format(NombreFoto, arma.name)); // la foto sale unos cuadros después

        // ---------- Pose de este cuadro, en el espacio de la cámara ----------
        Vector3 mover = Vector3.zero;
        Vector3 girar = Vector3.zero; // grados: x arriba/abajo (negativo = punta arriba), y costado, z inclinación

        // Disparo
        girar += new Vector3(golpePos.x, golpePos.y, golpePos.y * 0.5f);
        mover += new Vector3(0f, -golpePos.x * 0.002f, -golpePos.z);

        // Recarga: baja (0 a 0,2), queda abajo y vuelve (0,8 a 1); un empujón al meter el cargador (0,6).
        float recarga = municion != null ? municion.ReloadProgress : -1f;
        if (recarga >= 0f)
        {
            float bajada = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.2f, recarga)) *
                           (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 1f, recarga)));
            float empujon = Mathf.Exp(-Mathf.Pow((recarga - 0.6f) / 0.05f, 2f));
            mover += new Vector3(0f, -recargaAbajo * bajada + 0.015f * empujon, 0f);
            girar += new Vector3(recargaInclinacion * bajada * 0.4f, recargaGiro * bajada, recargaInclinacion * bajada);
        }

        // Sacar el arma: sube desde abajo, durante lo que tarda en estar lista.
        float sacar = Mathf.Clamp01((Time.time - sacarDesde) / Mathf.Max(0.01f, Sacar(arma)));
        float falta = 1f - Mathf.SmoothStep(0f, 1f, sacar);
        mover += new Vector3(0f, -sacarAbajo * falta, 0f);
        girar += new Vector3(sacarInclinacion * falta, 0f, -sacarInclinacion * 0.3f * falta);

        // Quieto: respira. Caminando se balancea; corriendo, más.
        float rapidez = new Vector2(vel.x, vel.z).magnitude;
        float intensidad = apoyado ? Mathf.Clamp(rapidez / Mathf.Max(0.1f, velocidadCaminando), 0f, 1.6f) : 0f;
        fase += dt * balanceoRitmo * Mathf.PI * 2f * Mathf.Clamp(rapidez / Mathf.Max(0.1f, velocidadCaminando), 0.5f, 1.6f);
        mover += new Vector3(Mathf.Sin(fase) * balanceoAncho, -Mathf.Abs(Mathf.Cos(fase)) * balanceoAlto, 0f) * intensidad;
        respira += dt * respiracionRitmo * Mathf.PI * 2f;
        float quieto = 1f - Mathf.Clamp01(intensidad);
        mover += new Vector3(0f, Mathf.Sin(respira) * respiracion, 0f) * quieto;
        girar += new Vector3(Mathf.Sin(respira + 0.6f) * 0.25f, 0f, 0f) * quieto;

        // Salto
        mover += new Vector3(0f, -saltoPos, 0f);

        // Mirar
        girar += new Vector3(retraso.x, retraso.y, -retraso.y * 0.5f);

        if (detalles) Piezas(arma).Actualizar(recarga, municion != null ? municion.Ammo : 1);
        else if (piezas.TryGetValue(arma, out IPiezasDelArma sinDetalles)) sinDetalles.Quieta();

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
