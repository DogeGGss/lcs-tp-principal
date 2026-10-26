using TMPro;
using UnityEngine;

// Mira telescópica (US 009 y US 068): solo la tienen los francotiradores. El clic derecho entra a la mira con el
// primer zoom de la ficha, un segundo clic pasa al segundo zoom y un tercero la saca. Con la mira puesta se ve la
// lente (MiraTelescopicaHud), el arma y la mira del HUD se ocultan, la cámara se acerca y el mouse se mueve más lento
// en proporción al zoom. Al disparar se sale de la mira durante el cerrojo y, al terminar, vuelve sola al mismo zoom
// (como el AWP de CS). Recargar, guardar el arma, pausar o abrir la tienda la sacan.
// US 254: antes de que aparezca la lente el arma sube hasta el ojo (Subida, la usa AnimacionPrimeraPersona) y al salir
// baja. Si se apunta en medio del cerrojo, primero termina el cerrojo.
public class MiraTelescopica : MonoBehaviour
{
    [Tooltip("Segundos que tarda la cámara en llegar al zoom.")]
    public float tiempoZoom = 0.12f;
    [Tooltip("Segundos que tarda el arma en subir hasta el ojo antes de que aparezca la lente (y en bajar al salir).")]
    public float tiempoSubida = 0.18f;
    [Tooltip("Hasta dónde mide la distancia que muestra la lente, en metros.")]
    public float distanciaMaxima = 500f;

    [Header("Lente")]
    public TMP_FontAsset fuenteNumeros;
    public TMP_FontAsset fuenteTextos;

    // La mira que está puesta ahora, o null.
    public static MiraTelescopica Activa { get; private set; }
    public static bool Puesta => Activa != null;

    // El francotirador que el jugador tiene en la mano, o null. Sin la mira puesta no tiene mira en el HUD (US 068).
    public static MiraTelescopica EnMano { get; private set; }

    // El mouse se mueve más lento en proporción al zoom, como en CS; encima va la sensibilidad de mira de Opciones (US 155).
    public static float EscalaSensibilidad => Activa != null ? 1f / Activa.ZoomActual : 1f;

    private ArmaDeFuego arma;
    private Camera camara;
    private Renderer[] partes;
    private float fovNormal;
    private bool fovCambiado;
    private int nivel;              // 0 = sin mira, 1 = primer zoom, 2 = segundo zoom
    private int nivelAlVolver;      // zoom al que vuelve después del cerrojo
    private float volverEn = -1f;
    private float cerrojoHasta = -1f; // hasta cuándo dura el cerrojo del último disparo
    private float subida;            // 0 abajo, 1 en el ojo
    private bool lente;              // la lente se ve (el arma ya llegó al ojo)
    private readonly RaycastHit[] impactos = new RaycastHit[16];

    public int Nivel => nivel;
    public float ZoomActual => nivel == 2 ? Ficha.zoom2 : nivel == 1 ? Ficha.zoom : 1f;
    public Camera Camara => camara;
    /// <summary>Cuánto subió el arma hacia el ojo (0 a 1).</summary>
    public float Subida => subida;
    /// <summary>La lente se ve: se apunta de verdad (la dispersión es la de apuntar).</summary>
    public bool ConLente => lente;

    private ShopItem Ficha => arma.shopItem;
    private int Niveles => Ficha == null || Ficha.zoom <= 1f ? 0 : Ficha.zoom2 > 1f ? 2 : 1;

    private void Awake()
    {
        arma = GetComponent<ArmaDeFuego>();
        partes = GetComponentsInChildren<Renderer>(true);
    }

    private void OnEnable()
    {
        camara = arma != null && arma.playerCamera != null ? arma.playerCamera : GetComponentInParent<Camera>();
        if (camara != null) fovNormal = camara.fieldOfView;
        fovCambiado = false;
        nivel = 0;
        volverEn = -1f;
        cerrojoHasta = -1f;
        subida = 0f;
        lente = false;
        EnMano = this;
    }

    private void OnDisable()
    {
        Salir();
        if (EnMano == this) EnMano = null;
    }

    private void Update()
    {
        if (arma == null || camara == null) return;

        // Pausa, tienda o un arma sin zoom en la ficha: sin mira.
        if (!arma.enabled || Niveles == 0)
        {
            Salir();
            return;
        }

        if (KeyBindings.Down(GameAction.Apuntar)) Cambiar();
        if (volverEn >= 0f && Time.time >= volverEn) Volver();
        Subir();
        AplicarZoom();
    }

