using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// F11 (NPCs), US 284: el enemigo muerto se desintegra de arriba hacia abajo cuando termina su animación de morir
// (lo empieza MuerteDeNpc).
// - CA1: el cuerpo se va borrando desde arriba hasta el piso, con el borde irregular (shader Efectos/Desintegrar, que
//   reemplaza su material durante estos segundos).
// - CA2: el borde que se borra brilla del color de sus marcas (TipoDeNpc.colorDeMarcas) y del cuerpo salen cenizas del
//   mismo color que suben y se apagan (justo de donde se está borrando).
// - CA3: dura 1,6 s. El Jefe, 2,6 s y en rojo (el color de su furia, US 268), con más cenizas y un estallido de
//   cenizas al final.
// Al terminar se borra el enemigo.
[RequireComponent(typeof(Npc))]
public class DesintegracionDeNpc : MonoBehaviour
{
    private const float Dura = 1.6f, DuraJefe = 2.6f;
    private static readonly Color RojoFuria = new Color(1f, 0.15f, 0.1f); // la luz de la furia del Jefe (FuriaDeNpc)
    private const float CenizasPorSegundo = 180f, CenizasPorSegundoJefe = 450f;
    private const float DespuesDeBorrarse = 1.2f; // s hasta que se apagan las últimas cenizas
    private const int MaxPuntos = 4000;              // puntos del cuerpo de donde pueden salir cenizas

    private static readonly int CorteId = Shader.PropertyToID("_Corte"), RuidoId = Shader.PropertyToID("_Ruido"),
        AnchoId = Shader.PropertyToID("_Ancho"), BordeId = Shader.PropertyToID("_ColorBorde"),
        BaseMapId = Shader.PropertyToID("_BaseMap"), BaseColorId = Shader.PropertyToID("_BaseColor");
    private static Shader shader;

    private Npc npc;
    private readonly List<Material> materiales = new List<Material>();
    private readonly List<Renderer> renders = new List<Renderer>();
    private Vector3[] puntos = new Vector3[0];
    private ParticleSystem cenizas;
    private float empezo, dura, arriba, abajo, ruido, ancho, porSegundo, acumuladas;
    private Color color;
    private bool termino;

    /// <summary>Empieza a desintegrar al enemigo (CA1).</summary>
    public static void Empezar(Npc npc)
    {
        if (npc == null || npc.GetComponent<DesintegracionDeNpc>() != null) return;
        npc.gameObject.AddComponent<DesintegracionDeNpc>();
    }

    private void Start()
    {
        npc = GetComponent<Npc>();
        bool jefe = npc.Tipo != null && npc.Tipo.esJefe;
        dura = jefe ? DuraJefe : Dura;
        porSegundo = jefe ? CenizasPorSegundoJefe : CenizasPorSegundo;
        Color c = jefe ? RojoFuria : npc.Tipo != null ? npc.Tipo.colorDeMarcas : new Color(0.6f, 1f, 0.8f);
        color = new Color(c.r, c.g, c.b) * 3f; // HDR: pasa el umbral del bloom

        if (shader == null)
        {
            shader = Resources.Load<Shader>("Efectos/Desintegrar");
            if (shader == null) shader = Shader.Find("Efectos/Desintegrar");
        }
        Transform modelo = npc.Modelo != null ? npc.Modelo : transform;

        // El alto del cuerpo (como quedó tirado) y los puntos de su superficie, para las cenizas.
        var caja = new Bounds(transform.position, Vector3.zero);
        bool hay = false;
        var todos = new List<Vector3>();
        foreach (Renderer r in modelo.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || !r.enabled) continue;
            if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;
            renders.Add(r);
            if (hay) caja.Encapsulate(r.bounds); else { caja = r.bounds; hay = true; }
            PuntosDe(r, todos);
        }
        if (todos.Count > MaxPuntos)
        {
            var elegidos = new Vector3[MaxPuntos];
            for (int i = 0; i < MaxPuntos; i++) elegidos[i] = todos[Random.Range(0, todos.Count)];
            puntos = elegidos;
        }
        else puntos = todos.ToArray();

        float alto = Mathf.Max(0.3f, caja.size.y);
        ruido = 0.08f * alto + 0.1f;
        ancho = 0.06f * alto + 0.06f;
        arriba = caja.max.y + ruido + 0.05f;
        abajo = caja.min.y - ruido - 0.05f;

        // CA1: el material que se borra, en lugar del suyo (uno por textura).
        if (shader != null)
        {
            var porTextura = new Dictionary<Texture, Material>();
            foreach (Renderer r in renders)
            {
                Material[] originales = r.sharedMaterials;
                var nuevos = new Material[originales.Length];
                for (int i = 0; i < originales.Length; i++) nuevos[i] = Desintegrable(originales[i], originales, porTextura);
                r.sharedMaterials = nuevos;
                r.shadowCastingMode = ShadowCastingMode.Off; // la sombra no se borraría
            }
        }

