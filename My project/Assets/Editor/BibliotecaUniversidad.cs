using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Biblioteca Horacio González en el mapa de la universidad (US 336), armada según el plano 1:500 y las fotos:
// - La entrada: el vestíbulo vidriado bajo la marquesina con la franja celeste del cartel, el muro curvo de ladrillo
//   con los carteles de la UByD, el portero, la alarma y el farol solar, y la explanada de hormigón con sus juntas.
// - El Bloque: el ala revocada de una planta con techo de chapa a un agua y los cabios a la vista, las ventanas con
//   reja y ornamento, el afiche del campus y el gabinete de la manguera; atrás, el volumen de ladrillo más alto.
// - La casona de la punta sur: la recova de arcos con antepecho de tejas, el tejaroz, el parapeto y los pendones.
// - Las Oficinas, de una planta, con el bloque de baños donde apoya la reja del callejón del 2A.
// - Los contenedores de reciclaje, los cestos de chapa, la hortensia y el arbusto del callejón.
// La sala grande queda como caja provisoria hasta tener sus fotos. Sin interiores: los vidrios son opacos. Medidas en
// metros, en el espacio de «Mapa (escala 1,25)». Se puede volver a correr: rehace el grupo Ambiente_Biblioteca.
public static class BibliotecaUniversidad
{
    const string MeshPath = "Assets/Meshes/Uni/Biblioteca.asset", Grupo = "Ambiente_Biblioteca";
    static readonly string[] CajasViejas = { "Biblioteca_Oficinas", "Biblioteca_Bloque", "Biblioteca_Sala" };
    static readonly HashSet<string> SinColision = new HashSet<string> { "MetalNegro" };
    // Oficinas (de la sala al callejón) y su bloque de baños.
    const float XS = 147.5f, XB = 178.9f, XBa = 182.0f, ZBa = 200.0f, ZN = 215.5f, HO = 4.0f;
    // Bloque y casona: oeste, límite ladrillo/revoque, frente este y el tramo de la entrada; recova, fondo de la recova,
    // fin de la casona, escalón, frente del vestíbulo, línea del vidrio y frente de las Oficinas.
    const float XW = 157.3f, XN = 164.5f, XE = 171.0f, XE2 = 170.4f;
    const float ZS = 165.9f, ZL = 169.0f, ZC = 173.4f, ZQ = 189.0f, ZV = 192.0f, ZG = 194.4f, ZO = 196.0f;
    const float HC = 4.4f, HE = 5.2f, HA = 3.3f, HL = 5.6f;
    // Vestíbulo, muro curvo (arco de círculo del vestíbulo a la esquina de las Oficinas) y el cordón de la explanada.
    const float XV1 = 176.1f, RCurvo = 4.61f, XCordon = 177.6f;
    static readonly Vector2 CentroCurvo = new Vector2(174.3f, 196.24f);

