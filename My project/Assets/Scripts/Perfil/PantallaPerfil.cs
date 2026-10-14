using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static ShopUIKit;
using static PerfilUI;
using Img = UnityEngine.UI.Image;
using Tipo = PerfilDatos.Tipo;
using Cosmetico = PerfilDatos.Cosmetico;

// PRUEBA DEL MENÚ: Perfil del jugador. Arriba, la cabecera con el banner, el avatar, el nombre, el título y un
// resumen; abajo, dos pestañas: Estadísticas (datos de ejemplo) y Personalización (avatares, banners, títulos y marcos).
public class PantallaPerfil : PantallaBase
{
    private const float Y0 = 520f, AnchoGrilla = 1260f;

    private Img banner, avatar, marcoAvatar;
    private RectTransform cabecera, marcoCabecera;
    private Transform bajoMarco;
    private TextMeshProUGUI nombre, titulo, infoPestana;
    private readonly TextMeshProUGUI[] resumen = new TextMeshProUGUI[4];
    private readonly TextMeshProUGUI[] pestanas = new TextMeshProUGUI[2];
    private Img subrayado;
    private RectTransform panelEstad, panelPers, grilla, vista;
    private UnityEngine.UI.ScrollRect scroll;
    private int pestana;

    private Tipo categoria = Tipo.Avatar;
    private bool mostrarBloqueados = true;
    private Cosmetico seleccionado;
    private readonly RectTransform[] chips = new RectTransform[4];
    private Img check;

    public PantallaPerfil(MenuRiftwalker menu, RectTransform parent) : base(menu, parent, "Perfil")
    {
        BarraSuperior("Registro del jugador", "Perfil");
        ArmarCabecera();
        ArmarPestanas();
        ArmarEstadisticas();
        ArmarPersonalizacion();
        ElegirPestana(0);
    }

    // ================= Cabecera =================

    private void ArmarCabecera()
    {
        RectTransform cab = cabecera = Place(Node("Cabecera", rect), 60f, 120f, 1800f, 300f);
        Image(cab, Rounded, PanelColor, 10f);
        banner = Cubrir(Place(Node("Banner", cab), 0f, 0f, 1800f, 300f), null);
        Image(Place(Node("Sombra", cab), 0f, 0f, 1150f, 300f), Degrade(true, 0.9f, 0f), Color.white);
        Img sombra = Image(Place(Node("SombraAbajo", cab), 0f, 140f, 1800f, 160f), Degrade(false, 0.55f, 0f), Color.white);
        Img borde = Borde(cab, 8f, White(0.08f));
        // El marco va sobre el banner, pero detrás del avatar, los textos y el botón.
        bajoMarco = borde != null ? borde.transform : sombra.transform;

        // Centrados en alto dentro del marco y corridos hacia adentro, así no tapan sus esquinas.
        // Borde blanco como el nombre: la rareza se ve en la colección, no en la cabecera.
        marcoAvatar = Image(Place(Node("MarcoAvatar", cab), 92f, 46f, 208f, 208f), Rounded, Ink, 8f);
        avatar = Cubrir(Place(Node("Avatar", cab), 96f, 50f, 200f, 200f), null);

        nombre = T(cab, "Nombre", 336f, 36f, 900f, 90f, FTitulo, 88f, Ink, "", TextAlignmentOptions.MidlineLeft, 3f, true);
        titulo = T(cab, "Titulo", 340f, 124f, 900f, 38f, FRotulo, 30f, Accent, "", TextAlignmentOptions.MidlineLeft, 6f);
        string[] rotulos = { "Partidas", "Victorias", "KDA*", "Oleada récord" };
        for (int i = 0; i < 4; i++)
        {
            RectTransform chip = Place(Node("Resumen" + i, cab), 340f + i * 170f, 178f, 156f, 70f);
            Image(chip, Rounded, new Color(0f, 0f, 0f, DarkAlpha(0.5f)), 6f);
            resumen[i] = T(chip, "Valor", 16f, 6f, 130f, 38f, FTitulo, 36f, Ink, "");
            T(chip, "Rotulo", 16f, 44f, 140f, 18f, FRotulo, 14f, Mute, rotulos[i], TextAlignmentOptions.MidlineLeft, 14f, true);
        }

        CrearBoton(cab, 1800f - 150f - 220f, 222f, 220f, 46f, "Cambiar nombre", false, () =>
        {
            PlayerNameScreen pantalla = Object.FindAnyObjectByType<PlayerNameScreen>();
            if (pantalla != null) pantalla.Open(false);
        }, 20f);
    }

