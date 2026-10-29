using System.Collections.Generic;
using UnityEngine;

// F11 (NPCs), US 037: cuándo pega y hasta dónde llega cada animación de ataque de un tipo de NPC. Se mide una vez por
// tipo y animación (al aparecer el primero), recorriendo el clip: el momento del golpe es cuando el cuerpo (sus zonas de
// impacto: cabeza, cuerpo, brazos y patas) llega más adelante, y el avance es cuánto pasa del frente que tiene quieto.
// Así el daño entra cuando se ve el manotazo (CA2) y el NPC se frena lo bastante lejos para que una embestida no
// atraviese al jugador (PersecucionDeNpc).
public struct GolpeDeNpc
{
    private const float Paso = 1f / 30f;   // s entre muestras del clip
    private const float CasiAdelante = 0.9f; // el golpe es la primera vez que llega al 90 % de lo más adelante

    /// <summary>Segundos desde que empieza la animación hasta el golpe.</summary>
    public float momento;
    /// <summary>Metros que el cuerpo pasa, en el golpe, del frente que tiene quieto (Npc.Frente).</summary>
    public float avance;

    private static readonly Dictionary<(TipoDeNpc, AnimationClip), GolpeDeNpc> medidos = new Dictionary<(TipoDeNpc, AnimationClip), GolpeDeNpc>();

    // Sin recarga de scripts al entrar al Play (Enter Play Mode Options): se vuelve a medir en cada partida, por si
    // cambiaron los datos.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Limpiar() => medidos.Clear();

    /// <summary>Mide esa animación en el modelo del NPC (lo deja en la pose del final: hay que volver a poner la suya).</summary>
    public static GolpeDeNpc Medir(Npc npc, TipoDeNpc tipo, AnimationClip clip)
    {
        if (medidos.TryGetValue((tipo, clip), out GolpeDeNpc ya)) return ya;
        var g = new GolpeDeNpc { momento = Mathf.Min(0.5f, clip.length), avance = 0f };
        if (npc.Modelo == null || clip.length <= 0f) return g;

        var colliders = new List<Collider>();
        foreach (HitZone zona in npc.GetComponentsInChildren<HitZone>(true)) colliders.AddRange(zona.GetComponents<Collider>());
        if (colliders.Count == 0) return g;

        // Lo más adelante que llega el cuerpo en cada momento del clip.
        var tiempos = new List<float>();
        var frentes = new List<float>();
        GameObject modelo = npc.Modelo.gameObject;
        for (float t = 0f; t <= clip.length + 1e-4f; t += Paso)
        {
            clip.SampleAnimation(modelo, Mathf.Min(t, clip.length));
            float frente = float.MinValue;
            foreach (Collider c in colliders)
            foreach ((Vector3 punto, float radio) in Npc.Puntos(c))
                frente = Mathf.Max(frente, npc.transform.InverseTransformPoint(punto).z + radio);
            tiempos.Add(t);
            frentes.Add(frente);
        }

        float maximo = float.MinValue, minimo = float.MaxValue;
        foreach (float f in frentes) { maximo = Mathf.Max(maximo, f); minimo = Mathf.Min(minimo, f); }
        float umbral = Mathf.Lerp(minimo, maximo, CasiAdelante);
        for (int i = 0; i < frentes.Count; i++)
            if (frentes[i] >= umbral) { g.momento = Mathf.Clamp(tiempos[i], 0.1f, clip.length); break; }
        g.avance = Mathf.Max(0f, maximo - npc.Frente);
        medidos[(tipo, clip)] = g;
        return g;
    }
}
