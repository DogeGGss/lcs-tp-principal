using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ArmadoUni;

// Acceso del Módulo 7 desde el estacionamiento (punta noreste del frente norte), según las fotos:
// 1. El cuerpo de chapa plateada en bandas con sus juntas, sobre la base color oliva: dos filas de ventanas metidas contra
//    el bloque de hormigón, el marco vertical de hormigón con el antepecho manchado junto al ladrillo, las dos ventanas
//    de la base, la puerta doble blanca de chapa con sus bisagras, el cartel «ENTRADA Módulo 7 Sector A», la cámara, la
//    sirena y el caño de chimenea galvanizado que sube por la esquina.
// 2. El bloque de hormigón visto con moho (textura propia) sobre la entrada en voladizo: tres ventanas por piso, el
//    reflector y los faroles de arriba, la columna de la esquina, los tubos de luz del cielorraso, los detectores de
//    metales y la cara a la calle con sus dos filas de ventanas y la base con ventanitas.
// 3. La plataforma de hormigón delante de la base, los escalones a la entrada y a la vereda, la rampa del lado de la
//    calle y la rampita hacia el 7a, con las barandas galvanizadas de postes con planchuela.
// No toca los assets del módulo: las mallas viejas que caen en la zona se copian recortadas a un asset propio.
// Medidas en metros, en el espacio de «Mapa (escala 1,25)». Se puede volver a correr: rehace Ambiente_Modulo7Acceso.
public static class Modulo7AccesoUniversidad
{
    const string MeshPath = "Assets/Meshes/Uni/Modulo7Acceso.asset", RecortesPath = "Assets/Meshes/Uni/Modulo7Acceso_Recortes.asset", Grupo = "Ambiente_Modulo7Acceso";
    // XO: junta con el ladrillo; XC: chapa | hormigón; XE: cara a la calle. Z0..ZF: espesor del frente. HB: base; HT: arriba.
    const float XO = 420.6f, XC = 434.6f, XE = 442.9f, Z0 = 295.85f, ZF = 297.75f, HB = 3.0f, HT = 9.4f, ZE0 = 276.6f;
    // Pisos: plataforma, entrada (bajo el voladizo). La plataforma va 2 cm arriba de la vieja (a 0,45) que sigue adentro.
    const float YP = .47f, YE = .9f;
    static readonly HashSet<string> SinColision = new HashSet<string> { "JuntaPanel" };
    // Piezas viejas enteras dentro de la zona: se apagan.
    static readonly string[] Apagar = { "Fachada/PanelBlanco", "Fachada/HormigonManchado", "Fachada/RevoqueAmarillo", "Fachada/CartelBlanco", "Fachada/CartelVerde", "AccesoPlataformaConectada", "EnvolventeEsquinaAcceso/HormigonManchado", "EnvolventeEsquinaAcceso/RevoqueAmarillo" };
    // Mallas viejas que cruzan la zona: se copian sin los triángulos que caen enteros adentro.
    static readonly string[] Recortar = { "Fachada/HormigonViejo", "Fachada/Hormigon","Fachada/Vidrio", "Fachada/PinturaBlanca", "Fachada/VidrioLuz", "Fachada/Galvanizado", "EnvolventeEsquinaAcceso/PinturaBlanca", "EnvolventeEsquinaAcceso/HormigonBeige", "EnvolventeEsquinaAcceso/Vidrio", "EnvolventeEsquinaAcceso/Hormigon", "AccesoRetranqueado/Hormigon", "AccesoRetranqueado/HormigonBeige" };

    static bool EnZona(Vector3 l) => (l.x > 420.45f && l.x < 443.3f && l.z > 294.95f && l.z < 304.5f) || (l.x > 442.3f && l.x < 443.4f && l.z > 276.4f && l.z < 298f && l.y > 2.95f && l.y < 9.6f);

    // ---------------------------------------------------------------- Muros con huecos

    struct Hueco { public float s0, s1, y0, y1; public Hueco(float s0, float s1, float y0, float y1) { this.s0 = s0; this.s1 = s1; this.y0 = y0; this.y1 = y1; } }