    // ================= Pestañas =================

    private void ArmarPestanas()
    {
        string[] nombres = { "Estadísticas", "Personalización" };
        float x = 60f;
        for (int i = 0; i < 2; i++)
        {
            int k = i;
            pestanas[i] = T(rect, nombres[i], x, 438f, 360f, 50f, FTitulo, 36f, Mute, nombres[i], TextAlignmentOptions.MidlineLeft, 6f, true);
            float w = Width(pestanas[i], nombres[i].ToUpperInvariant());
            pestanas[i].rectTransform.sizeDelta = new Vector2(w, 50f);
            pestanas[i].raycastTarget = true;
            var p = pestanas[i].gameObject.AddComponent<ShopPointerTarget>();
            p.Clicked = () => ElegirPestana(k);
            p.Hovered = () => { if (pestana != k) pestanas[k].color = Soft; };
            p.Exited = () => { if (pestana != k) pestanas[k].color = Mute; };
            x += w + 56f;
        }
        Image(Place(Node("Linea", rect), 60f, 494f, 1800f, 1f), null, White(0.1f));
        subrayado = Image(Place(Node("Subrayado", rect), 60f, 490f, 100f, 4f), null, Accent);
        infoPestana = T(rect, "Info", 1060f, 448f, 800f, 34f, FRotulo, 20f, Mute, "", TextAlignmentOptions.MidlineRight, 10f, true);
    }

    private void ElegirPestana(int i)
    {
        pestana = i;
        for (int k = 0; k < 2; k++) pestanas[k].color = k == i ? Ink : Mute;
        RectTransform t = pestanas[i].rectTransform;
        subrayado.rectTransform.anchoredPosition = new Vector2(t.anchoredPosition.x, -490f);
        subrayado.rectTransform.sizeDelta = new Vector2(t.sizeDelta.x, 4f);
        panelEstad.gameObject.SetActive(i == 0);
        panelPers.gameObject.SetActive(i == 1);
        RefrescarInfo();
    }

    private void RefrescarInfo()
    {
        if (pestana == 0) infoPestana.text = "Datos de ejemplo";
        else
        {
            int tiene = PerfilDatos.Obtenidos(Tipo.Avatar) + PerfilDatos.Obtenidos(Tipo.Banner) + PerfilDatos.Obtenidos(Tipo.Titulo) + PerfilDatos.Obtenidos(Tipo.Marco);
            int total = PerfilDatos.Total(Tipo.Avatar) + PerfilDatos.Total(Tipo.Banner) + PerfilDatos.Total(Tipo.Titulo) + PerfilDatos.Total(Tipo.Marco);
            infoPestana.text = $"Colección  <color=#F3F4F6>{tiene} / {total}</color>";
        }
    }

    // ================= Estadísticas =================

