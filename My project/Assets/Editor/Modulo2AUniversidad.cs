using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Módulo 2A en el mapa de la universidad (US 338): el laboratorio de química del ICI y el bloque chico de las aulas
// anexas, armados según las fotos y el plano que se acordó.
// - Frente (este): la entrada «2a ICI laboratorios de química» bajo el alero, con la puerta doble, el gabinete rojo,
//   el panel de chapa con avisos y la cartelera de Química.
// - Costado largo (sur): la ventana corrida con reja blanca, el alero largo sobre postes blancos y los aires.
// - Atrás (oeste): el anexo con la puerta de persiana, las chimeneas negras, los conductos, el equipo blanco y los
//   portones oxidados.
// - Cara norte: sencilla (no se llega).
// - Bloque chico: los caños de extracción con los ventiladores, la ventana con malla verde, el caño de gas y la torre
//   de antena.
// - El paso techado que sale derecho del 2b hacia el 2A y la escalera de emergencia del 2b (se ve, no se sube).
// Sin interiores: los vidrios son opacos. Medidas en metros, en el espacio de «Mapa (escala 1,25)». Se puede volver a
// correr: rehace el grupo Ambiente_Modulo2A.
public static class Modulo2AUniversidad
{
    const string MeshPath = "Assets/Meshes/Uni/Modulo2A.asset", Grupo = "Ambiente_Modulo2A";
    static readonly string[] CajasViejas = { "Modulo2A", "AulasAnexas2A" };
    static readonly HashSet<string> SinColision = new HashSet<string> { "MetalNegro" };
    const float X0 = 188.3f, X1 = 227.7f, Z0 = 175.2f, Z1 = 201.7f, HB = 3.5f;
    const float BX0 = 187.9f, BX1 = 217.9f, BZ0 = 205.0f, BZ1 = 215.0f;

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


    // ---------------------------------------------------------------- Ayudas propias

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

    // Reja de barrotes blancos en cuadrícula delante de una ventana corrida.
    static void Rejilla(Vector3 a, Vector3 b, Vector3 n, float y0, float y1, string mat = "PinturaBlanca")
    {
        var d = b - a; float L = d.magnitude; var r = d / L; var up = Vector3.up; var o = n * .12f;
        int kv = Mathf.Max(1, Mathf.RoundToInt(L / .3f));
        for (int i = 0; i <= kv; i++) Caja(mat, a + r * (L * i / kv) + up * (y0 + y1) / 2 + o, r * .012f, up * (y1 - y0) / 2, n * .012f);
        for (float y = y0 + .05f; y < y1; y += .38f) Caja(mat, a + r * L / 2 + up * y + o, r * L / 2, up * .012f, n * .015f);
    }

    // Aire acondicionado (unidad exterior): caja blanca con el ventilador negro, mirando a «n».
    static void Aire(Vector3 p, Vector3 n, float escala = 1)
    {
        var r = Vector3.Cross(Vector3.up, n).normalized; var up = Vector3.up;
        Caja("PinturaBlanca", p + up * .28f * escala, r * .42f * escala, up * .28f * escala, n * .14f * escala);
        Cilindro("MetalNegro", p + up * .28f * escala + r * -.08f * escala + n * .14f * escala, p + up * .28f * escala + r * -.08f * escala + n * .16f * escala, .19f * escala, 12);
    }

    // Puerta doble con paños de vidrio arriba; «z» es el centro sobre la cara x = x0 que mira a «n» (±x).
    static void PuertaDoble(float x0, float zc, float ancho, Vector3 n, bool oxidada)
    {
        float s = n.x, w = ancho / 2;
        Box("PinturaBlanca", x0, 0, zc - w, x0 + s * .05f, 2.25f, zc + w);
        Box("PinturaBlanca", x0, 2.25f, zc - w - .07f, x0 + s * .08f, 2.33f, zc + w + .07f);
        Box("PinturaBlanca", x0, 0, zc - w - .07f, x0 + s * .08f, 2.33f, zc - w);
        Box("PinturaBlanca", x0, 0, zc + w, x0 + s * .08f, 2.33f, zc + w + .07f);
        Box("PinturaBlanca", x0, 0, zc - .02f, x0 + s * .07f, 2.25f, zc + .02f);
        foreach (var c in new[] { zc - w / 2, zc + w / 2 })
        {
            Box("Vidrio", x0 + s * .05f, 1.25f, c - w * .32f, x0 + s * .055f, 2.0f, c + w * .32f);
            Box("MetalNegro", x0 + s * .05f, 1.0f, c + (c < zc ? w * .38f : -w * .38f) - .04f, x0 + s * .09f, 1.06f, c + (c < zc ? w * .38f : -w * .38f) + .04f);
        }
        if (!oxidada) return;
        // Chorreaduras de óxido finas y una franja gastada abajo.
        var rnd = new System.Random((int)(zc * 10));
        for (int i = 0; i < 6; i++)
        {
            float z = zc - w + .1f + (float)rnd.NextDouble() * (ancho - .25f), y = .3f + (float)rnd.NextDouble() * 1.5f, h = .25f + (float)rnd.NextDouble() * .5f;
            Box("ChapaOxido", x0 + s * .05f, y, z, x0 + s * .054f, Mathf.Min(2.2f, y + h), z + .03f + (float)rnd.NextDouble() * .05f);
        }
        Box("ChapaOxido", x0 + s * .05f, .02f, zc - w + .05f, x0 + s * .054f, .14f, zc + w - .05f);
    }

