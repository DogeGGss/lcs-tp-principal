using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// Muros de la base en la fase de compra (US 190). Las paredes son los hijos de este objeto (cada uno con su collider
// y su malla); se arman con "Generar muros" (menú del componente, en el Inspector) a partir de las zonas de compra
// del mapa, solo donde se puede salir caminando.
// - CA1: durante la compra, el borde de cada base se ve como una pared roja semitransparente, más alta de lo que se
//   puede saltar.
// - CA2: la pared frena como cualquier otra; al chocarla aparece el aviso "No podés salir de la base durante la compra".
// - CA3: todos ven las paredes de las dos bases.
// - CA4: al terminar la compra se desvanecen en menos de 1 s, con un sonido, y ya se puede salir.
// - CA5: siguen la fase de la ronda (RondasTacticas), así que bajan a la vez en todas las computadoras. Sin rondas
//   (probando el mapa solo), siguen la fase de compra de la tienda.
public class MurosDeBase : MonoBehaviour
{
    private const float Aparecer = 0.2f, Desvanecer = 0.6f; // segundos
    private const float DistanciaAviso = 0.8f;              // a cuántos metros de la pared se avisa
    private const string Aviso = "No podés salir de la base durante la compra";

    [Tooltip("Alto de las paredes al generarlas, en metros: más de lo que se puede saltar (1,3 m).")]
    public float alto = 3.5f;
    [Tooltip("Material de las paredes (rojo semitransparente).")]
    public Material material;
    [Tooltip("Sonido al bajar las paredes (CA4).")]
    public AudioClip sonidoBajar;
    [Tooltip("Grupo SFX del Audio Mixer.")]
    public AudioMixerGroup grupo;

    private readonly List<Collider> paredes = new List<Collider>();
    private readonly List<Renderer> vistas = new List<Renderer>();
    private Material instancia;
    private Color color;
    private bool arriba, primeraVez = true;
    private float cambio = -10f, avisado = -10f;
    private Transform jugador;
    private HealthSystem vida;
    private AudioSource fuente;

    private void Awake()
    {
        foreach (Collider c in GetComponentsInChildren<Collider>(true)) if (!c.isTrigger) paredes.Add(c);
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) vistas.Add(r);
        // Una sola copia del material para todas, así se desvanecen juntas.
        if (material != null)
        {
            instancia = new Material(material);
            color = instancia.HasProperty("_BaseColor") ? instancia.GetColor("_BaseColor") : instancia.color;
            foreach (Renderer r in vistas) r.sharedMaterial = instancia;
        }
        fuente = gameObject.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.spatialBlend = 0f;
        fuente.outputAudioMixerGroup = grupo;
    }

    private void OnDestroy()
    {
        if (instancia != null) Destroy(instancia);
    }

    private void Update()
    {
        bool compra = EnCompra();
        if (compra != arriba || primeraVez)
        {
            // CA4: bajan (al empezar la ronda ya están arriba, sin sonido).
            if (!compra && arriba && sonidoBajar != null) fuente.PlayOneShot(sonidoBajar);
            arriba = compra;
            cambio = primeraVez ? -10f : Time.time;
            primeraVez = false;
            foreach (Collider c in paredes) if (c != null) c.enabled = compra;
        }

        // Aparecen rápido y se desvanecen en 0,6 s.
        float t = Time.time - cambio;
        float opacidad = arriba ? Mathf.Clamp01(t / Aparecer) : 1f - Mathf.Clamp01(t / Desvanecer);
        bool visibles = opacidad > 0f;
        foreach (Renderer r in vistas) if (r != null && r.enabled != visibles) r.enabled = visibles;
        if (visibles && instancia != null)
        {
            // Un pulso suave, para que se note que es una barrera y no una pared del mapa.
            float pulso = 0.85f + 0.15f * Mathf.Sin(Time.time * 3f);
            Color c = color;
            c.a = color.a * opacidad * pulso;
            if (instancia.HasProperty("_BaseColor")) instancia.SetColor("_BaseColor", c); else instancia.color = c;
        }

        if (arriba) AvisarSiLaToca();
    }

    // Durante la compra, o la selección de personaje antes de la ronda 1.
    private static bool EnCompra()
    {
        RondasTacticas rondas = RondasTacticas.Actual;
        if (rondas != null && rondas.Listo)
            return rondas.FaseActual == RondasTacticas.Fase.Compra || rondas.FaseActual == RondasTacticas.Fase.Seleccion;
        BuyPhase compra = BuyPhase.Current;
        return compra != null && compra.IsActive;
    }

    // CA2: el jugador de esta computadora contra una pared.
    private void AvisarSiLaToca()
    {
        if (jugador == null)
        {
            PlayerMovement movimiento = FindAnyObjectByType<PlayerMovement>();
            if (movimiento == null) return;
            jugador = movimiento.transform;
            vida = movimiento.GetComponent<HealthSystem>();
        }
        if (vida != null && vida.currentHealth <= 0) return;
        if (Time.unscaledTime - avisado < 1.5f) return;
        Vector3 cuerpo = jugador.position;
        foreach (Collider pared in paredes)
        {
            if (pared == null) continue;
            if ((pared.ClosestPoint(cuerpo) - cuerpo).sqrMagnitude > DistanciaAviso * DistanciaAviso) continue;
            avisado = Time.unscaledTime;
            MatchHud.Warn(Aviso);
            return;
        }
    }

