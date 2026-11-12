using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using static ShopUIKit;

// F29 (Modo Invasión+), US 361: las cartas que tiene el jugador. Es la misma pantalla para verlas durante la partida y
// para descartar una desde la elección del puesto (US 360, CA5). La agrega PartidaDeInvasion.
// - CA1: mientras se mantiene la C (GameAction.Cartas, se cambia en Controles) se ven; al soltarla se cierra. No pausa,
//   como el marcador con Tab. También estando caído (US 357).
// - CA2: los 8 lugares, los ocupados con su carta y los libres vacíos, y arriba cuántos hay ocupados ("6 / 8").
// - CA3: cada carta con su nombre, su rareza (con su color), con qué rinde, lo que hace ahora (el texto de la 2.ª o 3.ª
//   vez si la tiene repetida) y cuántas veces la tiene de las que puede acumular ("2 de 3").
// - CA4: si no rinde con las armas que tiene, o la deja sin uso otra carta (Alimentación directa), se ve apagada con
//   "Sin uso" y el motivo.
// - CA5: desde la elección, "Descartar una carta" la abre para elegir: clic en una carta, confirmar ("¿Descartar…?") y
//   listo. Con "Volver" o Esc se vuelve a la elección sin descartar.
public class MisCartasInvasion : MonoBehaviour
{
    private const float AnchoCarta = 330f, AltoCarta = 300f, Separacion = 24f;

    /// <summary>Está abierta para descartar (la elección espera).</summary>
    public static bool Abierta { get; private set; }

    private static MisCartasInvasion actual;
    private System.Action<CartaDeInvasion> alDescartar;
    private CartaDeInvasion aConfirmar;

    private RectTransform lienzo, grilla, confirmar;
    private TextMeshProUGUI titulo, ocupados, pie, pregunta;
    private RectTransform volver;
    private TMP_FontAsset display, label;
    private Sprite redondeado;
    private bool viendo;
    private int firma = -1; // para no rearmar la grilla si no cambió nada
    private int devolverPausa = -1;

    private void OnEnable() => actual = this;

    private void OnDisable()
    {
        if (actual == this) actual = null;
        if (Abierta) CerrarDescarte();
        if (devolverPausa >= 0 && PauseMenu.Instance != null) PauseMenu.Instance.AllowPause = true;
        devolverPausa = -1;
    }

    private void OnDestroy()
    {
        if (lienzo != null) Destroy(lienzo.gameObject);
    }

    /// <summary>US 360, CA5: la abre la elección para elegir cuál se descarta. alDescartar recibe la carta confirmada.</summary>
    public static void AbrirParaDescartar(System.Action<CartaDeInvasion> alDescartar)
    {
        if (actual == null || MatchHud.Instance == null) return;
        actual.Armar();
        actual.alDescartar = alDescartar;
        Abierta = true;
        actual.aConfirmar = null;
        actual.firma = -1;
        if (PauseMenu.Instance != null) PauseMenu.Instance.AllowPause = false; // Esc es de esta pantalla
        actual.Mostrar(true);
    }

