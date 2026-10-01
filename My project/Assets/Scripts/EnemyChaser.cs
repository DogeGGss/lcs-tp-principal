using UnityEngine;

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

    [Header("Al morir")]
    [Tooltip("Segundos que tarda en desaparecer después de morir.")]
    public float destroyDelay = 1.5f;

    [Tooltip("Plata que gana el jugador al eliminarlo (por ahora, para llegar a comprar en el mapa de pruebas).")]
    public int killReward = 300;

    private Transform target;
    private HealthSystem health;
    private Collider hitbox;
    private Rigidbody rb;
    private bool dead;

    private void Awake()
    {
        health = GetComponent<HealthSystem>();
        hitbox = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>(); // Atrapamos el componente físico
    }

    private void Start()
    {
        FindTarget();
        health.Died += OnDied;
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

    private void FindTarget()
    {
        PlayerMovement player = FindAnyObjectByType<PlayerMovement>();
        if (player != null) target = player.transform;
    }

    private void OnDied()
    {
        dead = true;
        if (hitbox != null) hitbox.enabled = false;

        // Congelamos el cuerpo al morir para que deje de empujar
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;

        // La cápsula solo la puede eliminar el jugador al que persigue.
        PlayerWallet wallet = target != null ? target.GetComponentInParent<PlayerWallet>() : null;
        if (wallet != null) wallet.Add(killReward);

        Destroy(gameObject, destroyDelay);
    }
}