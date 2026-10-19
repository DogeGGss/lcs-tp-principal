using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Mapas · Colisiones de las piezas del kit Sci-fi de Creepy Cat. A diferencia de los otros kits (Dzeruza, con "Generate
// Colliders" en el modelo), estas piezas no traen colisión con la forma del modelo: algunas no traen nada (escalones,
// tiras de piso) y las demás traen cajas "Collider_01" puestas a mano que no coinciden con la pieza.
// El menú Riftwalker > Mapas: poner colisiones a Creepy Cat trabaja sobre la escena abierta: a cada pieza del kit que no
// tenga colisión en su modelo le agrega una MeshCollider con su malla
// (igual que Generate Colliders). Las cajas que trae el kit quedan como están, para sacarlas a mano donde molesten.
// Queda como cambio de la escena (no toca los prefabs del kit ni los otros mapas): se ve en el Inspector, se puede
// retocar pieza por pieza y se deshace con Ctrl+Z. Después hay que guardar la escena.
public static class ColisionesCreepyCat
{
    private const string Menu = "Riftwalker/Mapas: poner colisiones a Creepy Cat";
    private const string Kit = "/Creepy_Cat/";

    [MenuItem(Menu)]
    private static void Poner()
    {
        var escena = EditorSceneManager.GetActiveScene();
        int grupo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Colisiones de Creepy Cat");

        int piezas = 0;
        foreach (GameObject raiz in escena.GetRootGameObjects())
            foreach (MeshFilter filtro in raiz.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh malla = filtro.sharedMesh;
                if (malla == null || !AssetDatabase.GetAssetPath(malla).Contains(Kit)) continue;
                if (filtro.GetComponent<MeshRenderer>() == null) continue;
                if (filtro.GetComponent<Collider>() != null) continue; // ya tiene (por ejemplo los escalones)

                MeshCollider colision = Undo.AddComponent<MeshCollider>(filtro.gameObject);
                colision.sharedMesh = malla;
                piezas++;
            }

        Undo.CollapseUndoOperations(grupo);
        if (piezas > 0) EditorSceneManager.MarkSceneDirty(escena);
        string texto = $"{piezas} piezas de Creepy Cat con colisión de su forma en {escena.name}.\n\nRevisalo y guardá la escena (Ctrl+S). Se deshace con Ctrl+Z.";
        Debug.Log("[Colisiones] " + texto);
        EditorUtility.DisplayDialog("Colisiones de Creepy Cat", texto, "OK");
    }
}
