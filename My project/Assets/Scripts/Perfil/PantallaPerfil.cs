using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static ShopUIKit;
using static PerfilUI;
using Img = UnityEngine.UI.Image;
using Tipo = PerfilDatos.Tipo;
using Cosmetico = PerfilDatos.Cosmetico;

// Perfil del jugador (menú principal). Arriba, la cabecera con el banner, el avatar, el nombre, el título y un
// resumen; abajo, tres pestañas: Estadísticas (US 221: las del jugador, F24, y las del minijuego, US 159), Personalización
// (avatares, banners, títulos y marcos) y Emotes (US 264: los bailes que se llevan en la rueda).
public class PantallaPerfil : PantallaBase
{
    private const float Y0 = 520f, AnchoGrilla = 1260f;

    private Img banner, avatar, marcoAvatar;
    private RectTransform cabecera, marcoCabecera, cajaBanner;
    private Img fondoCabecera, bordeCabecera;
    private Transform bajoMarco;
    private TextMeshProUGUI nombre, titulo, infoPestana;
    private readonly TextMeshProUGUI[] resumen = new TextMeshProUGUI[4];
    private readonly TextMeshProUGUI[] pestanas = new TextMeshProUGUI[3];
    private Img subrayado;
    private RectTransform panelEstad, panelPers, panelEmotes, grilla, vista;
    private RectTransform cuerpoMinijuego;
    private float anchoTarjeta;
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
        ArmarEmotes();
        ElegirPestana(0);
    }

    // ================= Cabecera =================

    private void ArmarCabecera()
    {
        RectTransform cab = cabecera = Place(Node("Cabecera", rect), 60f, 120f, 1800f, 300f);
        fondoCabecera = Image(cab, Rounded, PanelColor, 10f);
        cajaBanner = Place(Node("Banner", cab), 0f, 0f, 1800f, 300f);
        banner = Cubrir(cajaBanner, null);
        // Las sombras van dentro del banner: con marco se recortan con él.
        Image(Place(Node("Sombra", cajaBanner), 0f, 0f, 1150f, 300f), Degrade(true, 0.9f, 0f), Color.white);
        Image(Place(Node("SombraAbajo", cajaBanner), 0f, 140f, 1800f, 160f), Degrade(false, 0.55f, 0f), Color.white);
        Img borde = bordeCabecera = Borde(cab, 8f, White(0.08f));
        // El marco va sobre el banner, pero detrás del avatar, los textos y el botón.
        bajoMarco = borde != null ? borde.transform : cajaBanner;

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
        string[] nombres = { "Estadísticas", "Personalización", "Emotes" };
        float x = 60f;
        for (int i = 0; i < nombres.Length; i++)
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
        for (int k = 0; k < pestanas.Length; k++) pestanas[k].color = k == i ? Ink : Mute;
        RectTransform t = pestanas[i].rectTransform;
        subrayado.rectTransform.anchoredPosition = new Vector2(t.anchoredPosition.x, -490f);
        subrayado.rectTransform.sizeDelta = new Vector2(t.sizeDelta.x, 4f);
        panelEstad.gameObject.SetActive(i == 0);
        panelPers.gameObject.SetActive(i == 1);
        panelEmotes.gameObject.SetActive(i == 2);
        if (i == 2) VerEmote(emoteVisto); else VistaPreviaDeEmote.Apagar(); // la vista previa solo dibuja si se ve
        RefrescarInfo();
    }

    private void RefrescarInfo()
    {
        if (pestana == 0) infoPestana.text = $"Partidas terminadas  <color=#F3F4F6>{N(EstadisticasJugador.Datos.Partidas)}</color>";
        else if (pestana == 2)
        {
            int equipados = 0;
            for (int i = 0; i < EmotesEquipados.Espacios; i++) if (EmotesEquipados.En(i) >= 0) equipados++;
            infoPestana.text = $"En la rueda  <color=#F3F4F6>{equipados} / {EmotesEquipados.Espacios}</color>";
        }
        else
        {
            int tiene = PerfilDatos.Obtenidos(Tipo.Avatar) + PerfilDatos.Obtenidos(Tipo.Banner) + PerfilDatos.Obtenidos(Tipo.Titulo) + PerfilDatos.Obtenidos(Tipo.Marco);
            int total = PerfilDatos.Total(Tipo.Avatar) + PerfilDatos.Total(Tipo.Banner) + PerfilDatos.Total(Tipo.Titulo) + PerfilDatos.Total(Tipo.Marco);
            infoPestana.text = $"Colección  <color=#F3F4F6>{tiene} / {total}</color>";
        }
    }

    // ================= Estadísticas =================

    private const string SinDatos = "Sin datos";
    private static readonly System.Globalization.CultureInfo Cultura = System.Globalization.CultureInfo.InvariantCulture;

    private void ArmarEstadisticas()
    {
        panelEstad = Place(Node("Estadisticas", rect), 60f, Y0, 1800f, 540f);
    }

    // US 221: se arma de nuevo cada vez que se abre la pantalla o cambian los datos (CA5), así refleja la última partida.
    // Solo lee: abrirla no suma nada.
    private void RefrescarEstadisticas()
    {
        BorrarHijos(panelEstad);
        EstadisticasJugador.Totales e = EstadisticasJugador.Datos;

        // General: Táctico y Deathmatch juntos (CA1). Sin partidas, los contadores en 0 y los indicadores "Sin datos" (CA4).
        RectTransform general = Place(Node("General", panelEstad), 0f, 0f, 1800f, 168f);
        Image(general, Rounded, PanelColor, 8f);
        Borde(general, 8f, White(0.06f));
        T(general, "Rotulo", 28f, 16f, 700f, 22f, FRotulo, 18f, Accent, "General · Táctico y Deathmatch", TextAlignmentOptions.MidlineLeft, 16f, true);
        if (e.SinDatos)
            T(general, "SinPartidas", 900f, 16f, 872f, 22f, FTexto, 18f, Mute, "Todavía no terminaste ninguna partida de Táctico o Deathmatch.", TextAlignmentOptions.MidlineRight);
        (string nombre, string valor)[] datos =
        {
            ("Partidas", N(e.Partidas)),
            ("Victorias", N(e.Victorias)),
            ("% de victorias", Porcentaje(e.Victorias, e.Partidas)),
            ("Bajas", N(e.Bajas)),
            ("Muertes", N(e.Muertes)),
            ("Asistencias", N(e.Asistencias)),
            ("KDA*", e.SinDatos ? SinDatos : Kda(e)),
            ("Tiros a la cabeza", Porcentaje(e.Cabezas, e.Bajas)),
            ("Horas jugadas", Horas(e.Segundos)),
        };
        float paso = 1744f / datos.Length;
        for (int i = 0; i < datos.Length; i++)
        {
            bool vacio = datos[i].valor == SinDatos;
            T(general, "Valor" + i, 28f + i * paso, 48f, paso - 10f, 58f, FTitulo, vacio ? 30f : 54f, vacio ? Mute : Ink, datos[i].valor);
            T(general, "Nombre" + i, 30f + i * paso, 112f, paso - 10f, 22f, FRotulo, 15f, Mute, datos[i].nombre, TextAlignmentOptions.MidlineLeft, 12f, true);
        }

        // Un panel por modo (CA2). El del minijuego lo llena RefrescarMinijuego (CA3).
        float w = anchoTarjeta = (1800f - 3f * 20f) / 4f;
        Tarjeta(0, "Táctico", "#F29A38", e.tactico.partidas == 0 ? "Todavía no terminaste ninguna partida de Táctico." : null, new[]
        {
            ("Partidas", N(e.tactico.partidas)),
            ("Victorias", N(e.tactico.victorias)),
            ("Rondas ganadas", N(e.rondasGanadas)),
            ("Plantados", N(e.plantados)),
            ("Desactivados", N(e.desactivados)),
            ("MVP", N(e.mvp)),
        });
        Tarjeta(1, "Deathmatch", "#FF4B4B", e.deathmatch.partidas == 0 ? "Todavía no terminaste ninguna partida de Deathmatch." : null, new[]
        {
            ("Partidas", N(e.deathmatch.partidas)),
            ("Primer puesto", N(e.deathmatch.victorias)),
            ("Bajas", N(e.deathmatch.bajas)),
            ("Mejor racha", N(e.mejorRacha)),
        });
        // El Zombie todavía no tiene final: se va a registrar con la US 151.
        Tarjeta(2, "Zombie", "#7DE05A", SinDatos + ". Las partidas Zombie se van a registrar cuando el modo tenga final.", null);
        RectTransform minijuego = Tarjeta(3, "Minijuego: cometas", "#4CC3FF", null, null);
        cuerpoMinijuego = Place(Node("Datos", minijuego), 0f, 60f, w, 236f);
        RefrescarMinijuego();

        TextMeshProUGUI nota = T(panelEstad, "Nota", 0f, 504f, 1800f, 26f, FTexto, 18f, Mute,
            "* KDA = (bajas + asistencias) ÷ muertes; sin muertes, ÷ 1. Asistencia: dañaste a un rival que otro eliminó en los 10 s siguientes. " +
            "Cuentan las partidas terminadas.");
        nota.textWrappingMode = TextWrappingModes.Normal;
    }

    // Tarjeta de un modo: con las filas o, si no hay datos, con el mensaje (CA4).
    private RectTransform Tarjeta(int columna, string nombre, string color, string vacio, (string nombre, string valor)[] filas)
    {
        float w = anchoTarjeta;
        ColorUtility.TryParseHtmlString(color, out Color c);
        RectTransform tarjeta = Place(Node(nombre, panelEstad), columna * (w + 20f), 188f, w, 300f);
        Image(tarjeta, Rounded, PanelColor, 8f);
        Borde(tarjeta, 8f, White(0.06f));
        Image(Place(Node("Color", tarjeta), 0f, 0f, w, 4f), null, c);
        T(tarjeta, "Titulo", 24f, 16f, w - 48f, 40f, FTitulo, 34f, c, nombre, TextAlignmentOptions.MidlineLeft, 4f, true);
        if (vacio != null)
        {
            TextMeshProUGUI t = T(tarjeta, "SinDatos", 24f, 80f, w - 48f, 120f, FTexto, 22f, Mute, vacio, TextAlignmentOptions.TopLeft);
            t.textWrappingMode = TextWrappingModes.Normal;
            return tarjeta;
        }
        if (filas == null) return tarjeta;
        for (int j = 0; j < filas.Length; j++)
        {
            float y = 66f + j * 37f;
            T(tarjeta, "Dato" + j, 24f, y, w - 140f, 32f, FTexto, 21f, Soft, filas[j].nombre);
            T(tarjeta, "Valor" + j, w - 164f, y, 140f, 32f, FTitulo, 28f, Ink, filas[j].valor, TextAlignmentOptions.MidlineRight);
            if (j < filas.Length - 1) Image(Place(Node("Linea" + j, tarjeta), 24f, y + 34f, w - 48f, 1f), null, White(0.06f));
        }
        return tarjeta;
    }

    private static string N(long n) => PerfilDatos.Numero((int)Mathf.Min(n, int.MaxValue));
    private static string Porcentaje(int parte, int total) => total > 0 ? Mathf.RoundToInt(100f * parte / total) + " %" : SinDatos;
    // US 223, CA4: se calcula con los totales y solo se redondea para mostrarlo ("1,79").
    private static string Kda(EstadisticasJugador.Totales e) => e.Kda.ToString("0.00", Cultura).Replace(".", ",");
    private static string Horas(long segundos)
    {
        if (segundos < 3600) return (segundos / 60) + " min";
        float h = segundos / 3600f;
        return h < 10f ? h.ToString("0.0", Cultura).Replace(".", ",") + " h" : Mathf.FloorToInt(h) + " h";
    }

    // US 159, CA3 y CA4: lo guardado del minijuego de cometas (EstadisticasMinijuego).
    private void RefrescarMinijuego()
    {
        if (cuerpoMinijuego == null) return;
        BorrarHijos(cuerpoMinijuego);
        float w = anchoTarjeta;
        EstadisticasMinijuego.Totales m = EstadisticasMinijuego.Datos;
        if (m.SinDatos)
        {
            TextMeshProUGUI vacio = T(cuerpoMinijuego, "SinDatos", 24f, 20f, w - 48f, 120f, FTexto, 22f, Mute,
                "Todavía no le disparaste a ningún cometa.", TextAlignmentOptions.TopLeft);
            vacio.textWrappingMode = TextWrappingModes.Normal;
            return;
        }
        string Pintado(TipoCometa tipo, int n) => $"<color=#{ColorUtility.ToHtmlStringRGB(Cometa.ColorDe(tipo))}>{PerfilDatos.Numero(n)}</color>";
        string Segundos(float s) => s.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',') + " s";
        (string nombre, string valor)[] filas =
        {
            ("Destruidos", PerfilDatos.Numero(m.Aciertos)),
            ("Azules · verdes · dorados", $"{Pintado(TipoCometa.Comun, m.aciertos[0])} · {Pintado(TipoCometa.Raro, m.aciertos[1])} · {Pintado(TipoCometa.Epico, m.aciertos[2])}"),
            ("Precisión", Mathf.RoundToInt(m.Precision * 100f) + " %"),
            ("Puntos", PerfilDatos.Numero(m.puntos)),
            ("Reacción promedio", m.Aciertos > 0 ? Segundos(m.ReaccionPromedio) : "—"),
            ("Récord en una fase", $"{m.recordCometas} · {PerfilDatos.Numero(m.recordPuntos)} pts"),
            ("Mejor reacción", m.mejorReaccionMs > 0 ? Segundos(m.mejorReaccionMs / 1000f) : "—"),
        };
        for (int j = 0; j < filas.Length; j++)
        {
            float y = 4f + j * 32f;
            T(cuerpoMinijuego, "Dato" + j, 24f, y, w - 220f, 30f, FTexto, 19f, Soft, filas[j].nombre);
            T(cuerpoMinijuego, "Valor" + j, w - 224f, y, 200f, 30f, FTitulo, 24f, Ink, filas[j].valor, TextAlignmentOptions.MidlineRight);
            if (j < filas.Length - 1) Image(Place(Node("Linea" + j, cuerpoMinijuego), 24f, y + 31f, w - 48f, 1f), null, White(0.06f));
        }
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
        bool nuevo = PerfilDatos.EsNuevo(c);
        const float AnchoNuevo = 64f;
        Color colorR = ColorRareza(c.rareza);
        RectTransform carta = Place(Node(c.nombre, grilla), x, y, w, h);
        Image(carta, Rounded, PanelColor, 8f, true);

        float textoY;
        if (c.tipo == Tipo.Titulo)
        {
            Image(Place(Node("Rareza", carta), 0f, 0f, 6f, h), null, colorR);
            // Con la etiqueta "Nuevo" arriba a la derecha, el nombre termina antes para no quedar debajo.
            TextMeshProUGUI t = T(carta, "Nombre", 22f, 4f, w - 40f - (nuevo ? AnchoNuevo : 0f), 42f, FTitulo, 27f, tiene ? Ink : Mute, c.nombre);
            t.overflowMode = TextOverflowModes.Ellipsis;
            textoY = 44f;
            if (!tiene) Candado(carta, w - 26f, 26f, 22f, Mute);
        }
        else
        {
            float ih = c.tipo == Tipo.Avatar ? w : c.tipo == Tipo.Marco ? w / 5.4f : w / 3f;
            RectTransform caja = Place(Node("Imagen", carta), 0f, 0f, w, ih);
            // Los marcos se muestran sobre el banner equipado, como quedan en la cabecera: el banner llega hasta el
            // borde interior del riel.
            Vector4 d = c.tipo == Tipo.Marco ? InteriorMarco(c, ih, true) : Vector4.zero;
            RectTransform arte = c.tipo == Tipo.Marco ? Place(Node("Arte", caja), d.x, d.y, w - d.x - d.z, ih - d.y - d.w) : caja;
            Img img = Cubrir(arte, PerfilDatos.Imagen(c.tipo == Tipo.Marco ? PerfilDatos.Banner : c));
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

        if (nuevo)
        {
            RectTransform etiqueta = Place(Node("Nuevo", carta), w - AnchoNuevo - 8f, 8f, AnchoNuevo, 24f);
            Image(etiqueta, Rounded, Accent, 5f);
            T(etiqueta, "T", 0f, 0f, AnchoNuevo, 24f, FRotulo, 14f, AccentInk, "Nuevo", TextAlignmentOptions.Center, 12f, true);
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
            RectTransform cajaMini = Place(Node("Banner", mini), 0f, 0f, 452f, 151f);
            Img bannerMini = Cubrir(cajaMini, PerfilDatos.Imagen(c.tipo == Tipo.Banner ? c : PerfilDatos.Banner));
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
            Cosmetico marcoMini = c.tipo == Tipo.Marco ? c : PerfilDatos.Marco;
            Enmarcar(ref mm, mini, 0f, 0f, 452f, 151f, marcoMini, false, sombraMini.transform);
            // Con marco, el banner y su sombra terminan en el borde interior del riel, como en la cabecera.
            Vector4 dm = InteriorMarco(marcoMini, 151f);
            Place(cajaMini, dm.x, dm.y, 452f - dm.x - dm.z, 151f - dm.y - dm.w);
            AjustarCubrir(bannerMini, cajaMini);
            Place(sombraMini.rectTransform, dm.x, dm.y, 300f - dm.x, 151f - dm.y - dm.w);
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

    // ================= Emotes (US 264) =================
    // Arriba, los 4 espacios de la rueda, en el mismo orden (arriba, derecha, abajo, izquierda); abajo, todos los bailes;
    // a la derecha, el personaje elegido haciendo el que se mira (CA2). Se elige un espacio y después un emote para
    // ponerlo ahí (CA3: si ya estaba en otro espacio, sale de ese); "Quitar" vacía un espacio (CA4). Se guarda con el
    // perfil (CA5, EmotesEquipados).

    private static readonly string[] LugaresDeLaRueda = { "Arriba", "Derecha", "Abajo", "Izquierda" };
    private int espacioElegido = 0;
    private int emoteVisto = -1;
    private RectTransform espaciosEmotes, grillaEmotes;
    private UnityEngine.UI.RawImage vistaEmote;
    private TextMeshProUGUI nombreVisto, ayudaVista, personajeVisto;
    private int pjVista = -1; // el personaje de la vista previa (índice en CharacterRoster.All); -1: el elegido

    private void ArmarEmotes()
    {
        panelEmotes = Place(Node("Emotes", rect), 60f, Y0, 1800f, 540f);
        espaciosEmotes = Place(Node("Espacios", panelEmotes), 0f, 0f, AnchoGrilla, 124f);
        T(panelEmotes, "Ayuda", 0f, 136f, AnchoGrilla, 26f, FTexto, 19f, Mute,
            "Elegí un espacio de la rueda y después el baile que va ahí. Mantené T en la partida para abrir la rueda.");
        Scroll(panelEmotes, 0f, 172f, AnchoGrilla, 368f, out grillaEmotes);

        RectTransform v = Place(Node("Vista", panelEmotes), 1300f, 0f, 500f, 540f);
        Image(Stretch(Node("Fondo", v)), Rounded, PanelColor, 8f);
        Borde(Stretch(Node("Marco", v)), 8f, White(0.08f));
        T(v, "Rotulo", 24f, 18f, 240f, 22f, FRotulo, 17f, Accent, "Vista previa", TextAlignmentOptions.MidlineLeft, 16f, true);
        // Con qué personaje se ve el baile (solo la vista previa: no cambia el personaje elegido).
        CrearBoton(v, 280f, 12f, 36f, 34f, "<", false, () => CambiarPersonajeVisto(-1), 22f);
        personajeVisto = T(v, "PersonajeVisto", 318f, 12f, 120f, 34f, FRotulo, 17f, Ink, "", TextAlignmentOptions.Center, 8f, true);
        personajeVisto.overflowMode = TextOverflowModes.Ellipsis;
        CrearBoton(v, 440f, 12f, 36f, 34f, ">", false, () => CambiarPersonajeVisto(1), 22f);
        RectTransform caja = Place(Node("Personaje", v), 62f, 50f, 376f, 401f);
        vistaEmote = caja.gameObject.AddComponent<UnityEngine.UI.RawImage>();
        vistaEmote.raycastTarget = false;
        nombreVisto = T(v, "Nombre", 24f, 452f, 452f, 50f, FTitulo, 40f, Ink, "", TextAlignmentOptions.Center, 3f, true);
        ayudaVista = T(v, "Detalle", 24f, 500f, 452f, 26f, FTexto, 18f, Mute, "", TextAlignmentOptions.Center);
    }

    private void RefrescarEmotes()
    {
        if (panelEmotes == null) return;
        CatalogoDeEmotes catalogo = CatalogoDeEmotes.Actual;

        // Los 4 espacios.
        BorrarHijos(espaciosEmotes);
        float w = (AnchoGrilla - 3f * 18f) / EmotesEquipados.Espacios;
        for (int i = 0; i < EmotesEquipados.Espacios; i++)
        {
            int k = i;
            int emote = EmotesEquipados.En(i);
            CatalogoDeEmotes.Emote e = catalogo.De(emote);
            bool elegido = espacioElegido == i;
            RectTransform carta = Place(Node("Espacio" + (i + 1), espaciosEmotes), i * (w + 18f), 0f, w, 124f);
            Image(carta, Rounded, elegido ? SelectedColor : PanelColor, 8f, true);
            T(carta, "Lugar", 18f, 12f, w - 36f, 20f, FRotulo, 15f, elegido ? Accent : Mute,
                $"Espacio {i + 1} · {LugaresDeLaRueda[i]}", TextAlignmentOptions.MidlineLeft, 14f, true);
            if (e != null && e.icono != null)
                Image(Place(Node("Icono", carta), 18f, 42f, 64f, 64f), e.icono, Color.white).preserveAspect = true;
            float tx = e != null && e.icono != null ? 96f : 18f;
            TextMeshProUGUI n = T(carta, "Nombre", tx, 44f, w - tx - 130f, 60f, FTitulo, 32f, e != null ? Ink : Mute,
                e != null ? e.nombre : "Vacío", TextAlignmentOptions.MidlineLeft, 3f, true);
            n.overflowMode = TextOverflowModes.Ellipsis;
            Img borde = Borde(carta, 8f, elegido ? Accent : White(0.08f));
            var p = Clickeable(carta, () =>
            {
                espacioElegido = k;
                if (EmotesEquipados.En(k) >= 0) emoteVisto = EmotesEquipados.En(k);
                Refrescar();
            });
            p.Hovered = () => { if (borde != null && espacioElegido != k) borde.color = White(0.35f); };
            p.Exited = () => { if (borde != null && espacioElegido != k) borde.color = White(0.08f); };
            // CA4: quitar el emote del espacio.
            if (e != null)
                CrearBoton(carta, w - 118f, 64f, 100f, 40f, "Quitar", false, () =>
                {
                    EmotesEquipados.Poner(k, -1);
                    menu.Avisar($"Espacio {k + 1} vacío");
                    Refrescar();
                }, 18f);
        }

        // Todos los bailes.
        BorrarHijos(grillaEmotes);
        const int Columnas = 4;
        const float Gap = 18f, Alto = 104f;
        float cw = (AnchoGrilla - (Columnas - 1) * Gap) / Columnas;
        for (int i = 0; i < catalogo.emotes.Count; i++)
        {
            CatalogoDeEmotes.Emote e = catalogo.emotes[i];
            if (e == null) continue;
            int k = i;
            int espacio = EspacioDe(i);
            bool visto = emoteVisto == i;
            RectTransform carta = Place(Node(e.nombre, grillaEmotes), (i % Columnas) * (cw + Gap), (i / Columnas) * (Alto + Gap), cw, Alto);
            Image(carta, Rounded, PanelColor, 8f, true);
            if (e.icono != null) Image(Place(Node("Icono", carta), 16f, 16f, 72f, 72f), e.icono, Color.white).preserveAspect = true;
            else
            {
                // Sin ícono: la inicial, en un cuadrado.
                RectTransform ini = Place(Node("Inicial", carta), 16f, 16f, 72f, 72f);
                Image(ini, Rounded, ChipColor, 6f);
                T(ini, "Letra", 0f, 0f, 72f, 72f, FTitulo, 40f, Soft, e.nombre.Length > 0 ? e.nombre.Substring(0, 1) : "?", TextAlignmentOptions.Center);
            }
            TextMeshProUGUI n = T(carta, "Nombre", 104f, 18f, cw - 120f, 40f, FTitulo, 30f, Ink, e.nombre, TextAlignmentOptions.MidlineLeft, 3f, true);
            n.overflowMode = TextOverflowModes.Ellipsis;
            T(carta, "Estado", 104f, 60f, cw - 120f, 24f, FRotulo, 16f, espacio >= 0 ? Ok : Mute,
                espacio >= 0 ? $"En el espacio {espacio + 1}" : "Sin equipar", TextAlignmentOptions.MidlineLeft, 12f, true);
            Img borde = Borde(carta, 8f, visto ? Accent : White(0.08f));
            var p = Clickeable(carta, () =>
            {
                // CA3: va al espacio elegido.
                emoteVisto = k;
                if (espacioElegido >= 0)
                {
                    EmotesEquipados.Poner(espacioElegido, k);
                    menu.Avisar($"{e.nombre} en el espacio {espacioElegido + 1} ({LugaresDeLaRueda[espacioElegido].ToLowerInvariant()})");
                }
                Refrescar();
            });
            // CA2: pasando el mouse se ve en la vista previa.
            p.Hovered = () => { VerEmote(k); if (borde != null && emoteVisto != k) borde.color = White(0.35f); };
            p.Exited = () => { VerEmote(emoteVisto); if (borde != null && emoteVisto != k) borde.color = White(0.08f); };
        }
        int filas = (catalogo.emotes.Count + Columnas - 1) / Columnas;
        grillaEmotes.sizeDelta = new Vector2(AnchoGrilla, Mathf.Max(368f, filas * (Alto + Gap)));
        if (pestana == 2) VerEmote(emoteVisto);
    }

    private static int EspacioDe(int emote)
    {
        for (int i = 0; i < EmotesEquipados.Espacios; i++) if (EmotesEquipados.En(i) == emote) return i;
        return -1;
    }

    // La vista previa: un personaje (de entrada, el elegido; con las flechas, cualquiera) haciendo ese emote (o quieto,
    // sin ninguno).
    private void VerEmote(int emote)
    {
        if (vistaEmote == null) return;
        CatalogoDeEmotes.Emote e = CatalogoDeEmotes.Actual.De(emote);
        CharacterData pj = PersonajeDeLaVista();
        VistaPreviaDeEmote.Mostrar(pj, e != null ? e.clip : null);
        vistaEmote.texture = VistaPreviaDeEmote.Textura;
        nombreVisto.text = e != null ? e.nombre : "";
        if (personajeVisto != null) personajeVisto.text = pj != null ? pj.displayName : "";
        ayudaVista.text = e == null ? "Pasá el mouse por un baile para verlo." : pj != null ? pj.role : "";
    }

    private CharacterData PersonajeDeLaVista()
    {
        var todos = CharacterRoster.All;
        if (todos.Count == 0) return null;
        if (pjVista < 0 || pjVista >= todos.Count) pjVista = Mathf.Max(0, CharacterRoster.SelectedIndex);
        return todos[pjVista];
    }

    private void CambiarPersonajeVisto(int paso)
    {
        int n = CharacterRoster.All.Count;
        if (n == 0) return;
        PersonajeDeLaVista();
        pjVista = (pjVista + paso + n) % n;
        VerEmote(emoteVisto);
    }

    // ================= Datos =================

    public override void Refrescar()
    {
        base.Refrescar();
        PerfilDatos.Cosmetico av = PerfilDatos.Avatar, bn = PerfilDatos.Banner, ti = PerfilDatos.Titulo;
        banner.sprite = PerfilDatos.Imagen(bn);
        avatar.sprite = PerfilDatos.Imagen(av);
        AjustarCubrir(avatar, (RectTransform)avatar.rectTransform.parent);
        marcoAvatar.color = Ink;
        Enmarcar(ref marcoCabecera, cabecera, 0f, 0f, 1800f, 300f, PerfilDatos.Marco, false, bajoMarco);
        // Con marco, el banner llega hasta el borde interior del riel y la cabecera no tiene fondo ni contorno propios:
        // así nada asoma por fuera del marco (en las esquinas redondeadas, por ejemplo).
        Vector4 d = InteriorMarco(PerfilDatos.Marco, 300f);
        bool conMarco = d != Vector4.zero;
        Place(cajaBanner, d.x, d.y, 1800f - d.x - d.z, 300f - d.y - d.w);
        AjustarCubrir(banner, cajaBanner);
        fondoCabecera.enabled = !conMarco;
        if (bordeCabecera != null) bordeCabecera.enabled = !conMarco;
        nombre.text = PlayerProfile.HasName ? PlayerProfile.Name : "Jugador";
        bool sinTitulo = string.IsNullOrEmpty(ti.id);
        titulo.text = sinTitulo ? "Sin título" : ti.nombre;
        titulo.color = sinTitulo ? Mute : Ink;
        EstadisticasJugador.Totales e = EstadisticasJugador.Datos;
        resumen[0].text = N(e.Partidas);
        resumen[1].text = N(e.Victorias);
        resumen[2].text = e.SinDatos ? "—" : Kda(e);
        resumen[3].text = "—"; // el Zombie todavía no registra la oleada récord (US 151)
        RefrescarEstadisticas();
        RefrescarInfo();
        RefrescarChips();
        RefrescarGrilla();
        RefrescarVista();
        RefrescarEmotes();
    }
}
