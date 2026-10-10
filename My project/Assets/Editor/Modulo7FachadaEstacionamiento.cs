using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

// Intervención localizada: fotos 21, 23, 24, 26 (derecha), 29 y 32.
// Coordenadas del ala existente; no modifica las otras fachadas del módulo 7.
public static class Modulo7FachadaEstacionamiento
{
    const float X = 375.816f, Z = 217.708f, Largo = 79.224f, Ancho = 22.881f;
    const float Puerta = 8.5f, PuertaAncho = 3.8f;
    const string MeshPath = "Assets/Meshes/Uni/Modulo7_FachadaEstacionamiento.asset";
    static Transform root;
    static readonly Dictionary<string, Batch> batches = new Dictionary<string, Batch>();

    class Batch
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<int> t = new List<int>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public Material mat;
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = v.Count;
            v.AddRange(new[] { a, b, c, d });
            float w = Vector3.Distance(a, b), h = Vector3.Distance(b, c);
            uv.AddRange(new[] { Vector2.zero, new Vector2(w, 0), new Vector2(w, h), new Vector2(0, h) });
            t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
    }

    static Batch Get(string material)
    {
        if (!batches.TryGetValue(material, out var b))
        {
            b = new Batch { mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Uni/Uni_" + material + ".mat") };
            if (b.mat == null) throw new InvalidOperationException("Falta material " + material);
            batches.Add(material, b);
        }
        return b;
    }

    // u sigue el sendero; d es distancia hacia afuera de la fachada.
    static Vector3 P(float u, float y, float d, bool lateral)
    {
        return lateral ? new Vector3(X + u, y, Z - d) : new Vector3(X - d, y, Z + u);
    }

    static void Box(string mat, float u, float y, float d, float w, float h, float depth, bool lateral = false, bool collision = false)
    {
        if (w < .005f || h < .005f || depth < .005f) return;
        float a = u - w / 2, b = u + w / 2, lo = y - h / 2, hi = y + h / 2;
        float near = d + depth / 2, far = d - depth / 2;
        Vector3 p0 = P(a, lo, near, lateral), p1 = P(b, lo, near, lateral), p2 = P(b, hi, near, lateral), p3 = P(a, hi, near, lateral);
        Vector3 q0 = P(a, lo, far, lateral), q1 = P(b, lo, far, lateral), q2 = P(b, hi, far, lateral), q3 = P(a, hi, far, lateral);
        var batch = Get(mat);
        // Las dos fachadas tienen bases de orientación diferentes.
        Action<Vector3, Vector3, Vector3, Vector3> face = (a0, b0, c0, d0) =>
        {
            if (lateral) batch.Quad(d0, c0, b0, a0); else batch.Quad(a0, b0, c0, d0);
        };
        face(p0, p1, p2, p3); face(q1, q0, q3, q2);
        face(q0, p0, p3, q3); face(p1, q1, q2, p2);
        face(p3, p2, q2, q3); face(q0, q1, p1, p0);
        if (collision)
        {
            var collider = root.gameObject.AddComponent<BoxCollider>();
            collider.center = P(u, y, d, lateral);
            collider.size = lateral ? new Vector3(w, h, depth) : new Vector3(depth, h, w);
        }
    }

    struct Opening
    {
        public float a, b, lo, hi;
        public Opening(float center, float width, float bottom, float top) { a = center - width / 2; b = center + width / 2; lo = bottom; hi = top; }
    }

    static void Wall(float length, List<Opening> openings, bool lateral)
    {
        var us = new List<float> { 0, length };
        var ys = new List<float> { 0, 9.0f };
        foreach (var o in openings) { us.Add(o.a); us.Add(o.b); ys.Add(o.lo); ys.Add(o.hi); }
        us = us.Distinct().OrderBy(v => v).ToList(); ys = ys.Distinct().OrderBy(v => v).ToList();
        for (int i = 1; i < us.Count; i++)
        for (int j = 1; j < ys.Count; j++)
        {
            float u = (us[i - 1] + us[i]) / 2, y = (ys[j - 1] + ys[j]) / 2;
            if (openings.Any(o => u > o.a && u < o.b && y > o.lo && y < o.hi)) continue;
            Box("Ladrillo", u, y, -.15f, us[i] - us[i - 1], ys[j] - ys[j - 1], .3f, lateral, true);
        }
    }

    static void Window(float u, float bottom, float width, float height, bool lateral, bool upper, bool awning)
    {
        float y = bottom + height / 2;
        Box("Vidrio", u, y, -.14f, width - .12f, height - .12f, .04f, lateral, true);
        Box("PinturaBlanca", u - width / 2 + .04f, y, -.02f, .08f, height, .22f, lateral);
        Box("PinturaBlanca", u + width / 2 - .04f, y, -.02f, .08f, height, .22f, lateral);
        Box("PinturaBlanca", u, bottom + .04f, -.02f, width, .08f, .22f, lateral);
        Box("PinturaBlanca", u, bottom + height - .04f, -.02f, width, .08f, .22f, lateral);
        Box("HormigonBeige", u, bottom + height + .09f, .02f, width + .18f, .14f, .34f, lateral);
        if (upper)
        {
            Box("PinturaBlanca", u, y, -.01f, .045f, height - .12f, .07f, lateral);
            Box("PinturaBlanca", u, bottom + height - .30f, -.01f, width, .045f, .07f, lateral);
        }
        else
        {
            Box("PinturaBlanca", u, bottom + height + .24f, .08f, width + .18f, .08f, .10f, lateral);
            if (awning)
            {
                // Toldos claros que sobresalen del muro, sobre algunas ventanas.
                var b = Get("Toldo");
                var p0 = P(u - width / 2 - .12f, bottom + height + .17f, .08f, lateral);
                var p1 = P(u + width / 2 + .12f, bottom + height + .17f, .08f, lateral);
                var p2 = P(u + width / 2 + .12f, bottom + height - .16f, .90f, lateral);
                var p3 = P(u - width / 2 - .12f, bottom + height - .16f, .90f, lateral);
                b.Quad(p0, p1, p2, p3); b.Quad(p3, p2, p1, p0);
            }
        }
    }

    static bool Selected(Vector3 v)
    {
        return (v.x < X + 1.0f && v.z >= Z - 1.0f && v.z <= Z + Largo + .75f)
            || (v.z < Z + .7f && v.z > Z - 2.0f && v.x >= X - 1.5f && v.x <= X + Ancho + .1f);
    }

    static void TrimExisting(GameObject building, Mesh asset)
    {
        foreach (var mf in building.transform.Find("Fachada").GetComponentsInChildren<MeshFilter>())
        {
            var src = mf.sharedMesh;
            var clone = UnityEngine.Object.Instantiate(src);
            clone.name = "Conservado_" + mf.name;
            var vertices = src.vertices;
            int removed = 0;
            for (int s = 0; s < src.subMeshCount; s++)
            {
                var triangles = src.GetTriangles(s); var kept = new List<int>();
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = mf.transform.TransformPoint(vertices[triangles[i]]), b = mf.transform.TransformPoint(vertices[triangles[i + 1]]), c = mf.transform.TransformPoint(vertices[triangles[i + 2]]);
                    if (Selected((a + b + c) / 3)) { removed++; continue; }
                    kept.Add(triangles[i]); kept.Add(triangles[i + 1]); kept.Add(triangles[i + 2]);
                }
                clone.SetTriangles(kept, s);
            }
            if (removed == 0) { UnityEngine.Object.DestroyImmediate(clone); continue; }
            AssetDatabase.AddObjectToAsset(clone, asset);
            Undo.RecordObject(mf, "Sustituir fachada blanca"); mf.sharedMesh = clone;
            var mc = mf.GetComponent<MeshCollider>();
            if (mc != null) { Undo.RecordObject(mc, "Actualizar colisión"); mc.sharedMesh = clone; }
        }
        var pb = building.transform.Find("Estructura").GetComponent<ProBuilderMesh>();
        Undo.RegisterCompleteObjectUndo(pb, "Abrir muro existente");
        var faces = pb.faces.Where(f =>
        {
            var vs = f.distinctIndexes.Select(i => pb.transform.TransformPoint(pb.positions[i])).ToArray();
            float minY = vs.Min(v => v.y), maxY = vs.Max(v => v.y);
            return maxY - minY > 1 && (vs.All(v => Mathf.Abs(v.x - X) < .03f) || vs.All(v => Mathf.Abs(v.z - Z) < .03f && v.x <= X + Ancho + .03f));
        }).ToArray();
        pb.DeleteFaces(faces); pb.ToMesh(); pb.Refresh();
        EditorUtility.SetDirty(pb);
        var structureCollider = pb.GetComponent<MeshCollider>();
        Undo.RecordObject(structureCollider, "Actualizar estructura"); structureCollider.sharedMesh = pb.GetComponent<MeshFilter>().sharedMesh;
        foreach (var c in building.transform.Find("Colisiones").GetComponentsInChildren<Collider>())
        {
            if (Selected(c.bounds.center)) { Undo.RecordObject(c, "Retirar colisión de columnas antiguas"); c.enabled = false; }
        }
    }

    static void Entrance()
    {
        float bottom = .10f, height = 2.65f;
        // Jambas y techo del hueco; el acceso retrocede 1,15 m desde el ladrillo.
        Box("Ladrillo", Puerta - PuertaAncho / 2 + .10f, 1.45f, -.62f, .20f, 2.9f, 1.24f, false, true);
        Box("Ladrillo", Puerta + PuertaAncho / 2 - .10f, 1.45f, -.62f, .20f, 2.9f, 1.24f, false, true);
        Box("HormigonBeige", Puerta, 2.89f, -.62f, PuertaAncho, .20f, 1.24f);
        Box("HormigonViejo", Puerta, .06f, -.40f, PuertaAncho, .12f, 2.10f, false, true);
        float usable = PuertaAncho - .4f, leaf = usable / 4;
        for (int i = 0; i < 4; i++)
        {
            float u = Puerta - usable / 2 + leaf * (i + .5f);
            // Paños opacos y vidrio superior, como los dos pares de puertas de las fotos.
            Box("PinturaBlanca", u, bottom + .56f, -1.15f, leaf - .025f, 1.12f, .07f, false, true);
            Box("PinturaBlanca", u - leaf / 2 + .045f, 1.62f, -1.15f, .09f, 1.08f, .07f);
            Box("PinturaBlanca", u + leaf / 2 - .045f, 1.62f, -1.15f, .09f, 1.08f, .07f);
            Box("PinturaBlanca", u, 2.16f, -1.15f, leaf, .10f, .07f);
            Box("Vidrio", u, 1.65f, -1.17f, leaf - .18f, .92f, .035f, false, true);
            Box("MetalNegro", u + leaf * .25f, 1.0f, -1.09f, .04f, .18f, .05f);
            Box("Vidrio", u, 2.47f, -1.17f, leaf - .1f, .34f, .035f);
        }
        Box("PinturaBlanca", Puerta, 1.42f, -1.10f, .14f, height, .13f);
        Box("PinturaBlanca", Puerta, 2.69f, -1.13f, usable, .12f, .12f);
        float signU = Puerta + PuertaAncho / 2 + .65f;
        Box("CartelVerde", signU, 2.76f, .04f, .54f, 1.10f, .08f);
        Box("CartelBlanco", signU, 3.0f, .10f, .64f, .47f, .08f);
        var label = new GameObject("Cartel 7a"); Undo.RegisterCreatedObjectUndo(label, "Cartel 7a"); label.transform.SetParent(root, false);
        label.transform.position = P(signU, 3.0f, .15f, false); label.transform.rotation = Quaternion.Euler(0, 90, 0);
        var text = label.AddComponent<TMPro.TextMeshPro>(); text.text = "7a"; text.fontSize = 3.1f;
        text.alignment = TMPro.TextAlignmentOptions.Center; text.color = new Color(.14f, .38f, .14f);
        text.rectTransform.sizeDelta = new Vector2(.60f, .44f);
    }

    static void BuildSide(float length, int count, bool lateral)
    {
        var openings = new List<Opening>();
        for (int i = 0; i < count; i++)
        {
            float u = (i + .5f) * length / count;
            bool atDoor = !lateral && Mathf.Abs(u - Puerta) < PuertaAncho / 2 + .85f;
            if (!atDoor) { openings.Add(new Opening(u, 1.32f, .90f, 2.35f)); Window(u, .90f, 1.32f, 1.45f, lateral, false, i % 6 == 2 || i % 7 == 4); }
            openings.Add(new Opening(u, 1.32f, 4.03f, 5.43f)); Window(u, 4.03f, 1.32f, 1.4f, lateral, false, false);
            openings.Add(new Opening(u, length / count - .45f, 7.02f, 8.32f)); Window(u, 7.02f, length / count - .45f, 1.3f, lateral, true, false);
            if (i > 0) Box("MetalNegro", i * length / count, 4.38f, .04f, .075f, 8.65f, .10f, lateral);
        }
        if (!lateral) openings.Add(new Opening(Puerta, PuertaAncho, 0, 2.99f));
        Wall(length, openings, lateral);
        Box("HormigonBeige", length / 2, 8.91f, .015f, length, .18f, .35f, lateral);
    }

    static void CornerDetails()
    {
        // Equipo exterior visible en la foto 24, en el retorno de la esquina.
        Box("PinturaBlanca", 11.2f, 3.35f, .26f, .95f, 1.15f, .42f, true);
        for (int i = 0; i < 11; i++)
            Box("MetalNegro", 11.2f, 2.89f + i * .09f, .49f, .74f, .035f, .018f, true);
        Box("PinturaBlanca", 10.62f, 2.92f, .22f, .05f, 1.65f, .06f, true);
        // Bajadas de agua y cañerías contiguas al acceso (fotos 29 y 32).
        Box("PinturaBlanca", Puerta + PuertaAncho / 2 + .05f, 4.45f, .10f, .10f, 8.9f, .11f);
        Box("PinturaBlanca", Puerta - PuertaAncho / 2 + .32f, 1.43f, -.30f, .08f, 2.7f, .09f);
        Box("PinturaBlanca", Puerta - PuertaAncho / 2 + .45f, 1.43f, -.30f, .08f, 2.7f, .09f);
    }

    [MenuItem("Riftwalker/Universidad/Corregir fachada 7a del estacionamiento")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/universidadMAPA.unity" || EditorApplication.isPlaying) throw new InvalidOperationException("Abrir universidadMAPA fuera de Play.");
        var building = GameObject.Find("Modulo7");
        if (building == null) throw new InvalidOperationException("No se encontró Modulo7.");
        if (building.transform.Find("FachadaEstacionamiento_7a") != null || AssetDatabase.LoadMainAssetAtPath(MeshPath) != null) throw new InvalidOperationException("La intervención ya existe; no se aplicará dos veces.");
        foreach (string m in new[] { "Ladrillo", "Vidrio", "PinturaBlanca", "HormigonBeige", "Toldo", "MetalNegro", "HormigonViejo", "CartelVerde", "CartelBlanco" }) Get(m);
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Corregir fachada 7a del estacionamiento");
        var container = new Mesh { name = "Modulo7_FachadaEstacionamiento" }; AssetDatabase.CreateAsset(container, MeshPath);
        var go = new GameObject("FachadaEstacionamiento_7a"); Undo.RegisterCreatedObjectUndo(go, "Crear fachada 7a");
        go.transform.SetParent(building.transform, false); root = go.transform;
        TrimExisting(building, container);
        BuildSide(Largo, 28, false); BuildSide(Ancho, 8, true); Entrance(); CornerDetails();
        foreach (var kv in batches)
        {
            var b = kv.Value; if (b.v.Count == 0) continue;
            var mesh = new Mesh { name = "7a_" + kv.Key, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(b.v); mesh.SetUVs(0, b.uv); mesh.SetTriangles(b.t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.AddObjectToAsset(mesh, container);
            var child = new GameObject(kv.Key); Undo.RegisterCreatedObjectUndo(child, "Geometría de fachada"); child.transform.SetParent(root, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh; child.AddComponent<MeshRenderer>().sharedMaterial = b.mat;
        }
        EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets(); Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = go;
        batches.Clear();
        Debug.Log("Fachada 7a corregida. La escena queda pendiente de revisión visual y guardado.");
    }

    public static void Capture(string path, Vector3 position, Vector3 target, bool orthographic = false, float size = 55)
    {
        var go = new GameObject("Vista temporal 7a") { hideFlags = HideFlags.HideAndDontSave };
        var camera = go.AddComponent<Camera>(); camera.transform.position = position; camera.transform.LookAt(target);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.65f, .72f, .78f);
        camera.fieldOfView = 63; camera.nearClipPlane = .1f; camera.farClipPlane = 600;
        camera.orthographic = orthographic; camera.orthographicSize = size;
        var lightGo = new GameObject("Luz temporal 7a") { hideFlags = HideFlags.HideAndDontSave };
        var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 2.5f; light.color = Color.white;
        lightGo.transform.rotation = Quaternion.Euler(45, 120, 0);
        var oldAmbient = RenderSettings.ambientMode; var oldColor = RenderSettings.ambientLight; var oldIntensity = RenderSettings.ambientIntensity;
        var rt = new RenderTexture(1500, 950, 24); var oldRT = RenderTexture.active;
        try
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.65f, .65f, .65f); RenderSettings.ambientIntensity = 1;
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
            System.IO.File.WriteAllBytes(path, image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
        }
        finally
        {
            RenderTexture.active = oldRT; camera.targetTexture = null; rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(lightGo);
            RenderSettings.ambientMode = oldAmbient; RenderSettings.ambientLight = oldColor; RenderSettings.ambientIntensity = oldIntensity;
        }
    }
}
