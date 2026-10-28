using UnityEngine;

// F11 (NPCs), US 037: el NPC golpea al jugador cuando lo alcanza.
// - CA1: ataca cuando el jugador está a menos de 1,5 m (PersecucionDeNpc lo frena ahí) y delante de él.
// - CA2: el golpe hace daño 0,5 s después de empezar la animación; si en ese tiempo el jugador se alejó más de 2 m (o
//   quedó atrás), falla.
// - CA3: hace el daño de su tipo (TipoDeNpc.danio). El sistema de vida del jugador lo aplica primero al escudo y el
//   resto a la vida (HealthSystem.TakeDamage).
// - CA4: golpea como máximo una vez por segundo (y nunca antes de terminar la animación del golpe anterior).
// - CA5: el aviso de daño en rojo hacia el lado del golpe es el de la US 192: se le dice desde dónde vino
//   (HealthSystem.DamageOrigin).
// Mientras golpea, la persecución lo deja quieto mirando al jugador (PersecucionDeNpc.Ocupado). Lo agrega Npc.Crear.
[RequireComponent(typeof(Npc))]
public class AtaqueDeNpc : MonoBehaviour
{
    private const float Impacto = 0.5f;        // CA2: s desde que empieza la animación hasta el daño
    private const float AlcanceDelGolpe = 2f;   // CA2: m; más lejos, falla
    private const float Cadencia = 1f;          // CA4: s entre golpes, como mínimo
    private const float Delante = 60f;          // CA1: grados a cada lado de hacia dónde mira
    private const float DuracionSinClip = 0.8f; // si el tipo no tiene animación de ataque

    private Npc npc;
    private PersecucionDeNpc persecucion;
    private float empezo = -10f, proximo, termina;
    private bool golpeando, pego;
    private Transform objetivo;

    /// <summary>Avisa cada golpe que le hizo daño al jugador: quién, a quién y cuánto.</summary>
    public static event System.Action<Npc, HealthSystem, int> Golpeo;

    private void Awake()
    {
        npc = GetComponent<Npc>();
        persecucion = GetComponent<PersecucionDeNpc>();
    }

    private void Update()
    {
        if (npc == null || npc.Tipo == null || persecucion == null) return;
        if (!npc.Vivo) { Terminar(); enabled = false; return; }

        if (golpeando)
        {
            // CA2: a los 0,5 s, el daño, si el jugador sigue al alcance y adelante.
            if (!pego && Time.time - empezo >= Impacto)
            {
                pego = true;
                Pegar();
            }
            if (Time.time >= termina) Terminar();
            return;
        }

        // CA1 y CA4: al lado del jugador, de frente, y pasado un segundo del golpe anterior.
        if (persecucion.EnDistanciaDeAtaque && persecucion.Objetivo != null && Time.time >= proximo && DeFrente(persecucion.Objetivo))
            Empezar(persecucion.Objetivo);
    }

    private void Empezar(Transform jugador)
    {
        golpeando = true;
        pego = false;
        objetivo = jugador;
        empezo = Time.time;
        proximo = Time.time + Cadencia;
        persecucion.Ocupado = true;

        AnimationClip[] ataques = npc.Tipo.ataques;
        AnimationClip clip = ataques != null && ataques.Length > 0 ? ataques[Random.Range(0, ataques.Length)] : null;
        float dura = clip != null ? clip.length : DuracionSinClip;
        termina = Time.time + Mathf.Max(dura, Impacto + 0.05f);
        if (clip != null && npc.Animaciones != null)
        {
            npc.Animaciones.Poner(clip, false, 0.1f, true);
            npc.Animaciones.Velocidad(1f);
        }
    }

    private void Pegar()
    {
        if (objetivo == null) return;
        HealthSystem vida = objetivo.GetComponent<HealthSystem>();
        if (vida == null || vida.currentHealth <= 0) return;
        Vector3 hasta = objetivo.position - transform.position;
        hasta.y = 0f;
        if (hasta.magnitude > AlcanceDelGolpe || !DeFrente(objetivo)) return; // CA2: se alejó o quedó atrás: falla

        int danio = npc.Tipo.danio;
        HealthSystem.DamageOrigin = transform.position + Vector3.up; // CA5: el aviso de daño hacia el lado del golpe
        HealthSystem.DamageSource = npc.Tipo.nombre;
        vida.TakeDamage(danio); // CA3: primero el escudo y después la vida
        HealthSystem.DamageSource = null;
        Golpeo?.Invoke(npc, vida, danio);
    }

    private void Terminar()
    {
        golpeando = false;
        if (persecucion != null) persecucion.Ocupado = false;
    }

    // CA1: el jugador está delante (no al costado ni atrás).
    private bool DeFrente(Transform jugador)
    {
        Vector3 hacia = jugador.position - transform.position;
        hacia.y = 0f;
        if (hacia.sqrMagnitude < 0.01f) return true;
        return Vector3.Angle(transform.forward, hacia) <= Delante;
    }
}
