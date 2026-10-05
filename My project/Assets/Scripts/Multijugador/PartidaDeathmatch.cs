using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// La partida del Modo Deathmatch (F09): todos contra todos, hasta 8 jugadores.
// US 136 (iniciar):
// - CA2: al empezar, cada uno en su punto, con 100 de vida y sin escudo.
// - CA3: cuenta regresiva en el centro (10 s; la US decía 5, se alargó para dar tiempo a elegir armas); se puede mirar y elegir armas, pero no moverse ni disparar.
// - CA4: al llegar a 0, "¡A pelear!", se liberan todos a la vez y empieza el reloj de 8 minutos.
// - CA6: música y ambiente propios del modo (ConfigRed), por el canal Música del mezclador.
// US 140 (puntuación y victoria):
// - CA1: cada baja suma 1 al que la hizo y 1 muerte al eliminado.
// - CA2: morir por la propia granada o sin rival suma la muerte y ninguna baja.
// - CA3: el primero en llegar a 25 bajas gana y la partida termina en ese momento.
// - CA4: si el reloj llega a 0, gana el que más bajas tenga.
// - CA5: con empate en bajas, gana el de menos muertes; si siguen, el que llegó primero a esa cantidad.
// - CA6: aviso para todos cuando alguien llega a 20 bajas o cuando quedan 60 s.
// El estado va en las propiedades de la sala y lo maneja el anfitrión, con la hora del servidor de Photon: todos
// ven el mismo reloj y los mismos números, aunque cambie el anfitrión. El marcador es MarcadorDeathmatch (US 141).
// Lo agrega PartidaEnRed al empezar una partida del Modo Deathmatch.
public class PartidaDeathmatch : MonoBehaviour
{
    public enum Fase { Cuenta, Combate, Terminada }

    public const float DuracionCuenta = 10f, DuracionCombate = 8f * 60f;
    public const int BajasParaGanar = 25, BajasDeAviso = 20;
    public const float SegundosDeAviso = 60f;

    private const string PropFase = "dm.fase", PropFin = "dm.fin", PropGanador = "dm.gan";
    private const string PropBajas = "dm.k", PropMuertes = "dm.d", PropCabezas = "dm.c", PropCuando = "dm.t";

    public static PartidaDeathmatch Actual { get; private set; }

    /// <summary>Avisa cuando la partida termina (US 142: pantalla de resultado).</summary>
    public static event System.Action Terminada;

    private PartidaEnRed partida;
    private int faseVista = -1, segundoVisto = -1;
    private bool avisoBajas, avisoTiempo;
    private float proximoArranque;
    private readonly List<Behaviour> trabados = new List<Behaviour>();

    private static Room Sala => PhotonNetwork.CurrentRoom;
    private static int Ahora => PhotonNetwork.ServerTimestamp;
    private static int Ms(float segundos) => Mathf.RoundToInt(segundos * 1000f);

    private static int Leer(string clave, int porDefecto) =>
        Sala != null && Sala.CustomProperties.TryGetValue(clave, out object v) && v is int n ? n : porDefecto;

    // =====================================================================
    // Lo que leen el marcador y la pantalla de resultado
    // =====================================================================

    public bool Lista => Sala != null && Sala.CustomProperties.ContainsKey(PropFase);
    public Fase FaseActual => (Fase)Leer(PropFase, 0);
    public int Ganador => Leer(PropGanador, 0);
    public static bool YaTermino => Actual != null && Actual.Lista && Actual.FaseActual == Fase.Terminada;

    /// <summary>Segundos que le quedan a la fase actual (la cuenta regresiva o el reloj de 8 minutos).</summary>
    // US 138, CA5: segundos después de reaparecer (o de empezar el combate) en que todavía se puede cambiar de equipo.
    public const float GraciaParaElegir = 5f;

