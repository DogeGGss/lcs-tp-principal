using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ArmadoUni;

// Edificio de León Suárez (US 342), con la parte que se ve desde el patio de la escuela infantil: el cuerpo alto de
// hormigón visto de la punta norte (con las juntas de los paneles), el ala baja con la ventana corrida arriba y la planta
// baja vidriada, y la escalera volada de escalones sueltos con su pretil macizo que sube hasta el descanso. Las caras que
// dan a la calle quedan lisas hasta tener fotos. Sin interiores. Medidas en metros, en el espacio de «Mapa (escala 1,25)».
// Se puede volver a correr: rehace el grupo Ambiente_LeonSuarez.
public static class LeonSuarezUniversidad
{
    const string MeshPath = "Assets/Meshes/Uni/LeonSuarez.asset", Grupo = "Ambiente_LeonSuarez";
    static readonly string[] CajasViejas = { "EdificioLeonSuarez" };
    static readonly HashSet<string> SinColision = new HashSet<string> { "MetalNegro" };
    static Vector2 V(float x, float z) => new Vector2(x, z);
    const float XE = 105.0f, ZA = 140.0f, ZN = 157.4f, HA = 12.5f, HB = 8.2f;

    static void Edificio()
    {
        Prisma("HormigonGris", "Techo", new[] { V(90.6f, ZA), V(XE, ZA), V(XE, ZN), V(90.6f, ZN) }, -.2f, HA);
        Prisma("HormigonGris", "Techo", new[] { V(90.6f, 80.1f), V(109.5f, 80.1f), V(110.0f, 117.0f), V(XE, ZA), V(90.6f, ZA) }, -.2f, HB);
        // Juntas de los paneles en las caras este y norte del cuerpo alto.
        for (float y = 2.5f; y < HA; y += 2.5f)
        {
            Box("MetalNegro", XE, y - .02f, ZA, XE + .015f, y + .02f, ZN);
            Box("MetalNegro", 90.6f, y - .02f, ZN, XE, y + .02f, ZN + .015f);
        }
        for (float z = ZA + 4.35f; z < ZN; z += 4.35f) Box("MetalNegro", XE, 0, z - .02f, XE + .015f, HA, z + .02f);
        for (float x = 94.2f; x < XE; x += 3.6f) Box("MetalNegro", x - .02f, 0, ZN, x + .02f, HA, ZN + .015f);
        // Ala baja: la ventana corrida arriba y la planta baja vidriada en la cara que da al patio (la oblicua).
        var a = new Vector3(110.0f, 0, 117.0f); var b = new Vector3(XE, 0, ZA);
        var n = Vector3.Cross(up, (b - a).normalized);
        Ventanas(a + (b - a) * .05f, a + (b - a) * .95f, n, 6.2f, 1.8f, 1.0f, 2.2f, "PinturaBlanca", false);
        Ventanas(a + (b - a) * .45f, a + (b - a) * .97f, n, .3f, 2.2f, 2.8f, 2.4f, "MetalNegro", false);
    }

    // Escalera volada: escalones sueltos que suben del piso (al norte) hasta el descanso, contra el cuerpo alto, con el
    // pretil macizo inclinado del lado de afuera; el descanso con su pretil y el tabique que lo sostiene.
    static void Escalera()
    {
        const float x0 = XE, x1 = XE + 2.2f, zb = 150.0f, zt = 141.2f, ht = 4.2f; const int k = 22;
        float run = (zb - zt) / k, rise = ht / k;
        for (int i = 0; i < k; i++)
        {
            float top = (i + 1) * rise;
            Box("HormigonGris", x0 + .02f, top - .16f, zb - (i + 1) * run, x1, top, zb - i * run);
        }
        var c = new Vector3(x1 + .11f, (-.7f + (ht - .7f)) / 2 + .85f, (zb + zt) / 2);
        Caja("HormigonGris", c, new Vector3(0, ht / 2, (zt - zb) / 2), up * .85f, Vector3.right * .11f);
        // Descanso, pretil y tabique.
        Box("HormigonGris", x0, ht - .25f, 137.4f, x1 + 1.0f, ht, zt);
        Box("HormigonGris", x1 + .78f, ht, 137.4f, x1 + 1.0f, ht + 1.0f, zt);
        Box("HormigonGris", x0, ht, 137.4f, x1 + 1.0f, ht + 1.0f, 137.62f);
        Box("HormigonGris", x1 + .4f, -.2f, 137.4f, x1 + 1.0f, ht - .25f, 138.2f);
        Box("MetalNegro", x0 + .3f, ht, 139.2f, x0 + .35f, ht + 2.2f, 140.4f);
    }

    // Alambrado entre la punta noroeste del cuerpo alto y la reja de la calle, con su colisión alta: no se puede pasar a
    // la franja que queda detrás del edificio.
    static void Cierre(Transform grupo, Transform mapa)
    {
        const float z = 157.2f, xa = 90.7f, xb = 87.05f;
        float y = Mathf.Min(AlturaTerreno(mapa, xa, z), AlturaTerreno(mapa, xb, z));
        Alambrado(new Vector3(xa, y, z), new Vector3(xb, y, z));
        Bloqueo(grupo, "Bloqueo_DetrasLeonSuarez", new Vector3((xa + xb) / 2, y + 2.5f, z), new Vector3(xa - xb + .4f, 5.0f, .4f));
    }

    [MenuItem("Riftwalker/Universidad/Armar León Suárez")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/universidadMAPA.unity" || EditorApplication.isPlaying) throw new InvalidOperationException("Abrir universidadMAPA fuera de Play.");
        GameObject mapaGo = null;
        foreach (var g in scene.GetRootGameObjects()) if (g.name == "Mapa (escala 1,25)") mapaGo = g;
        if (mapaGo == null) throw new InvalidOperationException("No se encontró Mapa (escala 1,25).");
        MaterialDesde("HormigonGris", "Hormigon", new Color(.5f, .51f, .5f));
        var mapa = mapaGo.transform;
        bool habiaCambios = scene.isDirty;
        lotes.Clear();
        var anterior = mapa.Find(Grupo); if (anterior != null) UnityEngine.Object.DestroyImmediate(anterior.gameObject);
        var sinDetalle = mapa.Find("Ambiente_EdificiosSinDetalle");
        foreach (var nn in CajasViejas) { var t = sinDetalle != null ? sinDetalle.Find(nn) : null; if (t != null) t.gameObject.SetActive(false); }
        var go = new GameObject(Grupo); go.transform.SetParent(mapa, false);
        Edificio(); Escalera(); Cierre(go.transform, mapa);
        Volcar(go.transform, MeshPath, "LeonSuarez", "LS_", SinColision);
        EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
        if (!habiaCambios) EditorSceneManager.SaveScene(scene);
        Debug.Log(habiaCambios ? "León Suárez armado (la escena tenía cambios sin guardar: no se guardó)." : "León Suárez armado.");
    }
}
