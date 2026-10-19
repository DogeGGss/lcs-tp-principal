using System;

// Lo prenden los tests automáticos (Assets/Tests) antes de entrar en Play, con la variable de entorno
// RIFTWALKER_PRUEBAS=1, así el juego que corre durante los tests no toca nada real: el guardado usa un archivo
// temporal (Guardado.Ruta) y no se conecta al ranking online ni manda progreso u opiniones (LogrosOnline).
// Es una variable de entorno y no un campo estático porque tiene que sobrevivir a la recarga de scripts al entrar en
// Play, y porque se puede prender también al correr los tests por línea de comandos.
public static class ModoPruebas
{
    public const string Variable = "RIFTWALKER_PRUEBAS";

    public static bool Activo => Environment.GetEnvironmentVariable(Variable) == "1";

#if UNITY_EDITOR
    // Red de seguridad: si una corrida de tests se corta antes de limpiar, al volver al editor se apaga igual, así el
    // próximo Play a mano usa el guardado real.
    [UnityEditor.InitializeOnLoadMethod]
    private static void ApagarAlSalirDePlay()
    {
        UnityEditor.EditorApplication.playModeStateChanged += estado =>
        {
            if (estado == UnityEditor.PlayModeStateChange.EnteredEditMode) Environment.SetEnvironmentVariable(Variable, null);
        };
    }
#endif
}
