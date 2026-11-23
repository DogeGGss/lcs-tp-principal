using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ArmadoUni;

// Estacionamiento de atrás del 3A y del 7, según las fotos:
// 1. La reja blanca de barrotes que corta el camino de ripio bien al fondo (del 3A a la reja de la calle), con el portón
//    cerrado y la colisión alta. Era la única entrada al Módulo 10, al terreno del informático y a todo el fondo (que
//    llega por la franja oeste hasta atrás de la biblioteca y del León Suárez): con esto queda todo cerrado.
// 2. El camino de ripio pegado al 3A (con la franja de pasto, el tronco tirado y la tapa) y el estacionamiento de ripio
//    entre los árboles; las islas de los árboles quedan con pasto. Los autos que ya estaban se quedan y se suma la fila
//    del este, debajo de los árboles nuevos.
// 3. El galpón de chapa verde con el aire, la puerta y las rejillas, el techo de chapa que sigue hasta el depósito de
//    corten con la persiana negra, el patiecito con media sombra y enredadera, los pallets de ladrillos, la tarima con
//    cajas, el bolsón, las estacas de madera, la columna con las cámaras y el volquete.
// 4. La planta de tratamiento cerrada con alambrado: dos piletones redondos, la pileta larga y la casilla de bombas.
// 5. La vereda de hormigón frente al 7 hasta la garita, con las motos y las bicis estacionadas bajo la fila de árboles.
// Medidas en metros, en el espacio de «Mapa (escala 1,25)». Se puede volver a correr: rehace Ambiente_EstacionamientoNorte.
public static class EstacionamientoUniversidad
{
    const string MeshPath = "Assets/Meshes/Uni/EstacionamientoNorte.asset", Grupo = "Ambiente_EstacionamientoNorte";
    const string Mobiliario = "Assets/Prefabs/Uni/Mobiliario", Arboles = "Assets/Prefabs/Uni/Arboles", Autos = "Assets/Prefabs/Uni/Autos";
    // Del estacionamiento viejo se apagan la caja de la planta y la vereda (se rehacen); autos, árboles y luces quedan.
    static readonly string[] Viejos = { "PlantaTratamiento", "CaminoAutos" };
    static readonly HashSet<string> SinColision = new HashSet<string> { "MediaSombra", "AguaPlanta" };
    // XC: la reja del fondo. Z3A: cara norte del 3A. ZRip..ZRn: el camino de ripio. ZPer: reja de la calle.
    const float XC = 265.0f, Z3A = 296.4f, ZRip = 298.3f, ZRn = 305.6f, ZPer = 355.5f;
    static Vector2 V(float x, float z) => new Vector2(x, z);

    // ---------------------------------------------------------------- Reja del fondo

