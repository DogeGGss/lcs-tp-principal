using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Caminos de la uni que faltaban, sacados del plano del campus (Assets/Art/Reference/plano_ungs.png, el de
// RefImage_Aerial): x y z en metros del mundo. Son losas de vereda con colisión, un poco más bajas que los caminos
// que ya estaban (así, donde se pisan, se ve el de antes y no titilan). Saca los árboles que quedaban encima,
// corre faroles y muebles al borde y saca el pasto del terreno.
// Se puede volver a correr: borra lo que armó antes y lo arma de nuevo.
public static class CaminosUniversidad
{
    const string MeshPath = "Assets/Meshes/Uni/CaminosNuevos.asset";
    const string Raiz = "Ambiente_CaminosNuevos";
    const float Alto = 0.15f, Fondo = -0.1f;

    class Camino
    {
        public string nombre;
        public float ancho;
        public bool curva;
        public Vector2[] puntos;
        public Camino(string nombre, float ancho, bool curva, params float[] xz)
        {
            this.nombre = nombre; this.ancho = ancho; this.curva = curva;
            puntos = Enumerable.Range(0, xz.Length / 2).Select(i => new Vector2(xz[2 * i], xz[2 * i + 1])).ToArray();
        }
    }

    static Camino[] Caminos => new[]
    {
        // Vereda del Módulo 10, sobre Sarratea: corre por el frente y baja hacia el estacionamiento.
        new Camino("VeredaModulo10", 3.8f, false, 101f, 313.7f, 214f, 313.7f, 222f, 311.5f, 229f, 306.2f, 236f, 304.8f, 266f, 304.8f),
        new Camino("VeredaModulo10_Patio", 11f, false, 146.5f, 313f, 146.5f, 322f),
        new Camino("VeredaModulo10_Este", 15f, false, 212.5f, 313f, 212.5f, 322.5f),
        // El camino en arco por detrás de los módulos 2B, 4, 5 y 6, que baja derecho hasta la reja de Gutiérrez.
        new Camino("ArcoDeLosModulos", 2.2f, true,
            231.4f, 219.4f, 245.8f, 229.2f, 266.4f, 234.5f, 297.2f, 235.7f, 317.8f, 234.8f, 338.3f, 232.3f, 353.7f, 228.1f,
            362f, 222.5f, 368f, 214.5f, 373f, 207.5f, 380f, 200.5f, 387f, 193f, 393f, 184f, 398f, 174f, 402.5f, 164f,
            406f, 154f, 408.5f, 146f, 409.6f, 138f, 409.8f, 120f, 409.8f, 78.5f),
        // Del extremo oeste del 2B al Módulo 3B.
        new Camino("Del2BAl3B", 4f, false, 224.5f, 212.5f, 239.5f, 231.5f),
        // Pasaje entre las Aulas Anexas 2A y el Módulo 2A, hasta la Biblioteca.
        new Camino("PasajeAulasAnexas", 2.8f, false, 224f, 203.35f, 183f, 203.35f),
        // Del cruce de la escalinata (cota 1,6) a la Biblioteca, entre el bloque de abajo y el Módulo 2A.
        new Camino("DelCruceALaBiblioteca", 4f, false, 198.5f, 148.5f, 180f, 177f, 176.5f, 195.5f),
        // Del cruce a José León Suárez, por debajo de la Biblioteca y arriba de la Escuela Infantil.
        new Camino("DelCruceALeonSuarez", 4f, false, 198.5f, 148.5f, 181f, 158f, 171f, 166f, 156f, 166f, 148f, 170f, 97f, 170f),
        // Del portón de Gutiérrez (entre los muretes) al cruce, por la escalinata.
        new Camino("DelPortonAlCruce", 4f, false, 190.2f, 77.3f, 192f, 95f, 196f, 120f, 198.5f, 148.5f),
        // Del cruce a la punta noroeste del Módulo 1.
        new Camino("DelCruceAlModulo1", 3f, false, 198.5f, 148.5f, 220.5f, 150.6f),
        // De la salida este del conector 5-6 al arco y hacia el ala sur del Módulo 7B.
        new Camino("DelConector3AlArco", 2.5f, false, 401.4f, 148.7f, 409.6f, 151.5f, 418.5f, 164f),
    };

    // Catmull-Rom para el arco; los demás quedan con sus quiebres.
    static List<Vector2> Linea(Camino c)
    {
        if (!c.curva) return c.puntos.ToList();
        var p = c.puntos; var r = new List<Vector2>();
        for (int i = 0; i + 1 < p.Length; i++)
        {
            Vector2 a = p[Mathf.Max(0, i - 1)], b = p[i], d = p[i + 1], e = p[Mathf.Min(p.Length - 1, i + 2)];
            int pasos = Mathf.Max(1, Mathf.CeilToInt((d - b).magnitude / 1.5f));
            for (int s = 0; s < pasos; s++)
            {
                float t = s / (float)pasos, t2 = t * t, t3 = t2 * t;
                r.Add(0.5f * (2 * b + (-a + d) * t + (2 * a - 5 * b + 4 * d - e) * t2 + (-a + 3 * b - 3 * d + e) * t3));
            }
        }
        r.Add(p[p.Length - 1]);
        return r;
    }

    static float Altura(Terrain terreno, Vector2 q)
    {
        float y = Alto;
        if (terreno != null) y = Mathf.Max(y, terreno.SampleHeight(new Vector3(q.x, 0, q.y)) + terreno.transform.position.y + 0.05f);
        return y;
    }

    static Mesh Losa(Camino c, List<Vector2> linea, Terrain terreno)
    {
        float m = c.ancho / 2;
        int n = linea.Count;
        var izq = new Vector2[n]; var der = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            Vector2 t0 = i > 0 ? (linea[i] - linea[i - 1]).normalized : (linea[1] - linea[0]).normalized;
            Vector2 t1 = i + 1 < n ? (linea[i + 1] - linea[i]).normalized : t0;
            Vector2 tan = (t0 + t1).normalized; if (tan.sqrMagnitude < 1e-6f) tan = t1;
            Vector2 nor = new Vector2(-tan.y, tan.x);
            float escala = 1f / Mathf.Max(0.35f, Vector2.Dot(nor, new Vector2(-t1.y, t1.x)));
            izq[i] = linea[i] + nor * m * escala; der[i] = linea[i] - nor * m * escala;
        }
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        void Quad(Vector3 a, Vector3 b, Vector3 d, Vector3 e, bool planta)
        {
            int k = v.Count; v.AddRange(new[] { a, b, d, e });
            if (planta) uv.AddRange(new[] { new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(d.x, d.z), new Vector2(e.x, e.z) }.Select(x => x * 0.5f));
            else { float w = Vector3.Distance(a, b); uv.AddRange(new[] { Vector2.zero, new Vector2(w, 0), new Vector2(w, 0.3f), new Vector2(0, 0.3f) }); }
            // Que la cara mire hacia arriba o hacia afuera: se decide al final con la normal.
            tri.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
        }
        Vector3 Arriba(Vector2 q) => new Vector3(q.x, Altura(terreno, q), q.y);
        Vector3 Abajo(Vector2 q) => new Vector3(q.x, Fondo, q.y);
        for (int i = 0; i + 1 < n; i++)
        {
            Quad(Arriba(izq[i]), Arriba(izq[i + 1]), Arriba(der[i + 1]), Arriba(der[i]), true);
            Quad(Abajo(izq[i]), Abajo(izq[i + 1]), Arriba(izq[i + 1]), Arriba(izq[i]), false);
            Quad(Abajo(der[i + 1]), Abajo(der[i]), Arriba(der[i]), Arriba(der[i + 1]), false);
        }
        Quad(Abajo(der[0]), Abajo(izq[0]), Arriba(izq[0]), Arriba(der[0]), false);
        Quad(Abajo(izq[n - 1]), Abajo(der[n - 1]), Arriba(der[n - 1]), Arriba(izq[n - 1]), false);
        // Orientar cada triángulo: los de arriba hacia +y; los costados hacia afuera de la línea.
        for (int i = 0; i < tri.Count; i += 3)
        {
            Vector3 a = v[tri[i]], b = v[tri[i + 1]], d = v[tri[i + 2]];
            var nrm = Vector3.Cross(b - a, d - a);
            var centro = (a + b + d) / 3;
            Vector3 afuera;
            if (Mathf.Abs(nrm.normalized.y) > 0.5f) afuera = Vector3.up;
            else
            {
                var q = new Vector2(centro.x, centro.z);
                var cerca = Cercano(linea, q, out _);
                afuera = new Vector3(q.x - cerca.x, 0, q.y - cerca.y);
                if (afuera.sqrMagnitude < 1e-6f) afuera = Vector3.Cross(Vector3.up, b - a); // tapas: hacia afuera del extremo
                if ((q - linea[0]).magnitude < 0.6f * c.ancho && Mathf.Abs(Vector2.Dot((q - linea[0]).normalized, (linea[1] - linea[0]).normalized)) > 0.9f) afuera = new Vector3(linea[0].x - linea[1].x, 0, linea[0].y - linea[1].y);
                if ((q - linea[n - 1]).magnitude < 0.6f * c.ancho && Mathf.Abs(Vector2.Dot((q - linea[n - 1]).normalized, (linea[n - 1] - linea[n - 2]).normalized)) > 0.9f) afuera = new Vector3(linea[n - 1].x - linea[n - 2].x, 0, linea[n - 1].y - linea[n - 2].y);
            }
            if (Vector3.Dot(nrm, afuera) < 0) { var x = tri[i + 1]; tri[i + 1] = tri[i + 2]; tri[i + 2] = x; }
        }
        var mesh = new Mesh { name = c.nombre, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
        return mesh;
    }

    static Vector2 Cercano(List<Vector2> linea, Vector2 q, out Vector2 tangente)
    {
        float mejor = float.MaxValue; Vector2 r = linea[0]; tangente = Vector2.right;
        for (int i = 0; i + 1 < linea.Count; i++)
        {
            var a = linea[i]; var b = linea[i + 1]; var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            var p = a + ab * t; float d = (q - p).sqrMagnitude;
            if (d < mejor) { mejor = d; r = p; tangente = ab.normalized; }
        }
        return r;
    }

    static void SacarPasto(Terrain terreno, List<Vector2> linea, float medio)
    {
        if (terreno == null) return;
        var td = terreno.terrainData; int res = td.detailResolution; var o = terreno.transform.position;
        float minX = linea.Min(p => p.x) - medio, maxX = linea.Max(p => p.x) + medio, minZ = linea.Min(p => p.y) - medio, maxZ = linea.Max(p => p.y) + medio;
        int x0 = Mathf.Clamp(Mathf.FloorToInt((minX - o.x) / td.size.x * res), 0, res - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((maxX - o.x) / td.size.x * res), 0, res - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((minZ - o.z) / td.size.z * res), 0, res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((maxZ - o.z) / td.size.z * res), 0, res - 1);
        if (x1 <= x0 || z1 <= z0) return;
        for (int capa = 0; capa < td.detailPrototypes.Length; capa++)
        {
            var datos = td.GetDetailLayer(x0, z0, x1 - x0 + 1, z1 - z0 + 1, capa);
            bool cambio = false;
            for (int j = 0; j <= z1 - z0; j++)
                for (int i = 0; i <= x1 - x0; i++)
                {
                    if (datos[j, i] == 0) continue;
                    var q = new Vector2(o.x + (x0 + i + 0.5f) / res * td.size.x, o.z + (z0 + j + 0.5f) / res * td.size.z);
                    if ((Cercano(linea, q, out _) - q).magnitude <= medio) { datos[j, i] = 0; cambio = true; }
                }
            if (cambio) td.SetDetailLayer(x0, z0, capa, datos);
        }
    }

    [MenuItem("Riftwalker/Universidad/Caminos que faltan")]
    public static void Aplicar()
    {
        var escena = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (escena.path != "Assets/Scenes/universidadMAPA.unity" || EditorApplication.isPlaying) throw new InvalidOperationException("Abrir universidadMAPA fuera de Play.");
        var vereda = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Uni/Uni_Vereda.mat");
        if (vereda == null) throw new InvalidOperationException("Falta Uni_Vereda.");
        var terreno = Terrain.activeTerrain;

        Undo.IncrementCurrentGroup(); int grupo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Caminos que faltan");
        var vieja = escena.GetRootGameObjects().FirstOrDefault(g => g.name == Raiz);
        if (vieja != null) Undo.DestroyObjectImmediate(vieja);
        if (AssetDatabase.LoadMainAssetAtPath(MeshPath) != null) AssetDatabase.DeleteAsset(MeshPath);
        var contenedor = new Mesh { name = "CaminosNuevos" };
        AssetDatabase.CreateAsset(contenedor, MeshPath);
        if (terreno != null) Undo.RegisterCompleteObjectUndo(terreno.terrainData, "Pasto debajo de los caminos");

        var raiz = new GameObject(Raiz);
        Undo.RegisterCreatedObjectUndo(raiz, "Caminos que faltan");
        GameObjectUtility.SetStaticEditorFlags(raiz, (StaticEditorFlags)~0);
        var lineas = new List<(Camino c, List<Vector2> l)>();
        foreach (var c in Caminos)
        {
            var linea = Linea(c);
            lineas.Add((c, linea));
            var mesh = Losa(c, linea, terreno);
            AssetDatabase.AddObjectToAsset(mesh, contenedor);
            var go = new GameObject(c.nombre);
            go.transform.SetParent(raiz.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = vereda;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)~0);
            SacarPasto(terreno, linea, c.ancho / 2 + 0.3f);
        }
        if (terreno != null) EditorUtility.SetDirty(terreno.terrainData);

        // Árboles encima de un camino: afuera. Faroles, bancos y tachos: al borde.
        var sacados = new List<string>(); var corridos = new List<string>();
        foreach (var grupoNombre in new[] { "Ambiente_Vegetacion", "Ambiente_Faroles", "Ambiente_Mobiliario" })
        {
            var g = GameObject.Find(grupoNombre);
            if (g == null) continue;
            bool vegetacion = grupoNombre == "Ambiente_Vegetacion";
            var cosas = g.GetComponentsInChildren<Transform>(true).Where(t => vegetacion ? t.parent != null && t.parent.parent == g.transform : t.parent == g.transform).ToList();
            foreach (var t in cosas)
            {
                var q = new Vector2(t.position.x, t.position.z);
                foreach (var (c, l) in lineas)
                {
                    var p = Cercano(l, q, out var tan);
                    float d = (q - p).magnitude, medio = c.ancho / 2;
                    if (d > medio + 0.7f) continue;
                    if (vegetacion) { sacados.Add($"{t.name} ({c.nombre})"); Undo.DestroyObjectImmediate(t.gameObject); }
                    else
                    {
                        var lado = d > 0.01f ? (q - p).normalized : new Vector2(-tan.y, tan.x);
                        var nuevo = p + lado * (medio + 0.8f);
                        Undo.RecordObject(t, "Correr al borde del camino");
                        t.position = new Vector3(nuevo.x, t.position.y, nuevo.y);
                        corridos.Add($"{t.name} ({c.nombre})");
                    }
                    break;
                }
            }
        }
        EditorSceneManager.MarkSceneDirty(escena);
        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(grupo);
        Debug.Log($"Caminos que faltan: {lineas.Count}. Saqué {sacados.Count} árboles: {string.Join(", ", sacados)}. Corrí {corridos.Count}: {string.Join(", ", corridos)}");
    }
}
