using System.Collections.Generic;
using UnityEngine;

// Animación del arma en primera persona, hecha por código (sin Animator): el arma y los brazos (la "foto" de
// BrazosEnCamara) se mueven juntos, como una sola pieza, alrededor de la mano que agarra el arma.
// - Disparo: un golpe hacia atrás y arriba que vuelve con un resorte.
// - Recarga: el arma baja y se inclina, queda abajo mientras se cambia el cargador y vuelve. La Trochita, en cambio,
//   sube de costado mostrando el tambor, que gira y va frenando, como un arma que se enfría.
// - Sacar el arma: sube desde abajo de la pantalla.
// - Quieto, caminar, correr y saltar: respira apenas quieto, se balancea al caminar (más al correr) y baja un poco al
//   despegar y al caer de un salto.
// - Mirar: un leve retraso del arma al girar la cámara.
// - Detalles: La Porteña mueve la corredera y cambia el cargador (PiezasDeLaPistola); La Trochita gira el tambor, con
//   una rayita por bala (PiezasDeLaTrochita, US 244).
// Por ahora con las dos pistolas, La Porteña y La Trochita (la secundaria de categoría Pistolas con ArmaDeFuego), y el
// Urquiza (la principal de categoría Subfusiles, US 246): su cargador cae al recargar (PiezasDelUrquiza) y al final
// tira de la palanca de carga. La Roca (escopeta, US 248): bombea después de cada tiro (PiezasDeLaRoca, con la mano
// izquierda) y carga de a un cartucho con el arma inclinada. Y el cuchillo (US 256): quieto, caminando, al sacarlo y al mirar como las demás, y el
// corte de costado al atacar, que mueve solo el cuchillo y el brazo derecho.
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

    [Header("Disparo (Urquiza): automático, golpes chicos y un resorte más duro")]
    public float urquizaAtras = 0.012f;
    public float urquizaArriba = 2.2f;
    public float urquizaCostado = 1f;
    [Tooltip("Qué tan rápido vuelve a su lugar al soltar el disparo (más alto, más rápido).")]
    public float urquizaResorte = 300f;
    [Tooltip("Al final de la recarga tira de la palanca de carga: el arma da un tirón hacia atrás (metros).")]
    public float urquizaPalanca = 0.03f;

    [Header("Roca (escopeta, US 248)")]
    public float rocaAtras = 0.075f;
    public float rocaArriba = 14f;
    public float rocaCostado = 3f;
    [Tooltip("Recarga: cuánto se inclina el arma para mostrar por dónde entran los cartuchos (grados).")]
    public float rocaRecargaInclinacion = 30f;
    [Tooltip("Recarga: cuánto va la mano izquierda hacia atrás, del guardamanos a la boca de carga (metros).")]
    public float rocaManoAtras = 0.11f;
    [Tooltip("Recarga: cuánto baja la mano izquierda a buscar cada cartucho (metros).")]
    public float rocaManoAbajo = 0.05f;

    [Header("Recarga")]
    public float recargaAbajo = 0.11f;
    public float recargaInclinacion = 28f;
    public float recargaGiro = 14f;

    [Header("Recarga (La Trochita): sube de costado mostrando el tambor")]
    [Tooltip("Cuánto sube el arma (metros).")]
    public float trochitaRecargaArriba = 0.09f;
    [Tooltip("Cuánto se corre hacia el centro de la pantalla (metros).")]
    public float trochitaRecargaAdentro = 0.06f;
    [Tooltip("Cuánto gira la punta hacia el centro, mostrando el costado con el tambor (grados).")]
    public float trochitaRecargaCostado = 25f;
    [Tooltip("Cuánto levanta la punta (grados).")]
    public float trochitaRecargaPunta = 50f;
    [Tooltip("Cuánto se inclina hacia la cámara (grados).")]
    public float trochitaRecargaInclinacion = 25f;
    [Tooltip("Mientras recarga suelta la mano izquierda, que baja y sale de la pantalla (metros hacia abajo).")]
    public float trochitaManoIzquierdaAbajo = 0.18f;

    [Header("Corte del cuchillo (US 256)")]
    [Tooltip("Cuánto cruza de un lado al otro (metros).")]
    public float corteAncho = 0.16f;
    [Tooltip("Cuánto sube al prepararse (metros).")]
    public float corteAlto = 0.05f;
    [Tooltip("Cuánto gira la punta hacia cada lado (grados).")]
    public float corteGiro = 40f;

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
    [Tooltip("Vueltas por segundo del balanceo caminando a velocidad normal. Cada vuelta son dos pasos (izquierdo y\n" +
             "derecho): 1 = 2 pasos por segundo.")]
    public float balanceoRitmo = 1f;
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
    private Transform[] movidos = new Transform[3];
    private Vector3[] lugarBase = new Vector3[3];
    private Quaternion[] giroBase = new Quaternion[3];
    private int cuantos;

    // Estado de cada animación
    private Vector3 golpePos, golpeVel;   // x, y en grados (arriba, costado); z en metros (atrás)
    private float saltoPos, saltoVel;     // metros hacia abajo
    private bool apoyadoAntes = true;
    private float caidaAntes;
    private float sacarDesde = -10f;
    private float recargaRoca;            // 0 a 1: cuánto está inclinada la Roca para cargar cartuchos
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

    private static bool EsDe(GameObject arma, ShopCategory categoria)
    {
        if (arma == null) return false;
        ArmaDeFuego fuego = arma.GetComponent<ArmaDeFuego>();
        return fuego != null && fuego.shopItem != null && fuego.shopItem.category == categoria;
    }

    private static bool EsTrochita(GameObject arma) => EsDe(arma, ShopCategory.Pistols);
    private static bool EsUrquiza(GameObject arma) => EsDe(arma, ShopCategory.SMGs);
    private static bool EsRoca(GameObject arma) => EsDe(arma, ShopCategory.Shotguns);

    // El arma de la mano, si es una de las que tienen animación.
    private GameObject Animable()
    {
        if (switcher == null) return null;
        GameObject arma = switcher.HeldSecondary;
        if (EsPortena(arma) || EsTrochita(arma)) return arma;
        arma = switcher.HeldPrimary;
        if (EsUrquiza(arma) || EsRoca(arma)) return arma;
        if (arma != null) return null;
        // US 256: el cuchillo.
        GameObject cuchillo = switcher.meleeScript != null ? switcher.meleeScript.CurrentViewModel : null;
        return cuchillo != null && cuchillo.activeInHierarchy ? cuchillo : null;
    }

    private bool EsCuchillo(GameObject arma) =>
        arma != null && switcher != null && switcher.meleeScript != null && arma == switcher.meleeScript.CurrentViewModel;

    // US 256: el corte de costado del cuchillo, según cuánto avanzó (MeleeAttack.AvanceDelCorte): la mano lo lleva
    // atrás y hacia adentro (el centro de la pantalla), cruza de adentro hacia afuera (el golpe es en la mitad) y vuelve. Solo se mueven el cuchillo y
    // el brazo derecho; el izquierdo queda donde está.
    private MeleeAttack ataque;
    private void Corte(out Vector3 mover, out Quaternion girar)
    {
        mover = Vector3.zero;
        girar = Quaternion.identity;
        if (ataque == null) ataque = jugador.GetComponentInChildren<MeleeAttack>(true);
        float avance = ataque != null ? ataque.AvanceDelCorte : -1f;
        if (avance < 0f) return;
        float t = avance * MeleeAttack.Total;
        // De adentro para afuera: arranca hacia el centro de la pantalla (izquierda) y cruza hacia la derecha.
        Vector3 atrasPos = new Vector3(-corteAncho * 0.45f, corteAlto, -0.02f), finPos = new Vector3(corteAncho * 0.55f, -corteAlto * 0.6f, 0.05f);
        Vector3 atrasGiro = new Vector3(-15f, -corteGiro, 35f), finGiro = new Vector3(10f, corteGiro * 1.1f, 60f);
        Vector3 pos, giro;
        if (t < MeleeAttack.Preparacion)
        {
            float f = Mathf.SmoothStep(0f, 1f, t / MeleeAttack.Preparacion);
            pos = atrasPos * f; giro = atrasGiro * f;
        }
        else if (t < MeleeAttack.Preparacion + MeleeAttack.Corte)
        {
            float f = (t - MeleeAttack.Preparacion) / MeleeAttack.Corte;
            f = f * f * (3f - 2f * f);
            pos = Vector3.Lerp(atrasPos, finPos, f); giro = Vector3.Lerp(atrasGiro, finGiro, f);
        }
        else
        {
            float f = Mathf.SmoothStep(0f, 1f, (t - MeleeAttack.Preparacion - MeleeAttack.Corte) / MeleeAttack.Vuelta);
            pos = finPos * (1f - f); giro = finGiro * (1f - f);
        }
        mover = pos;
        girar = Quaternion.Euler(giro);
    }

    private IPiezasDelArma Piezas(GameObject arma)
    {
        if (!piezas.TryGetValue(arma, out IPiezasDelArma p))
        {
            p = EsTrochita(arma) ? new PiezasDeLaTrochita(arma, transform)
              : EsUrquiza(arma) ? new PiezasDelUrquiza(arma, transform)
              : EsRoca(arma) ? new PiezasDeLaRoca(arma, transform)
              : (IPiezasDelArma)new PiezasDeLaPistola(arma, transform);
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
        bool trochita = EsTrochita(arma), urquiza = EsUrquiza(arma), roca = EsRoca(arma);
        float arriba = trochita ? trochitaArriba : urquiza ? urquizaArriba : roca ? rocaArriba : retrocesoArriba;
        float costado = trochita ? trochitaCostado : urquiza ? urquizaCostado : roca ? rocaCostado : retrocesoCostado;
        float atras = trochita ? trochitaAtras : urquiza ? urquizaAtras : roca ? rocaAtras : retrocesoAtras;
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
        float k = arma != null && EsUrquiza(arma) ? urquizaResorte : resorte;
        float w = Mathf.Sqrt(k);
        golpeVel += (-k * golpePos - 2f * amortiguacion * w * golpeVel) * dt;
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
        // La Trochita no: sube de costado mostrando el tambor, que se enfría girando (US 244).
        float recarga = municion != null ? municion.ReloadProgress : -1f;
        float sueltaIzquierda = 0f;
        if (recarga >= 0f && EsTrochita(arma))
        {
            float arriba = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.12f, recarga)) *
                           (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 1f, recarga)));
            // Tiembla mientras el tambor gira rápido y se va calmando a medida que frena.
            float temblor = arriba * Mathf.Clamp01(1f - Mathf.InverseLerp(0.12f, 0.85f, recarga)) * 0.6f;
            mover += new Vector3(-trochitaRecargaAdentro, trochitaRecargaArriba, 0f) * arriba;
            // La mano izquierda suelta el arma un poco antes de que suba y la vuelve a agarrar cuando ya bajó.
            // Tarda un cuarto de la recarga en irse y otro en volver (más de medio segundo cada vez), con una curva
            // que arranca y frena despacio.
            sueltaIzquierda = Suave(Mathf.InverseLerp(0f, 0.25f, recarga)) * (1f - Suave(Mathf.InverseLerp(0.75f, 1f, recarga)));
            girar += new Vector3(-trochitaRecargaPunta, -trochitaRecargaCostado, trochitaRecargaInclinacion) * arriba +
                     new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * temblor;
        }
        Vector3 moverIzquierdaExtra = Vector3.zero;
        if (EsRoca(arma))
        {
            // Roca (US 248): carga de a un cartucho y cada cartucho es una recarga corta, así que la inclinación sigue
            // si está recargando (y no si el avance de la recarga vuelve a 0 con cada cartucho). La mano izquierda va
            // del guardamanos a la boca de carga y con cada cartucho baja a buscarlo y lo empuja para adentro.
            ArmaDeFuego fuego = arma.GetComponent<ArmaDeFuego>();
            bool cargando = fuego != null && fuego.Recargando;
            recargaRoca = Mathf.MoveTowards(recargaRoca, cargando ? 1f : 0f, dt / 0.18f);
            float r = Mathf.SmoothStep(0f, 1f, recargaRoca);
            float cartucho = cargando && recarga >= 0f ? recarga : 1f;
            float busca = Mathf.Sin(Mathf.Clamp01(cartucho / 0.6f) * Mathf.PI);       // baja y vuelve a subir
            float empuja = Mathf.Exp(-Mathf.Pow((cartucho - 0.75f) / 0.07f, 2f));    // lo mete
            mover += new Vector3(-0.01f, 0.02f + 0.006f * empuja, 0f) * r;
            girar += new Vector3(-6f, 8f, rocaRecargaInclinacion + 3f * empuja) * r;
            Vector3 haciaAtras = transform.InverseTransformDirection(arma.transform.TransformDirection(Vector3.right)).normalized;
            moverIzquierdaExtra += (haciaAtras * rocaManoAtras + Vector3.down * (rocaManoAbajo * busca) + Vector3.up * (0.01f * empuja)) * r;
        }
        else if (recarga >= 0f)
        {
            float bajada = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.2f, recarga)) *
                           (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 1f, recarga)));
            float empujon = Mathf.Exp(-Mathf.Pow((recarga - 0.6f) / 0.05f, 2f));
            mover += new Vector3(0f, -recargaAbajo * bajada + 0.015f * empujon, 0f);
            girar += new Vector3(recargaInclinacion * bajada * 0.4f, recargaGiro * bajada, recargaInclinacion * bajada);
            // Urquiza (US 246): al final tira de la palanca de carga, un tirón hacia atrás y un poco de giro.
            if (EsUrquiza(arma))
            {
                float palanca = Mathf.Exp(-Mathf.Pow((recarga - 0.78f) / 0.04f, 2f));
                mover += new Vector3(0f, 0f, -urquizaPalanca * palanca);
                girar += new Vector3(-3f * palanca, 0f, -6f * palanca);
            }
        }

        // Sacar el arma: sube desde abajo, durante lo que tarda en estar lista.
        float sacar = Mathf.Clamp01((Time.time - sacarDesde) / Mathf.Max(0.01f, Sacar(arma)));
        float falta = 1f - Mathf.SmoothStep(0f, 1f, sacar);
        mover += new Vector3(0f, -sacarAbajo * falta, 0f);
        girar += new Vector3(sacarInclinacion * falta, 0f, -sacarInclinacion * 0.3f * falta);

        // Quieto: respira. Caminando se balancea; corriendo, más.
        float rapidez = new Vector2(vel.x, vel.z).magnitude;
        float intensidad = apoyado ? Mathf.Clamp(rapidez / Mathf.Max(0.1f, velocidadCaminando), 0f, 1.6f) : 0f;
        // Corriendo los pasos son un poco más rápidos, no en proporción a la velocidad (a 8 m/s, 2,5 pasos por segundo).
        fase += dt * balanceoRitmo * Mathf.PI * 2f * Mathf.Clamp(Mathf.Sqrt(rapidez / Mathf.Max(0.1f, velocidadCaminando)), 0.6f, 1.3f);
        mover += new Vector3(Mathf.Sin(fase) * balanceoAncho, -Mathf.Abs(Mathf.Cos(fase)) * balanceoAlto, 0f) * intensidad;
        respira += dt * respiracionRitmo * Mathf.PI * 2f;
        float quieto = 1f - Mathf.Clamp01(intensidad);
        mover += new Vector3(0f, Mathf.Sin(respira) * respiracion, 0f) * quieto;
        girar += new Vector3(Mathf.Sin(respira + 0.6f) * 0.25f, 0f, 0f) * quieto;

        // Salto
        mover += new Vector3(0f, -saltoPos, 0f);

        // Mirar
        girar += new Vector3(retraso.x, retraso.y, -retraso.y * 0.5f);

        Vector3 moverDerecha = Vector3.zero;
        Quaternion girarDerecha = Quaternion.identity;
        if (EsCuchillo(arma)) Corte(out moverDerecha, out girarDerecha); // el cuchillo no tiene piezas
        else if (detalles)
        {
            IPiezasDelArma p = Piezas(arma);
            p.Actualizar(recarga, municion != null ? municion.Ammo : 1);
            if (p is PiezasDeLaRoca roca) moverIzquierdaExtra += roca.ManoIzquierda(transform); // acompaña el bombeo
        }
        else if (piezas.TryGetValue(arma, out IPiezasDelArma sinDetalles)) sinDetalles.Quieta();

        Aplicar(arma.transform, mover, Quaternion.Euler(girar), sueltaIzquierda, moverDerecha, girarDerecha, moverIzquierdaExtra);
        estado = "Animando " + arma.name + (foto != null ? " con sus brazos" : " (sin encontrar la foto de los brazos)") +
                 $" | corrida {mover.magnitude * 100f:0.0} cm, girada {girar.magnitude:0.0}°";
    }

    // Mueve el arma y la foto de los brazos como una sola pieza, girando alrededor de la mano derecha.
    // "sueltaIzquierda" (0 a 1): la mano izquierda suelta el arma y baja fuera de la pantalla; el arma queda solo en la
    // mano derecha (la foto tiene el brazo derecho aparte, como hijo, y ese sigue al arma).
    // "moverDerecha" y "girarDerecha": lo que se mueven además solo el arma y el brazo derecho (el corte del cuchillo).
    // "moverIzquierdaExtra": lo que se corre además solo el brazo izquierdo (el bombeo y la carga de la Roca).
    private void Aplicar(Transform arma, Vector3 mover, Quaternion girar, float sueltaIzquierda,
                         Vector3 moverDerecha, Quaternion girarDerecha, Vector3 moverIzquierdaExtra)
    {
        Vector3 pivote = agarre != null ? transform.InverseTransformPoint(agarre.position) : arma.localPosition;
        cuantos = 0;
        Quaternion girarArma = girarDerecha * girar;
        Vector3 moverArma = mover + moverDerecha;
        Mover(arma, pivote, moverArma, girarArma);
        if (foto == null) return;
        bool aparte = sueltaIzquierda > 0.001f || moverDerecha != Vector3.zero || girarDerecha != Quaternion.identity ||
                      moverIzquierdaExtra != Vector3.zero;
        Transform derecho = aparte ? foto.Find("Brazo derecho") : null;
        if (derecho == null) { Mover(foto, pivote, moverArma, girarArma); return; }

        // Dónde iría la foto entera con el arma: ahí va el brazo derecho.
        Vector3 lugarDerecho = pivote + girarArma * (foto.localPosition - pivote) + moverArma;
        Quaternion giroDerecho = girarArma * foto.localRotation;
        // El izquierdo deja de seguir al arma de a poco y hace un arco: primero se separa del arma (a la izquierda y un
        // poco hacia atrás) y después baja, cada vez más rápido, hasta salir de la pantalla. Al volver hace lo mismo al revés.
        float s = sueltaIzquierda;
        Quaternion girarIzquierdo = Quaternion.Slerp(girar, Quaternion.identity, s);
        float separa = Mathf.Sin(Mathf.Min(1f, s * 1.6f) * Mathf.PI * 0.5f); // se separa enseguida
        float baja = s * s;                                                  // baja después
        Vector3 moverIzquierdo = Vector3.Lerp(mover, Vector3.zero, s) +
                                 new Vector3(-0.025f * separa, -trochitaManoIzquierdaAbajo * baja, -0.035f * separa) +
                                 moverIzquierdaExtra;
        Mover(foto, pivote, moverIzquierdo, girarIzquierdo);
        Guardar(derecho);
        derecho.SetPositionAndRotation(transform.TransformPoint(lugarDerecho), transform.rotation * giroDerecho);
    }

    // Curva que arranca y frena despacio (de 0 a 1).
    private static float Suave(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    // Anota dónde estaba, para dejarlo igual al empezar el cuadro siguiente.
    private void Guardar(Transform t)
    {
        movidos[cuantos] = t;
        lugarBase[cuantos] = t.localPosition;
        giroBase[cuantos] = t.localRotation;
        cuantos++;
    }

    private void Mover(Transform t, Vector3 pivote, Vector3 mover, Quaternion girar)
    {
        Guardar(t);
        t.SetLocalPositionAndRotation(pivote + girar * (t.localPosition - pivote) + mover, girar * t.localRotation);
    }
}
