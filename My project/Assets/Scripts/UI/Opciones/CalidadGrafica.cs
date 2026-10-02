using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Diferencias visibles entre los niveles de calidad (US 153, CA3). Los niveles Baja, Media y Alta del proyecto usan
// el mismo asset de URP, así que al cambiar de nivel casi no se notaba. Acá se ajusta, según el nivel elegido:
// - la escala de dibujado (Baja dibuja a menos resolución: se ve más pixelado y rinde más),
// - la distancia de las sombras y el suavizado de bordes (MSAA),
// - el detalle de las texturas, el filtro anisotrópico y la distancia de los modelos detallados (LOD).
// Los valores del asset se cambian en memoria; en el editor se devuelven al salir del Play para no ensuciar el asset.
public static class CalidadGrafica
{
    private struct Nivel
    {
        public float escala, sombras, lod;
        public int msaa, texturas; // texturas: 0 = completas, 1 = mitad, 2 = un cuarto
        public AnisotropicFiltering anisotropico;
    }

    private static readonly Nivel Baja = new Nivel { escala = 0.6f, sombras = 15f, lod = 0.4f, msaa = 1, texturas = 2, anisotropico = AnisotropicFiltering.Disable };
    private static readonly Nivel Media = new Nivel { escala = 0.85f, sombras = 40f, lod = 1.2f, msaa = 2, texturas = 0, anisotropico = AnisotropicFiltering.Enable };
    private static readonly Nivel Alta = new Nivel { escala = 1f, sombras = 90f, lod = 2f, msaa = 4, texturas = 0, anisotropico = AnisotropicFiltering.ForceEnable };

    private static UniversalRenderPipelineAsset asset;
    private static float escalaOriginal, sombrasOriginal;
    private static int msaaOriginal;

    /// <summary>Lo llama GraficosUIController después de elegir el nivel de calidad ("Baja", "Media" o "Alta").</summary>
    public static void Aplicar(string nombre)
    {
        Nivel nivel = nombre == "Baja" ? Baja : nombre == "Alta" ? Alta : Media;

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
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= AlSalirDelPlay;
            UnityEditor.EditorApplication.playModeStateChanged += AlSalirDelPlay;
#endif
        }
        asset.renderScale = nivel.escala;
        asset.shadowDistance = nivel.sombras;
        asset.msaaSampleCount = nivel.msaa;
        Debug.Log($"Calidad gráfica: {nombre} aplicada sobre {asset.name} (escala {asset.renderScale}, sombras hasta " +
                  $"{asset.shadowDistance} m, MSAA x{asset.msaaSampleCount}, texturas a 1/{1 << nivel.texturas}).");
    }

    private static void Restaurar()
    {
        if (asset == null) return;
        asset.renderScale = escalaOriginal;
        asset.shadowDistance = sombrasOriginal;
        asset.msaaSampleCount = msaaOriginal;
        asset = null;
    }

#if UNITY_EDITOR
    private static void AlSalirDelPlay(UnityEditor.PlayModeStateChange cambio)
    {
        if (cambio == UnityEditor.PlayModeStateChange.ExitingPlayMode) Restaurar();
    }
#endif
}
