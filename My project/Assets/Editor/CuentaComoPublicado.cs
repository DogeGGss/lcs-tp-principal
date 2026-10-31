using UnityEditor;

// F18, US 282: en el editor no hay launcher, así que la cuenta se abre desde el Perfil. Con Riftwalker > Cuenta > Como
// en el juego publicado prendido, el juego hace lo mismo que un build publicado: usa la sesión que guardó el launcher
// en esta PC (la carpeta de guardado es la misma) y, sin una, muestra «Iniciá sesión en el launcher». Se aplica al
// entrar en Play.
public static class CuentaComoPublicado
{
    private const string Ruta = "Riftwalker/Cuenta/Como en el juego publicado";

    [MenuItem(Ruta)]
    private static void Cambiar() => EditorPrefs.SetBool(LogrosOnline.ClaveComoPublicado, !EditorPrefs.GetBool(LogrosOnline.ClaveComoPublicado, false));

    [MenuItem(Ruta, true)]
    private static bool Marcar()
    {
        Menu.SetChecked(Ruta, EditorPrefs.GetBool(LogrosOnline.ClaveComoPublicado, false));
        return true;
    }
}