    private void ArmarEstadisticas()
    {
        panelEstad = Place(Node("Estadisticas", rect), 60f, Y0, 1800f, 540f);
        var grupos = PerfilDatos.Estadisticas;

        // General: una fila con todo.
        RectTransform general = Place(Node("General", panelEstad), 0f, 0f, 1800f, 168f);
        Image(general, Rounded, PanelColor, 8f);
        Borde(general, 8f, White(0.06f));
        T(general, "Rotulo", 28f, 16f, 400f, 22f, FRotulo, 18f, Accent, grupos[0].grupo, TextAlignmentOptions.MidlineLeft, 16f, true);
        var datos = grupos[0].datos;
        float paso = 1744f / datos.Length;
        for (int i = 0; i < datos.Length; i++)
        {
            T(general, "Valor" + i, 28f + i * paso, 48f, paso - 10f, 58f, FTitulo, 54f, Ink, datos[i].valor);
            T(general, "Nombre" + i, 30f + i * paso, 112f, paso - 10f, 22f, FRotulo, 15f, Mute, datos[i].nombre, TextAlignmentOptions.MidlineLeft, 12f, true);
        }

        // Un panel por modo.
        float w = (1800f - 3f * 20f) / 4f;
        for (int g = 1; g < grupos.Length; g++)
        {
            ColorUtility.TryParseHtmlString(grupos[g].color, out Color c);
            RectTransform tarjeta = Place(Node(grupos[g].grupo, panelEstad), (g - 1) * (w + 20f), 188f, w, 300f);
            Image(tarjeta, Rounded, PanelColor, 8f);
            Borde(tarjeta, 8f, White(0.06f));
            Image(Place(Node("Color", tarjeta), 0f, 0f, w, 4f), null, c);
            T(tarjeta, "Titulo", 24f, 16f, w - 48f, 40f, FTitulo, 34f, c, grupos[g].grupo, TextAlignmentOptions.MidlineLeft, 4f, true);
            var filas = grupos[g].datos;
            for (int j = 0; j < filas.Length; j++)
            {
                float y = 66f + j * 37f;
                T(tarjeta, "Dato" + j, 24f, y, w - 140f, 32f, FTexto, 21f, Soft, filas[j].nombre);
                T(tarjeta, "Valor" + j, w - 164f, y, 140f, 32f, FTitulo, 28f, Ink, filas[j].valor, TextAlignmentOptions.MidlineRight);
                if (j < filas.Length - 1) Image(Place(Node("Linea" + j, tarjeta), 24f, y + 34f, w - 48f, 1f), null, White(0.06f));
            }
        }
        TextMeshProUGUI nota = T(panelEstad, "Nota", 0f, 504f, 1800f, 26f, FTexto, 18f, Mute,
            "* KDA provisoria: (bajas + asistencias) ÷ muertes. Son datos de ejemplo: las estadísticas todavía no se registran (F24).");
        nota.textWrappingMode = TextWrappingModes.Normal;
    }

    // ================= Personalización =================

    private void ArmarPersonalizacion()
    {
        panelPers = Place(Node("Personalizacion", rect), 60f, Y0, 1800f, 540f);
        Tipo[] tipos = { Tipo.Avatar, Tipo.Banner, Tipo.Titulo, Tipo.Marco };
        for (int i = 0; i < 4; i++)
        {
            Tipo t = tipos[i];
            chips[i] = Place(Node("Categoria" + i, panelPers), i * 242f, 0f, 230f, 52f);
            Clickeable(chips[i], () => { categoria = t; seleccionado = null; scroll.verticalNormalizedPosition = 1f; Refrescar(); });
        }

        RectTransform toggle = Place(Node("Bloqueados", panelPers), 1000f, 8f, 260f, 36f);
        Image(Place(Node("Caja", toggle), 0f, 6f, 24f, 24f), Rounded, White(0.12f), 4f);
        check = Image(Place(Node("Check", toggle), 5f, 11f, 14f, 14f), Rounded, Accent, 3f);
        T(toggle, "Texto", 36f, 0f, 220f, 36f, FRotulo, 19f, Soft, "Mostrar bloqueados", TextAlignmentOptions.MidlineLeft, 8f, true);
        Clickeable(toggle, () => { mostrarBloqueados = !mostrarBloqueados; Refrescar(); });

        scroll = Scroll(panelPers, 0f, 68f, AnchoGrilla, 472f, out grilla);
        vista = Place(Node("Vista", panelPers), 1300f, 0f, 500f, 540f);
    }

    private void RefrescarChips()
    {
        string[] nombres = { "Avatares", "Banners", "Títulos", "Marcos" };
        Tipo[] tipos = { Tipo.Avatar, Tipo.Banner, Tipo.Titulo, Tipo.Marco };
        for (int i = 0; i < 4; i++)
        {
            RectTransform c = chips[i];
            BorrarHijos(c);
            Img fondo = c.GetComponent<Img>();
            bool activo = tipos[i] == categoria;
            fondo.sprite = Rounded;
            fondo.type = Img.Type.Sliced;
            fondo.pixelsPerUnitMultiplier = 16f / 8f;
            fondo.color = activo ? Accent : PanelColor;
            T(c, "Nombre", 20f, 0f, 150f, 52f, FTitulo, 28f, activo ? AccentInk : Soft, nombres[i], TextAlignmentOptions.MidlineLeft, 6f, true);
            T(c, "Cuenta", 118f, 0f, 96f, 52f, FMono, 18f, activo ? AccentInk : Mute,
                $"{PerfilDatos.Obtenidos(tipos[i])}/{PerfilDatos.Total(tipos[i])}", TextAlignmentOptions.MidlineRight);
            int nuevos = PerfilDatos.CantidadNuevos(tipos[i]);
            if (nuevos > 0)
            {
                RectTransform badge = Place(Node("Nuevos", c), 194f, -10f, 44f, 24f);
                Image(badge, Rounded, activo ? Ink : Accent, 6f);
                T(badge, "N", 0f, 0f, 44f, 24f, FRotulo, 15f, AccentInk, "+" + nuevos, TextAlignmentOptions.Center);
            }
        }
        check.enabled = mostrarBloqueados;
    }

