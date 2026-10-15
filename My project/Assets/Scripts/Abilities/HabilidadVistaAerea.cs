using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// US 021 · Vista aérea: la cámara sube y muestra todo el mapa desde arriba, sin los techos, durante 4 s (CA1 y CA3).
// Los rivales vivos se marcan en rojo en tiempo real, salvo los que están en el humo (CA2). Mientras tanto el personaje
// se queda quieto: no se mueve, no mira, no dispara ni cambia de arma (CA1). Termina antes con la Q o el clic derecho
// (CA3), o al recibir daño (CA4). La recarga empieza al terminar (CA8).
// Lo que se ve en los demás (los ojos en el cielo, el aviso a los rivales, las marcas del minimapa) lo mandan
// JugadorEnRed y RivalesVistos con el evento Cambio.
// La cámara del jugador no se toca (sigue siendo Camera.main): se apaga lo que dibuja y una cámara aparte, más arriba,
// dibuja la vista. Los techos se sacan con el plano cercano de esa cámara: no dibuja nada más arriba de 2,6 m sobre
// los pies del jugador.
public class HabilidadVistaAerea : EfectoHabilidad
{
    public const float Duracion = 4f;       // CA3
    private const float Subida = 0.4f;      // lo que tarda la cámara en subir
    private const float AlturaDeCorte = 2.6f;
    private const float Campo = 50f;        // grados verticales de la cámara de arriba
    private const float MapaMaximo = 260f;  // m: un piso enorme (un terreno) no aleja la cámara
    private static readonly Color ColorRival = new Color(1f, 0.36f, 0.36f);

    /// <summary>CA6, CA7 y CA11: el jugador que la usa y si empezó (true) o terminó (false).</summary>
    public static event System.Action<Transform, bool> Cambio;

    /// <summary>La vista del jugador de esta computadora está activa (RivalesVistos manda a los rivales al equipo).</summary>
    public static bool Activa { get; private set; }

    private static Sprite circulo;
    private static UnityEngine.SceneManagement.Scene escenaLimites;
    private static Bounds limitesCache;
    private static bool hayLimites;

    private PlayerAbility habilidad;
    private HealthSystem vida;
    private PlayerMovement movimiento;
    private Camera ojos;
    private Camera aerea;
    private bool activa;
    private float inicio;
    private int cuadroInicio;
    private Vector3 desdeLugar, hastaLugar;
    private Quaternion desdeGiro, hastaGiro;
    private float desdeCampo, corte;
    private bool sinControlesAntes;

    private readonly List<Behaviour> apagados = new List<Behaviour>();
    private readonly List<Camera> camarasApagadas = new List<Camera>();
    private int mascaraOjos;

    // Marcas en pantalla (CA2)
    private GameObject lienzo;
    private RectTransform raiz;
    private Image propia;
    private readonly List<Image> marcas = new List<Image>();
    private readonly List<HealthSystem> objetivos = new List<HealthSystem>();
    private float proximaBusqueda;

    public override bool RecargaAlTerminar => true;
    // CA10: durante la vista, el reloj muestra el tiempo que queda.
    public override float ProgresoActivo => activa ? Mathf.Clamp01(1f - (Time.time - inicio) / Duracion) : -1f;
    public override string TextoActivo => activa ? Mathf.CeilToInt(Mathf.Max(0f, Duracion - (Time.time - inicio))).ToString() : null;

    private void Awake()
    {
        habilidad = GetComponent<PlayerAbility>();
        vida = GetComponent<HealthSystem>();
        movimiento = GetComponent<PlayerMovement>();
    }

    public override bool Aplicar()
    {
        if (activa || Ojos() == null) return false;
        Empezar();
        return true;
    }

    private void OnDisable() { if (activa) Terminar(); }
    private void OnDestroy() { if (activa) Terminar(); }