    static void F(string mat, Vector3 a, Vector3 r, Vector3 n, float s0, float y0, float d0, float s1, float y1, float d1) =>
        Caja(mat, a + r * (s0 + s1) / 2 + n * (d0 + d1) / 2 + up * (y0 + y1) / 2, r * Mathf.Abs(s1 - s0) / 2, up * Mathf.Abs(y1 - y0) / 2, n * Mathf.Abs(d1 - d0) / 2);

    // Muro de «a» a lo largo de r (mirando a n), de 0 a L y de y0 a y1, con espesor de d0 a d1 (hacia n), salvo los huecos.
    // Con «junta» > 0 dibuja las juntas horizontales de los paneles cada «junta» y las verticales cada «juntaV».
    static void Muro(string mat, Vector3 a, Vector3 r, Vector3 n, float L, float y0, float y1, float d0, float d1, IList<Hueco> huecos, float junta = 0, float juntaV = 0)
    {
        var ss = new List<float> { 0, L }; var ys = new List<float> { y0, y1 };
        foreach (var h in huecos) { ss.Add(h.s0); ss.Add(h.s1); ys.Add(h.y0); ys.Add(h.y1); }
        ss = ss.Distinct().OrderBy(v => v).ToList(); ys = ys.Distinct().OrderBy(v => v).ToList();
        for (int i = 1; i < ss.Count; i++)
            for (int j = 1; j < ys.Count; j++)
            {
                float sm = (ss[i - 1] + ss[i]) / 2, ym = (ys[j - 1] + ys[j]) / 2;
                if (huecos.Any(h => sm > h.s0 && sm < h.s1 && ym > h.y0 && ym < h.y1)) continue;
                F(mat, a, r, n, ss[i - 1], ys[j - 1], d0, ss[i], ys[j], d1);
                if (junta > 0)
                    for (float y = y0 + junta; y < y1 - .05f; y += junta)
                        if (y > ys[j - 1] + .01f && y < ys[j] - .01f) F("JuntaPanel", a, r, n, ss[i - 1], y - .012f, d1, ss[i], y + .012f, d1 + .012f);
                if (juntaV > 0)
                    for (float s = juntaV; s < L - .05f; s += juntaV)
                        if (s > ss[i - 1] + .01f && s < ss[i] - .01f) F("JuntaPanel", a, r, n, s - .012f, ys[j - 1], d1, s + .012f, ys[j], d1 + .016f);
            }
    }

    // Ventana metida «prof» en el hueco, con el revestimiento del vano (dintel, jambas y alféizar).
    static void Vano(Vector3 a, Vector3 r, Vector3 n, Hueco h, float d1, float prof, string vano, int panos, bool travesano = false)
    {
        float dv = d1 - prof, e = .05f;
        F(vano, a, r, n, h.s0, h.y0, dv - .1f, h.s1, h.y0 + e, d1);
        F(vano, a, r, n, h.s0, h.y1 - e, dv - .1f, h.s1, h.y1, d1);
        F(vano, a, r, n, h.s0, h.y0 + e, dv - .1f, h.s0 + e, h.y1 - e, d1);
        F(vano, a, r, n, h.s1 - e, h.y0 + e, dv - .1f, h.s1, h.y1 - e, d1);
        float w = h.s1 - h.s0 - 2 * e, hh = h.y1 - h.y0 - 2 * e;
        var b = a + r * ((h.s0 + h.s1) / 2) + n * dv + up * (h.y0 + e);
        Ventana(b, n, r, w, hh, panos, false);
        if (travesano) Caja("PinturaBlanca", b + up * (hh * .78f) + n * .035f, r * w / 2, up * .035f, n * .035f);
    }

    // ---------------------------------------------------------------- Frente norte

    static readonly Vector3 AN = new Vector3(XO, 0, ZF);

