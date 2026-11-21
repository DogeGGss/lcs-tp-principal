using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ArmadoUni;

// Auditorio y gradas de la universidad (US 340), según el plano 1:500 y las fotos:
// - Las gradas frente al escenario: terrazas de pasto que suben, con las cotas del plano, de ±0 (lado del escenario) a
//   +0,40, +0,60, +0,80, +1,00 y +1,20 (fila de árboles), separadas por los bancos largos de hormigón en V. El terreno
//   se levanta solo ahí; el borde baja en pendiente de pasto.
// - El camino del portón de Gutiérrez sube por la terraza de +1,00 con escalones y baja al cruce por la escalera norte,
//   con su muro de contención y la baranda (reemplaza al camino plano que había).
// - El Auditorio: la caja alta de chapa gris horizontal con la boca del escenario hacia las gradas, las puertas de los
//   costados, el paño de óxido, el reflector y la tarima de hormigón delante; la escalera que sube a la terraza de
//   los baños, con baranda; el contenedor celeste junto al portón.
// Sin interiores. Medidas en metros, en el espacio de «Mapa (escala 1,25)». Se puede volver a correr: rehace el grupo
// Ambiente_Auditorio y vuelve a poner el terreno de las gradas.
public static class AuditorioUniversidad
{
    const string MeshPath = "Assets/Meshes/Uni/Auditorio.asset", Grupo = "Ambiente_Auditorio";
    static readonly string[] CajasViejas = { "Auditorio", "Auditorio_Banos" };
    static readonly HashSet<string> SinColision = new HashSet<string> { "Galvanizado", "CordonAmarillo", "MetalNegro" };
    static Vector2 V(float x, float z) => new Vector2(x, z);

    // ---------------------------------------------------------------- Gradas y terreno

    // Líneas de bancos (de norte a sur) y la cota de cada terraza: al oeste de la primera, ±0; al este de la última, +1,20.
    static readonly Vector2[][] Bancos =
    {
        new[] { V(175.8f, 128.0f), V(180.1f, 123.8f), V(184.2f, 98.5f) },
        new[] { V(182.4f, 130.4f), V(184.75f, 125.4f), V(189.15f, 106.75f), V(188.4f, 101.25f) },
        new[] { V(187.5f, 131.1f), V(193.55f, 113.5f), V(192.8f, 97.0f) },
        new[] { V(193.0f, 126.9f), V(198.5f, 112.8f), V(196.1f, 102.35f) },
        new[] { V(201.6f, 129.1f), V(205.3f, 113.7f), V(204.7f, 109.2f), V(199.05f, 99.2f) },
    };
    static readonly float[] Niveles = { 0, .4f, .6f, .8f, 1.0f, 1.2f };
    // Borde este de la terraza de +1,20 (fila de árboles), de norte a sur.
    static readonly Vector2[] BordeEste = { V(201.8f, 136f), V(211.2f, 121.2f), V(212.3f, 114.8f), V(209.0f, 107.1f), V(204.0f, 104.0f) };
    // Bloque norte (sigue la terraza de +1,00 junto a la escalera norte) y la escalera.
    const float BX0 = 193.2f, BX1 = 197.3f, BZ0 = 126.5f, BZ1 = 139.0f, EX1 = 200.8f, EZ0 = 129.6f, EZ1 = 133.6f;
    static readonly Rect Zona = new Rect(172, 94, 44, 47);

    static float? XEn(Vector2[] l, float z)
    {
        for (int i = 0; i + 1 < l.Length; i++)
        {
            Vector2 a = l[i], b = l[i + 1]; float z0 = Mathf.Min(a.y, b.y), z1 = Mathf.Max(a.y, b.y);
            if (z < z0 || z > z1 || z1 - z0 < 1e-4f) continue;
            return Mathf.Lerp(a.x, b.x, (z - a.y) / (b.y - a.y));
        }
        return null;
    }

    static float Suave(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }

