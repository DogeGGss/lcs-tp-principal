using UnityEngine;

public enum GameMode { Tactico, Deathmatch, Zombie }
public enum ZombieDifficulty { Facil, Normal, Dificil }

// Lo que el jugador eligió en el menú (US 047, US 161), para que lo lea la escena de la partida.
// Se guarda con el progreso del jugador (Guardado, US 162).
public static class MatchSettings
{
    private const string DifficultyKey = "Zombie.Dificultad";
    public const string ZombieRecordPrefix = "Zombie.Record.";

    public static GameMode Mode = GameMode.Tactico;
    public static string RoomCode;

    // La dificultad elegida se recuerda entre sesiones (US 161, CA2).
    public static ZombieDifficulty Difficulty
    {
        get => (ZombieDifficulty)Guardado.GetInt(DifficultyKey, (int)ZombieDifficulty.Normal);
        set { Guardado.SetInt(DifficultyKey, (int)value); Guardado.Save(); }
    }

    // Oleada más alta alcanzada en cada dificultad (US 151); 0 si todavía no jugó.
    public static int ZombieRecord(ZombieDifficulty difficulty) => Guardado.GetInt(ZombieRecordPrefix + difficulty, 0);

    /// <summary>Guarda la oleada si supera el récord de esa dificultad (también lo que vuelve de la nube, US 163).</summary>
    public static void SubirZombieRecord(ZombieDifficulty difficulty, int oleada)
    {
        if (oleada > ZombieRecord(difficulty)) Guardado.SetInt(ZombieRecordPrefix + difficulty, oleada);
    }
}
