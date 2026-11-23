using UnityEngine;
using UnityEngine.AI;

// F11 (Watcher, US 285) y US 146 (el Jefe): los invasores de cuerpo grande no se meten en las paredes. Su agente va por
// el NavMesh, que deja libre del borde solo lo que mide el agente para el que se armó (0,3 a 0,5 m; el del Jefe, 1,6 m).
// Un cuerpo más ancho (o el del Watcher, que flota a la altura de las paredes) igual quedaba metido en la pared al ir
// pegado al borde. Esto mira alrededor del cuerpo, a su altura, y si hay pared más cerca que su ancho lo corre hacia
// afuera por el NavMesh (agente.Move: nunca sale de él). Lo agrega PersecucionDeNpc a los que lo necesitan.
// Solo va en los que flotan (Watcher, Tirador, Francotirador) y en el Jefe: en los de a pie el NavMesh ya alcanza, y
// medirles el cuerpo entero (patas, garras, animación) los hacía rebotar contra todo. No cuentan el piso ni las rampas,
// en un pasillo angosto no empuja (las dos paredes se anulan) y se aparta suave, sin sacarlo de su camino.
public class LejosDeLasParedes : MonoBehaviour
{
    private const int Direcciones = 12;
    private const float Correr = 4f;       // qué tan rápido se aparta (1/s)
    private const float Suave = 8f;        // qué tan rápido cambia el empuje (1/s): sin saltos de un lado al otro
    private const float Minimo = 0.05f;    // m: lo que falta por debajo de esto no se corrige (no tiembla)
    private const float CadaCuanto = 0.05f; // s
    private const float Margen = 0.1f;     // m de más entre el cuerpo y la pared

    private Npc npc;
    private NavMeshAgent agente;
    private float radio, medio, bajo;
    private float proximo;
    private Vector3 empuje, objetivo;
    private readonly float[] cercas = new float[Direcciones];
    private static readonly RaycastHit[] golpes = new RaycastHit[8];

    /// <summary>Lo agrega si el cuerpo es más ancho que lo que el NavMesh deja libre del borde.</summary>
    public static void Poner(Npc npc, NavMeshAgent agente)
    {
        if (npc == null || agente == null || npc.GetComponent<LejosDeLasParedes>() != null) return;
        bool flota = npc.Tipo != null && npc.Tipo.alturaDeVuelo > 0f;
        bool jefe = npc.Tipo != null && npc.Tipo.esJefe;
        if (!flota && !jefe) return; // (los de a pie no lo necesitan: el NavMesh ya los deja lejos del borde)
        Transform modelo = npc.Modelo != null ? npc.Modelo : npc.transform;
        if (!Medir(modelo, out float radio, out float medio, out float alto)) return;
        float libre = NavMesh.GetSettingsByID(agente.agentTypeID).agentRadius;
        if (!flota && radio <= libre + 0.15f) return; // (entra en lo que deja libre el NavMesh)
        LejosDeLasParedes l = npc.gameObject.AddComponent<LejosDeLasParedes>();
        l.npc = npc;
        l.agente = agente;
        l.radio = Mathf.Min(radio, jefe ? 2f : 1.2f) + Margen;
        l.medio = medio;
        l.bajo = Mathf.Max(0.3f, medio - alto * 0.3f);
    }

    // El ancho del cuerpo (lo más lejos del centro, de costado), dónde está su mitad y lo alto, respecto del modelo.
    private static bool Medir(Transform modelo, out float radio, out float medio, out float alto)
    {
        radio = medio = alto = 0f;
        bool hay = false;
        Bounds b = default;
        foreach (Renderer r in modelo.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
            if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
        }
        if (!hay) return false;
        Vector3 c = b.center - modelo.position;
        radio = Mathf.Max(b.extents.x + Mathf.Abs(c.x), b.extents.z + Mathf.Abs(c.z)) * 0.85f; // (la caja es más que el cuerpo)
        medio = c.y;
        alto = b.size.y;
        return true;
    }

    private void Update()
    {
        if (agente == null || !agente.enabled || !agente.isOnNavMesh || npc == null || npc.Modelo == null) return;
        if (Time.time >= proximo)
        {
            proximo = Time.time + CadaCuanto;
            Vector3 centro = npc.Modelo.position + Vector3.up * medio; // (el Watcher sube y baja: va con el modelo)
            objetivo = Paredes(centro) + Paredes(npc.Modelo.position + Vector3.up * bajo);
            objetivo.y = 0f;
            objetivo = Vector3.ClampMagnitude(objetivo, radio);
            if (objetivo.magnitude < Minimo) objetivo = Vector3.zero;
        }
        empuje = Vector3.Lerp(empuje, objetivo, Mathf.Min(1f, Suave * Time.deltaTime));
        if (empuje.sqrMagnitude < 1e-6f) return;
        // (nunca más rápido que la mitad de lo que camina: se aparta sin ganarle a su propio camino)
        float tope = Mathf.Max(0.5f, agente.speed * 0.5f) * Time.deltaTime;
        agente.Move(Vector3.ClampMagnitude(empuje * Mathf.Min(1f, Correr * Time.deltaTime), tope));
    }

    // Cuánto y hacia dónde hay que correrse para que las paredes queden a "radio" (desde ese punto).
    private Vector3 Paredes(Vector3 desde)
    {
        Vector3 total = Vector3.zero;
        float mayor = 0f;
        for (int i = 0; i < Direcciones; i++)
        {
            float a = i * Mathf.PI * 2f / Direcciones;
            cercas[i] = Pared(desde, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
        }
        for (int i = 0; i < Direcciones; i++)
        {
            float cerca = cercas[i];
            if (cerca >= radio) continue;
            if (cercas[(i + Direcciones / 2) % Direcciones] < radio) continue; // (pared enfrente también: pasillo, no empuja)
            float a = i * Mathf.PI * 2f / Direcciones;
            Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float falta = radio - cerca;
            total -= d * falta;
            mayor = Mathf.Max(mayor, falta);
        }
        // (de varias direcciones suma de más: no más que lo que falta en la peor)
        return total.sqrMagnitude > mayor * mayor ? total.normalized * mayor : total;
    }

    // A cuántos m hay pared en esa dirección (radio si no hay). No cuentan los personajes ni los invasores.
    private float Pared(Vector3 desde, Vector3 d)
    {
        int n = Physics.RaycastNonAlloc(desde, d, golpes, radio, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float menor = radio;
        for (int i = 0; i < n; i++)
        {
            Collider c = golpes[i].collider;
            if (c == null || c.transform.IsChildOf(transform) || c is CharacterController) continue;
            if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue; // (lo que se mueve con física)
            if (c.GetComponentInParent<Npc>() != null || c.GetComponentInParent<HealthSystem>() != null) continue;
            if (golpes[i].distance <= 0f) continue; // (arrancó adentro: no se sabe para dónde salir)
            if (Mathf.Abs(golpes[i].normal.y) > 0.5f) continue; // (piso, rampa, escalón o techo: no es pared)
            menor = Mathf.Min(menor, golpes[i].distance);
        }
        return menor;
    }
}
