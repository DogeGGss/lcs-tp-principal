using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ArmadoUni;

// Punta noroeste del Módulo 1 (US 084), rehecha según las fotos: donde había un tambor de hormigón cerrado y una caja de
// revoque va la escalera exterior abierta de dos tramos (narices amarillas, barandas de acero) que llega al puente del
// primer piso, frente a la puerta de arriba; la aleta curva de hormigón queda como un pórtico alto, más bajo hacia el
// oeste, con su abertura; abajo, la puerta de planta baja entre los dos caños galvanizados y el camino de baldosas que
// pasa bajo el puente. Se puede volver a correr: rehace el grupo Ambiente_Modulo1Escalera.
public static class Modulo1EscaleraUniversidad
{
    const string MeshPath = "Assets/Meshes/Uni/Modulo1Escalera.asset", Grupo = "Ambiente_Modulo1Escalera";
    static readonly HashSet<string> SinColision = new HashSet<string> { "Galvanizado", "CordonAmarillo", "MetalNegro" };
    // Marco de la cara noroeste del bloque: s a lo largo (hacia el noreste), t hacia afuera (noroeste).
    static readonly Vector3 O = new Vector3(222.9f, 0, 151.2f), U = new Vector3(.70711f, 0, .70711f), N = new Vector3(-.70711f, 0, .70711f);
    static Vector3 P(float s, float t, float y) => O + U * s + N * t + up * y;
    const float F = 4.2f, Hm = 2.1f;                       // primer piso y descanso
    const float TA0 = 2.9f, TA1 = 4.5f, TB0 = 4.7f, TB1 = 6.3f; // carril de adentro (2º tramo) y de afuera (1º tramo)
    const float S0 = 1.9f, S1 = 6.0f, SP = 8.0f;           // descanso hasta S0, tramos de S0 a S1, puente de S1 a SP
    const float TP = 6.85f;                                  // el puente llega hasta la viga de la aleta
    // La aleta: arco de círculo (el del plano y del modelo anterior).
    static readonly Vector2 CA = new Vector2(227.72f, 146.94f);
    const float RA = 15.55f, EA = .2f, A0 = 92.3f, A1 = 153.9f;

    static void BoxST(string mat, float s0, float s1, float t0, float t1, float y0, float y1)
        => Caja(mat, P((s0 + s1) / 2, (t0 + t1) / 2, (y0 + y1) / 2), U * Mathf.Abs(s1 - s0) / 2, up * Mathf.Abs(y1 - y0) / 2, N * Mathf.Abs(t1 - t0) / 2);

    // ---------------------------------------------------------------- Lo que se saca del modelo anterior

    // Saca de las mallas del Módulo 1 el tambor curvo (Gris) y la caja de revoque con sus ventanas, bandas y techo.
    static int QuitarTamborYCaja(Transform mapa)
    {
        var m1 = mapa.Find("Ambiente_Modulo1"); if (m1 == null) return 0; int total = 0;
        var caja = new HashSet<string> { "Revoque", "Techos", "Marcos", "VidriosLuz", "Hormigon", "Bandas" };
        var ala = new HashSet<string> { "Modulo1_Paredes", "Revoque", "Techos", "Marcos", "VidriosLuz", "Hormigon", "Bandas", "Zocalo" };
        foreach (var mf in m1.GetComponentsInChildren<MeshFilter>(true))
        {
            var m = mf.sharedMesh; if (m == null || !AssetDatabase.GetAssetPath(m).EndsWith("Modulo1.asset")) continue;
            if (mf.name != "Gris" && !caja.Contains(mf.name) && !ala.Contains(mf.name)) continue;
            var v = m.vertices; var t = m.triangles; var nuevos = new List<int>(t.Length);
            for (int i = 0; i < t.Length; i += 3)
            {
                var c = mapa.InverseTransformPoint(mf.transform.TransformPoint((v[t[i]] + v[t[i + 1]] + v[t[i + 2]]) / 3));
                float s = Vector3.Dot(c - O, U), tt = Vector3.Dot(c - O, N);
                bool quitar = mf.name == "Gris" ? c.x > 212.5f && c.x < 228.5f && c.z > 152.5f && c.z < 164.5f
                                                : caja.Contains(mf.name) && s > -.15f && s < 6.7f && tt > .03f && tt < 8.1f && (mf.name != "Techos" || c.y > 8.2f);
                // El ala de ladrillo que salía de la cara noroeste, al lado de la escalera, tampoco va.
                if (!quitar && ala.Contains(mf.name) && s > -5.6f && s < .15f && tt > .03f && tt < 8.6f) quitar = true;
                if (!quitar) { nuevos.Add(t[i]); nuevos.Add(t[i + 1]); nuevos.Add(t[i + 2]); }
            }
            if (nuevos.Count == t.Length) continue;
            total += (t.Length - nuevos.Count) / 3;
            m.triangles = nuevos.ToArray(); m.RecalculateBounds(); EditorUtility.SetDirty(m);
            var mc = mf.GetComponent<MeshCollider>(); if (mc != null) { mc.sharedMesh = null; mc.sharedMesh = m; }
        }
        return total;
    }