    // Clic derecho: primer zoom, segundo zoom, sin mira.
    private void Cambiar()
    {
        if (arma.Recargando || arma.Equipando) return;

        // Durante el cerrojo, el clic cancela la vuelta a la mira.
        if (volverEn >= 0f)
        {
            volverEn = -1f;
            nivelAlVolver = 0;
            return;
        }
        // En medio del cerrojo (sin la mira puesta): apunta cuando termina. En la entrega del Sprint 3 (sin las
        // animaciones nuevas), como en la v0.2.0: apunta enseguida.
        if (nivel == 0 && Time.time < cerrojoHasta && AnimacionPrimeraPersona.AnimacionesNuevas)
        {
            nivelAlVolver = 1;
            volverEn = cerrojoHasta;
            return;
        }

        nivel = (nivel + 1) % (Niveles + 1);
        Mostrar();
    }

    // Lo llama el arma después de cada disparo: sale de la mira y vuelve cuando termina el cerrojo.
    public void AlDisparar(float cerrojo)
    {
        cerrojoHasta = Time.time + cerrojo;
        if (nivel == 0) return;
        nivelAlVolver = nivel;
        nivel = 0;
        volverEn = Time.time + cerrojo;
        Mostrar();
    }

    // Lo llama el arma al empezar a recargar.
    public void AlRecargar()
    {
        nivel = 0;
        nivelAlVolver = 0;
        volverEn = -1f;
        Mostrar();
    }

    private void Volver()
    {
        volverEn = -1f;
        if (nivelAlVolver > 0 && !arma.Recargando && arma.Ammo > 0)
        {
            nivel = nivelAlVolver;
            Mostrar();
        }
        nivelAlVolver = 0;
    }

    // Saca la mira en el momento, sin transición.
    private void Salir()
    {
        nivel = 0;
        nivelAlVolver = 0;
        volverEn = -1f;
        subida = 0f;
        Mostrar();
        if (fovCambiado && camara != null) camara.fieldOfView = fovNormal;
        fovCambiado = false;
    }

    // El arma sube hacia el ojo mientras se apunta y baja si no; la lente aparece cuando llega y se va enseguida al salir.
    private void Subir()
    {
        float hasta = nivel > 0 ? 1f : 0f;
        // Entrega del Sprint 3 (sin las animaciones nuevas): sin subir el arma, la lente aparece enseguida (v0.2.0).
        subida = AnimacionPrimeraPersona.AnimacionesNuevas
            ? Mathf.MoveTowards(subida, hasta, Time.deltaTime / Mathf.Max(0.01f, tiempoSubida)) : hasta;
        bool conLente = nivel > 0 && subida >= 1f;
        if (conLente != lente) Mostrar();
    }

    private void Mostrar()
    {
        if (nivel == 0) lente = false;
        else lente = subida >= 1f || !AnimacionPrimeraPersona.AnimacionesNuevas;
        bool puesta = lente;
        foreach (Renderer parte in partes)
            if (parte != null) parte.enabled = !puesta;

        if (puesta)
        {
            Activa = this;
            MiraTelescopicaHud.Mostrar(this);
        }
        else if (Activa == this)
        {
            Activa = null;
            MiraTelescopicaHud.Ocultar();
        }
    }

    // La cámara llega al zoom en tiempoZoom y vuelve igual al salir.
    private void AplicarZoom()
    {
        if (!lente && !fovCambiado)
        {
            fovNormal = camara.fieldOfView; // sigue los cambios de Opciones (US 153)
            return;
        }

        fovCambiado = true;
        float objetivo = fovNormal / (lente ? ZoomActual : 1f);
        camara.fieldOfView = Mathf.Lerp(camara.fieldOfView, objetivo, 1f - Mathf.Exp(-3f * Time.deltaTime / Mathf.Max(0.01f, tiempoZoom)));
        if (!lente && Mathf.Abs(camara.fieldOfView - fovNormal) < 0.05f)
        {
            camara.fieldOfView = fovNormal;
            fovCambiado = false;
        }
    }

    // Distancia a lo que está en el centro de la mira, en metros (-1 si no hay nada hasta distanciaMaxima).
    public float DistanciaAlBlanco()
    {
        Transform camaraT = camara.transform;
        Transform propio = transform.root;
        int cantidad = Physics.RaycastNonAlloc(camaraT.position, camaraT.forward, impactos, distanciaMaxima, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float cerca = -1f;
        for (int i = 0; i < cantidad; i++)
        {
            if (impactos[i].collider.transform.IsChildOf(propio)) continue;
            if (cerca < 0f || impactos[i].distance < cerca) cerca = impactos[i].distance;
        }
        return cerca;
    }
}
