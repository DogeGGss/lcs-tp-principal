using System.Collections;
using UnityEngine;

// US 120 · Bot del campo de práctica: el mismo muñeco de Scenes/Pruebas_Armas (MuniecoPrueba: el modelo del jugador,
// con sus zonas de impacto y el contorno rojo de rival), sin la persecución. Lo crea el PanelDePractica en cada lugar
// de bot de la zona de blancos, de frente a la línea de tiro.
// - Recibe el daño de verdad del arma (por zona y distancia, US 165) y muestra el marcador de impacto como un rival.
// - CA1: al morir cae hacia atrás, como los rivales en el online, y a los 3 s se levanta solo con la vida completa.
// - Con "Mover bots" (panel) camina de costado, para un lado y para el otro, con frenadas y cambios de lado al azar.
//   Los que están arriba de una caja no se mueven.
// - CA3: no ataca ni empuja: no le hace nada al jugador.
public class BotDePractica : MonoBehaviour
{
    public const float VuelveEn = 3f;
    private const float VelocidadAnimacion = 5f; // la del jugador caminando, como EnemyChaser
    private const float DuracionCaida = 0.45f;

    // Cuando muere, y si fue a la cabeza (para el marcador del panel).
    public event System.Action<BotDePractica, bool> Murio;

    public bool Moverse { get; set; }

    private HealthSystem vida;
    private Collider[] colliders;
    private Animator animator;
    private Transform modelo;
    private Quaternion giroModelo;
    private Vector3 lugar;
    private bool puedeMoverse;
    private float x0, x1;
    private bool muerto;
    private float velocidad, direccion, cambioEn;

    // Pone un muñeco parado en "pie" (sus pies, no su centro), mirando hacia la línea de tiro. "puedeMoverse": camina
    // de costado entre x0 y x1 cuando el panel lo pide.
    public static BotDePractica Crear(GameObject munieco, Transform padre, Vector3 pie, bool puedeMoverse, float x0, float x1)
    {
        GameObject m = Instantiate(munieco, padre);
        m.name = "Bot de práctica";
        // Se le saca la persecución, pero antes se toma su material del contorno rojo de rival (como PistaDeDistancias).
        Material contorno = null;
        foreach (EnemyChaser perseguir in m.GetComponentsInChildren<EnemyChaser>(true))
        {
            if (contorno == null) contorno = perseguir.outline;
            Destroy(perseguir);
        }
        if (contorno == null && ConfigRed.Actual != null) contorno = ConfigRed.Actual.contornoRival;
        foreach (Rigidbody rb in m.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;

        // El centro del muñeco está a media altura (su cápsula), no en los pies.
        CapsuleCollider capsula = m.GetComponent<CapsuleCollider>();
        float pies = capsula != null ? (capsula.center.y - capsula.height * 0.5f) * m.transform.lossyScale.y : 0f;
        m.transform.SetPositionAndRotation(pie - Vector3.up * pies, Quaternion.LookRotation(Vector3.back, Vector3.up));

        BotDePractica bot = m.AddComponent<BotDePractica>();
        bot.lugar = m.transform.position;
        bot.puedeMoverse = puedeMoverse;
        bot.x0 = Mathf.Min(x0, x1);
        bot.x1 = Mathf.Max(x0, x1);
        if (bot.animator != null && contorno != null)
            ContornoRival.Crear(m, bot.animator.transform, contorno, () => bot != null && !bot.muerto);
        return bot;
    }

    private void Awake()
    {
        vida = GetComponent<HealthSystem>();
        colliders = GetComponentsInChildren<Collider>(true); // la cápsula y las zonas de impacto
        animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
            modelo = animator.transform;
            giroModelo = modelo.localRotation;
        }
        if (vida != null) vida.Died += AlMorir;
    }

    private void OnDestroy()
    {
        if (vida != null) vida.Died -= AlMorir;
    }

    private void Update()
    {
        if (muerto) return;
        float vx = 0f;
        if (Moverse && puedeMoverse)
        {
            if (Time.time >= cambioEn) NuevoRumbo();
            Vector3 p = transform.position;
            p.x += direccion * velocidad * Time.deltaTime;
            if (p.x <= x0) { p.x = x0; direccion = 1f; }
            else if (p.x >= x1) { p.x = x1; direccion = -1f; }
            transform.position = p;
            vx = direccion * velocidad;
        }
        // Los mismos parámetros que manda PlayerMovement: así camina de costado con la animación del jugador.
        if (animator != null)
        {
            Vector3 local = transform.InverseTransformDirection(new Vector3(vx, 0f, 0f)) / VelocidadAnimacion;
            animator.SetFloat("VelX", Mathf.Clamp(local.x, -1f, 1f), 0.08f, Time.deltaTime);
            animator.SetFloat("VelZ", Mathf.Clamp(local.z, -1f, 1f), 0.08f, Time.deltaTime);
        }
    }

    // Como un jugador que esquiva: cada tanto frena o cambia de lado, a distinta velocidad.
    private void NuevoRumbo()
    {
        cambioEn = Time.time + Random.Range(0.4f, 1.4f);
        direccion = Random.value < 0.2f ? 0f : Random.value < 0.5f ? -1f : 1f;
        velocidad = Random.Range(2f, 4.5f);
    }

    private void AlMorir()
    {
        muerto = true;
        foreach (Collider c in colliders) if (c != null) c.enabled = false;
        if (animator != null)
        {
            animator.SetFloat("VelX", 0f);
            animator.SetFloat("VelZ", 0f);
        }
        StartCoroutine(CaerYVolver());
        Murio?.Invoke(this, vida.KilledByHeadshot);
    }

    // Cae hacia atrás como los rivales en el online (como EnemyChaser) y a los 3 s se levanta.
    private IEnumerator CaerYVolver()
    {
        if (modelo != null)
        {
            Quaternion desde = modelo.localRotation, hasta = Quaternion.Euler(-90f, 0f, 0f);
            for (float t = 0f; t < DuracionCaida; t += Time.deltaTime)
            {
                float k = t / DuracionCaida;
                modelo.localRotation = Quaternion.Slerp(desde, hasta, k * k);
                yield return null;
            }
            modelo.localRotation = hasta;
        }
        yield return new WaitForSeconds(VuelveEn - DuracionCaida);
        Revivir(false);
    }

    // De pie, con la vida completa. "alLugar": vuelve a donde arrancó (al reiniciar, CA2).
    public void Revivir(bool alLugar)
    {
        StopAllCoroutines();
        muerto = false;
        if (modelo != null) modelo.localRotation = giroModelo;
        foreach (Collider c in colliders) if (c != null) c.enabled = true;
        if (vida != null) vida.Revive();
        if (alLugar) transform.position = lugar;
        cambioEn = 0f;
    }
}
