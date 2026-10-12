using System.Collections.Generic;
using UnityEngine;

// Lista de personajes y el que eligió el jugador (US 015).
// - Los personajes son los CharacterData de Resources/Personajes, ordenados por "order".
// - El elegido se guarda entre sesiones (CA5) y lo usa PlayerAbility al empezar la partida (CA6).
public static class CharacterRoster
{
    private const string SelectedKey = "Personaje.Elegido";
    private static List<CharacterData> all;

    public static IReadOnlyList<CharacterData> All
    {
        get
        {
            if (all == null)
            {
                all = new List<CharacterData>(Resources.LoadAll<CharacterData>("Personajes"));
                all.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : string.CompareOrdinal(a.name, b.name));
            }
            return all;
        }
    }

    /// <summary>true si el jugador ya eligió alguna vez (y el personaje sigue existiendo).</summary>
    public static bool HasSelection => IndexOf(PlayerPrefs.GetString(SelectedKey, "")) >= 0;

    /// <summary>El último personaje elegido; si nunca eligió, el primero de la lista.</summary>
    public static CharacterData Selected
    {
        get
        {
            int i = IndexOf(PlayerPrefs.GetString(SelectedKey, ""));
            return i >= 0 ? All[i] : All.Count > 0 ? All[0] : null;
        }
        set
        {
            if (value == null) return;
            PlayerPrefs.SetString(SelectedKey, value.name);
            PlayerPrefs.Save();
        }
    }

    public static int SelectedIndex => IndexOf(PlayerPrefs.GetString(SelectedKey, ""));

    private static int IndexOf(string assetName)
    {
        if (string.IsNullOrEmpty(assetName)) return -1;
        for (int i = 0; i < All.Count; i++) if (All[i].name == assetName) return i;
        return -1;
    }
}
