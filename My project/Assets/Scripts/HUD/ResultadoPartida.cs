using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using static ShopUIKit;

// Pantalla de resultado del Modo Táctico (US 034). La muestra RondasTacticas cuando un equipo llega a 7 rondas
// (o gana la muerte súbita).
// - CA1: la partida se congela: el jugador no se mueve, no mira ni dispara, y no se puede pausar.
// - CA2: "Victoria" o "Derrota" con el marcador final (tus rondas primero).
// - CA3: tabla con los dos equipos y, por jugador, bajas, muertes, plantadas y desactivaciones, ordenados por bajas.
// - CA4: botón "Volver al menú" (también con Enter): sale de la sala y carga el menú principal.
// - CA5: los nombres son los de Photon, que salen del perfil de cada jugador (US 164).
public class ResultadoPartida : MonoBehaviour
{
    private const string EscenaMenu = "MenuPrincipal";
    private const float TablaW = 920f, FilaH = 38f;
    private static readonly float[] Anchos = { 352f, 110f, 110f, 130f, 170f }; // suman 872 (la tabla menos los márgenes)
    private static readonly string[] Columnas = { "Jugador", "Bajas", "Muertes", "Plantadas", "Desactivaciones" };

    public static ResultadoPartida Actual { get; private set; }

    private TMP_FontAsset displayFont, labelFont;
    private Sprite rounded;
    private RectTransform tabla;
    private float proximaTabla;
    private readonly List<Behaviour> congelados = new List<Behaviour>();

