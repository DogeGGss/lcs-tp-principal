using System.Collections.Generic;
using UnityEngine;

public enum TipoPajaro { Paloma, Gorrion, Golondrina }

// Un pájaro del minijuego (F21, US 156), low poly y armado por código: cuerpo, cola y dos alas que aletean.
// - CA2: cruza el cielo en línea recta y desaparece al terminar su recorrido.
// - CA4: tamaño, velocidad y puntos según el tipo (tabla de la historia).
// - CA5: al recibir un disparo cae girando con unas plumas y desaparece al llegar al piso o a los 2 s.
// - CA1: cuando se termina la compra se va volando: sube, acelera y se achica hasta desaparecer en 2,5 s.
// Lo crea MinijuegoPajaros y existe solo en esta computadora (CA6).
public class Pajaro : MonoBehaviour
{
    private struct Datos
    {
        public float envergadura, velocidad, aleteo;
        public int puntos;
        public Color lomo, alas, panza;
    }

    // Envergadura en metros: más grande que la de un pájaro real, para que se vea a 15-40 m de altura.
    private static readonly Datos[] Tabla =
    {
        new Datos { envergadura = 1.5f, velocidad = 6f, aleteo = 3.5f, puntos = 50,  // Paloma: grande y lenta
            lomo = new Color(0.56f, 0.58f, 0.64f), alas = new Color(0.43f, 0.45f, 0.52f), panza = new Color(0.72f, 0.74f, 0.78f) },
        new Datos { envergadura = 1.05f, velocidad = 10f, aleteo = 7f, puntos = 100, // Gorrión: mediano
            lomo = new Color(0.52f, 0.37f, 0.24f), alas = new Color(0.38f, 0.26f, 0.16f), panza = new Color(0.8f, 0.72f, 0.6f) },
        new Datos { envergadura = 0.8f, velocidad = 15f, aleteo = 5f, puntos = 150,   // Golondrina: chica y rápida
            lomo = new Color(0.1f, 0.12f, 0.24f), alas = new Color(0.06f, 0.07f, 0.15f), panza = new Color(0.93f, 0.91f, 0.87f) },
    };

    private const float Amplitud = 38f;    // grados que suben y bajan las alas
    private const float DuracionCaida = 2f; // CA5
    private const float DuracionHuida = 2.5f; // CA1

    public static int PuntosDe(TipoPajaro tipo) => Tabla[(int)tipo].puntos;
    public static float EnvergaduraDe(TipoPajaro tipo) => Tabla[(int)tipo].envergadura;

    public TipoPajaro Tipo { get; private set; }
    public int Puntos => Tabla[(int)Tipo].puntos;
    public float Aparecio { get; private set; } // para el tiempo de reacción de cada acierto
    public bool Cayendo { get; private set; }

    private Transform alaIzquierda, alaDerecha;
    private Vector3 velocidad, giro;
    private float recorrido, distancia, desfase, caidaDesde, seVaDesde = -1f;

    public static Pajaro Crear(TipoPajaro tipo, Vector3 desde, Vector3 direccion, float distancia, Transform padre)
    {
        Datos d = Tabla[(int)tipo];
        var go = new GameObject(tipo.ToString());
        go.transform.SetParent(padre, false);
        go.transform.SetPositionAndRotation(desde, Quaternion.LookRotation(direccion));
        var pajaro = go.AddComponent<Pajaro>();
        pajaro.Tipo = tipo;
        pajaro.Aparecio = Time.time;
        pajaro.distancia = distancia;
        pajaro.velocidad = direccion.normalized * d.velocidad;
        pajaro.desfase = Random.value * 10f;
        pajaro.Armar(d);

        // Se le pega en una esfera del ancho de las alas. Es un trigger: las balas del combate no lo tocan, y
        // WeaponFire lo busca a propósito durante la compra (US 157).
        var esfera = go.AddComponent<SphereCollider>();
        esfera.isTrigger = true;
        esfera.radius = d.envergadura * 0.5f;
        var cuerpo = go.AddComponent<Rigidbody>();
        cuerpo.isKinematic = true;
        cuerpo.useGravity = false;
        return pajaro;
    }

