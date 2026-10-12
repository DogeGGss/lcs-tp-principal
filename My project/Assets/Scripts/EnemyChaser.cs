using System.Collections;
using UnityEngine;

// Enemigo del campo de práctica: camina hacia el jugador. Tiene el mismo modelo que el jugador (AlienSoldier), con sus
// animaciones (el mismo Animator Controller) y sus zonas de impacto en los huesos (US 165), y el contorno rojo de
// rival mientras está vivo (ContornoRival). Al morir cae hacia atrás como los demás jugadores en el online (US 030)
// y desaparece.
// RequireComponent asegura que si o si haya un HealthSystem y un Rigidbody en el objeto
[RequireComponent(typeof(HealthSystem))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyChaser : MonoBehaviour
{
    [Header("Movimiento")]
    public float speed = 3f;

    [Tooltip("Distancia a la que se detiene, para no superponerse con el jugador.")]
    public float stopDistance = 1.2f;

    [Tooltip("Grados por segundo que gira el cuerpo para mirar hacia el jugador.")]
    public float turnSpeed = 240f;

    [Header("Animación")]
    [Tooltip("Velocidad (m/s) a la que la animación de caminar va al 100 %: la del jugador caminando (PlayerMovement.speed).")]
    public float walkAnimationSpeed = 5f;

    [Tooltip("Material del contorno rojo de rival (ContornoRival), como los rivales en el online (US 031, CA5).")]
    public Material outline;

    [Header("Al morir")]
    [Tooltip("Segundos que tarda en desaparecer después de morir.")]
    public float destroyDelay = 1.5f;

    [Tooltip("Plata que gana el jugador al eliminarlo (por ahora, para llegar a comprar en el mapa de pruebas).")]
    public int killReward = 300;

    private Transform target;
    private HealthSystem health;
    private Collider[] colliders;
    private Rigidbody rb;
    private Animator animator;
    private bool dead;

    private void Awake()
    {
        health = GetComponent<HealthSystem>();
        colliders = GetComponentsInChildren<Collider>(true); // la cápsula y las zonas de impacto
        rb = GetComponent<Rigidbody>(); // Atrapamos el componente físico
        animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false; // lo mueve el Rigidbody
    }

    private void Start()
    {
        FindTarget();
        health.Died += OnDied;
        if (animator != null) ContornoRival.Crear(gameObject, animator.transform, outline, () => !dead);
    }

    private void OnDestroy()
    {
        health.Died -= OnDied;
    }

    // FixedUpdate es obligatorio cuando movemos objetos usando físicas
    private void FixedUpdate()
    {
        if (dead) return;

        if (target == null)
        {
            FindTarget();
            if (target == null) return;
        }

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        if (distance <= stopDistance)
        {
            // Si llegó a la distancia, frena la velocidad física
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        Vector3 direction = toTarget.normalized;

        // En lugar de teletransportar, le aplicamos velocidad al cuerpo físico
        rb.linearVelocity = new Vector3(direction.x * speed, rb.linearVelocity.y, direction.z * speed);

        // La rotación también se maneja desde el Rigidbody para evitar vibraciones
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        rb.MoveRotation(Quaternion.RotateTowards(transform.rotation, lookRotation, turnSpeed * Time.fixedDeltaTime));
    }

    // Los mismos parámetros que manda PlayerMovement: VelX y VelZ van de -1 a 1 (1 = caminando a la velocidad del jugador).
    private void Update()
    {
        if (animator == null || dead) return;
        Vector3 velocity = transform.InverseTransformDirection(rb.linearVelocity);
        float scale = 1f / Mathf.Max(0.01f, walkAnimationSpeed);
        animator.SetFloat("VelX", Mathf.Clamp(velocity.x * scale, -1f, 1f), 0.08f, Time.deltaTime);
        animator.SetFloat("VelZ", Mathf.Clamp(velocity.z * scale, -1f, 1f), 0.08f, Time.deltaTime);
    }

    private void FindTarget()
    {
        PlayerMovement player = FindAnyObjectByType<PlayerMovement>();
        if (player != null) target = player.transform;
    }

    private void OnDied()
    {
        dead = true;
        // Ya no recibe disparos: ni la cápsula ni las zonas de impacto.
        foreach (Collider c in colliders) if (c != null) c.enabled = false;

        // Congelamos el cuerpo al morir para que deje de empujar
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;

        // Solo lo puede eliminar el jugador al que persigue.
        PlayerWallet wallet = target != null ? target.GetComponentInParent<PlayerWallet>() : null;
        if (wallet != null) wallet.Add(killReward);

        if (animator != null)
        {
            animator.SetFloat("VelX", 0f);
            animator.SetFloat("VelZ", 0f);
            StartCoroutine(Fall(animator.transform));
        }
        Destroy(gameObject, destroyDelay);
    }

    // Cae hacia atrás como los demás jugadores en el online (JugadorEnRed, US 030).
    private static IEnumerator Fall(Transform model)
    {
        Quaternion from = model.localRotation, to = Quaternion.Euler(-90f, 0f, 0f);
        const float duration = 0.45f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            model.localRotation = Quaternion.Slerp(from, to, k * k);
            yield return null;
        }
        model.localRotation = to;
    }
}
