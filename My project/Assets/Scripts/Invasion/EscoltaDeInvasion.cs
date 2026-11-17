using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// F29 (Modo Invasión+), US 394: la carta Escolta. Lo agrega PartidaDeInvasion.
// - CA1 y CA2: mientras el jugador tenga la carta (EleccionDeInvasion.Veces), hay un dron a su lado (DronEscolta);
//   al descartarla, se va. Así también vuelve solo al reconectarse con la carta (US 359).
// - CA6: en cooperativo, cada uno ve los drones de los compañeros que tienen la carta (la propiedad PropTiene del
//   jugador en la sala) al lado de sus copias, también en el modo espectador. El rayo y los arcos los decide el dueño
//   (su dron hace el daño) y llegan por la red solo para verse: a qué invasor va el rayo (EvRayo) y los puntos de los
//   arcos (EvArcos).
public class EscoltaDeInvasion : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public const string Carta = "Escolta";
    public const string PropTiene = "inv.escolta";
    private const byte EvRayo = 62, EvArcos = 63; // (InvasoresEnRed: 40 a 56; CaidaEnInvasion: 60 y 61)
    private const float RevisarCada = 0.25f, AvisarCada = 1f;

    private DronEscolta propio;
    private readonly Dictionary<int, DronEscolta> deCompaneros = new Dictionary<int, DronEscolta>();
    private readonly List<int> sobran = new List<int>();
    private bool publicado;
    private float proximaRevision, proximoAviso;

    /// <summary>El dron del jugador de esta computadora (null si no tiene la carta).</summary>
    public static DronEscolta Propio => actual != null ? actual.propio : null;
    private static EscoltaDeInvasion actual;

    public override void OnEnable()
    {
        base.OnEnable();
        actual = this;
    }

    public override void OnDisable()
    {
        base.OnDisable();
        if (actual == this) actual = null;
    }

    private void Start() => Publicar(false, true); // de una partida anterior en la misma sala, no

    private void OnDestroy()
    {
        if (propio != null) Destroy(propio.gameObject);
        foreach (DronEscolta d in deCompaneros.Values) if (d != null) Destroy(d.gameObject);
        deCompaneros.Clear();
    }

    private void Update()
    {
        if (Time.time < proximaRevision) return;
        proximaRevision = Time.time + RevisarCada;
        ElPropio();
        if (!CooperativoDeInvasion.Activo) return;
        LosDeLosCompaneros();
        // El rayo se vuelve a avisar cada tanto, para el compañero que recién lo ve (entró, o lo dejó de ver).
        if (propio != null && Time.time >= proximoAviso) { proximoAviso = Time.time + AvisarCada; MandarRayo(propio.Objetivo); }
    }

    // CA1 y CA2: el dron del jugador de esta computadora, mientras tenga la carta (y esté en juego: vivo o caído).
    private void ElPropio()
    {
        bool tiene = EleccionDeInvasion.Veces(Carta) > 0 && !CaidaEnInvasion.YoAfuera;
        PlayerMovement jugador = tiene ? FindFirstObjectByType<PlayerMovement>() : null;
        if (tiene && jugador != null)
        {
            if (propio == null)
            {
                Transform ojos = jugador.cameraTransform != null ? jugador.cameraTransform : null;
                propio = DronEscolta.Crear(jugador.transform, ojos, true);
                propio.CambioElRayo = MandarRayo;
                propio.SalieronArcos = MandarArcos;
            }
            else propio.Sigue = jugador.transform;
        }
        else if (propio != null) { Destroy(propio.gameObject); propio = null; }
        Publicar(propio != null, false);
    }

    private void Publicar(bool tiene, bool siempre)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null || (!siempre && tiene == publicado)) return;
        publicado = tiene;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { PropTiene, tiene } });
    }

    /// <summary>CA6: si ese jugador de la sala tiene la Escolta.</summary>
    public static bool Tiene(Player p) => p != null && p.CustomProperties.TryGetValue(PropTiene, out object v) && v is bool b && b;

    // CA6: los drones de los compañeros, al lado de sus copias (mientras estén en juego: vivos o caídos).
    private void LosDeLosCompaneros()
    {
        sobran.Clear();
        foreach (int actor in deCompaneros.Keys) sobran.Add(actor);
        PartidaEnRed partida = PartidaEnRed.Actual;
        if (partida != null)
            foreach (JugadorEnRed j in partida.Jugadores)
            {
                if (j == null || j.photonView == null || j.photonView.IsMine) continue;
                Player dueno = j.Dueno;
                CaidaEnInvasion.Estado estado = CaidaEnInvasion.EstadoDe(dueno);
                bool enJuego = estado == CaidaEnInvasion.Estado.Vivo || estado == CaidaEnInvasion.Estado.Caido;
                if (!Tiene(dueno) || !enJuego || !j.gameObject.activeInHierarchy) continue;
                sobran.Remove(j.Actor);
                if (!deCompaneros.TryGetValue(j.Actor, out DronEscolta d) || d == null)
                    deCompaneros[j.Actor] = DronEscolta.Crear(j.transform, null, false);
                else d.Sigue = j.transform;
            }
        foreach (int actor in sobran)
        {
            if (deCompaneros.TryGetValue(actor, out DronEscolta d) && d != null) Destroy(d.gameObject);
            deCompaneros.Remove(actor);
        }
    }

    // ---------- CA6: la red ----------

    private static void Mandar(byte codigo, object contenido, bool seguro)
    {
        if (!PhotonNetwork.InRoom) return;
        PhotonNetwork.RaiseEvent(codigo, contenido, new RaiseEventOptions { Receivers = ReceiverGroup.Others },
                                 seguro ? SendOptions.SendReliable : SendOptions.SendUnreliable);
    }

    private static void MandarRayo(Npc npc) => Mandar(EvRayo, npc != null ? npc.IdRed : 0, true);

    // Los puntos como números sueltos (x, y, z de cada uno).
    private static void MandarArcos(Vector3[] puntos)
    {
        if (puntos == null) return;
        var numeros = new float[puntos.Length * 3];
        for (int i = 0; i < puntos.Length; i++) { numeros[i * 3] = puntos[i].x; numeros[i * 3 + 1] = puntos[i].y; numeros[i * 3 + 2] = puntos[i].z; }
        Mandar(EvArcos, numeros, false);
    }

    public void OnEvent(EventData ev)
    {
        if (ev.Code != EvRayo && ev.Code != EvArcos) return;
        if (!deCompaneros.TryGetValue(ev.Sender, out DronEscolta d) || d == null) return;
        if (ev.Code == EvRayo)
        {
            int id = ev.CustomData is int i ? i : 0;
            d.RayoA(id != 0 ? InvasoresEnRed.Invasor(id) : null);
        }
        else if (ev.CustomData is float[] numeros && numeros.Length >= 6)
        {
            var puntos = new Vector3[numeros.Length / 3];
            for (int i = 0; i < puntos.Length; i++) puntos[i] = new Vector3(numeros[i * 3], numeros[i * 3 + 1], numeros[i * 3 + 2]);
            d.ArcosDeLaRed(puntos);
        }
    }
}
