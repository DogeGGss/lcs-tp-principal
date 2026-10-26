// Cuándo la reserva de balas no se gasta al recargar (el cargador sí se vacía, y hay que recargar igual):
// - US 121, CA1: en el campo de práctica.
// - Deathmatch: la partida es de reapariciones rápidas sin tienda de balas, así nadie se queda sin munición.
// La usan ArmaDeFuego, Mitre y Pistola al recargar, y el HUD para mostrar "∞" en lugar del número de la reserva.
public static class Municion
{
    public static bool ReservaInfinita => CampoDePractica.Activo || MatchSettings.Mode == GameMode.Deathmatch;
}
