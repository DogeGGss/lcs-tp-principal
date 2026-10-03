using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using Img = UnityEngine.UI.Image;

// El dispositivo del Modo Táctico. Lo agrega RondasTacticas; corre en todas las computadoras.
// US 130, portar y soltar:
// - CA1: al empezar cada ronda (en la compra), el anfitrión se lo da a un atacante al azar.
// - CA2: sus compañeros ven el ícono del dispositivo sobre él y el rombo en el marcador (US 134); los defensores no.
// - CA3: si el portador muere, queda en el piso donde murió.
// - CA4: ocupa su propio espacio (tecla 5). Con el dispositivo en la mano, G lo suelta para pasárselo a un compañero
//   (con un arma en la mano, G suelta el arma, US 184).
// - CA5: un atacante que pasa por encima lo levanta solo; los defensores no.
// - CA6: en el piso, todos ven dónde está con un ícono en pantalla.
// US 131, plantar: el portador, dentro de una zona de plantado (ZonaDePlantado) y en el combate, mantiene E 4 s
// (la tecla de "Plantar / desactivar"). No se puede mover ni disparar y ve una barra; si suelta la tecla, se mueve o
// muere, se corta (CA2 y CA3). Al terminar se llama a RondasTacticas.Plantar: el reloj pasa a 45 s (CA4), el premio
// (CA5) y la explosión (CA6) ya los hace RondasTacticas. Acá: el modelo plantado, el aviso, la cuenta y la explosión.
// US 132, desactivar: un defensor a menos de 1,5 m mantiene E 7 s, con las mismas reglas (CA1 y CA2). Mientras tanto
// el dispositivo suena distinto para todos (CA3). Al terminar se llama a RondasTacticas.Desactivar (CA4); si explota
// antes, ganan los atacantes (CA5, lo decide RondasTacticas).
// Cómo funciona: quién lo lleva, o dónde quedó en el piso, son propiedades de la sala. Los cambios se piden con
// "comparar y cambiar" (Photon solo los aplica si lo esperado sigue igual): si dos atacantes llegan a la vez a
// levantarlo, se lo queda uno solo. Plantado y desactivado viven en RondasTacticas.
public class DispositivoTactico : MonoBehaviour
{
    public const float TiempoPlantar = 4f, TiempoDesactivar = 7f; // US 131, CA2 y US 132, CA1
    public const float AlcanceDesactivar = 1.5f;                  // US 132, CA1
    private const float AlcanceLevantar = 0.9f;     // a cuánto de los pies se levanta (en horizontal)
    private const float SinVolverALevantar = 1.2f;  // el que lo soltó no lo levanta enseguida
    private const float TiroAdelante = 1.2f;        // a cuántos metros de los pies cae al soltarlo
    private const float AlturaMarcaPortador = 2.15f;

    private const string PropRonda = "dp.ronda", PropEstado = "dp.est", PropPortador = "dp.port", PropPiso = "dp.piso",
        PropGiro = "dp.giro", PropVersion = "dp.ver", PropManipula = "dp.manip";
    private enum Estado { Nadie, Portado, EnElPiso }
    private enum Accion { Nada, Plantando, Desactivando }

    public static DispositivoTactico Actual { get; private set; }

    private PartidaEnRed partida;
    private RondasTacticas rondas;
    private ConfigRed config;
    private AudioMixerGroup grupo;
    private float esperaAnfitrion, perdidoDesde = -1f;
    private Vector3? ultimoLugarPortador;

    // ---------- Jugador de esta computadora ----------
    private JugadorEnRed local;
    private HealthSystem vida;
    private WeaponSwitcher armas;
    private PlayerMovement movimiento;
    private CharacterController cuerpo;
    private Transform camara;
    private GameObject enMano;     // el dispositivo en primera persona (espacio 5)
    private Vector3 enManoLugar;
    private Accion accion;
    private float accionDesde, velocidadAntes = 1f;
    private float pedidoHasta, sinLevantarHasta;
    private string accionTexto;
    private float? accionProgreso;

    // ---------- Lo que se ve, en todas las computadoras ----------
    private class Modelo
    {
        public GameObject go;
        public TMP_Text pantalla;
        public Renderer luz;
        public Color luzColor;
    }
    private Modelo enPiso, plantado;
    private int versionEnPiso = -1;
    private Vector3 enPisoDesde, enPisoHasta;
    private float enPisoCae = -1f;
    private bool exploto;
    private AudioSource cuenta, manipulacion;
    private int manipulaVisto;
    private float proximoBip, luzHasta;

    private class Marca
    {
        public RectTransform rect;
        public Img luz;
        public TextMeshProUGUI texto;
    }
    private Marca marcaPortador, marcaPiso, marcaPlantado;

    // =====================================================================
    // Inicio
    // =====================================================================