    private void RefrescarGrilla()
    {
        BorrarHijos(grilla);
        var lista = new List<Cosmetico>();
        foreach (Cosmetico c in PerfilDatos.Catalogo)
            if (c.tipo == categoria && PerfilDatos.Tiene(c)) lista.Add(c);
        if (mostrarBloqueados)
            foreach (Cosmetico c in PerfilDatos.Catalogo)
                if (c.tipo == categoria && !PerfilDatos.Tiene(c)) lista.Add(c);

        if (seleccionado == null || seleccionado.tipo != categoria)
            seleccionado = lista.Find(PerfilDatos.Equipado) ?? (lista.Count > 0 ? lista[0] : null);

        int columnas = categoria == Tipo.Avatar ? 7 : categoria == Tipo.Marco ? 2 : 3;
        float gap = 18f;
        float w = (AnchoGrilla - (columnas - 1) * gap) / columnas;
        float h = categoria == Tipo.Avatar ? w + 58f : categoria == Tipo.Banner ? w / 3f + 60f : categoria == Tipo.Marco ? w / 5.4f + 60f : 78f;
        for (int i = 0; i < lista.Count; i++)
        {
            float x = (i % columnas) * (w + gap), y = (i / columnas) * (h + gap);
            Carta(lista[i], x, y, w, h);
        }
        int filas = (lista.Count + columnas - 1) / columnas;
        grilla.sizeDelta = new Vector2(AnchoGrilla, Mathf.Max(472f, filas * (h + gap)));
    }

    private void Carta(Cosmetico c, float x, float y, float w, float h)
    {
        bool tiene = PerfilDatos.Tiene(c), equipado = PerfilDatos.Equipado(c), elegido = seleccionado == c;
        Color colorR = ColorRareza(c.rareza);
        RectTransform carta = Place(Node(c.nombre, grilla), x, y, w, h);
        Image(carta, Rounded, PanelColor, 8f, true);

        float textoY;
        if (c.tipo == Tipo.Titulo)
        {
            Image(Place(Node("Rareza", carta), 0f, 0f, 6f, h), null, colorR);
            TextMeshProUGUI t = T(carta, "Nombre", 22f, 4f, w - 40f, 42f, FTitulo, 27f, tiene ? Ink : Mute, c.nombre);
            t.overflowMode = TextOverflowModes.Ellipsis;
            textoY = 44f;
            if (!tiene) Candado(carta, w - 26f, 26f, 22f, Mute);
        }
        else
        {
            float ih = c.tipo == Tipo.Avatar ? w : c.tipo == Tipo.Marco ? w / 5.4f : w / 3f;
            RectTransform caja = Place(Node("Imagen", carta), 0f, 0f, w, ih);
            // Los marcos se muestran sobre el banner equipado, como quedan en la cabecera.
            Img img = Cubrir(caja, PerfilDatos.Imagen(c.tipo == Tipo.Marco ? PerfilDatos.Banner : c));
            if (c.tipo == Tipo.Marco) { RectTransform m = null; Enmarcar(ref m, caja, 0f, 0f, w, ih, c, true); }
            if (!tiene)
            {
                img.color = new Color(0.45f, 0.45f, 0.5f, 1f);
                Image(Stretch(Node("Velo", caja)), null, new Color(0f, 0f, 0f, DarkAlpha(0.55f)));
                Candado(caja, w / 2f, ih / 2f, 34f, Soft);
            }
            TextMeshProUGUI t = T(carta, "Nombre", 12f, ih + 5f, w - 24f, 30f, FRotulo, 20f, tiene ? Ink : Mute, c.nombre, TextAlignmentOptions.MidlineLeft, 2f);
            t.overflowMode = TextOverflowModes.Ellipsis;
            textoY = ih + 32f;
            Image(Place(Node("Rareza", carta), 0f, h - 4f, w, 4f), null, colorR);
        }

        string estado = equipado ? "Equipado" : !tiene ? "Bloqueado" : PerfilUI.NombreRareza(c.rareza);
        Color colorEstado = equipado ? Ok : !tiene ? Mute : colorR;
        float sx = c.tipo == Tipo.Titulo ? 22f : 12f;
        T(carta, "Estado", sx, textoY, w - 24f, 20f, FRotulo, 15f, colorEstado, estado, TextAlignmentOptions.MidlineLeft, 14f, true);

        if (PerfilDatos.EsNuevo(c))
        {
            RectTransform nuevo = Place(Node("Nuevo", carta), 8f, 8f, 64f, 24f);
            Image(nuevo, Rounded, Accent, 5f);
            T(nuevo, "T", 0f, 0f, 64f, 24f, FRotulo, 14f, AccentInk, "Nuevo", TextAlignmentOptions.Center, 12f, true);
        }

        Img borde = Borde(carta, 8f, elegido ? Accent : White(0.08f));
        var p = Clickeable(carta, () => { seleccionado = c; PerfilDatos.Visto(c); Refrescar(); });
        p.Hovered = () => { if (borde != null && seleccionado != c) borde.color = White(0.35f); };
        p.Exited = () => { if (borde != null && seleccionado != c) borde.color = White(0.08f); };
    }

