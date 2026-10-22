using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Las animaciones de Mixamo que ven los demás jugadores (F26; US 259: lanzar la granada).
// - Las que se ponen en Personajes/Animaciones se importan como Humanoid (el avatar sale del mismo archivo) y el clip
//   se llama como el archivo, sin repetirse.
// - La capa "Granada" del Animator de los personajes (AnimatorControllerAlienSoldier) se arma sola en cuanto está la
//   animación LanzarGranada: el estado LanzarGranada (a la velocidad de LanzamientoDeGranada) y un estado vacío, con
//   la máscara BrazosGranada (solo brazos y dedos: el cuerpo y las piernas siguen con caminar, correr, agacharse...).
//   La capa queda con peso 0: la prende LanzamientoDeGranada solo mientras lanza.
// Para que también gire el torso al lanzar, tildar "Body" en la máscara BrazosGranada.
public class AnimacionesTerceraPersona : AssetPostprocessor
{
    public const string Carpeta = "Assets/Personajes/Animaciones/";
    private const string Granada = Carpeta + "LanzarGranada.fbx";
    private const string Mascara = Carpeta + "BrazosGranada.mask";
    private const string Controlador = "Assets/Player/Basic Shooter Pack/AnimatorControllerAlienSoldier.controller";

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
            clip.loopTime = false;
            clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = true;
            clip.keepOriginalOrientation = clip.keepOriginalPositionY = clip.keepOriginalPositionXZ = true;
        }
        importador.clipAnimations = clips;
    }

    private static void OnPostprocessAllAssets(string[] importados, string[] borrados, string[] movidos, string[] desde)
    {
        if (importados.Contains(Granada) || importados.Contains(Controlador)) EditorApplication.delayCall += ArmarCapaDeGranada;
    }

    [InitializeOnLoadMethod]
    private static void AlCargar() => EditorApplication.delayCall += ArmarCapaDeGranada;

    [MenuItem("Riftwalker/Armar la capa de la granada (tercera persona)")]
    public static void ArmarCapaDeGranada()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var controlador = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controlador);
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(Granada).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (controlador == null || clip == null) return;

        AnimatorControllerLayer capa = controlador.layers.FirstOrDefault(c => c.name == LanzamientoDeGranada.Capa);
        if (capa != null)
        {
            // Ya está: que tenga la velocidad de ahora.
            foreach (ChildAnimatorState s in capa.stateMachine.states)
                if (s.state.name == LanzamientoDeGranada.Estado && !Mathf.Approximately(s.state.speed, LanzamientoDeGranada.Velocidad))
                {
                    s.state.speed = LanzamientoDeGranada.Velocidad;
                    EditorUtility.SetDirty(controlador);
                    AssetDatabase.SaveAssets();
                }
            return;
        }

        var mascara = AssetDatabase.LoadAssetAtPath<AvatarMask>(Mascara);
        if (mascara == null)
        {
            mascara = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var parte = (AvatarMaskBodyPart)i;
                mascara.SetHumanoidBodyPartActive(parte, parte == AvatarMaskBodyPart.LeftArm || parte == AvatarMaskBodyPart.RightArm ||
                                                         parte == AvatarMaskBodyPart.LeftFingers || parte == AvatarMaskBodyPart.RightFingers);
            }
            AssetDatabase.CreateAsset(mascara, Mascara);
        }

        var maquina = new AnimatorStateMachine { name = LanzamientoDeGranada.Capa, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(maquina, controlador);
        AnimatorState nada = maquina.AddState("Nada");
        nada.writeDefaultValues = false;
        AnimatorState lanzar = maquina.AddState(LanzamientoDeGranada.Estado);
        lanzar.motion = clip;
        lanzar.speed = LanzamientoDeGranada.Velocidad;
        lanzar.writeDefaultValues = false;
        maquina.defaultState = nada;
        controlador.AddLayer(new AnimatorControllerLayer
        {
            name = LanzamientoDeGranada.Capa,
            defaultWeight = 0f, // apagada: la prende LanzamientoDeGranada mientras lanza
            avatarMask = mascara,
            blendingMode = AnimatorLayerBlendingMode.Override,
            stateMachine = maquina,
        });
        EditorUtility.SetDirty(controlador);
        AssetDatabase.SaveAssets();
        Debug.Log("Riftwalker: se armó la capa \"" + LanzamientoDeGranada.Capa + "\" del Animator de los personajes (US 259).");
    }
}
