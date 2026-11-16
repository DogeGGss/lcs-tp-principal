using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Una sola sesión por cuenta (F38, US 393; sesion-unica.sql). Como en el LoL, vale el último inicio de sesión:
// - Al entrar desde el Perfil (editor y builds de desarrollo) se cierran las demás sesiones de la cuenta
//   (logout?scope=others), como hace el launcher (CA1).
// - Con una cuenta abierta, cada 30 s se pregunta a la base si esta sesión sigue siendo la de la cuenta
//   (rw_check_session), antes de subir nada. Si se entró con la cuenta en otra computadora, se sale de la cuenta, se avisa
//   «Tu cuenta se abrió en otra computadora» y, si estaba en una partida, se vuelve al menú (CA2); desde ahí no se sube
//   nada más a la cuenta (CA4). En un build publicado el menú queda tapado hasta volver a entrar en el launcher.
// - El launcher y el juego de la misma PC comparten la sesión (el mismo archivo): cuentan como una sola (CA5).
// Sin la función en la base (falta correr el SQL) o sin conexión, no se echa a nadie.
public sealed partial class LogrosOnline
{
    private const float CadaCuantoRevisarSesion = 30f;
    private float proximaRevisionSesion;
    private bool sesionUnicaDisponible = true, cerrarOtrasSesiones;

    /// <summary>US 393: se entró con la cuenta en otra computadora y esta salió.</summary>
    public bool SesionDesplazada { get; private set; }

    [Serializable] private sealed class EstadoSesion { public bool active = true; }

    // Toca preguntar (lo mira Update para arrancar una vuelta de Trabajar).
    private bool RevisionDeSesionPendiente =>
        enCuenta && (cerrarOtrasSesiones || (sesionUnicaDisponible && Time.unscaledTime >= proximaRevisionSesion));

    // Se entró a una cuenta (desde el Perfil): esta sesión es la que vale y se cierran las otras.
    private void SesionNueva()
    {
        SesionDesplazada = false;
        cerrarOtrasSesiones = true;
        proximaRevisionSesion = 0f;
    }

    // Una vuelta de Trabajar, con la sesión ya renovada. Si quedó desplazada, Trabajar no sigue.
    private IEnumerator RevisarSesion()
    {
        if (!enCuenta) yield break;
        if (cerrarOtrasSesiones)
        {
            cerrarOtrasSesiones = false;
            yield return Post("/auth/v1/logout?scope=others", "{}", true); // CA1; si falla, el próximo login lo vuelve a intentar
        }
        if (!sesionUnicaDisponible || Time.unscaledTime < proximaRevisionSesion) yield break;
        proximaRevisionSesion = Time.unscaledTime + CadaCuantoRevisarSesion;
        yield return Post("/rest/v1/rpc/rw_check_session", "{}", true);
        if (error != null)
        {
            if (codigo == 404) sesionUnicaDisponible = false; // falta correr sesion-unica.sql
            yield break; // sin conexión: se vuelve a preguntar en 30 s
        }
        EstadoSesion estado = null;
        try { estado = JsonUtility.FromJson<EstadoSesion>(respuesta); } catch { }
        if (estado == null || estado.active) yield break;
        Desplazar();
    }

    // CA2 y CA4: la cuenta se abrió en otra computadora.
    private void Desplazar()
    {
        SesionDesplazada = true;
        SalirDeLaCuentaLocal();
        AvisarEnMenu(L10n.Source(ReglasCuenta.OtraComputadora, "profile."));
        MatchHud.Warn(L10n.Source(ReglasCuenta.OtraComputadora, "profile."), 4f);
        // Si estaba en una partida, sale de ella y vuelve al menú, que pide entrar de nuevo en el launcher.
        if (SceneManager.GetActiveScene().name != "MenuPrincipal" && PauseMenu.Instance != null) PauseMenu.Instance.LeaveMatch();
    }
}
