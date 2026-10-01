using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;

// Un jugador de una partida online (US 025, US 028, US 029 y US 030).
// En la computadora de su dueño es el jugador de siempre: manda su estado (posición, mirada, animación, arma,
// vida y escudo) y avisa sus disparos, cuchillazos, muerte y reaparición.
// En las demás es una copia del Player.prefab sin cámara, controles ni pausa, que repite lo que le llega.
// La vida la decide el dueño: si alguien le pega a la copia, el daño se le manda a él (US 029).
public class JugadorEnRed : MonoBehaviourPun, IPunObservable
{
    private const float AlcanceTrazadora = 300f;
    private const byte Agachado = 1, Corriendo = 2, Cayendo = 4, Invulnerable = 8;
    // Arma en la mano: las principales van desde PrimeraPrincipal, en el orden de WeaponSwitcher.Principales().
    private const byte SinArma = 0, ArmaPistola = 2, ArmaCuchillo = 3, PrimeraPrincipal = 10;

    private HealthSystem vida;
    private Animator animador;
    private Transform camara;     // "Main Camera": su giro vertical es hacia dónde mira
    private List<GameObject> principales = new List<GameObject>();
    private GameObject pistola;
    private MeleeWeaponHolder cuchillo;
    private PartidaEnRed partida;
    private bool muerto;

    // ---------- Dueño ----------
    private PlayerMovement movimiento;
    private byte saltos, aterrizajes;
    private float finInvulnerable;
    private int ultimoAtacante;
    private readonly List<Behaviour> bloqueados = new List<Behaviour>();

    // ---------- Copia ----------
    private bool recibido;
    private Vector3 posicionRed, velocidadRed;
    private double tiempoRed;
    private float yawRed, pitchRed, velXRed, velZRed;
    private byte banderasRed, armaRed, saltosRed, aterrizajesRed;
    private byte saltosVistos, aterrizajesVistos, armaVista = 255;
    private float alturaParado = 0.75f, alturaAgachado = 0.3f;
    private GameObject cuchilloListo;
    private Transform modelo;
    private Collider[] colisiones;
    private AudioSource sonido;
    private AudioClip sonidoPistola, sonidoCuchillo;
    private readonly List<AudioClip> sonidosPrincipales = new List<AudioClip>();
    private TextMeshPro cartel;
    private Coroutine caida;

    public bool Vivo => !muerto;
    public string Nombre => photonView != null && photonView.Owner != null ? photonView.Owner.NickName : "Jugador";

    // Número de jugador de Photon y equipo táctico (US 031).
    public int Actor => photonView != null && photonView.Owner != null ? photonView.Owner.ActorNumber : -1;
    public int Equipo => EquiposTacticos.DeActor(Actor);

    // =====================================================================
    // Dueño
    // =====================================================================

    public void IniciarLocal(PartidaEnRed partida)
    {
        this.partida = partida;
        vida = GetComponent<HealthSystem>();
        movimiento = GetComponent<PlayerMovement>();
        animador = GetComponentInChildren<Animator>();
        cuchillo = GetComponent<MeleeWeaponHolder>();
        WeaponSwitcher cambio = GetComponentInChildren<WeaponSwitcher>(true);
        camara = cambio != null ? cambio.transform : transform.Find("Main Camera");
        if (cambio != null) { principales = cambio.Principales(); pistola = cambio.pistolObj; }

        if (movimiento != null)
        {
            movimiento.Jumped += () => saltos++;
            movimiento.Landed += () => aterrizajes++;
        }
        if (vida != null) vida.Died += AlMorir;
        WeaponFire.Fired += AlDisparar;
        MeleeAttack.Swung += AlAcuchillar;
    }

    private void OnDestroy()
    {
        WeaponFire.Fired -= AlDisparar;
        MeleeAttack.Swung -= AlAcuchillar;
        if (vida != null) vida.Died -= AlMorir;
    }