    public void Iniciar(PartidaEnRed partida, RondasTacticas rondas)
    {
        this.partida = partida;
        this.rondas = rondas;
        Actual = this;
        config = ConfigRed.Actual;
        RondasTacticas.DispositivoPlantado += AlPlantar;
        RondasTacticas.DispositivoExploto += AlExplotar;
        RondasTacticas.DispositivoDesactivado += AlDesactivar;
    }

    private void OnDestroy()
    {
        RondasTacticas.DispositivoPlantado -= AlPlantar;
        RondasTacticas.DispositivoExploto -= AlExplotar;
        RondasTacticas.DispositivoDesactivado -= AlDesactivar;
        if (vida != null) vida.Died -= AlMorir;
        if (Actual == this) Actual = null;
        MatchHud.SetAction(null);
    }

    // El jugador local y sus piezas. Se busca hasta que esté (PartidaEnRed lo crea antes que las rondas).
    private bool BuscarLocal()
    {
        if (local != null) return true;
        local = partida != null ? partida.Local : null;
        if (local == null) return false;
        vida = local.GetComponent<HealthSystem>();
        armas = local.GetComponentInChildren<WeaponSwitcher>(true);
        movimiento = local.GetComponent<PlayerMovement>();
        cuerpo = local.GetComponent<CharacterController>();
        camara = armas != null ? armas.transform : local.Ojos;
        if (vida != null) vida.Died += AlMorir;
        // El grupo SFX del mezclador, el mismo de las armas.
        Pistola pistola = local.GetComponentInChildren<Pistola>(true);
        grupo = pistola != null ? pistola.sfxGroup : null;
        return true;
    }

    // =====================================================================
    // Estado (lo que está en la sala)
    // =====================================================================

    private static Room Sala => PhotonNetwork.CurrentRoom;
    private static int Yo => PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0;

    private static int Leer(string clave, int porDefecto) =>
        Sala != null && Sala.CustomProperties.TryGetValue(clave, out object v) && v is int n ? n : porDefecto;

    // El estado es de esta ronda (al empezar otra, hasta que el anfitrión lo reparte, no hay dispositivo).
    private bool DeEstaRonda => Leer(PropRonda, 0) == rondas.Ronda;
    private Estado EstadoActual => DeEstaRonda ? (Estado)Leer(PropEstado, 0) : Estado.Nadie;
    private int Version => Leer(PropVersion, 0);
    private Vector3 Piso => Sala != null && Sala.CustomProperties.TryGetValue(PropPiso, out object v) && v is Vector3 p ? p : Vector3.zero;
    private Quaternion Giro => Sala != null && Sala.CustomProperties.TryGetValue(PropGiro, out object v) && v is Quaternion q ? q : Quaternion.identity;

    private bool EnJuego
    {
        get
        {
            RondasTacticas.Fase fase = rondas.FaseActual;
            return fase == RondasTacticas.Fase.Compra || fase == RondasTacticas.Fase.Combate;
        }
    }

    /// <summary>El actor que lleva el dispositivo ahora (0 si nadie: está en el piso, plantado o no se repartió).</summary>
    public int Portador => EnJuego && !rondas.HayDispositivo && EstadoActual == Estado.Portado ? Leer(PropPortador, 0) : 0;

    private bool Llevo => Portador != 0 && Portador == Yo;
    private bool EnElPiso => EnJuego && !rondas.HayDispositivo && EstadoActual == Estado.EnElPiso;

    private static bool EsAtacante(int actor) =>
        EquiposTacticos.LadoDeEquipo(EquiposTacticos.DeActor(actor)) == LadoTactico.Atacante;

    // =====================================================================
    // Cuadro a cuadro
    // =====================================================================

    private void Update()
    {
        if (rondas == null || !rondas.Listo || Sala == null) return;
        if (PhotonNetwork.IsMasterClient) Anfitrion();
        if (BuscarLocal()) Jugador();
        Ver();
        MarcadorTactico.SetPortador(Portador); // US 134, CA5 (el rombo lo ven solo sus compañeros)
    }

    private void LateUpdate()
    {
        if (rondas == null || !rondas.Listo || Sala == null) return;
        Marcas();
    }

    // =====================================================================
    // Anfitrión: reparte el dispositivo y lo suelta si el portador se va
    // =====================================================================

