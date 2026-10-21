using UnityEngine;

// Los detalles del Último Tren (francotirador) en primera persona (US 254): el cargador sale y entra como en el Urquiza
// (PiezasDelUrquiza), y el cerrojo. El modelo (Sniper_3.fbx) no trae manija de cerrojo: acá se le agrega una, una
// varilla con una bolita en la punta, al costado del cajón y del lado de la mano derecha. En el cerrojo la manija gira
// hacia arriba, va hacia atrás, vuelve y baja; AnimacionPrimeraPersona lleva la mano derecha a la bolita (Manija).
// Las medidas están en las coordenadas del modelo (x hacia la boca, z hacia arriba); Unity da vuelta la x al
// importarlo: en la malla un punto (x, y, z) del modelo está en (-x, y, z) * escala.
public class PiezasDelTren : IPiezasDelArma
{
    private const float LargoModelo = 2.260f;   // de punta a punta en x (de -0.747 a 1.513)
    private const float EjeX = 0.06f, EjeZ = 0.235f; // donde está el cerrojo: arriba del cajón, sobre el gatillo
    private const float Ancho = 0.047f;          // el cajón mide 0,094 de ancho
    private const float LargoVarilla = 0.07f, GruesoVarilla = 0.022f, Bolita = 0.05f;
    private const float Recorrido = 0.12f;       // cuánto va hacia atrás
    private const float Giro = 70f;              // cuánto gira hacia arriba

    // Momentos del cerrojo (avance de 0 a 1), los mismos que usa la mano en AnimacionPrimeraPersona.
    public const float Agarra = 0.2f, Levantada = 0.33f, Atras = 0.52f, Adelante = 0.72f, Baja = 0.84f;

    private readonly PiezasDelUrquiza cargador;
    private readonly Transform arma;     // la que tiene la malla
    private readonly Transform camara;
    private readonly MeshRenderer render;
    private Transform eje, bolita;
    private Renderer[] vistas;
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
        bolita = Pieza(PrimitiveType.Sphere, "Bolita");
        bolita.localPosition = new Vector3(0f, lado * (Ancho + LargoVarilla), 0f) * escala;
        bolita.localScale = Vector3.one * (Bolita * escala);
        vistas = eje.GetComponentsInChildren<Renderer>(true);
        return true;
    }

    private float ladoManija = 1f;

    private Transform Pieza(PrimitiveType tipo, string nombre)
    {
        GameObject go = GameObject.CreatePrimitive(tipo);
        go.name = nombre;
        Collider choque = go.GetComponent<Collider>();
        if (choque != null) Object.Destroy(choque);
        go.layer = arma.gameObject.layer;
        go.transform.SetParent(eje, false);
        MeshRenderer vista = go.GetComponent<MeshRenderer>();
        vista.sharedMaterials = render.sharedMaterials.Length > 0 ? new[] { render.sharedMaterials[0] } : render.sharedMaterials;
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

    /// <summary>Dónde está ahora la bolita de la manija, en el espacio de esa cámara.</summary>
    public bool Manija(Transform camara, out Vector3 lugar)
    {
        lugar = Vector3.zero;
        if (!Armar() || bolita == null) return false;
        lugar = camara.InverseTransformPoint(bolita.position);
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
