using System.Collections.Generic;
using UnityEngine;

// US 021, CA6 · Mientras alguien usa la Vista aérea, además de los ojos en el cielo (OjosEnElCielo) le brillan los ojos
// de verde a su personaje, así se lo reconoce de cerca. Los ven los demás jugadores (los muestra JugadorEnRed al
// recibir el aviso de la Vista aérea). Son dos puntos verdes sin iluminación, más fuertes que 1 para que el Bloom los
// haga brillar, y una luz verde chica que tiñe la cara. Van pegados a la cabeza del modelo, así siguen sus movimientos.
public class OjosBrillantes : MonoBehaviour
{
    private const float Encender = 0.25f, Apagar = 0.35f;
    private const float Tamano = 0.035f;  // m de cada ojo (en un personaje de 1,8 m)
    private static readonly Color Verde = new Color(0.3f, 1f, 0.35f);

    private static readonly Dictionary<Transform, OjosBrillantes> activos = new Dictionary<Transform, OjosBrillantes>();
    private static Material material;
    private static Mesh esfera;

    private Transform jugador;
    private readonly List<Transform> ojos = new List<Transform>();
    private Light luz;
    private float escala = 1f, brillo, objetivo = 1f;

    /// <summary>Enciende (o apaga) los ojos verdes del personaje de ese jugador.</summary>
    public static void Mostrar(Transform jugador, bool mostrar)
    {
        if (jugador == null) return;
        activos.TryGetValue(jugador, out OjosBrillantes ojos);
        if (mostrar)
        {
            if (ojos == null)
            {
                Transform cabeza = Cabeza(jugador, out Animator animador);
                if (cabeza == null) return; // sin modelo con cabeza no hay dónde ponerlos
                ojos = new GameObject("Ojos brillantes").AddComponent<OjosBrillantes>();
                ojos.Armar(jugador, cabeza, animador);
                activos[jugador] = ojos;
            }
            ojos.objetivo = 1f;
        }
        else if (ojos != null) ojos.objetivo = 0f;
    }

    private static Transform Cabeza(Transform jugador, out Animator animador)
    {
        animador = null;
        foreach (Animator a in jugador.GetComponentsInChildren<Animator>(true))
            if (a.isHuman && a.GetBoneTransform(HumanBodyBones.Head) != null) { animador = a; return a.GetBoneTransform(HumanBodyBones.Head); }
        foreach (Transform t in jugador.GetComponentsInChildren<Transform>(true))
            if (t.name.ToLowerInvariant().EndsWith("head")) return t;
        return null;
    }

    private void Armar(Transform jugador, Transform cabeza, Animator animador)
    {
        this.jugador = jugador;
        transform.SetParent(cabeza, false);

        // Dónde van: los huesos de los ojos si el modelo los tiene; si no, adelante y arriba de la cabeza, a la escala
        // del personaje (medida por el cuello).
        Transform izq = animador != null ? animador.GetBoneTransform(HumanBodyBones.LeftEye) : null;
        Transform der = animador != null ? animador.GetBoneTransform(HumanBodyBones.RightEye) : null;
        Transform cuello = animador != null ? animador.GetBoneTransform(HumanBodyBones.Neck) : null;
        escala = cuello != null ? Mathf.Clamp(Vector3.Distance(cuello.position, cabeza.position) / 0.1f, 0.3f, 3f) : 1f;
        Vector3 adelante = Vector3.ProjectOnPlane(jugador.forward, Vector3.up).normalized;
        if (adelante.sqrMagnitude < 0.01f) adelante = jugador.forward;
        Vector3 costado = Vector3.Cross(Vector3.up, adelante).normalized;
        Vector3 centro = cabeza.position + Vector3.up * 0.075f * escala + adelante * 0.095f * escala;
        Vector3[] lugares =
        {
            izq != null ? izq.position + adelante * 0.012f * escala : centro - costado * 0.032f * escala,
            der != null ? der.position + adelante * 0.012f * escala : centro + costado * 0.032f * escala,
        };
        foreach (Vector3 lugar in lugares)
        {
            var go = new GameObject("Ojo");
            go.layer = 2; // Ignore Raycast: no frena balas ni cuenta como cuerpo
            go.transform.SetParent(transform, true);
            go.transform.position = lugar;
            go.AddComponent<MeshFilter>().sharedMesh = Esfera();
            MeshRenderer malla = go.AddComponent<MeshRenderer>();
            malla.sharedMaterial = Material();
            malla.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            malla.receiveShadows = false;
            ojos.Add(go.transform);
        }

        var l = new GameObject("Luz de los ojos");
        l.transform.SetParent(transform, true);
        l.transform.position = (lugares[0] + lugares[1]) * 0.5f + adelante * 0.08f * escala;
        luz = l.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = Verde;
        luz.range = 1.2f * escala;
        luz.shadows = LightShadows.None;
        luz.intensity = 0f;
        Aplicar();
    }

    private void LateUpdate()
    {
        if (jugador == null) objetivo = 0f;
        brillo = Mathf.MoveTowards(brillo, objetivo, Time.deltaTime / (objetivo > brillo ? Encender : Apagar));
        if (brillo <= 0f && objetivo <= 0f)
        {
            if (jugador != null && activos.TryGetValue(jugador, out OjosBrillantes yo) && yo == this) activos.Remove(jugador);
            Destroy(gameObject);
            return;
        }
        Aplicar();
    }

    private void Aplicar()
    {
        // Late despacio, como si mirara algo lejos.
        float pulso = 0.85f + 0.15f * Mathf.Sin(Time.time * 6f);
        float tam = Tamano * escala * Mathf.SmoothStep(0f, 1f, brillo);
        foreach (Transform ojo in ojos)
            if (ojo != null) ojo.localScale = Vector3.one * tam / Mathf.Max(0.0001f, ojo.parent.lossyScale.x);
        if (luz != null) luz.intensity = 2.5f * brillo * pulso;
    }

    private void OnDestroy()
    {
        if (jugador != null && activos.TryGetValue(jugador, out OjosBrillantes yo) && yo == this) activos.Remove(jugador);
    }

    private static Mesh Esfera()
    {
        if (esfera == null)
        {
            GameObject temporal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            esfera = temporal.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temporal);
        }
        return esfera;
    }

    // Sin iluminación y con un verde más fuerte que 1 (el mismo de los ojos del cielo): con el Bloom brilla.
    private static Material Material()
    {
        if (material != null) return material;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        Color color = Verde * 4f;
        material = new Material(shader) { name = "Ojos brillantes (verde)", color = color };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        return material;
    }
}
