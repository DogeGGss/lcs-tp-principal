using System.Collections.Generic;
using UnityEngine;

// Zona de compra del equipo (US 076, CA9). Si la escena no tiene ninguna, se puede comprar en cualquier lugar.
[RequireComponent(typeof(Collider))]
public class BuyZone : MonoBehaviour
{
    private static readonly List<BuyZone> zones = new List<BuyZone>();
    private Collider area;

    private void Awake()
    {
        area = GetComponent<Collider>();
        area.isTrigger = true;
    }

    private void OnEnable() => zones.Add(this);
    private void OnDisable() => zones.Remove(this);

    public static bool Contains(Vector3 position)
    {
        if (zones.Count == 0) return true;
        foreach (BuyZone zone in zones)
            if (zone.area != null && zone.area.bounds.Contains(position)) return true;
        return false;
    }
}
