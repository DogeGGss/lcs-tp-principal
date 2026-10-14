using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static ShopUIKit;
using static PerfilUI;
using Img = UnityEngine.UI.Image;
using Tipo = PerfilDatos.Tipo;
using Rareza = PerfilDatos.Rareza;
using Cosmetico = PerfilDatos.Cosmetico;

// PRUEBA DEL MENÚ: Estación del Rift. Con un boleto se abre una cápsula que trae un avatar, un banner, un título o un marco.
// La cápsula tiembla, toma el color de la rareza (si es épica o legendaria primero amaga con azul), se abre la grieta,
// hay un destello y aparece el premio. Probabilidades, garantía y canjes son de ejemplo (PerfilDatos).
public class PantallaEstacion : PantallaBase
{
    private enum Estado { Espera, Abriendo, Revelado }

    private const float W = 1180f, H = 690f, CX = 590f, CY = 320f;
    private const float Revelar = 1.25f, Fin = 1.7f;

    private RectTransform escena, capsula, premio, premios, sombra;
    private Img luz, destello, tapa, base_;
    private UIRing brillo;
    private RiftwalkerRiftGraphic grieta;
    private readonly List<RectTransform> chispas = new List<RectTransform>();
    private readonly List<Vector3> chispaDatos = new List<Vector3>(); // x, velocidad, fase
    private TextMeshProUGUI estadoTexto, numeroTexto, leyenda, garantiaTexto;
    private Boton abrir1, abrir5, saltar, equipar;
    private RectTransform barraGarantia, listaRecientes;
    private readonly List<(Boton boton, System.Func<bool> puede)> canjes = new List<(Boton, System.Func<bool>)>();

    private Estado estado;
    private float t;
    private List<PerfilDatos.Resultado> resultados;
    private Color colorFinal = Accent, colorAmague;
    private bool amague;

    public PantallaEstacion(MenuRiftwalker menu, RectTransform parent) : base(menu, parent, "Estacion")
    {
        BarraSuperior("Suministros del otro lado", "Estación del Rift");
        ArmarEscena();
        ArmarAcciones();
        ArmarColumna();
        Reiniciar();
    }

    // ================= Escena =================

