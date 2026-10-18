using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// Mapa 1: decoración. Cada tanto pasan cazas (Sparrow Fighter) volando por arriba del mapa, solos o en formación, de
// punta a punta y a buena altura, con un zumbido que se acerca y se aleja. No chocan con nada ni afectan la partida; cada
// computadora tira los suyos (es solo ambiente, no hace falta que coincidan).
public class NavesDePaso : MonoBehaviour
{
    [Tooltip("Prefabs de las naves (se elige una al azar por pasada).")]
    public GameObject[] naves;
    [Tooltip("Giro para que la trompa del modelo mire hacia adelante (+Z), si el modelo viene girado.")]
    public Vector3 giroDelModelo = Vector3.zero;
    public float escala = 1f;

    [Header("Cuándo")]
    public float esperaMin = 10f, esperaMax = 25f;
    [Range(1, 5)] public int formacionMax = 3;

    [Header("Cómo")]
    public float alturaMin = 35f, alturaMax = 70f;   // m sobre este objeto
    public float velocidadMin = 55f, velocidadMax = 85f;
    public float largo = 500f;                        // m de punta a punta de la pasada
    public float desvio = 60f;                        // m que la pasada puede correrse del centro
    public float inclinacion = 18f;                   // grados de alabeo en las curvas suaves

    [Header("Estela de los propulsores")]
    public bool estela = true;
    public Material materialEstela;                   // vacío: uno simple que se arma solo
    [ColorUsage(true, true)] public Color colorEstela = new Color(0.35f, 0.85f, 1f) * 2.5f;
    public float anchoEstela = 0.7f;
    public float duracionEstela = 0.9f;               // s que tarda en desvanecerse
    [Tooltip("Dónde están los propulsores, en el espacio de la nave. Vacío: se calculan solos (dos atrás, a los costados).")]
    public Vector3[] propulsores = new Vector3[0];

    [Header("Sonido")]
    public AudioClip sonido;                          // vacío: se genera un zumbido de motor
    [Range(0f, 1f)] public float volumen = 0.6f;
    public float alcanceSonido = 160f;

    private float proxima;
    private static AudioClip generado;

    private void Start() => proxima = Time.time + Random.Range(3f, esperaMin);

    private void Update()
    {
        if (naves == null || naves.Length == 0 || Time.time < proxima) return;
        proxima = Time.time + Random.Range(esperaMin, esperaMax);
        Pasada();
    }

    private const float Paso = 8f;        // m entre muestras del recorrido
    private const float Margen = 18f;     // m por encima de lo más alto que haya abajo
    private const float Anticipo = 120f;  // m antes de un obstáculo en que ya empieza a subir

    private readonly List<Bounds> grandes = new List<Bounds>();
    private bool listaHecha;

