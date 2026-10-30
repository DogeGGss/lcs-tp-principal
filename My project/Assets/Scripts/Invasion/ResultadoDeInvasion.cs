using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using static ShopUIKit;

// F08 (Modo Invasión), US 151: el resultado de la partida, como la opción A de la maqueta (la vía del marcador de la
// US 150 con la oleada alcanzada y cinco números). Lo muestran PartidaDeInvasion (al morir) y OleadasDeInvasion (al
// elegir "Terminar" después de la 10).
// - CA1: si el jugador muere, la partida termina: "Has muerto · No salvaste al mundo", en rojo.
// - CA2: si supera la 10 y elige "Terminar", "¡Sobreviviste! · Salvaste al mundo". Si siguió en modo infinito y después
//   muere, "Sobreviviste 10 oleadas · Salvaste al mundo" y la oleada a la que llegó.
// - CA3: la oleada alcanzada, las bajas, las bajas a la cabeza, los puntos ganados y el tiempo jugado.
// - CA4: guarda la oleada más alta de la dificultad (MatchSettings, que va con el progreso a la nube, US 162 y 163). Si la
//   superó, "¡Nuevo récord!"; si no, el récord actual (y la banderita en la vía).
// - CA5: "Jugar de nuevo" (Enter) vuelve a cargar el mapa con la misma dificultad, y "Volver al menú" (Esc), el menú.
// - CA6: caerse del mapa es una muerte (FueraDelMapa), así que termina igual que el CA1.
// Todo se congela (el tiempo se detiene) y no se puede pausar.
public class ResultadoDeInvasion : MonoBehaviour
{
    private const string EscenaMenu = "MenuPrincipal";
    private static readonly string[] NombreDificultad = { "Fácil", "Normal", "Difícil" };

    /// <summary>El resultado en pantalla, o null si la partida sigue.</summary>
    public static ResultadoDeInvasion Actual { get; private set; }

    private TMP_FontAsset display, label, mono;
    private ViaDeOleadas via;
    private bool saliendo; // ya eligió un botón: se está cargando la escena

