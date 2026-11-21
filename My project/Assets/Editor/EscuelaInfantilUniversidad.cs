using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ArmadoUni;

// Escuela Infantil y Sala de Juegos «María Elena Walsh» (US 341), con lo que se ve en las primeras fotos (las demás
// caras vienen después):
// - El edificio gris de dos plantas: la galería de columnas de hormigón en el frente sur, las ventanas de marcos oscuros
//   arriba, la línea marrón del entrepiso y la banda de chapa del techo alrededor.
// - El extremo este: la terraza del primer piso bajo el techo, con el paño verde lima al fondo y las columnas; abajo,
//   el lugar cubierto con el mural de colores; el muro de hormigón con la escalera que sube a la terraza.
// - El patio: la reja verde (con algunos pasamanos naranjas) hasta el camino de la biblioteca, el cartel de la escuela y
//   las hamacas.
// Sin interiores: los vidrios son opacos. Medidas en metros, en el espacio de «Mapa (escala 1,25)». Se puede volver a
// correr: rehace el grupo Ambiente_EscuelaInfantil.
public static class EscuelaInfantilUniversidad
{
    const string MeshPath = "Assets/Meshes/Uni/EscuelaInfantil.asset", Grupo = "Ambiente_EscuelaInfantil";
    static readonly string[] CajasViejas = { "EscuelaInfantil", "EscuelaInfantil_Escalera", "SalaDeJuegos" };
    static readonly HashSet<string> SinColision = new HashSet<string> { "Galvanizado", "MetalNegro", "PlacaNaranja", "PlacaAzul", "PlacaVioleta", "TachoAmarillo", "BannerBiblioteca" };
    const float XW = 122.0f, XT = 166.0f, XE = 173.5f, ZS = 130.6f, ZG = 132.4f, ZN = 152.5f, H1 = 3.2f, HS = 6.0f;
    static Vector2 V(float x, float z) => new Vector2(x, z);
    static readonly Vector2[] Reja = { V(140.5f, 160.0f), V(172.0f, 160.0f), V(177.5f, 158.6f), V(180.5f, 155.2f), V(181.3f, 150.5f), V(180.4f, 145.5f), V(177.4f, 141.2f), V(173.6f, 138.6f) };
    // Cierres: del final de la reja del patio al pilar de la terraza (no se entra bajo la terraza) y de la esquina del
    // pretil de la escalera a la esquina del auditorio (no se pasa por la franja de la galería).
    static readonly Vector2[][] Cierres = { new[] { V(173.6f, 138.6f), V(173.45f, 131.1f) }, new[] { V(165.75f, 128.25f), V(157.75f, 123.25f) }, new[] { V(114.0f, 157.6f), V(114.0f, 160.0f), V(131.0f, 160.0f) } };

