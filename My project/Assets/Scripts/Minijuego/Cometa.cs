using UnityEngine;

public enum TipoCometa { Comun, Raro, Epico }

// Un cometa del minijuego (F21, US 156), armado por código: un núcleo brillante, un halo y una estela de su color.
// - CA2: cruza el espacio en línea recta, lejos de la base. Se enciende de a poco al principio del recorrido y se apaga
//   de a poco al final, y se ve más tenue cuanto más lejos está: así no se nota dónde aparece ni dónde desaparece.
// - CA4: color, tamaño, velocidad y puntos según el tipo (tabla de la historia). Deja una estela de su color.
// - CA5: al recibir un disparo estalla en chispas de su color y desaparece.
// - CA1: cuando se termina la compra se aleja y se apaga en 2,5 s.
// Lo crea MinijuegoCometas y existe solo en esta computadora (CA6).
public class Cometa : MonoBehaviour
{
    private struct Datos
    {
        public float diametro, velocidad, estela;
        public int puntos;
        public Color nucleo, color;
    }

    // Medidas en metros: pasan a 150-300 m de la base, así que son grandes para que se vean y se les pueda pegar.
    private static readonly Datos[] Tabla =
    {
        new Datos { diametro = 6f, velocidad = 49f, estela = 50f, puntos = 50,    // Común: azul, grande y lento
            nucleo = new Color(0.86f, 0.94f, 1f), color = new Color(0.25f, 0.55f, 1f) },
        new Datos { diametro = 4.5f, velocidad = 77f, estela = 65f, puntos = 100, // Raro: verde, mediano
            nucleo = new Color(0.88f, 1f, 0.9f), color = new Color(0.2f, 0.95f, 0.42f) },
        new Datos { diametro = 3.2f, velocidad = 112f, estela = 85f, puntos = 150, // Épico: dorado, chico y rápido
            nucleo = new Color(1f, 0.98f, 0.82f), color = new Color(1f, 0.72f, 0.15f) },
    };

    private const float Halo = 2.6f;           // el halo mide 2,6 veces el núcleo
    private const float RadioDeAcierto = 1f;   // radio del collider, en diámetros del núcleo (más generoso que lo que se ve)
    private const float DuracionHuida = 2.5f;  // CA1
    private const float Encendido = 0.2f;      // parte del recorrido en la que se enciende, y la del final en la que se apaga
    private const float LejosDesde = 380f, LejosHasta = 720f; // m de la cámara: desde ahí se va perdiendo en la distancia
    private const float MinimoParaAcertar = 0.3f; // casi apagado (recién aparece o ya se va) no se le puede pegar

    public static int PuntosDe(TipoCometa tipo) => Tabla[(int)tipo].puntos;
    public static float DiametroDe(TipoCometa tipo) => Tabla[(int)tipo].diametro;
    public static Color ColorDe(TipoCometa tipo) => Tabla[(int)tipo].color;

    public TipoCometa Tipo { get; private set; }
    public int Puntos => Tabla[(int)Tipo].puntos;
    public float Aparecio { get; private set; } // para el tiempo de reacción de cada acierto
    public bool Estallo { get; private set; }

    private Transform nucleo, halo;
    private SpriteRenderer vistaNucleo, vistaHalo;
    private Color colorNucleo, colorHalo;
    private SphereCollider esfera;
    private TrailRenderer estela;
    private Vector3 velocidad;
    private float recorrido, distancia, desfase, ancho, seVaDesde = -1f, estalloDesde;

