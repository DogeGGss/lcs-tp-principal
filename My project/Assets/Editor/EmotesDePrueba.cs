using UnityEditor;

// F37 (Emotes coleccionables), US 388: para probar los bailes bloqueados sin abrir cápsulas ni completar el pase.
public static class EmotesDePrueba
{
    [MenuItem("Riftwalker/Emotes/Desbloquear todos los bailes")]
    private static void DesbloquearTodos()
    {
        ColeccionDeEmotes.SinNube = false;
        foreach (CatalogoDeEmotes.Emote e in CatalogoDeEmotes.Actual.emotes)
            if (e != null) ColeccionDeEmotes.Desbloquear(e.estado);
    }

    [MenuItem("Riftwalker/Emotes/Bloquear todos los bailes")]
    private static void BloquearTodos()
    {
        ColeccionDeEmotes.SinNube = true; // si no, los de la nube vuelven en la próxima sincronización
        ColeccionDeEmotes.Quitar();
    }
}
