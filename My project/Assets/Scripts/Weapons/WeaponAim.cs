using UnityEngine;

// Puntería del jugador (US 167 y US 168). Va en la cámara del jugador (se agrega sola la primera vez
// que se dispara) y lleva dos cosas:
// - Retroceso: cada disparo sube la mira según el patrón del arma (ficha). La mira va hacia el patrón en
//   unos milisegundos en vez de saltar, y cada tiro le suma un sacudón que sube y vuelve solo. Al dejar
//   de disparar vuelve de a poco por el mismo camino, y si se vuelve a disparar antes, el patrón sigue
//   desde donde quedó. Se suma encima de la rotación de CameraLook, así el jugador puede compensarlo con
//   el mouse.
// - Dispersión: el desvío máximo de cada bala según si el jugador está quieto, moviéndose, en el aire,
//   agachado o apuntando, más lo que suma tirar seguido.
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Camera))]
public class WeaponAim : MonoBehaviour
{
    public const float MovingSpeed = 1f;       // m/s desde los que cuenta como "en movimiento"
    public const float StopTime = 0.15f;       // al frenar, vuelve al valor "quieto" en este tiempo
    public const float AirMultiplier = 4f;
    public const float CrouchMultiplier = 0.75f;
    public const float AimMultiplier = 0.7f;
    public const float FollowTime = 0.04f;     // la mira sigue al patrón en este tiempo, sin saltos
    public const float PunchRise = 0.015f;     // el sacudón de cada tiro sube en este tiempo...
    public const float PunchReturn = 0.07f;    // ...y vuelve en este
    public const float PunchSide = 0.3f;       // parte del sacudón que va de costado, al azar

    private CharacterController controller;
    private PlayerMovement movement;
    private ShopItem pattern;      // arma del último disparo
    private float heat;            // balas seguidas; baja al recuperar
    private float recoverRate;     // balas por segundo que se recuperan
    private float lastShotTime = -10f;
    private float moveFactor;      // 0 = quieto, 1 = en movimiento
    private Vector2 shown;         // retroceso del patrón que se ve (x = sube, y = hacia la derecha)
    private Vector2 punch;         // sacudón actual
    private Vector2 punchTarget;   // hacia dónde va el sacudón; vuelve a cero solo
    private Quaternion applied = Quaternion.identity;

    public float MoveFactor => moveFactor;
    public bool Grounded => controller == null || controller.isGrounded;

    // Agachado: PlayerMovement achica el CharacterController mientras está agachado.
    public bool Crouching => movement != null && controller != null
        && controller.height < (movement.standingHeight + movement.crounchHeight) * 0.5f;

    // Retroceso actual de la mira (hacia dónde sale la próxima bala respecto de donde apunta el jugador).
    // Es el mismo que se ve en pantalla, así las balas van a donde apunta la mira (US 168, CA1).
    public Quaternion RecoilRotation => Quaternion.Euler(-(shown.x + punch.x), shown.y + punch.y, 0f);

    // Hacia dónde tiene que ir la mira según el patrón y las balas seguidas.
    private Vector2 TargetOffset => pattern == null
        ? Vector2.zero
        : PatternOffset(pattern, heat) * (Crouching ? CrouchMultiplier : 1f);

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
        // La mira va hacia el patrón de a poco en vez de saltar, y el sacudón sube rápido y vuelve solo.
        float dt = Time.deltaTime;
        shown = Vector2.Lerp(shown, TargetOffset, 1f - Mathf.Exp(-dt / FollowTime));
        punchTarget *= Mathf.Exp(-dt / PunchReturn);
        punch = Vector2.Lerp(punch, punchTarget, 1f - Mathf.Exp(-dt / PunchRise));

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

        // Sacudón del tiro: sube (y un poco de costado, al azar) y vuelve solo. Tirando rápido se van
        // sumando, con un tope de dos sacudones.
        if (weapon.recoilPunch > 0f)
        {
            float kick = weapon.recoilPunch * (Crouching ? CrouchMultiplier : 1f);
            punchTarget += new Vector2(kick, Random.Range(-PunchSide, PunchSide) * kick);
            punchTarget = Vector2.ClampMagnitude(punchTarget, 2f * kick);
        }
    }

    // Desvío máximo de la próxima bala, en grados (US 167).
    public float CurrentSpread(ShopItem weapon, bool aiming)
    {
        return SpreadFor(weapon, moveFactor, Grounded, Crouching, aiming, SpamSpread(weapon));
    }

    // Dispersión extra por tirar seguido: el primer tiro sale sin extra, cada tiro seguido suma y se va
    // con la recuperación del retroceso. Así tirar de a un tiro es más preciso que tirar rápido.
    private float SpamSpread(ShopItem weapon)
    {
        if (weapon != pattern || weapon.spamSpread <= 0f) return 0f;
        float extra = heat * weapon.spamSpread;
        return weapon.spamSpreadMax > 0f ? Mathf.Min(extra, weapon.spamSpreadMax) : extra;
    }

    public static float SpreadFor(ShopItem weapon, float moveFactor, bool grounded, bool crouching, bool aiming, float extra = 0f)
    {
        // Los francotiradores sin apuntar usan el valor "en movimiento".
        bool sniperHip = weapon.category == ShopCategory.Snipers && !aiming;
        float spread = (sniperHip ? weapon.spread.y : Mathf.Lerp(weapon.spread.x, weapon.spread.y, moveFactor)) + extra;
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

    // En las de ráfaga cuenta el tiempo entre balas de la ráfaga: la mira vuelve entre una ráfaga y la
    // siguiente (US 069, CA5).
    private static float ShotInterval(ShopItem weapon)
    {
        float rate = weapon.burstCount > 1 && weapon.burstRate > 0f ? weapon.burstRate : weapon.fireRate;
        return (rate > 0f ? 1f / rate : 0.2f) + 0.05f;
    }
}