    private void Anfitrion()
    {
        if (Time.unscaledTime < esperaAnfitrion || !EnJuego) return;

        // CA1: una vez por ronda. Si se perdió la compra (por ejemplo, cambió el anfitrión), en el combate.
        if (!DeEstaRonda)
        {
            Repartir(rondas.FaseActual == RondasTacticas.Fase.Combate);
            return;
        }

        // CA3: el portador se fue de la sala, o murió y su computadora no lo soltó: queda donde se lo vio por última vez.
        int portador = Portador;
        if (portador != 0)
        {
            JugadorEnRed jugador = partida.Buscar(portador);
            if (jugador != null) ultimoLugarPortador = jugador.transform.position;
            bool perdido = Sala.GetPlayer(portador) == null || (jugador != null && !jugador.Vivo);
            if (!perdido) perdidoDesde = -1f;
            else if (perdidoDesde < 0f) perdidoDesde = Time.unscaledTime;
            else if (Time.unscaledTime - perdidoDesde > 2f)
            {
                perdidoDesde = -1f;
                if (ultimoLugarPortador.HasValue)
                {
                    Lugar(ultimoLugarPortador.Value + Vector3.up, 0f, out Vector3 punto, out Quaternion giro);
                    Publicar(new Hashtable
                    {
                        { PropEstado, (int)Estado.EnElPiso }, { PropPortador, 0 }, { PropPiso, punto }, { PropGiro, giro },
                        { PropVersion, Version + 1 }
                    });
                }
                else Repartir(true);
            }
        }
        else perdidoDesde = -1f;

        // El que plantaba o desactivaba se fue o murió: se corta su sonido (US 132, CA3).
        int manipula = Leer(PropManipula, 0);
        if (manipula != 0)
        {
            JugadorEnRed jugador = partida.Buscar(manipula);
            if (Sala.GetPlayer(manipula) == null || (jugador != null && !jugador.Vivo)) Publicar(new Hashtable { { PropManipula, 0 } });
        }
    }

