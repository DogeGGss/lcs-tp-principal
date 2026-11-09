using UnityEngine;

// La bala que se ve al disparar: una estela corta y brillante que viaja desde la punta del arma hasta donde pegó,
// y un fogonazo de luz en la boca del arma. Es solo visual: el daño ya lo calculó WeaponFire con un rayo.
// La usan los disparos del propio jugador (WeaponFire) y los de los demás jugadores en red (JugadorEnRed, US 028).
public class Trazadora : MonoBehaviour
{
    private const float Velocidad = 260f;      // m/s: se llega a ver el recorrido sin que parezca lenta
    private const float Largo = 7f;            // largo de la estela, en metros
    private const float DistanciaMinima = 2.5f; // más cerca no se dibuja: no se llega a ver
    private const float TiempoFogonazo = 0.05f;

    private LineRenderer linea;
    private Vector3 desde, direccion;
    private float distancia, recorrido;

    /// <summary>Color del disparo con la bronca cargada (la carta Bronca de Invasión, US 330).</summary>
    public static readonly Color Bronca = new Color(1f, 0.12f, 0.08f);

    /// <summary>Si no es null, los disparos que salen ahora se tiñen de este color, gruesos y fuertes como los de la
    /// bronca (US 320: la descarga cargada de La Trochita, dorada). Lo pone y lo saca el arma alrededor del disparo.</summary>
    public static Color? Tinte;
    private static readonly System.Collections.Generic.Dictionary<Color, Material> teñidas =
        new System.Collections.Generic.Dictionary<Color, Material>();

    /// <summary>Estela de "desde" (la boca del arma) hasta "hasta" (donde pegó la bala); roja, la del disparo con bronca.</summary>
    public static void Mostrar(Vector3 desde, Vector3 hasta, bool conBronca = false)
    {
        Material material = ConfigRed.Actual != null ? ConfigRed.Actual.trazadora : null;
        Color? tinte = conBronca ? (Color?)Bronca : Tinte;
        if (material != null && tinte.HasValue) material = Teñida(material, tinte.Value);
        conBronca = tinte.HasValue; // (gruesa y fuerte)
        Vector3 tramo = hasta - desde;
        float distancia = tramo.magnitude;
        if (material == null || distancia < DistanciaMinima) return;

        var go = new GameObject("Trazadora");
        Trazadora t = go.AddComponent<Trazadora>();
        t.desde = desde;
        t.direccion = tramo / distancia;
        t.distancia = distancia;

        t.linea = go.AddComponent<LineRenderer>();
        t.linea.sharedMaterial = material;
        t.linea.positionCount = 2;
        t.linea.startWidth = conBronca ? 0.025f : 0.012f; // la cola, más fina
        t.linea.endWidth = conBronca ? 0.07f : 0.035f;    // la punta
        if (conBronca) t.linea.startColor = t.linea.endColor = tinte.Value;
        t.linea.numCapVertices = 2;
        t.linea.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        t.linea.receiveShadows = false;
        t.Colocar();
    }

    // La misma estela, teñida (más gruesa y fuerte, para que se note): roja la de la bronca, dorada la de la descarga.
    private static Material Teñida(Material comun, Color color)
    {
        if (teñidas.TryGetValue(color, out Material m) && m != null) return m;
        m = new Material(comun) { name = comun.name + " (teñida)" };
        foreach (string p in new[] { "_BaseColor", "_Color", "_TintColor", "_EmissionColor" })
            if (m.HasProperty(p)) m.SetColor(p, color * 3f);
        teñidas[color] = m;
        return m;
    }

    /// <summary>Destello de luz en la boca del arma, un instante; rojo y más fuerte, el del disparo con bronca.</summary>
    public static void Fogonazo(Vector3 lugar, bool conBronca = false)
    {
        var go = new GameObject("Fogonazo");
        go.transform.position = lugar;
        Light luz = go.AddComponent<Light>();
        luz.type = LightType.Point;
        Color? tinte = conBronca ? (Color?)Bronca : Tinte;
        conBronca = tinte.HasValue;
        luz.color = tinte ?? new Color(1f, 0.78f, 0.45f);
        luz.intensity = conBronca ? 6f : 2.2f;
        luz.range = conBronca ? 7f : 5f;
        luz.shadows = LightShadows.None;
        Destroy(go, conBronca ? TiempoFogonazo * 3f : TiempoFogonazo);
    }

    /// <summary>
    /// Dónde está la boca del arma del propio jugador: abajo a la derecha de la cámara, donde se ve el arma en la mano
    /// (el arma se dibuja con otra cámara, así que su posición real no sirve).
    /// </summary>
    public static Vector3 BocaLocal(Camera camara)
    {
        Transform t = camara.transform;
        return t.position + t.forward * 0.9f + t.right * 0.16f - t.up * 0.13f;
    }

    private void Update()
    {
        recorrido += Velocidad * Time.deltaTime;
        if (recorrido - Largo >= distancia) { Destroy(gameObject); return; }
        Colocar();
    }

    private void Colocar()
    {
        float punta = Mathf.Min(recorrido + Largo * 0.5f, distancia), cola = Mathf.Clamp(recorrido - Largo * 0.5f, 0f, distancia);
        linea.SetPosition(0, desde + direccion * cola);
        linea.SetPosition(1, desde + direccion * punta);
    }
}
