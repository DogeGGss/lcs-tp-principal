using UnityEngine;

// US 019 · Trasbordo (CA8). La luz, el aro que se abre en el piso y la columna que se apaga. Dura menos de medio segundo y se borra sola.
public class DestelloTrasbordo : MonoBehaviour
{
    private const float Duracion = 0.45f;
    private Light luz;
    private LineRenderer aro, columna;
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
            float radio = Mathf.Lerp(0.35f, 1.1f, 1f - apagado * apagado);
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
    }
}
