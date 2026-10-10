using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Edificios de la uni que todavía no estaban en el mapa, solo como volumen (sin detalles): muros de ladrillo,
// techo liso y colisión. Las plantas salen del plano del campus (Assets/Art/Reference/plano_ungs.png, el mismo
// de RefImage_Aerial): x y z en metros del mundo. Saca los árboles, faroles y muebles que quedaban adentro.
// Se puede volver a correr: borra lo que armó antes y lo arma de nuevo.
public static class EdificiosSinDetalleUniversidad
{
    const string MeshPath = "Assets/Meshes/Uni/EdificiosSinDetalle.asset";
    const string Raiz = "Ambiente_EdificiosSinDetalle";
    const float Base = -0.2f;

    class Edificio
    {
        public string nombre;
        public float alto;
        public Vector2[] planta; // convexa, en x/z del mundo
        public Edificio(string nombre, float alto, params float[] xz)
        {
            this.nombre = nombre; this.alto = alto;
            planta = Enumerable.Range(0, xz.Length / 2).Select(i => new Vector2(xz[2 * i], xz[2 * i + 1])).ToArray();
        }
    }

    static Edificio[] Edificios => new[]
    {
        // Módulo 1: la tira en diagonal y la punta sudeste, que gira un poco.
        new Edificio("Modulo1_Tira", 7.8f, 212.2f, 140.6f, 230.6f, 158.7f, 259.2f, 130.1f, 241.1f, 112.0f),
        new Edificio("Modulo1_Punta", 7.6f, 246.5f, 117.4f, 259.2f, 130.1f, 266.8f, 122.5f, 286.0f, 92.7f, 273.8f, 87.5f),
        // Biblioteca: la sala grande, las oficinas y el bloque de abajo.
        new Edificio("Biblioteca_Sala", 9.0f, 93.8f, 180.0f, 147.5f, 180.0f, 147.5f, 215.5f, 96.5f, 215.5f),
        new Edificio("Biblioteca_Oficinas", 7.8f, 147.5f, 196.5f, 182.0f, 196.5f, 182.0f, 215.5f, 147.5f, 215.5f),
        new Edificio("Biblioteca_Bloque", 7.0f, 155.8f, 169.5f, 171.3f, 169.5f, 171.3f, 196.5f, 155.8f, 196.5f),
        // Módulo 2A y Aulas Anexas 2A.
        new Edificio("Modulo2A", 7.8f, 188.3f, 175.2f, 227.7f, 175.2f, 227.7f, 201.7f, 188.3f, 201.7f),
        new Edificio("AulasAnexas2A", 4.5f, 187.9f, 205.0f, 217.9f, 205.0f, 217.9f, 215.0f, 187.9f, 215.0f),
        // Escuela Infantil y Sala de Juegos.
        new Edificio("EscuelaInfantil_Escalera", 7.0f, 121.5f, 127.5f, 135.5f, 127.5f, 135.5f, 149.0f, 121.5f, 149.0f),
        new Edificio("EscuelaInfantil", 7.0f, 135.5f, 130.0f, 173.5f, 130.0f, 173.5f, 152.5f, 135.5f, 152.5f),
        new Edificio("SalaDeJuegos", 4.0f, 130.8f, 156.6f, 140.2f, 156.6f, 140.2f, 166.0f, 130.8f, 166.0f),
        // Auditorio, la tira de baños de adelante y el edificio largo sobre José León Suárez.
        new Edificio("Auditorio", 12.0f, 112.5f, 89.0f, 161.2f, 95.3f, 157.6f, 123.1f, 109.9f, 116.9f),
        new Edificio("Auditorio_Banos", 4.5f, 113.5f, 80.1f, 167.3f, 80.1f, 167.3f, 89.1f, 113.5f, 89.1f),
        new Edificio("EdificioLeonSuarez", 7.8f, 90.6f, 80.1f, 109.5f, 80.1f, 110.0f, 117.0f, 104.1f, 157.4f, 90.6f, 157.4f),
        // Módulo 3: la tira de arriba (3A) y las tres alas que cuelgan (3B, 3C y 3D).
        new Edificio("Modulo3A", 7.8f, 215.7f, 281.3f, 362.5f, 281.3f, 362.5f, 295.8f, 215.7f, 295.8f),
        new Edificio("Modulo3A_Pasillo3B", 7.6f, 236.3f, 263.7f, 243.3f, 263.7f, 243.3f, 281.3f, 236.3f, 281.3f),
        // La esquina sudeste del 3B se ochava para que pase el camino en arco.
        new Edificio("Modulo3B", 7.8f, 225.3f, 232.0f, 246.0f, 232.0f, 260.6f, 238.0f, 260.6f, 263.7f, 225.3f, 263.7f),
        new Edificio("Modulo3B_Punta", 7.8f, 225.3f, 263.7f, 236.3f, 263.7f, 236.3f, 272.1f, 225.3f, 272.1f),
        new Edificio("Modulo3C", 7.8f, 283.7f, 241.8f, 319.3f, 241.8f, 319.3f, 281.3f, 283.7f, 281.3f),
        new Edificio("Modulo3D", 7.8f, 345.3f, 239.5f, 362.5f, 239.5f, 362.5f, 281.3f, 345.3f, 281.3f),
        new Edificio("Modulo3D_Anexo", 4.5f, 338.9f, 236.3f, 345.3f, 236.3f, 345.3f, 252.3f, 338.9f, 252.3f),
        // Módulo 10 (sobre Sarratea): tres plantas, con el patio abierto al frente.
        new Edificio("Modulo10_Oeste", 11.0f, 109.5f, 315.6f, 136.7f, 315.6f, 136.7f, 350.3f, 109.5f, 350.3f),
        new Edificio("Modulo10_Centro", 11.0f, 136.7f, 331.0f, 154.6f, 331.0f, 154.6f, 350.3f, 136.7f, 350.3f),
        new Edificio("Modulo10_Este", 11.0f, 154.6f, 315.6f, 202.1f, 315.6f, 202.1f, 350.3f, 154.6f, 350.3f),
        // El anexo llega hasta la calle; se corta antes de la reja (x ≈ 99-100 en ese tramo).
        new Edificio("Modulo10_Anexo", 4.5f, 101.2f, 320.6f, 109.5f, 320.6f, 109.5f, 340.0f, 101.2f, 340.0f),
    };

