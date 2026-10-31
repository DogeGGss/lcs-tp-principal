using UnityEngine;
using UnityEngine.AI;

// F11 (NPCs), US 039: el NPC muere al quedarse sin vida.
// - CA1: deja de moverse y de atacar, y ya no bloquea el paso ni recibe disparos (se apagan su cápsula, sus zonas de
//   impacto y su agente de navegación).
// - CA2: hace la animación de morir y, cuando termina, se desintegra de arriba hacia abajo (US 284,
//   DesintegracionDeNpc) y desaparece.
// - CA3 y CA4: avisa la baja (Npc.Murio) con los puntos: los de su tipo, o los de cabeza si el último impacto fue a la
//   cabeza. Con eso el modo Zombie suma los puntos (US 147) y descuenta un enemigo de la oleada (US 144, US 150).
// Lo agrega Npc.Crear.
[RequireComponent(typeof(Npc))]
public class MuerteDeNpc : MonoBehaviour
{
    private const float SinClip = 1.5f, Quieto = 0.15f; // s de la muerte sin animación, y quieto antes de desintegrarse

    private Npc npc;
    private float murioEn = -1f;
    private float desintegraEn = -1f;

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
            npc.Animaciones.CortarGolpe();
            npc.Animaciones.Poner(npc.Tipo.morir, false, 0.1f, true);
            npc.Animaciones.Velocidad(1f);
        }

        // CA3 y CA4: la baja, con sus puntos.
        bool cabeza = npc.Vida.KilledByHeadshot;
        int puntos = npc.Tipo == null ? 0 : cabeza ? npc.Tipo.puntosPorBajaALaCabeza : npc.Tipo.puntosPorBaja;
        Npc.AvisarMuerte(npc, puntos, cabeza);
    }

    // CA2: cuando termina la animación de morir, se desintegra (y DesintegracionDeNpc lo borra al final).
    private void Update()
    {
        if (murioEn < 0f || desintegraEn == float.MaxValue) return;
        if (desintegraEn < 0f)
        {
            AnimationClip clip = npc.Tipo != null ? npc.Tipo.morir : null;
            desintegraEn = murioEn + (clip != null ? clip.length : SinClip) + Quieto;
        }
        if (Time.time < desintegraEn) return;
        desintegraEn = float.MaxValue;
        DesintegracionDeNpc.Empezar(npc);
    }
}
