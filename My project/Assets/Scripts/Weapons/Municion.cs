// Cuándo la reserva de balas no se gasta al recargar (el cargador sí se vacía, y hay que recargar igual):
// - US 121, CA1: en el campo de práctica.
// - Deathmatch: la partida es de reapariciones rápidas sin tienda de balas, así nadie se queda sin munición.
// La usan ArmaDeFuego, Mitre y Pistola al recargar, y el HUD para mostrar "∞" en lugar del número de la reserva.
// US 315 (carta Mochila, Invasión): MasReserva agranda la reserva llena de todas las armas (Reserva); vuelve a 1 al
// empezar y al terminar cada partida de Invasión (PartidaDeInvasion).
public static class Municion
{
    public static bool ReservaInfinita => CampoDePractica.Activo || MatchSettings.Mode == GameMode.Deathmatch;

    /// <summary>US 315: cuánto más grande es la reserva llena de todas las armas (1: la de cada arma).</summary>
    public static float MasReserva { get; set; } = 1f;

    /// <summary>US 315: la reserva llena de un arma que viene con esa (con la Mochila, más).</summary>
    public static int Reserva(int llena) => llena < 0 ? llena : UnityEngine.Mathf.RoundToInt(llena * MasReserva);

    /// <summary>US 315: cuántas balas más tiene la reserva llena de un arma que viene con esa, desde que la reserva era
    /// "antes" veces más grande.</summary>
    public static int Agrandada(int llena, float antes) =>
        llena <= 0 ? 0 : Reserva(llena) - UnityEngine.Mathf.RoundToInt(llena * antes);

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Limpiar() => MasReserva = 1f;
}
