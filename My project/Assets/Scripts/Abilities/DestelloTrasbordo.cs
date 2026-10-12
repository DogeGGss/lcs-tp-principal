using UnityEngine;

// US 019 · Trasbordo (CA8). La luz, el aro que se abre en el piso y la columna que se apaga. Dura menos de un segundo y se borra sola.
// Lo que se ve no depende de la luz: el aro y la columna se dibujan sin iluminación, así se notan aunque el mapa ya tenga
// muchas luces cerca (URP descarta las luces que pasan del límite por objeto) y aunque se mire desde lejos.
public class DestelloTrasbordo : MonoBehaviour
{
    private const float Duracion = 0.7f;
    private const float Alto = 2.3f;
    private Light luz;
    private LineRenderer aro, columna, nucleo;
    private float fuerza = 1f, desde;

    public void Iniciar(Material material, float fuerza)
    {
        this.fuerza = fuerza;
        desde = Time.time;
        luz = gameObject.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = EfectosTrasbordo.Tono;
        luz.range = 7f;
        luz.shadows = LightShadows.None;
        luz.renderMode = LightRenderMode.ForcePixel; // "Importante": no la descarta por el límite de luces por objeto
        if (material != null)
        {
            aro = Linea("Aro", material, 0.12f);
            aro.loop = true;
            aro.positionCount = 40;
            columna = Linea("Columna", material, 0.8f);
            nucleo = Linea("Núcleo", material, 0.25f);
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
        linea.positionCount = 2;
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
        // Se mantiene fuerte la primera mitad y después se apaga rápido, en vez de irse apagando desde el primer cuadro.
        float queda = 1f - k * k;
        Vector3 pies = transform.position;
        luz.intensity = 6f * fuerza * queda;
        luz.transform.position = pies + Vector3.up * 1f;
        Color color = EfectosTrasbordo.Tono;
        if (aro != null)
        {
            float radio = Mathf.Lerp(0.35f, 1.4f, Mathf.Sqrt(k));
            for (int i = 0; i < aro.positionCount; i++)
            {
                float a = i * Mathf.PI * 2f / aro.positionCount;
                aro.SetPosition(i, pies + new Vector3(Mathf.Cos(a) * radio, 0.08f, Mathf.Sin(a) * radio));
            }
            color.a = Mathf.Clamp01(fuerza * queda);
            aro.startColor = aro.endColor = color;
        }
        if (columna != null)
        {
            // Se angosta mientras se apaga; la de adentro es casi blanca para que se distinga del fondo.
            columna.SetPosition(0, pies + Vector3.up * 0.05f);
            columna.SetPosition(1, pies + Vector3.up * Alto);
            columna.widthMultiplier = 0.8f * Mathf.Lerp(0.4f, 1f, queda);
            Color abajo = color, arriba = color;
            abajo.a = 0.75f * fuerza * queda;
            arriba.a = 0f;
            columna.startColor = abajo;
            columna.endColor = arriba;

            nucleo.SetPosition(0, pies + Vector3.up * 0.05f);
            nucleo.SetPosition(1, pies + Vector3.up * Alto * 0.85f);
            nucleo.widthMultiplier = 0.25f * queda;
            Color claro = Color.Lerp(color, Color.white, 0.6f);
            Color claroArriba = claro;
            claro.a = Mathf.Clamp01(fuerza * queda);
            claroArriba.a = 0f;
            nucleo.startColor = claro;
            nucleo.endColor = claroArriba;
        }
    }
}
