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
// - US 333, Despeje: cuando el puesto llega al 100 % peleando, una burbuja holográfica verde sale del puesto y empuja
//   10 m hacia afuera a los invasores del anillo, de tierra y en el aire (15 y 20 m con la carta repetida), salvo al
//   Jefe (BurbujaDeDespeje y EmpujonDeNpc).
// - US 310, Cargador gemelo: la primera vez que se vacía el cargador del subfusil, se recarga al instante desde la
//   reserva. Vuelve a estar lista después de terminar una recarga a mano (si la recarga se corta, no). Se nota con un
//   sonido corto (Efectos/CargadorGemelo) y un destello naranja sobre el arma (ArmaDeFuego.RecargaAlVaciarse).
// - US 311, Cerrojo suelto: después de una baja a la cabeza con el francotirador, se puede volver a disparar a los
//   0,2 s (ArmaDeFuego.AcortarCerrojo; la mira vuelve también a los 0,2 s).
// - US 325, Mira abierta: sin la mira puesta, el francotirador dispara sin dispersión y al doble de cadencia; con la
//   mira, normal (WeaponFire.SinDispersion y ArmaDeFuego.CadenciaPorCarta).
// - US 309, Doble cartucho: la escopeta dispara 2 cartuchos juntos (el doble de perdigones), gastando los dos; con
//   uno solo en el cargador, sale simple (ArmaDeFuego.DobleCartucho).
// - US 319, Bala compacta: con la carta, la escopeta apunta manteniendo el click derecho (un zoom chico, como no tiene
//   mira); apuntando junta todos sus perdigones en un solo proyectil sin dispersión, con el mismo daño total; desde
//   la cadera, los perdigones de siempre (ArmaDeFuego.ApuntaPorCarta y WeaponFire.BalaCompacta).
// - US 322, Tiro de reserva: con el cargador vacío del subfusil o del Mitre, manteniendo el gatillo sigue disparando
//   desde la reserva a la mitad de la cadencia hasta que se suelta (Municion.TiroDeReserva). Con Cargador gemelo,
//   primero la recarga instantánea; con Alimentación directa, sin uso.
// - US 323, Disparo concentrado: con la carta, el Mitre apunta con el click derecho (un zoom chico); apuntando, dispara
//   hasta 5 balas juntas en un solo impacto que suma su daño, cada 0,4 s; desde la cadera, automático (Mitre.Concentrado).
// - US 312, Sin intervalo: matar con el fusil de ráfaga saca la pausa entre sus próximas 3 ráfagas (5 y 7 con la carta
//   repetida); entre las balas de cada ráfaga, la separación sigue igual (ArmaDeFuego.SinPausaEntreRafagas).
// - US 324, Ráfaga creciente: cada baja con el fusil de ráfaga le suma una bala a sus ráfagas, hasta 6 (8 y 10 con la
//   carta repetida); al terminar de recargar vuelven a las de siempre (si la recarga se corta, no). Cada bala gasta
//   munición (ArmaDeFuego.BalasDeMasEnLaRafaga).
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
    public const float CerrojoSuelto = 0.2f;                         // US 311: s hasta el próximo disparo
    public const float MiraAbiertaCadencia = 2f;                     // US 325: veces la cadencia sin apuntar
    public const int SinIntervaloRafagas = 3, SinIntervaloPorVez = 2;     // US 312: 3, 5 y 7 ráfagas
    public const int RafagaCrecienteTope = 6, RafagaCrecientePorVez = 2;  // US 324: hasta 6, 8 y 10 balas
    public const float DespejeMetros = 10f, DespejePorVez = 5f;     // US 333: 10, 15 y 20 m

    private int bronca;               // US 330: el daño de más guardado para el próximo disparo (0: nada)
    private float broncaListaDesde;   // US 330: desde cuándo puede volver a cargarse
    private Light brilloDeBronca;     // US 330: el brillo rojo del arma mientras está cargada

    // US 329: desde cuándo está guardada cada arma (la principal y la secundaria).
    private readonly System.Collections.Generic.Dictionary<GameObject, float> guardadaDesde =
        new System.Collections.Generic.Dictionary<GameObject, float>();

    private int muertesParaCarronero;

    private readonly System.Collections.Generic.HashSet<ArmaDeFuego> gemeloUsado =
        new System.Collections.Generic.HashSet<ArmaDeFuego>(); // US 310: ya se recargó solo (hasta la próxima recarga a mano)
    private Light destelloGemelo;   // US 310: el destello naranja sobre el arma
    private float destelloGemeloDesde = -10f;
    private static AudioClip sonidoGemelo;
    private static readonly Color Naranja = new Color(1f, 0.55f, 0.1f);
    private const float DestelloGemeloDura = 0.6f;
    private int rafagasSinPausa; // US 312: cuántas ráfagas más salen sin la pausa
    // US 324: las balas de más de la ráfaga de cada arma (hasta recargar).
    private readonly System.Collections.Generic.Dictionary<ArmaDeFuego, int> balasDeMas =
        new System.Collections.Generic.Dictionary<ArmaDeFuego, int>();

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
        ArmaDeFuego.SinPausaEntreRafagas = SinPausa;
        ArmaDeFuego.BalasDeMasEnLaRafaga = BalasDeMas;
        ArmaDeFuego.RecargaAlVaciarse = CargadorGemelo;
        ArmaDeFuego.CadenciaPorCarta = CadenciaDeMiraAbierta;
        WeaponFire.SinDispersion = SinDispersionDeMiraAbierta;
        ArmaDeFuego.DobleCartucho = ConDobleCartucho;
        WeaponFire.BalaCompacta = ConBalaCompacta;
        ArmaDeFuego.ApuntaPorCarta = ApuntaConBalaCompacta;
        Municion.TiroDeReserva = ConTiroDeReserva;
        Mitre.Concentrado = ConDisparoConcentrado;
        ArmaDeFuego.TerminoDeRecargar += AlRecargar;
        WeaponFire.Fired += AlDisparar;
        PuestoDeSuministros.SeCargo += Despejar;
    }

    private void OnDisable()
    {
        WeaponFire.Killed -= AlMatar;
        MeleeAttack.Mato -= AlMatarConElCuchillo;
        Npc.Murio -= AlMorirUnInvasor;
        MeleeAttack.Golpeo -= AlAcuchillar;
        HealthSystem.Damaged -= AlRecibirDanio;
        WeaponFire.Fired -= AlDisparar;
        PuestoDeSuministros.SeCargo -= Despejar;
        if (WeaponFire.DanioDeMas == (System.Func<int>)TomarBronca) WeaponFire.DanioDeMas = null;
        if (ArmaDeFuego.SinPausaEntreRafagas == (System.Func<ArmaDeFuego, bool>)SinPausa) ArmaDeFuego.SinPausaEntreRafagas = null;
        if (ArmaDeFuego.BalasDeMasEnLaRafaga == (System.Func<ArmaDeFuego, int>)BalasDeMas) ArmaDeFuego.BalasDeMasEnLaRafaga = null;
        if (ArmaDeFuego.RecargaAlVaciarse == (System.Func<ArmaDeFuego, bool>)CargadorGemelo) ArmaDeFuego.RecargaAlVaciarse = null;
        if (ArmaDeFuego.CadenciaPorCarta == (System.Func<ArmaDeFuego, bool, float>)CadenciaDeMiraAbierta) ArmaDeFuego.CadenciaPorCarta = null;
        if (WeaponFire.SinDispersion == (System.Func<ShopItem, bool, bool>)SinDispersionDeMiraAbierta) WeaponFire.SinDispersion = null;
        if (ArmaDeFuego.DobleCartucho == (System.Func<ArmaDeFuego, bool>)ConDobleCartucho) ArmaDeFuego.DobleCartucho = null;
        if (WeaponFire.BalaCompacta == (System.Func<ShopItem, bool, bool>)ConBalaCompacta) WeaponFire.BalaCompacta = null;
        if (ArmaDeFuego.ApuntaPorCarta == (System.Func<ArmaDeFuego, bool>)ApuntaConBalaCompacta) ArmaDeFuego.ApuntaPorCarta = null;
        if (Municion.TiroDeReserva == (System.Func<bool>)ConTiroDeReserva) Municion.TiroDeReserva = null;
        if (Mitre.Concentrado == (System.Func<bool>)ConDisparoConcentrado) Mitre.Concentrado = null;
        ArmaDeFuego.TerminoDeRecargar -= AlRecargar;
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
        DespejarSiToca();
        MostrarBronca();
        MostrarDestelloGemelo();
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

    // US 333: la burbuja del puesto cargado empuja hacia afuera a los invasores del anillo (no al Jefe).
    // Si en ese momento se abre una elección (Entrega inmediata, US 332, pausa el juego), la burbuja sale al cerrarla,
    // así se ve.
    private PuestoDeSuministros despejePendiente;

    private void Despejar(PuestoDeSuministros puesto)
    {
        if (EleccionDeInvasion.Veces("Despeje") <= 0 || puesto == null) return;
        despejePendiente = puesto;
    }

    private void DespejarSiToca()
    {
        if (despejePendiente == null || EleccionDeInvasion.Abierta || Time.timeScale <= 0f) return;
        PuestoDeSuministros puesto = despejePendiente;
        despejePendiente = null;
        int veces = EleccionDeInvasion.Veces("Despeje");
        if (veces <= 0 || !puesto.Hay) return;
        float metros = DespejeMetros + DespejePorVez * (veces - 1);
        Vector3 centro = puesto.Lugar;
        float radio = puesto.Radio;
        int empujados = BurbujaDeDespeje.Lanzar(centro, radio, metros);
        Debug.Log($"Despeje: {empujados} invasores empujados {metros:0} m.");
    }

    // US 312 y US 324: una baja con el fusil de ráfaga.
    private void AlMatarConLaRafaga(ArmaDeFuego arma)
    {
        int sinIntervalo = EleccionDeInvasion.Veces("Sin intervalo");
        if (sinIntervalo > 0) rafagasSinPausa = SinIntervaloRafagas + SinIntervaloPorVez * (sinIntervalo - 1);
        int creciente = EleccionDeInvasion.Veces("Ráfaga creciente");
        if (creciente > 0)
        {
            int tope = RafagaCrecienteTope + RafagaCrecientePorVez * (creciente - 1) - arma.shopItem.burstCount;
            balasDeMas.TryGetValue(arma, out int n);
            balasDeMas[arma] = Mathf.Min(Mathf.Max(0, tope), n + 1);
        }
    }

    private static bool EsEscopeta(ShopItem item) => item != null && item.category == ShopCategory.Shotguns;

    // US 309.
    private bool ConDobleCartucho(ArmaDeFuego arma) =>
        EsEscopeta(arma.shopItem) && EleccionDeInvasion.Veces("Doble cartucho") > 0;

    // US 322 (sin uso con Alimentación directa, US 334).
    private bool ConTiroDeReserva() =>
        EleccionDeInvasion.Veces("Tiro de reserva") > 0 && EleccionDeInvasion.Veces(CartaDeInvasion.AlimentacionDirecta) <= 0;

    // US 323.
    private bool ConDisparoConcentrado() => EleccionDeInvasion.Veces("Disparo concentrado") > 0;

    // US 319: la escopeta puede apuntar con el click derecho.
    private bool ApuntaConBalaCompacta(ArmaDeFuego arma) =>
        EsEscopeta(arma.shopItem) && EleccionDeInvasion.Veces("Bala compacta") > 0;

    // US 319: apuntando.
    private bool ConBalaCompacta(ShopItem arma, bool apuntando) =>
        apuntando && EsEscopeta(arma) && EleccionDeInvasion.Veces("Bala compacta") > 0;

    private static bool EsFrancotirador(ShopItem item) => item != null && item.category == ShopCategory.Snipers;

    // US 325: sin la mira puesta, el doble de cadencia y sin dispersión.
    private float CadenciaDeMiraAbierta(ArmaDeFuego arma, bool conMira) =>
        !conMira && EsFrancotirador(arma.shopItem) && EleccionDeInvasion.Veces("Mira abierta") > 0 ? MiraAbiertaCadencia : 1f;

    private bool SinDispersionDeMiraAbierta(ShopItem arma, bool conMira) =>
        !conMira && EsFrancotirador(arma) && EleccionDeInvasion.Veces("Mira abierta") > 0;

    // US 312: la ráfaga que terminó gasta una de las que salen sin pausa.
    private bool SinPausa(ArmaDeFuego arma)
    {
        if (rafagasSinPausa <= 0 || EleccionDeInvasion.Veces("Sin intervalo") <= 0) return false;
        rafagasSinPausa--;
        return true;
    }

    // US 324: las balas de más de esa arma.
    private int BalasDeMas(ArmaDeFuego arma) =>
        EleccionDeInvasion.Veces("Ráfaga creciente") > 0 && balasDeMas.TryGetValue(arma, out int n) ? n : 0;

    // US 324: al terminar de recargar (no si se cortó), la ráfaga vuelve a sus balas de siempre.
    private void AlRecargar(ArmaDeFuego arma)
    {
        balasDeMas.Remove(arma);
        gemeloUsado.Remove(arma); // US 310: vuelve a estar lista
    }

    // US 310: el subfusil se quedó sin balas en el cargador: la primera vez, se recarga solo.
    private bool CargadorGemelo(ArmaDeFuego arma)
    {
        if (EleccionDeInvasion.Veces("Cargador gemelo") <= 0 || arma.shopItem == null || arma.shopItem.category != ShopCategory.SMGs) return false;
        if (EleccionDeInvasion.Veces(CartaDeInvasion.AlimentacionDirecta) > 0) return false; // sin uso (US 334)
        if (!gemeloUsado.Add(arma)) return false;
        if (sonidoGemelo == null) sonidoGemelo = Resources.Load<AudioClip>("Efectos/CargadorGemelo");
        arma.Sonar(sonidoGemelo);
        destelloGemeloDesde = Time.time;
        return true;
    }

    // US 310: una luz naranja sobre el arma en la mano (abajo a la derecha de la cámara), fuerte y que se apaga.
    private void MostrarDestelloGemelo()
    {
        float t = (Time.time - destelloGemeloDesde) / DestelloGemeloDura;
        if (t >= 1f || t < 0f)
        {
            if (destelloGemelo != null) destelloGemelo.enabled = false;
            return;
        }
        if (destelloGemelo == null)
        {
            Camera camara = Camera.main;
            if (camara == null) return;
            var go = new GameObject("Cargador gemelo");
            go.transform.SetParent(camara.transform, false);
            go.transform.localPosition = new Vector3(0.18f, -0.1f, 0.4f);
            destelloGemelo = go.AddComponent<Light>();
            destelloGemelo.type = LightType.Point;
            destelloGemelo.color = Naranja;
            destelloGemelo.range = 1.4f;
            destelloGemelo.shadows = LightShadows.None;
        }
        destelloGemelo.enabled = true;
        destelloGemelo.intensity = 7f * (1f - t) * (1f - t);
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
        ArmaDeFuego conLa = ArmaDeFuego.Disparando; // el arma de fuego que mató (null: cuchillo, granada...)
        if (conLa != null && conLa.DeRafaga) AlMatarConLaRafaga(conLa);
        // US 311: baja a la cabeza con el francotirador.
        if (cabeza && conLa != null && EsFrancotirador(conLa.shopItem) && EleccionDeInvasion.Veces("Cerrojo suelto") > 0)
            conLa.AcortarCerrojo(CerrojoSuelto);

        int reciclador = EleccionDeInvasion.Veces("Reciclador");
        if (cabeza && reciclador > 0)
            MunicionDelArma.SumarAlCargador(MunicionDelArma.EnLaMano(Armas()), BalasDelReciclador * reciclador); // US 316
    }
}

