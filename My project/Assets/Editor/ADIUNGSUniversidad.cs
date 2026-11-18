using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ADIUNGS en el mapa de la universidad (US 343, CA2), armado según las fotos del edificio:
// - Frente (mira a -x): galería con techito fino sobre columnas blancas finas (arriba no se camina), la puerta de vidrio,
//   el gabinete rojo de la manguera, paños crema, el cartel de ADIUNGS en el ladrillo y, a la izquierda, un entrante con
//   el balcón arriba (sin escalera por afuera) y la pérgola de policarbonato, que sigue por encima del anexo.
// - Atrás (+x), la fachada de tres paños: pilares de ladrillo, paños crema, marcos blancos y toldos grises arriba.
// - Del lado +z: seis aires en estantes de tres niveles, la escalera de metal con descanso y techito de policarbonato.
// - Aparte, entre ADIUNGS y el pasillo que va al patio de atrás: el anexo, un muro macizo tan largo como ADIUNGS con
//   malla y enredadera, y en el medio de la cara del pasillo la casilla de ladrillo de puerta doble blanca, el tablero
//   eléctrico y el reflector.
// - El camino por ese pasillo, desde el camino del frente hasta el caminito del patio, junto al Módulo 7, y la unión en el
//   ripio entre el camino que viene de los módulos y el sendero del acceso 7a.
// ADIUNGS y el anexo se arman con las medidas de abajo y se corren DZ hacia -z (a la derecha mirando el frente), para que
// el pasillo al patio quede ancho.
// Medidas reales en metros, en el espacio de «Mapa (escala 1,25)». Se puede volver a correr: rehace el grupo ADIUNGS.
public static class ADIUNGSUniversidad
{
    const float X0 = 386.5f, X1 = 396.3f, Z0 = 195.5f, Z1 = 208.5f, H = 6.5f, Piso = 3.2f;
    const string MeshPath = "Assets/Meshes/Uni/ADIUNGS.asset";
    // Entrante de adelante a la izquierda (balcón arriba).
    const float ZR = 205.6f, XR = X0 + 1.5f;
    // Anexo de enredadera, aparte: tan largo como ADIUNGS, con la casilla de ladrillo en el medio de la cara del pasillo.
    const float AX0 = 386.7f, AX1 = 396.1f, AZ0 = Z1 + 1.6f, AZ1 = AZ0 + 4.6f, AltoA = 2.9f;
    const float BX0 = 390.2f, BX1 = 393.0f, BZ0 = AZ1 - 2.4f, Hueco = .5f; // la casilla está metida medio metro
    // Cuánto se corre todo hacia -z para ensanchar el pasillo entre el anexo y el Módulo 7.
    const float DZ = -3f;
    static Vector3 corrimiento;
    static readonly HashSet<string> SinColision = new HashSet<string> { "Vidrio", "VidrioLuz", "Policarbonato", "CartelADIUNGS", "Corteza", "Enredadera", "EnredaderaTallos" };
    // Lo viejo de ADIUNGS dentro de las mallas combinadas de la zona del TUM.
    static readonly Bounds Viejo = new Bounds(new Vector3(391f, 3.75f, 202f), new Vector3(13f, 7.7f, 16f));
    static readonly string[] ObjetosViejos = { "Crema", "Techo", "Toldo", "CartelADIUNGS" };
    static readonly string[] MallasConViejo = { "Ladrillo", "Marcos", "Vidrio", "Galvanizado", "MetalNegro" };

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

    // UV proyectadas en el mundo, 2 m por unidad como el resto de la zona del TUM; el ladrillo y la malla, más chicos.
    static readonly Dictionary<string, float> EscalaUV = new Dictionary<string, float> { { "Ladrillo", 1f }, { "RejaMalla", 1f } };

