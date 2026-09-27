using System;
using System.Collections.Generic;
using UnityEngine;

// Las acciones del jugador que se pueden reasignar (US 155).
// El orden es el de la lista de la pestaña Controles.
public enum GameAction
{
    Adelante, Atras, Izquierda, Derecha,
    Saltar, Agacharse, Correr,
    Disparar, Apuntar, Recargar, Habilidad,
    ArmaPrincipal, ArmaSecundaria, Cuchillo, Granadas,
    Tienda, Plantar
}

// Teclas del jugador (US 155). Sigue usando el Input clásico: los scripts preguntan
// KeyBindings.Down(GameAction.Recargar) en lugar de Input.GetKeyDown(KeyCode.R).
// Las teclas y las opciones del mouse se guardan en PlayerPrefs (US 048).
// Esc no se puede reasignar: abre la pausa y cancela los cambios de tecla.
public static class KeyBindings
{
    // ---------- Teclas por defecto ----------

    private static readonly Dictionary<GameAction, KeyCode> Defaults = new Dictionary<GameAction, KeyCode>
    {
        { GameAction.Adelante, KeyCode.W },
        { GameAction.Atras, KeyCode.S },
        { GameAction.Izquierda, KeyCode.A },
        { GameAction.Derecha, KeyCode.D },
        { GameAction.Saltar, KeyCode.Space },
        { GameAction.Agacharse, KeyCode.LeftControl },
        { GameAction.Correr, KeyCode.LeftShift },
        { GameAction.Disparar, KeyCode.Mouse0 },
        { GameAction.Apuntar, KeyCode.Mouse1 },
        { GameAction.Recargar, KeyCode.R },
        { GameAction.Habilidad, KeyCode.Q },
        { GameAction.ArmaPrincipal, KeyCode.Alpha1 },
        { GameAction.ArmaSecundaria, KeyCode.Alpha2 },
        { GameAction.Cuchillo, KeyCode.Alpha3 },
        { GameAction.Granadas, KeyCode.Alpha4 },
        { GameAction.Tienda, KeyCode.B },
        { GameAction.Plantar, KeyCode.E },
    };

    private static readonly Dictionary<GameAction, string> Names = new Dictionary<GameAction, string>
    {
        { GameAction.Adelante, "Adelante" },
        { GameAction.Atras, "Atrás" },
        { GameAction.Izquierda, "Izquierda" },
        { GameAction.Derecha, "Derecha" },
        { GameAction.Saltar, "Saltar" },
        { GameAction.Agacharse, "Agacharse" },
        { GameAction.Correr, "Correr" },
        { GameAction.Disparar, "Disparar" },
        { GameAction.Apuntar, "Apuntar" },
        { GameAction.Recargar, "Recargar" },
        { GameAction.Habilidad, "Habilidad" },
        { GameAction.ArmaPrincipal, "Arma principal" },
        { GameAction.ArmaSecundaria, "Arma secundaria" },
        { GameAction.Cuchillo, "Cuchillo" },
        { GameAction.Granadas, "Granadas" },
        { GameAction.Tienda, "Tienda" },
        { GameAction.Plantar, "Plantar / desactivar" },
    };

    // ---------- Opciones del mouse (CA1, CA2) ----------

    public const string SensitivityKey = "Sensibilidad";        // la misma clave que ya usa el menú
    public const string AimSensitivityKey = "SensibilidadApuntar";
    public const string InvertYKey = "InvertirY";
    public const float MinSensitivity = 0.1f, MaxSensitivity = 5f, DefaultSensitivity = 1.5f;
    public const float MinAimSensitivity = 0.2f, MaxAimSensitivity = 2f, DefaultAimSensitivity = 1f;

