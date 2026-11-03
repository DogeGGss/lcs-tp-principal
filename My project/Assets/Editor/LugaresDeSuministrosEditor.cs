using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// F08 (Modo Invasión), US 292: arma los lugares donde puede caer el puesto de suministros en la escena abierta, uno por
// módulo (y la biblioteca y el bar), a partir de los nombres de los objetos del mapa. Quedan dentro de "Lugares de
// suministros (Invasión)", apoyados en el NavMesh, para moverlos o renombrarlos a mano. Hay que guardar la escena
// después. Si la escena no tiene ninguno, el puesto los arma solo al empezar con la misma regla.
public static class LugaresDeSuministrosEditor
{
    private const string Raiz = "Lugares de suministros (Invasión)";

    [MenuItem("Riftwalker/Invasión/Crear lugares de suministros")]
    private static void Crear()
    {
        var lugares = LugarDeSuministros.Automaticos();
        if (lugares.Count == 0)
        {
            Debug.LogWarning("[Invasión] No encontré módulos en la escena abierta (objetos que se llamen Modulo...).");
            return;
        }
        GameObject raiz = GameObject.Find(Raiz);
        if (raiz == null)
        {
            raiz = new GameObject(Raiz);
            Undo.RegisterCreatedObjectUndo(raiz, "Lugares de suministros");
        }
        int creados = 0;
        foreach (var (nombre, lugar) in lugares)
        {
            bool yaEsta = false;
            foreach (LugarDeSuministros l in raiz.GetComponentsInChildren<LugarDeSuministros>(true)) if (l.nombre == nombre) yaEsta = true;
            if (yaEsta) continue;
            Vector3 p = NavMesh.SamplePosition(lugar, out NavMeshHit hit, 20f, NavMesh.AllAreas) ? hit.position : lugar;
            var go = new GameObject("Suministros: " + nombre);
            Undo.RegisterCreatedObjectUndo(go, "Lugares de suministros");
            go.transform.SetParent(raiz.transform, true);
            go.transform.position = p;
            go.AddComponent<LugarDeSuministros>().nombre = nombre;
            creados++;
        }
        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Debug.Log($"[Invasión] {creados} lugares de suministros creados en \"{Raiz}\". Revisalos, movelos si hace falta y guardá la escena.");
    }
}