    static void CuerpoChapa(Transform grupo)
    {
        var r = Vector3.right; var n = Vector3.forward; var a = new Vector3(XO, 0, ZF);
        float L = XC - XO;
        // Base oliva.
        F("RevoqueOliva", a, r, n, 0, -.1f, Z0 - ZF, L, HB, 0);
        // Chapa en bandas con huecos: las dos filas de ventanas contra el hormigón y el marco vertical junto al ladrillo.
        var bajo = new Hueco(L - 5.6f, L - .25f, 4.25f, 5.55f); var alto = new Hueco(L - 5.6f, L - .25f, 6.95f, 8.25f);
        var marco = new Hueco(1.7f, 4.3f, 4.0f, 8.9f);
        Muro("PanelPlata", a, r, n, L, HB, HT, Z0 - ZF, 0, new[] { bajo, alto, marco }, .8f, 2.8f);
        foreach (var fila in new[] { bajo, alto })
        {
            float m = (fila.s0 + fila.s1) / 2;
            Vano(a, r, n, new Hueco(fila.s0, m - .2f, fila.y0, fila.y1), 0, .35f, "HormigonMoho", 2);
            Vano(a, r, n, new Hueco(m + .2f, fila.s1, fila.y0, fila.y1), 0, .35f, "HormigonMoho", 2);
            F("HormigonMoho", a, r, n, m - .2f, fila.y0, -.45f, m + .2f, fila.y1, -.05f);
        }
        // Marco vertical de hormigón un poco salido, con las dos ventanas y el antepecho manchado entre ellas.
        const float sal = .12f, g = .32f;
        F("HormigonMoho", a, r, n, marco.s0, marco.y0, -.3f, marco.s0 + g, marco.y1, sal);
        F("HormigonMoho", a, r, n, marco.s1 - g, marco.y0, -.3f, marco.s1, marco.y1, sal);
        F("HormigonMoho", a, r, n, marco.s0, marco.y1 - g, -.3f, marco.s1, marco.y1, sal);
        F("HormigonMoho", a, r, n, marco.s0, marco.y0, -.3f, marco.s1, marco.y0 + g, sal);
        F("HormigonManchado", a, r, n, marco.s0 + g, 5.55f, -.32f, marco.s1 - g, 7.25f, -.22f);
        Vano(a, r, n, new Hueco(marco.s0 + g, marco.s1 - g, marco.y0 + g, 5.55f), -.22f, .1f, "HormigonMoho", 2, true);
        Vano(a, r, n, new Hueco(marco.s0 + g, marco.s1 - g, 7.25f, marco.y1 - g), -.22f, .1f, "HormigonMoho", 2, true);
        // Ventanas de la base (con banderola) y la puerta doble blanca con bisagras de planchuela.
        foreach (var s in new[] { 6.35f, 10.95f }) { Ventana(a + r * s + up * 1.55f, n, r, 1.3f, 1.25f, 1, false); Caja("PinturaBlanca", a + r * s + up * 2.5f + n * .035f, r * .65f, up * .035f, n * .035f); }
        const float p0 = .9f, p1 = 2.6f, ph = YP + 2.35f;
        F("PinturaBlanca", a, r, n, p0, YP, 0, p1, ph, .05f);
        F("PinturaBlanca", a, r, n, p0 - .08f, YP, 0, p0, ph + .08f, .08f);
        F("PinturaBlanca", a, r, n, p1, YP, 0, p1 + .08f, ph + .08f, .08f);
        F("PinturaBlanca", a, r, n, p0 - .08f, ph, 0, p1 + .08f, ph + .08f, .08f);
        F("MetalNegro", a, r, n, (p0 + p1) / 2 - .008f, YP, .05f, (p0 + p1) / 2 + .008f, ph, .055f);
        foreach (var y in new[] { YP + .3f, YP + 1.15f, YP + 2.0f })
        {
            F("Galvanizado", a, r, n, p0 + .02f, y, .05f, p0 + .45f, y + .06f, .07f);
            F("Galvanizado", a, r, n, p1 - .45f, y, .05f, p1 - .02f, y + .06f, .07f);
        }
        F("Galvanizado", a, r, n, (p0 + p1) / 2 + .05f, YP + 1.0f, .05f, (p0 + p1) / 2 + .35f, YP + 1.06f, .08f);
        // Cartel de entrada contra el hormigón.
        float sc = L - .85f;
        F("CartelVerde", a, r, n, sc - .48f, 1.55f, .01f, sc + .48f, 2.95f, .06f);
        F("PinturaBlanca", a, r, n, sc - .38f, 1.63f, .06f, sc + .38f, 2.72f, .07f);
        F("AzulCartel", a, r, n, sc - .34f, 2.3f, .07f, sc + .34f, 2.66f, .075f);
        var c = a + r * sc + n * .08f;
        var tinta = new Color(.12f, .2f, .45f);
        Texto(grupo, "Cartel entrada 7 (título)", "<b>ENTRADA</b>", c + up * 2.84f, n, .75f, new Vector2(.86f, .16f), Color.white);
        Texto(grupo, "Cartel entrada 7 (módulo)", "<b>Módulo 7</b>\n<size=55%>Ingreso peatonal</size>", c + up * 2.48f, n, .62f, new Vector2(.66f, .34f), Color.white);
        Texto(grupo, "Cartel entrada 7 (sector)", "<size=45%>SECTOR</size>\n<b>A</b>", c + up * 1.98f, n, 1.6f, new Vector2(.66f, .6f), tinta);
        Texto(grupo, "Cartel entrada 7 (pie)", "del Estacionamiento", c + up * 1.68f, n, .42f, new Vector2(.7f, .08f), tinta);
        // Cámara arriba de la base junto al hormigón; cámara y sirena junto al ladrillo.
        foreach (var s in new[] { L - .35f, .55f })
        {
            F("PinturaBlanca", a, r, n, s - .04f, HB - .25f, 0, s + .04f, HB - .05f, .2f);
            Caja("PinturaBlanca", a + r * s + up * (HB - .3f) + n * .3f, r * .07f, up * .07f, n * .16f);
            Caja("MetalNegro", a + r * s + up * (HB - .3f) + n * .47f, r * .05f, up * .05f, n * .01f);
        }
        F("RevoqueCrema", a, r, n, 1.25f, HB + .05f, 0, 1.6f, HB + .4f, .1f);
        F("VolqueteRojo", a, r, n, 1.36f, HB + .14f, .1f, 1.49f, HB + .27f, .13f);
        // Caño de chimenea por la esquina con el ladrillo, con sus abrazaderas y el sombrerete.
        var cc = new Vector3(XO + .18f, 0, ZF + .2f);
        Cilindro("Galvanizado", cc + up * 1.0f, cc + up * (HT + 1.0f), .11f, 12);
        for (float y = 2.0f; y < HT; y += 1.6f) Box("Galvanizado", cc.x - .03f, y, ZF, cc.x + .03f, y + .05f, cc.z);
        Cilindro("Galvanizado", cc + up * (HT + 1.0f), cc + up * (HT + 1.06f), .22f, 12);
        Cilindro("Galvanizado", cc + up * (HT + 1.14f), cc + up * (HT + 1.2f), .26f, 12);
        // Remate de chapa arriba.
        Box("PanelPlata", XO, HT, Z0, XC, HT + .08f, ZF + .04f);
    }

