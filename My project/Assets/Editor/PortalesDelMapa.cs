using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Mapa 1: conecta los portales de la escena abierta (PortalDeTeletransporte). Los puntos de salida son los objetos
// que se llaman "TP_portal..." y van de a pares, por nombre: TP_portal con TP_portal (1), TP_portal (2) con
// TP_portal (3)... Cada punto es la salida de su portal (el portal más cercano a él). En cada portal se agrega un hijo
// "Pasaje (teletransporte)" con un trigger que tapa el remolino: el que entra por el portal de un punto sale por el
// punto del otro. Se puede correr de nuevo (rehace los pasajes). Después, guardar la escena.
public static class PortalesDelMapa
{
    private const string Pasaje = "Pasaje (teletransporte)";
    private const float Cerca = 8f; // m: lo más lejos que puede estar el punto de su portal

    [MenuItem("Riftwalker/Mapas/Conectar portales")]
    public static void Conectar()
    {
        var escena = EditorSceneManager.GetActiveScene();
        var todos = escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        List<Transform> puntos = todos.Where(t => t.name.StartsWith("TP_portal")).OrderBy(t => t.name, System.StringComparer.Ordinal).ToList();
        if (puntos.Count < 2)
        {
            EditorUtility.DisplayDialog("Conectar portales", "No encontré al menos dos objetos \"TP_portal\" en la escena abierta.", "OK");
            return;
        }

        // El portal de cada punto: la raíz de prefab más cercana que se llame Portal...
        var portales = todos.Where(t => t.name.StartsWith("Portal") && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)).ToList();
        var portalDe = new Dictionary<Transform, Transform>();
        foreach (Transform p in puntos)
        {
            Transform mejor = null;
            float mejorDistancia = Cerca;
            foreach (Transform portal in portales)
            {
                Vector3 d = portal.position - p.position;
                d.y = 0f;
                if (d.magnitude < mejorDistancia) { mejorDistancia = d.magnitude; mejor = portal; }
            }
            portalDe[p] = mejor;
        }

        var hechos = new List<string>();
        var faltan = new List<string>();
        for (int i = 0; i + 1 < puntos.Count; i += 2)
        {
            Transform a = puntos[i], b = puntos[i + 1];
            if (portalDe[a] == null || portalDe[b] == null)
            {
                faltan.Add($"{a.name} / {b.name}: no hay un portal a menos de {Cerca} m de {(portalDe[a] == null ? a.name : b.name)}");
                continue;
            }
            Armar(portalDe[a], b, portalDe[b]);
            Armar(portalDe[b], a, portalDe[a]);
            hechos.Add($"{portalDe[a].name} -> {b.name}   y   {portalDe[b].name} -> {a.name}");
        }
        if (puntos.Count % 2 == 1) faltan.Add($"{puntos[puntos.Count - 1].name} quedó sin pareja");

        EditorSceneManager.MarkSceneDirty(escena);
        string texto = (hechos.Count > 0 ? "Conectados:\n" + string.Join("\n", hechos) : "No se conectó ninguno.")
                     + (faltan.Count > 0 ? "\n\nFaltan:\n" + string.Join("\n", faltan) : "")
                     + "\n\nGuardá la escena (Ctrl+S).";
        Debug.Log("Portales: " + texto.Replace("\n", "  "));
        EditorUtility.DisplayDialog("Conectar portales", texto, "OK");
    }

    // En el portal, el pasaje (trigger sobre el remolino) que lleva al destino.
    private static void Armar(Transform portal, Transform destino, Transform portalDeSalida)
    {
        Transform viejo = portal.Find(Pasaje);
        if (viejo != null) Undo.DestroyObjectImmediate(viejo.gameObject);

        var go = new GameObject(Pasaje);
        Undo.RegisterCreatedObjectUndo(go, "Conectar portales");
        go.layer = 2; // Ignore Raycast: las balas y las granadas no chocan con el pasaje
        go.transform.SetParent(portal, false);

        // El remolino: lo que usa los shaders de portal (VortexPortal o LiquidFlow); si no, todo el portal.
        var remolino = portal.GetComponentsInChildren<Renderer>(true)
            .Where(r => r.sharedMaterials.Any(m => m != null && m.shader != null &&
                        (m.shader.name.Contains("VortexPortal") || m.shader.name.Contains("LiquidFlow")))).ToList();
        if (remolino.Count == 0) remolino = portal.GetComponentsInChildren<Renderer>(true).ToList();

        // Su caja, en el espacio del portal.
        bool hay = false;
        Bounds caja = new Bounds();
        foreach (Renderer r in remolino)
        {
            Bounds b = r.bounds;
            for (int k = 0; k < 8; k++)
            {
                Vector3 esquina = b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                Vector3 local = portal.InverseTransformPoint(esquina);
                if (!hay) { caja = new Bounds(local, Vector3.zero); hay = true; }
                else caja.Encapsulate(local);
            }
        }
        if (!hay) caja = new Bounds(Vector3.up, new Vector3(2f, 2f, 1f));
        // Del lado más fino, por lo menos 1 m: así no se pasa de largo corriendo.
        Vector3 tam = caja.size;
        int fino = tam.x <= tam.y && tam.x <= tam.z ? 0 : tam.y <= tam.z ? 1 : 2;
        tam[fino] = Mathf.Max(tam[fino], 1f);

        var area = go.AddComponent<BoxCollider>();
        area.isTrigger = true;
        area.center = caja.center;
        area.size = tam;
        var pasaje = go.AddComponent<PortalDeTeletransporte>();
        pasaje.destino = destino;
        pasaje.portalDeSalida = portalDeSalida;
    }
}
