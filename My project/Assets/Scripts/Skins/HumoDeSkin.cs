using UnityEngine;

// F35 (Skins de armas), US 381: el humo que larga la boca de un arma con skin legendaria después de disparar: cada
// disparo junta un poco y, cuando se deja de disparar, sale una columna de humo gris azulado (con brasas)
// que se va apagando. Después de una ráfaga sale más y dura cerca de dos segundos; con un tiro suelto casi no se
// nota. Con una skin con arcos (la Tormenta), en vez de humo (o además) saltan arquitos eléctricos en la boca, con la
// misma regla. Al recargar o guardar el arma propia se corta (y se borra el que había: el arma se mueve y el humo quedaría
// flotando solo). Va en el arma (la propia en la mano o la de la mano de los demás) y se agrega sola con el primer disparo.
public class HumoDeSkin : MonoBehaviour
{
    private const float PorDisparo = 0.25f;   // cuánto humo junta cada tiro (de 0 a 1)
    private const float Dura = 2.2f;         // s: lo que tarda en irse todo el humo
    private const float Espera = 0.12f;      // s: mientras se dispara no sale (lo tapa el fuego)
    private const float BocanadasPorSegundo = 40f, BrasasPorBocanada = 0.3f, ArcosPorBocanada = 0.2f;

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

    private void OnDisable() => Cortar(); // guardó el arma

    private void Cortar()
    {
        bool habia = cuanto > 0f;
        cuanto = 0f;
        pendientes = 0f;
        if (habia && propio) EfectosDeSkin.BorrarHumoPropio();
    }

    private IHudWeapon municion;

    private void Update()
    {
        if (cuanto <= 0f) return;
        if (propio)
        {
            municion ??= GetComponentInParent<IHudWeapon>();
            if (municion != null && municion.ReloadProgress >= 0f) { Cortar(); return; } // recargando: el arma baja
        }
        if (Time.time - ultimo < Espera) return;
        cuanto = Mathf.Max(0f, cuanto - Time.deltaTime / Dura);
        pendientes += BocanadasPorSegundo * cuanto * Time.deltaTime;
        if (pendientes < 1f) return;
        Vector3 boca;
        if (propio) { if (!EfectosDeSkin.BocaPropia(gameObject, ficha, out boca, out _)) { pendientes = 0f; return; } }
        else boca = transform.TransformPoint(bocaLocal);
        for (; pendientes >= 1f; pendientes -= 1f)
        {
            if (efectos.humo)
            {
                EfectosDeSkin.Bocanada(boca, propio, escala, cuanto, efectos, false);
                if (Random.value < BrasasPorBocanada * cuanto * 2f) EfectosDeSkin.Bocanada(boca, propio, escala, cuanto, efectos, true);
            }
            // Los arquitos, más espaciados que las bocanadas (unos 8 por segundo con todo el calor).
            if (efectos.arcos && Random.value < ArcosPorBocanada)
            {
                Camera cam = Camera.main;
                Vector3 adelante = propio ? (cam != null ? cam.transform.forward : transform.forward) : transform.forward;
                EfectosDeSkin.Arcos(boca, adelante, 0.13f * escala, efectos, 1);
            }
        }
    }
}
