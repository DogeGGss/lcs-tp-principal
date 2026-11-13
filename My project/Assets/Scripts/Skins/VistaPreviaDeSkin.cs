using UnityEngine;

// F35 (Skins de armas), US 379, CA2: el arma girando con la skin elegida, para la vista previa del perfil. Es una copia
// del modelo del arma (ModelosDeArmas) lejos del menú (abajo, donde no se ve), con su propia cámara y sus luces, que dibuja
// en una textura (Textura) que el perfil muestra. Como la de los emotes (VistaPreviaDeEmote).
public class VistaPreviaDeSkin : MonoBehaviour
{
    private static readonly Vector3 Lugar = new Vector3(0f, -3000f, 0f);
    private const int Ancho = 900, Alto = 600;
    private const float Largo = 2.7f, AltoMaximo = 1.4f; // lo que ocupa el arma (la cámara ve unos 3,1 × 2,1)
    private const float Vueltas = 40f;                   // grados por segundo

    private static VistaPreviaDeSkin unica;

    private RenderTexture textura;
    private Camera camara;
    private Transform pivote;
    private GameObject modelo;
    private string arma;

    /// <summary>La textura donde se dibuja (para un RawImage).</summary>
    public static RenderTexture Textura => Asegurar().textura;

    /// <summary>Muestra esa arma con esa skin (null: la original).</summary>
    public static void Mostrar(string arma, CatalogoDeSkins.Skin skin)
    {
        VistaPreviaDeSkin v = Asegurar();
        v.camara.enabled = true;
        if (arma != v.arma || v.modelo == null) v.Modelo(arma);
        if (v.modelo != null) SkinDeArma.Poner(v.modelo, skin);
    }

    /// <summary>Deja de dibujar (la vista previa no está en pantalla).</summary>
    public static void Apagar()
    {
        if (unica != null && unica.camara != null) unica.camara.enabled = false;
    }

    private static VistaPreviaDeSkin Asegurar()
    {
        if (unica != null) return unica;
        var go = new GameObject("Vista previa de skins");
        go.transform.position = Lugar;
        unica = go.AddComponent<VistaPreviaDeSkin>();
        unica.Armar();
        return unica;
    }

    private void Armar()
    {
        textura = new RenderTexture(Ancho, Alto, 24, RenderTextureFormat.ARGB32) { name = "Vista previa de skins", antiAliasing = 4 };
        var c = new GameObject("Cámara");
        c.transform.SetParent(transform, false);
        c.transform.localPosition = new Vector3(0f, 0f, 4f);
        c.transform.localRotation = Quaternion.LookRotation(Vector3.back);
        camara = c.AddComponent<Camera>();
        camara.targetTexture = textura;
        camara.clearFlags = CameraClearFlags.SolidColor;
        camara.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camara.fieldOfView = 30f;
        camara.nearClipPlane = 0.1f;
        camara.farClipPlane = 10f;

        // Una luz de frente y arriba y otra más suave desde atrás, para el contorno (solo llegan hasta acá).
        Luz("Luz", new Vector3(1.5f, 2.5f, 3.5f), 30f);
        Luz("Contraluz", new Vector3(-2f, 1.5f, -3f), 12f);

        pivote = new GameObject("Pivote").transform;
        pivote.SetParent(transform, false);
    }

    private void Luz(string nombre, Vector3 lugar, float intensidad)
    {
        var l = new GameObject(nombre);
        l.transform.SetParent(transform, false);
        l.transform.localPosition = lugar;
        l.transform.LookAt(transform.position);
        Light luz = l.AddComponent<Light>();
        luz.type = LightType.Spot;
        luz.range = 9f;
        luz.spotAngle = 60f;
        luz.intensity = intensidad;
    }

    private void Modelo(string nuevo)
    {
        if (modelo != null) Destroy(modelo);
        modelo = null;
        arma = nuevo;
        GameObject molde = ModelosDeArmas.Actual.De(nuevo);
        if (molde == null) return;
        pivote.localRotation = Quaternion.identity;
        modelo = Instantiate(molde, pivote);
        modelo.name = "Arma";
        foreach (MonoBehaviour m in modelo.GetComponentsInChildren<MonoBehaviour>(true)) m.enabled = false;
        foreach (Collider col in modelo.GetComponentsInChildren<Collider>(true)) col.enabled = false;

        // De costado: lo más largo del arma (el caño) de izquierda a derecha, centrada y del tamaño de la vista.
        Bounds b = Limites();
        if (b.size.z > b.size.x) { modelo.transform.Rotate(0f, 90f, 0f, Space.World); b = Limites(); }
        float escala = Mathf.Min(Largo / Mathf.Max(0.001f, b.size.x), AltoMaximo / Mathf.Max(0.001f, b.size.y));
        modelo.transform.localScale *= escala;
        b = Limites();
        modelo.transform.position -= b.center - pivote.position;
    }

    // La caja de todo el modelo, en el mundo.
    private Bounds Limites()
    {
        Renderer[] rs = modelo.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(pivote.position, Vector3.one);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    private void Update()
    {
        // Gira despacio (con el tiempo real: el menú puede estar en pausa).
        if (camara != null && camara.enabled && pivote != null) pivote.Rotate(0f, Vueltas * Time.unscaledDeltaTime, 0f, Space.Self);
    }

    private void OnDestroy()
    {
        if (textura != null) textura.Release();
        if (unica == this) unica = null;
    }
}
