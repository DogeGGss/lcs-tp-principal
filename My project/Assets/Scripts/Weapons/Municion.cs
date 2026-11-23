// Cuándo la reserva de balas no se gasta al recargar (el cargador sí se vacía, y hay que recargar igual):
// - US 121, CA1: en el campo de práctica.
// - Deathmatch: la partida es de reapariciones rápidas sin tienda de balas, así nadie se queda sin munición.
// La usan ArmaDeFuego, Mitre y Pistola al recargar, y el HUD para mostrar "∞" en lugar del número de la reserva.
// US 315 (carta Mochila, Invasión): MasReserva agranda la reserva llena de todas las armas (Reserva); vuelve a 1 al
// empezar y al terminar cada partida de Invasión (PartidaDeInvasion).
// F31: en Invasión la reserva llena de todas las armas es el doble que en los demás modos (EnInvasion), y la Mochila
// agranda esa.
public static class Municion
{
    public static bool ReservaInfinita => CampoDePractica.Activo || MatchSettings.Mode == GameMode.Deathmatch;

    /// <summary>US 315: cuánto más grande es la reserva llena de todas las armas (1: la de cada arma).</summary>
    public static float MasReserva { get; set; } = 1f;

    /// <summary>US 331, Sin respaldo: cuántas veces el cargador de las armas principales (2 con la carta).</summary>
    public static float MasCargadorPrincipal { get; set; } = 1f;
    /// <summary>El cargador de un arma (el de la ficha, o más grande si es principal y se tiene Sin respaldo).</summary>
    public static int Cargador(int deLaFicha, bool principal) =>
        principal ? UnityEngine.Mathf.RoundToInt(deLaFicha * MasCargadorPrincipal) : deLaFicha;

    /// <summary>US 322, Tiro de reserva: con el cargador vacío del subfusil o del Mitre, manteniendo el gatillo se sigue
    /// disparando desde la reserva a la mitad de la cadencia (true si se tiene la carta y le sirve).</summary>
    public static System.Func<bool> TiroDeReserva;
    public const float CadenciaDeReserva = 0.5f;

    /// <summary>F31: cuántas veces la reserva de la ficha tiene cada arma en Invasión.</summary>
    public const float EnInvasion = 2f;

    // La del modo: el doble en Invasión (no en el campo de práctica, que es infinita).
    private static float DelModo => MatchSettings.Mode == GameMode.Zombie && !CampoDePractica.Activo ? EnInvasion : 1f;

    /// <summary>US 315: la reserva llena de un arma que viene con esa (en Invasión, el doble; con la Mochila, más).</summary>
    public static int Reserva(int llena) => llena < 0 ? llena : UnityEngine.Mathf.RoundToInt(llena * DelModo * MasReserva);

    /// <summary>US 315: cuántas balas más tiene la reserva llena de un arma que viene con esa, desde que la reserva era
    /// "antes" veces más grande.</summary>
    public static int Agrandada(int llena, float antes) =>
        llena <= 0 ? 0 : Reserva(llena) - UnityEngine.Mathf.RoundToInt(llena * DelModo * antes);

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Limpiar() { MasReserva = 1f; MasCargadorPrincipal = 1f; }
}
