using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Mapas · Rampas invisibles en las escaleras del kit Sci-fi de Creepy Cat (US 240, CA4). Los escalones del kit son
// tablas sueltas de 50 cm de alto cada una: más que lo que el jugador sube solo (Step Offset 0,45 m), así que se
// trababa. El menú Riftwalker > Mapas: rampas en las escaleras de Creepy Cat trabaja sobre la escena abierta: a cada
// tramo de escalones (los de una misma casa y mirando para el mismo lado) le pone encima una rampa invisible que
// apoya en la punta del último escalón y baja a 38° hasta el piso (el jugador sube rampas de hasta 45°). La rampa
// está en la capa Ignore Raycast y tiene ParedSoloJugadores: los disparos y las granadas siguen chocando con los
// escalones de verdad. Se puede volver a correr: reemplaza las rampas que había puesto. Se deshace con Ctrl+Z.
public static class RampasEscaleras
{
    private const string Menu = "Riftwalker/Mapas: rampas en las escaleras de Creepy Cat";
    private const string Kit = "/Creepy_Cat/";
    private const string Escalon = "Stairway_Step";
    private const string NombreRampa = "Rampa (escalera)";
    private const float Angulo = 38f;    // grados de la rampa
    private const float Grosor = 0.1f;   // m
    private const float Margen = 0.03f;  // m por encima de las puntas de los escalones

