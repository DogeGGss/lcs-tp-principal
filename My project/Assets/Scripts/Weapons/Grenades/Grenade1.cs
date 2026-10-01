using System.Collections.Generic;
using UnityEngine;

// Granada de metralla ya lanzada (US 079). Va en el prefab Grenade (con su Rigidbody y su collider).
//   CA2: rebota en paredes y piso (se le pone un material con rebote al arrancar).
//   CA3: explota a los "delay" segundos de aparecer (2 s: GrenadeThrower lo toma de la ficha).
//   CA4: 100 hasta 1,5 m del centro, baja en línea recta hasta 25 a 5 m, y más lejos nada. Se mide hasta el
//        punto más cercano del cuerpo (no hasta sus pies) y cada personaje recibe el daño una sola vez.
//   CA5: si hay una pared entre el centro y el cuerpo, no hay daño.
//   CA7: daña a quien la tiró, pero no a sus compañeros (EquiposTacticos.SonAliados).
public class Grenade1 : MonoBehaviour
{
    [Tooltip("Segundos hasta explotar. En el juego lo pisa la ficha de la granada (fuse).")]
    public float delay = 2f;
    public float explosionForce = 70f;
    public float radius = 5f;

    public float maxDamage = 100f;
    public float minDamage = 25f;
    public float fullDamageRadius = 1.5f;

    // Fuerza con la que se lanza la granada
    public float throwForce = 15f;

    // Quién la tiró: sus compañeros no reciben el daño de la explosión (US 031, CA6).
    [HideInInspector] public Transform thrower;

    private float countdown;
    private bool exploded;
    private Rigidbody body;
    private Collider centerCollider;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        if (body != null) body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // que no atraviese paredes finas

        // CA2: sin material la granada no rebota (el rebote por defecto es 0).
        PhysicsMaterial bouncy = new PhysicsMaterial("Granada")
        {
            bounciness = 0.45f,
            dynamicFriction = 0.6f,
            staticFriction = 0.6f,
            bounceCombine = PhysicsMaterialCombine.Maximum,
        };
        foreach (Collider part in GetComponentsInChildren<Collider>())
            if (!part.isTrigger) part.sharedMaterial = bouncy;

