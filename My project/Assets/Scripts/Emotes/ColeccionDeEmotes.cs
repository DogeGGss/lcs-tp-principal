using System;
using System.Collections.Generic;
using UnityEngine;

// F37 (Emotes coleccionables), US 388: los bailes que tiene el jugador. Cada baile tiene su rareza (CatalogoDeEmotes);
// el de rareza Inicial (el Saludo) lo tienen todos de entrada y los demás se desbloquean (en las cápsulas, el pase o la
// tienda: US 389 y US 390, con Desbloquear). Se guardan con el progreso del jugador (Guardado, va a la cuenta) y en la
// nube (ProgresoNube), juntando los de las dos partes: nunca se pierde uno. Un baile recién conseguido queda "nuevo"
// hasta que se lo ve en el perfil.
public static class ColeccionDeEmotes
{
    public enum Estado { Bloqueado, Disponible, Equipado }

    public const string Clave = "Riftwalker.Emotes"; // (empieza con "Riftwalker.Emote": va a la cuenta)

    [Serializable]
    private class Datos
    {
        public List<string> obtenidos = new List<string>();
        public List<string> nuevos = new List<string>();
    }

    /// <summary>Cambió algo (se consiguió un baile, se vio uno nuevo o volvió de la nube).</summary>
    public static event Action Cambio;

    private static Datos d;

    private static Datos D
    {
        get
        {
            if (d != null) return d;
            string json = Guardado.GetString(Clave, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { d = JsonUtility.FromJson<Datos>(json); } catch { d = null; }
            }
            return d ??= new Datos();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void AlArrancar() => d = null; // se vuelve a leer en cada Play (el editor no recarga el dominio)

    private static void Guardar()
    {
        Guardado.SetString(Clave, JsonUtility.ToJson(D));
        Guardado.Save();
        Cambio?.Invoke();
    }

    /// <summary>Vuelve a leer lo guardado (cambió el jugador o lo cambió otro juego abierto).</summary>
    public static void Recargar()
    {
        d = null;
        Cambio?.Invoke();
    }

    // ---------- Consultas ----------

    /// <summary>Lo tiene: el de entrada (rareza Inicial) siempre; los demás, si los consiguió.</summary>
    public static bool Tiene(CatalogoDeEmotes.Emote e) =>
        e != null && (e.rareza == PerfilDatos.Rareza.Inicial || D.obtenidos.Contains(e.estado));

    public static bool EsNuevo(CatalogoDeEmotes.Emote e) => e != null && D.nuevos.Contains(e.estado);

    public static IEnumerable<string> Obtenidos => D.obtenidos;

    public static Estado EstadoDe(CatalogoDeEmotes.Emote e)
    {
        if (!Tiene(e)) return Estado.Bloqueado;
        List<CatalogoDeEmotes.Emote> lista = CatalogoDeEmotes.Actual.emotes;
        for (int i = 0; i < EmotesEquipados.Espacios; i++)
        {
            int k = EmotesEquipados.En(i);
            if (k >= 0 && k < lista.Count && lista[k] == e) return Estado.Equipado;
        }
        return Estado.Disponible;
    }

    // ---------- Cambios ----------

    /// <summary>Lo suma a los conseguidos (por su estado, el nombre de la animación). false si ya lo tenía.</summary>
    public static bool Desbloquear(string estado, bool marcarNuevo = true)
    {
        CatalogoDeEmotes.Emote e = CatalogoDeEmotes.Actual.emotes.Find(x => x != null && x.estado == estado);
        if (e == null || Tiene(e)) return false;
        D.obtenidos.Add(e.estado);
        if (marcarNuevo && !D.nuevos.Contains(e.estado)) D.nuevos.Add(e.estado);
        Guardar();
        return true;
    }

    /// <summary>Ya se lo vio en el perfil: deja de estar marcado como nuevo.</summary>
    public static void Visto(CatalogoDeEmotes.Emote e)
    {
        if (e != null && D.nuevos.Remove(e.estado)) Guardar();
    }

    /// <summary>
    /// Solo para probar, en el editor: no trae los bailes de la nube. La nube nunca pierde uno, así que sin esto los que saca
    /// "Bloquear todos los bailes" vuelven en la próxima sincronización. Lo prende "Bloquear todos los bailes" y lo apaga
    /// "Desbloquear todos los bailes" (Riftwalker > Emotes). En el juego siempre es false.
    /// </summary>
#if UNITY_EDITOR
    public static bool SinNube
    {
        get => UnityEditor.EditorPrefs.GetBool("Riftwalker.Emotes.SinNube", false);
        set => UnityEditor.EditorPrefs.SetBool("Riftwalker.Emotes.SinNube", value);
    }
#else
    public static bool SinNube => false;
#endif

    /// <summary>Solo para probar (Riftwalker > Emotes): saca todos los conseguidos.</summary>
    public static void Quitar()
    {
        D.obtenidos.Clear();
        D.nuevos.Clear();
        Guardar();
    }

    // ---------- Nube (ProgresoNube) ----------

    /// <summary>Junta los que vuelven de la nube con los de esta PC (nunca se pierde uno).</summary>
    public static void FusionarNube(IEnumerable<string> obtenidos)
    {
        if (SinNube) return; // "Bloquear todos los bailes" en el editor: la nube no los devuelve
        bool cambio = false;
        foreach (string estado in obtenidos)
            if (!string.IsNullOrEmpty(estado) && !D.obtenidos.Contains(estado)) { D.obtenidos.Add(estado); cambio = true; }
        if (cambio) Guardar();
    }
}
