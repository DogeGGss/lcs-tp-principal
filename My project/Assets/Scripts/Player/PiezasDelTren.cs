using UnityEngine;

// Los detalles del Último Tren (francotirador) en primera persona (US 254): el cargador sale y entra como en el Urquiza
// (PiezasDelUrquiza), y el cerrojo. El modelo (Sniper_3.fbx) no trae manija de cerrojo: acá se le agrega una, un
// palito al costado del cajón y del lado de la mano derecha. En el cerrojo la manija gira hacia arriba, va hacia atrás,
// vuelve y baja; AnimacionPrimeraPersona lleva la mano derecha a la punta del palito (Manija).
// Tampoco trae mira, aunque tiene mira telescópica: se le agrega una arriba, sobre el riel (tubo, ocular, objetivo con
// su vidrio y dos soportes), de un gris oscuro. AnimacionPrimeraPersona sube el arma hasta que el ojo queda en el
// centro del tubo (CentroDeLaMira).
// Las medidas están en las coordenadas del modelo (x hacia la boca, z hacia arriba); Unity da vuelta la x al
// importarlo: en la malla un punto (x, y, z) del modelo está en (-x, y, z) * escala.
public class PiezasDelTren : IPiezasDelArma
{
    private const float LargoModelo = 2.260f;   // de punta a punta en x (de -0.747 a 1.513)
    private const float EjeX = 0.06f, EjeZ = 0.235f; // donde está el cerrojo: arriba del cajón, sobre el gatillo
    private const float Ancho = 0.047f;          // el cajón mide 0,094 de ancho
    private const float LargoVarilla = 0.08f, GruesoVarilla = 0.02f;
    private const float Recorrido = 0.12f;       // cuánto va hacia atrás
    private const float Giro = 70f;              // cuánto gira hacia arriba

    // La mira, sobre el riel (x de 0,33 a 0,6; arriba en z 0,34).
    public const float MiraZ = 0.43f;              // el centro del tubo
    private const float MiraDesde = 0.12f, MiraHasta = 0.74f, MiraRadio = 0.035f;
    private const float OcularLargo = 0.1f, OcularRadio = 0.045f, ObjetivoLargo = 0.14f, ObjetivoRadio = 0.06f;
    private static readonly float[] Soportes = { 0.38f, 0.56f };
    private const float RielZ = 0.335f;

    // Momentos del cerrojo (avance de 0 a 1), los mismos que usa la mano en AnimacionPrimeraPersona.
    public const float Agarra = 0.2f, Levantada = 0.33f, Atras = 0.52f, Adelante = 0.72f, Baja = 0.84f;

    private readonly PiezasDelUrquiza cargador;
    private readonly Transform arma;     // la que tiene la malla
    private readonly Transform camara;
    private readonly MeshRenderer render;
    private Transform eje, punta, mira;
    private Renderer[] vistas;
    private Material gris, vidrio;
    private Vector3 lugarEje;
    private float escala;
    private bool armada;

    public PiezasDelTren(GameObject tren, Transform camara)
    {
        this.camara = camara;
        cargador = PiezasDelUrquiza.UltimoTren(tren, camara);
        MeshFilter filtro = tren.GetComponent<MeshFilter>();
        if (filtro == null) filtro = tren.GetComponentInChildren<MeshFilter>(true);
        arma = filtro != null ? filtro.transform : tren.transform;
        render = filtro != null ? filtro.GetComponent<MeshRenderer>() : null;
    }

    private bool Armar()
    {
        if (armada) return eje != null;
        armada = true;
        MeshFilter filtro = arma.GetComponent<MeshFilter>();
        if (filtro == null || filtro.sharedMesh == null || render == null) return false;
        escala = filtro.sharedMesh.bounds.size.x / LargoModelo;

        // Del lado de la mano derecha: el costado que queda más a la derecha de la cámara.
        Vector3 izq = camara.InverseTransformPoint(arma.TransformPoint(new Vector3(-EjeX, -Ancho, EjeZ) * escala));
        Vector3 der = camara.InverseTransformPoint(arma.TransformPoint(new Vector3(-EjeX, Ancho, EjeZ) * escala));
        float lado = der.x >= izq.x ? 1f : -1f;

        var go = new GameObject("Cerrojo");
        go.layer = arma.gameObject.layer;
        eje = go.transform;
        eje.SetParent(arma, false);
        lugarEje = new Vector3(-EjeX, 0f, EjeZ) * escala;
        eje.localPosition = lugarEje;
        ladoManija = lado;

        Transform varilla = Pieza(PrimitiveType.Cylinder, "Manija");
        varilla.localPosition = new Vector3(0f, lado * (Ancho + LargoVarilla * 0.5f), 0f) * escala;
        varilla.localScale = new Vector3(GruesoVarilla, LargoVarilla * 0.5f, GruesoVarilla) * escala;
        punta = new GameObject("Punta").transform;
        punta.SetParent(eje, false);
        punta.localPosition = new Vector3(0f, lado * (Ancho + LargoVarilla), 0f) * escala;

        ArmarMira();
        var lista = new System.Collections.Generic.List<Renderer>(eje.GetComponentsInChildren<Renderer>(true));
        lista.AddRange(mira.GetComponentsInChildren<Renderer>(true));
        vistas = lista.ToArray();
        return true;
    }

    private float ladoManija = 1f;