    static bool Dentro(Vector2[] p, Vector2 q, float margen)
    {
        // Convexa en cualquier sentido: todos los productos cruz con el mismo signo (con margen hacia afuera).
        int signo = 0;
        for (int i = 0; i < p.Length; i++)
        {
            var a = p[i]; var b = p[(i + 1) % p.Length];
            var e = (b - a).normalized;
            float c = e.x * (q.y - a.y) - e.y * (q.x - a.x);
            if (Mathf.Abs(c) <= margen) continue;
            int s = c > 0 ? 1 : -1;
            if (signo == 0) signo = s; else if (s != signo) return false;
        }
        return true;
    }

    static Mesh Prisma(Edificio e)
    {
        var p = e.planta;
        // Sentido antihorario visto desde arriba (x, z): así las normales quedan para afuera.
        float area = 0; for (int i = 0; i < p.Length; i++) { var a = p[i]; var b = p[(i + 1) % p.Length]; area += a.x * b.y - b.x * a.y; }
        if (area < 0) p = p.Reverse().ToArray();
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var muros = new List<int>(); var techo = new List<int>();
        float recorrido = 0;
        for (int i = 0; i < p.Length; i++)
        {
            var a = p[i]; var b = p[(i + 1) % p.Length]; float largo = (b - a).magnitude;
            int k = v.Count;
            v.Add(new Vector3(a.x, Base, a.y)); v.Add(new Vector3(b.x, Base, b.y)); v.Add(new Vector3(b.x, e.alto, b.y)); v.Add(new Vector3(a.x, e.alto, a.y));
            uv.Add(new Vector2(recorrido, Base) * 0.5f); uv.Add(new Vector2(recorrido + largo, Base) * 0.5f); uv.Add(new Vector2(recorrido + largo, e.alto) * 0.5f); uv.Add(new Vector2(recorrido, e.alto) * 0.5f);
            recorrido += largo;
            // a -> b antihorario visto desde arriba: el frente mira hacia afuera con este orden.
            muros.AddRange(new[] { k, k + 3, k + 2, k, k + 2, k + 1 });
        }
        int t0 = v.Count;
        foreach (var q in p) { v.Add(new Vector3(q.x, e.alto, q.y)); uv.Add(q * 0.25f); }
        for (int i = 1; i + 1 < p.Length; i++) techo.AddRange(new[] { t0, t0 + i + 1, t0 + i });
        var mesh = new Mesh { name = e.nombre };
        mesh.SetVertices(v); mesh.SetUVs(0, uv);
        mesh.subMeshCount = 2; mesh.SetTriangles(muros, 0); mesh.SetTriangles(techo, 1);
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
        // Si alguna cara quedó al revés, la normal del muro apunta para adentro: dar vuelta todo.
        var centro = new Vector3(p.Average(q => q.x), e.alto / 2, p.Average(q => q.y));
        var n = mesh.normals;
        if (Vector3.Dot(n[0], (v[0] + v[1]) / 2 - centro) < 0)
        {
            for (int s = 0; s < 2; s++) { var t = mesh.GetTriangles(s); for (int i = 0; i < t.Length; i += 3) { var x = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = x; } mesh.SetTriangles(t, s); }
            mesh.RecalculateNormals(); mesh.RecalculateTangents();
        }
        return mesh;
    }

