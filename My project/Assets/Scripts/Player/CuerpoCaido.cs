using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// El cuerpo de alguien que murió, como lo ven los demás (US 260), parecido a Overwatch:
// - CA1: una copia del modelo, en la misma pose en que estaba, que se afloja y cae con física (ragdoll): cadera, torso,
//   cabeza, brazos y piernas son piezas unidas con articulaciones que se doblan, y queda tirado según el piso.
// - CA2: sale empujada desde donde vino el golpe que lo mató (más fuerte con una granada).
// - CA3: el arma que tenía en la mano se suelta y cae por su cuenta.
// - CA4: choca con el piso y las paredes pero no con los personajes, y está en la capa Ignore Raycast: las balas la
//   atraviesan.
// - CA5: a los 4 s (o antes, si el jugador reaparece: Irse) se desvanece de a poco y desaparece.
// El modelo de verdad del jugador queda escondido mientras tanto (lo esconde quien crea el cuerpo). El ragdoll se arma
// con los huesos del esqueleto humanoide del Animator, así sirve con el modelo de cualquier personaje.
public class CuerpoCaido : MonoBehaviour
{
    public const float Dura = 4f;               // CA5
    private const float Desvanece = 0.8f, DesvaneceRapido = 0.3f;
    private const float Hundirse = 0.25f;       // metros que baja mientras se desvanece

    private readonly List<Rigidbody> partes = new List<Rigidbody>();
    private readonly List<Collider> choques = new List<Collider>();
    private GameObject arma;
    private bool yendose;

