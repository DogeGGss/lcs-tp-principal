using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Módulos centrales (2b, 4, 5 y 6), como en las fotos de la uni:
// - Atrás no tienen galería: la planta baja es un muro de ladrillo con ventanas angostas de marco blanco
//   (algunas con la hoja de abajo abierta), una ventana más grande en una punta y una puerta en la otra.
// - Adelante, en las puntas de la galería, arriba de la abertura va la viga blanca de hormigón
//   (antes se veía un hueco oscuro entre el techo de la galería y el ladrillo del primer piso).
// Se puede volver a correr: parte de las mallas originales y rehace lo suyo.
// Coordenadas locales de cada módulo: x a lo largo, z a lo ancho (el frente es el lado de los carteles), y para arriba.
public static class ModulosCentralesFondo
{
    const string MeshPath = "Assets/Meshes/Uni/ModulosCentrales_Fondo.asset";
    const string Original = "Assets/Meshes/Uni/Ambiente_ModulosCentrales.asset";
    const string Sufijo = "_SinGaleriaAtras";
    static readonly string[] Modulos = { "Modulo_2b", "Modulo_4", "Modulo_5", "Modulo_6" };

    const float Galeria = 2.9f;      // de la cara de afuera al muro de las aulas
    const float Muro = 0.3f;
    const float TechoGaleria = 2.72f, PrimerPiso = 3.05f;
    const float Ladrillo = 0.5f;     // UV por metro del ladrillo de los módulos

    class Batch
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<int> t = new List<int>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public Material mat;
        public float escala = 1f;

