using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// F29 (Modo Invasión+), US 394: la Escolta, el dron de la carta legendaria. Lo arma EscoltaDeInvasion: el del jugador de
// esta computadora (Propio: busca a quién atacar y hace el daño) y, en cooperativo, los de los compañeros (solo se ven:
// el rayo y los arcos llegan por la red).
// - CA2: flota alrededor del jugador, cambiando de lugar cada tanto, y lo sigue con un leve retraso, sin atravesar paredes; no se puede destruir ni choca con nada
//   (no tiene colliders) y sigue atacando con el jugador caído.
// - CA3: el rayo del ojo se engancha al invasor más cercano a la vista (hasta Alcance) y no lo suelta hasta que muere,
//   sale de la vista o se aleja; hace RayoDanio por segundo, que sube hasta RayoDanioMaximo en RayoCarga segundos sobre
//   el mismo invasor (al cambiar de objetivo vuelve a empezar). El daño va de a golpes cada RayoCada segundos.
// - CA4: cada ArcosCada segundos, arcos eléctricos desde las dos luces de abajo del ojo (sin cortar el rayo) al grupo
//   con más invasores a su alcance: saltan entre hasta ArcosSaltos invasores a menos de ArcosEntre metros entre sí, con
//   ArcosDanio a cada uno y ArcosParaliza segundos aturdidos (Npc.Aturdir), salvo el Jefe. Sin nadie a tiro, espera.
// - CA5: el daño lo hace esta computadora como cualquier disparo (HealthSystem.TakeDamage; a la copia de un invasor se
//   le manda al anfitrión con este jugador como atacante), así las bajas son del dueño: puntos, estadísticas y cartas.
// - CA7: esfera blanca con las franjas celestes (los materiales de Resources/Escolta), el Sol de Mayo en la tapa de atrás
//   (el propulsor: brilla, ilumina apenas y larga una llama corta, más cuanto más rápido va) y una
//   banderita argentina en una antena; el ojo brilla más mientras el rayo carga.
public class DronEscolta : MonoBehaviour
{
    public const float Alcance = 20f;                 // CA3 y CA4 (m)
    public const float RayoDanio = 40f, RayoDanioMaximo = 80f, RayoCarga = 3f, RayoCada = 0.5f; // CA3
    public const float ArcosCada = 8f, ArcosEntre = 6f, ArcosParaliza = 1.5f;                  // CA4
    public const int ArcosDanio = 35, ArcosSaltos = 4;                                           // CA4
    public const float Diametro = 0.35f;              // CA7 (m)
    public const float LuzDelPropulsor = 0.12f, RangoDeLaLuz = 0.6f; // CA7: la luz dorada del Sol de Mayo (intensidad y m)
    public const float AnchoDelRayo = 0.07f, AnchoDelRayoMaximo = 0.22f; // CA3 (m): al engancharse y con toda la carga
    private const float BuscarCada = 0.2f;
    // CA2: cada cuánto cambia de lugar alrededor del jugador (s), cuánto tarda en llegar (s) y su velocidad máxima (m/s).
    private const float CambiaLugarMin = 2.5f, CambiaLugarMax = 5f, Suavidad = 0.55f, Rapidez = 9f;

    public static readonly Color ColorElectrico = new Color(0.35f, 0.7f, 1f);
    private static readonly Color Celeste = new Color(0.455f, 0.675f, 0.875f);
    private static readonly Color Oro = new Color(0.96f, 0.7f, 0.06f);

    // Dónde están el ojo y las luces en el modelo (Resources/Escolta/Escolta.obj: 1 m de diámetro, el ojo mira a +Z).
    private static readonly Vector3 Ojo = new Vector3(0f, 0.01f, 0.47f), Luces = new Vector3(0f, -0.32f, 0.47f);
    private static readonly Vector3 TapaDeAtras = new Vector3(0f, 0.01f, -0.475f), Antena = new Vector3(0.17f, 0.44f, -0.17f);

    /// <summary>El de esta computadora: elige los objetivos y hace el daño.</summary>
    public bool Propio { get; private set; }
    /// <summary>Al lado de quién va (el jugador, o la copia de un compañero).</summary>
    public Transform Sigue { get; set; }
    /// <summary>Hacia dónde mira el jugador (la cámara, en el propio; null: hacia donde mira Sigue).</summary>
    public Transform Mira { get; set; }

    /// <summary>CA3: el rayo cambió de objetivo (el invasor, o null si se apagó). Lo manda EscoltaDeInvasion por la red.</summary>
    public System.Action<Npc> CambioElRayo;
    /// <summary>CA4: salieron los arcos (los puntos: de las luces al primero y de invasor en invasor).</summary>
    public System.Action<Vector3[]> SalieronArcos;

    private Transform modelo, bandera;
    private Material materialSol, materialLlama;
    private LineRenderer llama;
    private Light luzDelSol;
    private Material materialOjo;
    private LineRenderer rayo, nucleo;
    private Material materialRayo, materialNucleo;
    private Vector3 velocidad, alrededor = new Vector3(0.9f, 0.4f, -0.6f);
    private float cambiaLugarEn, cambiaMiradaEn, tapadoDesde = -1f;
    private static readonly RaycastHit[] choques = new RaycastHit[16];
    private Vector3 mirada = Vector3.forward;
    private float aparecio;