    private void AlDisparar(Transform tirador, Vector3 origen, Vector3[] direcciones)
    {
        if (tirador != transform || muerto) return;
        if (vida != null) vida.Invulnerable = false; // US 137, CA5: disparar corta la invulnerabilidad

        var plano = new float[direcciones.Length * 3];
        for (int i = 0; i < direcciones.Length; i++)
        {
            plano[i * 3] = direcciones[i].x;
            plano[i * 3 + 1] = direcciones[i].y;
            plano[i * 3 + 2] = direcciones[i].z;
        }
        photonView.RPC(nameof(RpcDisparo), RpcTarget.Others, origen, plano, ArmaEnMano());
    }

    private void AlAcuchillar(MeleeAttack ataque)
    {
        if (ataque.gameObject != gameObject || muerto) return;
        if (vida != null) vida.Invulnerable = false;
        photonView.RPC(nameof(RpcCuchillo), RpcTarget.Others);
    }

    // US 029: el daño que le hicieron a la copia de este jugador en otra computadora.
    [PunRPC]
    private void RpcDanio(int danio, int atacante, bool cabeza)
    {
        if (!photonView.IsMine || muerto || vida == null) return;
        if (EquiposTacticos.SonAliados(atacante, Actor)) return; // US 031, CA6: por las dudas, también acá
        ultimoAtacante = atacante;
        vida.TakeDamage(danio, cabeza);
    }

    // US 030: los demás lo ven caer. En Deathmatch reaparece; en Táctico espera la ronda siguiente (US 032).
    private void AlMorir()
    {
        if (muerto) return;
        muerto = true;
        bool cabeza = vida != null && vida.KilledByHeadshot;
        photonView.RPC(nameof(RpcMurio), RpcTarget.Others, ultimoAtacante, cabeza);
        Bloquear(true);
        AvisarBaja(ultimoAtacante, cabeza); // US 057, CA4

        Player asesino = ultimoAtacante != 0 && PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.GetPlayer(ultimoAtacante) : null;
        string titulo = asesino != null ? $"Te eliminó {asesino.NickName}" : "Te eliminaron";

        if (MatchSettings.Mode == GameMode.Deathmatch)
            StartCoroutine(Reaparecer(titulo, ConfigRed.Actual != null ? ConfigRed.Actual.reaparicion : 3f));
        // US 033: si la ronda ya terminó (por ejemplo, lo eliminó la explosión), el cartel de fin de ronda ya lo explica.
        else if (RondasTacticas.Actual == null || RondasTacticas.Actual.FaseActual == RondasTacticas.Fase.Combate)
            partida.Aviso(titulo, "Reaparecés en la ronda siguiente.");
    }

    private IEnumerator Reaparecer(string titulo, float espera)
    {
        float fin = Time.time + espera;
        while (Time.time < fin)
        {
            partida.Aviso(titulo, $"Reaparecés en {Mathf.CeilToInt(fin - Time.time)}");
            yield return null;
        }
        partida.Aviso(null);

        Pose punto = partida.PuntoDeReaparicion();
        Teletransportar(transform, punto.position, punto.rotation);
        vida.Revive();
        foreach (Pistola arma in GetComponentsInChildren<Pistola>(true)) arma.currentAmmo = arma.maxAmmo;
        foreach (Mitre arma in GetComponentsInChildren<Mitre>(true)) arma.Refill();
        foreach (ArmaDeFuego arma in GetComponentsInChildren<ArmaDeFuego>(true)) arma.Refill();
        muerto = false;
        ultimoAtacante = 0;
        Bloquear(false);

        float segundos = ConfigRed.Actual != null ? ConfigRed.Actual.invulnerabilidad : 2f;
        vida.Invulnerable = segundos > 0f;
        finInvulnerable = Time.time + segundos;

        photonView.RPC(nameof(RpcReaparecio), RpcTarget.Others, punto.position, punto.rotation.eulerAngles.y);
    }