// US 333: el pulso de Despeje, una burbuja holográfica verde (el color del puesto) que sale del centro del puesto, se
// agranda hasta el borde del anillo y se apaga. Empuja a cada invasor (de tierra o en el aire: cuenta la distancia en
// el plano) cuando el frente de la burbuja lo alcanza. Se ve también desde adentro (una cara para afuera y otra para
// adentro). Usa Efectos/Holograma, el material del perro del Callejero.
public class BurbujaDeDespeje : MonoBehaviour
{
    private const float Crece = 0.55f, Apaga = 0.35f; // s: hasta el borde del anillo y después, apagándose
    private static Mesh esfera, esferaAdentro;

    private Vector3 centro;
    private float radio, metros, desde;
    private Material material;
    private readonly System.Collections.Generic.List<(Npc npc, float distancia)> porEmpujar =
        new System.Collections.Generic.List<(Npc, float)>();

    /// <summary>Lanza la burbuja desde el centro del puesto: empuja esos metros a los invasores del anillo (no al Jefe).
    /// Devuelve cuántos va a empujar.</summary>
    public static int Lanzar(Vector3 centro, float radio, float metros)
    {
        var go = new GameObject("Burbuja de Despeje");
        go.transform.position = centro;
        var b = go.AddComponent<BurbujaDeDespeje>();
        b.centro = centro;
        b.radio = radio;
        b.metros = metros;
        b.desde = Time.time;
        foreach (Npc npc in Npc.Todos)
        {
            if (npc == null || !npc.Vivo || (npc.Tipo != null && npc.Tipo.esJefe)) continue;
            Vector3 d = npc.transform.position - centro;
            d.y = 0f;
            if (d.sqrMagnitude <= radio * radio) b.porEmpujar.Add((npc, d.magnitude));
        }
        b.Armar();
        b.Update();
        return b.porEmpujar.Count;
    }

