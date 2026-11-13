using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// F31 (Invasión cooperativa), US 357: caer y revivir, al estilo Fortnite. La agrega PartidaDeInvasion en cooperativo.
// - CA1: al quedarse sin vida el jugador no muere: queda caído (HealthSystem.AntesDeMorir), con la cámara casi en el
//   piso, y se arrastra despacio (PlayerMovement.Caido). No puede disparar, usar el cuchillo ni tirar granadas, y no
//   recibe más daño. Sí elige cuando se abre el puesto (US 356).
// - CA2: tiene 30 s para que lo revivan, con la cuenta en su pantalla (y arriba de él para los demás,
//   CaidoEnTerceraPersona). Si se terminan, muere: mira a sus compañeros (Espectador) y vuelve al empezar la oleada
//   siguiente, junto a un compañero vivo, con la vida llena (US 289, CA6).
// - CA3: un compañero vivo se acerca (2,2 m), mantiene la tecla de usar (E) y lo revive en 5 s, con la barra. Mientras,
//   el que revive queda agachado sobre él. Si suelta la tecla o se aleja, se corta (que lo golpeen no lo corta).
// - CA4: el revivido se levanta con un 30 % de vida, con sus armas y su munición.
// - CA5: los invasores no lo atacan (con 0 de vida no cuenta como objetivo: van al vivo más cercano).
// - CA6: si la oleada termina con él caído, se levanta solo con un 30 % de vida.
// - CA7: si todos están caídos o muertos a la vez, la partida termina (el resultado, en todas las computadoras).
// - CA8: los demás lo ven caer, arrastrarse y levantarse (CaidoEnTerceraPersona).
// El estado de cada jugador (vivo, caído o muerto, y desde cuándo está caído) viaja como propiedad del jugador en la
// sala; el revivir, con eventos de Photon.
public class CaidaEnInvasion : MonoBehaviourPunCallbacks, IOnEventCallback
{
    // Retirado: eligió "Retirarse" después de un Jefe (US 289, CA9): ya terminó su partida (no vuelve ni elige).
    public enum Estado : byte { Vivo = 0, Caido = 1, Muerto = 2, Retirado = 3 }

    public const float Desangra = 30f, TardaEnRevivir = 5f, Alcance = 2.2f, VidaAlLevantarse = 0.3f;
    private const float AlcanceParaCortar = 3f;
    private const string PropEstado = "inv.est", PropDesde = "inv.desde";
    private const byte EvReviviendo = 60, EvRevivido = 61; // (los de InvasoresEnRed van del 40 al 56)

    public static CaidaEnInvasion Actual { get; private set; }

    /// <summary>El jugador de esta computadora está caído.</summary>
    public static bool YoCaido => Actual != null && Actual.estado == Estado.Caido;
    /// <summary>US 356, CA2: muerto (espera la oleada siguiente) o retirado: esa vez no elige en el puesto.</summary>
    public static bool YoAfuera => Actual != null && (Actual.estado == Estado.Muerto || Actual.estado == Estado.Retirado);

    private Estado estado = Estado.Vivo;
    private double caidoDesde;
    private HealthSystem vida;
    private PlayerMovement movimiento;
    private JugadorEnRed local;
    private Espectador espectador;
    private readonly List<Behaviour> bloqueados = new List<Behaviour>();
    private Camera camaraArmas;
    private bool termino;

    // CA3: el que revive (en su computadora).
    private JugadorEnRed reviviendoA;
    private float reviviendoDesde;
    // CA3: quién me está reviviendo (en la mía) y a quién están reviviendo (en todas, para el cartel).
    private static readonly Dictionary<int, (int quien, double desde)> reviviendo = new Dictionary<int, (int, double)>();

    public override void OnEnable()
    {
        base.OnEnable();
        Actual = this;
        reviviendo.Clear();
        OleadasDeInvasion.Supero += AlSuperar;
        OleadasDeInvasion.Empezo += AlEmpezar;
    }

    public override void OnDisable()
    {
        base.OnDisable();
        if (Actual == this) Actual = null;
        OleadasDeInvasion.Supero -= AlSuperar;
        OleadasDeInvasion.Empezo -= AlEmpezar;
        if (vida != null) vida.AntesDeMorir = null;
        if (movimiento != null) { movimiento.Caido = false; movimiento.ForzarAgachado = false; }
    }

    // =====================================================================
    // El estado de cada uno (propiedades del jugador en la sala)
    // =====================================================================

    /// <summary>Si ese jugador está vivo, caído o muerto (esperando la oleada siguiente).</summary>
    public static Estado EstadoDe(Player p)
    {
        if (p == null) return Estado.Vivo;
        return p.CustomProperties.TryGetValue(PropEstado, out object v) && v is byte b ? (Estado)b : Estado.Vivo;
    }

