using UnityEngine;

// US 019 · Trasbordo (CA8). La luz, el aro que se abre en el piso, la columna y unos rayos alrededor que se apagan. Dura menos de un
// segundo y se borra sola. El aro y los rayos van por fuera de la cápsula del jugador (radio 0,5 m): en la llegada el destello
// aparece justo donde está el personaje, y si fueran por dentro los demás no lo verían, el cuerpo lo tapa.
public class DestelloTrasbordo : MonoBehaviour
{
    private const float Duracion = 0.7f;
    private const int CantidadRayos = 8;
    private const float RadioInicial = 0.65f, RadioFinal = 1.4f;
    private Light luz;
    private LineRenderer aro, columna;
    private LineRenderer[] rayos;
    private float fuerza = 1f, desde;

    public void Iniciar(Material material, float fuerza)
    {
        this.fuerza = fuerza;
        desde = Time.time;
        luz = gameObject.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = EfectosTrasbordo.Tono;
        luz.range = 5f;
        luz.shadows = LightShadows.None;
        luz.transform.localPosition = Vector3.zero;
        if (material != null)
        {
            aro = Linea("Aro", material, 0.06f);
            aro.loop = true;
            aro.positionCount = 32;
            columna = Linea("Columna", material, 0.5f);
            columna.positionCount = 2;
            columna.SetPosition(0, transform.position + Vector3.up * 0.05f);
            columna.SetPosition(1, transform.position + Vector3.up * 2.1f);
            rayos = new LineRenderer[CantidadRayos];
            for (int i = 0; i < CantidadRayos; i++)
            {
                rayos[i] = Linea("Rayo", material, 0.1f);
                rayos[i].positionCount = 2;
            }
        }
        Actualizar(0f);
    }

    private LineRenderer Linea(string nombre, Material material, float ancho)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        LineRenderer linea = go.AddComponent<LineRenderer>();
        linea.sharedMaterial = material;
        linea.useWorldSpace = true;
        linea.widthMultiplier = ancho;
        linea.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        linea.receiveShadows = false;
        linea.numCapVertices = 0;
        return linea;
    }

    private void Update()
    {
        float k = (Time.time - desde) / Duracion;
        if (k >= 1f) { Destroy(gameObject); return; }
        Actualizar(k);
    }

    private void Actualizar(float k)
    {
        float apagado = 1f - k;
        luz.intensity = 4f * fuerza * apagado * apagado;
        luz.transform.position = transform.position + Vector3.up * 1f;
        Color color = EfectosTrasbordo.Tono;
        if (aro != null)
        {
            float radio = Mathf.Lerp(RadioInicial, RadioFinal, 1f - apagado * apagado);
            for (int i = 0; i < aro.positionCount; i++)
            {
                float a = i * Mathf.PI * 2f / aro.positionCount;
                aro.SetPosition(i, transform.position + new Vector3(Mathf.Cos(a) * radio, 0.05f, Mathf.Sin(a) * radio));
            }
            color.a = 0.9f * fuerza * apagado;
            aro.startColor = aro.endColor = color;
        }
        if (columna != null)
        {
            columna.widthMultiplier = 0.55f * apagado;
            Color abajo = color, arriba = color;
            abajo.a = 0.55f * fuerza * apagado;
            arriba.a = 0f;
            columna.startColor = abajo;
            columna.endColor = arriba;
        }
        if (rayos != null)
        {
            // Suben desde el piso alrededor del personaje, un poco más cerca que el aro.
            float radio = Mathf.Lerp(RadioInicial, RadioFinal * 0.8f, k);
            float alto = Mathf.Lerp(0.6f, 2.4f, 1f - apagado * apagado);
            Color abajo = color, arriba = color;
            abajo.a = 0.85f * fuerza * apagado;
            arriba.a = 0f;
            for (int i = 0; i < rayos.Length; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / rayos.Length;
                Vector3 pie = transform.position + new Vector3(Mathf.Cos(a) * radio, 0.05f, Mathf.Sin(a) * radio);
                rayos[i].SetPosition(0, pie);
                rayos[i].SetPosition(1, pie + Vector3.up * alto);
                rayos[i].startColor = abajo;
                rayos[i].endColor = arriba;
            }
        }
    }
}
