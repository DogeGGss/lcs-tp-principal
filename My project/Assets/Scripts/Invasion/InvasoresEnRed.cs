using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// F31 (Invasión cooperativa), US 290: todos ven a los mismos invasores. La agrega PartidaDeInvasion en cooperativo.
// El anfitrión de Photon lleva las oleadas (OleadasDeInvasion) y a los invasores: los mueve, decide sus ataques y su
// vida. En las demás computadoras cada invasor es una copia (Npc.CrearCopia) que repite lo que le llega:
// - CA1: al aparecer cada uno se avisa (Aparece) y, 10 veces por segundo, van juntos dónde está cada uno, hacia dónde
//   mira, su altura (vuelo, embestida), su vida y su animación (Estados). La copia va interpolando entre lo que llega
//   (InvasorEnRed). Los portales se abren y se cierran en todas a la vez.
// - CA2: el daño a una copia se le manda al anfitrión con quién lo hizo (Danio); el anfitrión lo aplica y, si muere,
//   avisa la baja con el que lo mató (Muere): muere igual en todas (MuerteDeNpc, US 284) y los puntos son de ese
//   jugador (PuntosDeInvasion, Npc.LoMatoOtro). La flash y el empujón de Despeje también se le piden al anfitrión.
// - CA3: los invasores del anfitrión van hacia el jugador vivo más cercano, también hacia las copias de los compañeros
//   (PersecucionDeNpc.Jugadores). El golpe a la copia de un compañero le llega a su dueño (JugadorEnRed.RpcDanio, sin
//   atacante) y el empujón de la embestida también (Empujon).
// - CA4: lo que se ve de los ataques sale en todas: la línea de la embestida, la mira y el rayo del ojo, las bolas de
//   energía y las de veneno (que dejan su nube o su charco), y los sonidos de cada invasor. Lo que se dispara (el rayo,
//   las bolas, las nubes) le hace el daño, en cada computadora, al jugador de esa computadora. La barra del Jefe es la
//   misma para todos (su vida llega con los Estados).
// - CA5: si el anfitrión se va, Photon elige otro: en esa computadora las copias pasan a ser invasores de verdad
//   (Npc.Activar) y la oleada sigue desde donde estaba (OleadasDeInvasion.TomarElMando).
// Si una copia no se conoce (se perdió el aviso, o se entró tarde), se le pide al anfitrión (Pedir).
public class InvasoresEnRed : MonoBehaviourPunCallbacks, IOnEventCallback
{
    // Los eventos de Photon (el 1 es el de los jugadores, Multijugador.EventoJugador).
    private const byte EvAparece = 40, EvEstados = 41, EvMuere = 42, EvDanio = 43, EvPedir = 44, EvEfecto = 45,
        EvEmpujon = 46, EvPedidoNpc = 47, EvOleada = 48, EvEstadoOleada = 49, EvPortal = 50;

    // Lo que se ve de los ataques (EvEfecto).
    private const byte EfCargaRayo = 1, EfRayo = 2, EfBolaDeEnergia = 3, EfBolaDeVeneno = 4, EfEmbestida = 5,
        EfFinDeEmbestida = 6, EfSonido = 7;

    // Lo que una copia le pide al anfitrión (EvPedidoNpc).
    private const byte PedAturdir = 1, PedEmpujon = 2;

    // Los avisos de la oleada (EvOleada).
    public const byte OleadaEmpezo = 1, OleadaSupero = 2, OleadaLlegoElJefe = 3;

    private const float CadaEstado = 0.1f, CadaEstadoDeOleada = 1f, CadaPedido = 0.5f;
    private const int PorEstado = 14; // números por invasor en cada Estados
    private const int Quitar = -1;    // EvMuere sin baja: se borró sin morir (se perdió)

    /// <summary>La de esta partida (o null jugando solo).</summary>
    public static InvasoresEnRed Actual { get; private set; }

    /// <summary>Esta computadora lleva a los invasores en una partida cooperativa (el anfitrión).</summary>
    public static bool Anfitrion => CooperativoDeInvasion.Activo && PhotonNetwork.IsMasterClient;

    /// <summary>Los invasores los lleva otra computadora: acá son copias.</summary>
    public static bool EsCopia => CooperativoDeInvasion.Activo && !PhotonNetwork.IsMasterClient;

