using System.Collections;
using System.Collections.Generic;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using static ShopUIKit;

// Pantalla de resultado del Deathmatch (US 142). La muestra PartidaDeathmatch cuando alguien llega a 25 bajas
// o se termina el tiempo.
// - CA1: la partida se congela (lo hace PartidaDeathmatch) y no se puede pausar.
// - CA2: a los 2 s aparece el puesto del jugador ("3.º puesto"), o "Victoria" si ganó.
// - CA3: tabla final con todos, por puesto: bajas, muertes y bajas de cabeza.
// - CA4: botón "Volver al menú" (también con Enter): sale de la sala y carga el menú principal.
public class ResultadoDeathmatch : MonoBehaviour
{
    private const string EscenaMenu = "MenuPrincipal";
    private const float Espera = 2f;
    private const float TablaW = 920f, TablaY = 384f, FilaH = 38f;
    private static readonly float[] Anchos = { 56f, 306f, 150f, 150f, 190f }; // suman 852 (la tabla menos márgenes)
    private static readonly string[] Columnas = { "#", "Jugador", "Bajas", "Muertes", "Bajas de cabeza" };

    public static ResultadoDeathmatch Actual { get; private set; }

    private TMP_FontAsset displayFont, labelFont;
    private Sprite rounded;
    private RectTransform tabla, boton;
    private TextMeshProUGUI titulo, detalle;
    private UnityEngine.UI.Image linea;
    private float proximaTabla;
    private bool armado;