    /// <summary>Si ese jugador (por su número en la sala) está caído.</summary>
    public static bool EstaCaido(int actor) => EstadoDe(PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.GetPlayer(actor) : null) == Estado.Caido;

    private static double DesdeDe(Player p) =>
        p != null && p.CustomProperties.TryGetValue(PropDesde, out object v) && v is double d ? d : PhotonNetwork.Time;

    /// <summary>CA2: lo que se ve arriba de un compañero caído.</summary>
    public static string CartelDe(int actor)
    {
        Player p = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.GetPlayer(actor) : null;
        if (reviviendo.TryGetValue(actor, out var r))
            return "<color=#7DE05A>" + L10n.T("gameplay.down.being_revived") + "</color>";
        int queda = Mathf.Max(0, Mathf.CeilToInt(Desangra - (float)(PhotonNetwork.Time - DesdeDe(p))));
        return "<color=#FF5C5C>" + L10n.T("gameplay.down.overhead_fmt", queda) + "</color>";
    }

    private void Publicar()
    {
        if (!PhotonNetwork.InRoom) return;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { PropEstado, (byte)estado }, { PropDesde, caidoDesde } });
    }

    // =====================================================================
    // Cuadro a cuadro
    // =====================================================================

    private void Update()
    {
        if (!Preparar()) return;
        switch (estado)
        {
            case Estado.Caido: MientrasCaido(); break;
            case Estado.Vivo: Revivir(); break;
        }
        RevisarFin();
    }

    // Engancha al jugador de esta computadora en cuanto está.
    private bool Preparar()
    {
        if (vida != null && local != null) return true;
        PartidaEnRed partida = PartidaEnRed.Actual;
        local = partida != null ? partida.Local : null;
        if (local == null) return false;
        vida = local.GetComponent<HealthSystem>();
        movimiento = local.GetComponent<PlayerMovement>();
        if (vida == null) return false;
        vida.AntesDeMorir = AlQuedarseSinVida;
        // CA2: muerto, mira a sus compañeros (como en el Táctico, US 133).
        if (FindFirstObjectByType<Espectador>() == null)
        {
            espectador = gameObject.AddComponent<Espectador>();
            espectador.Iniciar(partida);
        }
        // US 358 y US 359, CA2: entró (o volvió) con una oleada en juego: mira a sus compañeros y aparece al empezar la
        // siguiente, junto a uno vivo (AlEmpezar). En el descanso aparece enseguida.
        if ((Multijugador.EntroEmpezada || Reconexion.Volvio) && InvasoresEnRed.EnOleada)
        {
            estado = Estado.Muerto;
            local.EsperarLaOleada();
        }
        Publicar();
        return true;
    }

    // ---------- CA1: caer ----------

    private bool AlQuedarseSinVida()
    {
        if (estado != Estado.Vivo || !CooperativoDeInvasion.Activo) return false;
        estado = Estado.Caido;
        caidoDesde = PhotonNetwork.Time;
        Publicar();
        CortarRevivir();
        if (movimiento != null) movimiento.Caido = true;
        ShopUI.Cerrar();
        Bloquear(true);
        MatchHud.Warn(L10n.T("gameplay.down.you_are_down"), 2f);
        return true;
    }

    private void MientrasCaido()
    {
        float queda = Desangra - (float)(PhotonNetwork.Time - caidoDesde);
        int yo = PhotonNetwork.LocalPlayer.ActorNumber;
        if (reviviendo.TryGetValue(yo, out var r))
        {
            Player quien = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.GetPlayer(r.quien) : null;
            float avance = Mathf.Clamp01((float)(PhotonNetwork.Time - r.desde) / TardaEnRevivir);
            MatchHud.SetAction(L10n.T("gameplay.down.reviving_you_fmt", quien != null ? quien.NickName : "?"), avance);
            caidoDesde += Time.deltaTime; // mientras lo reviven no se desangra (al cortarse se avisa la cuenta nueva)
            return;
        }
        MatchHud.SetAction(L10n.T("gameplay.down.bleeding_fmt", Mathf.Max(0, Mathf.CeilToInt(queda))), Mathf.Clamp01(queda / Desangra));
        if (queda <= 0f) Morir(); // CA2
    }

    // CA2: se terminó el tiempo: muere de verdad (los demás lo ven caer como siempre, JugadorEnRed).
    private void Morir()
    {
        estado = Estado.Muerto;
        Publicar();
        MatchHud.SetAction(null);
        Bloquear(false);
        if (movimiento != null) movimiento.Caido = false;
        if (vida != null) vida.Rematar();
    }

    // CA4 y CA6: se levanta con un 30 % de vida, con sus armas y su munición.
    private void Levantarse()
    {
        if (estado != Estado.Caido) return;
        estado = Estado.Vivo;
        Publicar();
        reviviendo.Remove(PhotonNetwork.LocalPlayer.ActorNumber);
        MatchHud.SetAction(null);
        if (movimiento != null) movimiento.Caido = false;
        Bloquear(false);
        if (vida != null) vida.SetState(Mathf.Max(1, Mathf.RoundToInt(vida.maxHealth * VidaAlLevantarse)), 0);
        MatchHud.Warn(L10n.T("gameplay.down.revived"), 1.5f);
    }

    // CA1: caído no dispara, ni usa el cuchillo, ni tira granadas, ni cambia de arma; el arma en la mano no se ve.
    private void Bloquear(bool bloquear)
    {
        if (!bloquear)
        {
            foreach (Behaviour b in bloqueados) if (b != null) b.enabled = true;
            bloqueados.Clear();
            if (camaraArmas != null) camaraArmas.enabled = true;
            camaraArmas = null;
            return;
        }
        bloqueados.Clear();
        if (local == null) return;
        foreach (Behaviour b in local.GetComponentsInChildren<Behaviour>(true))
            if ((b is Pistola || b is Mitre || b is ArmaDeFuego || b is MeleeAttack || b is WeaponSwitcher || b is PlayerAbility
                 || b is GrenadeThrower || b is SoltarArmas) && b.enabled)
            {
                b.enabled = false;
                bloqueados.Add(b);
            }
        int capa = LayerMask.NameToLayer("ArmaEnMano");
        if (capa >= 0)
            foreach (Camera c in local.GetComponentsInChildren<Camera>(true))
                if (c.enabled && c.cullingMask == 1 << capa) { c.enabled = false; camaraArmas = c; break; }
    }

    // ---------- CA3: revivir a un compañero ----------

    private void Revivir()
    {
        if (vida == null || vida.currentHealth <= 0 || EleccionDeInvasion.Abierta || PauseMenu.IsPaused || ShopUI.IsOpen)
        {
            CortarRevivir();
            return;
        }
        JugadorEnRed caido = reviviendoA != null ? reviviendoA : CaidoCerca();
        if (caido == null || !caido.EstaCaido || Distancia(caido) > (reviviendoA != null ? AlcanceParaCortar : Alcance))
        {
            CortarRevivir();
            if (caido == null || !caido.EstaCaido) return;
            caido = CaidoCerca();
            if (caido == null) return;
        }
        string nombre = caido.photonView != null && caido.photonView.Owner != null ? caido.photonView.Owner.NickName : "?";
        if (!KeyBindings.Held(GameAction.Plantar))
        {
            CortarRevivir();
            MatchHud.SetAction(L10n.T("gameplay.down.hold_to_revive_fmt", KeyBindings.Label(GameAction.Plantar), nombre));
            return;
        }
        if (reviviendoA == null)
        {
            reviviendoA = caido;
            reviviendoDesde = Time.time;
            if (movimiento != null) movimiento.ForzarAgachado = true; // agachado sobre el caído
            Mandar(EvReviviendo, new object[] { caido.Actor, true });
        }
        float avance = Mathf.Clamp01((Time.time - reviviendoDesde) / TardaEnRevivir);
        MatchHud.SetAction(L10n.T("gameplay.down.reviving_fmt", nombre), avance);
        if (avance < 1f) return;
        int actor = caido.Actor;
        PhotonNetwork.RaiseEvent(EvRevivido, actor, new RaiseEventOptions { TargetActors = new[] { actor } }, SendOptions.SendReliable);
        CortarRevivir();
    }

    private JugadorEnRed CaidoCerca()
    {
        JugadorEnRed mejor = null;
        float cerca = Alcance;
        foreach (JugadorEnRed j in FindObjectsByType<JugadorEnRed>(FindObjectsSortMode.None))
        {
            if (j == null || j.photonView == null || j.photonView.IsMine || !j.EstaCaido) continue;
            float d = Distancia(j);
            if (d <= cerca) { cerca = d; mejor = j; }
        }
        return mejor;
    }

    private float Distancia(JugadorEnRed j) => local != null && j != null ? Vector3.Distance(local.transform.position, j.transform.position) : float.MaxValue;

    private void CortarRevivir()
    {
        if (reviviendoA != null)
        {
            Mandar(EvReviviendo, new object[] { reviviendoA.Actor, false });
            reviviendoA = null;
            MatchHud.SetAction(null);
        }
        else if (estado == Estado.Vivo) MatchHud.SetAction(null);
        if (movimiento != null) movimiento.ForzarAgachado = false;
    }

    // ---------- CA6: fin de la oleada y la siguiente ----------

    private void AlSuperar(int oleada)
    {
        if (estado == Estado.Caido) Levantarse(); // CA6: se levanta solo
    }

    // US 289, CA6: el que murió vuelve al empezar la oleada siguiente, junto a un compañero vivo, con la vida llena.
    private void AlEmpezar(int oleada)
    {
        if (estado != Estado.Muerto || local == null) return;
        Pose punto = JuntoAUnCompanero();
        estado = Estado.Vivo;
        Publicar();
        local.EmpezarRonda(punto);
    }

    private Pose JuntoAUnCompanero()
    {
        foreach (JugadorEnRed j in FindObjectsByType<JugadorEnRed>(FindObjectsSortMode.None))
        {
            if (j == null || j.photonView == null || j.photonView.IsMine || !j.Vivo || j.EstaCaido) continue;
            Vector3 lugar = j.transform.position - j.transform.forward * 1.5f;
            if (UnityEngine.AI.NavMesh.SamplePosition(lugar, out UnityEngine.AI.NavMeshHit piso, 3f, UnityEngine.AI.NavMesh.AllAreas))
                lugar = piso.position + Vector3.up * 0.1f;
            else lugar = j.transform.position + Vector3.up * 0.1f;
            return new Pose(lugar, Quaternion.Euler(0f, j.transform.eulerAngles.y, 0f));
        }
        return new Pose(local.transform.position, local.transform.rotation);
    }

    // ---------- US 289, CA9: retirarse de a varios ----------

    /// <summary>US 289, CA9: se retiró después de un Jefe: terminó su partida (ve su resultado) y la de los demás
    /// sigue. Queda afuera, como muerto pero sin aviso de baja: los invasores no lo buscan, no elige en el puesto y no
    /// vuelve en la oleada siguiente.</summary>
    public static void Retirarse()
    {
        if (Actual == null || Actual.estado == Estado.Retirado) return;
        Actual.CortarRevivir();
        if (Actual.estado == Estado.Caido) { Actual.Bloquear(false); if (Actual.movimiento != null) Actual.movimiento.Caido = false; }
        Actual.estado = Estado.Retirado;
        Actual.termino = true; // (su resultado ya está en pantalla)
        Actual.Publicar();
        MatchHud.SetAction(null);
        if (Actual.local != null) Actual.local.QuedarFueraDeLaPartida();
    }

    // ---------- CA7: todos caídos o muertos ----------

    private void RevisarFin()
    {
        if (termino || !PhotonNetwork.InRoom || ResultadoDeInvasion.Actual != null) return;
        bool alguno = false;
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            if (p.IsInactive) continue;
            Estado e = p.IsLocal ? estado : EstadoDe(p);
            if (e == Estado.Vivo) return;
            alguno = true;
        }
        if (!alguno) return;
        termino = true;
        MatchHud.SetAction(null);
        Multijugador.TerminarSala(); // la sala se cierra: nadie entra a una partida que ya terminó
        ResultadoDeInvasion.Mostrar(true);
    }

    // =====================================================================
    // La red
    // =====================================================================

    private static void Mandar(byte codigo, object[] contenido)
    {
        if (!PhotonNetwork.InRoom) return;
        PhotonNetwork.RaiseEvent(codigo, contenido, new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable);
    }

    public void OnEvent(EventData ev)
    {
        if (ev.Code == EvReviviendo && ev.CustomData is object[] d)
        {
            int actor = (int)d[0];
            if ((bool)d[1]) reviviendo[actor] = (ev.Sender, PhotonNetwork.Time);
            else if (reviviendo.TryGetValue(actor, out var r) && r.quien == ev.Sender)
            {
                reviviendo.Remove(actor);
                if (PhotonNetwork.LocalPlayer != null && actor == PhotonNetwork.LocalPlayer.ActorNumber && estado == Estado.Caido) Publicar();
            }
        }
        else if (ev.Code == EvRevivido && ev.CustomData is int a && PhotonNetwork.LocalPlayer != null && a == PhotonNetwork.LocalPlayer.ActorNumber)
            Levantarse(); // CA4
    }

    public override void OnPlayerLeftRoom(Player otro)
    {
        // Si se fue el que me estaba reviviendo, se corta.
        var quitar = new List<int>();
        foreach (var par in reviviendo) if (par.Value.quien == otro.ActorNumber || par.Key == otro.ActorNumber) quitar.Add(par.Key);
        foreach (int k in quitar) reviviendo.Remove(k);
    }
}
