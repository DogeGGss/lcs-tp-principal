using UnityEngine;

// US 326, Carga adherente (carta de Invasión): una granada del Gran Capitán que le pega directo a un invasor se le
// queda pegada y explota al segundo con el doble de daño para los que alcanza (a quien la tiró, el de siempre).
// Se cuelga del hueso del esqueleto animado más cercano a donde pegó (no del cuerpo entero, que no sigue la
// animación): así se mueve con el brazo, la pierna o el torso como si estuviera clavada. La punta queda metida un poco
// hacia el hueso (la colisión del invasor es más grande que el modelo). Mientras está pegada, la cola de la cápsula
// titila rápido (CapsulaDeGranada). Si el invasor muere antes, se suelta y se queda donde estaba, y explota igual.
// F31 (US 355, CA2): en Invasión cooperativa los compañeros la ven pegada al mismo invasor (JugadorEnRed.RpcGranadaPegada).
public class GranadaPegada : MonoBehaviour
{
    public const float Espera = 1f;           // s pegada hasta explotar
    public const float MasDanio = 2f;         // veces el daño
    private const float HaciaElHueso = 0.45f; // qué parte del camino del choque al hueso se mete la punta

    /// <summary>F31 (US 355, CA2): una granada de verdad se le pegó a ese invasor en ese punto (el multijugador la repite).</summary>
    public static event System.Action<Grenade1, Npc, Vector3> SePego;

    private Grenade1 granada;
    private Npc invasor;
    private float desde;
    private bool suelta;

    public static void Pegar(Grenade1 granada, Npc invasor, Transform parte, Vector3 punto)
    {
        Rigidbody body = granada.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }
        granada.esMina = true; // la mecha de siempre no corre: explota esta, al segundo
        granada.multiplicadorDeDanio = MasDanio;
        foreach (Collider c in granada.GetComponentsInChildren<Collider>()) c.isTrigger = true; // no se choca con nada más

        var p = granada.gameObject.AddComponent<GranadaPegada>();
        p.granada = granada;
        p.invasor = invasor;
        p.desde = Time.time;

        Transform hueso = HuesoMasCercano(invasor, punto);
        if (hueso != null) punto = Vector3.Lerp(punto, hueso.position, HaciaElHueso);
        CapsulaDeGranada capsula = granada.GetComponent<CapsulaDeGranada>();
        if (capsula != null) capsula.Clavar(punto); // la punta ahí, como venía
        else granada.transform.position = punto;
        // Colgada del hueso (o del invasor, si no tiene esqueleto), sin cambiar dónde ni cómo está.
        granada.transform.SetParent(hueso != null ? hueso : invasor.transform, true);
        if (!granada.cosmetic) SePego?.Invoke(granada, invasor, punto);
    }

    // El hueso del modelo animado más cercano al punto (de las mallas con esqueleto del invasor).
    private static Transform HuesoMasCercano(Npc invasor, Vector3 punto)
    {
        Transform mejor = null;
        float menor = float.MaxValue;
        foreach (SkinnedMeshRenderer piel in invasor.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (piel.bones == null) continue;
            foreach (Transform hueso in piel.bones)
            {
                if (hueso == null) continue;
                float d = (hueso.position - punto).sqrMagnitude;
                if (d < menor) { menor = d; mejor = hueso; }
            }
        }
        return mejor;
    }

    private void LateUpdate()
    {
        if (granada == null) return;
        // Si el invasor murió (o desapareció), se suelta y se queda donde está: así no se borra con él.
        if (!suelta && (invasor == null || !invasor.Vivo))
        {
            suelta = true;
            granada.transform.SetParent(null, true);
        }
        if (granada.cosmetic) return; // (la copia de un compañero, US 355: explota cuando la red avisa)
        if (Time.time - desde >= Espera)
        {
            if (!suelta) granada.transform.SetParent(null, true);
            granada.ExplotarAhora();
        }
    }
}