    private readonly Dictionary<int, Npc> invasores = new Dictionary<int, Npc>();
    private readonly Dictionary<int, PortalDeEnemigos> portales = new Dictionary<int, PortalDeEnemigos>();
    private readonly HashSet<int> pedidos = new HashSet<int>();
    private readonly List<int> paraQuitar = new List<int>();
    private readonly List<float> datos = new List<float>();
    private int siguiente;
    private float proximoEstado, proximoEstadoDeOleada, proximoPedido;
    private OleadasDeInvasion oleadas;

    private static readonly Dictionary<string, TipoDeNpc> tipos = new Dictionary<string, TipoDeNpc>();
    private static readonly Dictionary<TipoDeNpc, List<AudioClip>> sonidos = new Dictionary<TipoDeNpc, List<AudioClip>>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Limpiar()
    {
        Actual = null;
        tipos.Clear();
    }

    public override void OnEnable()
    {
        base.OnEnable(); // (se anota para los eventos de Photon)
        Actual = this;
        Npc.Murio += AlMorir;
    }

    public override void OnDisable()
    {
        base.OnDisable();
        if (Actual == this) Actual = null;
        Npc.Murio -= AlMorir;
    }

    private OleadasDeInvasion Oleadas => oleadas != null ? oleadas : (oleadas = FindFirstObjectByType<OleadasDeInvasion>());

    private static int Yo => PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0;

    // =====================================================================
    // Anfitrión
    // =====================================================================

    private void LateUpdate()
    {
        if (!Anfitrion) return;

        // CA1: los nuevos se avisan (el cuadro siguiente al aparecer: ya tienen la vida de su vuelta).
        foreach (Npc npc in Npc.Todos)
        {
            if (npc == null || npc.IdRed != 0 || npc.EsEspejo || !npc.Vivo) continue;
            npc.IdRed = NuevoId();
            invasores[npc.IdRed] = npc;
            Mandar(EvAparece, Aparicion(npc), ReceiverGroup.Others, true);
        }

        // Los que se borraron (la desintegración terminó, o se perdieron sin morir).
        paraQuitar.Clear();
        foreach (KeyValuePair<int, Npc> par in invasores) if (par.Value == null) paraQuitar.Add(par.Key);
        foreach (int id in paraQuitar)
        {
            invasores.Remove(id);
            Mandar(EvMuere, new object[] { id, Quitar, false }, ReceiverGroup.Others, true);
        }

        if (Time.unscaledTime >= proximoEstado)
        {
            proximoEstado = Time.unscaledTime + CadaEstado;
            MandarEstados();
        }
        if (Time.unscaledTime >= proximoEstadoDeOleada && Oleadas != null)
        {
            proximoEstadoDeOleada = Time.unscaledTime + CadaEstadoDeOleada;
            Mandar(EvEstadoOleada, Oleadas.EstadoParaLaRed(), ReceiverGroup.Others, true);
        }
    }

    // Un número que no se repite aunque cambie el anfitrión (y que entra entero en un float: hasta 16 millones).
    private int NuevoId()
    {
        siguiente = siguiente % 99999 + 1;
        return Mathf.Clamp(Yo, 1, 160) * 100000 + siguiente;
    }

    private static object[] Aparicion(Npc npc)
    {
        FuriaDeNpc furia = npc.GetComponent<FuriaDeNpc>();
        return new object[]
        {
            npc.IdRed, npc.Tipo.name, npc.transform.position, npc.transform.eulerAngles.y, npc.Alfa != null,
            npc.Vida != null ? npc.Vida.maxHealth : 0, npc.Vida != null ? npc.Vida.currentHealth : 0,
            npc.MasDanioPorJefes, furia != null ? furia.Umbral : -1f
        };
    }

    // CA1: todos juntos, pocas veces por segundo (con hasta 30 invasores a la vez).
    private void MandarEstados()
    {
        datos.Clear();
        foreach (KeyValuePair<int, Npc> par in invasores)
        {
            Npc npc = par.Value;
            if (npc == null || !npc.Vivo) continue;
            AnimadorDeNpc a = npc.Animaciones;
            List<AnimationClip> clips = AnimadorDeNpc.ClipsDe(npc.Tipo);
            Vector3 p = npc.transform.position;
            datos.Add(par.Key);
            datos.Add(p.x); datos.Add(p.y); datos.Add(p.z);
            datos.Add(npc.transform.eulerAngles.y);
            datos.Add(npc.Modelo != null ? npc.Modelo.localPosition.y : 0f);
            datos.Add(npc.Vida != null ? npc.Vida.currentHealth : 0);
            datos.Add(a != null && a.Clip != null ? clips.IndexOf(a.Clip) : -1);
            datos.Add(a != null ? a.Puestas % 1000000 : 0);
            datos.Add(a != null && a.EnLoop ? 1f : 0f);
            datos.Add(a != null ? a.VelocidadActual : 1f);
            datos.Add(a != null && a.UltimoGolpe != null ? clips.IndexOf(a.UltimoGolpe) : -1);
            datos.Add(a != null ? a.Golpes % 1000000 : 0);
            datos.Add(a != null ? a.PesoDelGolpe : 0f);
        }
        if (datos.Count == 0) return;
        Mandar(EvEstados, new object[] { PhotonNetwork.Time, datos.ToArray() }, ReceiverGroup.Others, false);
    }

