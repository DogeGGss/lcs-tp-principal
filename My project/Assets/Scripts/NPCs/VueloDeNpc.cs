using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// F11 (NPCs), US 267: el jefe levanta vuelo y escupe veneno (los tipos con TipoDeNpc.vuela).
// - CA1: cada 25 s de pelea despega: el cuerpo sube unos metros con la animación de vuelo. Lo de abajo (el objeto
//   del NPC y su agente) sigue sobre el NavMesh: así no se pierde ni atraviesa paredes (CA7).
// - CA2: volando da vueltas alrededor del jugador durante 6 s, lejos del cuchillo. Se le puede disparar (sus zonas de
//   impacto suben con el cuerpo); su cápsula para chocar se apaga, así las granadas lo dañan solo si explotan cerca de
//   él, arriba.
// - CA3: mientras vuela escupe 3 bolas de veneno, una cada 1,5 s, hacia donde está el jugador: caen en arco, son verdes
//   y se ven venir, así que se pueden esquivar.
// - CA4: si la bola le pega al jugador le hace 15 de daño; donde cae deja un charco de 5 m de ancho.
// - CA5: cada charco dura 8 s y después se achica hasta desaparecer; el jugador que está adentro recibe 10 de daño por
//   segundo. No lastiman a los enemigos. Como mucho hay 6 (el más viejo se va).
// - CA6: después de escupir baja, queda 1,5 s quieto y vuelve a pelear por tierra.
// - CA7: si no hay lugar arriba (bajo techo), no despega y prueba más tarde. Las bolas chocan con las paredes y dejan el
//   charco en el piso más cercano.
// Mientras tanto la persecución lo deja quieto (Ocupado) y no lo gira (Controlado): lo mueve esto. Si muere volando, cae
// al piso (US 146, CA5). CadaCuanto y Bolas los cambia la furia (US 268).
// Lo agrega Npc.Crear.
[RequireComponent(typeof(Npc))]
public class VueloDeNpc : MonoBehaviour
{
    private const float Subir = 4f, SubeEn = 1f;         // CA1: m que sube y en cuántos s (lo mismo para bajar)
    private const float EnElAire = 6f;                  // CA2: s volando
    private const float Radio = 9f, Vuela = 4f;         // CA2: m alrededor del jugador y m/s
    private const float PrimeraBola = 1f, EntreBolas = 1.5f; // CA3: s
    private const float Recupera = 1.5f;                // CA6: s quieto después de aterrizar
    private const float PrimeraVez = 12f;               // s desde que aparece hasta el primer vuelo
    private const float SinLugar = 5f;                  // CA7: s hasta volver a probar si no había lugar arriba
    private const float CaeEn = 0.4f;                   // si muere volando

    /// <summary>Segundos entre vuelos (CA1). La furia (US 268) lo baja a 15.</summary>
    public float CadaCuanto = 25f;
    /// <summary>Bolas de veneno por vuelo (CA3). La furia (US 268) las sube a 5.</summary>
    public int Bolas = 3;

    /// <summary>La furia (US 268): vuela más seguido y escupe más bolas desde ahora.</summary>
    public void MasSeguido(float cadaCuanto, int bolas)
    {
        CadaCuanto = cadaCuanto;
        Bolas = bolas;
        proximo = Mathf.Min(proximo, Time.time + cadaCuanto);
    }

    /// <summary>Está volando (despegando, en el aire, aterrizando o recuperándose).</summary>
    public bool Volando => fase != Fase.Tierra;

    private enum Fase { Tierra, Despega, Aire, Aterriza, Recupera }

    private Npc npc;
    private PersecucionDeNpc persecucion;
    private Fase fase = Fase.Tierra;
    private float proximo, faseHasta, proximaBola, angulo;
    private int escupidas;
    private float subido, alturaModelo;
    private CapsuleCollider capsula;

    private void Awake()
    {
        npc = GetComponent<Npc>();
        persecucion = GetComponent<PersecucionDeNpc>();
        capsula = GetComponent<CapsuleCollider>();
        if (npc.Modelo != null) alturaModelo = npc.Modelo.localPosition.y;
        proximo = Time.time + PrimeraVez;
    }

