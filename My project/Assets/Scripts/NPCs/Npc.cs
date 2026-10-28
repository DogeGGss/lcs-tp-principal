using System.Collections.Generic;
using UnityEngine;

// F11 (NPCs), US 145: un NPC enemigo armado según su tipo (TipoDeNpc). Npc.Crear lo arma en un lugar:
// - el modelo de su tipo, al tamaño de su tipo (CA4), con su Animator (sin root motion: lo mueve el código);
// - la vida de su tipo, sin escudo (CA1 a CA3), en un HealthSystem como el de los jugadores, así las armas lo dañan
//   igual (US 038);
// - una cápsula para chocar y recibir disparos, del tamaño del modelo.
// - sus animaciones, desde el código (Animaciones, AnimadorDeNpc): el controlador de muestra del modelo no sirve;
// - la persecución del jugador (PersecucionDeNpc, US 035 y 036) y sus golpes (AtaqueDeNpc, US 037);
// - sus zonas de impacto (ZonasDeNpc), la reacción a los golpes (ReaccionDeNpc) y la muerte (MuerteDeNpc), US 038 y
//   039; la flash lo aturde (Aturdir).
// Para el modo Zombie (F08) avisa cada impacto de bala (ImpactoDeBala, puntos de la US 147) y cada baja (Murio,
// puntos y oleadas, US 144 y 147). Todos los NPCs de la escena están en Todos.: leen de acá el tipo (Tipo) y las animaciones.
public class Npc : MonoBehaviour
{
    /// <summary>El tipo de este NPC (vida, daño, velocidad, puntos).</summary>
    public TipoDeNpc Tipo { get; private set; }
    /// <summary>El Animator de su modelo.</summary>
    public Animator Animador { get; private set; }
    /// <summary>Su vida.</summary>
    public HealthSystem Vida { get; private set; }
    /// <summary>Su modelo (hijo de este objeto).</summary>
    public Transform Modelo { get; private set; }
    /// <summary>Sus animaciones (las de su tipo).</summary>
    public AnimadorDeNpc Animaciones { get; private set; }
    /// <summary>Está vivo.</summary>
    public bool Vivo => Vida != null && Vida.currentHealth > 0;
    /// <summary>Aturdido por la flash (US 038, CA2): quieto, sin perseguir ni atacar.</summary>
    public bool Aturdido => Time.time < aturdidoHasta;

    private float aturdidoHasta = -10f;

    private const float FlashAlcance = 15f, FlashAturde = 2f; // US 038, CA2

    private static readonly List<Npc> todos = new List<Npc>();
    /// <summary>Los NPCs de la escena (vivos o cayendo).</summary>
    public static IReadOnlyList<Npc> Todos => todos;

    /// <summary>US 038, CA4: un impacto de bala en un NPC (el NPC, el daño y la zona). El modo Zombie suma 10 puntos.</summary>
    public static event System.Action<Npc, int, BodyZone> ImpactoDeBala;
    /// <summary>US 039, CA3 y CA4: murió un NPC (el NPC, sus puntos y si fue a la cabeza).</summary>
    public static event System.Action<Npc, int, bool> Murio;

    // El proyecto entra al Play sin recargar scripts: se engancha una sola vez y se limpia al empezar cada partida.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Iniciar()
    {
        todos.Clear();
        WeaponFire.Impacto -= AlImpacto;
        WeaponFire.Impacto += AlImpacto;
        Grenade1.Exploded -= AlExplotar;
        Grenade1.Exploded += AlExplotar;
    }

    private static void AlImpacto(HealthSystem vida, int danio, BodyZone zona)
    {
        if (vida == null) return;
        Npc npc = vida.GetComponent<Npc>();
        if (npc != null && npc.Vivo) ImpactoDeBala?.Invoke(npc, danio, zona);
    }