    // CA2: la baja, con quién lo mató.
    private void AlMorir(Npc npc, int puntos, bool cabeza)
    {
        if (!Anfitrion || npc == null || npc.IdRed == 0 || npc.EsEspejo) return;
        int matador = npc.Matador != 0 ? npc.Matador : Yo;
        Mandar(EvMuere, new object[] { npc.IdRed, matador, cabeza }, ReceiverGroup.Others, true);
    }

    // CA2: el daño que le hizo otro jugador a su copia.
    private void AplicarDanio(int id, int danio, bool cabeza, Vector3 desde, int actor)
    {
        if (!invasores.TryGetValue(id, out Npc npc) || npc == null || !npc.Vivo || npc.Vida == null) return;
        npc.Matador = actor;
        HealthSystem.DamageOrigin = desde;
        npc.Vida.TakeDamage(danio, cabeza);
        if (npc.Vivo) npc.Matador = 0;
    }

    // =====================================================================
    // Lo que avisan los invasores (los llaman los del anfitrión)
    // =====================================================================

    private static bool Avisa(Npc npc) => npc != null && npc.IdRed != 0 && !npc.EsEspejo && Anfitrion && Actual != null;

    /// <summary>CA4: el rayo del ojo empezó a cargarse contra ese jugador.</summary>
    public static void AvisarCargaDelRayo(Npc npc, Transform jugador)
    {
        if (!Avisa(npc)) return;
        Actual.MandarEfecto(new object[] { EfCargaRayo, npc.IdRed, ActorDe(jugador) });
    }

    /// <summary>CA4: el rayo del ojo, de desde a fin.</summary>
    public static void AvisarRayo(Npc npc, Vector3 desde, Vector3 fin)
    {
        if (!Avisa(npc)) return;
        Actual.MandarEfecto(new object[] { EfRayo, npc.IdRed, desde, fin });
    }

    /// <summary>CA4: una bola de energía, de desde hacia ese punto.</summary>
    public static void AvisarBolaDeEnergia(Npc npc, Vector3 desde, Vector3 hacia)
    {
        if (!Avisa(npc)) return;
        Actual.MandarEfecto(new object[] { EfBolaDeEnergia, npc.IdRed, desde, hacia });
    }

    /// <summary>CA4: una bola de veneno del Jefe.</summary>
    public static void AvisarBolaDeVeneno(Npc npc, Vector3 desde, Vector3 hacia, float tamNube)
    {
        if (!Avisa(npc)) return;
        Actual.MandarEfecto(new object[] { EfBolaDeVeneno, npc.IdRed, desde, hacia, tamNube });
    }

    /// <summary>CA4: la línea de la embestida.</summary>
    public static void AvisarEmbestida(Npc npc, Vector3 origen, Vector3 direccion, float largo)
    {
        if (!Avisa(npc)) return;
        Actual.MandarEfecto(new object[] { EfEmbestida, npc.IdRed, origen, direccion, largo });
    }

    /// <summary>CA4: la línea de la embestida se va.</summary>
    public static void AvisarFinDeEmbestida(Npc npc)
    {
        if (!Avisa(npc)) return;
        Actual.MandarEfecto(new object[] { EfFinDeEmbestida, npc.IdRed });
    }

    /// <summary>CA4: un sonido del invasor (los gritos, los golpes, el rugido...). Las pisadas no: cada copia las hace
    /// al caminar.</summary>
    public static void AvisarSonido(AudioClip clip, Transform bicho, float hasta, float volumen)
    {
        if (clip == null || bicho == null || !Anfitrion || Actual == null) return;
        Npc npc = bicho.GetComponentInParent<Npc>();
        if (!Avisa(npc) || npc.Tipo == null) return;
        if (npc.Tipo.pasos != null && System.Array.IndexOf(npc.Tipo.pasos, clip) >= 0) return;
        int i = SonidosDe(npc.Tipo).IndexOf(clip);
        if (i < 0) return;
        Actual.MandarEfecto(new object[] { EfSonido, npc.IdRed, i, hasta, volumen });
    }

