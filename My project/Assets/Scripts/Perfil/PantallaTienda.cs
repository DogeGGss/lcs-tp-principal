using TMPro;
using UnityEngine;
using static ShopUIKit;
using static PerfilUI;
using Img = UnityEngine.UI.Image;
using Pack = PerfilDatos.Pack;

// PRUEBA DEL MENÚ: tienda de cristales (la moneda "de pago"). Es una SIMULACIÓN de negocio para el TP:
// "comprar" pide confirmación, suma los cristales y anota la compra, pero no se cobra nada.
public class PantallaTienda : PantallaBase
{
    private Boton botonOferta;
    private TextMeshProUGUI precioOferta;
    private readonly Boton[] botonesPack = new Boton[4];
    private RectTransform historial;
    private TextMeshProUGUI total, cuenta;

    public PantallaTienda(MenuRiftwalker menu, RectTransform parent) : base(menu, parent, "Tienda")
    {
        BarraSuperior("Simulación de negocio", "Tienda");

        RectTransform aviso = Place(Node("AvisoSimulacion", rect), 60f, 120f, 1800f, 62f);
        Image(aviso, Rounded, WithAlpha(Accent, 0.1f), 8f);
        Borde(aviso, 8f, WithAlpha(Accent, 0.5f));
        T(aviso, "Texto", 28f, 0f, 1744f, 62f, FTexto, 22f, Soft,
            "<color=#F29A38><b>Compras simuladas.</b></color>  Es un proyecto universitario: no se cobra dinero real. Se prueba cómo funcionaría la tienda de un juego gratuito.");

        ArmarOferta();
        for (int i = 1; i < PerfilDatos.Packs.Count; i++) ArmarPack(PerfilDatos.Packs[i], i - 1);
        ArmarResumen();
    }

    private void ArmarOferta()
    {
        Pack p = PerfilDatos.Packs[0];
        RectTransform carta = Place(Node("Oferta", rect), 60f, 204f, 620f, 560f);
        Image(carta, Rounded, PanelColor, 10f);
        Cubrir(Place(Node("Arte", carta), 0f, 0f, 620f, 560f), PerfilDatos.Fondo("fondo-02"), 0.8f);
        Image(Place(Node("Sombra", carta), 0f, 160f, 620f, 400f), Degrade(false, 0.95f, 0f), Color.white);
        Borde(carta, 8f, WithAlpha(Accent, 0.7f));
        RectTransform chip = Place(Node("Descuento", carta), 24f, 24f, 120f, 34f);
        Image(chip, Rounded, Accent, 6f);
        T(chip, "T", 0f, 0f, 120f, 34f, FRotulo, 18f, AccentInk, "-40 %", TextAlignmentOptions.Center, 10f, true);
        T(carta, "Rotulo", 28f, 300f, 560f, 24f, FRotulo, 19f, Accent, p.nombre, TextAlignmentOptions.MidlineLeft, 16f, true);
        T(carta, "Detalle", 26f, 328f, 570f, 56f, FTitulo, 44f, Ink, p.detalle, TextAlignmentOptions.MidlineLeft, 2f, true);
        T(carta, "UnaVez", 28f, 390f, 560f, 24f, FRotulo, 17f, Mute, "Una sola vez por cuenta", TextAlignmentOptions.MidlineLeft, 14f, true);
        T(carta, "Antes", 28f, 430f, 200f, 30f, FTexto, 22f, Mute, "<s>$ 3.300</s>");
        precioOferta = T(carta, "Precio", 28f, 456f, 260f, 70f, FTitulo, 64f, Ink, PerfilDatos.Pesos(p.precio));
        botonOferta = CrearBoton(carta, 620f - 28f - 250f, 470f, 250f, 64f, "Comprar", true, () => Comprar(p), 28f);
    }

    private void ArmarPack(Pack p, int i)
    {
        float w = 275f, x = 700f + i * (w + 20f);
        RectTransform carta = Place(Node(p.nombre, rect), x, 204f, w, 560f);
        Image(carta, Rounded, PanelColor, 10f);
        Img borde = Borde(carta, 8f, White(0.08f));
        UIRing halo = Circulo(carta, w / 2f, 140f, 120f + i * 14f, WithAlpha(ColorCristales, 0.22f + i * 0.06f));
        halo.glow = true;
        // Más cristales cuanto más grande es el paquete.
        int cuantos = i + 1;
        for (int k = 0; k < cuantos; k++)
        {
            float off = (k - (cuantos - 1) / 2f) * 44f;
            IconoMoneda(carta, 2, w / 2f + off, 140f + Mathf.Abs(off) * 0.4f, 86f - cuantos * 6f);
        }
        T(carta, "Cantidad", 0f, 262f, w, 80f, FTitulo, 76f, Ink, PerfilDatos.Numero(p.cristales), TextAlignmentOptions.Center, 2f);
        T(carta, "Cristales", 0f, 338f, w, 24f, FRotulo, 18f, Mute, "cristales", TextAlignmentOptions.Center, 12f, true);
        if (p.bonus > 0) T(carta, "Bonus", 0f, 366f, w, 26f, FRotulo, 20f, Accent, $"+{PerfilDatos.Numero(p.bonus)} de regalo", TextAlignmentOptions.Center, 10f, true);
        T(carta, "Nombre", 0f, 404f, w, 26f, FTexto, 20f, Soft, p.nombre, TextAlignmentOptions.Center);
        T(carta, "Precio", 0f, 432f, w, 52f, FTitulo, 44f, Ink, PerfilDatos.Pesos(p.precio), TextAlignmentOptions.Center, 2f);
        botonesPack[i] = CrearBoton(carta, 20f, 494f, w - 40f, 50f, "Comprar", false, () => Comprar(p), 24f, ColorCristales);
        string sello = p.id == "c400" ? "Más elegido" : p.id == "c2500" ? "Mejor valor" : null;
        if (sello != null)
        {
            RectTransform s = Place(Node("Sello", carta), w / 2f - 70f, -14f, 140f, 30f);
            Image(s, Rounded, ColorCristales, 6f);
            T(s, "T", 0f, 0f, 140f, 30f, FRotulo, 16f, AccentInk, sello, TextAlignmentOptions.Center, 12f, true);
            if (borde != null) borde.color = WithAlpha(ColorCristales, 0.6f);
        }
    }

