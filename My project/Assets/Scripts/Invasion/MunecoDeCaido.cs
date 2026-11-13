using TMPro;
using UnityEngine;

// F31 (US 357), solo para probar en el editor: un muñeco quieto adelante del jugador que repite en loop las animaciones
// de caído, con las mismas transiciones que ven los demás (CaidoEnTerceraPersona): parado, cae, queda 5 s
// arrastrándose, se levanta y vuelve a estar parado. Arriba dice en qué parte está.
// Se prende y apaga con Riftwalker > Invasión > Muñeco de caído (con el juego andando, en cualquier escena).
public class MunecoDeCaido : MonoBehaviour
{
    private const float Distancia = 3f, Parado = 2f, Arrastrado = 5f, Arrastre = 0.6f; // s, s y m/s
    private const float CaerSinClip = 2f, LevantarseSinClip = 2.5f;

    private enum Fase { Parado, Cayendo, Arrastrado, Levantandose }

    private Transform raiz;
    private Animator animador;
    private CaidoEnTerceraPersona caido;
    private TextMeshPro cartel;
    private Fase fase = Fase.Parado;
    private float faseHasta, caer = CaerSinClip, levantarse = LevantarseSinClip;
    private Vector3 inicio;

    /// <summary>Lo prende o lo apaga (lo llama el menú).</summary>
    public static void Cambiar()
    {
        MunecoDeCaido hay = FindAnyObjectByType<MunecoDeCaido>();
        if (hay != null) { Destroy(hay.gameObject); return; }
        new GameObject("Muñeco de caído (prueba)").AddComponent<MunecoDeCaido>();
    }

    private void Start()
    {
        Camera cam = Camera.main;
        PlayerMovement jugador = cam != null ? cam.GetComponentInParent<PlayerMovement>() : FindFirstObjectByType<PlayerMovement>();
        Animator original = null;
        if (jugador != null)
            foreach (Animator a in jugador.GetComponentsInChildren<Animator>())
                if (a.isHuman) { original = a; break; }
        if (original == null) { Debug.LogWarning("Muñeco de caído: no encontré el cuerpo del jugador."); Destroy(gameObject); return; }

        // La copia del cuerpo del jugador (como el muñeco espejo): el mismo modelo con su Animator, sin scripts ni choques.
        raiz = transform;
        var apagado = new GameObject("(copiando)");
        apagado.SetActive(false);
        GameObject cuerpo = Instantiate(original.gameObject, apagado.transform);
        for (int vuelta = 0; vuelta < 2; vuelta++)
            foreach (MonoBehaviour m in cuerpo.GetComponentsInChildren<MonoBehaviour>(true)) if (m != null) DestroyImmediate(m);
        foreach (Collider c in cuerpo.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        foreach (Rigidbody rb in cuerpo.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(rb);
        foreach (Transform t in cuerpo.GetComponentsInChildren<Transform>(true))
            if (t != null && t != cuerpo.transform && (t.name.StartsWith("Brazos") || t.name.StartsWith("Contorno")))
                DestroyImmediate(t.gameObject);
        foreach (Renderer r in cuerpo.GetComponentsInChildren<Renderer>(true))
        {
            r.enabled = true;
            r.forceRenderingOff = false;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
        foreach (Transform t in cuerpo.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
        cuerpo.name = "Cuerpo";

        Vector3 adelante = jugador.transform.forward;
        adelante.y = 0f;
        adelante = adelante.sqrMagnitude > 0.001f ? adelante.normalized : Vector3.forward;
        // De costado al jugador, así se ve bien el cuerpo al caer y al arrastrarse.
        raiz.SetPositionAndRotation(jugador.transform.position + adelante * Distancia, Quaternion.LookRotation(Vector3.Cross(Vector3.up, adelante)));
        inicio = raiz.position;
        cuerpo.transform.SetParent(raiz, false);
        cuerpo.transform.localPosition = jugador.transform.InverseTransformPoint(original.transform.position);
        cuerpo.transform.localRotation = Quaternion.Inverse(jugador.transform.rotation) * original.transform.rotation;
        cuerpo.transform.localScale = original.transform.lossyScale;
        Destroy(apagado);

        animador = cuerpo.GetComponent<Animator>();
        animador.applyRootMotion = false;
        animador.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        caido = CaidoEnTerceraPersona.Crear(raiz.gameObject, animador, null, null);
        if (animador.GetLayerIndex(CaidoEnTerceraPersona.Capa) < 0)
            Debug.LogWarning("Muñeco de caído: el Animator no tiene la capa \"Caido\" (Riftwalker > Invasión > Armar las animaciones de caído).");
        if (animador.runtimeAnimatorController != null)
            foreach (AnimationClip clip in animador.runtimeAnimatorController.animationClips)
            {
                if (clip == null) continue;
                if (clip.name == CaidoEnTerceraPersona.Caer) caer = clip.length;
                else if (clip.name == CaidoEnTerceraPersona.Levantarse) levantarse = clip.length / CaidoEnTerceraPersona.VelocidadLevantarse;
            }

        var go = new GameObject("Cartel");
        go.transform.SetParent(raiz, false);
        go.transform.localPosition = Vector3.up * 2.3f;
        cartel = go.AddComponent<TextMeshPro>();
        cartel.alignment = TextAlignmentOptions.Center;
        cartel.fontSize = 3f;
        cartel.rectTransform.sizeDelta = new Vector2(5f, 1f);
        Pasar(Fase.Parado, Parado);
    }

    private void Pasar(Fase nueva, float segundos)
    {
        fase = nueva;
        faseHasta = Time.time + segundos;
        if (nueva == Fase.Parado) raiz.position = inicio; // vuelve a su lugar para la vuelta siguiente
        caido.Caido = nueva == Fase.Cayendo || nueva == Fase.Arrastrado;
    }

    private void Update()
    {
        if (caido == null) return;
        if (fase == Fase.Arrastrado) raiz.position += raiz.forward * Arrastre * Time.deltaTime; // para que se arrastre (no quieto)
        if (Time.time >= faseHasta)
        {
            switch (fase)
            {
                case Fase.Parado: Pasar(Fase.Cayendo, caer); break;
                case Fase.Cayendo: Pasar(Fase.Arrastrado, Arrastrado); break;
                case Fase.Arrastrado: Pasar(Fase.Levantandose, levantarse); break;
                case Fase.Levantandose: Pasar(Fase.Parado, Parado); break;
            }
        }
        if (cartel != null)
        {
            cartel.text = fase + "  " + Mathf.Max(0f, faseHasta - Time.time).ToString("0.0") + " s";
            Camera cam = Camera.main;
            if (cam != null) cartel.transform.rotation = Quaternion.LookRotation(cartel.transform.position - cam.transform.position);
        }
    }
}
