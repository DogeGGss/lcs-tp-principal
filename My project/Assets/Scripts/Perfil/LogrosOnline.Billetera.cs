using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Las monedas de la cuenta (F18, US 272) y lo que tiene que llegar a la nube aunque se cierre el juego.
// - El saldo lo lleva la base (Backend/Supabase/monedas.sql, rw_wallet_apply). PerfilDatos anota lo ganado en una fila
//   (cada movimiento con su id) y Trabajar la sube; la base devuelve el saldo de la cuenta, que es el que se muestra.
//   Con la fila vacía, la primera vuelta solo trae el saldo: así una PC nueva tiene las monedas de la cuenta (CA1).
// - Para gastar (cápsulas y canjes) la base confirma antes: si se gastó en otra PC, no deja gastar de más (CA3), y sin
//   conexión no se gasta (CA4). Lo ganado sin conexión espera en la fila y sube cuando vuelve.
// - Al cerrar el juego, si queda algo sin subir (progreso, logros, monedas o el nombre), espera unos segundos a que salga.
// - El nombre de la cuenta (user_metadata.nombre) sigue al del jugador; si no se pudo guardar, se reintenta en cada vuelta.
public sealed partial class LogrosOnline
{
    [Serializable] private sealed class MovimientosRpc { public List<PerfilDatos.Movimiento> p_moves = new List<PerfilDatos.Movimiento>(); }
    [Serializable] private sealed class SaldoRpc { public int creditos, boletos, cristales; public string[] rechazados; }

    private const float EsperaAlCerrar = 4f; // s como mucho
    private bool billeteraDisponible = true; // false si la base todavía no tiene rw_wallet_apply (falta correr monedas.sql)
    private bool saldoTraido;                // ya llegó el saldo de la cuenta en esta sesión
    private bool cerrando, puedeCerrar;

    /// <summary>Con cuenta, las monedas se confirman con la base (si ya tiene monedas.sql).</summary>
    public bool BilleteraEnLaNube => Configurado && enCuenta && billeteraDisponible;

    private bool BilleteraPendiente => billeteraDisponible && (!saldoTraido || PerfilDatos.HayPendientes);

    // El nombre del jugador no es todavía el de la cuenta (no se pudo guardar, o se cambió sin conexión).
    private bool NombreDeCuentaPendiente => enCuenta && PlayerProfile.HasName && sesion?.user != null && PlayerProfile.Name != sesion.user.user_metadata?.nombre;

    // ---------- Monedas ----------

    /// <summary>
    /// US 272: gasta con la confirmación de la base. listo recibe null si se gastó, falta si no alcanzó el saldo de la
    /// cuenta (se gastó en otra PC), o el aviso de que hace falta conexión.
    /// </summary>
    public void Gastar(PerfilDatos.Movimiento m, string falta, Action<string> listo) => StartCoroutine(EnTurno(GastarAhora(m, falta, listo)));

    private IEnumerator GastarAhora(PerfilDatos.Movimiento m, string falta, Action<string> listo)
    {
        const string SinConexion = "Para abrir cápsulas y canjear hace falta conexión";
        yield return AsegurarSesion();
        if (!enCuenta) { listo?.Invoke(ReglasCuenta.SesionVencida); yield break; }
        if (falloSesion != null) { listo?.Invoke(SinConexion); yield break; }
        yield return SubirBilletera(m);
        if (!billeteraDisponible) { PerfilDatos.GastarEnLaPC(m); listo?.Invoke(null); yield break; } // falta correr monedas.sql
        if (falloBilletera != null) { listo?.Invoke(SinConexion); yield break; }
        listo?.Invoke(extraRechazado ? falta : null);
    }

    private string falloBilletera;
    private bool extraRechazado;

    // Sube la fila (y el gasto, si hay) y se queda con el saldo que devuelve la base. Si falla, deja el motivo en
    // falloBilletera; si la base no tiene la función, billeteraDisponible queda en false.
    private IEnumerator SubirBilletera(PerfilDatos.Movimiento gasto)
    {
        falloBilletera = null; extraRechazado = false;
        var pedido = new MovimientosRpc { p_moves = PerfilDatos.Pendientes };
        if (pedido.p_moves.Count > 200) pedido.p_moves.RemoveRange(200, pedido.p_moves.Count - 200); // el resto, en la próxima vuelta
        var enviados = new HashSet<string>();
        foreach (PerfilDatos.Movimiento p in pedido.p_moves) enviados.Add(p.id);
        if (gasto != null) pedido.p_moves.Add(gasto);
        yield return Post("/rest/v1/rpc/rw_wallet_apply", JsonUtility.ToJson(pedido), true);
        if (error != null)
        {
            if (codigo == 404) billeteraDisponible = false;
            else falloBilletera = error;
            yield break;
        }
        SaldoRpc saldo = null;
        try { saldo = JsonUtility.FromJson<SaldoRpc>(respuesta); } catch { }
        if (saldo == null) { falloBilletera = "Respuesta del saldo inválida."; yield break; }
        extraRechazado = gasto != null && saldo.rechazados != null && Array.IndexOf(saldo.rechazados, gasto.id) >= 0;
        PerfilDatos.SaldoDeLaNube(saldo.creditos, saldo.boletos, saldo.cristales, enviados);
        saldoTraido = true;
    }

    // ---------- El nombre de la cuenta ----------

    private IEnumerator SubirNombreDeCuenta()
    {
        string nombre = PlayerProfile.Name;
        yield return Pedir("PUT", "/auth/v1/user", JsonUtility.ToJson(new DatosUsuario { data = new Meta { nombre = nombre } }), true);
        if (error != null || sesion?.user == null) yield break; // se reintenta en la próxima vuelta
        if (sesion.user.user_metadata == null) sesion.user.user_metadata = new Meta();
        sesion.user.user_metadata.nombre = nombre;
        GuardarSesion(); // así lo ve también el launcher
    }

    // ---------- Al cerrar el juego ----------

    private bool HayAlgoSinSubir()
    {
        if (!Configurado || SinSesion || !enCuenta || !PlayerProfile.HasName) return false;
        return enviado != LogrosCometas.Revision || nombreEnviado != PlayerProfile.Name || NombreDeCuentaPendiente
            || (progresoDisponible && ProgresoNube.Json() != progresoEnviado) || (billeteraDisponible && PerfilDatos.HayPendientes);
    }

    // Application.wantsToQuit: si queda algo sin subir, el juego espera (como mucho EsperaAlCerrar) y después cierra.
    private bool AlCerrar()
    {
        if (puedeCerrar || !HayAlgoSinSubir()) return true;
        if (!cerrando) { cerrando = true; StartCoroutine(CerrarDespuesDeSubir()); }
        return false;
    }

    private IEnumerator CerrarDespuesDeSubir()
    {
        float hasta = Time.unscaledTime + EsperaAlCerrar;
        while (Ocupado && Time.unscaledTime < hasta) yield return null;
        if (!Ocupado && Time.unscaledTime < hasta) StartCoroutine(Trabajar());
        while (Ocupado && Time.unscaledTime < hasta) yield return null;
        puedeCerrar = true;
        Application.Quit();
    }
}
