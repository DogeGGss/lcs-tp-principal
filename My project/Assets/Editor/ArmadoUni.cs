using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Ayudas compartidas por los armadores de los edificios de la universidad (Mapa 3): lotes de geometría por material,
// piezas (cajas, vigas, cilindros, placas, ventanas, rejas), el volcado a mallas con colisión, textos, muebles como
// prefabs y el terreno (alturas y pasto). Todo en metros, en el espacio de «Mapa (escala 1,25)».
public static class ArmadoUni
{
    public class Lote
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> t = new List<int>();
        public Material mat;
    }

    public static readonly Dictionary<string, Lote> lotes = new Dictionary<string, Lote>();
    public static readonly Vector3 up = Vector3.up;

    public static Lote Get(string material)
    {
        if (!lotes.TryGetValue(material, out var l))
        {
            l = new Lote { mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Uni/Uni_" + material + ".mat") };
            if (l.mat == null) throw new InvalidOperationException("Falta material " + material);
            lotes.Add(material, l);
        }
        return l;
    }

    // UV proyectadas en el mundo: 2 m por unidad salvo las escalas propias; en los materiales «girados» (chapa
    // horizontal) se cambian los ejes de la textura.
    public static readonly Dictionary<string, float> EscalaUV = new Dictionary<string, float>
    {
        { "RejaVerde", 1f }, { "RejaMalla", 1f }, { "MallaBlanca", 1.4f }, { "GranitoBordo", 1.25f }, { "ChapaAuditorio", 1.2f }, { "ChapaFascia", 1.2f },
        { "PersianaNegra", 1.6f }, { "HormigonMoho", .25f },
    };
    public static readonly HashSet<string> Girada = new HashSet<string> { "ChapaAuditorio", "PersianaNegra" };

    static Vector2 Proyectar(string mat, Vector3 p, Vector3 n)
    {
        float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
        Vector2 r = ay >= ax && ay >= az ? new Vector2(p.x, p.z) : ax >= az ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y);
        if (Girada.Contains(mat)) r = new Vector2(r.y, r.x);
        return r * (EscalaUV.TryGetValue(mat, out var e) ? e : .5f);
    }

    public static void Tri(string mat, Vector3 a, Vector3 b, Vector3 c, Vector3 afuera)
    {
        var cr = Vector3.Cross(b - a, c - a);
        if (cr.sqrMagnitude < 1e-10f) return;
        if (Vector3.Dot(cr, afuera) < 0) { var x = b; b = c; c = x; }
        var l = Get(mat); int i = l.v.Count; var n = cr.normalized;
        l.v.Add(a); l.v.Add(b); l.v.Add(c);
        l.uv.Add(Proyectar(mat, a, n)); l.uv.Add(Proyectar(mat, b, n)); l.uv.Add(Proyectar(mat, c, n));
        l.t.Add(i); l.t.Add(i + 1); l.t.Add(i + 2);
    }

    public static void Quad(string mat, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, Vector2[] uvs = null)
    {
        var cr = Vector3.Cross(b - a, c - a);
        if (cr.sqrMagnitude < 1e-10f) { cr = Vector3.Cross(c - a, d - a); if (cr.sqrMagnitude < 1e-10f) return; }
        if (Vector3.Dot(cr, n) < 0)
        {
            var x = b; b = d; d = x;
            if (uvs != null) uvs = new[] { uvs[0], uvs[3], uvs[2], uvs[1] };
        }
        var l = Get(mat); int i = l.v.Count;
        l.v.Add(a); l.v.Add(b); l.v.Add(c); l.v.Add(d);
        if (uvs != null) l.uv.AddRange(uvs);
        else { l.uv.Add(Proyectar(mat, a, n)); l.uv.Add(Proyectar(mat, b, n)); l.uv.Add(Proyectar(mat, c, n)); l.uv.Add(Proyectar(mat, d, n)); }
        l.t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
    }

    // Caja con centro y medios ejes (pueden estar girados).
    public static void Caja(string mat, Vector3 c, Vector3 ex, Vector3 ey, Vector3 ez)
    {
        if (ex.magnitude < .0005f || ey.magnitude < .0005f || ez.magnitude < .0005f) return;
        Cara(mat, c, ey, ez, ex); Cara(mat, c, ey, ez, -ex);
        Cara(mat, c, ex, ez, ey); Cara(mat, c, ex, ez, -ey);
        Cara(mat, c, ex, ey, ez); Cara(mat, c, ex, ey, -ez);
    }

    static void Cara(string mat, Vector3 c, Vector3 u, Vector3 v, Vector3 w)
    {
        var o = c + w;
        Quad(mat, o - u - v, o + u - v, o + u + v, o - u + v, w);
    }

    public static void Box(string mat, float x0, float y0, float z0, float x1, float y1, float z1)
    {
        var c = new Vector3(x0 + x1, y0 + y1, z0 + z1) / 2;
        Caja(mat, c, Vector3.right * Mathf.Abs(x1 - x0) / 2, up * Mathf.Abs(y1 - y0) / 2, Vector3.forward * Mathf.Abs(z1 - z0) / 2);
    }

    // Viga de a hasta b: «alto» en el plano vertical y «ancho» de costado.
    public static void Viga(string mat, Vector3 a, Vector3 b, float alto, float ancho)
    {
        var d = b - a; if (d.magnitude < .001f) return;
        var dir = d.normalized;
        var lado = Vector3.Cross(dir, up); if (lado.sqrMagnitude < 1e-6f) lado = Vector3.right;
        lado.Normalize(); var arriba = Vector3.Cross(lado, dir).normalized;
        Caja(mat, (a + b) / 2, d / 2, arriba * alto / 2, lado * ancho / 2);
    }

    public static void Barra(string mat, Vector3 a, Vector3 b, float g) => Viga(mat, a, b, g, g);

    public static void Cilindro(string mat, Vector3 a, Vector3 b, float r, int lados = 10)
    {
        var d = b - a; if (d.magnitude < .001f) return; var dir = d.normalized;
        var u = Vector3.Cross(dir, Mathf.Abs(dir.y) > .9f ? Vector3.right : up).normalized; var v = Vector3.Cross(dir, u);
        for (int i = 0; i < lados; i++)
        {
            float a0 = i * Mathf.PI * 2 / lados, a1 = (i + 1) * Mathf.PI * 2 / lados;
            Vector3 o0 = (u * Mathf.Cos(a0) + v * Mathf.Sin(a0)) * r, o1 = (u * Mathf.Cos(a1) + v * Mathf.Sin(a1)) * r;
            Quad(mat, a + o0, a + o1, b + o1, b + o0, (o0 + o1).normalized);
            Tri(mat, a, a + o0, a + o1, -dir); Tri(mat, b, b + o0, b + o1, dir);
        }
    }

    // Losa fina con una esquina «o» y dos lados u y v.
    public static void Placa(string mat, Vector3 o, Vector3 u, Vector3 v, float espesor)
    {
        var n = Vector3.Cross(u, v).normalized; if (n.y < 0) n = -n;
        Caja(mat, o + (u + v) / 2, u / 2, n * espesor / 2, v / 2);
    }

    // Polígono convexo levantado de y0 a y1, con su techo.
    public static void Prisma(string mat, string techo, Vector2[] p, float y0, float y1)
    {
        var c = Vector2.zero; foreach (var q in p) c += q; c /= p.Length;
        for (int i = 0; i < p.Length; i++)
        {
            Vector2 a = p[i], b = p[(i + 1) % p.Length]; var m = (a + b) / 2 - c;
            Quad(mat, new Vector3(a.x, y0, a.y), new Vector3(b.x, y0, b.y), new Vector3(b.x, y1, b.y), new Vector3(a.x, y1, a.y), new Vector3(m.x, 0, m.y));
        }
        if (techo == null) return;
        for (int i = 1; i + 1 < p.Length; i++)
            Tri(techo, new Vector3(p[0].x, y1, p[0].y), new Vector3(p[i].x, y1, p[i].y), new Vector3(p[i + 1].x, y1, p[i + 1].y), up);
    }

    // Losa de cuatro esquinas (en planta, a la altura de cada una) con espesor «e» hacia arriba.
    public static void Losa4(string mat, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float e)
    {
        var u = up * e; var cen = (a + b + c + d) / 4;
        Quad(mat, a + u, b + u, c + u, d + u, up);
        Quad(mat, a, b, c, d, Vector3.down);
        void Lado(Vector3 p, Vector3 q) { var m = (p + q) / 2 - cen; m.y = 0; Quad(mat, p, q, q + u, p + u, m); }
        Lado(a, b); Lado(b, c); Lado(c, d); Lado(d, a);
    }

    // Piso plano de un polígono simple cualquiera (cóncavo también): orejas, con espesor hacia abajo.
    public static void Piso(string mat, IList<Vector2> poly, float y, float espesor = .25f)
    {
        var p = new List<Vector2>(poly); float area = 0;
        for (int i = 0; i < p.Count; i++) { var a = p[i]; var b = p[(i + 1) % p.Count]; area += a.x * b.y - b.x * a.y; }
        if (area < 0) p.Reverse();
        var idx = new List<int>(); for (int i = 0; i < p.Count; i++) idx.Add(i);
        bool Dentro(Vector2 q, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (b.x - a.x) * (q.y - a.y) - (b.y - a.y) * (q.x - a.x), d2 = (c.x - b.x) * (q.y - b.y) - (c.y - b.y) * (q.x - b.x), d3 = (a.x - c.x) * (q.y - c.y) - (a.y - c.y) * (q.x - c.x);
            return d1 > 0 && d2 > 0 && d3 > 0;
        }
        int guardia = 0;
        while (idx.Count > 3 && guardia++ < 5000)
        {
            bool corte = false;
            for (int k = 0; k < idx.Count; k++)
            {
                Vector2 a = p[idx[(k + idx.Count - 1) % idx.Count]], b = p[idx[k]], c = p[idx[(k + 1) % idx.Count]];
                if ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x) <= 0) continue;
                bool libre = true;
                foreach (var j in idx) { var q = p[j]; if (q == a || q == b || q == c) continue; if (Dentro(q, a, b, c)) { libre = false; break; } }
                if (!libre) continue;
                Tri(mat, new Vector3(a.x, y, a.y), new Vector3(b.x, y, b.y), new Vector3(c.x, y, c.y), up);
                idx.RemoveAt(k); corte = true; break;
            }
            if (!corte) break;
        }
        if (idx.Count == 3) Tri(mat, new Vector3(p[idx[0]].x, y, p[idx[0]].y), new Vector3(p[idx[1]].x, y, p[idx[1]].y), new Vector3(p[idx[2]].x, y, p[idx[2]].y), up);
        for (int i = 0; i < p.Count; i++)
        {
            var a = p[i]; var b = p[(i + 1) % p.Count]; var n = new Vector3(b.y - a.y, 0, a.x - b.x);
            Quad(mat, new Vector3(a.x, y - espesor, a.y), new Vector3(b.x, y - espesor, b.y), new Vector3(b.x, y, b.y), new Vector3(a.x, y, a.y), n);
        }
    }

    // Ventana sobre un muro: «b» es el punto del muro en el centro del borde de abajo, «n» sale del muro y «r» va a lo largo.
    public static void Ventana(Vector3 b, Vector3 n, Vector3 r, float w, float h, int panos, bool alfeizar, string marco = "PinturaBlanca")
    {
        var c = b + up * h / 2; const float f = .07f, prof = .09f;
        Caja("Vidrio", c + n * .015f, r * w / 2, up * h / 2, n * .015f);
        Caja(marco, c + r * (-w / 2 + f / 2) + n * prof / 2, r * f / 2, up * h / 2, n * prof / 2);
        Caja(marco, c + r * (w / 2 - f / 2) + n * prof / 2, r * f / 2, up * h / 2, n * prof / 2);
        Caja(marco, b + up * f / 2 + n * prof / 2, r * w / 2, up * f / 2, n * prof / 2);
        Caja(marco, b + up * (h - f / 2) + n * prof / 2, r * w / 2, up * f / 2, n * prof / 2);
        for (int i = 1; i < panos; i++) Caja(marco, c + r * (-w / 2 + i * w / panos) + n * .035f, r * .03f, up * h / 2, n * .035f);
        if (h > 1.4f) Caja(marco, b + up * (h * .72f) + n * .035f, r * w / 2, up * .03f, n * .035f);
        if (alfeizar) Caja("LadrilloChico", b - up * .05f + n * .07f, r * (w / 2 + .06f), up * .05f, n * .07f);
    }

    // Ventanas simples repartidas entre a y b (en el pie del muro), para las caras sin foto.
    public static void Ventanas(Vector3 a, Vector3 b, Vector3 n, float y, float w, float h, float paso, string marco = "PinturaBlanca", bool alfeizar = true)
    {
        var d = b - a; float L = d.magnitude; if (L < w + .4f) return; var r = d / L;
        int k = Mathf.Max(1, Mathf.FloorToInt(L / paso)); float p = L / k;
        for (int i = 0; i < k; i++) Ventana(a + r * ((i + .5f) * p) + up * y, n, r, w, h, 2, alfeizar, marco);
    }

    // Copete blanco en los bordes de arriba de un volumen rectangular y la membrana del techo.
    public static void Copete(float x0, float z0, float x1, float z1, float h)
    {
        const float a = .3f, e = .04f;
        Box("PinturaBlanca", x0 - e, h, z0 - e, x1 + e, h + .07f, z0 + a);
        Box("PinturaBlanca", x0 - e, h, z1 - a, x1 + e, h + .07f, z1 + e);
        Box("PinturaBlanca", x0 - e, h, z0 + a, x0 + a, h + .07f, z1 - a);
        Box("PinturaBlanca", x1 - a, h, z0 + a, x1 + e, h + .07f, z1 - a);
        Quad("Techo", new Vector3(x0 + a, h + .01f, z0 + a), new Vector3(x1 - a, h + .01f, z0 + a), new Vector3(x1 - a, h + .01f, z1 - a), new Vector3(x0 + a, h + .01f, z1 - a), up);
    }

    // Aire acondicionado (unidad exterior): caja blanca con el ventilador negro, mirando a «n».
    public static void Aire(Vector3 p, Vector3 n, float escala = 1)
    {
        var r = Vector3.Cross(up, n).normalized;
        Caja("PinturaBlanca", p + up * .28f * escala, r * .42f * escala, up * .28f * escala, n * .14f * escala);
        Cilindro("MetalNegro", p + up * .28f * escala + r * -.08f * escala + n * .14f * escala, p + up * .28f * escala + r * -.08f * escala + n * .16f * escala, .19f * escala, 12);
    }

    // Baranda de acero inoxidable: pasamanos a 1 m, tres caños intermedios y postes cada ~1,2 m (a y b al nivel del piso).
    public static void Baranda(Vector3 a, Vector3 b, string mat = "Galvanizado", float alto = 1.0f)
    {
        Cilindro(mat, a + up * alto, b + up * alto, .025f, 8);
        foreach (var f in new[] { .3f, .55f, .8f }) Cilindro(mat, a + up * alto * f, b + up * alto * f, .012f, 6);
        int k = Mathf.Max(1, Mathf.RoundToInt((b - a).magnitude / 1.2f));
        for (int i = 0; i <= k; i++) { var p = Vector3.Lerp(a, b, (float)i / k); Cilindro(mat, p, p + up * (alto + .02f), .022f, 6); }
    }

    // ---------------------------------------------------------------- Materiales y textos

    public static void MaterialColor(string nombre, Color color, float brillo)
    {
        string path = "Assets/Materials/Uni/Uni_" + nombre + ".mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Uni_" + nombre };
        m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", brillo);
        AssetDatabase.CreateAsset(m, path);
    }

    // Material copiado de otro (textura incluida) con su color; si ya existe, se actualiza.
    public static void MaterialDesde(string nombre, string origen, Color color, Texture textura = null)
    {
        string path = "Assets/Materials/Uni/Uni_" + nombre + ".mat";
        var o = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Uni/Uni_" + origen + ".mat");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(o) { name = "Uni_" + nombre }; AssetDatabase.CreateAsset(m, path); }
        else { m.shader = o.shader; m.CopyPropertiesFromMaterial(o); }
        if (textura != null) m.SetTexture("_BaseMap", textura);
        m.SetColor("_BaseColor", color); EditorUtility.SetDirty(m);
    }

    // Texto sobre una cara que mira hacia «afuera».
    public static TMPro.TextMeshPro Texto(Transform grupo, string nombre, string texto, Vector3 pos, Vector3 afuera, float tam, Vector2 caja, Color color)
    {
        var g = new GameObject(nombre); g.transform.SetParent(grupo, false);
        g.transform.localPosition = pos; g.transform.localRotation = Quaternion.LookRotation(-afuera);
        var tm = g.AddComponent<TMPro.TextMeshPro>();
        var fuente = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/UI/Fuentes/Barlow-SemiBold SDF.asset");
        if (fuente != null) tm.font = fuente;
        tm.text = texto; tm.fontSize = tam; tm.color = color; tm.alignment = TMPro.TextAlignmentOptions.Center;
        tm.rectTransform.sizeDelta = caja;
        return tm;
    }

    // ---------------------------------------------------------------- Mallas, muebles y fondos

    // Vuelca los lotes en mallas (una por material, sub-assets de «meshPath») hijas de un objeto nuevo en «grupo».
    public static void Volcar(Transform grupo, string meshPath, string nombre, string prefijo, ICollection<string> sinColision)
    {
        if (AssetDatabase.LoadMainAssetAtPath(meshPath) != null) AssetDatabase.DeleteAsset(meshPath);
        var contenedor = new Mesh { name = nombre }; AssetDatabase.CreateAsset(contenedor, meshPath);
        var mallas = new GameObject(nombre); mallas.transform.SetParent(grupo, false);
        foreach (var kv in lotes)
        {
            var l = kv.Value; if (l.v.Count == 0) continue;
            var mesh = new Mesh { name = prefijo + kv.Key, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(l.v); mesh.SetUVs(0, l.uv); mesh.SetTriangles(l.t, 0); mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            AssetDatabase.AddObjectToAsset(mesh, contenedor);
            var hijo = new GameObject(kv.Key); hijo.transform.SetParent(mallas.transform, false);
            hijo.AddComponent<MeshFilter>().sharedMesh = mesh;
            hijo.AddComponent<MeshRenderer>().sharedMaterial = l.mat;
            if (sinColision == null || !sinColision.Contains(kv.Key)) hijo.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
        lotes.Clear();
    }

    // Mueble suelto como prefab (si ya está, se usa el que hay): una malla con una submalla por material, los textos
    // como hijos y una caja de colisión. El origen queda en el piso.
    public static GameObject PrefabDeMueble(string carpeta, string nombre, Action<Transform> armar, bool colision = true)
    {
        string path = carpeta + "/" + nombre + ".prefab";
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (p != null) return p;
        var guardados = new Dictionary<string, Lote>(lotes); lotes.Clear();
        var root = new GameObject(nombre);
        armar(root.transform);
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var subs = new List<List<int>>(); var mats = new List<Material>();
        foreach (var kv in lotes)
        {
            var l = kv.Value; int o = v.Count; v.AddRange(l.v); uv.AddRange(l.uv);
            var t = new List<int>(); foreach (var i in l.t) t.Add(i + o); subs.Add(t); mats.Add(l.mat);
        }
        lotes.Clear(); foreach (var kv in guardados) lotes.Add(kv.Key, kv.Value);
        var mesh = new Mesh { name = nombre, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.subMeshCount = subs.Count;
        for (int i = 0; i < subs.Count; i++) mesh.SetTriangles(subs[i], i);
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, carpeta + "/" + nombre + ".asset");
        root.AddComponent<MeshFilter>().sharedMesh = mesh; root.AddComponent<MeshRenderer>().sharedMaterials = mats.ToArray();
        if (colision) { var bc = root.AddComponent<BoxCollider>(); bc.center = mesh.bounds.center; bc.size = mesh.bounds.size; }
        p = PrefabUtility.SaveAsPrefabAsset(root, path); UnityEngine.Object.DestroyImmediate(root);
        return p;
    }

    public static GameObject Colocar(Transform grupo, GameObject prefab, string nombre, Vector3 p, float giro)
    {
        var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, grupo); g.name = nombre;
        g.transform.localPosition = p; g.transform.localRotation = Quaternion.Euler(0, giro, 0);
        return g;
    }

    // Alambrado de malla entre postes negros (como el del pasillo del Auditorio), de «a» a «b» sobre el piso de «a».
    public static void Alambrado(Vector3 a, Vector3 b, float alto = 2.1f)
    {
        var d = b - a; d.y = 0; float largo = d.magnitude; if (largo < .05f) return;
        var u = d / largo; var n = Vector3.Cross(up, u); b = a + d; var m = (a + b) / 2;
        Caja("RejaMalla", m + up * (.1f + alto) / 2, u * largo / 2, up * (alto - .1f) / 2, n * .01f);
        int k = Mathf.Max(1, Mathf.CeilToInt(largo / 2.2f));
        for (int i = 0; i <= k; i++) { var p = a + u * (largo * i / k); Caja("MetalNegro", p + up * (alto - .1f) / 2, u * .04f, up * (alto + .3f) / 2, n * .05f); }
        foreach (var y in new[] { .09f, alto + .03f }) Caja("MetalNegro", m + up * y, u * largo / 2, up * .03f, n * .04f);
    }

    // Objeto suelto con su caja de colisión invisible (bloqueos).
    public static void Bloqueo(Transform grupo, string nombre, Vector3 centro, Vector3 tam, float giro = 0)
    {
        var g = new GameObject(nombre); g.transform.SetParent(grupo, false);
        g.transform.localPosition = centro; g.transform.localRotation = Quaternion.Euler(0, giro, 0);
        g.AddComponent<BoxCollider>().size = tam;
    }

    // ---------------------------------------------------------------- Terreno

    public static float AlturaTerreno(Transform mapa, float x, float z)
    {
        var ter = Terrain.activeTerrain; if (ter == null) return 0;
        var w = mapa.TransformPoint(new Vector3(x, 0, z));
        return mapa.InverseTransformPoint(new Vector3(w.x, ter.SampleHeight(w) + ter.transform.position.y, w.z)).y;
    }

    // Alturas del terreno dentro de «region» (x/z del mapa): «nivel» da la altura local, o null para dejar la que hay.
    public static void TerrenoNivel(Transform mapa, Rect region, Func<Vector2, float?> nivel)
    {
        var ter = Terrain.activeTerrain; if (ter == null) return;
        var td = ter.terrainData; var o = ter.transform.position; int res = td.heightmapResolution;
        var w0 = mapa.TransformPoint(new Vector3(region.xMin, 0, region.yMin)); var w1 = mapa.TransformPoint(new Vector3(region.xMax, 0, region.yMax));
        int i0 = Mathf.Clamp(Mathf.FloorToInt((w0.x - o.x) / td.size.x * (res - 1)), 0, res - 1), i1 = Mathf.Clamp(Mathf.CeilToInt((w1.x - o.x) / td.size.x * (res - 1)), 0, res - 1);
        int j0 = Mathf.Clamp(Mathf.FloorToInt((w0.z - o.z) / td.size.z * (res - 1)), 0, res - 1), j1 = Mathf.Clamp(Mathf.CeilToInt((w1.z - o.z) / td.size.z * (res - 1)), 0, res - 1);
        int an = i1 - i0 + 1, al = j1 - j0 + 1;
        var h = td.GetHeights(i0, j0, an, al); bool cambio = false;
        for (int j = 0; j < al; j++)
            for (int i = 0; i < an; i++)
            {
                var w = new Vector3(o.x + (float)(i0 + i) / (res - 1) * td.size.x, 0, o.z + (float)(j0 + j) / (res - 1) * td.size.z);
                var l = mapa.InverseTransformPoint(w); var y = nivel(new Vector2(l.x, l.z)); if (y == null) continue;
                float wy = mapa.TransformPoint(new Vector3(l.x, y.Value, l.z)).y; float v = Mathf.Clamp01((wy - o.y) / td.size.y);
                if (Mathf.Abs(h[j, i] - v) > 1e-7f) { h[j, i] = v; cambio = true; }
            }
        if (cambio) td.SetHeights(i0, j0, h);
    }

    // Pasto: cada celda de detalle dentro de «region» pasa por «nuevo», que recibe el punto (x/z del mapa), lo que hay y
    // una función para leer otra celda (por ejemplo, para copiar el pasto de al lado), y devuelve lo que queda.
    public static void Pasto(Transform mapa, Rect region, Func<Vector2, int, Func<Vector2, int>, int> nuevo)
    {
        var ter = Terrain.activeTerrain; if (ter == null) return;
        var td = ter.terrainData; var o = ter.transform.position; int res = td.detailResolution;
        var w0 = mapa.TransformPoint(new Vector3(region.xMin, 0, region.yMin)); var w1 = mapa.TransformPoint(new Vector3(region.xMax, 0, region.yMax));
        int i0 = Mathf.Clamp(Mathf.FloorToInt((w0.x - o.x) / td.size.x * res), 0, res - 1), i1 = Mathf.Clamp(Mathf.CeilToInt((w1.x - o.x) / td.size.x * res), 0, res - 1);
        int j0 = Mathf.Clamp(Mathf.FloorToInt((w0.z - o.z) / td.size.z * res), 0, res - 1), j1 = Mathf.Clamp(Mathf.CeilToInt((w1.z - o.z) / td.size.z * res), 0, res - 1);
        int an = i1 - i0 + 1, al = j1 - j0 + 1;
        for (int capa = 0; capa < td.detailPrototypes.Length; capa++)
        {
            var datos = td.GetDetailLayer(i0, j0, an, al, capa); var res2 = (int[,])datos.Clone(); bool cambio = false;
            int Leer(Vector2 q)
            {
                var w = mapa.TransformPoint(new Vector3(q.x, 0, q.y));
                int i = Mathf.FloorToInt((w.x - o.x) / td.size.x * res) - i0, j = Mathf.FloorToInt((w.z - o.z) / td.size.z * res) - j0;
                return i >= 0 && j >= 0 && i < an && j < al ? datos[j, i] : -1;
            }
            for (int j = 0; j < al; j++)
                for (int i = 0; i < an; i++)
                {
                    var l = mapa.InverseTransformPoint(new Vector3(o.x + (i0 + i + .5f) / res * td.size.x, 0, o.z + (j0 + j + .5f) / res * td.size.z));
                    int v = nuevo(new Vector2(l.x, l.z), datos[j, i], Leer);
                    if (v != res2[j, i]) { res2[j, i] = v; cambio = true; }
                }
            if (cambio) td.SetDetailLayer(i0, j0, capa, res2);
        }
    }

    // Distancia de un punto a una polilínea (con el punto más cercano y la dirección del tramo).
    public static float Distancia(IList<Vector2> l, Vector2 q, out Vector2 cerca, out Vector2 tan)
    {
        float dm = float.MaxValue; cerca = l[0]; tan = Vector2.up;
        for (int k = 0; k + 1 < l.Count; k++)
        {
            var ab = l[k + 1] - l[k]; float t = Mathf.Clamp01(Vector2.Dot(q - l[k], ab) / ab.sqrMagnitude); var p = l[k] + ab * t;
            float d = (q - p).magnitude; if (d < dm) { dm = d; cerca = p; tan = ab.normalized; }
        }
        return dm;
    }

    // Pasto de vuelta sobre el trazado viejo de un camino: copia la celda del mismo lado un poco más afuera, salvo
    // debajo de un edificio o de otro piso (rayo hacia abajo que no llega al terreno). Para usar dentro de Pasto.
    public static int PastoDeVuelta(Transform mapa, IList<Vector2> viejo, float medio, Vector2 q, int v, Func<Vector2, int> leer)
    {
        float d = Distancia(viejo, q, out var cerca, out var tan); if (d > medio) return v;
        var w = mapa.TransformPoint(new Vector3(q.x, 0, q.y));
        if (Physics.Raycast(new Vector3(w.x, 300f, w.z), Vector3.down, out var hit, 600f) && !(hit.collider is TerrainCollider)) return v;
        var dir = d > .01f ? (q - cerca) / d : new Vector2(-tan.y, tan.x);
        foreach (var extra in new[] { 1.0f, 2.5f, 4.0f })
        {
            var s = cerca + dir * (2 * medio - d + extra); int vs = leer(s);
            if (vs > 0 && Distancia(viejo, s, out _, out _) > medio) return vs;
        }
        return 6;
    }
}
