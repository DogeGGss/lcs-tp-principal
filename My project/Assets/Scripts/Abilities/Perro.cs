using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;

// US 022 · El perro espectral del Callejero. Lo suelta HabilidadCallejero y sale corriendo a 8 m/s hacia el rival vivo
// más cercano dentro de 25 m, siguiéndolo si se mueve (CA1 y CA5). Corre por el NavMesh que arma NavMeshEnVivo: sigue
// pasillos, puertas, escaleras y rampas y no atraviesa paredes. Si no hay rivales, corre 10 m hacia adelante y
// desaparece (CA6). Al alcanzar al rival lo muerde y desaparece: la mordida no saca vida, le nubla la vista 2,5 s y lo
// marca 3 s a través de las paredes para el equipo del dueño (CA7 y CA8). Tiene 60 de vida: los disparos, el cuchillo y
// las granadas de los rivales lo pueden matar (CA9). Si en 6 s no alcanzó a nadie, desaparece (CA10).
// Se ve como un holograma (CA2): del color de compañero para el equipo del dueño y rojo para los rivales (CA3).
// En línea (CA15), el perro se mueve en la computadora del dueño, que manda dónde está y si mordió o desapareció
// (JugadorEnRed); en las demás es una copia que sigue esas posiciones. El daño llega a todas por el servidor, en el
// mismo orden, así la vida y la muerte dan igual en todas.
public class Perro : MonoBehaviour
{
    public const float Velocidad = 8f;      // CA5
    public const float Alcance = 25f;       // CA1
    public const float SinRival = 10f;      // CA6
    public const float Duracion = 6f;       // CA10
    public const int VidaMax = 60;          // CA9
    public const float Nublado = 2.5f;      // CA7
    public const float Marca = 3f;          // CA8
    private const float Llegar = 1.1f;      // m al rival para morderlo
    private const float Altura = 0.8f;      // alto del perro

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

        // CA11: gruñe mientras corre y ladra de a ratos; se oye a 20 m.
        grunido = SonidoPerro.Fuente(gameObject, this.grupo, false);
        grunido.clip = SonidoPerro.Clip(SonidoPerro.Tipo.Grunido);
        grunido.loop = true;
        grunido.Play();
        proximoLadrido = Time.time + 0.6f;

        if (propio)
        {
            destinoSinRival = transform.position + adelante * SinRival;
            if (NavMeshEnVivo.Listo &&
                NavMesh.SamplePosition(transform.position, out NavMeshHit piso, 2.5f, Filtro()))
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
            Buscar();
        }
    }

    private static NavMeshQueryFilter Filtro() => new NavMeshQueryFilter { agentTypeID = NavMeshEnVivo.Tipo, areaMask = NavMesh.AllAreas };

    private static AudioMixerGroup Grupo(Transform jugador)
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

        if (propio) Simular();
        else Seguir();

        if (Time.time >= proximoLadrido)
        {
            proximoLadrido = Time.time + Random.Range(0.9f, 1.6f);
            AudioClip ladrido = SonidoPerro.Clip(SonidoPerro.Tipo.Ladrido);
            if (grunido != null && ladrido != null) grunido.PlayOneShot(ladrido);
        }
        Animar();
    }

    private void Simular()
    {
        if (Time.time - creado >= Duracion) { Terminar(Fin.Desaparecio, -1, null, true); return; } // CA10

        if (Time.time >= proximaBusqueda) Buscar();
        if (objetivo != null && (objetivo == null || objetivo.currentHealth <= 0)) objetivo = null;

        Vector3 destino = objetivo != null ? objetivo.transform.position : destinoSinRival;
        if (agente != null && agente.isOnNavMesh)
        {
            agente.SetDestination(destino);
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
            if (Physics.Raycast(transform.position + Vector3.up * 0.8f, Vector3.down, out RaycastHit piso, 3f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && De(piso.collider) == null)
                transform.position = new Vector3(transform.position.x, piso.point.y, transform.position.z);
        }

        if (objetivo != null)
        {
            Vector3 d = objetivo.transform.position - transform.position;
            if (new Vector2(d.x, d.z).magnitude <= Llegar && Mathf.Abs(d.y) < 1.8f) { Morder(objetivo); return; }
        }
        else
        {
            Vector3 d = destinoSinRival - transform.position;
            if (new Vector2(d.x, d.z).magnitude < 0.6f) { Terminar(Fin.Desaparecio, -1, null, true); return; } // CA6
        }

        if (Time.time >= proximoEnvio)
        {
            proximoEnvio = Time.time + 0.1f;
            EnviarPosicion?.Invoke(this, transform.position, transform.eulerAngles.y);
        }
    }

    // CA1 y CA5: el rival vivo más cercano dentro de 25 m (lo sigue aunque se aleje, mientras siga vivo).
    private void Buscar()
    {
        proximaBusqueda = Time.time + 0.25f;
        HealthSystem mejor = objetivo;
        float cerca = mejor != null ? Vector3.Distance(transform.position, mejor.transform.position) : Alcance;
        if (mejor != null) cerca = Mathf.Min(cerca, Alcance * 2f) - 2f; // cambia solo si otro está bastante más cerca
        foreach (HealthSystem h in FindObjectsByType<HealthSystem>(FindObjectsSortMode.None))
        {
            if (!EsRival(h)) continue;
            float d = Vector3.Distance(transform.position, h.transform.position);
            if (d < cerca) { cerca = d; mejor = h; }
        }
        objetivo = mejor;
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

    // CA3: el equipo del dueño lo ve del color de compañero; los rivales, rojo.
    private Color ColorParaMi()
    {
        if (propio || Actor < 0) return MatchHud.TeamColor;
        JugadorEnRed yo = PartidaEnRed.Actual != null ? PartidaEnRed.Actual.Local : null;
        if (yo == null) return MatchHud.TeamColor;
        bool rival = yo.Actor != Actor && !EquiposTacticos.SonAliados(yo.Actor, Actor);
        return rival ? MatchHud.RivalColor : MatchHud.TeamColor;
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
            if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = holo;
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        if (hay && b.size.y > 0.01f) m.transform.localScale *= Altura / b.size.y;
        animador = m.GetComponentInChildren<Animator>();
        if (animador != null) { animador.applyRootMotion = false; Estado(config => config.estadoPerroCorrer); }
        cuerpo = m.transform;
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

        if (patas.Count == 4)
        {
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
        if (animador != null) Estado(config => config.estadoPerroAtacar);
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
