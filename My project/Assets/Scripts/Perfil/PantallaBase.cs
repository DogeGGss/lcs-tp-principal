using UnityEngine;
using static ShopUIKit;
using static PerfilUI;

// Pantalla completa del menú principal (Perfil, Estación del Rift, Tienda, Logros).
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
        float a = MenuRiftwalker.Afuera; // también tapa el fondo del menú fuera del 16:9
        Image(Place(Node("Fondo", rect), -a, -a, 1920f + 2f * a, 1080f + 2f * a), null, Hex(0x07090D), 0f, true);
    }

    protected void BarraSuperior(string rotulo, string titulo)
    {
        Cabecera(rect, rotulo, titulo, menu.VolverAlMenu);
        monedas = new Monedas(rect, 1860f, 34f, () => menu.Abrir(menu.Estacion), () => menu.Abrir(menu.Tienda));
    }

    /// <summary>Se llama al abrir la pantalla (y cuando cambian los datos).</summary>
    public virtual void Refrescar() => monedas?.Refrescar();

    /// <summary>Cada cuadro mientras está abierta.</summary>
    public virtual void Tick() { }

    /// <summary>Esc: true si lo usó la pantalla (por ejemplo, para saltar una animación).</summary>
    public virtual bool Escape() => false;
}