    static void BloqueHormigon(Transform grupo)
    {
        var r = Vector3.right; var n = Vector3.forward; var a = new Vector3(XC, 0, ZF);
        float L = XE + .05f - XC;
        var vanos = new List<Hueco>();
        foreach (var (y0, y1) in new[] { (4.3f, 5.5f), (7.0f, 8.2f) })
            foreach (var s in new[] { 1.6f, 4.15f, 6.7f }) vanos.Add(new Hueco(s - .9f, s + .9f, y0, y1));
        Muro("HormigonMoho", a, r, n, L, 2.98f, HT, Z0 - ZF, 0, vanos);
        foreach (var h in vanos) Vano(a, r, n, h, 0, .3f, "HormigonMoho", 2);
        Box("Hormigon", XC, HT, Z0, XE + .05f, HT + .1f, ZF + .05f);
        // Reflector cerca de la chapa y los dos faroles chicos de la otra punta.
        var pr = a + r * .9f + up * (HT - .1f);
        Barra("MetalNegro", pr, pr + n * .35f, .04f);
        Caja("MetalNegro", pr + n * .45f + up * -.05f, r * .25f, up * .17f, n * .06f);
        Caja("PinturaBlanca", pr + n * .52f + up * -.05f, r * .2f, up * .13f, n * .01f);
        foreach (var s in new[] { L - 1.6f, L - .6f }) { var q = a + r * s + up * (HT - .55f); Barra("MetalNegro", q, q + n * .3f + up * .1f, .03f); Caja("MetalNegro", q + n * .33f + up * .08f, r * .07f, up * .05f, n * .09f); }
        // Columna de la esquina, cielorraso del voladizo con sus tubos de luz y los detectores de metales.
        Box("HormigonMoho", XE - .95f, -.1f, ZF - .9f, XE + .05f, 2.98f, ZF);
        for (float z = 290.0f; z < 297.3f; z += 2.4f)
            foreach (var x in new[] { 436.6f, 439.6f }) Box("PinturaBlanca", x - .6f, 2.88f, z - .07f, x + .6f, 2.95f, z + .07f);
        foreach (var x0 in new[] { 439.55f, 440.75f })
        {
            Box("Galvanizado", x0, YE, 294.85f, x0 + .12f, YE + 2.15f, 295.4f);
            Box("Galvanizado", x0 + .92f, YE, 294.85f, x0 + 1.04f, YE + 2.15f, 295.4f);
            Box("Galvanizado", x0, YE + 2.05f, 294.85f, x0 + 1.04f, YE + 2.25f, 295.4f);
        }
    }