    static void Edificio()
    {
        // Planta baja retirada detrás de la galería; planta alta sobre todo el fondo; losa de la galería y línea marrón.
        Box("RevoqueGris", XW, -.2f, ZG, XT, H1 - .25f, ZN);
        Box("RevoqueEscuela", XW, H1 - .25f, ZS, XT, HS, ZN);
        Box("ChapaOxido", XW, H1 - .2f, ZS - .03f, XT, H1 - .07f, ZS);
        // Columnas del frente, que suben hasta la banda del techo.
        for (float x = XW + 1.8f; x < XT - .5f; x += 3.6f) Box("RevoqueEscuela", x - .2f, -.2f, ZS - .22f, x + .2f, HS - .1f, ZS + .2f);
        // Ventanas de arriba entre columnas (marcos oscuros) y las de abajo, en la pared de la galería.
        for (float x = XW + 3.6f; x < XT - 1.0f; x += 3.6f)
        {
            Ventana(new Vector3(x, 3.75f, ZS), Vector3.back, Vector3.right, 2.7f, 1.65f, 3, false, "MetalNegro");
            Ventana(new Vector3(x, .95f, ZG), Vector3.back, Vector3.right, 2.4f, 1.35f, 2, false, "MetalNegro");
        }
        // Caras sin foto (norte y oeste): ventanas simples en las dos plantas.
        // Cara norte (al patio de atrás): ventanas de marco blanco arriba sobre la banda gris oscura, las de abajo detrás de
        // la reja, y los dos caños color óxido; al oeste, ventanas simples. Arriba, el volumen chico con techo a dos aguas.
        Box("RevoqueGris", XW, 2.75f, ZN, XT, 3.45f, ZN + .03f);
        Ventanas(new Vector3(XT - 1, 0, ZN), new Vector3(XW + 1, 0, ZN), Vector3.forward, 3.7f, 2.3f, 1.45f, 2.9f, "PinturaBlanca", false);
        Ventanas(new Vector3(XT - 1, 0, ZN), new Vector3(XW + 1, 0, ZN), Vector3.forward, .9f, 2.6f, 1.25f, 3.9f, "PinturaBlanca", false);
        foreach (var x in new[] { 150.2f, 150.55f }) Cilindro("ChapaOxido", new Vector3(x, 0, ZN + .15f), new Vector3(x, HS, ZN + .15f), .09f, 8);
        foreach (var y in new[] { .95f, 3.75f }) Ventanas(new Vector3(XW, 0, ZS + 1), new Vector3(XW, 0, ZN - 1), Vector3.left, y, 1.6f, 1.4f, 3.6f, "MetalNegro", false);
        Box("VerdeSalvia", 126.0f, HS - .1f, 141.0f, 131.0f, 7.6f, 149.0f);
        foreach (var z in new[] { 141.0f, 149.0f }) Tri("VerdeSalvia", new Vector3(126.0f, 7.6f, z), new Vector3(131.0f, 7.6f, z), new Vector3(128.5f, 9.1f, z), new Vector3(0, 0, z < 145 ? -1 : 1));
        foreach (var s in new[] { -1f, 1f }) Placa("ChapaFascia", new Vector3(128.5f, 9.12f, 140.7f), new Vector3(0, 0, 8.6f), new Vector3(s * 2.8f, -1.68f, 0), .05f);
        // Extremo este: losa de la terraza, techo que sigue hasta el borde, columnas, el paño verde lima y la baranda.
        Box("RevoqueEscuela", XT, H1 - .3f, ZS, XE, H1, ZN);
        Box("RevoqueEscuela", XT, HS - .3f, ZS, XE, HS, ZN);
        foreach (var z in new[] { ZS + .25f, (ZS + ZN) / 2, ZN - .25f }) Box("RevoqueEscuela", XE - .45f, -.2f, z - .2f, XE - .05f, HS - .3f, z + .2f);
        Box("VerdeLima", XT, H1, ZS + .2f, XT + .04f, HS - .3f, ZN - .2f);
        Baranda(new Vector3(XE - .25f, H1, ZS + .5f), new Vector3(XE - .25f, H1, ZN - .5f));
        Baranda(new Vector3(XT + .3f, H1, ZN - .15f), new Vector3(XE - .3f, H1, ZN - .15f));
        // Banda de chapa alrededor del techo y la membrana.
        const float o = .35f, y0 = HS - .1f, y1 = HS + .9f;
        Box("ChapaFascia", XW - o, y0, ZS - o, XE + o, y1, ZS - o + .1f);
        Box("ChapaFascia", XW - o, y0, ZN + o - .1f, XE + o, y1, ZN + o);
        Box("ChapaFascia", XW - o, y0, ZS - o + .1f, XW - o + .1f, y1, ZN + o - .1f);
        Box("ChapaFascia", XE + o - .1f, y0, ZS - o + .1f, XE + o, y1, ZN + o - .1f);
        Box("RevoqueEscuela", XW - o + .1f, HS - .1f, ZS - o + .1f, XE + o - .1f, HS - .01f, ZN + o - .1f);
        Quad("ChapaFascia", new Vector3(XW - o + .1f, y1 - .05f, ZS - o + .1f), new Vector3(XE + o - .1f, y1 - .05f, ZS - o + .1f), new Vector3(XE + o - .1f, y1 - .05f, ZN + o - .1f), new Vector3(XW - o + .1f, y1 - .05f, ZN + o - .1f), up);
        // Mural de colores en la pared de abajo, bajo la terraza.
        var rnd = new System.Random(5); string[] colores = { "PlacaNaranja", "PlacaAzul", "PlacaVioleta", "TachoAmarillo", "VerdeLima", "CartelRojo", "BannerBiblioteca" };
        Box("CartelBlanco", XT, .3f, ZS + 2.0f, XT + .02f, 2.75f, ZN - 2.0f);
        for (int i = 0; i < 26; i++)
        {
            float z = ZS + 2.3f + (float)rnd.NextDouble() * (ZN - ZS - 5.2f), y = .45f + (float)rnd.NextDouble() * 1.7f, w = .5f + (float)rnd.NextDouble() * 1.4f, h = .3f + (float)rnd.NextDouble() * .7f;
            Box(colores[i % colores.Length], XT + .02f, y, z, XT + .03f + i * .0004f, Mathf.Min(2.7f, y + h), Mathf.Min(ZN - 2.1f, z + w));
        }
        // Muro de hormigón y la escalera que sube a la terraza (hacia el oeste), con baranda del lado del edificio.
        const int k = 19; float run = 9.5f / k, rise = H1 / k;
        for (int i = 0; i < k; i++) Box("RevoqueEscuela", 177.5f - (i + 1) * run, -.1f, 128.5f, 177.5f - i * run, (i + 1) * rise, 130.4f);
        Box("RevoqueEscuela", XT, H1 - .3f, 128.5f, 168.0f, H1, ZS);
        for (int i = 0; i < 20; i++)
        {
            float xa = 177.5f - i * 9.5f / 20, xb = 177.5f - (i + 1) * 9.5f / 20;
            float ya = Mathf.Min(H1, (177.5f - xa) / 9.5f * H1) + 1.0f, yb = Mathf.Min(H1, (177.5f - xb) / 9.5f * H1) + 1.0f;
            Quad("RevoqueEscuela", new Vector3(xa, -.2f, 128.25f), new Vector3(xb, -.2f, 128.25f), new Vector3(xb, yb, 128.25f), new Vector3(xa, ya, 128.25f), Vector3.back);
            Quad("RevoqueEscuela", new Vector3(xa, -.2f, 128.5f), new Vector3(xb, -.2f, 128.5f), new Vector3(xb, yb, 128.5f), new Vector3(xa, ya, 128.5f), Vector3.forward);
            Quad("RevoqueEscuela", new Vector3(xa, ya, 128.25f), new Vector3(xb, yb, 128.25f), new Vector3(xb, yb, 128.5f), new Vector3(xa, ya, 128.5f), up);
        }
        // Pretil del descanso (al sur y al oeste) y baranda del borde sur de la terraza: que no haya por dónde caerse.
        Box("RevoqueEscuela", XT, -.2f, 128.25f, 168.0f, H1 + 1.0f, 128.5f);
        Box("RevoqueEscuela", XT - .25f, -.2f, 128.25f, XT, H1 + 1.0f, ZS);
        Baranda(new Vector3(168.1f, H1, ZS + .1f), new Vector3(XE - .3f, H1, ZS + .1f));
        Baranda(new Vector3(177.4f, 0, 130.3f), new Vector3(168.0f, H1, 130.3f));
    }

