using TMPro;
using UnityEngine;

// Mira telescópica (US 009 y US 068): solo la tienen los francotiradores. El clic derecho entra a la mira con el
// primer zoom de la ficha, un segundo clic pasa al segundo zoom y un tercero la saca. Con la mira puesta se ve la
// lente (MiraTelescopicaHud), el arma y la mira del HUD se ocultan, la cámara se acerca y el mouse se mueve más lento
// en proporción al zoom. Al disparar se sale de la mira durante el cerrojo y, al terminar, vuelve sola al mismo zoom
// (como el AWP de CS). Recargar, guardar el arma, pausar o abrir la tienda la sacan.
public class MiraTelescopica : MonoBehaviour
{
    [Tooltip("Segundos que tarda la cámara en llegar al zoom.")]
    public float tiempoZoom = 0.12f;
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
    private readonly RaycastHit[] impactos = new RaycastHit[16];

    public int Nivel => nivel;
    public float ZoomActual => nivel == 2 ? Ficha.zoom2 : nivel == 1 ? Ficha.zoom : 1f;
    public Camera Camara => camara;

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

        nivel = (nivel + 1) % (Niveles + 1);
        Mostrar();
    }

    // Lo llama el arma después de cada disparo: sale de la mira y vuelve cuando termina el cerrojo.
    public void AlDisparar(float cerrojo)
    {
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
        Mostrar();
        if (fovCambiado && camara != null) camara.fieldOfView = fovNormal;
        fovCambiado = false;
    }

    private void Mostrar()
    {
        bool puesta = nivel > 0;
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
        if (nivel == 0 && !fovCambiado)
        {
            fovNormal = camara.fieldOfView; // sigue los cambios de Opciones (US 153)
            return;
        }

        fovCambiado = true;
        float objetivo = fovNormal / ZoomActual;
        camara.fieldOfView = Mathf.Lerp(camara.fieldOfView, objetivo, 1f - Mathf.Exp(-3f * Time.deltaTime / Mathf.Max(0.01f, tiempoZoom)));
        if (nivel == 0 && Mathf.Abs(camara.fieldOfView - fovNormal) < 0.05f)
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
