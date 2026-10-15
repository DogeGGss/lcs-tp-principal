using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// US 020 · La pared de la Barrera. La levanta HabilidadBarrera en la computadora del jugador y JugadorEnRed la crea igual
// en las demás (CA11). Sube en 0,5 s y ya frena mientras sube (CA3); frena balas, granadas (rebotan) y jugadores de los dos
// equipos (CA4). Tiene 800 de vida (CA5): las balas y el cuchillo de los rivales le hacen el daño de cuerpo y la granada
// según la distancia; las de los compañeros (y las propias) se frenan sin dañarla. Dura 15 s (CA6).
// Se ve (CA7): chispas en cada impacto, grietas al 66 % y al 33 %, se hace pedazos al romperse y se hunde al terminar.
// Todo se arma por código para no depender de prefabs; en ConfigRed se le puede poner material y sonidos.
public class Barrera : MonoBehaviour
{
    public const float Ancho = 4f, Alto = 2.2f, Grosor = 0.3f; // CA3
    public const float Subida = 0.5f;                          // CA3
    public const float Duracion = 15f;                         // CA6
    public const int VidaMax = 800;                            // CA5
    public const float AlcanceSonido = 25f;                    // CA12 y CA13
    private const float Hundida = 0.6f;
    private static readonly Color Tono = new Color(0.42f, 0.47f, 0.53f);
    private static readonly Color ColorGrieta = new Color(0.08f, 0.09f, 0.1f, 1f);

    /// <summary>CA11: en una partida en línea, el daño se manda a todas las computadoras (lo pone JugadorEnRed). Sin red
    /// (null), se aplica acá nomás.</summary>
    public static System.Action<Barrera, int, Vector3> EnviarDanio;

    private static readonly Dictionary<long, Barrera> activas = new Dictionary<long, Barrera>();
    private static Material material, materialLineas;
    private static Mesh cubo;
    private static AudioClip clipSube, clipGolpe, clipGrieta, clipRompe, clipApaga;

    public int Actor { get; private set; }   // el número de jugador de Photon de su dueño (-1 sin red)
    public int Id { get; private set; }
    public Transform Duenio { get; private set; }
    public int Vida { get; private set; }
    /// <summary>En pie: no se rompió ni se terminó su tiempo.</summary>
    public bool Activa { get; private set; }

    private PlayerAbility habilidad; // solo en la computadora del dueño: avisa cuándo empieza a correr la recarga (CA8)
    private AudioMixerGroup grupo;
    private Transform pared;
    private BoxCollider caja;
    private ParticleSystem chispas, pedazos;
    private float creada, finHundida = -1f, proximoGolpe;
    private int grietas;

    // ---------- Crear y buscar ----------

    /// <param name="duenio">El jugador que la levantó (en las demás computadoras, su copia).</param>
    /// <param name="lugar">La base, al centro.</param>
    /// <param name="habilidad">La habilidad del dueño, solo en su computadora.</param>
    public static Barrera Crear(Transform duenio, int actor, int id, Vector3 lugar, float yaw, PlayerAbility habilidad,
        AudioMixerGroup grupo)
    {
        Buscar(actor, id)?.Quitar(); // por si llega repetida
        var go = new GameObject("Barrera");
        go.transform.SetPositionAndRotation(lugar, Quaternion.Euler(0f, yaw, 0f));
        Barrera barrera = go.AddComponent<Barrera>();
        barrera.Iniciar(duenio, actor, id, habilidad, grupo);
        return barrera;
    }

    public static Barrera Buscar(int actor, int id) => activas.TryGetValue(Clave(actor, id), out Barrera b) ? b : null;

    /// <summary>La barrera a la que pertenece ese collider (o null).</summary>
    public static Barrera De(Collider collider) => collider != null ? collider.GetComponentInParent<Barrera>() : null;

    /// <summary>Quita todas sin efectos (por ejemplo, al empezar una ronda del Táctico).</summary>
    public static void QuitarTodas()
    {
        foreach (Barrera b in new List<Barrera>(activas.Values)) if (b != null) b.Quitar();
        activas.Clear();
    }

    private static long Clave(int actor, int id) => ((long)actor << 32) | (uint)id;