    /// <summary>Muestra el resultado (una sola vez por partida).</summary>
    public static void Mostrar(RondasTacticas rondas)
    {
        if (Actual != null || rondas == null) return;
        var go = new GameObject("ResultadoPartida", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        Actual = go.AddComponent<ResultadoPartida>();
        Actual.Armar(rondas);
    }

    private void OnDestroy()
    {
        if (Actual == this) Actual = null;
    }

    // =====================================================================
    // Armado
    // =====================================================================

    private void Armar(RondasTacticas rondas)
    {
        MatchHud hud = MatchHud.Instance;
        displayFont = hud != null ? hud.DisplayFont : null;
        labelFont = hud != null ? hud.LabelFont : null;
        rounded = hud != null ? hud.Rounded : null;

        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200; // arriba del HUD, la tienda y los carteles
        var escala = GetComponent<UnityEngine.UI.CanvasScaler>();
        escala.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;

        RectTransform root = Node("Contenido", transform);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(1920f, 1080f);

        // Fondo oscuro que tapa el juego (y bloquea los clics a lo de abajo).
        RectTransform sombra = Node("Sombra", transform);
        sombra.SetAsFirstSibling();
        Stretch(sombra);
        Image(sombra, null, Rgb(6, 8, 12, DarkAlpha(0.8f)), 0f, true);

        // CA2: resultado.
        int mio = Mathf.Max(0, EquiposTacticos.Local), rival = 1 - mio;
        bool gane = rondas.Ganador == mio;
        Color color = gane ? MatchHud.TeamColor : MatchHud.RivalColor;

        Text(Place(Node("Antetitulo", root), 0f, 70f, 1920f, 30f), labelFont, 24f, Mute, TextAlignmentOptions.Center, 24f, true).text = "Fin de la partida";
        TextMeshProUGUI titulo = Text(Place(Node("Titulo", root), 0f, 100f, 1920f, 170f), displayFont, 150f, color, TextAlignmentOptions.Center, 6f, true);
        titulo.text = gane ? "Victoria" : "Derrota";
        Image(Place(Node("Linea", root), 870f, 280f, 180f, 4f), null, color);
        Text(Place(Node("Marcador", root), 0f, 300f, 1920f, 84f), displayFont, 72f, Ink, TextAlignmentOptions.Center).text =
            $"<color=#58A6FF>{rondas.RondasDe(mio)}</color>   <color=#8E96A3>–</color>   <color=#FF5C5C>{rondas.RondasDe(rival)}</color>";
        Text(Place(Node("Equipos", root), 0f, 398f, 1920f, 24f), labelFont, 20f, Mute, TextAlignmentOptions.Center, 16f, true).text =
            "<color=#58A6FF>Tu equipo</color>   ·   <color=#FF5C5C>Rivales</color>";

        // CA3: tabla (se arma de nuevo cada tanto, por si llegan estadísticas atrasadas).
        tabla = Place(Node("Tabla", root), (1920f - TablaW) / 2f, 450f, TablaW, 100f);
        float alto = DibujarTabla();

        // CA4: botón.
        RectTransform boton = Place(Node("VolverAlMenu", root), 830f, 450f + alto + 36f, 260f, 56f);
        UnityEngine.UI.Image fondo = Image(boton, rounded, Accent, 6f, true);
        Text(Stretch(Node("Texto", boton)), displayFont, 24f, AccentInk, TextAlignmentOptions.Center, 12f, true).text = "Volver al menú";
        ShopPointerTarget puntero = boton.gameObject.AddComponent<ShopPointerTarget>();
        puntero.Hovered = () => fondo.color = Hot;
        puntero.Exited = () => fondo.color = Accent;
        puntero.Clicked = VolverAlMenu;

        Congelar();
    }

    private float DibujarTabla()
    {
        for (int i = tabla.childCount - 1; i >= 0; i--) Destroy(tabla.GetChild(i).gameObject);

        int mio = Mathf.Max(0, EquiposTacticos.Local);
        List<Player> propios = Jugadores(mio), rivales = Jugadores(1 - mio);
        float alto = 18f + 30f + (22f + 6f + propios.Count * FilaH) + 14f + (22f + 6f + rivales.Count * FilaH) + 14f;
        tabla.sizeDelta = new Vector2(TablaW, alto);
        Image(Stretch(Node("Fondo", tabla)), rounded, Rgb(10, 12, 17, DarkAlpha(0.9f)), 10f);

        float y = 18f;
        Fila(y, Columnas, true, false, 0);
        y += 30f;
        y = Seccion("Tu equipo", MatchHud.TeamColor, propios, y) + 14f;
        Seccion("Rivales", MatchHud.RivalColor, rivales, y);
        return alto;
    }

    private float Seccion(string titulo, Color color, List<Player> jugadores, float y)
    {
        Text(Place(Node("Seccion", tabla), 24f, y + 6f, 400f, 22f), labelFont, 15f, color, TextAlignmentOptions.MidlineLeft, 16f, true).text = titulo;
        y += 28f;
        for (int i = 0; i < jugadores.Count; i++, y += FilaH)
        {
            Player p = jugadores[i];
            Fila(y, new[]
            {
                p.IsLocal ? $"{p.NickName} <color=#8E96A3>(vos)</color>" : p.NickName,
                Bajas(p).ToString(), Muertes(p).ToString(),
                Estadistica(p, RondasTacticas.PropPlantadas).ToString(),
                Estadistica(p, RondasTacticas.PropDesactivaciones).ToString()
            }, false, p.IsLocal, i);
        }
        return y;
    }

    private void Fila(float y, string[] celdas, bool encabezado, bool resaltada, int indice)
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
            float w = Anchos[c] - (c == 0 ? 10f : 0f);
            if (c == celdas.Length - 1) w -= 10f;
            var alineacion = c == 0 ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;
            TextMeshProUGUI t = encabezado
                ? Text(Place(Node("Columna", tabla), x, y, w, h), labelFont, 13f, Mute, alineacion, 12f, true)
                : Text(Place(Node("Celda", tabla), x, y, w, h), displayFont, c == 0 ? 20f : 21f, Ink, alineacion, c == 0 ? 2f : 0f);
            t.text = celdas[c];
            x += w;
        }
    }

    // Jugadores de un equipo, ordenados por bajas (CA3).
    private List<Player> Jugadores(int equipo)
    {
        var lista = new List<Player>();
        foreach (Player p in PhotonNetwork.PlayerList)
            if (EquiposTacticos.DeActor(p.ActorNumber) == equipo) lista.Add(p);
        lista.Sort((a, b) => Bajas(b).CompareTo(Bajas(a)));
        return lista;
    }

    private static int Bajas(Player p) => MatchHud.Instance != null ? MatchHud.Instance.KillsOf(p.NickName) : 0;
    private static int Muertes(Player p) => MatchHud.Instance != null ? MatchHud.Instance.DeathsOf(p.NickName) : 0;
    private static int Estadistica(Player p, string clave) =>
        p.CustomProperties.TryGetValue(clave, out object v) && v is int n ? n : 0;

    // =====================================================================
    // Cuadro a cuadro
    // =====================================================================

    private void Update()
    {
        // CA1: el mouse queda libre para el botón (por si algo lo vuelve a trabar).
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) VolverAlMenu();

        if (Time.unscaledTime >= proximaTabla)
        {
            proximaTabla = Time.unscaledTime + 1f;
            DibujarTabla();
        }
    }

    // CA1: nadie se mueve, mira ni dispara, y no se puede pausar.
    private void Congelar()
    {
        if (PauseMenu.Instance != null) PauseMenu.Instance.AllowPause = false;
        MatchHud.HideBanner();
        MatchHud.SetHint(null);

        JugadorEnRed local = PartidaEnRed.Actual != null ? PartidaEnRed.Actual.Local : null;
        if (local == null) return;
        foreach (Behaviour componente in local.GetComponentsInChildren<Behaviour>(true))
            if (componente.enabled && (componente is PlayerMovement || componente is CameraLook || componente is Pistola ||
                                       componente is Mitre || componente is ArmaDeFuego || componente is MeleeAttack ||
                                       componente is WeaponSwitcher || componente is PlayerAbility))
            {
                componente.enabled = false;
                congelados.Add(componente);
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
        SceneManager.LoadScene(EscenaMenu);
    }
}
