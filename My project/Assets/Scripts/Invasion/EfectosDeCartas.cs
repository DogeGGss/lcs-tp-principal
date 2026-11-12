using UnityEngine;

// F08 (Modo Invasión): lo que hace cada carta del puesto (CartaDeInvasion.aplicar), una por historia. Reciben al jugador
// y cuántas veces tiene la carta ahora (1 a 3, US 293, CA5). Las que actúan durante el juego (al matar, por ejemplo)
// no hacen nada al elegirlas (EnElJuego): las maneja CartasEnJuego.
public static class EfectosDeCartas
{
    /// <summary>Para las cartas que actúan durante el juego (CartasEnJuego): al elegirlas no pasa nada más.</summary>
    public static void EnElJuego(GameObject jugador, int veces) { }

    public const float MasReservaPorMochila = 0.4f; // US 315, CA1: por cada vez

    /// <summary>US 315, Mochila: todas las armas tienen 40 % más de reserva por cada vez (80 % y 120 %). Las que lleva
    /// suman esas balas al elegirla; las que agarre después ya vienen con la reserva más grande.</summary>
    public static void Mochila(GameObject jugador, int veces)
    {
        float antes = Municion.MasReserva;
        Municion.MasReserva = 1f + MasReservaPorMochila * veces;
        WeaponSwitcher armas = jugador != null ? jugador.GetComponentInChildren<WeaponSwitcher>(true) : null;
        if (armas != null) armas.AgrandarReservas(antes);
    }

    /// <summary>US 360, CA6: al descartar Mochila, la reserva vuelve a la normal y lo que sobra se pierde.</summary>
    public static void SacarMochila(GameObject jugador)
    {
        Municion.MasReserva = 1f;
        Recortar(jugador);
    }

    /// <summary>US 360: al descartar Sin respaldo, el cargador de la principal vuelve a su tamaño (lo que no entra va a
    /// la reserva) y se puede volver a tener secundaria (vuelve la del arranque).</summary>
    public static void SacarSinRespaldo(GameObject jugador)
    {
        Municion.MasCargadorPrincipal = 1f;
        WeaponSwitcher.SinSecundaria = false;
        Recortar(jugador);
    }

    private static void Recortar(GameObject jugador)
    {
        WeaponSwitcher armas = jugador != null ? jugador.GetComponentInChildren<WeaponSwitcher>(true) : null;
        if (armas == null) armas = Object.FindFirstObjectByType<WeaponSwitcher>();
        if (armas != null) armas.RecortarMunicion();
    }

    /// <summary>US 331, Sin respaldo: el arma principal tiene el doble de cargador (también las que agarre después) y se
    /// pierde la secundaria por el resto de la partida. Al elegirla, el cargador de la principal se llena desde la
    /// reserva.</summary>
    public static void SinRespaldo(GameObject jugador, int veces)
    {
        Municion.MasCargadorPrincipal = 2f;
        WeaponSwitcher armas = jugador != null ? jugador.GetComponentInChildren<WeaponSwitcher>(true) : null;
        if (armas == null) armas = Object.FindFirstObjectByType<WeaponSwitcher>();
        if (armas == null) return;
        armas.PerderSecundaria();
        if (armas.PrimaryObj != null) MunicionDelArma.LlenarCargador(armas.PrimaryObj);
    }
}
