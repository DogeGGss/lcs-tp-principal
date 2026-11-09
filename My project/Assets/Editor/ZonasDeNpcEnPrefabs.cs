using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// F11 (NPCs), US 038: pasa las zonas de impacto de los NPCs (ZonasDeNpc) a los prefabs de sus modelos, así se pueden
// ajustar a mano. Riftwalker > NPCs > Pasar zonas de impacto a los prefabs arma las zonas de siempre dentro de cada
// prefab que usan los tipos (Resources/NPCs), colgadas de los huesos. Después, en el prefab (doble clic):
// - para ajustar una zona, se cambia su collider (Radius, Height, Center, o Edit Collider en la escena);
// - para agregar una, se duplica una zona y se la cuelga del hueso que corresponda (la cola, la mandíbula...), y en
//   Hit Zone se elige si es cabeza, cuerpo o piernas. Tiene que ser trigger (Is Trigger).
// Con zonas en el prefab, el juego usa esas (ZonasDeNpc.Asegurar). Si un prefab ya tiene zonas, pregunta si
// rehacerlas (se pierden los ajustes).
public static class ZonasDeNpcEnPrefabs
{
    [MenuItem("Riftwalker/NPCs/Pasar zonas de impacto a los prefabs")]
    private static void Pasar()
    {
        var hechos = new List<string>();
        var vistos = new HashSet<string>();
        foreach (TipoDeNpc tipo in TipoDeNpc.Todos())
        {
            if (tipo.modelo == null) continue;
            string ruta = AssetDatabase.GetAssetPath(tipo.modelo);
            if (string.IsNullOrEmpty(ruta) || !vistos.Add(ruta)) continue;

            GameObject raiz = PrefabUtility.LoadPrefabContents(ruta);
            try
            {
                HitZone[] viejas = raiz.GetComponentsInChildren<HitZone>(true);
                if (viejas.Length > 0)
                {
                    bool rehacer = EditorUtility.DisplayDialog("Zonas de impacto",
                        $"{System.IO.Path.GetFileNameWithoutExtension(ruta)} ({tipo.nombre}) ya tiene {viejas.Length} zonas.\n" +
                        "¿Rehacerlas? Se pierden los ajustes que les hayas hecho.", "Rehacer", "Dejarlas");
                    if (!rehacer) { hechos.Add($"{tipo.nombre}: se dejaron sus {viejas.Length} zonas"); continue; }
                    foreach (HitZone z in viejas) Object.DestroyImmediate(z.gameObject);
                }
                int n = ZonasDeNpc.Armar(raiz.transform);
                PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
                hechos.Add(n > 0 ? $"{tipo.nombre}: {n} zonas en {System.IO.Path.GetFileName(ruta)}"
                                 : $"{tipo.nombre}: no se encontraron los huesos Bip01 (queda todo como cuerpo)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(raiz);
            }
        }
        string texto = hechos.Count > 0 ? string.Join("\n", hechos) : "No hay tipos de NPC con modelo en Resources/NPCs.";
        Debug.Log("Zonas de impacto de los NPCs: " + texto.Replace("\n", " · "));
        EditorUtility.DisplayDialog("Zonas de impacto", texto + "\n\nAhora se pueden ajustar en cada prefab (doble clic).", "OK");
    }
}
