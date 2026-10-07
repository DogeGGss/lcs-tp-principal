using UnityEngine;
using UnityEngine.UI;

// US 019 · Trasbordo. CA8: un destello corto en la pantalla del que hizo el Trasbordo.
public class PantallaTrasbordo : MonoBehaviour
{
    private const float Duracion = 0.25f;
    private Image imagen;
    private float desde;

    public void Iniciar(Image imagen)
    {
        this.imagen = imagen;
        desde = Time.unscaledTime;
        Update();
    }

    private void Update()
    {
        float k = (Time.unscaledTime - desde) / Duracion;
        if (k >= 1f) { Destroy(gameObject); return; }
        Color color = EfectosTrasbordo.Tono;
        color.a = 0.32f * (1f - k) * (1f - k);
        imagen.color = color;
    }
}