    // Puerta de persianas (rejillas horizontales) sobre la cara x = x0 que mira a «n».
    static void PuertaPersiana(float x0, float z0, float z1, float y1, Vector3 n, bool oxidada)
    {
        float s = n.x;
        Box("MetalNegro", x0, .02f, z0, x0 + s * .02f, y1, z1);
        for (float y = .1f; y < y1 - .05f; y += .12f)
            Viga("PinturaBlanca", new Vector3(x0 + s * .05f, y, z0 + .03f), new Vector3(x0 + s * .05f, y, z1 - .03f), .07f, .025f);
        Box("PinturaBlanca", x0, 0, z0 - .06f, x0 + s * .08f, y1 + .06f, z0);
        Box("PinturaBlanca", x0, 0, z1, x0 + s * .08f, y1 + .06f, z1 + .06f);
        Box("PinturaBlanca", x0, y1, z0, x0 + s * .08f, y1 + .06f, z1);
        if (!oxidada) return;
        Box("ChapaOxido", x0 + s * .06f, 1.05f, z0 + .05f, x0 + s * .07f, 1.35f, z1 - .05f);
    }

    // ---------------------------------------------------------------- Módulo 2A

    static void Modulo2A(Transform grupo)
    {
        // Cuerpo de una planta con techo plano; los volúmenes más altos dan los parapetos escalonados de las fotos.
        Box("LadrilloChico", X0, -.2f, Z0, X1, HB, Z1); Copete(X0, Z0, X1, Z1, HB);
        Box("LadrilloChico", 210.0f, HB - .1f, 188.0f, X1, 4.3f, Z1); Copete(210.0f, 188.0f, X1, Z1, 4.3f);
        Box("LadrilloChico", X0, HB - .1f, 191.0f, 201.0f, 4.6f, Z1); Copete(X0, 191.0f, 201.0f, Z1, 4.6f);
        Box("LadrilloChico", X0, HB - .1f, Z0, 194.5f, 4.2f, 181.0f); Copete(X0, Z0, 194.5f, 181.0f, 4.2f);
        CostadoSur(); FrenteEste(grupo); Atras(); CaraNorte();
    }

    // Costado largo (sur), hacia el pasto de los eucaliptos: ventana corrida con reja blanca, alero largo sobre postes
    // blancos con base de hormigón que da la vuelta a la esquina del frente, y los aires en el piso.
    static void CostadoSur()
    {
        var a = new Vector3(X0 + 1.0f, 0, Z0); var b = new Vector3(X1 - 1.0f, 0, Z0);
        Cinta(a, b, Vector3.back, 1.0f, 2.45f, 1.95f, 1.6f, 0, 2001);
        Rejilla(a, b, Vector3.back, 1.0f, 2.45f);
        for (float x = X0 + 2.5f; x < X1 - 1.5f; x += 4.4f) Aire(new Vector3(x, .05f, Z0 - .2f), Vector3.back);
        Alero(new Vector3(X0 - .2f, 0, Z0), new Vector3(X1 + Sale, 0, Z0), Vector3.back, true);
    }

    // Alero de chapa sobre postes: de «a» a «b» pegado a la pared, sale «Sale» hacia «n».
    const float Sale = 2.7f, AleroY = 2.62f;
    static void Alero(Vector3 a, Vector3 b, Vector3 n, bool conBases)
    {
        var d = b - a; float L = d.magnitude; var r = d / L; var up = Vector3.up;
        var frente0 = a + n * Sale; var frente1 = b + n * Sale;
        Placa("ChapaOxido", a + up * (AleroY + .17f), d, n * Sale + up * -.12f, .03f);
        Viga("PinturaBlanca", frente0 + up * AleroY, frente1 + up * AleroY, .14f, .07f);
        Viga("PinturaBlanca", a + up * (AleroY + .12f) + n * .05f, b + up * (AleroY + .12f) + n * .05f, .1f, .07f);
        int k = Mathf.Max(1, Mathf.RoundToInt(L / 3.3f));
        for (int i = 0; i <= k; i++)
        {
            var p = frente0 + r * (L * i / k) - n * .05f;
            if (conBases) Caja("Hormigon", p + up * .15f, r * .18f, up * .15f, n * .18f);
            Cilindro("PinturaBlanca", p + up * (conBases ? .3f : 0), p + up * AleroY, .055f, 10);
            Viga("PinturaBlanca", p + up * (AleroY + .02f), p - n * (Sale - .1f) + up * (AleroY + .14f), .08f, .05f);
        }
    }