    // Reja del patio: postes verdes, paños de malla, pasamanos (algunos naranjas) y el cartel de la escuela.
    static void Patio(Transform grupo)
    {
        int tramo = 0;
        foreach (var linea in new[] { Reja }.Concat(Cierres))
        for (int i = 0; i + 1 < linea.Length; i++)
        {
            Vector2 a = linea[i], b = linea[i + 1]; float L = (b - a).magnitude; int n = Mathf.Max(1, Mathf.RoundToInt(L / 2.4f));
            for (int j = 0; j < n; j++, tramo++)
            {
                var p0 = Vector2.Lerp(a, b, (float)j / n); var p1 = Vector2.Lerp(a, b, (float)(j + 1) / n);
                Vector3 q0 = new Vector3(p0.x, 0, p0.y), q1 = new Vector3(p1.x, 0, p1.y); var d = (q1 - q0).normalized; var nn = Vector3.Cross(up, d);
                Caja("PinturaVerde", q0 + up * 1.0f, d * .03f, up * 1.0f, nn * .03f);
                Caja("RejaVerde", (q0 + q1) / 2 + up * 1.0f, d * ((q1 - q0).magnitude / 2 - .04f), up * .9f, nn * .008f);
                Barra(tramo % 3 == 1 ? "PlacaNaranja" : "PinturaVerde", q0 + up * 1.95f, q1 + up * 1.95f, .04f);
                Barra("PinturaVerde", q0 + up * .08f, q1 + up * .08f, .035f);
            }
        }
        foreach (var linea in new[] { Reja }.Concat(Cierres)) { var e = new Vector3(linea[linea.Length - 1].x, 0, linea[linea.Length - 1].y); Caja("PinturaVerde", e + up * 1.0f, Vector3.right * .03f, up * 1.0f, Vector3.forward * .03f); }
        // Cartel colgado de la reja, mirando al camino de la biblioteca.
        Vector2 c0 = Reja[1], c1 = Reja[2]; var dc = new Vector3(c1.x - c0.x, 0, c1.y - c0.y).normalized; var nc = Vector3.Cross(dc, up);
        var cc = new Vector3((c0.x + c1.x) / 2, 1.25f, (c0.y + c1.y) / 2) + nc * .05f;
        Caja("CartelBlanco", cc, dc * 1.15f, up * .55f, nc * .01f);
        Caja("BannerBiblioteca", cc + nc * .012f - up * .42f, dc * 1.15f, up * .08f, nc * .003f);
        Texto(grupo, "Cartel Escuela Infantil", "<size=55%>Escuela Infantil y Sala de Juegos</size>\n<b>María Elena Walsh</b>\n<size=50%>UNGS</size>", cc + nc * .02f + up * .05f, nc, 1.6f, new Vector2(2.2f, .9f), new Color(.15f, .3f, .55f));
        // Hamacas: dos caballetes, el travesaño y dos asientos colgados.
        var h = new Vector3(176.0f, 0, 151.5f);
        foreach (var dz in new[] { -1.2f, 1.2f })
        {
            var top = h + new Vector3(0, 2.2f, dz);
            Barra("CartelRojo", h + new Vector3(-.75f, 0, dz), top, .06f); Barra("CartelRojo", h + new Vector3(.75f, 0, dz), top, .06f);
        }
        Barra("TachoAmarillo", h + new Vector3(0, 2.2f, -1.25f), h + new Vector3(0, 2.2f, 1.25f), .07f);
        foreach (var dz in new[] { -.5f, .5f })
        {
            foreach (var dx in new[] { -.2f, .2f }) Cilindro("Galvanizado", h + new Vector3(0, 2.18f, dz + dx * .9f), h + new Vector3(0, .48f, dz + dx), .008f, 4);
            Box("PinturaVerde", h.x - .12f, .42f, h.z + dz - .24f, h.x + .12f, .48f, h.z + dz + .24f);
        }
    }


