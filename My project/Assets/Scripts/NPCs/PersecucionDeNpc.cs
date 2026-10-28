using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// F11 (NPCs): el NPC va hacia el jugador por el mapa.
// US 035 (movimiento):
// - CA1: se mueve con un NavMeshAgent por lo caminable y rodea paredes y obstáculos. Usa el NavMesh del mapa si está
//   horneado (US 187) y, si no, el que se arma con el juego andando (NavMeshEnVivo, el del perro del Callejero).
// - CA2: a la velocidad de su tipo (TipoDeNpc.velocidad).
// - CA3: desde que aparece va hacia el jugador y recalcula el camino cada 0,25 s.
// - CA4: no se encima con los demás (esquiva a los otros agentes) y, cerca del jugador, cada uno busca su lugar
//   alrededor de él, así lo rodean en vez de hacer fila.
// - CA5: camina o corre (según su tipo) con la animación al ritmo de su velocidad; quieto, la de quieto.
// - CA6: a distancia de ataque (DistanciaDeAtaque) se frena mirándolo; el golpe es la US 037.
// US 036 (detectar al jugador):
// - CA1: siempre sabe dónde está el jugador (el más cercano, si hubiera varios) y lo usa de destino.
// - CA2: si lo ve (sin paredes en el medio) a menos de 10 m, grita una vez y va 20 % más rápido mientras lo vea.
// - CA3: si el jugador está donde no se llega caminando, va al punto alcanzable más cercano y espera ahí.
// - CA4: con el jugador muerto deja de perseguirlo y se queda quieto.
// Lo agrega Npc.Crear.
[RequireComponent(typeof(Npc))]
public class PersecucionDeNpc : MonoBehaviour
{
    public const float DistanciaDeAtaque = 1.5f;      // US 037, CA1
    private const float CadaCuanto = 0.25f;            // s entre cálculos del camino (CA3)
    private const float VeHasta = 10f, MasRapido = 1.2f; // US 036, CA2
    private const float Rodear = 4f;                   // m: desde acá cada uno busca su lugar alrededor (CA4)
    private const float BuscarPiso = 50f;              // m: hasta dónde se busca el punto alcanzable (US 036, CA3)

    private static int siguienteLugar;

    // El proyecto entra al Play sin recargar los scripts (Enter Play Mode Options): lo estático queda de la partida
    // anterior (por ejemplo, el jugador ya borrado). Se limpia al empezar cada una.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Limpiar()
    {
        siguienteLugar = 0;
        jugadores.Clear();
        jugadoresEn = -10f;
    }

    private Npc npc;
    private NavMeshAgent agente;
    private int tipoDeAgente = -1;
    private float proximoCamino, proximoAgente;
    private float angulo;          // su lugar alrededor del jugador (CA4)
    private bool grito, loVe;

    /// <summary>El jugador al que va (o null).</summary>
    public Transform Objetivo { get; private set; }
    /// <summary>Está frenado al lado del jugador, listo para golpear (US 037).</summary>
    public bool EnDistanciaDeAtaque { get; private set; }
    /// <summary>Ve al jugador (US 036, CA2).</summary>
    public bool LoVe => loVe;
    /// <summary>Otro componente (el ataque, US 037, o la flash, US 038) maneja las animaciones y lo frena.</summary>
    public bool Ocupado { get; set; }

    private void Awake()
    {
        npc = GetComponent<Npc>();
        // Lugares repartidos alrededor del jugador: cada NPC nuevo, el siguiente (CA4).
        angulo = (siguienteLugar++ * 137.5f) % 360f - 180f;
    }

