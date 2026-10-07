using System.Collections.Generic;
using UnityEngine;

// Granadas en la mano del jugador (US 079; la de humo es la US 074): elegirlas con la tecla 4 (CA1), lanzarlas con el clic izquierdo (CA2)
// y gastarlas al lanzar (CA6). Las que tiene salen de PlayerLoadout (lo que compró en la tienda).
// WeaponSwitcher lo agrega solo al jugador si no está: no hace falta tocar la escena.
public class GrenadeThrower : MonoBehaviour
{
    [Tooltip("Prefab de la granada (con Grenade1). Si está vacío se usa el de la ficha (ShopItem.grenadePrefab).")]
    public GameObject grenadePrefab;
    [Tooltip("Opcional: de dónde sale la granada. Si está vacío sale de adelante de la cámara.")]
    public Transform grenadeSpawnPoint;
    [Tooltip("Opcional: si está vacía se usa la Main Camera.")]
    public Camera playerCamera;

    [Header("Lanzamiento")]
    [Tooltip("Grados hacia arriba respecto de donde mira, para que haga arco.")]
    public float upwardAngle = 6f;
    [Tooltip("A qué distancia de la cámara aparece (m). Si hay una pared antes, aparece pegada a ella.")]
    public float spawnDistance = 0.6f;

    [Header("Modelo en la mano")]
    [Tooltip("Dónde se ve la granada en la mano, relativo a la cámara (x derecha, y arriba, z adelante). Solo si la cámara no tiene el objeto \"Mano con granada\".")]
    public Vector3 handPosition = new Vector3(0.28f, -0.22f, 0.55f);
    [Tooltip("Tamaño con el que se ve en la mano (m), con la \"Mano con granada\" a escala 1.")]
    public float handSize = 0.12f;

    // US 173: hijo de la cámara donde va la granada en la mano, con los agarres de las manos (BrazosEnCamara). Las tres
    // granadas se arman ahí, centradas y del tamaño de handSize por su escala: se ajusta una vez para todas.
    public const string NombreMano = "Mano con granada";

    private PlayerLoadout loadout;
    private Transform owner;
    private HealthSystem ownerHealth;
    private WeaponSwitcher switcher;
    private ShopItem selected;
    private GameObject heldModel;

    // Qué granada tiene en la mano (null si no hay ninguna).
    public bool IsHolding => selected != null;
    public ShopItem Selected => selected;

    // Se quedó sin la granada que tenía en la mano (la lanzó, la vendió o murió): hay que volver a un arma.
    public event System.Action Emptied;

    // Cada granada que se lanza, con quién la tiró: el multijugador la repite en las demás computadoras.
    public static event System.Action<Transform, Grenade1> Thrown;

    void Awake()
    {
        loadout = GetComponentInParent<PlayerLoadout>();
        PlayerMovement movement = GetComponentInParent<PlayerMovement>();
        owner = movement != null ? movement.transform : transform.root;
        ownerHealth = owner.GetComponent<HealthSystem>();
        switcher = GetComponentInParent<WeaponSwitcher>();
    }

    void Start()
    {
        if (loadout != null) loadout.Changed += OnLoadoutChanged;
    }

    void OnDestroy()
    {
        if (loadout != null) loadout.Changed -= OnLoadoutChanged;
        DestroyHeldModel();
    }

    // CA1: saca la primera granada que tenga; si ya tiene una en la mano, pasa a la siguiente distinta.
    // Devuelve false si no tiene ninguna (CA6: la tecla 4 no selecciona nada).
    public bool SelectNext()
    {
        if (loadout == null) return false;
        List<ShopItem> owned = loadout.OwnedGrenades();
        if (owned.Count == 0) return false;

        int index = selected != null ? owned.IndexOf(selected) : -1;
        SetSelected(owned[(index + 1) % owned.Count]);
        return true;
    }

    // Se guarda la granada (se eligió otra arma).
    public void Deselect() => SetSelected(null);

    void Update()
    {
        if (selected == null) return;
        if (PauseMenu.IsPaused || ShopUI.IsOpen) return; // el clic de la tienda no tira granadas
        if (ownerHealth != null && ownerHealth.currentHealth <= 0) return;

        if (KeyBindings.Down(GameAction.Disparar)) Throw(); // CA2
    }

