using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static ShopUIKit;
using static PerfilUI;
using Cosmetico = PerfilDatos.Cosmetico;
using Img = UnityEngine.UI.Image;
using Rareza = PerfilDatos.Rareza;

// US 386 (F36): la tienda directa, en la pestaña Tienda del menú de la tienda (US 385). Sin azar: el cosmético que elijas.
// - CA1: 8 ofertas (avatares, tarjetas de presentación, marcos, títulos y skins de armas), al menos una épica o legendaria,
//   con su imagen, nombre, tipo, rareza y precio: 400 (común), 800 (raro), 1.600 (épico) o 3.200 cristales (legendario).
// - CA2: cambian todos los días a las 00:00 de Argentina y son las mismas para todos (la fecha es la semilla).
// - CA3: al elegir una se ve más grande, con "Comprar · N cristales", que pide confirmar. Lo comprado va a la colección.
// - CA4: si no alcanzan los cristales, el botón dice cuántos faltan y lleva a la pestaña Cristales; si ya lo tiene, "Obtenido".
// - CA5: el gasto va con el saldo de la cuenta (US 272) y el cosmético, con el Perfil a la nube.
public class PantallaTiendaDirecta : PantallaBase
{
    public static readonly int[] Precios = { 0, 400, 800, 1600, 3200 }; // por Rareza (Inicial no se vende)
    private const int Ofertas = 8, Columnas = 4;
    private const float GrillaX = 60f, GrillaY = 196f, AnchoGrilla = 1300f, CartaH = 360f, Gap = 20f;
    private const float PanelX = 1390f, PanelW = 470f;

    private RectTransform grilla, panel;
    private TextMeshProUGUI cuentaRegresiva;
    private List<Premio> ofertas;
    private int diaDeLasOfertas = -1;
    private Premio elegida;

    public PantallaTiendaDirecta(MenuRiftwalker menu, RectTransform parent) : base(menu, parent, "TiendaDirecta")
    {
        BarraDeTienda(PestanaTienda.Tienda, "Sin azar");
        T(rect, "Rotulo", 60f, 124f, 600f, 26f, FRotulo, 20f, Accent, "Ofertas del día", TextAlignmentOptions.MidlineLeft, 16f, true);
        T(rect, "Detalle", 60f, 150f, 900f, 30f, FTexto, 22f, Mute, "El cosmético que elijas, sin azar. Compras simuladas: no se cobra dinero real.");
        cuentaRegresiva = T(rect, "Cuenta", 1000f, 136f, 860f, 40f, FTitulo, 30f, Soft, "", TextAlignmentOptions.MidlineRight, 3f, true);
        grilla = Place(Node("Ofertas", rect), GrillaX, GrillaY, AnchoGrilla, 2f * CartaH + Gap);
        RectTransform marco = Place(Node("Detalle", rect), PanelX, GrillaY, PanelW, 2f * CartaH + Gap);
        Image(marco, Rounded, PanelColor, 10f);
        panel = Place(Node("Contenido", marco), 0f, 0f, PanelW, 2f * CartaH + Gap);
    }

    // ================= Ofertas del día (CA1 y CA2) =================

    private static DateTime HoyEnArgentina => DateTime.UtcNow.AddHours(-3);
    private static int Dia => HoyEnArgentina.Year * 10000 + HoyEnArgentina.Month * 100 + HoyEnArgentina.Day;

    public static List<Premio> OfertasDe(int dia)
    {
        var todos = new List<Premio>();
        foreach (Cosmetico c in PerfilDatos.Catalogo) if (c.rareza != Rareza.Inicial) todos.Add(Premio.De(c));
        foreach (CatalogoDeSkins.Skin s in CatalogoDeSkins.Skins) todos.Add(Premio.De(s));
        var rng = new System.Random(dia * 31 + 7);
        var lista = new List<Premio>();
        var usados = new HashSet<string>();
        void Tomar(Predicate<Premio> filtro)
        {
            List<Premio> posibles = todos.FindAll(p => !usados.Contains(p.Clave) && filtro(p));
            if (posibles.Count == 0) return;
            Premio p = posibles[rng.Next(posibles.Count)];
            usados.Add(p.Clave);
            lista.Add(p);
        }
        // Una épica o legendaria, una skin, una de cada tipo del Perfil y el resto de cualquier cosa.
        Tomar(p => p.Rareza >= Rareza.Epico);
        Tomar(p => p.skin != null);
        foreach (PerfilDatos.Tipo t in new[] { PerfilDatos.Tipo.Avatar, PerfilDatos.Tipo.Banner, PerfilDatos.Tipo.Marco, PerfilDatos.Tipo.Titulo })
            Tomar(p => p.cosmetico != null && p.cosmetico.tipo == t);
        while (lista.Count < Ofertas && usados.Count < todos.Count) Tomar(p => true);
        // Ordenadas de la más rara a la más común, como una vidriera.
        lista.Sort((a, b) => b.Rareza.CompareTo(a.Rareza));
        return lista;
    }

    public static int PrecioDe(Premio p) => Precios[(int)p.Rareza];

    // ================= Pantalla =================

    public override void Refrescar()
    {
        base.Refrescar();
        if (diaDeLasOfertas != Dia || ofertas == null)
        {
            diaDeLasOfertas = Dia;
            ofertas = OfertasDe(Dia);
            elegida = ofertas.Count > 0 ? ofertas[0] : null;
        }
        Dibujar();
    }

    public override void Tick()
    {
        // CA2: cuánto falta para las ofertas nuevas; a las 00:00 cambian solas.
        DateTime hoy = HoyEnArgentina;
        TimeSpan falta = hoy.Date.AddDays(1) - hoy;
        cuentaRegresiva.text = $"{L10n.Source("Nuevas ofertas en", "profile.")} {(int)falta.TotalHours} h {falta.Minutes:00} min";
        if (Dia != diaDeLasOfertas) Refrescar();
    }

