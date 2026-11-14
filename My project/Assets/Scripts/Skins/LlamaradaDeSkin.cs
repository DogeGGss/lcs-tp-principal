using UnityEngine;

// F35 (Skins de armas), US 381: el fogonazo de las legendarias (EfectosDeSkin): una lengua de fuego del color de la skin
// que sale de la boca del arma, crece, se enrosca hacia un costado y se apaga en un instante (EfectosDeSkin tira tres, en
// abanico). Es un cuadrado que mira a la cámara, con su x hacia donde sale
// la llama ("haciaDonde", ya en el plano de la pantalla). Art/Skins/EfectoLlamarada (SkinEfectos.shader, modo 2).
public class LlamaradaDeSkin : MonoBehaviour
{
    private const float Duracion = 0.09f; // s
    private static readonly int Abrir = Shader.PropertyToID("_Abrir"), Enrosque = Shader.PropertyToID("_Enrosque");
    private static readonly int ColorFuego = Shader.PropertyToID("_ColorFuego"), ColorNucleo = Shader.PropertyToID("_ColorNucleo");

    private Vector3 boca, haciaDonde;
    private float tamano, ancho, enrosque, empezo;
    private MeshRenderer r;
    private MaterialPropertyBlock bloque;

    /// <summary>Una lengua de fuego de ese largo (m); ancho: respecto del largo; enrosque: hacia qué costado se curva y
    /// cuánto (-1 a 1, 0 derecha).</summary>
    public static void Crear(Vector3 boca, Vector3 haciaDonde, float tamano, CatalogoDeSkins.Efectos e, float ancho = 0.6f, float enrosque = 0f)
    {
        Material material = ModelosDeArmas.Actual.MaterialDe("efecto-llamarada");
        if (material == null) return;
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Fogonazo (skin)";
        Destroy(go.GetComponent<Collider>());
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        LlamaradaDeSkin l = go.AddComponent<LlamaradaDeSkin>();
        l.boca = boca;
        l.haciaDonde = haciaDonde.sqrMagnitude > 0.0001f ? haciaDonde.normalized : Vector3.right;
        l.tamano = tamano;
        l.ancho = ancho;
        l.enrosque = enrosque;
        l.empezo = Time.time;
        l.r = r;
        l.bloque = new MaterialPropertyBlock();
        l.bloque.SetColor(ColorFuego, e.fuego);
        l.bloque.SetColor(ColorNucleo, e.nucleo);
        l.Colocar();
    }

    private void LateUpdate() => Colocar();

    private void Colocar()
    {
        float t = (Time.time - empezo) / Duracion;
        if (t >= 1f) { Destroy(gameObject); return; }
        Camera cam = Camera.main;
        Vector3 x = haciaDonde;
        if (cam != null)
        {
            Vector3 mira = (boca - cam.transform.position).normalized; // el cuadrado mira a la cámara
            x = Vector3.ProjectOnPlane(haciaDonde, mira);
            if (x.sqrMagnitude < 0.0001f) x = cam.transform.right;
            x.Normalize();
            transform.rotation = Quaternion.LookRotation(mira, Vector3.Cross(mira, x)); // (así su derecha es x)
        }
        transform.localScale = new Vector3(tamano, tamano * ancho, 1f);
        transform.position = boca + x * tamano * 0.5f; // el borde izquierdo del cuadrado (x = 0) en la boca
        // Crece rápido y se apaga: el largo llega al todo en la primera mitad.
        bloque.SetFloat(Abrir, Mathf.Clamp01(t * 2.5f) * (1f - Mathf.Clamp01((t - 0.6f) / 0.4f) * 0.6f));
        bloque.SetFloat(Enrosque, enrosque * Mathf.Clamp01(t * 1.5f)); // se va enroscando mientras dura
        r.SetPropertyBlock(bloque);
    }
}
