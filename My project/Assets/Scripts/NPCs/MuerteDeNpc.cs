using UnityEngine;
using UnityEngine.AI;

// F11 (NPCs), US 039: el NPC muere al quedarse sin vida.
// - CA1: deja de moverse y de atacar, y ya no bloquea el paso ni recibe disparos (se apagan su cápsula, sus zonas de
//   impacto y su agente de navegación).
// - CA2: hace la animación de morir y a los 5 s desaparece (el último medio segundo se hunde en el piso).
// - CA3 y CA4: avisa la baja (Npc.Murio) con los puntos: los de su tipo, o los de cabeza si el último impacto fue a la
//   cabeza. Con eso el modo Zombie suma los puntos (US 147) y descuenta un enemigo de la oleada (US 144, US 150).
// Lo agrega Npc.Crear.
[RequireComponent(typeof(Npc))]
public class MuerteDeNpc : MonoBehaviour
{
    private const float Desaparece = 5f, Hundirse = 0.6f, Hondo = 1.2f;

    private Npc npc;
    private float murioEn = -1f;
    private Vector3 dondeCayo;

    private void Awake() => npc = GetComponent<Npc>();

    private void Start()
    {
        if (npc != null && npc.Vida != null) npc.Vida.Died += Morir;
    }

    private void OnDestroy()
    {
        if (npc != null && npc.Vida != null) npc.Vida.Died -= Morir;
    }

    private void Morir()
    {
        if (murioEn >= 0f) return;
        murioEn = Time.time;
        dondeCayo = transform.position;

        // CA1: quieto, sin atacar, sin chocar y sin recibir disparos.
        NavMeshAgent agente = GetComponent<NavMeshAgent>();
        if (agente != null) agente.enabled = false;
        foreach (Collider c in GetComponentsInChildren<Collider>(true)) c.enabled = false;
        PersecucionDeNpc persecucion = GetComponent<PersecucionDeNpc>();
        if (persecucion != null) persecucion.enabled = false;
        AtaqueDeNpc ataque = GetComponent<AtaqueDeNpc>();
        if (ataque != null) ataque.enabled = false;

        // CA2: la animación de morir (queda en el último cuadro).
        if (npc.Animaciones != null && npc.Tipo != null && npc.Tipo.morir != null)
        {
            npc.Animaciones.Poner(npc.Tipo.morir, false, 0.1f, true);
            npc.Animaciones.Velocidad(1f);
        }

        // CA3 y CA4: la baja, con sus puntos.
        bool cabeza = npc.Vida.KilledByHeadshot;
        int puntos = npc.Tipo == null ? 0 : cabeza ? npc.Tipo.puntosPorBajaALaCabeza : npc.Tipo.puntosPorBaja;
        Npc.AvisarMuerte(npc, puntos, cabeza);
    }

    // CA2: a los 5 s, se hunde y desaparece.
    private void Update()
    {
        if (murioEn < 0f) return;
        float t = Time.time - murioEn;
        float hundido = Mathf.InverseLerp(Desaparece - Hundirse, Desaparece, t);
        if (hundido > 0f) transform.position = dondeCayo + Vector3.down * Hondo * hundido;
        if (t >= Desaparece) Destroy(gameObject);
    }
}