    // Frente (este): la entrada del laboratorio de química bajo el alero.
    static void FrenteEste(Transform grupo)
    {
        var n = Vector3.right; float x = X1;
        Alero(new Vector3(x, 0, Z0), new Vector3(x, 0, 196.8f), n, false);
        // Vereda de baldosas bajo el alero y el camino de hormigón hasta la puerta.
        Box("Vereda", x, 0, Z0 - Sale, x + Sale, .06f, Z1);
        Box("HormigonViejo", x + Sale, 0, 181.0f, 244.6f, .05f, 182.6f);
        // Gabinete rojo con su caño y la caja gris, en la punta.
        Box("CartelRojo", x, 1.35f, 175.9f, x + .22f, 2.05f, 176.6f);
        Box("PinturaBlanca", x + .22f, 1.4f, 175.97f, x + .225f, 2.0f, 176.53f);
        Cilindro("CartelRojo", new Vector3(x + .11f, 0, 176.78f), new Vector3(x + .11f, 1.45f, 176.78f), .045f, 8);
        Box("PuertaGris", x, .3f, 175.95f, x + .18f, 1.1f, 176.6f);
        // Cartel del módulo, la puerta doble con el aviso de zona restringida y la viga de hormigón encima.
        var cartel = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Uni/Carteles/CartelModulo.prefab"), grupo);
        cartel.name = "Cartel 2a"; cartel.transform.localPosition = new Vector3(x, 1.45f, 179.9f); cartel.transform.localRotation = Quaternion.LookRotation(n);
        foreach (var t in cartel.GetComponentsInChildren<TMPro.TextMeshPro>())
        {
            t.text = t.name == "Numero" ? "2a" : "ICI\nlaboratorios\nde química";
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        }
        PuertaDoble(x, 181.8f, 1.9f, n, false);
        Box("CordonAmarillo", x + .055f, 1.45f, 180.98f, x + .06f, 1.75f, 181.62f);
        Box("HormigonViejo", x, 2.38f, 179.6f, x + .12f, 2.55f, 186.6f);
        // Matafuego, el panel de chapa con avisos, la cartelera de Química y la rejilla redonda.
        Cilindro("CartelRojo", new Vector3(x + .1f, 1.0f, 183.6f), new Vector3(x + .1f, 1.55f, 183.6f), .08f, 10);
        Box("Galvanizado", x, .05f, 184.8f, x + .04f, 2.62f, 186.6f);
        foreach (var (z, y) in new[] { (185.1f, 1.7f), (185.6f, 1.45f), (186.1f, 1.8f), (185.4f, 1.15f) }) Box("PinturaBlanca", x + .04f, y, z, x + .045f, y + .28f, z + .21f);
        Box("Corcho", x, 1.05f, 187.3f, x + .04f, 2.35f, 190.9f);
        string[] materias = { "QUÍMICA ORGÁNICA", "QUÍMICA GENERAL", "QUÍMICA INORGÁNICA" };
        for (int i = 0; i < 3; i++)
        {
            float zc = 187.9f + i * 1.2f;
            Box("CartelBlanco", x + .04f, 2.16f, zc - .45f, x + .05f, 2.3f, zc + .45f);
            var g = new GameObject("Cartelera " + (i + 1)); g.transform.SetParent(grupo, false);
            g.transform.localPosition = new Vector3(x + .055f, 2.23f, zc); g.transform.localRotation = Quaternion.LookRotation(-n);
            var tm = g.AddComponent<TMPro.TextMeshPro>(); tm.text = materias[i]; tm.fontSize = .5f; tm.fontStyle = TMPro.FontStyles.Bold;
            tm.alignment = TMPro.TextAlignmentOptions.Center; tm.color = new Color(.1f, .1f, .1f); tm.rectTransform.sizeDelta = new Vector2(.88f, .13f);
        }
        Cilindro("PinturaBlanca", new Vector3(x, .9f, 192.5f), new Vector3(x + .05f, .9f, 192.5f), .12f, 12);
        // Chimenea de acero que sale de la pared y sube por encima del techo; un aire arriba, en la parte alta.
        Cilindro("Galvanizado", new Vector3(x, 3.15f, 178.4f), new Vector3(x + .32f, 3.15f, 178.4f), .14f, 10);
        Cilindro("Galvanizado", new Vector3(x + .32f, 3.0f, 178.4f), new Vector3(x + .32f, 5.0f, 178.4f), .14f, 10);
        Box("Galvanizado", x + .1f, 5.0f, 178.18f, x + .54f, 5.08f, 178.62f);
        Aire(new Vector3(x + .14f, 3.55f, 196.6f), n, .9f);
        // Tapa verde de la cisterna en el pasto.
        Box("Hormigon", 233.0f, 0, 183.5f, 234.6f, .16f, 184.6f);
        Box("PinturaVerde", 233.1f, .16f, 183.6f, 234.5f, .18f, 184.5f);
    }

    // Atrás (oeste), hacia la biblioteca: el anexo con la puerta de persiana, las chimeneas negras, los conductos, el
    // equipo blanco, los aires y los portones oxidados (uno de persianas, con su techito de chapa).
    static void Atras()
    {
        var n = Vector3.left; float x = X0;
        Box("LadrilloChico", x - 3.3f, -.2f, 195.0f, x, 3.0f, Z1 - .7f); Copete(x - 3.3f, 195.0f, x, Z1 - .7f, 3.0f);
        PuertaPersiana(x - 3.3f, 197.4f, 198.4f, 2.1f, n, false);
        Box("PinturaBlanca", x - 3.32f, .3f, 196.0f, x - 3.3f, .8f, 196.9f);
        for (float y = .36f; y < .78f; y += .08f) Box("PinturaBlanca", x - 3.36f, y, 196.0f, x - 3.3f, y + .03f, 196.9f);
        Aire(new Vector3(x - 2.0f, 3.08f, 196.0f), n, .8f); Aire(new Vector3(x - 1.2f, 3.08f, 199.4f), n, .8f);
        Cilindro("PinturaNegra", new Vector3(193.5f, HB, 198.6f), new Vector3(193.5f, 5.7f, 198.6f), .15f, 10);
        Cilindro("PinturaNegra", new Vector3(198.5f, 4.6f, 196.0f), new Vector3(198.5f, 6.1f, 196.0f), .15f, 10);
        Ventana(new Vector3(x, 1.25f, 193.0f), n, Vector3.back, 1.0f, .9f, 2, true);
        Rejilla(new Vector3(x, 0, 193.55f), new Vector3(x, 0, 192.45f), n, 1.25f, 2.15f);
        // Equipo blanco sobre su base, con el conducto que sube y entra en la pared.
        Box("Hormigon", x - 1.6f, 0, 188.8f, x - .1f, .15f, 190.8f);
        Box("PinturaBlanca", x - 1.4f, .15f, 189.0f, x - .3f, 1.1f, 190.5f);
        Box("Galvanizado", x - 1.05f, 1.1f, 189.15f, x - .6f, 2.75f, 189.6f);
        Box("Galvanizado", x - 1.05f, 2.3f, 189.15f, x, 2.75f, 189.6f);
        Cilindro("Galvanizado", new Vector3(x - .8f, 1.1f, 190.1f), new Vector3(x - .8f, 2.2f, 190.1f), .2f, 10);
        Cilindro("Galvanizado", new Vector3(x - .8f, 2.2f, 190.1f), new Vector3(x, 2.4f, 190.1f), .2f, 10);
        Aire(new Vector3(x - .15f, 2.1f, 191.6f), n, .8f); Aire(new Vector3(x - .15f, 2.8f, 187.4f), n, .8f);
        Box("PuertaGris", x - .12f, 1.0f, 183.3f, x, 1.45f, 183.65f);
        Box("Galvanizado", x - .05f, 1.45f, 183.42f, x, 3.2f, 183.5f);
        PuertaDoble(x, 185.0f, 2.0f, n, true);
        // Portón de persianas en un marco de ladrillo que sobresale, con su techito de chapa.
        Box("LadrilloChico", x - .45f, -.2f, 177.0f, x, 2.7f, 181.2f);
        PuertaPersiana(x - .45f, 177.5f, 180.7f, 2.3f, n, true);
        Placa("ChapaClara", new Vector3(x - 1.0f, 2.72f, 176.8f), new Vector3(0, 0, 4.6f), new Vector3(1.0f, .15f, 0), .03f);
    }