    private void ArmarResumen()
    {
        RectTransform h = Place(Node("Historial", rect), 60f, 790f, 1180f, 250f);
        Image(h, Rounded, PanelColor, 8f);
        Borde(h, 8f, White(0.06f));
        T(h, "Rotulo", 24f, 18f, 600f, 22f, FRotulo, 17f, Accent, "Compras simuladas", TextAlignmentOptions.MidlineLeft, 16f, true);
        historial = Place(Node("Lista", h), 24f, 54f, 1132f, 186f);

        RectTransform r = Place(Node("Resumen", rect), 1260f, 790f, 600f, 250f);
        Image(r, Rounded, PanelColor, 8f);
        Borde(r, 8f, White(0.06f));
        T(r, "Rotulo", 24f, 18f, 560f, 22f, FRotulo, 17f, Accent, "Total gastado (simulado)", TextAlignmentOptions.MidlineLeft, 16f, true);
        total = T(r, "Total", 22f, 46f, 560f, 70f, FTitulo, 66f, Ink, "");
        cuenta = T(r, "Cuenta", 24f, 118f, 560f, 28f, FTexto, 20f, Mute, "");
        CrearBoton(r, 24f, 170f, 552f, 56f, "Reiniciar datos de prueba", false, () =>
            menu.Confirmar("Prueba del menú", "Reiniciar datos",
                "Vuelve todo a como arranca la prueba: monedas, colección, equipado, compras y cápsulas abiertas.",
                "Reiniciar", () => { PerfilDatos.Reiniciar(); menu.Avisar("Datos de prueba reiniciados"); }), 22f);
    }

    private void Comprar(Pack p)
    {
        if (!PerfilDatos.PackDisponible(p)) return;
        string recibe = p.boletos > 0 ? p.detalle : $"{PerfilDatos.Numero(p.cristales)} cristales" + (p.bonus > 0 ? $" + {PerfilDatos.Numero(p.bonus)} de regalo" : "");
        menu.Confirmar("Compra simulada", p.nombre,
            $"Recibís <color=#F3F4F6>{recibe}</color> por <color=#F3F4F6>{PerfilDatos.Pesos(p.precio)}</color>.\n\n" +
            "No se cobra nada: es una simulación del negocio para el TP.",
            "Comprar (simulado)", () =>
            {
                if (PerfilDatos.ComprarSimulado(p))
                    menu.Avisar(p.boletos > 0 ? $"+{p.boletos} boletos y +{p.cristales} cristales" : $"+{PerfilDatos.Numero(p.cristales + p.bonus)} cristales");
            });
    }

    public override void Refrescar()
    {
        base.Refrescar();
        bool oferta = PerfilDatos.PackDisponible(PerfilDatos.Packs[0]);
        botonOferta.Habilitar(oferta);
        botonOferta.texto.text = oferta ? "Comprar" : "Ya la compraste";
        precioOferta.color = oferta ? Ink : Mute;

        BorrarHijos(historial);
        int n = 0;
        foreach (var (pack, precio, fecha) in PerfilDatos.Compras)
        {
            if (n < 5)
            {
                float y = n * 36f;
                T(historial, "Fecha", 0f, y, 140f, 30f, FMono, 18f, Mute, fecha);
                T(historial, "Pack", 150f, y, 700f, 30f, FTexto, 21f, Ink, pack);
                T(historial, "Precio", 900f, y, 232f, 30f, FTitulo, 26f, Soft, PerfilDatos.Pesos(precio), TextAlignmentOptions.MidlineRight);
            }
            n++;
        }
        if (n == 0) T(historial, "Vacio", 0f, 0f, 1100f, 30f, FTexto, 21f, Mute, "Todavía no hiciste ninguna compra simulada.");
        total.text = PerfilDatos.Pesos(PerfilDatos.Gastado);
        cuenta.text = n == 1 ? "1 compra" : $"{n} compras";
    }
}
