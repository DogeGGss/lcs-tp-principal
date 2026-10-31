using UnityEngine;

// F11 (NPCs), US 037: el NPC golpea al jugador cuando lo alcanza.
// - CA1: ataca cuando el jugador está a menos de 1,5 m (PersecucionDeNpc lo frena ahí) y delante de él. Las distancias
//   son entre los cuerpos (del frente del NPC, según sus zonas de impacto, al costado del jugador), no entre los
//   centros: así el Tanque, que es más largo, no se mete adentro del jugador para pegarle.
// - CA2: el golpe hace daño cuando se ve el manotazo en la animación (GolpeDeNpc: el momento en que el cuerpo llega más
//   adelante; 0,5 s si no se pudo medir). Si en ese momento el jugador quedó más allá de donde llega la animación,
//   más el alcance de su tipo (TipoDeNpc.alcance), o quedó atrás, falla. De sus animaciones de ataque elige al azar
//   entre las que llegan hasta el jugador desde donde está.
// - CA3: hace el daño de su tipo (TipoDeNpc.danio). El sistema de vida del jugador lo aplica primero al escudo y el
//   resto a la vida (HealthSystem.TakeDamage).
// - CA4: golpea como máximo una vez por segundo (y nunca antes de terminar la animación del golpe anterior).
// - CA5: el aviso de daño en rojo hacia el lado del golpe es el de la US 192: se le dice desde dónde vino
//   (HealthSystem.DamageOrigin).
// Mientras golpea, la persecución lo deja quieto mirando al jugador (PersecucionDeNpc.Ocupado). Lo agrega Npc.Crear.
[RequireComponent(typeof(Npc))]
public class AtaqueDeNpc : MonoBehaviour
{
    private const float ImpactoSinMedir = 0.5f; // CA2: s desde que empieza la animación hasta el daño, si no se midió
    // CA4: s entre golpes, como mínimo: los de su tipo (TipoDeNpc.cadencia; el jefe, 1,5 s, US 266).
    private const float Delante = 60f;          // CA1: grados a cada lado de hacia dónde mira
    private const float DuracionSinClip = 0.8f; // si el tipo no tiene animación de ataque

    private Npc npc;
    private PersecucionDeNpc persecucion;
    private float empezo = -10f, proximo, termina;
    private float impacto = ImpactoSinMedir, llega; // del golpe en curso: cuándo pega y hasta dónde llega (m entre los cuerpos)
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
        if (npc.Aturdido) { if (golpeando) Terminar(); return; } // la flash corta el golpe (US 038, CA2)

        if (golpeando)
        {
            // CA2: en el momento del manotazo, el daño, si el jugador sigue al alcance y adelante.
            if (!pego && Time.time - empezo >= impacto)
            {
                pego = true;
                Pegar();
            }
            if (Time.time >= termina) Terminar();
            return;
        }

        // CA1 y CA4: al lado del jugador, de frente, y pasado un segundo del golpe anterior.
        // (No mientras otro ataque, como la embestida del jefe, lo tiene ocupado.)
        if (!persecucion.Ocupado && persecucion.EnDistanciaDeAtaque && persecucion.Objetivo != null && Time.time >= proximo && DeFrente(persecucion.Objetivo))
            Empezar(persecucion.Objetivo);
    }

    private void Empezar(Transform jugador)
    {
        golpeando = true;
        pego = false;
        objetivo = jugador;
        empezo = Time.time;
        proximo = Time.time + Mathf.Max(0.2f, npc.Tipo.cadencia / Mathf.Max(0.1f, npc.MasVelocidadDeAtaque));
        persecucion.Ocupado = true;

        AnimationClip clip = Elegir(persecucion.EntreCuerpos(jugador));
        GolpeDeNpc? medido = npc.GolpeDe(clip);
        float rapido = Mathf.Max(0.1f, npc.Tipo.velocidadDeAtaque * npc.MasVelocidadDeAtaque); // más rápido: el golpe entra antes y termina antes
        impacto = (medido.HasValue ? medido.Value.momento : ImpactoSinMedir) / rapido;
        llega = (medido.HasValue ? medido.Value.avance : 0f) + npc.Tipo.alcance;
        float dura = (clip != null ? clip.length : DuracionSinClip) / rapido;
        termina = Time.time + Mathf.Max(dura, impacto + 0.05f);
        if (clip != null && npc.Animaciones != null)
        {
            npc.Animaciones.Poner(clip, false, 0.1f, true);
            npc.Animaciones.Velocidad(rapido);
        }
        SonidoDeNpc.Uno(npc.Tipo.sonidoDeAtaque, transform, npc.Tipo.esJefe ? 40f : 25f);
    }

    private void Pegar()
    {
        if (objetivo == null) return;
        HealthSystem vida = objetivo.GetComponent<HealthSystem>();
        if (vida == null || vida.currentHealth <= 0) return;
        if (persecucion.EntreCuerpos(objetivo) > llega || !DeFrente(objetivo)) return; // CA2: se alejó o quedó atrás: falla

        int danio = DificultadDeInvasion.DanioDeEnemigo(npc.Tipo.danio); // US 161, CA3
        HealthSystem.DamageOrigin = transform.position + Vector3.up; // CA5: el aviso de daño hacia el lado del golpe
        HealthSystem.DamageSource = npc.Tipo.nombre;
        vida.TakeDamage(danio); // CA3: primero el escudo y después la vida
        HealthSystem.DamageSource = null;
        Golpeo?.Invoke(npc, vida, danio);
    }

    // Al azar entre sus animaciones de ataque que llegan hasta el jugador desde donde está (las cortas, si está pegado;
    // la embestida, si quedó más lejos). Si ninguna llega, la que más se adelanta.
    private AnimationClip Elegir(float entre)
    {
        AnimationClip[] ataques = npc.Tipo.ataques;
        if (ataques == null || ataques.Length == 0) return null;
        var llegan = new System.Collections.Generic.List<AnimationClip>();
        AnimationClip larga = null;
        float masLarga = float.MinValue;
        foreach (AnimationClip c in ataques)
        {
            if (c == null) continue;
            GolpeDeNpc? g = npc.GolpeDe(c);
            float avance = g.HasValue ? g.Value.avance : 0f;
            if (avance + npc.Tipo.alcance >= entre) llegan.Add(c);
            if (avance > masLarga) { masLarga = avance; larga = c; }
        }
        return llegan.Count > 0 ? llegan[Random.Range(0, llegan.Count)] : larga;
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
