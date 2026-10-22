using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// F27 (Emotes), US 262: los bailes de Mixamo. Alcanza con poner el .fbx en Personajes/Emotes (bajado "Without Skin",
// 30 fps); el nombre del archivo es el del baile. Lo demás lo hace el editor solo:
// - lo importa como Humanoid (el avatar sale del mismo archivo), en loop y sin moverse de su lugar (el movimiento de la
//   cadera va en la pose), sin materiales;
// - en el Animator de los personajes (AnimatorControllerAlienSoldier) arma la capa "Emotes" (todo el cuerpo, peso 0: la
//   prende BaileEnTerceraPersona mientras baila) con un estado por baile;
// - lo agrega al final del catálogo (Resources/Emotes), donde se le pone el nombre que se muestra y el ícono.
// También desde el menú: Riftwalker > Emotes > Armar los bailes.
public class AnimacionesDeEmotes : AssetPostprocessor
{
    public const string Carpeta = "Assets/Personajes/Emotes/";
    private const string Controlador = "Assets/Player/Basic Shooter Pack/AnimatorControllerAlienSoldier.controller";
    private const string Catalogo = "Assets/Resources/Emotes.asset";

    private void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(Carpeta)) return;
        var importador = (ModelImporter)assetImporter;
        importador.materialImportMode = ModelImporterMaterialImportMode.None;
        importador.importCameras = false;
        importador.importLights = false;
        if (importador.animationType != ModelImporterAnimationType.Human)
        {
            importador.animationType = ModelImporterAnimationType.Human;
            importador.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        }
    }

    private void OnPreprocessAnimation()
    {
        if (!assetPath.StartsWith(Carpeta)) return;
        var importador = (ModelImporter)assetImporter;
        if (importador.clipAnimations != null && importador.clipAnimations.Length > 0) return; // ya tiene sus clips
        ModelImporterClipAnimation[] clips = importador.defaultClipAnimations;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            clip.name = Path.GetFileNameWithoutExtension(assetPath);
            clip.loopTime = true;
            clip.loopPose = true;
            // En su lugar: el giro, la altura y el paso de la cadera quedan en la pose (no mueven al personaje).
            clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = true;
            clip.keepOriginalOrientation = clip.keepOriginalPositionY = clip.keepOriginalPositionXZ = true;
        }
        importador.clipAnimations = clips;
    }

    private static void OnPostprocessAllAssets(string[] importados, string[] borrados, string[] movidos, string[] desde)
    {
        if (importados.Any(p => p.StartsWith(Carpeta) && p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)))
            EditorApplication.delayCall += Armar;
    }

    [InitializeOnLoadMethod]
    private static void AlCargar() => EditorApplication.delayCall += Armar;

    [MenuItem("Riftwalker/Emotes/Armar los bailes")]
    public static void Armar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var controlador = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controlador);
        if (controlador == null) return;
        var bailes = AssetDatabase.FindAssets("t:Model", new[] { Carpeta.TrimEnd('/') })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p)
            .Select(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__")))
            .Where(c => c != null)
            .ToList();
        if (bailes.Count == 0) return;

        bool cambio = false;
        AnimatorControllerLayer capa = controlador.layers.FirstOrDefault(c => c.name == BaileEnTerceraPersona.Capa);
        if (capa == null)
        {
            var maquina = new AnimatorStateMachine { name = BaileEnTerceraPersona.Capa, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(maquina, controlador);
            AnimatorState nada = maquina.AddState("Nada");
            nada.writeDefaultValues = false;
            maquina.defaultState = nada;
            controlador.AddLayer(new AnimatorControllerLayer
            {
                name = BaileEnTerceraPersona.Capa,
                defaultWeight = 0f, // apagada: la prende BaileEnTerceraPersona mientras baila
                blendingMode = AnimatorLayerBlendingMode.Override,
                stateMachine = maquina,
            });
            capa = controlador.layers.First(c => c.name == BaileEnTerceraPersona.Capa);
            cambio = true;
        }
        foreach (AnimationClip clip in bailes)
        {
            ChildAnimatorState hay = capa.stateMachine.states.FirstOrDefault(s => s.state.name == clip.name);
            if (hay.state != null)
            {
                if (hay.state.motion != clip) { hay.state.motion = clip; cambio = true; }
                continue;
            }
            AnimatorState estado = capa.stateMachine.AddState(clip.name);
            estado.motion = clip;
            estado.writeDefaultValues = false;
            cambio = true;
        }
        if (cambio) EditorUtility.SetDirty(controlador);

        // El catálogo: los bailes nuevos, al final (el orden es el número que viaja por la red).
        var catalogo = AssetDatabase.LoadAssetAtPath<CatalogoDeEmotes>(Catalogo);
        if (catalogo == null)
        {
            catalogo = ScriptableObject.CreateInstance<CatalogoDeEmotes>();
            AssetDatabase.CreateAsset(catalogo, Catalogo);
        }
        bool nuevos = false;
        foreach (AnimationClip clip in bailes)
            if (!catalogo.emotes.Any(e => e.estado == clip.name))
            {
                catalogo.emotes.Add(new CatalogoDeEmotes.Emote { estado = clip.name, nombre = clip.name });
                nuevos = true;
            }
        if (nuevos) EditorUtility.SetDirty(catalogo);
        if (cambio || nuevos)
        {
            AssetDatabase.SaveAssets();
            Debug.Log("Riftwalker: bailes armados (" + bailes.Count + "): " + string.Join(", ", bailes.Select(c => c.name)) + ".");
        }
    }
}
