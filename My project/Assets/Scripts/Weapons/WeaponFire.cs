using System.Collections.Generic;
using UnityEngine;

public enum HitMarkerKind { Body, Head, Kill }

// Núcleo de disparo (US 165, US 167 y US 168): todas las armas de fuego disparan por acá.
// Un disparo tira un rayo por perdigón desde donde apunta la mira (con el retroceso), cada uno con su
// propia dispersión; busca la zona tocada, hace el daño de la ficha según zona y distancia, deja la
// marca en el escenario (US 170) y avisa al marcador de impacto una sola vez por disparo.
// Los números salen de la ficha del arma (ShopItem), no del código.
public static class WeaponFire
{
    // Un disparo que pegó: el HUD muestra el marcador de impacto (US 165).
    public static event System.Action<HitMarkerKind> Hit;

    private static readonly RaycastHit[] hits = new RaycastHit[32];
    private static readonly Dictionary<HealthSystem, bool> zoned = new Dictionary<HealthSystem, bool>();
    private static readonly IComparer<RaycastHit> byDistance =
        Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

    public static void Fire(ShopItem weapon, Camera camera, Transform shooter, bool aiming, float range)
    {
        if (weapon == null || camera == null) return;

        WeaponAim aim = WeaponAim.For(camera);
        Quaternion view = camera.transform.rotation * aim.RecoilRotation;
        float spread = aim.CurrentSpread(weapon, aiming);

        bool anyHit = false, head = false, kill = false;
        int pellets = Mathf.Max(1, weapon.pellets);
        for (int i = 0; i < pellets; i++)
        {
            Vector3 direction = view * Deviation(spread) * Vector3.forward;
            if (!Trace(camera.transform.position, direction, range, shooter, out RaycastHit hit, out HealthSystem target, out BodyZone zone))
                continue;

            if (target == null)
            {
                ImpactMarks.Spawn(hit);
                continue;
            }

            bool wasAlive = target.currentHealth > 0;
            target.TakeDamage(weapon.HitDamage(zone, hit.distance));
            anyHit = true;
            if (zone == BodyZone.Head) head = true;
            if (wasAlive && target.currentHealth <= 0) kill = true;
        }

        aim.Kick(weapon);
        if (anyHit) Hit?.Invoke(kill ? HitMarkerKind.Kill : head ? HitMarkerKind.Head : HitMarkerKind.Body);
    }

    // Ficha del arma para un jugador: la del catálogo de su tienda con ese alias (por ejemplo, "Mitre").
    public static ShopItem FindInCatalog(Component from, string alias)
    {
        PlayerLoadout loadout = from.GetComponentInParent<PlayerLoadout>();
        if (loadout == null || loadout.Catalog == null) return null;
        foreach (ShopItem item in loadout.Catalog.items)
            if (item != null && item.alias == alias) return item;
        return null;
    }

    // Desvío al azar dentro de un cono de "degrees" grados alrededor de la mira (US 167).
    private static Quaternion Deviation(float degrees)
    {
        if (degrees <= 0f) return Quaternion.identity;
        Vector2 p = Random.insideUnitCircle * degrees;
        return Quaternion.Euler(-p.y, p.x, 0f);
    }

    // Lo primero que toca la bala. Se saltea:
    // - lo del propio tirador (sus zonas y su cápsula: nunca se daña a sí mismo, CA10 de US 165);
    // - las zonas invisibles (triggers que no son zonas de impacto, como la zona de compra);
    // - la cápsula de movimiento de un personaje que tiene zonas: las balas pegan en las zonas.
    // Un personaje sin zonas cuenta como cuerpo (CA8 de US 165).
    private static bool Trace(Vector3 origin, Vector3 direction, float range, Transform shooter,
        out RaycastHit best, out HealthSystem target, out BodyZone zone)
    {
        best = default;
        target = null;
        zone = BodyZone.Body;

        int count = Physics.RaycastNonAlloc(origin, direction, hits, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, 0, count, byDistance);
        for (int i = 0; i < count; i++)
        {
            Collider collider = hits[i].collider;
            if (shooter != null && collider.transform.IsChildOf(shooter)) continue;

            HitZone hitZone = collider.GetComponent<HitZone>();
            if (collider.isTrigger && hitZone == null) continue;

            HealthSystem health = collider.GetComponentInParent<HealthSystem>();
            if (health != null && hitZone == null && HasZones(health)) continue;

            best = hits[i];
            target = health;
            zone = hitZone != null ? hitZone.zone : BodyZone.Body;
            return true;
        }
        return false;
    }

    private static bool HasZones(HealthSystem health)
    {
        if (!zoned.TryGetValue(health, out bool has))
        {
            if (zoned.Count > 256) zoned.Clear(); // personajes que ya no existen (zombis muertos, cambio de escena)
            has = health.GetComponentInChildren<HitZone>(true) != null;
            zoned[health] = has;
        }
        return has;
    }
}
