using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Audio;

// US 022 · El perro espectral del Callejero. Lo suelta HabilidadCallejero y sale corriendo a 12 m/s (más rápido que un jugador corriendo) hacia el rival vivo
// más cercano, esté a la distancia que esté, siguiéndolo si se mueve (CA1 y CA5). Corre por el NavMesh que arma NavMeshEnVivo: sigue
// pasillos, puertas, escaleras y rampas y no atraviesa paredes. Si no hay rivales, corre 10 m hacia adelante y
// desaparece (CA6). Al alcanzar al rival lo muerde y desaparece: la mordida no saca vida, le nubla la vista 2,5 s y lo
// marca 3 s a través de las paredes para el equipo del dueño (CA7 y CA8). Tiene 60 de vida: los disparos, el cuchillo y
// las granadas de los rivales lo pueden matar (CA9). Si en 15 s no alcanzó a nadie, desaparece (CA10).
// Se ve como un holograma (CA2): naranja, espectral, para el dueño y su equipo, y rojo para los rivales (CA3).
// En línea (CA15), el perro se mueve en la computadora del dueño, que manda dónde está y si mordió o desapareció
// (JugadorEnRed); en las demás es una copia que sigue esas posiciones. El daño llega a todas por el servidor, en el
// mismo orden, así la vida y la muerte dan igual en todas.
// Revisión (testers: a veces no seguía o no atacaba bien al rival):
// - Si el NavMesh no estaba listo al soltarlo, corría derecho para siempre y se trababa contra las paredes: ahora se
//   sube al NavMesh en cuanto está listo, y vuelve a subirse si se sale.
// - Pedía el camino de nuevo en cada cuadro y, con un rival lejos, a veces no llegaba a calcularlo y se quedaba
//   dudando: ahora lo pide cuando el rival se movió o cada un ratito.
// - Elegía al rival más cercano en línea recta, aunque estuviera detrás de una pared y otro estuviera más cerca
//   caminando, y cambiaba de uno a otro: ahora mide por el camino que tiene que hacer y solo cambia si el otro está
//   bastante más cerca.
// - Si el rival estaba arriba de algo sin camino (una caja, un escalón alto), se quedaba abajo sin morder: ahora, si
//   está cerca, a la vista y no puede acercarse más, salta y lo muerde.
public class Perro : MonoBehaviour
{
    public const float Velocidad = 12f;     // CA5: más que los 8 m/s de un jugador corriendo, así lo alcanza
    public const float SinRival = 10f;      // CA6
    public const float Duracion = 15f;      // CA10: lo que tiene para alcanzar a alguien
    public const float Espera = 5f;         // CA6: sentado esperando a que aparezca un rival
    public const float Persecucion = Duracion; // lo que tiene para alcanzar a uno que apareció mientras esperaba
    public const int VidaMax = 60;          // CA9
    public const float Nublado = 2.5f;      // CA7
    public const float Marca = 3f;          // CA8
    private const float Llegar = 1.1f;      // m al rival para morderlo
    private const float Altura = 0.8f;      // alto del perro
    private const float PasoCorrer = 8f;    // m/s a los que la animación de correr se ve a su velocidad normal
    private const float Salto = 2.4f;       // m al rival para saltarle encima si no puede acercarse más
    private const float CadaCamino = 0.15f; // s entre pedidos de camino (o antes, si el rival se movió)

    public enum Fin { Mordio, Murio, Desaparecio }

    // ---------- Red (las pone JugadorEnRed; null sin conexión) ----------
    public static System.Action<Perro, Vector3, float> EnviarPosicion;
    public static System.Action<Perro, int, Vector3> EnviarDanio;
    public static System.Action<Perro, Fin, int> EnviarFin;

    private static readonly Dictionary<long, Perro> activos = new Dictionary<long, Perro>();
    private static Material materialHolo, materialRastro;
    private static Mesh esfera, capsula, cubo;

    public int Actor { get; private set; }
    public int Id { get; private set; }
    public Transform Duenio { get; private set; }
    public int Vida { get; private set; }
    public bool Activo { get; private set; }

    private bool propio;           // se mueve en esta computadora
    private AudioMixerGroup grupo;
    private float creado, proximaBusqueda, proximoEnvio, proximoLadrido, finDesvanecer = -1f, desvanecer = 0.5f;
    private NavMeshAgent agente;
    private HealthSystem objetivo;
    private Vector3 adelante, destinoSinRival;
    private Vector3 redLugar;
    private float redGiro;
    private Vector3 ultimoLugar;
    private float velocidadVista;

    private Material holo;
    private Animator animador;
    private readonly List<Transform> patas = new List<Transform>();
    private Transform cuerpo;
    private AudioSource grunido;
    private TrailRenderer rastro;
    private Vector3 lunge; // el salto de la mordida, en el perro hecho a mano
    private bool corriendo, sentadoVista;
    private float limite;    // cuándo desaparece si no mordió a nadie
    private bool esperando;  // CA6: llegó a los 10 m sin rivales y espera sentado
    private float caida;     // sin NavMesh: velocidad con la que cae si quedó en el aire
    private float lentoDesde = -1f; // desde cuándo está casi quieto (las copias se sientan después de un rato quietas)
    private float proximoAgente;    // cuándo vuelve a probar subirse al NavMesh
    private float proximoCamino;    // cuándo vuelve a pedir el camino
    private Vector3 destinoPedido = new Vector3(float.NaN, 0f, 0f);
    private float trabadoDesde = -1f; // desde cuándo casi no avanza yendo hacia el rival
    private static NavMeshPath caminoPrueba;

