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
// - US 329, Reserva lista: el arma guardada (la principal o la secundaria que no está en la mano) se recarga sola desde
//   su reserva después de 2 s guardada (mientras está guardada no se puede disparar).
// - US 330, Bronca: cuando un invasor le pega al jugador (un golpe, una bola o un rayo), el próximo disparo suma 5 veces
//   la vida que perdió en ese golpe (6 y 7 con la carta repetida); con la escopeta se reparte entre los perdigones y,
//   con el Gran Capitán, va en la explosión. Las nubes y los charcos de veneno del Jefe no la cargan. El disparo con
//   bronca se ve rojo (el fogonazo y la estela; la granada vuela con una luz roja) y, mientras está cargada, el arma en
//   la mano tiene un brillo rojo que late. Guarda un solo golpe y, después de usarlo, espera 8 s para volver a cargarse.
// - US 334, Alimentación directa (legendaria): el cargador y la reserva son un solo depósito: después de cada disparo
//   el cargador se vuelve a llenar desde la reserva, así no se recarga nunca hasta que se acaban las balas. Deja sin
//   uso a las cartas que solo recargan (la elección lo avisa).
// - US 327, Filo voraz: cada cuchillazo a un invasor cura lo mismo que el daño que hizo, hasta 25 de vida por golpe (35
//   y 45 con la carta repetida).
public class CartasEnJuego : MonoBehaviour
{
    public const int BalasDelReciclador = 5; // US 316, CA1: por cada vez
    public const int CarroneroCada = 5;      // US 318, CA1: 1 de cada 5 (uno menos por cada vez que se repite)
    public const float RemateDesde = 0.25f, RematePorVez = 0.05f; // US 328: 25 %, 30 % y 35 % de la vida
    public const int FiloVorazTope = 25, FiloVorazPorVez = 10;      // US 327: hasta 25, 35 y 45 de vida por golpe
    public const float ReservaListaTarda = 2f;                      // US 329: s guardada hasta recargarse
    public const int BroncaPor = 5;                                 // US 330: veces la vida perdida (6 y 7 repetida)
    public const float BroncaEspera = 8f;                           // US 330: s hasta volver a cargarse

    private int bronca;               // US 330: el daño de más guardado para el próximo disparo (0: nada)
    private float broncaListaDesde;   // US 330: desde cuándo puede volver a cargarse
    private Light brilloDeBronca;     // US 330: el brillo rojo del arma mientras está cargada

    // US 329: desde cuándo está guardada cada arma (la principal y la secundaria).
    private readonly System.Collections.Generic.Dictionary<GameObject, float> guardadaDesde =
        new System.Collections.Generic.Dictionary<GameObject, float>();

    private int muertesParaCarronero;

    private WeaponSwitcher armas;

    private void OnEnable()
    {
        WeaponFire.Killed += AlMatar;
        MeleeAttack.Mato += AlMatarConElCuchillo;
        Npc.Murio += AlMorirUnInvasor;
        MeleeAttack.AjustarGolpe = Rematar;
        MeleeAttack.Golpeo += AlAcuchillar;
        HealthSystem.Damaged += AlRecibirDanio;
        WeaponFire.DanioDeMas = TomarBronca;
        WeaponFire.Fired += AlDisparar;
    }

    private void OnDisable()
    {
        WeaponFire.Killed -= AlMatar;
        MeleeAttack.Mato -= AlMatarConElCuchillo;
        Npc.Murio -= AlMorirUnInvasor;
        MeleeAttack.Golpeo -= AlAcuchillar;
        HealthSystem.Damaged -= AlRecibirDanio;
        WeaponFire.Fired -= AlDisparar;
        if (WeaponFire.DanioDeMas == (System.Func<int>)TomarBronca) WeaponFire.DanioDeMas = null;
        if (MeleeAttack.AjustarGolpe == (System.Func<MeleeAttack, HealthSystem, int, int>)Rematar) MeleeAttack.AjustarGolpe = null;
    }