    public static Cometa Crear(TipoCometa tipo, Vector3 desde, Vector3 direccion, float distancia, Transform padre)
    {
        Datos d = Tabla[(int)tipo];
        var go = new GameObject("Cometa " + tipo);
        go.transform.SetParent(padre, false);
        go.transform.SetPositionAndRotation(desde, Quaternion.LookRotation(direccion));
        var cometa = go.AddComponent<Cometa>();
        cometa.Tipo = tipo;
        cometa.Aparecio = Time.time;
        cometa.distancia = distancia;
        cometa.velocidad = direccion.normalized * d.velocidad;
        cometa.desfase = Random.value * 10f;
        cometa.Armar(d);

        // Se le pega en una esfera algo más grande que el núcleo. Es un trigger: las balas del combate no lo tocan, y
        // WeaponFire lo busca a propósito durante la compra (US 157).
        var esfera = go.AddComponent<SphereCollider>();
        esfera.isTrigger = true;
        // AddComponent intenta ajustar el collider a los renderers; la estela recién creada puede incluir
        // el origen del mundo en sus bounds. El blanco siempre debe estar centrado en el núcleo.
        esfera.center = Vector3.zero;
        esfera.radius = d.diametro * RadioDeAcierto;
        esfera.enabled = false; // arranca apagado: se enciende de a poco (Update)
        cometa.esfera = esfera;
        var cuerpo = go.AddComponent<Rigidbody>();
        cuerpo.isKinematic = true;
        cuerpo.useGravity = false;
        return cometa;
    }

    /// <summary>CA5: recibió un disparo. Estalla en chispas y se apaga; la estela se termina de borrar sola.</summary>
    public void Acertado(Vector3 punto)
    {
        if (Estallo) return;
        Estallo = true;
        estalloDesde = Time.time;
        esfera.enabled = false;
        nucleo.gameObject.SetActive(false);
        halo.gameObject.SetActive(false);
        estela.emitting = false;
        Datos d = Tabla[(int)Tipo];
        Chispas.Soltar(transform.position, d.color, d.nucleo, d.diametro, velocidad * 0.25f, transform.parent);
    }

    /// <summary>CA1: se termina la fase de compra; el que sigue pasando se aleja y se apaga.</summary>
    public void Irse()
    {
        if (!Estallo && seVaDesde < 0f) seVaDesde = Time.time;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (Estallo)
        {
            // Se espera a que la estela termine de borrarse para no cortarla de golpe.
            if (Time.time - estalloDesde >= estela.time) Destroy(gameObject);
            return;
        }

        float brillo = 1f;
        if (seVaDesde >= 0f)
        {
            float huida = Time.time - seVaDesde;
            if (huida >= DuracionHuida) { Destroy(gameObject); return; }
            velocidad += velocidad.normalized * (40f * dt);
            brillo = 1f - huida / DuracionHuida;
        }
        transform.position += velocidad * dt;
        recorrido += velocidad.magnitude * dt;
        if (recorrido >= distancia) { Destroy(gameObject); return; }

        // CA2: se enciende y se apaga en las puntas del recorrido, y se pierde en la distancia.
        float visible = Visibilidad();
        esfera.enabled = visible >= MinimoParaAcertar;

        // Titila apenas, como algo que arde.
        float titileo = 1f + 0.08f * Mathf.Sin((Time.time + desfase) * 23f) + 0.05f * Mathf.Sin((Time.time + desfase) * 37f);
        float tamano = Mathf.Lerp(0.35f, 1f, visible);
        nucleo.localScale = Vector3.one * (ancho * titileo * tamano * Mathf.Max(0.2f, brillo));
        halo.localScale = Vector3.one * (ancho * Halo * titileo * tamano * brillo);
        vistaNucleo.color = new Color(colorNucleo.r, colorNucleo.g, colorNucleo.b, colorNucleo.a * visible);
        vistaHalo.color = new Color(colorHalo.r, colorHalo.g, colorHalo.b, colorHalo.a * visible);
        estela.widthMultiplier = ancho * 0.9f * brillo * visible;
    }

    // 0 (no se ve) a 1: sube al principio del recorrido, baja al final, y baja con la distancia a la cámara.
    private float Visibilidad()
    {
        float avance = Mathf.Clamp01(recorrido / Mathf.Max(1f, distancia));
        float puntas = Mathf.SmoothStep(0f, 1f, avance / Encendido) * Mathf.SmoothStep(0f, 1f, (1f - avance) / Encendido);
        Camera camara = Camera.main;
        if (camara == null) return puntas;
        float lejos = Vector3.Distance(camara.transform.position, transform.position);
        return puntas * (1f - Mathf.SmoothStep(0f, 1f, (lejos - LejosDesde) / (LejosHasta - LejosDesde)));
    }

