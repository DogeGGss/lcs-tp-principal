using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Diferencias visibles entre los niveles de calidad (US 153, CA3). Los niveles Baja, Media y Alta del proyecto usan
// el mismo asset de URP, así que al cambiar de nivel casi no se notaba. Acá se ajusta, según el nivel elegido:
// - la escala de dibujado (Baja dibuja a menos resolución: se ve más pixelado y rinde más),
// - la distancia de las sombras y el suavizado de bordes (MSAA),
// - el detalle de las texturas, el filtro anisotrópico y la distancia de los modelos detallados (LOD).
// US 241 (Mapa 1, rendimiento): también
// - las cascadas y la resolución de las sombras (Media usa 2 cascadas en vez de 4; Baja, 1 y a menos resolución),
// - la vegetación del terreno: hasta dónde se dibujan los árboles y el pasto, cuánto pasto hay y el detalle del suelo.
//   Nunca más de lo que tiene puesto la escena: el nivel solo recorta. Se aplica a cada escena que se carga.
// Los valores del asset se cambian en memoria; en el editor se devuelven al salir del Play para no ensuciar el asset.
public static class CalidadGrafica
{
    private struct Nivel
    {
        public float escala, sombras, lod;
        public int msaa, texturas; // texturas: 0 = completas, 1 = mitad, 2 = un cuarto
        public AnisotropicFiltering anisotropico;
        public int cascadas, resolucionSombras;
        public float arboles, pasto, densidadPasto, errorSuelo; // terreno: m, m, 0 a 1, píxeles
    }

    private static readonly Nivel Baja = new Nivel { escala = 0.6f, sombras = 15f, lod = 0.4f, msaa = 1, texturas = 2, anisotropico = AnisotropicFiltering.Disable,
        cascadas = 1, resolucionSombras = 1024, arboles = 150f, pasto = 30f, densidadPasto = 0.35f, errorSuelo = 10f };
    private static readonly Nivel Media = new Nivel { escala = 0.85f, sombras = 40f, lod = 1.2f, msaa = 2, texturas = 0, anisotropico = AnisotropicFiltering.Enable,
        cascadas = 2, resolucionSombras = 2048, arboles = 260f, pasto = 55f, densidadPasto = 0.6f, errorSuelo = 6f };
    private static readonly Nivel Alta = new Nivel { escala = 1f, sombras = 90f, lod = 2f, msaa = 4, texturas = 0, anisotropico = AnisotropicFiltering.ForceEnable,
        cascadas = 4, resolucionSombras = 2048, arboles = 400f, pasto = 90f, densidadPasto = 1f, errorSuelo = 4f };

    private static UniversalRenderPipelineAsset asset;
    private static float escalaOriginal, sombrasOriginal;
    private static int msaaOriginal, cascadasOriginal, resolucionOriginal;
    private static Nivel? elegido;

    // Lo que trae cada terreno de su escena (el nivel recorta a partir de esto, así se puede volver a subir).
    private struct Terreno { public float arboles, pasto, densidad, error; }
    private static readonly Dictionary<Terrain, Terreno> terrenos = new Dictionary<Terrain, Terreno>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AlArrancar()
    {
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;
    }

    private static void AlCargarEscena(Scene escena, LoadSceneMode modo)
    {
        // Entrando al Play directo en un mapa (sin pasar por el menú), se usa lo guardado igual.
        if (elegido == null) GraficosUIController.AplicarGuardado();
        else Terrenos(elegido.Value);
    }

    /// <summary>Lo llama GraficosUIController después de elegir el nivel de calidad ("Baja", "Media" o "Alta").</summary>
    public static void Aplicar(string nombre)
    {
        Nivel nivel = nombre == "Baja" ? Baja : nombre == "Alta" ? Alta : Media;
        elegido = nivel;
        Terrenos(nivel);

        QualitySettings.globalTextureMipmapLimit = nivel.texturas;
        QualitySettings.anisotropicFiltering = nivel.anisotropico;
        QualitySettings.lodBias = nivel.lod;

        var actual = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (actual == null)
        {
            Debug.LogWarning($"Calidad gráfica: se eligió {nombre}, pero el proyecto no está usando un asset de URP; solo cambian las texturas.");
            return;
        }
        if (actual != asset)
        {
            Restaurar(); // por si antes se había tocado otro asset
            asset = actual;
            escalaOriginal = asset.renderScale;
            sombrasOriginal = asset.shadowDistance;
            msaaOriginal = asset.msaaSampleCount;
            cascadasOriginal = asset.shadowCascadeCount;
            resolucionOriginal = asset.mainLightShadowmapResolution;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= AlSalirDelPlay;
            UnityEditor.EditorApplication.playModeStateChanged += AlSalirDelPlay;
#endif
        }
        asset.renderScale = nivel.escala;
        asset.shadowDistance = nivel.sombras;
        asset.msaaSampleCount = nivel.msaa;
        asset.shadowCascadeCount = nivel.cascadas;
        asset.mainLightShadowmapResolution = nivel.resolucionSombras;
        Debug.Log($"Calidad gráfica: {nombre} aplicada sobre {asset.name} (escala {asset.renderScale}, sombras hasta " +
                  $"{asset.shadowDistance} m en {asset.shadowCascadeCount} cascadas, MSAA x{asset.msaaSampleCount}, " +
                  $"texturas a 1/{1 << nivel.texturas}, árboles hasta {nivel.arboles} m, pasto hasta {nivel.pasto} m).");
    }

    // La vegetación y el suelo de los terrenos cargados.
    private static void Terrenos(Nivel nivel)
    {
        var sinTerreno = new List<Terrain>();
        foreach (Terrain t in terrenos.Keys) if (t == null) sinTerreno.Add(t);
        foreach (Terrain t in sinTerreno) terrenos.Remove(t);

        foreach (Terrain t in Terrain.activeTerrains)
        {
            if (!terrenos.TryGetValue(t, out Terreno original))
            {
                original = new Terreno { arboles = t.treeDistance, pasto = t.detailObjectDistance, densidad = t.detailObjectDensity, error = t.heightmapPixelError };
                terrenos[t] = original;
                // No se prende acá el suelo instanciado (drawInstanced): si la escena no lo tiene puesto, la build
                // descarta esa versión del shader del terreno y en la versión armada el suelo no se dibuja.
            }
            t.treeDistance = Mathf.Min(original.arboles, nivel.arboles);
            t.detailObjectDistance = Mathf.Min(original.pasto, nivel.pasto);
            t.detailObjectDensity = Mathf.Min(original.densidad, nivel.densidadPasto);
            t.heightmapPixelError = Mathf.Max(original.error, nivel.errorSuelo);
        }
    }

    private static void Restaurar()
    {
        if (asset == null) return;
        asset.renderScale = escalaOriginal;
        asset.shadowDistance = sombrasOriginal;
        asset.msaaSampleCount = msaaOriginal;
        asset.shadowCascadeCount = cascadasOriginal;
        asset.mainLightShadowmapResolution = resolucionOriginal;
        asset = null;
    }

#if UNITY_EDITOR
    private static void AlSalirDelPlay(UnityEditor.PlayModeStateChange cambio)
    {
        if (cambio != UnityEditor.PlayModeStateChange.ExitingPlayMode) return;
        Restaurar();
        elegido = null; // el próximo Play vuelve a aplicar todo (aunque no se recarguen los scripts)
        terrenos.Clear();
    }
#endif
}