    // Cara norte, hacia el bloque chico: sencilla (no se llega).
    static void CaraNorte()
    {
        // Solo dentro del pasillo cerrado: del lado del paso la pared queda lisa.
        Ventanas(new Vector3(X0 + 1, 0, Z1), new Vector3(215.5f, 0, Z1), Vector3.forward, 2.0f, 1.4f, .8f, 5.0f);
    }

    // ---------------------------------------------------------------- Bloque chico

    // El bloque de las aulas anexas: ladrillo con el parapeto escalonado. En la cara que da al paso, los dos caños blancos
    // de extracción con los ventiladores en la ménsula, la ventana con malla verde, el caño de gas y la torre de antena.
    static void BloqueChico()
    {
        Box("LadrilloChico", BX0, -.2f, BZ0, BX1, 3.8f, BZ1); Copete(BX0, BZ0, BX1, 209.2f, 3.8f);
        Box("LadrilloChico", BX0, 3.7f, 209.2f, BX1, 4.6f, BZ1); Copete(BX0, 209.2f, BX1, BZ1, 4.6f);
        Box("LadrilloChico", BX1 - .45f, -.2f, BZ1 - .55f, BX1 + .06f, 4.85f, BZ1 + .06f);
        Box("PinturaBlanca", BX1 - .49f, 4.85f, BZ1 - .59f, BX1 + .1f, 4.92f, BZ1 + .1f);
        var n = Vector3.right; float x = BX1;
        // Ventana corrida con la malla verde por delante.
        Cinta(new Vector3(x, 0, 207.6f), new Vector3(x, 0, 212.0f), n, 1.35f, 2.4f, 2.4f, 1.1f, 0, 2002);
        for (int i = 0; i < 4; i++)
        {
            float z0 = 207.6f + i * 1.1f, z1 = z0 + 1.1f;
            Caja("RejaVerde", new Vector3(x + .14f, 1.87f, (z0 + z1) / 2), Vector3.forward * .53f, Vector3.up * .55f, Vector3.right * .01f);
            foreach (var z in new[] { z0 + .02f, z1 - .02f }) Box("VerdeOscuro", x + .1f, 1.3f, z - .03f, x + .17f, 2.45f, z + .03f);
            Box("VerdeOscuro", x + .1f, 1.3f, z0, x + .17f, 1.36f, z1); Box("VerdeOscuro", x + .1f, 2.39f, z0, x + .17f, 2.45f, z1);
        }
        // Ménsula con los dos ventiladores y los caños blancos que suben por encima del techo.
        Box("Galvanizado", x, 2.55f, 211.0f, x + .95f, 2.62f, 213.1f);
        foreach (var z in new[] { 212.3f, 213.05f }) Barra("Galvanizado", new Vector3(x, 1.95f, z), new Vector3(x + .9f, 2.55f, z), .05f);
        foreach (var (z, alto) in new[] { (211.55f, 6.4f), (212.55f, 6.9f) })
        {
            Cilindro("Galvanizado", new Vector3(x + .48f, 2.95f, z - .17f), new Vector3(x + .48f, 2.95f, z + .17f), .33f, 14);
            Box("Galvanizado", x + .3f, 2.62f, z - .2f, x + .66f, 3.0f, z + .2f);
            Cilindro("MetalNegro", new Vector3(x + .48f, 2.95f, z + .17f), new Vector3(x + .48f, 2.95f, z + .3f), .12f, 10);
            Cilindro("PinturaBlanca", new Vector3(x + .48f, 3.2f, z), new Vector3(x + .48f, alto, z), .16f, 12);
            Cilindro("PinturaBlanca", new Vector3(x + .48f, alto - .02f, z), new Vector3(x + .48f, alto + .06f, z), .19f, 12);
        }
        // Caño de gas amarillo, la cajita de luz y el gabinete de medidor al pie de la esquina.
        Cilindro("CordonAmarillo", new Vector3(x + .08f, 0, 213.5f), new Vector3(x + .08f, 2.95f, 213.5f), .03f, 8);
        Cilindro("CordonAmarillo", new Vector3(x + .08f, 2.95f, 213.5f), new Vector3(x, 2.95f, 213.5f), .03f, 8);
        Cilindro("CordonAmarillo", new Vector3(x + .08f, 0, 213.25f), new Vector3(x + .08f, 1.2f, 213.25f), .025f, 8);
        Box("PuertaGris", x, .35f, 212.7f, x + .06f, .6f, 213.0f);
        Box("Hormigon", x, 0, 214.0f, x + .6f, 1.0f, BZ1 + .05f);
        Box("PuertaGris", x + .6f, .25f, 214.15f, x + .62f, .85f, 214.75f);
        // Torre de antena reticulada, roja y blanca por tramos.
        var c = new Vector3(x + 1.0f, 0, BZ1 + .7f); const float lado = .45f, alto2 = 16f;
        var pies = new Vector3[3]; for (int i = 0; i < 3; i++) { float ang = i * Mathf.PI * 2 / 3; pies[i] = c + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * lado * .577f; }
        for (float y = 0; y < alto2 - .01f; y += 1f)
        {
            string mat = ((int)(y / 2)) % 2 == 0 ? "CartelRojo" : "PinturaBlanca";
            for (int i = 0; i < 3; i++)
            {
                var p0 = pies[i] + Vector3.up * y; var p1 = pies[(i + 1) % 3] + Vector3.up * y;
                Barra(mat, p0, p0 + Vector3.up, .035f);
                Barra(mat, p0 + Vector3.up, p1 + Vector3.up, .02f);
                Barra(mat, p0, p1 + Vector3.up, .015f);
            }
        }
        // Las otras caras, sencillas.
        Ventanas(new Vector3(BX0 + 1, 0, BZ0), new Vector3(BX1 - 1, 0, BZ0), Vector3.back, 1.2f, 1.4f, 1.0f, 5.0f);
    }

