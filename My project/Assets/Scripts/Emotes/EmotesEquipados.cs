// F27 (Emotes): los emotes que el jugador lleva en la rueda (US 261), en sus 4 espacios y en orden. Se guardan con el
// perfil (Guardado); cada espacio guarda el baile por su estado (el nombre de la animación), así no cambia si se agregan
// bailes al catálogo. Se eligen desde el perfil (US 264); la primera vez, los 4 vienen vacíos.
public static class EmotesEquipados
{
    public const int Espacios = 4;
    private const string Clave = "Riftwalker.Emote";

    /// <summary>Cambió algún espacio.</summary>
    public static event System.Action Cambio;

    /// <summary>El número en el catálogo del emote de ese espacio (0 a 3), o -1 si está vacío.</summary>
    public static int En(int espacio)
    {
        if (espacio < 0 || espacio >= Espacios) return -1;
        string estado = Guardado.GetString(Clave + espacio, "");
        if (string.IsNullOrEmpty(estado)) return -1;
        var lista = CatalogoDeEmotes.Actual.emotes;
        for (int i = 0; i < lista.Count; i++)
            if (lista[i] != null && lista[i].estado == estado) return i;
        return -1;
    }

    /// <summary>Hay al menos un emote equipado.</summary>
    public static bool Alguno
    {
        get
        {
            for (int i = 0; i < Espacios; i++) if (En(i) >= 0) return true;
            return false;
        }
    }

    /// <summary>
    /// Pone ese emote (su número en el catálogo; -1, vacío) en ese espacio. Un emote no puede estar en dos espacios: si ya
    /// estaba en otro, ese queda vacío.
    /// </summary>
    public static void Poner(int espacio, int emote)
    {
        if (espacio < 0 || espacio >= Espacios) return;
        CatalogoDeEmotes.Emote e = CatalogoDeEmotes.Actual.De(emote);
        if (e != null)
            for (int i = 0; i < Espacios; i++)
                if (i != espacio && En(i) == emote) Guardado.SetString(Clave + i, "");
        Guardado.SetString(Clave + espacio, e != null ? e.estado : "");
        Cambio?.Invoke();
    }
}
