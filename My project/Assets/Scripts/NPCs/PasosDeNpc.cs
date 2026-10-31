using UnityEngine;

// F11 (NPCs): los pasos de los enemigos, para escuchar a los que se acercan. Suena una pisada cada largoDelPaso metros
// que camina (así van al ritmo de su andar: más seguido al correr), una al azar entre las del tipo (TipoDeNpc.pasos) y
// nunca la misma dos veces seguidas. Se escuchan de cerca (15 m) y bajito, y como mucho suenan unas pocas a la vez,
// así una horda no tapa todo. Quieto, atacando, volando o muerto no pisa.
// Lo agrega Npc.Crear si el tipo tiene pasos.
[RequireComponent(typeof(Npc))]
public class PasosDeNpc : MonoBehaviour
{
    private const float Hasta = 15f, Volumen = 0.6f;
    private const int AlaVez = 6; // pisadas sonando a la vez, entre todos los enemigos

    private static int sonando;
    private static float limpiar;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar() { sonando = 0; limpiar = 0f; } // sin recarga de dominio

    private Npc npc;
    private PersecucionDeNpc persecucion;
    private Vector3 antes;
    private float andado;
    private int ultimo = -1;

    private void Awake()
    {
        npc = GetComponent<Npc>();
        antes = transform.position;
        andado = Random.Range(0f, 1f); // no todos pisan al mismo tiempo
    }

    private void Update()
    {
        if (persecucion == null) persecucion = GetComponent<PersecucionDeNpc>();
        Vector3 ahora = transform.position;
        Vector3 paso = ahora - antes;
        antes = ahora;
        if (npc == null || npc.Tipo == null || !npc.Vivo || npc.Tipo.pasos == null || npc.Tipo.pasos.Length == 0) return;
        if (persecucion != null && persecucion.Controlado) return; // embestida o vuelo: lo maneja otro (y no pisa)
        paso.y = 0f;
        float metros = paso.magnitude;
        if (metros > 3f) return; // lo movieron de golpe (apareció o lo empujaron)
        andado += metros;
        float largo = Mathf.Max(0.3f, npc.Tipo.largoDelPaso);
        if (andado < largo) return;
        andado %= largo;
        Pisar();
    }

    private void Pisar()
    {
        if (Time.time > limpiar) { sonando = 0; limpiar = Time.time + 0.3f; } // cuenta las de los últimos 0,3 s
        if (sonando >= AlaVez) return;
        AudioClip[] pasos = npc.Tipo.pasos;
        int i = Random.Range(0, pasos.Length);
        if (pasos.Length > 1 && i == ultimo) i = (i + 1) % pasos.Length;
        ultimo = i;
        if (pasos[i] == null) return;
        AudioSource fuente = SonidoDeNpc.Uno(pasos[i], transform, Hasta, Volumen);
        if (fuente == null) return;
        fuente.transform.localPosition = Vector3.zero; // en el piso
        fuente.pitch = Random.Range(0.92f, 1.08f);      // un poco distinta cada vez
        sonando++;
    }
}
