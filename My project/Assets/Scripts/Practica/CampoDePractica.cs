using UnityEngine;

// US 119 · Probar armas en el campo de práctica (Shooting Range, US 118). Va en la escena Mapa_practica (lo pone el menú
// Riftwalker > Mapa de práctica): mientras la escena está abierta, el juego sabe que es el campo de práctica.
// - CA1 y CA4: todas las armas se eligen gratis con la tienda (B), en cualquier parte del mapa y cuando quiera. Es la
//   misma pantalla que el "Elegí tu equipo" del Deathmatch (PlayerLoadout.Free), sin plata ni zona de compra.
// - CA2 y CA3: el cambio de arma y las armas son los mismos de una partida (WeaponSwitcher, ArmaDeFuego): no se
//   tocan.
// - Las armas no se sueltan con G (SoltarArmas): acá se cambian con la tienda.
public class CampoDePractica : MonoBehaviour
{
    public static bool Activo { get; private set; }

    private void Awake() => Activo = true;

    private void OnDestroy() => Activo = false;
}