    private void Armar()
    {
        Shader shader = Resources.Load<Shader>("Efectos/Holograma");
        if (shader == null) shader = Shader.Find("Efectos/Holograma");
        if (shader == null) return;
        material = new Material(shader) { name = "Burbuja de Despeje" };
        Color verde = MarcadorInvasion.Verde;
        material.SetColor("_BaseColor", new Color(verde.r, verde.g, verde.b, 0.9f));
        Mallas();
        Cara(esfera);
        Cara(esferaAdentro);
    }

    private void Cara(Mesh malla)
    {
        var go = new GameObject("Cara");
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = malla;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    // La esfera de Unity (radio 0,5) y una copia dada vuelta, para verla desde adentro.
    private static void Mallas()
    {
        if (esfera != null && esferaAdentro != null) return;
        GameObject p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        esfera = p.GetComponent<MeshFilter>().sharedMesh;
        Destroy(p);
        esferaAdentro = Instantiate(esfera);
        esferaAdentro.name = "Esfera dada vuelta";
        Vector3[] normales = esferaAdentro.normals;
        for (int i = 0; i < normales.Length; i++) normales[i] = -normales[i];
        esferaAdentro.normals = normales;
        int[] t = esferaAdentro.triangles;
        for (int i = 0; i < t.Length; i += 3) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
        esferaAdentro.triangles = t;
    }

    private void Update()
    {
        float t = Time.time - desde;
        float avance = Mathf.Clamp01(t / Crece);
        float r = Mathf.Lerp(0.3f, radio, 1f - (1f - avance) * (1f - avance)); // sale rápido y frena en el borde
        transform.localScale = Vector3.one * (r * 2f);

        // Empuja a cada uno cuando lo alcanza el frente.
        for (int i = porEmpujar.Count - 1; i >= 0; i--)
        {
            if (porEmpujar[i].distancia > r) continue;
            Npc npc = porEmpujar[i].npc;
            porEmpujar.RemoveAt(i);
            if (npc == null || !npc.Vivo) continue;
            Vector3 d = npc.transform.position - centro;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) d = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            EmpujonDeNpc.Empujar(npc, d, metros);
        }

        float apagado = Mathf.Clamp01((t - Crece) / Apaga);
        if (material != null) material.SetFloat("_Desvanecer", apagado);
        if (apagado >= 1f) Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