    // ---------------------------------------------------------------- Escalera y puente

    static float AltoTramo1(float s) => s <= S0 ? Hm : Hm * (S1 - s) / (S1 - S0);
    static float AltoTramo2(float s) => s <= S0 ? Hm : s >= S1 ? F : Hm + (F - Hm) * (s - S0) / (S1 - S0);

    static void Escalera()
    {
        const int k = 12; float run = (S1 - S0) / k, rise = Hm / k;
        // 1º tramo (carril de afuera): sube del piso, en S1, hasta el descanso, en S0.
        for (int i = 0; i < k; i++)
        {
            float sb = S1 - i * run, sa = sb - run, y = (i + 1) * rise;
            BoxST("HormigonGris", sa, sb, TB0, TB1, Mathf.Max(-.05f, y - .32f), y);
            BoxST("CordonAmarillo", sb - .07f, sb - .01f, TB0 + .05f, TB1 - .05f, y, y + .005f);
        }
        // 2º tramo (carril de adentro): del descanso sube hasta el puente.
        for (int i = 0; i < k; i++)
        {
            float sa = S0 + i * run, sb = sa + run, y = Hm + (i + 1) * rise;
            BoxST("HormigonGris", sa, sb, TA0, TA1, y - .32f, y);
            BoxST("CordonAmarillo", sa + .01f, sa + .07f, TA0 + .05f, TA1 - .05f, y, y + .005f);
        }
        // Lado de abajo de los tramos (losa inclinada oscura).
        Quad("HormigonViejo", P(S1, TB0, -.05f), P(S0, TB0, Hm - .32f), P(S0, TB1, Hm - .32f), P(S1, TB1, -.05f), -N * 0 + Vector3.down * 1 + U * .5f);
        Quad("HormigonViejo", P(S0, TA0, Hm - .3f), P(S1, TA0, F - .3f), P(S1, TA1, F - .3f), P(S0, TA1, Hm - .3f), Vector3.down - U * .5f);
        // Descanso, con su losa y dos columnas en las esquinas de afuera.
        BoxST("HormigonGris", 0, S0, TA0, TB1, Hm - .22f, Hm);
        foreach (var s in new[] { .18f, S0 - .18f }) BoxST("HormigonGris", s - .14f, s + .14f, TB1 - .3f, TB1 - .02f, -.05f, Hm - .22f);
        // Tabique entre tramos: sube con el 2º tramo y llega al descanso.
        int n = 16;
        for (int i = 0; i < n; i++)
        {
            float s0 = (float)i / n * S1, s1 = (float)(i + 1) / n * S1;
            float y0 = AltoTramo2(s0) - .32f, y1 = AltoTramo2(s1) - .32f;
            var a = P(s0, TA1, 0); var b = P(s1, TA1, 0);
            Quad("HormigonGris", a + up * -.05f, b + up * -.05f, b + up * y1, a + up * y0, -N);
            Quad("HormigonGris", a + N * .2f + up * -.05f, b + N * .2f + up * -.05f, b + N * .2f + up * y1, a + N * .2f + up * y0, N);
            Quad("HormigonGris", a + up * y0, b + up * y1, b + N * .2f + up * y1, a + N * .2f + up * y0, up);
        }
        // Puente del primer piso: losa con su viga de borde gruesa, apoyada en la pared y en una columna.
        BoxST("HormigonGris", S1, SP, 0, TP, F - .25f, F);
        BoxST("HormigonGris", S1, SP, TP - .3f, TP, F - .65f, F);
        BoxST("HormigonGris", SP - .3f, SP, 0, TP, F - .65f, F);
        BoxST("HormigonGris", SP - .28f, SP - .02f, TP - .62f, TP - .34f, -.05f, F - .65f);
        BoxST("HormigonViejo", S1 + .05f, SP - .32f, .05f, TP - .32f, F - .27f, F - .25f);
        // Barandas: lado de afuera del 1º tramo y del descanso, lado del camino del 2º tramo, bordes libres del puente.
        Baranda(P(S1, TB1 - .05f, 0), P(S0, TB1 - .05f, Hm));
        Baranda(P(S0, TB1 - .05f, Hm), P(.05f, TB1 - .05f, Hm));
        Baranda(P(.05f, TB1 - .05f, Hm), P(.05f, TA0 + .05f, Hm));
        Baranda(P(.05f, TA0 + .05f, Hm), P(S0, TA0 + .05f, Hm));
        Baranda(P(S0, TA0 + .05f, Hm), P(S1, TA0 + .05f, F));
        Baranda(P(S1, TP - .05f, F), P(SP - .05f, TP - .05f, F));
        Baranda(P(SP - .05f, TP - .05f, F), P(SP - .05f, .15f, F));
        Baranda(P(S1 + .05f, TB0, F), P(S1 + .05f, TP - .05f, F));
        Baranda(P(S1 + .05f, .15f, F), P(S1 + .05f, TA0, F));
    }