    private void Update()
    {
        if (!activa) return;
        bool fin = Time.time - inicio >= Duracion || (vida != null && vida.currentHealth <= 0) || !PuedeUsar();
        // CA3: la Q otra vez o el clic derecho (no en el mismo cuadro en que se activó).
        if (Time.frameCount > cuadroInicio && !PauseMenu.IsPaused && !ShopUI.IsOpen &&
            (KeyBindings.Down(GameAction.Habilidad) || KeyBindings.Down(GameAction.Apuntar))) fin = true;
        if (fin) { Terminar(); return; }

        // La cámara sube desde los ojos del jugador.
        float k = Mathf.Clamp01((Time.time - inicio) / Subida);
        float e = 1f - Mathf.Pow(1f - k, 3f);
        Vector3 lugar = Vector3.Lerp(desdeLugar, hastaLugar, e);
        aerea.transform.SetPositionAndRotation(lugar, Quaternion.Slerp(desdeGiro, hastaGiro, e));
        aerea.fieldOfView = Mathf.Lerp(desdeCampo, Campo, e);
        aerea.nearClipPlane = Mathf.Max(0.05f, lugar.y - corte); // sin techos
        Marcar();
    }

    // CA4: un golpe que de verdad le saca vida o escudo corta la vista (antes de que muera, si muere).
    private void AlRecibirDanio(HealthSystem quien, int danio, Vector3? origen)
    {
        if (activa && quien == vida) Terminar();
    }

    // ---------- Empezar y terminar ----------

    private void Empezar()
    {
        activa = Activa = true;
        inicio = Time.time;
        cuadroInicio = Time.frameCount;
        HealthSystem.Damaged += AlRecibirDanio;

        // Desde dónde y hasta dónde sube la cámara.
        Transform ojo = ojos.transform;
        float pies = transform.position.y;
        CharacterController cuerpo = GetComponent<CharacterController>();
        if (cuerpo != null) pies = transform.TransformPoint(cuerpo.center).y - cuerpo.height * 0.5f * transform.lossyScale.y;
        corte = pies + AlturaDeCorte;
        Bounds mapa = Limites(transform.position);
        float yaw = transform.eulerAngles.y;
        Quaternion giro = Quaternion.Euler(0f, yaw, 0f);
        Vector3 derecha = giro * Vector3.right, adelante = giro * Vector3.forward;
        Vector3 ext = mapa.extents;
        float mitadAncho = Mathf.Abs(derecha.x) * ext.x + Mathf.Abs(derecha.z) * ext.z;
        float mitadAlto = Mathf.Abs(adelante.x) * ext.x + Mathf.Abs(adelante.z) * ext.z;
        float t = Mathf.Tan(Campo * 0.5f * Mathf.Deg2Rad), aspecto = Mathf.Max(0.5f, (float)Screen.width / Mathf.Max(1, Screen.height));
        float distancia = Mathf.Max(mitadAlto / t, mitadAncho / (t * aspecto)) * 1.08f + 2f;
        desdeLugar = ojo.position;
        desdeGiro = ojo.rotation;
        desdeCampo = ojos.fieldOfView;
        hastaLugar = new Vector3(mapa.center.x, corte + distancia, mapa.center.z);
        hastaGiro = Quaternion.Euler(90f, yaw, 0f); // mirando abajo, con "adelante" hacia arriba de la pantalla

        var go = new GameObject("Vista aérea (cámara)");
        aerea = go.AddComponent<Camera>();
        aerea.depth = ojos.depth + 10f;
        aerea.clearFlags = CameraClearFlags.SolidColor;
        aerea.backgroundColor = new Color(0.04f, 0.05f, 0.07f);
        aerea.farClipPlane = distancia + 400f;
        aerea.nearClipPlane = 0.05f;
        aerea.fieldOfView = desdeCampo;
        go.transform.SetPositionAndRotation(desdeLugar, desdeGiro);

        // La cámara del jugador sigue (para todo lo que usa Camera.main) pero no dibuja nada, ni las armas.
        mascaraOjos = ojos.cullingMask;
        ojos.cullingMask = 0;
        camarasApagadas.Clear();
        foreach (Camera c in GetComponentsInChildren<Camera>())
            if (c != ojos && c.enabled) { c.enabled = false; camarasApagadas.Add(c); }

        // CA1: quieto, sin mirar, sin disparar ni cambiar de arma.
        apagados.Clear();
        foreach (Behaviour b in GetComponentsInChildren<Behaviour>())
            if (b.enabled && (b is CameraLook || b is WeaponSwitcher || b is Pistola || b is Mitre || b is ArmaDeFuego ||
                              b is MeleeAttack || b is GrenadeThrower || b is SoltarArmas))
            {
                b.enabled = false;
                apagados.Add(b);
            }
        foreach (ShopUI tienda in FindObjectsByType<ShopUI>(FindObjectsSortMode.None))
            if (tienda.enabled) { tienda.enabled = false; apagados.Add(tienda); }
        if (movimiento != null)
        {
            sinControlesAntes = movimiento.SinControles;
            movimiento.SinControles = true;
        }

        ArmarMarcas();
        SonidoVistaAerea.Activar();
        Cambio?.Invoke(transform, true);
    }