    private void MandarEfecto(object[] contenido) => Mandar(EvEfecto, contenido, ReceiverGroup.Others, true);

    /// <summary>CA3: el empujón de la embestida a la copia de un compañero: se le manda a su dueño. Devuelve si lo era.</summary>
    public static bool EmpujarCompanero(Transform jugador, Vector3 cuanto, float segundos)
    {
        if (jugador == null || !CooperativoDeInvasion.Activo) return false;
        JugadorEnRed j = jugador.GetComponent<JugadorEnRed>();
        if (j == null || j.photonView == null || j.photonView.IsMine) return false;
        PhotonNetwork.RaiseEvent(EvEmpujon, new object[] { cuanto, segundos },
            new RaiseEventOptions { TargetActors = new[] { j.Actor } }, SendOptions.SendReliable);
        return true;
    }

    /// <summary>CA1: se abrió un portal (en el anfitrión, se avisa).</summary>
    public static void AvisarPortal(PortalDeEnemigos portal, Vector3 piso, Vector3 mira, float escala)
    {
        if (portal == null || !Anfitrion || Actual == null) return;
        portal.IdRed = Actual.NuevoId();
        Actual.portales[portal.IdRed] = portal;
        Actual.Mandar(EvPortal, new object[] { (byte)1, portal.IdRed, piso, mira, escala }, ReceiverGroup.Others, true);
    }

    /// <summary>CA1: se cierra un portal.</summary>
    public static void AvisarCierre(PortalDeEnemigos portal)
    {
        if (portal == null || portal.IdRed == 0 || !Anfitrion || Actual == null) return;
        Actual.portales.Remove(portal.IdRed);
        Actual.Mandar(EvPortal, new object[] { (byte)2, portal.IdRed }, ReceiverGroup.Others, true);
    }

    /// <summary>Cómo va la oleada (empezó, se superó, llegó el Jefe).</summary>
    public static void AvisarOleada(byte que, int oleada, int total, int jefesDerrotados)
    {
        if (!Anfitrion || Actual == null) return;
        Actual.Mandar(EvOleada, new object[] { que, oleada, total, jefesDerrotados }, ReceiverGroup.Others, true);
        Actual.proximoEstadoDeOleada = 0f; // y enseguida el estado completo
    }

    // =====================================================================
    // Lo que piden las copias (en las demás computadoras)
    // =====================================================================