    // US 038, CA2: la flash aturde 2 s a los que estén a menos de 15 m sin una pared en el medio. El humo, nada.
    private static void AlExplotar(Grenade1 granada, Vector3 centro)
    {
        if (granada == null || granada.Type != GrenadeType.Flash) return;
        foreach (Npc npc in todos)
        {
            if (npc == null || !npc.Vivo) continue;
            Vector3 pecho = npc.transform.position + Vector3.up * 0.8f;
            if (Vector3.Distance(pecho, centro) > FlashAlcance) continue;
            if (Physics.Linecast(centro, pecho, out RaycastHit tapa, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && tapa.collider.GetComponentInParent<Npc>() == null) continue;
            npc.Aturdir(FlashAturde);
        }
    }

    /// <summary>Lo deja quieto esos segundos, con la animación de aturdido (o de quieto) (US 038, CA2).</summary>
    public void Aturdir(float segundos)
    {
        if (!Vivo) return;
        aturdidoHasta = Mathf.Max(aturdidoHasta, Time.time + segundos);
        if (Animaciones != null && Tipo != null)
        {
            Animaciones.Poner(Tipo.aturdido != null ? Tipo.aturdido : Tipo.quieto, true, 0.1f);
            Animaciones.Velocidad(1f);
        }
    }

    /// <summary>Lo llama MuerteDeNpc al morir.</summary>
    public static void AvisarMuerte(Npc npc, int puntos, bool cabeza) => Murio?.Invoke(npc, puntos, cabeza);

    /// <summary>Arma un NPC de ese tipo en ese lugar, mirando hacia ahí. persigue: sale a buscar al jugador (US 035).</summary>
    public static Npc Crear(TipoDeNpc tipo, Vector3 lugar, Quaternion giro, bool persigue = true)
    {
        if (tipo == null || tipo.modelo == null)
        {
            Debug.LogWarning("Npc: el tipo " + (tipo != null ? tipo.name : "(ninguno)") + " no tiene modelo.");
            return null;
        }
        var go = new GameObject("NPC · " + tipo.nombre);
        go.transform.SetPositionAndRotation(lugar, giro);
        Npc npc = go.AddComponent<Npc>();
        npc.Armar(tipo);
        go.AddComponent<ReaccionDeNpc>(); // US 038
        go.AddComponent<MuerteDeNpc>();   // US 039
        if (persigue)
        {
            go.AddComponent<PersecucionDeNpc>();
            go.AddComponent<AtaqueDeNpc>(); // US 037
        }
        return npc;
    }

    private void Armar(TipoDeNpc tipo)
    {
        Tipo = tipo;

        // El modelo, al tamaño del tipo (CA4).
        GameObject m = Instantiate(tipo.modelo, transform);
        m.name = "Modelo";
        m.transform.localPosition = Vector3.zero;
        m.transform.localRotation = Quaternion.identity;
        m.transform.localScale = tipo.modelo.transform.localScale * tipo.escala;
        Modelo = m.transform;
        Animador = m.GetComponentInChildren<Animator>();
        if (Animador != null)
        {
            Animador.applyRootMotion = false;                          // lo mueve el código (US 035)
            Animador.cullingMode = AnimatorCullingMode.CullUpdateTransforms; // lejos de la cámara no se anima de más
            Animaciones = new AnimadorDeNpc(Animador);
            Animaciones.Poner(tipo.quieto != null ? tipo.quieto : tipo.quietoEnCombate, true, 0f);
        }

        // La vida del tipo, sin escudo (CA1 a CA3).
        Vida = gameObject.AddComponent<HealthSystem>();
        Vida.maxHealth = tipo.vida;
        Vida.maxShield = 0;
        Vida.SetState(tipo.vida, 0);

        // Una cápsula del tamaño del modelo, parada (para chocar y para los disparos).
        Bounds caja = Limites(m);
        var capsula = gameObject.AddComponent<CapsuleCollider>();
        Vector3 centro = transform.InverseTransformPoint(caja.center);
        float alto = caja.size.y / Mathf.Max(0.0001f, transform.lossyScale.y);
        float ancho = Mathf.Min(caja.size.x, caja.size.z) / Mathf.Max(0.0001f, transform.lossyScale.x);
        capsula.direction = 1;
        capsula.center = centro;
        capsula.height = Mathf.Max(alto, ancho);
        capsula.radius = Mathf.Max(0.2f, ancho * 0.5f);

        // US 038, CA1: cabeza, cuerpo y piernas, sobre sus huesos (las del prefab, si las tiene).
        ZonasDeNpc.Asegurar(m.transform);
    }

    private void OnEnable() => todos.Add(this);
    private void OnDisable() => todos.Remove(this);

    private void Update()
    {
        if (Animaciones != null) Animaciones.Actualizar(Time.deltaTime);
    }

    private void OnDestroy()
    {
        if (Animaciones != null) Animaciones.Destruir();
    }

    // La caja de lo que se ve del modelo (en el mundo).
    private static Bounds Limites(GameObject m)
    {
        Renderer[] partes = m.GetComponentsInChildren<Renderer>();
        if (partes.Length == 0) return new Bounds(m.transform.position + Vector3.up, Vector3.one);
        Bounds b = partes[0].bounds;
        foreach (Renderer r in partes) b.Encapsulate(r.bounds);
        return b;
    }
}