#if UNITY_EDITOR
    // ---------- Armado en el editor ----------

    private const float Paso = 0.5f, Grosor = 0.12f;

    /// <summary>
    /// Arma las paredes en el borde de cada zona de compra del mapa, solo en los tramos por donde se puede salir
    /// caminando (sin paredes ni obstáculos en el medio). No pone pared entre dos zonas del mismo lado que se tocan.
    /// Borra las que había. Volver a usarlo si cambia el mapa.
    /// </summary>
    [ContextMenu("Generar muros")]
    public void Generar()
    {
        for (int i = transform.childCount - 1; i >= 0; i--) UnityEditor.Undo.DestroyObjectImmediate(transform.GetChild(i).gameObject);
        var zonas = new List<BuyZone>(FindObjectsByType<BuyZone>(FindObjectsInactive.Exclude));
        int cantidad = 0;
        foreach (BuyZone zona in zonas)
        {
            Collider area = zona.GetComponent<Collider>();
            if (area == null || zona.Lado == LadoTactico.Cualquiera) continue;
            Bounds b = area.bounds;
            Vector3[] esquinas =
            {
                new Vector3(b.min.x, 0f, b.min.z), new Vector3(b.max.x, 0f, b.min.z),
                new Vector3(b.max.x, 0f, b.max.z), new Vector3(b.min.x, 0f, b.max.z)
            };
            for (int e = 0; e < 4; e++)
                cantidad += Borde(zona, zonas, esquinas[e], esquinas[(e + 1) % 4], b.min.y);
        }
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"MurosDeBase: {cantidad} paredes.");
    }

    // Recorre un borde y pone una pared en cada tramo abierto.
    private int Borde(BuyZone zona, List<BuyZone> zonas, Vector3 desde, Vector3 hasta, float piso)
    {
        float largo = Vector3.Distance(desde, hasta);
        Vector3 dir = (hasta - desde).normalized;
        int pasos = Mathf.Max(1, Mathf.RoundToInt(largo / Paso));
        int cantidad = 0, inicio = -1;
        for (int i = 0; i <= pasos; i++)
        {
            Vector3 p = desde + dir * (largo * i / pasos);
            bool abierto = i < pasos && Abierto(zona, zonas, p + dir * (largo / pasos * 0.5f), piso);
            if (abierto && inicio < 0) inicio = i;
            if (!abierto && inicio >= 0)
            {
                Pared(desde + dir * (largo * inicio / pasos), p, piso);
                cantidad++;
                inicio = -1;
            }
        }
        return cantidad;
    }

    // Se puede pasar caminando por ahí: no hay nada a la altura del cuerpo y no es el borde con otra zona del mismo lado.
    private static bool Abierto(BuyZone zona, List<BuyZone> zonas, Vector3 punto, float piso)
    {
        foreach (BuyZone otra in zonas)
        {
            if (otra == zona || otra.Lado != zona.Lado) continue;
            Bounds b = otra.GetComponent<Collider>().bounds;
            b.Expand(new Vector3(0.6f, 0f, 0.6f));
            if (punto.x > b.min.x && punto.x < b.max.x && punto.z > b.min.z && punto.z < b.max.z) return false;
        }
        Vector3 centro = new Vector3(punto.x, piso + 1.1f, punto.z);
        foreach (Collider c in Physics.OverlapBox(centro, new Vector3(0.2f, 0.7f, 0.2f), Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            if (c.GetComponentInParent<CharacterController>() == null) return false;
        return true;
    }

    private void Pared(Vector3 desde, Vector3 hasta, float piso)
    {
        GameObject pared = GameObject.CreatePrimitive(PrimitiveType.Cube);
        UnityEditor.Undo.RegisterCreatedObjectUndo(pared, "Generar muros");
        pared.name = "Pared";
        pared.transform.SetParent(transform, true);
        Vector3 medio = (desde + hasta) * 0.5f;
        pared.transform.position = new Vector3(medio.x, piso + alto * 0.5f, medio.z);
        pared.transform.rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, hasta - desde).normalized, Vector3.up);
        pared.transform.localScale = new Vector3(Vector3.Distance(desde, hasta), alto, Grosor);
        Renderer vista = pared.GetComponent<Renderer>();
        vista.sharedMaterial = material;
        vista.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        vista.receiveShadows = false;
    }
#endif
}
