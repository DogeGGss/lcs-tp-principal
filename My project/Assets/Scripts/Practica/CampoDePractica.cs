using System.Collections.Generic;
using UnityEngine;

// US 119 · Probar armas en el campo de práctica (Shooting Range, US 118). Va en la escena Mapa_practica (lo pone el menú
// Riftwalker > Mapa de práctica): mientras la escena está abierta, el juego sabe que es el campo de práctica.
// - CA1 y CA4: todas las armas se eligen gratis con la tienda (B), en cualquier parte del mapa y cuando quiera. Es la
//   misma pantalla que el "Elegí tu equipo" del Deathmatch (PlayerLoadout.Free), sin plata ni zona de compra.
// - CA2 y CA3: el cambio de arma y las armas son los mismos de una partida (WeaponSwitcher, ArmaDeFuego): no se
//   tocan.
// - Las armas no se sueltan con G (SoltarArmas): acá se cambian con la tienda.
// - Sin habilidad (PlayerAbility; US 166: el tutorial presta el Trasbordo durante su paso) ni fase de compra
//   (BuyPhase). US 166: con todas las granadas, que no se gastan (PlayerLoadout), para practicarlas en el tutorial. Las granadas atraviesan la pared
//   invisible de la línea de tiro (la que frena al jugador), así llegan a los bots.
// - US 121: munición infinita (al recargar no se gasta la reserva: ArmaDeFuego, Mitre y Pistola) y el jugador no pierde
//   vida ni muere (invulnerable). Solo acá: en las partidas no cambia nada (CA3).
// - US 120, CA4: las marcas de bala no se desvanecen hasta que se reinicia con el panel (PanelDePractica).
// Corre antes que el resto de los scripts, así "Activo" ya vale cuando el jugador y la tienda arrancan.
[DefaultExecutionOrder(-1000)]
public class CampoDePractica : MonoBehaviour
{
    public static bool Activo { get; private set; }

    private void Awake() => Activo = true;

    // La pared invisible de la barrera (MapaDePractica: "Pared invisible", en la capa Ignore Raycast).
    private const string NombreParedInvisible = "Pared invisible";
    private const int CapaSinDisparos = 2;
    private readonly List<Collider> paredes = new List<Collider>();

    private void OnEnable() => GrenadeThrower.Thrown += AlTirarGranada;
    private void OnDisable() => GrenadeThrower.Thrown -= AlTirarGranada;

    private void AlTirarGranada(Transform quien, Grenade1 granada)
    {
        if (granada == null) return;
        foreach (Collider propio in granada.GetComponentsInChildren<Collider>())
            foreach (Collider pared in paredes)
                if (pared != null) Physics.IgnoreCollision(propio, pared);
    }

    // US 121, CA2: el jugador no recibe daño (HealthSystem.TakeDamage no hace nada si es invulnerable).
    private void Start()
    {
        ImpactMarks.Permanent = true;
        foreach (Collider c in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            if (c.gameObject.layer == CapaSinDisparos && c.name == NombreParedInvisible) paredes.Add(c);
        foreach (PlayerLoadout jugador in FindObjectsByType<PlayerLoadout>(FindObjectsSortMode.None))
        {
            HealthSystem vida = jugador.GetComponent<HealthSystem>();
            if (vida != null) vida.Invulnerable = true;
        }
    }

    private void OnDestroy() => Activo = false;
}