    // ---------------------------------------------------------------- Sala de Juegos

    // La Sala de Juegos: la casita de revoque crema con techo de chapa a dos aguas, alero con cabios a la vista y tapas
    // blancas; la pared norte (hacia el camino y los juegos) pintada de azul, con los dos aires, la ventana con malla, el
    // caño de lluvia y el banco blanco; al este, el anexo de chapa verde oscura con techo en pendiente (la escalera).
    // El mural de la pared azul va después.
    static void SalaDeJuegos()
    {
        const float x0 = 131.0f, x1 = 138.3f, z0 = 157.0f, z1 = 166.0f, he = 3.0f, hr = 5.8f, vu = .6f;
        float zr = (z0 + z1) / 2, pend = (hr - he) / (zr - z0);
        Box("RevoqueCrema", x0, -.2f, z0, x1, he, z1);
        Tri("RevoqueCrema", new Vector3(x0, he, z0), new Vector3(x0, he, z1), new Vector3(x0, hr, zr), Vector3.left);
        Tri("RevoqueCrema", new Vector3(x1, he, z0), new Vector3(x1, he, z1), new Vector3(x1, hr, zr), Vector3.right);
        foreach (var s in new[] { -1f, 1f })
        {
            float largo = zr - z0 + vu, ze = zr + s * largo, ye = hr + .05f - largo * pend;
            Placa("ChapaTecho", new Vector3(x0 - .4f, hr + .05f, zr), new Vector3(x1 - x0 + .8f, 0, 0), new Vector3(0, ye - hr - .05f, ze - zr), .05f);
            Box("PinturaBlanca", x0 - .42f, ye - .2f, Mathf.Min(ze, ze - s * .05f), x1 + .42f, ye, Mathf.Max(ze, ze - s * .05f));
            float zw = s < 0 ? z0 : z1;
            for (float x = x0 + .2f; x < x1; x += .6f) Viga("PinturaBlanca", new Vector3(x, he - .05f, zw), new Vector3(x, ye - .1f, ze - s * .05f), .1f, .06f);
        }
        foreach (var x in new[] { x0 - .4f, x1 + .4f })
        {
            Viga("PinturaBlanca", new Vector3(x, hr + .02f, zr), new Vector3(x, hr + .05f - (zr - z0 + vu) * pend - .02f, z0 - vu), .22f, .05f);
            Viga("PinturaBlanca", new Vector3(x, hr + .02f, zr), new Vector3(x, hr + .05f - (zr - z0 + vu) * pend - .02f, z1 + vu), .22f, .05f);
        }
        Ventana(new Vector3(x1, 3.55f, zr), Vector3.right, Vector3.forward, .6f, .55f, 1, false);
        // Pared norte azul y lo que tiene apoyado.
        Box("AzulMural", x0 + .05f, .25f, z1, x1 - .05f, he - .05f, z1 + .02f);
        Aire(new Vector3(134.9f, 2.0f, z1 + .02f), Vector3.forward); Aire(new Vector3(137.1f, 2.0f, z1 + .02f), Vector3.forward);
        Ventana(new Vector3(132.9f, 1.25f, z1 + .02f), Vector3.forward, Vector3.right, .9f, 1.0f, 1, false);
        Caja("MallaBlanca", new Vector3(132.9f, 1.75f, z1 + .1f), Vector3.right * .42f, up * .48f, Vector3.forward * .004f);
        Cilindro("BannerBiblioteca", new Vector3(131.45f, 0, z1 + .1f), new Vector3(131.45f, he, z1 + .1f), .06f, 8);
        // Banco de listones blancos frente a la pared.
        var bb = new Vector3(134.4f, 0, z1 + .75f);
        for (int i = 0; i < 5; i++) Box("PinturaBlanca", bb.x - .85f, .42f, bb.z - .22f + i * .1f, bb.x + .85f, .46f, bb.z - .15f + i * .1f);
        for (int i = 0; i < 4; i++) Box("PinturaBlanca", bb.x - .85f, .55f + i * .12f, bb.z - .3f + i * .015f, bb.x + .85f, .63f + i * .12f, bb.z - .26f + i * .015f);
        foreach (var dx in new[] { -.7f, .7f }) { Box("MetalNegro", bb.x + dx - .03f, 0, bb.z - .25f, bb.x + dx + .03f, .44f, bb.z + .25f); Box("MetalNegro", bb.x + dx - .03f, .44f, bb.z - .32f, bb.x + dx + .03f, 1.0f, bb.z - .27f); }
        // Anexo de la escalera: chapa verde oscura con techo en pendiente y una ventana con malla. El techo arranca contra
        // el hastial por debajo del alero de la casa (que sobre el anexo baja hasta 3,2), así no lo atraviesa.
        const float ax = x1, ax1 = x1 + 2.0f, az0 = 158.0f, az1 = 165.4f, ya = 3.1f, yb = 2.3f, yw = 2.3f;
        Box("ChapaVerde", ax, -.2f, az0, ax1, yw, az1);
        Placa("ChapaVerde", new Vector3(ax, ya, az0 - .3f), new Vector3(0, 0, az1 - az0 + .6f), new Vector3(ax1 + .35f - ax, yb - ya, 0), .05f);
        float yTop = ya + (yb - ya) * (ax1 - ax) / (ax1 + .35f - ax);
        foreach (var (z, n) in new[] { (az0, Vector3.back), (az1, Vector3.forward) })
            Quad("ChapaVerde", new Vector3(ax, yw, z), new Vector3(ax1, yw, z), new Vector3(ax1, yTop, z), new Vector3(ax, ya, z), n);
        Ventana(new Vector3(ax1, 1.5f, 161.7f), Vector3.right, Vector3.forward, 1.0f, .7f, 1, false, "MetalNegro");
        Caja("MallaBlanca", new Vector3(ax1 + .07f, 1.85f, 161.7f), Vector3.forward * .46f, up * .33f, Vector3.right * .004f);
    }