    private void Iniciar(Transform duenio, int actor, int id, PlayerAbility habilidad, AudioMixerGroup grupo)
    {
        Duenio = duenio;
        Actor = actor;
        Id = id;
        Vida = VidaMax;
        Activa = true;
        this.habilidad = habilidad;
        this.grupo = grupo != null ? grupo : Grupo(duenio);
        creada = Time.time;
        activas[Clave(actor, id)] = this;

        // El collider va en la raíz (sin escalar) y crece con la pared: frena desde que empieza a subir (CA3).
        caja = gameObject.AddComponent<BoxCollider>();

        pared = new GameObject("Pared").transform;
        pared.SetParent(transform, false);
        var bloque = new GameObject("Bloque");
        bloque.transform.SetParent(pared, false);
        bloque.transform.localPosition = new Vector3(0f, Alto * 0.5f, 0f);
        bloque.transform.localScale = new Vector3(Ancho, Alto, Grosor);
        bloque.AddComponent<MeshFilter>().sharedMesh = Cubo();
        bloque.AddComponent<MeshRenderer>().sharedMaterial = MaterialPared();

        chispas = Particulas("Chispas", false);
        pedazos = Particulas("Pedazos", true);

        Alturar(0.02f);
        Sonar(Clip(ref clipSube, c => c != null ? c.sonidoBarreraSube : null, GenerarSube), transform.position + Vector3.up, 1f);
    }

    // ---------- Daño (CA5) ----------

    /// <summary>Un golpe de alguien (su jugador, arma o granada). Los del dueño y sus compañeros no la dañan.</summary>
    public void PedirDanio(int danio, Component atacante, Vector3 punto)
    {
        if (!Activa || danio <= 0 || EsAliadoDe(atacante)) return;
        if (EnviarDanio != null) EnviarDanio(this, danio, punto);
        else AplicarDanio(danio, punto);
    }

    /// <summary>CA11: el daño ya decidido (llega por la red a todas las computadoras, también a la de quien lo hizo).</summary>
    public void AplicarDanio(int danio, Vector3 punto)
    {
        if (!Activa) return;
        Vida = Mathf.Max(0, Vida - danio);
        if (Time.time >= proximoGolpe)
        {
            proximoGolpe = Time.time + 0.07f; // una ráfaga no suena como un solo ruido
            Sonar(Clip(ref clipGolpe, c => c != null ? c.sonidoBarreraGolpe : null, GenerarGolpe), punto, 0.7f);
        }
        if (Vida <= 0) { Romper(); return; }
        // CA7: grietas al 66 % y al 33 %.
        int etapa = Vida <= VidaMax / 3 ? 2 : Vida <= VidaMax * 2 / 3 ? 1 : 0;
        while (grietas < etapa)
        {
            grietas++;
            Agrietar(grietas);
            Sonar(Clip(ref clipGrieta, c => c != null ? c.sonidoBarreraGrieta : null, GenerarGrieta), punto, 1f);
        }
    }

    /// <summary>Si ese atacante es el dueño o un compañero suyo (US 031): la bala se frena sin dañar la pared.</summary>
    public bool EsAliadoDe(Component atacante)
    {
        if (atacante == null) return false;
        if (Duenio != null && atacante.transform.IsChildOf(Duenio)) return true;
        JugadorEnRed jugador = atacante.GetComponentInParent<JugadorEnRed>();
        if (jugador == null || Actor < 0) return false;
        return jugador.Actor == Actor || EquiposTacticos.SonAliados(jugador.Actor, Actor);
    }

    /// <summary>CA7: chispas donde pegó la bala (cada computadora las hace con el disparo que ve).</summary>
    public void Chispas(Vector3 punto, Vector3 normal)
    {
        if (!Activa || chispas == null) return;
        var emitir = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        for (int i = 0; i < 9; i++)
        {
            Vector3 salida = (normal + Random.insideUnitSphere * 0.8f).normalized;
            emitir.position = punto + normal * 0.02f;
            emitir.velocity = salida * Random.Range(2.5f, 6f);
            emitir.startColor = Color.Lerp(new Color(1f, 0.85f, 0.45f), Color.white, Random.value * 0.5f);
            emitir.startSize = Random.Range(0.025f, 0.05f);
            emitir.startLifetime = Random.Range(0.12f, 0.28f);
            chispas.Emit(emitir, 1);
        }
    }

    // ---------- Fin ----------