    /// <summary>
    /// US 138, CA5: si ahora se puede abrir la tienda. Se puede en la cuenta inicial, muerto y en los primeros
    /// segundos después de reaparecer si todavía no atacó. texto y restante son para mostrar en la tienda
    /// (restante menor que 0: sin reloj).
    /// </summary>
    public static bool PuedeElegir(out string texto, out float restante)
    {
        texto = "Elegí tu equipo";
        restante = -1f;
        PartidaDeathmatch p = Actual;
        if (p == null || !p.Lista) return true;
        Fase fase = p.FaseActual;
        if (fase == Fase.Terminada) return false;
        if (fase == Fase.Cuenta)
        {
            texto = "Empieza en";
            restante = p.Restante;
            return true;
        }
        JugadorEnRed yo = p.partida != null ? p.partida.Local : null;
        if (yo == null) return false;
        if (!yo.Vivo)
        {
            bool esperando = yo.EsperaRestante > 0f;
            texto = esperando ? "Reaparecés en" : "Cerrá para reaparecer";
            restante = esperando ? yo.EsperaRestante : yo.TopeRestante;
            return true;
        }
        if (yo.AtacoDesdeReaparicion) return false;
        texto = "Podés cambiar";
        restante = yo.ReaparecioEn + GraciaParaElegir - Time.time;
        return restante > 0f;
    }

    public float Restante => Lista ? Mathf.Max(0f, unchecked(Leer(PropFin, Ahora) - Ahora) / 1000f) : DuracionCuenta;

    public static int Bajas(int actor) => Leer(PropBajas + actor, 0);
    public static int Muertes(int actor) => Leer(PropMuertes + actor, 0);
    public static int Cabezas(int actor) => Leer(PropCabezas + actor, 0);

    /// <summary>Los jugadores por puesto: más bajas; con las mismas, menos muertes; después, el que llegó antes (CA5).</summary>
    public static List<Player> Puestos()
    {
        var lista = new List<Player>(PhotonNetwork.PlayerList);
        int ahora = Ahora;
        lista.Sort((a, b) =>
        {
            int c = Bajas(b.ActorNumber).CompareTo(Bajas(a.ActorNumber));
            if (c != 0) return c;
            c = Muertes(a.ActorNumber).CompareTo(Muertes(b.ActorNumber));
            if (c != 0) return c;
            // Hace cuánto llegó cada uno a sus bajas: el que lleva más tiempo llegó primero.
            int haceA = unchecked(ahora - Leer(PropCuando + a.ActorNumber, ahora));
            int haceB = unchecked(ahora - Leer(PropCuando + b.ActorNumber, ahora));
            c = haceB.CompareTo(haceA);
            return c != 0 ? c : a.ActorNumber.CompareTo(b.ActorNumber);
        });
        return lista;
    }

    // =====================================================================
    // Ciclo de vida
    // =====================================================================

    public void Iniciar(PartidaEnRed partida)
    {
        this.partida = partida;
        Actual = this;
        anotado.Clear();
        ArrancarMusica();
        if (PruebaSolo.Activa)
            Debug.Log("Prueba solo (Deathmatch): F9 suma una baja tuya · F8 te deja en 19 bajas · F11 deja el reloj en 1:05.");
    }

    private void OnDestroy()
    {
        if (Actual != this) return;
        Actual = null;
        Destrabar();
    }

    // US 136, CA6
    private void ArrancarMusica()
    {
        ConfigRed config = ConfigRed.Actual;
        if (config == null) return;
        Fuente(config.musicaDeathmatch, config.volumenMusica, config.grupoMusica);
        Fuente(config.ambienteDeathmatch, config.volumenAmbiente, config.grupoMusica);
    }

    private void Fuente(AudioClip clip, float volumen, UnityEngine.Audio.AudioMixerGroup grupo)
    {
        if (clip == null) return;
        AudioSource fuente = gameObject.AddComponent<AudioSource>();
        fuente.clip = clip;
        fuente.loop = true;
        fuente.playOnAwake = false;
        fuente.spatialBlend = 0f;
        fuente.volume = volumen;
        fuente.outputAudioMixerGroup = grupo;
        fuente.Play();
    }

    // =====================================================================
    // Cuadro a cuadro
    // =====================================================================