    [MenuItem("Riftwalker/Universidad/Edificios que faltan (sin detalle)")]
    public static void Aplicar()
    {
        var escena = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (escena.path != "Assets/Scenes/universidadMAPA.unity" || EditorApplication.isPlaying) throw new InvalidOperationException("Abrir universidadMAPA fuera de Play.");
        var muro = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Uni/Uni_LadrilloChico.mat");
        var techo = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Uni/Uni_Techo.mat");
        if (muro == null || techo == null) throw new InvalidOperationException("Faltan Uni_LadrilloChico o Uni_Techo.");

        Undo.IncrementCurrentGroup(); int grupo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Edificios que faltan");
        var vieja = escena.GetRootGameObjects().FirstOrDefault(g => g.name == Raiz);
        if (vieja != null) Undo.DestroyObjectImmediate(vieja);
        if (AssetDatabase.LoadMainAssetAtPath(MeshPath) != null) AssetDatabase.DeleteAsset(MeshPath);
        var contenedor = new Mesh { name = "EdificiosSinDetalle" };
        AssetDatabase.CreateAsset(contenedor, MeshPath);

        var raiz = new GameObject(Raiz);
        Undo.RegisterCreatedObjectUndo(raiz, "Edificios que faltan");
        var edificios = Edificios;
        foreach (var e in edificios)
        {
            var mesh = Prisma(e);
            AssetDatabase.AddObjectToAsset(mesh, contenedor);
            var go = new GameObject(e.nombre);
            go.transform.SetParent(raiz.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { muro, techo };
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)~0);
        }
        GameObjectUtility.SetStaticEditorFlags(raiz, (StaticEditorFlags)~0);

        // Árboles, faroles y muebles que quedaban adentro de algún edificio.
        var sacados = new List<string>();
        foreach (var grupoNombre in new[] { "Ambiente_Vegetacion", "Ambiente_Faroles", "Ambiente_Mobiliario" })
        {
            var g = GameObject.Find(grupoNombre);
            if (g == null) continue;
            // En vegetación cada árbol cuelga de un grupo (Arboles_Campus, etc.); faroles y muebles cuelgan directo.
            bool vegetacion = grupoNombre == "Ambiente_Vegetacion";
            var candidatos = g.GetComponentsInChildren<Transform>(true)
                .Where(t => vegetacion ? t.parent != null && t.parent.parent == g.transform : t.parent == g.transform)
                .ToList();
            foreach (var t in candidatos)
            {
                if (t == null) continue;
                var q = new Vector2(t.position.x, t.position.z);
                var e = edificios.FirstOrDefault(x => Dentro(x.planta, q, 1.0f));
                if (e == null) continue;
                sacados.Add($"{t.name} ({e.nombre})");
                Undo.DestroyObjectImmediate(t.gameObject);
            }
        }
        EditorSceneManager.MarkSceneDirty(escena);
        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(grupo);
        Debug.Log($"Edificios sin detalle: {edificios.Length} volúmenes. Saqué {sacados.Count}: {string.Join(", ", sacados)}");
    }
}
