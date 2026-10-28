using UnityEngine;

// F11 (NPCs), US 145: un NPC enemigo armado según su tipo (TipoDeNpc). Npc.Crear lo arma en un lugar:
// - el modelo de su tipo, al tamaño de su tipo (CA4), con su Animator (sin root motion: lo mueve el código);
// - la vida de su tipo, sin escudo (CA1 a CA3), en un HealthSystem como el de los jugadores, así las armas lo dañan
//   igual (US 038);
// - una cápsula para chocar y recibir disparos, del tamaño del modelo.
// Moverse, detectar, atacar y morir son las US 035 a 039: leen de acá el tipo (Tipo) y el Animator (Animador).
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

    /// <summary>Arma un NPC de ese tipo en ese lugar, mirando hacia ahí.</summary>
    public static Npc Crear(TipoDeNpc tipo, Vector3 lugar, Quaternion giro)
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
