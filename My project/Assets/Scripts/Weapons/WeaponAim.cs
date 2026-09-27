using UnityEngine;

// Puntería del jugador (US 167 y US 168). Va en la cámara del jugador (se agrega sola la primera vez
// que se dispara) y lleva dos cosas:
// - Retroceso: cada disparo sube la mira según el patrón del arma (ficha). Al dejar de disparar vuelve
//   de a poco por el mismo camino, y si se vuelve a disparar antes, el patrón sigue desde donde quedó.
//   Se suma encima de la rotación de CameraLook, así el jugador puede compensarlo con el mouse.
// - Dispersión: el desvío máximo de cada bala según si el jugador está quieto, moviéndose, en el aire,
//   agachado o apuntando.
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Camera))]
public class WeaponAim : MonoBehaviour
{
    public const float MovingSpeed = 1f;       // m/s desde los que cuenta como "en movimiento"
    public const float StopTime = 0.15f;       // al frenar, vuelve al valor "quieto" en este tiempo
    public const float AirMultiplier = 4f;
    public const float CrouchMultiplier = 0.75f;
    public const float AimMultiplier = 0.7f;

    private CharacterController controller;
    private PlayerMovement movement;
    private ShopItem pattern;      // arma del último disparo
    private float heat;            // balas seguidas; baja al recuperar
    private float recoverRate;     // balas por segundo que se recuperan
    private float lastShotTime = -10f;
    private float moveFactor;      // 0 = quieto, 1 = en movimiento
    private Quaternion applied = Quaternion.identity;

    public float MoveFactor => moveFactor;
    public bool Grounded => controller == null || controller.isGrounded;

    // Agachado: PlayerMovement achica el CharacterController mientras está agachado.
    public bool Crouching => movement != null && controller != null
        && controller.height < (movement.standingHeight + movement.crounchHeight) * 0.5f;

    // Retroceso actual de la mira (hacia dónde sale la próxima bala respecto de donde apunta el jugador).
    public Quaternion RecoilRotation
    {
        get
        {
            if (pattern == null) return Quaternion.identity;
            Vector2 offset = PatternOffset(pattern, heat) * (Crouching ? CrouchMultiplier : 1f);
            return Quaternion.Euler(-offset.x, offset.y, 0f);
        }
    }

    public static WeaponAim For(Camera camera)
    {
        WeaponAim aim = camera.GetComponent<WeaponAim>();
        return aim != null ? aim : camera.gameObject.AddComponent<WeaponAim>();
    }

    private void Awake()
    {
        controller = GetComponentInParent<CharacterController>();
        movement = GetComponentInParent<PlayerMovement>();
    }

    private void Update()
    {
        // Saca el retroceso del cuadro anterior: los demás scripts ven la mira "limpia".
        transform.localRotation *= Quaternion.Inverse(applied);
        applied = Quaternion.identity;

        float speed = 0f;
        if (controller != null)
        {
            Vector3 velocity = controller.velocity;
            velocity.y = 0f;
            speed = velocity.magnitude;
        }
        moveFactor = speed > MovingSpeed ? 1f : Mathf.MoveTowards(moveFactor, 0f, Time.deltaTime / StopTime);

        // Recuperación: pasado el tiempo entre disparos, el patrón vuelve hacia el principio.
        if (pattern != null && heat > 0f && Time.time - lastShotTime > ShotInterval(pattern))
            heat = Mathf.Max(0f, heat - recoverRate * Time.deltaTime);
    }

    private void LateUpdate()
    {
        applied = RecoilRotation;
        transform.localRotation *= applied;
    }

    private void OnDisable()
    {
        transform.localRotation *= Quaternion.Inverse(applied);
        applied = Quaternion.identity;
    }

    // Se llama después de cada disparo: la mira sube un paso del patrón.
    public void Kick(ShopItem weapon)
    {
        if (weapon != pattern)
        {
            pattern = weapon;
            heat = 0f;
        }
        heat += 1f;
        lastShotTime = Time.time;
        recoverRate = heat / Mathf.Max(0.01f, weapon.recoilRecovery);
    }

    // Desvío máximo de la próxima bala, en grados (US 167).
    public float CurrentSpread(ShopItem weapon, bool aiming)
    {
        return SpreadFor(weapon, moveFactor, Grounded, Crouching, aiming);
    }

    public static float SpreadFor(ShopItem weapon, float moveFactor, bool grounded, bool crouching, bool aiming)
    {
        // Los francotiradores sin apuntar usan el valor "en movimiento".
        bool sniperHip = weapon.category == ShopCategory.Snipers && !aiming;
        float spread = sniperHip ? weapon.spread.y : Mathf.Lerp(weapon.spread.x, weapon.spread.y, moveFactor);
        if (!grounded) spread *= AirMultiplier;
        if (crouching) spread *= CrouchMultiplier;
        if (aiming) spread *= AimMultiplier;
        return spread;
    }

    // Retroceso del patrón (x = sube, y = hacia la derecha), en grados, después de "shots" balas seguidas.
    // La primera bala siempre sale exacta; con recoilExactShots = 2, también la segunda.
    public static Vector2 PatternOffset(ShopItem weapon, float shots)
    {
        if (weapon.recoilKick <= 0f) return Vector2.zero;
        float exact = Mathf.Max(1, weapon.recoilExactShots);
        float climbShots = Mathf.Max(0f, shots - exact + 1f);
        float max = weapon.recoilMaxClimb > 0f ? weapon.recoilMaxClimb : float.MaxValue;
        float pitch = Mathf.Min(climbShots * weapon.recoilKick, max);

        // Arriba de todo, zigzag de un lado al otro como una víbora (US 168, CA4).
        float yaw = 0f;
        if (weapon.recoilSway > 0f && weapon.recoilMaxClimb > 0f)
        {
            float past = climbShots - weapon.recoilMaxClimb / weapon.recoilKick;
            if (past > 0f)
            {
                float perShot = 2f * weapon.recoilSway / Mathf.Max(1, weapon.recoilSwayShots);
                yaw = Mathf.PingPong(past * perShot + weapon.recoilSway, 2f * weapon.recoilSway) - weapon.recoilSway;
            }
        }
        return new Vector2(pitch, yaw);
    }

    private static float ShotInterval(ShopItem weapon)
    {
        return (weapon.fireRate > 0f ? 1f / weapon.fireRate : 0.2f) + 0.05f;
    }
}