    // CA1: a un atacante al azar (en la compra, todos; en el combate, solo los vivos).
    private void Repartir(bool soloVivos)
    {
        var atacantes = new List<int>();
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            if (!EsAtacante(p.ActorNumber)) continue;
            JugadorEnRed jugador = partida.Buscar(p.ActorNumber);
            if (soloVivos && jugador != null && !jugador.Vivo) continue;
            atacantes.Add(p.ActorNumber);
        }
        Dar(atacantes.Count > 0 ? atacantes[Random.Range(0, atacantes.Count)] : 0);
    }

    // Se lo da a ese jugador (0: a nadie). Solo el anfitrión.
    private void Dar(int actor)
    {
        ultimoLugarPortador = null;
        Publicar(new Hashtable
        {
            { PropRonda, rondas.Ronda }, { PropEstado, (int)(actor != 0 ? Estado.Portado : Estado.Nadie) }, { PropPortador, actor },
            { PropVersion, Version + 1 }, { PropManipula, 0 }
        });
    }

    /// <summary>Prueba solo (F3): el anfitrión pasa a llevar el dispositivo, o lo deja de llevar.</summary>
    public static void CambiarPortadorDePrueba()
    {
        DispositivoTactico d = Actual;
        if (d == null || !PhotonNetwork.IsMasterClient || !d.EnJuego || d.rondas.HayDispositivo) return;
        d.Dar(d.Portador == Yo ? 0 : Yo);
    }

    private void Publicar(Hashtable datos)
    {
        Sala.SetCustomProperties(datos);
        esperaAnfitrion = Time.unscaledTime + 0.5f;
    }

    // =====================================================================
    // Jugador de esta computadora: llevar, soltar, levantar, plantar y desactivar
    // =====================================================================

    private void Jugador()
    {
        bool vivo = local.Vivo;
        bool llevo = Llevo && vivo;
        if (llevo && enMano == null) CrearEnMano();
        if (armas != null) armas.LlevaDispositivo = llevo;
        // Ya no lo lleva (lo soltó, lo plantó o terminó la ronda) y lo tenía en la mano: vuelve a su arma.
        if (enMano != null && enMano.activeSelf && !llevo)
        {
            if (vivo && armas != null) armas.VolverAlArma(); else enMano.SetActive(false);
        }

        if (!vivo || !EnJuego)
        {
            CortarAccion();
            MostrarAccion(null, null);
            return;
        }

        bool libre = !PauseMenu.IsPaused && !ShopUI.IsOpen;

        // CA4: con el dispositivo en la mano, G lo suelta.
        if (llevo && accion == Accion.Nada && libre && armas != null && armas.DispositivoEquipado &&
            KeyBindings.Down(GameAction.Soltar) && Time.unscaledTime > pedidoHasta)
            Soltar(false);

        // CA5: un atacante que pasa por encima lo levanta (probando solo, cualquiera).
        if (!llevo && EnElPiso && (EsAtacante(Yo) || PruebaSolo.Activa) && Time.time > sinLevantarHasta && Time.unscaledTime > pedidoHasta)
        {
            Vector3 hasta = Piso - Pies();
            float vertical = Mathf.Abs(hasta.y);
            hasta.y = 0f;
            if (hasta.magnitude <= AlcanceLevantar && vertical <= 1.2f) Levantar();
        }

        Acciones(llevo, libre);
    }

    // US 131 y US 132: mantener E para plantar o desactivar.
    private void Acciones(bool llevo, bool libre)
    {
        bool combate = rondas.FaseActual == RondasTacticas.Fase.Combate;
        Vector3 pies = Pies();
        ZonaDePlantado zona = llevo && combate ? ZonaDePlantado.En(pies) : null;
        bool puedePlantar = zona != null;
        // Probando solo, el mismo jugador puede plantar y desactivar.
        bool defiende = EquiposTacticos.LadoLocal == LadoTactico.Defensor || PruebaSolo.Activa;
        bool puedeDesactivar = combate && rondas.HayDispositivo && !rondas.EstaDesactivado && !exploto && defiende &&
                               CercaDelPlantado(pies);
        bool apretada = KeyBindings.Held(GameAction.Plantar);

        if (accion != Accion.Nada)
        {
            // CA3 (US 131) y CA2 (US 132): si suelta la tecla, se mueve o ya no puede, se corta y vuelve a 0.
            bool puede = accion == Accion.Plantando ? puedePlantar : puedeDesactivar;
            if (!apretada || !libre || SeMueve() || !puede) CortarAccion();
            else
            {
                float duracion = accion == Accion.Plantando ? TiempoPlantar : TiempoDesactivar;
                float t = (Time.time - accionDesde) / duracion;
                if (t >= 1f) { Terminar(); MostrarAccion(null, null); return; }
                if (accion == Accion.Plantando && enMano != null)
                    // El dispositivo baja hacia el piso mientras se planta.
                    enMano.transform.localPosition = enManoLugar + new Vector3(-0.08f, -0.12f, 0.1f) * Mathf.SmoothStep(0f, 1f, t);
                MostrarAccion(accion == Accion.Plantando ? $"Plantando el dispositivo en {zona.nombre}" : "Desactivando el dispositivo", t);
                return;
            }
        }

        string tecla = $"<color=#F29A38>{KeyBindings.Label(GameAction.Plantar)}</color>";
        string texto = puedePlantar ? $"Mantené {tecla} para plantar el dispositivo en {zona.nombre}"
            : puedeDesactivar ? $"Mantené {tecla} para desactivar el dispositivo" : null;
        MostrarAccion(texto, null);
        if ((puedePlantar || puedeDesactivar) && apretada && libre && !SeMueve() && EnElSuelo())
            Empezar(puedePlantar ? Accion.Plantando : Accion.Desactivando);
    }

    private void Empezar(Accion nueva)
    {
        accion = nueva;
        accionDesde = Time.time;
        if (armas != null)
        {
            armas.Ocupado = true;
            // Plantando, el dispositivo en la mano; desactivando, nada: no se dispara.
            armas.GuardarTodo();
            if (nueva == Accion.Plantando) armas.SacarDispositivo();
        }
        if (movimiento != null)
        {
            velocidadAntes = movimiento.speedMultiplier;
            movimiento.speedMultiplier = 0f; // no se mueve
        }
        Sala.SetCustomProperties(new Hashtable { { PropManipula, Yo } }); // para el sonido (US 132, CA3)
    }

    private void CortarAccion()
    {
        if (accion == Accion.Nada) return;
        accion = Accion.Nada;
        if (enMano != null) enMano.transform.localPosition = enManoLugar;
        if (movimiento != null) movimiento.speedMultiplier = velocidadAntes;
        if (armas != null)
        {
            armas.Ocupado = false;
            if (local != null && local.Vivo) armas.VolverAlArma();
        }
        if (Leer(PropManipula, 0) == Yo) Sala.SetCustomProperties(new Hashtable { { PropManipula, 0 } });
    }

    private void Terminar()
    {
        Accion hecha = accion;
        if (hecha == Accion.Plantando)
        {
            float yaw = local.transform.eulerAngles.y + 180f; // con la pantallita hacia el que lo plantó
            Lugar(Pies() + Vector3.up * 0.3f, yaw, out Vector3 lugar, out Quaternion giro);
            Sala.SetCustomProperties(new Hashtable { { PropGiro, giro } });
            RondasTacticas.Plantar(lugar, Yo);
        }
        else RondasTacticas.Desactivar(Yo);
        CortarAccion();
    }

    // Si se está moviendo (o salta): plantando o desactivando, eso lo corta.
    private static bool SeMueve() =>
        KeyBindings.Held(GameAction.Adelante) || KeyBindings.Held(GameAction.Atras) || KeyBindings.Held(GameAction.Izquierda) ||
        KeyBindings.Held(GameAction.Derecha) || KeyBindings.Down(GameAction.Saltar);

    private bool EnElSuelo() => cuerpo == null || cuerpo.isGrounded;

    private bool CercaDelPlantado(Vector3 pies)
    {
        Vector3 hasta = rondas.LugarDelDispositivo - pies;
        float vertical = Mathf.Abs(hasta.y);
        hasta.y = 0f;
        return hasta.magnitude <= AlcanceDesactivar && vertical <= AlcanceDesactivar;
    }

    private void MostrarAccion(string texto, float? progreso)
    {
        if (texto == accionTexto && System.Nullable.Equals(progreso, accionProgreso)) return;
        accionTexto = texto;
        accionProgreso = progreso;
        MatchHud.SetAction(texto, progreso);
    }

    // CA3 y CA4: al morir cae a los pies; si no, sale hacia adelante (antes de atravesar una pared).
    private void Soltar(bool alMorir)
    {
        Vector3 pies = Pies();
        Vector3 frente = camara != null ? camara.forward : local.transform.forward;
        frente.y = 0f;
        frente = frente.sqrMagnitude > 0.001f ? frente.normalized : local.transform.forward;
        Vector3 pecho = pies + Vector3.up * 1f;
        Vector3 desde = pecho;
        if (!alMorir)
        {
            float distancia = TiroAdelante;
            foreach (RaycastHit golpe in Physics.RaycastAll(pecho, frente, TiroAdelante + 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (!EsPersonaje(golpe.collider)) distancia = Mathf.Min(distancia, Mathf.Max(0f, golpe.distance - 0.3f));
            desde = pecho + frente * distancia;
        }
        Lugar(desde, Mathf.Atan2(frente.x, frente.z) * Mathf.Rad2Deg + 180f, out Vector3 punto, out Quaternion giro);
        Sala.SetCustomProperties(
            new Hashtable
            {
                { PropEstado, (int)Estado.EnElPiso }, { PropPortador, 0 }, { PropPiso, punto }, { PropGiro, giro },
                { PropVersion, Version + 1 }
            },
            new Hashtable { { PropEstado, (int)Estado.Portado }, { PropPortador, Yo } });
        pedidoHasta = Time.unscaledTime + 1f;
        sinLevantarHasta = Time.time + SinVolverALevantar;
    }

    private void AlMorir()
    {
        CortarAccion();
        if (Llevo) Soltar(true); // CA3
    }

    // CA5: si dos lo piden a la vez, Photon se lo da al primero (el segundo ya no encuentra la misma versión).
    private void Levantar()
    {
        Sala.SetCustomProperties(
            new Hashtable { { PropEstado, (int)Estado.Portado }, { PropPortador, Yo }, { PropVersion, Version + 1 } },
            new Hashtable { { PropEstado, (int)Estado.EnElPiso }, { PropVersion, Version } });
        pedidoHasta = Time.unscaledTime + 1f;
    }

    private Vector3 Pies()
    {
        if (cuerpo == null) return local.transform.position;
        Vector3 centro = cuerpo.transform.TransformPoint(cuerpo.center);
        return centro - Vector3.up * (cuerpo.height * 0.5f * Mathf.Abs(cuerpo.transform.lossyScale.y));
    }

    // El piso debajo de "desde" (sin contar a los personajes), con el dispositivo apoyado según la inclinación.
    private static void Lugar(Vector3 desde, float yaw, out Vector3 punto, out Quaternion giro)
    {
        punto = desde;
        Vector3 normal = Vector3.up;
        float mejor = float.MaxValue;
        foreach (RaycastHit golpe in Physics.RaycastAll(desde, Vector3.down, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (golpe.distance >= mejor || EsPersonaje(golpe.collider)) continue;
            mejor = golpe.distance;
            punto = golpe.point;
            normal = golpe.normal;
        }
        giro = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, yaw, 0f);
    }

    private static bool EsPersonaje(Collider c) =>
        c.GetComponentInParent<HealthSystem>() != null || c.GetComponentInParent<CharacterController>() != null;

    // El dispositivo en primera persona: hijo de la cámara, en la capa de las armas (lo dibuja la cámara de armas).
    private void CrearEnMano()
    {
        if (config == null || config.modeloDispositivo == null || camara == null) return;
        enMano = Instantiate(config.modeloDispositivo, camara, false);
        enMano.name = "Dispositivo (en la mano)";
        enManoLugar = new Vector3(0.16f, -0.24f, 0.42f);
        enMano.transform.localPosition = enManoLugar;
        enMano.transform.localRotation = Quaternion.Euler(32f, 166f, -4f); // con la pantallita hacia la cámara
        int capa = LayerMask.NameToLayer("ArmaEnMano");
        foreach (Transform parte in enMano.GetComponentsInChildren<Transform>(true))
            if (capa >= 0) parte.gameObject.layer = capa;
        foreach (Renderer r in enMano.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        TMP_Text pantalla = Buscar<TMP_Text>(enMano.transform, "Pantalla");
        if (pantalla != null) pantalla.text = "- - : - -";
        enMano.SetActive(false);
        if (armas != null) armas.dispositivoObj = enMano;
    }

    // =====================================================================
    // Lo que se ve y se escucha (todas las computadoras)
    // =====================================================================

    private void Ver()
    {
        RondasTacticas.Fase fase = rondas.FaseActual;
        if (fase == RondasTacticas.Fase.Compra) exploto = false;

        // En el piso (CA3, CA4 y CA6). Cae un poco al aparecer; al levantarlo suena y se va.
        bool piso = EnElPiso;
        if (piso && (enPiso == null || versionEnPiso != Version))
        {
            Quitar(ref enPiso);
            enPiso = Crear("Dispositivo en el piso", Piso, Giro);
            versionEnPiso = Version;
            enPisoHasta = Piso;
            enPisoDesde = Piso + Vector3.up * 0.5f;
            enPisoCae = 0f;
            if (enPiso != null) enPiso.go.transform.position = enPisoDesde;
        }
        else if (!piso && enPiso != null)
        {
            if (EstadoActual == Estado.Portado) Sonar(config != null ? config.sonidoLevantarArma : null, enPiso.go.transform.position, 2f, 25f);
            Quitar(ref enPiso);
        }
        if (enPiso != null && enPisoCae >= 0f)
        {
            enPisoCae += Time.deltaTime / 0.25f;
            float k = Mathf.Clamp01(enPisoCae);
            enPiso.go.transform.position = Vector3.LerpUnclamped(enPisoDesde, enPisoHasta, k * k);
            if (k >= 1f)
            {
                enPisoCae = -1f;
                Sonar(config != null ? config.sonidoSoltarArma : null, enPisoHasta, 2f, 25f);
            }
        }

        // Plantado (US 131): queda hasta que explota o empieza otra ronda.
        bool hayPlantado = rondas.HayDispositivo && !exploto && fase != RondasTacticas.Fase.Compra && fase != RondasTacticas.Fase.Seleccion;
        if (hayPlantado && plantado == null)
        {
            plantado = Crear("Dispositivo plantado", rondas.LugarDelDispositivo, Giro);
            EmpezarCuenta();
        }
        else if (!hayPlantado && plantado != null)
        {
            Quitar(ref plantado);
            PararCuenta();
        }
        if (plantado != null) Plantado(fase);

        // US 132, CA3: mientras alguien lo desactiva suena distinto; plantando, las teclas.
        int manipula = Leer(PropManipula, 0);
        if (manipula != manipulaVisto)
        {
            manipulaVisto = manipula;
            if (manipulacion != null) manipulacion.Stop();
            if (manipula != 0 && EnJuego)
            {
                bool desactivando = rondas.HayDispositivo;
                JugadorEnRed quien = partida.Buscar(manipula);
                Vector3 donde = desactivando ? rondas.LugarDelDispositivo : quien != null ? quien.transform.position : Piso;
                AudioClip clip = config == null ? null : desactivando ? config.sonidoDesactivando : config.sonidoPlantando;
                if (clip != null)
                {
                    if (manipulacion == null) manipulacion = Fuente("Plantando o desactivando", 1f, 3f, 30f);
                    manipulacion.transform.position = donde;
                    manipulacion.clip = clip;
                    manipulacion.loop = desactivando;
                    manipulacion.Play();
                }
            }
        }
    }

    // Cuenta del plantado: la pantallita, la luz y el pitido, cada vez más seguido.
    private void Plantado(RondasTacticas.Fase fase)
    {
        bool desactivado = rondas.EstaDesactivado;
        float restante = rondas.Restante;
        if (plantado.pantalla != null)
        {
            if (desactivado) plantado.pantalla.text = "OFF";
            else if (fase == RondasTacticas.Fase.Combate)
            {
                int s = Mathf.CeilToInt(restante);
                plantado.pantalla.text = $"{s / 60}:{s % 60:00}";
            }
            else plantado.pantalla.text = "0:00";
        }

        if (desactivado || fase != RondasTacticas.Fase.Combate)
        {
            Luz(plantado, desactivado ? ShopUIKit.Ok : Apagada(plantado.luzColor));
            return;
        }
        if (Time.time >= proximoBip)
        {
            float cada = restante > 20f ? 1f : restante > 10f ? 0.7f : restante > 5f ? 0.45f : 0.25f;
            proximoBip = Time.time + cada;
            luzHasta = Time.time + Mathf.Min(0.12f, cada * 0.5f);
            Sonar(config != null ? config.sonidoBipDispositivo : null, plantado.go.transform.position, 3f, 40f);
        }
        Luz(plantado, Time.time < luzHasta ? plantado.luzColor : Apagada(plantado.luzColor));
    }

    private static Color Apagada(Color color) => new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.25f, color.a);

    private void AlPlantar(Vector3 lugar)
    {
        // CA4: todos escuchan el aviso.
        Sonar2D(config != null ? config.sonidoPlantado : null, 0.9f);
        ZonaDePlantado zona = ZonaDePlantado.En(lugar);
        MatchHud.Warn(zona != null ? $"Dispositivo plantado en {zona.nombre}" : "Dispositivo plantado", 3f);
    }

    private void AlDesactivar()
    {
        PararCuenta();
        if (plantado != null) Sonar(config != null ? config.sonidoDesactivado : null, plantado.go.transform.position, 4f, 60f);
        if (manipulacion != null) manipulacion.Stop();
    }

    // US 131, CA6: la explosión (el daño lo hace RondasTacticas).
    private void AlExplotar(Vector3 lugar)
    {
        exploto = true;
        PararCuenta();
        if (manipulacion != null) manipulacion.Stop();
        Quitar(ref plantado);
        if (config == null) return;
        if (config.efectoExplosionDispositivo != null)
            Destroy(Instantiate(config.efectoExplosionDispositivo, lugar + Vector3.up * 0.3f, Quaternion.identity), 12f);
        Sonar(config.sonidoExplosionDispositivo, lugar, 25f, 250f, AudioRolloffMode.Logarithmic);
    }

    // La cuenta regresiva: arranca para terminar justo cuando explota (el sonido es más corto que los 45 s).
    private void EmpezarCuenta()
    {
        proximoBip = 0f;
        if (config == null || config.sonidoCuentaDispositivo == null || rondas.FaseActual != RondasTacticas.Fase.Combate ||
            rondas.EstaDesactivado) return;
        if (cuenta == null)
        {
            cuenta = gameObject.AddComponent<AudioSource>();
            cuenta.playOnAwake = false;
            cuenta.spatialBlend = 0f;
            cuenta.outputAudioMixerGroup = grupo;
        }
        cuenta.clip = config.sonidoCuentaDispositivo;
        cuenta.volume = config.volumenCuentaDispositivo;
        float espera = rondas.Restante - Mathf.Min(config.finCuentaDispositivo, cuenta.clip.length);
        if (espera > 0f) cuenta.PlayDelayed(espera);
        else
        {
            cuenta.time = Mathf.Clamp(-espera, 0f, cuenta.clip.length - 0.05f);
            cuenta.Play();
        }
    }

    private void PararCuenta()
    {
        if (cuenta != null) cuenta.Stop();
    }

    // ---------- Modelos ----------

    private Modelo Crear(string nombre, Vector3 lugar, Quaternion giro)
    {
        if (config == null || config.modeloDispositivo == null) return null;
        var modelo = new Modelo { go = Instantiate(config.modeloDispositivo, lugar, giro) };
        modelo.go.name = nombre;
        foreach (Collider c in modelo.go.GetComponentsInChildren<Collider>(true)) Destroy(c);
        modelo.pantalla = Buscar<TMP_Text>(modelo.go.transform, "Pantalla");
        if (modelo.pantalla != null) modelo.pantalla.text = "- - : - -";
        modelo.luz = Buscar<Renderer>(modelo.go.transform, "Luz");
        if (modelo.luz != null)
        {
            Material m = modelo.luz.material; // una copia, para que titile solo esta
            modelo.luzColor = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
        }
        return modelo;
    }

    private static void Luz(Modelo modelo, Color color)
    {
        if (modelo == null || modelo.luz == null) return;
        Material m = modelo.luz.material;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color); else m.color = color;
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color * 2f);
    }

    private static void Quitar(ref Modelo modelo)
    {
        if (modelo != null && modelo.go != null) Destroy(modelo.go);
        modelo = null;
    }

    private static T Buscar<T>(Transform raiz, string nombre) where T : Component
    {
        foreach (T c in raiz.GetComponentsInChildren<T>(true))
            if (c.name == nombre) return c;
        return null;
    }

    // ---------- Sonidos ----------

    private AudioSource Fuente(string nombre, float mezcla, float minimo, float maximo,
        AudioRolloffMode caida = AudioRolloffMode.Linear)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        AudioSource fuente = go.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.outputAudioMixerGroup = grupo;
        fuente.spatialBlend = mezcla;
        fuente.rolloffMode = caida;
        fuente.minDistance = minimo;
        fuente.maxDistance = maximo;
        fuente.dopplerLevel = 0f;
        return fuente;
    }

    // Un sonido 3D en un punto; el objeto se borra cuando termina.
    private void Sonar(AudioClip clip, Vector3 en, float minimo, float maximo, AudioRolloffMode caida = AudioRolloffMode.Linear)
    {
        if (clip == null) return;
        AudioSource fuente = Fuente("Sonido " + clip.name, 1f, minimo, maximo, caida);
        fuente.transform.SetParent(null, true);
        fuente.transform.position = en;
        fuente.clip = clip;
        fuente.Play();
        Destroy(fuente.gameObject, clip.length + 0.1f);
    }

    private void Sonar2D(AudioClip clip, float volumen)
    {
        if (clip == null) return;
        AudioSource fuente = Fuente("Sonido " + clip.name, 0f, 1f, 500f);
        fuente.volume = volumen;
        fuente.clip = clip;
        fuente.Play();
        Destroy(fuente.gameObject, clip.length + 0.1f);
    }

    // =====================================================================
    // Marcas en pantalla (CA2 y CA6)
    // =====================================================================

    private void Marcas()
    {
        RectTransform raiz = MatchHud.Instance != null ? MatchHud.Instance.Root : null;
        Camera ojo = Camera.main;
        if (raiz == null || ojo == null) return;

        // CA2: el portador, solo para sus compañeros (no para él mismo ni para los defensores).
        int portador = Portador;
        JugadorEnRed conElDispositivo = portador != 0 && portador != Yo && EquiposTacticos.SonAliados(Yo, portador)
            ? partida.Buscar(portador) : null;
        bool verPortador = conElDispositivo != null && conElDispositivo.Vivo;
        Poner(ref marcaPortador, raiz, ojo, verPortador, verPortador ? conElDispositivo.transform.position + Vector3.up * AlturaMarcaPortador : Vector3.zero,
            ShopUIKit.Accent, false);

        // CA6: en el piso, todos.
        bool verPiso = enPiso != null;
        Poner(ref marcaPiso, raiz, ojo, verPiso, verPiso ? enPiso.go.transform.position + Vector3.up * 0.35f : Vector3.zero, ShopUIKit.Accent, true);

        // Plantado: todos ven dónde está, en rojo, con la luz que titila con el pitido.
        bool verPlantado = plantado != null;
        Poner(ref marcaPlantado, raiz, ojo, verPlantado, verPlantado ? plantado.go.transform.position + Vector3.up * 0.35f : Vector3.zero,
            MatchHud.RivalColor, true);
        if (verPlantado && marcaPlantado != null)
            marcaPlantado.luz.color = rondas.EstaDesactivado ? ShopUIKit.Ok : Time.time < luzHasta ? Color.white : MatchHud.RivalColor;
    }

    private void Poner(ref Marca marca, RectTransform raiz, Camera ojo, bool ver, Vector3 donde, Color color, bool distancia)
    {
        if (!ver)
        {
            if (marca != null) marca.rect.gameObject.SetActive(false);
            return;
        }
        if (marca == null) marca = NuevaMarca(raiz, color, distancia);
        Vector3 pantalla = ojo.WorldToScreenPoint(donde);
        bool adelante = pantalla.z > 0f;
        marca.rect.gameObject.SetActive(adelante);
        if (!adelante) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(raiz, pantalla, null, out Vector2 lugar);
        marca.rect.anchoredPosition = lugar;
        if (marca.texto != null) marca.texto.text = $"{Mathf.RoundToInt(Vector3.Distance(ojo.transform.position, donde))} m";
    }

    private static Marca NuevaMarca(RectTransform raiz, Color color, bool distancia)
    {
        var marca = new Marca { rect = ShopUIKit.Node("Marca del dispositivo", raiz) };
        marca.rect.anchorMin = marca.rect.anchorMax = marca.rect.pivot = new Vector2(0.5f, 0.5f);
        marca.rect.sizeDelta = new Vector2(28f, 28f);
        marca.rect.SetAsFirstSibling(); // debajo del resto del HUD
        Sprite redondeado = MatchHud.Instance != null ? MatchHud.Instance.Rounded : null;
        RectTransform icono = MatchHud.DeviceGlyph(marca.rect, color, redondeado, out marca.luz);
        ShopUIKit.Place(icono, 0f, 0f, 28f, 28f);
        if (distancia)
        {
            RectTransform texto = ShopUIKit.Node("Distancia", marca.rect);
            texto.anchorMin = texto.anchorMax = new Vector2(0.5f, 0f);
            texto.pivot = new Vector2(0.5f, 1f);
            texto.anchoredPosition = new Vector2(0f, -2f);
            texto.sizeDelta = new Vector2(80f, 18f);
            TMP_FontAsset fuente = MatchHud.Instance != null ? MatchHud.Instance.LabelFont : null;
            marca.texto = ShopUIKit.Text(texto, fuente, 15f, ShopUIKit.Ink, TextAlignmentOptions.Center);
        }
        return marca;
    }
}
