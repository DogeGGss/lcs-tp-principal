using System;
using System.Collections.Generic;
using UnityEngine;

// Nombre del jugador (US 164). Lo leen el menú, y más adelante las salas, la tabla con Tab,
// los avisos de bajas y Photon (US 026, US 027, US 057, F06).
//
// Se guarda con el progreso del jugador (Guardado, US 162) y sube a la nube con los logros (LogrosOnline).
// El resto del juego usa solo PlayerProfile.Name.
public static class PlayerProfile
{
    public const int MinLength = 3;
    public const int MaxLength = 16;

    private const string Key = "Jugador.Nombre";

    /// <summary>Avisa cuando el jugador cambia su nombre.</summary>
    public static event Action<string> NameChanged;

    /// <summary>El nombre guardado, o vacío si todavía no eligió uno.</summary>
    public static string Name => Guardado.GetString(Key, "");

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
    private enum NameIssue { None, Invalid, Short, Long }
    private static NameIssue Inspect(string raw,out char invalid,out int count)
    {
        string name = Clean(raw); invalid = '\0'; count = 0;
        foreach (char c in name) if (!IsAllowed(c)) { invalid = c; return NameIssue.Invalid; }
        if (name.Length < MinLength) { count = MinLength - name.Length; return NameIssue.Short; }
        if (name.Length > MaxLength) { count = name.Length - MaxLength; return NameIssue.Long; }
        return NameIssue.None;
    }
    public static string Validate(string raw)
    {
        switch (Inspect(raw,out char invalid,out int count))
        {
            case NameIssue.Invalid: return $"No se puede usar \"{invalid}\". Solo letras, números, espacio, guion y guion bajo.";
            case NameIssue.Short: return count == 1 ? "Falta 1 carácter: el mínimo es 3." : $"Faltan {count} caracteres: el mínimo es 3.";
            case NameIssue.Long: return $"Sobran {count} caracteres: el máximo es 16.";
            default: return null;
        }
    }
    public static string ValidationMessage(string raw)
    {
        switch (Inspect(raw,out char invalid,out int count))
        {
            case NameIssue.Invalid: return L10n.T("ui.name.invalid",invalid);
            case NameIssue.Short: return count == 1 ? L10n.Source("Falta 1 carácter: el mínimo es 3.","ui.") : L10n.T("ui.name.short",count);
            case NameIssue.Long: return L10n.T("ui.name.long",count);
            default: return null;
        }
    }

    public static bool IsAllowed(char c) => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_';

    /// <summary>CA4: guarda el nombre si es válido. Devuelve el error si no lo es.</summary>
    public static bool TrySetName(string raw, out string error)
    {
        error = Validate(raw);
        if (error != null) return false;

        string name = Clean(raw);
        Guardado.SetString(Key, name);
        Guardado.Save();
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
