using UnityEngine;

// F08 (Modo Invasión): las cartas del puesto que actúan durante el juego (no al elegirlas), según cuántas veces se
// tienen (EleccionDeInvasion.Veces, hasta 3: US 293, CA5). Lo agrega PartidaDeInvasion.
// - US 316, Reciclador: cada baja a la cabeza devuelve 5 balas al cargador del arma con la que se hizo (10 y 15 con la
//   carta repetida). Lo que no entra en el cargador va a la reserva.
// - US 317, Filo: matar con el cuchillo recarga al instante la última arma de fuego que se tuvo en la mano (desde su
//   reserva), porque en ese momento la mano tiene el cuchillo.
// - US 318, Carroñero: 1 de cada 5 invasores que mueren suelta un cargador (1 de cada 4 y 1 de cada 3 con la carta
//   repetida), que se junta pasando por encima (CargadorEnElPiso).
// - US 328, Remate: el cuchillo mata de un golpe a un invasor con el 25 % de la vida o menos (30 % y 35 % con la carta
//   repetida), salvo al Jefe.
// - US 327, Filo voraz: cada cuchillazo a un invasor cura lo mismo que el daño que hizo, hasta 25 de vida por golpe (35
//   y 45 con la carta repetida).
public class CartasEnJuego : MonoBehaviour
{
    public const int BalasDelReciclador = 5; // US 316, CA1: por cada vez
    public const int CarroneroCada = 5;      // US 318, CA1: 1 de cada 5 (uno menos por cada vez que se repite)
    public const float RemateDesde = 0.25f, RematePorVez = 0.05f; // US 328: 25 %, 30 % y 35 % de la vida
    public const int FiloVorazTope = 25, FiloVorazPorVez = 10;      // US 327: hasta 25, 35 y 45 de vida por golpe

    private int muertesParaCarronero;

    private WeaponSwitcher armas;

    private void OnEnable()
    {
        WeaponFire.Killed += AlMatar;
        MeleeAttack.Mato += AlMatarConElCuchillo;
        Npc.Murio += AlMorirUnInvasor;
        MeleeAttack.AjustarGolpe = Rematar;
        MeleeAttack.Golpeo += AlAcuchillar;
    }

    private void OnDisable()
    {
        WeaponFire.Killed -= AlMatar;
        MeleeAttack.Mato -= AlMatarConElCuchillo;
        Npc.Murio -= AlMorirUnInvasor;
        MeleeAttack.Golpeo -= AlAcuchillar;
        if (MeleeAttack.AjustarGolpe == (System.Func<MeleeAttack, HealthSystem, int, int>)Rematar) MeleeAttack.AjustarGolpe = null;
    }

    // US 327: con Filo voraz, cada cuchillazo a un invasor cura lo que le sacó (solo el daño que de verdad hizo).
    private void AlAcuchillar(MeleeAttack cuchillo, HealthSystem herido, int hecho)
    {
        int voraz = EleccionDeInvasion.Veces("Filo voraz");
        if (voraz <= 0 || cuchillo == null || herido == null || herido.GetComponentInParent<Npc>() == null) return;
        HealthSystem propia = cuchillo.GetComponentInParent<HealthSystem>();
        if (propia == null) propia = cuchillo.transform.root.GetComponentInChildren<HealthSystem>();
        if (propia == null || propia.currentHealth <= 0) return;
        int cura = Mathf.Min(hecho, FiloVorazTope + FiloVorazPorVez * (voraz - 1));
        propia.SetState(propia.currentHealth + cura, propia.currentShield);
    }

    // US 328: con Remate, un cuchillazo a un invasor (no al Jefe) que ya tiene poca vida lo mata.
    private int Rematar(MeleeAttack cuchillo, HealthSystem vida, int danio)
    {
        int remate = EleccionDeInvasion.Veces("Remate");
        if (remate <= 0 || vida == null || vida.maxHealth <= 0) return danio;
        Npc npc = vida.GetComponentInParent<Npc>();
        if (npc == null || !npc.Vivo || (npc.Tipo != null && npc.Tipo.esJefe)) return danio;
        float umbral = RemateDesde + RematePorVez * (remate - 1);
        if (vida.currentHealth > vida.maxHealth * umbral) return danio;
        return Mathf.Max(danio, vida.currentHealth + vida.currentShield); // de un golpe
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
