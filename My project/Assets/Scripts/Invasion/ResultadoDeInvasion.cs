using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using static ShopUIKit;

// F08 (Modo Invasión), US 151: el resultado de la partida, como la opción A de la maqueta (la vía del marcador de la
// US 150 con la oleada alcanzada y cinco números). Lo muestran PartidaDeInvasion (al morir) y OleadasDeInvasion (al
// elegir "Retirarse" después de un Jefe, US 303).
// - CA1: si el jugador muere, la partida termina: "Has muerto · No salvaste al mundo", en rojo.
// - CA2 (con la US 303): si se retira después de un Jefe, "¡Sobreviviste! · Salvaste al mundo". Si ya había bajado
//   algún Jefe y después muere, "Sobreviviste N oleadas · Salvaste al mundo", la oleada a la que llegó y los Jefes.
// - CA3: la oleada alcanzada, las bajas, las bajas a la cabeza, los puntos ganados y el tiempo jugado.
// - CA4: guarda la oleada más alta de la dificultad (MatchSettings, que va con el progreso a la nube, US 162 y 163). Si la
//   superó, "¡Nuevo récord!"; si no, el récord actual (y la banderita en la vía).
// - CA5: "Jugar de nuevo" (Enter) vuelve a cargar el mapa con la misma dificultad, y "Volver al menú" (Esc), el menú.
// - CA6: caerse del mapa es una muerte (FueraDelMapa), así que termina igual que el CA1.
// - US 222, CA2: la partida se suma una vez a las estadísticas de Invasión del jugador (EstadisticasJugador), que ve el Perfil.
// Todo se congela (el tiempo se detiene) y no se puede pausar.
// F31 (US 289): en cooperativo el récord va aparte (CA13: MatchSettings.ZombieRecordCoop) y "Jugar de nuevo" vuelve al
// menú, porque la partida de los demás sigue (para jugar otra se vuelve a buscar o a armar la sala).
public class ResultadoDeInvasion : MonoBehaviour
{
    private const string EscenaMenu = "MenuPrincipal";
    private static readonly string[] ClaveDificultad = { "gameplay.difficulty.easy", "gameplay.difficulty.normal", "gameplay.difficulty.hard" };
    private static string Dificultad(int i) => L10n.T(ClaveDificultad[i]);

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
        bool infinito = oleadas != null && oleadas.Infinito; // US 303: ya bajó algún Jefe
        int jefes = oleadas != null ? oleadas.JefesDerrotados : 0;
        bool salvo = !murio || infinito;
        ZombieDifficulty dificultad = MatchSettings.Difficulty;
        bool coop = CooperativoDeInvasion.Activo; // US 289, CA13: el récord del cooperativo, aparte
        int recordAntes = coop ? MatchSettings.ZombieRecordCoop(dificultad) : MatchSettings.ZombieRecord(dificultad);
        bool nuevo = oleada > recordAntes;
        if (coop) MatchSettings.SubirZombieRecordCoop(dificultad, oleada);
        else MatchSettings.SubirZombieRecord(dificultad, oleada);
        int segundos = Mathf.FloorToInt(Time.time - partida.Inicio);
        // US 222, CA2: la partida se suma a las estadísticas de Invasión, aparte de las de multijugador.
        EstadisticasJugador.Registrar(new EstadisticasJugador.Partida
        {
            id = partida.Id,
            invasion = true,
            gano = salvo,
            bajas = puntos != null ? puntos.Bajas : 0,
            cabezas = puntos != null ? puntos.BajasALaCabeza : 0,
            muertes = murio ? 1 : 0,
            segundos = segundos,
        });
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
        string titulo = !murio ? L10n.T("gameplay.ui.you_survived") : infinito ? L10n.T("gameplay.result.survived_fmt", jefes * OleadasDeInvasion.Ultima) : L10n.T("gameplay.result.dead");
        Text(Place(Node("Antetitulo", raiz), 0f, 70f, 1920f, 30f), label, 24f, Mute, TextAlignmentOptions.Center, 24f, true).text =
            L10n.T("gameplay.result.game_over_fmt", Dificultad((int)dificultad));
        Text(Place(Node("Titulo", raiz), 0f, 100f, 1920f, 170f), display, titulo.Length > 16 ? 120f : 150f, color,
            TextAlignmentOptions.Center, 6f, true).text = titulo;
        Image(Place(Node("Linea", raiz), 870f, 280f, 180f, 4f), null, color);
        Text(Place(Node("Lema", raiz), 0f, 296f, 1920f, 34f), label, 26f, color, TextAlignmentOptions.Center, 24f, true).text =
            L10n.T(salvo ? "gameplay.result.saved_world" : "gameplay.result.not_saved_world");
        Text(Place(Node("Detalle", raiz), 0f, 336f, 1920f, 36f), label, 30f, Ink, TextAlignmentOptions.Center, 12f, true).text =
            !murio ? L10n.T("gameplay.result.retired_fmt", oleada)
            : infinito ? L10n.T("gameplay.result.reached_fmt", oleada, L10n.Plural("gameplay.result.bosses_defeated", jefes))
            : L10n.T("gameplay.result.fell_fmt", oleada, OleadasDeInvasion.Ultima);

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
            string texto = L10n.T("gameplay.result.new_record_fmt", oleada);
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
                coop ? L10n.T("gameplay.result.record_coop_fmt", Dificultad((int)dificultad), recordAntes, MatchSettings.ZombieRecord(dificultad))
                     : L10n.T("gameplay.result.record_fmt", Dificultad((int)dificultad), recordAntes);
        // US 289, CA13: en cooperativo también el récord de un jugador (con un récord nuevo, debajo de la píldora).
        if (coop && nuevo)
            Text(Place(Node("RecordSolo", raiz), 0f, 540f, 1920f, 28f), label, 18f, Mute, TextAlignmentOptions.Center, 12f, true).text =
                L10n.T("gameplay.result.record_solo_fmt", MatchSettings.ZombieRecord(dificultad));

