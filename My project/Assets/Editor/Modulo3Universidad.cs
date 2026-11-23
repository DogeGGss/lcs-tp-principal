using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Módulo 3 en el mapa de la universidad (US 339), armado según las fotos y el plano que se acordó:
// 1. La cara del 3d al ripio: el muro de ladrillo con la boca del pasillo, los carteles «3d laboratorio de ingeniería»
//    y «3a aulas 3001 a 3014», el gabinete rojo, los tableros, los reflectores y la cámara.
// 2. El pasillo del 3d: techado, con piso de granito bordó, abierto al patio. Al norte, las aulas chicas con las puertas
//    cerradas; al sur, junto al TUM, la puerta al TUM (cerrada). Al final, la rampa con tres barandas y las bandas
//    amarillas, que termina donde empieza la pared del 3C, y la pila de sillas que corta el paso.
// 3. Las paredes del patio: la del 3C con la ventana corrida; del lado del TUM, la plataforma de los aires y el contenedor.
// 4. El frente del 3C hacia el Conector_1: la entrada vidriada alta, el cartel y el gabinete rojo; en la esquina oeste,
//    la galería de policarbonato con el cartel de salida de emergencia.
// 5. La reja verde con el portón encadenado, de la esquina del 3C hasta el 3B: cierra el patio interno.
// 6. La cara sur del 3B con el mural y, en la esquina, el cartel «3b aulas 3015 a 3019» y la entrada bajo la pérgola.
// 7. El seto alto de cañas, del 2A al 3B, que cierra el paso de atrás.
// 8. La cara del 3a al estacionamiento: la ventana corrida de punta a punta, con el alero de chapa.
// Las caras sin foto van sencillas (ladrillo y ventanas simples). Sin interiores: los vidrios son opacos.
// Medidas en metros, en el espacio de «Mapa (escala 1,25)». Se puede volver a correr: rehace el grupo Ambiente_Modulo3.
public static class Modulo3Universidad
{
    const string MeshPath = "Assets/Meshes/Uni/Modulo3.asset", Grupo = "Ambiente_Modulo3";
    static readonly string[] CajasViejas = { "Modulo3A", "Modulo3A_Pasillo3B", "Modulo3B", "Modulo3B_Punta", "Modulo3C" };
    static readonly HashSet<string> SinColision = new HashSet<string> { "Policarbonato", "Canas", "SillaAzul", "MetalNegro" };

    // 3A: aulas del 3a y, en la punta este, las aulas chicas y el pasillo del 3d.
    const float AX0 = 215.7f, AX1 = 362.5f, AZ0 = 281.3f, AZ1 = 295.8f;
    const float Muro = 3.7f, Alero = 3.62f, Cumbrera = 4.62f, Parapeto = 4.7f;
    // Pasillo del 3d: de la pila de sillas (PX0) a la cara del ripio; junto al TUM se angosta hasta TumZ.
    const float PX0 = 319.3f, PX1 = 361.7f, PZ1 = 285.6f, TumX = 345.3f, TumZ = 282.4f;
    const float RampaX0 = 320.0f, RampaX1 = 326.5f, PisoBajo = .12f, PisoAlto = .42f, Losa = 3.0f;
    // Pasillo de adentro, detrás de las sillas: sigue por el 3A y se pierde en lo oscuro (no se entra).
    const float IX0 = 307.3f, IZ0 = 282.0f, IZ1 = 285.0f, ITecho = 2.9f;
    // 3C: el cuerpo es más bajo que el 3A. Al frente, los dos muros tienen la altura de las otras paredes (siguen la
    // pendiente del techo); la entrada vidriada es un poco más alta adelante y su techo baja hacia atrás, así que a los
    // costados queda un pico de vidrio por donde entra la luz.
    const float CX0 = 283.7f, CX1 = 319.3f, CZ0 = 241.8f, CZ1 = 281.3f;
    const float CMuro = 3.58f, CAlero = 3.55f, CCumbrera = 4.1f, VidrieraFrente = 4.6f, VidrieraFondo = 2.6f;

    // Altura del techo del 3C (a dos aguas, cumbrera de norte a sur) en cada x.
    static float TechoC(float x)
    {
        float xc = (CX0 + CX1) / 2, a = CX0 - .6f, b = CX1 + .8f;
        return x <= xc ? CAlero + (x - a) / (xc - a) * (CCumbrera - CAlero) : CAlero + (b - x) / (b - xc) * (CCumbrera - CAlero);
    }
    // Reja del patio interno, del 3B a la esquina del 3C, con el portón.
    const float RejaZ = 244.0f, RejaX0 = 260.6f, RejaX1 = 283.7f, PortonX0 = 277.3f, PortonX1 = 280.3f;
    // Seto de cañas: de la esquina del 2A a la esquina del 3B.
    static readonly Vector2 SetoA = new Vector2(217.9f, 215.0f), SetoB = new Vector2(225.3f, 232.0f);

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
    static readonly Dictionary<string, float> EscalaUV = new Dictionary<string, float> { { "RejaVerde", 1f }, { "RejaMalla", 1f }, { "GranitoBordo", 1.25f } };

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

    // ---------------------------------------------------------------- 3A

    static void Modulo3A()
    {
        // Cuerpo macizo: aulas del 3a y, al este, las aulas chicas del 3d (el pasillo queda afuera, al sur). Detrás de
        // las sillas queda el hueco del pasillo de adentro.
        Box("LadrilloChico", AX0, -.2f, AZ0, IX0, Muro, AZ1);
        Box("LadrilloChico", IX0, -.2f, IZ1, PX0, Muro, AZ1);
        Box("LadrilloChico", IX0, -.2f, AZ0, PX0, Muro, IZ0);
        Box("LadrilloChico", IX0, ITecho, IZ0, PX0, Muro, IZ1);
        Box("LadrilloChico", PX0, -.2f, PZ1, PX1, Muro, AZ1);
        // Chapa a dos aguas con la cumbrera a lo largo y aleros de 0,8 m; al este lo tapa el muro alto del 3d.
        float zc = (AZ0 + AZ1) / 2, xa = AX0 - .45f;
        var largo = new Vector3(PX1 - xa, 0, 0);
        Placa("ChapaClara", new Vector3(xa, Alero, AZ1 + .8f), largo, new Vector3(0, Cumbrera - Alero, zc - (AZ1 + .8f)), .05f);
        Placa("ChapaClara", new Vector3(xa, Alero, AZ0 - .7f), largo, new Vector3(0, Cumbrera - Alero, zc - (AZ0 - .7f)), .05f);
        Box("ChapaClara", xa, Cumbrera - .02f, zc - .16f, PX1, Cumbrera + .05f, zc + .16f);
        Box("PinturaBlanca", xa, Alero - .2f, AZ1 + .77f, PX1, Alero + .03f, AZ1 + .81f);
        Box("PinturaBlanca", xa, Alero - .2f, AZ0 - .71f, PX0, Alero + .03f, AZ0 - .67f);
        Tri("LadrilloChico", new Vector3(AX0, Muro - .05f, AZ0), new Vector3(AX0, Muro - .05f, AZ1), new Vector3(AX0, Cumbrera - .03f, zc), Vector3.left);
        // Cara al estacionamiento (foto del cartel 3a): base de ladrillo y ventana corrida de punta a punta, con banda
        // blanca arriba, debajo del alero; algunas hojas de abajo abiertas.
        Cinta(new Vector3(AX0 + .7f, 0, AZ1), new Vector3(PX1 - .25f, 0, AZ1), Vector3.forward, 1.05f, 3.28f, 2.3f, 1.52f, 9, 3001);
        Box("PinturaBlanca", AX0 + .7f, 3.28f, AZ1, PX1 - .25f, Muro, AZ1 + .03f);
        // Bajadas de agua de cobre en las puntas.
        Cilindro("ChapaOxido", new Vector3(PX1 + .2f, .05f, AZ1 + .1f), new Vector3(PX1 + .2f, Alero, AZ1 + .1f), .045f, 8);
        Cilindro("ChapaOxido", new Vector3(AX0 + .35f, .05f, AZ1 + .1f), new Vector3(AX0 + .35f, Alero, AZ1 + .1f), .045f, 8);
        // Caras sin foto: ventanas simples.
        Ventanas(new Vector3(AX0, 0, AZ0 + .8f), new Vector3(AX0, 0, AZ1 - .8f), Vector3.left, 1.1f, 1.8f, 1.3f, 4.5f);
        Ventanas(new Vector3(AX0 + .5f, 0, AZ0), new Vector3(236.0f, 0, AZ0), Vector3.back, 1.1f, 1.8f, 1.3f, 3.6f);
        Ventanas(new Vector3(243.6f, 0, AZ0), new Vector3(CX0 - .3f, 0, AZ0), Vector3.back, 1.1f, 1.8f, 1.3f, 3.6f);
    }

    // ---------------------------------------------------------------- 3d: cara al ripio y pasillo

    static float PisoY(float x) => x >= RampaX1 ? PisoBajo : x <= RampaX0 ? PisoAlto : PisoBajo + (RampaX1 - x) / (RampaX1 - RampaX0) * (PisoAlto - PisoBajo);