    private void ArmarEscena()
    {
        escena = Place(Node("Escena", rect), 60f, 120f, W, H);
        Image(escena, Rounded, Hex(0x0B0E14), 10f);
        escena.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        Image(Place(Node("Cielo", escena), 0f, 0f, W, 360f), Degrade(false, 0f, 0.6f), Color.white);
        // Piso en perspectiva, con la línea de seguridad del andén.
        Poligono(escena, "Piso", Hex(0x10141C), new Vector2(0f, 540f), new Vector2(W, 540f), new Vector2(W, H), new Vector2(0f, H));
        Image(Place(Node("Linea", escena), 0f, 538f, W, 3f), null, WithAlpha(Accent, 0.7f));
        for (int i = 0; i <= 12; i++)
        {
            float x = i * W / 12f;
            UIPolyline l = Figura<UIPolyline>(escena, "Junta", 0f, 0f, White(0.05f));
            l.thickness = 2f;
            l.SetPoints(new[] { new Vector2(CX + (x - CX) * 0.35f, 541f), new Vector2(CX + (x - CX) * 1.6f, H) });
        }
        // Marcos de cámara en las esquinas.
        foreach (Vector2 esquina in new[] { new Vector2(40f, 40f), new Vector2(W - 40f, 40f), new Vector2(40f, H - 40f), new Vector2(W - 40f, H - 40f) })
        {
            float sx = esquina.x < CX ? 1f : -1f, sy = esquina.y < CY ? 1f : -1f;
            UIPolyline l = Figura<UIPolyline>(escena, "Esquina", 0f, 0f, White(0.25f));
            l.thickness = 2f;
            l.SetPoints(new[] { esquina + new Vector2(0f, 26f * sy), esquina, esquina + new Vector2(26f * sx, 0f) });
        }
        numeroTexto = T(escena, "Numero", 70f, 54f, 500f, 22f, FMono, 16f, Mute, "");
        estadoTexto = T(escena, "Estado", W - 570f, 54f, 500f, 22f, FMono, 16f, Ok, "", TextAlignmentOptions.MidlineRight);

        brillo = Circulo(escena, CX, CY, 330f, WithAlpha(Accent, 0.3f));
        brillo.glow = true;
        brillo.glowMid = 0.25f;

        sombra = Node("Sombra", escena);
        sombra.anchorMin = sombra.anchorMax = new Vector2(0f, 1f);
        sombra.pivot = new Vector2(0.5f, 0.5f);
        sombra.anchoredPosition = new Vector2(CX, -548f);
        sombra.sizeDelta = new Vector2(300f, 60f);
        Image(sombra, Rounded, new Color(0f, 0f, 0f, DarkAlpha(0.6f)), 30f);

        RectTransform nodoGrieta = Node("Grieta", escena);
        nodoGrieta.anchorMin = nodoGrieta.anchorMax = nodoGrieta.pivot = new Vector2(0f, 1f);
        nodoGrieta.anchoredPosition = new Vector2(CX, -CY);
        nodoGrieta.sizeDelta = Vector2.one;
        nodoGrieta.gameObject.AddComponent<CanvasRenderer>();
        grieta = nodoGrieta.gameObject.AddComponent<RiftwalkerRiftGraphic>();
        grieta.raycastTarget = false;

        // Cápsula.
        capsula = Node("Capsula", escena);
        capsula.anchorMin = capsula.anchorMax = new Vector2(0f, 1f);
        capsula.pivot = new Vector2(0.5f, 0.5f);
        capsula.anchoredPosition = new Vector2(CX, -CY);
        capsula.sizeDelta = new Vector2(230f, 340f);
        Image(Place(Node("Cuerpo", capsula), 0f, 0f, 230f, 340f), Rounded, Hex(0x1A1F29), 16f);
        Image(Place(Node("Reflejo", capsula), 18f, 50f, 22f, 240f), Rounded, White(0.08f), 8f);
        luz = Image(Place(Node("Luz", capsula), 95f, 70f, 40f, 200f), Rounded, Hot, 10f);
        Image(Place(Node("Ventana", capsula), 70f, 60f, 90f, 220f), Rounded, new Color(0f, 0f, 0f, DarkAlpha(0.35f)), 12f);
        tapa = Image(Place(Node("Tapa", capsula), -10f, -6f, 250f, 58f), Rounded, Accent, 12f);
        base_ = Image(Place(Node("Base", capsula), -10f, 288f, 250f, 58f), Rounded, Accent, 12f);
        T(base_.rectTransform, "Marca", 0f, 0f, 250f, 58f, FMono, 18f, AccentInk, "RW · RIFT", TextAlignmentOptions.Center, 20f);
        if (Icono != null) Image(Place(Node("Icono", capsula), 71f, 126f, 88f, 88f), Icono, Color.white);
        Borde(capsula, 8f, White(0.15f));

        for (int i = 0; i < 22; i++)
        {
            RectTransform c = Place(Node("Chispa", escena), 0f, 0f, 4f, 4f);
            Image(c, null, Hot);
            chispas.Add(c);
            chispaDatos.Add(new Vector3(Random.Range(-240f, 240f), Random.Range(30f, 90f), Random.value));
        }

        destello = Image(Stretch(Node("Destello", escena)), null, new Color(1f, 1f, 1f, 0f));
        premio = Place(Node("Premio", escena), 0f, 0f, W, H);
        premio.pivot = new Vector2(0.5f, 0.5f); // crece desde el centro
        premio.anchoredPosition = new Vector2(W / 2f, -H / 2f);
        premios = Place(Node("Premios", escena), 0f, 0f, W, H);
    }