    /// <summary>
    /// Arma el cuerpo caído a partir del modelo (el que tiene el Animator humanoide), con el empujón inicial (m/s) y,
    /// si tenía, el arma que se suelta. Devuelve null si el modelo no es humanoide (queda la caída de antes).
    /// </summary>
    public static CuerpoCaido Crear(Transform modelo, Vector3 empujon, GameObject armaEnMano = null)
    {
        if (modelo == null) return null;
        Animator animador = modelo.GetComponentInChildren<Animator>(true);
        if (animador == null || !animador.isHuman) return null;

        // Los huesos, por su camino desde el modelo: así se encuentran en la copia.
        var caminos = new Dictionary<HumanBodyBones, string>();
        foreach (HumanBodyBones hueso in Usados)
        {
            Transform t = animador.GetBoneTransform(hueso);
            if (t != null && t.IsChildOf(modelo)) caminos[hueso] = Camino(modelo, t);
        }
        if (!caminos.ContainsKey(HumanBodyBones.Hips)) return null;

        GameObject copia = CopiaQuieta(modelo.gameObject, modelo.name + " (caído)");
        CuerpoCaido cuerpo = copia.AddComponent<CuerpoCaido>();
        // Los contornos rojos son copias de la malla que maneja el jugador: en el cuerpo no van.
        foreach (Renderer r in copia.GetComponentsInChildren<Renderer>(true))
            if (r != null && (r.name == "Contorno" || r.name == "ContornoMascara")) DestroyImmediate(r.gameObject);
        foreach (SkinnedMeshRenderer piel in copia.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            piel.updateWhenOffscreen = true; // los huesos se van lejos de donde estaba: que no desaparezca por los límites

        var huesos = new Dictionary<HumanBodyBones, Transform>();
        foreach (KeyValuePair<HumanBodyBones, string> c in caminos)
        {
            Transform t = c.Value.Length == 0 ? copia.transform : copia.transform.Find(c.Value);
            if (t != null) huesos[c.Key] = t;
        }
        cuerpo.Armar(huesos);
        Capa(copia);
        copia.SetActive(true);

        if (armaEnMano != null) cuerpo.SoltarArma(armaEnMano, empujon);
        cuerpo.NoChocarConPersonajes();
        foreach (Rigidbody rb in cuerpo.partes)
        {
            rb.linearVelocity = empujon;
            rb.angularVelocity = Vector3.zero;
        }
        cuerpo.StartCoroutine(cuerpo.Esperar());
        return cuerpo;
    }

    /// <summary>Se desvanece ya (el jugador reapareció). Rápido: más corto.</summary>
    public void Irse(bool rapido)
    {
        if (yendose || this == null) return;
        StopAllCoroutines();
        StartCoroutine(Desvanecerse(rapido ? DesvaneceRapido : Desvanece));
    }

    // ---------- La copia ----------

    private static readonly HumanBodyBones[] Usados =
    {
        HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Neck, HumanBodyBones.Head,
        HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
        HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
        HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
        HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
    };

    private static string Camino(Transform raiz, Transform t)
    {
        var partesCamino = new List<string>();
        for (Transform x = t; x != null && x != raiz; x = x.parent) partesCamino.Insert(0, x.name);
        return string.Join("/", partesCamino);
    }

    // Copia solo visual, en el mismo lugar, pose y tamaño, sin ningún script, animador, sonido ni choque. Se copia
    // dentro de un objeto apagado, así ningún script de la copia llega a arrancar. Queda apagada (la prende Crear).
    private static GameObject CopiaQuieta(GameObject original, string nombre)
    {
        var apagado = new GameObject("(copiando)");
        apagado.SetActive(false);
        GameObject copia = Instantiate(original, apagado.transform);
        copia.name = nombre;
        copia.transform.SetPositionAndRotation(original.transform.position, original.transform.rotation);
        copia.transform.localScale = original.transform.lossyScale;
        // Primero los scripts (pueden pedir otros componentes), dos vueltas por si uno depende de otro.
        for (int vuelta = 0; vuelta < 2; vuelta++)
            foreach (MonoBehaviour m in copia.GetComponentsInChildren<MonoBehaviour>(true)) if (m != null) DestroyImmediate(m);
        foreach (Joint j in copia.GetComponentsInChildren<Joint>(true)) DestroyImmediate(j);
        foreach (Rigidbody rb in copia.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(rb);
        foreach (Collider c in copia.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        foreach (Behaviour b in copia.GetComponentsInChildren<Behaviour>(true)) if (b != null) DestroyImmediate(b);
        // Lo que estaba apagado (armas guardadas, efectos) no va.
        foreach (Transform t in copia.GetComponentsInChildren<Transform>(true))
            if (t != null && t != copia.transform && !t.gameObject.activeSelf) DestroyImmediate(t.gameObject);
        copia.SetActive(false);
        copia.transform.SetParent(null, true);
        Destroy(apagado);
        copia.transform.localScale = original.transform.lossyScale;
        return copia;
    }

    private static void Capa(GameObject go)
    {
        int capa = LayerMask.NameToLayer("Ignore Raycast");
        if (capa < 0) return;
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = capa;
    }

    // ---------- El ragdoll ----------

    private void Armar(Dictionary<HumanBodyBones, Transform> h)
    {
        Transform cadera = h[HumanBodyBones.Hips];
        Transform torso = Hueso(h, HumanBodyBones.Chest) ?? Hueso(h, HumanBodyBones.Spine);
        Transform cabeza = Hueso(h, HumanBodyBones.Head);
        Transform cuello = Hueso(h, HumanBodyBones.Neck);

        // Cadera: una caja que va de las piernas a la columna.
        var puntosCadera = new List<Vector3> { cadera.position };
        Agregar(puntosCadera, h, HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.Spine);
        Rigidbody rbCadera = Pieza(cadera, null, 12f);
        Caja(cadera, puntosCadera);

        // Torso: de la columna a los hombros y el cuello.
        Rigidbody rbTorso = null;
        if (torso != null && torso != cadera)
        {
            var puntosTorso = new List<Vector3> { torso.position };
            Agregar(puntosTorso, h, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, HumanBodyBones.Neck);
            if (cuello == null && cabeza != null) puntosTorso.Add(cabeza.position);
            rbTorso = Pieza(torso, rbCadera, 15f);
            Caja(torso, puntosTorso);
            Articulacion(torso, rbCadera, 10f, 10f);
        }
        Rigidbody arriba = rbTorso != null ? rbTorso : rbCadera;

        // Cabeza: una esfera.
        if (cabeza != null)
        {
            Pieza(cabeza, arriba, 5f);
            Transform base0 = cuello != null ? cuello : torso;
            float radioMundo = Mathf.Clamp((base0 != null ? Vector3.Distance(cabeza.position, base0.position) : 0.2f) * 0.9f, 0.08f, 0.14f);
            var esfera = cabeza.gameObject.AddComponent<SphereCollider>();
            esfera.radius = radioMundo / Escala(cabeza);
            esfera.center = cabeza.InverseTransformPoint(cabeza.position + Vector3.up * radioMundo * 0.7f);
            choques.Add(esfera);
            Articulacion(cabeza, arriba, 20f, 20f);
        }

        // Brazos y piernas: cápsulas de cada hueso al siguiente.
        Miembro(h, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, arriba, 3f, 0.2f, 40f, 35f, out Rigidbody brazoIzq);
        Miembro(h, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, brazoIzq, 2f, 0.18f, 40f, 10f, out _);
        Miembro(h, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, arriba, 3f, 0.2f, 40f, 35f, out Rigidbody brazoDer);
        Miembro(h, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, brazoDer, 2f, 0.18f, 40f, 10f, out _);
        Miembro(h, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, rbCadera, 7f, 0.2f, 30f, 20f, out Rigidbody piernaIzq);
        Miembro(h, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, piernaIzq, 4f, 0.16f, 30f, 5f, out _);
        Miembro(h, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, rbCadera, 7f, 0.2f, 30f, 20f, out Rigidbody piernaDer);
        Miembro(h, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, piernaDer, 4f, 0.16f, 30f, 5f, out _);

        // Las piezas pegadas no chocan entre sí (se trabarían).
        for (int i = 0; i < choques.Count; i++)
            for (int j = i + 1; j < choques.Count; j++)
                Physics.IgnoreCollision(choques[i], choques[j]);
    }

    private static Transform Hueso(Dictionary<HumanBodyBones, Transform> h, HumanBodyBones b) =>
        h.TryGetValue(b, out Transform t) ? t : null;

    private static void Agregar(List<Vector3> puntos, Dictionary<HumanBodyBones, Transform> h, params HumanBodyBones[] huesos)
    {
        foreach (HumanBodyBones b in huesos) if (h.TryGetValue(b, out Transform t)) puntos.Add(t.position);
    }

    private static float Escala(Transform t)
    {
        Vector3 s = t.lossyScale;
        return Mathf.Max(1e-5f, (Mathf.Abs(s.x) + Mathf.Abs(s.y) + Mathf.Abs(s.z)) / 3f);
    }

    private Rigidbody Pieza(Transform t, Rigidbody padre, float masa)
    {
        var rb = t.gameObject.AddComponent<Rigidbody>();
        rb.mass = masa;
        rb.linearDamping = 0.1f;
        rb.angularDamping = 3f; // frena los giros: sin esto las piezas se sacuden como gelatina
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.solverIterations = 25;
        rb.solverVelocityIterations = 10;
        partes.Add(rb);
        return rb;
    }

    // Caja que envuelve esos puntos (en el espacio del hueso), con un mínimo de grosor.
    private void Caja(Transform t, List<Vector3> puntos)
    {
        var limites = new Bounds(t.InverseTransformPoint(puntos[0]), Vector3.zero);
        foreach (Vector3 p in puntos) limites.Encapsulate(t.InverseTransformPoint(p));
        Vector3 tam = limites.size;
        float mayor = Mathf.Max(tam.x, tam.y, tam.z);
        tam = new Vector3(Mathf.Max(tam.x, mayor * 0.5f), Mathf.Max(tam.y, mayor * 0.5f), Mathf.Max(tam.z, mayor * 0.5f));
        var caja = t.gameObject.AddComponent<BoxCollider>();
        caja.center = limites.center;
        caja.size = tam;
        choques.Add(caja);
    }

    private void Miembro(Dictionary<HumanBodyBones, Transform> h, HumanBodyBones desde, HumanBodyBones hasta, Rigidbody padre,
                         float masa, float grosor, float giro, float costado, out Rigidbody rb)
    {
        rb = null;
        Transform a = Hueso(h, desde);
        if (a == null || padre == null) return;
        Transform b = Hueso(h, hasta);
        Vector3 fin = b != null ? a.InverseTransformPoint(b.position)
                                : a.childCount > 0 ? a.GetChild(0).localPosition : Vector3.up * 0.25f / Escala(a);
        rb = Pieza(a, padre, masa);
        int eje = Mathf.Abs(fin.x) > Mathf.Abs(fin.y) ? (Mathf.Abs(fin.x) > Mathf.Abs(fin.z) ? 0 : 2) : (Mathf.Abs(fin.y) > Mathf.Abs(fin.z) ? 1 : 2);
        float largo = fin.magnitude;
        var capsula = a.gameObject.AddComponent<CapsuleCollider>();
        capsula.direction = eje;
        capsula.center = fin * 0.5f;
        capsula.radius = largo * grosor;
        capsula.height = largo + capsula.radius;
        choques.Add(capsula);
        Articulacion(a, padre, giro, costado, fin.normalized);
    }

    private static void Articulacion(Transform t, Rigidbody padre, float giro, float costado, Vector3? eje = null)
    {
        var j = t.gameObject.AddComponent<CharacterJoint>();
        j.connectedBody = padre;
        j.enableProjection = true;
        j.projectionDistance = 0.02f;
        j.projectionAngle = 5f;
        j.enablePreprocessing = false;
        Vector3 largo = eje ?? Vector3.up;
        Vector3 otro = Mathf.Abs(Vector3.Dot(largo, Vector3.right)) < 0.9f ? Vector3.right : Vector3.forward;
        j.axis = largo;
        j.swingAxis = Vector3.Cross(largo, otro).normalized;
        j.lowTwistLimit = new SoftJointLimit { limit = -giro * 0.5f };
        j.highTwistLimit = new SoftJointLimit { limit = giro * 0.5f };
        j.swing1Limit = new SoftJointLimit { limit = Mathf.Max(costado, giro * 0.5f) };
        j.swing2Limit = new SoftJointLimit { limit = costado };
    }

    // CA3: una copia del arma que tenía en la mano, que cae por su cuenta (y se va con el cuerpo).
    private void SoltarArma(GameObject enMano, Vector3 empujon)
    {
        Transform t = enMano.transform;
        bool prendida = enMano.activeSelf;
        GameObject copia = CopiaQuieta(enMano, enMano.name + " (suelta)");
        copia.transform.SetPositionAndRotation(t.position, t.rotation);
        copia.transform.localScale = t.lossyScale;
        Capa(copia);
        copia.SetActive(true);
        if (!prendida) foreach (Renderer r in copia.GetComponentsInChildren<Renderer>(true)) r.enabled = true;

        // Una caja del tamaño de lo que se ve.
        Renderer[] vistas = copia.GetComponentsInChildren<Renderer>(true);
        if (vistas.Length > 0)
        {
            Bounds mundo = vistas[0].bounds;
            foreach (Renderer r in vistas) mundo.Encapsulate(r.bounds);
            var caja = copia.AddComponent<BoxCollider>();
            caja.center = copia.transform.InverseTransformPoint(mundo.center);
            Vector3 tam = copia.transform.InverseTransformVector(mundo.size);
            caja.size = new Vector3(Mathf.Abs(tam.x), Mathf.Abs(tam.y), Mathf.Abs(tam.z)) * 0.8f;
            choques.Add(caja);
        }
        var rb = copia.AddComponent<Rigidbody>();
        rb.mass = 3f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.linearVelocity = empujon * 0.8f + Vector3.up * 1.2f;
        rb.angularVelocity = new Vector3(3f, 0.5f, 4f);
        arma = copia;
    }

    // CA4: con los personajes (vivos o no) no choca: ni los frena ni lo empujan.
    private void NoChocarConPersonajes()
    {
        foreach (HealthSystem personaje in FindObjectsByType<HealthSystem>(FindObjectsSortMode.None))
            foreach (Collider suyo in personaje.GetComponentsInChildren<Collider>(true))
                foreach (Collider mio in choques)
                    if (suyo != null && mio != null) Physics.IgnoreCollision(suyo, mio);
    }

    // ---------- Irse (CA5) ----------

    private IEnumerator Esperar()
    {
        yield return new WaitForSeconds(Dura);
        Irse(false);
    }

    private IEnumerator Desvanecerse(float duracion)
    {
        yendose = true;
        // Deja de chocar y se hunde apenas mientras se vuelve transparente.
        foreach (Rigidbody rb in partes) if (rb != null) rb.isKinematic = true;
        foreach (Collider c in choques) if (c != null) c.enabled = false;
        var cosas = new List<Transform> { transform };
        if (arma != null)
        {
            Rigidbody rbArma = arma.GetComponent<Rigidbody>();
            if (rbArma != null) rbArma.isKinematic = true;
            cosas.Add(arma.transform);
        }
        var materiales = new List<Tinte>();
        foreach (Transform cosa in cosas)
            foreach (Renderer r in cosa.GetComponentsInChildren<Renderer>(true))
            {
                Material[] propios = r.materials; // copias propias: se pueden tocar sin cambiar las de los demás
                foreach (Material m in propios)
                {
                    if (m == null) continue;
                    Transparente(m);
                    string color = m.HasProperty("_BaseColor") ? "_BaseColor" : m.HasProperty("_Color") ? "_Color" : null;
                    if (color != null) materiales.Add(new Tinte { m = m, color = color, original = m.GetColor(color) });
                }
            }
        var inicio = new List<Vector3>();
        foreach (Transform cosa in cosas) inicio.Add(cosa.position);

        for (float t = 0f; t < duracion; t += Time.deltaTime)
        {
            float k = t / duracion;
            foreach (Tinte x in materiales)
            {
                if (x.m == null) continue;
                Color c = x.original;
                c.a = x.original.a * (1f - k);
                x.m.SetColor(x.color, c);
            }
            for (int i = 0; i < cosas.Count; i++)
                if (cosas[i] != null) cosas[i].position = inicio[i] + Vector3.down * (Hundirse * k * k);
            yield return null;
        }
        foreach (Tinte x in materiales) if (x.m != null) Destroy(x.m);
        if (arma != null) Destroy(arma);
        Destroy(gameObject);
    }

    private struct Tinte { public Material m; public string color; public Color original; }

    // Los materiales de URP (Lit, Simple Lit, Unlit) pasan a superficie transparente, como Semitransparente.
    private static void Transparente(Material m)
    {
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHATEST_ON");
        m.renderQueue = (int)RenderQueue.Transparent;
    }

    private void OnDestroy()
    {
        if (arma != null) Destroy(arma);
    }
}