    // ---------------------------------------------------------------- Paso techado y escalera del 2b

    // Cara oeste del 2b (está girada): t va de su punta sur hacia el norte y «afuera» sale hacia el 2A.
    static readonly Vector2 B2Sur = new Vector2(235.0f, 201.1f), B2Norte = new Vector2(230.7f, 214.0f);
    static Vector3 B2(float t, float afuera, float y)
    {
        var d = (B2Norte - B2Sur).normalized; var o = new Vector2(-d.y, d.x); if (o.x > 0) o = -o;
        var p = B2Sur + d * t + o * afuera; return new Vector3(p.x, y, p.y);
    }

    // El paso techado sale derecho del 2b hacia el 2A y termina contra la pared del frente del 2A, sin pasar al otro
    // lado de su esquina; donde no hay pared del 2b se apoya en postes blancos.
    static void PasoTechado()
    {
        const float t0 = -1.6f, t1 = 2.8f, y2b = 2.98f, y2a = 2.86f, xa = X1 + .03f;
        var o = B2(0, 1, 0) - B2(0, 0, 0); o.y = 0; o.Normalize();
        Vector3 Lejos(float t, float y) { var p = B2(t, 0, 0); return p + o * ((xa - p.x) / o.x) + Vector3.up * y; }
        Losa4("ChapaOxido", B2(t0, 0, y2b + .12f), B2(t1, 0, y2b + .12f), Lejos(t1, y2a + .12f), Lejos(t0, y2a + .12f), .03f);
        Viga("PinturaBlanca", Lejos(t0, y2a), Lejos(t1, y2a), .14f, .07f);
        Viga("PinturaBlanca", B2(t0, .03f, y2b), B2(t1, .03f, y2b), .14f, .07f);
        foreach (var t in new[] { t0, (t0 + t1) / 2, t1 }) Viga("PinturaBlanca", B2(t, .05f, y2b), Lejos(t, y2a), .12f, .06f);
        foreach (var f in new[] { .05f, .55f })
        {
            var p = Vector3.Lerp(B2(t0, 0, 0), Lejos(t0, 0), f); p.y = 0;
            Cilindro("PinturaBlanca", p, p + Vector3.up * Mathf.Lerp(y2b, y2a, f), .06f, 10);
        }
    }

    // Losa fina con cuatro esquinas cualquiera (en el mismo plano), con sus cantos.
    static void Losa4(string mat, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float e)
    {
        var up = Vector3.up * e; var cen = (a + b + c + d) / 4;
        Quad(mat, a + up, b + up, c + up, d + up, Vector3.up);
        Quad(mat, a, b, c, d, Vector3.down);
        void Lado(Vector3 p, Vector3 q) { var m = (p + q) / 2 - cen; m.y = 0; Quad(mat, p, q, q + up, p + up, m); }
        Lado(a, b); Lado(b, c); Lado(c, d); Lado(d, a);
    }

