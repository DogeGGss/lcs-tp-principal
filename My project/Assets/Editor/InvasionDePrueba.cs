using UnityEditor;
using UnityEngine;

// F08 (Modo Invasión): atajos para probar las últimas oleadas sin jugar toda la partida. Con el juego andando en una
// partida de Invasión:
// - Riftwalker > Invasión > Ir a la oleada 10: saca a los enemigos que quedan y arranca la oleada 10 (la del jefe).
// - Riftwalker > Invasión > Matar a todos los invasores: mata a los que están vivos, como si les hubiera disparado
//   (suman sus puntos). La oleada se supera cuando ya aparecieron todos, así que puede hacer falta usarlo varias veces.
// - Riftwalker > Invasión > Cargar el puesto: deja cargado el puesto de suministros que está en el mapa (US 292).
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

    [MenuItem("Riftwalker/Invasión/Cargar el puesto")]
    private static void CargarElPuesto()
    {
        PuestoDeSuministros puesto = PuestoDeSuministros.Actual;
        if (puesto == null || !puesto.Hay) { Debug.Log("Invasión de prueba: no hay un puesto en el mapa (cae desde la oleada 2)."); return; }
        puesto.CargarDePrueba();
        Debug.Log($"Invasión de prueba: puesto de {puesto.NombreDelLugar} cargado.");
    }

    [MenuItem("Riftwalker/Invasión/Ir a la oleada 10", true)]
    [MenuItem("Riftwalker/Invasión/Matar a todos los invasores", true)]
    [MenuItem("Riftwalker/Invasión/Cargar el puesto", true)]
    private static bool SePuede() =>
        Application.isPlaying && PartidaDeInvasion.Actual != null && PartidaDeInvasion.Actual.GetComponent<OleadasDeInvasion>() != null;
}