    private void Terminar()
    {
        if (!activa) return;
        activa = Activa = false;
        HealthSystem.Damaged -= AlRecibirDanio;

        if (aerea != null) Destroy(aerea.gameObject);
        if (ojos != null) ojos.cullingMask = mascaraOjos;
        foreach (Camera c in camarasApagadas) if (c != null) c.enabled = true;
        camarasApagadas.Clear();

        // Con la pausa abierta, lo que se devuelve se lo queda la pausa (lo prende al reanudar).
        foreach (Behaviour b in apagados)
            if (b != null && !PauseMenu.Hold(b)) b.enabled = true;
        apagados.Clear();
        if (movimiento != null)
        {
            movimiento.SinControles = sinControlesAntes;
            if (!sinControlesAntes && PauseMenu.IsPaused) PauseMenu.Hold(movimiento);
        }

        if (lienzo != null) Destroy(lienzo);
        lienzo = null;
        marcas.Clear();
        objetivos.Clear();

        SonidoVistaAerea.Terminar();
        Cambio?.Invoke(transform, false);
        if (habilidad != null) habilidad.TerminoEfecto(); // CA8
    }

    private Camera Ojos()
    {
        if (ojos == null)
            foreach (Camera c in GetComponentsInChildren<Camera>(true))
                if (c.CompareTag("MainCamera")) { ojos = c; break; }
        return ojos;
    }

    // ---------- Qué parte del mundo es el mapa ----------

    // Todo lo que tiene colisión menos los personajes y lo que se mueve, como el minimapa (US 194). Se calcula una vez
    // por escena; un piso enorme se recorta alrededor del jugador.
    private static Bounds Limites(Vector3 centro)
    {
        UnityEngine.SceneManagement.Scene escena = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (escena != escenaLimites)
        {
            escenaLimites = escena;
            hayLimites = false;
            foreach (Collider c in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (!c.enabled || c.isTrigger || c.attachedRigidbody != null || c.GetComponentInParent<HealthSystem>() != null ||
                    c.GetComponentInParent<CharacterController>() != null || c.GetComponentInParent<Barrera>() != null) continue;
                if (!hayLimites) { limitesCache = c.bounds; hayLimites = true; }
                else limitesCache.Encapsulate(c.bounds);
            }
        }
        if (!hayLimites) return new Bounds(centro, new Vector3(60f, 10f, 60f));
        Vector3 min = limitesCache.min, max = limitesCache.max;
        min.x = Mathf.Max(min.x, centro.x - MapaMaximo * 0.5f); max.x = Mathf.Min(max.x, centro.x + MapaMaximo * 0.5f);
        min.z = Mathf.Max(min.z, centro.z - MapaMaximo * 0.5f); max.z = Mathf.Min(max.z, centro.z + MapaMaximo * 0.5f);
        var b = new Bounds();
        b.SetMinMax(min, max);
        return b;
    }

