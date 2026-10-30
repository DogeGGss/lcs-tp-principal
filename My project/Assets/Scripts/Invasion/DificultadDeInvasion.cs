using UnityEngine;

// F08 (Modo Invasión), US 161: lo que cambia cada dificultad. Se elige en el andén Invasión de la selección de modo
// (ModeSelectScreen) y queda guardada en MatchSettings.Difficulty, que también recuerda la última (CA2).
//                                Fácil    Normal   Difícil
// - CA3: vida de los enemigos     75 %     100 %    125 %
//        daño de los enemigos     75 %     100 %    150 %
//        cantidad por oleada      −25 %    igual    +25 %
// - CA4: espera para regenerar     3 s      4 s      6 s   (RegeneracionDeVida.Espera)
//        puntos por baja e impacto igual   igual    +25 %
// - CA5: el nombre se ve en el marcador (US 150) y en el resultado (US 151).
// - CA6: el récord se guarda por dificultad (MatchSettings.ZombieRecord).
public static class DificultadDeInvasion
{
    public static readonly string[] Nombres = { "Fácil", "Normal", "Difícil" };

    public static string Nombre => Nombres[(int)MatchSettings.Difficulty];

    /// <summary>CA3: multiplica la vida de cada enemigo.</summary>
    public static float Vida => MatchSettings.Difficulty switch
    {
        ZombieDifficulty.Facil => 0.75f,
        ZombieDifficulty.Dificil => 1.25f,
        _ => 1f
    };

    /// <summary>CA3: multiplica el daño de cada golpe de un enemigo.</summary>
    public static float Danio => MatchSettings.Difficulty switch
    {
        ZombieDifficulty.Facil => 0.75f,
        ZombieDifficulty.Dificil => 1.5f,
        _ => 1f
    };

    /// <summary>CA3: la vida de un enemigo con la dificultad aplicada (solo en una partida de Invasión).</summary>
    public static int VidaDeEnemigo(int vida) =>
        PartidaDeInvasion.Actual != null ? Mathf.Max(1, Mathf.RoundToInt(vida * Vida)) : vida;

    /// <summary>CA3: el daño de un golpe de enemigo con la dificultad aplicada (solo en una partida de Invasión: en el
    /// campo de práctica los NPCs pegan lo de su tipo).</summary>
    public static int DanioDeEnemigo(int danio) =>
        danio > 0 && PartidaDeInvasion.Actual != null ? Mathf.Max(1, Mathf.RoundToInt(danio * Danio)) : danio;

    /// <summary>CA3: multiplica la cantidad de enemigos de cada oleada.</summary>
    public static float Cantidad => MatchSettings.Difficulty switch
    {
        ZombieDifficulty.Facil => 0.75f,
        ZombieDifficulty.Dificil => 1.25f,
        _ => 1f
    };

    /// <summary>CA4: multiplica los puntos de cada impacto y cada baja.</summary>
    public static float Puntos => MatchSettings.Difficulty == ZombieDifficulty.Dificil ? 1.25f : 1f;
}