    /// <summary>CA2: le pegaron a una copia: el daño se le manda al anfitrión, que decide su vida.</summary>
    public static void MandarDanio(Npc npc, int danio, bool cabeza, Vector3? desde)
    {
        if (npc == null || npc.IdRed == 0 || danio <= 0 || !PhotonNetwork.InRoom) return;
        Vector3 origen = desde ?? (Camera.main != null ? Camera.main.transform.position : npc.transform.position);
        PhotonNetwork.RaiseEvent(EvDanio, new object[] { npc.IdRed, danio, cabeza, origen },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }

    /// <summary>La flash aturdió a una copia: que lo aturda el anfitrión.</summary>
    public static void PedirAturdir(Npc npc, float segundos)
    {
        if (npc == null || npc.IdRed == 0 || !PhotonNetwork.InRoom) return;
        PhotonNetwork.RaiseEvent(EvPedidoNpc, new object[] { PedAturdir, npc.IdRed, segundos },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }

    /// <summary>Despeje empujó a una copia: que lo empuje el anfitrión.</summary>
    public static void PedirEmpujon(Npc npc, Vector3 direccion, float metros)
    {
        if (npc == null || npc.IdRed == 0 || !PhotonNetwork.InRoom) return;
        PhotonNetwork.RaiseEvent(EvPedidoNpc, new object[] { PedEmpujon, npc.IdRed, direccion, metros },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }

    // =====================================================================
    // Lo que llega
    // =====================================================================

    public void OnEvent(EventData ev)
    {
        if (ev.Code < EvAparece || ev.Code > EvPortal) return;
        object[] d = ev.CustomData as object[];
        switch (ev.Code)
        {
            case EvAparece: if (EsCopia && d != null) Aparecer(d); break;
            case EvEstados: if (EsCopia && d != null) Estados((double)d[0], (float[])d[1]); break;
            case EvMuere: if (EsCopia && d != null) Muere((int)d[0], (int)d[1], (bool)d[2]); break;
            case EvDanio: if (Anfitrion && d != null) AplicarDanio((int)d[0], (int)d[1], (bool)d[2], (Vector3)d[3], ev.Sender); break;
            case EvPedir: if (Anfitrion && ev.CustomData is int[] ids) Responder(ids, ev.Sender); break;
            case EvEfecto: if (EsCopia && d != null) Efecto(d); break;
            case EvEmpujon: if (d != null) EmpujarmeA((Vector3)d[0], (float)d[1]); break;
            case EvPedidoNpc: if (Anfitrion && d != null) Pedido(d); break;
            case EvOleada: if (EsCopia && d != null) Oleada(d); break;
            case EvEstadoOleada: if (EsCopia && d != null && Oleadas != null) Oleadas.EstadoDelAnfitrion(d); break;
            case EvPortal: if (EsCopia && d != null) Portal(d); break;
        }
    }

    private static TipoDeNpc Tipo(string nombre)
    {
        if (string.IsNullOrEmpty(nombre)) return null;
        if (tipos.TryGetValue(nombre, out TipoDeNpc t) && t != null) return t;
        t = TipoDeNpc.Buscar(nombre);
        tipos[nombre] = t;
        return t;
    }

    private void Aparecer(object[] d)
    {
        int id = (int)d[0];
        pedidos.Remove(id);
        if (invasores.TryGetValue(id, out Npc ya) && ya != null) return;
        TipoDeNpc tipo = Tipo((string)d[1]);
        if (tipo == null) { Debug.LogWarning("[Invasión en red] No está el tipo de invasor " + d[1] + "."); return; }
        VarianteAlfa alfa = (bool)d[4] && ConfigInvasion.Actual != null ? ConfigInvasion.Actual.alfa : null;
        Npc npc = Npc.CrearCopia(tipo, (Vector3)d[2], Quaternion.Euler(0f, (float)d[3], 0f), alfa);
        if (npc == null) return;
        npc.IdRed = id;
        npc.name += " (copia)";
        invasores[id] = npc;
        int maxima = (int)d[5], vida = (int)d[6];
        if (npc.Vida != null && maxima > 0)
        {
            npc.Vida.maxHealth = maxima;
            npc.Vida.SetState(Mathf.Max(1, vida), 0);
        }
        npc.MasDanioPorJefes = (float)d[7];
        float umbral = (float)d[8];
        FuriaDeNpc furia = npc.GetComponent<FuriaDeNpc>();
        if (furia != null && umbral >= 0f)
        {
            furia.Umbral = umbral;
            if (npc.Barra != null) npc.Barra.MarcaDeFuria(umbral);
        }
        npc.gameObject.AddComponent<InvasorEnRed>().Iniciar(npc);
    }

    private void Estados(double cuando, float[] d)
    {
        if (d == null) return;
        for (int i = 0; i + PorEstado <= d.Length; i += PorEstado)
        {
            int id = Mathf.RoundToInt(d[i]);
            if (invasores.TryGetValue(id, out Npc npc) && npc != null)
            {
                InvasorEnRed copia = npc.GetComponent<InvasorEnRed>();
                if (copia != null) copia.Recibir(cuando, d, i);
            }
            else if (!invasores.ContainsKey(id)) pedidos.Add(id); // no se conoce: se le pide al anfitrión
        }
        if (pedidos.Count > 0 && Time.unscaledTime >= proximoPedido)
        {
            proximoPedido = Time.unscaledTime + CadaPedido;
            var ids = new int[pedidos.Count];
            pedidos.CopyTo(ids);
            PhotonNetwork.RaiseEvent(EvPedir, ids, new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
        }
    }

    // El anfitrión le contesta a quien no conocía a esos invasores.
    private void Responder(int[] ids, int actor)
    {
        foreach (int id in ids)
        {
            if (!invasores.TryGetValue(id, out Npc npc) || npc == null || !npc.Vivo) continue;
            PhotonNetwork.RaiseEvent(EvAparece, Aparicion(npc), new RaiseEventOptions { TargetActors = new[] { actor } }, SendOptions.SendReliable);
        }
    }

    private void Muere(int id, int matador, bool cabeza)
    {
        if (!invasores.TryGetValue(id, out Npc npc)) return;
        if (matador == Quitar)
        {
            invasores.Remove(id);
            if (npc != null && npc.Vivo) Destroy(npc.gameObject); // se perdió sin morir (en el anfitrión ya no está)
            return;
        }
        if (npc == null || !npc.Vivo) return;
        bool esJefe = npc.Tipo != null && npc.Tipo.esJefe;
        npc.MorirPorRed(matador, cabeza);
        if (Oleadas != null) Oleadas.MurioEnElAnfitrion(esJefe);
    }

    private void Efecto(object[] d)
    {
        byte que = (byte)d[0];
        if (!invasores.TryGetValue((int)d[1], out Npc npc) || npc == null) return;
        switch (que)
        {
            case EfCargaRayo:
            {
                DistanciaDeNpc distancia = npc.GetComponent<DistanciaDeNpc>();
                if (distancia != null) distancia.CargaRemota(JugadorDe((int)d[2]));
                break;
            }
            case EfRayo:
            {
                DistanciaDeNpc distancia = npc.GetComponent<DistanciaDeNpc>();
                if (distancia != null) distancia.RayoRemoto((Vector3)d[2], (Vector3)d[3]);
                break;
            }
            case EfBolaDeEnergia:
                if (npc.Tipo != null) BolaDeEnergia.Lanzar((Vector3)d[2], (Vector3)d[3], npc.Tipo, npc.transform);
                break;
            case EfBolaDeVeneno:
                if (npc.Tipo != null)
                    BolaDeVeneno.Lanzar((Vector3)d[2], (Vector3)d[3], npc.Tipo.nombre, npc.transform, npc.Tipo.veneno,
                        npc.Tipo.materialDeNube, npc.Tipo.sonidoDeImpactoDeVeneno, (float)d[4]);
                break;
            case EfEmbestida:
            {
                EmbestidaDeNpc embestida = npc.GetComponent<EmbestidaDeNpc>();
                if (embestida != null) embestida.LineaRemota((Vector3)d[2], (Vector3)d[3], (float)d[4]);
                break;
            }
            case EfFinDeEmbestida:
            {
                EmbestidaDeNpc embestida = npc.GetComponent<EmbestidaDeNpc>();
                if (embestida != null) embestida.BorrarLineaRemota();
                break;
            }
            case EfSonido:
            {
                List<AudioClip> lista = SonidosDe(npc.Tipo);
                int i = (int)d[2];
                if (i >= 0 && i < lista.Count) SonidoDeNpc.Uno(lista[i], npc.transform, (float)d[3], (float)d[4]);
                break;
            }
        }
    }

    // CA3: la embestida del Jefe me empujó (en la computadora del anfitrión le pegó a mi copia).
    private static void EmpujarmeA(Vector3 cuanto, float segundos)
    {
        PlayerMovement yo = FindFirstObjectByType<PlayerMovement>();
        if (yo != null) EmpujonAlJugador.Empujar(yo.transform, cuanto, segundos);
    }

    private void Pedido(object[] d)
    {
        if (!invasores.TryGetValue((int)d[1], out Npc npc) || npc == null || !npc.Vivo) return;
        if ((byte)d[0] == PedAturdir) npc.Aturdir((float)d[2]);
        else if ((byte)d[0] == PedEmpujon) EmpujonDeNpc.Empujar(npc, (Vector3)d[2], (float)d[3]);
    }

    private void Oleada(object[] d)
    {
        if (Oleadas == null) return;
        byte que = (byte)d[0];
        int n = (int)d[1];
        if (que == OleadaEmpezo) Oleadas.EmpezoEnElAnfitrion(n, (int)d[2], (int)d[3]);
        else if (que == OleadaSupero) Oleadas.SuperoEnElAnfitrion(n, (int)d[3]);
        else if (que == OleadaLlegoElJefe) Oleadas.LlegoElJefeEnElAnfitrion();
    }

    private void Portal(object[] d)
    {
        int id = (int)d[1];
        if ((byte)d[0] == 1)
        {
            if (portales.ContainsKey(id)) return;
            PortalDeEnemigos portal = PortalDeEnemigos.Abrir((Vector3)d[2], (Vector3)d[3], (float)d[4]);
            portal.IdRed = id;
            portales[id] = portal;
        }
        else if (portales.TryGetValue(id, out PortalDeEnemigos portal))
        {
            portales.Remove(id);
            if (portal != null) portal.Cerrar();
        }
    }

    // =====================================================================
    // CA5: se fue el anfitrión
    // =====================================================================

    public override void OnMasterClientSwitched(Player nuevo)
    {
        if (nuevo == null || !nuevo.IsLocal || !CooperativoDeInvasion.Activo) return;
        Debug.Log("[Invasión en red] Se fue el anfitrión: ahora esta computadora lleva a los invasores.");
        // Los portales de antes se cierran (la oleada abre los suyos).
        foreach (PortalDeEnemigos portal in portales.Values) if (portal != null) portal.Cerrar();
        portales.Clear();
        var vivos = new List<Npc>();
        paraQuitar.Clear();
        foreach (KeyValuePair<int, Npc> par in invasores)
        {
            Npc npc = par.Value;
            if (npc == null) { paraQuitar.Add(par.Key); continue; }
            InvasorEnRed copia = npc.GetComponent<InvasorEnRed>();
            if (copia != null) copia.Soltar();
            npc.Activar();
            if (npc.Vivo) vivos.Add(npc);
        }
        foreach (int id in paraQuitar) invasores.Remove(id);
        pedidos.Clear();
        if (Oleadas != null) Oleadas.TomarElMando(vivos);
        proximoEstadoDeOleada = 0f;
    }

    // =====================================================================
    // Ayudas
    // =====================================================================

    private void Mandar(byte codigo, object contenido, ReceiverGroup quienes, bool seguro)
    {
        if (!PhotonNetwork.InRoom) return;
        PhotonNetwork.RaiseEvent(codigo, contenido, new RaiseEventOptions { Receivers = quienes },
            seguro ? SendOptions.SendReliable : SendOptions.SendUnreliable);
    }

    // El jugador al que apunta: su número en la sala (el de esta computadora, si no es la copia de otro).
    private static int ActorDe(Transform jugador)
    {
        JugadorEnRed j = jugador != null ? jugador.GetComponentInParent<JugadorEnRed>() : null;
        return j != null && j.photonView != null && !j.photonView.IsMine ? j.Actor : Yo;
    }

    // Al revés: el jugador de esta computadora o la copia del compañero con ese número.
    private static Transform JugadorDe(int actor)
    {
        if (actor == Yo)
        {
            PlayerMovement yo = FindFirstObjectByType<PlayerMovement>();
            return yo != null ? yo.transform : null;
        }
        foreach (JugadorEnRed j in FindObjectsByType<JugadorEnRed>(FindObjectsSortMode.None))
            if (j != null && j.photonView != null && !j.photonView.IsMine && j.Actor == actor) return j.transform;
        return null;
    }

    // Los sonidos del tipo, siempre en el mismo orden (por el nombre del campo): el número de cada uno viaja por la red.
    private static List<AudioClip> SonidosDe(TipoDeNpc tipo)
    {
        if (tipo == null) return new List<AudioClip>();
        if (sonidos.TryGetValue(tipo, out List<AudioClip> lista)) return lista;
        lista = new List<AudioClip>();
        System.Reflection.FieldInfo[] campos = typeof(TipoDeNpc).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        System.Array.Sort(campos, (a, b) => string.CompareOrdinal(a.Name, b.Name));
        foreach (System.Reflection.FieldInfo campo in campos)
        {
            if (campo.FieldType == typeof(AudioClip))
            {
                var clip = campo.GetValue(tipo) as AudioClip;
                if (clip != null && !lista.Contains(clip)) lista.Add(clip);
            }
            else if (campo.FieldType == typeof(AudioClip[]) && campo.GetValue(tipo) is AudioClip[] varios)
                foreach (AudioClip clip in varios) if (clip != null && !lista.Contains(clip)) lista.Add(clip);
        }
        sonidos[tipo] = lista;
        return lista;
    }
}

// F31 (US 290, CA1): la copia de un invasor en una computadora que no es la del anfitrión. Va interpolando entre lo que
// llega (un poco atrasada, así siempre tiene dos puntos entre los que moverse), y repite su animación y su vida.
public class InvasorEnRed : MonoBehaviour
{
    private const double Retraso = 0.15; // s atrás de lo último que llegó
    private const int MaxFotos = 20;
    private const float Teletransporte = 8f; // m: más lejos que esto no interpola (apareció o lo empujaron fuerte)

    private struct Foto
    {
        public double t;
        public Vector3 lugar;
        public float giro, altura;
    }

    private readonly List<Foto> fotos = new List<Foto>();
    private Npc npc;
    private List<AnimationClip> clips;
    private int puestas = int.MinValue, golpes = int.MinValue;
    private float velocidad = 1f;
    private bool soltada;

    public void Iniciar(Npc n)
    {
        npc = n;
        clips = AnimadorDeNpc.ClipsDe(n.Tipo);
    }

    /// <summary>CA5: deja de repetir (pasa a ser un invasor de verdad).</summary>
    public void Soltar()
    {
        soltada = true;
        Destroy(this);
    }

    public void Recibir(double cuando, float[] d, int i)
    {
        if (npc == null || soltada) return;
        var foto = new Foto { t = cuando, lugar = new Vector3(d[i + 1], d[i + 2], d[i + 3]), giro = d[i + 4], altura = d[i + 5] };
        int k = fotos.Count;
        while (k > 0 && fotos[k - 1].t > cuando) k--; // (llegan fuera de orden a veces)
        if (k > 0 && fotos[k - 1].t == cuando) return;
        fotos.Insert(k, foto);
        while (fotos.Count > MaxFotos) fotos.RemoveAt(0);

        if (!npc.Vivo) return;
        // La vida (la barra del Jefe, CA4): nunca 0 desde acá; la muerte llega con su aviso.
        int vida = Mathf.RoundToInt(d[i + 6]);
        if (npc.Vida != null && vida != npc.Vida.currentHealth) npc.Vida.SetState(Mathf.Max(1, vida), 0);

        AnimadorDeNpc a = npc.Animaciones;
        if (a == null) return;
        int clip = Mathf.RoundToInt(d[i + 7]), suPuestas = Mathf.RoundToInt(d[i + 8]);
        float suVelocidad = d[i + 10];
        if (suPuestas != puestas && clip >= 0 && clip < clips.Count)
        {
            bool mismo = a.Clip == clips[clip];
            a.Poner(clips[clip], d[i + 9] > 0.5f, puestas == int.MinValue ? 0f : 0.15f, mismo && puestas != int.MinValue);
            a.Velocidad(suVelocidad);
            velocidad = suVelocidad;
        }
        else if (!Mathf.Approximately(suVelocidad, velocidad)) { a.Velocidad(suVelocidad); velocidad = suVelocidad; }
        puestas = suPuestas;

        int golpe = Mathf.RoundToInt(d[i + 11]), susGolpes = Mathf.RoundToInt(d[i + 12]);
        if (golpes != int.MinValue && susGolpes != golpes && golpe >= 0 && golpe < clips.Count) a.Golpe(clips[golpe], d[i + 13]);
        golpes = susGolpes;
    }

    private void Update()
    {
        if (npc == null || soltada) return;
        if (!npc.Vivo) { Caer(); return; }
        if (fotos.Count == 0) return;

        double ahora = PhotonNetwork.Time - Retraso;
        Foto a = fotos[0], b = fotos[fotos.Count - 1];
        for (int k = fotos.Count - 1; k >= 0; k--)
        {
            if (fotos[k].t <= ahora) { a = fotos[k]; b = k + 1 < fotos.Count ? fotos[k + 1] : fotos[k]; break; }
        }
        float f = b.t > a.t ? Mathf.Clamp01((float)((ahora - a.t) / (b.t - a.t))) : 1f;
        Vector3 lugar = Vector3.Lerp(a.lugar, b.lugar, f);
        float giro = Mathf.LerpAngle(a.giro, b.giro, f);
        float altura = Mathf.Lerp(a.altura, b.altura, f);
        if ((transform.position - lugar).sqrMagnitude > Teletransporte * Teletransporte) transform.position = lugar;
        else transform.position = Vector3.Lerp(transform.position, lugar, 1f - Mathf.Exp(-25f * Time.deltaTime));
        transform.rotation = Quaternion.Euler(0f, giro, 0f);
        if (npc.Modelo != null)
        {
            Vector3 p = npc.Modelo.localPosition;
            p.y = altura;
            npc.Modelo.localPosition = p;
        }
        // Lo que ya pasó no hace falta (se deja una para tener desde dónde interpolar).
        while (fotos.Count > 2 && fotos[1].t <= ahora) fotos.RemoveAt(0);
    }

    // Muerto en el aire (el Jefe volando o un Watcher): cae al piso.
    private void Caer()
    {
        if (npc.Modelo == null || npc.Tipo == null) return;
        float piso = npc.Tipo.aDistancia != AtaqueADistancia.Ninguno ? 0f : npc.Tipo.alturaDeVuelo;
        Vector3 p = npc.Modelo.localPosition;
        if (Mathf.Approximately(p.y, piso)) return;
        p.y = Mathf.MoveTowards(p.y, piso, 10f * Time.deltaTime);
        npc.Modelo.localPosition = p;
    }
}
