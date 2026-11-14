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

    // Cada perdigón que pega en un personaje, con el daño y la zona, justo antes de hacerle el daño (el campo de
    // práctica lo muestra arriba de sus muñecos, US 120).
    public static event System.Action<HealthSystem, int, BodyZone> Impacto;

    // Cada disparo, con quién tiró, desde dónde y la dirección de cada perdigón. El multijugador lo repite
    // en las demás computadoras (US 028).
    public static event System.Action<Transform, Vector3, Vector3[]> Fired;
    // Daño de más para el disparo que sale ahora (la carta Bronca de Invasión: US 330): se pide una vez por disparo y
    // se reparte entre los perdigones.
    public static System.Func<int> DanioDeMas;

    /// <summary>Un disparo de esa arma le pegó a alguien (US 321, Acople: cuenta los aciertos de pistola).</summary>
    public static event System.Action<ShopItem> Acerto;

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

    /// <summary>US 325, Mira abierta: true si este disparo sale sin dispersión (el arma, si tiene la mira puesta).</summary>
    public static System.Func<ShopItem, bool, bool> SinDispersion;

    /// <summary>US 319, Bala compacta: true si los perdigones de este disparo salen juntos en un solo proyectil sin
    /// dispersión, con el mismo daño total (el arma, si está apuntando).</summary>
    public static System.Func<ShopItem, bool, bool> BalaCompacta;

    /// <summary>La dispersión con la que saldría el próximo disparo (para la mira dinámica del HUD): 0 si una carta lo
    /// hace salir sin dispersión (US 325, Mira abierta; US 319, Bala compacta).</summary>
    public static float DispersionDelDisparo(ShopItem weapon, WeaponAim aim, bool aiming)
    {
        if (SinDispersion != null && SinDispersion(weapon, aiming)) return 0f;
        if (weapon.pellets > 1 && BalaCompacta != null && BalaCompacta(weapon, aiming)) return 0f;
        return aim.CurrentSpread(weapon, aiming);
    }

    /// <summary>cartuchos: cuántos cartuchos salen juntos (US 309, Doble cartucho: 2; los perdigones se multiplican).
    /// balasJuntas: cuántas balas valen en un solo impacto (US 323, Disparo concentrado: el daño se multiplica).</summary>
    public static void Fire(ShopItem weapon, Camera camera, Transform shooter, bool aiming, float range, int cartuchos = 1, int balasJuntas = 1)
    {
        if (weapon == null || camera == null) return;

        WeaponAim aim = WeaponAim.For(camera);
        Quaternion view = camera.transform.rotation * aim.RecoilRotation;
        float spread = aim.CurrentSpread(weapon, aiming);
        if (SinDispersion != null && SinDispersion(weapon, aiming)) spread = 0f; // US 325

        bool anyHit = false, head = false, kill = false;
        cartuchos = Mathf.Max(1, cartuchos);
        int pellets = Mathf.Max(1, weapon.pellets) * cartuchos;
        int porPerdigon = Mathf.Max(1, balasJuntas); // cuántos perdigones (o balas) vale cada uno (US 319 y US 323)
        if (pellets > 1 && BalaCompacta != null && BalaCompacta(weapon, aiming))
        {
            porPerdigon *= pellets;
            pellets = 1;
            spread = 0f;
        }
        Vector3[] directions = new Vector3[pellets];
        // La bala se ve: fogonazo en la boca del arma y una estela por perdigón hasta donde pegó.
        Vector3 muzzle = Trazadora.BocaLocal(camera);
        if (MinijuegoCometas.Activo)
        {
            Trazadora.Fogonazo(muzzle);
            FirePractice(weapon, camera, aim, view, spread, pellets, muzzle, range);
            return;
        }
        int deMas = DanioDeMas != null ? Mathf.Max(0, DanioDeMas()) : 0; // US 330
        bool conBronca = deMas > 0; // US 330: el disparo con bronca se ve rojo
        // F35 (US 381): con una skin legendaria, su fogonazo y su estela (la bronca y la descarga tienen prioridad).
        CatalogoDeSkins.Efectos legendaria = conBronca || Trazadora.Tinte.HasValue ? null : SkinsEnLasArmas.EfectosDe(weapon);
        if (legendaria == null) Trazadora.Fogonazo(muzzle, conBronca);
        Vector3[] finales = legendaria != null ? new Vector3[pellets] : null; // (sus estelas salen todas juntas, al final)
        GameObject conSkin = SkinsEnLasArmas.ArmaDe(weapon);
        CalorDeSkin.Disparo(conSkin); // el fuego de la skin se enciende con cada tiro
        int deMasPorPerdigon = deMas / pellets, deMasSobra = deMas - deMasPorPerdigon * pellets;
        for (int i = 0; i < pellets; i++)
        {
            Vector3 direction = view * Deviation(spread) * Vector3.forward;
            directions[i] = direction;
            bool touched = Trace(camera.transform.position, direction, range, shooter, out RaycastHit hit, out HealthSystem target, out BodyZone zone);
            Vector3 fin = touched ? hit.point : camera.transform.position + direction * Mathf.Min(range, 300f);
            if (legendaria != null) finales[i] = fin;
            else Trazadora.Mostrar(muzzle, fin, conBronca);
            if (!touched)
                continue;

            if (target == null)
            {
                // US 020, CA5: la Barrera frena la bala; si el que tira es rival, le hace el daño de cuerpo.
                Barrera barrera = Barrera.De(hit.collider);
                if (barrera != null)
                {
                    barrera.Chispas(hit.point, hit.normal);
                    barrera.PedirDanio(weapon.HitDamage(BodyZone.Body, hit.distance), shooter, hit.point);
                }
                // US 022, CA9: el perro del Callejero de un rival recibe el daño de cuerpo.
                else if (Perro.De(hit.collider) != null)
                {
                    if (Perro.De(hit.collider).PedirDanio(weapon.HitDamage(BodyZone.Body, hit.distance), shooter, hit.point)) anyHit = true;
                }
                // US 120: los botones del panel del campo de práctica también se aprietan de un tiro.
                else if (!BotonDePractica.Disparar(hit.collider)) ImpactMarks.Spawn(hit);
                continue;
            }

            // US 031, CA6: los compañeros de equipo no reciben daño (la bala se frena en ellos).
            if (EquiposTacticos.SonAliados(shooter, target)) continue;

            // Se mira antes de aplicar el daño: a un jugador de otra computadora el daño le llega después (US 029).
            int damage = weapon.HitDamage(zone, hit.distance) * porPerdigon + deMasPorPerdigon + (i == 0 ? deMasSobra : 0);
            bool lethal = target.WouldDie(damage);
            Impacto?.Invoke(target, damage, zone);
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
        // F35 (US 381): el fogonazo y las estelas de la skin legendaria, desde la punta del arma como se ve.
        if (legendaria != null) EfectosDeSkin.DisparoPropio(conSkin, weapon, muzzle, finales, legendaria);

        aim.Kick(weapon); // (US 309: con el doble cartucho patea igual que uno)
        Fired?.Invoke(shooter, camera.transform.position, directions);
        if (anyHit) Hit?.Invoke(kill ? HitMarkerKind.Kill : head ? HitMarkerKind.Head : HitMarkerKind.Body);
        if (anyHit) Acerto?.Invoke(weapon);
    }

    // Fase de compra del Táctico (F21, US 157): el disparo es solo para el minijuego. Tiene la cadencia, la dispersión y
    // el retroceso del arma, pero solo le pega a los cometas: no daña a nadie, no deja marcas y no va por la red (cada
    // jugador tiene sus cometas). Un acierto muestra el marcador de impacto (US 158, CA4).
    private static void FirePractice(ShopItem weapon, Camera camera, WeaponAim aim, Quaternion view, float spread, int pellets,
        Vector3 muzzle, float range)
    {
        MinijuegoCometas.ContarDisparo();
        float reach = MinijuegoCometas.Alcance(range);
        bool anyHit = false;
        for (int i = 0; i < pellets; i++)
        {
            Vector3 direction = view * Deviation(spread) * Vector3.forward;
            Cometa comet = MinijuegoCometas.Buscar(camera.transform.position, direction, reach, out Vector3 point);
            Trazadora.Mostrar(muzzle, comet != null ? point : camera.transform.position + direction * Mathf.Min(reach, 300f));
            if (comet != null && MinijuegoCometas.Acertar(comet, point)) anyHit = true;
        }
        aim.Kick(weapon);
        if (anyHit) Hit?.Invoke(HitMarkerKind.Body);
    }

    // El disparo de otro jugador que llega por la red (US 028): se busca dónde pega para dibujar la trazadora y
    // la marca en el escenario, sin hacer daño (el daño lo calcula quien disparó). Devuelve el punto final.
    public static Vector3 Replay(Vector3 origin, Vector3 direction, float range, Transform shooter)
    {
        if (!Trace(origin, direction, range, shooter, out RaycastHit hit, out HealthSystem target, out BodyZone _))
            return origin + direction * range;
        if (target == null)
        {
            // US 020, CA7: en la Barrera, chispas en vez de marca (el daño lo manda quien disparó).
            Barrera barrera = Barrera.De(hit.collider);
            if (barrera != null) barrera.Chispas(hit.point, hit.normal);
            else if (Perro.De(hit.collider) == null) ImpactMarks.Spawn(hit);
        }
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
    /// <summary>US 287: un disparo de un arma que lanza granadas (el Gran Capitán): sale de la boca del arma hacia donde
    /// mira, con su dispersión y su retroceso, a la velocidad de la ficha; explota al tocar algo (Grenade1).</summary>
    public static void FireProjectile(ShopItem weapon, Camera camera, Transform shooter, bool aiming)
    {
        if (weapon == null || camera == null || weapon.grenadePrefab == null) return;
        WeaponAim aim = WeaponAim.For(camera);
        Quaternion view = camera.transform.rotation * aim.RecoilRotation;
        Vector3 direction = view * Deviation(aim.CurrentSpread(weapon, aiming)) * Vector3.forward;
        Vector3 muzzle = Trazadora.BocaLocal(camera);
        // Pegado a una pared, la granada sale de este lado (si no, aparecería del otro).
        Vector3 desde = camera.transform.position, hacia = muzzle - desde;
        foreach (RaycastHit h in Physics.RaycastAll(desde, hacia.normalized, hacia.magnitude, ~0, QueryTriggerInteraction.Ignore))
            if (shooter == null || !h.collider.transform.IsChildOf(shooter))
                if (Vector3.Distance(desde, h.point) < Vector3.Distance(desde, muzzle)) muzzle = h.point - hacia.normalized * 0.15f;
        int deMas = DanioDeMas != null ? Mathf.Max(0, DanioDeMas()) : 0; // US 330: la bronca va en la explosión
        Trazadora.Fogonazo(muzzle, deMas > 0);

        GameObject thrown = Object.Instantiate(weapon.grenadePrefab, muzzle, Quaternion.LookRotation(direction));
        Grenade1 grenade = thrown.GetComponent<Grenade1>();
        if (grenade != null)
        {
            grenade.thrower = shooter;
            grenade.Configure(weapon);
            grenade.throwForce = 0f;
            grenade.danioDeMas = deMas;
            grenade.Throw(direction);
        }
        if (deMas > 0) Brasa(thrown.transform); // US 330: la granada con bronca vuela roja
        Rigidbody body = thrown.GetComponent<Rigidbody>();
        if (body != null) body.linearVelocity = direction * weapon.velocidadDelProyectil;

        aim.Kick(weapon);
        Fired?.Invoke(shooter, camera.transform.position, new[] { direction });
    }

    // US 330: una luz roja que acompaña a la granada con bronca hasta que explota.
    private static void Brasa(Transform granada)
    {
        var go = new GameObject("Bronca");
        go.transform.SetParent(granada, false);
        Light luz = go.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = Trazadora.Bronca;
        luz.intensity = 4f;
        luz.range = 3f;
        luz.shadows = LightShadows.None;
    }

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
