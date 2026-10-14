using TMPro;
using UnityEngine;
using static ShopUIKit;
using static PerfilUI;

// PRUEBA DEL MENÚ: pantalla completa (Perfil, Estación del Rift, Tienda) sobre el menú principal.
// Arriba tiene "« Menú", el rótulo, el título y las monedas. MenuRiftwalker la abre con un fundido.
public abstract class PantallaBase
{
    public RectTransform rect;
    public CanvasGroup grupo;
    protected MenuRiftwalker menu;
    protected Monedas monedas;

    protected PantallaBase(MenuRiftwalker menu, RectTransform parent, string nombre)
    {
        this.menu = menu;
        rect = Place(Node(nombre, parent), 0f, 0f, 1920f, 1080f);
        grupo = rect.gameObject.AddComponent<CanvasGroup>();
        Image(Stretch(Node("Fondo", rect)), null, Hex(0x07090D), 0f, true);
    }

    protected void BarraSuperior(string rotulo, string titulo)
    {
        CrearBoton(rect, 60f, 34f, 150f, 52f, "« Menú", false, menu.VolverAlMenu, 24f);
        T(rect, "Rotulo", 240f, 32f, 700f, 22f, FRotulo, 18f, Accent, rotulo, TextAlignmentOptions.MidlineLeft, 16f, true);
        T(rect, "Titulo", 238f, 50f, 900f, 50f, FTitulo, 46f, Ink, titulo, TextAlignmentOptions.MidlineLeft, 4f, true);
        monedas = new Monedas(rect, 1860f, 34f, () => menu.Abrir(menu.Estacion), () => menu.Abrir(menu.Tienda));
    }

    /// <summary>Se llama al abrir la pantalla (y cuando cambian los datos).</summary>
    public virtual void Refrescar() => monedas?.Refrescar();

    /// <summary>Cada cuadro mientras está abierta.</summary>
    public virtual void Tick() { }

    /// <summary>Esc: true si lo usó la pantalla (por ejemplo, para saltar una animación).</summary>
    public virtual bool Escape() => false;
}