    /// <summary>CA5: recibió un disparo.</summary>
    public void Acertado(Vector3 punto)
    {
        if (Cayendo) return;
        Cayendo = true;
        caidaDesde = Time.time;
        velocidad *= 0.35f;
        giro = new Vector3(Random.Range(-220f, 220f), Random.Range(360f, 720f), Random.Range(-220f, 220f));
        GetComponent<Collider>().enabled = false;
        alaIzquierda.localRotation = Quaternion.Euler(0f, 0f, -70f);
        alaDerecha.localRotation = Quaternion.Euler(0f, 0f, 70f);
        Datos d = Tabla[(int)Tipo];
        Plumas.Soltar(punto, d.alas, d.panza, d.envergadura, transform.parent);
    }

    /// <summary>CA1: se termina la fase de compra; el que sigue volando se va.</summary>
    public void Irse()
    {
        if (!Cayendo && seVaDesde < 0f) seVaDesde = Time.time;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (Cayendo)
        {
            Caer(dt);
            return;
        }
        if (seVaDesde >= 0f)
        {
            float huida = Time.time - seVaDesde;
            if (huida >= DuracionHuida) { Destroy(gameObject); return; }
            velocidad += (velocidad.normalized * 12f + Vector3.up * 10f) * dt;
            transform.rotation = Quaternion.LookRotation(velocidad);
            transform.localScale = Vector3.one * Mathf.Clamp01(DuracionHuida - huida); // el último segundo se achica
        }
        transform.position += velocidad * dt;
        recorrido += velocidad.magnitude * dt;
        if (recorrido >= distancia) { Destroy(gameObject); return; }

        float angulo = Amplitud * Mathf.Sin((Time.time + desfase) * Tabla[(int)Tipo].aleteo * Mathf.PI * 2f);
        alaDerecha.localRotation = Quaternion.Euler(0f, 0f, angulo);
        alaIzquierda.localRotation = Quaternion.Euler(0f, 0f, -angulo);
    }