        cenizas = Cenizas(Mathf.Clamp(alto * 0.05f, 0.03f, 0.12f) * (jefe ? 1.4f : 1f));
        empezo = Time.time;
    }

    // Su material con el mismo dibujo (la textura del primero que tenga, para la pasada de las marcas que brillan).
    private Material Desintegrable(Material original, Material[] todos, Dictionary<Texture, Material> porTextura)
    {
        Material conTextura = original != null && original.mainTexture != null ? original : null;
        if (conTextura == null) foreach (Material m in todos) if (m != null && m.mainTexture != null) { conTextura = m; break; }
        Texture tex = conTextura != null ? conTextura.mainTexture : Texture2D.whiteTexture;
        if (porTextura.TryGetValue(tex, out Material hecho)) return hecho;

        var nuevo = new Material(shader) { name = "Desintegrar (" + tex.name + ")" };
        nuevo.SetTexture(BaseMapId, tex);
        if (conTextura != null)
        {
            nuevo.SetTextureScale(BaseMapId, conTextura.mainTextureScale);
            nuevo.SetTextureOffset(BaseMapId, conTextura.mainTextureOffset);
            if (conTextura.HasProperty(BaseColorId)) nuevo.SetColor(BaseColorId, conTextura.GetColor(BaseColorId));
        }
        nuevo.SetColor(BordeId, color);
        nuevo.SetFloat(RuidoId, ruido);
        nuevo.SetFloat(AnchoId, ancho);
        nuevo.SetFloat(CorteId, arriba);
        porTextura[tex] = nuevo;
        materiales.Add(nuevo);
        return nuevo;
    }

    // Los puntos de la superficie de ese render, en el mundo, en la pose en que quedó.
    private static void PuntosDe(Renderer r, List<Vector3> todos)
    {
        Mesh malla = null;
        bool propia = false;
        if (r is SkinnedMeshRenderer piel && piel.sharedMesh != null)
        {
            malla = new Mesh();
            piel.BakeMesh(malla);
            propia = true;
        }
        else if (r.TryGetComponent(out MeshFilter f) && f.sharedMesh != null && f.sharedMesh.isReadable) malla = f.sharedMesh;
        if (malla == null) return;
        Matrix4x4 aMundo = r.transform.localToWorldMatrix;
        Vector3[] v = malla.vertices;
        int paso = Mathf.Max(1, v.Length / 1500);
        for (int i = 0; i < v.Length; i += paso) todos.Add(aMundo.MultiplyPoint3x4(v[i]));
        if (propia) Destroy(malla);
    }

    private ParticleSystem Cenizas(float tam)
    {
        var go = new GameObject("Cenizas");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main;
        m.loop = false;
        m.duration = 10f; // dura más que la desintegración
        m.playOnAwake = false;
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
        m.startSize = new ParticleSystem.MinMaxCurve(tam * 0.5f, tam);
        m.gravityModifier = -0.12f; // suben
        m.maxParticles = 2000;
        var e = ps.emission;
        e.enabled = false; // las larga Update, de donde se está borrando
        var forma = ps.shape;
        forma.enabled = false;
        var ruidoP = ps.noise;
        ruidoP.enabled = true;
        ruidoP.strength = 0.5f;
        ruidoP.frequency = 0.8f;
        var c = ps.colorOverLifetime;
        c.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
        c.color = g;
        var t = ps.sizeOverLifetime;
        t.enabled = true;
        t.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
        var render = go.GetComponent<ParticleSystemRenderer>();
        render.shadowCastingMode = ShadowCastingMode.Off;
        render.receiveShadows = false;
        Shader brillo = Resources.Load<Shader>("Efectos/ParticulaBrillante");
        if (brillo == null) brillo = Shader.Find("Efectos/ParticulaBrillante");
        if (brillo != null)
        {
            var mat = new Material(brillo) { name = "Cenizas" };
            mat.SetColor("_Color", color);
            mat.SetFloat("_Suave", 0.7f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.One);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            render.sharedMaterial = mat;
            materiales.Add(mat);
        }
        ps.Play();
        return ps;
    }

    private void Update()
    {
        float t = Time.time - empezo;
        if (!termino)
        {
            float avance = Mathf.Clamp01(t / dura);
            float corte = Mathf.Lerp(arriba, abajo, avance * avance * (3f - 2f * avance)); // arranca y termina suave
            foreach (Material m in materiales) if (m.HasProperty(CorteId)) m.SetFloat(CorteId, corte);
            Largar(corte);
            if (avance >= 1f) Terminar();
        }
        else if (t >= dura + DespuesDeBorrarse) Destroy(gameObject);
    }

    // CA2: cenizas de los puntos que se están borrando (justo debajo del corte).
    private void Largar(float corte)
    {
        if (cenizas == null || puntos.Length == 0) return;
        acumuladas += porSegundo * Time.deltaTime;
        int cuantas = Mathf.FloorToInt(acumuladas);
        acumuladas -= cuantas;
        var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        for (int i = 0, intentos = 0; i < cuantas && intentos < cuantas * 6; intentos++)
        {
            Vector3 punto = puntos[Random.Range(0, puntos.Length)];
            float d = corte - punto.y;
            if (d < -ruido || d > ruido + ancho) continue; // lejos del borde que se quema
            p.position = punto;
            cenizas.Emit(p, 1);
            i++;
        }
    }

    // El cuerpo ya no está: quedan las últimas cenizas. CA3: el Jefe, con un estallido de cenizas rojas.
    private void Terminar()
    {
        termino = true;
        foreach (Renderer r in renders) if (r != null) r.enabled = false;
        if (npc != null && npc.Tipo != null && npc.Tipo.esJefe && cenizas != null && puntos.Length > 0)
        {
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < 350; i++)
            {
                p.position = puntos[Random.Range(0, puntos.Length)];
                p.velocity = (Random.onUnitSphere + Vector3.up * 1.2f) * Random.Range(1.5f, 4f);
                cenizas.Emit(p, 1);
            }
        }
    }

    private void OnDestroy()
    {
        foreach (Material m in materiales) if (m != null) Destroy(m);
    }
}
