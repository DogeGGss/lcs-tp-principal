using UnityEngine;

// F08 (Modo Invasión): las cartas del puesto que actúan durante el juego (no al elegirlas), según cuántas veces se
// tienen (EleccionDeInvasion.Veces, hasta 3: US 293, CA5). Lo agrega PartidaDeInvasion.
// - US 316, Reciclador: cada baja a la cabeza devuelve 5 balas al cargador del arma con la que se hizo (10 y 15 con la
//   carta repetida). Lo que no entra en el cargador va a la reserva.
// - US 317, Filo: matar con el cuchillo recarga al instante la última arma de fuego que se tuvo en la mano (desde su
//   reserva), porque en ese momento la mano tiene el cuchillo.
public class CartasEnJuego : MonoBehaviour
{
    public const int BalasDelReciclador = 5; // US 316, CA1: por cada vez

    private WeaponSwitcher armas;

    private void OnEnable()
    {
        WeaponFire.Killed += AlMatar;
        MeleeAttack.Mato += AlMatarConElCuchillo;
    }

    private void OnDisable()
    {
        WeaponFire.Killed -= AlMatar;
        MeleeAttack.Mato -= AlMatarConElCuchillo;
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