    // La mira: un tubo a lo largo del arma con el ocular atrás, el objetivo adelante (con un vidrio oscuro en cada
    // punta) y dos soportes que la unen al riel.
    private void ArmarMira()
    {
        gris = Liso(new Color(0.16f, 0.17f, 0.18f), 0.45f);
        vidrio = Liso(new Color(0.05f, 0.09f, 0.14f), 0.9f);
        var go = new GameObject("Mira");
        go.layer = arma.gameObject.layer;
        mira = go.transform;
        mira.SetParent(arma, false);

        Tubo("Tubo", MiraDesde, MiraHasta, MiraRadio, gris);
        Tubo("Ocular", MiraDesde - 0.02f, MiraDesde - 0.02f + OcularLargo, OcularRadio, gris);
        Tubo("Objetivo", MiraHasta - ObjetivoLargo + 0.03f, MiraHasta + 0.03f, ObjetivoRadio, gris);
        Tubo("Vidrio atrás", MiraDesde - 0.025f, MiraDesde - 0.02f, OcularRadio * 0.8f, vidrio);
        Tubo("Vidrio adelante", MiraHasta + 0.03f, MiraHasta + 0.035f, ObjetivoRadio * 0.85f, vidrio);
        foreach (float x in Soportes)
        {
            Transform soporte = Pieza(PrimitiveType.Cube, "Soporte", mira, gris);
            float alto = MiraZ - RielZ;
            soporte.localPosition = new Vector3(-x, 0f, RielZ + alto * 0.5f) * escala;
            soporte.localScale = new Vector3(0.035f, 0.03f, alto) * escala;
        }
    }

    // Un cilindro a lo largo del arma (en x del modelo), de "desde" a "hasta", a la altura de la mira.
    private void Tubo(string nombre, float desde, float hasta, float radio, Material material)
    {
        Transform t = Pieza(PrimitiveType.Cylinder, nombre, mira, material);
        t.localPosition = new Vector3(-(desde + hasta) * 0.5f, 0f, MiraZ) * escala;
        t.localRotation = Quaternion.Euler(0f, 0f, 90f); // el cilindro va en y: lo acuesta en x
        t.localScale = new Vector3(radio * 2f, (hasta - desde) * 0.5f, radio * 2f) * escala;
    }

    // Un material liso de ese color, a partir del del arma (así usa el mismo shader).
    private Material Liso(Color color, float brillo)
    {
        var m = new Material(render.sharedMaterial);
        m.name = "Mira del Último Tren";
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", null);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", brillo);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.6f);
        return m;
    }

    /// <summary>El centro del tubo de la mira, donde va el ojo al apuntar, en el espacio de esa cámara.</summary>
    public bool CentroDeLaMira(Transform camara, out Vector3 lugar)
    {
        lugar = Vector3.zero;
        if (!Armar()) return false;
        lugar = camara.InverseTransformPoint(arma.TransformPoint(new Vector3(-(MiraDesde + 0.05f), 0f, MiraZ) * escala));
        return true;
    }

    private Transform Pieza(PrimitiveType tipo, string nombre) =>
        Pieza(tipo, nombre, eje, render.sharedMaterials.Length > 0 ? render.sharedMaterials[0] : null);

    private Transform Pieza(PrimitiveType tipo, string nombre, Transform padre, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(tipo);
        go.name = nombre;
        Collider choque = go.GetComponent<Collider>();
        if (choque != null) Object.Destroy(choque);
        go.layer = arma.gameObject.layer;
        go.transform.SetParent(padre, false);
        MeshRenderer vista = go.GetComponent<MeshRenderer>();
        if (material != null) vista.sharedMaterial = material;
        vista.shadowCastingMode = render.shadowCastingMode;
        vista.receiveShadows = render.receiveShadows;
        return go.transform;
    }

    /// <summary>Pone el cerrojo según cuánto avanzó (0 a 1; fuera de eso, cerrado).</summary>
    public void Poner(float avance)
    {
        if (!Armar()) return;
        float giro = 0f, atras = 0f;
        if (avance > 0f && avance < 1f)
        {
            giro = Suave(Mathf.InverseLerp(Agarra, Levantada, avance)) * (1f - Suave(Mathf.InverseLerp(Adelante, Baja, avance)));
            atras = Suave(Mathf.InverseLerp(Levantada, Atras, avance)) * (1f - Suave(Mathf.InverseLerp(Atras, Adelante, avance)));
        }
        eje.localPosition = lugarEje + Vector3.right * (Recorrido * escala * atras);
        eje.localRotation = Quaternion.AngleAxis(ladoManija * Giro * giro, Vector3.right);
        // Con la mira puesta el arma se esconde: la manija también.
        bool visible = render.enabled;
        foreach (Renderer r in vistas)
        {
            if (r == null) continue;
            if (r.enabled != visible) r.enabled = visible;
            if (r.gameObject.layer != arma.gameObject.layer) r.gameObject.layer = arma.gameObject.layer;
        }
    }

    /// <summary>Dónde está ahora la punta de la manija y hacia dónde apunta (hacia afuera del arma), en el espacio de
    /// esa cámara.</summary>
    public bool Manija(Transform camara, out Vector3 lugar, out Vector3 afuera)
    {
        lugar = afuera = Vector3.zero;
        if (!Armar() || punta == null) return false;
        lugar = camara.InverseTransformPoint(punta.position);
        afuera = (lugar - camara.InverseTransformPoint(eje.position)).normalized;
        return true;
    }

    private static float Suave(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    public void Disparo(int balasQueQuedan) => cargador.Disparo(balasQueQuedan);
    public void Actualizar(float recarga, int balas) => cargador.Actualizar(recarga, balas);
    public void ActualizarSueltas(float dt) => cargador.ActualizarSueltas(dt);

    public void Quieta()
    {
        cargador.Quieta();
        if (eje != null) Poner(0f);
    }
}