    // CA4: las animaciones del lobo, sin el controlador de demostración del asset (encadenaba correr, sentarse,
    // caminar, atacar y morir solas). Se mezclan acá: correr y sentado se repiten, atacar se ve una vez.
    private const int Correr = 0, Sentado = 1, Morder_ = 2;
    private PlayableGraph grafo;
    private AnimationMixerPlayable mezcla;
    private readonly AnimationClipPlayable[] clips = new AnimationClipPlayable[3];
    private readonly bool[] hayClip = new bool[3];
    private bool hayGrafo;
    private int pose = Correr;

    // =====================================================================
    // Crear y buscar
    // =====================================================================

    /// <param name="propio">Se mueve en esta computadora (la del dueño).</param>
    public static Perro Crear(Transform duenio, int actor, int id, Vector3 lugar, float yaw, bool propio, AudioMixerGroup grupo)
    {
        Buscar(actor, id)?.Quitar();
        var go = new GameObject("Perro (Callejero)");
        go.transform.SetPositionAndRotation(lugar, Quaternion.Euler(0f, yaw, 0f));
        Perro perro = go.AddComponent<Perro>();
        perro.Iniciar(duenio, actor, id, propio, grupo);
        return perro;
    }

    public static Perro Buscar(int actor, int id) => activos.TryGetValue(Clave(actor, id), out Perro p) ? p : null;
    public static Perro De(Collider c) => c != null ? c.GetComponentInParent<Perro>() : null;