    // ---------------------------------------------------------------- Juegos y patio de atrás

    static void Juegos()
    {
        // Domo de caños azules y verde lima.
        var dc = new Vector3(124.5f, 0, 175.6f); const float rd = 1.5f; var anillos = new[] { 0f, 30f, 58f };
        Vector3 PD(float lat, float lon) { float a = lat * Mathf.Deg2Rad, b = lon * Mathf.Deg2Rad; return dc + new Vector3(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(a), Mathf.Cos(a) * Mathf.Sin(b)) * rd; }
        for (int r = 0; r < anillos.Length; r++)
            for (int k = 0; k < 10; k++)
            {
                float l0 = k * 36 + (r % 2) * 18, l1 = l0 + 36;
                Barra(k % 2 == 0 ? "PlacaAzul" : "VerdeLima", PD(anillos[r], l0), PD(anillos[r], l1), .045f);
                if (r + 1 < anillos.Length) { Barra("VerdeLima", PD(anillos[r], l0), PD(anillos[r + 1], l0 + 18), .045f); Barra("PlacaAzul", PD(anillos[r], l0), PD(anillos[r + 1], l0 - 18), .045f); }
                else Barra(k % 2 == 0 ? "PlacaAzul" : "VerdeLima", PD(anillos[r], l0), dc + up * rd, .045f);
            }
        // Gusano de aros amarillos.
        for (int i = 0; i < 5; i++)
        {
            var c = new Vector3(116.0f + i * .6f, .75f, 175.5f); const int n = 16;
            for (int j = 0; j < n; j++)
            {
                float a0 = j * Mathf.PI * 2 / n, a1 = (j + 1) * Mathf.PI * 2 / n;
                Barra("TachoAmarillo", c + new Vector3(0, Mathf.Sin(a0), Mathf.Cos(a0)) * .55f, c + new Vector3(0, Mathf.Sin(a1), Mathf.Cos(a1)) * .55f, .05f);
            }
            foreach (var dz in new[] { -.3f, .3f }) Barra("TachoAmarillo", c + new Vector3(0, -.46f, dz), new Vector3(c.x, 0, c.z + dz * 1.4f), .05f);
        }
        // Torre roja y amarilla con el tobogán naranja.
        var t = new Vector3(104.5f, 0, 175.5f);
        foreach (var (dx, dz) in new[] { (-.7f, -.7f), (.7f, -.7f), (-.7f, .7f), (.7f, .7f) }) Cilindro("TachoAmarillo", t + new Vector3(dx, 0, dz), t + new Vector3(dx, 2.6f, dz), .06f, 8);
        Box("CartelRojo", t.x - .7f, 1.15f, t.z - .7f, t.x + .7f, 1.25f, t.z + .7f);
        foreach (var dz in new[] { -.7f, .7f }) Box("CartelRojo", t.x - .7f, 1.25f, t.z + dz - .02f, t.x + .7f, 1.95f, t.z + dz + .02f);
        var ap = t + up * 3.3f;
        foreach (var (a, b) in new[] { ((-.8f, -.8f), (.8f, -.8f)), ((.8f, -.8f), (.8f, .8f)), ((.8f, .8f), (-.8f, .8f)), ((-.8f, .8f), (-.8f, -.8f)) })
            Tri("CartelRojo", t + new Vector3(a.Item1, 2.6f, a.Item2), t + new Vector3(b.Item1, 2.6f, b.Item2), ap, new Vector3(a.Item1 + b.Item1, 1, a.Item2 + b.Item2));
        Placa("PlacaNaranja", t + new Vector3(.7f, 1.2f, -.3f), new Vector3(0, 0, .6f), new Vector3(2.6f, -1.0f, 0), .05f);
        foreach (var dz in new[] { -.32f, .32f }) Barra("PlacaNaranja", t + new Vector3(.7f, 1.4f, dz), t + new Vector3(3.3f, .4f, dz), .05f);
        // Pozo con su jaula verde (bomba adentro).
        var p0 = new Vector2(110.2f, 175.0f); var p1 = new Vector2(113.2f, 177.3f);
        foreach (var (a, b) in new[] { (V(p0.x, p0.y), V(p1.x, p0.y)), (V(p1.x, p0.y), V(p1.x, p1.y)), (V(p1.x, p1.y), V(p0.x, p1.y)), (V(p0.x, p1.y), V(p0.x, p0.y)) })
        {
            Vector3 qa = new Vector3(a.x, 0, a.y), qb = new Vector3(b.x, 0, b.y); var d = (qb - qa).normalized; var nn = Vector3.Cross(up, d);
            Caja("RejaVerde", (qa + qb) / 2 + up * .7f, d * (qb - qa).magnitude / 2, up * .6f, nn * .008f);
            Caja("PinturaVerde", qa + up * .7f, d * .05f, up * .72f, nn * .05f);
            Barra("PinturaVerde", qa + up * 1.35f, qb + up * 1.35f, .05f); Barra("PinturaVerde", qa + up * .1f, qb + up * .1f, .05f);
        }
        Box("HormigonViejo", 110.8f, 0, 175.5f, 112.6f, .12f, 176.8f);
        Box("Galvanizado", 111.4f, .12f, 175.9f, 111.6f, 1.1f, 176.1f);
        Box("Galvanizado", 112.0f, .12f, 175.8f, 112.45f, 1.5f, 176.3f);
        // Estructura de madera con rampa de tablones y pasamanos amarillo, junto a la Sala de Juegos.
        var w = new Vector3(143.5f, 0, 163.5f);
        foreach (var (dx, dz) in new[] { (-.9f, -.9f), (.9f, -.9f), (-.9f, .9f), (.9f, .9f) }) Box("MaderaOscura", w.x + dx - .07f, 0, w.z + dz - .07f, w.x + dx + .07f, 2.1f, w.z + dz + .07f);
        Box("MaderaOscura", w.x - .95f, .95f, w.z - .95f, w.x + .95f, 1.05f, w.z + .95f);
        Placa("MaderaOscura", new Vector3(w.x + .95f, 1.0f, w.z - .45f), new Vector3(0, 0, .9f), new Vector3(2.4f, -1.0f, 0), .06f);
        Barra("TachoAmarillo", new Vector3(w.x - .9f, 1.85f, w.z - .9f), new Vector3(w.x + .9f, 1.85f, w.z - .9f), .04f);
        Barra("TachoAmarillo", new Vector3(w.x - .9f, 1.85f, w.z + .9f), new Vector3(w.x + .9f, 1.85f, w.z + .9f), .04f);
        Placa("MaderaOscura", new Vector3(w.x - .95f, 1.0f, w.z - .4f), new Vector3(0, 0, .8f), new Vector3(-1.1f, -1.0f, 0), .04f);
        // Hamacas de madera frente al León Suárez.
        var h = new Vector3(113.5f, 0, 152.5f);
        foreach (var dz in new[] { -1.5f, 1.5f })
        {
            var top = h + new Vector3(0, 2.3f, dz);
            Barra("MaderaOscura", h + new Vector3(-.95f, 0, dz), top, .1f); Barra("MaderaOscura", h + new Vector3(.95f, 0, dz), top, .1f);
        }
        Barra("MaderaOscura", h + new Vector3(0, 2.3f, -1.55f), h + new Vector3(0, 2.3f, 1.55f), .12f);
        foreach (var dz in new[] { -.8f, 0f, .8f })
        {
            foreach (var dx in new[] { -.2f, .2f }) Cilindro("Galvanizado", h + new Vector3(0, 2.25f, dz + dx), h + new Vector3(0, .5f, dz + dx), .008f, 4);
            Box("MetalNegro", h.x - .13f, .45f, h.z + dz - .24f, h.x + .13f, .52f, h.z + dz + .24f);
        }
        // Contenedor azul contra el edificio, los tres baños químicos delante y las chapas con el portón de madera.
        Box("ContenedorAzul", 105.2f, 0, 151.0f, 107.7f, 2.6f, 157.1f);
        string[] colores = { "BanoBeige", "CartelBlanco", "CartelBlanco" };
        for (int i = 0; i < 3; i++)
        {
            float z0 = 151.6f + i * 1.25f;
            Box(colores[i], 108.0f, 0, z0, 109.15f, 2.15f, z0 + 1.15f);
            Box(colores[i], 108.05f, 2.15f, z0 + .05f, 109.1f, 2.3f, z0 + 1.1f);
            Box("MetalNegro", 109.15f, .1f, z0 + .12f, 109.17f, 1.95f, z0 + .16f);
            Box("CartelBlanco", 109.15f, 1.55f, z0 + .3f, 109.18f, 1.85f, z0 + .85f);
        }
        for (float x = 107.7f; x < 113.9f; x += 1.0f) Box("ChapaClara", x, 0, 157.55f, Mathf.Min(x + 1.0f, 114.0f), 2.1f, 157.6f);
        foreach (var x in new[] { 107.7f, 110.9f, 114.0f }) Box("MaderaOscura", x - .05f, 0, 157.48f, x + .05f, 2.2f, 157.68f);
    }