    /// <summary>
    /// US 032, CA4 y CA7: arranca una ronda nueva del Táctico. Todos vuelven a su base; el que estaba muerto
    /// revive (sin escudo y con lo que le quedó después de morir) y el que estaba vivo conserva su escudo.
    /// </summary>
    public void EmpezarRonda(Pose punto)
    {
        if (photonView == null || !photonView.IsMine || vida == null) return;
        partida.Aviso(null);
        Teletransportar(transform, punto.position, punto.rotation);
        if (muerto) vida.Revive();
        else vida.SetState(vida.maxHealth, vida.currentShield);
        foreach (Pistola arma in GetComponentsInChildren<Pistola>(true)) arma.currentAmmo = arma.maxAmmo;
        foreach (Mitre arma in GetComponentsInChildren<Mitre>(true)) arma.Refill();
        foreach (ArmaDeFuego arma in GetComponentsInChildren<ArmaDeFuego>(true)) arma.Refill();
        muerto = false;
        ultimoAtacante = 0;
        Bloquear(false);
        vida.Invulnerable = false;
        photonView.RPC(nameof(RpcReaparecio), RpcTarget.Others, punto.position, punto.rotation.eulerAngles.y);
    }

    /// <summary>US 033: lo elimina la explosión del dispositivo (también lo usa la prueba solo).</summary>
    public void Eliminar()
    {
        if (photonView == null || !photonView.IsMine || muerto || vida == null) return;
        ultimoAtacante = 0;
        vida.Invulnerable = false;
        vida.TakeDamage(vida.currentHealth + vida.currentShield + 1);
    }

    // Muerto no se mueve, no dispara ni cambia de arma; puede seguir mirando alrededor.
    private void Bloquear(bool bloquear)
    {
        if (!bloquear)
        {
            foreach (Behaviour componente in bloqueados)
                if (componente != null) componente.enabled = true;
            bloqueados.Clear();
            return;
        }
        bloqueados.Clear();
        foreach (Behaviour componente in GetComponentsInChildren<Behaviour>(true))
            if (componente.enabled && (componente is PlayerMovement || componente is Pistola || componente is Mitre ||
                componente is ArmaDeFuego || componente is MeleeAttack || componente is WeaponSwitcher || componente is PlayerAbility))
            {
                componente.enabled = false;
                bloqueados.Add(componente);
            }
    }

    private float Pitch()
    {
        if (camara == null) return 0f;
        float x = camara.localEulerAngles.x;
        return x > 180f ? x - 360f : x;
    }

    private byte Banderas()
    {
        byte banderas = 0;
        if (animador != null)
        {
            if (animador.GetBool("isCrouching")) banderas |= Agachado;
            if (animador.GetBool("isSprinting")) banderas |= Corriendo;
            if (animador.GetBool("isFalling")) banderas |= Cayendo;
        }
        if (vida != null && vida.Invulnerable) banderas |= Invulnerable;
        return banderas;
    }

    // US 057, CA4 y CA7: el aviso "asesino [arma] víctima" con los nombres de cada jugador, igual en todas las computadoras,
    // con la marca de tiro a la cabeza si el golpe que lo mató fue a la cabeza.
    private void AvisarBaja(int atacante, bool cabeza)
    {
        Player asesino = atacante != 0 && PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.GetPlayer(atacante) : null;
        JugadorEnRed tirador = partida != null ? partida.Buscar(atacante) : null;
        MatchHud.ReportKill(asesino != null ? asesino.NickName : "", tirador != null ? tirador.NombreArma : "", Nombre,
            ColorDe(atacante), ColorDe(photonView.OwnerActorNr), cabeza, tirador != null ? tirador.IconoArma : null);
    }

    // Azul el propio equipo (y uno mismo), rojo el rival; blanco si no hay equipos.
    private static Color ColorDe(int actor)
    {
        if (actor == 0) return Color.white;
        bool yo = PhotonNetwork.LocalPlayer != null && actor == PhotonNetwork.LocalPlayer.ActorNumber;
        if (!EquiposTacticos.HayEquipos) return yo ? MatchHud.TeamColor : Color.white;
        return yo || EquiposTacticos.SonAliados(PhotonNetwork.LocalPlayer.ActorNumber, actor) ? MatchHud.TeamColor : MatchHud.RivalColor;
    }