    static float Nivel(Vector2 q)
    {
        if (q.x >= BX0 + .4f && q.x <= BX1 - .4f && q.y >= BZ0 && q.y <= BZ1 - .4f) return 1.0f;
        int k = 0; foreach (var l in Bancos) { var x = XEn(l, q.y); if (x != null && q.x > x.Value + .6f) k++; }
        float L = Niveles[k];
        if (k == 5)
        {
            float d = Distancia(BordeEste, q, out var c, out var tan);
            var rel = q - c; bool este = tan.x * rel.y - tan.y * rel.x > 0;
            if (este) L *= 1 - Mathf.Clamp01(d / 3.5f);
        }
        return L * Mathf.Min(Suave(97.5f, 103.0f, q.y), 1 - Suave(EZ0, EZ1, q.y));
    }

    // Bancos: como en las fotos, el muro de contención del escalón entre terrazas y, arriba, la viga gruesa del banco
    // apoyada en tacos amarillentos (queda la ranura en sombra), con las marcas amarillas. Donde la diferencia se
    // achica (las puntas), el banco termina.
    static void BancosDeLasGradas()
    {
        foreach (var l in Bancos)
            for (int i = 0; i + 1 < l.Length; i++)
            {
                Vector2 a = l[i], b = l[i + 1]; float L = (b - a).magnitude; var d2 = (b - a) / L; var oeste2 = new Vector2(d2.y, -d2.x);
                int n = Mathf.Max(1, Mathf.CeilToInt(L / .5f)); float taco = 1.1f, marca = 0;
                for (int j = 0; j < n; j++)
                {
                    var p0 = Vector2.Lerp(a, b, (float)j / n); var p1 = Vector2.Lerp(a, b, (float)(j + 1) / n); var m = (p0 + p1) / 2;
                    float yb = Nivel(m + oeste2 * 1.6f), ya = Nivel(m - oeste2 * 1.6f); if (ya - yb < .08f) { taco = 1.1f; continue; }
                    var d = new Vector3(d2.x, 0, d2.y); var o = new Vector3(oeste2.x, 0, oeste2.y); float h = (p1 - p0).magnitude / 2 + .004f;
                    var c = new Vector3(m.x, 0, m.y);
                    Caja("HormigonGris", c + up * ((yb - .15f + ya) / 2), d * h, up * ((ya - yb + .15f) / 2), o * .175f);
                    Caja("HormigonBanco", c + o * .08f + up * (ya + .205f), d * h, up * .125f, o * .25f);
                    taco += h * 2; marca += h * 2;
                    if (taco > 2.2f) { taco = 0; Caja("AmarilloPalido", c + up * (ya + .04f), d * .14f, up * .04f, o * .13f); }
                    if (marca > 3.1f) { marca = 0; Cilindro("CordonAmarillo", c + o * .08f + up * (ya + .33f), c + o * .08f + up * (ya + .335f), .07f, 10); }
                }
            }
    }

