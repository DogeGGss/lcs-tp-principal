using UnityEngine;

// F35 (Skins de armas), US 379, CA2: el arma girando con la skin elegida, para la vista previa del perfil. Es una copia
// del modelo del arma (ModelosDeArmas) lejos del menú (abajo, donde no se ve), con su propia cámara y sus luces, que dibuja
// en una textura (Textura) que el perfil muestra. Como la de los emotes (VistaPreviaDeEmote).
// US 382: también se pueden armar otras aparte (Nueva), cada una con su textura y en su lugar, para mostrar varias armas
// a la vez (las cartas de las cápsulas); se borran con Destruir.
// F35: las cartas del pase de batalla y de la tienda directa la muestran con Mostrar(imagen, skin): una por skin,
// compartida entre las imágenes que la muestran (la carta y el panel), que dibuja solo mientras alguna está en pantalla y
// se borra cuando ya no la usa ninguna (UsoDeVistaDeSkin).
public class VistaPreviaDeSkin : MonoBehaviour, IVistaCompartida
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
    public static void Mostrar(string arma, CatalogoDeSkins.Skin skin) => Asegurar().Poner(arma, skin);

    private static int otras;

    /// <summary>Una vista previa aparte (con su propia textura), con esa arma y esa skin. Borrarla con Destruir.</summary>
    public static VistaPreviaDeSkin Nueva(string arma, CatalogoDeSkins.Skin skin)
    {
        var go = new GameObject("Vista previa de skins (aparte)");
        otras = (otras + 1) % 1000;
        go.transform.position = Lugar + new Vector3(50f * (otras + 1), 0f, 0f); // lejos de las demás (y de sus luces)
        var v = go.AddComponent<VistaPreviaDeSkin>();
        v.Armar();
        v.Poner(arma, skin);
        return v;
    }

    /// <summary>La textura de esta vista previa.</summary>
    public RenderTexture Imagen => textura;

    public void Destruir()
    {
        if (this != null) Destroy(gameObject);
    }

    // ---------- Compartidas (pase de batalla y tienda directa) ----------

    private static readonly System.Collections.Generic.Dictionary<string, VistaPreviaDeSkin> compartidas =
        new System.Collections.Generic.Dictionary<string, VistaPreviaDeSkin>();
    private string clave;
    private int usos, enPantalla;

    /// <summary>Muestra en esa imagen el arma de la skin girando. La vista previa se comparte entre las imágenes de la
    /// misma skin y se borra sola cuando se borran todas.</summary>
    public static void Mostrar(UnityEngine.UI.RawImage imagen, CatalogoDeSkins.Skin skin)
    {
        if (imagen == null || skin == null || skin.arma == null) return;
        string k = skin.arma.id + "|" + skin.id;
        if (!compartidas.TryGetValue(k, out VistaPreviaDeSkin v) || v == null)
        {
            v = Nueva(skin.arma.id, skin);
            v.clave = k;
            v.camara.enabled = false; // dibuja recién cuando una imagen está en pantalla
            compartidas[k] = v;
        }
        imagen.texture = v.textura;
        imagen.gameObject.AddComponent<UsoDeVistaDeSkin>().Usar(v);
    }

    public void Tomar() => usos++;

    public void Soltar()
    {
        if (--usos > 0) return;
        if (clave != null && compartidas.TryGetValue(clave, out VistaPreviaDeSkin v) && v == this) compartidas.Remove(clave);
        Destruir();
    }

    public void Visible(bool visible)
    {
        enPantalla = Mathf.Max(0, enPantalla + (visible ? 1 : -1));
        if (camara != null) camara.enabled = enPantalla > 0;
    }

    private void Poner(string nueva, CatalogoDeSkins.Skin skin)
    {
        camara.enabled = true;
        if (nueva != arma || modelo == null) Modelo(nueva);
        if (modelo == null) return;
        SkinDeArma.Poner(modelo, skin);
        // El patrón del tamaño que tiene en la mano (acá el arma está agrandada para que entre en la vista).
        SkinDeArma.AjustarALaMano(modelo, arma, arma == "la-portena" || arma == "la-trochita" ? 0.45f : arma == "ultimo-tren" ? 1.13f : 0.95f);
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
        // El modelo del Último Tren no trae la mira: se la agrega como en la mano (PiezasDelTren), antes de medirlo y de
        // ponerle la skin (así la mira también va con la skin, como el fuego del Dragón Abisal). En el FBX el cuerpo y el
        // cargador son dos hijos (en el prefab de la mano, el cuerpo es la raíz): las piezas van sobre el cuerpo, la malla
        // más grande. Poner(0) las arma, con el cerrojo cerrado.
        if (nuevo == "ultimo-tren")
        {
            GameObject cuerpo = MallaMasGrande(modelo);
            if (cuerpo != null) new PiezasDelTren(cuerpo, camara.transform).Poner(0f);
        }

        // De costado: lo más largo del arma (el caño) de izquierda a derecha, centrada y del tamaño de la vista.
        Bounds b = Limites();
        if (b.size.z > b.size.x) { modelo.transform.Rotate(0f, 90f, 0f, Space.World); b = Limites(); }
        float escala = Mathf.Min(Largo / Mathf.Max(0.001f, b.size.x), AltoMaximo / Mathf.Max(0.001f, b.size.y));
        modelo.transform.localScale *= escala;
        b = Limites();
        modelo.transform.position -= b.center - pivote.position;
    }

    // La pieza del modelo con la malla más grande.
    private static GameObject MallaMasGrande(GameObject go)
    {
        GameObject mejor = null;
        float mayor = -1f;
        foreach (MeshFilter f in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (f.sharedMesh == null) continue;
            Vector3 t = f.sharedMesh.bounds.size;
            float v = t.x * t.y * t.z;
            if (v > mayor) { mayor = v; mejor = f.gameObject; }
        }
        return mejor;
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
        if (textura != null) { textura.Release(); Destroy(textura); }
        if (unica == this) unica = null;
        if (clave != null && compartidas.TryGetValue(clave, out VistaPreviaDeSkin v) && v == this) compartidas.Remove(clave);
    }
}

/// <summary>Una vista previa que comparten varias imágenes (la de una skin o la de un emote, US 392).</summary>
public interface IVistaCompartida
{
    void Tomar();
    void Soltar();
    void Visible(bool visible);
}

// F35 (y US 392): una imagen que muestra una vista previa compartida (VistaPreviaDeSkin.Mostrar o
// VistaPreviaDeEmote.Mostrar): la hace dibujar mientras está en pantalla y la suelta al borrarse.
public class UsoDeVistaDeSkin : MonoBehaviour
{
    private IVistaCompartida vista;
    private bool visible;

    public void Usar(IVistaCompartida v)
    {
        vista = v;
        vista.Tomar();
        if (isActiveAndEnabled) Mostrar(true);
    }

    private void OnEnable() { if (vista != null) Mostrar(true); }
    private void OnDisable() => Mostrar(false);

    private void Mostrar(bool si)
    {
        if (vista == null || visible == si) return;
        visible = si;
        vista.Visible(si);
    }

    private void OnDestroy()
    {
        Mostrar(false);
        if (vista != null) vista.Soltar();
        vista = null;
    }
}