    // El pasillo entre el 2A y el bloque chico está cerrado (por ahí no se va). Del lado del paso, la jaula de malla
    // blanca de la foto: el frente de paños, la puerta en el costado en diagonal hacia la pared del 2A y el techito de
    // chapa que sobresale por delante. Del otro lado, una reja simple de la misma malla.
    // Panel negro sin luz, como objeto propio (para moverlo o ajustarlo solo): una caja de min a max, sin colisión.
    static void Fondo(Transform grupo, string nombre, Vector3 min, Vector3 max)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = nombre;
        UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(grupo, false); g.transform.localPosition = (min + max) / 2; g.transform.localScale = max - min;
        g.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Uni/Uni_Negro.mat");
    }

    static void CerrarPasaje(Transform grupo)
    {
        const float h = 2.45f, xf = BX1 + .15f, zp = Z1 + .85f, xp = xf - .65f;
        void Panel(Vector3 a, Vector3 b, int panos, bool puerta)
        {
            var d = b - a; float L = d.magnitude; var r = d / L; var n = Vector3.Cross(Vector3.up, r).normalized; var up = Vector3.up;
            Caja("MallaBlanca", (a + b) / 2 + up * (h / 2 + .03f), r * L / 2, up * (h / 2 - .05f), n * .012f);
            foreach (var y in new[] { .05f, 1.25f, h - .03f }) Barra("PinturaBlanca", a + up * y, b + up * y, .05f);
            for (int i = 0; i <= panos; i++) { var p = Vector3.Lerp(a, b, (float)i / panos); Barra("PinturaBlanca", p, p + up * h, .06f); }
            if (puerta) { var m = Vector3.Lerp(a, b, .7f); Caja("MetalNegro", m + up * 1.1f + n * .05f, r * .02f, up * .06f, n * .04f); }
        }
        var A = new Vector3(xp, 0, Z1); var B = new Vector3(xf, 0, zp); var C = new Vector3(xf, 0, BZ0);
        Panel(B, C, 2, false);
        Panel(A, B, 1, true);
        // Techito de chapa: de adentro del pasillo baja hacia adelante y sobresale.
        Placa("ChapaClara", new Vector3(xp - 1.4f, h + .3f, Z1), new Vector3(0, 0, BZ0 - Z1), new Vector3(xf + .35f - (xp - 1.4f), -.22f, 0), .03f);
        Box("PinturaBlanca", xf + .3f, h + .02f, Z1 - .02f, xf + .37f, h + .14f, BZ0 + .02f);
        var g = new GameObject("Bloqueo_PasajeEste"); g.transform.SetParent(grupo, false);
        var bc = g.AddComponent<BoxCollider>(); bc.center = new Vector3(xp - .2f, 1.75f, (Z1 + BZ0) / 2); bc.size = new Vector3(.5f, 3.5f, BZ0 - Z1);
        // Detrás de la jaula, un panel negro sin luz hasta el techito: no se ve el pasillo de atrás.
        float xn = xp - .5f, yn = h + .3f - .22f * (xn - (xp - 1.4f)) / (xf + .35f - (xp - 1.4f)) - .03f;
        Fondo(grupo, "Fondo negro (jaula)", new Vector3(xn - .02f, -.2f, Z1), new Vector3(xn, yn, BZ0));
        // El pasillo desemboca en el callejón de la biblioteca (entre su bloque de baños y el anexo del 2A). Como en la
        // foto, el callejón se cierra al fondo con la reja de malla y su portón, de la esquina de los baños al anexo, y
        // atrás queda el panel negro sin luz. El caminito de hormigón llega por el callejón hasta el portón.
        const float zr = 200.0f, xb = 182.0f, xa = X0 - 3.3f;
        Panel(new Vector3(xb, 0, zr), new Vector3(xb + 1.3f, 0, zr), 1, false);
        Panel(new Vector3(xb + 1.3f, 0, zr), new Vector3(xa, 0, zr), 1, true);
        Fondo(grupo, "Fondo negro (callejón)", new Vector3(xb - .02f, -.2f, 201.3f), new Vector3(xa + .02f, 2.6f, 201.32f));
        var g2 = new GameObject("Bloqueo_Callejon"); g2.transform.SetParent(grupo, false);
        var bc2 = g2.AddComponent<BoxCollider>(); bc2.center = new Vector3((xb + xa) / 2, 1.75f, zr + .1f); bc2.size = new Vector3(xa - xb, 3.5f, .5f);
        Box("Vereda", xa - 1.85f, 0, 192.4f, xa - .6f, .05f, zr);
    }

    // Escalera de emergencia del 2b (foto del paso techado), pegada a su cara oeste: el tramo de abajo sube junto a la
    // pared hasta el descanso del medio y el de arriba vuelve, al lado, hasta el descanso de arriba, donde está la puerta
    // al piso de arriba con su marco y su techito. Pisos de rejilla, columnas blancas y barandas apoyadas en sus postes.
    // Se ve pero no se sube.
    static void EscaleraDel2b(Transform grupo)
    {
        const float tAbajo = 10.8f, tMedio = 6.6f, yM = 2.4f, yA = 4.8f, ancho = 2.4f;
        const float a0 = .12f, a1 = 1.18f, a2 = 1.32f, a3 = 2.38f;
        var dtt = (B2(1, 0, 0) - B2(0, 0, 0)).normalized; var nf = (B2(0, 1, 0) - B2(0, 0, 0)).normalized;
        void Baranda(Vector3 a, Vector3 b)
        {
            Cilindro("PinturaBlanca", a + Vector3.up * 1.0f, b + Vector3.up * 1.0f, .025f, 8);
            Cilindro("PinturaBlanca", a + Vector3.up * .5f, b + Vector3.up * .5f, .018f, 8);
            int k = Mathf.Max(1, Mathf.RoundToInt((b - a).magnitude / 1.2f));
            for (int i = 0; i <= k; i++) { var p = Vector3.Lerp(a, b, (float)i / k); Barra("PinturaBlanca", p, p + Vector3.up * 1.02f, .035f); }
        }
        void Tramo(float tIni, float yIni, float tFin, float yFin, float o0, float o1)
        {
            int k = Mathf.RoundToInt(Mathf.Abs(yFin - yIni) / .185f);
            for (int i = 0; i < k; i++)
            {
                float f = (i + .5f) / k, t = Mathf.Lerp(tIni, tFin, f), y = Mathf.Lerp(yIni, yFin, f);
                var c = (B2(t, o0, y) + B2(t, o1, y)) / 2;
                Caja("Galvanizado", c, dtt * Mathf.Abs(tFin - tIni) / k * .45f, Vector3.up * .02f, nf * (o1 - o0) / 2);
            }
            foreach (var o in new[] { o0, o1 })
            {
                Viga("PinturaBlanca", B2(tIni, o, yIni - .1f), B2(tFin, o, yFin - .1f), .2f, .05f);
                Baranda(B2(tIni, o, yIni), B2(tFin, o, yFin));
            }
        }
        void Descanso(float tIni, float tFin, float y)
        {
            var c = (B2(tIni, 0, y) + B2(tFin, ancho, y)) / 2;
            Caja("Galvanizado", c - Vector3.up * .04f, dtt * (tFin - tIni) / 2, Vector3.up * .04f, nf * ancho / 2);
            Viga("PinturaBlanca", B2(tIni, ancho - .04f, y - .16f), B2(tFin, ancho - .04f, y - .16f), .2f, .08f);
            Viga("PinturaBlanca", B2(tIni, .04f, y - .16f), B2(tFin, .04f, y - .16f), .2f, .08f);
            Viga("PinturaBlanca", B2(tIni + .04f, 0, y - .16f), B2(tIni + .04f, ancho, y - .16f), .2f, .08f);
            Viga("PinturaBlanca", B2(tFin - .04f, 0, y - .16f), B2(tFin - .04f, ancho, y - .16f), .2f, .08f);
            foreach (var t in new[] { tIni + .08f, tFin - .08f }) Cilindro("PinturaBlanca", B2(t, ancho - .08f, 0), B2(t, ancho - .08f, y - .1f), .07f, 10);
        }
        // Descanso del medio: llegan los dos tramos por su lado norte; baranda en el sur y en el borde de afuera.
        Descanso(tMedio - 1.0f, tMedio, yM);
        Baranda(B2(tMedio - 1.0f, 0, yM), B2(tMedio - 1.0f, ancho, yM));
        Baranda(B2(tMedio - 1.0f, ancho, yM), B2(tMedio, ancho, yM));
        // Descanso de arriba: llega el tramo de arriba por el sur; barandas en el resto de los bordes libres.
        Descanso(tAbajo, tAbajo + 1.2f, yA);
        Baranda(B2(tAbajo, ancho, yA), B2(tAbajo + 1.2f, ancho, yA));
        Baranda(B2(tAbajo + 1.2f, 0, yA), B2(tAbajo + 1.2f, ancho, yA));
        Baranda(B2(tAbajo, 0, yA), B2(tAbajo, a2 - .02f, yA));
        // Los dos tramos, lado a lado: el de abajo junto a la pared y el de arriba por afuera.
        Tramo(tAbajo, 0, tMedio, yM, a0, a1);
        Tramo(tMedio, yM, tAbajo, yA, a2, a3);
        // Puerta al piso de arriba del 2b: hoja gris con mirilla, marco blanco, picaporte y techito de chapa.
        float tp = tAbajo + .6f;
        Caja("PuertaGris", B2(tp, .05f, yA + 1.02f), dtt * .46f, Vector3.up * 1.02f, nf * .05f);
        Caja("Vidrio", B2(tp, .1f, yA + 1.55f), dtt * .12f, Vector3.up * .25f, nf * .01f);
        Caja("PinturaBlanca", B2(tp, .06f, yA + 2.1f), dtt * .56f, Vector3.up * .06f, nf * .06f);
        foreach (var sg in new[] { -1f, 1f }) Caja("PinturaBlanca", B2(tp + sg * .52f, .06f, yA + 1.06f), dtt * .05f, Vector3.up * 1.06f, nf * .06f);
        Caja("MetalNegro", B2(tp + .3f, .13f, yA + 1.0f), dtt * .07f, Vector3.up * .02f, nf * .03f);
        var o0 = B2(tp - .75f, 0, yA + 2.3f); var u = B2(tp + .75f, 0, yA + 2.3f) - o0;
        Placa("ChapaClara", o0, u, B2(tp - .75f, .9f, yA + 2.18f) - o0, .03f);
        // Bloqueo invisible: la escalera no se sube.
        var g = new GameObject("Bloqueo_Escalera2b"); g.transform.SetParent(grupo, false);
        var mid = (B2(tMedio - 1.0f, 0, 0) + B2(tAbajo + 1.2f, ancho, 0)) / 2;
        g.transform.localPosition = mid + Vector3.up * 3.0f; g.transform.localRotation = Quaternion.LookRotation(dtt);
        var bc = g.AddComponent<BoxCollider>(); bc.size = new Vector3(ancho + .3f, 6.0f, tAbajo + 1.2f - (tMedio - 1.0f) + .3f);
    }

    static void MaterialColor(string nombre, Color color, float brillo)
    {
        string path = "Assets/Materials/Uni/Uni_" + nombre + ".mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Uni_" + nombre };
        m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", brillo);
        AssetDatabase.CreateAsset(m, path);
    }

    // ---------------------------------------------------------------- Terreno

    // Saca el pasto de los pisos nuevos y pinta de ripio la franja bajo el alero largo del costado sur.
    static void AjustarTerreno(Transform mapa)
    {
        var terreno = Terrain.activeTerrain; if (terreno == null) return;
        var td = terreno.terrainData; var o = terreno.transform.position;
        bool En(Vector2 q, float x0, float z0, float x1, float z1) => q.x >= x0 && q.x <= x1 && q.y >= z0 && q.y <= z1;
        bool Ripio(Vector2 q) => En(q, X0 - .2f, Z0 - Sale - .1f, X1 + Sale, Z0);
        bool SinPasto(Vector2 q) => Ripio(q) || En(q, X1, Z0 - Sale, X1 + Sale + .1f, Z1 + .1f) || En(q, X1 + Sale, 180.9f, 244.7f, 182.7f)
            || En(q, X0 - 5.2f, 192.3f, X0 - 3.85f, 200.1f) || En(q, X0 - 1.7f, 188.7f, X0, 190.9f) || En(q, X0 - 1.1f, 176.7f, X0, 181.5f) || En(q, 232.9f, 183.4f, 234.7f, 184.7f);
        var w0 = mapa.TransformPoint(new Vector3(X0 - 6, 0, Z0 - 4)); var w1 = mapa.TransformPoint(new Vector3(246f, 0, 203f));
        int res = td.detailResolution;
        int i0 = Mathf.Clamp(Mathf.FloorToInt((w0.x - o.x) / td.size.x * res), 0, res - 1), i1 = Mathf.Clamp(Mathf.CeilToInt((w1.x - o.x) / td.size.x * res), 0, res - 1);
        int j0 = Mathf.Clamp(Mathf.FloorToInt((w0.z - o.z) / td.size.z * res), 0, res - 1), j1 = Mathf.Clamp(Mathf.CeilToInt((w1.z - o.z) / td.size.z * res), 0, res - 1);
        for (int capa = 0; capa < td.detailPrototypes.Length; capa++)
        {
            var datos = td.GetDetailLayer(i0, j0, i1 - i0 + 1, j1 - j0 + 1, capa); bool cambio = false;
            for (int j = 0; j <= j1 - j0; j++)
                for (int i = 0; i <= i1 - i0; i++)
                {
                    if (datos[j, i] == 0) continue;
                    var l = mapa.InverseTransformPoint(new Vector3(o.x + (i0 + i + .5f) / res * td.size.x, 0, o.z + (j0 + j + .5f) / res * td.size.z));
                    if (SinPasto(new Vector2(l.x, l.z))) { datos[j, i] = 0; cambio = true; }
                }
            if (cambio) td.SetDetailLayer(i0, j0, capa, datos);
        }
        int ar = td.alphamapResolution, capas = td.alphamapLayers;
        if (capas > 2)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt((w0.x - o.x) / td.size.x * ar), 0, ar - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((w1.x - o.x) / td.size.x * ar), 0, ar - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt((w0.z - o.z) / td.size.z * ar), 0, ar - 1), y1 = Mathf.Clamp(Mathf.CeilToInt((w1.z - o.z) / td.size.z * ar), 0, ar - 1);
            var al = td.GetAlphamaps(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            for (int j = 0; j <= y1 - y0; j++)
                for (int i = 0; i <= x1 - x0; i++)
                {
                    var l = mapa.InverseTransformPoint(new Vector3(o.x + (x0 + i + .5f) / ar * td.size.x, 0, o.z + (y0 + j + .5f) / ar * td.size.z));
                    if (!Ripio(new Vector2(l.x, l.z))) continue;
                    for (int c = 0; c < capas; c++) al[j, i, c] = c == 2 ? 1 : 0;
                }
            td.SetAlphamaps(x0, y0, al);
        }
        EditorUtility.SetDirty(td);
    }

    // ---------------------------------------------------------------- Armado

    static readonly string[] CaminosQuitados = { "PasajeAulasAnexas" };

    [MenuItem("Riftwalker/Universidad/Armar Módulo 2A")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/universidadMAPA.unity" || EditorApplication.isPlaying) throw new InvalidOperationException("Abrir universidadMAPA fuera de Play.");
        GameObject mapaGo = null;
        foreach (var g in scene.GetRootGameObjects()) if (g.name == "Mapa (escala 1,25)") mapaGo = g;
        if (mapaGo == null) throw new InvalidOperationException("No se encontró Mapa (escala 1,25).");
        MaterialColor("Corcho", new Color(.82f, .66f, .42f), .1f);
        if (AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Uni/Uni_Negro.mat") == null)
        {
            // Negro sin iluminación: tapa lo de atrás sin que la luz lo aclare.
            var mn = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Uni_Negro" };
            mn.SetColor("_BaseColor", Color.black);
            AssetDatabase.CreateAsset(mn, "Assets/Materials/Uni/Uni_Negro.mat");
        }
        if (AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Uni/Uni_MallaBlanca.mat") == null)
        {
            var mb = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Uni/Uni_RejaMalla.mat")) { name = "Uni_MallaBlanca" };
            mb.SetColor("_BaseColor", new Color(.9f, .91f, .9f));
            AssetDatabase.CreateAsset(mb, "Assets/Materials/Uni/Uni_MallaBlanca.mat");
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Uni/Carteles/CartelModulo.prefab") == null) throw new InvalidOperationException("Falta el prefab CartelModulo (se crea al armar el Módulo 3).");
        var mapa = mapaGo.transform;
        // Si la escena ya tenía cambios sin guardar, se arma igual pero no se guarda: lo decide quien la está editando.
        bool habiaCambios = scene.isDirty;
        lotes.Clear();
        var anterior = mapa.Find(Grupo); if (anterior != null) UnityEngine.Object.DestroyImmediate(anterior.gameObject);
        if (AssetDatabase.LoadMainAssetAtPath(MeshPath) != null) AssetDatabase.DeleteAsset(MeshPath);
        // Las cajas provisorias quedan apagadas, como las del Módulo 3.
        var sinDetalle = mapa.Find("Ambiente_EdificiosSinDetalle");
        foreach (var n in CajasViejas) { var t = sinDetalle != null ? sinDetalle.Find(n) : null; if (t != null) t.gameObject.SetActive(false); }
        // El camino que cruzaba el pasillo cerrado ya no va: por ahí no se pasa.
        foreach (var n in CaminosQuitados) { var t = mapa.Find("Ambiente_CaminosNuevos/" + n); if (t != null) t.gameObject.SetActive(false); }
        var go = new GameObject(Grupo); go.transform.SetParent(mapa, false);
        var g2 = go.transform;
        Modulo2A(g2); BloqueChico(); PasoTechado(); EscaleraDel2b(g2); CerrarPasaje(g2);
        AjustarTerreno(mapa);
        var contenedor = new Mesh { name = "Modulo2A" }; AssetDatabase.CreateAsset(contenedor, MeshPath);
        var mallas = new GameObject("Modulo2A"); mallas.transform.SetParent(g2, false);
        foreach (var kv in lotes)
        {
            var l = kv.Value; if (l.v.Count == 0) continue;
            var mesh = new Mesh { name = "M2A_" + kv.Key, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(l.v); mesh.SetUVs(0, l.uv); mesh.SetTriangles(l.t, 0); mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            AssetDatabase.AddObjectToAsset(mesh, contenedor);
            var hijo = new GameObject(kv.Key); hijo.transform.SetParent(mallas.transform, false);
            hijo.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = hijo.AddComponent<MeshRenderer>(); mr.sharedMaterial = l.mat;
            if (!SinColision.Contains(kv.Key)) hijo.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
        lotes.Clear();
        EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
        if (!habiaCambios) EditorSceneManager.SaveScene(scene);
        Debug.Log(habiaCambios ? "Módulo 2A armado (la escena tenía cambios sin guardar: no se guardó)." : "Módulo 2A armado.");
    }
}
