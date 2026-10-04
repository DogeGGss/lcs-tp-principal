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

    // Cada baja de un disparo, y si fue a la cabeza: sin conexión, el aviso de baja sale de acá (US 057, CA4).
    public static event System.Action<bool> Killed;

    // Cada disparo, con quién tiró, desde dónde y la dirección de cada perdigón. El multijugador lo repite
    // en las demás computadoras (US 028).
    public static event System.Action<Transform, Vector3, Vector3[]> Fired;

    private static readonly RaycastHit[] hits = new RaycastHit[32];
    private static readonly Dictionary<HealthSystem, bool> zoned = new Dictionary<HealthSystem, bool>();
    private static readonly IComparer<RaycastHit> byDistance =
        Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

    /// <summary>Un golpe cuerpo a cuerpo que acertó: muestra el marcador de impacto y, si mató, avisa la baja.</summary>
    public static void ReportMelee(bool lethal)
    {
        if (lethal) Killed?.Invoke(false);
        Hit?.Invoke(lethal ? HitMarkerKind.Kill : HitMarkerKind.Body);
    }

    public static void Fire(ShopItem weapon, Camera camera, Transform shooter, bool aiming, float range)
    {
        if (weapon == null || camera == null) return;

        WeaponAim aim = WeaponAim.For(camera);
        Quaternion view = camera.transform.rotation * aim.RecoilRotation;
        float spread = aim.CurrentSpread(weapon, aiming);

        bool anyHit = false, head = false, kill = false;
        int pellets = Mathf.Max(1, weapon.pellets);
        Vector3[] directions = new Vector3[pellets];
        // La bala se ve: fogonazo en la boca del arma y una estela por perdigón hasta donde pegó.
        Vector3 muzzle = Trazadora.BocaLocal(camera);
        Trazadora.Fogonazo(muzzle);
        for (int i = 0; i < pellets; i++)
        {
            Vector3 direction = view * Deviation(spread) * Vector3.forward;
            directions[i] = direction;
            bool touched = Trace(camera.transform.position, direction, range, shooter, out RaycastHit hit, out HealthSystem target, out BodyZone zone);
            Trazadora.Mostrar(muzzle, touched ? hit.point : camera.transform.position + direction * Mathf.Min(range, 300f));
            if (!touched)
                continue;

            if (target == null)
            {
                ImpactMarks.Spawn(hit);
                continue;
            }

            // US 031, CA6: los compañeros de equipo no reciben daño (la bala se frena en ellos).
            if (EquiposTacticos.SonAliados(shooter, target)) continue;

            // Se mira antes de aplicar el daño: a un jugador de otra computadora el daño le llega después (US 029).
            int damage = weapon.HitDamage(zone, hit.distance);
            bool lethal = target.WouldDie(damage);
            HealthSystem.DamageOrigin = camera.transform.position; // US 192: desde dónde le dispararon
            target.TakeDamage(damage, zone == BodyZone.Head);
            anyHit = true;
            if (zone == BodyZone.Head) head = true;
            if (lethal)
            {
                kill = true;
                Killed?.Invoke(zone == BodyZone.Head);
            }
        }

        aim.Kick(weapon);
        Fired?.Invoke(shooter, camera.transform.position, directions);
        if (anyHit) Hit?.Invoke(kill ? HitMarkerKind.Kill : head ? HitMarkerKind.Head : HitMarkerKind.Body);
    }

    // El disparo de otro jugador que llega por la red (US 028): se busca dónde pega para dibujar la trazadora y
    // la marca en el escenario, sin hacer daño (el daño lo calcula quien disparó). Devuelve el punto final.
    public static Vector3 Replay(Vector3 origin, Vector3 direction, float range, Transform shooter)
    {
        if (!Trace(origin, direction, range, shooter, out RaycastHit hit, out HealthSystem target, out BodyZone _))
            return origin + direction * range;
        if (target == null) ImpactMarks.Spawn(hit);
        return hit.point;
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
