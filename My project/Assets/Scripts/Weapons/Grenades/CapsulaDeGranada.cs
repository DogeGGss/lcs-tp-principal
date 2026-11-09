using UnityEngine;

// El proyectil del Gran Capitán (US 287): una cápsula en lugar de la granada de mano. Adelante una punta naranja
// redonda, después dos anillos grises con una franja negra en el medio y atrás una cola roja que titila: brilla sola y
// tiene una luz muy corta (unos 40 cm) que tiñe un poco lo de alrededor, así se ve aunque quede entre el pasto (una luz
// más grande manchaba el piso o la pared y parecían dos). En vuelo apunta hacia donde va. Si queda como mina (US 313,
// Campo minado) se queda clavada como venía, con la punta en el lugar del choque, y la cola titila más rápido: así se
// nota que hay un explosivo armado. Lo arma Grenade1 al configurarse con la ficha del lanzagranadas; las piezas son
// primitivas sin colisión (la colisión sigue siendo la de la granada).
public class CapsulaDeGranada : MonoBehaviour
{
    private const float Radio = 0.045f;   // m (unos 40 mm de calibre... un poco más, para que se vea)
    private static readonly Color Naranja = new Color(1f, 0.5f, 0.12f);
    private static readonly Color Gris = new Color(0.45f, 0.45f, 0.47f);
    private static readonly Color Negro = new Color(0.05f, 0.05f, 0.05f);
    private static readonly Color Rojo = new Color(1f, 0.08f, 0.08f);

    private Grenade1 granada;
    private Rigidbody body;
    private Transform cuerpo;       // las piezas, a lo largo de su Z (la punta adelante)
    private Material cola;
    private Light luz;
    private const float AlcanceDeLaLuz = 0.4f; // m: tiñe lo de al lado, sin manchar lejos
    private bool clavada;
    private const float HastaLaPunta = 0.11f; // m del centro de la granada a la punta naranja

    /// <summary>Le pone la cápsula a la granada (esconde el modelo de la granada de mano).</summary>
    public static void Poner(Grenade1 granada)
    {
        if (granada.GetComponent<CapsulaDeGranada>() != null) return;
        Material baseMat = null;
        foreach (Renderer r in granada.GetComponentsInChildren<Renderer>())
        {
            if (baseMat == null && r is MeshRenderer) baseMat = r.sharedMaterial;
            r.enabled = false;
        }
        var c = granada.gameObject.AddComponent<CapsulaDeGranada>();
        c.granada = granada;
        c.body = granada.GetComponent<Rigidbody>();
        c.Armar(baseMat);
    }

    private void Armar(Material baseMat)
    {
        cuerpo = new GameObject("Cápsula").transform;
        cuerpo.SetParent(transform, false);
        // Las medidas van en metros de verdad: se descuenta la escala de la granada (el prefab está agrandado).
        Vector3 escala = transform.lossyScale;
        cuerpo.localScale = new Vector3(1f / Mathf.Max(0.0001f, escala.x), 1f / Mathf.Max(0.0001f, escala.y), 1f / Mathf.Max(0.0001f, escala.z));
        float d = Radio * 2f;
        // De atrás (z negativa) para adelante: cola roja, gris, negro (más fino), gris, punta naranja.
        float z = -0.1f;
        cola = Pieza(PrimitiveType.Cylinder, Rojo, baseMat, ref z, 0.045f, d, true);
        Pieza(PrimitiveType.Cylinder, Gris, baseMat, ref z, 0.045f, d, false);
        Pieza(PrimitiveType.Cylinder, Negro, baseMat, ref z, 0.03f, d * 0.88f, false);
        Pieza(PrimitiveType.Cylinder, Gris, baseMat, ref z, 0.045f, d, false);
        // La punta: media esfera (una esfera con la mitad metida en el anillo gris).
        GameObject punta = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Preparar(punta, Naranja, baseMat, false);
        punta.transform.localPosition = new Vector3(0f, 0f, z);
        punta.transform.localScale = Vector3.one * d;

        var go = new GameObject("Luz de la cola");
        go.transform.SetParent(cuerpo, false);
        go.transform.localPosition = new Vector3(0f, 0f, -0.1f);
        luz = go.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = Rojo;
        luz.range = AlcanceDeLaLuz;
        luz.shadows = LightShadows.None;
    }

    // Un cilindro acostado a lo largo de Z, de "largo" m, desde z hacia adelante.
    private Material Pieza(PrimitiveType tipo, Color color, Material baseMat, ref float z, float largo, float ancho, bool brilla)
    {
        GameObject p = GameObject.CreatePrimitive(tipo);
        Material m = Preparar(p, color, baseMat, brilla);
        p.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // el cilindro de Unity va en Y
        p.transform.localPosition = new Vector3(0f, 0f, z + largo * 0.5f);
        p.transform.localScale = new Vector3(ancho, largo * 0.5f, ancho); // alto 2 de fábrica
        z += largo;
        return m;
    }

    private Material Preparar(GameObject p, Color color, Material baseMat, bool brilla)
    {
        Destroy(p.GetComponent<Collider>());
        p.transform.SetParent(cuerpo, false);
        p.layer = gameObject.layer;
        Renderer r = p.GetComponent<Renderer>();
        Material m = baseMat != null ? new Material(baseMat) : r.material;
        foreach (string prop in new[] { "_BaseColor", "_Color" }) if (m.HasProperty(prop)) m.SetColor(prop, color);
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", null);
        if (brilla) m.EnableKeyword("_EMISSION");
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return m;
    }

    /// <summary>US 313: quedó como mina: se queda en la posición en que venía, con la punta en el punto del choque.</summary>
    public void Clavar(Vector3 punto)
    {
        clavada = true;
        Vector3 hacia = cuerpo != null ? cuerpo.forward : transform.forward;
        transform.position = punto - hacia * HastaLaPunta;
        if (cuerpo != null) cuerpo.localPosition = Vector3.zero;
    }

    private void LateUpdate()
    {
        if (cuerpo == null) return;
        // En vuelo apunta hacia donde va.
        if (!clavada && body != null && !body.isKinematic && body.linearVelocity.sqrMagnitude > 0.5f)
            cuerpo.rotation = Quaternion.LookRotation(body.linearVelocity);

        // La cola titila: en vuelo despacio, armada como mina rápido.
        float rapido = granada != null && granada.esMina ? 9f : 5f;
        bool prendida = Mathf.Sin(Time.time * rapido) > 0f;
        float brillo = prendida ? 1f : 0.12f;
        // Fuerte, así se ve aunque quede entre el pasto (con el bloom, resplandece).
        if (cola != null && cola.HasProperty("_EmissionColor")) cola.SetColor("_EmissionColor", Rojo * (8f * brillo));
        if (luz != null) luz.intensity = 2f * brillo;
    }
}
