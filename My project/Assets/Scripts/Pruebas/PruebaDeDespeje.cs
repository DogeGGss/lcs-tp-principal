using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// US 333 · Despeje, solo para probarla en el editor (Riftwalker > Invasión > Probar Despeje). Con el puesto en el mapa
// todavía sin cargar (desde la oleada 3: Riftwalker > Invasión > Ir a la oleada 3):
// - te da Despeje si no la tenés;
// - arma 6 invasores adentro del anillo (4 comunes y los dos que vuelan, el tirador y el francotirador), que salen a
//   buscarte como los de la oleada;
// - un segundo después carga el puesto, así sale la burbuja;
// - cuando terminan los empujones, deja en el piso el anillo del puesto y, por cada invasor, una línea de dónde estaba a
//   dónde quedó con los metros que lo empujó (también sale en la Consola). Se borra a los 12 s.
// En una build no existe.
public class PruebaDeDespeje : MonoBehaviour
{
#if UNITY_EDITOR
    private const float Espera = 1f;      // s antes de cargar el puesto (para ver a los invasores adentro)
    private const float Medir = 1.1f;     // s después de cargar: la burbuja ya llegó al borde y terminaron los empujones
    private const float Duracion = 12f;   // s que quedan las marcas
    private static readonly string[] Tipos = { "Comun", "Comun", "Tirador", "Comun", "Francotirador", "Comun" };
    private static readonly Color ColorAnillo = new Color(0.49f, 0.88f, 0.35f);
    private static readonly Color ColorLinea = new Color(1f, 0.85f, 0.2f);

    public static void Arrancar()
    {
        PuestoDeSuministros puesto = PuestoDeSuministros.Actual;
        if (puesto == null || !puesto.Hay)
        {
            Debug.Log("Probar Despeje: no hay un puesto en el mapa. Usá Riftwalker > Invasión > Ir a la oleada 3.");
            return;
        }
        if (puesto.Cargado)
        {
            Debug.Log("Probar Despeje: el puesto ya está cargado. Usá Riftwalker > Invasión > Ir a la oleada 3 para que caiga uno nuevo.");
            return;
        }
        PruebaDeDespeje prueba = new GameObject("Prueba de Despeje").AddComponent<PruebaDeDespeje>();
        prueba.StartCoroutine(prueba.Correr(puesto));
    }

    private IEnumerator Correr(PuestoDeSuministros puesto)
    {
        if (EleccionDeInvasion.Veces("Despeje") <= 0 && EleccionDeInvasion.Actual != null)
            foreach (CartaDeInvasion c in CartaDeInvasion.Todas)
                if (c.nombre == "Despeje") { EleccionDeInvasion.Actual.Dar(c); break; }

        Vector3 centro = puesto.Lugar;
        float radio = puesto.Radio;
        var armados = new List<Npc>();
        for (int i = 0; i < Tipos.Length; i++)
        {
            TipoDeNpc tipo = TipoDeNpc.Buscar(Tipos[i]);
            float angulo = i * 360f / Tipos.Length;
            float distancia = Mathf.Lerp(radio * 0.25f, radio * 0.8f, i / (float)(Tipos.Length - 1));
            Vector3 lugar = centro + Quaternion.Euler(0f, angulo, 0f) * Vector3.forward * distancia;
            if (NavMesh.SamplePosition(lugar, out NavMeshHit piso, 3f, NavMesh.AllAreas)) lugar = piso.position;
            Vector3 mira = centro - lugar;
            mira.y = 0f;
            Npc npc = Npc.Crear(tipo, lugar, Quaternion.LookRotation(mira.sqrMagnitude > 0.01f ? mira : Vector3.forward));
            if (npc != null) armados.Add(npc);
        }
        Debug.Log($"Probar Despeje: {armados.Count} invasores en el anillo. En {Espera:0} s se carga el puesto.");

        yield return new WaitForSeconds(Espera);
        var antes = new Dictionary<Npc, Vector3>();
        foreach (Npc npc in armados) if (npc != null && npc.Vivo) antes[npc] = npc.transform.position;
        puesto.CargarDePrueba();

        yield return new WaitForSeconds(Medir);
        GameObject grupo = MarcasDePrueba.Grupo("Prueba de Despeje", Duracion);
        Vector3 suelo = MarcasDePrueba.Piso(centro) + Vector3.up * 0.05f;
        MarcasDePrueba.Circulo(grupo, suelo, radio, 0.08f, ColorAnillo);
        MarcasDePrueba.Cartel(grupo, suelo + new Vector3(radio, 0.3f, 0f), $"anillo {radio:0} m", 0.3f, ColorAnillo);
        foreach (KeyValuePair<Npc, Vector3> e in antes)
        {
            if (e.Key == null) continue;
            Vector3 desde = MarcasDePrueba.Piso(e.Value) + Vector3.up * 0.1f;
            Vector3 hasta = MarcasDePrueba.Piso(e.Key.transform.position) + Vector3.up * 0.1f;
            Vector3 d = hasta - desde;
            d.y = 0f;
            float metros = d.magnitude;
            MarcasDePrueba.Linea(grupo, desde, hasta, 0.06f, ColorLinea);
            MarcasDePrueba.Cartel(grupo, hasta + Vector3.up * 2.2f, $"{metros:0.0} m", 0.35f, ColorLinea);
            Debug.Log($"Probar Despeje: {e.Key.name} empujado {metros:0.0} m.");
        }
        Destroy(gameObject);
    }
#endif
}
