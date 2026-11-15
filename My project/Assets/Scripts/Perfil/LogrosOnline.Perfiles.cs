using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Perfiles y partidas (F34, perfiles.sql), con la misma sesión y el mismo turno que el resto de LogrosOnline:
// - US 374: sube las partidas del historial que todavía no están en la nube (rw_add_matches) y junta las 20 de la cuenta
//   que devuelve la base, así el historial es el mismo en otra computadora (CA4). La primera vez con cada cuenta pide
//   las de la nube aunque no haya nada para subir.
// - US 373: el perfil público de otro jugador (rw_public_profile), que pide PerfilAjeno al abrirse.
public sealed partial class LogrosOnline
{
    /// <summary>Lo público de un jugador (US 373, CA2 y CA3), como lo manda la base.</summary>
    [Serializable]
    public sealed class PerfilPublico
    {
        public bool found;
        public string player_id, display_name;
        public ProgresoNube.Texto[] valores = Array.Empty<ProgresoNube.Texto>();
        public ProgresoNube.Numero[] contadores = Array.Empty<ProgresoNube.Numero>();
        public int[] cometas = Array.Empty<int>();
        public string[] achievements = Array.Empty<string>();
        public int rank_bajas, rank_logros;
        public HistorialDePartidas.Partida[] partidas = Array.Empty<HistorialDePartidas.Partida>();

        public string Valor(string clave)
        {
            if (valores != null) foreach (var t in valores) if (t != null && t.clave == clave) return t.valor;
            return null;
        }

        public long Contador(string clave)
        {
            if (contadores != null) foreach (var n in contadores) if (n != null && n.clave == clave) return n.valor;
            return 0;
        }

        public bool Tiene(string logro) => achievements != null && Array.IndexOf(achievements, logro) >= 0;
    }

    /// <summary>Cómo está el historial de la cuenta respecto de la nube (US 374, CA4).</summary>
    public enum EstadoDelHistorial { SinCuenta, SinSincronizar, EnLaNube, FaltaActualizarLaBase }

    [Serializable] private sealed class EnvioPartidas { public List<HistorialDePartidas.Partida> p = new List<HistorialDePartidas.Partida>(); }
    [Serializable] private sealed class ConsultaPerfil { public string p_player; }

    private bool historialDisponible = true, perfilesDisponible = true;
    private string historialDeCuenta; // la cuenta cuyo historial ya se trajo de la nube
    private bool historialAlDia;      // la última sincronización del historial salió bien
    private string perfilPedido;
    private Action<PerfilPublico, string> perfilListo;

    public EstadoDelHistorial Historial =>
        !Configurado || !enCuenta ? EstadoDelHistorial.SinCuenta
        : !historialDisponible ? EstadoDelHistorial.FaltaActualizarLaBase
        : historialAlDia && historialDeCuenta == IdDeLaCuenta && !HistorialDePartidas.HayPorSubir
            ? EstadoDelHistorial.EnLaNube : EstadoDelHistorial.SinSincronizar;

    // Hay algo de perfiles para hacer en la próxima vuelta (Update).
    private bool PerfilesPendientes =>
        perfilPedido != null || (enCuenta && historialDisponible && (historialDeCuenta != IdDeLaCuenta || HistorialDePartidas.HayPorSubir));

    /// <summary>
    /// US 373: pide el perfil público de un jugador. listo recibe el perfil (found false si el jugador no tiene cuenta)
    /// o, si no se pudo, null y el motivo (CA4). Si se pide otro antes de que llegue, el anterior no se contesta.
    /// </summary>
    public void PedirPerfil(string idJugador, Action<PerfilPublico, string> listo)
    {
        if (!Configurado || SinSesion) { listo?.Invoke(null, L10n.T("profile.other.offline")); return; }
        if (!perfilesDisponible) { listo?.Invoke(null, L10n.T("profile.other.update_service")); return; }
        perfilPedido = idJugador;
        perfilListo = listo;
        intentar = Time.unscaledTime; // lo pidió el jugador: no espera el reintento de un error anterior
    }

    /// <summary>Deja de esperar el perfil pedido (se cerró la pantalla).</summary>
    public void CancelarPerfil(Action<PerfilPublico, string> listo)
    {
        if (perfilListo != listo) return;
        perfilPedido = null;
        perfilListo = null;
    }

    private void ResponderPerfil(PerfilPublico perfil, string fallo)
    {
        Action<PerfilPublico, string> listo = perfilListo;
        perfilPedido = null;
        perfilListo = null;
        listo?.Invoke(perfil, fallo);
    }

    private IEnumerator TraerPerfil()
    {
        string id = perfilPedido;
        yield return Post("/rest/v1/rpc/rw_public_profile", JsonUtility.ToJson(new ConsultaPerfil { p_player = id }), true);
        if (id != perfilPedido) yield break; // se pidió otro (o se cerró) mientras tanto
        if (error != null)
        {
            if (codigo == 404) perfilesDisponible = false; // falta correr perfiles.sql
            ResponderPerfil(null, codigo == 404 ? L10n.T("profile.other.update_service")
                : codigo == 0 ? L10n.T("profile.other.offline") : L10n.T("profile.other.unavailable"));
            yield break;
        }
        PerfilPublico perfil = null;
        try { perfil = JsonUtility.FromJson<PerfilPublico>(respuesta); } catch { }
        ResponderPerfil(perfil, perfil == null ? L10n.T("profile.other.unavailable") : null);
    }

    // US 374: sube lo que falta del historial y junta lo de la nube. Un error no corta el resto de la sincronización.
    private IEnumerator SincronizarHistorial()
    {
        if (!historialDisponible) yield break;
        string cuenta = IdDeLaCuenta;
        var envio = new EnvioPartidas { p = HistorialDePartidas.PorSubir() };
        if (envio.p.Count == 0 && historialDeCuenta == cuenta) yield break;
        yield return Post("/rest/v1/rpc/rw_add_matches", JsonUtility.ToJson(envio), true);
        historialAlDia = false;
        if (error != null)
        {
            if (codigo == 404) historialDisponible = false; // falta correr perfiles.sql: el historial queda en la PC
            else historialDeCuenta = cuenta; // sin conexión: no se reintenta en cada vuelta, sale con la próxima partida
            yield break;
        }
        HistorialDePartidas.Lista nube = null;
        try { nube = JsonUtility.FromJson<HistorialDePartidas.Lista>(respuesta); } catch { }
        if (nube == null || nube.partidas == null || cuenta != IdDeLaCuenta) yield break;
        HistorialDePartidas.MarcarSubidas(envio.p); // las que se mandaron ya están (aunque la base solo guarde las 20 más nuevas)
        HistorialDePartidas.UnirNube(nube.partidas);
        historialDeCuenta = cuenta;
        historialAlDia = true;
    }
}