    /// <summary>La altura del piso debajo de ese lugar (hasta 60 m), sin contar perros, personajes ni lo que se ignora.
    /// Así, si se suelta saltando, el perro cae al piso en vez de quedar en el aire.</summary>
    public static bool Piso(Vector3 lugar, Transform ignorar, out float altura)
    {
        altura = lugar.y;
        RaycastHit[] golpes = Physics.RaycastAll(lugar + Vector3.up * 0.8f, Vector3.down, 60f, Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        float mejor = float.PositiveInfinity;
        foreach (RaycastHit g in golpes)
        {
            Collider c = g.collider;
            if (De(c) != null || c is CharacterController || c.GetComponentInParent<HealthSystem>() != null) continue;
            if (ignorar != null && c.transform.IsChildOf(ignorar)) continue;
            if (g.distance < mejor) { mejor = g.distance; altura = g.point.y; }
        }
        return !float.IsPositiveInfinity(mejor);
    }
    private static long Clave(int actor, int id) => ((long)actor << 32) | (uint)id;

    /// <summary>Quita todos sin efectos (por ejemplo, al empezar una ronda del Táctico).</summary>
    public static void QuitarTodos()
    {
        foreach (Perro p in new List<Perro>(activos.Values)) if (p != null) p.Quitar();
        activos.Clear();
    }

    private void Iniciar(Transform duenio, int actor, int id, bool propio, AudioMixerGroup grupo)
    {
        Duenio = duenio;
        Actor = actor;
        Id = id;
        Vida = VidaMax;
        Activo = true;
        this.propio = propio;
        this.grupo = grupo != null ? grupo : Grupo(duenio);
        creado = Time.time;
        activos[Clave(actor, id)] = this;
        adelante = transform.forward;
        redLugar = ultimoLugar = transform.position;
        redGiro = transform.eulerAngles.y;

        // CA9: las balas lo tocan. Un poco más chico que el dibujo, a la altura del cuerpo.
        CapsuleCollider choque = gameObject.AddComponent<CapsuleCollider>();
        choque.direction = 2;
        choque.radius = 0.24f;
        choque.height = 0.95f;
        choque.center = new Vector3(0f, 0.42f, 0f);

        ArmarDibujo();

        // CA11: ladra de a ratos mientras corre (y, si no hay ladridos cargados, gruñe de fondo); se oye a 20 m.
        grunido = SonidoPerro.Fuente(gameObject, this.grupo, false);
        if (!SonidoPerro.HayLadridos)
        {
            grunido.clip = SonidoPerro.Clip(SonidoPerro.Tipo.Grunido);
            grunido.loop = true;
            grunido.Play();
        }
        proximoLadrido = Time.time + 0.6f;

        if (propio)
        {
            if (Piso(transform.position, duenio, out float suelo) && suelo < transform.position.y)
                transform.position = new Vector3(transform.position.x, suelo, transform.position.z);
            destinoSinRival = transform.position + adelante * SinRival;
            SubirAlNavMesh();
            Buscar();
        }
    }

    // Lo pone a correr por el NavMesh (si ya está armado). Si no está listo todavía, o se salió de él, lo vuelve a probar
    // cada medio segundo desde Simular: antes, si lo soltaban antes de que estuviera, corría derecho todo el tiempo.
    private void SubirAlNavMesh()
    {
        proximoAgente = Time.time + 0.5f;
        if (!NavMeshEnVivo.Listo) return;
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit piso, 2.5f, Filtro())) return;
        if (agente == null)
        {
            transform.position = piso.position;
            agente = gameObject.AddComponent<NavMeshAgent>();
            agente.agentTypeID = NavMeshEnVivo.Tipo;
            agente.speed = Velocidad;
            agente.acceleration = 60f;
            agente.angularSpeed = 900f;
            agente.radius = 0.3f;
            agente.height = 0.9f;
            agente.baseOffset = 0f;
            agente.stoppingDistance = 0f;
            agente.autoBraking = false;
            agente.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            if (NavMesh.SamplePosition(destinoSinRival, out NavMeshHit lejos, 3f, Filtro())) destinoSinRival = lejos.position;
        }
        else if (!agente.isOnNavMesh) agente.Warp(piso.position);
        destinoPedido = new Vector3(float.NaN, 0f, 0f); // pide el camino de nuevo
    }

    private static NavMeshQueryFilter Filtro() => new NavMeshQueryFilter { agentTypeID = NavMeshEnVivo.Tipo, areaMask = NavMesh.AllAreas };

    /// <summary>El canal de efectos (SFX) del jugador, el de sus armas: así lo regulan General y Efectos de Opciones.</summary>
    public static AudioMixerGroup Grupo(Transform jugador)
    {
        if (jugador == null) return null;
        Pistola pistola = jugador.GetComponentInChildren<Pistola>(true);
        return pistola != null ? pistola.sfxGroup : null;
    }

    // =====================================================================
    // Movimiento
    // =====================================================================

    private void Update()
    {
        if (finDesvanecer > 0f)
        {
            float k = 1f - Mathf.Clamp01((finDesvanecer - Time.time) / desvanecer);
            if (holo != null) holo.SetFloat("_Desvanecer", k);
            if (grunido != null) grunido.volume *= 0.9f;
            if (Time.time >= finDesvanecer) Destroy(gameObject);
            Animar();
            return;
        }
        if (!Activo) return;
        if (!corriendo && animador != null) { corriendo = true; Pose(Correr); }

        if (propio) Simular();
        else Seguir();

        if (Time.time >= proximoLadrido)
        {
            proximoLadrido = Time.time + Random.Range(0.9f, 1.6f);
            AudioClip ladrido = SonidoPerro.Ladrido();
            if (grunido != null && ladrido != null) grunido.PlayOneShot(ladrido);
        }
        Animar();
    }

    private void Simular()
    {
        if (limite <= 0f) limite = creado + Duracion;
        if (Time.time >= limite) { Terminar(Fin.Desaparecio, -1, null, true); return; } // CA6 y CA10

        if ((agente == null || !agente.isOnNavMesh) && Time.time >= proximoAgente) SubirAlNavMesh();
        if (objetivo != null && !EsRival(objetivo)) { objetivo = null; Buscar(); } // murió: va enseguida por otro
        if (Time.time >= proximaBusqueda) Buscar();
        // Esperando sentado aparece un rival: se levanta y sale a buscarlo, con un rato para alcanzarlo.
        if (esperando && objetivo != null)
        {
            esperando = false;
            limite = Mathf.Max(limite, Time.time + Persecucion);
            if (agente != null && agente.isOnNavMesh) agente.isStopped = false;
        }
        if (esperando)
        {
            if (Time.time >= proximoEnvio)
            {
                proximoEnvio = Time.time + 0.25f;
                EnviarPosicion?.Invoke(this, transform.position, transform.eulerAngles.y);
            }
            return;
        }

        Vector3 destino = objetivo != null ? objetivo.transform.position : destinoSinRival;
        if (agente != null && agente.isOnNavMesh)
        {
            // El camino se pide cuando el rival se movió o cada un ratito, no en cada cuadro: calcularlo lleva más de un
            // cuadro y, pidiéndolo siempre de nuevo, a veces no terminaba y el perro se quedaba dudando.
            bool movido = float.IsNaN(destinoPedido.x) || (destino - destinoPedido).sqrMagnitude > 0.5f * 0.5f;
            if (movido && Time.time >= proximoCamino || Time.time >= proximoCamino + 0.5f)
            {
                proximoCamino = Time.time + CadaCamino;
                destinoPedido = destino;
                agente.SetDestination(destino);
            }
        }
        else
        {
            // Sin NavMesh (todavía no se armó): derecho hacia el destino, frenando contra las paredes.
            Vector3 hacia = destino - transform.position;
            hacia.y = 0f;
            if (hacia.sqrMagnitude > 0.01f)
            {
                Vector3 dir = hacia.normalized;
                float paso = Mathf.Min(Velocidad * Time.deltaTime, hacia.magnitude);
                if (!Physics.SphereCast(transform.position + Vector3.up * 0.45f, 0.25f, dir, out RaycastHit _, paso + 0.1f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    transform.position += dir * paso;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 900f * Time.deltaTime);
            }
            // Pegado al piso: sube escalones y, si quedó en el aire, cae.
            if (Piso(transform.position, Duenio, out float suelo))
            {
                float y = transform.position.y;
                if (suelo >= y - 0.05f || suelo > y - 0.6f && caida <= 0f) { y = suelo; caida = 0f; }
                else { caida += 25f * Time.deltaTime; y = Mathf.Max(suelo, y - caida * Time.deltaTime); }
                transform.position = new Vector3(transform.position.x, y, transform.position.z);
            }
        }

        if (objetivo != null)
        {
            Vector3 d = objetivo.transform.position - transform.position;
            float plano = new Vector2(d.x, d.z).magnitude;
            if (plano <= Llegar && Mathf.Abs(d.y) < 1.8f) { Morder(objetivo); return; }
            // Cerca pero sin poder acercarse más (arriba de una caja, del otro lado de un escalón alto): salta y lo muerde,
            // si lo tiene a la vista.
            bool avanzando = velocidadVista > 2f;
            if (avanzando || plano > Salto) trabadoDesde = -1f;
            else if (trabadoDesde < 0f) trabadoDesde = Time.time;
            bool sinCamino = agente != null && agente.isOnNavMesh && !agente.pathPending &&
                             agente.pathStatus != NavMeshPathStatus.PathComplete;
            if (plano <= Salto && Mathf.Abs(d.y) < 2.5f && (sinCamino || trabadoDesde >= 0f && Time.time - trabadoDesde > 0.3f) &&
                ALaVista(objetivo)) { Morder(objetivo); return; }
        }
        else
        {
            Vector3 d = destinoSinRival - transform.position;
            if (new Vector2(d.x, d.z).magnitude < 0.6f)
            {
                // CA6: llegó a los 10 m sin rivales: se sienta y espera 5 s. Si aparece uno, va a buscarlo.
                esperando = true;
                limite = Time.time + Espera;
                if (agente != null && agente.isOnNavMesh) { agente.isStopped = true; agente.velocity = Vector3.zero; }
                EnviarPosicion?.Invoke(this, transform.position, transform.eulerAngles.y);
                return;
            }
        }

        if (Time.time >= proximoEnvio)
        {
            proximoEnvio = Time.time + 0.1f;
            EnviarPosicion?.Invoke(this, transform.position, transform.eulerAngles.y);
        }
    }

    // CA1 y CA5: el rival vivo más cercano, sin límite de distancia (lo sigue aunque se aleje, mientras siga vivo).
    // "Más cercano" es por el camino que tiene que correr (por el NavMesh), no en línea recta: uno detrás de una pared
    // puede estar mucho más lejos. Con uno elegido, cambia solo si otro está bastante más cerca (si no, iba y venía).
    private void Buscar()
    {
        proximaBusqueda = Time.time + 0.25f;
        HealthSystem mejor = objetivo != null && EsRival(objetivo) ? objetivo : null;
        float cerca = mejor != null ? Distancia(mejor) * 0.7f - 1f : float.PositiveInfinity;
        foreach (HealthSystem h in FindObjectsByType<HealthSystem>(FindObjectsSortMode.None))
        {
            if (h == mejor || !EsRival(h)) continue;
            float d = Distancia(h);
            if (d < cerca) { cerca = d; mejor = h; }
        }
        objetivo = mejor;
    }

    // Cuánto tiene que correr hasta ese rival: el largo del camino por el NavMesh. Si no hay camino completo, lo que
    // falta en línea recta cuenta el triple (seguramente hay que dar la vuelta); sin NavMesh, la línea recta.
    private float Distancia(HealthSystem h)
    {
        Vector3 desde = transform.position, hasta = h.transform.position;
        float recta = Vector3.Distance(desde, hasta);
        if (agente == null || !agente.isOnNavMesh || !NavMeshEnVivo.Listo) return recta;
        if (!NavMesh.SamplePosition(hasta, out NavMeshHit piso, 2.5f, Filtro())) return recta * 3f;
        if (caminoPrueba == null) caminoPrueba = new NavMeshPath();
        if (!NavMesh.CalculatePath(desde, piso.position, Filtro(), caminoPrueba) || caminoPrueba.status == NavMeshPathStatus.PathInvalid)
            return recta * 3f;
        Vector3[] esquinas = caminoPrueba.corners;
        float largo = 0f;
        for (int i = 1; i < esquinas.Length; i++) largo += Vector3.Distance(esquinas[i - 1], esquinas[i]);
        if (caminoPrueba.status == NavMeshPathStatus.PathPartial && esquinas.Length > 0)
            largo += Vector3.Distance(esquinas[esquinas.Length - 1], hasta) * 3f;
        return largo;
    }

    // Lo ve sin una pared en el medio (de la cabeza del perro al pecho del rival).
    private bool ALaVista(HealthSystem h)
    {
        Vector3 desde = transform.position + Vector3.up * 0.6f, hasta = h.transform.position + Vector3.up * 0.9f;
        Vector3 hacia = hasta - desde;
        foreach (RaycastHit g in Physics.RaycastAll(desde, hacia.normalized, hacia.magnitude, Physics.DefaultRaycastLayers,
                     QueryTriggerInteraction.Ignore))
        {
            Collider c = g.collider;
            if (De(c) != null || c is CharacterController || c.GetComponentInParent<HealthSystem>() != null) continue;
            if (Duenio != null && c.transform.IsChildOf(Duenio)) continue;
            return false;
        }
        return true;
    }

    private bool EsRival(HealthSystem h)
    {
        if (h == null || h.currentHealth <= 0) return false;
        if (Duenio != null && (h.transform.IsChildOf(Duenio) || Duenio.IsChildOf(h.transform))) return false;
        JugadorEnRed jugador = h.GetComponentInParent<JugadorEnRed>();
        if (jugador == null) return true; // zombis y muñecos de práctica
        if (!jugador.Vivo || jugador.Actor == Actor) return false;
        return Actor < 0 || !EquiposTacticos.SonAliados(Actor, jugador.Actor);
    }

    // La copia de otra computadora va hacia donde dice el dueño.
    private void Seguir()
    {
        transform.position = Vector3.Lerp(transform.position, redLugar, 1f - Mathf.Exp(-14f * Time.deltaTime));
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, redGiro, 0f), 1f - Mathf.Exp(-14f * Time.deltaTime));
    }

    /// <summary>CA15: dónde está el perro según su dueño.</summary>
    public void PosicionRed(Vector3 lugar, float giro)
    {
        redLugar = lugar;
        redGiro = giro;
    }

    // =====================================================================
    // Mordida, daño y fin
    // =====================================================================

    private void Morder(HealthSystem victima)
    {
        JugadorEnRed jugador = victima.GetComponentInParent<JugadorEnRed>();
        Terminar(Fin.Mordio, jugador != null ? jugador.Actor : -1, victima.transform, true);
    }

    /// <summary>CA9: un golpe de alguien. Los del dueño y sus compañeros no le hacen nada. Devuelve si le hizo daño.</summary>
    public bool PedirDanio(int danio, Component atacante, Vector3 punto)
    {
        if (!Activo || danio <= 0 || EsAliadoDe(atacante)) return false;
        if (EnviarDanio != null) EnviarDanio(this, danio, punto);
        else AplicarDanio(danio, punto);
        return true;
    }

    public void AplicarDanio(int danio, Vector3 punto)
    {
        if (!Activo) return;
        Vida = Mathf.Max(0, Vida - danio);
        if (Vida <= 0) Terminar(Fin.Murio, -1, null, false);
    }

    public bool EsAliadoDe(Component atacante)
    {
        if (atacante == null) return false;
        if (Duenio != null && atacante.transform.IsChildOf(Duenio)) return true;
        JugadorEnRed jugador = atacante.GetComponentInParent<JugadorEnRed>();
        if (jugador == null || Actor < 0) return false;
        return jugador.Actor == Actor || EquiposTacticos.SonAliados(jugador.Actor, Actor);
    }

    /// <summary>Termina: mordió (a quién), murió o desapareció. El dueño lo avisa a los demás (avisar = true).</summary>
    public void Terminar(Fin fin, int actorVictima, Transform victima, bool avisar)
    {
        if (!Activo) return;
        Activo = false;
        if (activos.TryGetValue(Clave(Actor, Id), out Perro p) && p == this) activos.Remove(Clave(Actor, Id));
        if (agente != null) { agente.enabled = false; }
        Collider choque = GetComponent<Collider>();
        if (choque != null) choque.enabled = false;
        if (rastro != null) rastro.emitting = false;
        if (grunido != null) grunido.loop = false;

        // CA17: un sonido distinto en cada caso.
        SonidoPerro.Tipo sonido = fin == Fin.Mordio ? SonidoPerro.Tipo.Mordida : fin == Fin.Murio ? SonidoPerro.Tipo.Muerte : SonidoPerro.Tipo.Desaparece;
        SonidoPerro.Tocar(sonido, transform.position + Vector3.up * 0.5f, grupo);

        if (fin == Fin.Mordio)
        {
            // CA4: la animación de atacar y después se desvanece.
            Atacar(victima);
            desvanecer = 0.5f;
            finDesvanecer = Time.time + 0.35f + desvanecer;
            EfectosDeMordida(Actor, victima);
        }
        else
        {
            desvanecer = fin == Fin.Murio ? 0.35f : 0.5f;
            finDesvanecer = Time.time + desvanecer;
        }
        if (avisar && propio) EnviarFin?.Invoke(this, fin, actorVictima);
    }

    private void Quitar()
    {
        Activo = false;
        if (activos.TryGetValue(Clave(Actor, Id), out Perro p) && p == this) activos.Remove(Clave(Actor, Id));
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (activos.TryGetValue(Clave(Actor, Id), out Perro p) && p == this) activos.Remove(Clave(Actor, Id));
        if (holo != null) Destroy(holo);
        if (hayGrafo && grafo.IsValid()) grafo.Destroy();
    }

    /// <summary>CA7 y CA8, en esta computadora: si el mordido es el jugador de acá, se le nubla la vista; si el jugador de
    /// acá es el dueño o un compañero suyo, ve al mordido a través de las paredes.</summary>
    public static void EfectosDeMordida(int actorDuenio, Transform victima)
    {
        if (victima == null) return;
        JugadorEnRed yo = PartidaEnRed.Actual != null ? PartidaEnRed.Actual.Local : null;
        if (yo == null)
        {
            // Sin conexión: el jugador de acá es el dueño.
            MarcaAtraves.Marcar(victima, Marca, MatchHud.RivalColor);
            return;
        }
        if (victima == yo.transform || victima.IsChildOf(yo.transform)) { VistaNublada.Aplicar(Nublado); return; }
        if (yo.Actor == actorDuenio || EquiposTacticos.SonAliados(yo.Actor, actorDuenio))
            MarcaAtraves.Marcar(victima, Marca, MatchHud.RivalColor);
    }

    // =====================================================================
    // Dibujo (CA2, CA3 y CA4)
    // =====================================================================

    // CA3: el dueño y su equipo lo ven naranja, espectral; los rivales, rojo.
    private Color ColorParaMi()
    {
        ConfigRed config = ConfigRed.Actual;
        Color propioColor = config != null ? config.colorPerro : new Color(1f, 0.55f, 0.12f);
        if (propio || Actor < 0) return propioColor;
        JugadorEnRed yo = PartidaEnRed.Actual != null ? PartidaEnRed.Actual.Local : null;
        if (yo == null) return propioColor;
        bool rival = yo.Actor != Actor && !EquiposTacticos.SonAliados(yo.Actor, Actor);
        return rival ? MatchHud.RivalColor : propioColor;
    }

    private void ArmarDibujo()
    {
        Color color = ColorParaMi();
        holo = new Material(MaterialHolo());
        holo.SetColor("_BaseColor", color);

        ConfigRed config = ConfigRed.Actual;
        if (config != null && config.modeloPerro != null) ArmarModelo(config.modeloPerro);
        else ArmarAMano();

        // CA2: el rastro al correr.
        var r = new GameObject("Rastro");
        r.transform.SetParent(transform, false);
        r.transform.localPosition = new Vector3(0f, 0.45f, -0.2f);
        rastro = r.AddComponent<TrailRenderer>();
        rastro.time = 0.35f;
        rastro.startWidth = 0.45f;
        rastro.endWidth = 0f;
        rastro.minVertexDistance = 0.1f;
        rastro.sharedMaterial = MaterialRastro();
        rastro.startColor = new Color(color.r, color.g, color.b, 0.45f);
        rastro.endColor = new Color(color.r, color.g, color.b, 0f);
        rastro.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // El lobo del Asset Store (ConfigRed.modeloPerro): se ajusta a 80 cm de alto, sin sus colisiones y con el holograma.
    private void ArmarModelo(GameObject modelo)
    {
        GameObject m = Instantiate(modelo, transform);
        m.transform.localPosition = Vector3.zero;
        m.transform.localRotation = Quaternion.identity;
        foreach (Collider c in m.GetComponentsInChildren<Collider>(true)) Destroy(c);
        Renderer[] partes = m.GetComponentsInChildren<Renderer>(true);
        Bounds b = default;
        bool hay = false;
        foreach (Renderer r in partes)
        {
            // El pelo del lobo son láminas con transparencia: con el holograma se verían como placas. Se ocultan.
            if (EsPelo(r)) { r.enabled = false; continue; }
            if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = holo;
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        if (hay && b.size.y > 0.01f) m.transform.localScale *= Altura / b.size.y;
        animador = m.GetComponentInChildren<Animator>();
        if (animador == null) animador = m.AddComponent<Animator>();
        ConfigRed cfg = ConfigRed.Actual;
        if (animador.runtimeAnimatorController == null && cfg != null) animador.runtimeAnimatorController = cfg.animacionesPerro;
        animador.applyRootMotion = false;
        animador.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        ArmarAnimaciones(cfg);
        Pose(Correr);
        cuerpo = m.transform;
    }

    private static bool EsPelo(Renderer r)
    {
        string n = r.gameObject.name.ToLowerInvariant();
        if (n.Contains("fur") || n.Contains("hair")) return true;
        foreach (Material m in r.sharedMaterials)
            if (m != null && (m.name.ToLowerInvariant().Contains("fur") || m.name.ToLowerInvariant().Contains("hair"))) return true;
        return false;
    }

    // Toma los clips de correr, sentado y atacar del controlador del lobo y los reproduce con un PlayableGraph propio.
    private void ArmarAnimaciones(ConfigRed cfg)
    {
        RuntimeAnimatorController controlador = animador.runtimeAnimatorController;
        if (controlador == null || cfg == null) return;
        AnimationClip[] todos = controlador.animationClips;
        AnimationClip[] elegidos = { Clip(todos, cfg.estadoPerroCorrer), Clip(todos, cfg.estadoPerroSentado), Clip(todos, cfg.estadoPerroAtacar) };
        if (elegidos[Correr] == null) return; // sin el de correr queda el controlador, como antes

        grafo = PlayableGraph.Create("Perro");
        grafo.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        mezcla = AnimationMixerPlayable.Create(grafo, 3);
        for (int i = 0; i < 3; i++)
        {
            if (elegidos[i] == null) continue;
            clips[i] = AnimationClipPlayable.Create(grafo, elegidos[i]);
            clips[i].SetApplyFootIK(false);
            grafo.Connect(clips[i], 0, mezcla, i);
            hayClip[i] = true;
        }
        mezcla.SetInputWeight(Correr, 1f);
        AnimationPlayableOutput salida = AnimationPlayableOutput.Create(grafo, "Perro", animador);
        salida.SetSourcePlayable(mezcla);
        animador.runtimeAnimatorController = null;
        grafo.Play();
        hayGrafo = true;
    }

    // El clip de ese estado: los del lobo se llaman como el estado más un número ("Running_New (1)").
    private static AnimationClip Clip(AnimationClip[] todos, string estado)
    {
        if (string.IsNullOrEmpty(estado) || todos == null) return null;
        string buscado = SoloLetras(estado);
        foreach (AnimationClip c in todos)
            if (c != null && SoloLetras(c.name).StartsWith(buscado)) return c;
        return null;
    }

    private static string SoloLetras(string texto)
    {
        var sb = new System.Text.StringBuilder(texto.Length);
        foreach (char ch in texto) if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
        return sb.ToString();
    }

    // Cambia de animación: con el grafo, la mezcla pasa a esa pose (atacar y sentarse arrancan desde el principio).
    private void Pose(int nueva)
    {
        if (!hayGrafo)
        {
            if (nueva == Correr) Estado(c => c.estadoPerroCorrer);
            else if (nueva == Sentado) Estado(c => c.estadoPerroSentado);
            else Estado(c => c.estadoPerroAtacar);
            return;
        }
        if (nueva != pose && nueva != Correr && hayClip[nueva]) clips[nueva].SetTime(0);
        pose = hayClip[nueva] ? nueva : Correr;
    }

    // Cada cuadro: la mezcla va hacia la pose en 0,12 s, correr y sentado se repiten, y correr acompaña la velocidad.
    private void MezclarAnimaciones()
    {
        if (!hayGrafo || !grafo.IsValid()) return;
        float paso = Time.deltaTime / 0.12f;
        for (int i = 0; i < 3; i++)
        {
            if (!hayClip[i]) continue;
            float w = Mathf.MoveTowards(mezcla.GetInputWeight(i), i == pose ? 1f : 0f, paso);
            mezcla.SetInputWeight(i, w);
            if (i == Morder_) continue;
            double largo = clips[i].GetAnimationClip().length;
            if (largo > 0.01 && clips[i].GetTime() >= largo) clips[i].SetTime(clips[i].GetTime() % largo);
        }
        clips[Correr].SetSpeed(Activo ? Mathf.Clamp(velocidadVista / PasoCorrer, 0.7f, 1.8f) : 1f);
    }

    private void Estado(System.Func<ConfigRed, string> nombre)
    {
        ConfigRed config = ConfigRed.Actual;
        string estado = config != null ? nombre(config) : null;
        if (animador == null || string.IsNullOrEmpty(estado)) return;
        int hash = Animator.StringToHash(estado);
        if (animador.HasState(0, hash)) animador.CrossFade(hash, 0.1f, 0);
    }

    // Mientras no esté el modelo del lobo: un perro armado con formas simples, con el mismo holograma.
    private void ArmarAMano()
    {
        cuerpo = new GameObject("Perro").transform;
        cuerpo.SetParent(transform, false);
        Pieza("Cuerpo", cuerpo, Capsula(), new Vector3(0f, 0.48f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.3f, 0.38f, 0.3f));
        Transform cabeza = Pieza("Cabeza", cuerpo, Esfera(), new Vector3(0f, 0.68f, 0.42f), Quaternion.identity, new Vector3(0.26f, 0.24f, 0.28f));
        Pieza("Hocico", cabeza, Cubo(), new Vector3(0f, -0.15f, 0.62f), Quaternion.identity, new Vector3(0.45f, 0.4f, 0.75f));
        Pieza("Oreja", cabeza, Cubo(), new Vector3(-0.3f, 0.5f, -0.1f), Quaternion.Euler(0f, 0f, 15f), new Vector3(0.18f, 0.55f, 0.12f));
        Pieza("Oreja", cabeza, Cubo(), new Vector3(0.3f, 0.5f, -0.1f), Quaternion.Euler(0f, 0f, -15f), new Vector3(0.18f, 0.55f, 0.12f));
        Pieza("Cola", cuerpo, Capsula(), new Vector3(0f, 0.6f, -0.45f), Quaternion.Euler(-50f, 0f, 0f), new Vector3(0.07f, 0.18f, 0.07f));
        foreach (Vector3 lugar in new[] { new Vector3(-0.1f, 0.34f, 0.28f), new Vector3(0.1f, 0.34f, 0.28f),
                                          new Vector3(-0.1f, 0.34f, -0.28f), new Vector3(0.1f, 0.34f, -0.28f) })
        {
            var cadera = new GameObject("Pata").transform;
            cadera.SetParent(cuerpo, false);
            cadera.localPosition = lugar;
            Pieza("Pierna", cadera, Capsula(), new Vector3(0f, -0.17f, 0f), Quaternion.identity, new Vector3(0.08f, 0.17f, 0.08f));
            patas.Add(cadera);
        }
    }

    private Transform Pieza(string nombre, Transform padre, Mesh malla, Vector3 lugar, Quaternion giro, Vector3 escala)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = lugar;
        go.transform.localRotation = giro;
        go.transform.localScale = escala;
        go.AddComponent<MeshFilter>().sharedMesh = malla;
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = holo;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go.transform;
    }

    private void Animar()
    {
        Vector3 mov = transform.position - ultimoLugar;
        ultimoLugar = transform.position;
        float v = Time.deltaTime > 0f ? new Vector2(mov.x, mov.z).magnitude / Time.deltaTime : 0f;
        velocidadVista = Mathf.Lerp(velocidadVista, v, 1f - Mathf.Exp(-10f * Time.deltaTime));

        // Esperando sentado (CA6): se sienta. El dueño lo sabe; las demás computadoras lo deducen de que quedó quieto un
        // rato (no por frenar un instante en una esquina, que lo hacía sentarse y pararse).
        if (velocidadVista < 0.4f) { if (lentoDesde < 0f) lentoDesde = Time.time; }
        else lentoDesde = -1f;
        bool quieto = lentoDesde >= 0f && Time.time - lentoDesde > 0.35f;
        bool sentado = Activo && Time.time - creado > 0.6f && (propio ? esperando : quieto);
        if (sentado != sentadoVista && animador != null && Activo) Pose(sentado ? Sentado : Correr);
        sentadoVista = sentado;
        MezclarAnimaciones();

        if (patas.Count == 4 && sentado && cuerpo != null)
        {
            // El hecho a mano: la cola abajo y el pecho arriba, con las patas quietas.
            cuerpo.localRotation = Quaternion.Slerp(cuerpo.localRotation, Quaternion.Euler(-28f, 0f, 0f), 10f * Time.deltaTime);
            cuerpo.localPosition = Vector3.Lerp(cuerpo.localPosition, new Vector3(0f, -0.12f, 0f), 10f * Time.deltaTime);
            for (int i = 0; i < 4; i++) patas[i].localRotation = Quaternion.Euler(i < 2 ? 28f : 70f, 0f, 0f);
        }
        else if (patas.Count == 4)
        {
            if (cuerpo != null) cuerpo.localRotation = Quaternion.Slerp(cuerpo.localRotation, Quaternion.identity, 10f * Time.deltaTime);
            // Galope: las patas de adelante y las de atrás van alternadas.
            float t = Time.time * 14f, amplitud = Mathf.Clamp01(velocidadVista / Velocidad) * 40f;
            for (int i = 0; i < 4; i++)
                patas[i].localRotation = Quaternion.Euler(Mathf.Sin(t + (i < 2 ? 0f : Mathf.PI)) * amplitud, 0f, 0f);
            if (cuerpo != null) cuerpo.localPosition = lunge + Vector3.up * Mathf.Abs(Mathf.Sin(t)) * 0.04f * amplitud / 40f;
        }
    }

    // CA4: la mordida. El lobo usa su animación de atacar; el hecho a mano salta hacia adelante.
    private void Atacar(Transform victima)
    {
        if (victima != null)
        {
            Vector3 hacia = victima.position - transform.position;
            hacia.y = 0f;
            if (hacia.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(hacia);
        }
        if (animador != null) Pose(Morder_);
        else lunge = new Vector3(0f, 0.25f, 0.45f);
    }

    // ---------- Recursos ----------

    private static Material MaterialHolo()
    {
        if (materialHolo == null)
        {
            Shader shader = Resources.Load<Shader>("Efectos/Holograma");
            if (shader == null) shader = Shader.Find("Efectos/Holograma");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            materialHolo = new Material(shader) { name = "Holograma (perro)" };
        }
        return materialHolo;
    }

    private static Material MaterialRastro()
    {
        if (materialRastro == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) materialRastro = new Material(shader) { name = "Rastro (perro)" };
        }
        return materialRastro;
    }

    private static Mesh Esfera() => esfera != null ? esfera : esfera = Primitiva(PrimitiveType.Sphere);
    private static Mesh Capsula() => capsula != null ? capsula : capsula = Primitiva(PrimitiveType.Capsule);
    private static Mesh Cubo() => cubo != null ? cubo : cubo = Primitiva(PrimitiveType.Cube);

    private static Mesh Primitiva(PrimitiveType tipo)
    {
        GameObject temporal = GameObject.CreatePrimitive(tipo);
        Mesh malla = temporal.GetComponent<MeshFilter>().sharedMesh;
        Destroy(temporal);
        return malla;
    }
}