    private void ArmarAcciones()
    {
        abrir1 = CrearBoton(rect, 60f, 870f, 340f, 74f, "Abrir 1 · 1 boleto", true, () => Abrir(1), 30f);
        abrir5 = CrearBoton(rect, 416f, 870f, 340f, 74f, "Abrir 5 · 5 boletos", false, () => Abrir(5), 30f);
        saltar = CrearBoton(rect, 772f, 870f, 200f, 74f, "Saltar", false, Saltar, 28f);
        equipar = CrearBoton(rect, 772f, 870f, 260f, 74f, "Equipar", true, Equipar, 30f, Ok);
        leyenda = T(rect, "Leyenda", 60f, 822f, W, 36f, FTexto, 24f, Soft, "");
        TextMeshProUGUI nota = T(rect, "Nota", 60f, 956f, W, 50f, FTexto, 18f, Mute,
            "Simulación de negocio: no hay pagos reales. Los boletos se consiguen jugando (créditos) o con cristales de la tienda. " +
            "Las probabilidades se muestran siempre.", TextAlignmentOptions.TopLeft);
        nota.textWrappingMode = TextWrappingModes.Normal;
    }

    // ================= Columna derecha =================

    private void ArmarColumna()
    {
        const float x = 1270f, w = 590f;

        RectTransform prob = Place(Node("Probabilidades", rect), x, 120f, w, 262f);
        Image(prob, Rounded, PanelColor, 8f);
        Borde(prob, 8f, White(0.06f));
        T(prob, "Rotulo", 24f, 18f, 400f, 22f, FRotulo, 17f, Accent, "Probabilidades", TextAlignmentOptions.MidlineLeft, 16f, true);
        for (int r = (int)Rareza.Legendario, fila = 0; r >= (int)Rareza.Comun; r--, fila++)
        {
            float y = 52f + fila * 36f;
            Color c = ColorRareza((Rareza)r);
            T(prob, "Rareza" + r, 24f, y, 140f, 30f, FTitulo, 26f, c, PerfilUI.NombreRareza((Rareza)r), TextAlignmentOptions.MidlineLeft, 3f, true);
            Image(Place(Node("Pista" + r, prob), 170f, y + 12f, 300f, 8f), Rounded, White(0.08f), 4f);
            Image(Place(Node("Barra" + r, prob), 170f, y + 12f, 300f * PerfilDatos.Probabilidad[r] / 55f, 8f), Rounded, c, 4f);
            T(prob, "Porcentaje" + r, 480f, y, 86f, 30f, FMono, 20f, Ink, PerfilDatos.Probabilidad[r] + " %", TextAlignmentOptions.MidlineRight);
        }
        garantiaTexto = T(prob, "Garantia", 24f, 200f, 540f, 26f, FTexto, 19f, Soft, "");
        barraGarantia = Place(Node("Garantia", prob), 24f, 232f, 542f, 10f);

        RectTransform canje = Place(Node("Canje", rect), x, 398f, w, 290f);
        Image(canje, Rounded, PanelColor, 8f);
        Borde(canje, 8f, White(0.06f));
        T(canje, "Rotulo", 24f, 18f, 400f, 22f, FRotulo, 17f, Accent, "Conseguir boletos", TextAlignmentOptions.MidlineLeft, 16f, true);
        Canje(canje, 52f, 0, $"{PerfilDatos.Numero(PerfilDatos.CreditosPorBoleto)} créditos", "1 boleto",
            () => PerfilDatos.Creditos >= PerfilDatos.CreditosPorBoleto,
            () => PerfilDatos.CanjearCreditos() ? "+1 boleto" : "Te faltan créditos: se ganan jugando partidas");
        Canje(canje, 120f, 2, $"{PerfilDatos.CristalesPorBoleto} cristales", "1 boleto",
            () => PerfilDatos.Cristales >= PerfilDatos.CristalesPorBoleto,
            () => PerfilDatos.CanjearCristales(1) ? "+1 boleto" : "Te faltan cristales");
        Canje(canje, 188f, 2, $"{PerfilDatos.CristalesPorDiez} cristales", "10 boletos · 10 % menos",
            () => PerfilDatos.Cristales >= PerfilDatos.CristalesPorDiez,
            () => PerfilDatos.CanjearCristales(10) ? "+10 boletos" : "Te faltan cristales");
        TextMeshProUGUI tienda = T(canje, "Tienda", 24f, 252f, 540f, 26f, FRotulo, 18f, ColorCristales, "¿Sin cristales? Ir a la tienda »", TextAlignmentOptions.MidlineLeft, 8f, true);
        tienda.raycastTarget = true;
        var pt = tienda.gameObject.AddComponent<ShopPointerTarget>();
        pt.Clicked = () => menu.Abrir(menu.Tienda);
        pt.Hovered = () => tienda.color = Ink;
        pt.Exited = () => tienda.color = ColorCristales;

        RectTransform rec = Place(Node("Recientes", rect), x, 704f, w, 232f);
        Image(rec, Rounded, PanelColor, 8f);
        Borde(rec, 8f, White(0.06f));
        T(rec, "Rotulo", 24f, 18f, 400f, 22f, FRotulo, 17f, Accent, "Últimos premios", TextAlignmentOptions.MidlineLeft, 16f, true);
        listaRecientes = Place(Node("Lista", rec), 24f, 50f, 542f, 172f);
        CrearBoton(rect, x, 952f, w, 56f, "Ver contenido de la cápsula", false, VerContenido, 24f);
    }

