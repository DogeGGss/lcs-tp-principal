using UnityEditor;

// Riftwalker > Muñeco espejo (tercera persona): prende o apaga el muñeco que muestra, adelante del jugador, cómo lo ven
// los demás jugadores (EspejoTerceraPersona). Queda prendido para las próximas veces que se dé Play.
public static class MuniecoEspejoMenu
{
    private const string Opcion = "Riftwalker/Muñeco espejo (tercera persona)";

    [MenuItem(Opcion)]
    private static void Cambiar()
    {
        bool prender = !EditorPrefs.GetBool(EspejoTerceraPersona.Preferencia, false);
        EditorPrefs.SetBool(EspejoTerceraPersona.Preferencia, prender);
        if (EditorApplication.isPlaying) EspejoTerceraPersona.Cambiar(prender);
    }

    [MenuItem(Opcion, true)]
    private static bool Marcar()
    {
        Menu.SetChecked(Opcion, EditorPrefs.GetBool(EspejoTerceraPersona.Preferencia, false));
        return true;
    }
}