    // ---------------------------------------------------------------- Aleta

    static float AltoAleta(float a)
    {
        float f = Mathf.Clamp01((a - A0) / (A1 - A0));
        return 4.9f + 5.3f * Mathf.Sqrt(Mathf.Max(0, 1 - f * f));
    }

    static Vector3 PA(float a, float r, float y)
    {
        float rad = a * Mathf.Deg2Rad;
        return new Vector3(CA.x + r * Mathf.Cos(rad), y, CA.y + r * Mathf.Sin(rad));
    }

    // Tramo de muro curvo de a0 a a1 (grados), de y0 a y1. y1 < 0: hasta el borde de arriba; y0 < -1: la banda de
    // arriba (0,95 bajo el borde).
    static void TramoAleta(float a0, float a1, float y0, float y1, bool tapa0, bool tapa1)
    {
        const string M = "HormigonGris";
        float yt0 = y1 < 0 ? AltoAleta(a0) : y1, yt1 = y1 < 0 ? AltoAleta(a1) : y1;
        float yb0 = y0 < -1 ? AltoAleta(a0) - .95f : y0, yb1 = y0 < -1 ? AltoAleta(a1) - .95f : y0;
        float ri = RA - EA, re = RA + EA; float am = (a0 + a1) / 2 * Mathf.Deg2Rad; var nr = new Vector3(Mathf.Cos(am), 0, Mathf.Sin(am));
        Quad(M, PA(a0, re, yb0), PA(a1, re, yb1), PA(a1, re, yt1), PA(a0, re, yt0), nr);
        Quad(M, PA(a0, ri, yb0), PA(a1, ri, yb1), PA(a1, ri, yt1), PA(a0, ri, yt0), -nr);
        Quad(M, PA(a0, ri, yt0), PA(a1, ri, yt1), PA(a1, re, yt1), PA(a0, re, yt0), up);
        if (yb0 > 0) Quad(M, PA(a0, ri, yb0), PA(a1, ri, yb1), PA(a1, re, yb1), PA(a0, re, yb0), Vector3.down);
        Vector3 Tg(float a) { float r = a * Mathf.Deg2Rad; return new Vector3(-Mathf.Sin(r), 0, Mathf.Cos(r)); }
        if (tapa0) Quad(M, PA(a0, ri, yb0), PA(a0, re, yb0), PA(a0, re, yt0), PA(a0, ri, yt0), -Tg(a0));
        if (tapa1) Quad(M, PA(a1, ri, yb1), PA(a1, re, yb1), PA(a1, re, yt1), PA(a1, ri, yt1), Tg(a1));
    }

