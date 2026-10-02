using System.Collections;
using UnityEngine;

public class MeleeAttack : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private MeleeWeaponHolder weaponHolder;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private AudioClip swingSound;
    private AudioSource audioSource;

    private float nextTimeToAttack = 0f;
    private bool isAttacking = false;

    // Cada ataque con el cuchillo: el multijugador lo repite en las demás computadoras (US 028).
    public static event System.Action<MeleeAttack> Swung;
    public AudioClip SwingSound => swingSound;

    // Parámetros de movimiento del cuchiloo para animación rápida por código
    private float stabDistance = 0.3f;
    private float stabDuration = 0.08f;
    private float returnDuration = 0.12f;

    private void Start()
    {
        if (weaponHolder == null)
            weaponHolder = GetComponent<MeleeWeaponHolder>();

        if (playerCamera == null)
            playerCamera = Camera.main;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }



    private void Update()
    {
        // CA1: Clic izquierdo para atacar
        if (KeyBindings.Down(GameAction.Disparar))
        {
            TryAttack();
        }
    }

    public void TryAttack()
    {
        // Verifica si hay un arma equipada
        if (weaponHolder == null || weaponHolder.CurrentWeapon == null) return;

        // Si el cuchillo está oculto porque tenés la pistola en mano, no ataca
        if (weaponHolder.CurrentViewModel == null || !weaponHolder.CurrentViewModel.activeInHierarchy) return;

        // Verifica que no esté atacando ni en cooldown
        if (isAttacking || Time.time < nextTimeToAttack) return;

        MeleeWeaponData data = weaponHolder.CurrentWeapon;
        nextTimeToAttack = Time.time + data.cooldown;

        StartCoroutine(AttackRoutine(data));
    }

    private IEnumerator AttackRoutine(MeleeWeaponData data)
    {
        isAttacking = true;
        Swung?.Invoke(this);

        // CA6: Sonido de ataque
        if (swingSound != null)
            audioSource.PlayOneShot(swingSound);

        GameObject viewModel = weaponHolder.CurrentViewModel;
        Vector3 initialPos = Vector3.zero;

        // CA6: Animación procedural en el prefab visual instanciado
        if (viewModel != null)
        {
            initialPos = viewModel.transform.localPosition;
            Vector3 targetPos = initialPos + Vector3.forward * stabDistance;

            float elapsed = 0f;
            while (elapsed < stabDuration)
            {
                if (viewModel == null) yield break;
                viewModel.transform.localPosition = Vector3.Lerp(initialPos, targetPos, elapsed / stabDuration);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        // Detección de impacto
        ExecuteHitCheck(data);

        // Regreso del arma a su posición
        if (viewModel != null)
        {
            Vector3 targetPos = initialPos + Vector3.forward * stabDistance;
            float elapsed = 0f;
            while (elapsed < returnDuration)
            {
                if (viewModel == null) yield break;
                viewModel.transform.localPosition = Vector3.Lerp(targetPos, initialPos, elapsed / returnDuration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            viewModel.transform.localPosition = initialPos;
        }

        isAttacking = false;
    }

    private void ExecuteHitCheck(MeleeWeaponData data)
    {
        // CA2: Busca todos los colliders dentro del rango del arma, con las zonas de impacto de los huesos (US 165)
        Vector3 origin = playerCamera.transform.position;
        Vector3 forward = playerCamera.transform.forward;
        Collider[] hitColliders = Physics.OverlapSphere(origin, data.range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

        HealthSystem best = null;
        float bestAngle = float.MaxValue;
        foreach (Collider col in hitColliders)
        {
            // Ignorar al propio jugador y los triggers que no son zonas de impacto (como la zona de compra)
            if (col.transform.root == transform.root) continue;
            if (col.isTrigger && col.GetComponent<HitZone>() == null) continue;

            HealthSystem health = col.GetComponentInParent<HealthSystem>();
            if (health == null || health.currentHealth <= 0 || EquiposTacticos.SonAliados(this, health)) continue; // US 031, CA6

            // CA2: Comprobación del cono angular (attackAngle), hasta el punto más cercano del collider y no hasta su
            // centro: así un rival parado entra en el cono aunque su centro quede más abajo que los ojos.
            Vector3 toTarget = ClosestPoint(col, origin) - origin;
            float distance = toTarget.magnitude;
            float angleToTarget = distance < 0.001f ? 0f : Vector3.Angle(forward, toTarget);
            if (angleToTarget > data.attackAngle / 2f || angleToTarget >= bestAngle) continue;

            // Verificar que no haya una pared en el medio (sin contar triggers, como las zonas de impacto de US 165)
            if (distance >= 0.001f && Physics.Raycast(origin, toTarget / distance, out RaycastHit hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<HealthSystem>() != health && hit.collider.transform.root != transform.root) continue;

            best = health;
            bestAngle = angleToTarget;
        }

        // CA3: Aplicar daño al HealthSystem. Golpea a un solo objetivo por ataque: el más cercano a la mira.
        if (best != null)
        {
            // Se mira antes de aplicar el daño (a un jugador de otra computadora le llega después), para el marcador
            // de impacto y el aviso de baja: antes las bajas con cuchillo no salían en los avisos sin conexión.
            bool lethal = best.WouldDie(data.damage);
            best.TakeDamage(data.damage);
            WeaponFire.ReportMelee(lethal);
            Debug.Log($"Golpe cuerpo a cuerpo a {best.name}. Daño: {data.damage}");
        }
    }

    // ClosestPoint solo sirve con cajas, esferas, cápsulas y mallas convexas; para el resto alcanza con su caja.
    private static Vector3 ClosestPoint(Collider col, Vector3 point)
    {
        if (col is MeshCollider mesh && !mesh.convex) return col.bounds.ClosestPoint(point);
        return col.ClosestPoint(point);
    }
}