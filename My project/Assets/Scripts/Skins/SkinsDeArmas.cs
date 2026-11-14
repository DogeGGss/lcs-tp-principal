using System;
using System.Collections.Generic;
using UnityEngine;

// F35 (Skins de armas), US 378: las skins que tiene el jugador y la equipada de cada arma. Se guardan con el progreso del
// jugador (Guardado, va a la cuenta) y en la nube con el resto (ProgresoNube): las obtenidas se juntan (nunca se
// pierde una) y las equipadas son las de esta PC, salvo en una PC nueva, que primero trae las de la nube (como el Perfil).
// Las skins las da el pase (F32) y las cápsulas (US 382) con Desbloquear; se equipan desde el perfil (US 379).
public static class SkinsDeArmas
{
    public enum Estado { Bloqueada, Disponible, Equipada }

    public const string Clave = "Riftwalker.Skins";

    [Serializable] private class Equipada { public string arma, skin; }

    [Serializable]
    private class Datos
    {
        public List<string> obtenidas = new List<string>();
        public List<string> nuevas = new List<string>();
        public List<Equipada> equipadas = new List<Equipada>();
        public bool nubeRecibida, elegidaEnEstaPC;
    }

    /// <summary>Cambió algo (se obtuvo, se equipó o volvió de la nube).</summary>
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

    public static bool Tiene(CatalogoDeSkins.Skin s) => s != null && D.obtenidas.Contains(s.id);
    public static bool EsNueva(CatalogoDeSkins.Skin s) => s != null && D.nuevas.Contains(s.id);
    public static IEnumerable<string> Obtenidas => D.obtenidas;
    public static int CantidadObtenidas => CatalogoDeSkins.Skins.FindAll(Tiene).Count;
    public static int CantidadNuevas => CatalogoDeSkins.Skins.FindAll(EsNueva).Count;

    /// <summary>La skin equipada de esa arma, o null (sin skin: la original).</summary>
    public static CatalogoDeSkins.Skin EquipadaDe(string arma)
    {
        Equipada e = D.equipadas.Find(x => x.arma == arma);
        CatalogoDeSkins.Skin s = e != null ? CatalogoDeSkins.De(e.skin) : null;
        return s != null && s.arma.id == arma && Tiene(s) ? s : null;
    }

    public static Estado EstadoDe(CatalogoDeSkins.Skin s) =>
        !Tiene(s) ? Estado.Bloqueada : EquipadaDe(s.arma.id) == s ? Estado.Equipada : Estado.Disponible;

    // ---------- Cambios ----------

    /// <summary>La suma a las obtenidas (el pase o una cápsula). Devuelve false si ya la tenía (repetida).</summary>
    public static bool Desbloquear(string id, bool marcarNueva = true)
    {
        CatalogoDeSkins.Skin s = CatalogoDeSkins.De(id);
        if (s == null || Tiene(s)) return false;
        D.obtenidas.Add(s.id);
        if (marcarNueva) D.nuevas.Add(s.id);
        Guardar();
        return true;
    }

    public static void Vista(CatalogoDeSkins.Skin s)
    {
        if (s != null && D.nuevas.Remove(s.id)) Guardar();
    }

    /// <summary>La equipa en su arma (la que tenía queda sin equipar). Las bloqueadas no se equipan.</summary>
    public static bool Equipar(CatalogoDeSkins.Skin s)
    {
        if (!Tiene(s)) return false;
        Poner(s.arma.id, s.id);
        D.nuevas.Remove(s.id);
        D.elegidaEnEstaPC = true;
        Guardar();
        return true;
    }

    /// <summary>Deja esa arma sin skin (la original).</summary>
    public static void Desequipar(string arma)
    {
        if (D.equipadas.RemoveAll(x => x.arma == arma) == 0) return;
        D.elegidaEnEstaPC = true;
        Guardar();
    }

    private static void Poner(string arma, string skin)
    {
        D.equipadas.RemoveAll(x => x.arma == arma);
        if (!string.IsNullOrEmpty(skin)) D.equipadas.Add(new Equipada { arma = arma, skin = skin });
    }

    /// <summary>
    /// Solo para probar, en el editor: no trae las skins obtenidas de la nube. La nube nunca pierde una skin (las junta), así
    /// que sin esto las que se sacan con "Bloquear todas" vuelven en la próxima sincronización. Lo prende "Bloquear todas"
    /// y lo apaga "Desbloquear todas" (Riftwalker > Armas > Skins de prueba). En el juego siempre es false.
    /// </summary>
#if UNITY_EDITOR
    public static bool SinNube
    {
        get => UnityEditor.EditorPrefs.GetBool(Clave + ".SinNube", false);
        set => UnityEditor.EditorPrefs.SetBool(Clave + ".SinNube", value);
    }
#else
    public static bool SinNube => false;
#endif

    /// <summary>Solo para probar (Riftwalker > Armas > Skins de prueba): saca una skin o todas.</summary>
    public static void Quitar(string id = null)
    {
        if (id == null) { D.obtenidas.Clear(); D.nuevas.Clear(); D.equipadas.Clear(); }
        else { D.obtenidas.Remove(id); D.nuevas.Remove(id); D.equipadas.RemoveAll(x => x.skin == id); }
        Guardar();
    }

    // ---------- Nube (ProgresoNube) ----------

    /// <summary>Si lo equipado en esta PC ya se puede subir: en una PC nueva primero se trae lo de la nube.</summary>
    public static bool EquipadoPropio => D.nubeRecibida || D.elegidaEnEstaPC;

    /// <summary>La equipada de cada arma (arma, id de la skin).</summary>
    public static IEnumerable<(string arma, string skin)> Equipadas
    {
        get { foreach (Equipada e in D.equipadas) yield return (e.arma, e.skin); }
    }

    /// <summary>Junta lo que vuelve de la nube: suma las obtenidas y, en una PC nueva, equipa las de la nube.</summary>
    public static void FusionarNube(IEnumerable<string> obtenidas, IReadOnlyDictionary<string, string> equipadas)
    {
        bool cambio = false;
        if (!SinNube)
            foreach (string id in obtenidas)
                if (CatalogoDeSkins.De(id) != null && !D.obtenidas.Contains(id)) { D.obtenidas.Add(id); cambio = true; }
        if (!EquipadoPropio)
            foreach (KeyValuePair<string, string> e in equipadas)
            {
                CatalogoDeSkins.Skin s = CatalogoDeSkins.De(e.Value);
                if (CatalogoDeSkins.ArmaDe(e.Key) == null) continue;
                if (!string.IsNullOrEmpty(e.Value) && (s == null || s.arma.id != e.Key || !Tiene(s))) continue; // US 163: no se equipa lo que no se tiene
                if (EquipadaDe(e.Key)?.id == (s?.id)) continue;
                Poner(e.Key, s?.id);
                cambio = true;
            }
        if (!D.nubeRecibida) { D.nubeRecibida = true; cambio = true; }
        if (cambio) Guardar();
    }
}
