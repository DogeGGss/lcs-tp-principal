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
        if (Input.GetMouseButtonDown(0))
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
        // CA2: Busca todos los colliders dentro del rango del arma
        Collider[] hitColliders = Physics.OverlapSphere(playerCamera.transform.position, data.range);

        foreach (Collider col in hitColliders)
        {
            // Ignorar al propio jugador
            if (col.transform.root == transform.root) continue;

            // CA2: Comprobación del cono angular (attackAngle)
            Vector3 directionToTarget = (col.bounds.center - playerCamera.transform.position).normalized;
            float angleToTarget = Vector3.Angle(playerCamera.transform.forward, directionToTarget);

            if (angleToTarget <= data.attackAngle / 2f)
            {
                // Verificar que no haya una pared en el medio
                if (Physics.Raycast(playerCamera.transform.position, directionToTarget, out RaycastHit hit, data.range))
                {
                    if (hit.collider == col)
                    {
                        // CA3: Aplicar daño al HealthSystem[cite: 1]
                        HealthSystem health = col.GetComponent<HealthSystem>();
                        if (health != null)
                        {
                            health.TakeDamage(data.damage);
                            Debug.Log($"Golpe cuerpo a cuerpo a {col.name}. Daño: {data.damage}");
                            break; // Golpea a un solo objetivo por ataque
                        }
                    }
                }
            }
        }
    }
}