    private void Update()
    {
        if (npc == null || npc.Tipo == null) return;
        if (!npc.Vivo) { Frenar(); enabled = false; return; } // muerto (US 039)
        if (npc.Aturdido) { Frenar(); return; }                 // la flash lo dejó quieto (US 038, CA2)

        Objetivo = JugadorMasCercano(transform.position);
        if (Objetivo == null) { Frenar(); Animar(false); return; } // US 036, CA4

        if (agente == null || !agente.isOnNavMesh)
        {
            if (Time.time >= proximoAgente) SubirAlNavMesh();
            if (agente == null || !agente.isOnNavMesh) { Animar(false); return; }
        }

        // US 036, CA2: lo ve de cerca: grita una vez y acelera mientras lo vea.
        loVe = Ve(Objetivo);
        if (loVe && !grito) Gritar();
        ReaccionDeNpc reaccion = GetComponent<ReaccionDeNpc>();
        float golpeado = reaccion != null ? reaccion.Velocidad : 1f; // US 038, CA3: recién golpeado, un poco más lento
        agente.speed = npc.Tipo.velocidad * (loVe ? MasRapido : 1f) * golpeado;

        Vector3 hasta = Objetivo.position - transform.position;
        hasta.y = 0f;
        EnDistanciaDeAtaque = hasta.magnitude <= DistanciaDeAtaque;

        // CA6: a distancia de ataque se frena y lo mira.
        if (EnDistanciaDeAtaque || Ocupado)
        {
            agente.isStopped = true;
            agente.velocity = Vector3.zero;
            if (hasta.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(hasta.normalized), 540f * Time.deltaTime);
        }
        else
        {
            agente.isStopped = false;
            if (Time.time >= proximoCamino) Camino();
        }
        if (!Ocupado) Animar(true);
    }

    // CA3 (y US 036, CA3): el camino hacia el jugador; cerca, a su lugar alrededor de él (CA4). Si no se puede llegar,
    // al punto caminable más cercano.
    private void Camino()
    {
        proximoCamino = Time.time + CadaCuanto;
        Vector3 destino = Objetivo.position;
        Vector3 desdeJugador = transform.position - Objetivo.position;
        desdeJugador.y = 0f;
        if (desdeJugador.magnitude < Rodear && desdeJugador.sqrMagnitude > 1e-4f)
        {
            float giro = Mathf.Lerp(angulo, 0f, desdeJugador.magnitude / Rodear) * 0.5f;
            destino = Objetivo.position + Quaternion.Euler(0f, giro, 0f) * desdeJugador.normalized * (DistanciaDeAtaque * 0.8f);
        }
        if (NavMesh.SamplePosition(destino, out NavMeshHit cerca, BuscarPiso, Filtro())) destino = cerca.position;
        agente.SetDestination(destino); // si no llega entero, va hasta donde se pueda y espera (camino parcial)
    }