    private void Canje(RectTransform parent, float y, int moneda, string costo, string recibe, System.Func<bool> puede, System.Func<string> hacer)
    {
        Image(Place(Node("Fila", parent), 16f, y, 558f, 58f), Rounded, White(0.03f), 6f);
        IconoMoneda(parent, moneda, 44f, y + 29f, 22f);
        T(parent, "Costo", 66f, y + 6f, 300f, 26f, FTitulo, 26f, Ink, costo);
        T(parent, "Recibe", 66f, y + 32f, 300f, 20f, FRotulo, 15f, Mute, "por " + recibe, TextAlignmentOptions.MidlineLeft, 10f, true);
        Boton b = CrearBoton(parent, 574f - 160f - 10f, y + 9f, 160f, 40f, "Canjear", false, () => menu.Avisar(hacer()), 22f);
        canjes.Add((b, puede));
    }

    // ================= Acciones =================

    private void Abrir(int cantidad)
    {
        if (estado == Estado.Abriendo) return;
        if (!PerfilDatos.PuedeAbrir(cantidad))
        {
            menu.Avisar(cantidad == 1 ? "No tenés boletos: canjeá créditos o cristales" : $"Te faltan boletos para abrir {cantidad}");
            return;
        }
        resultados = PerfilDatos.Abrir(cantidad);
        Rareza mejor = Rareza.Comun;
        foreach (var r in resultados) if (r.cosmetico.rareza > mejor) mejor = r.cosmetico.rareza;
        colorFinal = ColorRareza(mejor);
        amague = mejor >= Rareza.Epico;
        colorAmague = ColorRareza(Rareza.Raro);
        BorrarHijos(premio);
        BorrarHijos(premios);
        estado = Estado.Abriendo;
        t = 0f;
        capsula.gameObject.SetActive(true);
        leyenda.text = "Recibiendo la carga…";
        Refrescar();
    }

    private void Saltar()
    {
        if (estado == Estado.Abriendo) t = Fin;
    }

    public override bool Escape()
    {
        if (estado != Estado.Abriendo) return false;
        Saltar();
        return true;
    }

    private void Equipar()
    {
        if (resultados == null || resultados.Count != 1) return;
        Cosmetico c = resultados[0].cosmetico;
        if (PerfilDatos.Equipar(c)) menu.Avisar($"<color={Rich(ColorRareza(c.rareza))}>{c.nombre}</color> equipado");
    }

    private void Reiniciar()
    {
        estado = Estado.Espera;
        t = 0f;
        capsula.gameObject.SetActive(true);
        grieta.SetTime(0f);
        destello.color = new Color(1f, 1f, 1f, 0f);
        leyenda.text = "Un boleto, una cápsula. Cada una trae un avatar, un banner, un título o un marco.";
    }

    // ================= Animación =================