    [MenuItem("Riftwalker/Universidad/Armar Escuela Infantil")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/universidadMAPA.unity" || EditorApplication.isPlaying) throw new InvalidOperationException("Abrir universidadMAPA fuera de Play.");
        GameObject mapaGo = null;
        foreach (var g in scene.GetRootGameObjects()) if (g.name == "Mapa (escala 1,25)") mapaGo = g;
        if (mapaGo == null) throw new InvalidOperationException("No se encontró Mapa (escala 1,25).");
        MaterialColor("VerdeLima", new Color(.66f, .73f, .3f), .15f);
        MaterialColor("RevoqueCrema", new Color(.88f, .84f, .72f), .1f);
        MaterialColor("AzulMural", new Color(.17f, .3f, .55f), .15f);
        MaterialColor("VerdeSalvia", new Color(.62f, .68f, .55f), .1f);
        MaterialColor("MaderaOscura", new Color(.33f, .22f, .15f), .1f);
        MaterialColor("BanoBeige", new Color(.74f, .66f, .5f), .2f);
        MaterialDesde("ChapaVerde", "ChapaClara", new Color(.2f, .27f, .24f), AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/Uni/Uni_ChapaLimpia.png"));
        MaterialDesde("ContenedorAzul", "ChapaClara", new Color(.42f, .55f, .68f), AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/Uni/Uni_ChapaLimpia.png"));
        MaterialDesde("RevoqueEscuela", "Hormigon", new Color(.74f, .74f, .72f));
        MaterialDesde("ChapaFascia", "ChapaClara", new Color(.3f, .31f, .33f), AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/Uni/Uni_ChapaLimpia.png"));
        var mapa = mapaGo.transform;
        bool habiaCambios = scene.isDirty;
        lotes.Clear();
        var anterior = mapa.Find(Grupo); if (anterior != null) UnityEngine.Object.DestroyImmediate(anterior.gameObject);
        var sinDetalle = mapa.Find("Ambiente_EdificiosSinDetalle");
        foreach (var n in CajasViejas) { var t = sinDetalle != null ? sinDetalle.Find(n) : null; if (t != null) t.gameObject.SetActive(false); }
        var go = new GameObject(Grupo); go.transform.SetParent(mapa, false);
        Edificio(); Patio(go.transform); SalaDeJuegos(); Juegos();
        Volcar(go.transform, MeshPath, "EscuelaInfantil", "EI_", SinColision);
        EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
        if (!habiaCambios) EditorSceneManager.SaveScene(scene);
        Debug.Log(habiaCambios ? "Escuela Infantil armada (la escena tenía cambios sin guardar: no se guardó)." : "Escuela Infantil armada.");
    }
}
