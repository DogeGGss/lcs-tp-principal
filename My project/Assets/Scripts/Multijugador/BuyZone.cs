using System.Collections.Generic;
using UnityEngine;

// Zona de compra del equipo (US 076, CA9). Si la escena no tiene ninguna, se puede comprar en cualquier lugar.
// Táctico (US 031, CA4): "lado" dice de qué equipo es la zona (Atacante o Defensor). Cada jugador solo compra
// en la zona de su lado. Cualquiera = la zona sirve para todos.
[RequireComponent(typeof(Collider))]
public class BuyZone : MonoBehaviour
{
    private static readonly List<BuyZone> zones = new List<BuyZone>();
    private Collider area;

    [Tooltip("De qué lado es la zona de compra. Cualquiera: la puede usar todo el mundo.")]
    [SerializeField] private LadoTactico lado = LadoTactico.Cualquiera;

    private void Awake()
    {
        area = GetComponent<Collider>();
        area.isTrigger = true;
    }

    private void OnEnable() => zones.Add(this);
    private void OnDisable() => zones.Remove(this);

    // "lado" es el lado del jugador que quiere comprar. Sin indicarlo (o con Cualquiera) vale cualquier zona.
    public static bool Contains(Vector3 position, LadoTactico lado = LadoTactico.Cualquiera)
    {
        if (zones.Count == 0) return true;
        foreach (BuyZone zone in zones)
        {
            if (zone.area == null) continue;
            bool ladoServe = lado == LadoTactico.Cualquiera || zone.lado == LadoTactico.Cualquiera || zone.lado == lado;
            if (ladoServe && zone.area.bounds.Contains(position)) return true;
        }
        return false;
    }
}