    static void Fachada3d(Transform grupo)
    {
        // Muro alto de ladrillo con la boca del pasillo y la viga de hormigón encima.
        Box("LadrilloChico", PX1, -.2f, AZ0, AX1, Parapeto, TumZ);
        Box("LadrilloChico", PX1, -.2f, PZ1, AX1, Parapeto, AZ1);
        Box("LadrilloChico", PX1, 2.85f, TumZ, AX1, Parapeto, PZ1);
        Box("Hormigon", PX1 - .02f, 2.62f, TumZ - .05f, AX1 + .02f, 2.85f, PZ1 + .05f);
        Box("Galvanizado", PX1 - .05f, Parapeto, AZ0 - .05f, AX1 + .08f, Parapeto + .05f, AZ1 + .05f);
        var n = Vector3.right; float x = AX1;
        // Carteles: el del 3d en el pilar, al lado de la boca; el del 3a en la esquina del estacionamiento.
        ColocarCartel(grupo, "Cartel 3d", new Vector3(x, 0, 282.0f), n, "3d", "laboratorio\nde\ningeniería", 1.4f, 1.6f);
        ColocarCartel(grupo, "Cartel 3a", new Vector3(x, 0, 294.9f), n, "3a", "aulas\n3001 a 3014", .8f, 1.75f);
        // Gabinete rojo de la manguera con su caño, y los tableros eléctricos sobre una base de ladrillo.
        Box("CartelRojo", x, 1.55f, 286.35f, x + .2f, 2.15f, 286.95f);
        Box("PinturaBlanca", x + .2f, 1.6f, 286.42f, x + .205f, 2.1f, 286.88f);
        Cilindro("CartelRojo", new Vector3(x + .1f, 0, 286.2f), new Vector3(x + .1f, 1.62f, 286.2f), .045f, 8);
        Box("LadrilloChico", x, -.2f, 287.15f, x + .28f, .55f, 288.65f);
        Box("PuertaGris", x, .55f, 287.2f, x + .26f, 1.32f, 287.95f);
        Box("PuertaGris", x, .55f, 288.05f, x + .22f, 1.2f, 288.6f);
        Box("MetalNegro", x + .22f, .65f, 288.12f, x + .225f, 1.1f, 288.53f);
        Box("PuertaGris", x, .78f, 293.25f, x + .08f, .95f, 293.45f);
        // Reflectores arriba, en las dos puntas, y la cámara.
        foreach (var z in new[] { 281.6f, 295.4f })
        {
            Barra("MetalNegro", new Vector3(x, 4.45f, z), new Vector3(x + .25f, 4.45f, z), .04f);
            Caja("MetalNegro", new Vector3(x + .32f, 4.42f, z), new Vector3(.08f, -.04f, 0), new Vector3(.04f, .08f, 0), Vector3.forward * .16f);
        }
        Barra("PinturaBlanca", new Vector3(x, 4.5f, 289.6f), new Vector3(x + .2f, 4.5f, 289.6f), .05f);
        Box("PinturaBlanca", x + .15f, 4.42f, 289.5f, x + .38f, 4.55f, 289.7f);
        // Vereda de hormigón de la boca al ripio.
        Box("HormigonViejo", AX1, 0, 281.9f, AX1 + 1.7f, .06f, 286.1f);
    }