    private void Update()
    {
        if (persecucion == null) persecucion = GetComponent<PersecucionDeNpc>();
        if (npc == null || npc.Tipo == null || persecucion == null) return;

        // Muerto: si estaba en el aire, cae al piso y listo.
        if (!npc.Vivo)
        {
            Altura(0f, Subir / CaeEn);
            if (subido <= 0f) enabled = false;
            return;
        }
        if (npc.Aturdido && fase == Fase.Tierra) return;

        switch (fase)
        {
            case Fase.Tierra:
                if (Time.time < proximo || persecucion.Ocupado || persecucion.Objetivo == null) break;
                if (!HayLugarArriba()) { proximo = Time.time + SinLugar; break; }
                Despegar();
                break;
            case Fase.Despega:
                Altura(Subir, Subir / SubeEn);
                Mirar();
                if (subido >= Subir) { fase = Fase.Aire; faseHasta = Time.time + EnElAire; proximaBola = Time.time + PrimeraBola; }
                break;
            case Fase.Aire:
                Rodear();
                Mirar();
                if (escupidas < Bolas && Time.time >= proximaBola) Escupir();
                if (Time.time >= faseHasta && escupidas >= Bolas) Aterrizar();
                break;
            case Fase.Aterriza:
                Altura(0f, Subir / SubeEn);
                if (subido <= 0f) { fase = Fase.Recupera; faseHasta = Time.time + Recupera; Animar(npc.Tipo.quietoEnCombate, true); }
                break;
            case Fase.Recupera:
                if (Time.time >= faseHasta) Terminar();
                break;
        }
    }

    // CA7: lugar arriba del cuerpo para subir (sin techo).
    private bool HayLugarArriba()
    {
        float alto = capsula != null ? capsula.height : 3f;
        Vector3 desde = transform.position + Vector3.up * (alto * 0.5f);
        float radio = Mathf.Clamp(npc.Ancho * 0.5f, 0.3f, 1f);
        foreach (RaycastHit g in Physics.SphereCastAll(desde, radio, Vector3.up, alto * 0.5f + Subir, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (g.collider.transform.IsChildOf(transform)) continue;
            if (g.collider.GetComponentInParent<Npc>() != null || g.collider.GetComponentInParent<PlayerMovement>() != null) continue;
            return false;
        }
        return true;
    }

    private void Despegar()
    {
        fase = Fase.Despega;
        escupidas = 0;
        persecucion.Ocupado = true;
        persecucion.Controlado = true;
        if (capsula != null) capsula.enabled = false; // CA2: arriba no hay nada que choque abajo
        Vector3 desdeJugador = transform.position - persecucion.Objetivo.position;
        angulo = Mathf.Atan2(desdeJugador.z, desdeJugador.x) * Mathf.Rad2Deg;
        Animar(npc.Tipo.volar != null ? npc.Tipo.volar : npc.Tipo.caminar, true);
    }

    private void Aterrizar()
    {
        fase = Fase.Aterriza;
        Animar(npc.Tipo.volar != null ? npc.Tipo.volar : npc.Tipo.caminar, true);
    }

    private void Terminar()
    {
        fase = Fase.Tierra;
        proximo = Time.time + CadaCuanto;
        if (capsula != null) capsula.enabled = true;
        persecucion.Ocupado = false;
        persecucion.Controlado = false;
    }

    // Sube o baja el cuerpo (el modelo) sobre su lugar.
    private void Altura(float meta, float velocidad)
    {
        if (npc.Modelo == null || Mathf.Approximately(subido, meta)) return;
        subido = Mathf.MoveTowards(subido, meta, velocidad * Time.deltaTime);
        Vector3 p = npc.Modelo.localPosition;
        p.y = alturaModelo + subido;
        npc.Modelo.localPosition = p;
    }

    // CA2: va dando la vuelta alrededor del jugador, a unos 9 m, por el NavMesh.
    private void Rodear()
    {
        NavMeshAgent agente = persecucion.Agente;
        Transform jugador = persecucion.Objetivo;
        if (agente == null || jugador == null) return;
        angulo += Vuela / Radio * Mathf.Rad2Deg * Time.deltaTime;
        Vector3 meta = jugador.position + new Vector3(Mathf.Cos(angulo * Mathf.Deg2Rad), 0f, Mathf.Sin(angulo * Mathf.Deg2Rad)) * Radio;
        Vector3 hacia = meta - transform.position;
        hacia.y = 0f;
        if (hacia.magnitude > 0.05f) agente.Move(Vector3.ClampMagnitude(hacia, Vuela * 1.5f * Time.deltaTime));
    }

    private void Mirar()
    {
        Transform jugador = persecucion.Objetivo;
        if (jugador == null) return;
        Vector3 hacia = jugador.position - transform.position;
        hacia.y = 0f;
        if (hacia.sqrMagnitude > 1e-4f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(hacia), 360f * Time.deltaTime);
    }

    // CA3: una bola de veneno desde la boca hacia donde está el jugador.
    private void Escupir()
    {
        escupidas++;
        proximaBola = Time.time + EntreBolas;
        Transform jugador = persecucion.Objetivo;
        if (jugador == null) return;
        if (npc.Tipo.escupir != null && npc.Animaciones != null) npc.Animaciones.Golpe(npc.Tipo.escupir, 0.9f);
        Vector3 boca = Boca();
        BolaDeVeneno.Lanzar(boca, jugador.position, npc.Tipo.nombre, transform);
    }

    // La boca: la zona de impacto de la cabeza (o el frente del cuerpo, si no tiene).
    private Vector3 Boca()
    {
        foreach (HitZone z in GetComponentsInChildren<HitZone>())
            if (z.zone == BodyZone.Head) return z.transform.position + transform.forward * 0.5f;
        return transform.position + Vector3.up * (alturaModelo + subido + 2f) + transform.forward * npc.Frente;
    }

    private void Animar(AnimationClip clip, bool loop)
    {
        if (clip == null || npc.Animaciones == null) return;
        npc.Animaciones.Poner(clip, loop, 0.2f);
        npc.Animaciones.Velocidad(1f);
    }

    private void OnDisable()
    {
        if (fase != Fase.Tierra && npc != null && npc.Vivo) Terminar();
    }
}

// Una bola de veneno (US 267, CA3 y CA4): cae en arco hacia donde estaba el jugador. Si le pega a un jugador le hace 15
// de daño; donde cae (o en el piso más cercano, si choca con una pared) deja un charco.
public class BolaDeVeneno : MonoBehaviour
{
    private const float Vuelo = 1.1f;   // s hasta llegar (el arco)
    private const float Tam = 0.45f;    // m de diámetro
    private const int Danio = 15;
    private const float Gravedad = 9.8f;