    /// <summary>Nombre del arma que este jugador tiene en la mano (para los avisos de bajas).</summary>
    public string NombreArma => NombreDeArma(ArmaActual);

    /// <summary>Ícono (de la ficha de la tienda) del arma en la mano, o null.</summary>
    public Sprite IconoArma
    {
        get
        {
            byte arma = ArmaActual;
            ShopItem ficha = null;
            if (photonView != null && photonView.IsMine)
            {
                // Dueño: los scripts de armas siguen estando.
                if (arma == ArmaPistola && pistola != null)
                {
                    Pistola p = pistola.GetComponentInChildren<Pistola>(true);
                    ficha = p != null ? p.shopItem : null;
                }
                else if (arma >= PrimeraPrincipal && arma - PrimeraPrincipal < principales.Count)
                    ficha = WeaponSwitcher.FichaDe(principales[arma - PrimeraPrincipal]);
            }
            else if (arma == ArmaPistola) ficha = fichaPistola;
            else if (arma >= PrimeraPrincipal && arma - PrimeraPrincipal < fichasPrincipales.Count)
                ficha = fichasPrincipales[arma - PrimeraPrincipal];
            return ficha != null ? ficha.icon : null;
        }
    }

    // Fichas de la tienda de cada arma, guardadas al armar el jugador (en las copias se quitan los scripts de armas).
    private readonly List<ShopItem> fichasPrincipales = new List<ShopItem>();
    private ShopItem fichaPistola;

    private byte ArmaActual => photonView != null && photonView.IsMine ? ArmaEnMano() : armaRed;

    private string NombreDeArma(byte arma)
    {
        if (arma == ArmaCuchillo) return "Cuchillo";
        if (arma == ArmaPistola) return "Pistola";
        int i = arma - PrimeraPrincipal;
        if (arma >= PrimeraPrincipal && i < principales.Count && principales[i] != null)
        {
            IHudWeapon hud = principales[i].GetComponentInChildren<IHudWeapon>(true);
            return hud != null ? hud.HudName : principales[i].name;
        }
        return "";
    }

    private byte ArmaEnMano()
    {
        for (int i = 0; i < principales.Count; i++)
            if (principales[i] != null && principales[i].activeSelf) return (byte)(PrimeraPrincipal + i);
        if (pistola != null && pistola.activeSelf) return ArmaPistola;
        if (cuchillo != null && cuchillo.CurrentViewModel != null && cuchillo.CurrentViewModel.activeSelf) return ArmaCuchillo;
        return SinArma;
    }

    // =====================================================================
    // Copia
    // =====================================================================

