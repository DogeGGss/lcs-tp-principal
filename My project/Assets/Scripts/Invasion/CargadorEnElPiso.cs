using UnityEngine;

// US 318 (carta Carroñero): el cargador que suelta un invasor al morir. Queda en el piso girando y flotando un poco, con
// un brillo, y se junta pasando por encima: suma un cargador entero al arma que se tiene en la mano (a la reserva; si
// está llena, al cargador). Si nadie lo junta, a los 25 s se va (los últimos 5 titila).
public class CargadorEnElPiso : MonoBehaviour
{
    private const float Dura = 25f, Titila = 5f;
    private const float Alcance = 1.3f;          // m en horizontal para juntarlo
    private static readonly Color Brillo = new Color(1f, 0.72f, 0.2f);

    private System.Action alJuntar;
    private Transform jugador;
    private Renderer dibujo;
    private float nacio;
    private Vector3 piso;

    /// <summary>Lo deja en el piso debajo de ese lugar (sin contar al que lo suelta).</summary>
    public static CargadorEnElPiso Soltar(Vector3 lugar, Transform quien, System.Action alJuntar)
    {
        Vector3 piso = lugar;
        float masCerca = float.MaxValue;
        foreach (RaycastHit h in Physics.RaycastAll(lugar + Vector3.up, Vector3.down, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (quien != null && h.collider.transform.IsChildOf(quien)) continue; // el cuerpo del invasor
            if (h.distance < masCerca) { masCerca = h.distance; piso = h.point; }
        }

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Cargador (Carroñero)";
        Object.Destroy(go.GetComponent<Collider>()); // no frena ni balas ni invasores
        go.transform.localScale = new Vector3(0.1f, 0.26f, 0.06f);
        go.transform.position = piso + Vector3.up * 0.35f;
        var cargador = go.AddComponent<CargadorEnElPiso>();
        cargador.alJuntar = alJuntar;
        cargador.piso = piso;
        cargador.nacio = Time.time;
        cargador.dibujo = go.GetComponent<Renderer>();
        cargador.dibujo.sharedMaterial = Material();
        cargador.dibujo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return cargador;
    }

    private static Material material;

    private static Material Material()
    {
        if (material != null) return material;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        material = new Material(shader) { name = "Cargador (Carroñero)" };
        material.color = Brillo * 0.6f;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Brillo * 0.6f);
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Brillo * 2.5f);
        }
        return material;
    }

    private void Update()
    {
        float edad = Time.time - nacio;
        if (edad >= Dura) { Destroy(gameObject); return; }
        // Gira y flota; los últimos segundos titila.
        transform.position = piso + Vector3.up * (0.35f + Mathf.Sin(edad * 3f) * 0.06f);
        transform.rotation = Quaternion.Euler(0f, edad * 120f, 0f);
        if (dibujo != null) dibujo.enabled = edad < Dura - Titila || Mathf.Repeat(edad * 6f, 1f) < 0.6f;

        if (jugador == null)
        {
            PlayerMovement p = FindFirstObjectByType<PlayerMovement>();
            if (p == null) return;
            jugador = p.transform;
        }
        Vector3 d = jugador.position - piso;
        if (Mathf.Abs(d.y) > 2f || new Vector2(d.x, d.z).magnitude > Alcance) return;
        alJuntar?.Invoke();
        Destroy(gameObject);
    }
}
