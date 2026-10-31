using UnityEditor;
using UnityEngine;

// F08 (Modo Invasión): atajos para probar las últimas oleadas sin jugar toda la partida. Con el juego andando en una
// partida de Invasión:
// - Riftwalker > Invasión > Ir a la oleada 10: saca a los enemigos que quedan y arranca la oleada 10 (la del jefe).
// - Riftwalker > Invasión > Matar a todos los invasores: mata a los que están vivos, como si les hubiera disparado
//   (suman sus puntos). La oleada se supera cuando ya aparecieron todos, así que puede hacer falta usarlo varias veces.
// - Riftwalker > Invasión > Sumar 5.000 puntos: suma como si fueran bajas (pasa por la dificultad y saca el tope), para
//   probar que los puntos no tienen tope (US 147, CA5) sin jugar muchas oleadas.
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

    [MenuItem("Riftwalker/Invasión/Sumar 5.000 puntos")]
    private static void SumarPuntos()
    {
        PuntosDeInvasion puntos = PartidaDeInvasion.Actual.GetComponent<PuntosDeInvasion>();
        if (puntos == null) puntos = Object.FindFirstObjectByType<PuntosDeInvasion>();
        if (puntos == null) { Debug.LogWarning("Invasión de prueba: no hay PuntosDeInvasion en la partida."); return; }
        typeof(PuntosDeInvasion)
            .GetMethod("Sumar", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(puntos, new object[] { 5000, false });
        Debug.Log("Invasión de prueba: 5.000 puntos sumados.");
    }

    [MenuItem("Riftwalker/Invasión/Ir a la oleada 10", true)]
    [MenuItem("Riftwalker/Invasión/Matar a todos los invasores", true)]
    [MenuItem("Riftwalker/Invasión/Sumar 5.000 puntos", true)]
    private static bool SePuede() =>
        Application.isPlaying && PartidaDeInvasion.Actual != null && PartidaDeInvasion.Actual.GetComponent<OleadasDeInvasion>() != null;
}