    // US 334: con Alimentación directa, el disparo que salió se repone enseguida desde la reserva.
    private void AlDisparar(Transform tirador, Vector3 desde, Vector3[] direcciones)
    {
        if (EleccionDeInvasion.Veces(CartaDeInvasion.AlimentacionDirecta) > 0)
            MunicionDelArma.LlenarCargador(MunicionDelArma.EnLaMano(Armas()));
    }

    private void Update()
    {
        if (EleccionDeInvasion.Veces(CartaDeInvasion.AlimentacionDirecta) > 0) AlimentarLasArmas();
        else if (EleccionDeInvasion.Veces("Reserva lista") > 0) RecargarLasGuardadas();
        MostrarBronca();
    }

    // US 330: mientras la bronca está cargada, una luz roja que late sobre el arma en la mano (abajo a la derecha de la
    // cámara, donde se ve el arma).
    private void MostrarBronca()
    {
        bool cargada = bronca > 0;
        if (!cargada)
        {
            if (brilloDeBronca != null) brilloDeBronca.enabled = false;
            return;
        }
        if (brilloDeBronca == null)
        {
            Camera camara = Camera.main;
            if (camara == null) return;
            var go = new GameObject("Bronca cargada");
            go.transform.SetParent(camara.transform, false);
            go.transform.localPosition = new Vector3(0.18f, -0.1f, 0.45f);
            brilloDeBronca = go.AddComponent<Light>();
            brilloDeBronca.type = LightType.Point;
            brilloDeBronca.color = Trazadora.Bronca;
            brilloDeBronca.range = 1.2f;
            brilloDeBronca.shadows = LightShadows.None;
        }
        brilloDeBronca.enabled = true;
        brilloDeBronca.intensity = 2f + 1.5f * Mathf.Sin(Time.time * 7f);
    }

    // US 334: las dos armas, siempre con el cargador lleno desde la reserva (también la que se agarra después).
    private void AlimentarLasArmas()
    {
        WeaponSwitcher a = Armas();
        if (a == null) return;
        MunicionDelArma.LlenarCargador(a.PrimaryObj);
        MunicionDelArma.LlenarCargador(a.SecondaryObj);
    }

    // US 329: el arma que lleva guardada 2 s se recarga sola (una vez llena, no hace nada más).
    private void RecargarLasGuardadas()
    {
        WeaponSwitcher a = Armas();
        if (a == null) return;
        foreach (GameObject arma in new[] { a.PrimaryObj, a.SecondaryObj })
        {
            if (arma == null) continue;
            if (arma.activeInHierarchy) { guardadaDesde.Remove(arma); continue; }
            if (!guardadaDesde.TryGetValue(arma, out float desde)) { guardadaDesde[arma] = Time.time; continue; }
            if (Time.time - desde >= ReservaListaTarda) MunicionDelArma.LlenarCargador(arma);
        }
    }

    // US 330: un invasor le pegó al jugador (los ataques de los invasores dicen quién pega: DamageSource).
    private void AlRecibirDanio(HealthSystem vida, int danio, Vector3? desde)
    {
        int veces = EleccionDeInvasion.Veces("Bronca");
        if (veces <= 0 || danio <= 0 || bronca > 0 || Time.time < broncaListaDesde) return;
        if (string.IsNullOrEmpty(HealthSystem.DamageSource) || NubeDeVeneno.Aplicando) return; // las nubes y charcos no cuentan
        if (vida.GetComponentInParent<PlayerMovement>() == null) return;
        int perdida = Mathf.Min(danio, vida.currentHealth + vida.currentShield); // lo que de verdad le saca
        bronca = perdida * (BroncaPor + veces - 1);
    }

    // US 330: el disparo que sale se lleva la bronca guardada.
    private int TomarBronca()
    {
        if (bronca <= 0) return 0;
        int deMas = bronca;
        bronca = 0;
        broncaListaDesde = Time.time + BroncaEspera;
        return deMas;
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