    private Vector3 velocidad;
    private string fuente;
    private Transform jefe;
    private float muere;

    public static void Lanzar(Vector3 desde, Vector3 hacia, string fuente, Transform jefe)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Bola de veneno";
        Destroy(go.GetComponent<Collider>());
        go.transform.position = desde;
        go.transform.localScale = Vector3.one * Tam;
        var render = go.GetComponent<MeshRenderer>();
        render.sharedMaterial = Veneno.Material(new Color(0.45f, 1f, 0.2f, 0.95f));
        render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var bola = go.AddComponent<BolaDeVeneno>();
        // Tiro parabólico: llega a "hacia" en Vuelo segundos.
        bola.velocidad = (hacia - desde) / Vuelo + 0.5f * Gravedad * Vuelo * Vector3.up;
        bola.fuente = fuente;
        bola.jefe = jefe;
        bola.muere = Time.time + 6f;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        Vector3 antes = transform.position;
        velocidad += Vector3.down * Gravedad * dt;
        Vector3 despues = antes + velocidad * dt;
        Vector3 paso = despues - antes;
        foreach (RaycastHit g in Physics.SphereCastAll(antes, Tam * 0.5f, paso.normalized, paso.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (jefe != null && g.collider.transform.IsChildOf(jefe)) continue;
            if (g.collider.GetComponentInParent<Npc>() != null) continue; // los enemigos no la frenan
            PlayerMovement jugador = g.collider.GetComponentInParent<PlayerMovement>();
            if (jugador != null)
            {
                HealthSystem vida = jugador.GetComponent<HealthSystem>();
                if (vida != null && vida.currentHealth > 0)
                {
                    HealthSystem.DamageOrigin = antes;
                    HealthSystem.DamageSource = fuente;
                    vida.TakeDamage(DificultadDeInvasion.DanioDeEnemigo(Danio)); // US 161, CA3
                    HealthSystem.DamageSource = null;
                }
                Caer(jugador.transform.position + Vector3.up * 0.5f);
                return;
            }
            Caer(g.point + g.normal * 0.1f);
            return;
        }
        transform.position = despues;
        if (Time.time >= muere) Destroy(gameObject);
    }

