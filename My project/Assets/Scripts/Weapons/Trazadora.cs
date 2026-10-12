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

    /// <summary>Estela de "desde" (la boca del arma) hasta "hasta" (donde pegó la bala).</summary>
    public static void Mostrar(Vector3 desde, Vector3 hasta)
    {
        Material material = ConfigRed.Actual != null ? ConfigRed.Actual.trazadora : null;
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
        t.linea.startWidth = 0.012f; // la cola, más fina
        t.linea.endWidth = 0.035f;   // la punta
        t.linea.numCapVertices = 2;
        t.linea.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        t.linea.receiveShadows = false;
        t.Colocar();
    }

    /// <summary>Destello de luz en la boca del arma, un instante.</summary>
    public static void Fogonazo(Vector3 lugar)
    {
        var go = new GameObject("Fogonazo");
        go.transform.position = lugar;
        Light luz = go.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = new Color(1f, 0.78f, 0.45f);
        luz.intensity = 2.2f;
        luz.range = 5f;
        luz.shadows = LightShadows.None;
        Destroy(go, TiempoFogonazo);
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
