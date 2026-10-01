// Prueba solo del Modo Táctico (solo en el editor). Se prende y apaga desde el menú de Unity:
// Riftwalker > Prueba solo (Táctico). Con la prueba prendida:
// - se puede iniciar una sala de Táctico estando solo (normalmente hacen falta 2 jugadores);
// - en la partida, el anfitrión tiene atajos para avanzar las rondas (los lee RondasTacticas):
//   F9 gana tu equipo, F10 gana el rival, F11 salta a la fase siguiente, F8 pone 6 a 6 (muerte súbita),
//   F5 te elimina, F6 planta el dispositivo donde estás y F7 lo desactiva (US 033),
//   F4 te paga una baja ($ 200, US 135), F3 te marca como portador del dispositivo (US 134).
// Además, Riftwalker > Prueba: todos en el mismo equipo deja a todos los jugadores en el mismo equipo (alcanza con
// prenderlo en la computadora del anfitrión, que es la que reparte): sirve para probar el espectador (US 133) y el
// personaje único (US 016) con dos jugadores.
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

    // ---------- Todos en el mismo equipo ----------

    private const string ClaveAliados = "Riftwalker.PruebaTodosAliados";
    private const string MenuAliados = "Riftwalker/Prueba: todos en el mismo equipo";

    private static bool TodosAliados => UnityEditor.EditorPrefs.GetBool(ClaveAliados, false);

    [UnityEditor.MenuItem(MenuAliados)]
    private static void CambiarAliados()
    {
        UnityEditor.EditorPrefs.SetBool(ClaveAliados, !TodosAliados);
        EquiposTacticos.PruebaTodosAliados = TodosAliados;
        UnityEngine.Debug.Log("Prueba: todos en el mismo equipo: " + (TodosAliados ? "prendida" : "apagada"));
    }

    [UnityEditor.MenuItem(MenuAliados, true)]
    private static bool MarcarAliados()
    {
        UnityEditor.Menu.SetChecked(MenuAliados, TodosAliados);
        return true;
    }

    // Al darle Play, el reparto de equipos usa lo que diga el menú.
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AplicarAliados() => EquiposTacticos.PruebaTodosAliados = TodosAliados;
#else
    public static bool Activa => false;
#endif
}