    [MenuItem(Menu)]
    private static void Poner()
    {
        var escena = EditorSceneManager.GetActiveScene();
        int grupo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Rampas en las escaleras");

        // Las rampas que había puesto antes se reemplazan.
        int sacadas = 0;
        foreach (GameObject raiz in escena.GetRootGameObjects())
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true).ToArray())
                if (t != null && t.name == NombreRampa) { Undo.DestroyObjectImmediate(t.gameObject); sacadas++; }
        Physics.SyncTransforms();

        // Escalones del kit, agrupados por tramo: la pieza (prefab) va dentro de una casa y mira para un lado.
        var tramos = new Dictionary<(Transform, int), List<Transform>>();
        foreach (GameObject raiz in escena.GetRootGameObjects())
            foreach (MeshFilter filtro in raiz.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filtro.sharedMesh == null || !AssetDatabase.GetAssetPath(filtro.sharedMesh).Contains(Kit)) continue;
                GameObject pieza = PrefabUtility.GetOutermostPrefabInstanceRoot(filtro.gameObject) ?? filtro.gameObject;
                if (!pieza.name.StartsWith(Escalon)) continue;
                int giro = Mathf.RoundToInt(pieza.transform.eulerAngles.y / 5f) * 5 % 360;
                var clave = (pieza.transform.parent, giro);
                if (!tramos.TryGetValue(clave, out var lista)) tramos[clave] = lista = new List<Transform>();
                if (!lista.Contains(pieza.transform)) lista.Add(pieza.transform);
            }

        int puestas = 0;
        foreach (var tramo in tramos)
            if (tramo.Value.Count >= 2 && Rampa(tramo.Key.Item1, tramo.Value)) puestas++;

        Undo.CollapseUndoOperations(grupo);
        EditorSceneManager.MarkSceneDirty(escena);
        string texto = $"{puestas} rampas puestas en las escaleras de {escena.name}" + (sacadas > 0 ? $" (reemplazan a {sacadas})." : ".") +
                       "\n\nProbalas subiendo y bajando, y guardá la escena (Ctrl+S). Se deshace con Ctrl+Z.";
        Debug.Log("[Rampas] " + texto);
        EditorUtility.DisplayDialog("Rampas en las escaleras", texto, "OK");
    }

    private static bool Rampa(Transform casa, List<Transform> escalones)
    {
        escalones = escalones.OrderBy(e => e.position.y).ToList();
        Vector3 primero = escalones[0].position, ultimo = escalones[escalones.Count - 1].position;
        Vector3 adelante = Vector3.ProjectOnPlane(ultimo - primero, Vector3.up);
        if (adelante.sqrMagnitude < 0.01f) return false;
        adelante.Normalize();
        Vector3 costado = Vector3.Cross(Vector3.up, adelante);

        // Punta de cada escalón: el borde de adelante de su cara de arriba (con los vértices reales del modelo).
        float abajo = float.MaxValue, izq = float.MaxValue, der = float.MinValue;
        var puntas = new List<Vector2>(); // (avance, altura)
        foreach (Transform e in escalones)
        {
            var vertices = new List<Vector3>();
            foreach (MeshFilter f in e.GetComponentsInChildren<MeshFilter>(true))
                if (f.sharedMesh != null && f.GetComponent<MeshRenderer>() != null)
                    foreach (Vector3 v in f.sharedMesh.vertices) vertices.Add(f.transform.TransformPoint(v));
            if (vertices.Count == 0) continue;
            float arriba = vertices.Max(v => v.y);
            float punta = vertices.Where(v => v.y > arriba - 0.03f).Min(v => Vector3.Dot(v - primero, adelante));
            puntas.Add(new Vector2(punta, arriba));
            abajo = Mathf.Min(abajo, vertices.Min(v => v.y));
            foreach (Vector3 v in vertices)
            {
                float c = Vector3.Dot(v - primero, costado);
                izq = Mathf.Min(izq, c); der = Mathf.Max(der, c);
            }
        }
        if (puntas.Count < 2) return false;
        Vector2 arribaDeTodo = puntas[puntas.Count - 1];

        // Piso: lo que haya abajo de la punta del primer escalón (si no encuentra nada, la base del primer escalón).
        Vector3 bajoPrimero = primero + adelante * (puntas[0].x - 0.3f);
        float piso = abajo;
        if (Physics.Raycast(new Vector3(bajoPrimero.x, puntas[0].y + 0.1f, bajoPrimero.z), Vector3.down, out RaycastHit hit, 5f, ~(1 << 2), QueryTriggerInteraction.Ignore)
            && escalones.All(e => !hit.collider.transform.IsChildOf(e)))
            piso = hit.point.y;

        // La rampa apoya en la punta de arriba y baja a 38°; si alguna punta quedara por encima, se la sube.
        float pendiente = Mathf.Tan(Angulo * Mathf.Deg2Rad);
        float subida = 0f;
        foreach (Vector2 p in puntas)
            subida = Mathf.Max(subida, p.y - (arribaDeTodo.y - (arribaDeTodo.x - p.x) * pendiente));
        float alto = arribaDeTodo.y + subida + Margen;
        float largoPlano = (alto - piso) / pendiente;
        if (largoPlano <= 0.1f) return false;

        Vector3 fin = primero + adelante * arribaDeTodo.x + costado * ((izq + der) * 0.5f);
        fin.y = alto;
        Vector3 inicio = fin - adelante * largoPlano;
        inicio.y = piso;
        Vector3 subiendo = (fin - inicio).normalized;
        Vector3 normal = Vector3.Cross(subiendo, costado).normalized;
        if (normal.y < 0f) normal = -normal;

        var rampa = new GameObject(NombreRampa);
        Undo.RegisterCreatedObjectUndo(rampa, "Rampa");
        rampa.layer = 2; // Ignore Raycast: los disparos chocan con los escalones de verdad
        rampa.transform.SetParent(casa, false);
        rampa.transform.position = (inicio + fin) * 0.5f - normal * (Grosor * 0.5f);
        rampa.transform.rotation = Quaternion.LookRotation(subiendo, normal);
        Vector3 escala = rampa.transform.lossyScale;
        var caja = rampa.AddComponent<BoxCollider>();
        caja.size = new Vector3(
            Mathf.Max(0.1f, der - izq - 0.05f) / Mathf.Max(0.0001f, escala.x),
            Grosor / Mathf.Max(0.0001f, escala.y),
            Vector3.Distance(inicio, fin) / Mathf.Max(0.0001f, escala.z));
        rampa.AddComponent<ParedSoloJugadores>(); // las granadas rebotan en los escalones, no en la rampa
        return true;
    }
}
