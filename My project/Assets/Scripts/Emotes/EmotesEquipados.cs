using System.Collections.Generic;

// F27 (Emotes): los emotes que el jugador lleva en la rueda (US 261), en sus 4 espacios y en orden. Se guardan con el
// perfil (Guardado); cada espacio guarda el baile por su estado (el nombre de la animación), así no cambia si se agregan
// bailes al catálogo. Se eligen desde el perfil (US 264); la primera vez, los 4 vienen vacíos. Solo se equipan (y solo
// cuentan) los bailes que el jugador tiene (F37, US 388: ColeccionDeEmotes).
// Los 4 espacios también van a la nube (ProgresoNube): en una PC nueva (donde todavía no se eligió nada) se ponen los de
// la nube; después manda lo elegido en esa PC (como las skins equipadas, SkinsDeArmas).
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
            if (lista[i] != null && lista[i].estado == estado) return ColeccionDeEmotes.Tiene(lista[i]) ? i : -1; // F37: solo los que tiene
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
        if (e != null && !ColeccionDeEmotes.Tiene(e)) return; // F37 (US 388): los bloqueados no se equipan
        if (e != null)
            for (int i = 0; i < Espacios; i++)
                if (i != espacio && En(i) == emote) Guardado.SetString(Clave + i, "");
        Guardado.SetString(Clave + espacio, e != null ? e.estado : "");
        Guardado.SetString(ClavePropia, "1");
        Guardado.Save();
        Cambio?.Invoke();
    }

    // ---------- La nube ----------

    private const string ClavePropia = Clave + ".propia", ClaveNube = Clave + ".nube";

    /// <summary>Lo de esta PC manda (ya se trajo la nube, o se eligió algo acá): se sube a la nube.</summary>
    public static bool Propio => Guardado.GetString(ClavePropia, "") == "1" || Guardado.GetString(ClaveNube, "") == "1";

    /// <summary>El baile de cada espacio, por su estado ("" vacío).</summary>
    public static string EstadoEn(int espacio)
    {
        int k = En(espacio);
        CatalogoDeEmotes.Emote e = k >= 0 ? CatalogoDeEmotes.Actual.De(k) : null;
        return e != null ? e.estado : "";
    }

    /// <summary>Lo que volvió de la nube (el estado de cada espacio): en una PC nueva, se pone eso. Después de juntar los
    /// bailes conseguidos (solo se equipan los que se tienen).</summary>
    public static void FusionarNube(IReadOnlyDictionary<int, string> espacios)
    {
        if (Propio) return;
        bool cambio = false;
        var lista = CatalogoDeEmotes.Actual.emotes;
        foreach (KeyValuePair<int, string> par in espacios)
        {
            if (par.Key < 0 || par.Key >= Espacios) continue;
            string estado = "";
            CatalogoDeEmotes.Emote e = lista.Find(x => x != null && x.estado == par.Value);
            if (e != null && ColeccionDeEmotes.Tiene(e)) estado = e.estado;
            if (Guardado.GetString(Clave + par.Key, "") == estado) continue;
            Guardado.SetString(Clave + par.Key, estado);
            cambio = true;
        }
        Guardado.SetString(ClaveNube, "1");
        Guardado.Save();
        if (cambio) Cambio?.Invoke();
    }
}
