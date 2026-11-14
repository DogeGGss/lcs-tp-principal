using UnityEngine;

// F35 (Skins de armas), US 381: el humo que larga la boca de un arma con skin legendaria después de disparar: cada
// disparo junta un poco y, cuando se deja de disparar, sale una columna fina de humo negro azulado (con alguna brasa)
// que se va apagando. Después de una ráfaga sale más y dura cerca de un segundo y medio; con un tiro suelto casi no se
// nota. Va en el arma (la propia en la mano o la de la mano de los demás) y se agrega sola con el primer disparo.
public class HumoDeSkin : MonoBehaviour
{
    private const float PorDisparo = 0.2f;   // cuánto humo junta cada tiro (de 0 a 1)
    private const float Dura = 1.6f;         // s: lo que tarda en irse todo el humo
    private const float Espera = 0.12f;      // s: mientras se dispara no sale (lo tapa el fuego)
    private const float BocanadasPorSegundo = 30f, BrasasPorBocanada = 0.12f;

    private float cuanto, ultimo, pendientes;
    private CatalogoDeSkins.Efectos efectos;
    private bool propio;
    private ShopItem ficha;
    private Vector3 bocaLocal; // la boca en el modelo (los demás)
    private float escala = 1f;

    /// <summary>Disparo propio: el humo sale de la boca tal como se ve en la pantalla (EfectosDeSkin.BocaPropia).</summary>
    public static void DisparoPropio(GameObject arma, ShopItem ficha, CatalogoDeSkins.Efectos e)
    {
        HumoDeSkin h = De(arma, e);
        if (h == null) return;
        h.propio = true;
        h.ficha = ficha;
        h.escala = 1f;
    }

    /// <summary>Disparo de otro jugador: el humo sale de ese punto del modelo de su arma (y lo sigue).</summary>
    public static void DisparoDeOtro(GameObject modelo, Vector3 boca, CatalogoDeSkins.Efectos e)
    {
        HumoDeSkin h = De(modelo, e);
        if (h == null) return;
        h.propio = false;
        h.bocaLocal = modelo.transform.InverseTransformPoint(boca);
        h.escala = 3.5f;
    }

    private static HumoDeSkin De(GameObject arma, CatalogoDeSkins.Efectos e)
    {
        if (arma == null || e == null) return null;
        HumoDeSkin h = arma.GetComponent<HumoDeSkin>();
        if (h == null) h = arma.AddComponent<HumoDeSkin>();
        h.efectos = e;
        h.cuanto = Mathf.Min(1f, h.cuanto + PorDisparo);
        h.ultimo = Time.time;
        return h;
    }

    private void OnDisable() { cuanto = 0f; pendientes = 0f; } // guardó el arma: se corta el humo

    private void Update()
    {
        if (cuanto <= 0f || Time.time - ultimo < Espera) return;
        cuanto = Mathf.Max(0f, cuanto - Time.deltaTime / Dura);
        pendientes += BocanadasPorSegundo * cuanto * Time.deltaTime;
        if (pendientes < 1f) return;
        Vector3 boca;
        if (propio) { if (!EfectosDeSkin.BocaPropia(gameObject, ficha, out boca, out _)) { pendientes = 0f; return; } }
        else boca = transform.TransformPoint(bocaLocal);
        for (; pendientes >= 1f; pendientes -= 1f)
        {
            EfectosDeSkin.Bocanada(boca, propio, escala, cuanto, efectos, false);
            if (Random.value < BrasasPorBocanada * cuanto * 2f) EfectosDeSkin.Bocanada(boca, propio, escala, cuanto, efectos, true);
        }
    }
}