    // Arma la copia de otro jugador a partir del Player.prefab. Se instancia bajo un objeto apagado para sacarle
    // lo que es solo del dueño antes de que corra nada: cámaras, controles, armas que leen el mouse, tienda y pausa.
    public static JugadorEnRed CrearCopia(GameObject molde, Vector3 posicion, Quaternion rotacion, int viewId, Player dueno, PartidaEnRed partida)
    {
        var soporte = new GameObject("Cargando jugador");
        soporte.SetActive(false);
        GameObject go = Instantiate(molde, posicion, rotacion, soporte.transform);
        go.name = "Jugador en red (" + dueno.NickName + ")";
        go.tag = "Untagged";

        WeaponSwitcher cambio = go.GetComponentInChildren<WeaponSwitcher>(true);
        Pistola pistola = go.GetComponentInChildren<Pistola>(true);
        MeleeAttack ataque = go.GetComponent<MeleeAttack>();
        PlayerMovement movimiento = go.GetComponent<PlayerMovement>();

        var jugador = go.AddComponent<JugadorEnRed>();
        jugador.partida = partida;
        jugador.camara = cambio != null ? cambio.transform : go.transform.Find("Main Camera");
        if (cambio != null) jugador.principales = cambio.Principales();
        foreach (GameObject arma in jugador.principales)
        {
            ArmaDeFuego fuego = arma.GetComponent<ArmaDeFuego>();
            Mitre mitre = arma.GetComponent<Mitre>();
            jugador.sonidosPrincipales.Add(fuego != null ? fuego.shootSound : mitre != null ? mitre.shootSound : null);
            jugador.fichasPrincipales.Add(WeaponSwitcher.FichaDe(arma)); // US 057: antes de quitar los componentes
        }
        jugador.fichaPistola = pistola != null ? pistola.shopItem : null;
        jugador.pistola = cambio != null ? cambio.pistolObj : pistola != null ? pistola.gameObject : null;
        jugador.sonidoPistola = pistola != null ? pistola.shootSound : null;
        jugador.sonidoCuchillo = ataque != null ? ataque.SwingSound : null;
        if (movimiento != null) { jugador.alturaParado = movimiento.standingCameraHeight; jugador.alturaAgachado = movimiento.crouchCameraHeight; }
        AudioMixerGroup efectos = pistola != null ? pistola.sfxGroup : null;

        foreach (PauseMenu pausa in go.GetComponentsInChildren<PauseMenu>(true)) DestroyImmediate(pausa.gameObject);
        Quitar<CameraLook>(go);
        Quitar<WeaponSwitcher>(go);
        Quitar<Pistola>(go);
        Quitar<Mitre>(go);
        Quitar<ArmaDeFuego>(go);
        Quitar<MeleeAttack>(go);
        Quitar<PlayerAbility>(go);
        Quitar<PlayerLoadout>(go); // antes que la billetera, que la necesita
        Quitar<PlayerWallet>(go);
        Quitar<PlayerMovement>(go);
        Quitar<HideOwnBody>(go);
        Quitar<AudioListener>(go);
        Quitar<CharacterController>(go); // la copia se mueve con lo que llega; la cápsula sigue chocando
        foreach (Camera c in go.GetComponentsInChildren<Camera>(true))
        {
            c.enabled = false;
            c.gameObject.tag = "Untagged";
        }
        // Las armas en la mano se dibujan con una cámara aparte (capa ArmaEnMano): en la copia van con el resto del mundo.
        foreach (GameObject arma in jugador.principales) PonerCapa(arma, 0);
        PonerCapa(jugador.pistola, 0);

        jugador.animador = go.GetComponentInChildren<Animator>(true);
        if (jugador.animador != null)
        {
            jugador.animador.applyRootMotion = false;
            jugador.modelo = jugador.animador.transform;
        }
        jugador.vida = go.GetComponent<HealthSystem>();
        jugador.cuchillo = go.GetComponent<MeleeWeaponHolder>();
        jugador.colisiones = go.GetComponentsInChildren<Collider>(true);

        var vista = go.AddComponent<PhotonView>();
        vista.ObservedComponents = new List<Component> { jugador };
        vista.Synchronization = ViewSynchronization.UnreliableOnChange;
        vista.OwnershipTransfer = OwnershipOption.Fixed;

        go.transform.SetParent(null, true);
        Destroy(soporte);
        vista.ViewID = viewId;
        jugador.IniciarCopia(efectos);
        return jugador;
    }

    private static void Quitar<T>(GameObject go) where T : Component
    {
        foreach (T componente in go.GetComponentsInChildren<T>(true)) DestroyImmediate(componente);
    }

    private static void PonerCapa(GameObject go, int capa)
    {
        if (go == null) return;
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = capa;
    }

    private void IniciarCopia(AudioMixerGroup efectos)
    {
        posicionRed = transform.position;
        yawRed = transform.eulerAngles.y;

        sonido = gameObject.AddComponent<AudioSource>();
        sonido.playOnAwake = false;
        sonido.spatialBlend = 1f;
        sonido.minDistance = 3f;
        sonido.maxDistance = 80f;
        sonido.rolloffMode = AudioRolloffMode.Linear;
        sonido.outputAudioMixerGroup = efectos;

        // US 029: el daño a esta copia se le manda al dueño, que es el que sabe su vida.
        if (vida != null)
            vida.DamageRedirect = (danio, cabeza) =>
            {
                if (photonView.Owner != null)
                    photonView.RPC(nameof(RpcDanio), photonView.Owner, danio, PhotonNetwork.LocalPlayer.ActorNumber, cabeza);
            };

        CrearCartel();
    }

