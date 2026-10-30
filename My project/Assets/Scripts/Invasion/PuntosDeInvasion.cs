using UnityEngine;

// F08 (Modo Invasión), US 147: en este modo no hay plata, se juega con puntos (la billetera del jugador). La agrega
// PartidaDeInvasion. Los puntos se gastan en la tienda (US 148).
// - CA1: arranca con 500 (lo pone PartidaDeInvasion: PuntosIniciales).
// - CA2: cada impacto de bala en un enemigo vivo suma 10 (cada perdigón de la escopeta cuenta como un impacto).
// - CA3 y CA4: cada baja suma los puntos de su tipo (TipoDeNpc): 60, o 100 si el último impacto fue a la cabeza, y
//   500 el Jefe. Cuenta cualquier baja (bala, cuchillo o granada).
// - CA5: la billetera no tiene tope en este modo.
// - CA6: avisa cada suma (Sumo) y el marcador (US 150) la muestra un instante junto a los puntos.
public class PuntosDeInvasion : MonoBehaviour
{
    public const int PorImpacto = 10; // CA2

    /// <summary>CA6: cada suma de puntos, y si fue por una baja a la cabeza.</summary>
    public static event System.Action<int, bool> Sumo;

    private PlayerWallet billetera;

    private void OnEnable()
    {
        Npc.ImpactoDeBala += AlImpactar;
        Npc.Murio += AlMorir;
    }

    private void OnDisable()
    {
        Npc.ImpactoDeBala -= AlImpactar;
        Npc.Murio -= AlMorir;
    }

    private void AlImpactar(Npc npc, int danio, BodyZone zona) => Sumar(PorImpacto, false);

    private void AlMorir(Npc npc, int puntos, bool cabeza) => Sumar(puntos, cabeza);

    private void Sumar(int puntos, bool cabeza)
    {
        if (puntos <= 0) return;
        if (billetera == null) billetera = FindFirstObjectByType<PlayerWallet>();
        if (billetera == null) return;
        billetera.SinTope(); // CA5
        billetera.Add(puntos);
        Sumo?.Invoke(puntos, cabeza);
    }
}
