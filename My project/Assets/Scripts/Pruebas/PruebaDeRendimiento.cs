#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// US 241 (rendimiento del Mapa 1): prueba para saber qué es lo que más cuesta dibujar en esta computadora. Con F2 en
// una partida (solo en el editor o en una versión de desarrollo), quieto y mirando hacia donde más se ve del mapa:
// apaga de a una cosa (la vegetación, las sombras, las luces, los efectos, el postprocesado...), mide unos segundos
// cada una y compara con todo prendido. Lo que más baja el tiempo por cuadro es lo que conviene optimizar.
// El resultado se ve en pantalla y queda en Logs/PruebaDeRendimiento.txt (al lado de Assets; en una versión, en la
// carpeta de datos del juego). Al terminar todo vuelve a como estaba.
public class PruebaDeRendimiento : MonoBehaviour
{
    private const float Asentar = 1.5f, Medir = 4f;

    private static PruebaDeRendimiento unica;

    private bool corriendo;
    private string estado = "";
    private readonly StringBuilder informe = new StringBuilder();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Crear()
    {
        if (unica != null) return;
        var go = new GameObject("Prueba de rendimiento") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(go);
        unica = go.AddComponent<PruebaDeRendimiento>();
    }

    private void Update()
    {
        if (!corriendo && Input.GetKeyDown(KeyCode.F2)) StartCoroutine(Correr());
    }

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(estado)) return;
        var estilo = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 15, richText = false };
        estilo.normal.textColor = Color.white;
        GUI.Box(new Rect(20, 60, 760, 30 + 19 * estado.Split('\n').Length), estado, estilo);
    }

    private struct Medida
    {
        public float cuadro, cpu, render, gpu;
        public long lotes, setPass, triangulos, sombras;
    }

    private struct Paso
    {
        public string nombre;
        public System.Func<System.Action> apagar; // apaga algo y devuelve cómo volver a prenderlo
    }

    private IEnumerator Correr()
    {
        corriendo = true;
        int vsync = QualitySettings.vSyncCount, objetivo = Application.targetFrameRate;
        QualitySettings.vSyncCount = 0;      // sin tope: se mide lo que de verdad tarda
        Application.targetFrameRate = -1;

        var pasos = new List<Paso>
        {
            new Paso { nombre = "Todo prendido", apagar = () => null },
            new Paso { nombre = "Sin árboles ni pasto", apagar = SinVegetacion },
            new Paso { nombre = "Sin pasto (árboles sí)", apagar = SinPasto },
            new Paso { nombre = "Sin sombras", apagar = SinSombras },
            new Paso { nombre = "Sin luces chicas (vallas, puertas)", apagar = SinLucesChicas },
            new Paso { nombre = "Sin partículas, naves ni viento", apagar = SinEfectos },
            new Paso { nombre = "Sin postprocesado", apagar = SinPostprocesado },
            new Paso { nombre = "Sin el suelo del terreno", apagar = SinSuelo },
            new Paso { nombre = "Sin el resto del mapa (edificios, rocas...)", apagar = SinMapa },
            new Paso { nombre = "A media resolución", apagar = MediaResolucion },
        };

        informe.Clear();
        informe.AppendLine($"Prueba de rendimiento, {System.DateTime.Now:yyyy-MM-dd HH:mm}, escena {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        informe.AppendLine($"{SystemInfo.processorType} ({SystemInfo.processorCount} hilos), {SystemInfo.graphicsDeviceName} " +
                           $"({SystemInfo.graphicsMemorySize} MB, {SystemInfo.graphicsDeviceType}), RAM {SystemInfo.systemMemorySize} MB");
        informe.AppendLine($"Pantalla {Screen.width}x{Screen.height}, calidad {QualitySettings.names[QualitySettings.GetQualityLevel()]}, " +
                           (Application.isEditor ? "en el editor" : "versión de desarrollo"));
        Luces();
        informe.AppendLine();
        informe.AppendLine("Paso                                       | cuadro ms |  FPS | CPU ms | render ms | GPU ms | lotes | SetPass | triáng. (miles) | sombras");

        Medida base_ = default;
        for (int i = 0; i < pasos.Count; i++)
        {
            System.Action volver = pasos[i].apagar();
            estado = $"Prueba de rendimiento ({i + 1}/{pasos.Count}): {pasos[i].nombre}...\nQuedate quieto, sin mover el mouse.";
            yield return new WaitForSecondsRealtime(Asentar);
            Medida m = default;
            yield return Medir_(r => m = r);
            volver?.Invoke();
            if (i == 0) base_ = m;
            string ahorro = i == 0 ? "" : $"   ({base_.cuadro - m.cuadro:+0.0;-0.0} ms)";
            informe.AppendLine($"{pasos[i].nombre,-42} | {m.cuadro,9:0.0} | {1000f / Mathf.Max(0.01f, m.cuadro),4:0} | {m.cpu,6:0.0} | " +
                               $"{m.render,9:0.0} | {m.gpu,6:0.0} | {m.lotes,5} | {m.setPass,7} | {m.triangulos / 1000,15} | {m.sombras,7}{ahorro}");
        }

        QualitySettings.vSyncCount = vsync;
        Application.targetFrameRate = objetivo;
        string ruta = Ruta();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            File.AppendAllText(ruta, informe + "\n\n");
        }
        catch (System.Exception e) { Debug.LogWarning("Prueba de rendimiento: no se pudo guardar " + ruta + ": " + e.Message); }
        Debug.Log(informe.ToString());
        estado = informe + "\nGuardado en " + ruta + "  (F2 de nuevo para repetir)";
        corriendo = false;
        yield return new WaitForSecondsRealtime(25f);
        if (!corriendo) estado = "";
    }

    // Qué luces hay prendidas y cuáles proyectan sombra (las que no son la del sol cuestan mucho: 6 vistas cada una).
    private void Luces()
    {
        var porTipo = new Dictionary<string, int>();
        var conSombra = new List<string>();
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!l.enabled) continue;
            string clave = $"{l.type} {l.lightmapBakeType}{(l.bakingOutput.isBaked ? " (horneada)" : "")}";
            porTipo[clave] = porTipo.TryGetValue(clave, out int n) ? n + 1 : 1;
            if (l.shadows != LightShadows.None) conSombra.Add($"  {Camino(l.transform)}: {l.type}, sombra {l.shadows}, alcance {l.range:0.#}, {l.lightmapBakeType}");
        }
        informe.Append("Luces prendidas:");
        foreach (var par in porTipo) informe.Append($" {par.Value} {par.Key};");
        informe.AppendLine();
        informe.AppendLine($"Con sombra ({conSombra.Count}):");
        for (int i = 0; i < conSombra.Count && i < 40; i++) informe.AppendLine(conSombra[i]);
    }

    private static string Camino(Transform t)
    {
        string c = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) c = p.name + "/" + c;
        return c;
    }

    private static string Ruta()
    {
        string carpeta = Application.isEditor ? Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs") : Application.persistentDataPath;
        return Path.Combine(carpeta, "PruebaDeRendimiento.txt");
    }

    private static IEnumerator Medir_(System.Action<Medida> listo)
    {
        using var cpu = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "CPU Main Thread Frame Time", 600);
        using var render = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "CPU Render Thread Frame Time", 600);
        using var gpu = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GPU Frame Time", 600);
        using var lotes = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
        using var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        using var triangulos = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        using var sombras = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count");

        double sumaCpu = 0, sumaRender = 0, sumaGpu = 0;
        long sumaLotes = 0, sumaSetPass = 0, sumaTri = 0, sumaSombras = 0;
        int cuadros = 0, conGpu = 0;
        float inicio = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - inicio < Medir)
        {
            yield return null;
            FrameTimingManager.CaptureFrameTimings();
            cuadros++;
            sumaCpu += cpu.LastValue * 1e-6;
            sumaRender += render.LastValue * 1e-6;
            if (gpu.LastValue > 0) { sumaGpu += gpu.LastValue * 1e-6; conGpu++; }
            sumaLotes += lotes.LastValue;
            sumaSetPass += setPass.LastValue;
            sumaTri += triangulos.LastValue;
            sumaSombras += sombras.LastValue;
        }
        float total = (Time.realtimeSinceStartup - inicio) * 1000f;
        int n = Mathf.Max(1, cuadros);
        listo(new Medida
        {
            cuadro = total / n,
            cpu = (float)(sumaCpu / n),
            render = (float)(sumaRender / n),
            gpu = conGpu > 0 ? (float)(sumaGpu / conGpu) : 0f,
            lotes = sumaLotes / n,
            setPass = sumaSetPass / n,
            triangulos = sumaTri / n,
            sombras = sumaSombras / n,
        });
    }

    // ---- Lo que se apaga en cada paso ----

    private static System.Action SinVegetacion()
    {
        var antes = new List<(Terrain, bool)>();
        foreach (Terrain t in Terrain.activeTerrains) { antes.Add((t, t.drawTreesAndFoliage)); t.drawTreesAndFoliage = false; }
        return () => { foreach (var (t, v) in antes) if (t != null) t.drawTreesAndFoliage = v; };
    }

    private static System.Action SinPasto()
    {
        var antes = new List<(Terrain, float)>();
        foreach (Terrain t in Terrain.activeTerrains) { antes.Add((t, t.detailObjectDensity)); t.detailObjectDensity = 0f; }
        return () => { foreach (var (t, v) in antes) if (t != null) t.detailObjectDensity = v; };
    }

    private static System.Action SinSuelo()
    {
        var antes = new List<(Terrain, bool)>();
        foreach (Terrain t in Terrain.activeTerrains) { antes.Add((t, t.drawHeightmap)); t.drawHeightmap = false; }
        return () => { foreach (var (t, v) in antes) if (t != null) t.drawHeightmap = v; };
    }

    private static System.Action SinSombras()
    {
        var antes = new List<(Light, LightShadows)>();
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (l.shadows != LightShadows.None) { antes.Add((l, l.shadows)); l.shadows = LightShadows.None; }
        return () => { foreach (var (l, v) in antes) if (l != null) l.shadows = v; };
    }

    private static System.Action SinLucesChicas()
    {
        var antes = new List<Light>();
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (l.type != LightType.Directional && l.enabled) { antes.Add(l); l.enabled = false; }
        return () => { foreach (Light l in antes) if (l != null) l.enabled = true; };
    }

    private static System.Action SinEfectos()
    {
        var antes = new List<GameObject>();
        foreach (ParticleSystem p in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (p.transform.parent == null || p.transform.parent.GetComponentInParent<ParticleSystem>() == null)
                if (p.gameObject.activeSelf) { antes.Add(p.gameObject); p.gameObject.SetActive(false); }
        foreach (NavesDePaso n in Object.FindObjectsByType<NavesDePaso>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (n.gameObject.activeSelf) { antes.Add(n.gameObject); n.gameObject.SetActive(false); }
        return () => { foreach (GameObject g in antes) if (g != null) g.SetActive(true); };
    }

    private static System.Action SinPostprocesado()
    {
        var antes = new List<UniversalAdditionalCameraData>();
        foreach (Camera c in Camera.allCameras)
            if (c.TryGetComponent(out UniversalAdditionalCameraData d) && d.renderPostProcessing) { antes.Add(d); d.renderPostProcessing = false; }
        return () => { foreach (var d in antes) if (d != null) d.renderPostProcessing = true; };
    }

    // Todo lo que se dibuja con MeshRenderer y no es de un jugador (los personajes, sus armas y la cámara quedan).
    private static System.Action SinMapa()
    {
        var antes = new List<Renderer>();
        foreach (MeshRenderer r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!r.enabled || r.GetComponentInParent<CharacterController>() != null) continue;
            antes.Add(r);
            r.enabled = false;
        }
        return () => { foreach (Renderer r in antes) if (r != null) r.enabled = true; };
    }

    private static System.Action MediaResolucion()
    {
        var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (asset == null) return null;
        float antes = asset.renderScale;
        asset.renderScale = antes * 0.5f;
        return () => { if (asset != null) asset.renderScale = antes; };
    }
}
#endif
