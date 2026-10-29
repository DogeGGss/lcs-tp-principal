using UnityEngine;

// F11 (NPCs), US 038: lo que le pasa a un NPC cuando le pegan.
// - CA1: el daño de cada arma según la zona y la distancia lo hace WeaponFire, igual que a un jugador (ZonasDeNpc).
// - CA2: el cuchillo y la granada de metralla también lo dañan (MeleeAttack y Grenade1 buscan su vida). La flash lo
//   deja quieto 2 s (Npc.Aturdir, desde la explosión) y el humo no le hace nada.
// - CA3: al recibir un impacto hace la animación de recibir un golpe (TipoDeNpc.golpeRecibido) del torso para arriba,
//   mezclada con la de caminar, y le salta sangre, sin dejar de avanzar. Sin esa animación, se sacude un instante. Si
//   su tipo se frena con los impactos (TipoDeNpc.frenaAlRecibirImpacto), baja un poco la velocidad y la recupera de a
//   poco (no se frena en seco). El Tanque (y el Jefe) no.
// - CA4: cada impacto de bala avisa (Npc.ImpactoDeBala), para que el modo Zombie sume los 10 puntos (US 147).
// Lo agrega Npc.Crear.
[RequireComponent(typeof(Npc))]
public class ReaccionDeNpc : MonoBehaviour
{
    private const float Sacudida = 0.18f, Angulo = 9f;     // s y grados del sacudón
    private const float Freno = 0.4f, VelocidadFrenado = 0.65f; // s para recuperarse y fracción de la velocidad, si se frena

    private Npc npc;
    private float sacudidaHasta = -10f, frenadoEn = -10f;
    private Quaternion giroModelo = Quaternion.identity;
    private bool sacudiendo;

    /// <summary>Cuánto de su velocidad tiene ahora (1: toda; menos, recién golpeado). Lo usa la persecución.</summary>
    public float Velocidad => Mathf.Lerp(VelocidadFrenado, 1f, Mathf.Clamp01((Time.time - frenadoEn) / Freno));

    private void Awake() => npc = GetComponent<Npc>();

    private void OnEnable() => HealthSystem.Damaged += AlRecibirDanio;
    private void OnDisable() => HealthSystem.Damaged -= AlRecibirDanio;

    private void AlRecibirDanio(HealthSystem vida, int danio, Vector3? desde)
    {
        if (npc == null || vida != npc.Vida || danio <= 0) return;
        // CA3: la animación del golpe (o la sacudida), sangre y, si corresponde, un freno corto. El golpe que lo mata no:
        // ahí va la de morir (US 039).
        AnimationClip clip = npc.Tipo != null ? npc.Tipo.golpeRecibido : null;
        if (npc.Vivo && clip != null && npc.Animaciones != null) npc.Animaciones.Golpe(clip);
        else if (npc.Vivo)
        {
            if (!sacudiendo && npc.Modelo != null) giroModelo = npc.Modelo.localRotation;
            sacudidaHasta = Time.time + Sacudida;
            sacudiendo = true;
        }
        if (npc.Vivo && npc.Tipo != null && npc.Tipo.frenaAlRecibirImpacto) frenadoEn = Time.time;
        Sangre.Salpicar(PuntoDelGolpe(desde), desde);
    }

    // Donde le pegaron: el punto de su cuerpo más cercano a quien le disparó (o el pecho, si no se sabe).
    private Vector3 PuntoDelGolpe(Vector3? desde)
    {
        Collider cuerpo = GetComponent<Collider>();
        Vector3 pecho = cuerpo != null ? cuerpo.bounds.center : transform.position + Vector3.up;
        if (!desde.HasValue || cuerpo == null || !cuerpo.enabled) return pecho;
        Vector3 cerca = cuerpo.ClosestPoint(desde.Value);
        return Vector3.Lerp(cerca, pecho, 0.3f);
    }

    // La sacudida, después de la animación.
    private void LateUpdate()
    {
        if (!sacudiendo || npc == null || npc.Modelo == null) return;
        float queda = sacudidaHasta - Time.time;
        if (queda <= 0f)
        {
            npc.Modelo.localRotation = giroModelo;
            sacudiendo = false;
            return;
        }
        float fuerza = queda / Sacudida;
        float x = Mathf.Sin(Time.time * 70f) * Angulo * fuerza, z = Mathf.Cos(Time.time * 53f) * Angulo * 0.6f * fuerza;
        npc.Modelo.localRotation = giroModelo * Quaternion.Euler(x, 0f, z);
    }
}

// La sangre que salta al pegarle a un NPC (US 038, CA3): unas gotas rojas que salen hacia el que disparó y caen.
public static class Sangre
{
    private static Material material;

    public static void Salpicar(Vector3 punto, Vector3? desde)
    {
        var go = new GameObject("Sangre");
        go.transform.position = punto;
        Vector3 hacia = desde.HasValue ? (desde.Value - punto) : Vector3.up;
        if (hacia.sqrMagnitude > 1e-4f) go.transform.rotation = Quaternion.LookRotation(hacia.normalized);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.2f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0f, 0.02f), new Color(0.75f, 0.05f, 0.05f));
        main.gravityModifier = 1.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 30;
        var emision = ps.emission;
        emision.rateOverTime = 0f;
        emision.SetBursts(new[] { new ParticleSystem.Burst(0f, 14, 20) });
        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 35f;
        forma.radius = 0.05f;
        var render = go.GetComponent<ParticleSystemRenderer>();
        render.material = Material();
        render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        render.receiveShadows = false;
        ps.Play();
        Object.Destroy(go, 1.5f);
    }

    private static Material Material()
    {
        if (material == null)
        {
            Shader shader = Shader.Find("Sprites/Default"); // siempre está en el build (lo usa también el Trasbordo)
            if (shader != null) material = new Material(shader) { name = "Sangre de NPC" };
        }
        return material;
    }
}