    private void Dibujar()
    {
        BorrarHijos(grilla);
        float w = (AnchoGrilla - (Columnas - 1) * Gap) / Columnas;
        for (int i = 0; i < ofertas.Count; i++)
            Carta(ofertas[i], (i % Columnas) * (w + Gap), (i / Columnas) * (CartaH + Gap), w);
        DibujarPanel();
    }

    private void Carta(Premio p, float x, float y, float w)
    {
        bool tiene = p.Tiene, elegido = elegida == p;
        Color colorR = ColorRareza(p.Rareza);
        RectTransform carta = Place(Node(p.Nombre, grilla), x, y, w, CartaH);
        Image(carta, Rounded, PanelColor, 10f, true);
        const float artH = 200f; // igual en todas, así los nombres y los precios quedan alineados
        p.DibujarArte(carta, 0f, 0f, w, artH, tiene);
        Image(Place(Node("Rareza", carta), 0f, artH, w, 4f), null, colorR);
        TextMeshProUGUI n = T(carta, "Nombre", 18f, artH + 14f, w - 36f, 34f, FTitulo, 28f, Ink, p.Nombre, TextAlignmentOptions.MidlineLeft, 2f);
        n.overflowMode = TextOverflowModes.Ellipsis;
        T(carta, "Tipo", 18f, artH + 50f, w - 36f, 22f, FRotulo, 16f, colorR, $"{p.NombreTipo} · {NombreRareza(p.Rareza)}", TextAlignmentOptions.MidlineLeft, 10f, true);
        if (tiene)
            T(carta, "Obtenido", 18f, CartaH - 58f, w - 36f, 40f, FTitulo, 26f, Ok, "Obtenido", TextAlignmentOptions.MidlineLeft, 4f, true);
        else
        {
            IconoMoneda(carta, 2, 32f, CartaH - 38f, 18f);
            T(carta, "Precio", 52f, CartaH - 58f, w - 70f, 40f, FTitulo, 32f, Ink, PerfilDatos.Numero(PrecioDe(p)), TextAlignmentOptions.MidlineLeft, 2f);
        }
        Img borde = Borde(carta, 8f, elegido ? Accent : White(0.08f));
        var c = Clickeable(carta, () => { elegida = p; Dibujar(); });
        c.Hovered = () => { if (borde != null && elegida != p) borde.color = White(0.35f); };
        c.Exited = () => { if (borde != null && elegida != p) borde.color = White(0.08f); };
    }

    // CA3 y CA4: la oferta elegida, más grande, con su botón.
    private void DibujarPanel()
    {
        BorrarHijos(panel);
        if (elegida == null) return;
        Premio p = elegida;
        float h = panel.sizeDelta.y;
        Borde(panel, 8f, WithAlpha(ColorRareza(p.Rareza), 0.6f));
        float artH = p.cosmetico != null && p.cosmetico.tipo == PerfilDatos.Tipo.Avatar ? PanelW - 48f : 230f;
        p.DibujarArte(panel, 24f, 24f, PanelW - 48f, artH);
        float y = 24f + artH + 18f;
        T(panel, "Tipo", 24f, y, PanelW - 48f, 22f, FRotulo, 17f, ColorRareza(p.Rareza), $"{p.NombreTipo} · {NombreRareza(p.Rareza)}", TextAlignmentOptions.MidlineLeft, 12f, true);
        TextMeshProUGUI n = T(panel, "Nombre", 24f, y + 24f, PanelW - 48f, 84f, FTitulo, 38f, Ink, p.Nombre, TextAlignmentOptions.TopLeft, 2f, true);
        n.textWrappingMode = TextWrappingModes.Normal;

        int precio = PrecioDe(p), faltan = precio - PerfilDatos.Cristales;
        string texto = p.Tiene ? "Obtenido" : faltan > 0 ? $"Te faltan {PerfilDatos.Numero(faltan)} cristales" : $"Comprar · {PerfilDatos.Numero(precio)} cristales";
        Boton b = CrearBoton(panel, 24f, h - 24f - 64f, PanelW - 48f, 64f, texto, !p.Tiene && faltan <= 0, () => Comprar(p), 26f,
            faltan > 0 && !p.Tiene ? ColorCristales : (Color?)null);
        b.texto.text = texto;
        if (p.Tiene) b.Habilitar(false);
        if (!p.Tiene && faltan > 0)
            T(panel, "Ayuda", 24f, h - 24f - 64f - 30f, PanelW - 48f, 24f, FTexto, 18f, Mute, "Te llevamos a la pestaña Cristales.", TextAlignmentOptions.MidlineLeft);
    }

    private void Comprar(Premio p)
    {
        if (p.Tiene) return;
        int precio = PrecioDe(p);
        if (PerfilDatos.Cristales < precio) { menu.AbrirTienda(PestanaTienda.Cristales); return; } // CA4
        menu.Confirmar("Tienda directa", $"¿Comprar {p.Nombre}?",
            $"{p.NombreTipo} {NombreRareza(p.Rareza).ToLowerInvariant()} por {PerfilDatos.Numero(precio)} cristales. Compra simulada: no se cobra dinero real.",
            "Comprar", () => PerfilDatos.PagarCristales("tienda:" + p.Clave, precio, error =>
            {
                if (error != null) { menu.Avisar(error); return; }
                p.Dar("tienda");
                menu.Avisar($"¡{p.Nombre}! Ya está en tu colección del Perfil.");
                Refrescar();
            }));
    }
}
