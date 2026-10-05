using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Applies to both Build Profiles / Build And Run and Riftwalker > Publicar versión.
// Editor-only: no scene objects or runtime dependencies are required.
public sealed class RiftwalkerBuildBranding : IPreprocessBuildWithReport, IProcessSceneWithReport
{
    internal const string IconPath = "Assets/UI/Branding/RiftwalkerIcon.png";
    internal const string LogoPath = "Assets/UI/Branding/RiftwalkerLogo.png";
    public int callbackOrder => -1000;

    public void OnProcessScene(UnityEngine.SceneManagement.Scene scene, BuildReport report)
    {
        if (report == null || scene.path != "Assets/Scenes/MenuPrincipal.unity"
            || (report.summary.platform != BuildTarget.StandaloneWindows64
                && report.summary.platform != BuildTarget.StandaloneWindows)) return;
        var roots = scene.GetRootGameObjects().Where(root => root.activeSelf).ToArray();
        var introObject = new GameObject("Riftwalker opening");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(introObject, scene);
        var intro = introObject.AddComponent<RiftwalkerIntro>();
        intro.ConfigureForBuild(AssetDatabase.LoadAssetAtPath<Texture2D>(LogoPath), roots);
        foreach (var root in roots) root.SetActive(false);
        // Only the temporary scene being built is changed, never the source .unity file.
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform == BuildTarget.StandaloneWindows64
            || report.summary.platform == BuildTarget.StandaloneWindows)
            Apply();
    }

    [MenuItem("Riftwalker/Identidad visual/Aplicar ícono y logo de inicio")]
    public static void Apply()
    {
        Import(IconPath, false);
        Import(LogoPath, true);
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        var logo = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
        if (icon == null || logo == null)
            throw new BuildFailedException("Falta un recurso de identidad visual de Riftwalker en Assets/UI/Branding.");

        var target = NamedBuildTarget.Standalone;
        var sizes = PlayerSettings.GetIconSizes(target, IconKind.Application);
        if (sizes.Length == 0)
            throw new BuildFailedException("Unity no informó tamaños de ícono para Standalone. Revisá el soporte de build de Windows.");
        PlayerSettings.SetIcons(target, sizes.Select(_ => icon).ToArray(), IconKind.Application);
        // Default icon is also used as fallback by standalone players.
        var defaultSizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Unknown, IconKind.Application);
        if (defaultSizes.Length > 0)
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, defaultSizes.Select(_ => icon).ToArray(), IconKind.Application);

        // The custom runtime opening owns the animation; disable the native splash.
        PlayerSettings.SplashScreen.showUnityLogo = false;
        PlayerSettings.SplashScreen.logos = new PlayerSettings.SplashScreenLogo[0];
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.SplashScreen.background = null;
        PlayerSettings.SplashScreen.backgroundPortrait = null;
        PlayerSettings.SplashScreen.backgroundColor = Color.black;
        PlayerSettings.SplashScreen.blurBackgroundImage = false;
        PlayerSettings.SplashScreen.overlayOpacity = 0f;
        PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Static;
        PlayerSettings.SplashScreen.animationBackgroundZoom = 0f;
        PlayerSettings.SplashScreen.animationLogoZoom = 0f;
        AssetDatabase.SaveAssets();
        Debug.Log("Riftwalker: ícono de Windows y logo de inicio configurados para la build.");
    }

    private static void Import(string path, bool sprite)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            importer = AssetImporter.GetAtPath(path) as TextureImporter;
        }
        if (importer == null) throw new BuildFailedException("No se pudo importar " + path);
        if (RiftwalkerBrandingImporter.Configure(importer, sprite)) importer.SaveAndReimport();
    }
}

public sealed class RiftwalkerBrandingImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (assetPath == RiftwalkerBuildBranding.IconPath || assetPath == RiftwalkerBuildBranding.LogoPath)
            Configure((TextureImporter)assetImporter, assetPath != RiftwalkerBuildBranding.IconPath);
    }

    internal static bool Configure(TextureImporter importer, bool sprite)
    {
        var type = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
        bool changed = importer.textureType != type || importer.alphaSource != TextureImporterAlphaSource.FromInput
            || !importer.alphaIsTransparency || importer.mipmapEnabled || importer.maxTextureSize != 4096
            || importer.textureCompression != TextureImporterCompression.Uncompressed
            || importer.npotScale != TextureImporterNPOTScale.None || importer.wrapMode != TextureWrapMode.Clamp
            || !importer.sRGBTexture || importer.filterMode != FilterMode.Bilinear;
        importer.textureType = type;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.sRGBTexture = true;
        if (sprite)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            changed |= importer.spriteImportMode != SpriteImportMode.Single || settings.spriteMeshType != SpriteMeshType.FullRect;
            importer.spriteImportMode = SpriteImportMode.Single;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }
        return changed;
    }
}
