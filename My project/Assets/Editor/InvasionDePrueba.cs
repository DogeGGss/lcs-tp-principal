using UnityEditor;
using UnityEngine;

// F08 (Modo Invasión): atajos para probar las últimas oleadas sin jugar toda la partida. Con el juego andando en una
// partida de Invasión:
// - Riftwalker > Invasión > Ir a la oleada 10: saca a los enemigos que quedan y arranca la oleada 10 (la del jefe).
// - Riftwalker > Invasión > Matar a todos los invasores: mata a los que están vivos, como si les hubiera disparado
//   (suman sus puntos). La oleada se supera cuando ya aparecieron todos, así que puede hacer falta usarlo varias veces.
public static class InvasionDePrueba
{
    [MenuItem("Riftwalker/Invasión/Ir a la oleada 10")]
    private static void IrALaDiez() => PartidaDeInvasion.Actual.GetComponent<OleadasDeInvasion>().IrA(OleadasDeInvasion.Ultima);

    [MenuItem("Riftwalker/Invasión/Matar a todos los invasores")]
    private static void MatarATodos()
    {
        int cuantos = 0;
        foreach (Npc npc in new System.Collections.Generic.List<Npc>(Npc.Todos))
        {
            if (npc == null || !npc.Vivo) continue;
            npc.Vida.TakeDamage(npc.Vida.currentHealth + 1);
            cuantos++;
        }
        Debug.Log($"Invasión de prueba: {cuantos} invasores muertos.");
    }

    [MenuItem("Riftwalker/Invasión/Ir a la oleada 10", true)]
    [MenuItem("Riftwalker/Invasión/Matar a todos los invasores", true)]
    private static bool SePuede() =>
        Application.isPlaying && PartidaDeInvasion.Actual != null && PartidaDeInvasion.Actual.GetComponent<OleadasDeInvasion>() != null;
}