    class Lote
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> t = new List<int>();
        public Material mat;
    }

    static readonly Dictionary<string, Lote> lotes = new Dictionary<string, Lote>();

    static Lote Get(string material)
    {
        if (!lotes.TryGetValue(material, out var l))
        {
            l = new Lote { mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Uni/Uni_" + material + ".mat") };
            if (l.mat == null) throw new InvalidOperationException("Falta material " + material);
            lotes.Add(material, l);
        }
        return l;
    }

    // UV proyectadas en el mundo: 2 m por unidad, salvo el ladrillo y la malla (1 m) y el granito (0,8 m, 2 x 2 baldosas).
    static readonly Dictionary<string, float> EscalaUV = new Dictionary<string, float> { { "RejaVerde", 1f }, { "RejaMalla", 1f }, { "MallaBlanca", 1.4f }, { "GranitoBordo", 1.25f } };

    static Vector2 Proyectar(string mat, Vector3 p, Vector3 n)
    {
        float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
        Vector2 r = ay >= ax && ay >= az ? new Vector2(p.x, p.z) : ax >= az ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y);
        return r * (EscalaUV.TryGetValue(mat, out var e) ? e : .5f);
    }

    static void Tri(string mat, Vector3 a, Vector3 b, Vector3 c, Vector3 afuera)
    {
        var cr = Vector3.Cross(b - a, c - a);
        if (cr.sqrMagnitude < 1e-10f) return;
        if (Vector3.Dot(cr, afuera) < 0) { var x = b; b = c; c = x; }
        var l = Get(mat); int i = l.v.Count; var n = cr.normalized;
        l.v.Add(a); l.v.Add(b); l.v.Add(c);
        l.uv.Add(Proyectar(mat, a, n)); l.uv.Add(Proyectar(mat, b, n)); l.uv.Add(Proyectar(mat, c, n));
        l.t.Add(i); l.t.Add(i + 1); l.t.Add(i + 2);
    }

    static void Quad(string mat, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, Vector2[] uvs = null)
    {
        var cr = Vector3.Cross(b - a, c - a);
        if (cr.sqrMagnitude < 1e-10f) return;
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
    static void Caja(string mat, Vector3 c, Vector3 ex, Vector3 ey, Vector3 ez)
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

    static void Box(string mat, float x0, float y0, float z0, float x1, float y1, float z1)
    {
        var c = new Vector3(x0 + x1, y0 + y1, z0 + z1) / 2;
        Caja(mat, c, Vector3.right * Mathf.Abs(x1 - x0) / 2, Vector3.up * Mathf.Abs(y1 - y0) / 2, Vector3.forward * Mathf.Abs(z1 - z0) / 2);
    }

    // Viga de a hasta b: «alto» en el plano vertical y «ancho» de costado.
    static void Viga(string mat, Vector3 a, Vector3 b, float alto, float ancho)
    {
        var d = b - a; if (d.magnitude < .001f) return;
        var dir = d.normalized;
        var lado = Vector3.Cross(dir, Vector3.up); if (lado.sqrMagnitude < 1e-6f) lado = Vector3.right;
        lado.Normalize(); var arriba = Vector3.Cross(lado, dir).normalized;
        Caja(mat, (a + b) / 2, d / 2, arriba * alto / 2, lado * ancho / 2);
    }

    static void Barra(string mat, Vector3 a, Vector3 b, float g) => Viga(mat, a, b, g, g);

    static void Cilindro(string mat, Vector3 a, Vector3 b, float r, int lados = 10)
    {
        var d = (b - a); if (d.magnitude < .001f) return; var dir = d.normalized;
        var u = Vector3.Cross(dir, Mathf.Abs(dir.y) > .9f ? Vector3.right : Vector3.up).normalized; var v = Vector3.Cross(dir, u);
        for (int i = 0; i < lados; i++)
        {
            float a0 = i * Mathf.PI * 2 / lados, a1 = (i + 1) * Mathf.PI * 2 / lados;
            Vector3 o0 = (u * Mathf.Cos(a0) + v * Mathf.Sin(a0)) * r, o1 = (u * Mathf.Cos(a1) + v * Mathf.Sin(a1)) * r;
            Quad(mat, a + o0, a + o1, b + o1, b + o0, (o0 + o1).normalized);
            Tri(mat, a, a + o0, a + o1, -dir); Tri(mat, b, b + o0, b + o1, dir);
        }
    }

    // Losa fina (chapa, policarbonato) con una esquina «o» y dos lados u y v perpendiculares.
    static void Placa(string mat, Vector3 o, Vector3 u, Vector3 v, float espesor)
    {
        var n = Vector3.Cross(u, v).normalized; if (n.y < 0) n = -n;
        Caja(mat, o + (u + v) / 2, u / 2, n * espesor / 2, v / 2);
    }

    // Polígono convexo levantado de y0 a y1, con su techo.
    static void Prisma(string mat, string techo, Vector2[] p, float y0, float y1)
    {
        var c = Vector2.zero; foreach (var q in p) c += q; c /= p.Length;
        for (int i = 0; i < p.Length; i++)
        {
            Vector2 a = p[i], b = p[(i + 1) % p.Length]; var m = (a + b) / 2 - c;
            Quad(mat, new Vector3(a.x, y0, a.y), new Vector3(b.x, y0, b.y), new Vector3(b.x, y1, b.y), new Vector3(a.x, y1, a.y), new Vector3(m.x, 0, m.y));
        }
        for (int i = 1; i + 1 < p.Length; i++)
            Tri(techo, new Vector3(p[0].x, y1, p[0].y), new Vector3(p[i].x, y1, p[i].y), new Vector3(p[i + 1].x, y1, p[i + 1].y), Vector3.up);
    }

    // Ventana sobre un muro: «b» es el punto del muro en el centro del borde de abajo, «n» sale del muro y «r» va a lo largo.
    static void Ventana(Vector3 b, Vector3 n, Vector3 r, float w, float h, int panos, bool alfeizar, string marco = "PinturaBlanca")
    {
        var up = Vector3.up; var c = b + up * h / 2; const float f = .07f, prof = .09f;
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
    static void Ventanas(Vector3 a, Vector3 b, Vector3 n, float y, float w, float h, float paso)
    {
        var d = b - a; float L = d.magnitude; if (L < w + .4f) return; var r = d / L;
        int k = Mathf.Max(1, Mathf.FloorToInt(L / paso)); float p = L / k;
        for (int i = 0; i < k; i++) Ventana(a + r * ((i + .5f) * p) + Vector3.up * y, n, r, w, h, 2, true);
    }

    // Ventana corrida sobre una cara maciza (de a a b, al pie del muro; «n» hacia afuera): vidrio de punta a punta,
    // marcos blancos cada «paso», travesaño en «yt» y, en algunos paños, la hoja de abajo abierta hacia afuera.
    static void Cinta(Vector3 a, Vector3 b, Vector3 n, float y0, float y1, float yt, float paso, int abiertas, int semilla)
    {
        var d = b - a; float L = d.magnitude; var r = d / L; var up = Vector3.up; var c0 = a + d / 2;
        Caja("Vidrio", c0 + up * (y0 + y1) / 2 + n * .02f, r * L / 2, up * (y1 - y0) / 2, n * .015f);
        Caja("LadrilloChico", c0 + up * (y0 - .05f) + n * .07f, r * (L / 2 + .05f), up * .05f, n * .07f);
        Caja("PinturaBlanca", c0 + up * (y0 + .03f) + n * .05f, r * L / 2, up * .03f, n * .05f);
        Caja("PinturaBlanca", c0 + up * (y1 - .03f) + n * .05f, r * L / 2, up * .03f, n * .05f);
        Caja("PinturaBlanca", c0 + up * yt + n * .05f, r * L / 2, up * .025f, n * .05f);
        int k = Mathf.Max(1, Mathf.RoundToInt(L / paso)); float p = L / k; var rnd = new System.Random(semilla);
        for (int i = 0; i <= k; i++)
            Caja("PinturaBlanca", a + r * (i * p) + up * (y0 + y1) / 2 + n * .05f, r * .03f, up * (y1 - y0) / 2, n * .05f);
        for (int i = 0; i < k; i++)
        {
            if (rnd.Next(100) >= abiertas) continue;
            float h = yt - y0 - .08f, ang = (16 + rnd.Next(14)) * Mathf.Deg2Rad;
            var abajo = -up * Mathf.Cos(ang) + n * Mathf.Sin(ang);
            var bisagra = a + r * ((i + .5f) * p) + up * (yt - .03f) + n * .1f;
            var cen = bisagra + abajo * h / 2; var fuera = Vector3.Cross(r, abajo).normalized;
            Caja("Vidrio", cen, r * (p / 2 - .07f), abajo * h / 2, fuera * .01f);
            Caja("PinturaBlanca", cen + r * (p / 2 - .08f), r * .022f, abajo * h / 2, fuera * .025f);
            Caja("PinturaBlanca", cen - r * (p / 2 - .08f), r * .022f, abajo * h / 2, fuera * .025f);
            Caja("PinturaBlanca", bisagra + abajo * (h - .02f), r * (p / 2 - .06f), abajo * .022f, fuera * .025f);
        }
    }

    // Tarjeta de dos caras con su recorte de la textura.
    static void TarjetaEjes(string mat, Vector3 c, Vector3 r, Vector3 u, float w, float h, Rect uv, bool espejo)
    {
        var n = Vector3.Cross(r, u).normalized; Vector3 hr = r * w / 2, hu = u * h / 2;
        float u0 = espejo ? uv.xMax : uv.xMin, u1 = espejo ? uv.xMin : uv.xMax;
        var uvs = new[] { new Vector2(u0, uv.yMin), new Vector2(u1, uv.yMin), new Vector2(u1, uv.yMax), new Vector2(u0, uv.yMax) };
        Quad(mat, c - hr - hu, c + hr - hu, c + hr + hu, c - hr + hu, n, (Vector2[])uvs.Clone());
        Quad(mat, c - hr - hu, c + hr - hu, c + hr + hu, c - hr + hu, -n, (Vector2[])uvs.Clone());
    }

    // ---------------------------------------------------------------- Ayudas copiadas del 2A

    // Copete blanco en los bordes de arriba de un volumen y la membrana del techo.
    static void Copete(float x0, float z0, float x1, float z1, float h)
    {
        const float a = .3f, e = .04f;
        Box("PinturaBlanca", x0 - e, h, z0 - e, x1 + e, h + .07f, z0 + a);
        Box("PinturaBlanca", x0 - e, h, z1 - a, x1 + e, h + .07f, z1 + e);
        Box("PinturaBlanca", x0 - e, h, z0 + a, x0 + a, h + .07f, z1 - a);
        Box("PinturaBlanca", x1 - a, h, z0 + a, x1 + e, h + .07f, z1 - a);
        Quad("Techo", new Vector3(x0 + a, h + .01f, z0 + a), new Vector3(x1 - a, h + .01f, z0 + a), new Vector3(x1 - a, h + .01f, z1 - a), new Vector3(x0 + a, h + .01f, z1 - a), Vector3.up);
    }

    // Aire acondicionado (unidad exterior): caja blanca con el ventilador negro, mirando a «n».
    static void Aire(Vector3 p, Vector3 n, float escala = 1)
    {
        var r = Vector3.Cross(Vector3.up, n).normalized; var up = Vector3.up;
        Caja("PinturaBlanca", p + up * .28f * escala, r * .42f * escala, up * .28f * escala, n * .14f * escala);
        Cilindro("MetalNegro", p + up * .28f * escala + r * -.08f * escala + n * .14f * escala, p + up * .28f * escala + r * -.08f * escala + n * .16f * escala, .19f * escala, 12);
    }

    static void MaterialColor(string nombre, Color color, float brillo)
    {
        string path = "Assets/Materials/Uni/Uni_" + nombre + ".mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Uni_" + nombre };
        m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", brillo);
        AssetDatabase.CreateAsset(m, path);
    }

    // ---------------------------------------------------------------- Ayudas propias

    static readonly Vector3 up = Vector3.up;

    static void MaterialDesde(string nombre, string origen, Color color)
    {
        string path = "Assets/Materials/Uni/Uni_" + nombre + ".mat";
        var o = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Uni/Uni_" + origen + ".mat");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(o) { name = "Uni_" + nombre }; AssetDatabase.CreateAsset(m, path); }
        else { m.shader = o.shader; m.CopyPropertiesFromMaterial(o); }
        m.SetColor("_BaseColor", color); EditorUtility.SetDirty(m);
    }

    // Texto sobre una cara que mira hacia «afuera».
    static TMPro.TextMeshPro Texto(Transform grupo, string nombre, string texto, Vector3 pos, Vector3 afuera, float tam, Vector2 caja, Color color)
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

    // Muro con arcos de medio punto, de «a» a «b» (cara de afuera, que mira a «n») y con el espesor hacia adentro.
    // Cada arco: centro a lo largo del muro, luz y alto del antepecho (0 si llega al piso). «yArr»: arranque del arco.
    static void MuroArcos(string mat, Vector3 a, Vector3 b, Vector3 n, float esp, float alto, (float s, float luz, float pie)[] arcos, float yArr)
    {
        var d = b - a; float L = d.magnitude; var r = d / L;
        Vector3 P(float s, float y, float e) => a + r * s + up * y - n * e;
        void Macizo(float s0, float s1, float y0, float y1)
        {
            if (s1 - s0 < .001f || y1 - y0 < .001f) return;
            Caja(mat, (P(s0, y0, 0) + P(s1, y1, esp)) / 2, r * (s1 - s0) / 2, up * (y1 - y0) / 2, n * esp / 2);
        }
        float prev = 0;
        foreach (var (s, luz, pie) in arcos)
        {
            float w = luz / 2;
            Macizo(prev, s - w, -.2f, alto);
            Macizo(s - w, s + w, -.2f, pie);
            Macizo(s - w, s + w, yArr + w, alto);
            if (pie > 0) Caja("TejaRoja", P(s, pie + .025f, esp / 2), r * (w + .03f), up * .025f, n * (esp / 2 + .04f));
            // Enjutas (de cada lado del medio punto) y el intradós.
            const int N = 14;
            for (int k = 0; k < N; k++)
            {
                float t0 = Mathf.PI * k / N, t1 = Mathf.PI * (k + 1) / N;
                float s0 = s + w * Mathf.Cos(t0), y0 = yArr + w * Mathf.Sin(t0), s1 = s + w * Mathf.Cos(t1), y1 = yArr + w * Mathf.Sin(t1);
                float sc = k < N / 2 ? s + w : s - w, yc = yArr + w;
                Tri(mat, P(sc, yc, 0), P(s0, y0, 0), P(s1, y1, 0), n);
                Tri(mat, P(sc, yc, esp), P(s0, y0, esp), P(s1, y1, esp), -n);
                float tm = (t0 + t1) / 2;
                Quad(mat, P(s0, y0, 0), P(s1, y1, 0), P(s1, y1, esp), P(s0, y0, esp), -(r * Mathf.Cos(tm) + up * Mathf.Sin(tm)));
            }
            prev = s + w;
        }
        Macizo(prev, L, -.2f, alto);
    }

    // Tejaroz de tejas coloniales sobre la línea de un muro (de «a» a «b», saliendo hacia «n»), con su moldura abajo.
    static void Tejaroz(Vector3 a, Vector3 b, Vector3 n, float y, float vuelo)
    {
        var d = b - a; float L = d.magnitude; var r = d / L;
        var o = a + up * (y + .25f); var caida = n * vuelo + Vector3.down * .25f;
        Placa("TejaRoja", o, d, caida, .05f);
        int k = Mathf.RoundToInt(L / .24f);
        for (int i = 0; i <= k; i++)
        {
            var p = o + r * (L * i / k) + up * .04f;
            Cilindro("TejaRoja", p, p + caida + n * .04f, .055f, 6);
        }
        Caja("RevoqueViejo", a + d / 2 + up * (y - .02f) + n * .07f, r * L / 2, up * .07f, n * .07f);
    }

    // Reja de barrotes delante de una ventana, con la malla fina contra el vidrio y, si va, el ornamento de arriba:
    // el aro con la cruz y las dos volutas. «b»: centro del borde de abajo sobre el muro.
    static void Reja(Vector3 b, Vector3 n, Vector3 r, float w, float h, bool ornamento)
    {
        const string M = "PinturaBlanca";
        var o = n * .13f;
        float x0 = -w / 2 - .12f, x1 = w / 2 + .12f, y0 = -.12f, y1 = h + .1f;
        Vector3 P(float s, float y) => b + r * s + up * y + o;
        Caja("MallaBlanca", b + up * h / 2 + n * .065f, r * w / 2, up * h / 2, n * .004f);
        foreach (var y in new[] { y0, y0 + .12f, (y0 + y1) / 2, y1 - .12f }) Barra(M, P(x0, y), P(x1, y), .03f);
        int k = Mathf.RoundToInt((x1 - x0) / .11f);
        for (int i = 0; i <= k; i++) { float s = Mathf.Lerp(x0, x1, (float)i / k); Barra(M, P(s, y0 - .03f), P(s, y1 + .1f), .022f); }
        foreach (var s in new[] { x0, x1 }) foreach (var y in new[] { y0 + .06f, y1 - .2f }) Barra(M, P(s, y) - o, P(s, y), .03f);
        if (!ornamento) return;
        float ra = .16f, yo = y1 + .32f;
        const int na = 16;
        for (int i = 0; i < na; i++)
        {
            float a0 = i * Mathf.PI * 2 / na, a1 = (i + 1) * Mathf.PI * 2 / na;
            Barra(M, P(ra * Mathf.Cos(a0), yo + ra * Mathf.Sin(a0)), P(ra * Mathf.Cos(a1), yo + ra * Mathf.Sin(a1)), .02f);
        }
        Barra(M, P(0, yo - ra), P(0, y1 + .1f), .02f);
        Barra(M, P(0, yo + ra), P(0, yo + ra + .26f), .022f);
        Barra(M, P(-.07f, yo + ra + .17f), P(.07f, yo + ra + .17f), .022f);
        foreach (var lado in new[] { -1f, 1f })
        {
            // Voluta: espiral que se cierra hacia afuera, unida al aro por arriba y a la reja por abajo.
            var cen = new Vector2(lado * .46f, y1 + .22f);
            var prev = P(lado * ra * .9f, yo + .06f);
            for (int i = 0; i <= 18; i++)
            {
                float t = i / 18f, ang = Mathf.PI / 2 - lado * t * Mathf.PI * 2.4f, rr = .15f * (1 - .72f * t);
                var q = P(cen.x + rr * Mathf.Cos(ang), cen.y + rr * Mathf.Sin(ang));
                Barra(M, prev, q, .018f); prev = q;
            }
            Barra(M, P(lado * .7f, y1 + .1f), P(cen.x + lado * .15f, cen.y), .018f);
        }
    }

    // Puerta vieja de madera de dos hojas con tableros, marco y dintel blancos. «b»: centro del pie sobre el muro.
    static void PuertaVieja(Vector3 b, Vector3 n, Vector3 r, float w, float h)
    {
        Caja("Madera", b + up * h / 2 + n * .03f, r * w / 2, up * h / 2, n * .03f);
        Caja("PinturaBlanca", b + up * (h + .05f) + n * .05f, r * (w / 2 + .1f), up * .05f, n * .05f);
        foreach (var s in new[] { -1f, 1f }) Caja("PinturaBlanca", b + r * s * (w / 2 + .05f) + up * h / 2 + n * .05f, r * .05f, up * h / 2, n * .05f);
        Caja("MetalNegro", b + up * h / 2 + n * .065f, r * .01f, up * (h / 2 - .04f), n * .006f);
        foreach (var s in new[] { -1f, 1f })
            foreach (var (y, a) in new[] { (.55f, .32f), (1.55f, .55f) })
                Caja("Madera", b + r * s * w / 4 + up * y + n * .07f, r * (w / 4 - .09f), up * a, n * .012f);
        Caja("MetalNegro", b + r * .1f + up * 1.05f + n * .085f, r * .02f, up * .07f, n * .02f);
    }

    // Pendón azul de la biblioteca, colgado de su barral sobre una pila.
    static void Pendon(Transform grupo, Vector3 c, Vector3 n, Vector3 r)
    {
        Caja("BannerBiblioteca", c + n * .03f, r * .29f, up * 1.05f, n * .01f);
        Barra("MetalNegro", c + n * .05f + up * 1.07f - r * .34f, c + n * .05f + up * 1.07f + r * .34f, .025f);
        Texto(grupo, "Pendón Biblioteca", "<b>BI\nBLIO\nTECA</b>\n<size=40%>Horacio\nGonzález\n\nUNGS   →</size>", c + n * .045f + up * .05f, n, 1.7f, new Vector2(.7f, 2.0f), Color.white);
    }

    // Contenedor de reciclaje con ruedas: cuerpo de color, tapa negra, manija y el rótulo del frente (mira a «frente»).
    static void Contenedor(Transform grupo, Vector3 p, float giro, string mat, string rotulo, Color tinta)
    {
        var rot = Quaternion.Euler(0, giro, 0); Vector3 fr = rot * Vector3.left, ld = rot * Vector3.forward;
        Vector3 L(float x, float y, float z) => p - fr * x + up * y + ld * z;
        Caja(mat, L(0, .54f, 0), fr * .35f, up * .5f, ld * .29f);
        Caja("PinturaNegra", L(-.02f, 1.07f, 0), fr * .39f, up * .035f, ld * .31f);
        Barra("PinturaNegra", L(.4f, 1.0f, -.24f), L(.4f, 1.0f, .24f), .04f);
        foreach (var z in new[] { -.3f, .3f }) Cilindro("PinturaNegra", L(.3f, .12f, z), L(.3f, .12f, z + Mathf.Sign(z) * .06f), .12f, 10);
        Caja("CartelBlanco", L(-.352f, .6f, 0), fr * .004f, up * .3f, ld * .17f);
        Caja(mat, L(-.358f, .82f, 0), fr * .004f, up * .07f, ld * .17f);
        Texto(grupo, "Rótulo " + rotulo.Replace("\n", " "), rotulo, L(-.362f, .52f, 0), fr, .55f, new Vector2(.32f, .4f), tinta);
    }

    // Cesto de chapa perforada colgado de su aro en un poste.
    static void Cesto(Vector3 p, float giro)
    {
        var rot = Quaternion.Euler(0, giro, 0); var f = rot * Vector3.forward;
        Barra("Galvanizado", p, p + up * 1.15f, .07f);
        var c = p + f * .34f;
        Cilindro("Galvanizado", c + up * .38f, c + up * 1.0f, .23f, 14);
        Cilindro("MetalNegro", c + up * 1.0f, c + up * 1.01f, .205f, 14);
        const int na = 16;
        for (int i = 0; i < na; i++)
        {
            float a0 = i * Mathf.PI * 2 / na, a1 = (i + 1) * Mathf.PI * 2 / na;
            Barra("Galvanizado", c + up * 1.06f + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * .27f, c + up * 1.06f + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * .27f, .025f);
        }
        Barra("Galvanizado", p + up * 1.06f, c + up * 1.06f - f * .27f, .035f);
    }

    // Arbusto del catálogo, escalado para que mida «tam» y apoyado en el piso.
    static void Arbusto(Transform grupo, string prefab, string nombre, Vector3 p, Vector3 tam, float giro)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Uni/Arboles/" + prefab + ".prefab"); if (pf == null) return;
        var g = (GameObject)PrefabUtility.InstantiatePrefab(pf, grupo); g.name = nombre;
        g.transform.localPosition = p; g.transform.localRotation = Quaternion.identity;
        var esc = g.transform.localScale; var mapa = grupo.parent;
        Bounds b = default; bool primero = true;
        foreach (var rd in g.GetComponentsInChildren<Renderer>()) { if (primero) { b = rd.bounds; primero = false; } else b.Encapsulate(rd.bounds); }
        if (primero) return;
        var min = mapa.InverseTransformPoint(b.min); var max = mapa.InverseTransformPoint(b.max); var med = max - min;
        g.transform.localScale = new Vector3(esc.x * tam.x / med.x, esc.y * tam.y / med.y, esc.z * tam.z / med.z);
        float baseY = (min.y - p.y) * tam.y / med.y; var cx = ((min.x + max.x) / 2 - p.x) * tam.x / med.x; var cz = ((min.z + max.z) / 2 - p.z) * tam.z / med.z;
        g.transform.localPosition = p - new Vector3(cx, baseY, cz);
        g.transform.RotateAround(mapa.TransformPoint(p), mapa.up, giro);
    }

    // ---------------------------------------------------------------- Oficinas

    // Las Oficinas: ladrillo, más bajas que la sala. Hacia el callejón del 2A, el bloque de baños donde apoya la reja.
    static void Oficinas()
    {
        Box("LadrilloChico", XS, -.2f, ZO, XB, HO, ZN); Copete(XS, ZO, XB, ZN, HO);
        Box("LadrilloChico", XB, -.2f, ZBa, XBa, HO, ZN);
        const float a = .3f, e = .04f;
        Box("PinturaBlanca", XB + e, HO, ZBa - e, XBa + e, HO + .07f, ZBa + a);
        Box("PinturaBlanca", XBa - a, HO, ZBa + a, XBa + e, HO + .07f, ZN + e);
        Box("PinturaBlanca", XB + e, HO, ZN - a, XBa - a, HO + .07f, ZN + e);
        Quad("Techo", new Vector3(XB + e, HO + .01f, ZBa + a), new Vector3(XBa - a, HO + .01f, ZBa + a), new Vector3(XBa - a, HO + .01f, ZN - a), new Vector3(XB + e, HO + .01f, ZN - a), Vector3.up);
        // Entre la sala y el Bloque, ventanas simples; contra el callejón, el caño de lluvia blanco.
        Ventanas(new Vector3(XS + .5f, 0, ZO), new Vector3(XW - .5f, 0, ZO), Vector3.back, 1.0f, 1.4f, 1.4f, 2.9f);
        Cilindro("PinturaBlanca", new Vector3(XB + .06f, 0, ZBa - .1f), new Vector3(XB + .06f, HO, ZBa - .1f), .045f, 8);
    }

    // ---------------------------------------------------------------- Sala grande

    // La sala de lectura: caja de ladrillo con la fachada vidriada de dos plantas al sur (hacia el patio de la escuela
    // infantil) y al oeste (hacia la calle), las dos con los parasoles de lamas metálicas entre los postes, el alero
    // oscuro que sube hacia afuera y los lucernarios en diente de sierra; en la esquina suroeste se cruzan las lamas y el
    // alero, y queda el pilar de ladrillo de la punta este.
    static void SalaGrande()
    {
        var planta = new[] { new Vector2(93.8f, 180f), new Vector2(147.5f, 180f), new Vector2(147.5f, 215.5f), new Vector2(96.5f, 215.5f) };
        Prisma("LadrilloChico", "Techo", planta, -.2f, 9.0f);
        var so = new Vector3(93.8f, 0, 180f);
        // La sur se queda con la esquina de las lamas (y su poste); la oeste llega hasta ellas y cierra las bandas.
        FachadaVidriada(so, new Vector3(147.5f, 0, 180f), 1.6f, 0f, 2.75f, .5f, false);
        FachadaVidriada(new Vector3(96.5f, 0, 215.5f), so, 0f, 1.36f, .5f, 2.8f, true);
        Box("PinturaBlanca", 93.36f, .5f, 179.56f, 93.84f, 8.0f, 180.04f);
        Box("LadrilloChico", 147.5f - .9f, -.2f, 180f - 1.8f, 147.5f + .4f, 8.95f, 180f);
    }

    // Fachada vidriada de «a» a «b» (la sala queda a la izquierda, mirando de «a» a «b»): zócalo, dos plantas de vidrio
    // con montantes blancos, bandas de losa, postes cada ~6,65 m con lamas, alero que sube hacia afuera y lucernarios.
    // lama/alero A y B: cuánto siguen las lamas y el alero más allá de cada punta (para resolver una esquina); con
    // lamaA > 1 va también el poste de la esquina. bandasHastaB: las bandas siguen en «b» hasta tapar la esquina.
    static void FachadaVidriada(Vector3 a, Vector3 b, float lamaA, float lamaB, float aleroA, float aleroB, bool bandasHastaB)
    {
        a.y = 0; b.y = 0; float L = Vector3.Distance(a, b); var u = (b - a) / L; var n = Vector3.Cross(up, u);
        void F(string mat, float s0, float y0, float d0, float s1, float y1, float d1) =>
            Caja(mat, a + u * (s0 + s1) / 2 + n * (d0 + d1) / 2 + up * (y0 + y1) / 2, u * Mathf.Abs(s1 - s0) / 2, up * Mathf.Abs(y1 - y0) / 2, n * Mathf.Abs(d1 - d0) / 2);
        Vector3 P(float s, float y, float d) => a + u * s + up * y + n * d;
        float Fin(float d) => bandasHastaB ? L + d : L;
        F("Hormigon", 0, -.2f, 0, Fin(.5f), .5f, .5f);
        foreach (var (y0, y1) in new[] { (.5f, 4.0f), (4.5f, 8.0f) })
        {
            F("Vidrio", 0, y0, .32f, L, y1, .38f);
            for (float s = .1f; s < L; s += 1.65f) F("PinturaBlanca", s - .04f, y0, .32f, s + .04f, y1, .42f);
            foreach (var y in new[] { y0 + .05f, y0 + (y1 - y0) * .62f, y1 - .05f }) F("PinturaBlanca", 0, y - .03f, .32f, L, y + .03f, .42f);
        }
        F("Hormigon", 0, 4.0f, 0, Fin(.7f), 4.5f, .7f);
        F("Hormigon", 0, 8.0f, 0, Fin(.5f), 8.7f, .5f);
        // Parasoles: postes y lamas horizontales delante de la planta alta y de la mitad de arriba de la baja.
        int k = Mathf.Max(1, Mathf.RoundToInt((L - .5f) / 6.65f)); float paso = (L - .5f) / k;
        for (int i = lamaA > 1f ? -1 : 0; i <= k; i++)
        {
            float s = i < 0 ? -1.43f : Mathf.Min(L - .15f, .5f + i * paso);
            F("Galvanizado", s - .12f, -.2f, 1.31f, s + .12f, 8.9f, 1.55f);
            if (i >= 0) F("Galvanizado", s - .06f, 4.15f, .7f, s + .06f, 4.3f, 1.31f);
        }
        for (float y = 2.6f; y < 4.1f; y += .3f) F("Galvanizado", -lamaA, y, 1.25f, L + lamaB, y + .035f, 1.6f);
        for (float y = 4.7f; y < 8.5f; y += .32f) F("Galvanizado", -lamaA, y, 1.25f, L + lamaB, y + .035f, 1.6f);
        // Alero oscuro que sube hacia afuera y lucernarios en diente de sierra entre la banda de arriba y el alero.
        Placa("ChapaFascia", P(-aleroA, 8.95f, -.3f), u * (L + aleroA + aleroB), up * .75f + n * 2.9f, .1f);
        for (int i = 0; i < k; i++)
        {
            float s0 = .5f + i * paso, s1 = s0 + paso;
            Tri("Vidrio", P(s0, 8.7f, .4f), P(s1, 8.7f, .4f), P(s1, 9.15f, .4f), n);
            Tri("Vidrio", P(s0, 8.7f, .4f), P(s1, 8.7f, .4f), P(s1, 9.15f, .4f), -n);
            F("PinturaBlanca", s1 - .05f, 8.7f, .35f, s1 + .05f, 9.2f, .45f);
        }
    }

    // ---------------------------------------------------------------- Bloque

    // Techo de chapa a un agua del ala revocada: alto contra el ladrillo y bajando hacia el este, con 0,8 de alero.
    const float TechoAlto = 4.3f, Alero = .8f;
    static float TechoY(float x) => TechoAlto + (HA - .08f - TechoAlto) * (x - XN) / (XE + Alero - XN);

    static void Bloque(Transform grupo)
    {
        // Volumen de ladrillo de atrás, más alto, con dos filas de ventanas a la sala de lectura y los aires arriba.
        Box("LadrilloChico", XW, -.2f, ZC, XN, HL, ZO); Copete(XW, ZC, XN, ZO, HL);
        Ventanas(new Vector3(XW, 0, ZC + .6f), new Vector3(XW, 0, ZO - .6f), Vector3.left, 1.0f, 1.4f, 1.4f, 3.4f);
        Ventanas(new Vector3(XW, 0, ZC + .6f), new Vector3(XW, 0, ZO - .6f), Vector3.left, 3.4f, 1.4f, 1.1f, 3.4f);
        Aire(new Vector3(160.2f, HL + .07f, 178.6f), Vector3.right); Aire(new Vector3(160.2f, HL + .07f, 186.2f), Vector3.right);
        Box("Galvanizado", 161.4f, HL + .07f, 181.0f, 162.4f, HL + .8f, 182.4f);

        // Ala revocada de una planta: el frente este hasta el escalón y el tramo que llega a la entrada.
        Box("RevoqueViejo", XN, -.2f, ZC, XE, HA, ZQ);
        Box("RevoqueViejo", XN, -.2f, ZQ, XE2, HA, ZO);
        Box("RevoqueViejo", XN, HA, ZQ, XE2, TechoY(XE2) - .02f, ZO);
        Placa("ChapaTecho", new Vector3(XN, TechoAlto, ZC), new Vector3(0, 0, ZQ - ZC), new Vector3(XE + Alero - XN, TechoY(XE + Alero) - TechoAlto, 0), .04f);
        Placa("ChapaTecho", new Vector3(XN, TechoAlto, ZQ), new Vector3(0, 0, ZO - ZQ), new Vector3(XE2 + .3f - XN, TechoY(XE2 + .3f) - TechoAlto, 0), .04f);
        Quad("RevoqueViejo", new Vector3(XN, HA, ZC), new Vector3(XE, HA, ZC), new Vector3(XE, TechoY(XE), ZC), new Vector3(XN, TechoAlto, ZC), Vector3.back);
        Quad("RevoqueViejo", new Vector3(XN, HA, ZO), new Vector3(XE2, HA, ZO), new Vector3(XE2, TechoY(XE2), ZO), new Vector3(XN, TechoAlto, ZO), Vector3.forward);
        // Cabios a la vista bajo el alero, la canaleta del borde y el caño de bajada blanco junto al escalón.
        for (float z = ZC + .45f; z < ZQ - .3f; z += .9f)
            Viga("RevoqueViejo", new Vector3(XE - .1f, TechoY(XE - .1f) - .12f, z), new Vector3(XE + Alero - .08f, TechoY(XE + Alero - .08f) - .12f, z), .14f, .1f);
        Box("Galvanizado", XE + Alero - .04f, TechoY(XE + Alero) - .17f, ZC, XE + Alero + .06f, TechoY(XE + Alero) + .02f, ZQ);
        Cilindro("PinturaBlanca", new Vector3(XE + .08f, 0, ZQ - .15f), new Vector3(XE + .08f, 3.05f, ZQ - .15f), .045f, 8);
        Cilindro("PinturaBlanca", new Vector3(XE + .08f, 3.05f, ZQ - .15f), new Vector3(XE + Alero, TechoY(XE + Alero) - .1f, ZQ - .15f), .045f, 8);

        // Frente este: las ventanas con reja y ornamento, el afiche del campus, el gabinete de la manguera y su caño.
        var n = Vector3.right; var r = Vector3.forward;
        foreach (var (zc, w) in new[] { (176.4f, 2.6f), (180.5f, 2.6f), (184.5f, 3.0f) })
        {
            var b = new Vector3(XE, .95f, zc);
            Ventana(b, n, r, w, 1.6f, 3, true); Reja(b, n, r, w, 1.6f, true);
        }
        Box("CartelRojo", XE, 1.2f, 186.0f, XE + .02f, 2.55f, 186.9f);
        Box("AficheVerde", XE + .02f, 1.25f, 186.05f, XE + .03f, 2.5f, 186.85f);
        Texto(grupo, "Afiche Campus", "<b>Campus UNGS</b>\n\nCuidarnos\nentre todos", new Vector3(XE + .035f, 1.95f, 186.45f), n, .7f, new Vector2(.72f, .9f), Color.white);
        Box("CartelRojo", XE, 1.15f, 187.4f, XE + .22f, 1.85f, 187.95f);
        Box("PinturaBlanca", XE + .22f, 1.62f, 187.5f, XE + .225f, 1.76f, 187.85f);
        Cilindro("CartelRojo", new Vector3(XE + .1f, 0, 187.2f), new Vector3(XE + .1f, 1.32f, 187.2f), .05f, 8);
        Cilindro("CartelRojo", new Vector3(XE + .1f, 1.32f, 187.2f), new Vector3(XE + .1f, 1.32f, 187.42f), .05f, 8);
        Cilindro("MetalNegro", new Vector3(XE + .1f, 0, 187.2f), new Vector3(XE + .1f, .22f, 187.2f), .065f, 8);

        // Escalón hacia la entrada: la ventana chica con malla; en el porche, la puerta de aluminio y los afiches.
        Ventana(new Vector3(XE2, 1.0f, 189.95f), n, r, 1.1f, 1.3f, 2, true);
        Caja("MallaBlanca", new Vector3(XE2 + .055f, 1.65f, 189.95f), r * .5f, up * .6f, n * .004f);
        Box("Galvanizado", XE2, .2f, 192.55f, XE2 + .06f, 2.3f, 193.5f);
        Box("Vidrio", XE2 + .06f, .9f, 192.7f, XE2 + .065f, 2.1f, 193.35f);
        Box("CordonAmarillo", XE2 + .065f, 1.0f, 192.7f, XE2 + .07f, 1.05f, 193.35f);
        Box("MetalNegro", XE2 + .06f, 1.0f, 193.36f, XE2 + .1f, 1.3f, 193.4f);
        Box("CartelBlanco", XE2, 1.3f, 193.6f, XE2 + .01f, 1.75f, 193.92f);
        Box("PlacaNaranja", XE2, .95f, 193.98f, XE2 + .01f, 1.45f, 194.3f);
        Box("PlacaAzul", XE2, 1.55f, 193.98f, XE2 + .01f, 1.95f, 194.3f);
        Box("MetalNegro", XE2, 3.0f, 191.25f, XE2 + .2f, 3.2f, 191.6f);
    }

    // ---------------------------------------------------------------- Casona

    // La casona de la punta sur: la recova de arcos con antepecho de tejas (el tercero llega al piso), el tejaroz, el
    // parapeto con su moldura y, en el frente este, el arco de la esquina y el nicho con la ventana enrejada, más alto.
    static void Casona(Transform grupo)
    {
        const string M = "RevoqueViejo"; const float esp = .45f, yArr = 2.0f, luz = 2.2f;
        float xi = XE - esp, Ls = xi - XW, pila = (Ls - 4 * luz) / 5;
        var arcosS = new (float s, float luz, float pie)[4];
        for (int i = 0; i < 4; i++) arcosS[i] = (pila + luz / 2 + i * (luz + pila), luz, i == 2 ? 0f : .45f);
        MuroArcos(M, new Vector3(XW, 0, ZS), new Vector3(xi, 0, ZS), Vector3.back, esp, HC, arcosS, yArr);
        MuroArcos(M, new Vector3(XW, 0, ZL), new Vector3(XW, 0, ZS + esp), Vector3.left, esp, HC, new[] { ((ZL - ZS - esp) / 2, 1.7f, .45f) }, yArr);
        const float sE0 = 1.7f, sE1 = 5.15f;
        MuroArcos(M, new Vector3(XE, 0, ZS), new Vector3(XE, 0, ZC), Vector3.right, esp, HE, new[] { (sE0, luz, .45f), (sE1, luz, .45f) }, yArr);
        // Cuerpo detrás de la recova; el nicho del frente este tiene su fondo con la ventana enrejada.
        float xn = xi - .6f, zn0 = ZS + sE1 - luz / 2, zn1 = ZS + sE1 + luz / 2;
        Box(M, XW, -.2f, ZL, xn, HC - .1f, ZC);
        Box(M, xn, -.2f, ZL, xi, HC - .1f, zn0); Box(M, xn, -.2f, zn1, xi, HC - .1f, ZC);
        Box(M, xn, -.2f, zn0, xi, .45f, zn1); Box(M, xn, yArr + luz / 2, zn0, xi, HC - .1f, zn1);
        var bn = new Vector3(xn, .75f, (zn0 + zn1) / 2);
        Ventana(bn, Vector3.right, Vector3.forward, 1.3f, 1.8f, 2, false); Reja(bn, Vector3.right, Vector3.forward, 1.3f, 1.8f, false);
        // La recova: piso de baldosas rojas, cielorraso y, en el fondo, las puertas de madera y las ventanas enrejadas.
        Quad("Techo", new Vector3(XW + esp, HC - .09f, ZS + esp), new Vector3(xi, HC - .09f, ZS + esp), new Vector3(xi, HC - .09f, ZC), new Vector3(XW + esp, HC - .09f, ZC), up);
        Box("TejaRoja", XW + esp, 0, ZS + esp, xi, .14f, ZL);
        Box(M, XW + esp, 3.15f, ZS + esp, xi, HC - .1f, ZL);
        float X(int i) => XW + arcosS[i].s;
        PuertaVieja(new Vector3(X(1), .14f, ZL), Vector3.back, Vector3.right, 1.15f, 2.45f);
        PuertaVieja(new Vector3(X(2), .14f, ZL), Vector3.back, Vector3.right, 1.15f, 2.45f);
        foreach (var i in new[] { 0, 3 })
        {
            var b = new Vector3(X(i), .95f, ZL);
            Ventana(b, Vector3.back, Vector3.right, 1.2f, 1.5f, 2, false); Reja(b, Vector3.back, Vector3.right, 1.2f, 1.5f, false);
        }
        // Tejaroces, molduras del parapeto y la pilastra de la esquina.
        Tejaroz(new Vector3(XW - .3f, 0, ZS), new Vector3(XE + .65f, 0, ZS), Vector3.back, 3.35f, .65f);
        Tejaroz(new Vector3(XE, 0, ZS), new Vector3(XE, 0, ZC), Vector3.right, 3.35f, .65f);
        Tejaroz(new Vector3(XE, 0, ZS - .3f), new Vector3(XE, 0, ZC), Vector3.right, 4.5f, .35f);
        Box(M, XW - .1f, HC - .2f, ZS - .1f, XE + .1f, HC, ZS + .05f);
        Box(M, XE - .05f, HE - .2f, ZS - .1f, XE + .1f, HE, ZC);
        Box(M, XE - .25f, -.2f, ZS - .08f, XE + .08f, HE, ZS + .3f);
        Box(M, XW - .08f, -.2f, ZS - .08f, XW + .3f, HC, ZS + .2f);
        // Reflectores de las esquinas y los pendones de la biblioteca en las pilas.
        Box("MetalNegro", XE - 1.1f, 3.75f, ZS - .28f, XE - .8f, 3.95f, ZS);
        Box("MetalNegro", XW + .5f, 3.75f, ZS - .28f, XW + .8f, 3.95f, ZS);
        Pendon(grupo, new Vector3(XE, 2.0f, ZS + (sE0 + luz / 2 + sE1 - luz / 2) / 2), Vector3.right, Vector3.forward);
        Pendon(grupo, new Vector3(XW + (arcosS[2].s + arcosS[3].s) / 2, 2.0f, ZS), Vector3.back, Vector3.left);
    }

    // ---------------------------------------------------------------- Entrada

    // Vestíbulo vidriado bajo la marquesina, con la franja celeste del cartel; adelante, el porche y el atril.
    static void Vestibulo(Transform grupo)
    {
        Box("RevoqueViejo", XE2, -.2f, ZG + .05f, XV1, 3.0f, ZO);
        Box("HormigonViejo", XE2, 3.0f, ZV, XV1, 3.25f, ZO);
        Box("CianBiblioteca", XE2, 2.72f, ZV - .12f, XV1, 3.5f, ZV + .1f);
        Texto(grupo, "Cartel Biblioteca", "BIBLIOTECA <b>HORACIO GONZÁLEZ</b>", new Vector3((XE2 + XV1) / 2, 3.1f, ZV - .125f), Vector3.back, 3.3f, new Vector2(5.5f, .7f), Color.white);
        Box("PinturaBlanca", 172.1f, 2.94f, 192.45f, 174.6f, 3.0f, 192.6f);
        Box("Vereda", XE2, 0, ZV - .01f, XV1, .21f, ZG + .05f);
        // El frente vidriado: travesaño, banderolas, montantes y las hojas de las puertas.
        const float yT = 2.3f, yS = 2.95f, y0 = .21f;
        Box("Vidrio", XE2, y0, ZG - .02f, XV1, yS, ZG + .02f);
        foreach (var x in new[] { XE2 + .05f, 171.9f, 173.3f, 174.7f, XV1 - .05f }) Box("Galvanizado", x - .04f, y0, ZG - .06f, x + .04f, yS + .05f, ZG + .04f);
        Box("Galvanizado", XE2, yT - .04f, ZG - .06f, XV1, yT + .04f, ZG + .04f);
        Box("Galvanizado", XE2, yS, ZG - .06f, XV1, yS + .05f, ZG + .04f);
        var hojas = new[] { (XE2 + .09f, 171.86f, true), (171.94f, 172.6f, true), (172.6f, 173.26f, false), (173.34f, 174.0f, true), (174.0f, 174.66f, false) };
        foreach (var (x0, x1, izq) in hojas)
        {
            Box("Galvanizado", x0, y0, ZG - .08f, x1, y0 + .17f, ZG - .03f);
            Box("Galvanizado", x0, yT - .14f, ZG - .08f, x1, yT - .04f, ZG - .03f);
            Box("Galvanizado", x0, y0, ZG - .08f, x0 + .05f, yT - .04f, ZG - .03f);
            Box("Galvanizado", x1 - .05f, y0, ZG - .08f, x1, yT - .04f, ZG - .03f);
            float xm = izq ? x1 - .13f : x0 + .1f;
            Box("MetalNegro", xm, .95f, ZG - .13f, xm + .03f, 1.4f, ZG - .08f);
        }
        foreach (var (x0, x1) in new[] { (172.68f, 173.18f), (174.08f, 174.58f) })
        {
            Box("VinilVerde", x0, 1.05f, ZG - .036f, x1, 2.0f, ZG - .03f);
            Texto(grupo, "Vinilo Biblioteca", "<size=60%>BIBLIOTECA</size>\n<b>HORACIO\nGONZÁLEZ</b>\n<size=50%>UNGS</size>", new Vector3((x0 + x1) / 2, 1.72f, ZG - .04f), Vector3.back, .55f, new Vector2(.48f, .5f), Color.white);
        }
    }

    static Vector3 Pc(float a, float rad, float y) => new Vector3(CentroCurvo.x + rad * Mathf.Cos(a), y, CentroCurvo.y + rad * Mathf.Sin(a));

    // Muro curvo de ladrillo de la esquina de la entrada, con los carteles de la UByD, el portero, la alarma y el
    // farol solar en su poste.
    static void MuroCurvo(Transform grupo)
    {
        const string M = "LadrilloChico"; const int N = 18; const float e = .16f, h = 4.6f;
        float a0 = Mathf.Atan2(ZV - CentroCurvo.y, XV1 - CentroCurvo.x), a1 = Mathf.Atan2(ZO - CentroCurvo.y, XB - CentroCurvo.x);
        for (int k = 0; k < N; k++)
        {
            float t0 = Mathf.Lerp(a0, a1, (float)k / N), t1 = Mathf.Lerp(a0, a1, (float)(k + 1) / N), tm = (t0 + t1) / 2;
            var nr = new Vector3(Mathf.Cos(tm), 0, Mathf.Sin(tm));
            Quad(M, Pc(t0, RCurvo + e, -.2f), Pc(t1, RCurvo + e, -.2f), Pc(t1, RCurvo + e, h), Pc(t0, RCurvo + e, h), nr);
            Quad(M, Pc(t0, RCurvo - e, -.2f), Pc(t1, RCurvo - e, -.2f), Pc(t1, RCurvo - e, h), Pc(t0, RCurvo - e, h), -nr);
            Quad(M, Pc(t0, RCurvo - e, h), Pc(t1, RCurvo - e, h), Pc(t1, RCurvo + e, h), Pc(t0, RCurvo + e, h), up);
        }
        foreach (var (a, s) in new[] { (a0, -1f), (a1, 1f) })
            Quad(M, Pc(a, RCurvo - e, -.2f), Pc(a, RCurvo + e, -.2f), Pc(a, RCurvo + e, h), Pc(a, RCurvo - e, h), new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)) * s);
        // Adentro de la curva, el volumen ciego que la completa.
        var planta = new List<Vector2>();
        for (int k = 0; k <= N; k++) { var p = Pc(Mathf.Lerp(a0, a1, (float)k / N), RCurvo - e, 0); planta.Add(new Vector2(p.x, p.z)); }
        planta.Add(new Vector2(XV1, ZO));
        Prisma(M, "Techo", planta.ToArray(), -.2f, h - .2f);
        // Carteles de la UByD: el blanco con «ubyd» y el amarillo de biblioteca y documentación.
        float aS = Mathf.Lerp(a0, a1, .17f); var nS = new Vector3(Mathf.Cos(aS), 0, Mathf.Sin(aS)); var tS = new Vector3(-Mathf.Sin(aS), 0, Mathf.Cos(aS));
        var pS = Pc(aS, RCurvo + e + .05f, 0);
        Caja("CartelBlanco", pS + up * 2.5f, tS * .47f, up * .36f, nS * .025f);
        Caja("AmarilloUbyd", pS + up * 1.55f + nS * .005f, tS * .47f, up * .58f, nS * .025f);
        Texto(grupo, "Cartel ubyd", "<b>ubyd</b>", pS + up * 2.5f + nS * .03f, nS, 3.0f, new Vector2(1.2f, .6f), new Color(.62f, .4f, .14f));
        Texto(grupo, "Cartel biblioteca y documentación", "biblioteca y\ndocumentación", pS + up * 1.22f + nS * .035f, nS, 1.15f, new Vector2(.9f, .4f), Color.white);
        // Portero, la caja de la alarma con su campana y el farol con panel solar en el poste.
        float aI = Mathf.Lerp(a0, a1, .3f); var nI = new Vector3(Mathf.Cos(aI), 0, Mathf.Sin(aI)); var tI = new Vector3(-Mathf.Sin(aI), 0, Mathf.Cos(aI));
        var pI = Pc(aI, RCurvo + e, 0);
        Caja("PuertaGris", pI + up * 2.95f + nI * .04f, tI * .07f, up * .11f, nI * .04f);
        Caja("CartelRojo", pI + up * 3.5f + nI * .06f, tI * .13f, up * .17f, nI * .06f);
        Cilindro("PinturaBlanca", pI + up * 4.0f, pI + up * 4.0f + nI * .1f, .15f, 14);
        float aL = Mathf.Lerp(a0, a1, .45f); var nL = new Vector3(Mathf.Cos(aL), 0, Mathf.Sin(aL));
        var pL = Pc(aL, RCurvo + e + .14f, 0);
        Cilindro("Galvanizado", pL + up * 2.7f, pL + up * 6.5f, .045f, 8);
        foreach (var y in new[] { 3.0f, 4.3f }) Barra("Galvanizado", pL + up * y - nL * .14f, pL + up * y, .04f);
        Caja("MetalNegro", pL + up * 6.25f + nL * .22f, nL * .18f, up * .05f, Vector3.Cross(up, nL) * .1f);
        Barra("Galvanizado", pL + up * 6.3f, pL + up * 6.3f + nL * .3f, .03f);
        var panel = pL + up * 6.62f; var tP = Vector3.Cross(up, nL).normalized;
        Placa("PanelSolar", panel - tP * .3f - nL * .2f, tP * .6f, -nL * .05f + up * .3f + nL * .45f, .03f);
    }

    // Explanada de hormigón frente a la entrada, del Bloque al cordón, con sus juntas blancas; el escalón; la vereda que
    // baja junto al Bloque hasta la casona.
    static void Explanada()
    {
        const float y = .2f;
        Box("Vereda", XE, 0, 183.0f, XCordon, y, ZV);
        Box("Vereda", XV1 - .1f, 0, ZV - .01f, XCordon, y, 192.9f);
        Box("Vereda", XE2, 0, ZQ, XE, y, ZV);
        Box("Vereda", 172.4f, 0, ZS - 1.4f, 174.0f, y, 183.0f);
        for (float x = XE + 2.2f; x < XCordon - .2f; x += 2.2f) Box("PinturaBlanca", x - .03f, y, 183.05f, x + .03f, y + .005f, ZV - .05f);
        for (float z = 185.2f; z < ZV - .2f; z += 2.2f) Box("PinturaBlanca", XE + .05f, y, z - .03f, XCordon - .05f, y + .005f, z + .03f);
        Box("HormigonViejo", XCordon, 0, 183.0f, XCordon + .18f, y + .1f, 192.85f);
    }

    // Mueble suelto como prefab (si ya está, se usa el que hay): una malla con una submalla por material, los textos
    // como hijos y una caja de colisión. El origen queda en el piso.
    const string CarpetaMobiliario = "Assets/Prefabs/Uni/Mobiliario";
    static GameObject PrefabDeMueble(string nombre, Action<Transform> armar)
    {
        string path = CarpetaMobiliario + "/" + nombre + ".prefab";
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (p != null) return p;
        lotes.Clear();
        var root = new GameObject(nombre);
        armar(root.transform);
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var subs = new List<List<int>>(); var mats = new List<Material>();
        foreach (var kv in lotes)
        {
            var l = kv.Value; int o = v.Count; v.AddRange(l.v); uv.AddRange(l.uv);
            var t = new List<int>(); foreach (var i in l.t) t.Add(i + o); subs.Add(t); mats.Add(l.mat);
        }
        lotes.Clear();
        var mesh = new Mesh { name = nombre }; mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.subMeshCount = subs.Count;
        for (int i = 0; i < subs.Count; i++) mesh.SetTriangles(subs[i], i);
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, CarpetaMobiliario + "/" + nombre + ".asset");
        root.AddComponent<MeshFilter>().sharedMesh = mesh; root.AddComponent<MeshRenderer>().sharedMaterials = mats.ToArray();
        var bc = root.AddComponent<BoxCollider>(); bc.center = mesh.bounds.center; bc.size = mesh.bounds.size;
        p = PrefabUtility.SaveAsPrefabAsset(root, path); UnityEngine.Object.DestroyImmediate(root);
        return p;
    }

    static void Colocar(Transform grupo, GameObject prefab, string nombre, Vector3 p, float giro)
    {
        var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, grupo); g.name = nombre;
        g.transform.localPosition = p; g.transform.localRotation = Quaternion.Euler(0, giro, 0);
    }

    // Contenedores de reciclaje y cestos de chapa, cada uno suelto para moverlo; la hortensia contra el Bloque y el
    // arbusto del callejón.
    static void Equipamiento(Transform grupo)
    {
        var azul = PrefabDeMueble("ContenedorReciclajeAzul", r => Contenedor(r, Vector3.zero, 0, "TachoAzul", "Papel y\ncartón", new Color(.1f, .2f, .45f)));
        var amarillo = PrefabDeMueble("ContenedorReciclajeAmarillo", r => Contenedor(r, Vector3.zero, 0, "TachoAmarillo", "Plásticos,\nvidrios y\nmetales", new Color(.75f, .35f, .08f)));
        var cesto = PrefabDeMueble("CestoChapa", r => Cesto(Vector3.zero, 0));
        Colocar(grupo, azul, "Contenedor azul (papel y cartón)", new Vector3(178.25f, 0, 190.55f), 12f);
        Colocar(grupo, amarillo, "Contenedor amarillo (plásticos, vidrios y metales)", new Vector3(178.95f, 0, 190.15f), 4f);
        Colocar(grupo, cesto, "Cesto de chapa (callejón)", new Vector3(180.5f, 0, 191.3f), 200f);
        Colocar(grupo, cesto, "Cesto de chapa (casona)", new Vector3(172.1f, 0, 164.8f), 70f);
        Arbusto(grupo, "Arbusto0", "Hortensia", new Vector3(171.8f, 0, 180.5f), new Vector3(1.5f, 1.8f, 3.2f), 0);
        Arbusto(grupo, "Arbusto1", "Arbusto del callejón", new Vector3(184.6f, 0, 199.25f), new Vector3(.8f, 1.7f, 1.0f), 30);
    }

    // ---------------------------------------------------------------- Patio de lectura

    // El patio entre la sala, el Bloque y las Oficinas (inventado, sin fotos): sendero de baldosas desde el camino, una
    // placita contra las Oficinas con el árbol en su cantero redondo y los bancos alrededor, el cantero largo de ladrillo
    // contra la sala, una mesa de madera para estudiar afuera, el bicicletero contra el Bloque y la cajita de «libros
    // libres» para dejar uno y llevarse otro.
    static readonly Rect PlazaPatio = Rect.MinMaxRect(148.3f, 188.6f, 156.9f, 195.9f), SenderoPatio = Rect.MinMaxRect(151.8f, 179.0f, 153.4f, 188.6f);
    static readonly Rect CanteroPatio = Rect.MinMaxRect(148.0f, 181.0f, 149.35f, 188.2f), BicisPatio = Rect.MinMaxRect(155.6f, 180.6f, 157.3f, 185.6f);

    static void PatioDeLectura(Transform grupo, Transform mapa)
    {
        float yMax = float.MinValue, yMin = float.MaxValue;
        for (float x = 148f; x <= 157.2f; x += 1f)
            for (float z = 179f; z <= 195.9f; z += 1f) { float h = ArmadoUni.AlturaTerreno(mapa, x, z); yMax = Mathf.Max(yMax, h); yMin = Mathf.Min(yMin, h); }
        float y = yMax + .05f, yb = yMin - .25f;
        // Pisos: la placita, el sendero con su cordón y la losa del bicicletero.
        foreach (var r in new[] { PlazaPatio, SenderoPatio }) Box("BaldosaGris", r.xMin, yb, r.yMin, r.xMax, y, r.yMax);
        foreach (var x in new[] { SenderoPatio.xMin - .1f, SenderoPatio.xMax }) Box("HormigonGris", x, yb, SenderoPatio.yMin, x + .1f, y + .04f, SenderoPatio.yMax);
        Box("HormigonGris", BicisPatio.xMin, yb, BicisPatio.yMin, BicisPatio.xMax, y, BicisPatio.yMax);
        // Cantero largo contra la sala, con su borde de hormigón, la tierra y dos arbustos.
        var c = CanteroPatio; const float hc = .45f, borde = .18f;
        Box("LadrilloChico", c.xMin, yb, c.yMin, c.xMax, y + hc, c.yMax);
        Box("HormigonGris", c.xMin, y + hc, c.yMin, c.xMin + borde, y + hc + .05f, c.yMax);
        Box("HormigonGris", c.xMax - borde, y + hc, c.yMin, c.xMax, y + hc + .05f, c.yMax);
        Box("HormigonGris", c.xMin + borde, y + hc, c.yMin, c.xMax - borde, y + hc + .05f, c.yMin + borde);
        Box("HormigonGris", c.xMin + borde, y + hc, c.yMax - borde, c.xMax - borde, y + hc + .05f, c.yMax);
        Box("TierraMonticulo", c.xMin + borde, y + hc, c.yMin + borde, c.xMax - borde, y + hc + .02f, c.yMax - borde);
        Arbusto(grupo, "Arbusto0", "Arbusto del cantero (sur)", new Vector3(c.center.x, y + hc + .02f, 183.1f), new Vector3(2.7f, 1.0f, .9f), 90);
        Arbusto(grupo, "Arbusto1", "Arbusto del cantero (norte)", new Vector3(c.center.x, y + hc + .02f, 186.2f), new Vector3(2.5f, .9f, .85f), 90);
        // Placita: cantero redondo con el árbol, bancos alrededor mirándolo, el cesto y el farol del sendero.
        var ca = new Vector3(152.6f, y, 192.9f);
        Cilindro("LadrilloChico", ca - up * .3f, ca + up * .45f, 1.15f, 16);
        Cilindro("HormigonGris", ca + up * .45f, ca + up * .5f, 1.22f, 16);
        Cilindro("TierraMonticulo", ca + up * .4f, ca + up * .52f, 1.0f, 16);
        var arbol = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Uni/Arboles/Arbol_Copa0.prefab");
        if (arbol != null) ArmadoUni.Colocar(grupo, arbol, "Árbol del patio de lectura", ca + up * .5f, 35f).transform.localScale = Vector3.one * .6f;
        GameObject Mueble(string n) => AssetDatabase.LoadAssetAtPath<GameObject>(CarpetaMobiliario + "/" + n + ".prefab");
        var banco = Mueble("Banco"); var tacho = Mueble("Tacho"); var farol = Mueble("FarolUNGS"); var bici = Mueble("Bicicleta");
        if (banco != null)
        {
            Colocar(grupo, banco, "Banco del patio (oeste)", new Vector3(150.05f, y, 192.9f), 90f);
            Colocar(grupo, banco, "Banco del patio (este)", new Vector3(155.15f, y, 192.9f), 270f);
            Colocar(grupo, banco, "Banco del patio (norte)", new Vector3(152.6f, y, 195.35f), 180f);
        }
        if (tacho != null) Colocar(grupo, tacho, "Cesto del patio", new Vector3(149.95f, y, 194.7f), 0f);
        if (farol != null) Colocar(grupo, farol, "Farol del patio", new Vector3(151.3f, ArmadoUni.AlturaTerreno(mapa, 151.3f, 180.6f), 180.6f), 90f);
        // Bicicletero de arcos galvanizados contra el Bloque, con tres bicicletas.
        var zs = new[] { 181.0f, 181.95f, 182.9f, 183.85f, 184.8f };
        foreach (var z in zs)
        {
            Barra("Galvanizado", new Vector3(155.95f, y - .1f, z), new Vector3(155.95f, y + .75f, z), .05f);
            Barra("Galvanizado", new Vector3(156.75f, y - .1f, z), new Vector3(156.75f, y + .75f, z), .05f);
            Barra("Galvanizado", new Vector3(155.925f, y + .75f, z), new Vector3(156.775f, y + .75f, z), .05f);
        }
        if (bici != null) foreach (var i in new[] { 0, 1, 3 }) Colocar(grupo, bici, "Bicicleta del patio " + (i + 1), new Vector3(156.35f, y, zs[i] + .32f), i == 1 ? 270f : 90f);
        // Mesa de madera con sus dos bancos, para estudiar afuera.
        var m = new Vector3(150.45f, ArmadoUni.AlturaTerreno(mapa, 150.45f, 184.6f), 184.6f); const float lm = .9f;
        for (int i = 0; i < 4; i++) Box("Madera", m.x - .37f + i * .185f, m.y + .72f, m.z - lm, m.x - .37f + i * .185f + .17f, m.y + .76f, m.z + lm);
        foreach (var s in new[] { -1f, 1f })
        {
            Box("Madera", m.x + s * .57f - .14f, m.y + .42f, m.z - lm, m.x + s * .57f + .14f, m.y + .46f, m.z + lm);
            foreach (var dz in new[] { -.6f, .6f })
            {
                Barra("MetalNegro", new Vector3(m.x + s * .2f, m.y + .72f, m.z + dz), new Vector3(m.x + s * .55f, m.y - .05f, m.z + dz), .05f);
                Box("MetalNegro", m.x - .72f, m.y + .38f, m.z + dz - .025f, m.x + .72f, m.y + .42f, m.z + dz + .025f);
            }
        }
        LibrosLibres(grupo, new Vector3(154.4f, y, 189.5f));
    }

    // Cajita de libros libres sobre un poste: frente abierto hacia el sendero (al oeste), un estante con libros de
    // colores, el cartel y el techito a dos aguas de chapa verde.
    static void LibrosLibres(Transform grupo, Vector3 p)
    {
        float x0 = p.x, x1 = p.x + .45f, z0 = p.z - .4f, z1 = p.z + .4f, y0 = p.y + 1.0f, y1 = p.y + 1.75f;
        Box("MaderaOscura", x0 + .18f, p.y - .3f, p.z - .05f, x0 + .28f, y0, p.z + .05f);
        Box("VerdeSalvia", x1 - .04f, y0, z0, x1, y1, z1);
        Box("VerdeSalvia", x0, y0, z0, x1 - .04f, y1, z0 + .04f);
        Box("VerdeSalvia", x0, y0, z1 - .04f, x1 - .04f, y1, z1);
        Box("VerdeSalvia", x0, y0, z0 + .04f, x1 - .04f, y0 + .04f, z1 - .04f);
        Box("VerdeSalvia", x0, y1 - .04f, z0 + .04f, x1 - .04f, y1, z1 - .04f);
        Box("Madera", x0 + .02f, y0 + .345f, z0 + .04f, x1 - .04f, y0 + .365f, z1 - .04f);
        Box("PinturaBlanca", x0 - .02f, y0 + .56f, z0 + .04f, x0, y1 - .04f, z1 - .04f);
        Texto(grupo, "Cartel libros libres", "<b>LIBROS LIBRES</b>\n<size=60%>tomá uno, dejá uno</size>", new Vector3(x0 - .025f, y0 + .655f, p.z), Vector3.left, .7f, new Vector2(.72f, .17f), new Color(.12f, .3f, .25f));
        var colores = new[] { "AmarilloUbyd", "CianBiblioteca", "BannerBiblioteca", "VinilVerde", "TachoAzul", "PinturaBlanca", "MaderaOscura", "TachoAmarillo" };
        var azar = new System.Random(7);
        foreach (var yb in new[] { y0 + .04f, y0 + .365f })
            for (float z = z0 + .06f; z < z1 - .1f;)
            {
                float e = .03f + (float)azar.NextDouble() * .025f, h = .15f + (float)azar.NextDouble() * .06f;
                Box(colores[azar.Next(colores.Length)], x0 + .06f, yb, z, x1 - .1f, yb + h, z + e);
                z += e + .004f;
            }
        Placa("ChapaVerde", new Vector3(x0 - .07f, y1, z0 - .08f), new Vector3(x1 - x0 + .14f, 0, 0), new Vector3(0, .2f, p.z - z0 + .08f), .03f);
        Placa("ChapaVerde", new Vector3(x0 - .07f, y1, z1 + .08f), new Vector3(x1 - x0 + .14f, 0, 0), new Vector3(0, .2f, p.z - z1 - .08f), .03f);
        Tri("VerdeSalvia", new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x0, y1 + .18f, p.z), Vector3.left);
        Tri("VerdeSalvia", new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y1 + .18f, p.z), Vector3.right);
    }

    // Alambrado de malla entre postes negros (igual al de ArmadoUni, pero en los lotes de la biblioteca).
    static void Alambrado(Vector3 a, Vector3 b, float alto = 2.1f)
    {
        var d = b - a; d.y = 0; float largo = d.magnitude; if (largo < .05f) return;
        var u = d / largo; var n = Vector3.Cross(up, u); b = a + d; var m = (a + b) / 2;
        Caja("RejaMalla", m + up * (.1f + alto) / 2, u * largo / 2, up * (alto - .1f) / 2, n * .01f);
        int k = Mathf.Max(1, Mathf.CeilToInt(largo / 2.2f));
        for (int i = 0; i <= k; i++) { var q = a + u * (largo * i / k); Caja("MetalNegro", q + up * (alto - .1f) / 2, u * .04f, up * (alto + .3f) / 2, n * .05f); }
        foreach (var y in new[] { .09f, alto + .03f }) Caja("MetalNegro", m + up * y, u * largo / 2, up * .03f, n * .04f);
    }

    // Alambrado entre la punta suroeste de la sala y la reja de la calle, con su colisión alta: no se puede pasar a la
    // franja que queda detrás de la biblioteca.
    static void CierreOeste(Transform grupo, Transform mapa)
    {
        const float z = 179.75f, xa = 93.85f, xb = 88.7f;
        float y = Mathf.Min(ArmadoUni.AlturaTerreno(mapa, xa, z), ArmadoUni.AlturaTerreno(mapa, xb, z));
        Alambrado(new Vector3(xa, y, z), new Vector3(xb, y, z));
        ArmadoUni.Bloqueo(grupo, "Bloqueo_DetrasBiblioteca", new Vector3((xa + xb) / 2, y + 2.5f, z), new Vector3(xa - xb + .4f, 5.0f, .4f));
    }

    // ---------------------------------------------------------------- Terreno

    // Pasto: se saca debajo de la explanada, las veredas y los caminos con el trazado nuevo, y vuelve donde estaban los
    // tramos viejos de esos caminos (copiando el del costado, salvo debajo de un edificio o de otro piso).
    static void AjustarTerreno(Transform mapa)
    {
        var terreno = Terrain.activeTerrain; if (terreno == null) return;
        var td = terreno.terrainData; var o = terreno.transform.position; int res = td.detailResolution;
        var nuevos = new[] { CaminosUniversidad.LineaDe("DelCruceALaBiblioteca"), CaminosUniversidad.LineaDe("DelCruceALeonSuarez") };
        var viejos = new[] { new[] { new Vector2(180f, 177f), new Vector2(176.5f, 195.5f) }, new[] { new Vector2(181f, 158f), new Vector2(171f, 166f), new Vector2(156f, 166f), new Vector2(148f, 170f) } };
        const float medio = 2.3f;
        bool En(Vector2 q, float x0, float z0, float x1, float z1) => q.x >= x0 && q.x <= x1 && q.y >= z0 && q.y <= z1;
        bool Piso(Vector2 q) => En(q, XE - .1f, 183f, XCordon + .2f, 192.9f) || En(q, XW, ZS - .1f, XE, ZL) || En(q, XE2 - .1f, ZV - .1f, XV1, ZG + .1f) || En(q, XE2, ZQ, XE, ZV) || En(q, 172.3f, ZS - 1.5f, 174.1f, 183f) || PlazaPatio.Contains(q) || SenderoPatio.Contains(q) || CanteroPatio.Contains(q) || BicisPatio.Contains(q);
        float Dist(IList<Vector2> l, Vector2 q, out Vector2 cerca, out Vector2 tan)
        {
            float dm = float.MaxValue; cerca = l[0]; tan = Vector2.up;
            for (int k = 0; k + 1 < l.Count; k++)
            {
                var ab = l[k + 1] - l[k]; float t = Mathf.Clamp01(Vector2.Dot(q - l[k], ab) / ab.sqrMagnitude); var p = l[k] + ab * t;
                float d = (q - p).magnitude; if (d < dm) { dm = d; cerca = p; tan = ab.normalized; }
            }
            return dm;
        }
        float DistNuevos(Vector2 q) { float d = float.MaxValue; foreach (var l in nuevos) if (l != null) d = Mathf.Min(d, Dist(l, q, out _, out _)); return d; }
        var w0 = mapa.TransformPoint(new Vector3(140f, 0, 150f)); var w1 = mapa.TransformPoint(new Vector3(202f, 0, 202f));
        int i0 = Mathf.Clamp(Mathf.FloorToInt((w0.x - o.x) / td.size.x * res), 0, res - 1), i1 = Mathf.Clamp(Mathf.CeilToInt((w1.x - o.x) / td.size.x * res), 0, res - 1);
        int j0 = Mathf.Clamp(Mathf.FloorToInt((w0.z - o.z) / td.size.z * res), 0, res - 1), j1 = Mathf.Clamp(Mathf.CeilToInt((w1.z - o.z) / td.size.z * res), 0, res - 1);
        int an = i1 - i0 + 1, al = j1 - j0 + 1;
        Vector2 Local(int i, int j) { var l = mapa.InverseTransformPoint(new Vector3(o.x + (i0 + i + .5f) / res * td.size.x, 0, o.z + (j0 + j + .5f) / res * td.size.z)); return new Vector2(l.x, l.z); }
        bool Celda(Vector2 q, out int i, out int j)
        {
            var w = mapa.TransformPoint(new Vector3(q.x, 0, q.y));
            i = Mathf.FloorToInt((w.x - o.x) / td.size.x * res) - i0; j = Mathf.FloorToInt((w.z - o.z) / td.size.z * res) - j0;
            return i >= 0 && j >= 0 && i < an && j < al;
        }
        for (int capa = 0; capa < td.detailPrototypes.Length; capa++)
        {
            var datos = td.GetDetailLayer(i0, j0, an, al, capa); var nuevo = (int[,])datos.Clone(); bool cambio = false;
            for (int j = 0; j < al; j++)
                for (int i = 0; i < an; i++)
                {
                    var q = Local(i, j); int v = datos[j, i];
                    if (Piso(q) || DistNuevos(q) <= medio + .3f) v = 0;
                    else
                    {
                        Vector2 cerca = default, tan = default; float d = float.MaxValue;
                        foreach (var l in viejos) { float dd = Dist(l, q, out var c, out var t); if (dd < d) { d = dd; cerca = c; tan = t; } }
                        if (d > medio + .3f) continue;
                        var w = mapa.TransformPoint(new Vector3(q.x, 0, q.y));
                        if (Physics.Raycast(new Vector3(w.x, o.y + 200f, w.z), Vector3.down, out var hit, 400f) && !(hit.collider is TerrainCollider)) continue;
                        var dir = d > .01f ? (q - cerca) / d : new Vector2(-tan.y, tan.x);
                        v = 6;
                        foreach (var extra in new[] { 1.0f, 2.5f, 4.0f })
                        {
                            var s = cerca + dir * (2 * medio - d + extra);
                            if (!Celda(s, out int si, out int sj) || datos[sj, si] == 0 || Piso(s)) continue;
                            v = datos[sj, si]; break;
                        }
                    }
                    if (v != nuevo[j, i]) { nuevo[j, i] = v; cambio = true; }
                }
            if (cambio) td.SetDetailLayer(i0, j0, capa, nuevo);
        }
    }

    // ---------------------------------------------------------------- Armado

    [MenuItem("Riftwalker/Universidad/Armar Biblioteca")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/universidadMAPA.unity" || EditorApplication.isPlaying) throw new InvalidOperationException("Abrir universidadMAPA fuera de Play.");
        GameObject mapaGo = null;
        foreach (var g in scene.GetRootGameObjects()) if (g.name == "Mapa (escala 1,25)") mapaGo = g;
        if (mapaGo == null) throw new InvalidOperationException("No se encontró Mapa (escala 1,25).");
        MaterialDesde("RevoqueViejo", "RevoqueBlanco", new Color(.84f, .79f, .67f));
        MaterialColor("CianBiblioteca", new Color(.13f, .62f, .75f), .35f);
        MaterialColor("AmarilloUbyd", new Color(.93f, .67f, .13f), .3f);
        MaterialColor("BannerBiblioteca", new Color(.17f, .47f, .78f), .15f);
        MaterialColor("TachoAzul", new Color(.33f, .43f, .66f), .35f);
        MaterialColor("TachoAmarillo", new Color(.93f, .85f, .42f), .35f);
        MaterialColor("AficheVerde", new Color(.1f, .33f, .4f), .2f);
        MaterialColor("VinilVerde", new Color(.12f, .5f, .45f), .3f);
        var mapa = mapaGo.transform;
        // Si la escena ya tenía cambios sin guardar, se arma igual pero no se guarda: lo decide quien la está editando.
        bool habiaCambios = scene.isDirty;
        lotes.Clear();
        var anterior = mapa.Find(Grupo); if (anterior != null) UnityEngine.Object.DestroyImmediate(anterior.gameObject);
        if (AssetDatabase.LoadMainAssetAtPath(MeshPath) != null) AssetDatabase.DeleteAsset(MeshPath);
        var sinDetalle = mapa.Find("Ambiente_EdificiosSinDetalle");
        foreach (var n in CajasViejas) { var t = sinDetalle != null ? sinDetalle.Find(n) : null; if (t != null) t.gameObject.SetActive(false); }
        var go = new GameObject(Grupo); go.transform.SetParent(mapa, false);
        var g2 = go.transform;
        // Los muebles sueltos van primero: sus prefabs usan los mismos lotes.
        Equipamiento(g2); lotes.Clear();
        Oficinas(); SalaGrande(); Bloque(g2); Casona(g2); Vestibulo(g2); MuroCurvo(g2); Explanada(); PatioDeLectura(g2, mapa); CierreOeste(g2, mapa);
        var contenedor = new Mesh { name = "Biblioteca" }; AssetDatabase.CreateAsset(contenedor, MeshPath);
        var mallas = new GameObject("Biblioteca"); mallas.transform.SetParent(g2, false);
        foreach (var kv in lotes)
        {
            var l = kv.Value; if (l.v.Count == 0) continue;
            var mesh = new Mesh { name = "Bib_" + kv.Key, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(l.v); mesh.SetUVs(0, l.uv); mesh.SetTriangles(l.t, 0); mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            AssetDatabase.AddObjectToAsset(mesh, contenedor);
            var hijo = new GameObject(kv.Key); hijo.transform.SetParent(mallas.transform, false);
            hijo.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = hijo.AddComponent<MeshRenderer>(); mr.sharedMaterial = l.mat;
            if (!SinColision.Contains(kv.Key)) hijo.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
        lotes.Clear();
        // Los caminos del plano que llegan a la entrada y pasan frente a la casona, con su trazado corregido.
        CaminosUniversidad.RehacerCamino(mapa, "DelCruceALaBiblioteca");
        CaminosUniversidad.RehacerCamino(mapa, "DelCruceALeonSuarez");
        Physics.SyncTransforms();
        AjustarTerreno(mapa);
        EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
        if (!habiaCambios) EditorSceneManager.SaveScene(scene);
        Debug.Log(habiaCambios ? "Biblioteca armada (la escena tenía cambios sin guardar: no se guardó)." : "Biblioteca armada.");
    }
}