    // US 036, CA2: sin paredes entre sus ojos y el pecho del jugador, a menos de 10 m.
    private bool Ve(Transform jugador)
    {
        Vector3 ojos = transform.position + Vector3.up * Mathf.Max(0.5f, Alto() * 0.8f);
        Vector3 pecho = jugador.position + Vector3.up * 0.4f;
        Vector3 hacia = pecho - ojos;
        if (hacia.magnitude > VeHasta) return false;
        foreach (RaycastHit g in Physics.RaycastAll(ojos, hacia.normalized, hacia.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (g.collider.transform.IsChildOf(transform) || g.collider.transform.IsChildOf(jugador)) continue;
            if (g.collider.GetComponentInParent<Npc>() != null) continue; // otro bicho no le tapa la vista
            return false;
        }
        return true;
    }

    private void Gritar()
    {
        grito = true;
        AudioClip clip = npc.Tipo.grito;
        if (clip == null) return;
        var go = new GameObject("NPC (grito)");
        go.transform.position = transform.position + Vector3.up;
        AudioSource fuente = go.AddComponent<AudioSource>();
        fuente.clip = clip;
        fuente.spatialBlend = 1f;
        fuente.rolloffMode = AudioRolloffMode.Linear;
        fuente.minDistance = 3f;
        fuente.maxDistance = 30f;
        fuente.outputAudioMixerGroup = GrupoDeEfectos();
        fuente.Play();
        Destroy(go, clip.length + 0.1f);
    }

    // CA5: caminar o correr al ritmo de su velocidad; quieto, la de quieto (frente al jugador, la de combate).
    private void Animar(bool persiguiendo)
    {
        AnimadorDeNpc a = npc.Animaciones;
        if (a == null) return;
        TipoDeNpc t = npc.Tipo;
        float v = agente != null && agente.isOnNavMesh ? agente.velocity.magnitude : 0f;
        if (persiguiendo && v > 0.2f)
        {
            AnimationClip paso = t.corre ? (t.correr != null ? t.correr : t.caminar) : (t.caminar != null ? t.caminar : t.correr);
            a.Poner(paso, true);
            a.Velocidad(Mathf.Clamp(v / t.velocidad, 0.5f, 1.6f));
        }
        else
        {
            AnimationClip quieto = persiguiendo && t.quietoEnCombate != null ? t.quietoEnCombate : t.quieto != null ? t.quieto : t.quietoEnCombate;
            a.Poner(quieto, true);
            a.Velocidad(1f);
        }
    }

    private void Frenar()
    {
        EnDistanciaDeAtaque = false;
        if (agente != null && agente.isOnNavMesh) { agente.isStopped = true; agente.velocity = Vector3.zero; }
    }

    // CA1: un NavMeshAgent sobre el NavMesh horneado del mapa, o el que se arma en vivo. Si todavía no está listo, se
    // vuelve a probar cada medio segundo.
    private void SubirAlNavMesh()
    {
        proximoAgente = Time.time + 0.5f;
        if (tipoDeAgente < 0)
        {
            // ¿El mapa tiene NavMesh horneado (el tipo de agente común, Humanoid)?
            int humanoide = NavMesh.GetSettingsByIndex(0).agentTypeID;
            var filtro = new NavMeshQueryFilter { agentTypeID = humanoide, areaMask = NavMesh.AllAreas };
            if (NavMesh.SamplePosition(transform.position, out _, 3f, filtro)) tipoDeAgente = humanoide;
            else
            {
                NavMeshEnVivo.Preparar(transform.position);
                if (!NavMeshEnVivo.Listo) return;
                tipoDeAgente = NavMeshEnVivo.Tipo;
            }
        }
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit piso, 3f, Filtro())) return;
        CapsuleCollider capsula = GetComponent<CapsuleCollider>();
        if (agente == null)
        {
            transform.position = piso.position;
            agente = gameObject.AddComponent<NavMeshAgent>();
            agente.agentTypeID = tipoDeAgente;
            agente.speed = npc.Tipo.velocidad;
            agente.acceleration = 20f;
            agente.angularSpeed = 540f;
            agente.radius = capsula != null ? Mathf.Clamp(capsula.radius, 0.25f, 1.2f) : 0.4f;
            agente.height = Alto();
            agente.baseOffset = 0f;
            agente.stoppingDistance = 0.1f;
            agente.autoBraking = true;
            agente.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance; // CA4: no se enciman
            agente.avoidancePriority = Random.Range(30, 70);
        }
        else agente.Warp(piso.position);
        proximoCamino = 0f;
    }

    private NavMeshQueryFilter Filtro() => new NavMeshQueryFilter { agentTypeID = tipoDeAgente, areaMask = NavMesh.AllAreas };

    private float Alto()
    {
        CapsuleCollider c = GetComponent<CapsuleCollider>();
        return c != null ? Mathf.Max(0.5f, c.height) : 1.5f;
    }

    // ---------- Los jugadores ----------

    private static readonly List<HealthSystem> jugadores = new List<HealthSystem>();
    private static float jugadoresEn = -10f;

    /// <summary>El jugador vivo más cercano a ese punto (US 036, CA1), o null si no hay ninguno vivo (CA4).</summary>
    public static Transform JugadorMasCercano(Vector3 desde)
    {
        // Cada segundo, o si alguno ya no está (o el reloj volvió a empezar: otra partida).
        if (Time.time - jugadoresEn > 1f || Time.time < jugadoresEn || jugadores.Count == 0 || jugadores.Exists(j => j == null))
        {
            jugadoresEn = Time.time;
            jugadores.Clear();
            foreach (PlayerMovement p in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
            {
                HealthSystem v = p.GetComponent<HealthSystem>();
                if (v != null) jugadores.Add(v);
            }
        }
        Transform mejor = null;
        float mejorDistancia = float.MaxValue;
        foreach (HealthSystem v in jugadores)
        {
            if (v == null || v.currentHealth <= 0) continue;
            float d = (v.transform.position - desde).sqrMagnitude;
            if (d < mejorDistancia) { mejorDistancia = d; mejor = v.transform; }
        }
        return mejor;
    }

    private static UnityEngine.Audio.AudioMixerGroup grupo;
    private static UnityEngine.Audio.AudioMixerGroup GrupoDeEfectos()
    {
        if (grupo == null)
        {
            Pistola pistola = FindFirstObjectByType<Pistola>(FindObjectsInactive.Include);
            if (pistola != null) grupo = pistola.sfxGroup;
        }
        return grupo;
    }
}