    private void Update()
    {
        if (PhotonNetwork.IsMasterClient) Anfitrion();
        if (!Lista) { Trabar(false); return; } // todavía no arrancó: nadie se mueve

        Fase fase = FaseActual;
        if ((int)fase != faseVista)
        {
            AlCambiar(fase);
            faseVista = (int)fase;
        }

        if (fase == Fase.Cuenta) Cuenta();
        else if (fase == Fase.Combate) Avisos();
        else Trabar(true); // terminada: nadie se mueve, mira ni dispara
    }

    // El anfitrión mueve la partida: arranca la cuenta, pasa al combate y la termina por tiempo.
    private void Anfitrion()
    {
        if (Sala == null) return;
        if (!Lista)
        {
            // Espera a que todos terminen de cargar el mapa (pantalla de carga, US 196).
            if (PantallaDeCarga.EsperandoJugadores || Time.unscaledTime < proximoArranque) return;
            proximoArranque = Time.unscaledTime + 2f; // la propiedad tarda un instante en volver del servidor
            Sala.SetCustomProperties(new Hashtable { { PropFase, (int)Fase.Cuenta }, { PropFin, Ahora + Ms(DuracionCuenta) }, { PropGanador, 0 } });
            return;
        }

        Atajos();
        Fase fase = FaseActual;
        if (fase == Fase.Cuenta && Restante <= 0f && Time.unscaledTime >= proximoArranque)
        {
            proximoArranque = Time.unscaledTime + 2f;
            Sala.SetCustomProperties(new Hashtable { { PropFase, (int)Fase.Combate }, { PropFin, Ahora + Ms(DuracionCombate) } });
        }
        else if (fase == Fase.Combate && Restante <= 0f && Time.unscaledTime >= proximoArranque)
        {
            // CA4 y CA5: se acabó el tiempo; gana el primero de la tabla.
            proximoArranque = Time.unscaledTime + 2f;
            List<Player> puestos = Puestos();
            Sala.SetCustomProperties(new Hashtable { { PropFase, (int)Fase.Terminada }, { PropGanador, puestos.Count > 0 ? puestos[0].ActorNumber : 0 } });
        }
    }

    private void AlCambiar(Fase fase)
    {
        JugadorEnRed local = partida != null ? partida.Local : null;
        switch (fase)
        {
            case Fase.Cuenta:
                segundoVisto = -1;
                break;
            case Fase.Combate:
                // US 136, CA2 y CA4: todos libres a la vez, con 100 de vida y sin escudo.
                Destrabar();
                ShopUI.Rebloquear(); // si la tienda sigue abierta, las armas siguen trabadas hasta cerrarla
                if (local != null) local.AbrirVentanaDeEquipo(); // US 138, CA5
                if (faseVista == (int)Fase.Cuenta || faseVista < 0)
                {
                    HealthSystem vida = local != null ? local.GetComponent<HealthSystem>() : null;
                    if (vida != null && local.Vivo) vida.SetState(vida.maxHealth, 0);
                    MatchHud.ShowBanner("Deathmatch", "¡A pelear!", ShopUIKit.Accent, $"Gana el primero en llegar a {BajasParaGanar} bajas", null, 1.6f);
                }
                break;
            case Fase.Terminada:
                ShopUI.Cerrar();
                MatchHud.HideBanner();
                MatchHud.SetHint(null);
                if (partida != null) partida.Aviso(null);
                Terminada?.Invoke();
                break;
        }
    }

    // US 136, CA3: la cuenta regresiva. Se puede mirar y cambiar de arma, pero no moverse ni disparar.
    private void Cuenta()
    {
        Trabar(false);
        int segundo = Mathf.CeilToInt(Restante);
        if (segundo == segundoVisto || segundo <= 0) return;
        segundoVisto = segundo;
        MatchHud.ShowBanner("Deathmatch · Todos contra todos", segundo.ToString(), ShopUIKit.Accent,
            $"{KeyBindings.Label(GameAction.Tienda)}: elegí tu equipo", null, 1.2f);
    }