    // ---------- Marcas de los rivales (CA2) ----------

    private void ArmarMarcas()
    {
        lienzo = new GameObject("Vista aérea (marcas)");
        Canvas canvas = lienzo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40; // debajo del HUD
        raiz = (RectTransform)lienzo.transform;
        propia = Marca(new Color(1f, 1f, 1f, 0.95f), 0.7f);
        proximaBusqueda = 0f;
    }

    private Image Marca(Color color, float tamanio)
    {
        var go = new GameObject("Marca", typeof(RectTransform));
        go.transform.SetParent(raiz, false);
        Image imagen = go.AddComponent<Image>();
        imagen.sprite = Circulo();
        imagen.color = color;
        imagen.raycastTarget = false;
        RectTransform r = imagen.rectTransform;
        r.anchorMin = r.anchorMax = Vector2.zero;
        float lado = Mathf.Max(14f, Screen.height * 0.024f) * tamanio;
        r.sizeDelta = new Vector2(lado, lado);
        return imagen;
    }

    private void Marcar()
    {
        if (raiz == null) return;
        if (Time.time >= proximaBusqueda)
        {
            proximaBusqueda = Time.time + 0.5f;
            objetivos.Clear();
            foreach (HealthSystem h in FindObjectsByType<HealthSystem>(FindObjectsSortMode.None))
                if (EsRival(h)) objetivos.Add(h);
        }

        int usadas = 0;
        Vector3 desde = aerea.transform.position;
        foreach (HealthSystem h in objetivos)
        {
            if (h == null || h.currentHealth <= 0) continue;
            Vector3 pecho = h.transform.position + Vector3.up * 1.1f;
            if (HumoGranada.TapaVista(pecho + Vector3.up * 30f, pecho) || HumoGranada.GrisEn(pecho) > 0.5f) continue; // CA2: el humo los tapa
            if (!Poner(usadas < marcas.Count ? marcas[usadas] : Agregar(), pecho)) continue;
            usadas++;
        }
        for (int i = usadas; i < marcas.Count; i++) marcas[i].enabled = false;
        Poner(propia, transform.position + Vector3.up * 1.1f);
    }

    private Image Agregar()
    {
        Image nueva = Marca(ColorRival, 1f);
        marcas.Add(nueva);
        return nueva;
    }

    private bool Poner(Image marca, Vector3 mundo)
    {
        Vector3 p = aerea.WorldToScreenPoint(mundo);
        bool ve = p.z > 0f;
        marca.enabled = ve;
        if (ve) marca.rectTransform.anchoredPosition = new Vector2(p.x, p.y);
        return ve;
    }

    // Los jugadores rivales (en línea) y los enemigos sin jugador (zombis, muñecos de práctica). Nunca uno mismo ni
    // los compañeros.
    private bool EsRival(HealthSystem h)
    {
        if (h == null || h.currentHealth <= 0 || h.transform.IsChildOf(transform) || transform.IsChildOf(h.transform)) return false;
        JugadorEnRed jugador = h.GetComponentInParent<JugadorEnRed>();
        if (jugador == null) return true;
        return jugador.Vivo && EquiposTacticos.EsRival(jugador.Actor);
    }

    private static Sprite Circulo()
    {
        if (circulo != null) return circulo;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixeles = new Color32[n * n];
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                // Relleno del color y un borde oscuro, así se ve sobre pisos claros y oscuros.
                float alfa = Mathf.Clamp01((1f - d) * c);
                byte v = d > 0.78f ? (byte)40 : (byte)255;
                pixeles[y * n + x] = new Color32(v, v, v, (byte)(alfa * 255f));
            }
        tex.SetPixels32(pixeles);
        tex.Apply();
        circulo = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return circulo;
    }
}
