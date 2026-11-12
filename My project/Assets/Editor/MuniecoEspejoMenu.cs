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

    // F31 (US 357): para ver las animaciones de caído en el muñeco, sin jugar de a varios.
    [MenuItem("Riftwalker/Muñeco espejo: caer o levantarse")]
    private static void CaerOLevantarse()
    {
        EspejoTerceraPersona m = Muneco();
        if (m != null) m.caido = !m.caido;
    }

    [MenuItem("Riftwalker/Muñeco espejo: arrastrarse")]
    private static void Arrastrarse()
    {
        EspejoTerceraPersona m = Muneco();
        if (m != null) m.arrastrarse = !m.arrastrarse;
    }

    private static EspejoTerceraPersona Muneco()
    {
        if (!EditorApplication.isPlaying) { UnityEngine.Debug.Log("Muñeco espejo: dale Play primero."); return null; }
        EspejoTerceraPersona m = UnityEngine.Object.FindFirstObjectByType<EspejoTerceraPersona>();
        if (m == null)
        {
            EditorPrefs.SetBool(EspejoTerceraPersona.Preferencia, true);
            EspejoTerceraPersona.Cambiar(true);
            UnityEngine.Debug.Log("Muñeco espejo: lo prendí; probá de nuevo en un segundo.");
        }
        else Selection.activeGameObject = m.gameObject; // (así se ven sus opciones en el Inspector)
        return m;
    }

    [MenuItem(Opcion, true)]
    private static bool Marcar()
    {
        Menu.SetChecked(Opcion, EditorPrefs.GetBool(EspejoTerceraPersona.Preferencia, false));
        return true;
    }
}
