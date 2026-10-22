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
//   la máscara BrazosGranada: el torso, la cabeza, los brazos y los dedos (las piernas siguen con caminar, correr,
//   agacharse...). La cadera no se mueve de su lugar: el paso adelante de la animación no se usa (no va "baked" en
//   la pose), así el torso se inclina y gira sobre las piernas sin despegarse de ellas.
//   La capa queda con peso 0: la prende LanzamientoDeGranada solo mientras lanza.
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
            CaderaQuieta(clip);
        }
        importador.clipAnimations = clips;
    }

    // El movimiento de la cadera (el paso adelante, agacharse, girar) no va en la pose: queda en la raíz, que no se usa.
    private static bool CaderaQuieta(ModelImporterClipAnimation clip)
    {
        bool cambia = clip.lockRootRotation || clip.lockRootHeightY || clip.lockRootPositionXZ;
        clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = false;
        clip.keepOriginalOrientation = clip.keepOriginalPositionY = clip.keepOriginalPositionXZ = true;
        return cambia;
    }

    private static bool EnLaMascara(AvatarMaskBodyPart parte) =>
        parte == AvatarMaskBodyPart.Body || parte == AvatarMaskBodyPart.Head ||
        parte == AvatarMaskBodyPart.LeftArm || parte == AvatarMaskBodyPart.RightArm ||
        parte == AvatarMaskBodyPart.LeftFingers || parte == AvatarMaskBodyPart.RightFingers;

    // Las versiones anteriores: el clip con la cadera en la pose y la máscara solo con los brazos.
    private static void Actualizar(AvatarMask mascara)
    {
        if (AssetImporter.GetAtPath(Granada) is ModelImporter importador && importador.clipAnimations.Length > 0)
        {
            ModelImporterClipAnimation[] clips = importador.clipAnimations;
            bool cambia = false;
            foreach (ModelImporterClipAnimation clip in clips) cambia |= CaderaQuieta(clip);
            if (cambia)
            {
                importador.clipAnimations = clips;
                importador.SaveAndReimport();
            }
        }
        if (mascara == null) return;
        bool cambiaMascara = false;
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
        {
            var parte = (AvatarMaskBodyPart)i;
            if (mascara.GetHumanoidBodyPartActive(parte) == EnLaMascara(parte)) continue;
            mascara.SetHumanoidBodyPartActive(parte, EnLaMascara(parte));
            cambiaMascara = true;
        }
        if (cambiaMascara)
        {
            EditorUtility.SetDirty(mascara);
            AssetDatabase.SaveAssets();
        }
    }

    private static void OnPostprocessAllAssets(string[] importados, string[] borrados, string[] movidos, string[] desde)
    {
        if (importados.Contains(Granada) || importados.Contains(Controlador)) EditorApplication.delayCall += ArmarCapaDeGranada;
    }

    [InitializeOnLoadMethod]
    private static void AlCargar() => EditorApplication.delayCall += ArmarCapaDeGranada;

    // Con la granada en la mano: la pose del principio de la animación (la granada a la altura del pecho), quieta.
    private static void Sostener(AnimatorStateMachine maquina, AnimationClip clip)
    {
        AnimatorState sostener = maquina.AddState(LanzamientoDeGranada.Sostener);
        sostener.motion = clip;
        sostener.speed = 0f;
        sostener.writeDefaultValues = false;
    }

    [MenuItem("Riftwalker/Armar la capa de la granada (tercera persona)")]
    public static void ArmarCapaDeGranada()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var controlador = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controlador);
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(Granada).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (controlador == null || clip == null) return;
        Actualizar(AssetDatabase.LoadAssetAtPath<AvatarMask>(Mascara));

        AnimatorControllerLayer capa = controlador.layers.FirstOrDefault(c => c.name == LanzamientoDeGranada.Capa);
        if (capa != null)
        {
            // Ya está: que tenga la velocidad de ahora y el estado para sostenerla (versiones anteriores).
            bool cambia = false;
            foreach (ChildAnimatorState s in capa.stateMachine.states)
                if (s.state.name == LanzamientoDeGranada.Estado && !Mathf.Approximately(s.state.speed, LanzamientoDeGranada.Velocidad))
                {
                    s.state.speed = LanzamientoDeGranada.Velocidad;
                    cambia = true;
                }
            if (!capa.stateMachine.states.Any(s => s.state.name == LanzamientoDeGranada.Sostener))
            {
                Sostener(capa.stateMachine, clip);
                cambia = true;
            }
            if (cambia)
            {
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
                mascara.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, EnLaMascara((AvatarMaskBodyPart)i));
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
        Sostener(maquina, clip);
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