    // Los brillos son planos que miran siempre a la cámara.
    private void LateUpdate()
    {
        Camera camara = Camera.main;
        if (camara == null || Estallo) return;
        Quaternion mira = camara.transform.rotation;
        nucleo.rotation = mira;
        halo.rotation = mira;
    }

    // ---------- Modelo ----------

    private void Armar(Datos d)
    {
        ancho = d.diametro;
        colorHalo = new Color(d.color.r, d.color.g, d.color.b, 0.55f);
        colorNucleo = d.nucleo;
        vistaHalo = Brillo("Halo", transform, new Color(colorHalo.r, colorHalo.g, colorHalo.b, 0f), 0);
        vistaNucleo = Brillo("Núcleo", transform, new Color(colorNucleo.r, colorNucleo.g, colorNucleo.b, 0f), 1);
        halo = vistaHalo.transform;
        nucleo = vistaNucleo.transform;

        // CA4: la estela, del color del cometa, se afina y se apaga hacia atrás.
        estela = gameObject.AddComponent<TrailRenderer>();
        estela.sharedMaterial = Material();
        estela.time = d.estela / d.velocidad;
        estela.minVertexDistance = 1.5f;
        estela.numCapVertices = 4;
        estela.alignment = LineAlignment.View;
        estela.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        estela.receiveShadows = false;
        estela.widthMultiplier = d.diametro * 0.9f;
        estela.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.25f, 0.6f), new Keyframe(1f, 0f));
        var degradado = new Gradient();
        degradado.SetKeys(
            new[] { new GradientColorKey(d.nucleo, 0f), new GradientColorKey(d.color, 0.15f), new GradientColorKey(d.color * 0.6f, 1f) },
            new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.5f, 0.35f), new GradientAlphaKey(0f, 1f) });
        estela.colorGradient = degradado;
    }

    /// <summary>Un brillo redondo (billboard) del color dado. orden: el de mayor número se dibuja encima.</summary>
    public static SpriteRenderer Brillo(string nombre, Transform padre, Color color, int orden)
    {
        var go = new GameObject(nombre, typeof(SpriteRenderer));
        go.transform.SetParent(padre, false);
        var vista = go.GetComponent<SpriteRenderer>();
        vista.sprite = orden > 0 ? Nitido() : Suave();
        vista.sharedMaterial = Material();
        vista.color = color;
        vista.sortingOrder = orden;
        vista.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        vista.receiveShadows = false;
        return vista;
    }

    // ---------- Recursos compartidos ----------

    private static Material material;
    private static Sprite suave, nitido;

    // Sprites/Default está siempre incluido en el build (Graphics Settings) y no depende de la luz: los cometas brillan.
    private static Material Material()
    {
        if (material != null) return material;
        material = new Material(Shader.Find("Sprites/Default")) { name = "Cometa" };
        return material;
    }

    private static Sprite Suave() => suave != null ? suave : suave = Circulo("Brillo suave", false);
    private static Sprite Nitido() => nitido != null ? nitido : nitido = Circulo("Brillo nítido", true);

    // Un círculo blanco que se desvanece hacia el borde. El nítido tiene el centro lleno y el borde corto.
    private static Sprite Circulo(string nombre, bool definido)
    {
        const int Lado = 64;
        var textura = new Texture2D(Lado, Lado, TextureFormat.RGBA32, false) { name = nombre, wrapMode = TextureWrapMode.Clamp };
        var pixeles = new Color32[Lado * Lado];
        for (int y = 0; y < Lado; y++)
            for (int x = 0; x < Lado; x++)
            {
                float dx = (x + 0.5f) / Lado * 2f - 1f, dy = (y + 0.5f) / Lado * 2f - 1f;
                float r = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                float a = definido
                    ? Mathf.Clamp01((0.7f - r) * 6f) + 0.4f * (1f - r) * (1f - r)
                    : Mathf.Pow(1f - r, 2.2f);
                pixeles[y * Lado + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
            }
        textura.SetPixels32(pixeles);
        textura.Apply(false, true);
        return Sprite.Create(textura, new Rect(0, 0, Lado, Lado), new Vector2(0.5f, 0.5f), Lado);
    }
}