    private Npc objetivo;
    private float enganchadoDesde, proximaBusqueda, acumulado, proximoGolpe, arcosListosEn;
    private readonly List<Npc> candidatos = new List<Npc>();
    private static readonly RaycastHit[] golpes = new RaycastHit[16];

    /// <summary>Arma un dron (el del jugador de esta computadora si propio) al lado de sigue.</summary>
    public static DronEscolta Crear(Transform sigue, Transform mira, bool propio)
    {
        var go = new GameObject(propio ? "Escolta (US 394)" : "Escolta de un compañero (US 394)");
        DronEscolta d = go.AddComponent<DronEscolta>();
        d.Propio = propio;
        d.Sigue = sigue;
        d.Mira = mira;
        d.Armar();
        if (sigue != null) go.transform.position = d.Lugar();
        d.arcosListosEn = Time.time + ArcosCada;
        return d;
    }

    // ---------- CA7: cómo se ve ----------

    private void Armar()
    {
        aparecio = Time.time;
        modelo = new GameObject("Modelo").transform;
        modelo.SetParent(transform, false);
        modelo.localScale = Vector3.one * Diametro;
        GameObject molde = Resources.Load<GameObject>("Escolta/Escolta");
        if (molde != null)
        {
            GameObject cuerpo = Instantiate(molde, modelo, false);
            cuerpo.transform.localPosition = Vector3.zero;
            cuerpo.transform.localRotation = Quaternion.identity;
            foreach (Collider c in cuerpo.GetComponentsInChildren<Collider>(true)) Destroy(c);
            PonerMateriales(cuerpo);
            // El .obj mide 1 m de diámetro; por si se importó con otra escala, se lo lleva a Diametro.
            Bounds caja = default;
            bool hay = false;
            foreach (Renderer r in cuerpo.GetComponentsInChildren<Renderer>(true))
            {
                if (!hay) { caja = r.bounds; hay = true; } else caja.Encapsulate(r.bounds);
            }
            float mayor = hay ? Mathf.Max(caja.size.x, caja.size.y, caja.size.z) : 0f;
            if (mayor > 0.0001f) cuerpo.transform.localScale *= Diametro / mayor;
        }
        else
        {
            // Sin el modelo, una esfera blanca (para no quedarse sin dron).
            GameObject esfera = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(esfera.GetComponent<Collider>());
            esfera.transform.SetParent(modelo, false);
            esfera.GetComponent<Renderer>().sharedMaterial = Lit(Color.white, null, 0.5f, 0.6f);
        }
        materialSol = SolDeMayo(modelo, TapaDeAtras, 0.15f, Vector3.back);
        ArmarPropulsor();
        ArmarBandera();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
    }

    // Los materiales del modelo (Resources/Escolta: URP Lit con sus texturas, normales y brillo), por el nombre de cada
    // parte en el .obj (Main, Back, Eye_Detail, Eye, Lights, Inside). El del ojo es una copia: su brillo cambia (CA7).
    private void PonerMateriales(GameObject cuerpo)
    {
        Material casco = Cargar("EscoltaCasco", Color.white), tapa = Cargar("EscoltaTapa", new Color(0.85f, 0.85f, 0.87f));
        Material aro = Cargar("EscoltaAro", new Color(0.9f, 0.9f, 0.9f)), luces = Cargar("EscoltaLuces", new Color(0.7f, 0.85f, 1f));
        Material adentro = Cargar("EscoltaAdentro", new Color(0.12f, 0.12f, 0.13f));
        materialOjo = new Material(Cargar("EscoltaOjo", new Color(0.45f, 0.65f, 0.95f)));
        Brillar(materialOjo, ColorElectrico * 1.2f);
        string[] orden = { "Main", "Inside", "Eye", "Lights", "Eye_Detail", "Back" };
        foreach (Renderer r in cuerpo.GetComponentsInChildren<Renderer>(true))
        {
            Material[] actuales = r.sharedMaterials;
            var nuevos = new Material[actuales.Length];
            for (int i = 0; i < actuales.Length; i++)
            {
                string parte = actuales[i] != null ? actuales[i].name : "";
                if (parte.Contains("Detail")) nuevos[i] = aro;
                else if (parte.Contains("Eye")) nuevos[i] = materialOjo;
                else if (parte.Contains("Light")) nuevos[i] = luces;
                else if (parte.Contains("Inside")) nuevos[i] = adentro;
                else if (parte.Contains("Back")) nuevos[i] = tapa;
                else if (parte.Contains("Main")) nuevos[i] = casco;
                else
                {
                    Debug.LogWarning("Escolta: parte sin material conocido: '" + parte + "' (" + i + ")");
                    string porOrden = i < orden.Length ? orden[i] : "Main";
                    nuevos[i] = porOrden == "Inside" ? adentro : porOrden == "Eye" ? materialOjo : porOrden == "Lights" ? luces
                              : porOrden == "Eye_Detail" ? aro : porOrden == "Back" ? tapa : casco;
                }
            }
            r.sharedMaterials = nuevos;
        }
    }