    public static float Sensitivity
    {
        get => PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity);
        set { PlayerPrefs.SetFloat(SensitivityKey, Mathf.Clamp(value, MinSensitivity, MaxSensitivity)); Changed?.Invoke(); }
    }

    /// <summary>Multiplica la sensibilidad mientras se apunta con mira.</summary>
    public static float AimSensitivity
    {
        get => PlayerPrefs.GetFloat(AimSensitivityKey, DefaultAimSensitivity);
        set { PlayerPrefs.SetFloat(AimSensitivityKey, Mathf.Clamp(value, MinAimSensitivity, MaxAimSensitivity)); Changed?.Invoke(); }
    }

    public static bool InvertY
    {
        get => PlayerPrefs.GetInt(InvertYKey, 0) == 1;
        set { PlayerPrefs.SetInt(InvertYKey, value ? 1 : 0); Changed?.Invoke(); }
    }

    /// <summary>Avisa cuando cambia una tecla o una opción del mouse.</summary>
    public static event Action Changed;

    public static IEnumerable<GameAction> All => (GameAction[])Enum.GetValues(typeof(GameAction));

    // ---------- Consultas para los scripts del juego ----------

    public static KeyCode Key(GameAction action) =>
        (KeyCode)PlayerPrefs.GetInt(PrefKey(action), (int)Defaults[action]);

    public static bool Down(GameAction action) => Input.GetKeyDown(Key(action));
    public static bool Held(GameAction action) => Input.GetKey(Key(action));
    public static bool Up(GameAction action) => Input.GetKeyUp(Key(action));

    /// <summary>-1, 0 o 1 según qué tecla esté apretada (como Input.GetAxisRaw).</summary>
    public static float Axis(GameAction negative, GameAction positive) =>
        (Held(positive) ? 1f : 0f) - (Held(negative) ? 1f : 0f);

    // ---------- Textos (CA6: "R · Recargar" usa la tecla actual) ----------

    public static string Name(GameAction action) => Names[action];

    public static string Label(GameAction action) => Label(Key(action));

    public static string Label(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.Mouse0: return "Clic izq.";
            case KeyCode.Mouse1: return "Clic der.";
            case KeyCode.Mouse2: return "Rueda";
            case KeyCode.Mouse3: return "Mouse 4";
            case KeyCode.Mouse4: return "Mouse 5";
            case KeyCode.Space: return "Espacio";
            case KeyCode.LeftShift: case KeyCode.RightShift: return "Shift";
            case KeyCode.LeftControl: case KeyCode.RightControl: return "Ctrl";
            case KeyCode.LeftAlt: case KeyCode.RightAlt: return "Alt";
            case KeyCode.Return: return "Enter";
            case KeyCode.Backspace: return "Retroceso";
            case KeyCode.CapsLock: return "Bloq Mayús";
            case KeyCode.UpArrow: return "Arriba";
            case KeyCode.DownArrow: return "Abajo";
            case KeyCode.LeftArrow: return "Izquierda";
            case KeyCode.RightArrow: return "Derecha";
            case KeyCode.None: return "—";
        }
        if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return ((int)(key - KeyCode.Alpha0)).ToString();
        if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9) return "Num " + (int)(key - KeyCode.Keypad0);
        return key.ToString();
    }

    // ---------- Cambiar teclas (CA4, CA5) ----------

    /// <summary>La acción que ya usa esa tecla, o null si está libre.</summary>
    public static GameAction? ActionUsing(KeyCode key, GameAction except)
    {
        foreach (GameAction action in All)
            if (action != except && Key(action) == key) return action;
        return null;
    }

    public static void Set(GameAction action, KeyCode key)
    {
        PlayerPrefs.SetInt(PrefKey(action), (int)key);
        Changed?.Invoke();
    }

    /// <summary>CA5: la acción toma la tecla nueva y la otra se queda con la tecla vieja.</summary>
    public static void Swap(GameAction action, GameAction other)
    {
        KeyCode a = Key(action), b = Key(other);
        PlayerPrefs.SetInt(PrefKey(action), (int)b);
        PlayerPrefs.SetInt(PrefKey(other), (int)a);
        Changed?.Invoke();
    }

    public static void ResetDefaults()
    {
        foreach (GameAction action in All) PlayerPrefs.DeleteKey(PrefKey(action));
        PlayerPrefs.SetFloat(SensitivityKey, DefaultSensitivity);
        PlayerPrefs.DeleteKey(AimSensitivityKey);
        PlayerPrefs.DeleteKey(InvertYKey);
        Save();
        Changed?.Invoke();
    }

    public static void Save() => PlayerPrefs.Save();

    /// <summary>Teclas que se pueden asignar: todo menos Esc (cancela) y los joysticks.</summary>
    public static bool CanAssign(KeyCode key) =>
        key != KeyCode.None && key != KeyCode.Escape && key < KeyCode.JoystickButton0;

    private static string PrefKey(GameAction action) => "Tecla." + action;
}
