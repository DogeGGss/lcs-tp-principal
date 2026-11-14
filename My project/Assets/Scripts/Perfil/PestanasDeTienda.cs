using System;
using TMPro;
using UnityEngine;
using static ShopUIKit;
using static PerfilUI;
using Img = UnityEngine.UI.Image;

// US 385 (F36): las cuatro pestañas del menú de la tienda (Tienda, Cápsulas, Pase de batalla y Cristales). Cada pestaña es
// una pantalla del menú con la misma barra arriba: al cambiar de pestaña se pasa a la otra pantalla sin fundido, así se ve
// como una sola pantalla. Van entre el título "Tienda" y las monedas.
public enum PestanaTienda { Tienda, Capsulas, Pase, Cristales }

public class PestanasDeTienda
{
    public static readonly string[] Nombres = { "Tienda", "Cápsulas", "Pase de batalla", "Cristales" };
    private const float X0 = 450f, Y = 40f, W = 180f, H = 52f, Gap = 6f;

    /// <summary>
    /// Cuántas recompensas del pase hay para reclamar (US 363, CA5). Lo asigna el pase de batalla (F32); mientras no
    /// exista, no hay avisos.
    /// </summary>
    public static Func<int> RecompensasDelPase = () => 0;

    private readonly RectTransform avisoPase;

    public PestanasDeTienda(RectTransform parent, MenuRiftwalker menu, PestanaTienda activa)
    {
        for (int i = 0; i < Nombres.Length; i++)
        {
            var pestana = (PestanaTienda)i;
            bool esta = pestana == activa;
            RectTransform r = Place(Node("Pestaña " + Nombres[i], parent), X0 + i * (W + Gap), Y, W, H);
            Img fondo = Image(r, Rounded, esta ? WithAlpha(Accent, 0.16f) : PanelColor, 6f, true);
            Img borde = Borde(r, 6f, esta ? WithAlpha(Accent, 0.7f) : White(0.08f));
            if (esta) Image(Place(Node("Linea", r), 10f, H - 3f, W - 20f, 3f), null, Accent);
            TextMeshProUGUI t = T(r, "Texto", 0f, 0f, W, H, FTitulo, 24f, esta ? Ink : Soft, Nombres[i], TextAlignmentOptions.Center, 4f, true);
            if (pestana == PestanaTienda.Pase)
            {
                avisoPase = Place(Node("Aviso", r), W - 22f, -6f, 16f, 16f);
                Image(avisoPase, Rounded, Accent, 8f);
            }
            if (esta) continue;
            var p = Clickeable(r, () => menu.AbrirTienda(pestana));
            p.Hovered = () => { t.color = Accent; if (borde != null) borde.color = WithAlpha(Accent, 0.6f); };
            p.Exited = () => { t.color = Soft; if (borde != null) borde.color = White(0.08f); };
        }
        Refrescar();
    }

    public void Refrescar()
    {
        if (avisoPase != null) avisoPase.gameObject.SetActive(RecompensasDelPase() > 0);
    }
}