    /// <summary>Muestra el resultado (una sola vez por partida), después de una pausa corta.</summary>
    public static void Mostrar()
    {
        if (Actual != null) return;
        var go = new GameObject("ResultadoDeathmatch", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        Actual = go.AddComponent<ResultadoDeathmatch>();
        Actual.StartCoroutine(Actual.Esperar());
    }

    private void OnDestroy()
    {
        if (Actual == this) Actual = null;
    }

    private IEnumerator Esperar()
    {
        // CA1: desde ya no se puede pausar; la pantalla aparece a los 2 s (CA2).
        if (PauseMenu.Instance != null) PauseMenu.Instance.AllowPause = false;
        GetComponent<Canvas>().enabled = false;
        yield return new WaitForSecondsRealtime(Espera);
        Armar();
    }

    // =====================================================================
    // Armado
    // =====================================================================

    private void Armar()
    {
        MatchHud hud = MatchHud.Instance;
        displayFont = hud != null ? hud.DisplayFont : null;
        labelFont = hud != null ? hud.LabelFont : null;
        rounded = hud != null ? hud.Rounded : null;
        MatchHud.HideBanner();
        MatchHud.SetHint(null);
        ShopUI.Cerrar();

        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200; // arriba del HUD, la tienda y los carteles
        canvas.enabled = true;
        var escala = GetComponent<UnityEngine.UI.CanvasScaler>();
        escala.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;

        // Fondo oscuro que tapa el juego (y bloquea los clics a lo de abajo).
        Image(Stretch(Node("Sombra", transform)), null, Rgb(6, 8, 12, 0.8f), 0f, true);

        RectTransform root = Node("Contenido", transform);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(1920f, 1080f);

        Text(Place(Node("Antetitulo", root), 0f, 70f, 1920f, 30f), labelFont, 24f, Mute, TextAlignmentOptions.Center, 16f).text =
            "FIN DE LA PARTIDA  ·  DEATHMATCH";
        titulo = Text(Place(Node("Titulo", root), 0f, 100f, 1920f, 170f), displayFont, 150f, Ink, TextAlignmentOptions.Center, 6f);
        linea = Image(Place(Node("Linea", root), 870f, 280f, 180f, 4f), null, Ink);
        detalle = Text(Place(Node("Detalle", root), 0f, 298f, 1920f, 48f), displayFont, 34f, Ink, TextAlignmentOptions.Center, 2f);

        tabla = Place(Node("Tabla", root), (1920f - TablaW) / 2f, TablaY, TablaW, 100f);

        boton = Place(Node("VolverAlMenu", root), 830f, TablaY + 136f, 260f, 56f);
        UnityEngine.UI.Image fondo = Image(boton, rounded, Accent, 6f, true);
        Text(Stretch(Node("Texto", boton)), displayFont, 24f, AccentInk, TextAlignmentOptions.Center, 12f).text = "VOLVER AL MENÚ";
        ShopPointerTarget puntero = boton.gameObject.AddComponent<ShopPointerTarget>();
        puntero.Hovered = () => fondo.color = Hot;
        puntero.Exited = () => fondo.color = Accent;
        puntero.Clicked = VolverAlMenu;

        armado = true;
        Dibujar();
    }

    // Se dibuja de nuevo cada tanto, por si llega atrasada la última baja o se va alguien.
    private void Dibujar()
    {
        List<Player> puestos = PartidaDeathmatch.Puestos();
        int ganador = PartidaDeathmatch.Actual != null ? PartidaDeathmatch.Actual.Ganador : 0;
        // El ganador que anotó el anfitrión va siempre primero, aunque un empate lo ordene distinto.
        int indice = puestos.FindIndex(p => p.ActorNumber == ganador);
        if (indice > 0)
        {
            Player p = puestos[indice];
            puestos.RemoveAt(indice);
            puestos.Insert(0, p);
        }
        Player primero = puestos.Count > 0 ? puestos[0] : null;
        int mio = puestos.FindIndex(p => p.IsLocal);

        // CA2: "Victoria" o el puesto.
        bool gane = mio == 0;
        Color color = gane ? Accent : Ink;
        titulo.text = gane ? "VICTORIA" : mio > 0 ? $"{mio + 1}.º PUESTO" : "FIN DE LA PARTIDA";
        titulo.color = color;
        linea.color = color;
        int bajas = primero != null ? PartidaDeathmatch.Bajas(primero.ActorNumber) : 0;
        bool porBajas = bajas >= PartidaDeathmatch.BajasParaGanar;
        string cuantas = bajas == 1 ? "1 baja" : $"{bajas} bajas";
        if (gane)
            detalle.text = porBajas ? $"Llegaste primero a <color=#F29A38>{cuantas}</color>"
                                    : $"Se acabó el tiempo: ganaste con <color=#F29A38>{cuantas}</color>";
        else if (primero != null)
            detalle.text = (porBajas ? "Ganó " : "Se acabó el tiempo: ganó ") + $"<color=#F29A38>{primero.NickName}</color> con {cuantas}";
        else detalle.text = "";

        // CA3: tabla.
        for (int i = tabla.childCount - 1; i >= 0; i--) Destroy(tabla.GetChild(i).gameObject);
        float alto = 18f + 30f + puestos.Count * FilaH + 14f;
        tabla.sizeDelta = new Vector2(TablaW, alto);
        Image(Stretch(Node("Fondo", tabla)), rounded, Rgb(10, 12, 17, 0.9f), 10f);

        float y = 18f;
        Fila(y, Columnas, true, false, 0, false);
        y += 30f;
        for (int i = 0; i < puestos.Count; i++, y += FilaH)
        {
            Player p = puestos[i];
            int actor = p.ActorNumber;
            Fila(y, new[]
            {
                $"{i + 1}.º",
                p.IsLocal ? $"{p.NickName} <color=#8E96A3>(vos)</color>" : p.NickName,
                PartidaDeathmatch.Bajas(actor).ToString(), PartidaDeathmatch.Muertes(actor).ToString(),
                PartidaDeathmatch.Cabezas(actor).ToString()
            }, false, p.IsLocal, i, i == 0);
        }

        boton.anchoredPosition = new Vector2(830f, -(TablaY + alto + 36f));
    }

    private void Fila(float y, string[] celdas, bool encabezado, bool resaltada, int indice, bool ganador)
    {
        float ancho = TablaW - 48f;
        if (encabezado)
            Image(Place(Node("Separador", tabla), 24f, y + 29f, ancho, 1f), null, White(0.08f));
        else if (resaltada)
        {
            Image(Place(Node("Resaltada", tabla), 24f, y, ancho, FilaH), rounded, Over(WithAlpha(Accent, 0.16f), Rgb(10, 12, 17)), 3f);
            Image(Place(Node("Borde", tabla), 24f, y, 3f, FilaH), null, Accent);
        }
        else if (indice % 2 == 0)
            Image(Place(Node("Franja", tabla), 24f, y, ancho, FilaH), rounded, White(0.035f), 3f);

        float x = 34f, h = encabezado ? 30f : FilaH;
        for (int c = 0; c < celdas.Length; c++)
        {
            float w = Anchos[c];
            var alineacion = c <= 1 ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;
            Color tinta = c == 0 ? (ganador ? Accent : Mute) : Ink;
            TextMeshProUGUI t = encabezado
                ? Text(Place(Node("Columna", tabla), x, y, w, h), labelFont, 13f, Mute, alineacion, 12f)
                : Text(Place(Node("Celda", tabla), x, y, w, h), displayFont, c == 1 ? 20f : 21f, tinta, alineacion, c == 1 ? 2f : 0f);
            t.text = encabezado ? celdas[c].ToUpperInvariant() : celdas[c];

            // Etiqueta "Ganador" al lado del nombre del primero.
            if (!encabezado && ganador && c == 1)
            {
                float nombre = t.GetPreferredValues(celdas[c], 4000f, 400f).x;
                RectTransform chip = Place(Node("Ganador", tabla), x + nombre + 10f, y + 9f, 10f, 20f);
                Image(chip, rounded, Accent, 3f);
                TextMeshProUGUI ct = Text(Stretch(Node("Texto", chip)), labelFont, 13f, AccentInk, TextAlignmentOptions.Center, 10f);
                ct.text = "GANADOR";
                chip.sizeDelta = new Vector2(Width(ct, "GANADOR") + 16f, 20f);
            }
            x += w;
        }
    }

    // =====================================================================
    // Cuadro a cuadro
    // =====================================================================

    private void Update()
    {
        if (!armado) return;
        // El mouse queda libre para el botón (por si algo lo vuelve a trabar).
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { VolverAlMenu(); return; }

        if (Time.unscaledTime >= proximaTabla)
        {
            proximaTabla = Time.unscaledTime + 1f;
            Dibujar();
        }
    }

    // CA4
    private void VolverAlMenu()
    {
        if (PauseMenu.Instance != null)
        {
            PauseMenu.Instance.AllowPause = true;
            PauseMenu.Instance.LeaveMatch(); // sale de la sala y carga el menú principal
            return;
        }
        Time.timeScale = 1f;
        Multijugador.SalirDeLaSala();
        PantallaDeCarga.Cargar(EscenaMenu, "", "Menú principal"); // US 196
    }
}