    void Throw()
    {
        ShopItem item = selected;
        GameObject prefab = grenadePrefab != null ? grenadePrefab : item.grenadePrefab;
        if (prefab == null || prefab.GetComponent<Grenade1>() == null)
        {
            Debug.LogWarning("GrenadeThrower: la granada no tiene prefab con Grenade1 (revisá la ficha de la granada).");
            return;
        }

        Camera cam = ViewCamera();
        if (cam == null || loadout == null) return;
        if (!loadout.Consume(item)) return; // CA6: se gasta al lanzarla

        Vector3 forward = cam.transform.forward;
        Vector3 direction = Quaternion.AngleAxis(-upwardAngle, cam.transform.right) * forward;

        Vector3 origin;
        if (grenadeSpawnPoint != null) origin = grenadeSpawnPoint.position;
        else
        {
            // Si hay una pared pegada, que aparezca antes de atravesarla.
            float distance = spawnDistance;
            if (Physics.Raycast(cam.transform.position, forward, out RaycastHit wall, spawnDistance + 0.1f, ~0, QueryTriggerInteraction.Ignore)
                && !wall.collider.transform.IsChildOf(owner))
                distance = Mathf.Max(0.05f, wall.distance - 0.1f);
            origin = cam.transform.position + forward * distance;
        }

        GameObject thrown = Instantiate(prefab, origin, Quaternion.identity);
        Grenade1 grenade = thrown.GetComponent<Grenade1>();
        grenade.thrower = owner; // quién la tira (US 031, CA7)
        grenade.Configure(item);  // mecha, alcance y daño salen de la ficha

        CharacterController body = owner.GetComponent<CharacterController>();
        grenade.Throw(direction, body != null ? body.velocity : Vector3.zero);
        Thrown?.Invoke(owner, grenade);
    }

    // La que tenía en la mano se acabó (la lanzó, la vendió, la deshizo en la tienda o murió).
    void OnLoadoutChanged()
    {
        if (selected == null || loadout.Count(selected) > 0) return;
        SetSelected(null);
        Emptied?.Invoke();
    }

    void SetSelected(ShopItem item)
    {
        selected = item;
        DestroyHeldModel();
        if (item == null) return;

        // Si el WeaponSwitcher tiene su propio objeto de granada en la mano, ese se usa.
        if (switcher != null && switcher.grenadeObj != null) return;

        GameObject prefab = grenadePrefab != null ? grenadePrefab : item.grenadePrefab;
        Camera cam = ViewCamera();
        if (prefab == null || cam == null) return;
        heldModel = BuildHeldModel(prefab, cam);
    }

    // Copia solo visual del modelo (sin física ni scripts), centrada y a tamaño fijo en la mano.
    GameObject BuildHeldModel(GameObject prefab, Camera cam)
    {
        GameObject model = Instantiate(prefab);
        model.name = "GranadaEnMano";

        Grenade1 logic = model.GetComponent<Grenade1>();
        if (logic != null) logic.enabled = false;
        foreach (Collider c in model.GetComponentsInChildren<Collider>()) c.enabled = false;
        Rigidbody rb = model.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        Transform mano = cam.transform.Find(NombreMano);
        model.transform.SetParent(mano != null ? mano : cam.transform, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        // Como las demás armas en la mano: en su capa la dibuja la cámara de las armas, así no atraviesa las paredes.
        int handLayer = LayerMask.NameToLayer("ArmaEnMano");
        if (handLayer >= 0)
            foreach (Transform part in model.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = handLayer;

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return model;

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
        float biggest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        float size = handSize * (mano != null ? mano.lossyScale.x / cam.transform.lossyScale.x : 1f);
        if (biggest > 0.0001f) model.transform.localScale *= size / biggest;

        bounds = renderers[0].bounds;
        foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
        Vector3 center = mano != null ? mano.position : cam.transform.TransformPoint(handPosition);
        model.transform.position += center - bounds.center;
        return model;
    }

    /// <summary>
    /// Espectador (US 133): un modelo de la granada en la mano, igual al que ve el que la sostiene, sin tocar la que
    /// tiene este jugador. Lo destruye el que lo pide.
    /// </summary>
    public GameObject BuildPreview(ShopItem item)
    {
        GameObject prefab = grenadePrefab != null ? grenadePrefab : item != null ? item.grenadePrefab : null;
        Camera cam = ViewCamera();
        return prefab != null && cam != null ? BuildHeldModel(prefab, cam) : null;
    }

    void DestroyHeldModel()
    {
        if (heldModel != null) Destroy(heldModel);
        heldModel = null;
    }

    Camera ViewCamera()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        return playerCamera;
    }
}
