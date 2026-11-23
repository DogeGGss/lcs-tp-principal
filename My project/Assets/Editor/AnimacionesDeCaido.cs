using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// F31 (Invasión cooperativa), US 357, CA8: las animaciones de caído, de Mixamo, en Personajes/Caido: Caer, Arrastrarse
// y Levantarse (el nombre del archivo es el del estado). Lo demás lo hace el editor solo, como con los bailes
// (AnimacionesDeEmotes):
// - las importa como Humanoid (el avatar sale del mismo archivo), sin moverse de su lugar (el movimiento de la cadera
//   va en la pose) y sin materiales; la de arrastrarse, en loop;
// - en el Animator de los personajes (AnimatorControllerAlienSoldier) arma la capa "Caido" (todo el cuerpo, peso 0: la
//   prende CaidoEnTerceraPersona mientras está caído), con Caer, que sigue sola con Arrastrarse (quieta con el parámetro
//   Arrastre en 0), y Levantarse.
// También desde el menú: Riftwalker > Invasión > Armar las animaciones de caído. Para verlas: Riftwalker > Invasión >
// Muñeco de caído (MunecoDeCaido).
public class AnimacionesDeCaido : AssetPostprocessor
{
    public const string Carpeta = "Assets/Personajes/Caido/";
    private const string Controlador = "Assets/Player/Basic Shooter Pack/AnimatorControllerAlienSoldier.controller";
    // De caer a arrastrarse: empieza a mezclar antes del final de la de caer, durante medio segundo.
    private const float SaleDeCaer = 0.8f, MezclaCaerArrastrarse = 0.5f;

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

    // Subirlo hace que Unity vuelva a importar las animaciones con lo de abajo (versión 2: sin el avance en la pose).
    public override uint GetVersion() => 2;

    private void OnPreprocessAnimation()
    {
        if (!assetPath.StartsWith(Carpeta)) return;
        var importador = (ModelImporter)assetImporter;
        string nombre = Path.GetFileNameWithoutExtension(assetPath);
        bool tenia = importador.clipAnimations != null && importador.clipAnimations.Length > 0; // (respeta Start y End)
        ModelImporterClipAnimation[] clips = tenia ? importador.clipAnimations : importador.defaultClipAnimations;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            clip.name = nombre;
            clip.loopTime = clip.loopPose = nombre == CaidoEnTerceraPersona.Arrastrarse;
            // El giro y la altura de la cadera quedan en la pose. El avance (adelante y a los costados) no: sale como
            // movimiento del personaje, que no se aplica, así el cuerpo cae, se arrastra y se levanta siempre en el mismo
            // lugar. Con el avance en la pose, la de caer terminaba un metro más adelante de donde empieza la de
            // arrastrarse (y lo mismo con la de levantarse): se veía un salto.
            clip.lockRootRotation = clip.lockRootHeightY = true;
            clip.lockRootPositionXZ = false;
            clip.keepOriginalOrientation = clip.keepOriginalPositionY = true;
            clip.keepOriginalPositionXZ = false; // (el centro de masa, no el pie)
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

    private static AnimationClip Clip(string nombre)
    {
        string ruta = Carpeta + nombre + ".fbx";
        return AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
    }

    // Para ver las transiciones sin jugar de a varios: un muñeco que cae, se arrastra y se levanta en loop.
    [MenuItem("Riftwalker/Invasión/Muñeco de caído (animaciones en loop)")]
    private static void Muneco()
    {
        if (!EditorApplication.isPlaying) { Debug.Log("Muñeco de caído: dale Play primero (en cualquier escena con el jugador)."); return; }
        MunecoDeCaido.Cambiar();
    }

    [MenuItem("Riftwalker/Invasión/Armar las animaciones de caído")]
    public static void Armar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var controlador = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controlador);
        AnimationClip caer = Clip(CaidoEnTerceraPersona.Caer), arrastrarse = Clip(CaidoEnTerceraPersona.Arrastrarse),
            levantarse = Clip(CaidoEnTerceraPersona.Levantarse);
        if (controlador == null || caer == null || arrastrarse == null || levantarse == null) return;