    private void RefrescarVista()
    {
        BorrarHijos(vista);
        Image(Stretch(Node("Fondo", vista)), Rounded, PanelColor, 8f);
        Borde(Stretch(Node("Marco", vista)), 8f, White(0.08f));
        T(vista, "Rotulo", 24f, 18f, 400f, 22f, FRotulo, 17f, Accent, "Vista previa", TextAlignmentOptions.MidlineLeft, 16f, true);
        Cosmetico c = seleccionado;
        if (c == null) return;
        Color colorR = ColorRareza(c.rareza);

        if (c.tipo == Tipo.Avatar)
        {
            Image(Place(Node("MarcoAvatar", vista), 96f, 54f, 308f, 308f), Rounded, colorR, 8f);
            Cubrir(Place(Node("Avatar", vista), 100f, 58f, 300f, 300f), PerfilDatos.Imagen(c));
        }
        else
        {
            // Cómo queda la cabecera con ese banner o ese título.
            RectTransform mini = Place(Node("Cabecera", vista), 24f, 60f, 452f, 296f);
            Image(mini, Rounded, Hex(0x11141B), 8f);
            RectTransform cajaBanner = Place(Node("Banner", mini), 0f, 0f, 452f, 151f);
            Cubrir(cajaBanner, PerfilDatos.Imagen(c.tipo == Tipo.Banner ? c : PerfilDatos.Banner));
            Img sombraMini = Image(Place(Node("Sombra", mini), 0f, 0f, 300f, 151f), Degrade(true, 0.9f, 0f), Color.white);
            PerfilDatos.Cosmetico av = PerfilDatos.Avatar;
            Image(Place(Node("MarcoAvatar", mini), 34f, 29.5f, 92f, 92f), Rounded, Ink, 6f);
            Cubrir(Place(Node("Avatar", mini), 36f, 31.5f, 88f, 88f), PerfilDatos.Imagen(av));
            T(mini, "Nombre", 140f, 40f, 300f, 40f, FTitulo, 38f, Ink, PlayerProfile.HasName ? PlayerProfile.Name : "Jugador", TextAlignmentOptions.MidlineLeft, 3f, true);
            PerfilDatos.Cosmetico ti = c.tipo == Tipo.Titulo ? c : PerfilDatos.Titulo;
            TextMeshProUGUI tt = T(mini, "Titulo", 142f, 82f, 290f, 26f, FRotulo, 20f,
                string.IsNullOrEmpty(ti.id) ? Mute : Ink, string.IsNullOrEmpty(ti.id) ? "Sin título" : ti.nombre);
            tt.overflowMode = TextOverflowModes.Ellipsis;
            RectTransform mm = null;
            Enmarcar(ref mm, mini, 0f, 0f, 452f, 151f, c.tipo == Tipo.Marco ? c : PerfilDatos.Marco, false, sombraMini.transform);
            if (c.tipo == Tipo.Titulo)
            {
                TextMeshProUGUI grande = T(mini, "Grande", 20f, 170f, 412f, 110f, FTitulo, 44f, colorR, c.nombre, TextAlignmentOptions.Center, 2f, true);
                grande.textWrappingMode = TextWrappingModes.Normal;
                grande.enableAutoSizing = true;
                grande.fontSizeMin = 24f;
                grande.fontSizeMax = 44f;
            }
            else T(mini, "Ayuda", 20f, 168f, 412f, 110f, FTexto, 20f, Mute, c.tipo == Tipo.Marco
                ? "Así queda el marco en la cabecera de tu Perfil. Más adelante, también en la tabla con Tab."
                : "Así se ve tu cabecera en el Perfil.\nEl lado izquierdo del banner queda para tu avatar y tu nombre.", TextAlignmentOptions.TopLeft)
                .textWrappingMode = TextWrappingModes.Normal;
        }

        TextMeshProUGUI n = T(vista, "Nombre", 24f, 366f, 452f, 58f, FTitulo, 42f, Ink, c.nombre, TextAlignmentOptions.MidlineLeft, 3f, true);
        n.overflowMode = TextOverflowModes.Ellipsis;
        string tipo = NombreTipo(c.tipo);
        string detalle = $"{tipo} · <color={Rich(colorR)}>{PerfilUI.NombreRareza(c.rareza)}</color>";
        if (!string.IsNullOrEmpty(c.estilo)) detalle += " · " + c.estilo;
        PerfilDatos.Personaje pj = PerfilDatos.Personajes.Find(x => x.id == c.personaje);
        if (pj != null) detalle += " · " + pj.nombre;
        T(vista, "Detalle", 24f, 420f, 452f, 28f, FTexto, 20f, Mute, detalle);

        if (!PerfilDatos.Tiene(c))
            CrearBoton(vista, 24f, 464f, 452f, 56f, "Conseguilo en la estación »", false, () => menu.Abrir(menu.Estacion), 24f);
        else if (PerfilDatos.Equipado(c))
            CrearBoton(vista, 24f, 464f, 452f, 56f, "Equipado", true, null, 26f, Ok).button.interactable = false;
        else
            CrearBoton(vista, 24f, 464f, 452f, 56f, "Equipar", true, () =>
            {
                if (PerfilDatos.Equipar(c)) menu.Avisar($"<color={Rich(colorR)}>{c.nombre}</color> equipado");
            }, 26f);
    }