        // CA3: los números.
        string[,] numeros =
        {
            { L10n.T("gameplay.result.wave"), oleada.ToString() },
            { L10n.T("gameplay.dm.col.kills"), (puntos != null ? puntos.Bajas : 0).ToString() },
            { L10n.T("gameplay.result.headshots"), (puntos != null ? puntos.BajasALaCabeza : 0).ToString() },
            { L10n.T("gameplay.result.points"), Miles(puntos != null ? puntos.PuntosGanados : 0) },
            { L10n.T("gameplay.result.time"), $"{segundos / 60}:{segundos % 60:00}" },
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

        // F31 (US 289, CA7): en cooperativo, cada jugador con sus bajas y sus puntos (debajo de los números; los botones
        // bajan).
        float yBotones = 760f;
        if (coop) yBotones = Jugadores(raiz, redondeado, puntos);

        // CA5: los botones.
        Boton(L10n.T("gameplay.result.play_again"), 676f, MarcadorInvasion.Verde, Hex(0x10180C), false, redondeado, JugarDeNuevo);
        Boton(L10n.T("gameplay.result.back_menu"), 984f, Color.clear, Ink, true, redondeado, VolverAlMenu);
        Text(Place(Node("Teclas", raiz), 0f, yBotones + 76f, 1920f, 24f), label, 15f, Mute, TextAlignmentOptions.Center, 12f, true).text =
            L10n.T("gameplay.result.keys_hint");

        void Boton(string texto, float x, Color fondo, Color tinta, bool borde, Sprite sprite, System.Action alApretar)
        {
            RectTransform b = Place(Node(texto, raiz), x, yBotones, 260f, 56f);
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
        // US 289, CA9: el que se retira termina su partida; la de los demás sigue.
        if (coop && !murio) CaidaEnInvasion.Retirarse();
    }

    // US 289, CA7: los jugadores de la partida, ordenados por puntos. Devuelve dónde van los botones.
    private float Jugadores(RectTransform raiz, Sprite redondeado, PuntosDeInvasion mios)
    {
        if (!Photon.Pun.PhotonNetwork.InRoom) return 760f;
        var lista = new System.Collections.Generic.List<Photon.Realtime.Player>(Photon.Pun.PhotonNetwork.PlayerList);
        int Bajas(Photon.Realtime.Player p) => p.IsLocal && mios != null ? mios.Bajas : CompanerosEnInvasion.Numero(p, CompanerosEnInvasion.PropBajas);
        int Puntos(Photon.Realtime.Player p) => p.IsLocal && mios != null ? mios.PuntosGanados : CompanerosEnInvasion.Numero(p, CompanerosEnInvasion.PropPuntos);
        lista.Sort((a, b) => Puntos(b).CompareTo(Puntos(a)));
        const float Ancho = 720f, Alto = 34f, Y = 728f;
        float x = (1920f - Ancho) / 2f;
        Text(Place(Node("Jugadores", raiz), x + 16f, Y, 300f, 24f), label, 15f, Mute, TextAlignmentOptions.MidlineLeft, 14f, true).text = L10n.T("gameplay.mates.player");
        Text(Place(Node("ColBajas", raiz), x + Ancho - 300f, Y, 140f, 24f), label, 15f, Mute, TextAlignmentOptions.MidlineRight, 14f, true).text = L10n.T("gameplay.mates.kills");
        Text(Place(Node("ColPuntos", raiz), x + Ancho - 156f, Y, 140f, 24f), label, 15f, Mute, TextAlignmentOptions.MidlineRight, 14f, true).text = L10n.T("gameplay.mates.points");
        float y = Y + 28f;
        foreach (Photon.Realtime.Player p in lista)
        {
            RectTransform fila = Place(Node("Jugador " + p.ActorNumber, raiz), x, y, Ancho, Alto);
            Image(fila, redondeado, p.IsLocal ? WithAlpha(Accent, 0.18f) : Rgb(10, 12, 17, DarkAlpha(0.9f)), 6f);
            Color tinta = p.IsInactive ? Mute : Ink;
            Text(Place(Node("Nombre", fila), 16f, 0f, 380f, Alto), label, 20f, tinta, TextAlignmentOptions.MidlineLeft).text =
                string.IsNullOrEmpty(p.NickName) ? "?" : p.NickName;
            Text(Place(Node("Bajas", fila), Ancho - 300f, 0f, 140f, Alto), mono, 20f, tinta, TextAlignmentOptions.MidlineRight).text = Bajas(p).ToString();
            Text(Place(Node("Puntos", fila), Ancho - 156f, 0f, 140f, Alto), mono, 20f, tinta, TextAlignmentOptions.MidlineRight).text = Miles(Puntos(p));
            y += Alto + 4f;
        }
        return Mathf.Max(760f, y + 20f);
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
        // F31 (US 356): en cooperativo la partida sigue para los demás (y si es el anfitrión, lleva a los invasores).
        if (!CooperativoDeInvasion.Activo) Time.timeScale = 0f;
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
        if (CooperativoDeInvasion.Activo) { VolverAlMenu(); return; } // F31: la partida de los demás sigue
        if (saliendo) return;
        saliendo = true;
        Soltar();
        string escena = SceneManager.GetActiveScene().name, mapa = escena;
        Sprite imagen = null;
        if (ConfigRed.Actual != null)
            foreach (ConfigRed.Mapa m in ConfigRed.Actual.mapas)
                if (m != null && m.escena == escena) { mapa = m.nombre; imagen = m.imagen; }
        PantallaDeCarga.Cargar(escena, L10n.T("gameplay.ui.invasion_mode") + " · " + Dificultad((int)MatchSettings.Difficulty), mapa, imagen, GameMode.Zombie);
    }

    private void VolverAlMenu()
    {
        if (saliendo) return;
        saliendo = true;
        Soltar();
        if (CooperativoDeInvasion.Activo) Multijugador.SalirDeLaSala(); // F31: sale de la sala (no vuelve, US 195)
        PantallaDeCarga.Cargar(EscenaMenu, "", "Menú principal"); // US 046 y US 196
    }
}