    /// <summary>Termina la partida y muestra el resultado (una sola vez). murio: false si eligió "Terminar".</summary>
    public static void Mostrar(bool murio)
    {
        if (Actual != null || PartidaDeInvasion.Actual == null) return;
        var go = new GameObject("Resultado de Invasión", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        Actual = go.AddComponent<ResultadoDeInvasion>();
        Actual.Armar(murio);
    }

    private void OnDestroy()
    {
        if (Actual == this) Actual = null;
    }

    private void Armar(bool murio)
    {
        PartidaDeInvasion partida = PartidaDeInvasion.Actual;
        OleadasDeInvasion oleadas = partida.GetComponent<OleadasDeInvasion>();
        PuntosDeInvasion puntos = partida.GetComponent<PuntosDeInvasion>();
        MatchHud hud = MatchHud.Instance;
        display = hud != null ? hud.DisplayFont : null;
        label = hud != null ? hud.LabelFont : null;
        CombatHud combate = FindAnyObjectByType<CombatHud>();
        mono = combate != null && combate.MonoFont != null ? combate.MonoFont : label;
        Sprite redondeado = hud != null ? hud.Rounded : null;

        // CA2 y CA4: hasta dónde llegó y el récord.
        int oleada = Mathf.Max(1, oleadas != null ? oleadas.Oleada : 1);
        bool infinito = oleadas != null && oleadas.Infinito;
        bool salvo = !murio || infinito;
        ZombieDifficulty dificultad = MatchSettings.Difficulty;
        int recordAntes = MatchSettings.ZombieRecord(dificultad);
        bool nuevo = oleada > recordAntes;
        MatchSettings.SubirZombieRecord(dificultad, oleada);
        Guardado.Save(); // la nube lo sube sola cuando ve el cambio (LogrosOnline)

        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200; // arriba del HUD, la tienda y los carteles
        canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
        var escala = GetComponent<UnityEngine.UI.CanvasScaler>();
        escala.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;

        // El juego tapado (y los clics bloqueados).
        RectTransform sombra = Stretch(Node("Sombra", transform));
        Image(sombra, null, Rgb(6, 8, 12, DarkAlpha(0.8f)), 0f, true);

        RectTransform raiz = Node("Contenido", transform);
        raiz.anchorMin = raiz.anchorMax = raiz.pivot = new Vector2(0.5f, 0.5f);
        raiz.sizeDelta = new Vector2(1920f, 1080f);

        // Títulos.
        Color color = salvo ? MarcadorInvasion.Verde : Bad;
        string titulo = !murio ? "¡Sobreviviste!" : infinito ? "Sobreviviste 10 oleadas" : "Has muerto";
        Text(Place(Node("Antetitulo", raiz), 0f, 70f, 1920f, 30f), label, 24f, Mute, TextAlignmentOptions.Center, 24f, true).text =
            $"Fin de la partida  ·  {NombreDificultad[(int)dificultad]}";
        Text(Place(Node("Titulo", raiz), 0f, 100f, 1920f, 170f), display, titulo.Length > 16 ? 120f : 150f, color,
            TextAlignmentOptions.Center, 6f, true).text = titulo;
        Image(Place(Node("Linea", raiz), 870f, 280f, 180f, 4f), null, color);
        Text(Place(Node("Lema", raiz), 0f, 296f, 1920f, 34f), label, 26f, color, TextAlignmentOptions.Center, 24f, true).text =
            salvo ? "Salvaste al mundo" : "No salvaste al mundo";
        Text(Place(Node("Detalle", raiz), 0f, 336f, 1920f, 36f), label, 30f, Ink, TextAlignmentOptions.Center, 12f, true).text =
            !murio ? $"Superaste las {OleadasDeInvasion.Ultima} oleadas"
            : infinito ? $"Llegaste a la oleada {oleada}  ·  modo infinito"
            : $"Caíste en la oleada {oleada} de {OleadasDeInvasion.Ultima}";

        // La vía, como en el marcador (US 150).
        MarcadorInvasion.Figura<FormaRedondeada>(Place(Node("Panel", raiz), 660f, 382f, 600f, 96f))
            .Pintar(Rgb(6, 10, 6, DarkAlpha(0.74f)), WithAlpha(MarcadorInvasion.Verde, FadeAlpha(0.28f)), 10f, 1f);
        RectTransform viaRect = Place(Node("Via", raiz), 676f, 396f, 568f, 62f);
        via = MarcadorInvasion.Figura<ViaDeOleadas>(viaRect);
        via.raycastTarget = false;
        int actual = Mathf.Min(oleada, OleadasDeInvasion.Ultima + 1) - 1;
        via.actual = actual;
        int r = !nuevo && recordAntes > 0 ? Mathf.Min(recordAntes, OleadasDeInvasion.Ultima + 1) - 1 : -1;
        via.record = r;
        for (int i = 0; i <= OleadasDeInvasion.Ultima; i++)
        {
            float x = ViaDeOleadas.X(i);
            TextMeshProUGUI n = Text(Place(Node("Estacion" + (i + 1), viaRect), x - 20f, 52f, 40f, 16f), mono, 12f, i == actual ? Ink : Mute, TextAlignmentOptions.Baseline);
            n.text = i == OleadasDeInvasion.Ultima ? "∞" : (i + 1).ToString();
        }
        if (r >= 0)
        {
            float x = ViaDeOleadas.X(r);
            bool alFinal = r >= OleadasDeInvasion.Ultima - 1;
            TextMeshProUGUI bandera = Text(Place(Node("Record", viaRect), alFinal ? x - 125f : x + 15f, 3f, 110f, 16f), display, 11f, Accent,
                alFinal ? TextAlignmentOptions.BaselineRight : TextAlignmentOptions.BaselineLeft, 12f, true);
            bandera.text = "Récord";
        }

        // CA4: el récord.
        if (nuevo)
        {
            string texto = $"¡Nuevo récord! · Oleada {oleada}";
            RectTransform pildora = Node("NuevoRecord", raiz);
            TextMeshProUGUI t = Text(Stretch(Node("Texto", pildora)), display, 24f, MarcadorInvasion.Verde, TextAlignmentOptions.Center, 10f, true);
            t.text = texto;
            float w = Width(t, texto) + 44f;
            Place(pildora, (1920f - w) / 2f, 500f, w, 44f);
            MarcadorInvasion.Figura<FormaRedondeada>(Stretch(Node("Fondo", pildora)))
                .Pintar(Rgb(10, 24, 8, DarkAlpha(0.9f)), MarcadorInvasion.Verde, 22f, 1f);
            t.transform.SetAsLastSibling();
        }
        else
            Text(Place(Node("Record", raiz), 0f, 504f, 1920f, 36f), label, 22f, Mute, TextAlignmentOptions.Center, 12f, true).text =
                $"Récord en {NombreDificultad[(int)dificultad]}: <color=#F3F4F6>oleada {recordAntes}</color>";

        // CA3: los números.
        int segundos = Mathf.FloorToInt(Time.time - partida.Inicio);
        string[,] numeros =
        {
            { "Oleada", oleada.ToString() },
            { "Bajas", (puntos != null ? puntos.Bajas : 0).ToString() },
            { "A la cabeza", (puntos != null ? puntos.BajasALaCabeza : 0).ToString() },
            { "Puntos", Miles(puntos != null ? puntos.PuntosGanados : 0) },
            { "Tiempo", $"{segundos / 60}:{segundos % 60:00}" },
        };
        const float Ancho = 176f, Espacio = 16f;
        float x0 = (1920f - (5 * Ancho + 4 * Espacio)) / 2f;
        for (int i = 0; i < 5; i++)
        {
            RectTransform tarjeta = Place(Node("Numero" + numeros[i, 0], raiz), x0 + i * (Ancho + Espacio), 584f, Ancho, 128f);
            Image(tarjeta, redondeado, Rgb(10, 12, 17, DarkAlpha(0.9f)), 10f);
            Text(Place(Node("Rotulo", tarjeta), 0f, 22f, Ancho, 20f), label, 15f, Mute, TextAlignmentOptions.Center, 14f, true).text = numeros[i, 0];
            Color tinta = i == 0 ? color : i == 2 ? Accent : Ink;
            Text(Place(Node("Valor", tarjeta), 0f, 50f, Ancho, 64f), display, 56f, tinta, TextAlignmentOptions.Center, 2f).text = numeros[i, 1];
        }

        // CA5: los botones.
        Boton("Jugar de nuevo", 676f, MarcadorInvasion.Verde, Hex(0x10180C), false, redondeado, JugarDeNuevo);
        Boton("Volver al menú", 984f, Color.clear, Ink, true, redondeado, VolverAlMenu);
        Text(Place(Node("Teclas", raiz), 0f, 836f, 1920f, 24f), label, 15f, Mute, TextAlignmentOptions.Center, 12f, true).text =
            "Enter  ·  Jugar de nuevo          Esc  ·  Volver al menú";

        void Boton(string texto, float x, Color fondo, Color tinta, bool borde, Sprite sprite, System.Action alApretar)
        {
            RectTransform b = Place(Node(texto, raiz), x, 760f, 260f, 56f);
            if (borde)
                MarcadorInvasion.Figura<FormaRedondeada>(Stretch(Node("Borde", b))).Pintar(Color.clear, White(FadeAlpha(0.35f)), 6f, 1.5f);
            UnityEngine.UI.Image img = Image(b, sprite, borde ? new Color(1f, 1f, 1f, 0f) : fondo, 6f, true);
            Text(Stretch(Node("Texto", b)), display, 24f, tinta, TextAlignmentOptions.Center, 12f, true).text = texto;
            ShopPointerTarget puntero = b.gameObject.AddComponent<ShopPointerTarget>();
            puntero.Hovered = () => img.color = borde ? White(0.08f) : Color.Lerp(fondo, Color.white, 0.25f);
            puntero.Exited = () => img.color = borde ? new Color(1f, 1f, 1f, 0f) : fondo;
            puntero.Clicked = alApretar;
        }

        Congelar();
    }

    private static string Miles(int n) => n.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", ".");

    private void Update()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (via != null)
        {
            via.pulso = Mathf.Repeat(Time.unscaledTime, 1.6f) / 1.6f;
            via.Refrescar();
        }
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) JugarDeNuevo();
        else if (Input.GetKeyDown(KeyCode.Escape)) VolverAlMenu();
    }

    // Todo se detiene: los invasores, el jugador y la cámara. No se puede pausar ni abrir la tienda.
    private static void Congelar()
    {
        Time.timeScale = 0f;
        CameraLook.Quieta = true;
        if (PauseMenu.Instance != null) PauseMenu.Instance.AllowPause = false;
        MatchHud.HideBanner();
    }

    private static void Soltar()
    {
        Time.timeScale = 1f;
        CameraLook.Quieta = false;
        if (PauseMenu.Instance != null) PauseMenu.Instance.AllowPause = true;
    }

    // CA5: la misma escena, con la misma dificultad (MatchSettings la recuerda).
    // Con la pantalla de carga (US 196), con el nombre y la imagen del mapa como desde la selección de modo.
    private void JugarDeNuevo()
    {
        if (saliendo) return;
        saliendo = true;
        Soltar();
        string escena = SceneManager.GetActiveScene().name, mapa = escena;
        Sprite imagen = null;
        if (ConfigRed.Actual != null)
            foreach (ConfigRed.Mapa m in ConfigRed.Actual.mapas)
                if (m != null && m.escena == escena) { mapa = m.nombre; imagen = m.imagen; }
        PantallaDeCarga.Cargar(escena, "Modo Invasión · " + NombreDificultad[(int)MatchSettings.Difficulty], mapa, imagen, GameMode.Zombie);
    }

    private void VolverAlMenu()
    {
        if (saliendo) return;
        saliendo = true;
        Soltar();
        PantallaDeCarga.Cargar(EscenaMenu, "", "Menú principal"); // US 046 y US 196
    }
}