        FitCollider();
    }

    void Start()
    {
        countdown = delay;
    }

    void Update()
    {
        if (exploded) return;
        countdown -= Time.deltaTime; // con la pausa local (timeScale 0) la mecha se congela

        if (countdown <= 0f)
        {
            exploded = true;
            Explode();
        }
    }

    // Los números salen de la ficha (ShopItem): mecha, alcance y daño por tramo.
    public void Configure(ShopItem item)
    {
        if (item == null) return;
        if (item.fuse > 0f) delay = item.fuse;
        if (item.effectRadius > 0f) radius = item.effectRadius;
        if (item.bands != null && item.bands.Length > 0)
        {
            DamageBand near = item.bands[0];
            DamageBand far = item.bands[item.bands.Length - 1];
            maxDamage = near.body;
            fullDamageRadius = near.upTo;
            minDamage = far.body;
            if (item.effectRadius <= 0f) radius = far.upTo;
        }
    }

    // Lanza la granada en la dirección indicada (CA2). 'inherited' es la velocidad de quien la tira.
    public void Throw(Vector3 direction, Vector3 inherited = default)
    {
        // Que no choque con quien la tira mientras sale de su mano.
        if (thrower != null)
            foreach (Collider mine in GetComponentsInChildren<Collider>())
                foreach (Collider theirs in thrower.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(mine, theirs);

        if (body == null) return;
        body.AddForce(direction * throwForce, ForceMode.Impulse);
        body.AddForce(inherited, ForceMode.VelocityChange);
        body.angularVelocity = Random.insideUnitSphere * 6f;
    }

    // Daño a una distancia del centro (CA4): pleno cerca, baja en línea recta y 0 más lejos.
    public int DamageAt(float distance)
    {
        if (distance > radius) return 0;
        if (distance <= fullDamageRadius) return Mathf.RoundToInt(maxDamage);
        float t = (distance - fullDamageRadius) / (radius - fullDamageRadius);
        return Mathf.RoundToInt(Mathf.Lerp(maxDamage, minDamage, t));
    }

    void Explode()
    {
        Vector3 center = Center();
        Collider[] parts = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide);

        // Distancia más corta de cada personaje al centro, mirando solo las partes que la explosión "ve".
        var reached = new Dictionary<HealthSystem, float>();
        var pushed = new HashSet<Rigidbody>();

        foreach (Collider part in parts)
        {
            if (part.transform.IsChildOf(transform)) continue;

            HealthSystem health = part.GetComponentInParent<HealthSystem>();
            if (health != null && (health.currentHealth <= 0 || EquiposTacticos.SonAliados(thrower, health))) continue; // CA7

            Vector3 point = ClosestPoint(part, center);
            float distance = Vector3.Distance(center, point);
            if (distance > radius) continue;
            if (Blocked(center, point, health != null ? health.transform : part.transform)) continue; // CA5

            if (health != null && (!reached.TryGetValue(health, out float best) || distance < best))
                reached[health] = distance;

            // Fuerza de explosión sobre objetos con física (una sola vez por objeto)
            Rigidbody rb = part.attachedRigidbody;
            if (rb != null && rb != body && pushed.Add(rb))
                rb.AddExplosionForce(explosionForce * 10, center, radius);
        }

        // El daño se aplica una sola vez por personaje, aunque tenga varios colliders (CA4).
        foreach (KeyValuePair<HealthSystem, float> entry in reached)
        {
            int damage = DamageAt(entry.Value);
            if (damage > 0) entry.Key.TakeDamage(damage); // primero el escudo y el resto a la vida
        }

        Destroy(gameObject);
    }

    // Centro de la explosión: el de la granada (el modelo puede tener el pivote corrido).
    Vector3 Center()
    {
        if (centerCollider == null) centerCollider = GetComponentInChildren<Collider>();
        return centerCollider != null ? centerCollider.bounds.center : transform.position;
    }

    // Punto de la parte más cercano al centro. CharacterController y MeshCollider no convexo no soportan
    // Collider.ClosestPoint: el primero se trata como cápsula y el segundo con su caja.
    static Vector3 ClosestPoint(Collider part, Vector3 from)
    {
        if (part is CharacterController controller) return ClosestOnCapsule(controller, from);
        if (part is MeshCollider mesh && !mesh.convex) return part.bounds.ClosestPoint(from);
        return part.ClosestPoint(from);
    }

    static Vector3 ClosestOnCapsule(CharacterController controller, Vector3 from)
    {
        Transform t = controller.transform;
        Vector3 scale = t.lossyScale;
        float r = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float h = Mathf.Max(controller.height * Mathf.Abs(scale.y), 2f * r);
        Vector3 middle = t.TransformPoint(controller.center);
        Vector3 halfAxis = t.up * (h * 0.5f - r);
        Vector3 a = middle - halfAxis, b = middle + halfAxis;

        Vector3 axis = b - a;
        float along = axis.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(from - a, axis) / axis.sqrMagnitude) : 0f;
        Vector3 onAxis = a + axis * along;
        Vector3 away = from - onAxis;
        float distance = away.magnitude;
        if (distance <= r) return from; // el centro está adentro del cuerpo
        return onAxis + away / distance * r;
    }

    // ¿Hay una pared entre el centro y ese punto? Los triggers, la propia granada y los personajes
    // (el objetivo y cualquier otro) no cuentan como pared; sí el escenario y los colliders sólidos.
    bool Blocked(Vector3 from, Vector3 to, Transform target)
    {
        Vector3 direction = to - from;
        float distance = direction.magnitude - 0.05f; // no contar la superficie donde termina el rayo (piso, pared del objetivo)
        if (distance <= 0f) return false;

        foreach (RaycastHit hit in Physics.RaycastAll(from, direction.normalized, distance, ~0, QueryTriggerInteraction.Ignore))
        {
            Transform other = hit.collider.transform;
            if (other.IsChildOf(transform)) continue;
            if (other.IsChildOf(target)) continue;
            if (hit.collider.GetComponentInParent<HealthSystem>() != null) continue;
            return true;
        }
        return false;
    }

    // Si el collider del prefab quedó muy chico (el modelo viene escalado por la importación), lo ajusta al
    // modelo, entre 6 y 15 cm de radio: si no, a 15 m/s la granada atraviesa pisos y paredes.
    void FitCollider()
    {
        SphereCollider sphere = GetComponent<SphereCollider>();
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (sphere == null || renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);

        float world = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z), 0.06f, 0.15f);
        Vector3 scale = transform.lossyScale;
        float biggest = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        sphere.center = transform.InverseTransformPoint(bounds.center);
        sphere.radius = world / Mathf.Max(0.0001f, biggest);
    }
}