    static Vector2 Proyectar(string mat, Vector3 p, Vector3 n)
    {
        float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
        Vector2 r = ay >= ax && ay >= az ? new Vector2(p.x, p.z) : ax >= az ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y);
        return r * (EscalaUV.TryGetValue(mat, out var e) ? e : .5f);
    }

    static void Tri(string mat, Vector3 a, Vector3 b, Vector3 c, Vector3 afuera)
    {
        a += corrimiento; b += corrimiento; c += corrimiento;
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
        a += corrimiento; b += corrimiento; c += corrimiento; d += corrimiento;
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
        if (ex.magnitude < .001f || ey.magnitude < .001f || ez.magnitude < .001f) return;
        Cara(mat, c, ey, ez, ex); Cara(mat, c, ey, ez, -ex);
        Cara(mat, c, ex, ez, ey); Cara(mat, c, ex, ez, -ey);
        Cara(mat, c, ex, ey, ez); Cara(mat, c, ex, ey, -ez);
    }

    static void Cara(string mat, Vector3 c, Vector3 u, Vector3 v, Vector3 w)
    {
        var o = c + w;
        Quad(mat, o - u - v, o + u - v, o + u + v, o - u + v, w);
    }

    static void Box(string mat, Vector3 min, Vector3 max)
    {
        var c = (min + max) / 2; var h = (max - min) / 2;
        Caja(mat, c, Vector3.right * Mathf.Abs(h.x), Vector3.up * Mathf.Abs(h.y), Vector3.forward * Mathf.Abs(h.z));
    }

    static void Box(string mat, float x0, float y0, float z0, float x1, float y1, float z1) => Box(mat, new Vector3(x0, y0, z0), new Vector3(x1, y1, z1));

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
        if (alfeizar) Caja("PinturaBlanca", b - up * .05f + n * .08f, r * (w / 2 + .08f), up * .05f, n * .08f);
    }

    static void Muros()
    {
        // El cuerpo, con el entrante de adelante a la izquierda (balcón arriba, cubierto abajo).
        Box("Ladrillo", X0, 0, Z0, X1, H, ZR);
        Box("Ladrillo", XR, 0, ZR, X1, H, Z1);
        // Zócalo de cemento.
        Box("Hormigon", X0 - .03f, 0, Z0 - .03f, X1 + .03f, .3f, Z0);
        Box("Hormigon", XR - .03f, 0, Z1, X1 + .03f, .3f, Z1 + .03f);
        Box("Hormigon", X0 - .03f, 0, Z0, X0, .3f, ZR);
        Box("Hormigon", XR - .03f, 0, ZR, XR, .3f, Z1);
        // Techo blanco y fino que sobresale; arriba, la membrana.
        Box("PinturaBlanca", X0 - .6f, H, Z0 - .5f, X1 + .5f, H + .22f, Z1 + .5f);
        Box("Techo", X0 - .5f, H + .22f, Z0 - .4f, X1 + .4f, H + .24f, Z1 + .4f);
        // Bajadas de agua en las esquinas de atrás.
        Cilindro("PinturaBlanca", new Vector3(X1 + .1f, 0, Z0 - .12f), new Vector3(X1 + .1f, H, Z0 - .12f), .05f, 8);
        Cilindro("PinturaBlanca", new Vector3(X1 + .3f, 0, Z1 + .12f), new Vector3(X1 + .3f, H, Z1 + .12f), .05f, 8);
    }

    // Fachada de atrás (foto 1): tres paños crema entre pilares de ladrillo, con ventanas abajo y arriba y toldos grises.
    static void FachadaTrasera()
    {
        Vector3 n = Vector3.right, r = Vector3.forward;
        var pilares = new float[4];
        for (int k = 0; k < 4; k++)
        {
            pilares[k] = Z0 + .3f + k * (Z1 - Z0 - .6f) / 3;
            Box("Ladrillo", X1, 0, pilares[k] - .3f, X1 + .18f, H, pilares[k] + .3f);
        }
        Box("Hormigon", X1, 0, Z0, X1 + .03f, .3f, Z1);
        for (int k = 0; k < 3; k++)
        {
            float za = pilares[k] + .3f, zb = pilares[k + 1] - .3f, zc = (za + zb) / 2, w = 2.5f;
            float xc = X1 + .06f;
            Box("HormigonBeige", X1, .3f, za, xc, H, zb);
            Ventana(new Vector3(xc, .95f, zc), n, r, w, 1.55f, 3, true);
            Ventana(new Vector3(xc, 3.95f, zc), n, r, w, 1.45f, 3, true);
            // Toldo gris de chapa sobre la ventana de arriba, con dos ménsulas.
            var baja = new Vector3(.85f, -.3f, 0); var grosor = new Vector3(.3f, .85f, 0).normalized * .02f;
            Caja("Galvanizado", new Vector3(xc, 5.7f, zc) + baja / 2, baja / 2, grosor, r * (w / 2 + .15f));
            for (int s = -1; s <= 1; s += 2)
                Barra("Galvanizado", new Vector3(xc, 5.15f, zc + s * (w / 2 + .1f)), new Vector3(xc + .8f, 5.4f, zc + s * (w / 2 + .1f)), .03f);
        }
    }

    // Frente (fotos 2, 6 y las de la vereda): galería con techito fino de perfiles sobre columnas finas (no es un piso),
    // ventanas con paños crema, puerta de vidrio, gabinete rojo, el cartel en el ladrillo y, a la izquierda, el entrante
    // con el balcón arriba y la pérgola, que sigue por encima del anexo.
    static void Frente()
    {
        Vector3 n = Vector3.left, r = Vector3.forward;
        const float xc = X0 - .005f, xg = X0 - 2.0f, yg = 2.95f;
        Box("Hormigon", X0 - 2.3f, 0, Z0, X0, .22f, ZR);
        Box("Hormigon", X0 - 2.3f, 0, ZR, XR, .22f, Z1);
        // Techito de la galería: chapa fina sobre perfiles blancos, de punta a punta del frente, con cuatro columnas finas;
        // termina en el borde del edificio.
        Box("ChapaClara", xg, yg, Z0 - .1f, X0, yg + .03f, Z1);
        Box("PinturaBlanca", xg - .05f, yg - .14f, Z0 - .1f, xg + .05f, yg + .04f, Z1);
        Box("PinturaBlanca", X0 - .08f, yg - .1f, Z0 - .1f, X0, yg + .04f, ZR);
        for (float z = Z0 - .05f; z < Z1 - .3f; z += 1.2f) Box("PinturaBlanca", xg, yg - .08f, z - .03f, X0, yg, z + .03f);
        Box("PinturaBlanca", xg, yg - .08f, Z1 - .06f, XR, yg, Z1);
        for (int k = 0; k < 4; k++)
        {
            float z = Z0 + .1f + k * (Z1 - Z0 - .15f) / 3;
            Cilindro("PinturaBlanca", new Vector3(xg, .22f, z), new Vector3(xg, yg - .1f, z), .06f, 10);
        }
        // Planta baja.
        Ventana(new Vector3(xc, .95f, 197.4f), n, r, 2.8f, 1.5f, 2, true);
        Ventana(new Vector3(xc, .95f, 203.85f), n, r, 2.1f, 1.5f, 2, true);
        Box("HormigonBeige", X0 - .04f, .3f, 195.9f, X0, .88f, 198.9f);
        Box("HormigonBeige", X0 - .04f, .3f, 202.7f, X0, .88f, 205.0f);
        Ventana(new Vector3(xc, .22f, 201.05f), n, r, 1.5f, 2.25f, 2, false, "Galvanizado");
        Box("Galvanizado", X0 - .2f, 2.46f, 200.25f, X0, 2.56f, 201.85f);
        Box("MetalNegro", X0 - .14f, 1.0f, 200.92f, X0 - .1f, 1.35f, 200.98f);
        Box("MetalNegro", X0 - .14f, 1.0f, 201.12f, X0 - .1f, 1.35f, 201.18f);
        // Gabinete rojo de la manguera, al lado de la puerta.
        Box("CartelRojo", X0 - .16f, 1.0f, 202.08f, X0, 1.85f, 202.6f);
        Box("Vidrio", X0 - .17f, 1.1f, 202.15f, X0 - .16f, 1.75f, 202.53f);
        // Planta alta: dos ventanas con paños crema arriba y abajo (la de la derecha con toldo) y el cartel en el ladrillo.
        foreach (var (zc, w) in new[] { (197.4f, 2.8f), (203.85f, 2.1f) })
        {
            Ventana(new Vector3(xc, 3.9f, zc), n, r, w, 1.35f, 2, true);
            Box("HormigonBeige", X0 - .04f, 3.2f, zc - w / 2 - .1f, X0, 3.82f, zc + w / 2 + .1f);
            Box("HormigonBeige", X0 - .04f, 5.35f, zc - w / 2 - .1f, X0, H, zc + w / 2 + .1f);
        }
        var baja = new Vector3(-.8f, -.3f, 0); var grosor = new Vector3(-.3f, .8f, 0).normalized * .02f;
        Caja("Galvanizado", new Vector3(X0 - .04f, 5.5f, 197.4f) + baja / 2, baja / 2, grosor, r * 1.55f);
        foreach (float z in new[] { 195.95f, 198.85f })
            Barra("Galvanizado", new Vector3(X0 - .04f, 4.95f, z), new Vector3(X0 - .8f, 5.2f, z), .03f);
        // Mirando el frente, la derecha es -z: la u del cartel va de +z a -z.
        float xl = X0 - .01f;
        Quad("CartelADIUNGS", new Vector3(xl, 4.15f, 202.55f), new Vector3(xl, 4.15f, 199.15f), new Vector3(xl, 4.88f, 199.15f), new Vector3(xl, 4.88f, 202.55f), n,
            new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
        Balcon(yg);
    }

    static void Baranda(Vector3 a, Vector3 b, float y0, float alto, float paso)
    {
        int postes = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / paso));
        for (int i = 0; i <= postes; i++)
        {
            var p = Vector3.Lerp(a, b, (float)i / postes);
            Cilindro("Galvanizado", new Vector3(p.x, y0, p.z), new Vector3(p.x, y0 + alto, p.z), .025f, 6);
        }
        Cilindro("Galvanizado", new Vector3(a.x, y0 + alto, a.z), new Vector3(b.x, y0 + alto, b.z), .03f, 8);
        Cilindro("Galvanizado", new Vector3(a.x, y0 + alto * .5f, a.z), new Vector3(b.x, y0 + alto * .5f, b.z), .018f, 6);
        Cilindro("Galvanizado", new Vector3(a.x, y0 + .1f, a.z), new Vector3(b.x, y0 + .1f, b.z), .018f, 6);
    }

    // Balcón dentro del entrante, al ras de la fachada (se sale desde adentro: afuera no hay escalera), y la pérgola de
    // policarbonato, que sale hacia el costado por encima del anexo.
    static void Balcon(float yg)
    {
        const float zg1 = AZ0 + .4f;
        Vector3 n = Vector3.left, r = Vector3.forward;
        Box("Hormigon", X0, 3.0f, ZR, XR, Piso, Z1);
        Ventana(new Vector3(XR - .005f, Piso, 207.05f), n, r, 1.0f, 2.2f, 2, false, "Galvanizado");
        Ventana(new Vector3(XR - .005f, .95f, 207.05f), n, r, 1.4f, 1.5f, 2, true);
        Baranda(new Vector3(X0 + .04f, 0, ZR + .05f), new Vector3(X0 + .04f, 0, Z1 - .04f), Piso, 1.0f, .6f);
        Baranda(new Vector3(X0 + .04f, 0, Z1 - .04f), new Vector3(XR - .05f, 0, Z1 - .04f), Piso, 1.0f, .6f);
        // Pérgola: perfiles blancos con policarbonato, sobre postes finos.
        const float yp = 5.65f, xp = X0 - 1.3f;
        Vector3 a = new Vector3(XR, yp, ZR), b = new Vector3(xp, yp, ZR), c = new Vector3(xp, yp, zg1), d = new Vector3(XR, yp, zg1);
        Quad("Policarbonato", a, b, c, d, Vector3.up); Quad("Policarbonato", a, b, c, d, Vector3.down);
        Box("PinturaBlanca", xp - .04f, yp - .14f, ZR, xp + .04f, yp, zg1);
        Box("PinturaBlanca", xp, yp - .1f, ZR, XR, yp, ZR + .06f);
        Box("PinturaBlanca", xp, yp - .1f, zg1 - .06f, XR, yp, zg1);
        for (float z = ZR + .8f; z < zg1 - .3f; z += .8f) Box("PinturaBlanca", xp, yp - .07f, z - .025f, XR, yp, z + .025f);
        // Postes: sobre el techito de la galería, uno hasta el piso en la punta y, sobre el muro del anexo, los de atrás.
        foreach (float z in new[] { ZR + .1f, Z1 - .05f })
            Cilindro("PinturaBlanca", new Vector3(xp + .05f, yg + .04f, z), new Vector3(xp + .05f, yp - .14f, z), .04f, 8);
        Cilindro("PinturaBlanca", new Vector3(xp + .05f, 0, zg1 - .05f), new Vector3(xp + .05f, yp - .14f, zg1 - .05f), .04f, 8);
        foreach (float x in new[] { AX0 + .15f, XR - .05f })
            Cilindro("PinturaBlanca", new Vector3(x, AltoA + .06f, zg1 - .05f), new Vector3(x, yp - .1f, zg1 - .05f), .04f, 8);
    }

    // Costado +z (fotos 1 y 5): aires en estantes, escalera de metal con descanso y techito de policarbonato.
    static void CostadoAires()
    {
        Vector3 n = Vector3.forward, r = Vector3.right;
        const float zs0 = Z1 + .1f, zs1 = Z1 + 1.1f, xe = 387.9f, huella = .215f, xd0 = 391.55f, xd1 = 393.1f;
        const int escalones = 17; float subida = Piso / (escalones + 1);
        for (int i = 0; i < escalones; i++)
            Box("Galvanizado", xe + i * huella, (i + 1) * subida - .04f, zs0, xe + (i + 1) * huella + .03f, (i + 1) * subida, zs1);
        Box("Galvanizado", xd0, Piso - .06f, Z1 + .05f, xd1, Piso, zs1 + .1f);
        foreach (float z in new[] { zs0 + .02f, zs1 - .02f })
            Viga("Galvanizado", new Vector3(xe - .1f, -.05f, z), new Vector3(xd0, Piso - .12f, z), .2f, .04f);
        Cilindro("Galvanizado", new Vector3(xd1 - .04f, 0, zs1 + .06f), new Vector3(xd1 - .04f, Piso - .06f, zs1 + .06f), .045f, 8);
        Cilindro("Galvanizado", new Vector3(xd0 + .05f, 0, zs1 + .06f), new Vector3(xd0 + .05f, Piso - .06f, zs1 + .06f), .045f, 8);
        // Pasamanos de afuera y baranda del descanso.
        for (int i = 0; i <= escalones; i += 3)
        {
            float x = xe + (i + .5f) * huella, y = Mathf.Min(i + 1, escalones) * subida;
            Cilindro("Galvanizado", new Vector3(x, y, zs1 - .03f), new Vector3(x, y + .95f, zs1 - .03f), .022f, 6);
        }
        Cilindro("Galvanizado", new Vector3(xe + .1f, subida + .95f, zs1 - .03f), new Vector3(xd0, Piso + .95f, zs1 - .03f), .03f, 8);
        Baranda(new Vector3(xd0, 0, zs1 + .07f), new Vector3(xd1 - .03f, 0, zs1 + .07f), Piso, .95f, .55f);
        Baranda(new Vector3(xd1 - .03f, 0, zs1 + .07f), new Vector3(xd1 - .03f, 0, Z1 + .05f), Piso, .95f, .55f);
        // Puerta de arriba, en el descanso.
        Box("PuertaGris", 391.95f, Piso, Z1, 392.85f, 5.25f, Z1 + .04f);
        Box("PinturaBlanca", 391.88f, Piso, Z1, 391.95f, 5.32f, Z1 + .07f);
        Box("PinturaBlanca", 392.85f, Piso, Z1, 392.92f, 5.32f, Z1 + .07f);
        Box("PinturaBlanca", 391.88f, 5.25f, Z1, 392.92f, 5.32f, Z1 + .07f);
        Box("MetalNegro", 392.7f, 4.2f, Z1 + .04f, 392.76f, 4.26f, Z1 + .1f);
        // Techito de policarbonato sobre el descanso.
        Vector3 a = new Vector3(391.4f, 5.75f, Z1), b = new Vector3(391.4f, 5.45f, Z1 + 1.35f), c = new Vector3(393.25f, 5.45f, Z1 + 1.35f), d = new Vector3(393.25f, 5.75f, Z1);
        Quad("Policarbonato", a, b, c, d, Vector3.up); Quad("Policarbonato", a, b, c, d, Vector3.down);
        Barra("Galvanizado", b, c, .05f); Barra("Galvanizado", a, b, .05f); Barra("Galvanizado", d, c, .05f);
        Barra("Galvanizado", new Vector3(391.4f, 5.0f, Z1), b, .035f); Barra("Galvanizado", new Vector3(393.25f, 5.0f, Z1), c, .035f);
        Ventana(new Vector3(388.8f, 4.0f, Z1 + .005f), n, r, 1.2f, 1.2f, 2, true);

        // Seis aires en tres niveles, sobre estantes de perfiles.
        const float rx0 = 393.45f, rx1 = 396.05f, zf = Z1 + .48f;
        foreach (float x in new[] { rx0, rx1 })
            Box("Galvanizado", x - .025f, 0, zf - .025f, x + .025f, 3.95f, zf + .025f);
        float[] niveles = { .25f, 1.65f, 3.05f };
        float[] columnas = { 394.05f, 395.45f };
        Box("PinturaBlanca", 393.25f, .3f, Z1, 393.35f, H - .3f, Z1 + .08f);
        foreach (float yb in niveles)
        {
            foreach (float z in new[] { Z1 + .12f, Z1 + .4f, zf })
                Barra("Galvanizado", new Vector3(rx0, yb - .025f, z), new Vector3(rx1, yb - .025f, z), .05f);
            foreach (float x in new[] { rx0, (rx0 + rx1) / 2, rx1 })
                Barra("Galvanizado", new Vector3(x, yb - .025f, Z1), new Vector3(x, yb - .025f, zf), .05f);
            Barra("Galvanizado", new Vector3(rx0, yb - .6f, Z1), new Vector3(rx0, yb - .05f, zf - .03f), .035f);
            foreach (float xc in columnas)
            {
                float x0 = xc - .475f, x1 = xc + .475f;
                Box("PanelBlanco", x0, yb, Z1 + .07f, x1, yb + .68f, Z1 + .4f);
                Cilindro("MetalNegro", new Vector3(xc - .1f, yb + .34f, Z1 + .4f), new Vector3(xc - .1f, yb + .34f, Z1 + .415f), .25f, 14);
                Box("PanelBlanco", xc - .38f, yb + .32f, Z1 + .41f, xc + .18f, yb + .36f, Z1 + .425f);
                Box("PanelBlanco", xc - .12f, yb + .08f, Z1 + .41f, xc - .08f, yb + .6f, Z1 + .425f);
                Box("PuertaGris", x1 - .2f, yb + .05f, Z1 + .4f, x1 - .03f, yb + .3f, Z1 + .42f);
                // Caños hasta la bajada blanca.
                Barra("MetalNegro", new Vector3(393.3f, yb + .12f, Z1 + .04f), new Vector3(x0 + .05f, yb + .12f, Z1 + .04f), .035f);
            }
        }
    }

    // Costado -z: ventanas arriba y abajo.
    static void CostadoSur()
    {
        Vector3 n = Vector3.back, r = Vector3.right;
        foreach (float x in new[] { 388.8f, 393.8f })
        {
            Ventana(new Vector3(x, .95f, Z0 - .005f), n, r, 1.6f, 1.5f, 2, true);
            Ventana(new Vector3(x, 3.95f, Z0 - .005f), n, r, 1.6f, 1.4f, 2, true);
        }
    }

    // Anexo aparte, entre ADIUNGS y el pasillo que va al patio de atrás (fotos de la enredadera y de la puerta blanca):
    // un muro macizo tan largo como ADIUNGS, con marco de caños, malla y la enredadera encima; en el medio de la cara del
    // pasillo, la casilla de ladrillo con la puerta doble blanca, el tablero eléctrico, el tubo de luz y el reflector.
    static void Anexo()
    {
        var rnd = new System.Random(343);
        Box("HormigonViejo", AX0, 0, AZ0, BX0, AltoA, AZ1 - .03f);
        Box("HormigonViejo", BX1, 0, AZ0, AX1, AltoA, AZ1 - .03f);
        Box("HormigonViejo", BX0, 0, AZ0, BX1, AltoA, BZ0);
        Box("HormigonViejo", AX0 - .04f, AltoA, AZ0 - .04f, AX1 + .04f, AltoA + .06f, AZ1 + .01f);
        // Hacia ADIUNGS y hacia el patio, tapada entera; al frente, las hojas arriba y los tallos abajo; del lado del
        // pasillo, más rala, con los tallos y la malla a la vista.
        CaraEnredadera(new Vector3(AX1, 0, AZ0), new Vector3(AX0, 0, AZ0), Vector3.back, 1f, .2f, .3f, rnd);
        CaraEnredadera(new Vector3(AX0, 0, AZ0), new Vector3(AX0, 0, AZ1), Vector3.left, .85f, 1.3f, .4f, rnd);
        CaraEnredadera(new Vector3(AX1, 0, AZ1), new Vector3(AX1, 0, AZ0), Vector3.right, 1f, .3f, .4f, rnd);
        CaraEnredadera(new Vector3(AX0, 0, AZ1), new Vector3(BX0, 0, AZ1), Vector3.forward, .55f, 1.2f, .35f, rnd);
        CaraEnredadera(new Vector3(BX1, 0, AZ1), new Vector3(AX1, 0, AZ1), Vector3.forward, .55f, 1.2f, .35f, rnd);
        Copete(rnd);
        Casilla();
    }

    // Una cara del muro: malla pegada, marco de caños por delante, troncos, tallos y hojas del lado de «afuera».
    // «tupida» va de 0 a 1; abajo de «sinHojasAbajo» casi no hay hojas, solo tallos.
    static void CaraEnredadera(Vector3 a, Vector3 b, Vector3 afuera, float tupida, float sinHojasAbajo, float maxSalida, System.Random rnd)
    {
        float largo = Vector3.Distance(a, b); var dir = (b - a).normalized; var up = Vector3.up;
        Caja("RejaMalla", (a + b) / 2 + up * (AltoA / 2) + afuera * .07f, dir * largo / 2, up * (AltoA - .1f) / 2, afuera * .008f);
        int postes = Mathf.Max(1, Mathf.RoundToInt(largo / 2.3f));
        for (int k = 0; k <= postes; k++)
        {
            var p = Vector3.Lerp(a, b, (float)k / postes) + afuera * .12f;
            Caja("Galvanizado", p + up * (AltoA / 2), dir * .035f, up * AltoA / 2, afuera * .035f);
        }
        foreach (float y in new[] { .12f, 1.0f, 1.95f, AltoA - .05f })
            Caja("Galvanizado", (a + b) / 2 + up * y + afuera * .12f, dir * largo / 2, up * .025f, afuera * .025f);
        // Troncos retorcidos que suben desde el pasto.
        int troncos = Mathf.Max(1, Mathf.RoundToInt(largo / 2f));
        for (int k = 0; k < troncos; k++)
        {
            var s = Vector3.Lerp(a, b, (k + .2f + (float)rnd.NextDouble() * .6f) / troncos) + afuera * .2f;
            float alto = 1.2f + (float)rnd.NextDouble() * 1.4f, r0 = .05f + (float)rnd.NextDouble() * .025f;
            for (int i = 0; i < 6; i++)
            {
                var q = s + up * (alto / 6) + dir * (float)(rnd.NextDouble() - .5) * .35f + afuera * (float)(rnd.NextDouble() - .5) * .05f;
                Cilindro("Corteza", s, q, r0 * (1 - i * .12f), 6); s = q;
            }
        }
        // Tallos sobre la malla.
        int tallos = Mathf.Max(1, Mathf.RoundToInt(largo / .75f));
        for (int k = 0; k < tallos; k++)
        {
            float w = .9f + (float)rnd.NextDouble() * .5f, h = 1.9f + (float)rnd.NextDouble() * .9f;
            float d = Mathf.Clamp((k + (float)rnd.NextDouble()) * largo / tallos, w * .45f, largo - w * .45f);
            Tarjeta("EnredaderaTallos", a + dir * d + up * (h / 2) + afuera * (.16f + (float)rnd.NextDouble() * .05f), dir, afuera, w, h,
                (float)(rnd.NextDouble() - .5) * .15f, 0, new Rect(0, 0, 1, 1), rnd.NextDouble() < .5);
        }
        // Hojas, más tupidas arriba.
        int hojas = Mathf.RoundToInt(largo * 15 * tupida * tupida);
        for (int k = 0; k < hojas; k++)
        {
            float w = .8f + (float)rnd.NextDouble() * .7f, h = .7f + (float)rnd.NextDouble() * .6f, r = (float)rnd.NextDouble();
            float d = Mathf.Clamp((float)rnd.NextDouble() * largo, w * .4f, largo - w * .4f);
            float y = Mathf.Lerp(sinHojasAbajo, AltoA + .05f, 1 - r * r);
            Tarjeta("Enredadera", a + dir * d + up * y + afuera * (.14f + (float)rnd.NextDouble() * (maxSalida - .14f)), dir, afuera, w, h,
                (float)(rnd.NextDouble() - .5) * .8f, .1f + (float)rnd.NextDouble() * .3f, Cuadrante(rnd), rnd.NextDouble() < .5);
        }
        // Lo que cuelga del borde de arriba.
        for (float d = .25f; d < largo - .2f; d += .45f)
        {
            float w = .7f + (float)rnd.NextDouble() * .4f, h = .7f + (float)rnd.NextDouble() * .6f;
            Tarjeta("Enredadera", a + dir * d + up * (AltoA + .15f - h * .4f) + afuera * (.25f + (float)rnd.NextDouble() * .12f), dir, afuera, w, h,
                (float)(rnd.NextDouble() - .5) * .5f, .35f + (float)rnd.NextDouble() * .3f, Cuadrante(rnd), rnd.NextDouble() < .5);
        }
    }

    // Arriba del muro, la enredadera lo tapa todo y le salen ramas sueltas.
    static void Copete(System.Random rnd)
    {
        for (float x = AX0 + .3f; x < AX1; x += .75f)
        for (float z = AZ0 + .3f; z < AZ1; z += .75f)
        {
            float px = x + (float)(rnd.NextDouble() - .5) * .4f, pz = z + (float)(rnd.NextDouble() - .5) * .4f;
            if (px > BX0 - .2f && px < BX1 + .2f && pz > BZ0 - .2f) continue;
            float yaw = (float)rnd.NextDouble() * Mathf.PI * 2, t = (float)(rnd.NextDouble() - .5) * .7f, w = 1f + (float)rnd.NextDouble() * .5f;
            var r = new Vector3(Mathf.Cos(yaw), 0, Mathf.Sin(yaw)); var u = new Vector3(-Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
            u = (u * Mathf.Cos(t) + Vector3.up * Mathf.Sin(t)).normalized;
            TarjetaEjes("Enredadera", new Vector3(px, AltoA + .1f + (float)rnd.NextDouble() * .2f, pz), r, u, w, w * .85f, Cuadrante(rnd), rnd.NextDouble() < .5);
        }
        for (int i = 0; i < 28; i++)
        {
            float px = AX0 + (float)rnd.NextDouble() * (AX1 - AX0), pz = AZ0 + (float)rnd.NextDouble() * (AZ1 - AZ0);
            if (px > BX0 - .2f && px < BX1 + .2f && pz > BZ0 - .2f) continue;
            float yaw = (float)rnd.NextDouble() * Mathf.PI * 2, h = .5f + (float)rnd.NextDouble() * .6f;
            var r = new Vector3(Mathf.Cos(yaw), 0, Mathf.Sin(yaw)); var n = new Vector3(-Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
            var u = (Vector3.up + n * (float)(rnd.NextDouble() - .5) * .6f).normalized;
            TarjetaEjes("Enredadera", new Vector3(px, AltoA + .1f + h / 2, pz), r, u, .45f + (float)rnd.NextDouble() * .3f, h, Cuadrante(rnd), rnd.NextDouble() < .5);
        }
    }

    // Casilla de ladrillo en el medio de la cara del pasillo (fotos de la puerta blanca), metida medio metro respecto del
    // muro de enredadera; la losa de adelante, del mismo hormigón que el camino, entra en el hueco.
    static void Casilla()
    {
        const float zc = AZ1 - Hueco;
        Box("Ladrillo", BX0, 0, BZ0, BX1, AltoA + .12f, zc);
        Box("Hormigon", BX0 - .06f, AltoA + .12f, BZ0, BX1 + .06f, AltoA + .2f, zc + .1f);
        Box("HormigonViejo", BX0, 0, zc, BX1, .08f, AZ1);
        Box("HormigonViejo", BX0 - .3f, 0, AZ1, BX1 + .1f, .08f, AZ1 + 1.0f);
        const float xa = 390.4f, xm = 391.15f, xb = 391.9f, alto = 2.2f;
        Box("PinturaBlanca", xa, .08f, zc, xm - .005f, alto, zc + .04f);
        Box("PinturaBlanca", xm + .005f, .08f, zc, xb, alto, zc + .04f);
        Box("PinturaBlanca", xa - .07f, .08f, zc, xa, alto + .07f, zc + .06f);
        Box("PinturaBlanca", xb, .08f, zc, xb + .07f, alto + .07f, zc + .06f);
        Box("PinturaBlanca", xa - .07f, alto, zc, xb + .07f, alto + .07f, zc + .06f);
        Box("MetalNegro", xm - .005f, .08f, zc + .035f, xm + .005f, alto, zc + .045f);
        Box("MetalNegro", xm + .05f, 1.0f, zc + .04f, xm + .09f, 1.15f, zc + .08f);
        // Tubo de luz sobre la puerta y el agujero de ventilación del ladrillo.
        Box("PinturaBlanca", xa - .05f, 2.45f, zc, xb + .05f, 2.53f, zc + .09f);
        Box("VidrioLuz", xa, 2.43f, zc + .01f, xb, 2.45f, zc + .08f);
        Cilindro("MetalNegro", new Vector3(392.4f, 2.5f, zc - .01f), new Vector3(392.4f, 2.5f, zc + .005f), .07f, 10);
        // Tablero eléctrico sobre patas, entre la puerta y la enredadera.
        Box("PuertaGris", 392.2f, .35f, zc + .02f, 392.8f, 1.95f, zc + .32f);
        Box("MetalNegro", 392.49f, .4f, zc + .32f, 392.51f, 1.9f, zc + .325f);
        Box("CartelRojo", 392.26f, 1.6f, zc + .32f, 392.42f, 1.75f, zc + .325f);
        Box("MetalNegro", 392.27f, 1.1f, zc + .32f, 392.3f, 1.25f, zc + .34f);
        foreach (float x in new[] { 392.25f, 392.75f })
            foreach (float z in new[] { zc + .05f, zc + .29f })
                Box("MetalNegro", x - .015f, .08f, z - .015f, x + .015f, .35f, z + .015f);
        // Reflector en un caño arriba de la casilla, apuntando al pasillo.
        var o = new Vector3(392.75f, AltoA + .12f, zc - .1f); var e = new Vector3(392.85f, AltoA + .55f, zc + .3f);
        Barra("MetalNegro", o, e, .04f);
        var f = new Vector3(0, -.5f, .866f); var arriba = new Vector3(0, .866f, .5f);
        Caja("MetalNegro", e + f * .02f, Vector3.right * .17f, arriba * .13f, f * .06f);
        Caja("VidrioLuz", e + f * .085f, Vector3.right * .15f, arriba * .11f, f * .008f);
    }

    static Rect Cuadrante(System.Random rnd)
    {
        int i = rnd.Next(4);
        return new Rect((i % 2) * .5f + .004f, (i / 2) * .5f + .004f, .492f, .492f);
    }

    // Tarjeta de hojas sobre una cara: «giro» la rota en el plano del muro y «inclinacion» echa la punta hacia afuera.
    static void Tarjeta(string mat, Vector3 c, Vector3 dir, Vector3 afuera, float w, float h, float giro, float inclinacion, Rect uv, bool espejo)
    {
        var rot = Quaternion.AngleAxis(giro * Mathf.Rad2Deg, afuera);
        var r = rot * dir; var u = rot * Vector3.up;
        u = (u * Mathf.Cos(inclinacion) + afuera * Mathf.Sin(inclinacion)).normalized;
        TarjetaEjes(mat, c, r, u, w, h, uv, espejo);
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

    // Camino de tablones (fotos del frente y del pasillo): sale del frente de ADIUNGS, hace el zigzag por delante del
    // anexo (reemplaza al zigzag liso que había) y, en el codo, dobla hacia el pasillo entre el anexo y el Módulo 7 hasta el
    // borde del caminito del patio, que desde ahí sigue hacia el fondo. Es igual a ese caminito: hormigón viejo, 1,8 m de
    // ancho y tablones atravesados de 0,24 m. Arranca metido debajo del frente. Va en coordenadas del mapa.
    static readonly Vector2[] CaminoPuntos = { new Vector2(382.0f, 202.0f), new Vector2(381.5f, 202.6f), new Vector2(376.0f, 209.0f), new Vector2(379.4f, 213.6f), new Vector2(397.4f, 213.6f), new Vector2(401.0f, 216.9f), new Vector2(401.0f, 219.5f) };

    // Franja de camino: ancho y alto por punto, codos a inglete. Las puntas quedan metidas en los caminos que empalma.
    static void Franja(string mat, Vector2[] p, float[] anchos, float[] altos, Vector2? izq0 = null, Vector2? der0 = null, Vector2? izqN = null, Vector2? derN = null)
    {
        int n = p.Length; var izq = new Vector3[n]; var der = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            if (i == 0 && izq0.HasValue && der0.HasValue)
            {
                izq[0] = new Vector3(izq0.Value.x, altos[0], izq0.Value.y); der[0] = new Vector3(der0.Value.x, altos[0], der0.Value.y);
                continue;
            }
            if (i == n - 1 && izqN.HasValue && derN.HasValue)
            {
                izq[i] = new Vector3(izqN.Value.x, altos[i], izqN.Value.y); der[i] = new Vector3(derN.Value.x, altos[i], derN.Value.y);
                continue;
            }
            Vector2 d0 = (i > 0 ? p[i] - p[i - 1] : p[1] - p[0]).normalized, d1 = (i < n - 1 ? p[i + 1] - p[i] : d0).normalized;
            Vector2 n0 = new Vector2(-d0.y, d0.x), n1 = new Vector2(-d1.y, d1.x), m = (n0 + n1).normalized;
            float k = anchos[i] / 2 / Mathf.Max(.3f, Vector2.Dot(m, n1));
            izq[i] = new Vector3(p[i].x + m.x * k, altos[i], p[i].y + m.y * k);
            der[i] = new Vector3(p[i].x - m.x * k, altos[i], p[i].y - m.y * k);
        }
        for (int i = 0; i < n - 1; i++)
        {
            Quad(mat, izq[i], der[i], der[i + 1], izq[i + 1], Vector3.up);
            var s = izq[i] + izq[i + 1] - der[i] - der[i + 1]; s.y = 0; s.Normalize();
            Quad(mat, izq[i], izq[i + 1], new Vector3(izq[i + 1].x, 0, izq[i + 1].z), new Vector3(izq[i].x, 0, izq[i].z), s);
            Quad(mat, der[i], der[i + 1], new Vector3(der[i + 1].x, 0, der[i + 1].z), new Vector3(der[i].x, 0, der[i].z), -s);
        }
    }

    // Losa plana con la forma de un polígono convexo; «bordes[i]» dice si el lado que sale del vértice i queda a la vista.
    static void Losa(string mat, Vector2[] poly, float alto, bool[] bordes)
    {
        var a = new Vector3(poly[0].x, alto, poly[0].y);
        for (int i = 1; i + 1 < poly.Length; i++)
            Tri(mat, a, new Vector3(poly[i].x, alto, poly[i].y), new Vector3(poly[i + 1].x, alto, poly[i + 1].y), Vector3.up);
        var centro = Vector2.zero; foreach (var q in poly) centro += q; centro /= poly.Length;
        for (int i = 0; i < poly.Length; i++)
        {
            if (!bordes[i]) continue;
            Vector2 p0 = poly[i], p1 = poly[(i + 1) % poly.Length]; var afuera = (p0 + p1) / 2 - centro;
            Quad(mat, new Vector3(p0.x, alto, p0.y), new Vector3(p1.x, alto, p1.y), new Vector3(p1.x, 0, p1.y), new Vector3(p0.x, 0, p0.y), new Vector3(afuera.x, 0, afuera.y));
        }
    }

    static void CaminoAlPatio()
    {
        var p = CaminoPuntos;
        const float ancho = 1.8f, alto = .07f, tablon = .08f, paso = .245f; int n = p.Length;
        // Base continua, apenas más baja que los tablones.
        var anchos = new float[n]; var altos = new float[n];
        for (int i = 0; i < n; i++) { anchos[i] = ancho; altos[i] = alto; }
        Franja("HormigonViejo", p, anchos, altos);
        // Tablones atravesados; un tramo sí y otro no van 3 mm más bajos para que no titilen donde se cruzan en los codos.
        for (int i = 0; i + 1 < n; i++)
        {
            Vector2 a = p[i], b = p[i + 1]; float largo = Vector2.Distance(a, b); var d = (b - a) / largo;
            var d3 = new Vector3(d.x, 0, d.y); var lado = new Vector3(-d.y, 0, d.x);
            int k = Mathf.Max(1, Mathf.RoundToInt(largo / paso)); float l = largo / k, h = tablon - (i % 2) * .003f; // el último va 3 mm más bajo que el caminito del patio
            for (int j = 0; j < k; j++)
                Caja("HormigonViejo", new Vector3(a.x, h / 2, a.y) + d3 * (l * (j + .5f)), d3 * (l / 2 - .006f), Vector3.up * (h / 2), lado * (ancho / 2));
        }
    }

    // Uniones en el ripio (fotos del ripio y plano marcado):
    // - El camino que viene de los módulos (ArcoDeLosModulos) terminaba en el ripio y el sendero de hormigón del acceso 7a
    //   empezaba 6 m más al norte, sin tocarse. Este tramo es el mismo camino (misma vereda): arranca justo en su punta, a
    //   su altura, sigue su dirección bordeando el ripio y dobla suave hasta meterse en el sendero, angostándose y bajando
    //   a la altura de este.
    // - La vereda techada de Conector_2 se cortaba en el ripio: sigue en la misma dirección, cruzando el ripio, hasta la
    //   esquina donde se juntan los dos de arriba.
    // La punta del camino de los módulos termina en diagonal; el tramo arranca exactamente sobre ella, a su altura.
    static readonly Vector2 ArcoPuntaIzq = new Vector2(372.16f, 206.79f), ArcoPuntaDer = new Vector2(372.93f, 209.33f);
    static readonly Vector2[] Union7aPuntos = { new Vector2(372.55f, 208.06f), new Vector2(369.4f, 211.85f), new Vector2(368.15f, 213.45f) };
    static readonly float[] Union7aAnchos = { 2.65f, 2.3f, 2.0f };
    static readonly Vector2 ConectorPuntaIzq = new Vector2(359.8f, 207.6f), ConectorPuntaDer = new Vector2(361.3f, 206.4f);
    static readonly Vector2[] ConectorPuntos = { new Vector2(360.55f, 207.0f), new Vector2(363.6f, 210.1f), new Vector2(366.33f, 213.05f) };
    // Losa donde se juntan los dos con el sendero del 7a: un lado es la punta del sendero y los otros, las de los tramos.
    static readonly Vector2 SenderoPuntaIzq = new Vector2(365.90f, 215.04f), SenderoPuntaDer = new Vector2(367.70f, 214.96f);
    static readonly Vector2 TramoFinIzq = new Vector2(367.45f, 212.75f), TramoFinDer = new Vector2(368.85f, 214.15f);
    static readonly Vector2 ConectorFinIzq = new Vector2(365.65f, 213.75f), ConectorFinDer = new Vector2(367.0f, 212.35f);

    static void CaminoAl7a()
    {
        const float altoLosa = .06f; // la altura del sendero
        Franja("Vereda", Union7aPuntos, Union7aAnchos, new[] { .15f, .11f, altoLosa }, ArcoPuntaIzq, ArcoPuntaDer, TramoFinIzq, TramoFinDer);
        Franja("Vereda", ConectorPuntos, new[] { 1.92f, 1.9f, 1.94f }, new[] { .19f, .12f, altoLosa }, ConectorPuntaIzq, ConectorPuntaDer, ConectorFinIzq, ConectorFinDer);
        Losa("Vereda", new[] { SenderoPuntaIzq, SenderoPuntaDer, TramoFinDer, TramoFinIzq, ConectorFinDer, ConectorFinIzq }, altoLosa,
            new[] { false, true, false, true, false, true });
    }

    // Distancia de «q» a la línea del tramo, el medio ancho ahí y de qué lado queda (negativo: del lado del Módulo 7).
    static float DistanciaUnion(Vector2 q, out float medio, out float lado)
    {
        float mejor = float.MaxValue; medio = 1; lado = 0; var p = Union7aPuntos;
        for (int k = 0; k + 1 < p.Length; k++)
        {
            var ab = p[k + 1] - p[k]; float t = Mathf.Clamp01(Vector2.Dot(q - p[k], ab) / ab.sqrMagnitude);
            var c = p[k] + ab * t; float d = (q - c).magnitude;
            if (d < mejor) { mejor = d; medio = Mathf.Lerp(Union7aAnchos[k], Union7aAnchos[k + 1], t) / 2; var v = q - c; lado = ab.x * v.y - ab.y * v.x; }
        }
        return mejor;
    }

    // Terreno de la unión: sin pasto encima del tramo; del lado del Módulo 7, pasto (textura y matas) en vez de ripio,
    // así el ripio queda de un solo lado del camino, como en las fotos.
    static void TerrenoUnion7a(Transform zona)
    {
        var terreno = Terrain.activeTerrain; if (terreno == null) return;
        var td = terreno.terrainData; var o = terreno.transform.position;
        var w0 = zona.TransformPoint(new Vector3(358f, 0, 204f)); var w1 = zona.TransformPoint(new Vector3(376f, 0, 218f));
        bool LadoPasto(Vector2 q, float lado) => lado < 0 && q.x >= 367.9f && q.x <= 374.6f && q.y >= 209.4f && q.y <= 216.8f;
        // Textura.
        int aw = td.alphamapWidth, ah = td.alphamapHeight, capas = td.alphamapLayers;
        int ai0 = Mathf.Clamp(Mathf.FloorToInt((w0.x - o.x) / td.size.x * aw), 0, aw - 1), ai1 = Mathf.Clamp(Mathf.CeilToInt((w1.x - o.x) / td.size.x * aw), 0, aw - 1);
        int aj0 = Mathf.Clamp(Mathf.FloorToInt((w0.z - o.z) / td.size.z * ah), 0, ah - 1), aj1 = Mathf.Clamp(Mathf.CeilToInt((w1.z - o.z) / td.size.z * ah), 0, ah - 1);
        var alfa = td.GetAlphamaps(ai0, aj0, ai1 - ai0 + 1, aj1 - aj0 + 1);
        for (int j = 0; j <= aj1 - aj0; j++)
        for (int i = 0; i <= ai1 - ai0; i++)
        {
            var l = zona.InverseTransformPoint(new Vector3(o.x + (ai0 + i + .5f) / aw * td.size.x, 0, o.z + (aj0 + j + .5f) / ah * td.size.z));
            var q = new Vector2(l.x, l.z); float d = DistanciaUnion(q, out float medio, out float lado);
            if (!LadoPasto(q, lado)) continue;
            float t = Mathf.Clamp01((d - medio - .1f) / .9f); if (t <= 0) continue;
            for (int c = 0; c < capas; c++) alfa[j, i, c] = Mathf.Lerp(alfa[j, i, c], c == 0 ? 1 : 0, t);
        }
        td.SetAlphamaps(ai0, aj0, alfa);
        // Matas de pasto.
        int res = td.detailResolution;
        int i0 = Mathf.Clamp(Mathf.FloorToInt((w0.x - o.x) / td.size.x * res), 0, res - 1), i1 = Mathf.Clamp(Mathf.CeilToInt((w1.x - o.x) / td.size.x * res), 0, res - 1);
        int j0 = Mathf.Clamp(Mathf.FloorToInt((w0.z - o.z) / td.size.z * res), 0, res - 1), j1 = Mathf.Clamp(Mathf.CeilToInt((w1.z - o.z) / td.size.z * res), 0, res - 1);
        for (int capa = 0; capa < td.detailPrototypes.Length; capa++)
        {
            var datos = td.GetDetailLayer(i0, j0, i1 - i0 + 1, j1 - j0 + 1, capa); bool cambio = false;
            for (int j = 0; j <= j1 - j0; j++)
            for (int i = 0; i <= i1 - i0; i++)
            {
                var l = zona.InverseTransformPoint(new Vector3(o.x + (i0 + i + .5f) / res * td.size.x, 0, o.z + (j0 + j + .5f) / res * td.size.z));
                var q = new Vector2(l.x, l.z); float d = DistanciaUnion(q, out float medio, out float lado); int v = datos[j, i];
                float dc = float.MaxValue;
                for (int k = 0; k + 1 < ConectorPuntos.Length; k++)
                {
                    var ab = ConectorPuntos[k + 1] - ConectorPuntos[k]; float t = Mathf.Clamp01(Vector2.Dot(q - ConectorPuntos[k], ab) / ab.sqrMagnitude);
                    dc = Mathf.Min(dc, (ConectorPuntos[k] + ab * t - q).magnitude);
                }
                if (d <= medio + .25f || dc <= 1.2f || (q - new Vector2(367.0f, 213.7f)).magnitude <= 1.9f) v = 0;
                else if (LadoPasto(q, lado) && d > medio + .6f && v == 0) v = 6;
                if (v != datos[j, i]) { datos[j, i] = v; cambio = true; }
            }
            if (cambio) td.SetDetailLayer(i0, j0, capa, datos);
        }
        EditorUtility.SetDirty(td);
    }

    // Pasto del terreno: se saca del camino nuevo, de la losa de la casilla y de la franja que ahora ocupa ADIUNGS, y se
    // vuelve a poner en la franja que quedó libre entre ADIUNGS y el anexo. Las medidas van en el espacio del mapa.
    static void AjustarPasto(Transform zona)
    {
        var terreno = Terrain.activeTerrain; if (terreno == null) return;
        var td = terreno.terrainData; int res = td.detailResolution; var o = terreno.transform.position;
        var camino = CaminoPuntos; const float medioCamino = 1.15f;
        bool En(Vector2 q, float x0, float z0, float x1, float z1) => q.x >= x0 && q.x <= x1 && q.y >= z0 && q.y <= z1;
        var w0 = zona.TransformPoint(new Vector3(374f, 0, 190f)); var w1 = zona.TransformPoint(new Vector3(404f, 0, 222f));
        int i0 = Mathf.Clamp(Mathf.FloorToInt((w0.x - o.x) / td.size.x * res), 0, res - 1), i1 = Mathf.Clamp(Mathf.CeilToInt((w1.x - o.x) / td.size.x * res), 0, res - 1);
        int j0 = Mathf.Clamp(Mathf.FloorToInt((w0.z - o.z) / td.size.z * res), 0, res - 1), j1 = Mathf.Clamp(Mathf.CeilToInt((w1.z - o.z) / td.size.z * res), 0, res - 1);
        for (int capa = 0; capa < td.detailPrototypes.Length; capa++)
        {
            var datos = td.GetDetailLayer(i0, j0, i1 - i0 + 1, j1 - j0 + 1, capa); bool cambio = false;
            for (int j = 0; j <= j1 - j0; j++)
            for (int i = 0; i <= i1 - i0; i++)
            {
                var w = new Vector3(o.x + (i0 + i + .5f) / res * td.size.x, 0, o.z + (j0 + j + .5f) / res * td.size.z);
                var l = zona.InverseTransformPoint(w); var q = new Vector2(l.x, l.z); int antes = datos[j, i], v = antes;
                float dist = float.MaxValue;
                for (int k = 0; k + 1 < camino.Length; k++)
                {
                    var ab = camino[k + 1] - camino[k]; float t = Mathf.Clamp01(Vector2.Dot(q - camino[k], ab) / ab.sqrMagnitude);
                    dist = Mathf.Min(dist, (camino[k] + ab * t - q).magnitude);
                }
                if (dist <= medioCamino) v = 0;
                else if (En(q, 384.0f, 192.0f, 396.7f, 194.6f)) v = 0;                       // donde ahora está ADIUNGS
                else if (En(q, 389.8f, AZ1 + DZ - Hueco - .1f, 393.2f, AZ1 + DZ + 1.1f)) v = 0; // losa de la casilla
                else if (En(q, 388.1f, Z1 + DZ + .2f, 396.5f, AZ0 + DZ - .1f) && v == 0) v = 6; // franja libre al costado
                else if (En(q, 398.6f, 219.4f, 400.1f, 221.4f) && v == 0) v = 6;               // donde estaba el tramo hasta la pared
                if (v != antes) { datos[j, i] = v; cambio = true; }
            }
            if (cambio) td.SetDetailLayer(i0, j0, capa, datos);
        }
        EditorUtility.SetDirty(td);
    }

    // Texturas recortadas de la enredadera (hojas compuestas como las de la glicina de las fotos, y tallos); se generan
    // una sola vez y quedan como assets.
    static void MaterialesEnredadera()
    {
        CrearMaterialRecortado("Enredadera", TexturaHojas);
        CrearMaterialRecortado("EnredaderaTallos", TexturaTallos);
    }

    static void CrearMaterialRecortado(string nombre, Func<Texture2D> generar)
    {
        string matPath = "Assets/Materials/Uni/Uni_" + nombre + ".mat", texPath = "Assets/Materials/Uni/Uni_" + nombre + ".png";
        if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null) return;
        var tex = generar();
        System.IO.File.WriteAllBytes(texPath, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
        imp.alphaIsTransparency = true; imp.mipmapEnabled = true; imp.mipMapsPreserveCoverage = true; imp.alphaTestReferenceValue = .5f;
        imp.wrapMode = TextureWrapMode.Clamp; imp.SaveAndReimport();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Uni_" + nombre };
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_AlphaClip", 1); mat.SetFloat("_Cutoff", .5f); mat.EnableKeyword("_ALPHATEST_ON");
        mat.SetFloat("_Smoothness", .12f);
        mat.SetOverrideTag("RenderType", "TransparentCutout");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        AssetDatabase.CreateAsset(mat, matPath);
    }

    // Cuatro matas distintas (una por cuadrante) de hojas compuestas, con el borde irregular.
    static Texture2D TexturaHojas()
    {
        const int N = 1024, C = 512; var px = new Color32[N * N]; var fondo = new Color32(52, 84, 32, 0);
        for (int i = 0; i < px.Length; i++) px[i] = fondo;
        var rnd = new System.Random(7);
        for (int q = 0; q < 4; q++)
        {
            int ox = (q % 2) * C, oy = (q / 2) * C; var lim = new RectInt(ox + 4, oy + 4, C - 8, C - 8);
            var centro = new Vector2(ox + C / 2, oy + C / 2); float fase = (float)rnd.NextDouble() * 6;
            for (int k = 0; k < 120; k++)
            {
                float th = (float)rnd.NextDouble() * Mathf.PI * 2, rmax = 105 * (.75f + .25f * Mathf.Sin(3 * th + fase));
                var o = centro + new Vector2(Mathf.Cos(th), Mathf.Sin(th)) * Mathf.Sqrt((float)rnd.NextDouble()) * rmax;
                var col = Color.Lerp(new Color(.15f, .29f, .1f), new Color(.4f, .55f, .22f), (float)rnd.NextDouble());
                HojaCompuesta(px, N, lim, o, th + (float)(rnd.NextDouble() - .5) * 1.8f, 45 + (float)rnd.NextDouble() * 45, col, rnd);
            }
        }
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, true); tex.SetPixels32(px); tex.Apply(); return tex;
    }

    // Tallos retorcidos que suben, con algunas hojas arriba.
    static Texture2D TexturaTallos()
    {
        const int W = 512, Al = 1024; var px = new Color32[W * Al]; var fondo = new Color32(95, 78, 60, 0);
        for (int i = 0; i < px.Length; i++) px[i] = fondo;
        var rnd = new System.Random(11); var lim = new RectInt(4, 0, W - 8, Al - 4);
        for (int k = 0; k < 9; k++)
            Tallo(px, W, lim, new Vector2(30 + (float)rnd.NextDouble() * (W - 60), 0), Mathf.PI / 2 + (float)(rnd.NextDouble() - .5) * .5f,
                Al * (.7f + (float)rnd.NextDouble() * .3f), 8f, rnd, 2);
        for (int k = 0; k < 16; k++)
        {
            var o = new Vector2(70 + (float)rnd.NextDouble() * (W - 140), Al * (.35f + (float)rnd.NextDouble() * .5f));
            var col = Color.Lerp(new Color(.2f, .34f, .12f), new Color(.36f, .5f, .2f), (float)rnd.NextDouble());
            HojaCompuesta(px, W, lim, o, (float)rnd.NextDouble() * Mathf.PI * 2, 35 + (float)rnd.NextDouble() * 30, col, rnd);
        }
        var tex = new Texture2D(W, Al, TextureFormat.RGBA32, true); tex.SetPixels32(px); tex.Apply(); return tex;
    }

    static void Tallo(Color32[] px, int ancho, RectInt lim, Vector2 p, float ang, float largo, float grosor, System.Random rnd, int nivel)
    {
        var col = Color.Lerp(new Color(.36f, .28f, .2f), new Color(.52f, .44f, .34f), (float)rnd.NextDouble());
        float recorrido = 0, fase = (float)rnd.NextDouble() * 6;
        while (recorrido < largo)
        {
            const float paso = 6;
            ang += Mathf.Sin(recorrido * .02f + fase) * .06f + (float)(rnd.NextDouble() - .5) * .12f;
            ang = Mathf.Lerp(ang, Mathf.PI / 2, .04f);
            var q = p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * paso;
            float g = Mathf.Max(1.5f, grosor * (1 - recorrido / largo * .6f));
            Linea(px, ancho, lim, p, q, g, col);
            if (nivel > 0 && rnd.NextDouble() < .025)
                Tallo(px, ancho, lim, q, ang + (rnd.NextDouble() < .5 ? -1 : 1) * (.5f + (float)rnd.NextDouble() * .5f), largo * .35f, g * .6f, rnd, nivel - 1);
            p = q; recorrido += paso;
            if (p.y >= lim.yMax || p.x < lim.xMin || p.x >= lim.xMax) break;
        }
    }

    static void HojaCompuesta(Color32[] px, int ancho, RectInt lim, Vector2 o, float ang, float largo, Color col, System.Random rnd)
    {
        var d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
        Linea(px, ancho, lim, o, o + d * largo, 2f, col * .7f);
        int pares = 4 + rnd.Next(4);
        float fa = 10 + (float)rnd.NextDouble() * 5, fb = 4.5f + (float)rnd.NextDouble() * 1.5f;
        for (int j = 0; j < pares; j++)
        {
            var p = o + d * largo * ((j + .7f) / (pares + .5f));
            for (int s = -1; s <= 1; s += 2)
            {
                float a2 = ang + s * (.9f + (float)(rnd.NextDouble() - .5) * .3f);
                var tono = col * (.85f + (float)rnd.NextDouble() * .3f);
                Elipse(px, ancho, lim, p + new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)) * fa * .9f, fa, fb, a2, tono);
            }
        }
        Elipse(px, ancho, lim, o + d * (largo + fa * .8f), fa, fb, ang, col);
    }

    static void Elipse(Color32[] px, int ancho, RectInt lim, Vector2 c, float a, float b, float ang, Color col)
    {
        float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang); int R = Mathf.CeilToInt(a) + 1;
        for (int y = Mathf.Max(lim.yMin, (int)c.y - R); y < Mathf.Min(lim.yMax, (int)c.y + R + 1); y++)
        for (int x = Mathf.Max(lim.xMin, (int)c.x - R); x < Mathf.Min(lim.xMax, (int)c.x + R + 1); x++)
        {
            float dx = x - c.x, dy = y - c.y, u = dx * ca + dy * sa, v = -dx * sa + dy * ca;
            float e = u * u / (a * a) + v * v / (b * b); if (e > 1) continue;
            float luz = .78f + .3f * (1 - e) + .12f * (v / b);
            if (Mathf.Abs(v) < .7f && Mathf.Abs(u) < a * .85f) luz *= .82f;
            px[y * ancho + x] = new Color(col.r * luz, col.g * luz, col.b * luz, 1);
        }
    }

    static void Linea(Color32[] px, int ancho, RectInt lim, Vector2 a, Vector2 b, float grosor, Color col)
    {
        int pasos = Mathf.CeilToInt(Vector2.Distance(a, b) * 2) + 1; float r = grosor / 2;
        for (int i = 0; i <= pasos; i++)
        {
            var c = Vector2.Lerp(a, b, (float)i / pasos);
            for (int y = Mathf.Max(lim.yMin, Mathf.FloorToInt(c.y - r)); y <= Mathf.Min(lim.yMax - 1, Mathf.CeilToInt(c.y + r)); y++)
            for (int x = Mathf.Max(lim.xMin, Mathf.FloorToInt(c.x - r)); x <= Mathf.Min(lim.xMax - 1, Mathf.CeilToInt(c.x + r)); x++)
            {
                float dx = x - c.x, dy = y - c.y; if (dx * dx + dy * dy > r * r + .25f) continue;
                float luz = 1 - .35f * Mathf.Abs(dx) / Mathf.Max(r, .5f);
                px[y * ancho + x] = new Color(col.r * luz, col.g * luz, col.b * luz, 1);
            }
        }
    }

    static void SacarViejo(Transform zona)
    {
        foreach (var nombre in ObjetosViejos)
        {
            var t = zona.Find(nombre); if (t == null) continue;
            var mesh = t.GetComponent<MeshFilter>() ? t.GetComponent<MeshFilter>().sharedMesh : null;
            UnityEngine.Object.DestroyImmediate(t.gameObject);
            if (mesh != null && AssetDatabase.IsSubAsset(mesh)) { AssetDatabase.RemoveObjectFromAsset(mesh); UnityEngine.Object.DestroyImmediate(mesh, true); }
        }
        foreach (var nombre in MallasConViejo) QuitarTriangulos(zona, nombre, Viejo);
        // Del frente: el zigzag liso (lo reemplaza el camino de tablones) y el tramo recto que pasaba el camino de los
        // módulos y terminaba en el pasto.
        QuitarPieza(zona, "Vereda", new Vector2(375.4f, 208.5f));
        QuitarPieza(zona, "Vereda", new Vector2(375.5f, 200.4f));
        // La vereda vieja del costado quedaría debajo de la escalera y del anexo.
        QuitarTriangulos(zona, "Vereda", new Bounds(new Vector3(389.2f, .05f, 207.15f), new Vector3(7.4f, 1f, 3.8f)));
    }

    // Saca de la malla de «nombre» la pieza entera (los triángulos unidos) que tiene un vértice en «punto» (espacio del mapa).
    static void QuitarPieza(Transform zona, string nombre, Vector2 punto)
    {
        var t = zona.Find(nombre); if (t == null) return;
        var mesh = t.GetComponent<MeshFilter>().sharedMesh; var v = mesh.vertices; var tri = mesh.triangles;
        var M = zona.worldToLocalMatrix * t.localToWorldMatrix;
        int n = tri.Length / 3; var padre = new int[n]; for (int i = 0; i < n; i++) padre[i] = i;
        int Raiz(int i) { while (padre[i] != i) { padre[i] = padre[padre[i]]; i = padre[i]; } return i; }
        var porPos = new Dictionary<Vector3Int, int>(); int semilla = -1;
        for (int i = 0; i < n; i++)
            for (int j = 0; j < 3; j++)
            {
                var q = M.MultiplyPoint3x4(v[tri[i * 3 + j]]);
                if (semilla < 0 && new Vector2(q.x - punto.x, q.z - punto.y).magnitude < .15f) semilla = i;
                var key = new Vector3Int(Mathf.RoundToInt(q.x * 50), Mathf.RoundToInt(q.y * 50), Mathf.RoundToInt(q.z * 50));
                if (porPos.TryGetValue(key, out var o)) padre[Raiz(i)] = Raiz(o); else porPos[key] = i;
            }
        if (semilla < 0) return;
        int r = Raiz(semilla); var quedan = new List<int>(); int sacados = 0;
        for (int i = 0; i < n; i++)
        {
            if (Raiz(i) == r) { sacados++; continue; }
            quedan.Add(tri[i * 3]); quedan.Add(tri[i * 3 + 1]); quedan.Add(tri[i * 3 + 2]);
        }
        mesh.SetTriangles(quedan, 0); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
        var mc = t.GetComponent<MeshCollider>(); if (mc != null) { mc.sharedMesh = null; mc.sharedMesh = mesh; }
        Debug.Log("ADIUNGS: " + sacados + " triángulos fuera de " + nombre + " (pieza en " + punto + ")");
    }

    static void QuitarTriangulos(Transform zona, string nombre, Bounds caja) => QuitarTriangulos(zona, zona.Find(nombre), caja);

    // Saca de la malla de «t» los triángulos cuyo centro cae en «caja» (medida en el espacio del mapa).
    static void QuitarTriangulos(Transform zona, Transform t, Bounds caja)
    {
        if (t == null) return; string nombre = t.name;
        var mf = t.GetComponent<MeshFilter>(); var mesh = mf.sharedMesh; var v = mesh.vertices; int sacados = 0;
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
        Debug.Log("ADIUNGS: " + sacados + " triángulos viejos fuera de " + nombre);
    }

    [MenuItem("Riftwalker/Universidad/Armar ADIUNGS")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/universidadMAPA.unity" || EditorApplication.isPlaying) throw new InvalidOperationException("Abrir universidadMAPA fuera de Play.");
        GameObject mapa = null;
        foreach (var g in scene.GetRootGameObjects()) if (g.name == "Mapa (escala 1,25)") mapa = g;
        var zona = mapa != null ? mapa.transform.Find("Ambiente_ZonaTUM") : null;
        if (zona == null) throw new InvalidOperationException("No se encontró Mapa (escala 1,25)/Ambiente_ZonaTUM.");
        MaterialesEnredadera();
        lotes.Clear();
        var anterior = zona.Find("ADIUNGS"); if (anterior != null) UnityEngine.Object.DestroyImmediate(anterior.gameObject);
        if (AssetDatabase.LoadMainAssetAtPath(MeshPath) != null) AssetDatabase.DeleteAsset(MeshPath);
        SacarViejo(zona);
        // La punta del camino de los módulos tenía un canto suelto donde ahora arranca el tramo al sendero del 7a.
        QuitarTriangulos(zona, mapa.transform.Find("Ambiente_CaminosNuevos/ArcoDeLosModulos"), new Bounds(new Vector3(372.5f, 0, 209.9f), new Vector3(1.2f, 1f, 1.3f)));
        // El caminito del patio seguía hasta la pared del Módulo 7; ahora arranca donde llega el camino del pasillo.
        QuitarTriangulos(zona, mapa.transform.Find("Modulo7/FrenteBlancoPatio_7b/HormigonViejo"),
            new Bounds(new Vector3(399.29f, 0, 220.4f), new Vector3(1.8f, 1.2f, 2.2f)));
        try
        {
            corrimiento = new Vector3(0, 0, DZ);
            Muros(); FachadaTrasera(); Frente(); CostadoAires(); CostadoSur(); Anexo();
        }
        finally { corrimiento = Vector3.zero; }
        CaminoAlPatio();
        CaminoAl7a();
        AjustarPasto(zona);
        TerrenoUnion7a(zona);
        var contenedor = new Mesh { name = "ADIUNGS" }; AssetDatabase.CreateAsset(contenedor, MeshPath);
        var go = new GameObject("ADIUNGS"); go.transform.SetParent(zona, false);
        foreach (var kv in lotes)
        {
            var l = kv.Value; if (l.v.Count == 0) continue;
            var mesh = new Mesh { name = "AD_" + kv.Key, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(l.v); mesh.SetUVs(0, l.uv); mesh.SetTriangles(l.t, 0); mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            AssetDatabase.AddObjectToAsset(mesh, contenedor);
            var hijo = new GameObject(kv.Key); hijo.transform.SetParent(go.transform, false);
            hijo.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = hijo.AddComponent<MeshRenderer>(); mr.sharedMaterial = l.mat;
            if (kv.Key == "VidrioLuz") mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (!SinColision.Contains(kv.Key)) hijo.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
        lotes.Clear();
        GrafitiTUM(zona, contenedor);
        EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
        Debug.Log("ADIUNGS armado.");
    }

    // Easter egg del TUM (US 343, CA3): el grafiti «Plaga Gris» en la pared de paneles blancos que mira a -z, en la punta
    // derecha, a la derecha del segundo aire: es el pedazo más grande que queda a la vista de piso a techo sin que lo tape
    // la puerta, el portón ni los aires. La imagen ya viene sin el fondo claro (transparente desde los bordes, con la
    // niebla del aerosol semitransparente), así que sobre la pared no se ve ningún cuadrado.
    const string GrafitiTex = "Assets/Materials/Uni/Uni_TUM_Grafiti.png", GrafitiMat = "Assets/Materials/Uni/Uni_TUM_Grafiti.mat";
    // Arranca a 1,3 m, por encima de los aires (1,35 m y 2 m adelante), para que las letras se vean también de costado.
    const float GrafitiZ = 239.95f - .01f, GrafitiX0 = 360.35f, GrafitiAncho = 2.4f, GrafitiY0 = 1.3f;

    static void GrafitiTUM(Transform zona, Mesh contenedor)
    {
        var anterior = zona.Find("TUM_GrafitiPlagaGris"); if (anterior != null) UnityEngine.Object.DestroyImmediate(anterior.gameObject);
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(GrafitiTex);
        if (tex == null) { Debug.LogWarning("Falta " + GrafitiTex); return; }
        var imp = (TextureImporter)AssetImporter.GetAtPath(GrafitiTex);
        // Sin llevarla a potencia de dos, para que conserve la proporción del dibujo.
        if (!imp.alphaIsTransparency || imp.wrapMode != TextureWrapMode.Clamp || imp.maxTextureSize < 2048 || imp.npotScale != TextureImporterNPOTScale.None)
        {
            imp.alphaIsTransparency = true; imp.mipmapEnabled = true; imp.wrapMode = TextureWrapMode.Clamp; imp.maxTextureSize = 2048;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.SaveAndReimport(); tex = AssetDatabase.LoadAssetAtPath<Texture2D>(GrafitiTex);
        }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(GrafitiMat);
        if (mat == null)
        {
            // Transparente con mezcla por alfa, sin escribir profundidad: los bordes suaves del aerosol se funden con la pared.
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Uni_TUM_Grafiti" };
            mat.SetTexture("_BaseMap", tex); mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", 0); mat.SetFloat("_AlphaClip", 0);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One); mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0); mat.SetFloat("_Smoothness", .1f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            AssetDatabase.CreateAsset(mat, GrafitiMat);
        }
        // Mirando la pared desde afuera (hacia +z), la derecha es +x: la u va de x0 a x1.
        float alto = GrafitiAncho * tex.height / tex.width, x1 = GrafitiX0 + GrafitiAncho, y1 = GrafitiY0 + alto;
        var mesh = new Mesh { name = "TUM_Grafiti" };
        mesh.SetVertices(new List<Vector3> { new Vector3(GrafitiX0, GrafitiY0, GrafitiZ), new Vector3(x1, GrafitiY0, GrafitiZ), new Vector3(x1, y1, GrafitiZ), new Vector3(GrafitiX0, y1, GrafitiZ) });
        mesh.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
        mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        if (Vector3.Dot(mesh.normals[0], Vector3.back) < 0) { mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0); mesh.RecalculateNormals(); mesh.RecalculateTangents(); }
        AssetDatabase.AddObjectToAsset(mesh, contenedor);
        var go = new GameObject("TUM_GrafitiPlagaGris"); go.transform.SetParent(zona, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
