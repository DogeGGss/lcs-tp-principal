using UnityEngine;

// F08 (Modo Invasión): las cartas del puesto que actúan durante el juego (no al elegirlas), según cuántas veces se
// tienen (EleccionDeInvasion.Veces, hasta 3: US 293, CA5). Lo agrega PartidaDeInvasion.
// - US 316, Reciclador: cada baja a la cabeza devuelve 5 balas al cargador del arma con la que se hizo (10 y 15 con la
//   carta repetida). Lo que no entra en el cargador va a la reserva.
// - US 317, Filo: matar con el cuchillo recarga al instante la última arma de fuego que se tuvo en la mano (desde su
//   reserva), porque en ese momento la mano tiene el cuchillo.
// - US 318, Carroñero: 1 de cada 5 invasores que mueren suelta un cargador (1 de cada 4 y 1 de cada 3 con la carta
//   repetida), que se junta pasando por encima (CargadorEnElPiso).
public class CartasEnJuego : MonoBehaviour
{
    public const int BalasDelReciclador = 5; // US 316, CA1: por cada vez
    public const int CarroneroCada = 5;      // US 318, CA1: 1 de cada 5 (uno menos por cada vez que se repite)

    private int muertesParaCarronero;

    private WeaponSwitcher armas;

    private void OnEnable()
    {
        WeaponFire.Killed += AlMatar;
        MeleeAttack.Mato += AlMatarConElCuchillo;
        Npc.Murio += AlMorirUnInvasor;
    }

    private void OnDisable()
    {
        WeaponFire.Killed -= AlMatar;
        MeleeAttack.Mato -= AlMatarConElCuchillo;
        Npc.Murio -= AlMorirUnInvasor;
    }

    // US 318: cada invasor que muere (por lo que sea) cuenta para el Carroñero.
    private void AlMorirUnInvasor(Npc npc, int puntos, bool cabeza)
    {
        int carronero = EleccionDeInvasion.Veces("Carroñero");
        if (carronero <= 0 || npc == null) return;
        if (++muertesParaCarronero < Mathf.Max(1, CarroneroCada - (carronero - 1))) return;
        muertesParaCarronero = 0;
        CargadorEnElPiso.Soltar(npc.transform.position, npc.transform, () => MunicionDelArma.SumarUnCargador(MunicionDelArma.EnLaMano(Armas())));
    }

    // US 317: una baja con el cuchillo.
    private void AlMatarConElCuchillo(MeleeAttack cuchillo)
    {
        if (EleccionDeInvasion.Veces("Filo") > 0) MunicionDelArma.LlenarCargador(MunicionDelArma.EnLaMano(Armas()));
    }

    private WeaponSwitcher Armas()
    {
        if (armas == null)
        {
            PlayerLoadout equipo = FindFirstObjectByType<PlayerLoadout>();
            if (equipo != null) armas = equipo.GetComponentInChildren<WeaponSwitcher>(true);
        }
        return armas;
    }

    // Una baja con un disparo del jugador (a la cabeza o no).
    private void AlMatar(bool cabeza)
    {
        int reciclador = EleccionDeInvasion.Veces("Reciclador");
        if (cabeza && reciclador > 0)
            MunicionDelArma.SumarAlCargador(MunicionDelArma.EnLaMano(Armas()), BalasDelReciclador * reciclador); // US 316
    }
}