    private void Caer(float dt)
    {
        velocidad += Physics.gravity * dt;
        Vector3 paso = velocidad * dt;
        bool piso = Physics.Raycast(transform.position, paso.normalized, paso.magnitude + 0.15f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (piso || Time.time - caidaDesde >= DuracionCaida)
        {
            Destroy(gameObject);
            return;
        }
        transform.position += paso;
        transform.Rotate(giro * dt, Space.Self);
    }

    // ---------- Modelo ----------

    // Proporciones según la envergadura (s): un rombo alargado de cuerpo (pico adelante), la cola atrás y un ala a
    // cada lado que gira sobre el hombro. La golondrina tiene alas en punta y la cola en tijera.
    private void Armar(Datos d)
    {
        bool golondrina = Tipo == TipoPajaro.Golondrina;
        float s = d.envergadura;
        float largo = s * (golondrina ? 0.5f : 0.45f), ancho = s * 0.17f, alto = s * 0.16f;

        Vector3 pico = new Vector3(0f, alto * 0.15f, largo * 0.5f), atras = new Vector3(0f, 0f, -largo * 0.45f);
        Vector3 izquierda = new Vector3(-ancho * 0.5f, 0f, largo * 0.08f), derecha = new Vector3(ancho * 0.5f, 0f, largo * 0.08f);
        Vector3 arriba = new Vector3(0f, alto * 0.5f, largo * 0.12f), abajo = new Vector3(0f, -alto * 0.5f, largo * 0.05f);
        Pieza("Lomo", transform, d.lomo,
            pico, arriba, derecha, pico, izquierda, arriba, atras, derecha, arriba, atras, arriba, izquierda);
        Pieza("Panza", transform, d.panza,
            pico, derecha, abajo, pico, abajo, izquierda, atras, abajo, derecha, atras, izquierda, abajo);

        if (golondrina)
            Pieza("Cola", transform, d.alas,
                atras, new Vector3(-s * 0.04f, 0f, -largo * 0.7f), new Vector3(-s * 0.13f, 0f, -largo * 1.05f),
                atras, new Vector3(s * 0.13f, 0f, -largo * 1.05f), new Vector3(s * 0.04f, 0f, -largo * 0.7f));
        else
            Pieza("Cola", transform, d.alas,
                atras, new Vector3(-s * 0.11f, 0f, -largo * 0.78f), new Vector3(s * 0.11f, 0f, -largo * 0.78f));

        float hombro = ancho * 0.35f, tramo = s * 0.5f - hombro;
        Vector3 frente = new Vector3(0f, 0f, largo * 0.18f), fondo = new Vector3(0f, 0f, -largo * 0.12f);
        Vector3 bordeDeAtaque = new Vector3(tramo * (golondrina ? 0.7f : 0.85f), 0f, -largo * (golondrina ? 0.05f : 0.02f));
        Vector3 punta = new Vector3(tramo, 0f, -largo * (golondrina ? 0.55f : 0.2f));
        alaDerecha = Ala("Ala derecha", new Vector3(hombro, alto * 0.2f, largo * 0.1f), d.alas, 1f, frente, bordeDeAtaque, punta, fondo);
        alaIzquierda = Ala("Ala izquierda", new Vector3(-hombro, alto * 0.2f, largo * 0.1f), d.alas, -1f, frente, bordeDeAtaque, punta, fondo);
    }

    private Transform Ala(string nombre, Vector3 hombro, Color color, float lado, Vector3 frente, Vector3 bordeDeAtaque, Vector3 punta, Vector3 fondo)
    {
        var ala = new GameObject(nombre).transform;
        ala.SetParent(transform, false);
        ala.localPosition = hombro;
        Vector3 Espejo(Vector3 v) => new Vector3(v.x * lado, v.y, v.z);
        Pieza("Malla", ala, color, Espejo(frente), Espejo(bordeDeAtaque), Espejo(punta), Espejo(frente), Espejo(punta), Espejo(fondo));
        return ala;
    }

    // Una pieza de caras planas con las dos caras de cada triángulo (se ve de los dos lados y se ilumina bien).
    private static void Pieza(string nombre, Transform padre, Color color, params Vector3[] triangulos)
    {
        var vertices = new Vector3[triangulos.Length * 2];
        var indices = new int[triangulos.Length * 2];
        for (int i = 0; i < triangulos.Length; i += 3)
        {
            int a = i * 2;
            vertices[a] = triangulos[i]; vertices[a + 1] = triangulos[i + 1]; vertices[a + 2] = triangulos[i + 2];
            vertices[a + 3] = triangulos[i]; vertices[a + 4] = triangulos[i + 2]; vertices[a + 5] = triangulos[i + 1];
            for (int k = 0; k < 6; k++) indices[a + k] = a + k;
        }
        var malla = new Mesh { name = nombre, vertices = vertices, triangles = indices };
        malla.RecalculateNormals();
        malla.RecalculateBounds();

        var go = new GameObject(nombre, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(padre, false);
        go.GetComponent<MeshFilter>().sharedMesh = malla;
        var render = go.GetComponent<MeshRenderer>();
        render.sharedMaterial = MaterialDe(color);
        render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static Shader sombreado;
    private static readonly Dictionary<Color, Material> materiales = new Dictionary<Color, Material>();

    public static Material MaterialDe(Color color)
    {
        if (materiales.TryGetValue(color, out Material material) && material != null) return material;
        if (sombreado == null)
            sombreado = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Standard");
        material = new Material(sombreado) { name = "Pájaro", color = color };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);
        materiales[color] = material;
        return material;
    }

    private void OnDestroy()
    {
        foreach (MeshFilter filtro in GetComponentsInChildren<MeshFilter>(true))
            if (filtro.sharedMesh != null) Destroy(filtro.sharedMesh);
    }
}