    // La aleta: pórtico curvo abierto. Un poste alto en cada punta, la banda de arriba que baja hacia el oeste y la viga
    // a la altura del puente, donde apoya; por debajo pasa el camino.
    static void Aleta()
    {
        float g(float m) => m / RA * Mathf.Rad2Deg;
        float b0 = A0 + g(.9f), b1 = A1 - g(.9f);
        TramoAleta(A0, b0, -.2f, -1, true, false);
        TramoAleta(b1, A1, -.2f, -1, false, true);
        const int n = 40;
        for (int i = 0; i < n; i++)
        {
            float x0 = Mathf.Lerp(b0, b1, (float)i / n), x1 = Mathf.Lerp(b0, b1, (float)(i + 1) / n);
            TramoAleta(x0, x1, F - .1f, F + .55f, false, false);
            TramoAleta(x0, x1, -2, -1, false, false);
        }
        Vector3 Tg(float a) { float r = a * Mathf.Deg2Rad; return new Vector3(-Mathf.Sin(r), 0, Mathf.Cos(r)); }
        foreach (var (a, sg) in new[] { (b0, 1f), (b1, -1f) })
        {
            Quad("HormigonGris", PA(a, RA - EA, -.2f), PA(a, RA + EA, -.2f), PA(a, RA + EA, F - .1f), PA(a, RA - EA, F - .1f), Tg(a) * sg);
            Quad("HormigonGris", PA(a, RA - EA, F + .55f), PA(a, RA + EA, F + .55f), PA(a, RA + EA, AltoAleta(a) - .95f), PA(a, RA - EA, AltoAleta(a) - .95f), Tg(a) * sg);
        }
    }

    // ---------------------------------------------------------------- Cara noroeste

    // Puerta de vidrio con marco de aluminio sobre la cara (en s, a la altura y).
    static void PuertaVidrio(float s, float y, float w, float h)
    {
        BoxST("Vidrio", s - w / 2, s + w / 2, .0f, .04f, y, y + h);
        foreach (var d in new[] { -1f, 1f }) BoxST("PinturaBlanca", s + d * w / 2 - .05f, s + d * w / 2 + .05f, 0, .1f, y, y + h + .06f);
        BoxST("PinturaBlanca", s - w / 2, s + w / 2, 0, .1f, y + h, y + h + .06f);
        BoxST("PinturaBlanca", s - .02f, s + .02f, 0, .08f, y, y + h);
        BoxST("PinturaBlanca", s - w / 2, s + w / 2, 0, .08f, y + h * .62f, y + h * .62f + .05f);
        BoxST("MetalNegro", s + .1f, s + .13f, .08f, .13f, y + .95f, y + 1.3f);
    }

