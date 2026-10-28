using UnityEngine;

// F11 (NPCs), US 038, CA1: las zonas de impacto de un NPC (cabeza, cuerpo y piernas, como las de los jugadores de la
// US 165), así cada bala hace el daño de su arma según dónde pega y a qué distancia (WeaponFire). Los tres modelos
// tienen el esqueleto de 3ds Max (Bip01): se arman solas sobre sus huesos y se mueven con la animación.
// - Cabeza: una esfera en "Bip01 Head".
// - Cuerpo: una cápsula de la pelvis al cuello (acostada en los de cuatro patas).
// - Piernas: una cápsula por pata (muslo a pie) y por brazo (brazo a mano).
// Son triggers: no frenan a nadie, solo las encuentran las balas. Si el modelo no tiene esos huesos, no se arma nada
// y todo el bicho cuenta como cuerpo (su cápsula).
// Si el prefab del bicho ya trae zonas (las que se pasan con Riftwalker > NPCs > Pasar zonas de impacto a los prefabs
// y después se ajustan a mano), se usan esas y no se arma ninguna.
public static class ZonasDeNpc
{
    /// <summary>Las zonas del modelo: si ya tiene, las deja; si no, las arma. Devuelve cuántas tiene.</summary>
    public static int Asegurar(Transform modelo)
    {
        int tiene = modelo.GetComponentsInChildren<HitZone>(true).Length;
        return tiene > 0 ? tiene : Armar(modelo);
    }

    /// <summary>Arma las zonas en el modelo (sin mirar si ya tiene). Devuelve cuántas armó.</summary>
    public static int Armar(Transform modelo)
    {
        Transform cabeza = Hueso(modelo, "Bip01 Head");
        Transform cuello = Hueso(modelo, "Bip01 Neck");
        Transform pelvis = Hueso(modelo, "Bip01 Pelvis");
        if (cabeza == null || pelvis == null) return 0;
        if (cuello == null) cuello = cabeza.parent;

        int hechas = 0;
        float largoCuerpo = Vector3.Distance(pelvis.position, cuello.position);

        // Cabeza: esfera del tamaño de la distancia cabeza-cuello (con un mínimo).
        float radioCabeza = Mathf.Max(0.08f, Vector3.Distance(cabeza.position, cuello.position) * 0.9f, largoCuerpo * 0.18f);
        hechas += Esfera(cabeza, radioCabeza, BodyZone.Head);

        // Cuerpo: de la pelvis al cuello.
        hechas += Capsula(pelvis, pelvis.position, cuello.position, Mathf.Max(0.1f, largoCuerpo * 0.3f), BodyZone.Body);

        // Patas y brazos.
        foreach (string lado in new[] { "L", "R" })
        {
            hechas += Extremidad(modelo, $"Bip01 {lado} Thigh", $"Bip01 {lado} Foot", largoCuerpo);
            hechas += Extremidad(modelo, $"Bip01 {lado} UpperArm", $"Bip01 {lado} Hand", largoCuerpo);
        }
        return hechas;
    }

    private static int Extremidad(Transform modelo, string desde, string hasta, float largoCuerpo)
    {
        Transform a = Hueso(modelo, desde), b = Hueso(modelo, hasta);
        if (a == null || b == null) return 0;
        return Capsula(a, a.position, b.position, Mathf.Max(0.05f, largoCuerpo * 0.12f), BodyZone.Legs);
    }

    private static int Esfera(Transform hueso, float radio, BodyZone zona)
    {
        GameObject go = Zona(hueso, zona);
        go.transform.position = hueso.position;
        var esfera = go.AddComponent<SphereCollider>();
        esfera.isTrigger = true;
        esfera.radius = radio / Mathf.Max(0.0001f, go.transform.lossyScale.x);
        return 1;
    }

    // Una cápsula de "a" a "b" (en el mundo), colgada del hueso para que se mueva con él.
    private static int Capsula(Transform hueso, Vector3 a, Vector3 b, float radio, BodyZone zona)
    {
        Vector3 eje = b - a;
        if (eje.sqrMagnitude < 1e-6f) return 0;
        GameObject go = Zona(hueso, zona);
        go.transform.SetPositionAndRotation((a + b) * 0.5f, Quaternion.LookRotation(eje.normalized));
        var capsula = go.AddComponent<CapsuleCollider>();
        capsula.isTrigger = true;
        float escala = Mathf.Max(0.0001f, go.transform.lossyScale.x);
        capsula.direction = 2; // a lo largo de z (de a hacia b)
        capsula.radius = radio / escala;
        capsula.height = (eje.magnitude + radio * 2f) / escala;
        return 1;
    }

    private static GameObject Zona(Transform hueso, BodyZone zona)
    {
        var go = new GameObject("Zona de impacto (" + zona + ")");
        go.layer = hueso.gameObject.layer;
        go.transform.SetParent(hueso, false);
        go.AddComponent<HitZone>().zone = zona;
        return go;
    }

    private static Transform Hueso(Transform raiz, string nombre)
    {
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            if (t.name == nombre) return t;
        return null;
    }
}