    // Cerco de obra de chapa (del lado de adentro está el terreno del futuro Módulo Informático): ciego, para que no se vea
    // el fondo, con el portón corredizo de chapa sobre el ripio, la lona de la obra y los postes y travesaños de madera del
    // lado de adentro. Detrás, una cortina de eucaliptos y pinos tapa lo que asoma por arriba.
    static void Cierre(Transform grupo)
    {
        const float h = 2.6f, zp0 = ZRip - .3f, zp1 = ZRn + .3f;
        Box("ChapaObra", XC - .03f, -.05f, Z3A, XC + .03f, h, zp0);
        Box("ChapaObra", XC - .03f, -.05f, zp1, XC + .03f, h, ZPer);
        // Portón corredizo: hoja de chapa con marco de caño y diagonal, apenas adelante del cerco, y el candado.
        const float xg = XC + .1f, zg0 = zp0 - .15f, zg1 = zp1 + .15f;
        Box("ChapaObra", xg - .02f, .06f, zg0, xg + .02f, h - .06f, zg1);
        foreach (var (y0, y1) in new[] { (.02f, .1f), (h - .1f, h - .02f) }) Box("Galvanizado", xg - .04f, y0, zg0, xg + .04f, y1, zg1);
        foreach (var z in new[] { zg0, (zg0 + zg1) / 2, zg1 }) Box("Galvanizado", xg - .04f, .02f, z - .04f, xg + .04f, h - .02f, z + .04f);
        Barra("Galvanizado", new Vector3(xg + .03f, .15f, zg0 + .1f), new Vector3(xg + .03f, h - .15f, (zg0 + zg1) / 2 - .1f), .05f);
        Barra("Galvanizado", new Vector3(xg + .03f, .15f, zg1 - .1f), new Vector3(xg + .03f, h - .15f, (zg0 + zg1) / 2 + .1f), .05f);
        Box("Galvanizado", xg - .02f, -.02f, zg0 - .3f, xg + .02f, .04f, zg1 + .3f);
        Box("MetalNegro", xg + .04f, 1.05f, zg1 - .25f, xg + .1f, 1.2f, zg1 - .15f);
        // Postes y travesaños de madera del lado de adentro.
        for (float z = Z3A + .1f; z < ZPer; z += 2.5f) Box("MaderaOscura", XC - .14f, -.2f, z - .05f, XC - .03f, h + .05f, z + .05f);
        foreach (var y in new[] { .4f, h - .5f })
        {
            Box("MaderaOscura", XC - .11f, y, Z3A, XC - .03f, y + .1f, zp0);
            Box("MaderaOscura", XC - .11f, y, zp1, XC - .03f, y + .1f, ZPer);
        }
        // Lona de la obra colgada del portón, que es lo que se ve al fondo del ripio.
        const float lz0 = 299.0f, lz1 = 304.9f, lx = xg + .07f;
        Box("LonaObra", lx - .01f, .75f, lz0, lx + .01f, 2.2f, lz1);
        Box("CartelVerde", lx + .01f, .75f, lz0, lx + .02f, 1.02f, lz1);
        Texto(grupo, "Lona de la obra", "<b>OBRA EN CONSTRUCCIÓN</b>\n<size=60%>Futuro Módulo Informático · UNGS</size>", new Vector3(lx + .03f, 1.6f, (lz0 + lz1) / 2), Vector3.right, 2.1f, new Vector2(5.6f, 1.1f), new Color(.1f, .32f, .2f));
        Texto(grupo, "Lona de la obra (pie)", "PROHIBIDO EL PASO", new Vector3(lx + .03f, .885f, (lz0 + lz1) / 2), Vector3.right, 1.0f, new Vector2(5.6f, .26f), Color.white);
        Bloqueo(grupo, "Bloqueo_CierreFondo", new Vector3(XC, 3.0f, (Z3A + ZPer) / 2), new Vector3(.4f, 6.0f, ZPer - Z3A));
        // Cortina de árboles del otro lado.
        var cortina = new (float x, float z, string pf, float esc, float giro)[]
        {
            (261.0f, 301.0f, "Arbol_Eucalipto1", 1.0f, 10), (256.5f, 306.0f, "Arbol_Eucalipto0", 1.1f, 80), (261.0f, 311.5f, "Arbol_Eucalipto0", 1.0f, 150),
            (256.5f, 317.0f, "Arbol_Eucalipto1", .95f, 220), (261.0f, 322.5f, "Arbol_Pino1", 1.15f, 30), (256.5f, 328.0f, "Arbol_Eucalipto0", 1.05f, 300),
            (261.0f, 333.5f, "Arbol_Eucalipto1", 1.0f, 120), (256.5f, 339.0f, "Arbol_Eucalipto0", 1.0f, 200), (261.0f, 344.5f, "Arbol_Pino1", 1.1f, 260),
            (256.5f, 350.0f, "Arbol_Eucalipto1", .9f, 40), (251.5f, 312.0f, "Arbol_Pino1", 1.2f, 90), (251.5f, 336.0f, "Arbol_Eucalipto1", 1.05f, 170),
        };
        foreach (var a in cortina)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(Arboles + "/" + a.pf + ".prefab"); if (pf == null) continue;
            ArmadoUni.Colocar(grupo, pf, "Árbol detrás del cerco", new Vector3(a.x, 0, a.z), a.giro).transform.localScale = Vector3.one * a.esc;
        }
    }

    // ---------------------------------------------------------------- Vereda del 7

    // Losas de hormigón con sus juntas, del hueco entre el 3A y el 7 hasta la garita, y el cordón contra el ripio.
    static void Vereda()
    {
        // Arriba de la calzada de la garita (a 0,04) y del sendero del 7a (a 0,065), y empezando donde termina ese sendero,
        // para que no queden dos pisos en el mismo plano.
        const float x0 = 376.85f, x1 = 444.5f, z0 = 297.0f, z1 = 306.0f, y = .08f;
        Box("Vereda", x0, -.1f, z0, x1, y, z1);
        for (float x = x0 + 4f; x < x1 - .5f; x += 4f) Box("HormigonGris", x - .02f, y, z0, x + .02f, y + .006f, z1);
        Box("HormigonGris", x0, y, 301.48f, x1, y + .006f, 301.52f);
        Box("HormigonGris", x0, -.1f, z1, 438.8f, .14f, z1 + .25f);
    }

    // ---------------------------------------------------------------- Galpón verde y depósito

    const float GX0 = 354.5f, GX1 = 364.5f, GZ0 = 309.0f, GZ1 = 315.5f, GH = 2.45f;
    static float TechoGalpon(float z) => 2.95f - .4f * (z - (GZ0 - .6f)) / (GZ1 + .3f - (GZ0 - .6f));

    static void Galpon()
    {
        Box("ChapaVerdeGalpon", GX0, -.1f, GZ0, GX1, GH, GZ1);
        // Techo a un agua de chapa clara, con alero adelante, que sigue hasta la pared del depósito.
        float za = GZ0 - .6f, zb = GZ1 + .3f;
        Placa("ChapaClara", new Vector3(GX0 - .3f, TechoGalpon(za), za), new Vector3(366.0f - (GX0 - .3f), 0, 0), new Vector3(0, TechoGalpon(zb) - TechoGalpon(za), zb - za), .04f);
        Box("ChapaVerdeGalpon", GX0, GH, GZ0, GX1, TechoGalpon(GZ0) - .03f, GZ0 + .04f);
        foreach (var (x, n) in new[] { (GX0, Vector3.left), (GX1, Vector3.right) })
            Quad("ChapaVerdeGalpon", new Vector3(x, GH, GZ0), new Vector3(x, GH, GZ1), new Vector3(x, TechoGalpon(GZ1) - .03f, GZ1), new Vector3(x, TechoGalpon(GZ0) - .03f, GZ0), n);
        Barra("Galvanizado", new Vector3(GX1, TechoGalpon(za) - .06f, za + .05f), new Vector3(366.0f, TechoGalpon(za) - .06f, za + .05f), .06f);
        Cilindro("Galvanizado", new Vector3(365.85f, 0, za + .1f), new Vector3(365.85f, TechoGalpon(za + .1f) - .03f, za + .1f), .04f, 8);
        // Puerta, rejillas y el aire arriba a la izquierda.
        Box("ChapaVerde", 357.0f, 0, GZ0 - .04f, 358.1f, 2.05f, GZ0);
        Box("MetalNegro", 357.9f, .95f, GZ0 - .07f, 357.98f, 1.1f, GZ0 - .04f);
        for (int i = 0; i < 5; i++) Box("MetalNegro", 359.3f + i * .13f, 1.7f, GZ0 - .03f, 359.36f + i * .13f, 2.2f, GZ0);
        for (int i = 0; i < 2; i++) Box("MetalNegro", 360.3f + i * .13f, 1.7f, GZ0 - .03f, 360.36f + i * .13f, 2.2f, GZ0);
        Aire(new Vector3(355.6f, 1.75f, GZ0), Vector3.back, .9f);
        // Patiecito con media sombra entre el galpón y el depósito.
        const float px0 = 359.8f, px1 = 365.6f, pz0 = 306.6f;
        var esquinas = new[] { new Vector3(px0, 0, GZ0), new Vector3(px0, 0, pz0), new Vector3(px1, 0, pz0), new Vector3(px1, 0, GZ0) };
        for (int i = 0; i + 1 < esquinas.Length; i++)
        {
            var a = esquinas[i]; var b = esquinas[i + 1]; var d = b - a; var u = d.normalized; var n = Vector3.Cross(up, u);
            int m = Mathf.Max(1, Mathf.CeilToInt(d.magnitude / 1.9f));
            for (int j = 0; j <= m; j++) Cilindro("Galvanizado", a + d * j / m - up * .2f, a + d * j / m + up * 2.1f, .025f, 6);
            Barra("Galvanizado", a + up * 2.08f, b + up * 2.08f, .04f);
            Caja("MediaSombra", (a + b) / 2 + up * 1.1f + n * .03f, d / 2, up * .95f, n * .006f);
        }
    }

    static void Deposito()
    {
        const float x0 = 366.0f, x1 = 375.5f, z0 = 308.2f, z1 = 316.0f, h = 3.8f;
        Box("HormigonGris", x0, -.1f, z0, x1, h, z1);
        Box("PersianaNegra", x0 + .55f, 0, z0 - .06f, x1 - .55f, 2.9f, z0);
        // Marco de corten: los dos parantes, la banda de arriba partida por una buña oscura y su vuelta por el costado.
        Box("Corten", x0 - .05f, -.1f, z0 - .2f, x0 + .55f, 2.9f, z0);
        Box("Corten", x1 - .55f, -.1f, z0 - .2f, x1 + .05f, 2.9f, z0);
        Box("Corten", x0 - .05f, 2.9f, z0 - .2f, x1 + .05f, 3.3f, z0);
        Box("MetalNegro", x0 + .1f, 3.3f, z0 - .12f, x1 - .1f, 3.4f, z0);
        Box("Corten", x0 - .05f, 3.4f, z0 - .2f, x1 + .05f, h + .05f, z0);
        Box("Corten", x0 - .2f, 2.9f, z0, x0, h + .04f, z1);
        // Platea de hormigón delante de la persiana.
        Box("Vereda", x0 - .2f, -.1f, ZRn, x1 + .3f, .05f, z0 - .2f);
        // Volquete oxidado al costado.
        const float vx0 = 376.3f, vx1 = 378.5f, vz0 = 306.7f, vz1 = 309.6f, vh = 1.25f;
        Box("VolqueteRojo", vx0, .12f, vz0, vx1, .2f, vz1);
        Box("VolqueteRojo", vx0, .12f, vz0, vx0 + .06f, vh, vz1);
        Box("VolqueteRojo", vx1 - .06f, .12f, vz0, vx1, vh, vz1);
        Box("VolqueteRojo", vx0 + .06f, .12f, vz0, vx1 - .06f, vh, vz0 + .06f);
        Box("VolqueteRojo", vx0 + .06f, .12f, vz1 - .06f, vx1 - .06f, vh, vz1);
        Box("MetalNegro", vx0 + .25f, 0, vz0 + .2f, vx0 + .45f, .12f, vz1 - .2f);
        Box("MetalNegro", vx1 - .45f, 0, vz0 + .2f, vx1 - .25f, .12f, vz1 - .2f);
    }

    // Pallets de ladrillos envueltos, la tarima oscura con cajas, el bolsón blanco, las estacas y la columna de cámaras.
    static void Materiales()
    {
        foreach (var x in new[] { 352.3f, 353.5f })
        {
            const float z = 306.3f;
            Box("Madera", x, 0, z, x + 1.1f, .14f, z + 1.1f);
            Box("Ladrillo", x + .05f, .14f, z + .05f, x + 1.05f, .95f, z + 1.05f);
            foreach (var y in new[] { .4f, .75f }) Box("PinturaBlanca", x + .03f, y, z + .03f, x + 1.07f, y + .04f, z + 1.07f);
        }
        Box("MaderaOscura", 354.9f, 0, 306.2f, 357.0f, .32f, 307.4f);
        Box("Carton", 355.2f, .32f, 306.4f, 355.8f, .7f, 306.9f);
        Box("Carton", 355.9f, .32f, 306.35f, 356.7f, .62f, 307.0f);
        var d = new Vector3(.35f, .94f, 0); var p = new Vector3(-.94f, .35f, 0);
        Caja("Carton", new Vector3(356.35f, .95f, 307.15f), d * .6f, p * .04f, Vector3.forward * .25f);
        Box("PinturaBlanca", 358.2f, 0, 306.2f, 359.2f, .85f, 307.1f);
        Box("CianBiblioteca", 358.4f, .3f, 306.17f, 358.9f, .55f, 306.2f);
        for (float x = 352.0f; x < 359.0f; x += 1.7f) Box("MaderaOscura", x - .06f, -.2f, ZRn + .15f, x + .06f, .7f, ZRn + .27f);
        var c = new Vector3(359.5f, 0, 305.95f);
        Cilindro("Galvanizado", c - up * .2f, c + up * 6.6f, .07f, 10);
        Barra("Galvanizado", c + up * 6.4f, c + up * 6.4f + Vector3.back * .55f, .05f);
        foreach (var s in new[] { -1f, 1f })
        {
            var b = c + up * 6.28f + Vector3.back * .5f + Vector3.right * s * .13f;
            Caja("PinturaBlanca", b, Vector3.right * .06f, up * .06f, Vector3.back * .14f);
            Caja("MetalNegro", b + Vector3.back * .15f, Vector3.right * .045f, up * .045f, Vector3.back * .01f);
        }
    }

    // ---------------------------------------------------------------- Planta de tratamiento

    static void PlantaTratamiento()
    {
        const float x0 = 361.0f, x1 = 377.0f, z0 = 318.0f, z1 = 344.0f;
        var e = new[] { new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), new Vector3(x0, 0, z1), new Vector3(x0, 0, z0) };
        for (int i = 0; i + 1 < e.Length; i++) Alambrado(e[i], e[i + 1]);
        Caja("MediaSombra", new Vector3((x0 + x1) / 2, 1.1f, z0 - .04f), Vector3.right * (x1 - x0) / 2, up * .9f, Vector3.forward * .006f);
        foreach (var q in new[] { V(365.5f, 323.4f), V(372.5f, 323.4f) })
        {
            Cilindro("HormigonGris", new Vector3(q.x, -.1f, q.y), new Vector3(q.x, 1.6f, q.y), 2.2f, 20);
            Cilindro("AguaPlanta", new Vector3(q.x, 1.4f, q.y), new Vector3(q.x, 1.62f, q.y), 1.95f, 20);
        }
        Box("HormigonGris", 363.5f, -.1f, 329.5f, 374.5f, 1.2f, 340.0f);
        Box("AguaPlanta", 363.8f, 1.0f, 329.8f, 374.2f, 1.22f, 339.7f);
        Baranda(new Vector3(363.5f, 1.2f, 329.6f), new Vector3(374.5f, 1.2f, 329.6f));
        // Casilla de las bombas.
        Box("ChapaVerdeGalpon", 373.2f, -.1f, 340.8f, 376.2f, 2.4f, 343.4f);
        Placa("ChapaClara", new Vector3(373.0f, 2.55f, 340.5f), new Vector3(3.4f, 0, 0), new Vector3(0, -.2f, 3.2f), .04f);
        Box("ChapaVerde", 374.2f, 0, 340.76f, 375.1f, 2.0f, 340.8f);
    }

    // Franja de pasto contra el 3A: el tronco tirado y la tapa de inspección.
    static void Franja3A()
    {
        Cilindro("MaderaOscura", new Vector3(318.2f, .13f, 297.5f), new Vector3(321.6f, .15f, 297.8f), .13f, 10);
        Box("MetalNegro", 330.2f, -.05f, 297.0f, 330.9f, .03f, 297.7f);
    }

    // ---------------------------------------------------------------- Motos, árboles, autos y setos

    static GameObject Moto(string nombre, string color) => PrefabDeMueble(Mobiliario, nombre, r =>
    {
        foreach (var z in new[] { -.68f, .68f })
        {
            Cilindro("Neumatico", new Vector3(-.055f, .3f, z), new Vector3(.055f, .3f, z), .3f, 16);
            Cilindro("Galvanizado", new Vector3(-.06f, .3f, z), new Vector3(.06f, .3f, z), .16f, 12);
        }
        Box("MetalNegro", -.12f, .32f, -.25f, .12f, .55f, .3f);
        Box(color, -.15f, .62f, -.05f, .15f, .9f, .42f);
        Box("MetalNegro", -.14f, .78f, -.62f, .14f, .88f, -.02f);
        Box(color, -.12f, .62f, -.95f, .12f, .78f, -.55f);
        foreach (var s in new[] { -1f, 1f }) Barra("Galvanizado", new Vector3(s * .09f, .3f, .68f), new Vector3(s * .09f, 1.02f, .48f), .04f);
        Barra("MetalNegro", new Vector3(-.36f, 1.04f, .44f), new Vector3(.36f, 1.04f, .44f), .03f);
        Cilindro("PinturaBlanca", new Vector3(0, .93f, .55f), new Vector3(0, .93f, .63f), .08f, 12);
        Barra("MetalNegro", new Vector3(0, .5f, .3f), new Vector3(0, .62f, 0), .06f);
        Barra("MetalNegro", new Vector3(0, .62f, -.55f), new Vector3(0, .3f, -.68f), .05f);
        Cilindro("Galvanizado", new Vector3(.17f, .36f, -.15f), new Vector3(.19f, .48f, -.85f), .045f, 8);
    });

    static void Vegetal(Transform grupo, string prefab, string nombre, Vector3 p, Vector3 tam, float giro)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(Arboles + "/" + prefab + ".prefab"); if (pf == null) return;
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

    static void Sueltos(Transform grupo, GameObject[] motos)
    {
        // Motos y bicis contra el cordón de la vereda del 7, bajo la fila de árboles.
        var bici = AssetDatabase.LoadAssetAtPath<GameObject>(Mobiliario + "/Bicicleta.prefab");
        var orden = new[] { 0, 1, 0, 2, 1, 0 };
        for (int i = 0; i < orden.Length; i++)
        {
            var m = ArmadoUni.Colocar(grupo, motos[orden[i]], "Moto " + (i + 1), new Vector3(386.6f + i * 1.15f, 0, 307.6f), i % 2 == 0 ? 0f : 180f);
            m.transform.Rotate(0, 0, i % 2 == 0 ? 7f : -7f, Space.Self);
        }
        if (bici != null) for (int i = 0; i < 5; i++) ArmadoUni.Colocar(grupo, bici, "Bicicleta " + (i + 1), new Vector3(394.4f + i * .75f, 0, 307.4f), i % 2 == 0 ? 0f : 180f);
        // Árboles nuevos: la fila de la vereda, los dos grandes junto a la planta, el de la entrada a las filas y otros
        // sueltos para que no quede pelado.
        var arboles = new (float x, float z, string pf, float esc, float giro)[]
        {
            (386f, 310.3f, "Arbol_Copa1", 1.0f, 20), (395f, 310.3f, "Arbol_Copa2", .9f, 110), (404f, 310.3f, "Arbol_Copa0", 1.05f, 200),
            (413f, 310.3f, "Arbol_Copa1", .9f, 290), (422f, 310.3f, "Arbol_Copa2", 1.0f, 60), (431f, 310.3f, "Arbol_Copa0", .95f, 150),
            (357.8f, 321f, "Arbol_Copa2", 1.15f, 40), (357.8f, 338f, "Arbol_Copa1", 1.1f, 230), (344.6f, 311.2f, "Arbol_Copa2", 1.25f, 80),
            (283f, 352f, "Arbol_Copa1", 1.0f, 10), (369f, 349.5f, "Arbol_Copa0", 1.0f, 300), (275f, 340f, "Arbol_Copa2", 1.1f, 170),
            (275f, 318f, "Arbol_Copa1", 1.0f, 250), (322f, 353f, "Arbol_Copa0", .9f, 90), (346f, 353f, "Arbol_Copa1", .95f, 15),
        };
        foreach (var a in arboles)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(Arboles + "/" + a.pf + ".prefab"); if (pf == null) continue;
            ArmadoUni.Colocar(grupo, pf, "Árbol del estacionamiento", new Vector3(a.x, 0, a.z), a.giro).transform.localScale = Vector3.one * a.esc;
        }
        // Fila nueva de autos del lado este de las filas de ripio, desde la planta para el norte.
        var autos = new[] { "Auto_Sedan", "Auto_Hatch", "Auto_SUV", "Auto_Hatch", "Auto_Sedan", "Auto_SUV", "Auto_Sedan", "Auto_Hatch", "Auto_SUV", "Auto_Sedan" };
        var zs = new[] { 319.2f, 321.9f, 327.4f, 330.1f, 332.8f, 335.5f, 341.0f, 343.7f, 346.4f, 351.6f };
        for (int i = 0; i < zs.Length; i++)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(Autos + "/" + autos[i] + ".prefab"); if (pf == null) continue;
            ArmadoUni.Colocar(grupo, pf, autos[i], new Vector3(352.3f, 0, zs[i]), (i % 3 == 1 ? 270f : 90f) + (i % 2 == 0 ? 1.5f : -2f));
        }
        // Setos contra la reja de la calle, detrás de las filas de arriba.
        int n = 0;
        foreach (var (x0, x1) in new[] { (287f, 357f), (379f, 442f) })
            for (float x = x0 + 1.7f; x < x1; x += 3.4f)
                Vegetal(grupo, n % 2 == 0 ? "Arbusto1" : "Arbusto0", "Seto " + ++n, new Vector3(x, 0, 354.4f), new Vector3(3.7f, 1.5f, 1.2f), n % 3 == 0 ? 180f : 0f);
        // Enredadera sobre la media sombra del patiecito del galpón.
        Vegetal(grupo, "Arbusto0", "Enredadera del galpón", new Vector3(364.6f, 0, 306.9f), new Vector3(2.0f, 2.0f, .7f), 0);
    }

    // ---------------------------------------------------------------- Terreno

    static bool En(Vector2 q, float x0, float z0, float x1, float z1) => q.x >= x0 && q.x <= x1 && q.y >= z0 && q.y <= z1;

    // Ripio: el camino del 3A y las dos playas, salvo las islas de los árboles y el pasto delante del galpón.
    static bool EnRipio(Vector2 q)
    {
        if (En(q, XC, ZRip, 376f, ZRn)) return true;
        float xe = q.y < 318f ? 351.5f : 358.5f;
        if (En(q, 285.5f, ZRn, xe, 354.3f) && !(q.y > 306.8f && (En(q, 307.6f, 0, 312.4f, 999) || En(q, 331.6f, 0, 336.4f, 999)))) return true;
        if (En(q, 377.5f, 306.2f, 443.5f, 354.3f) && !En(q, 380f, 332.2f, 442f, 335.8f)) return true;
        return false;
    }

    static Vector2 Ondular(Vector2 q) => q + new Vector2(Mathf.PerlinNoise(q.x * .31f, q.y * .31f) - .5f, Mathf.PerlinNoise(q.x * .31f + 50f, q.y * .31f + 50f) - .5f) * 1.3f;

    static bool SinPasto(Vector2 q) => EnRipio(Ondular(q)) || En(q, 375.8f, 296.8f, 444.7f, 306.4f) || En(q, 365.6f, 305.4f, 376f, 308.3f)
        || En(q, GX0 - .3f, GZ0 - .2f, 366.1f, GZ1 + .2f) || En(q, 365.8f, 308.0f, 375.7f, 316.2f) || En(q, 376.2f, 306.6f, 378.6f, 309.7f)
        || (q - V(365.5f, 323.4f)).magnitude < 2.4f || (q - V(372.5f, 323.4f)).magnitude < 2.4f || En(q, 363.3f, 329.3f, 374.7f, 340.2f) || En(q, 373f, 340.6f, 376.4f, 343.6f);

    static void Terreno(Transform mapa)
    {
        var ter = Terrain.activeTerrain; if (ter == null) return;
        var td = ter.terrainData; var o = ter.transform.position;
        var w0 = mapa.TransformPoint(new Vector3(262f, 0, 294f)); var w1 = mapa.TransformPoint(new Vector3(447f, 0, 357f));
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
                    var q = V(l.x, l.z); if (q.y < 297.5f) continue;
                    int capa = EnRipio(Ondular(q)) ? 2 : 0;
                    for (int c = 0; c < capas; c++) al[j, i, c] = c == capa ? 1 : 0;
                }
            td.SetAlphamaps(x0, y0, al);
        }
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
                    if (SinPasto(V(l.x, l.z))) { datos[j, i] = 0; cambio = true; }
                }
            if (cambio) td.SetDetailLayer(i0, j0, capa, datos);
        }
        EditorUtility.SetDirty(td);
    }

    // ---------------------------------------------------------------- Armado

    [MenuItem("Riftwalker/Universidad/Armar estacionamiento")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/universidadMAPA.unity" || EditorApplication.isPlaying) throw new InvalidOperationException("Abrir universidadMAPA fuera de Play.");
        GameObject mapaGo = null;
        foreach (var g in scene.GetRootGameObjects()) if (g.name == "Mapa (escala 1,25)") mapaGo = g;
        if (mapaGo == null) throw new InvalidOperationException("No se encontró Mapa (escala 1,25).");
        MaterialDesde("HormigonGris", "Hormigon", new Color(.5f, .51f, .5f));
        MaterialDesde("ChapaVerdeGalpon", "ChapaClara", new Color(.22f, .46f, .3f), AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/Uni/Uni_ChapaLimpia.png"));
        MaterialDesde("PersianaNegra", "ChapaAuditorio", new Color(.16f, .17f, .19f));
        MaterialColor("Corten", new Color(.48f, .23f, .12f), .25f);
        MaterialColor("MediaSombra", new Color(.1f, .27f, .2f), .05f);
        MaterialColor("VolqueteRojo", new Color(.5f, .14f, .09f), .25f);
        MaterialColor("Carton", new Color(.6f, .46f, .3f), .05f);
        MaterialColor("AguaPlanta", new Color(.16f, .25f, .22f), .85f);
        MaterialColor("Neumatico", new Color(.05f, .05f, .06f), .15f);
        MaterialColor("MotoRoja", new Color(.6f, .07f, .07f), .6f);
        MaterialDesde("ChapaObra", "ChapaClara", new Color(.66f, .68f, .7f));
        MaterialColor("LonaObra", new Color(.93f, .93f, .9f), .1f);
        var mapa = mapaGo.transform;
        bool habiaCambios = scene.isDirty;
        lotes.Clear();
        var anterior = mapa.Find(Grupo); if (anterior != null) UnityEngine.Object.DestroyImmediate(anterior.gameObject);
        var viejo = mapa.Find("Ambiente_Estacionamiento");
        foreach (var n in Viejos) { var t = viejo != null ? viejo.Find(n) : null; if (t != null) t.gameObject.SetActive(false); }
        // Árboles sueltos del campus que quedaban en el medio del camino de ripio o encima de lo nuevo.
        var campus = mapa.Find("Ambiente_Vegetacion/Arboles_Campus");
        if (campus != null)
            foreach (Transform a in campus)
            {
                var q = V(a.localPosition.x, a.localPosition.z);
                if (En(q, XC - .5f, ZRip - .5f, 376.5f, ZRn + .5f) || En(q, 375.5f, 296.5f, 445f, 306.8f) || En(q, GX0 - .5f, GZ0 - 3.2f, 378.8f, 316.5f) || En(q, 360.5f, 317.5f, 377.5f, 344.5f))
                    a.gameObject.SetActive(false);
            }
        var go = new GameObject(Grupo); go.transform.SetParent(mapa, false);
        // Las motos van primero: sus prefabs usan los mismos lotes.
        var motos = new[] { Moto("Moto_Negra", "MetalNegro"), Moto("Moto_Roja", "MotoRoja"), Moto("Moto_Blanca", "PinturaBlanca") };
        lotes.Clear();
        Cierre(go.transform); Vereda(); Galpon(); Deposito(); Materiales(); PlantaTratamiento(); Franja3A();
        Volcar(go.transform, MeshPath, "EstacionamientoNorte", "EN_", SinColision);
        Sueltos(go.transform, motos);
        // La conexión de la calzada de la garita estaba en el mismo plano que la calzada de ingreso (parpadeaba): queda
        // 1,5 cm más arriba, debajo de la vereda nueva.
        var conexion = mapa.Find("AccesosSeparadosGarita/Conexion calzada con calle interior");
        var rc = conexion != null ? conexion.GetComponent<Renderer>() : null;
        if (rc != null) conexion.position += mapa.up * ((.055f - mapa.InverseTransformPoint(rc.bounds.max).y) * mapa.lossyScale.y);
        Physics.SyncTransforms();
        Terreno(mapa);
        EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
        if (!habiaCambios) EditorSceneManager.SaveScene(scene);
        Debug.Log(habiaCambios ? "Estacionamiento armado (la escena tenía cambios sin guardar: no se guardó)." : "Estacionamiento armado.");
    }
}