    // Cara a la calle: el hormigón con dos filas de ventanas y la base oliva con las ventanitas.
    static void CaraCalle()
    {
        var r = Vector3.back; var n = Vector3.right; var a = new Vector3(XE + .05f, 0, Z0);
        float L = Z0 - ZE0;
        var vanos = new List<Hueco>();
        foreach (var (y0, y1) in new[] { (4.4f, 5.4f), (7.1f, 8.1f) })
            for (int i = 0; i < 5; i++) { float s = 2.0f + i * 3.6f; vanos.Add(new Hueco(s - .75f, s + .75f, y0, y1)); }
        Muro("HormigonMoho", a, r, n, L, 2.98f, HT, -.45f, 0, vanos);
        foreach (var h in vanos) Vano(a, r, n, h, 0, .25f, "HormigonMoho", 2);
        Box("Hormigon", XE - .4f, HT, ZE0, XE + .05f, HT + .1f, Z0);
        // Base oliva del tramo cerrado (el resto es el voladizo de la entrada).
        float sb = Z0 - 288.6f;
        F("RevoqueOliva", a, r, n, sb, -.1f, -.45f, L, HB, 0);
        foreach (var s in new[] { sb + 2.6f, sb + 6.0f, sb + 9.4f }) Ventana(a + r * s + up * 1.8f, n, r, .9f, .8f, 1, false);
    }

    // ---------------------------------------------------------------- Plataforma, escalones, rampas y barandas

