using UnityEngine;

public class HealthSystem : MonoBehaviour
{
    public int maxHealth = 100;
    public int currentHealth;

    public int maxShield = 50;
    public int currentShield;

    public event System.Action Died;

    // Multijugador (US 029): en la copia de otro jugador el daño no se aplica acá, se le manda a su dueño,
    // que es el que decide su vida. Mientras es invulnerable (al reaparecer, US 030) no recibe daño.
    // El bool dice si fue a la cabeza, para el aviso de baja (US 057, CA4).
    public System.Action<int, bool> DamageRedirect;
    public bool Invulnerable;

    // Si el golpe que lo dejó en 0 fue a la cabeza: el aviso de baja lo marca (US 057, CA4).
    public bool KilledByHeadshot { get; private set; }

    // Con qué se está haciendo el daño en este momento, para el aviso de baja (por ejemplo, la granada de metralla,
    // US 073). Vacío: el arma que el atacante tiene en la mano. Lo pone quien hace el daño justo antes de TakeDamage.
    public static string DamageSource;

    // Desde dónde viene el daño (US 192): el lugar del que disparó o donde explotó la granada. Lo pone quien hace el
    // daño justo antes de TakeDamage y se borra al usarlo. Sin lugar, el indicador de dirección no muestra nada.
    public static Vector3? DamageOrigin;

    // Avisa cada golpe que de verdad le sacó escudo o vida a alguien: a quién, cuánto y desde dónde (US 192).
    public static event System.Action<HealthSystem, int, Vector3?> Damaged;

    void Start()
    {
        currentHealth = maxHealth;
        currentShield = 0;
    }

    // Si este daño lo deja en 0 (lo usa el marcador de impacto para mostrar la baja, US 165).
    public bool WouldDie(int damageAmount)
    {
        return currentHealth > 0 && !Invulnerable && damageAmount >= currentHealth + currentShield;
    }

    // Vida y escudo que manda el dueño de este jugador (multijugador, US 029).
    public void SetState(int health, int shield)
    {
        currentHealth = Mathf.Clamp(health, 0, maxHealth);
        currentShield = Mathf.Clamp(shield, 0, maxShield);
    }

    // Reaparición (US 030): vida completa, sin escudo y con el movimiento de vuelta.
    public void Revive()
    {
        currentHealth = maxHealth;
        currentShield = 0;
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null) movement.enabled = true;
    }

    public void AddShield(int shieldAmount)
    {
        currentShield += shieldAmount;
        if (currentShield > maxShield)
        {
            currentShield = maxShield;
        }
        Debug.Log("Escudo adquirido. Escudo actual: " + currentShield);
    }

    // Deja el escudo en un valor exacto (la tienda lo usa al comprar, vender o deshacer).
    public void SetShield(int value)
    {
        currentShield = Mathf.Clamp(value, 0, maxShield);
    }

    // head: el golpe fue a la cabeza (zona de impacto, US 165).
    public void TakeDamage(int damageAmount, bool head = false)
    {
        Vector3? origin = DamageOrigin;
        if (DamageRedirect != null)
        {
            DamageRedirect(damageAmount, head); // lee DamageOrigin para mandarlo por la red
            DamageOrigin = null;
            return;
        }
        DamageOrigin = null;
        if (currentHealth <= 0 || Invulnerable) return;
        if (damageAmount > 0) Damaged?.Invoke(this, damageAmount, origin);

        if (currentShield > 0)
        {
            if (damageAmount <= currentShield)
            {
                currentShield -= damageAmount;
                damageAmount = 0;
            }
            else
            {
                damageAmount -= currentShield;
                currentShield = 0;
            }
        }

        // El daño nunca deja la vida por debajo de 0 (US 029, CA4).
        if (damageAmount > 0)
        {
            currentHealth = Mathf.Max(0, currentHealth - damageAmount);
        }

        Debug.Log("Recibes danio. Escudo: " + currentShield + " | Vida: " + currentHealth);

        if (currentHealth <= 0)
        {
            KilledByHeadshot = head;
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("El personaje ha muerto (0 HP).");
        Died?.Invoke();
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.enabled = false;
        }
    }
}