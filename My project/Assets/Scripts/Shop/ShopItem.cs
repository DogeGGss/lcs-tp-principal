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
    [Tooltip("Disparos por segundo; en armas de ráfaga, ráfagas por segundo.")]
    public float fireRate;
    public int burstCount = 1;
    public int magazine;
    public int reserve;
    public float reloadTime;
    public bool reloadPerShell;
    public float mobility = 1f;
    public float zoom;
    public float zoom2;
    public int pellets = 1;
    public string penetration;
    [Tooltip("Dispersión en grados: x = quieto, y = en movimiento.")]
    public Vector2 spread;
    [TextArea] public string recoil;
    public DamageBand[] bands;

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

    public bool IsWeapon => kind == ShopItemKind.PrimaryWeapon || kind == ShopItemKind.SecondaryWeapon;

    // Daño de un disparo completo a una zona (en escopetas, todos los perdigones).
    public int ShotDamage(DamageBand band, BodyZone zone)
    {
        int perHit = zone == BodyZone.Head ? band.head : zone == BodyZone.Legs ? band.legs : band.body;
        return perHit * Mathf.Max(1, pellets);
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
