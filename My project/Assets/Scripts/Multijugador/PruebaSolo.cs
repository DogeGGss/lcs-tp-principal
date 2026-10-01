// Prueba solo del Modo Táctico (solo en el editor). Se prende y apaga desde el menú de Unity:
// Riftwalker > Prueba solo (Táctico). Con la prueba prendida:
// - se puede iniciar una sala de Táctico estando solo (normalmente hacen falta 2 jugadores);
// - en la partida, el anfitrión tiene atajos para avanzar las rondas (los lee RondasTacticas):
//   F9 gana tu equipo, F10 gana el rival, F11 salta a la fase siguiente, F8 pone 6 a 6 (muerte súbita).
// En una build siempre está apagada.
public static class PruebaSolo
{
#if UNITY_EDITOR
    private const string Clave = "Riftwalker.PruebaSolo";
    private const string Menu = "Riftwalker/Prueba solo (Táctico)";

    public static bool Activa => UnityEditor.EditorPrefs.GetBool(Clave, false);

    [UnityEditor.MenuItem(Menu)]
    private static void Cambiar()
    {
        UnityEditor.EditorPrefs.SetBool(Clave, !Activa);
        UnityEngine.Debug.Log("Prueba solo (Táctico): " + (Activa ? "prendida" : "apagada"));
    }

    [UnityEditor.MenuItem(Menu, true)]
    private static bool Marcar()
    {
        UnityEditor.Menu.SetChecked(Menu, Activa);
        return true;
    }
#else
    public static bool Activa => false;
#endif
}