    // CA7: se hace pedazos.
    private void Romper()
    {
        Terminar();
        pared.gameObject.SetActive(false);
        var emitir = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        for (int i = 0; i < 26; i++)
        {
            Vector3 local = new Vector3(Random.Range(-0.5f, 0.5f) * Ancho, Random.Range(0.1f, 0.95f) * Alto, Random.Range(-0.5f, 0.5f) * Grosor);
            emitir.position = transform.TransformPoint(local);
            emitir.velocity = transform.rotation * new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(0.5f, 3f), Random.Range(-3f, 3f));
            emitir.startSize = Random.Range(0.12f, 0.32f);
            emitir.startLifetime = Random.Range(1f, 1.5f);
            emitir.rotation3D = Random.insideUnitSphere * 180f;
            emitir.angularVelocity3D = Random.insideUnitSphere * 360f;
            emitir.startColor = Color.white;
            pedazos.Emit(emitir, 1);
        }
        Sonar(Clip(ref clipRompe, c => c != null ? c.sonidoBarreraRompe : null, GenerarRompe), transform.position + Vector3.up, 1f);
        Destroy(gameObject, 1.7f);
    }

    // CA6 y CA7: se terminó el tiempo: deja de frenar y se hunde en el piso.
    private void Apagar()
    {
        Terminar();
        finHundida = Time.time + Hundida;
        Sonar(Clip(ref clipApaga, c => c != null ? c.sonidoBarreraApaga : null, GenerarApaga), transform.position + Vector3.up, 0.8f);
        Destroy(gameObject, Hundida + 0.2f);
    }

    private void Quitar()
    {
        Terminar();
        Destroy(gameObject);
    }

    private void Terminar()
    {
        if (!Activa) return;
        Activa = false;
        if (caja != null) caja.enabled = false;
        if (activas.TryGetValue(Clave(Actor, Id), out Barrera b) && b == this) activas.Remove(Clave(Actor, Id));
        // CA8: la recarga de la habilidad empieza ahora.
        if (habilidad != null) habilidad.TerminoEfecto();
    }

    private void OnDestroy() => Terminar();

    private void Update()
    {
        float edad = Time.time - creada;
        if (Activa)
        {
            if (edad < Subida) Alturar(1f - Mathf.Pow(1f - edad / Subida, 3f));
            else if (caja.size.y < Alto) Alturar(1f);
            if (edad >= Duracion) Apagar();
        }
        else if (finHundida > 0f && pared != null)
        {
            float k = 1f - Mathf.Clamp01((finHundida - Time.time) / Hundida);
            pared.localPosition = Vector3.down * (Alto * k * k);
        }
    }

    // La pared (y su collider) a esa fracción de la altura, desde el piso.
    private void Alturar(float k)
    {
        float alto = Mathf.Max(0.02f, Alto * k);
        caja.size = new Vector3(Ancho, alto, Grosor);
        caja.center = new Vector3(0f, alto * 0.5f, 0f);
        pared.localScale = new Vector3(1f, alto / Alto, 1f);
    }

    // ---------- Grietas (CA7) ----------

    private void Agrietar(int etapa)
    {
        var azar = new System.Random(Id * 7919 + Actor * 31 + etapa);
        int cuantas = etapa == 1 ? 2 : 3;
        for (int i = 0; i < cuantas; i++)
        {
            Vector2 inicio = new Vector2(Rango(azar, -1.5f, 1.5f), Rango(azar, 0.4f, Alto - 0.4f));
            int ramas = 2 + azar.Next(2);
            for (int r = 0; r < ramas; r++)
            {
                float angulo = (float)(azar.NextDouble() * Mathf.PI * 2f);
                var puntos = new List<Vector2> { inicio };
                Vector2 p = inicio;
                int tramos = 4 + azar.Next(3);
                for (int t = 0; t < tramos; t++)
                {
                    angulo += Rango(azar, -0.6f, 0.6f);
                    p += new Vector2(Mathf.Cos(angulo), Mathf.Sin(angulo)) * Rango(azar, 0.12f, 0.28f);
                    p.x = Mathf.Clamp(p.x, -Ancho * 0.5f + 0.02f, Ancho * 0.5f - 0.02f);
                    p.y = Mathf.Clamp(p.y, 0.02f, Alto - 0.02f);
                    puntos.Add(p);
                }
                Linea(puntos, Grosor * 0.5f + 0.006f);
                Linea(puntos, -Grosor * 0.5f - 0.006f); // en las dos caras
            }
        }
    }

    private void Linea(List<Vector2> puntos, float cara)
    {
        var go = new GameObject("Grieta");
        go.transform.SetParent(pared, false);
        LineRenderer linea = go.AddComponent<LineRenderer>();
        linea.useWorldSpace = false;
        linea.sharedMaterial = MaterialLineas();
        linea.startColor = linea.endColor = ColorGrieta;
        linea.startWidth = 0.035f;
        linea.endWidth = 0.01f;
        linea.numCapVertices = 1;
        linea.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        linea.receiveShadows = false;
        linea.positionCount = puntos.Count;
        for (int i = 0; i < puntos.Count; i++) linea.SetPosition(i, new Vector3(puntos[i].x, puntos[i].y, cara));
    }

    private static float Rango(System.Random azar, float desde, float hasta) => desde + (float)azar.NextDouble() * (hasta - desde);

    // ---------- Partículas (chispas y pedazos) ----------

    private ParticleSystem Particulas(string nombre, bool comoPedazos)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        go.layer = 2;
        ParticleSystem sistema = go.AddComponent<ParticleSystem>();
        sistema.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = sistema.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = comoPedazos ? 1.6f : 0.6f;
        main.maxParticles = comoPedazos ? 40 : 300;
        main.startRotation3D = comoPedazos;
        ParticleSystem.EmissionModule emision = sistema.emission;
        emision.enabled = false;
        ParticleSystem.ShapeModule forma = sistema.shape;
        forma.enabled = false;
        ParticleSystemRenderer dibujo = go.GetComponent<ParticleSystemRenderer>();
        if (comoPedazos)
        {
            // Pedazos de la pared que caen y rebotan en el piso.
            ParticleSystem.CollisionModule choque = sistema.collision;
            choque.enabled = true;
            choque.type = ParticleSystemCollisionType.World;
            choque.mode = ParticleSystemCollisionMode.Collision3D;
            choque.bounce = 0.25f;
            choque.dampen = 0.4f;
            choque.radiusScale = 0.5f;
            choque.collidesWith = Physics.DefaultRaycastLayers;
            ParticleSystem.SizeOverLifetimeModule tamanio = sistema.sizeOverLifetime;
            tamanio.enabled = true;
            tamanio.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f)));
            dibujo.renderMode = ParticleSystemRenderMode.Mesh;
            dibujo.mesh = Cubo();
            dibujo.sharedMaterial = MaterialPared();
            dibujo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
        else
        {
            dibujo.renderMode = ParticleSystemRenderMode.Stretch;
            dibujo.velocityScale = 0.04f;
            dibujo.lengthScale = 1f;
            dibujo.sharedMaterial = MaterialLineas();
            dibujo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        sistema.Play();
        return sistema;
    }

    // ---------- Materiales ----------

    private static Material MaterialPared()
    {
        ConfigRed config = ConfigRed.Actual;
        if (config != null && config.materialBarrera != null) return config.materialBarrera;
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            material = new Material(shader) { name = "Barrera (generado)", color = Tono };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Tono);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.35f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.2f);
        }
        return material;
    }

    private static Material MaterialLineas()
    {
        if (materialLineas == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) materialLineas = new Material(shader) { name = "Barrera (líneas)" };
        }
        return materialLineas;
    }

    private static Mesh Cubo()
    {
        if (cubo == null)
        {
            GameObject temporal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubo = temporal.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temporal);
        }
        return cubo;
    }

    // ---------- Sonidos (CA12 y CA13) ----------

    private static AudioMixerGroup Grupo(Transform jugador)
    {
        if (jugador == null) return null;
        Pistola pistola = jugador.GetComponentInChildren<Pistola>(true);
        return pistola != null ? pistola.sfxGroup : null;
    }

    // Se escucha desde donde está hasta unos 25 m, por el grupo de efectos (SFX).
    private void Sonar(AudioClip clip, Vector3 lugar, float volumen)
    {
        if (clip == null) return;
        ConfigRed config = ConfigRed.Actual;
        var go = new GameObject("Barrera (sonido)");
        go.transform.position = lugar;
        AudioSource fuente = go.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.outputAudioMixerGroup = grupo;
        fuente.spatialBlend = 1f;
        fuente.rolloffMode = AudioRolloffMode.Linear;
        fuente.minDistance = 3f;
        fuente.maxDistance = AlcanceSonido;
        fuente.dopplerLevel = 0f;
        fuente.clip = clip;
        fuente.volume = volumen * (config != null ? config.volumenBarrera : 0.8f);
        fuente.Play();
        Destroy(go, clip.length + 0.1f);
    }

    // El clip de ConfigRed o, si no hay, uno generado (así suena aunque todavía no se hayan cargado sonidos).
    private static AudioClip Clip(ref AudioClip generado, System.Func<ConfigRed, AudioClip> deConfig, System.Func<AudioClip> generar)
    {
        AudioClip propio = deConfig(ConfigRed.Actual);
        if (propio != null) return propio;
        if (generado == null) generado = generar();
        return generado;
    }

    private delegate float Muestra(float t, float k, float ruido, ref float fase);

    private static AudioClip Sintetizar(string nombre, float duracion, int semilla, float filtro, Muestra muestra)
    {
        const int frecuencia = 44100;
        int n = Mathf.CeilToInt(frecuencia * duracion);
        var datos = new float[n];
        var azar = new System.Random(semilla);
        float ruido = 0f, fase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)frecuencia, k = t / duracion;
            float blanco = (float)azar.NextDouble() * 2f - 1f;
            ruido += (blanco - ruido) * filtro;
            datos[i] = Mathf.Clamp(muestra(t, k, ruido, ref fase), -1f, 1f);
        }
        AudioClip clip = AudioClip.Create(nombre, n, 1, frecuencia, false);
        clip.SetData(datos, 0);
        return clip;
    }

    // Un retumbo grave que sube, como una losa saliendo del piso.
    private static AudioClip GenerarSube() => Sintetizar("Barrera sube (generado)", 0.6f, 20, 0.08f,
        (float t, float k, float ruido, ref float fase) =>
        {
            fase += 2f * Mathf.PI * Mathf.Lerp(55f, 95f, k) / 44100f;
            float env = Mathf.Min(1f, t / 0.03f) * Mathf.Pow(1f - k, 1.4f);
            return env * (0.55f * Mathf.Sin(fase) + 0.9f * ruido);
        });

    // Un golpe seco y corto.
    private static AudioClip GenerarGolpe() => Sintetizar("Barrera golpe (generado)", 0.09f, 21, 0.35f,
        (float t, float k, float ruido, ref float fase) =>
        {
            fase += 2f * Mathf.PI * 180f / 44100f;
            return Mathf.Pow(1f - k, 3f) * (0.4f * Mathf.Sin(fase) + 0.7f * ruido);
        });

    // Un crujido: chasquidos sueltos sobre un ruido agudo.
    private static AudioClip GenerarGrieta() => Sintetizar("Barrera grieta (generado)", 0.3f, 22, 0.7f,
        (float t, float k, float ruido, ref float fase) =>
        {
            float chasquido = Mathf.Repeat(t * 37f, 1f) < 0.12f ? 1f : 0.35f;
            return Mathf.Pow(1f - k, 2f) * chasquido * 0.8f * ruido;
        });

    // Se hace pedazos: un golpe grave y un ruido que se va apagando.
    private static AudioClip GenerarRompe() => Sintetizar("Barrera rompe (generado)", 0.8f, 23, 0.25f,
        (float t, float k, float ruido, ref float fase) =>
        {
            fase += 2f * Mathf.PI * Mathf.Lerp(120f, 45f, k) / 44100f;
            float golpe = Mathf.Exp(-t * 18f) * Mathf.Sin(fase);
            float lluvia = Mathf.Pow(1f - k, 2f) * ruido * (Mathf.Repeat(t * 23f, 1f) < 0.3f ? 1f : 0.5f);
            return 0.6f * golpe + 0.8f * lluvia;
        });

    // Se apaga: un soplido suave que baja.
    private static AudioClip GenerarApaga() => Sintetizar("Barrera apaga (generado)", 0.55f, 24, 0.06f,
        (float t, float k, float ruido, ref float fase) =>
        {
            fase += 2f * Mathf.PI * Mathf.Lerp(90f, 40f, k) / 44100f;
            float env = Mathf.Min(1f, t / 0.05f) * (1f - k);
            return env * (0.3f * Mathf.Sin(fase) + 0.8f * ruido);
        });
}