    static void Rampa(string mat, float x0, float x1, float z0, float z1, float y0, float y1)
    {
        var p = new[] { new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1) };
        var nTop = Vector3.Cross(p[3] - p[0], p[1] - p[0]).normalized; if (nTop.y < 0) nTop = -nTop;
        Quad(mat, p[0], p[1], p[2], p[3], nTop);
        Quad(mat, new Vector3(x0, -.1f, z0), new Vector3(x0, y0, z0), new Vector3(x0, y1, z1), new Vector3(x0, -.1f, z1), Vector3.left);
        Quad(mat, new Vector3(x1, -.1f, z0), new Vector3(x1, y0, z0), new Vector3(x1, y1, z1), new Vector3(x1, -.1f, z1), Vector3.right);
        if (y0 > y1) Quad(mat, new Vector3(x0, -.1f, z0), new Vector3(x1, -.1f, z0), new Vector3(x1, y0, z0), new Vector3(x0, y0, z0), z1 > z0 ? Vector3.back : Vector3.forward);
    }

    // Rampa a lo largo de x (para la rampita del 7a).
    static void RampaX(string mat, float x0, float x1, float z0, float z1, float y0, float y1)
    {
        var p = new[] { new Vector3(x0, y0, z0), new Vector3(x0, y0, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0) };
        Quad(mat, p[0], p[1], p[2], p[3], up);
        Quad(mat, new Vector3(x0, -.1f, z1), new Vector3(x0, y0, z1), new Vector3(x1, y1, z1), new Vector3(x1, -.1f, z1), Vector3.forward);
        Quad(mat, new Vector3(x0, -.1f, z0), new Vector3(x0, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, -.1f, z0), Vector3.back);
    }

    // Baranda galvanizada: postes con planchuela cada ~1,5 m, pasamanos a 1 m y dos caños más abajo (a y b en el piso).
    static void BarandaPostes(Vector3 a, Vector3 b)
    {
        var d = b - a; int k = Mathf.Max(1, Mathf.CeilToInt(new Vector2(d.x, d.z).magnitude / 1.5f));
        for (int i = 0; i <= k; i++)
        {
            var p = a + d * i / k;
            Cilindro("Galvanizado", p, p + up * 1.0f, .024f, 8);
            Box("Galvanizado", p.x - .07f, p.y, p.z - .07f, p.x + .07f, p.y + .012f, p.z + .07f);
        }
        Cilindro("Galvanizado", a + up * 1.0f, b + up * 1.0f, .026f, 8);
        Cilindro("Galvanizado", a + up * .55f, b + up * .55f, .02f, 8);
        Cilindro("Galvanizado", a + up * .16f, b + up * .16f, .02f, 8);
    }

    static void Plataforma(Transform grupo)
    {
        const string h = "HormigonViejo";
        const float zw = 299.3f, zp = 300.6f, xs0 = 434.9f, xs1 = 438.6f, xr0 = 439.6f, xr1 = 442.6f, zr = 304.6f, xw = 414.6f;
        // Piso de la entrada bajo el voladizo y los tres escalones hasta la plataforma.
        Box(h, XC + .1f, -.1f, 294.35f, XE - .95f, YE, 296.3f);
        Box(h, XC + .1f, -.1f, 296.3f, XE - .95f, .75f, 296.6f);
        Box(h, XC + .1f, -.1f, 296.6f, XE - .95f, .6f, 296.9f);
        // Plataforma delante de la entrada y vereda elevada delante de la base.
        Box(h, 433.2f, -.1f, 296.9f, XE, YP, zp);
        Box(h, XO, -.1f, ZF, 433.2f, YP, zw);
        // Escalones a la vereda y la rampa del lado de la calle.
        Box(h, xs0, -.1f, zp, xs1, .32f, zp + .3f);
        Box(h, xs0, -.1f, zp + .3f, xs1, .17f, zp + .6f);
        Rampa(h, xr0, xr1, zp, zr, YP, 0);
        // Rampita hacia el 7a: baja de la vereda elevada contra el ladrillo.
        RampaX(h, xw, XO, 296.95f, zw, 0, YP);
        // Barandas.
        BarandaPostes(new Vector3(xw, 0, zw), new Vector3(XO, YP, zw));
        BarandaPostes(new Vector3(XO, YP, zw), new Vector3(433.2f, YP, zw));
        BarandaPostes(new Vector3(433.2f, YP, zw), new Vector3(433.2f, YP, zp));
        BarandaPostes(new Vector3(433.2f, YP, zp), new Vector3(xs0, YP, zp));
        BarandaPostes(new Vector3(xs1, YP, zp), new Vector3(xr0, YP, zp));
        BarandaPostes(new Vector3(xr0, YP, zp), new Vector3(xr0, 0, zr));
        BarandaPostes(new Vector3(xr1, YP, zp), new Vector3(xr1, 0, zr));
        BarandaPostes(new Vector3(XE - .1f, YP, ZF), new Vector3(XE - .1f, YP, zp));
        // Atril blanco con el plano, sobre la plataforma.
        var at = new Vector3(439.1f, YP, 299.7f);
        Box("Galvanizado", at.x - .03f, at.y, at.z - .03f, at.x + .03f, at.y + .95f, at.z + .03f);
        Caja("PinturaBlanca", at + up * 1.05f, Vector3.right * .33f, new Vector3(0, .87f, -.5f) * .24f, new Vector3(0, .5f, .87f) * .015f);
        Caja("CianBiblioteca", at + up * 1.05f + new Vector3(0, .5f, .87f) * .016f, Vector3.right * .26f, new Vector3(0, .87f, -.5f) * .17f, new Vector3(0, .5f, .87f) * .002f);
    }

    // ---------------------------------------------------------------- Recortes de lo viejo

    static void Recortes(Transform mapa, Transform m7)
    {
        var contenedor = AssetDatabase.LoadAssetAtPath<Mesh>(RecortesPath);
        if (contenedor == null) { contenedor = new Mesh { name = "Modulo7Acceso_Recortes" }; AssetDatabase.CreateAsset(contenedor, RecortesPath); }
        foreach (var ruta in Recortar)
        {
            var t = m7.Find(ruta); if (t == null) continue;
            var mf = t.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
            if (AssetDatabase.GetAssetPath(mf.sharedMesh) == RecortesPath) continue;
            var src = mf.sharedMesh; var copia = UnityEngine.Object.Instantiate(src); copia.name = "M7A_" + t.parent.name + "_" + t.name;
            var v = src.vertices; int quitados = 0;
            for (int s = 0; s < src.subMeshCount; s++)
            {
                var tr = src.GetTriangles(s); var quedan = new List<int>();
                for (int i = 0; i < tr.Length; i += 3)
                {
                    bool adentro = true;
                    for (int k = 0; k < 3 && adentro; k++) adentro = EnZona(mapa.InverseTransformPoint(t.TransformPoint(v[tr[i + k]])));
                    if (adentro) { quitados++; continue; }
                    quedan.Add(tr[i]); quedan.Add(tr[i + 1]); quedan.Add(tr[i + 2]);
                }
                copia.SetTriangles(quedan, s);
            }
            if (quitados == 0) { UnityEngine.Object.DestroyImmediate(copia); continue; }
            AssetDatabase.AddObjectToAsset(copia, contenedor);
            mf.sharedMesh = copia;
            var mc = t.GetComponent<MeshCollider>(); if (mc != null) mc.sharedMesh = copia;
        }
        foreach (var ruta in Apagar) { var t = m7.Find(ruta); if (t != null) t.gameObject.SetActive(false); }
        var carteles = m7.Find("Carteles");
        if (carteles != null)
            foreach (Transform c in carteles)
            {
                var l = mapa.InverseTransformPoint(c.position);
                if (l.z > 297f && l.x > 420f && l.x < 443f) c.gameObject.SetActive(false);
            }
    }

    // ---------------------------------------------------------------- Armado

    [MenuItem("Riftwalker/Universidad/Armar acceso del Módulo 7")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/universidadMAPA.unity" || EditorApplication.isPlaying) throw new InvalidOperationException("Abrir universidadMAPA fuera de Play.");
        GameObject mapaGo = null;
        foreach (var g in scene.GetRootGameObjects()) if (g.name == "Mapa (escala 1,25)") mapaGo = g;
        if (mapaGo == null) throw new InvalidOperationException("No se encontró Mapa (escala 1,25).");
        var mapa = mapaGo.transform; var m7 = mapa.Find("Modulo7");
        if (m7 == null) throw new InvalidOperationException("No se encontró Modulo7.");
        const string tex = "Assets/Materials/Uni/Uni_HormigonMoho.png";
        AssetDatabase.ImportAsset(tex);
        MaterialDesde("HormigonMoho", "Hormigon", new Color(.88f, .88f, .88f), AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
        MaterialDesde("RevoqueOliva", "RevoqueAmarillo", new Color(.68f, .67f, .47f));
        MaterialDesde("PanelPlata", "PinturaBlanca", new Color(.69f, .72f, .75f));
        MaterialColor("JuntaPanel", new Color(.47f, .49f, .52f), .3f);
        MaterialColor("AzulCartel", new Color(.13f, .22f, .5f), .2f);
        MaterialColor("VolqueteRojo", new Color(.5f, .14f, .09f), .25f);
        bool habiaCambios = scene.isDirty;
        lotes.Clear();
        var anterior = mapa.Find(Grupo); if (anterior != null) UnityEngine.Object.DestroyImmediate(anterior.gameObject);
        Recortes(mapa, m7);
        var go = new GameObject(Grupo); go.transform.SetParent(mapa, false);
        CuerpoChapa(go.transform); BloqueHormigon(go.transform); CaraCalle(); Plataforma(go.transform);
        Volcar(go.transform, MeshPath, "Modulo7Acceso", "M7A_", SinColision);
        EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
        if (!habiaCambios) EditorSceneManager.SaveScene(scene);
        Debug.Log(habiaCambios ? "Acceso del Módulo 7 armado (la escena tenía cambios sin guardar: no se guardó)." : "Acceso del Módulo 7 armado.");
    }
}
