using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// F27 (Emotes), US 264, CA2: el personaje elegido haciendo un emote, para la vista previa del perfil. Es una copia del
// modelo del personaje (CharacterData.modelo) lejos del menú (abajo, donde no se ve), con su propia cámara y su luz, que
// dibuja en una textura (Textura) que el perfil muestra. La animación se pasa directo al Animator de la copia (sin el
// Animator Controller de la partida).
public class VistaPreviaDeEmote : MonoBehaviour
{
    private static readonly Vector3 Lugar = new Vector3(0f, -2000f, 0f);
    private const int Ancho = 600, Alto = 640;

    private static VistaPreviaDeEmote unica;

    private RenderTexture textura;
    private Camera camara;
    private GameObject modelo;
    private CharacterData personaje;
    private Animator animador;
    private AnimationClip clip;
    private PlayableGraph grafo;

    /// <summary>La textura donde se dibuja (para un RawImage).</summary>
    public static RenderTexture Textura => Asegurar().textura;

    /// <summary>Muestra a ese personaje (o al primero, si es null) haciendo ese emote (o quieto, si es null).</summary>
    public static void Mostrar(CharacterData personaje, AnimationClip clip)
    {
        VistaPreviaDeEmote v = Asegurar();
        v.camara.enabled = true;
        if (personaje == null && CharacterRoster.All.Count > 0) personaje = CharacterRoster.All[0];
        if (personaje != v.personaje || v.modelo == null) v.Modelo(personaje);
        if (clip != v.clip || !v.grafo.IsValid()) v.Bailar(clip);
    }

    /// <summary>Deja de dibujar (la vista previa no está en pantalla).</summary>
    public static void Apagar()
    {
        if (unica != null && unica.camara != null) unica.camara.enabled = false;
    }

    private static VistaPreviaDeEmote Asegurar()
    {
        if (unica != null) return unica;
        var go = new GameObject("Vista previa de emotes");
        go.transform.position = Lugar;
        unica = go.AddComponent<VistaPreviaDeEmote>();
        unica.Armar();
        return unica;
    }

    private void Armar()
    {
        textura = new RenderTexture(Ancho, Alto, 24, RenderTextureFormat.ARGB32) { name = "Vista previa de emotes", antiAliasing = 4 };
        var c = new GameObject("Cámara");
        c.transform.SetParent(transform, false);
        c.transform.localPosition = new Vector3(0f, 1.05f, 4.2f);
        c.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, -0.05f, -1f));
        camara = c.AddComponent<Camera>();
        camara.targetTexture = textura;
        camara.clearFlags = CameraClearFlags.SolidColor;
        camara.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camara.fieldOfView = 34f;
        camara.nearClipPlane = 0.1f;
        camara.farClipPlane = 12f;

        // Una luz de frente y un poco de arriba (solo llega hasta acá).
        var l = new GameObject("Luz");
        l.transform.SetParent(transform, false);
        l.transform.localPosition = new Vector3(1.2f, 3f, 3f);
        l.transform.LookAt(transform.position + Vector3.up);
        Light luz = l.AddComponent<Light>();
        luz.type = LightType.Spot;
        luz.range = 9f;
        luz.spotAngle = 70f;
        luz.intensity = 25f;
    }

    private void Modelo(CharacterData nuevo)
    {
        if (grafo.IsValid()) grafo.Destroy();
        if (modelo != null) Destroy(modelo);
        clip = null;
        personaje = nuevo;
        modelo = null;
        animador = null;
        if (nuevo == null || nuevo.modelo == null) return;
        modelo = Instantiate(nuevo.modelo, transform);
        modelo.name = "Personaje";
        modelo.transform.localPosition = Vector3.zero;
        modelo.transform.localRotation = Quaternion.identity; // mirando a la cámara (está adelante, en +z)
        foreach (MonoBehaviour m in modelo.GetComponentsInChildren<MonoBehaviour>(true)) m.enabled = false;
        foreach (Collider col in modelo.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        foreach (Animator a in modelo.GetComponentsInChildren<Animator>(true))
            if (a.isHuman) { animador = a; break; }
        if (animador != null)
        {
            animador.runtimeAnimatorController = null;
            animador.applyRootMotion = false;
            animador.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
    }

    private void Bailar(AnimationClip nuevo)
    {
        if (grafo.IsValid()) grafo.Destroy();
        clip = nuevo;
        if (animador == null) return;
        if (nuevo == null) { animador.Rebind(); return; } // quieto, en su pose
        AnimationPlayableUtilities.PlayClip(animador, nuevo, out grafo);
    }

    private void OnDestroy()
    {
        if (grafo.IsValid()) grafo.Destroy();
        if (textura != null) textura.Release();
        if (unica == this) unica = null;
    }
}