    private void Update()
    {
        if (photonView == null) return;
        if (photonView.IsMine)
        {
            if (vida != null && vida.Invulnerable && Time.time >= finInvulnerable) vida.Invulnerable = false;
            return;
        }
        if (!recibido) return;

        // US 025: se acerca a lo último que llegó, adelantándolo según su velocidad lo que tardó en llegar.
        float dt = Time.deltaTime;
        float atraso = Mathf.Clamp((float)(PhotonNetwork.Time - tiempoRed), 0f, 0.2f);
        Vector3 objetivo = posicionRed + (muerto ? Vector3.zero : velocidadRed * atraso);
        if ((transform.position - objetivo).sqrMagnitude > 16f) transform.position = objetivo;
        else transform.position = Vector3.Lerp(transform.position, objetivo, 1f - Mathf.Exp(-15f * dt));
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, yawRed, 0f), 1f - Mathf.Exp(-20f * dt));

        bool agachado = (banderasRed & Agachado) != 0;
        if (camara != null)
        {
            camara.localRotation = Quaternion.Slerp(camara.localRotation, Quaternion.Euler(pitchRed, 0f, 0f), 1f - Mathf.Exp(-20f * dt));
            Vector3 lugar = camara.localPosition;
            lugar.y = Mathf.Lerp(lugar.y, agachado ? alturaAgachado : alturaParado, 1f - Mathf.Exp(-8f * dt));
            camara.localPosition = lugar;
        }

        if (animador != null && !muerto)
        {
            animador.SetFloat("VelX", velXRed, 0.08f, dt);
            animador.SetFloat("VelZ", velZRed, 0.08f, dt);
            animador.SetBool("isCrouching", agachado);
            animador.SetBool("isSprinting", (banderasRed & Corriendo) != 0);
            animador.SetBool("isFalling", (banderasRed & Cayendo) != 0);
            if (saltosVistos != saltosRed) { saltosVistos = saltosRed; animador.SetTrigger("Jump"); }
            if (aterrizajesVistos != aterrizajesRed) { aterrizajesVistos = aterrizajesRed; animador.SetTrigger("Land"); }
        }
        if (vida != null) vida.Invulnerable = (banderasRed & Invulnerable) != 0;

        MostrarArma(muerto ? SinArma : armaRed);
        ActualizarCartel();
    }

    private void MostrarArma(byte arma)
    {
        // El cuchillo lo arma MeleeWeaponHolder en su Start: cuando aparece, se pasa a la capa del mundo.
        GameObject modeloCuchillo = cuchillo != null ? cuchillo.CurrentViewModel : null;
        if (modeloCuchillo != cuchilloListo)
        {
            cuchilloListo = modeloCuchillo;
            PonerCapa(modeloCuchillo, 0);
            armaVista = 255;
        }
        if (arma == armaVista) return;
        armaVista = arma;
        for (int i = 0; i < principales.Count; i++)
            if (principales[i] != null) principales[i].SetActive(arma == PrimeraPrincipal + i);
        if (pistola != null) pistola.SetActive(arma == ArmaPistola);
        if (modeloCuchillo != null) modeloCuchillo.SetActive(arma == ArmaCuchillo);
    }

    // US 028: el disparo se ve y se escucha desde el arma de la copia, con la trazadora hasta donde pegó y la
    // marca en el escenario. El daño ya lo calculó el que disparó.
    [PunRPC]
    private void RpcDisparo(Vector3 origen, float[] direcciones, byte arma)
    {
        if (photonView.IsMine) return;
        armaRed = arma;
        MostrarArma(arma);
        int principal = arma - PrimeraPrincipal;
        bool esPrincipal = principal >= 0 && principal < principales.Count;
        AudioClip clip = esPrincipal ? sonidosPrincipales[principal] : sonidoPistola;
        if (clip != null && sonido != null) sonido.PlayOneShot(clip);

        GameObject enMano = esPrincipal ? principales[principal] : pistola;
        Vector3 boca = enMano != null ? enMano.transform.position : origen;
        for (int i = 0; i + 2 < direcciones.Length; i += 3)
        {
            Vector3 direccion = new Vector3(direcciones[i], direcciones[i + 1], direcciones[i + 2]);
            Vector3 fin = WeaponFire.Replay(origen, direccion, AlcanceTrazadora, transform);
            Trazadora(boca, fin);
        }
    }

    [PunRPC]
    private void RpcCuchillo()
    {
        if (photonView.IsMine) return;
        armaRed = ArmaCuchillo;
        MostrarArma(ArmaCuchillo);
        if (sonidoCuchillo != null && sonido != null) sonido.PlayOneShot(sonidoCuchillo);
        if (cuchilloListo != null) StartCoroutine(Estocada(cuchilloListo.transform));
    }

    private static IEnumerator Estocada(Transform hoja)
    {
        Vector3 inicio = hoja.localPosition, adelante = inicio + Vector3.forward * 0.3f;
        for (float t = 0f; t < 0.2f && hoja != null; t += Time.deltaTime)
        {
            hoja.localPosition = Vector3.Lerp(inicio, adelante, t < 0.08f ? t / 0.08f : 1f - (t - 0.08f) / 0.12f);
            yield return null;
        }
        if (hoja != null) hoja.localPosition = inicio;
    }

    private void Trazadora(Vector3 desde, Vector3 hasta)
    {
        Material material = ConfigRed.Actual != null ? ConfigRed.Actual.trazadora : null;
        if (material == null) return;
        var go = new GameObject("Trazadora");
        var linea = go.AddComponent<LineRenderer>();
        linea.sharedMaterial = material;
        linea.positionCount = 2;
        linea.SetPosition(0, desde);
        linea.SetPosition(1, hasta);
        linea.startWidth = 0.025f;
        linea.endWidth = 0.01f;
        linea.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        linea.receiveShadows = false;
        Destroy(go, 0.05f);
    }

    // US 030, CA1 y CA2: el cuerpo cae y ya no recibe disparos.
    [PunRPC]
    private void RpcMurio(int atacante, bool cabeza)
    {
        if (photonView.IsMine) return;
        muerto = true;
        AvisarBaja(atacante, cabeza); // US 057, CA4
        if (vida != null) vida.SetState(0, 0);
        foreach (Collider c in colisiones) if (c != null) c.enabled = false;
        MostrarArma(SinArma);
        if (caida != null) StopCoroutine(caida);
        caida = StartCoroutine(Caer(true));
    }

    // US 030, CA3 y CA5: aparece en el punto que eligió el dueño, sin deslizarse desde donde murió.
    [PunRPC]
    private void RpcReaparecio(Vector3 posicion, float yaw)
    {
        if (photonView.IsMine) return;
        muerto = false;
        transform.SetPositionAndRotation(posicion, Quaternion.Euler(0f, yaw, 0f));
        posicionRed = posicion;
        velocidadRed = Vector3.zero;
        yawRed = yaw;
        if (vida != null) vida.SetState(vida.maxHealth, 0);
        foreach (Collider c in colisiones) if (c != null) c.enabled = true;
        if (caida != null) StopCoroutine(caida);
        caida = StartCoroutine(Caer(false));
    }

    private IEnumerator Caer(bool cae)
    {
        if (modelo == null) yield break;
        Quaternion desde = modelo.localRotation, hasta = cae ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.identity;
        float duracion = cae ? 0.45f : 0f;
        for (float t = 0f; t < duracion; t += Time.deltaTime)
        {
            float k = t / duracion;
            modelo.localRotation = Quaternion.Slerp(desde, hasta, k * k);
            yield return null;
        }
        modelo.localRotation = hasta;
    }

    // Nombre y vida arriba de la cabeza (US 029, CA3: todos ven la misma vida).
    private void CrearCartel()
    {
        var go = new GameObject("Nombre");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 1.35f, 0f);
        cartel = go.AddComponent<TextMeshPro>();
        cartel.fontSize = 2.2f;
        cartel.alignment = TextAlignmentOptions.Center;
        cartel.textWrappingMode = TextWrappingModes.NoWrap;
        cartel.rectTransform.sizeDelta = new Vector2(3f, 0.6f);
        cartel.outlineWidth = 0.25f;
        cartel.outlineColor = new Color32(0, 0, 0, 220);
    }

    private void ActualizarCartel()
    {
        if (cartel == null) return;
        cartel.gameObject.SetActive(!muerto);
        if (muerto) return;

        int llenas = vida != null ? Mathf.CeilToInt(10f * vida.currentHealth / Mathf.Max(1, vida.maxHealth)) : 10;
        int escudo = vida != null && vida.currentShield > 0 ? Mathf.CeilToInt(5f * vida.currentShield / Mathf.Max(1, vida.maxShield)) : 0;
        string barra = "<color=#7DE05A>" + new string('|', llenas) + "</color><color=#FFFFFF40>" + new string('|', 10 - llenas) + "</color>";
        if (escudo > 0) barra += " <color=#6CB8FF>" + new string('|', escudo) + "</color>";
        // US 031, CA5: en Táctico el nombre va en verde si es aliado y en rojo si es rival.
        string nombre = Nombre;
        string tinte = EquiposTacticos.ColorHexDe(Actor);
        if (tinte != null) nombre = $"<color={tinte}>{nombre}</color>";
        cartel.text = nombre + "\n<size=70%>" + barra + "</size>";

        Camera mirando = Camera.main;
        if (mirando != null) cartel.transform.rotation = mirando.transform.rotation;
    }

    // =====================================================================
    // Red
    // =====================================================================

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.eulerAngles.y);
            stream.SendNext(Pitch());
            stream.SendNext(animador != null ? animador.GetFloat("VelX") : 0f);
            stream.SendNext(animador != null ? animador.GetFloat("VelZ") : 0f);
            stream.SendNext(Banderas());
            stream.SendNext(muerto ? SinArma : ArmaEnMano());
            stream.SendNext(saltos);
            stream.SendNext(aterrizajes);
            stream.SendNext(vida != null ? (short)vida.currentHealth : (short)0);
            stream.SendNext(vida != null ? (short)vida.currentShield : (short)0);
            return;
        }

        Vector3 posicion = (Vector3)stream.ReceiveNext();
        yawRed = (float)stream.ReceiveNext();
        pitchRed = (float)stream.ReceiveNext();
        velXRed = (float)stream.ReceiveNext();
        velZRed = (float)stream.ReceiveNext();
        banderasRed = (byte)stream.ReceiveNext();
        armaRed = (byte)stream.ReceiveNext();
        saltosRed = (byte)stream.ReceiveNext();
        aterrizajesRed = (byte)stream.ReceiveNext();
        short salud = (short)stream.ReceiveNext();
        short blindaje = (short)stream.ReceiveNext();

        double tiempo = info.SentServerTime;
        if (recibido && tiempo > tiempoRed)
            velocidadRed = Vector3.ClampMagnitude((posicion - posicionRed) / (float)(tiempo - tiempoRed), 20f);
        if (!recibido)
        {
            // Los contadores arrancan donde está el dueño: no se repiten saltos viejos.
            saltosVistos = saltosRed;
            aterrizajesVistos = aterrizajesRed;
        }
        posicionRed = posicion;
        tiempoRed = tiempo;
        recibido = true;
        if (!muerto && vida != null) vida.SetState(salud, blindaje);
    }

    // =====================================================================
    // Utilidades
    // =====================================================================

    public static void Teletransportar(Transform jugador, Vector3 posicion, Quaternion rotacion)
    {
        CharacterController cuerpo = jugador.GetComponent<CharacterController>();
        bool prendido = cuerpo != null && cuerpo.enabled;
        if (prendido) cuerpo.enabled = false;
        jugador.SetPositionAndRotation(posicion, rotacion);
        if (prendido) cuerpo.enabled = true;
        Physics.SyncTransforms();
    }
}
