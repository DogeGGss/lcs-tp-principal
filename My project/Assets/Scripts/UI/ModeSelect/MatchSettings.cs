using UnityEngine;

public enum GameMode { Tactico, Deathmatch, Zombie }
public enum ZombieDifficulty { Facil, Normal, Dificil }

// Lo que el jugador eligió en el menú (US 047, US 161), para que lo lea la escena de la partida.
public static class MatchSettings
{
    private const string DifficultyKey = "Zombie.Dificultad";

    public static GameMode Mode = GameMode.Tactico;
    public static string RoomCode;

    // La dificultad elegida se recuerda entre sesiones (US 161, CA2).
    public static ZombieDifficulty Difficulty
    {
        get => (ZombieDifficulty)PlayerPrefs.GetInt(DifficultyKey, (int)ZombieDifficulty.Normal);
        set { PlayerPrefs.SetInt(DifficultyKey, (int)value); PlayerPrefs.Save(); }
    }

    // Oleada más alta alcanzada en cada dificultad (US 151); 0 si todavía no jugó.
    public static int ZombieRecord(ZombieDifficulty difficulty) => PlayerPrefs.GetInt("Zombie.Record." + difficulty, 0);
}
