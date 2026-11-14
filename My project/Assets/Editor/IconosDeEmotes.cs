using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// F37 (Emotes coleccionables): los íconos de los bailes, generados. Pone al primer personaje (CharacterRoster) en un
// momento del baile (Emote.momentoIcono, de 0 a 1), lo dibuja como silueta blanca y la pone en un círculo del color de
// su rareza (oscuro, con el borde vivo). Guarda cada ícono en Art/Emotes/Iconos (<estado>.png, como sprite) y se lo
// asigna en el catálogo. Si un ícono no representa bien al baile, se cambia su momentoIcono en el catálogo
// (Resources/Emotes) y se vuelve a generar. Riftwalker > Emotes > Generar íconos.
public static class IconosDeEmotes
{
    private const string Carpeta = "Assets/Art/Emotes/Iconos";
    private const int Captura = 512, Lado = 256;

    [MenuItem("Riftwalker/Emotes/Generar íconos")]
    public static void Generar()
    {
        CatalogoDeEmotes catalogo = CatalogoDeEmotes.Actual;
        CharacterData pj = CharacterRoster.All.Count > 0 ? CharacterRoster.All[0] : null;
        if (pj == null || pj.modelo == null) { Debug.LogWarning("Íconos de emotes: no hay personajes."); return; }
        if (!AssetDatabase.IsValidFolder("Assets/Art/Emotes")) AssetDatabase.CreateFolder("Assets/Art", "Emotes");
        if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/Art/Emotes", "Iconos");
        int hechos = 0;
        try
        {
            for (int i = 0; i < catalogo.emotes.Count; i++)
            {
                CatalogoDeEmotes.Emote e = catalogo.emotes[i];
                if (e == null || e.clip == null) continue;
                EditorUtility.DisplayProgressBar("Íconos de emotes", e.nombre, (float)i / catalogo.emotes.Count);
                Texture2D silueta = Silueta(pj.modelo, e.clip, e.momentoIcono);
                if (silueta == null) continue;
                Texture2D icono = Componer(silueta, PerfilUI.ColorRareza(e.rareza));
                Object.DestroyImmediate(silueta);
                string ruta = Carpeta + "/" + e.estado + ".png";
                File.WriteAllBytes(ruta, icono.EncodeToPNG());
                Object.DestroyImmediate(icono);
                AssetDatabase.ImportAsset(ruta);
                var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.SaveAndReimport();
                e.icono = AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
                hechos++;
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        EditorUtility.SetDirty(catalogo);
        AssetDatabase.SaveAssets();
        Debug.Log("Íconos de emotes: " + hechos + " generados en " + Carpeta + ".");
    }

    // El personaje en ese momento del baile, en blanco sobre negro (de frente, centrado).
    private static Texture2D Silueta(GameObject modelo, AnimationClip clip, float momento)
    {
        GameObject pj = Object.Instantiate(modelo);
        pj.hideFlags = HideFlags.HideAndDontSave;
        var piezas = new System.Collections.Generic.List<GameObject>();
        var blanco = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
        blanco.SetColor("_BaseColor", Color.white);
        blanco.SetColor("_Color", Color.white);
        var util = new PreviewRenderUtility();
        try
        {
            pj.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Animator animador = null;
            foreach (Animator a in pj.GetComponentsInChildren<Animator>(true)) if (a.isHuman) { animador = a; break; }
            if (animador == null) return null;
            animador.runtimeAnimatorController = null;
            animador.applyRootMotion = false;
            // La pose de ese momento.
            PlayableGraph grafo = PlayableGraph.Create("Ícono");
            var salida = AnimationPlayableOutput.Create(grafo, "Salida", animador);
            var reproducir = AnimationClipPlayable.Create(grafo, clip);
            salida.SetSourcePlayable(reproducir);
            reproducir.SetTime(Mathf.Clamp01(momento) * clip.length);
            grafo.Evaluate();
            // Cada malla, fija en esa pose (se dibuja en otra escena, la de la vista previa).
            Bounds limites = default;
            bool hay = false;
            foreach (Renderer r in pj.GetComponentsInChildren<Renderer>())
            {
                Mesh malla = null;
                if (r is SkinnedMeshRenderer s) { malla = new Mesh(); s.BakeMesh(malla, true); }
                else if (r.GetComponent<MeshFilter>() is MeshFilter f) malla = f.sharedMesh;
                if (malla == null) continue;
                var go = new GameObject("Pieza") { hideFlags = HideFlags.HideAndDontSave };
                go.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
                if (!(r is SkinnedMeshRenderer)) go.transform.localScale = r.transform.lossyScale;
                go.AddComponent<MeshFilter>().sharedMesh = malla;
                var mr = go.AddComponent<MeshRenderer>();
                var mats = new Material[Mathf.Max(1, malla.subMeshCount)];
                for (int k = 0; k < mats.Length; k++) mats[k] = blanco;
                mr.sharedMaterials = mats;
                util.AddSingleGO(go);
                piezas.Add(go);
                if (!hay) { limites = mr.bounds; hay = true; } else limites.Encapsulate(mr.bounds);
            }
            grafo.Destroy();
            if (!hay) return null;

            Camera cam = util.camera;
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(limites.extents.y, limites.extents.x) * 1.1f;
            cam.transform.SetPositionAndRotation(limites.center + Vector3.forward * 10f, Quaternion.LookRotation(Vector3.back));
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 30f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            util.lights[0].intensity = 0f;
            util.lights[1].intensity = 0f;
            util.BeginStaticPreview(new Rect(0, 0, Captura, Captura));
            util.Render(true);
            return util.EndStaticPreview();
        }
        finally
        {
            util.Cleanup();
            foreach (GameObject go in piezas) Object.DestroyImmediate(go);
            Object.DestroyImmediate(pj);
            Object.DestroyImmediate(blanco);
        }
    }

    // El ícono: un círculo oscuro del color de la rareza, con el borde vivo, y la silueta blanca adentro (recortada a lo
    // que ocupa y centrada).
    private static Texture2D Componer(Texture2D silueta, Color rareza)
    {
        int w = silueta.width, h = silueta.height;
        Color[] px = silueta.GetPixels();
        int x0 = w, y0 = h, x1 = -1, y1 = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (px[y * w + x].grayscale > 0.5f) { x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); }
        var icono = new Texture2D(Lado, Lado, TextureFormat.RGBA32, false);
        var salida = new Color[Lado * Lado];
        float c = (Lado - 1) / 2f, radio = Lado * 0.48f, borde = Lado * 0.035f;
        float ancho = Mathf.Max(1, x1 - x0 + 1), alto = Mathf.Max(1, y1 - y0 + 1);
        float escala = Mathf.Min(Lado * 0.7f / ancho, Lado * 0.74f / alto);
        float cx = (x0 + x1) / 2f, cy = (y0 + y1) / 2f;
        Color fondo = Color.Lerp(new Color(0.05f, 0.06f, 0.08f), rareza, 0.35f);
        for (int y = 0; y < Lado; y++)
            for (int x = 0; x < Lado; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                Color col = new Color(0, 0, 0, 0);
                if (d <= radio)
                {
                    col = d > radio - borde ? rareza : fondo;
                    // La silueta (con un poco de suavizado: el promedio de la captura en ese lugar).
                    if (x1 >= 0 && d <= radio - borde)
                    {
                        float sx = cx + (x - c) / escala, sy = cy + (y - c) / escala;
                        float v = Muestra(px, w, h, sx, sy, 0.5f / escala);
                        col = Color.Lerp(col, Color.white, v);
                    }
                    col.a = Mathf.Clamp01(radio - d + 1f); // el borde del círculo, suave
                }
                salida[y * Lado + x] = col;
            }
        icono.SetPixels(salida);
        icono.Apply();
        return icono;
    }

    private static float Muestra(Color[] px, int w, int h, float x, float y, float r)
    {
        float suma = 0f;
        int n = 0;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int ix = Mathf.RoundToInt(x + dx * r), iy = Mathf.RoundToInt(y + dy * r);
                if (ix < 0 || iy < 0 || ix >= w || iy >= h) { n++; continue; }
                suma += px[iy * w + ix].grayscale > 0.5f ? 1f : 0f;
                n++;
            }
        return suma / n;
    }
}