    private void CerrarDescarte()
    {
        Abierta = false;
        alDescartar = null;
        aConfirmar = null;
        devolverPausa = Time.frameCount; // el Esc que la cerró no tiene que abrir la pausa en el mismo cuadro
        if (lienzo != null) lienzo.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (devolverPausa >= 0 && !Abierta && Time.frameCount > devolverPausa)
        {
            devolverPausa = -1;
            if (PauseMenu.Instance != null) PauseMenu.Instance.AllowPause = true;
        }
        if (Abierta)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (aConfirmar != null) Cancelar();
                else CerrarDescarte();
            }
            else Refrescar(true);
            return;
        }

        // CA1: mientras se mantiene la C (sin la pausa, la tienda, el resultado ni la elección abiertos).
        bool ver = KeyBindings.Held(GameAction.Cartas) && !PauseMenu.IsPaused && !ShopUI.IsOpen && !EleccionDeInvasion.Abierta
                   && ResultadoDeInvasion.Actual == null && MatchHud.Instance != null && !CharacterSelectScreen.IsOpen;
        if (ver && !viendo) { Armar(); firma = -1; Mostrar(false); }
        else if (!ver && viendo) { viendo = false; lienzo.gameObject.SetActive(false); }
        if (viendo) Refrescar(false);
    }

    // ---------- Pantalla ----------

    private void Armar()
    {
        if (lienzo != null) return;
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        display = MatchHud.Instance.DisplayFont;
        label = MatchHud.Instance.LabelFont;
        redondeado = MatchHud.Instance.Rounded;

        var go = new GameObject("Tus cartas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        go.layer = 5;
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 49; // sobre la elección (48), debajo de la tienda (50) y la pausa
        var escala = go.GetComponent<UnityEngine.UI.CanvasScaler>();
        escala.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        lienzo = (RectTransform)go.transform;
        Image(Stretch(Node("Fondo", lienzo)), null, new Color(0f, 0f, 0f, DarkAlpha(0.78f)), 0f, true);

        RectTransform cabeza = Node("Titulo", lienzo);
        cabeza.anchorMin = cabeza.anchorMax = cabeza.pivot = new Vector2(0.5f, 1f);
        cabeza.anchoredPosition = new Vector2(0f, -110f);
        cabeza.sizeDelta = new Vector2(1500f, 110f);
        ocupados = Text(Place(Node("Ocupados", cabeza), 0f, 0f, 1500f, 26f), label, 20f, MarcadorInvasion.Verde, TextAlignmentOptions.Center, 16f, true);
        titulo = Text(Place(Node("Nombre", cabeza), 0f, 30f, 1500f, 64f), display, 58f, Ink, TextAlignmentOptions.Center, 4f, true);

        grilla = Node("Cartas", lienzo);
        grilla.anchorMin = grilla.anchorMax = grilla.pivot = new Vector2(0.5f, 0.5f);
        grilla.anchoredPosition = new Vector2(0f, -30f);
        grilla.sizeDelta = new Vector2(AnchoCarta * 4f + Separacion * 3f, AltoCarta * 2f + Separacion);

        pie = Text(Node("Pie", lienzo), label, 18f, Mute, TextAlignmentOptions.Center, 8f, true);
        RectTransform pr = pie.rectTransform;
        pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 0f);
        pr.anchoredPosition = new Vector2(0f, 60f);
        pr.sizeDelta = new Vector2(1200f, 26f);

        volver = Boton(lienzo, "Volver", L10n.T("gameplay.card.back"), new Vector2(0f, 100f), new Vector2(300f, 52f), CerrarDescarte, ChipColor, Ink);
        ArmarConfirmar();
        lienzo.gameObject.SetActive(false);
    }

    private RectTransform Boton(RectTransform padre, string nombre, string texto, Vector2 lugar, Vector2 tamanio,
        UnityEngine.Events.UnityAction alHacerClic, Color fondo, Color tinta)
    {
        RectTransform rect = Node(nombre, padre);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = lugar;
        rect.sizeDelta = tamanio;
        UnityEngine.UI.Image imagen = Image(rect, redondeado, fondo, 8f, true);
        var boton = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        boton.targetGraphic = imagen;
        boton.onClick.AddListener(alHacerClic);
        Text(Stretch(Node("Texto", rect)), label, 20f, tinta, TextAlignmentOptions.Center, 8f, true).text = texto;
        return rect;
    }

    // CA5: "¿Descartar…?" con Descartar y Cancelar.
    private void ArmarConfirmar()
    {
        confirmar = Node("Confirmar", lienzo);
        confirmar.anchorMin = confirmar.anchorMax = confirmar.pivot = new Vector2(0.5f, 0.5f);
        confirmar.sizeDelta = new Vector2(760f, 260f);
        MarcadorInvasion.Figura<FormaRedondeada>(Stretch(Node("Fondo", confirmar))).Pintar(Rgb(12, 14, 19, 0.98f), Bad, 16f, 3f);
        Image(Stretch(Node("Bloqueo", confirmar)), null, new Color(0f, 0f, 0f, 0f), 0f, true); // que el clic no pase a las cartas
        pregunta = Text(Place(Node("Pregunta", confirmar), 40f, 34f, 680f, 110f), label, 28f, Ink, TextAlignmentOptions.Center);
        pregunta.textWrappingMode = TextWrappingModes.Normal;
        RectTransform si = Boton(confirmar, "Descartar", L10n.T("gameplay.card.discard_confirm"), new Vector2(-150f, 36f), new Vector2(260f, 56f), Confirmar, Bad, KeyInk);
        RectTransform no = Boton(confirmar, "Cancelar", L10n.T("gameplay.card.cancel"), new Vector2(150f, 36f), new Vector2(260f, 56f), Cancelar, ChipColor, Ink);
        si.anchorMin = si.anchorMax = no.anchorMin = no.anchorMax = new Vector2(0.5f, 0f);
        confirmar.gameObject.SetActive(false);
    }

    private void Mostrar(bool descartar)
    {
        viendo = !descartar;
        lienzo.gameObject.SetActive(true);
        confirmar.gameObject.SetActive(false);
        volver.gameObject.SetActive(descartar);
        Refrescar(descartar);
    }

    // Se rearma si cambió algo (otra carta, otra arma, otra vez la misma carta).
    private void Refrescar(bool descartar)
    {
        IReadOnlyList<CartaDeInvasion> cartas = EleccionDeInvasion.Tenidas;
        int f = descartar ? 1 : 0;
        foreach (CartaDeInvasion c in cartas) f = f * 31 + c.nombre.GetHashCode() + EleccionDeInvasion.Veces(c) * 7 + (c.RindeConLoQueTenes() ? 1 : 0);
        if (f == firma) return;
        firma = f;

        ocupados.text = L10n.T("gameplay.card.occupied_fmt", cartas.Count, EleccionDeInvasion.Lugares);
        titulo.text = L10n.T(descartar ? "gameplay.card.pick_discard" : "gameplay.card.your_cards");
        pie.text = descartar ? L10n.T("gameplay.card.discard_hint") : L10n.T("gameplay.card.hold_hint", KeyBindings.Label(GameAction.Cartas));

        for (int i = grilla.childCount - 1; i >= 0; i--) Destroy(grilla.GetChild(i).gameObject);
        for (int i = 0; i < EleccionDeInvasion.Lugares; i++)
        {
            float x = (i % 4 - 1.5f) * (AnchoCarta + Separacion);
            float y = (i / 4 == 0 ? 0.5f : -0.5f) * (AltoCarta + Separacion);
            if (i < cartas.Count) Carta(cartas[i], x, y, descartar);
            else Libre(x, y);
        }
    }

    private RectTransform Lugar(string nombre, float x, float y)
    {
        RectTransform r = Node(nombre, grilla);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(AnchoCarta, AltoCarta);
        return r;
    }

    // CA2: un lugar libre.
    private void Libre(float x, float y)
    {
        RectTransform r = Lugar("Libre", x, y);
        MarcadorInvasion.Figura<FormaRedondeada>(Stretch(Node("Fondo", r))).Pintar(Rgb(12, 14, 19, 0.45f), WithAlpha(Mute, 0.35f), 16f, 2f);
        Text(Stretch(Node("Texto", r)), label, 22f, WithAlpha(Mute, 0.7f), TextAlignmentOptions.Center, 16f, true).text = L10n.T("gameplay.card.free_slot");
    }

    // CA3 y CA4: una carta que se tiene.
    private void Carta(CartaDeInvasion c, float x, float y, bool descartar)
    {
        int veces = EleccionDeInvasion.Veces(c);
        Color color = CartaDeInvasion.ColorDe(c.rareza);
        string sinUso = MotivoSinUso(c);
        RectTransform r = Lugar("Carta " + c.nombre, x, y);
        MarcadorInvasion.Figura<FormaRedondeada>(Stretch(Node("Fondo", r))).Pintar(Rgb(12, 14, 19, 0.96f), color, 16f, 3f);

        Image(Place(Node("Franja", r), 3f, 3f, AnchoCarta - 6f, 40f), redondeado, WithAlpha(color, 0.18f), 12f);
        Text(Place(Node("Rareza", r), 0f, 3f, AnchoCarta, 40f), label, 16f, color, TextAlignmentOptions.Center, 18f, true).text =
            L10n.Source(CartaDeInvasion.NombreDe(c.rareza));
        TextMeshProUGUI nombre = Text(Place(Node("Nombre", r), 18f, 54f, AnchoCarta - 36f, 40f), display, 30f, Ink, TextAlignmentOptions.Top, 3f, true);
        nombre.text = L10n.Source(c.nombre);
        TextMeshProUGUI efecto = Text(Place(Node("Efecto", r), 22f, 100f, AnchoCarta - 44f, 112f), label, 18f, Soft, TextAlignmentOptions.Top);
        efecto.text = L10n.Source(c.EfectoActual(veces));
        efecto.textWrappingMode = TextWrappingModes.Normal;
        Text(Place(Node("Rinde", r), 18f, 216f, AnchoCarta - 36f, 22f), label, 15f, Mute, TextAlignmentOptions.Center, 6f, true).text =
            L10n.T("gameplay.card.works_with_fmt", L10n.Source(c.rindeCon));

        // Cuántas veces la tiene de las que puede acumular: puntos y "2 de 3" (las de una sola vez, "Única").
        string veo = c.Maximo > 1 ? L10n.T("gameplay.card.times_fmt", veces, c.Maximo) : L10n.T("gameplay.card.single");
        var puntos = new System.Text.StringBuilder();
        for (int k = 0; k < c.Maximo; k++) puntos.Append(k < veces ? "●" : "○");
        Text(Place(Node("Veces", r), 18f, 248f, AnchoCarta - 36f, 30f), label, 18f, color, TextAlignmentOptions.Center, 6f, true).text =
            (c.Maximo > 1 ? puntos + "  " : "") + veo;

        // CA4: sin uso, apagada y con el motivo.
        if (sinUso != null)
        {
            Image(Stretch(Node("Apagada", r)), null, new Color(0f, 0f, 0f, DarkAlpha(0.55f)), 0f);
            RectTransform aviso = Place(Node("SinUso", r), 14f, AltoCarta / 2f - 30f, AnchoCarta - 28f, 60f);
            Image(aviso, redondeado, Rgb(40, 12, 12, 0.92f), 8f);
            TextMeshProUGUI t = Text(Stretch(Node("Texto", aviso)), label, 17f, Ink, TextAlignmentOptions.Center, 2f);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.text = "<b>" + L10n.T("gameplay.card.unused") + "</b>\n" + sinUso;
        }

        // CA5: descartando, toda la carta es un botón.
        if (descartar)
        {
            UnityEngine.UI.Image zona = Image(Stretch(Node("Zona", r)), null, new Color(1f, 1f, 1f, 0f), 0f, true);
            var boton = zona.gameObject.AddComponent<UnityEngine.UI.Button>();
            boton.targetGraphic = zona;
            var colores = boton.colors;
            colores.normalColor = new Color(1f, 1f, 1f, 0f);
            colores.highlightedColor = new Color(1f, 0.3f, 0.3f, 0.12f);
            colores.pressedColor = new Color(1f, 0.3f, 0.3f, 0.2f);
            boton.colors = colores;
            boton.onClick.AddListener(() => Preguntar(c));
        }
    }

    // CA4: por qué no le sirve ahora (o null si le sirve).
    public static string MotivoSinUso(CartaDeInvasion c)
    {
        if (EleccionDeInvasion.Veces(CartaDeInvasion.AlimentacionDirecta) > 0 &&
            System.Array.IndexOf(CartaDeInvasion.SinUsoConAlimentacion, c.nombre) >= 0)
            return L10n.T("gameplay.card.unused_direct_feed");
        if (!c.RindeConLoQueTenes()) return L10n.T("gameplay.card.unused_weapon_fmt", L10n.Source(c.rindeCon));
        return null;
    }

    // ---------- Descartar (CA5) ----------

    private void Preguntar(CartaDeInvasion c)
    {
        aConfirmar = c;
        pregunta.text = L10n.T("gameplay.card.discard_question_fmt", L10n.Source(c.nombre));
        confirmar.gameObject.SetActive(true);
        confirmar.SetAsLastSibling();
    }

    private void Cancelar()
    {
        aConfirmar = null;
        confirmar.gameObject.SetActive(false);
    }

    private void Confirmar()
    {
        CartaDeInvasion c = aConfirmar;
        System.Action<CartaDeInvasion> seguir = alDescartar;
        CerrarDescarte();
        if (c != null) seguir?.Invoke(c);
    }
}
