using UnityEngine;

public enum ShopCategory { Pistols, SMGs, Shotguns, Rifles, Snipers, Shields, Grenades }
public enum ShopItemKind { PrimaryWeapon, SecondaryWeapon, Grenade, Shield }
public enum GrenadeType { Frag, Smoke, Flash }

// Daño de un tramo de distancia. Para las escopetas es el daño de cada perdigón.
[System.Serializable]
public class DamageBand
{
    [Tooltip("Hasta qué distancia (m) vale este tramo. 0 = sin límite.")]
    public float upTo;
    public int head;
    public int body;
    public int legs;
}

// Un elemento que se vende en la tienda: arma, granada o escudo (F20).
// Los números salen del diseño de armas (vida 100 + escudo 25/50).
[CreateAssetMenu(fileName = "ShopItem", menuName = "Tienda/Elemento de tienda")]
public class ShopItem : ScriptableObject
{
    [Header("Tienda")]
    public string displayName;
    public string alias;
    public ShopCategory category;
    public ShopItemKind kind;
    public int price;
    public Sprite icon;
    [Tooltip("Color del ícono o del punto que lo identifica (escudos y granadas).")]
    public Color tint = Color.white;
    [TextArea] public string description;

    [Header("Arma")]
    public string fireMode;
    [Tooltip("Dispara mientras se mantiene apretado el botón. Si no, un disparo (o una ráfaga) por clic.")]
    public bool automatic;
    [Tooltip("Disparos por segundo; en armas de ráfaga, ráfagas por segundo.")]
    public float fireRate;
    public int burstCount = 1;
    [Tooltip("Armas de ráfaga: balas por segundo dentro de la ráfaga.")]
    public float burstRate;
    [Tooltip("Segundos que tarda en estar lista al sacarla.")]
    public float equipTime = 1f;
    public int magazine;
    public int reserve;
    public float reloadTime;
    public bool reloadPerShell;
    public float mobility = 1f;
    [Tooltip("Largo real del arma, en metros: así se ve en la mano de los demás jugadores y en el piso (US 182 y US 184). 0 = el que trae el modelo de primera persona.")]
    public float realLength;
    public float zoom;
    public float zoom2;
    public int pellets = 1;
    public string penetration;
    [Tooltip("Dispersión en grados: x = quieto, y = en movimiento.")]
    public Vector2 spread;
    [Tooltip("Dispersión extra por cada tiro seguido, en grados (0 = no cambia). El primer tiro sale sin extra y se va al dejar de disparar, con la recuperación del retroceso.")]
    public float spamSpread;
    [Tooltip("Tope de la dispersión extra por tirar seguido, en grados (0 = sin tope).")]
    public float spamSpreadMax;
    [TextArea] public string recoil;
    public DamageBand[] bands;

    [Header("Retroceso (US 168)")]
    [Tooltip("Balas seguidas que salen exactas antes de que la mira empiece a subir.")]
    public int recoilExactShots;
    [Tooltip("Grados que sube la mira por bala.")]
    public float recoilKick;
    [Tooltip("Grados máximos que sube la mira.")]
    public float recoilMaxClimb;
    [Tooltip("Ancho del zigzag a cada lado, en grados, una vez que llegó arriba (0 = sin zigzag).")]
    public float recoilSway;
    [Tooltip("Balas que tarda el zigzag en cruzar de un lado al otro.")]
    public int recoilSwayShots = 4;
    [Tooltip("Segundos que tarda la mira en volver al punto de partida al dejar de disparar.")]
    public float recoilRecovery = 0.3f;
    [Tooltip("Sacudón de la mira en cada tiro, en grados: sube y vuelve enseguida, además del patrón (0 = sin sacudón).")]
    public float recoilPunch;