    static void CaraNoroeste(Transform grupo)
    {
        // Puertas de planta baja y del primer piso (debajo de su alero de hormigón), los caños galvanizados y el cesto.
        PuertaVidrio(7.0f, .05f, 1.2f, 2.3f);
        PuertaVidrio(7.0f, F, 1.1f, 2.25f);
        BoxST("HormigonGris", 6.15f, 7.85f, 0, .7f, F + 2.5f, F + 2.62f);
        foreach (var s in new[] { 5.65f, 8.35f }) Cilindro("Galvanizado", P(s, .25f, -.05f), P(s, .25f, 8.85f), .19f, 14);
        BoxST("TachoAzul", 7.75f, 8.2f, .25f, .7f, 0, .75f);
        BoxST("PinturaNegra", 7.72f, 8.23f, .22f, .73f, .75f, .8f);
        // Camino de baldosas desde el pasto, por la abertura de la aleta y debajo del puente, hasta la puerta.
        BoxST("BaldosaGris", 6.3f, 7.6f, .1f, 11.5f, 0, .06f);
        BoxST("BaldosaGris", -4.6f, 9.6f, .05f, 2.2f, 0, .065f);
        // El medio cilindro de hormigón en el pasto, junto a la puerta.
        var b = P(9.8f, 2.9f, .12f);
        Cilindro("Hormigon", b - U * .14f, b + U * .14f, .32f, 16);
    }

    // ---------------------------------------------------------------- Armado

    [MenuItem("Riftwalker/Universidad/Armar escalera del Módulo 1")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/universidadMAPA.unity" || EditorApplication.isPlaying) throw new InvalidOperationException("Abrir universidadMAPA fuera de Play.");
        GameObject mapaGo = null;
        foreach (var g in scene.GetRootGameObjects()) if (g.name == "Mapa (escala 1,25)") mapaGo = g;
        if (mapaGo == null) throw new InvalidOperationException("No se encontró Mapa (escala 1,25).");
        MaterialColor("BaldosaGris", new Color(.33f, .34f, .36f), .2f);
        MaterialDesde("HormigonGris", "Hormigon", new Color(.5f, .51f, .5f));
        var mapa = mapaGo.transform;
        bool habiaCambios = scene.isDirty;
        lotes.Clear();
        var anterior = mapa.Find(Grupo); if (anterior != null) UnityEngine.Object.DestroyImmediate(anterior.gameObject);
        int sacados = QuitarTamborYCaja(mapa);
        // El pino que estaba donde va la escalera.
        var arboles = mapa.Find("Ambiente_Vegetacion/Arboles_Campus");
        if (arboles != null)
            foreach (Transform a in arboles)
            {
                var l = mapa.InverseTransformPoint(a.position);
                if (a.name.StartsWith("Arbol_Pino") && Mathf.Abs(l.x - 227.2f) < .6f && Mathf.Abs(l.z - 159.7f) < .6f) a.gameObject.SetActive(false);
            }
        var go = new GameObject(Grupo); go.transform.SetParent(mapa, false);
        Escalera(); Aleta(); CaraNoroeste(go.transform);
        Volcar(go.transform, MeshPath, "Modulo1Escalera", "M1E_", SinColision);
        // Pasto: fuera debajo de las baldosas.
        Pasto(mapa, new Rect(212, 148, 20, 22), (q, v, leer) =>
        {
            float s = Vector2.Dot(q - new Vector2(O.x, O.z), new Vector2(U.x, U.z)), t = Vector2.Dot(q - new Vector2(O.x, O.z), new Vector2(N.x, N.z));
            return (s > 6.2f && s < 7.8f && t > 0 && t < 11.6f) || (s > -4.7f && s < 9.7f && t > 0 && t < 2.3f) ? 0 : v;
        });
        EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
        if (!habiaCambios) EditorSceneManager.SaveScene(scene);
        Debug.Log("Escalera del Módulo 1 armada (" + sacados + " triángulos del modelo anterior sacados)" + (habiaCambios ? "; la escena tenía cambios sin guardar: no se guardó." : "."));
    }
}
