using System;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Volver a una partida online después de un corte (US 195).
// - Al empezar la partida, la sala guarda 2 minutos a quien se desconecta (PlayerTtl): sigue en la sala como
//   inactivo, con su número de jugador, su equipo y sus propiedades (plata, armas, bajas y muertes).
// - Mientras se juega, esta computadora anota con qué sala, región, usuario de Photon y nombre se está jugando, y
//   hasta cuándo se puede volver (se renueva cada pocos segundos). Si se corta la conexión o se cierra el juego, el
//   menú principal muestra "Volver a la partida" hasta que pasan los 2 minutos (CA2, VolverALaPartida).
// - Salir desde la pausa o que la partida termine borra lo anotado: ahí no se ofrece volver (CA7).
// Lo agrega PartidaEnRed al empezar una partida online.
public class Reconexion : MonoBehaviour
{
    public const int SegundosParaVolver = 120; // CA2
    private const float CadaCuanto = 5f;
    private const string ClaveSala = "Reconexion.Sala", ClaveRegion = "Reconexion.Region", ClaveUsuario = "Reconexion.Usuario",
        ClaveNombre = "Reconexion.Nombre", ClaveHasta = "Reconexion.Hasta";

    /// <summary>
    /// Usuario de Photon de esta vez que se abrió el juego. Es uno nuevo cada vez, así dos juegos abiertos en la misma
    /// computadora no se confunden; para volver a una partida se usa el que quedó anotado.
    /// </summary>
    public static readonly string UsuarioDeEstaVez = Guid.NewGuid().ToString("N");

    /// <summary>La partida anotada a la que se puede volver.</summary>
    public struct Partida
    {
        public string sala, region, usuario, nombre;
        public float restante; // segundos que quedan para poder volver
    }

    /// <summary>El jugador de esta computadora entró a la sala volviendo a una partida (y no desde el andén).</summary>
    public static bool Volvio => PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer != null && PhotonNetwork.LocalPlayer.HasRejoined;

    /// <summary>Ese jugador sigue en la sala y conectado (no se fue ni está desconectado esperando volver).</summary>
    public static bool Conectado(int actor)
    {
        Player jugador = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.GetPlayer(actor) : null;
        return jugador != null && !jugador.IsInactive;
    }

    // =====================================================================
    // Lo anotado en esta computadora
    // =====================================================================

    /// <summary>CA2: si hay una partida a la que todavía se puede volver.</summary>
    public static bool HayPartida(out Partida partida)
    {
        partida = new Partida
        {
            sala = PlayerPrefs.GetString(ClaveSala, ""),
            region = PlayerPrefs.GetString(ClaveRegion, ""),
            usuario = PlayerPrefs.GetString(ClaveUsuario, ""),
            nombre = PlayerPrefs.GetString(ClaveNombre, "")
        };
        if (string.IsNullOrEmpty(partida.sala) || string.IsNullOrEmpty(partida.usuario)) return false;
        if (!long.TryParse(PlayerPrefs.GetString(ClaveHasta, ""), out long hasta)) return false;
        partida.restante = (float)(new DateTime(hasta, DateTimeKind.Utc) - DateTime.UtcNow).TotalSeconds;
        return partida.restante > 0f && partida.restante <= SegundosParaVolver + CadaCuanto;
    }

    /// <summary>CA7: ya no se puede volver (salió a propósito, terminó la partida o no se pudo volver).</summary>
    public static void Olvidar()
    {
        if (!PlayerPrefs.HasKey(ClaveSala)) return;
        PlayerPrefs.DeleteKey(ClaveSala);
        PlayerPrefs.DeleteKey(ClaveRegion);
        PlayerPrefs.DeleteKey(ClaveUsuario);
        PlayerPrefs.DeleteKey(ClaveNombre);
        PlayerPrefs.DeleteKey(ClaveHasta);
        PlayerPrefs.Save();
    }

    /// <summary>CA7: sale a propósito de esta sala; si era la de la partida anotada, ya no se ofrece volver.</summary>
    public static void OlvidarSiEsLaSala(string sala)
    {
        if (!string.IsNullOrEmpty(sala) && PlayerPrefs.GetString(ClaveSala, "") == sala) Olvidar();
    }

    /// <summary>La región de Photon sin el "/*" que agrega a veces (por ejemplo, "sa").</summary>
    public static string Region(string region)
    {
        if (string.IsNullOrEmpty(region)) return "";
        int barra = region.IndexOf('/');
        return barra >= 0 ? region.Substring(0, barra) : region;
    }

    private static void Anotar()
    {
        Room sala = PhotonNetwork.CurrentRoom;
        Player yo = PhotonNetwork.LocalPlayer;
        if (sala == null || yo == null) return;
        string usuario = !string.IsNullOrEmpty(yo.UserId) ? yo.UserId : PhotonNetwork.AuthValues != null ? PhotonNetwork.AuthValues.UserId : "";
        if (string.IsNullOrEmpty(usuario)) return;
        PlayerPrefs.SetString(ClaveSala, sala.Name);
        PlayerPrefs.SetString(ClaveRegion, Region(PhotonNetwork.CloudRegion));
        PlayerPrefs.SetString(ClaveUsuario, usuario);
        PlayerPrefs.SetString(ClaveNombre, yo.NickName);
        PlayerPrefs.SetString(ClaveHasta, DateTime.UtcNow.AddSeconds(SegundosParaVolver).Ticks.ToString());
        PlayerPrefs.Save();
    }

    // =====================================================================
    // Mientras se juega
    // =====================================================================

    private float proxima;
    private bool terminada;
    private static bool simulandoCorte;

    /// <summary>Prueba solo: deja de simular el corte (lo llama Multijugador al desconectarse), así se puede volver.</summary>
    public static void TerminarCorteDePrueba()
    {
        if (!simulandoCorte) return;
        simulandoCorte = false;
        PhotonNetwork.NetworkingClient.SimulateConnectionLoss(false);
    }

    private void Update()
    {
        // Prueba solo (solo en el editor): F12 corta la conexión como si se cayera la red. A los pocos segundos vuelve
        // al menú, que ofrece volver a la partida.
        if (PruebaSolo.Activa && !simulandoCorte && PhotonNetwork.InRoom && Input.GetKeyDown(KeyCode.F12))
        {
            simulandoCorte = true;
            PhotonNetwork.NetworkingClient.SimulateConnectionLoss(true);
            Debug.Log("Prueba solo: se simula un corte de conexión (F12). En unos segundos vuelve al menú.");
        }

        if (terminada || !PhotonNetwork.InRoom || Time.unscaledTime < proxima) return;
        proxima = Time.unscaledTime + CadaCuanto;

        // La partida ya terminó: no hay a qué volver.
        RondasTacticas rondas = RondasTacticas.Actual;
        if ((rondas != null && rondas.Listo && rondas.FaseActual == RondasTacticas.Fase.Terminada) || PartidaDeathmatch.YaTermino)
        {
            terminada = true;
            Olvidar();
            return;
        }
        Anotar();
    }
}
