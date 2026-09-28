using System;
using System.Collections.Generic;
using UnityEngine;

// Nombre del jugador (US 164). Lo leen el menú, y más adelante las salas, la tabla con Tab,
// los avisos de bajas y Photon (US 026, US 027, US 057, F06).
//
// Por ahora se guarda en PlayerPrefs. Cuando exista el guardado del progreso (US 162),
// alcanza con cambiar Load/Save de esta clase: el resto del juego usa solo PlayerProfile.Name.
public static class PlayerProfile
{
    public const int MinLength = 3;
    public const int MaxLength = 16;

    private const string Key = "Jugador.Nombre";

    /// <summary>Avisa cuando el jugador cambia su nombre.</summary>
    public static event Action<string> NameChanged;

    /// <summary>El nombre guardado, o vacío si todavía no eligió uno.</summary>
    public static string Name => PlayerPrefs.GetString(Key, "");

    public static bool HasName => !string.IsNullOrEmpty(Name);

    /// <summary>Espacios de más afuera y en el medio, como se va a guardar.</summary>
    public static string Clean(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        string trimmed = raw.Trim();
        while (trimmed.Contains("  ")) trimmed = trimmed.Replace("  ", " ");
        return trimmed;
    }

    /// <summary>CA2: null si el nombre sirve; si no, qué le falta, para mostrarlo en la ventana.</summary>
    public static string Validate(string raw)
    {
        string name = Clean(raw);

        foreach (char c in name)
            if (!IsAllowed(c))
                return $"No se puede usar \"{c}\". Solo letras, números, espacio, guion y guion bajo.";

        if (name.Length < MinLength)
        {
            int missing = MinLength - name.Length;
            return missing == 1 ? "Falta 1 carácter: el mínimo es 3." : $"Faltan {missing} caracteres: el mínimo es 3.";
        }
        if (name.Length > MaxLength)
            return $"Sobran {name.Length - MaxLength} caracteres: el máximo es 16.";

        return null;
    }

    public static bool IsAllowed(char c) => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_';

    /// <summary>CA4: guarda el nombre si es válido. Devuelve el error si no lo es.</summary>
    public static bool TrySetName(string raw, out string error)
    {
        error = Validate(raw);
        if (error != null) return false;

        string name = Clean(raw);
        PlayerPrefs.SetString(Key, name);
        PlayerPrefs.Save();
        NameChanged?.Invoke(name);
        return true;
    }

    /// <summary>
    /// CA6: nombre para mostrar en una sala. Si ya lo usa otro jugador, le agrega un número: "Luka (2)".
    /// No cambia el nombre guardado; es solo para esa partida. Lo usará la sala cuando exista (US 026, US 027).
    /// </summary>
    public static string UniqueInRoom(string name, IEnumerable<string> takenNames)
    {
        var taken = new HashSet<string>(takenNames, StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(name)) return name;

        int n = 2;
        while (taken.Contains($"{name} ({n})")) n++;
        return $"{name} ({n})";
    }
}
