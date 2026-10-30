using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// F08 (Modo Invasión), US 187, CA2: crea 3 puntos de aparición provisorios en la escena abierta, para moverlos a mano
// hasta definir dónde van. Quedan dentro de "Puntos de aparición (Invasión)", alrededor de donde mira la vista de
// escena, apoyados en el piso. Hay que guardar la escena después.
public static class PuntosDeAparicionEditor
{
    private const string Raiz = "Puntos de aparición (Invasión)";

    [MenuItem("Riftwalker/Invasión/Crear 3 puntos de aparición")]
    private static void Crear()
    {
        GameObject raiz = GameObject.Find(Raiz);
        if (raiz == null)
        {
            raiz = new GameObject(Raiz);
            Undo.RegisterCreatedObjectUndo(raiz, "Puntos de aparición");
        }
        Vector3 centro = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        var nuevos = new Object[3];
        for (int i = 0; i < 3; i++)
        {
            float a = i * Mathf.PI * 2f / 3f;
            Vector3 p = centro + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 12f;
            if (Physics.Raycast(p + Vector3.up * 50f, Vector3.down, out RaycastHit piso, 200f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                p = piso.point;
            var go = new GameObject($"Punto de aparición {raiz.transform.childCount + 1}");
            Undo.RegisterCreatedObjectUndo(go, "Puntos de aparición");
            go.transform.SetParent(raiz.transform, true);
            go.transform.position = p;
            go.AddComponent<AparicionDeEnemigos>();
            nuevos[i] = go;
        }
        Selection.objects = nuevos;
        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Debug.Log("[Invasión] 3 puntos de aparición creados alrededor de la vista de escena. Movelos adonde quieras y guardá la escena.");
    }
}