        bool cambio = false;
        if (controlador.parameters.All(p => p.name != CaidoEnTerceraPersona.Parametro))
        {
            controlador.AddParameter(new AnimatorControllerParameter
            {
                name = CaidoEnTerceraPersona.Parametro, type = AnimatorControllerParameterType.Float, defaultFloat = 0f,
            });
            cambio = true;
        }

        AnimatorControllerLayer capa = controlador.layers.FirstOrDefault(c => c.name == CaidoEnTerceraPersona.Capa);
        if (capa == null)
        {
            var maquina = new AnimatorStateMachine { name = CaidoEnTerceraPersona.Capa, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(maquina, controlador);
            AnimatorState nada = maquina.AddState("Nada");
            nada.writeDefaultValues = false;
            maquina.defaultState = nada;
            controlador.AddLayer(new AnimatorControllerLayer
            {
                name = CaidoEnTerceraPersona.Capa,
                defaultWeight = 0f, // apagada: la prende CaidoEnTerceraPersona
                blendingMode = AnimatorLayerBlendingMode.Override,
                stateMachine = maquina,
            });
            capa = controlador.layers.First(c => c.name == CaidoEnTerceraPersona.Capa);
            cambio = true;
        }

        AnimatorState Estado(string nombre, AnimationClip clip)
        {
            ChildAnimatorState hay = capa.stateMachine.states.FirstOrDefault(s => s.state.name == nombre);
            AnimatorState estado = hay.state;
            if (estado == null)
            {
                estado = capa.stateMachine.AddState(nombre);
                estado.writeDefaultValues = false;
                cambio = true;
            }
            if (estado.motion != clip) { estado.motion = clip; cambio = true; }
            return estado;
        }

        AnimatorState eCaer = Estado(CaidoEnTerceraPersona.Caer, caer);
        AnimatorState eArrastrarse = Estado(CaidoEnTerceraPersona.Arrastrarse, arrastrarse);
        AnimatorState eLevantarse = Estado(CaidoEnTerceraPersona.Levantarse, levantarse);
        if (!Mathf.Approximately(eLevantarse.speed, CaidoEnTerceraPersona.VelocidadLevantarse))
        {
            eLevantarse.speed = CaidoEnTerceraPersona.VelocidadLevantarse; // más rápida (menos de 3 s)
            cambio = true;
        }

        // Quieto en el piso (Arrastre en 0) o arrastrándose (1).
        if (!eArrastrarse.speedParameterActive || eArrastrarse.speedParameter != CaidoEnTerceraPersona.Parametro)
        {
            eArrastrarse.speedParameterActive = true;
            eArrastrarse.speedParameter = CaidoEnTerceraPersona.Parametro;
            cambio = true;
        }
        // Al terminar de caer, sigue en el piso con la de arrastrarse (con una mezcla larga: no terminan en la misma pose).
        AnimatorStateTransition aArrastrarse = eCaer.transitions.FirstOrDefault(t => t.destinationState == eArrastrarse);
        if (aArrastrarse == null) { aArrastrarse = eCaer.AddTransition(eArrastrarse); cambio = true; }
        if (!aArrastrarse.hasExitTime || !Mathf.Approximately(aArrastrarse.exitTime, SaleDeCaer) ||
            !Mathf.Approximately(aArrastrarse.duration, MezclaCaerArrastrarse) || !aArrastrarse.hasFixedDuration)
        {
            aArrastrarse.hasExitTime = true;
            aArrastrarse.exitTime = SaleDeCaer;
            aArrastrarse.duration = MezclaCaerArrastrarse;
            aArrastrarse.hasFixedDuration = true;
            cambio = true;
        }

        if (cambio)
        {
            EditorUtility.SetDirty(controlador);
            AssetDatabase.SaveAssets();
            Debug.Log("Riftwalker: animaciones de caído armadas (capa \"" + CaidoEnTerceraPersona.Capa + "\").");
        }
    }
}
