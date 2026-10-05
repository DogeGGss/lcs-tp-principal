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

    /// <summary>De qué lado es la zona (US 190: los muros de la base se arman por zona).</summary>
    public LadoTactico Lado => lado;

    private void OnEnable() => zones.Add(this);
    private void OnDisable() => zones.Remove(this);

    // "lado" es el lado del jugador que quiere comprar. Sin indicarlo (o con Cualquiera) vale cualquier zona.
    // ignorarAltura (US 032): solo mira el piso de la zona, así saltar no cuenta como salir de la base.
    public static bool Contains(Vector3 position, LadoTactico lado = LadoTactico.Cualquiera, bool ignorarAltura = false)
    {
        if (zones.Count == 0) return true;
        foreach (BuyZone zone in zones)
        {
            if (zone.area == null) continue;
            bool ladoServe = lado == LadoTactico.Cualquiera || zone.lado == LadoTactico.Cualquiera || zone.lado == lado;
            Vector3 punto = ignorarAltura ? new Vector3(position.x, zone.area.bounds.center.y, position.z) : position;
            if (ladoServe && zone.area.bounds.Contains(punto)) return true;
        }
        return false;
    }

    // La base de un lado: todas sus zonas juntas (si no tiene, las que sirven para todos). Los pájaros del minijuego
    // vuelan sobre la base del equipo (F21, US 156).
    public static bool Area(LadoTactico lado, out Bounds limites)
    {
        limites = default;
        bool hay = false;
        for (int pasada = 0; pasada < 2 && !hay; pasada++)
            foreach (BuyZone zone in zones)
            {
                if (zone.area == null || zone.lado != (pasada == 0 ? lado : LadoTactico.Cualquiera)) continue;
                if (hay) limites.Encapsulate(zone.area.bounds);
                else { limites = zone.area.bounds; hay = true; }
            }
        return hay;
    }
}
