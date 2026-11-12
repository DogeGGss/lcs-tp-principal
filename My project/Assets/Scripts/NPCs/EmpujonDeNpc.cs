using UnityEngine;
using UnityEngine.AI;

// Un empujón a un NPC: lo lleva unos metros hacia un lado en poco tiempo, por el NavMesh (así no atraviesa paredes) y
// sin que persiga ni ataque mientras dura (lo deja aturdido ese rato). Lo usa la carta Despeje (US 333).
public class EmpujonDeNpc : MonoBehaviour
{
    private const float Dura = 0.45f; // s que tarda en recorrer el empujón

    private Npc npc;
    private NavMeshAgent agente;
    private Vector3 cuanto;
    private float desde, hecho;

    /// <summary>Empuja al NPC esos metros (en el plano) hacia esa dirección.</summary>
    public static void Empujar(Npc npc, Vector3 direccion, float metros)
    {
        if (npc == null || !npc.Vivo) return;
        direccion.y = 0f;
        if (direccion.sqrMagnitude < 0.0001f) return;
        if (npc.EsEspejo) { InvasoresEnRed.PedirEmpujon(npc, direccion, metros); return; } // F31 (US 290): lo mueve el anfitrión
        EmpujonDeNpc e = npc.GetComponent<EmpujonDeNpc>();
        if (e == null) e = npc.gameObject.AddComponent<EmpujonDeNpc>();
        e.npc = npc;
        e.agente = npc.GetComponent<NavMeshAgent>();
        e.cuanto = direccion.normalized * metros;
        e.desde = Time.time;
        e.hecho = 0f;
        e.enabled = true;
        npc.Aturdir(Dura + 0.25f);
    }

    private void Update()
    {
        if (npc == null || !npc.Vivo) { enabled = false; return; }
        float t = Mathf.Clamp01((Time.time - desde) / Dura);
        float avance = 1f - (1f - t) * (1f - t); // sale fuerte y se frena
        Vector3 paso = cuanto * (avance - hecho);
        hecho = avance;
        if (agente != null && agente.enabled && agente.isOnNavMesh) agente.Move(paso);
        else transform.position += paso; // los que vuelan (sin agente en ese momento)
        if (t >= 1f) enabled = false;
    }
}