    public override void Tick()
    {
        float dt = Time.unscaledDeltaTime, now = Time.unscaledTime;
        if (estado == Estado.Abriendo)
        {
            t += dt;
            if (t >= Revelar && premio.childCount == 0 && premios.childCount == 0) MostrarPremios();
            if (t >= Fin) Terminar();
        }

        float temblor = estado == Estado.Abriendo ? Mathf.Clamp01(t / Revelar) : 0f;
        float bob = Mathf.Sin(now * 1.6f) * 7f;
        Vector2 sacudida = temblor > 0f ? new Vector2(Mathf.Sin(now * 70f), Mathf.Cos(now * 53f)) * 9f * temblor * temblor : Vector2.zero;
        capsula.anchoredPosition = new Vector2(CX, -CY + bob) + sacudida;
        capsula.localScale = Vector3.one * (1f + 0.05f * temblor);
        sombra.localScale = new Vector3(1f - bob / 60f, 1f, 1f);

        // Color: naranja en espera; al abrir, la rareza (si es épica o mejor, primero amaga con azul).
        Color objetivo = Accent;
        if (estado == Estado.Abriendo) objetivo = amague && t < 0.75f ? colorAmague : colorFinal;
        else if (estado == Estado.Revelado) objetivo = colorFinal;
        luz.color = Color.Lerp(luz.color, objetivo, dt * 8f);
        tapa.color = base_.color = estado == Estado.Espera ? Accent : Color.Lerp(tapa.color, objetivo, dt * 6f);
        float pulso = estado == Estado.Espera ? 0.22f + 0.06f * Mathf.Sin(now * 2f) : 0.3f + 0.4f * temblor;
        brillo.color = WithAlpha(Color.Lerp(brillo.color, objetivo, dt * 6f), pulso);
        brillo.radius = 300f + 60f * temblor;
        brillo.Refresh();

        if (estado == Estado.Abriendo) grieta.SetTime(Mathf.Max(0f, (t - 0.85f) * 1.7f));
        float flash = estado == Estado.Abriendo ? Mathf.Clamp01(1f - Mathf.Abs(t - Revelar) / 0.3f) : 0f;
        destello.color = new Color(1f, 1f, 1f, 0.85f * flash);

        float velocidad = 1f + 4f * temblor;
        for (int i = 0; i < chispas.Count; i++)
        {
            Vector3 d = chispaDatos[i];
            float fase = (d.z + now * d.y * velocidad / 400f) % 1f;
            chispas[i].anchoredPosition = new Vector2(CX + d.x * (0.6f + fase * 0.5f), -(CY + 170f - fase * 380f));
            chispas[i].GetComponent<Img>().color = WithAlpha(Color.Lerp(Hot, objetivo, 0.6f), Mathf.Sin(fase * Mathf.PI) * 0.8f);
        }

        // Premio: entra creciendo.
        if (premio.childCount > 0 || premios.childCount > 0)
        {
            float k = Mathf.Clamp01((t - Revelar) / 0.35f);
            if (estado == Estado.Revelado) k = 1f;
            float e = 1f - Mathf.Pow(1f - k, 3f);
            premio.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, e);
            for (int i = 0; i < premios.childCount; i++)
            {
                float ki = estado == Estado.Revelado ? 1f : Mathf.Clamp01((t - Revelar - i * 0.08f) / 0.3f);
                premios.GetChild(i).localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, 1f - Mathf.Pow(1f - ki, 3f));
            }
        }
    }

    private void MostrarPremios()
    {
        capsula.gameObject.SetActive(false);
        premio.localScale = Vector3.one;
        if (resultados.Count == 1) CartaGrande(resultados[0]);
        else
        {
            float w = 196f, gap = 18f, x0 = (W - (resultados.Count * w + (resultados.Count - 1) * gap)) / 2f;
            for (int i = 0; i < resultados.Count; i++) CartaChica(resultados[i], x0 + i * (w + gap), 170f, w);
        }
    }

    private void Terminar()
    {
        if (premio.childCount == 0 && premios.childCount == 0) MostrarPremios();
        estado = Estado.Revelado;
        t = Fin;
        grieta.SetTime(0f);
        destello.color = new Color(1f, 1f, 1f, 0f);
        int nuevos = resultados.FindAll(r => r.nuevo).Count, creditos = 0;
        foreach (var r in resultados) creditos += r.creditos;
        if (resultados.Count == 1)
        {
            var r = resultados[0];
            leyenda.text = r.nuevo
                ? $"¡Nuevo! <color={Rich(ColorRareza(r.cosmetico.rareza))}>{r.cosmetico.nombre}</color> ya está en tu colección."
                : $"Repetido: se convirtió en <color=#D6DAE0>{r.creditos} créditos</color>.";
        }
        else leyenda.text = $"{nuevos} {(nuevos == 1 ? "nuevo" : "nuevos")}" + (creditos > 0 ? $" · repetidos convertidos en {creditos} créditos" : "");
        Refrescar();
    }

    private void CartaGrande(PerfilDatos.Resultado r)
    {
        Cosmetico c = r.cosmetico;
        Color colorR = ColorRareza(c.rareza);
        UIRing halo = Circulo(premio, CX, CY, 320f, WithAlpha(colorR, 0.45f));
        halo.glow = true;
        RectTransform carta = Place(Node("Carta", premio), CX - 190f, CY - 250f, 380f, 500f);
        Image(carta, Rounded, colorR, 12f);
        Image(Place(Node("Interior", carta), 4f, 4f, 372f, 492f), Rounded, Hex(0x0E1117), 10f);
        T(carta, "Rareza", 0f, 18f, 380f, 26f, FRotulo, 22f, colorR, PerfilUI.NombreRareza(c.rareza) + (r.garantia ? " · garantía" : ""), TextAlignmentOptions.Center, 16f, true);
        Arte(carta, c, 40f, 56f, 300f);
        TextMeshProUGUI n = T(carta, "Nombre", 20f, 372f, 340f, 46f, FTitulo, 40f, Ink, c.nombre, TextAlignmentOptions.Center, 3f, true);
        n.enableAutoSizing = true;
        n.fontSizeMin = 24f;
        n.fontSizeMax = 40f;
        string tipo = NombreTipo(c.tipo);
        T(carta, "Tipo", 20f, 418f, 340f, 22f, FRotulo, 16f, Mute, tipo + (string.IsNullOrEmpty(c.estilo) ? "" : " · " + c.estilo), TextAlignmentOptions.Center, 14f, true);
        Etiqueta(carta, r, 190f, 456f);
    }

    private void CartaChica(PerfilDatos.Resultado r, float x, float y, float w)
    {
        Cosmetico c = r.cosmetico;
        Color colorR = ColorRareza(c.rareza);
        RectTransform carta = Node("Carta", premios);
        carta.anchorMin = carta.anchorMax = new Vector2(0f, 1f);
        carta.pivot = new Vector2(0.5f, 0.5f);
        carta.sizeDelta = new Vector2(w, 320f);
        carta.anchoredPosition = new Vector2(x + w / 2f, -(y + 160f));
        Image(carta, Rounded, colorR, 10f);
        Image(Place(Node("Interior", carta), 3f, 3f, w - 6f, 314f), Rounded, Hex(0x0E1117), 8f);
        T(carta, "Rareza", 0f, 12f, w, 20f, FRotulo, 15f, colorR, PerfilUI.NombreRareza(c.rareza), TextAlignmentOptions.Center, 12f, true);
        Arte(carta, c, 14f, 38f, w - 28f);
        TextMeshProUGUI n = T(carta, "Nombre", 10f, 216f, w - 20f, 52f, FRotulo, 20f, Ink, c.nombre, TextAlignmentOptions.Center);
        n.textWrappingMode = TextWrappingModes.Normal;
        Etiqueta(carta, r, w / 2f, 284f);
    }

    private static void Arte(RectTransform carta, Cosmetico c, float x, float y, float lado)
    {
        if (c.tipo == Tipo.Avatar) Cubrir(Place(Node("Arte", carta), x, y, lado, lado), PerfilDatos.Imagen(c));
        else if (c.tipo == Tipo.Banner)
        {
            Image(Place(Node("Fondo", carta), x, y, lado, lado), Rounded, Hex(0x151922), 6f);
            Cubrir(Place(Node("Arte", carta), x, y + lado / 3f, lado, lado / 3f), PerfilDatos.Imagen(c));
        }
        else if (c.tipo == Tipo.Marco)
        {
            // Sobre el banner que tenés equipado, como queda en la cabecera del Perfil.
            Image(Place(Node("Fondo", carta), x, y, lado, lado), Rounded, Hex(0x151922), 6f);
            float alto = lado / 3f;
            RectTransform caja = Place(Node("Arte", carta), x, y + (lado - alto) / 2f, lado, alto);
            Cubrir(caja, PerfilDatos.Imagen(PerfilDatos.Banner));
            RectTransform marco = null;
            Enmarcar(ref marco, carta, x, y + (lado - alto) / 2f, lado, alto, c, true);
        }
        else
        {
            Image(Place(Node("Fondo", carta), x, y, lado, lado), Rounded, Hex(0x151922), 6f);
            TextMeshProUGUI t = T(carta, "Titulo", x + 12f, y, lado - 24f, lado, FTitulo, lado / 6f, ColorRareza(c.rareza), c.nombre, TextAlignmentOptions.Center, 2f, true);
            t.textWrappingMode = TextWrappingModes.Normal;
            // Achica la letra antes que cortar una palabra larga ("Exterminador").
            t.enableAutoSizing = true;
            t.fontSizeMin = lado / 12f;
            t.fontSizeMax = lado / 6f;
        }
    }

    private static void Etiqueta(RectTransform carta, PerfilDatos.Resultado r, float cx, float y)
    {
        string texto = r.nuevo ? "Nuevo" : $"+{r.creditos} créditos";
        float w = r.nuevo ? 110f : 170f;
        RectTransform chip = Place(Node("Etiqueta", carta), cx - w / 2f, y, w, 30f);
        Image(chip, Rounded, r.nuevo ? Accent : White(0.12f), 6f);
        T(chip, "Texto", 0f, 0f, w, 30f, FRotulo, 17f, r.nuevo ? AccentInk : Soft, texto, TextAlignmentOptions.Center, 14f, true);
    }

    // ================= Contenido =================

    private void VerContenido()
    {
        RectTransform p = menu.AbrirModal(1160f, 860f);
        T(p, "Rotulo", 48f, 36f, 900f, 22f, FRotulo, 18f, Accent, "Estación del Rift", TextAlignmentOptions.MidlineLeft, 16f, true);
        T(p, "Titulo", 46f, 60f, 900f, 54f, FTitulo, 48f, Ink, "Contenido de la cápsula", TextAlignmentOptions.MidlineLeft, 3f, true);
        TextMeshProUGUI ayuda = T(p, "Ayuda", 48f, 118f, 1064f, 74f, FTexto, 20f, Mute,
            $"Primero sale la rareza (con estas probabilidades), después el tipo ({PerfilDatos.ProbabilidadTipo[0]} % avatar, " +
            $"{PerfilDatos.ProbabilidadTipo[1]} % banner, {PerfilDatos.ProbabilidadTipo[2]} % título, {PerfilDatos.ProbabilidadTipo[3]} % marco) y por último un objeto al azar. Cada {PerfilDatos.Garantia} cápsulas " +
            "hay uno épico o mejor asegurado. Los repetidos se convierten en créditos.", TextAlignmentOptions.TopLeft);
        ayuda.textWrappingMode = TextWrappingModes.Normal;
        Scroll(p, 48f, 200f, 1064f, 556f, out RectTransform lista);
        float y = 0f;
        for (int r = (int)Rareza.Legendario; r >= (int)Rareza.Comun; r--)
        {
            Color c = ColorRareza((Rareza)r);
            List<Cosmetico> items = PerfilDatos.Catalogo.FindAll(x => (int)x.rareza == r);
            T(lista, "Rareza" + r, 0f, y, 1064f, 36f, FTitulo, 30f, c,
                $"{PerfilUI.NombreRareza((Rareza)r)}  <color=#8E96A3><size=20>{PerfilDatos.Probabilidad[r]} % · {items.Count} objetos</size></color>",
                TextAlignmentOptions.MidlineLeft, 3f, true);
            y += 44f;
            for (int i = 0; i < items.Count; i++)
            {
                float x = (i % 3) * 356f;
                if (i > 0 && i % 3 == 0) y += 34f;
                Cosmetico it = items[i];
                string tipo = NombreTipo(it.tipo);
                bool tiene = PerfilDatos.Tiene(it);
                TextMeshProUGUI fila = T(lista, it.nombre, x, y, 346f, 30f, FTexto, 20f, tiene ? Ink : Soft,
                    $"{it.nombre}  <color=#8E96A3>{tipo}</color>{(tiene ? "  <color=#3DDC97>· tenés</color>" : "")}");
                fila.overflowMode = TextOverflowModes.Ellipsis;
            }
            y += 50f;
        }
        lista.sizeDelta = new Vector2(1064f, Mathf.Max(556f, y));
        CrearBoton(p, 1160f - 48f - 200f, 860f - 40f - 60f + 8f, 200f, 56f, "Cerrar", false, menu.CerrarModal, 24f);
    }

    // ================= Datos =================

    public override void Refrescar()
    {
        base.Refrescar();
        bool abriendo = estado == Estado.Abriendo;
        abrir1.Habilitar(!abriendo && PerfilDatos.PuedeAbrir(1));
        abrir5.Habilitar(!abriendo && PerfilDatos.PuedeAbrir(5));
        abrir1.texto.text = estado == Estado.Revelado ? "Abrir otra · 1 boleto" : "Abrir 1 · 1 boleto";
        saltar.rect.gameObject.SetActive(abriendo);
        bool puedeEquipar = estado == Estado.Revelado && resultados != null && resultados.Count == 1 && !PerfilDatos.Equipado(resultados[0].cosmetico);
        equipar.rect.gameObject.SetActive(puedeEquipar);
        foreach (var (boton, puede) in canjes) boton.Habilitar(puede());

        numeroTexto.text = $"CÁPSULA DEL RIFT · Nº {PerfilDatos.Aperturas + (abriendo ? 0 : 1):000}";
        estadoTexto.text = abriendo ? "• ABRIENDO" : estado == Estado.Revelado ? "• ENTREGADO" : "• EN ESPERA";
        estadoTexto.color = abriendo ? Accent : Ok;

        int faltan = PerfilDatos.FaltanParaGarantia;
        garantiaTexto.text = faltan <= 1 ? "¡La próxima trae <color=#B061FF>épico o mejor</color> asegurado!"
                                         : $"Épico o mejor asegurado en <color=#F3F4F6>{faltan} cápsulas</color>";
        BorrarHijos(barraGarantia);
        float paso = 542f / PerfilDatos.Garantia;
        for (int i = 0; i < PerfilDatos.Garantia; i++)
            Image(Place(Node("Paso" + i, barraGarantia), i * paso, 0f, paso - 4f, 10f), Rounded,
                i < PerfilDatos.Garantia - faltan ? ColorRareza(Rareza.Epico) : White(0.1f), 3f);

        BorrarHijos(listaRecientes);
        int fila = 0;
        foreach (string clave in PerfilDatos.Recientes)
        {
            if (fila >= 5) break;
            Cosmetico c = PerfilDatos.DesdeClave(clave);
            if (c == null) continue;
            Color colorR = ColorRareza(c.rareza);
            Image(Place(Node("Punto", listaRecientes), 0f, fila * 34f + 10f, 10f, 10f), Rounded, colorR, 5f);
            string tipo = NombreTipo(c.tipo);
            TextMeshProUGUI tx = T(listaRecientes, "Item", 22f, fila * 34f, 520f, 30f, FTexto, 20f, Ink, $"{c.nombre}  <color=#8E96A3>· {tipo}</color>");
            tx.overflowMode = TextOverflowModes.Ellipsis;
            fila++;
        }
        if (fila == 0) T(listaRecientes, "Vacio", 0f, 0f, 520f, 30f, FTexto, 20f, Mute, "Todavía no abriste ninguna cápsula.");
    }
}