    // Camino de hormigón por bloques que siguen la cota del terreno; con «escalones», la cota se redondea en peldaños.
    static void Camino(IList<Vector2> pts, float ancho, bool escalones, float plano = -1)
    {
        const float paso = .3f, peldano = 1f / 6;
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            Vector2 a = pts[i], b = pts[i + 1]; float L = (b - a).magnitude; var d2 = (b - a) / L; int n = Mathf.Max(1, Mathf.CeilToInt(L / paso));
            for (int j = 0; j < n; j++)
            {
                var m = Vector2.Lerp(a, b, (j + .5f) / n); float y = plano >= 0 ? plano : Nivel(m);
                if (escalones) y = Mathf.Ceil(y / peldano - .02f) * peldano;
                var d = new Vector3(d2.x, 0, d2.y); var o = Vector3.Cross(up, d);
                Caja("Vereda", new Vector3(m.x, (y + .06f - .15f) / 2, m.y), d * (L / n / 2 + .01f), up * ((y + .06f + .15f) / 2), o * ancho / 2);
            }
        }
    }

    static readonly Vector2[] CaminoSur = { V(190.2f, 77.3f), V(192.0f, 95.0f) };
    static readonly Vector2[] CaminoGradas = { V(192.0f, 95.0f), V(197.5f, 103.0f), V(201.0f, 112.5f), V(201.2f, 120.0f), V(199.05f, EZ0) };
    static readonly Vector2[] CaminoNorte = { V(199.05f, EZ1), V(198.5f, 148.5f) };
    static readonly Vector2[] CaminoViejo = { V(192f, 95f), V(196f, 120f), V(198.5f, 148.5f) };

    // Escalera norte: baja de la terraza de +1,00 al cruce, con el muro de contención del bloque a su lado (y baranda arriba).
    static void EscaleraNorte()
    {
        const int k = 7; float run = (EZ1 - EZ0) / k;
        for (int i = 1; i < k; i++) Box("HormigonGris", BX1, -.15f, EZ0 + (i - 1) * run, EX1, 1.0f * (k - i) / k, EZ0 + i * run);
        Box("HormigonGris", BX1, -.15f, EZ0 - 1.2f, EX1, 1.0f, EZ0);
        // Bloque: muros al oeste, al norte y al este (junto a la escalera), piso de hormigón y barandas.
        Box("HormigonGris", BX0 - .25f, -.2f, BZ0, BX0, 1.1f, BZ1 + .25f);
        Box("HormigonGris", BX0 - .25f, -.2f, BZ1, BX1 + .25f, 1.1f, BZ1 + .25f);
        Box("HormigonGris", BX1, -.2f, EZ0, BX1 + .25f, 1.1f, BZ1 + .25f);
        Box("Vereda", BX0, .9f, BZ0, BX1, 1.04f, BZ1);
        Baranda(new Vector3(BX0 - .12f, 1.1f, BZ1 + .12f), new Vector3(BX1 + .12f, 1.1f, BZ1 + .12f));
        Baranda(new Vector3(BX1 + .12f, 1.1f, BZ1 + .12f), new Vector3(BX1 + .12f, 1.1f, EZ0));
        Baranda(new Vector3(BX0 - .12f, 1.1f, BZ0 + .5f), new Vector3(BX0 - .12f, 1.1f, BZ1 + .12f));
        // Pasamanos sobre la escalera, del lado del muro.
        var p0 = new Vector3(BX1 + .4f, 1.9f, EZ0); var p1 = new Vector3(BX1 + .4f, .9f, EZ1 + .3f);
        Cilindro("Galvanizado", p0, p1, .025f, 8);
        foreach (var f in new[] { .1f, .5f, .9f }) { var p = Vector3.Lerp(p0, p1, f); Cilindro("Galvanizado", p, new Vector3(BX1 + .25f, p.y, p.z), .015f, 6); }
    }

    // ---------------------------------------------------------------- Auditorio

    static readonly Vector2[] Planta = { V(112.5f, 89.0f), V(161.2f, 95.3f), V(157.6f, 123.1f), V(109.9f, 116.9f) };
    const float HAud = 12.5f;
    static readonly Vector3 E0 = new Vector3(161.2f, 0, 95.3f), E1 = new Vector3(157.6f, 0, 123.1f);
    static Vector3 Re => (E1 - E0).normalized;
    static Vector3 Ne => Vector3.Cross(up, Re).normalized;
    static Vector3 PF(float s, float y, float e) => E0 + Re * s + Ne * e + up * y;

    static void PanelFrente(string mat, float s0, float s1, float y0, float y1, float e0, float e1)
        => Caja(mat, PF((s0 + s1) / 2, (y0 + y1) / 2, (e0 + e1) / 2), Re * (s1 - s0) / 2, up * (y1 - y0) / 2, Ne * Mathf.Max(.005f, (e1 - e0) / 2));

    // Polígono convexo agrandado «d» hacia afuera (cada vértice se aleja del centro).
    static Vector2[] Expandir(Vector2[] p, float d)
    {
        var c = Vector2.zero; foreach (var q in p) c += q; c /= p.Length;
        var r = new Vector2[p.Length]; for (int i = 0; i < p.Length; i++) r[i] = p[i] + (p[i] - c).normalized * d * 1.42f;
        return r;
    }

    static void Auditorio(Transform grupo)
    {
        Prisma("ChapaAuditorio", "Techo", Planta, -.2f, HAud);
        Prisma("MetalNegro", null, Expandir(Planta, .05f), HAud - .25f, HAud + .02f);
        float L = (E1 - E0).magnitude;
        // Frente al este, hacia las gradas: la boca del escenario con su portón, las dos puertas de los costados con su
        // dintel, las juntas de los paneles, el paño de óxido del extremo sur y el reflector arriba.
        PanelFrente("PortonSeccional", 8.0f, 20.0f, 0, 4.6f, 0, .03f);
        PanelFrente("MetalNegro", 7.8f, 20.2f, 4.6f, 4.95f, 0, .18f);
        foreach (var s in new[] { 7.8f, 20.0f }) PanelFrente("MetalNegro", s, s + .2f, 0, 4.6f, 0, .14f);
        foreach (var s in new[] { 5.4f, 20.6f }) { PanelFrente("MetalNegro", s, s + 2.0f, 0, 2.6f, 0, .02f); PanelFrente("ChapaOxido", s - .2f, s + 2.2f, 2.6f, 3.1f, 0, .1f); }
        foreach (var y in new[] { 8.4f }) PanelFrente("MetalNegro", 0, L, y, y + .08f, 0, .03f);
        foreach (var s in new[] { 4.6f, 14.0f, 23.4f }) PanelFrente("MetalNegro", s, s + .08f, 4.95f, HAud - .25f, 0, .03f);
        PanelFrente("MetalNegro", 13.7f, 14.3f, HAud - .7f, HAud - .45f, 0, .35f);
        PanelFrente("PinturaBlanca", 13.75f, 14.25f, HAud - .68f, HAud - .47f, .35f, .36f);
        // Tarima de hormigón delante de la boca, con dos escalones.
        Losa4("Hormigon", PF(6, -.2f, 0), PF(22, -.2f, 0), PF(22, -.2f, 8.5f), PF(6, -.2f, 8.5f), .65f);
        Losa4("Hormigon", PF(6.3f, -.2f, 8.5f), PF(21.7f, -.2f, 8.5f), PF(21.7f, -.2f, 8.9f), PF(6.3f, -.2f, 8.9f), .5f);
        Losa4("Hormigon", PF(6.6f, -.2f, 8.9f), PF(21.4f, -.2f, 8.9f), PF(21.4f, -.2f, 9.3f), PF(6.6f, -.2f, 9.3f), .35f);
        // Explanada de hormigón entre el auditorio y el primer banco.
        Piso("Vereda", new[] { V(161.4f, 94.0f), V(157.2f, 124.5f), V(175.8f, 128.0f), V(180.1f, 123.8f), V(184.2f, 98.5f), V(183.0f, 93.0f) }, .06f);
        // Lado del portón: la terraza de hormigón sobre los baños con barandas, la escalera ancha que sube desde la
        // explanada (barandas a los costados y al medio) y el bloque gris de la esquina con el paño alto de óxido.
        const float hT = 3.0f, bx0 = 113.5f, bx1 = 169.5f, bz0 = 80.1f, bz1 = 89.1f, ex0 = 164.5f, ez0 = 95.5f;
        Box("HormigonGris", bx0, -.2f, bz0, bx1, hT, bz1);
        Box("Vereda", bx0 + .1f, hT, bz0 + .1f, bx1 - .1f, hT + .06f, bz1 - .1f);
        const int k = 18; float run = (ez0 - bz1) / k, rise = hT / k;
        for (int i = 0; i < k; i++) Box("HormigonGris", ex0, -.15f, ez0 - (i + 1) * run, bx1, (i + 1) * rise, ez0 - i * run);
        foreach (var x in new[] { (ex0 + bx1) / 2, bx1 - .1f }) Baranda(new Vector3(x, rise, ez0 - .1f), new Vector3(x, hT, bz1 + .05f));
        Baranda(new Vector3(bx0 + .2f, hT + .06f, bz1 - .12f), new Vector3(158.5f, hT + .06f, bz1 - .12f));
        Baranda(new Vector3(bx1 - .12f, hT + .06f, bz0 + .2f), new Vector3(bx1 - .12f, hT + .06f, bz1 - .3f));
        Baranda(new Vector3(bx0 + .2f, hT + .06f, bz0 + .12f), new Vector3(bx1 - .2f, hT + .06f, bz0 + .12f));
        Box("HormigonGris", 158.5f, -.2f, bz1, ex0, 7.5f, 95.2f);
        Box("ChapaOxido", ex0, .3f, 93.6f, ex0 + .05f, 7.0f, 95.0f);
        Box("MetalNegro", 160.4f, 0, 95.2f, 162.0f, 2.4f, 95.25f);
        Box("HormigonGris", 158.4f, 7.5f, bz1 - .05f, ex0 + .05f, 7.6f, 95.3f);
        // Paso cerrado por la izquierda: portón de barrotes al pie de la escalera y alambrado entre los baños y la reja
        // de la calle; colisiones invisibles sobre la escalera, la terraza y ese pasillo.
        Box("MetalNegro", ex0, 0, ez0 + .05f, bx1, .08f, ez0 + .12f);
        Box("MetalNegro", ex0, .9f, ez0 + .05f, bx1, .96f, ez0 + .12f);
        Box("MetalNegro", ex0, 1.95f, ez0 + .05f, bx1, 2.02f, ez0 + .12f);
        for (float x = ex0 + .12f; x < bx1 - .05f; x += .12f) Box("MetalNegro", x - .015f, .08f, ez0 + .07f, x + .015f, 1.95f, ez0 + .1f);
        Box("Galvanizado", bx1 - .1f, 0, ez0 + .02f, bx1 + .05f, 2.1f, ez0 + .17f);
        Box("RejaMalla", 169.69f, .1f, 75.7f, 169.71f, 2.1f, 80.1f);
        foreach (var z in new[] { 75.8f, 78.0f, 80.0f }) Box("MetalNegro", 169.65f, 0, z - .04f, 169.75f, 2.2f, z + .04f);
        Box("MetalNegro", 169.66f, 2.1f, 75.7f, 169.74f, 2.16f, 80.1f);
        Box("MetalNegro", 169.66f, .06f, 75.7f, 169.74f, .12f, 80.1f);
        Bloqueo(grupo, "Bloqueo_EscaleraTerraza", new Vector3((ex0 + bx1) / 2, 3.0f, (bz1 + ez0 + .2f) / 2), new Vector3(bx1 - ex0, 6.0f, ez0 + .2f - bz1));
        Bloqueo(grupo, "Bloqueo_Terraza", new Vector3((bx0 + bx1) / 2, 5.5f, (bz0 + bz1) / 2), new Vector3(bx1 - bx0, 5.0f, bz1 - bz0));
        Bloqueo(grupo, "Bloqueo_PasilloSur", new Vector3(169.7f, 2.5f, 77.9f), new Vector3(.4f, 5.0f, 4.6f));
        // Contenedor celeste junto al portón: chapa ondulada, esquineros, las puertas del lado oeste y un aire.
        Box("ContenedorCeleste", 178.0f, 0, 82.6f, 184.06f, 2.6f, 85.04f);
        foreach (var (x, z) in new[] { (178.0f, 82.6f), (184.06f, 82.6f), (178.0f, 85.04f), (184.06f, 85.04f) }) Box("Galvanizado", x - .08f, 0, z - .08f, x + .08f, 2.62f, z + .08f);
        Box("Galvanizado", 177.98f, 2.5f, 82.55f, 184.08f, 2.64f, 85.09f);
        foreach (var z in new[] { 83.2f, 83.8f, 84.4f }) Box("MetalNegro", 177.96f, .2f, z - .02f, 177.98f, 2.4f, z + .02f);
        Aire(new Vector3(182.6f, 1.7f, 85.05f), Vector3.forward, .9f);
    }

    // ---------------------------------------------------------------- Armado

    [MenuItem("Riftwalker/Universidad/Armar Auditorio y gradas")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/universidadMAPA.unity" || EditorApplication.isPlaying) throw new InvalidOperationException("Abrir universidadMAPA fuera de Play.");
        GameObject mapaGo = null;
        foreach (var g in scene.GetRootGameObjects()) if (g.name == "Mapa (escala 1,25)") mapaGo = g;
        if (mapaGo == null) throw new InvalidOperationException("No se encontró Mapa (escala 1,25).");
        var chapa = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/Uni/Uni_ChapaLimpia.png");
        MaterialDesde("ChapaAuditorio", "ChapaClara", new Color(.62f, .63f, .66f), chapa);
        MaterialDesde("HormigonGris", "Hormigon", new Color(.5f, .51f, .5f));
        MaterialColor("AmarilloPalido", new Color(.88f, .82f, .58f), .1f);
        MaterialDesde("ContenedorCeleste", "ChapaClara", new Color(.6f, .84f, .86f), chapa);
        var mapa = mapaGo.transform;
        bool habiaCambios = scene.isDirty;
        lotes.Clear();
        var anterior = mapa.Find(Grupo); if (anterior != null) UnityEngine.Object.DestroyImmediate(anterior.gameObject);
        var sinDetalle = mapa.Find("Ambiente_EdificiosSinDetalle");
        foreach (var n in CajasViejas) { var t = sinDetalle != null ? sinDetalle.Find(n) : null; if (t != null) t.gameObject.SetActive(false); }
        var viejo = mapa.Find("Ambiente_CaminosNuevos/DelPortonAlCruce"); if (viejo != null) viejo.gameObject.SetActive(false);
        // Lo que está apoyado en el pasto de la zona (árboles, faroles, bancos) sigue al terreno nuevo.
        var apoyados = new List<(Transform t, float sobre)>();
        foreach (var nombre in new[] { "Ambiente_Vegetacion/Arboles_Campus", "Ambiente_Faroles", "Ambiente_Mobiliario" })
        {
            var g = mapa.Find(nombre); if (g == null) continue;
            foreach (Transform t in g)
            {
                var l = mapa.InverseTransformPoint(t.position);
                if (!Zona.Contains(new Vector2(l.x, l.z))) continue;
                apoyados.Add((t, l.y - AlturaTerreno(mapa, l.x, l.z)));
            }
        }
        TerrenoNivel(mapa, Zona, q => Nivel(q));
        foreach (var (t, sobre) in apoyados)
        {
            var l = mapa.InverseTransformPoint(t.position); l.y = Nivel(new Vector2(l.x, l.z)) + sobre;
            t.position = mapa.TransformPoint(l);
        }
        var go = new GameObject(Grupo); go.transform.SetParent(mapa, false);
        BancosDeLasGradas();
        Camino(CaminoSur, 3.0f, false, 0);
        Camino(CaminoGradas, 2.4f, true);
        Camino(CaminoNorte, 3.0f, false, 0);
        EscaleraNorte();
        Auditorio(go.transform);
        Volcar(go.transform, MeshPath, "Auditorio", "Aud_", SinColision);
        Physics.SyncTransforms();
        // Pasto: fuera debajo de los caminos, la explanada, la tarima y el bloque; de vuelta sobre el tramo viejo del camino.
        Pasto(mapa, new Rect(150, 76, 66, 74), (q, v, leer) =>
        {
            if (Distancia(CaminoSur, q, out _, out _) < 1.8f || Distancia(CaminoGradas, q, out _, out _) < 1.5f || Distancia(CaminoNorte, q, out _, out _) < 1.8f) return 0;
            if (q.x >= BX0 - .3f && q.x <= EX1 + .2f && q.y >= BZ0 && q.y <= BZ1 + .3f) return 0;
            // Al pie de cada banco, una franja sin pasto para que se lea el escalón de hormigón.
            foreach (var l in Bancos) if (Distancia(l, q, out _, out _) < .55f && Nivel(q - new Vector2(.6f, 0)) < Nivel(q + new Vector2(1.2f, 0)) - .05f) return 0;
            var w = mapa.TransformPoint(new Vector3(q.x, 0, q.y));
            bool tapado = Physics.Raycast(new Vector3(w.x, 300f, w.z), Vector3.down, out var hit, 600f) && hit.collider.transform.IsChildOf(go.transform) && hit.point.y < mapa.TransformPoint(new Vector3(0, 1.5f, 0)).y;
            if (tapado && q.x < 186f) return 0;
            return PastoDeVuelta(mapa, CaminoViejo, 2.3f, q, v, leer);
        });
        EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
        if (!habiaCambios) EditorSceneManager.SaveScene(scene);
        Debug.Log(habiaCambios ? "Auditorio y gradas armados (la escena tenía cambios sin guardar: no se guardó)." : "Auditorio y gradas armados.");
    }
}