    // Lo alto del mapa que no tiene colisión (naves grandes como el USS Virginia, edificios de adorno): sus cajas.
    private void ArmarObstaculos()
    {
        listaHecha = true;
        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (r.GetComponentInParent<Vuelo>() != null || r.GetComponentInParent<Terrain>() != null) continue;
            Bounds b = r.bounds;
            if (b.size.x < 6f && b.size.z < 6f && b.size.y < 6f) continue; // lo chico no importa
            if (!r.enabled || b.min.y > 300f || b.size.x > 600f || b.size.z > 600f) continue; // invisibles, cielo y fondos gigantes
            grandes.Add(b);
        }
    }

    // Qué tan alto hay algo en ese punto: terreno, colisiones y cajas de lo grande.
    private float Techo(Vector3 p)
    {
        float alto = float.NegativeInfinity;
        foreach (Terrain t in Terrain.activeTerrains) alto = Mathf.Max(alto, t.SampleHeight(p) + t.transform.position.y);
        if (Physics.Raycast(new Vector3(p.x, 2000f, p.z), Vector3.down, out RaycastHit golpe, 4000f, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
            alto = Mathf.Max(alto, golpe.point.y);
        foreach (Bounds b in grandes)
            if (p.x > b.min.x - 10f && p.x < b.max.x + 10f && p.z > b.min.z - 10f && p.z < b.max.z + 10f)
                alto = Mathf.Max(alto, b.max.y);
        return alto;
    }

    private void Pasada()
    {
        if (!listaHecha) ArmarObstaculos();
        float angulo = Random.Range(0f, 360f);
        Vector3 dir = Quaternion.Euler(0f, angulo, 0f) * Vector3.forward;
        Vector3 lado = Vector3.Cross(Vector3.up, dir);
        float baseY = transform.position.y + Random.Range(alturaMin, alturaMax);
        Vector3 inicio = transform.position + lado * Random.Range(-desvio, desvio) - dir * largo * 0.5f;
        inicio.y = 0f;

        // El perfil de altura del recorrido: nunca por debajo de lo que hay abajo (más un margen, y un poco más ancho
        // por la formación), y subiendo con tiempo antes de las montañas o las naves grandes.
        int n = Mathf.CeilToInt(largo / Paso) + 1;
        var piso = new float[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 p = inicio + dir * (i * Paso);
            float t = Mathf.Max(Techo(p), Techo(p + lado * 12f), Techo(p - lado * 12f));
            piso[i] = Mathf.Max(baseY, t + Margen);
        }
        int adelante = Mathf.CeilToInt(Anticipo / Paso);
        var perfil = new float[n];
        for (int i = 0; i < n; i++)
        {
            float m = piso[i];
            for (int k = 1; k <= adelante; k++)
            {
                // Lo de adelante pesa menos cuanto más lejos está: sube de a poco, no de golpe.
                float peso = 1f - k / (float)(adelante + 1);
                if (i + k < n) m = Mathf.Max(m, Mathf.Lerp(baseY, piso[i + k], peso));
                if (i - k >= 0) m = Mathf.Max(m, Mathf.Lerp(baseY, piso[i - k], peso)); // y baja de a poco después
            }
            perfil[i] = m;
        }

        float vel = Random.Range(velocidadMin, velocidadMax);
        float alabeo = Random.Range(-1f, 1f) * inclinacion;
        GameObject modelo = naves[Random.Range(0, naves.Length)];
        int cuantas = Random.Range(1, formacionMax + 1);
        for (int i = 0; i < cuantas; i++)
        {
            // Formación en V: la primera adelante y las demás atrás a los costados.
            int fila = (i + 1) / 2, ala = i % 2 == 1 ? -1 : 1;
            Vector3 corrida = -dir * fila * 12f + lado * ala * fila * 10f;
            var nave = Instantiate(modelo, inicio + corrida + Vector3.up * perfil[0], Quaternion.LookRotation(dir) * Quaternion.Euler(giroDelModelo));
            nave.name = "Nave de paso";
            nave.transform.localScale *= escala;
            foreach (Collider c in nave.GetComponentsInChildren<Collider>()) Destroy(c);
            if (estela) Estelas(nave, dir, lado);
            nave.AddComponent<Vuelo>().Iniciar(inicio + corrida, dir, vel, perfil, Paso, fila * 1.5f, alabeo, giroDelModelo);
            if (i == 0) Sonar(nave);
        }
    }

    // Una estela celeste por propulsor: atrás de la nave, que se desvanece en poco tiempo.
    private void Estelas(GameObject nave, Vector3 dir, Vector3 lado)
    {
        var lugares = new List<Vector3>();
        if (propulsores != null && propulsores.Length > 0)
            foreach (Vector3 p in propulsores) lugares.Add(nave.transform.TransformPoint(p));
        else
        {
            // Con la nave recién puesta (mirando a dir, sin alabeo): la cola de su caja, a los dos costados.
            Bounds b = default;
            bool hay = false;
            foreach (Renderer r in nave.GetComponentsInChildren<Renderer>())
            {
                if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
            }
            if (!hay) return;
            Vector3 abs = new Vector3(Mathf.Abs(dir.x), Mathf.Abs(dir.y), Mathf.Abs(dir.z));
            Vector3 absLado = new Vector3(Mathf.Abs(lado.x), Mathf.Abs(lado.y), Mathf.Abs(lado.z));
            float largoMedio = Vector3.Dot(b.extents, abs), anchoMedio = Vector3.Dot(b.extents, absLado);
            Vector3 cola = b.center - dir * largoMedio * 0.92f;
            lugares.Add(cola + lado * anchoMedio * 0.22f);
            lugares.Add(cola - lado * anchoMedio * 0.22f);
        }

        Material mat = MaterialEstela();
        var degradado = new Gradient();
        degradado.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, // el color lo da el material
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
        foreach (Vector3 lugar in lugares)
        {
            var go = new GameObject("Estela");
            go.transform.SetParent(nave.transform, true);
            go.transform.position = lugar;
            TrailRenderer t = go.AddComponent<TrailRenderer>();
            t.sharedMaterial = mat;
            t.colorGradient = degradado;
            t.time = duracionEstela;
            t.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            t.widthMultiplier = anchoEstela * escala;
            t.minVertexDistance = 0.5f;
            t.numCapVertices = 2;
            t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            t.receiveShadows = false;
        }
    }

    private static Material estelaPorDefecto;

    private Material MaterialEstela()
    {
        if (materialEstela != null) return materialEstela;
        if (estelaPorDefecto == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            estelaPorDefecto = new Material(sh) { name = "Estela (generada)" };
            ConfigurarEstela(estelaPorDefecto, colorEstela);
        }
        return estelaPorDefecto;
    }

    /// <summary>Material aditivo (brilla, sin sombras) del color dado, para las estelas.</summary>
    public static void ConfigurarEstela(Material m, Color color)
    {
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f);           // aditivo
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = 3000;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        else if (m.HasProperty("_Color")) m.SetColor("_Color", color);
    }

    private void Sonar(GameObject nave)
    {
        AudioClip clip = sonido != null ? sonido : Zumbido();
        var fuente = nave.AddComponent<AudioSource>();
        fuente.clip = clip;
        fuente.loop = true;
        fuente.spatialBlend = 1f;
        fuente.rolloffMode = AudioRolloffMode.Linear;
        fuente.minDistance = 15f;
        fuente.maxDistance = alcanceSonido;
        fuente.dopplerLevel = 1.2f;
        fuente.volume = volumen;
        Pistola pistola = FindAnyObjectByType<Pistola>(FindObjectsInactive.Include);
        if (pistola != null) fuente.outputAudioMixerGroup = pistola.sfxGroup; // volúmenes de Opciones
        fuente.Play();
    }

    // Un zumbido de turbina: ruido grave filtrado con un silbido agudo encima, en bucle.
    private static AudioClip Zumbido()
    {
        if (generado != null) return generado;
        const int f = 44100;
        int n = f * 2;
        var datos = new float[n];
        var azar = new System.Random(5);
        float grave = 0f, medio = 0f, fase = 0f;
        for (int i = 0; i < n; i++)
        {
            float r = (float)azar.NextDouble() * 2f - 1f;
            grave += (r - grave) * 0.02f;
            medio += (r - medio) * 0.15f;
            fase += 2f * Mathf.PI * 1850f / f;
            float k = i / (float)n;
            float borde = Mathf.Min(1f, Mathf.Min(k, 1f - k) * 40f); // sin clic al repetirse
            datos[i] = borde * (grave * 2.2f + medio * 0.35f + Mathf.Sin(fase) * 0.04f);
        }
        generado = AudioClip.Create("Zumbido de nave (generado)", n, 1, f, false);
        generado.SetData(datos, 0);
        return generado;
    }

    // Mueve una nave de paso por su recorrido (recto, con la altura del perfil) y la borra al terminar.
    private class Vuelo : MonoBehaviour
    {
        private Vector3 inicio, dir;
        private float vel, paso, extra, alabeo, recorrido;
        private float[] perfil;
        private Quaternion ajuste;

        public void Iniciar(Vector3 inicio, Vector3 dir, float vel, float[] perfil, float paso, float extra, float alabeo,
            Vector3 giroDelModelo)
        {
            this.inicio = inicio;
            this.dir = dir;
            this.vel = vel;
            this.perfil = perfil;
            this.paso = paso;
            this.extra = extra;
            this.alabeo = alabeo;
            ajuste = Quaternion.Euler(giroDelModelo);
        }

        private float Altura(float s)
        {
            float f = Mathf.Clamp(s / paso, 0f, perfil.Length - 1.001f);
            int i = (int)f;
            return Mathf.Lerp(perfil[i], perfil[i + 1], f - i) + extra;
        }

        private void Update()
        {
            recorrido += vel * Time.deltaTime;
            float y = Altura(recorrido);
            transform.position = inicio + dir * recorrido + Vector3.up * y;
            // Cabecea según si sube o baja, y un alabeo suave que va y viene.
            float pendiente = (Altura(recorrido + 5f) - y) / 5f;
            float cabeceo = -Mathf.Atan(pendiente) * Mathf.Rad2Deg;
            float ala = alabeo * Mathf.Sin(recorrido * 0.01f);
            transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(cabeceo, 0f, ala) * ajuste;
            if (recorrido >= (perfil.Length - 1) * paso) Destroy(gameObject);
        }
    }
}