    [Header("Forma del retroceso")]
    [Tooltip("Patrón bala por bala, como en CS: dónde sale cada bala respecto de la mira, en grados (x = derecha, y = arriba). Si tiene puntos, reemplaza la forma calculada con los campos de abajo; recoilKick sigue marcando si el arma tiene retroceso.")]
    public Vector2[] recoilPoints = new Vector2[0];
    [Tooltip("Arriba de todo la mira sigue subiendo, cada vez menos: grados por la raíz de las balas pasado el tope (0 = se queda a la misma altura).")]
    public float recoilLateClimb;
    [Tooltip("Grados por bala que la mira se corre hacia un costado mientras sube (0 = sube recto).")]
    public float recoilDrift;
    [Tooltip("Arriba, el costado va en bloques: esa cantidad de balas de un lado y después salta al otro. Si no, cruza de a poco.")]
    public bool recoilSwayBlocks;
    [Tooltip("Cuánto se abre el zigzag por cada bala arriba (0,08 = 8 % más ancho por bala).")]
    public float recoilSwayGrowth;
    [Tooltip("Asimetría del zigzag: positivo va más a la derecha que a la izquierda (0,25 = 125 % y 75 %).")]
    [Range(-1f, 1f)] public float recoilSwayBias;
    [Tooltip("Variación de cada ráfaga: se sortea hacia qué lado arranca y el ancho cambia hasta este porcentaje (0,15 = ±15 %). 0 = todas las ráfagas iguales.")]
    [Range(0f, 0.5f)] public float recoilVariation;

    [Header("Escudo")]
    public int shieldPoints;

    [Header("Granada")]
    public GrenadeType grenadeType;
    public int maxCarry = 1;
    public float fuse;
    [Tooltip("Radio del humo o de la explosión, en metros.")]
    public float effectRadius;
    [Tooltip("Duración del humo, o de la ceguera total de la flash, en segundos.")]
    public float effectDuration;
    [Tooltip("Flash: segundos que tarda en volver la vista después de la ceguera total.")]
    public float recovery;
    [Tooltip("Resumen corto para la lista de la tienda (por ejemplo, \"Hasta 100\").")]
    public string shortEffect;

    [Tooltip("Granadas: prefab que se lanza (con el componente Grenade1).")]
    public GameObject grenadePrefab;

    public bool IsWeapon => kind == ShopItemKind.PrimaryWeapon || kind == ShopItemKind.SecondaryWeapon;

    // Daño de un disparo completo a una zona (en escopetas, todos los perdigones).
    public int ShotDamage(DamageBand band, BodyZone zone)
    {
        int perHit = zone == BodyZone.Head ? band.head : zone == BodyZone.Legs ? band.legs : band.body;
        return perHit * Mathf.Max(1, pellets);
    }

    // Tramo de distancia que corresponde a un impacto (US 165). Sin tramos, null.
    public DamageBand BandAt(float distance)
    {
        if (bands == null || bands.Length == 0) return null;
        foreach (DamageBand band in bands)
            if (band.upTo <= 0f || distance <= band.upTo) return band;
        return bands[bands.Length - 1];
    }

    // Daño de una bala (o de un perdigón) a una zona y a una distancia (US 165).
    public int HitDamage(BodyZone zone, float distance)
    {
        DamageBand band = BandAt(distance);
        if (band == null) return 0;
        return zone == BodyZone.Head ? band.head : zone == BodyZone.Legs ? band.legs : band.body;
    }

    public string BandLabel(int index)
    {
        if (bands == null || bands.Length == 0) return "";
        float from = index == 0 ? 0f : bands[index - 1].upTo;
        DamageBand band = bands[index];
        if (band.upTo <= 0f) return from > 0f ? $"{from:0}+ m" : "Toda distancia";
        return $"{from:0}–{band.upTo:0} m";
    }
}

public enum BodyZone { Head, Body, Legs }

public static class DamageMath
{
    public static int ShotsToKill(int damage, int effectiveHealth)
    {
        if (damage <= 0) return int.MaxValue;
        return Mathf.CeilToInt(effectiveHealth / (float)damage);
    }
}