    static void Pasillo3d(Transform grupo)
    {
        // Piso de granito bordó, con la rampa al fondo (sube hacia el oeste) y el descanso donde están las sillas.
        Box("GranitoBordo", RampaX1, 0, AZ0, TumX, PisoBajo, PZ1);
        Box("GranitoBordo", TumX, 0, TumZ, AX1, PisoBajo, PZ1);
        Box("GranitoBordo", PX0, 0, AZ0, RampaX0, PisoAlto, PZ1);
        Quad("GranitoBordo", new Vector3(RampaX0, PisoAlto, AZ0), new Vector3(RampaX1, PisoBajo, AZ0), new Vector3(RampaX1, PisoBajo, PZ1), new Vector3(RampaX0, PisoAlto, PZ1), Vector3.up);
        Quad("GranitoBordo", new Vector3(RampaX0, 0, AZ0), new Vector3(RampaX1, 0, AZ0), new Vector3(RampaX1, PisoBajo, AZ0), new Vector3(RampaX0, PisoAlto, AZ0), Vector3.back);
        // Bandas amarillas de prevención al pie y arriba de la rampa.
        Box("CordonAmarillo", RampaX1, PisoBajo, AZ0 + .05f, RampaX1 + .6f, PisoBajo + .012f, PZ1 - .05f);
        float y0 = PisoY(RampaX0 + .6f);
        Quad("CordonAmarillo", new Vector3(RampaX0, PisoAlto + .012f, AZ0 + .05f), new Vector3(RampaX0 + .6f, y0 + .012f, AZ0 + .05f), new Vector3(RampaX0 + .6f, y0 + .012f, PZ1 - .05f), new Vector3(RampaX0, PisoAlto + .012f, PZ1 - .05f), Vector3.up);
        // Las tres barandas galvanizadas de la rampa: la del borde del patio, la del medio y la de la pared.
        foreach (var z in new[] { AZ0 + .2f, 283.45f, PZ1 - .2f }) Baranda(z, RampaX0 + .3f, RampaX1 + .35f);
        // Losa de hormigón del techo, apoyada del lado del patio en columnas blancas redondas; arriba, el frente blanco.
        Box("Hormigon", PX0, Losa, AZ0 - .3f, TumX, Losa + .2f, PZ1);
        Box("Hormigon", TumX, Losa, TumZ, PX1, Losa + .2f, PZ1);
        Box("PinturaBlanca", PX0, Losa, AZ0 - .34f, TumX, Alero + .05f, AZ0 - .28f);
        foreach (var xc in new[] { 321.2f, 327.6f, 334.0f, 340.4f })
            Cilindro("PinturaBlanca", new Vector3(xc, 0, AZ0 - .14f), new Vector3(xc, Losa, AZ0 - .14f), .09f, 12);
        for (float xc = 322f; xc < PX1 - .5f; xc += 4f) Box("PinturaBlanca", xc - .6f, Losa - .06f, 283.4f, xc + .6f, Losa, 283.5f);
        // Junto al TUM, el muro del pasillo con la puerta al TUM (cerrada).
        Box("LadrilloChico", TumX, -.2f, AZ0, PX1, Muro, TumZ);
        Puerta(353.3f, TumZ, Vector3.forward, 1.0f, "ColumnaGris");
        // Pared de las aulas chicas: revoque blanco, puertas cerradas y ventanas altas.
        Box("RevoqueBlanco", PX0, 0, PZ1 - .03f, PX1, Losa, PZ1);
        foreach (var xc in new[] { 330.0f, 336.5f, 343.0f, 349.5f, 356.0f }) Puerta(xc, PZ1 - .03f, Vector3.back, .9f, "PuertaGris");
        foreach (var xc in new[] { 323.6f, 333.25f, 339.75f, 346.25f, 352.75f, 359.0f })
            Ventana(new Vector3(xc, 1.95f, PZ1 - .03f), Vector3.back, Vector3.right, 1.5f, .8f, 2, false);
        // Al final, la pila de sillas que corta el paso hacia el resto del 3A, y un bloqueo invisible. Detrás, el pasillo
        // de adentro: piso, paredes y techo que se van a negro, sin detalle.
        Vector2[] Uv(float a, float b) => new[] { new Vector2(a, .5f), new Vector2(b, .5f), new Vector2(b, .5f), new Vector2(a, .5f) };
        // Las caras van 2 cm adentro del ladrillo para que no titilen con él.
        float x0 = IX0 + .02f, z0 = IZ0 + .02f, z1 = IZ1 - .02f, yt = ITecho - .02f, yp = PisoAlto + .005f;
        Quad("PisoFundido", new Vector3(PX0, yp, z0), new Vector3(x0, yp, z0), new Vector3(x0, yp, z1), new Vector3(PX0, yp, z1), Vector3.up, Uv(0, 1));
        Quad("PasilloFundido", new Vector3(PX0, yt, z0), new Vector3(x0, yt, z0), new Vector3(x0, yt, z1), new Vector3(PX0, yt, z1), Vector3.down, Uv(0, 1));
        Quad("PasilloFundido", new Vector3(PX0, yp, z1), new Vector3(x0, yp, z1), new Vector3(x0, yt, z1), new Vector3(PX0, yt, z1), Vector3.back, Uv(0, 1));
        Quad("PasilloFundido", new Vector3(PX0, yp, z0), new Vector3(x0, yp, z0), new Vector3(x0, yt, z0), new Vector3(PX0, yt, z0), Vector3.forward, Uv(0, 1));
        Quad("PasilloFundido", new Vector3(x0, yp, z0), new Vector3(x0, yp, z1), new Vector3(x0, yt, z1), new Vector3(x0, yt, z0), Vector3.right, Uv(1, 1));
        var pila = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPila), grupo);
        pila.transform.localPosition = new Vector3(PX0, PisoAlto, (AZ0 + PZ1) / 2); pila.transform.localRotation = Quaternion.identity;
    }

    static void Puerta(float xc, float z, Vector3 n, float ancho, string hoja)
    {
        float y0 = PisoY(xc), h = 2.05f, w = ancho / 2; float zf(float d) => z + n.z * d;
        Box(hoja, xc - w, y0, zf(0), xc + w, y0 + h, zf(.04f));
        Box("PinturaBlanca", xc - w - .07f, y0, zf(0), xc - w, y0 + h + .07f, zf(.06f));
        Box("PinturaBlanca", xc + w, y0, zf(0), xc + w + .07f, y0 + h + .07f, zf(.06f));
        Box("PinturaBlanca", xc - w - .07f, y0 + h, zf(0), xc + w + .07f, y0 + h + .07f, zf(.06f));
        float xm = xc + w - .14f;
        Box("MetalNegro", xm - .02f, y0 + .98f, zf(.04f), xm + .02f, y0 + 1.06f, zf(.08f));
        Box("MetalNegro", xm - .14f, y0 + 1.01f, zf(.07f), xm + .01f, y0 + 1.04f, zf(.1f));
        if (hoja == "PuertaGris") Box("Vidrio", xc - .1f, y0 + 1.3f, zf(.04f), xc + .1f, y0 + 1.85f, zf(.045f));
    }

    static void Baranda(float z, float x0, float x1)
    {
        foreach (var h in new[] { .95f, .72f, .3f })
        {
            float g = h > .9f ? .025f : .017f;
            Cilindro("Galvanizado", new Vector3(x0, PisoY(x0) + h, z), new Vector3(RampaX1, PisoY(RampaX1) + h, z), g, 8);
            Cilindro("Galvanizado", new Vector3(RampaX1, PisoY(RampaX1) + h, z), new Vector3(x1, PisoY(x1) + h, z), g, 8);
        }
        int k = Mathf.CeilToInt((x1 - x0) / 1.6f);
        for (int i = 0; i <= k; i++)
        {
            float x = Mathf.Lerp(x0, x1, (float)i / k);
            Barra("Galvanizado", new Vector3(x, PisoY(x), z), new Vector3(x, PisoY(x) + .97f, z), .045f);
        }
    }

    // Silla escolar: casco de plástico azul y patas de caño negro.
    static void Silla(Vector3 p, Quaternion q)
    {
        Vector3 X = q * Vector3.right, Y = q * Vector3.up, Z = q * Vector3.forward;
        Vector3 P(float x, float y, float z) => p + X * x + Y * y + Z * z;
        Caja("SillaAzul", P(0, .45f, .02f), X * .21f, Y * .018f, Z * .2f);
        var B = (Y - Z * .17f).normalized;
        Caja("SillaAzul", P(0, .74f, -.21f), X * .2f, B * .15f, Vector3.Cross(X, B).normalized * .015f);
        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sz = -1; sz <= 1; sz += 2) Barra("MetalNegro", P(sx * .18f, 0, sz * .18f), P(sx * .18f, .44f, sz * .18f), .022f);
            Barra("MetalNegro", P(sx * .17f, .44f, -.19f), P(sx * .17f, .86f, -.24f), .02f);
        }
    }

    static List<(Vector3, Quaternion)> PosicionesDeSillas()
    {
        var lista = new List<(Vector3, Quaternion)>();
        var rnd = new System.Random(339); float R() => (float)rnd.NextDouble();
        // Abajo, dos filas de sillas apiladas paradas (encastradas, 6 cm cada una), de pared a pared.
        var altoPila = new float[2, 6];
        for (int f = 0; f < 2; f++)
            for (int i = 0; i < 6; i++)
            {
                float z = AZ0 + .5f + i * .63f + (R() - .5f) * .08f, x = PX0 + .4f + f * .62f + (R() - .5f) * .1f, yaw = (f == 0 ? 90 : -90) + (R() - .5f) * 30;
                int n = 2 + rnd.Next(4);
                for (int k = 0; k < n; k++) lista.Add((new Vector3(x, PisoAlto + k * .06f, z), Quaternion.Euler(0, yaw + (R() - .5f) * 8, 0)));
                altoPila[f, i] = PisoAlto + .86f + (n - 1) * .06f;
            }
        // Encima, sillas acostadas de costado, de espaldas y dadas vuelta, cada vez menos; apoyadas en lo de abajo.
        for (int capa = 0; capa < 2; capa++)
            for (int i = 0; i < 6 - capa * 2; i++)
                for (int f = 0; f < 2 - capa; f++)
                {
                    int tipo = rnd.Next(3);
                    var giro = tipo == 0 ? Quaternion.Euler(0, R() * 360, 85 + R() * 15)                 // de costado
                             : tipo == 1 ? Quaternion.Euler(-80 - R() * 20, R() * 360, (R() - .5f) * 20)  // de espaldas
                             : Quaternion.Euler(180 + (R() - .5f) * 25, R() * 360, (R() - .5f) * 25);   // dada vuelta
                    float apoyo = altoPila[f, Mathf.Min(5, i + capa / 2)] + capa * .42f;
                    float y = apoyo + (tipo == 2 ? .86f : tipo == 0 ? .22f : .25f) + R() * .06f;
                    float z = AZ0 + .55f + (i + capa * .5f) * .63f + (R() - .5f) * .2f, x = PX0 + .4f + f * .62f + (R() - .5f) * .2f;
                    lista.Add((new Vector3(x, y, z), giro));
                }
        return lista;
    }

    // ---------------------------------------------------------------- Piezas sueltas (prefabs)
    // Los carteles de módulo y la pila de sillas van aparte, como prefabs: se pueden mover, copiar o editar sin tocar
    // las mallas del edificio. Se crean una sola vez (si faltan); volver a armar el Módulo 3 no los pisa, solo los coloca.
    const string CarpetaCarteles = "Assets/Prefabs/Uni/Carteles", CarpetaMobiliario = "Assets/Prefabs/Uni/Mobiliario";
    const string PrefabCartel = CarpetaCarteles + "/CartelModulo.prefab", PrefabSilla = CarpetaMobiliario + "/SillaEscolar.prefab", PrefabPila = CarpetaMobiliario + "/PilaDeSillas.prefab";
    // Medidas del cartel a escala 1 (el del 3b): placa blanca de 0,62 x 0,48 m y placa verde de 0,56 x 0,9 m.
    const float CartelAnchoBlanco = .62f, CartelAltoBlanco = .48f, CartelAnchoVerde = .56f, CartelAltoVerde = .9f;

    static void AsegurarCarpeta(string carpeta)
    {
        if (AssetDatabase.IsValidFolder(carpeta)) return;
        int i = carpeta.LastIndexOf('/');
        AsegurarCarpeta(carpeta.Substring(0, i));
        AssetDatabase.CreateFolder(carpeta.Substring(0, i), carpeta.Substring(i + 1));
    }

    // Junta lotes en una malla con una submalla por material, en el orden dado.
    static Mesh MallaDeLotes(string nombre, params string[] mats)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var subs = new List<List<int>>();
        foreach (var m in mats)
        {
            var l = lotes[m]; int o = v.Count; v.AddRange(l.v); uv.AddRange(l.uv);
            var t = new List<int>(); foreach (var i in l.t) t.Add(i + o); subs.Add(t);
        }
        var mesh = new Mesh { name = nombre }; mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.subMeshCount = subs.Count;
        for (int i = 0; i < subs.Count; i++) mesh.SetTriangles(subs[i], i);
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        return mesh;
    }

    // Silla escolar suelta: una malla con el casco azul y las patas negras, apoyada en el piso.
    static GameObject PrefabDeSilla()
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabSilla); if (p != null) return p;
        AsegurarCarpeta(CarpetaMobiliario);
        lotes.Clear(); Silla(Vector3.zero, Quaternion.identity);
        var mesh = MallaDeLotes("SillaEscolar", "SillaAzul", "MetalNegro");
        var mats = new[] { lotes["SillaAzul"].mat, lotes["MetalNegro"].mat }; lotes.Clear();
        AssetDatabase.CreateAsset(mesh, CarpetaMobiliario + "/SillaEscolar.asset");
        var go = new GameObject("SillaEscolar");
        go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterials = mats;
        p = PrefabUtility.SaveAsPrefabAsset(go, PrefabSilla); UnityEngine.Object.DestroyImmediate(go);
        return p;
    }

    // Sillas que se sacaron de la pila porque se salían hacia las paredes (una asomaba al patio y otra se metía en la
    // pared de las aulas).
    static readonly int[] SillasQuitadas = { 45, 56 };

    // Pila de sillas que tapa un pasillo: sillas sueltas (instancias de SillaEscolar) y un bloqueo invisible para que no
    // se pase. El origen está en el piso, al pie de la pila; +x es hacia donde se mira la pila.
    static GameObject PrefabDePila()
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPila); if (p != null) return p;
        var silla = PrefabDeSilla();
        var root = new GameObject("PilaDeSillas"); var o = new Vector3(PX0, PisoAlto, (AZ0 + PZ1) / 2); int n = 0;
        foreach (var (pos, rot) in PosicionesDeSillas())
        {
            if (Array.IndexOf(SillasQuitadas, ++n) >= 0) continue;
            var s = (GameObject)PrefabUtility.InstantiatePrefab(silla, root.transform);
            s.name = "Silla " + n; s.transform.localPosition = pos - o; s.transform.localRotation = rot;
        }
        var bc = root.AddComponent<BoxCollider>(); bc.center = new Vector3(.85f, 1.28f, 0); bc.size = new Vector3(1.7f, 3.4f, PZ1 - AZ0 + .2f);
        p = PrefabUtility.SaveAsPrefabAsset(root, PrefabPila); UnityEngine.Object.DestroyImmediate(root);
        return p;
    }

    // Cartel de módulo: placa blanca con el número en verde y, debajo, la placa verde con el texto en blanco. El origen
    // está en la pared, abajo al medio de la placa verde, y +z sale de la pared. El número y el texto se cambian en cada
    // instancia; el tamaño, con la escala.
    static GameObject PrefabDeCartel()
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabCartel); if (p != null) return p;
        AsegurarCarpeta(CarpetaCarteles);
        lotes.Clear(); Box("CartelBlanco", -.5f, -.5f, -.5f, .5f, .5f, .5f);
        var placa = MallaDeLotes("CartelModulo_Placa", "CartelBlanco"); var blanco = lotes["CartelBlanco"].mat; lotes.Clear();
        var verde = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Uni/Uni_CartelVerde.mat");
        AssetDatabase.CreateAsset(placa, CarpetaCarteles + "/CartelModulo_Placa.asset");
        var root = new GameObject("CartelModulo");
        void Pieza(string nombre, Material m, Vector3 pos, Vector3 esc)
        {
            var g = new GameObject(nombre); g.transform.SetParent(root.transform, false); g.transform.localPosition = pos; g.transform.localScale = esc;
            g.AddComponent<MeshFilter>().sharedMesh = placa; g.AddComponent<MeshRenderer>().sharedMaterial = m;
        }
        const float wb = CartelAnchoBlanco, hb = CartelAltoBlanco, wv = CartelAnchoVerde, hv = CartelAltoVerde;
        Pieza("PlacaVerde", verde, new Vector3(0, hv / 2, .04f), new Vector3(wv, hv, .06f));
        Pieza("PlacaBlanca", blanco, new Vector3(0, hv + hb / 2, .06f), new Vector3(wb, hb, .08f));
        TMPro.TextMeshPro Texto(string nombre, Vector3 pos)
        {
            var g = new GameObject(nombre); g.transform.SetParent(root.transform, false);
            g.transform.localPosition = pos; g.transform.localRotation = Quaternion.Euler(0, 180, 0);
            return g.AddComponent<TMPro.TextMeshPro>();
        }
        var num = Texto("Numero", new Vector3(0, hv + hb / 2, .105f));
        num.text = "3b"; num.fontSize = hb * 7.2f; num.fontStyle = TMPro.FontStyles.Bold; num.alignment = TMPro.TextAlignmentOptions.Center;
        num.color = new Color(.14f, .42f, .14f); num.rectTransform.sizeDelta = new Vector2(wb * .95f, hb * .95f);
        var txt = Texto("Texto", new Vector3(0, hv * .62f, .075f));
        txt.text = "aulas\n3015 a 3019"; txt.fontSize = .74f; txt.alignment = TMPro.TextAlignmentOptions.TopLeft; txt.color = Color.white;
        txt.rectTransform.sizeDelta = new Vector2(wb * .78f, hv * .6f);
        p = PrefabUtility.SaveAsPrefabAsset(root, PrefabCartel); UnityEngine.Object.DestroyImmediate(root);
        return p;
    }

    // Coloca un cartel en la pared: «c» es el punto de la pared (la altura no importa), «n» sale de la pared.
    static void ColocarCartel(Transform grupo, string nombre, Vector3 c, Vector3 n, string numero, string texto, float escala, float yBase)
    {
        var g = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabCartel), grupo); g.name = nombre;
        g.transform.localPosition = new Vector3(c.x, yBase, c.z); g.transform.localRotation = Quaternion.LookRotation(n); g.transform.localScale = Vector3.one * escala;
        foreach (var t in g.GetComponentsInChildren<TMPro.TextMeshPro>())
        {
            t.text = t.name == "Numero" ? numero : texto;
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        }
    }

    // ---------------------------------------------------------------- 3C

    static void Modulo3C(Transform grupo)
    {
        Box("LadrilloChico", CX0, -.2f, CZ0, CX1, CMuro, CZ1);
        // Chapa a dos aguas con la cumbrera de norte a sur; atrás del parapeto del frente.
        float xc = (CX0 + CX1) / 2; var largo = new Vector3(0, 0, CZ1 - CZ0 - .1f);
        var o = new Vector3(0, 0, CZ0 + .1f);
        Placa("ChapaClara", o + new Vector3(CX1 + .8f, CAlero, 0), largo, new Vector3(xc - CX1 - .8f, CCumbrera - CAlero, 0), .05f);
        Placa("ChapaClara", o + new Vector3(CX0 - .6f, CAlero, 0), largo, new Vector3(xc - CX0 + .6f, CCumbrera - CAlero, 0), .05f);
        Box("ChapaClara", xc - .16f, CCumbrera - .02f, CZ0 + .1f, xc + .16f, CCumbrera + .05f, CZ1);
        Box("PinturaBlanca", CX1 + .77f, CAlero - .2f, CZ0 + .1f, CX1 + .81f, CAlero + .03f, CZ1);
        Box("PinturaBlanca", CX0 - .61f, CAlero - .2f, CZ0 + .1f, CX0 - .57f, CAlero + .03f, CZ1);
        Tri("LadrilloChico", new Vector3(CX0, CMuro - .05f, CZ1), new Vector3(CX1, CMuro - .05f, CZ1), new Vector3(xc, CCumbrera - .03f, CZ1), Vector3.forward);
        // Cara del patio (foto de los árboles): ventana corrida sobre la base de ladrillo.
        Cinta(new Vector3(CX1, 0, CZ0 + .9f), new Vector3(CX1, 0, CZ1 - .4f), Vector3.right, 1.0f, 3.1f, 2.3f, 1.45f, 6, 3003);
        Box("PinturaBlanca", CX1, 3.1f, CZ0 + .9f, CX1 + .03f, CMuro, CZ1 - .4f);
        // Cara del patio interno, sin foto: ventanas simples.
        Ventanas(new Vector3(CX0, 0, RejaZ + 1f), new Vector3(CX0, 0, CZ1 - .5f), Vector3.left, 1.0f, 2.0f, 1.4f, 4f);
        Ventanas(new Vector3(CX0, 0, CZ0 + .6f), new Vector3(CX0, 0, RejaZ - .3f), Vector3.left, 1.0f, 1.2f, 1.2f, 2f);
        // Frente al Conector_1: parapeto de ladrillo con copete blanco a la izquierda, la entrada vidriada alta en el medio
        // y, a la derecha, el otro muro, igual de alto: los dos siguen la pendiente del techo con su copete blanco.
        const float E0 = 297.0f, E1 = 303.0f;
        MuroInclinado(CX0, E0, CZ0, CZ0 + .3f, TechoC(CX0) + .1f, TechoC(E0) + .1f);
        MuroInclinado(E1, CX1, CZ0, CZ0 + .3f, TechoC(E1) + .1f, TechoC(CX1) + .1f);
        Vidriera(E0, E1, VidrieraFrente, VidrieraFondo);
        ColocarCartel(grupo, "Cartel 3c", new Vector3(296.1f, 0, CZ0), Vector3.back, "3c", "aulas", .82f, 1.95f);
        // Gabinete rojo con el caño, a la derecha de la entrada, y el bicicletero.
        Box("CartelRojo", 304.0f, 1.35f, CZ0 - .2f, 304.62f, 1.95f, CZ0);
        Box("PinturaBlanca", 304.06f, 1.4f, CZ0 - .205f, 304.56f, 1.9f, CZ0 - .2f);
        Cilindro("CartelRojo", new Vector3(304.85f, 0, CZ0 - .1f), new Vector3(304.85f, 1.75f, CZ0 - .1f), .045f, 8);
        for (int i = 0; i < 4; i++)
        {
            float x = 305.8f + i * .8f;
            Barra("Galvanizado", new Vector3(x, 0, CZ0 - .3f), new Vector3(x, .75f, CZ0 - .3f), .035f);
            Barra("Galvanizado", new Vector3(x, 0, CZ0 - 1.0f), new Vector3(x, .75f, CZ0 - 1.0f), .035f);
            Barra("Galvanizado", new Vector3(x, .75f, CZ0 - 1.0f), new Vector3(x, .75f, CZ0 - .3f), .035f);
        }
        // Camino de hormigón hasta la entrada.
        Box("HormigonViejo", 298.3f, 0, 236.4f, 301.3f, .05f, CZ0 - .2f);
        Box("MetalNegro", 298.4f, .05f, CZ0 - .75f, 302.0f, .06f, CZ0 - .2f);
    }

    // Muro con el borde de arriba inclinado (la parte derecha del frente del 3C), con su copete.
    static void MuroInclinado(float x0, float x1, float z0, float z1, float h0, float h1)
    {
        const float yb = -.2f;
        Quad("LadrilloChico", new Vector3(x0, yb, z0), new Vector3(x1, yb, z0), new Vector3(x1, h1, z0), new Vector3(x0, h0, z0), Vector3.back);
        Quad("LadrilloChico", new Vector3(x0, yb, z1), new Vector3(x1, yb, z1), new Vector3(x1, h1, z1), new Vector3(x0, h0, z1), Vector3.forward);
        Quad("LadrilloChico", new Vector3(x1, yb, z0), new Vector3(x1, yb, z1), new Vector3(x1, h1, z1), new Vector3(x1, h1, z0), Vector3.right);
        Quad("LadrilloChico", new Vector3(x0, yb, z0), new Vector3(x0, yb, z1), new Vector3(x0, h0, z1), new Vector3(x0, h0, z0), Vector3.left);
        var u = new Vector3(x1 - x0, h1 - h0, 0);
        Placa("PinturaBlanca", new Vector3(x0, h0 + .035f, z0 - .05f), u, new Vector3(0, 0, z1 - z0 + .1f), .07f);
    }

    // Entrada vidriada: bloque de vidrio más alto adelante (h) que atrás, donde se junta con el techo; a los costados,
    // por encima de los muros, el pico de vidrio. Marcos blancos, puertas abajo y tapa blanca inclinada.
    static void Vidriera(float e0, float e1, float h, float prof)
    {
        float zf = CZ0 - .15f, zb = CZ0 + prof, hb = TechoC((e0 + e1) / 2) - .02f;
        Quad("Vidrio", new Vector3(e0, 0, zf), new Vector3(e1, 0, zf), new Vector3(e1, h, zf), new Vector3(e0, h, zf), Vector3.back);
        Quad("Vidrio", new Vector3(e0, 0, zb), new Vector3(e1, 0, zb), new Vector3(e1, hb, zb), new Vector3(e0, hb, zb), Vector3.forward);
        foreach (var x in new[] { e0, e1 })
        {
            var afuera = x == e0 ? Vector3.left : Vector3.right;
            Quad("Vidrio", new Vector3(x, 0, zf), new Vector3(x, 0, zb), new Vector3(x, hb, zb), new Vector3(x, h, zf), afuera);
            // Marcos del pico: la arista inclinada de arriba y un parante en el medio.
            float xo = x + afuera.x * .03f, zm = (zf + zb) / 2, ym = h + (hb - h) * (zm - zf) / (zb - zf);
            Viga("PinturaBlanca", new Vector3(xo, h - .04f, zf), new Vector3(xo, hb - .04f, zb), .08f, .07f);
            Box("PinturaBlanca", xo - .035f, TechoC(x), zm - .04f, xo + .035f, ym, zm + .04f);
        }
        Placa("PinturaBlanca", new Vector3(e0 - .08f, h, zf - .1f), new Vector3(e1 - e0 + .16f, 0, 0), new Vector3(0, hb - h, zb - zf + .1f), .2f);
        foreach (var x in new[] { e0, 298.0f, 299.3f, 300.6f, 301.8f, e1 })
            Box("PinturaBlanca", x - .045f, 0, zf - .08f, x + .045f, h, zf);
        foreach (var y in new[] { .02f, 2.45f, 3.6f, h - .04f })
            Box("PinturaBlanca", e0, y - .04f, zf - .08f, e1, y + .04f, zf);
        // Barras de empuje de las puertas y la del medio partida.
        Box("PinturaBlanca", 299.93f, 0, zf - .07f, 299.97f, 2.45f, zf);
        foreach (var x in new[] { 297.5f, 299.6f, 300.3f })
            Box("MetalNegro", x - .3f, 1.0f, zf - .13f, x + .3f, 1.05f, zf - .08f);
    }

    // Galería de policarbonato en la esquina oeste del 3C (sobre el portón), con el cartel de salida de emergencia.
    static void Galeria(Transform grupo)
    {
        const float x0 = 275.4f, x1 = CX0, z0 = CZ0 - .4f, z1 = RejaZ + .3f, yf = 2.8f, yb = 2.95f;
        foreach (var x in new[] { 276.0f, 279.9f })
        {
            Box("PosteBlanco", x - .05f, 0, z0 + .05f, x + .05f, yf, z0 + .15f);
            Box("PosteBlanco", x - .05f, 0, z1 - .15f, x + .05f, yb, z1 - .05f);
        }
        Box("PinturaBlanca", x0, yf, z0 + .02f, x1, yf + .14f, z0 + .1f);
        Box("PinturaBlanca", x0, yb, z1 - .1f, x1, yb + .14f, z1 - .02f);
        for (float x = x0; x <= x1 + .01f; x += 1.15f)
            Viga("PinturaBlanca", new Vector3(x, yf + .14f, z0 + .06f), new Vector3(x, yb + .14f, z1 - .06f), .06f, .05f);
        Placa("Policarbonato", new Vector3(x0 - .1f, yf + .17f, z0 - .05f), new Vector3(x1 - x0 + .1f, 0, 0), new Vector3(0, yb - yf, z1 - z0 + .1f), .02f);
        foreach (var x in new[] { 278.0f, 281.8f }) Box("PinturaBlanca", x - .6f, yf - .07f, 242.6f, x + .6f, yf - .01f, 242.72f);
        Box("HormigonViejo", x0, 0, z0 - .2f, x1, .05f, RejaZ);
        // Cartel colgado de «salida de emergencia».
        Box("PinturaBlanca", 276.75f, 2.5f, z0 + .3f, 277.35f, 2.72f, z0 + .36f);
        Barra("MetalNegro", new Vector3(277.05f, 2.72f, z0 + .33f), new Vector3(277.05f, yf + .02f, z0 + .33f), .015f);
        var g = new GameObject("Cartel salida de emergencia"); g.transform.SetParent(grupo, false);
        g.transform.localPosition = new Vector3(277.05f, 2.61f, z0 + .295f); g.transform.localRotation = Quaternion.identity;
        var t = g.AddComponent<TMPro.TextMeshPro>(); t.text = "SALIDA\nDE EMERGENCIA"; t.fontSize = .55f; t.fontStyle = TMPro.FontStyles.Bold;
        t.alignment = TMPro.TextAlignmentOptions.Center; t.color = new Color(.1f, .55f, .25f); t.rectTransform.sizeDelta = new Vector2(.58f, .2f);
    }

    // ---------------------------------------------------------------- Reja del patio interno

    static void Reja()
    {
        void Panel(float a, float b)
        {
            Caja("RejaVerde", new Vector3((a + b) / 2, 1.0f, RejaZ), Vector3.right * (b - a) / 2, Vector3.up * .93f, Vector3.forward * .012f);
            foreach (var y in new[] { .08f, .55f, 1.35f, 1.92f }) Barra("VerdeOscuro", new Vector3(a, y, RejaZ), new Vector3(b, y, RejaZ), y > 1.9f || y < .1f ? .03f : .02f);
        }
        void Tramo(float a, float b)
        {
            int k = Mathf.CeilToInt((b - a) / 2.5f); float p = (b - a) / k;
            for (int i = 0; i < k; i++) Panel(a + i * p, a + (i + 1) * p);
            for (int i = 0; i <= k; i++) { float x = a + i * p; Box("VerdeOscuro", x - .03f, 0, RejaZ - .03f, x + .03f, 2.0f, RejaZ + .03f); }
        }
        Tramo(RejaX0, PortonX0 - .05f);
        Tramo(PortonX1 + .05f, RejaX1);
        // Portón de dos hojas, cerrado con cadena y candado.
        Box("VerdeOscuro", PortonX0 - .1f, 0, RejaZ - .05f, PortonX0, 2.45f, RejaZ + .05f);
        Box("VerdeOscuro", PortonX1, 0, RejaZ - .05f, PortonX1 + .1f, 2.45f, RejaZ + .05f);
        Box("VerdeOscuro", PortonX0 - .1f, 2.35f, RejaZ - .05f, PortonX1 + .1f, 2.45f, RejaZ + .05f);
        float m = (PortonX0 + PortonX1) / 2;
        foreach (var (a, b) in new[] { (PortonX0 + .02f, m - .01f), (m + .01f, PortonX1 - .02f) })
        {
            Caja("RejaVerde", new Vector3((a + b) / 2, 1.15f, RejaZ), Vector3.right * (b - a) / 2 * .96f, Vector3.up * 1.05f, Vector3.forward * .012f);
            Box("VerdeOscuro", a, .06f, RejaZ - .025f, b, .12f, RejaZ + .025f);
            Box("VerdeOscuro", a, 2.2f, RejaZ - .025f, b, 2.26f, RejaZ + .025f);
            Box("VerdeOscuro", a, 1.12f, RejaZ - .025f, b, 1.17f, RejaZ + .025f);
            Box("VerdeOscuro", a, .06f, RejaZ - .025f, a + .06f, 2.26f, RejaZ + .025f);
            Box("VerdeOscuro", b - .06f, .06f, RejaZ - .025f, b, 2.26f, RejaZ + .025f);
        }
        for (int i = 0; i < 7; i++)
        {
            float y = 1.05f + i * .045f, dx = (i % 2 == 0 ? -.03f : .03f);
            Box("MetalNegro", m - .05f + dx, y, RejaZ - .045f, m + .05f + dx, y + .03f, RejaZ - .03f);
        }
        Box("MetalNegro", m - .04f, .93f, RejaZ - .07f, m + .04f, 1.03f, RejaZ - .04f);
        // Adentro, la vereda de adoquines junto a la reja y los dos bancos de la foto.
        Box("Vereda", RejaX0 + .1f, 0, RejaZ + .05f, RejaX1 - .1f, .05f, RejaZ + 1.6f);
        Banco(278.4f, RejaZ + 1.05f, true);
        Banco(281.6f, RejaZ + 1.15f, false);
    }

    static void Banco(float x, float z, bool madera)
    {
        if (madera)
        {
            for (int i = 0; i < 3; i++) Box("Madera", x - .8f, .43f, z - .2f + i * .14f, x + .8f, .47f, z - .1f + i * .14f);
            for (int i = 0; i < 2; i++) Box("Madera", x - .8f, .62f + i * .16f, z + .25f, x + .8f, .74f + i * .16f, z + .28f);
            foreach (var s in new[] { -.65f, .65f }) { Box("MetalNegro", x + s - .03f, 0, z - .2f, x + s + .03f, .45f, z + .22f); Box("MetalNegro", x + s - .03f, .45f, z + .2f, x + s + .03f, .92f, z + .26f); }
        }
        else
        {
            Box("MetalNegro", x - .75f, .42f, z - .2f, x + .75f, .46f, z + .2f);
            foreach (var s in new[] { -.68f, .68f }) Box("MetalNegro", x + s - .025f, 0, z - .18f, x + s + .025f, .42f, z + .18f);
        }
    }

    // ---------------------------------------------------------------- 3B, el pasillo al 3A y la punta

    static void Modulo3B(Transform grupo)
    {
        const float H = 4.4f;
        var planta = new[] { new Vector2(225.3f, 232.0f), new Vector2(246.0f, 232.0f), new Vector2(260.6f, 238.0f), new Vector2(260.6f, 263.7f), new Vector2(225.3f, 263.7f) };
        Prisma("LadrilloChico", "Techo", planta, -.2f, H);
        Box("ChapaOxido", 225.2f, H, 231.95f, 246.05f, H + .06f, 232.3f);
        // El mural: el dibujo de la foto del lado del cartel y el resto del muro en ladrillo pintado de rosa.
        Quad("3B_Mural", new Vector3(225.3f, 0, 231.985f), new Vector3(246.0f, 0, 231.985f), new Vector3(246.0f, H, 231.985f), new Vector3(225.3f, H, 231.985f), Vector3.back,
            new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
        // En la esquina, el poste gris con el cartel del 3b; arriba, la camarita con su panel solar.
        Box("ColumnaGris", 246.1f, 0, 231.55f, 246.6f, 3.3f, 231.85f);
        ColocarCartel(grupo, "Cartel 3b", new Vector3(246.25f, 0, 231.55f), Vector3.back, "3b", "aulas\n3015 a 3019", 1.0f, 1.65f);
        Barra("Galvanizado", new Vector3(245.6f, H, 232.4f), new Vector3(245.6f, H + .75f, 232.4f), .05f);
        Caja("PanelSolar", new Vector3(245.6f, H + .8f, 232.4f), new Vector3(.25f, 0, 0), new Vector3(0, .12f, -.14f), new Vector3(0, .014f, .012f));
        Box("PinturaBlanca", 245.52f, H + .55f, 232.2f, 245.68f, H + .65f, 232.36f);
        // Entrada en la cara en diagonal: vidrio con puertas bajo la pérgola blanca; más allá, ladrillo con ventanas altas.
        var a = new Vector2(246.0f, 232.0f); var d = (new Vector2(260.6f, 238.0f) - a); float L = d.magnitude; d /= L;
        var r = new Vector3(d.x, 0, d.y); var n = new Vector3(d.y, 0, -d.x);
        Vector3 P(float s, float y, float o) => new Vector3(a.x, 0, a.y) + r * s + Vector3.up * y + n * o;
        Caja("Vidrio", P(2.8f, 1.4f, .02f), r * 2.0f, Vector3.up * 1.4f, n * .02f);
        foreach (var s in new[] { .8f, 1.9f, 2.8f, 3.7f, 4.8f }) Caja("PinturaBlanca", P(s, 1.4f, .06f), r * .045f, Vector3.up * 1.4f, n * .05f);
        foreach (var y in new[] { .03f, 2.2f, 2.77f }) Caja("PinturaBlanca", P(2.8f, y, .06f), r * 2.0f, Vector3.up * .035f, n * .05f);
        Caja("MetalNegro", P(2.4f, 1.05f, .13f), r * .25f, Vector3.up * .025f, n * .03f);
        Caja("MetalNegro", P(3.2f, 1.05f, .13f), r * .25f, Vector3.up * .025f, n * .03f);
        const float yp = 2.95f, sale = 2.4f;
        foreach (var s in new[] { .5f, 2.1f, 3.7f, 5.3f }) Caja("PinturaBlanca", P(s, yp, sale / 2), r * .05f, Vector3.up * .11f, n * sale / 2);
        for (float o = .25f; o <= sale; o += .33f) Caja("PinturaBlanca", P(2.9f, yp + .15f, o), r * 2.65f, Vector3.up * .05f, n * .03f);
        foreach (var s in new[] { .5f, 5.3f }) Cilindro("PinturaBlanca", P(s, 0, sale - .1f), P(s, yp, sale - .1f), .06f, 10);
        Quad("HormigonViejo", P(0, .04f, 0), P(5.8f, .04f, 0), P(5.8f, .04f, sale + .3f), P(0, .04f, sale + .3f), Vector3.up);
        for (float s = 7.0f; s < L - 1f; s += 3.0f) Ventana(P(s, 1.6f, 0), n, r, 1.6f, 1.0f, 2, true);
        // Caras sin foto: ventanas simples (la del oeste, la del patio interno y la del este).
        Ventanas(new Vector3(225.3f, 0, 233.0f), new Vector3(225.3f, 0, 263.0f), Vector3.left, 1.1f, 1.8f, 1.3f, 4.5f);
        Ventanas(new Vector3(243.6f, 0, 263.7f), new Vector3(260.3f, 0, 263.7f), Vector3.forward, 1.1f, 1.8f, 1.3f, 4f);
        Ventanas(new Vector3(260.6f, 0, RejaZ + .6f), new Vector3(260.6f, 0, 263.3f), Vector3.right, 1.1f, 1.8f, 1.3f, 4f);
        Ventanas(new Vector3(260.6f, 0, 238.3f), new Vector3(260.6f, 0, RejaZ - .3f), Vector3.right, 1.1f, 1.6f, 1.3f, 5f);
        // El pasillo del 3A al 3B y la punta, más bajos y sencillos.
        Prisma("LadrilloChico", "Techo", new[] { new Vector2(236.3f, 263.7f), new Vector2(243.3f, 263.7f), new Vector2(243.3f, AZ0), new Vector2(236.3f, AZ0) }, -.2f, 3.6f);
        Prisma("LadrilloChico", "Techo", new[] { new Vector2(225.3f, 263.7f), new Vector2(236.3f, 263.7f), new Vector2(236.3f, 272.1f), new Vector2(225.3f, 272.1f) }, -.2f, H);
        Box("PinturaBlanca", 236.2f, 3.6f, 263.6f, 243.4f, 3.68f, AZ0);
        Ventanas(new Vector3(243.3f, 0, 264.2f), new Vector3(243.3f, 0, AZ0 - .4f), Vector3.right, 1.1f, 1.6f, 1.2f, 3.5f);
        Ventanas(new Vector3(236.3f, 0, 272.5f), new Vector3(236.3f, 0, AZ0 - .4f), Vector3.left, 1.1f, 1.6f, 1.2f, 3.5f);
        Ventanas(new Vector3(225.3f, 0, 264.2f), new Vector3(225.3f, 0, 271.6f), Vector3.left, 1.1f, 1.8f, 1.3f, 3.7f);
        Ventanas(new Vector3(225.8f, 0, 272.1f), new Vector3(235.8f, 0, 272.1f), Vector3.forward, 1.1f, 1.8f, 1.3f, 3.5f);
    }

    // ---------------------------------------------------------------- Seto de cañas

    static void Seto(Transform grupo)
    {
        var d = SetoB - SetoA; float L = d.magnitude; d /= L; var r = new Vector3(d.x, 0, d.y); var n = new Vector3(d.y, 0, -d.x);
        var rnd = new System.Random(7339); float R() => (float)rnd.NextDouble();
        const float alto = 5.2f, ancho = 2.6f;
        // Matas de cañas de alturas distintas, algo inclinadas y cruzadas en varios ángulos, para que no quede una pared.
        for (float s = .2f; s < L - .2f; s += .8f)
        {
            int cuantas = 3 + rnd.Next(2);
            for (int k = 0; k < cuantas; k++)
            {
                float o = k == 0 ? -.5f : k == 1 ? .5f : (R() - .5f) * .7f;
                var dir = k < 2 ? Quaternion.AngleAxis((R() - .5f) * 24, Vector3.up) * r : Quaternion.AngleAxis(25 + R() * 130, Vector3.up) * r;
                float h = alto * (.72f + R() * .5f);
                var arriba = Quaternion.AngleAxis((R() - .5f) * 12, Vector3.Cross(dir, Vector3.up)) * Quaternion.AngleAxis((R() - .5f) * 10, Vector3.Cross(Vector3.up, dir).normalized) * Vector3.up;
                arriba = (arriba - Vector3.Dot(arriba, dir) * dir).normalized;
                var c = new Vector3(SetoA.x, 0, SetoA.y) + r * (s + (R() - .5f) * .5f) + n * o + arriba * (h / 2 - .1f);
                var uv = new Rect(rnd.Next(2) * .5f + .003f, 0, .494f, 1);
                TarjetaEjes("Canas", c, dir, arriba, ancho * (.8f + R() * .35f), h, uv, rnd.Next(2) == 0);
            }
        }
        var col = new GameObject("Colision_Seto"); col.transform.SetParent(grupo, false);
        var m = (SetoA + SetoB) / 2; col.transform.localPosition = new Vector3(m.x, 1.6f, m.y);
        col.transform.localRotation = Quaternion.LookRotation(r);
        var bc = col.AddComponent<BoxCollider>(); bc.size = new Vector3(1.2f, 3.2f, L + .6f);
    }

    // ---------------------------------------------------------------- Patio entre el 3C y el TUM

    static void Patio()
    {
        // Plataforma de los aires sobre patas, contra la pared del TUM, con la baranda y el equipo grande arriba.
        const float x0 = 340.6f, x1 = 344.4f, z0 = 247.0f, z1 = 253.0f, yp = 2.6f;
        Box("Galvanizado", x0, yp, z0, x1, yp + .1f, z1);
        foreach (var x in new[] { x0 + .1f, x1 - .1f }) foreach (var z in new[] { z0 + .1f, (z0 + z1) / 2, z1 - .1f })
                Box("Galvanizado", x - .06f, 0, z - .06f, x + .06f, yp, z + .06f);
        foreach (var (a, b) in new[] { (new Vector3(x0, 0, z0), new Vector3(x0, 0, z1)), (new Vector3(x0, 0, z0), new Vector3(x1, 0, z0)), (new Vector3(x0, 0, z1), new Vector3(x1, 0, z1)) })
        {
            Barra("Galvanizado", a + Vector3.up * (yp + 1.05f), b + Vector3.up * (yp + 1.05f), .045f);
            Barra("Galvanizado", a + Vector3.up * (yp + .55f), b + Vector3.up * (yp + .55f), .03f);
            float L = (b - a).magnitude; int k = Mathf.CeilToInt(L / 1.5f);
            for (int i = 0; i <= k; i++) { var p = Vector3.Lerp(a, b, (float)i / k); Barra("Galvanizado", p + Vector3.up * yp, p + Vector3.up * (yp + 1.05f), .04f); }
        }
        Box("PuertaGris", x0 + .5f, yp + .1f, z0 + .5f, x1 - .4f, yp + 1.3f, z0 + 3.6f);
        Box("MetalNegro", x0 + .48f, yp + .3f, z0 + 2.0f, x0 + .5f, yp + 1.2f, z0 + 3.4f);
        Box("PuertaGris", x0 + .6f, yp + .1f, z0 + 3.9f, x1 - .6f, yp + .9f, z1 - .5f);
        Box("Galvanizado", x1 - .4f, yp + .5f, z0 + 1.2f, 345.3f, yp + 1.0f, z0 + 2.0f);
        // Losa marrón del pozo, adelante.
        Box("HormigonViejo", 340.3f, 0, 254.6f, 342.1f, .18f, 255.7f);
        Box("ChapaOxido", 340.35f, .18f, 254.65f, 342.05f, .2f, 255.65f);
        // Contenedor gris con la franja verde, sobre tacos de hormigón, con la campana oxidada arriba.
        const float cx0 = 333.6f, cx1 = 336.04f, cz0 = 245.0f, cz1 = 251.06f, cy0 = .25f, cy1 = 2.84f;
        foreach (var x in new[] { cx0 + .2f, cx1 - .2f }) foreach (var z in new[] { cz0 + .3f, cz1 - .3f })
                Box("Hormigon", x - .2f, 0, z - .2f, x + .2f, cy0, z + .2f);
        Box("PuertaGris", cx0, cy0, cz0, cx1, cy1, cz1);
        Box("CartelVerde", cx0 - .01f, cy0 + .05f, cz0 - .01f, cx1 + .01f, cy0 + .42f, cz1 + .01f);
        // Perfiles azules del borde de arriba (apenas más altos que el techo, para que no titilen con él).
        Box("PuertaAzul", cx0 - .02f, cy1 - .12f, cz0 - .02f, cx0 + .06f, cy1 + .012f, cz1 + .02f);
        Box("PuertaAzul", cx1 - .06f, cy1 - .12f, cz0 - .02f, cx1 + .02f, cy1 + .012f, cz1 + .02f);
        Box("PuertaAzul", cx0 + .06f, cy1 - .12f, cz0 - .02f, cx1 - .06f, cy1 + .01f, cz0 + .06f);
        Box("PuertaAzul", cx0 + .06f, cy1 - .12f, cz1 - .06f, cx1 - .06f, cy1 + .01f, cz1 + .02f);
        for (float z = cz0 + .25f; z < cz1 - .1f; z += .3f)
            foreach (var x in new[] { cx0 - .03f, cx1 })
                Box("PuertaGris", x, cy0 + .45f, z - .05f, x + .03f, cy1 - .14f, z + .05f);
        foreach (var x in new[] { cx0 + .35f, cx0 + .9f, cx1 - .9f, cx1 - .35f })
            Cilindro("Galvanizado", new Vector3(x, cy0 + .2f, cz1 + .05f), new Vector3(x, cy1 - .15f, cz1 + .05f), .025f, 6);
        var tilt = Quaternion.AngleAxis(-28, Vector3.right);
        Caja("ChapaOxido", new Vector3((cx0 + cx1) / 2, cy1 + .75f, cz1 - .8f), Vector3.right * 1.1f, tilt * Vector3.up * .7f, tilt * Vector3.forward * .55f);
        Caja("RejaMalla", new Vector3((cx0 + cx1) / 2, cy1 + .82f, cz1 - .8f) + tilt * Vector3.forward * .56f, Vector3.right * .95f, tilt * Vector3.up * .55f, tilt * Vector3.forward * .01f);
    }

    // Pared del TUM que da al patio (foto del contenedor): ventana corrida sobre la base de ladrillo, con los barrotes
    // oxidados por delante. Se sacan las ventanas sueltas que tenía esa cara.
    static void ParedTUM(Transform mapa)
    {
        var zona = mapa.Find("Ambiente_ZonaTUM"); if (zona == null) return;
        var caja = new Bounds(new Vector3(345.2f, 2.2f, 267.0f), new Vector3(1.2f, 3.0f, 28.4f));
        foreach (var n in new[] { "Vidrio", "Marcos" }) QuitarTriangulos(zona, zona.Find(n), caja);
        const float x = 345.3f, z0 = 253.8f, z1 = 280.7f, y0 = 1.3f, y1 = 3.15f;
        Cinta(new Vector3(x, 0, z0), new Vector3(x, 0, z1), Vector3.left, y0, y1, 2.55f, 1.45f, 0, 3004);
        Box("PinturaBlanca", x - .03f, y1, z0, x, y1 + .3f, z1);
        float xb = x - .17f;
        for (float y = y0 + .1f; y < y1 - .05f; y += .17f) Box("ChapaOxido", xb - .006f, y, z0 + .05f, xb + .006f, y + .035f, z1 - .05f);
        int k = Mathf.RoundToInt((z1 - z0) / 1.45f);
        for (int i = 0; i <= k; i++)
        {
            float z = Mathf.Lerp(z0 + .05f, z1 - .05f, (float)i / k);
            Box("ChapaOxido", xb - .02f, y0 - .02f, z - .02f, xb + .02f, y1 + .02f, z + .02f);
            foreach (var y in new[] { y0 + .15f, y1 - .15f }) Box("ChapaOxido", xb, y - .02f, z - .015f, x, y + .02f, z + .015f);
        }
    }

    // El corral de la escalera del TUM (esquina sudoeste) tenía dos huecos por donde se entraba: adelante, entre los
    // postes amarillos donde arranca la escalera, y al costado del camino del portón, entre la reja y la esquina del TUM.
    // Se cierran con paños como los que ya tiene y un bloqueo invisible que cubre todo el corral: se ve, pero no se entra.
    const float CorralX0 = 339.2f, CorralX1 = 345.3f, CorralZ0 = 237.2f, CorralZ1 = 245.8f;

    static void CerrarCorralTUM(Transform grupo)
    {
        void Panel(Vector3 a, Vector3 b)
        {
            var d = b - a; float L = d.magnitude; var r = d / L; var n = Vector3.Cross(Vector3.up, r); var up = Vector3.up;
            Caja("RejaMalla", (a + b) / 2 + up * 1.1f, r * L / 2, up * 1.0f, n * .01f);
            foreach (var y in new[] { .105f, 2.105f }) Barra("PosteBlanco", a + up * y, b + up * y, .05f);
            foreach (var p in new[] { a, b }) Barra("PosteBlanco", p, p + up * 2.1f, .06f);
        }
        Panel(new Vector3(341.93f, 0, CorralZ0), new Vector3(343.67f, 0, CorralZ0));
        Panel(new Vector3(CorralX1, 0, CorralZ0 + .03f), new Vector3(CorralX1, 0, 239.7f));
        var g = new GameObject("Bloqueo_CorralTUM"); g.transform.SetParent(grupo, false);
        var bc = g.AddComponent<BoxCollider>();
        bc.center = new Vector3((CorralX0 + CorralX1) / 2, 2.4f, (CorralZ0 + CorralZ1) / 2); bc.size = new Vector3(CorralX1 - CorralX0, 4.8f, CorralZ1 - CorralZ0);
    }

    // Saca de la malla de «t» los triángulos cuyo centro cae en «caja» (medida en el espacio de «zona»).
    static void QuitarTriangulos(Transform zona, Transform t, Bounds caja)
    {
        if (t == null) return;
        var mesh = t.GetComponent<MeshFilter>().sharedMesh; var v = mesh.vertices; int sacados = 0;
        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            var tri = mesh.GetTriangles(s); var quedan = new List<int>();
            for (int i = 0; i < tri.Length; i += 3)
            {
                var c = t.localToWorldMatrix.MultiplyPoint3x4((v[tri[i]] + v[tri[i + 1]] + v[tri[i + 2]]) / 3);
                if (caja.Contains(zona.worldToLocalMatrix.MultiplyPoint3x4(c))) { sacados++; continue; }
                quedan.Add(tri[i]); quedan.Add(tri[i + 1]); quedan.Add(tri[i + 2]);
            }
            mesh.SetTriangles(quedan, s);
        }
        if (sacados == 0) return;
        mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
        var mc = t.GetComponent<MeshCollider>(); if (mc != null) { mc.sharedMesh = null; mc.sharedMesh = mesh; }
        Debug.Log("Módulo 3: " + sacados + " triángulos viejos fuera de " + t.name);
    }

    // ---------------------------------------------------------------- Terreno

    // Saca el pasto de los pisos nuevos (pasillo, rampa, veredas, galería) y pinta de ripio la calle de atrás del 2b,
    // entre la reja y el camino.
    static void AjustarTerreno(Transform mapa)
    {
        var terreno = Terrain.activeTerrain; if (terreno == null) return;
        var td = terreno.terrainData; var o = terreno.transform.position;
        bool En(Vector2 q, float x0, float z0, float x1, float z1) => q.x >= x0 && q.x <= x1 && q.y >= z0 && q.y <= z1;
        bool SinPasto(Vector2 q) =>
            En(q, PX0 - .2f, AZ0 - .5f, AX1 + 1.9f, PZ1 + .2f) || En(q, 297.9f, 235.5f, 301.7f, CZ0) || En(q, 274.8f, 240.6f, CX0, RejaZ + 1.8f)
            || En(q, RejaX0, RejaZ - .2f, RejaX1, RejaZ + 1.8f) || En(q, 333.0f, 244.5f, 336.6f, 251.6f) || EnRipio(q) || EnEntrada3B(q);
        var w0 = mapa.TransformPoint(new Vector3(215f, 0, 228f)); var w1 = mapa.TransformPoint(new Vector3(366f, 0, 298f));
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
                    var w = new Vector3(o.x + (i0 + i + .5f) / res * td.size.x, 0, o.z + (j0 + j + .5f) / res * td.size.z);
                    var l = mapa.InverseTransformPoint(w);
                    if (SinPasto(new Vector2(l.x, l.z))) { datos[j, i] = 0; cambio = true; }
                }
            if (cambio) td.SetDetailLayer(i0, j0, capa, datos);
        }
        int ar = td.alphamapResolution, capas = td.alphamapLayers;
        if (capas > 2)
        {
            var a0 = mapa.TransformPoint(new Vector3(256f, 0, 234f)); var a1 = mapa.TransformPoint(new Vector3(286f, 0, 246f));
            int x0 = Mathf.Clamp(Mathf.FloorToInt((a0.x - o.x) / td.size.x * ar), 0, ar - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((a1.x - o.x) / td.size.x * ar), 0, ar - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt((a0.z - o.z) / td.size.z * ar), 0, ar - 1), y1 = Mathf.Clamp(Mathf.CeilToInt((a1.z - o.z) / td.size.z * ar), 0, ar - 1);
            var al = td.GetAlphamaps(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            for (int j = 0; j <= y1 - y0; j++)
                for (int i = 0; i <= x1 - x0; i++)
                {
                    var w = new Vector3(o.x + (x0 + i + .5f) / ar * td.size.x, 0, o.z + (y0 + j + .5f) / ar * td.size.z);
                    var l = mapa.InverseTransformPoint(w);
                    if (!EnRipio(new Vector2(l.x, l.z))) continue;
                    for (int c = 0; c < capas; c++) al[j, i, c] = c == 2 ? 1 : 0;
                }
            td.SetAlphamaps(x0, y0, al);
        }
        EditorUtility.SetDirty(td);
    }

    // Calle de ripio entre la reja y el camino del 2b (de la cara en diagonal del 3B a la esquina del 3C).
    static bool EnRipio(Vector2 q) => q.x >= 258.5f && q.x <= CX0 + .3f && q.y >= 238.6f + Mathf.Max(0, (260.6f - q.x)) * .41f && q.y <= RejaZ - .05f;

    static bool EnEntrada3B(Vector2 q)
    {
        var a = new Vector2(246.0f, 232.0f); var d = (new Vector2(260.6f, 238.0f) - a).normalized; var n = new Vector2(d.y, -d.x);
        float s = Vector2.Dot(q - a, d), o = Vector2.Dot(q - a, n);
        return s >= -.1f && s <= 5.9f && o >= -.1f && o <= 2.8f;
    }

    // ---------------------------------------------------------------- Materiales nuevos

    static void Materiales()
    {
        MaterialTextura("GranitoBordo", "png", .35f, TextureWrapMode.Repeat, false);
        MaterialTextura("3B_Mural", "jpg", .06f, TextureWrapMode.Clamp, false);
        MaterialTextura("Canas", "png", .1f, TextureWrapMode.Clamp, true);
        MaterialColor("VerdeOscuro", new Color(.09f, .25f, .17f), .35f);
        // Degradé a negro del pasillo de adentro (u = 0 en la boca, 1 al fondo).
        const string fundido = "Assets/Materials/Uni/Uni_Fundido.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(fundido) == null)
        {
            var tex = new Texture2D(256, 4, TextureFormat.RGB24, false);
            for (int x = 0; x < 256; x++) { float v = Mathf.Pow(1 - x / 255f, 2.2f) * .62f; for (int y = 0; y < 4; y++) tex.SetPixel(x, y, new Color(v, v, v)); }
            System.IO.File.WriteAllBytes(fundido, tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(fundido, ImportAssetOptions.ForceSynchronousImport);
        }
        MaterialTextura("Fundido", "png", .05f, TextureWrapMode.Clamp, false, "PasilloFundido", new Color(.95f, .95f, .93f));
        MaterialTextura("Fundido", "png", .2f, TextureWrapMode.Clamp, false, "PisoFundido", new Color(.75f, .42f, .42f));
        if (AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Uni/Uni_RejaVerde.mat") == null)
        {
            var m = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Uni/Uni_RejaMalla.mat")) { name = "Uni_RejaVerde" };
            m.SetColor("_BaseColor", new Color(.16f, .38f, .26f));
            AssetDatabase.CreateAsset(m, "Assets/Materials/Uni/Uni_RejaVerde.mat");
        }
    }

    static void MaterialColor(string nombre, Color color, float brillo)
    {
        string path = "Assets/Materials/Uni/Uni_" + nombre + ".mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Uni_" + nombre };
        m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", brillo);
        AssetDatabase.CreateAsset(m, path);
    }

    static void MaterialTextura(string nombre, string ext, float brillo, TextureWrapMode wrap, bool recorte, string material = null, Color? color = null)
    {
        string path = "Assets/Materials/Uni/Uni_" + (material ?? nombre) + ".mat", texPath = "Assets/Materials/Uni/Uni_" + nombre + "." + ext;
        var imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
        if (imp == null) throw new InvalidOperationException("Falta la textura " + texPath);
        if (imp.wrapMode != wrap || imp.npotScale != TextureImporterNPOTScale.None || imp.maxTextureSize < 4096 || imp.alphaIsTransparency != recorte)
        {
            imp.wrapMode = wrap; imp.npotScale = TextureImporterNPOTScale.None; imp.maxTextureSize = 4096; imp.mipmapEnabled = true;
            imp.alphaIsTransparency = recorte; if (recorte) { imp.mipMapsPreserveCoverage = true; imp.alphaTestReferenceValue = .5f; }
            imp.SaveAndReimport();
        }
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Uni_" + (material ?? nombre) };
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath)); m.SetColor("_BaseColor", color ?? Color.white); m.SetFloat("_Smoothness", brillo);
        if (recorte)
        {
            m.SetFloat("_AlphaClip", 1); m.SetFloat("_Cutoff", .5f); m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", 0); m.SetOverrideTag("RenderType", "TransparentCutout"); m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        }
        AssetDatabase.CreateAsset(m, path);
    }

    // ---------------------------------------------------------------- Armado

    [MenuItem("Riftwalker/Universidad/Armar Módulo 3")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/universidadMAPA.unity" || EditorApplication.isPlaying) throw new InvalidOperationException("Abrir universidadMAPA fuera de Play.");
        GameObject mapaGo = null;
        foreach (var g in scene.GetRootGameObjects()) if (g.name == "Mapa (escala 1,25)") mapaGo = g;
        if (mapaGo == null) throw new InvalidOperationException("No se encontró Mapa (escala 1,25).");
        var mapa = mapaGo.transform;
        Materiales();
        lotes.Clear();
        PrefabDeSilla(); PrefabDePila(); PrefabDeCartel();
        lotes.Clear();
        var anterior = mapa.Find(Grupo); if (anterior != null) UnityEngine.Object.DestroyImmediate(anterior.gameObject);
        if (AssetDatabase.LoadMainAssetAtPath(MeshPath) != null) AssetDatabase.DeleteAsset(MeshPath);
        // Las cajas provisorias quedan apagadas, como la del 3D cuando se hizo el TUM.
        var sinDetalle = mapa.Find("Ambiente_EdificiosSinDetalle");
        foreach (var n in CajasViejas) { var t = sinDetalle != null ? sinDetalle.Find(n) : null; if (t != null) t.gameObject.SetActive(false); }
        var go = new GameObject(Grupo); go.transform.SetParent(mapa, false);
        var g3 = go.transform;
        Modulo3A(); Fachada3d(g3); Pasillo3d(g3); Modulo3C(g3); Galeria(g3); Reja(); Modulo3B(g3); Seto(g3); Patio(); ParedTUM(mapa); CerrarCorralTUM(g3);
        AjustarTerreno(mapa);
        var contenedor = new Mesh { name = "Modulo3" }; AssetDatabase.CreateAsset(contenedor, MeshPath);
        var mallas = new GameObject("Modulo3"); mallas.transform.SetParent(g3, false);
        foreach (var kv in lotes)
        {
            var l = kv.Value; if (l.v.Count == 0) continue;
            var mesh = new Mesh { name = "M3_" + kv.Key, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(l.v); mesh.SetUVs(0, l.uv); mesh.SetTriangles(l.t, 0); mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            AssetDatabase.AddObjectToAsset(mesh, contenedor);
            var hijo = new GameObject(kv.Key); hijo.transform.SetParent(mallas.transform, false);
            hijo.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = hijo.AddComponent<MeshRenderer>(); mr.sharedMaterial = l.mat;
            if (kv.Key == "Canas" || kv.Key == "Policarbonato") mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            if (!SinColision.Contains(kv.Key)) hijo.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
        lotes.Clear();
        EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
        Debug.Log("Módulo 3 armado.");
    }
}