    // US 140, CA6: alguien llegó a 20 bajas o quedan 60 s.
    private void Avisos()
    {
        if (!avisoTiempo && Restante <= SegundosDeAviso)
        {
            avisoTiempo = true;
            MatchHud.Warn("Queda 1 minuto", 3f);
        }
        if (avisoBajas) return;
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            if (Bajas(p.ActorNumber) < BajasDeAviso) continue;
            avisoBajas = true;
            MatchHud.Warn(p.IsLocal ? $"Llegaste a {BajasDeAviso} bajas: te faltan {BajasParaGanar - Bajas(p.ActorNumber)}"
                                    : $"{p.NickName} llegó a {BajasDeAviso} bajas", 3f);
            break;
        }
    }

    // =====================================================================
    // Bajas (US 140)
    // =====================================================================

    /// <summary>
    /// Lo llama JugadorEnRed en cada muerte, en todas las computadoras; solo el anfitrión anota, así hay una sola
    /// cuenta. atacante 0 (o el mismo que murió): nadie suma la baja (CA2).
    /// </summary>
    public static void ContarBaja(int atacante, int muerto, bool cabeza)
    {
        PartidaDeathmatch p = Actual;
        if (p == null || !PhotonNetwork.IsMasterClient || Sala == null || !p.Lista || p.FaseActual != Fase.Combate) return;

        var cambios = new Hashtable();
        if (muerto > 0) cambios[PropMuertes + muerto] = Sumar(PropMuertes + muerto);
        if (atacante > 0 && atacante != muerto)
        {
            int bajas = Sumar(PropBajas + atacante);
            cambios[PropBajas + atacante] = bajas;
            cambios[PropCuando + atacante] = Ahora;
            if (cabeza) cambios[PropCabezas + atacante] = Sumar(PropCabezas + atacante);
            // CA3: llegó a 25, gana y se termina en este momento.
            if (bajas >= BajasParaGanar)
            {
                cambios[PropFase] = (int)Fase.Terminada;
                cambios[PropGanador] = atacante;
            }
        }
        if (cambios.Count > 0) Sala.SetCustomProperties(cambios);
    }

    // Lo que el anfitrión ya anotó y todavía no volvió del servidor: dos bajas casi juntas no se pisan.
    private static readonly Dictionary<string, int> anotado = new Dictionary<string, int>();

    private static int Sumar(string clave)
    {
        int valor = Mathf.Max(Leer(clave, 0), anotado.TryGetValue(clave, out int n) ? n : 0) + 1;
        anotado[clave] = valor;
        return valor;
    }

    // =====================================================================
    // Controles
    // =====================================================================

    // todo = false: no se mueve ni dispara, pero mira y cambia de arma (cuenta regresiva). todo = true: tampoco mira.
    private void Trabar(bool todo)
    {
        JugadorEnRed local = partida != null ? partida.Local : null;
        if (local == null) return;
        foreach (Behaviour c in local.GetComponentsInChildren<Behaviour>(true))
        {
            bool frena = c is PlayerMovement || c is Pistola || c is Mitre || c is ArmaDeFuego || c is MeleeAttack ||
                         c is GrenadeThrower || c is PlayerAbility || (todo && (c is CameraLook || c is WeaponSwitcher));
            if (c.enabled && frena)
            {
                c.enabled = false;
                trabados.Add(c);
            }
        }
    }

    private void Destrabar()
    {
        foreach (Behaviour c in trabados) if (c != null) c.enabled = true;
        trabados.Clear();
    }

    // =====================================================================
    // Prueba solo (solo en el editor)
    // =====================================================================

    private void Atajos()
    {
        if (!PruebaSolo.Activa || FaseActual != Fase.Combate) return;
        int yo = PhotonNetwork.LocalPlayer.ActorNumber;
        if (Input.GetKeyDown(KeyCode.F9)) ContarBaja(yo, 0, false);
        else if (Input.GetKeyDown(KeyCode.F8))
            Sala.SetCustomProperties(new Hashtable { { PropBajas + yo, BajasDeAviso - 1 }, { PropCuando + yo, Ahora } });
        else if (Input.GetKeyDown(KeyCode.F11))
            Sala.SetCustomProperties(new Hashtable { { PropFin, Ahora + Ms(SegundosDeAviso + 5f) } });
    }
}