    // CA4 y CA7: el charco en el piso de abajo (si chocó con una pared, el piso más cercano a ese punto).
    private void Caer(Vector3 donde)
    {
        // El piso de abajo, sin contar jugadores ni enemigos (si le pegó al jugador, abajo está él: el charco quedaba
        // flotando sobre su cuerpo).
        RaycastHit? piso = null;
        foreach (RaycastHit g in Physics.RaycastAll(donde + Vector3.up * 0.3f, Vector3.down, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (g.collider.GetComponentInParent<PlayerMovement>() != null || g.collider.GetComponentInParent<Npc>() != null) continue;
            if (g.collider.GetComponentInParent<CharacterController>() != null) continue;
            if (piso == null || g.distance < piso.Value.distance) piso = g;
        }
        if (piso.HasValue)
            CharcoDeVeneno.Crear(piso.Value.point, fuente);
        else if (NavMesh.SamplePosition(donde, out NavMeshHit cerca, 5f, NavMesh.AllAreas))
            CharcoDeVeneno.Crear(cerca.position, fuente);
        Destroy(gameObject);
    }
}

// Un charco de veneno (US 267, CA5): 5 m de ancho, dura 8 s y se achica hasta desaparecer. El jugador que está
// adentro recibe 10 de daño por segundo (de a 5 cada medio segundo). Como mucho hay 6: el más viejo se va.
public class CharcoDeVeneno : MonoBehaviour
{
    private const float Ancho = 5f, Dura = 8f, Achica = 1.5f;
    private const float Tic = 0.5f;
    private const int DanioPorTic = 5; // 10 por segundo
    private const int Maximo = 6;

    private static readonly List<CharcoDeVeneno> charcos = new List<CharcoDeVeneno>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Limpiar() => charcos.Clear();

    private float nace, proximoTic;
    private string fuente;
    private readonly List<HealthSystem> jugadores = new List<HealthSystem>();

    public static void Crear(Vector3 donde, string fuente)
    {
        charcos.RemoveAll(c => c == null);
        while (charcos.Count >= Maximo) { Destroy(charcos[0].gameObject); charcos.RemoveAt(0); }

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Charco de veneno";
        Destroy(go.GetComponent<Collider>());
        go.transform.position = donde + Vector3.up * 0.03f;
        go.transform.localScale = new Vector3(Ancho, 0.02f, Ancho);
        var render = go.GetComponent<MeshRenderer>();
        render.sharedMaterial = Veneno.Material(new Color(0.35f, 0.9f, 0.15f, 0.6f));
        render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        render.receiveShadows = false;
        var charco = go.AddComponent<CharcoDeVeneno>();
        charco.nace = Time.time;
        charco.proximoTic = Time.time + Tic;
        charco.fuente = fuente;
        foreach (PlayerMovement p in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            HealthSystem v = p.GetComponent<HealthSystem>();
            if (v != null) charco.jugadores.Add(v);
        }
        charcos.Add(charco);
    }

    private void Update()
    {
        float edad = Time.time - nace;
        float tam = edad < Dura ? 1f : 1f - Mathf.Clamp01((edad - Dura) / Achica);
        if (tam <= 0f) { charcos.Remove(this); Destroy(gameObject); return; }
        transform.localScale = new Vector3(Ancho * tam, 0.02f, Ancho * tam);

        if (Time.time < proximoTic) return;
        proximoTic += Tic;
        float radio = Ancho * tam * 0.5f;
        foreach (HealthSystem vida in jugadores)
        {
            if (vida == null || vida.currentHealth <= 0) continue;
            Vector3 rel = vida.transform.position - transform.position;
            if (Mathf.Abs(rel.y) > 1.6f) continue; // arriba o abajo (otro piso)
            rel.y = 0f;
            if (rel.magnitude > radio) continue;
            HealthSystem.DamageOrigin = transform.position;
            HealthSystem.DamageSource = fuente;
            vida.TakeDamage(DificultadDeInvasion.DanioDeEnemigo(DanioPorTic)); // US 161, CA3
            HealthSystem.DamageSource = null;
        }
    }

    private void OnDestroy() => charcos.Remove(this);
}

// El material del veneno (verde, semitransparente, sin luces), uno por color.
public static class Veneno
{
    private static readonly Dictionary<Color, Material> materiales = new Dictionary<Color, Material>();

    public static Material Material(Color color)
    {
        if (materiales.TryGetValue(color, out Material m) && m != null) return m;
        m = new Material(Shader.Find("Sprites/Default")) { name = "Veneno", color = color, renderQueue = 3000 };
        materiales[color] = m;
        return m;
    }
}