    // Un material de Resources/Escolta (o, si falta, uno liso de ese color).
    private static Material Cargar(string nombre, Color color)
    {
        Material m = Resources.Load<Material>("Escolta/" + nombre);
        return m != null ? m : Lit(color, null, 0.5f, 0.6f);
    }

    private static Material Lit(Color color, Texture textura, float metal, float liso)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var m = new Material(shader) { name = "Escolta" };
        m.color = color;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (textura != null)
        {
            m.mainTexture = textura;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", textura);
        }
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", liso);
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f); // de los dos lados (el sol y la bandera son planos)
        return m;
    }

    private static void Brillar(Material m, Color color)
    {
        if (m == null || !m.HasProperty("_EmissionColor")) return;
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        m.SetColor("_EmissionColor", color);
    }

    // El Sol de Mayo: un círculo con 32 rayos (rectos y ondulados alternados, los ondulados más cortos), de radio
    // radio, en lugar, mirando hacia frente.
    private static Material SolDeMayo(Transform padre, Vector3 lugar, float radio, Vector3 frente)
    {
        var go = new GameObject("Sol de Mayo");
        go.transform.SetParent(padre, false);
        go.transform.localPosition = lugar;
        go.transform.localRotation = Quaternion.LookRotation(-frente, Vector3.up); // la cara (+Z del plano) hacia afuera
        var vertices = new List<Vector3>();
        var triangulos = new List<int>();
        float centro = radio * 0.42f;
        const int Lados = 24;
        vertices.Add(Vector3.zero);
        for (int i = 0; i < Lados; i++)
        {
            float a = i * Mathf.PI * 2f / Lados;
            vertices.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * centro);
        }
        for (int i = 0; i < Lados; i++) { triangulos.Add(0); triangulos.Add(1 + i); triangulos.Add(1 + (i + 1) % Lados); }
        for (int i = 0; i < 32; i++)
        {
            float a = i * Mathf.PI / 16f, ancho = i % 2 == 0 ? 0.09f : 0.05f;
            float largo = i % 2 == 0 ? radio : radio * 0.8f;
            float torcido = i % 2 == 0 ? 0f : 0.12f; // los ondulados, un poco torcidos
            int b = vertices.Count;
            vertices.Add(new Vector3(Mathf.Cos(a - ancho), Mathf.Sin(a - ancho), 0f) * centro * 0.95f);
            vertices.Add(new Vector3(Mathf.Cos(a + ancho), Mathf.Sin(a + ancho), 0f) * centro * 0.95f);
            vertices.Add(new Vector3(Mathf.Cos(a + torcido), Mathf.Sin(a + torcido), 0f) * largo);
            triangulos.Add(b); triangulos.Add(b + 1); triangulos.Add(b + 2);
        }
        var malla = new Mesh { name = "Sol de Mayo" };
        malla.SetVertices(vertices);
        malla.SetTriangles(triangulos, 0);
        malla.RecalculateNormals();
        malla.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = malla;
        Material oro = Lit(Oro, null, 0.8f, 0.55f);
        Brillar(oro, Oro * 0.25f);
        go.AddComponent<MeshRenderer>().sharedMaterial = oro;
        return oro;
    }

    // CA7: el Sol de Mayo de atrás es el propulsor: brilla, ilumina apenas alrededor (una luz dorada chica y débil) y
    // larga una llama corta hacia atrás. Todo late suave y crece cuanto más rápido va (Propulsar).
    private void ArmarPropulsor()
    {
        var luz = new GameObject("Luz del propulsor");
        luz.transform.SetParent(modelo, false);
        luz.transform.localPosition = TapaDeAtras + Vector3.back * 0.25f;
        luzDelSol = luz.AddComponent<Light>();
        luzDelSol.type = LightType.Point;
        luzDelSol.color = new Color(1f, 0.72f, 0.25f);
        luzDelSol.range = RangoDeLaLuz;
        luzDelSol.intensity = LuzDelPropulsor;
        luzDelSol.shadows = LightShadows.None;

        var go = new GameObject("Llama del propulsor");
        go.transform.SetParent(modelo, false);
        llama = go.AddComponent<LineRenderer>();
        llama.positionCount = 2;
        llama.useWorldSpace = true;
        llama.shadowCastingMode = ShadowCastingMode.Off;
        llama.receiveShadows = false;
        llama.numCapVertices = 4;
        materialLlama = Rayo.Material(new Color(1f, 0.65f, 0.15f));
        if (materialLlama.HasProperty("_Color")) materialLlama.SetColor("_Color", new Color(1f, 0.65f, 0.15f, 1f) * 0.9f); // suave, sin encandilar
        llama.sharedMaterial = materialLlama;
    }

    private void Propulsar()
    {
        if (llama == null || luzDelSol == null) return;
        float rapido = Mathf.Clamp01(velocidad.magnitude / 3f);
        float late = 0.85f + 0.15f * Mathf.Sin(Time.time * 9f) + 0.05f * Mathf.Sin(Time.time * 23f);
        float fuerza = (0.55f + 0.45f * rapido) * late;
        if (materialSol != null && materialSol.HasProperty("_EmissionColor")) materialSol.SetColor("_EmissionColor", Oro * (0.35f + 0.5f * fuerza));
        Vector3 desde = modelo.TransformPoint(TapaDeAtras + Vector3.back * 0.02f);
        Vector3 atras = -transform.forward;
        luzDelSol.intensity = LuzDelPropulsor * (0.6f + 0.6f * fuerza);
        llama.SetPosition(0, desde);
        llama.SetPosition(1, desde + atras * Diametro * (0.25f + 0.9f * rapido) * late);
        llama.startWidth = Diametro * 0.2f * late;
        llama.endWidth = Diametro * 0.04f;
        float c = 0.35f + 0.3f * fuerza;
        llama.startColor = new Color(c, c, c, 1f);
        llama.endColor = new Color(c * 0.6f, c * 0.4f, c * 0.2f, 1f);
    }

    // La antena con la banderita (celeste, blanca y celeste, con el sol), que flamea.
    private const float AnchoBandera = 0.36f, AltoBandera = 0.24f, AlturaBandera = 0.4f, AlturaMastil = 0.58f; // (unidades del modelo)
    private const int ColumnasTela = 18, FilasTela = 10;
    private Mesh tela;
    private Vector3[] puntosTela, puntosFlameando;
    private static Texture2D texturaBandera;

    // El mástil hasta arriba, con una esfera dorada en la punta, y la bandera de tela (una malla que flamea, Flamear).
    private void ArmarBandera()
    {
        GameObject palo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(palo.GetComponent<Collider>());
        palo.name = "Mastil";
        palo.transform.SetParent(modelo, false);
        palo.transform.localPosition = Antena + Vector3.up * (AlturaMastil / 2f);
        palo.transform.localScale = new Vector3(0.022f, AlturaMastil / 2f, 0.022f);
        palo.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.8f, 0.8f, 0.82f), null, 0.8f, 0.7f);
        GameObject punta = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(punta.GetComponent<Collider>());
        punta.name = "Punta del mastil";
        punta.transform.SetParent(modelo, false);
        punta.transform.localPosition = Antena + Vector3.up * (AlturaMastil + 0.02f);
        punta.transform.localScale = Vector3.one * 0.055f;
        punta.GetComponent<Renderer>().sharedMaterial = Lit(Oro, null, 0.85f, 0.75f);

        bandera = new GameObject("Bandera").transform;
        bandera.SetParent(modelo, false);
        bandera.localPosition = Antena + Vector3.up * AlturaBandera;
        puntosTela = new Vector3[(ColumnasTela + 1) * (FilasTela + 1)];
        var uv = new Vector2[puntosTela.Length];
        var triangulos = new int[ColumnasTela * FilasTela * 6];
        for (int y = 0, k = 0; y <= FilasTela; y++)
            for (int x = 0; x <= ColumnasTela; x++, k++)
            {
                uv[k] = new Vector2((float)x / ColumnasTela, (float)y / FilasTela);
                puntosTela[k] = new Vector3(uv[k].x * AnchoBandera, (uv[k].y - 0.5f) * AltoBandera, 0f);
            }
        for (int y = 0, t = 0; y < FilasTela; y++)
            for (int x = 0; x < ColumnasTela; x++)
            {
                int a = y * (ColumnasTela + 1) + x, b = a + ColumnasTela + 1;
                triangulos[t++] = a; triangulos[t++] = b; triangulos[t++] = a + 1;
                triangulos[t++] = a + 1; triangulos[t++] = b; triangulos[t++] = b + 1;
            }
        tela = new Mesh { name = "Bandera" };
        tela.MarkDynamic();
        tela.vertices = puntosTela;
        tela.uv = uv;
        tela.triangles = triangulos;
        tela.RecalculateNormals();
        var go = new GameObject("Tela");
        go.transform.SetParent(bandera, false);
        go.AddComponent<MeshFilter>().sharedMesh = tela;
        go.AddComponent<MeshRenderer>().sharedMaterial = Lit(Color.white, TexturaBandera(), 0f, 0.25f);
    }

    // La bandera flamea: ondas que corren del mástil a la punta, más grandes lejos del mástil y cuanto más rápido va.
    private void Flamear()
    {
        if (tela == null) return;
        float t = Time.time, viento = 1f + Mathf.Clamp01(velocidad.magnitude / 3f);
        if (puntosFlameando == null) puntosFlameando = new Vector3[puntosTela.Length];
        Vector3[] puntos = puntosFlameando;
        for (int k = 0; k < puntos.Length; k++)
        {
            Vector3 p = puntosTela[k];
            float lejos = p.x / AnchoBandera; // 0 en el mástil, 1 en la punta
            float onda = Mathf.Sin(p.x * 26f - t * 7f * viento) * 0.035f + Mathf.Sin(p.x * 41f + p.y * 18f - t * 11f) * 0.012f;
            float cae = (1f - Mathf.Clamp01(viento - 1f)) * 0.03f * lejos * lejos; // sin viento, la punta cae un poco
            puntos[k] = new Vector3(p.x * (1f - 0.04f * lejos), p.y - cae, onda * lejos * viento);
        }
        tela.vertices = puntos;
        tela.RecalculateNormals();
        tela.RecalculateBounds();
    }

    // La bandera argentina pintada en una textura (celeste, blanca y celeste, con el Sol de Mayo en el medio).
    private static Texture2D TexturaBandera()
    {
        if (texturaBandera != null) return texturaBandera;
        const int Ancho = 384, Alto = 256;
        texturaBandera = new Texture2D(Ancho, Alto, TextureFormat.RGBA32, true) { name = "Bandera", wrapMode = TextureWrapMode.Clamp };
        var pixeles = new Color32[Ancho * Alto];
        Color32 celeste = Celeste, blanco = Color.white, oro = Oro, borde = new Color(0.52f, 0.2f, 0.06f);
        float cx = Ancho / 2f, cy = Alto / 2f, radio = Alto / 6f * 0.95f, centro = radio * 0.42f;
        for (int y = 0; y < Alto; y++)
            for (int x = 0; x < Ancho; x++)
            {
                Color32 c = y < Alto / 3 || y >= Alto * 2 / 3 ? celeste : blanco;
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy, r = Mathf.Sqrt(dx * dx + dy * dy);
                if (r <= radio)
                {
                    float a = Mathf.Atan2(dy, dx), paso = Mathf.PI / 16f;
                    int i = Mathf.RoundToInt(a / paso);
                    float dist = Mathf.Abs(a - i * paso), largo = (i & 1) == 0 ? radio : radio * 0.8f;
                    bool rayo = r <= largo && dist <= 0.09f * (1f - (r - centro) / (largo - centro));
                    if (r <= centro * 0.93f || rayo) c = oro;
                    else if (r <= centro) c = borde;
                }
                pixeles[y * Ancho + x] = c;
            }
        texturaBandera.SetPixels32(pixeles);
        texturaBandera.Apply(true, true);
        return texturaBandera;
    }

    // ---------- CA2: seguir al jugador ----------

    // Da vueltas alrededor del jugador: cada tanto elige otro lugar libre a su alrededor (a cualquier lado, a un poco más
    // de 1 m y algo más arriba de la cabeza) y va flotando hasta ahí. El lugar no gira con la cámara, así que en la
    // pantalla del dueño entra y sale de la vista; delante de los ojos no se pone (taparía la mira), y si hay una
    // pared, se queda de este lado.
    private Vector3 Lugar()
    {
        Vector3 cabeza = Mira != null ? Mira.position : (Sigue != null ? Sigue.position + Vector3.up * 1.7f : transform.position);
        if (Time.time >= cambiaLugarEn) ElegirLugar();
        Vector3 lugar = cabeza + alrededor;
        Vector3 hacia = lugar - cabeza;
        float largo = hacia.magnitude;
        if (largo > 0.001f && Choque(cabeza, hacia / largo, largo, out float pared))
            lugar = cabeza + hacia / largo * Mathf.Max(0f, pared - 0.05f);
        float vaiven = Mathf.Sin((Time.time - aparecio) * 2.1f) * 0.06f;
        return lugar + Vector3.up * vaiven;
    }

    private void ElegirLugar()
    {
        cambiaLugarEn = Time.time + Random.Range(CambiaLugarMin, CambiaLugarMax);
        Vector3 ojos = Mira != null ? Vector3.ProjectOnPlane(Mira.forward, Vector3.up) : Vector3.zero;
        for (int intento = 0; intento < 8; intento++)
        {
            float angulo = Random.Range(0f, 360f);
            Vector3 lado = Quaternion.Euler(0f, angulo, 0f) * Vector3.forward;
            // Delante de los ojos del dueño, no (taparía la mira).
            if (ojos.sqrMagnitude > 0.001f && Vector3.Angle(lado, ojos) < 35f) continue;
            alrededor = lado * Random.Range(0.9f, 1.6f) + Vector3.up * Random.Range(0.15f, 0.7f);
            return;
        }
    }

    private void LateUpdate()
    {
        if (Sigue == null) return;
        // CA2: no atraviesa paredes: si en el camino hay una, se queda de este lado. Si queda del otro lado del jugador (por
        // ejemplo, si el jugador pasó una puerta y el dron se trabó), vuelve a su lado.
        Vector3 antes = transform.position;
        Vector3 nueva = Vector3.SmoothDamp(antes, Lugar(), ref velocidad, Suavidad, Rapidez);
        Vector3 paso = nueva - antes;
        float mide = paso.magnitude;
        if (mide > 0.0001f && Choque(antes, paso / mide, mide, out float hasta))
        {
            nueva = antes + paso / mide * Mathf.Max(0f, hasta - 0.01f);
            velocidad = Vector3.zero;
        }
        transform.position = nueva;
        Vector3 cabeza = Mira != null ? Mira.position : Sigue.position + Vector3.up * 1.7f;
        Vector3 alJugador = nueva - cabeza;
        bool tapado = alJugador.sqrMagnitude > 0.01f && Choque(cabeza, alJugador.normalized, alJugador.magnitude, out _);
        if (!tapado) tapadoDesde = -1f;
        else if (tapadoDesde < 0f) tapadoDesde = Time.time;
        else if (Time.time - tapadoDesde > 0.6f) { transform.position = Lugar(); velocidad = Vector3.zero; tapadoDesde = -1f; }
        // Hacia dónde mira: al invasor del rayo (el rayo sale del ojo); si no, hacia donde va (así el Sol de Mayo, atrás,
        // queda para atrás); y quieto, mira a su aire, hacia un lado que cambia cada tanto (no hacia donde mira el jugador).
        Vector3 hacia;
        float giro = 3f;
        if (objetivo != null && objetivo.Vivo) { hacia = Centro(objetivo) - transform.position; giro = 8f; }
        else if (velocidad.sqrMagnitude > 0.09f) hacia = velocidad + Vector3.down * velocidad.y * 0.6f; // casi sin cabecear
        else
        {
            if (Time.time >= cambiaMiradaEn)
            {
                cambiaMiradaEn = Time.time + Random.Range(1.5f, 4f);
                mirada = Quaternion.Euler(Random.Range(-15f, 20f), Random.Range(0f, 360f), 0f) * Vector3.forward;
            }
            hacia = mirada;
            giro = 1.5f;
        }
        if (hacia.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(hacia, Vector3.up), giro * Time.deltaTime);
        Propulsar();
        if (bandera != null) bandera.localRotation = Quaternion.Euler(0f, 90f + Mathf.Sin(Time.time * 1.7f) * 8f, 0f); // hacia atrás
        Flamear();
        DibujarRayo();
    }

    // ---------- CA3 y CA4: el ataque (solo el propio) ----------

    private void Update()
    {
        if (!Propio) return;
        if (objetivo != null && !Sirve(objetivo, true)) Soltar();
        if (Time.time >= proximaBusqueda)
        {
            proximaBusqueda = Time.time + BuscarCada;
            if (objetivo == null) Enganchar(MasCercano());
            if (Time.time >= arcosListosEn && Arcos()) arcosListosEn = Time.time + ArcosCada; // sin nadie a tiro, esperan
        }
        if (objetivo != null)
        {
            acumulado += DanioPorSegundo(Time.time - enganchadoDesde) * Time.deltaTime;
            if (Time.time >= proximoGolpe)
            {
                proximoGolpe = Time.time + RayoCada;
                int danio = Mathf.FloorToInt(acumulado);
                acumulado -= danio;
                if (danio > 0) Daniar(objetivo, danio, true);
            }
        }
    }

    /// <summary>CA3: cuánto pega el rayo por segundo, con tantos segundos sobre el mismo invasor.</summary>
    public static float DanioPorSegundo(float segundos) =>
        Mathf.Lerp(RayoDanio, RayoDanioMaximo, Mathf.Clamp01(segundos / RayoCarga));

    private void Enganchar(Npc npc)
    {
        if (npc == objetivo) return;
        objetivo = npc;
        enganchadoDesde = Time.time;
        acumulado = 0f;
        proximoGolpe = Time.time + RayoCada;
        CambioElRayo?.Invoke(npc);
    }

    private void Soltar() => Enganchar(null);

    /// <summary>A quién le está dando el rayo (null: a nadie).</summary>
    public Npc Objetivo => objetivo;

    /// <summary>Para los de los compañeros: el rayo que llegó por la red (si es el mismo, sigue cargando).</summary>
    public void RayoA(Npc npc)
    {
        if (npc == objetivo) return;
        objetivo = npc;
        enganchadoDesde = Time.time;
    }

    // Vivo, cerca y (si hace falta) a la vista.
    private bool Sirve(Npc npc, bool vista)
    {
        if (npc == null || !npc.Vivo || npc.Vida == null) return false;
        Vector3 c = Centro(npc);
        if ((c - transform.position).sqrMagnitude > Alcance * Alcance) return false;
        return !vista || ALaVista(npc, c);
    }

    private Npc MasCercano()
    {
        candidatos.Clear();
        foreach (Npc n in Npc.Todos) if (n != null && n.Vivo && n.Vida != null) candidatos.Add(n);
        Vector3 yo = transform.position;
        candidatos.Sort((a, b) => (a.transform.position - yo).sqrMagnitude.CompareTo((b.transform.position - yo).sqrMagnitude));
        foreach (Npc n in candidatos) if (Sirve(n, true)) return n;
        return null;
    }

    // CA4: los arcos. Devuelve si salieron (sin nadie a tiro, esperan).
    private bool Arcos()
    {
        candidatos.Clear();
        foreach (Npc n in Npc.Todos) if (Sirve(n, true)) candidatos.Add(n);
        if (candidatos.Count == 0) return false;
        var lugares = new List<Vector3>(candidatos.Count);
        foreach (Npc n in candidatos) lugares.Add(Centro(n));
        List<int> cadena = Cadena(lugares, ArcosSaltos, ArcosEntre);
        var puntos = new Vector3[cadena.Count + 1];
        puntos[0] = transform.TransformPoint(Luces * Diametro);
        for (int i = 0; i < cadena.Count; i++)
        {
            Npc npc = candidatos[cadena[i]];
            puntos[i + 1] = lugares[cadena[i]];
            Daniar(npc, ArcosDanio, false);
            if (npc != null && npc.Vivo && (npc.Tipo == null || !npc.Tipo.esJefe)) npc.Aturdir(ArcosParaliza);
        }
        MostrarArcos(puntos);
        SalieronArcos?.Invoke(puntos);
        return true;
    }

    /// <summary>
    /// CA4: a quiénes van los arcos (sus índices en lugares, en orden): el primero es el que tiene más invasores a
    /// menos de entre metros (el grupo más grande; si empatan, el primero de la lista); después, cada salto va al más
    /// cercano del último que todavía no recibió, si está a menos de entre metros. Hasta saltos invasores.
    /// </summary>
    public static List<int> Cadena(IReadOnlyList<Vector3> lugares, int saltos, float entre)
    {
        var cadena = new List<int>();
        if (lugares == null || lugares.Count == 0 || saltos <= 0) return cadena;
        float entre2 = entre * entre;
        int primero = 0, mejor = -1;
        for (int i = 0; i < lugares.Count; i++)
        {
            int cerca = 0;
            for (int k = 0; k < lugares.Count; k++)
                if (k != i && (lugares[k] - lugares[i]).sqrMagnitude <= entre2) cerca++;
            if (cerca > mejor) { mejor = cerca; primero = i; }
        }
        cadena.Add(primero);
        while (cadena.Count < saltos)
        {
            Vector3 ultimo = lugares[cadena[cadena.Count - 1]];
            int siguiente = -1;
            float menor = entre2;
            for (int k = 0; k < lugares.Count; k++)
            {
                if (cadena.Contains(k)) continue;
                float d = (lugares[k] - ultimo).sqrMagnitude;
                if (d <= menor) { menor = d; siguiente = k; }
            }
            if (siguiente < 0) break;
            cadena.Add(siguiente);
        }
        return cadena;
    }

    // CA5: el daño, como un disparo del dueño (a la copia de un invasor se le manda al anfitrión).
    private void Daniar(Npc npc, int danio, bool delRayo)
    {
        if (npc == null || !npc.Vivo || npc.Vida == null || danio <= 0) return;
        Vector3 desde = transform.TransformPoint((delRayo ? Ojo : Luces) * Diametro);
        HealthSystem.DamageOrigin = desde;
        npc.Vida.TakeDamage(danio, false);
        if (delRayo) Destellos.En(Centro(npc), ColorElectrico, 4);
    }

    // ---------- Lo que se ve del ataque ----------

    /// <summary>CA4: los arcos en zigzag, de las luces al primero y de invasor en invasor.</summary>
    public static void MostrarArcos(Vector3[] puntos)
    {
        if (puntos == null) return;
        var efectos = new CatalogoDeSkins.Efectos { fuego = ColorElectrico, nucleo = Color.white, rayo = true };
        for (int i = 1; i < puntos.Length; i++)
        {
            RayoDeSkin.Crear(puntos[i - 1], puntos[i], efectos, 0.03f, 0.06f, 2, 0.4f);
            Destellos.En(puntos[i], ColorElectrico, 10);
        }
    }

    /// <summary>La copia del dron de un compañero: sus arcos.</summary>
    public void ArcosDeLaRed(Vector3[] puntos)
    {
        if (puntos == null || puntos.Length == 0) return;
        puntos[0] = transform.TransformPoint(Luces * Diametro); // desde donde está acá
        MostrarArcos(puntos);
    }

    // CA3 y CA7: el rayo del ojo y el brillo del ojo, que crecen con la carga.
    private void DibujarRayo()
    {
        bool hay = objetivo != null && objetivo.Vivo;
        float carga = hay ? Mathf.Clamp01((Time.time - enganchadoDesde) / RayoCarga) : 0f;
        if (materialOjo != null && materialOjo.HasProperty("_EmissionColor"))
            materialOjo.SetColor("_EmissionColor", ColorElectrico * (hay ? 1.5f + 3.5f * carga : 1.2f));
        if (!hay) { if (rayo != null) rayo.enabled = nucleo.enabled = false; return; }
        if (rayo == null)
        {
            var go = new GameObject("Rayo del ojo");
            go.transform.SetParent(transform, false);
            rayo = go.AddComponent<LineRenderer>();
            rayo.positionCount = 2;
            rayo.useWorldSpace = true;
            rayo.shadowCastingMode = ShadowCastingMode.Off;
            rayo.receiveShadows = false;
            rayo.numCapVertices = 3;
            materialRayo = Rayo.Material(ColorElectrico);
            rayo.sharedMaterial = materialRayo;
            // El centro blanco, más finito, adentro del rayo.
            var centro = new GameObject("Centro del rayo");
            centro.transform.SetParent(go.transform, false);
            nucleo = centro.AddComponent<LineRenderer>();
            nucleo.positionCount = 2;
            nucleo.useWorldSpace = true;
            nucleo.shadowCastingMode = ShadowCastingMode.Off;
            nucleo.receiveShadows = false;
            nucleo.numCapVertices = 3;
            materialNucleo = Rayo.Material(Color.white);
            nucleo.sharedMaterial = materialNucleo;
        }
        rayo.enabled = nucleo.enabled = true;
        Vector3 desde = transform.TransformPoint(Ojo * Diametro), hasta = Centro(objetivo);
        rayo.SetPosition(0, desde);
        rayo.SetPosition(1, hasta);
        nucleo.SetPosition(0, desde);
        nucleo.SetPosition(1, hasta);
        // CA3: más ancho que se ensancha con el daño (de AnchoDelRayo a AnchoDelRayoMaximo), latiendo un poco.
        float ancho = Mathf.Lerp(AnchoDelRayo, AnchoDelRayoMaximo, carga * carga) * (1f + 0.12f * Mathf.Sin(Time.time * 40f));
        rayo.startWidth = ancho * 0.8f;
        rayo.endWidth = ancho;
        nucleo.startWidth = ancho * 0.3f;
        nucleo.endWidth = ancho * 0.35f;
        float brillo = Mathf.Lerp(0.7f, 1f, carga);
        rayo.startColor = rayo.endColor = new Color(brillo, brillo, brillo, 1f);
        nucleo.startColor = nucleo.endColor = new Color(1f, 1f, 1f, Mathf.Lerp(0.6f, 1f, carga));
    }

    // ---------- Ayudas ----------

    // El centro del cuerpo del invasor (sus zonas de impacto), adonde apunta.
    private static Vector3 Centro(Npc npc)
    {
        Bounds caja = default;
        bool hay = false;
        foreach (Collider c in npc.GetComponentsInChildren<Collider>())
        {
            if (c.isTrigger || !c.enabled) continue;
            if (!hay) { caja = c.bounds; hay = true; } else caja.Encapsulate(c.bounds);
        }
        return hay ? caja.center : npc.transform.position + Vector3.up;
    }

    // CA2: si una esfera del tamaño del dron, yendo de desde hacia dir, choca contra algo del escenario antes de distancia
    // (los jugadores y los invasores no cuentan: pasa por arriba); en hasta, a cuánto.
    private static bool Choque(Vector3 desde, Vector3 dir, float distancia, out float hasta)
    {
        hasta = distancia;
        int n = Physics.SphereCastNonAlloc(desde, Diametro * 0.5f, dir, choques, distancia, ~0, QueryTriggerInteraction.Ignore);
        bool hay = false;
        for (int i = 0; i < n; i++)
        {
            Collider c = choques[i].collider;
            if (c == null || choques[i].distance <= 0f || choques[i].distance >= hasta) continue; // (0: ya estaba tocando)
            if (c.GetComponentInParent<PlayerMovement>() != null || c.GetComponentInParent<JugadorEnRed>() != null
                || c.GetComponentInParent<Npc>() != null || c.GetComponentInParent<CharacterController>() != null) continue;
            hasta = choques[i].distance;
            hay = true;
        }
        return hay;
    }

    // Si el ojo lo ve: lo primero que cruza la línea (sin contar a los jugadores) es él.
    private bool ALaVista(Npc npc, Vector3 centro)
    {
        Vector3 desde = transform.position;
        Vector3 hacia = centro - desde;
        float distancia = hacia.magnitude;
        if (distancia < 0.01f) return true;
        int n = Physics.RaycastNonAlloc(desde, hacia / distancia, golpes, distancia + 0.5f, ~0, QueryTriggerInteraction.Ignore);
        float masCerca = float.MaxValue;
        Npc primero = null;
        bool pared = false;
        for (int i = 0; i < n; i++)
        {
            Collider c = golpes[i].collider;
            if (c == null || golpes[i].distance >= masCerca) continue;
            if (c.GetComponentInParent<PlayerMovement>() != null || c.GetComponentInParent<JugadorEnRed>() != null) continue;
            masCerca = golpes[i].distance;
            primero = c.GetComponentInParent<Npc>();
            pared = primero == null;
        }
        return !pared && (primero == null || primero == npc);
    }

    private void OnDestroy()
    {
        if (materialRayo != null) Destroy(materialRayo);
        if (materialNucleo != null) Destroy(materialNucleo);
        if (materialOjo != null) Destroy(materialOjo);
        if (materialLlama != null) Destroy(materialLlama);
    }
}