    // ================= Datos =================

    public override void Refrescar()
    {
        base.Refrescar();
        PerfilDatos.Cosmetico av = PerfilDatos.Avatar, bn = PerfilDatos.Banner, ti = PerfilDatos.Titulo;
        banner.sprite = PerfilDatos.Imagen(bn);
        AjustarCubrir(banner, (RectTransform)banner.rectTransform.parent);
        avatar.sprite = PerfilDatos.Imagen(av);
        AjustarCubrir(avatar, (RectTransform)avatar.rectTransform.parent);
        marcoAvatar.color = Ink;
        Enmarcar(ref marcoCabecera, cabecera, 0f, 0f, 1800f, 300f, PerfilDatos.Marco, false, bajoMarco);
        nombre.text = PlayerProfile.HasName ? PlayerProfile.Name : "Jugador";
        bool sinTitulo = string.IsNullOrEmpty(ti.id);
        titulo.text = sinTitulo ? "Sin título" : ti.nombre;
        titulo.color = sinTitulo ? Mute : Ink;
        var general = PerfilDatos.Estadisticas[0].datos;
        resumen[0].text = general[0].valor;
        resumen[1].text = general[1].valor;
        resumen[2].text = general[6].valor;
        resumen[3].text = PerfilDatos.Estadisticas[3].datos[1].valor;
        RefrescarInfo();
        RefrescarChips();
        RefrescarGrilla();
        RefrescarVista();
    }
}