        // Cara con el frente hacia "afuera"; textura proyectada según hacia dónde mira (en metros por la escala).
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 afuera)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), afuera) < 0) { var x = b; b = d; d = x; }
            int i = v.Count;
            v.AddRange(new[] { a, b, c, d });
            var n = afuera; float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            Vector2 UV(Vector3 p) => (az >= ax && az >= ay ? new Vector2(p.x, p.y - PrimerPiso)
                                    : ax >= ay ? new Vector2(p.z, p.y - PrimerPiso) : new Vector2(p.x, p.z)) * escala;
            uv.AddRange(new[] { UV(a), UV(b), UV(c), UV(d) });
            t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
    }

    static readonly Dictionary<string, Batch> batches = new Dictionary<string, Batch>();
    static float L, W, s; // medio largo, medio ancho, signo del lado de atrás en z

    static Batch Get(string material)
    {
        if (!batches.TryGetValue(material, out var b))
        {
            b = new Batch { mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Uni/Uni_" + material + ".mat") };
            if (b.mat == null) throw new InvalidOperationException("Falta el material Uni_" + material);
            if (material == "LadrilloChico") b.escala = Ladrillo;
            batches.Add(material, b);
        }
        return b;
    }

    // Punto en la cara de atrás: x a lo largo, y alto, d hacia adentro desde la cara de afuera (negativo = afuera).
    static Vector3 P(float x, float y, float d) => new Vector3(x, y, s * (W - d));

    static void Caja(string mat, Vector3 min, Vector3 max)
    {
        if (max.x - min.x < 0.002f || max.y - min.y < 0.002f || max.z - min.z < 0.002f) return;
        var b = Get(mat);
        Vector3 C(int i) => new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
        b.Quad(C(0), C(1), C(3), C(2), Vector3.back);
        b.Quad(C(4), C(5), C(7), C(6), Vector3.forward);
        b.Quad(C(0), C(4), C(6), C(2), Vector3.left);
        b.Quad(C(1), C(5), C(7), C(3), Vector3.right);
        b.Quad(C(0), C(1), C(5), C(4), Vector3.down);
        b.Quad(C(2), C(3), C(7), C(6), Vector3.up);
    }

    // Caja en la fachada de atrás: x0..x1, y0..y1, d0..d1 (profundidad desde la cara de afuera).
    static void CajaAtras(string mat, float x0, float x1, float y0, float y1, float d0, float d1)
    {
        var a = P(x0, y0, d0); var b = P(x1, y1, d1);
        Caja(mat, Vector3.Min(a, b), Vector3.Max(a, b));
    }

    static void Barra(string mat, Vector3 a, Vector3 b, float ancho, float alto, Vector3 referencia)
    {
        var batch = Get(mat);
        var dir = (b - a).normalized;
        var costado = Vector3.Cross(referencia, dir).normalized * (ancho / 2);
        var arriba = Vector3.Cross(dir, costado).normalized * (alto / 2);
        var A = new[] { a - costado - arriba, a + costado - arriba, a + costado + arriba, a - costado + arriba };
        var B = new[] { b - costado - arriba, b + costado - arriba, b + costado + arriba, b - costado + arriba };
        var centro = (a + b) / 2;
        void Cara(Vector3 p, Vector3 q, Vector3 r, Vector3 t) => batch.Quad(p, q, r, t, (p + q + r + t) / 4 - centro);
        Cara(A[0], A[1], A[2], A[3]); Cara(B[0], B[1], B[2], B[3]);
        for (int i = 0; i < 4; i++) { int j = (i + 1) % 4; Cara(A[i], A[j], B[j], B[i]); }
    }

    struct Hueco { public float x0, x1, y0, y1; public Hueco(float x, float ancho, float abajo, float alto) { x0 = x - ancho / 2; x1 = x + ancho / 2; y0 = abajo; y1 = abajo + alto; } }

    // Ventana: vidrio hundido, marco blanco con travesaño, alféizar de hormigón y, si está abierta,
    // la hoja de abajo proyectada hacia afuera (bisagra arriba), como en la foto.
    static void Ventana(Hueco h, bool abierta)
    {
        const float m = 0.06f;
        float yt = h.y0 + (h.y1 - h.y0) * 0.58f;
        float x = (h.x0 + h.x1) / 2;
        Get("Vidrio").Quad(P(h.x0, h.y0, 0.16f), P(h.x1, h.y0, 0.16f), P(h.x1, h.y1, 0.16f), P(h.x0, h.y1, 0.16f), new Vector3(0, 0, s));
        CajaAtras("PinturaBlanca", h.x0, h.x0 + m, h.y0, h.y1, 0.08f, 0.15f);
        CajaAtras("PinturaBlanca", h.x1 - m, h.x1, h.y0, h.y1, 0.08f, 0.15f);
        CajaAtras("PinturaBlanca", h.x0, h.x1, h.y1 - m, h.y1, 0.08f, 0.15f);
        CajaAtras("PinturaBlanca", h.x0, h.x1, h.y0, h.y0 + m, 0.08f, 0.15f);
        CajaAtras("PinturaBlanca", h.x0, h.x1, yt - m / 2, yt + m / 2, 0.08f, 0.15f);
        if (h.x1 - h.x0 > 1.0f) CajaAtras("PinturaBlanca", x - m / 2, x + m / 2, yt, h.y1, 0.08f, 0.15f);
        CajaAtras("Hormigon", h.x0 - 0.05f, h.x1 + 0.05f, h.y0 - 0.06f, h.y0, -0.06f, 0.08f);
        if (!abierta) return;
        float alto = yt - h.y0 - m, ancho = h.x1 - h.x0 - 2 * m, angulo = 32f * Mathf.Deg2Rad;
        var bisagraA = P(h.x0 + m, yt - m / 2, 0.06f); var bisagraB = P(h.x1 - m, yt - m / 2, 0.06f);
        var caida = new Vector3(0, -Mathf.Cos(angulo) * alto, s * Mathf.Sin(angulo) * alto);
        var afuera = Vector3.Cross(bisagraB - bisagraA, caida).normalized;
        if (Vector3.Dot(afuera, new Vector3(0, 0, s)) < 0) afuera = -afuera;
        Get("Vidrio").Quad(bisagraA, bisagraB, bisagraB + caida, bisagraA + caida, afuera);
        Get("Vidrio").Quad(bisagraA, bisagraB, bisagraB + caida, bisagraA + caida, -afuera);
        var lado = (bisagraB - bisagraA).normalized;
        Barra("PinturaBlanca", bisagraA, bisagraB, 0.05f, 0.05f, afuera);
        Barra("PinturaBlanca", bisagraA + caida, bisagraB + caida, 0.05f, 0.05f, afuera);
        Barra("PinturaBlanca", bisagraA, bisagraA + caida, 0.05f, 0.05f, lado);
        Barra("PinturaBlanca", bisagraB, bisagraB + caida, 0.05f, 0.05f, lado);
        // brazos de la ventana proyectante
        Barra("Galvanizado", bisagraA + caida * 0.8f + lado * 0.03f, P(h.x0 + m + 0.03f, h.y0 + 0.25f, 0.1f), 0.015f, 0.015f, Vector3.up);
        Barra("Galvanizado", bisagraB + caida * 0.8f - lado * 0.03f, P(h.x1 - m - 0.03f, h.y0 + 0.25f, 0.1f), 0.015f, 0.015f, Vector3.up);
    }

    static void Puerta(Hueco h)
    {
        CajaAtras("PinturaBlanca", h.x0, h.x1, h.y0, h.y1, 0.1f, 0.15f);
        CajaAtras("PinturaBlanca", h.x0 - 0.06f, h.x0, h.y0, h.y1 + 0.06f, -0.02f, 0.15f);
        CajaAtras("PinturaBlanca", h.x1, h.x1 + 0.06f, h.y0, h.y1 + 0.06f, -0.02f, 0.15f);
        CajaAtras("PinturaBlanca", h.x0, h.x1, h.y1, h.y1 + 0.06f, -0.02f, 0.15f);
        CajaAtras("MetalNegro", h.x1 - 0.18f, h.x1 - 0.08f, 1.0f, 1.04f, 0.04f, 0.1f);
        CajaAtras("Hormigon", h.x0 - 0.2f, h.x1 + 0.2f, -0.1f, 0.06f, -0.5f, 0.1f);
    }

    static void Armar(Transform modulo, Mesh contenedor)
    {
        var ladrillo = modulo.Find("Ladrillo").GetComponent<MeshFilter>().sharedMesh.bounds;
        L = ladrillo.max.x; W = ladrillo.max.z;
        s = modulo.Find("CartelNumero_A").GetComponent<MeshFilter>().sharedMesh.bounds.center.z < 0 ? 1f : -1f;

        // ---- Sacar la galería de atrás de las mallas existentes ----
        bool AtrasGaleria(Vector3 p) => s * p.z >= W - Galeria - 0.05f;
        Recortar(modulo, "Pilares", p => s * p.z >= W - 1.0f && p.y <= 2.8f, contenedor);
        Recortar(modulo, "Blancos", p => AtrasGaleria(p) && p.y >= TechoGaleria - 0.02f && p.y <= PrimerPiso + 0.01f, contenedor);
        Recortar(modulo, "PisoPasillo", p => s * p.z >= W - Galeria - 0.1f, contenedor);
        Recortar(modulo, "Luminarias", p => AtrasGaleria(p) && p.y > 2.4f && p.y < 2.9f, contenedor);
        foreach (var luz in modulo.Cast<Transform>().Where(t => t.name == "Luz" && s * t.localPosition.z > 0).ToList())
            Undo.DestroyObjectImmediate(luz.gameObject);

        var raiz = new GameObject("FondoSinGaleria");
        Undo.RegisterCreatedObjectUndo(raiz, "Fondo de los módulos");
        raiz.transform.SetParent(modulo, false);
        var colisiones = new GameObject("Colisiones").transform;
        colisiones.SetParent(raiz.transform, false);

        // ---- Muro de atrás con sus ventanas ----
        var huecos = new List<(Hueco h, int tipo)>(); // 0 angosta, 1 angosta abierta, 2 grande abierta, 3 puerta
        huecos.Add((new Hueco(L - 2.4f, 1.3f, 1.05f, 1.45f), 2));
        huecos.Add((new Hueco(-L + 2.4f, 0.95f, -0.1f, 2.15f), 3));
        float desde = -L + 4.6f, hasta = L - 4.6f;
        int n = Mathf.FloorToInt((hasta - desde) / 2.3f);
        for (int i = 0; i <= n; i++)
        {
            float x = Mathf.Lerp(desde, hasta, i / (float)n);
            huecos.Add((new Hueco(x, 0.75f, 0.95f, 1.65f), (i % 5 == 1 || i % 7 == 4) ? 1 : 0));
        }
        huecos = huecos.OrderBy(h => h.h.x0).ToList();
        float cursor = -L;
        foreach (var (h, tipo) in huecos)
        {
            CajaAtras("LadrilloChico", cursor, h.x0, -0.1f, PrimerPiso, 0, Muro);
            CajaAtras("LadrilloChico", h.x0, h.x1, -0.1f, h.y0, 0, Muro);
            CajaAtras("LadrilloChico", h.x0, h.x1, h.y1, PrimerPiso, 0, Muro);
            cursor = h.x1;
            if (tipo == 3) Puerta(h); else Ventana(h, tipo != 0);
        }
        CajaAtras("LadrilloChico", cursor, L, -0.1f, PrimerPiso, 0, Muro);
        AgregarColision(colisiones, "MuroAtras", new Vector3(0, (PrimerPiso - 0.1f) / 2, s * (W - Muro / 2)), new Vector3(2 * L, PrimerPiso + 0.1f, Muro));

        // ---- Cerrar las puntas de la galería de atrás ----
        foreach (float lado in new[] { -1f, 1f })
        {
            float x0 = lado > 0 ? L - Muro : -L, x1 = lado > 0 ? L : -L + Muro;
            CajaAtras("LadrilloChico", x0, x1, -0.1f, PrimerPiso, Muro, Galeria);
            AgregarColision(colisiones, "PuntaAtras", new Vector3((x0 + x1) / 2, (PrimerPiso - 0.1f) / 2, s * (W - (Muro + Galeria) / 2)), new Vector3(Muro, PrimerPiso + 0.1f, Galeria - Muro));
        }

        // ---- Viga blanca arriba de las aberturas de las puntas de la galería del frente ----
        foreach (float lado in new[] { -1f, 1f })
        {
            float x0 = lado > 0 ? L - Muro : -L, x1 = lado > 0 ? L : -L + Muro;
            float za = -s * (W - Galeria), zb = -s * (W - 0.31f);
            Caja("Hormigon", new Vector3(x0, TechoGaleria, Mathf.Min(za, zb)), new Vector3(x1, PrimerPiso, Mathf.Max(za, zb)));
        }

        // ---- Tapas de cámaras en el pasto, al pie del muro ----
        var terreno = Terrain.activeTerrain;
        foreach (var (x, d) in new[] { (-L * 0.55f, -1.8f), (-L * 0.55f + 1.6f, -2.0f), (L * 0.3f, -1.7f) })
        {
            var centro = P(x, 0, d);
            float y = terreno != null ? terreno.SampleHeight(modulo.TransformPoint(centro)) + terreno.transform.position.y - modulo.position.y : 0f;
            Caja("HormigonViejo", new Vector3(centro.x - 0.48f, y - 0.05f, centro.z - 0.42f), new Vector3(centro.x + 0.48f, y + 0.07f, centro.z + 0.42f));
            Caja("Tacho", new Vector3(centro.x - 0.36f, y + 0.07f, centro.z - 0.3f), new Vector3(centro.x + 0.36f, y + 0.1f, centro.z + 0.3f));
            SacarPasto(terreno, modulo.TransformPoint(centro), 0.8f);
        }

        foreach (var kv in batches)
        {
            var b = kv.Value;
            if (b.v.Count == 0) continue;
            var mesh = new Mesh { name = modulo.name + "_Fondo_" + kv.Key, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(b.v); mesh.SetUVs(0, b.uv); mesh.SetTriangles(b.t, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
            AssetDatabase.AddObjectToAsset(mesh, contenedor);
            var hijo = new GameObject(kv.Key);
            hijo.transform.SetParent(raiz.transform, false);
            hijo.AddComponent<MeshFilter>().sharedMesh = mesh;
            hijo.AddComponent<MeshRenderer>().sharedMaterial = b.mat;
        }
        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, (StaticEditorFlags)~0);
        batches.Clear();
    }

    // Pasto del terreno alrededor de las tapas de cámara (como en la foto, el pasto está cortado).
    static void SacarPasto(Terrain terreno, Vector3 centro, float radio)
    {
        if (terreno == null) return;
        var td = terreno.terrainData;
        int res = td.detailResolution;
        var o = terreno.transform.position;
        int cx = Mathf.RoundToInt((centro.x - o.x) / td.size.x * res), cz = Mathf.RoundToInt((centro.z - o.z) / td.size.z * res);
        int r = Mathf.CeilToInt(radio / td.size.x * res) + 1;
        int x0 = Mathf.Clamp(cx - r, 0, res - 1), z0 = Mathf.Clamp(cz - r, 0, res - 1);
        int x1 = Mathf.Clamp(cx + r, 0, res - 1), z1 = Mathf.Clamp(cz + r, 0, res - 1);
        Undo.RegisterCompleteObjectUndo(td, "Pasto alrededor de las cámaras");
        for (int capa = 0; capa < td.detailPrototypes.Length; capa++)
        {
            var datos = td.GetDetailLayer(x0, z0, x1 - x0 + 1, z1 - z0 + 1, capa);
            for (int j = 0; j <= z1 - z0; j++)
                for (int i = 0; i <= x1 - x0; i++)
                {
                    float wx = o.x + (x0 + i + 0.5f) / res * td.size.x, wz = o.z + (z0 + j + 0.5f) / res * td.size.z;
                    if ((new Vector2(wx, wz) - new Vector2(centro.x, centro.z)).magnitude <= radio) datos[j, i] = 0;
                }
            td.SetDetailLayer(x0, z0, capa, datos);
        }
        EditorUtility.SetDirty(td);
    }

    static void AgregarColision(Transform padre, string nombre, Vector3 centro, Vector3 medida)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = centro;
        go.AddComponent<BoxCollider>().size = medida;
    }

    // Copia la malla original de la pieza sin los triángulos que caen enteros en la zona; también la colisión.
    static void Recortar(Transform modulo, string pieza, Func<Vector3, bool> dentro, Mesh contenedor)
    {
        var mf = modulo.Find(pieza).GetComponent<MeshFilter>();
        string nombre = mf.sharedMesh != null ? mf.sharedMesh.name.Replace(Sufijo, "") : modulo.name + "_" + pieza;
        var original = AssetDatabase.LoadAllAssetsAtPath(Original).OfType<Mesh>().FirstOrDefault(m => m.name == nombre);
        if (original == null) throw new InvalidOperationException("No está la malla original " + nombre);
        var copia = UnityEngine.Object.Instantiate(original);
        copia.name = nombre + Sufijo;
        var v = original.vertices;
        for (int sub = 0; sub < original.subMeshCount; sub++)
        {
            var tris = original.GetTriangles(sub); var quedan = new List<int>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                if (dentro(v[tris[i]]) && dentro(v[tris[i + 1]]) && dentro(v[tris[i + 2]])) continue;
                quedan.Add(tris[i]); quedan.Add(tris[i + 1]); quedan.Add(tris[i + 2]);
            }
            copia.SetTriangles(quedan, sub);
        }
        copia.RecalculateBounds();
        AssetDatabase.AddObjectToAsset(copia, contenedor);
        Undo.RecordObject(mf, "Sacar la galería de atrás");
        mf.sharedMesh = copia;
        var mc = mf.GetComponent<MeshCollider>();
        if (mc != null) { Undo.RecordObject(mc, "Sacar la galería de atrás"); mc.sharedMesh = copia; }
    }

    [MenuItem("Riftwalker/Universidad/Cerrar el fondo de los módulos centrales")]
    public static void Aplicar()
    {
        var escena = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (escena.path != "Assets/Scenes/universidadMAPA.unity" || EditorApplication.isPlaying) throw new InvalidOperationException("Abrir universidadMAPA fuera de Play.");
        var raiz = GameObject.Find("Ambiente_ModulosCentrales");
        if (raiz == null) throw new InvalidOperationException("No se encontró Ambiente_ModulosCentrales.");
        foreach (var m in new[] { "LadrilloChico", "PinturaBlanca", "Vidrio", "Hormigon", "HormigonViejo", "Galvanizado", "MetalNegro", "Tacho" }) Get(m);
        batches.Clear();

        Undo.IncrementCurrentGroup(); int grupo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Cerrar el fondo de los módulos centrales");
        foreach (var nombre in Modulos)
        {
            var viejo = raiz.transform.Find(nombre + "/FondoSinGaleria");
            if (viejo != null) Undo.DestroyObjectImmediate(viejo.gameObject);
        }
        if (AssetDatabase.LoadMainAssetAtPath(MeshPath) != null) AssetDatabase.DeleteAsset(MeshPath);
        var contenedor = new Mesh { name = "ModulosCentrales_Fondo" };
        AssetDatabase.CreateAsset(contenedor, MeshPath);
        foreach (var nombre in Modulos)
        {
            var modulo = raiz.transform.Find(nombre);
            if (modulo == null) throw new InvalidOperationException("No se encontró " + nombre + ".");
            Armar(modulo, contenedor);
        }
        EditorSceneManager.MarkSceneDirty(escena);
        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(grupo);
        Debug.Log("Fondo de los módulos centrales cerrado. Falta revisarlo y guardar la escena.");
    }
}
