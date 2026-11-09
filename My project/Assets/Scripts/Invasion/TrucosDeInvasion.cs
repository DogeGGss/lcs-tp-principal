using System.Collections.Generic;
using UnityEngine;

// F08 (Modo Invasión): menú de trucos para mostrar el modo sin jugar toda la partida (por ejemplo, para el PO).
// Con F10 en una partida de Invasión se abre (el juego se pausa) y se puede:
// - Jugador: vida infinita, balas infinitas (no se recarga nunca), noclip (volar y atravesar paredes: WASD, Espacio
//   sube y Ctrl baja, Shift más rápido) y matar de un tiro.
// - Sumar puntos (para volver a tirar en el puesto).
// - Ir a la oleada que se quiera (saca a los invasores que quedan y la arranca ya, como el atajo del editor).
// - Agarrar cualquier arma de las que pueden salir en el puesto, y sumarse cualquier carta (como si se la hubiera
//   elegido; hasta el máximo de veces de cada una).
// - Matar a todos los invasores y cargar el puesto de suministros.
// Disponibles: con false no se crea el menú (PartidaDeInvasion). Los trucos se apagan al terminar cada partida.
public class TrucosDeInvasion : MonoBehaviour
{
    public static readonly bool Disponibles = true;

    public static bool VidaInfinita { get; private set; }
    public static bool BalasInfinitas { get; private set; }
    public static bool Noclip { get; private set; }
    public static bool UnTiro { get; private set; }

    /// <summary>El menú está abierto (el juego está pausado).</summary>
    public static bool Abierto { get; private set; }

    private const KeyCode Tecla = KeyCode.F10;
    private const float AltoDeReferencia = 1080f;

    private float escalaDeTiempo = 1f;
    private bool devolverPausa;
    private readonly List<Behaviour> bloqueados = new List<Behaviour>();
    private Vector2 scrollArmas, scrollCartas;
    private string oleadaTexto = "10";
    private string puntosTexto = "1000";
    private GUIStyle titulo, seccion, boton, botonActivo, texto, campo, caja;
    private Texture2D fondo, fondoCaja, fondoBoton, fondoBotonHover, fondoActivo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Limpiar() => Apagar();

    private static void Apagar()
    {
        VidaInfinita = BalasInfinitas = Noclip = UnTiro = false;
        Abierto = false;
    }

    private void OnEnable() => Npc.ImpactoDeBala += AlImpactar;

    private void OnDisable()
    {
        Npc.ImpactoDeBala -= AlImpactar;
        if (Abierto) Cerrar();
        HealthSystem vida = Vida();
        if (vida != null && VidaInfinita) vida.Invulnerable = false;
        Apagar();
    }

    // Matar de un tiro: el impacto de bala que no lo mató, lo mata (salvo que ya esté muerto).
    private static void AlImpactar(Npc npc, int danio, BodyZone zona)
    {
        if (!UnTiro || npc == null || !npc.Vivo) return;
        npc.Vida.TakeDamage(npc.Vida.currentHealth + 1, zona == BodyZone.Head);
    }

    private void Update()
    {
        if (devolverPausa)
        {
            devolverPausa = false;
            if (PauseMenu.Instance != null) PauseMenu.Instance.AllowPause = true;
        }

        if (Input.GetKeyDown(Tecla))
        {
            if (Abierto) Cerrar();
            else if (!EleccionDeInvasion.Abierta && !PauseMenu.IsPaused && !ShopUI.IsOpen) Abrir();
        }
        else if (Abierto && Input.GetKeyDown(KeyCode.Escape)) Cerrar();

        if (Abierto && Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        Mantener();
    }

    // Lo que tiene que valer en cada cuadro: la vida llena y los cargadores llenos.
    private void Mantener()
    {
        if (VidaInfinita)
        {
            HealthSystem vida = Vida();
            if (vida != null)
            {
                vida.Invulnerable = true;
                if (vida.currentHealth > 0 && vida.currentHealth < vida.maxHealth) vida.currentHealth = vida.maxHealth;
            }
        }
        if (BalasInfinitas)
        {
            WeaponSwitcher armas = FindFirstObjectByType<WeaponSwitcher>();
            if (armas != null)
            {
                MunicionDelArma.SumarAlCargador(armas.PrimaryObj, 999);
                MunicionDelArma.SumarAlCargador(armas.SecondaryObj, 999);
            }
        }
    }

    private void Abrir()
    {
        Abierto = true;
        escalaDeTiempo = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        if (PauseMenu.Instance != null) PauseMenu.Instance.AllowPause = false; // Esc cierra los trucos, no abre la pausa
        bloqueados.Clear();
        Bloquear(FindObjectsByType<CameraLook>(FindObjectsSortMode.None));
        Bloquear(FindObjectsByType<MeleeAttack>(FindObjectsSortMode.None));
        Bloquear(FindObjectsByType<Pistola>(FindObjectsSortMode.None));
        Bloquear(FindObjectsByType<Mitre>(FindObjectsSortMode.None));
        Bloquear(FindObjectsByType<ArmaDeFuego>(FindObjectsSortMode.None));
        Bloquear(FindObjectsByType<WeaponSwitcher>(FindObjectsSortMode.None));
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Cerrar()
    {
        Abierto = false;
        if (!EleccionDeInvasion.Abierta) Time.timeScale = escalaDeTiempo;
        foreach (Behaviour c in bloqueados) if (c != null) c.enabled = true;
        bloqueados.Clear();
        devolverPausa = true; // el cuadro siguiente: así el mismo Esc no abre la pausa
        if (!EleccionDeInvasion.Abierta)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Bloquear<T>(T[] componentes) where T : Behaviour
    {
        foreach (T c in componentes)
        {
            if (!c.enabled) continue;
            c.enabled = false;
            bloqueados.Add(c);
        }
    }

    private static HealthSystem Vida()
    {
        PlayerLoadout equipo = FindFirstObjectByType<PlayerLoadout>();
        return equipo != null ? equipo.GetComponent<HealthSystem>() : null;
    }

    // ---------- Acciones ----------

    private void CambiarVidaInfinita()
    {
        VidaInfinita = !VidaInfinita;
        HealthSystem vida = Vida();
        if (vida == null) return;
        vida.Invulnerable = VidaInfinita;
        if (VidaInfinita) vida.currentHealth = vida.maxHealth;
    }

    private void CambiarNoclip()
    {
        Noclip = !Noclip;
        PlayerMovement movimiento = FindFirstObjectByType<PlayerMovement>();
        if (movimiento != null) movimiento.Frenar();
    }

    private void SumarPuntos()
    {
        if (!int.TryParse(puntosTexto, out int puntos) || puntos <= 0) return;
        PlayerWallet billetera = FindFirstObjectByType<PlayerWallet>();
        if (billetera == null) return;
        billetera.SinTope();
        billetera.Add(puntos);
    }

    private void IrALaOleada()
    {
        if (!int.TryParse(oleadaTexto, out int n) || n < 1) return;
        OleadasDeInvasion oleadas = PartidaDeInvasion.Actual != null ? PartidaDeInvasion.Actual.GetComponent<OleadasDeInvasion>() : null;
        if (oleadas == null) return;
        Cerrar();
        oleadas.IrA(Mathf.Min(n, 999));
    }

    private static void MatarATodos()
    {
        foreach (Npc npc in new List<Npc>(Npc.Todos))
            if (npc != null && npc.Vivo) npc.Vida.TakeDamage(npc.Vida.currentHealth + 1);
    }

    private static void CargarElPuesto()
    {
        PuestoDeSuministros puesto = PuestoDeSuministros.Actual;
        if (puesto == null || !puesto.Hay) { MatchHud.Warn("Todavía no hay un puesto en el mapa (cae desde la oleada 2).", 2f); return; }
        puesto.CargarDePrueba();
    }

    private static List<ShopItem> Armas()
    {
        var lista = new List<ShopItem>();
        PlayerLoadout equipo = FindFirstObjectByType<PlayerLoadout>();
        if (equipo != null && equipo.Catalog != null) lista.AddRange(equipo.Catalog.items);
        if (ConfigInvasion.Actual != null && ConfigInvasion.Actual.armasSoloInvasion != null) lista.AddRange(ConfigInvasion.Actual.armasSoloInvasion);
        lista.RemoveAll(i => i == null || (i.kind != ShopItemKind.PrimaryWeapon && i.kind != ShopItemKind.SecondaryWeapon));
        return lista;
    }

    private static string Nombre(ShopItem arma) => L10n.Source(
        !string.IsNullOrEmpty(arma.displayName) ? arma.displayName : !string.IsNullOrEmpty(arma.alias) ? arma.alias : arma.name);

    // ---------- Pantalla ----------

    private void OnGUI()
    {
        if (!Abierto) return;
        if (titulo == null) Estilos();
        float escala = Screen.height / AltoDeReferencia;
        float ancho = Screen.width / escala;
        GUI.matrix = Matrix4x4.Scale(new Vector3(escala, escala, 1f));
        GUI.DrawTexture(new Rect(0f, 0f, ancho, AltoDeReferencia), fondo);

        float w = Mathf.Min(1500f, ancho - 80f), x = (ancho - w) / 2f, y = 60f;
        GUI.Label(new Rect(x, y, w, 50f), "TRUCOS · INVASIÓN", titulo);
        GUI.Label(new Rect(x, y + 50f, w, 30f), "F10 o Esc para volver al juego. El juego está pausado.", texto);
        y += 100f;

        float col = (w - 40f) / 3f;
        float alto = AltoDeReferencia - y - 60f;

        // Columna 1: jugador, puntos, oleada y extras.
        GUILayout.BeginArea(new Rect(x, y, col, alto), caja);
        GUILayout.Label("JUGADOR", seccion);
        if (Interruptor("Vida infinita", VidaInfinita)) CambiarVidaInfinita();
        if (Interruptor("Balas infinitas", BalasInfinitas)) BalasInfinitas = !BalasInfinitas;
        if (Interruptor("Noclip (volar y atravesar paredes)", Noclip)) CambiarNoclip();
        if (Noclip) GUILayout.Label("WASD para moverte, Espacio sube, Ctrl baja, Shift más rápido.", texto);
        if (Interruptor("Matar de un tiro", UnTiro)) UnTiro = !UnTiro;

        GUILayout.Space(16f);
        GUILayout.Label("PUNTOS", seccion);
        PlayerWallet billetera = FindFirstObjectByType<PlayerWallet>();
        GUILayout.Label("Tenés " + (billetera != null ? billetera.Money.ToString("N0") : "0") + " puntos.", texto);
        GUILayout.BeginHorizontal();
        puntosTexto = GUILayout.TextField(puntosTexto, 7, campo, GUILayout.Width(140f), GUILayout.Height(44f));
        if (GUILayout.Button("Sumar puntos", boton, GUILayout.Height(44f))) SumarPuntos();
        GUILayout.EndHorizontal();

        GUILayout.Space(16f);
        GUILayout.Label("OLEADA", seccion);
        OleadasDeInvasion oleadas = PartidaDeInvasion.Actual != null ? PartidaDeInvasion.Actual.GetComponent<OleadasDeInvasion>() : null;
        GUILayout.Label("Ahora: oleada " + (oleadas != null ? oleadas.Oleada : 0) + ". Las del Jefe son la 10, la 20, la 30…", texto);
        GUILayout.BeginHorizontal();
        oleadaTexto = GUILayout.TextField(oleadaTexto, 3, campo, GUILayout.Width(140f), GUILayout.Height(44f));
        if (GUILayout.Button("Ir a esa oleada", boton, GUILayout.Height(44f))) IrALaOleada();
        GUILayout.EndHorizontal();

        GUILayout.Space(16f);
        GUILayout.Label("INVASORES Y PUESTO", seccion);
        if (GUILayout.Button("Matar a todos los invasores", boton, GUILayout.Height(44f))) MatarATodos();
        if (GUILayout.Button("Cargar el puesto de suministros", boton, GUILayout.Height(44f))) CargarElPuesto();
        GUILayout.EndArea();

        // Columna 2: armas.
        GUILayout.BeginArea(new Rect(x + col + 20f, y, col, alto), caja);
        GUILayout.Label("ARMAS", seccion);
        GUILayout.Label("Te la pone en la mano, en el lugar de la principal o de la secundaria.", texto);
        scrollArmas = GUILayout.BeginScrollView(scrollArmas);
        PlayerLoadout equipo = FindFirstObjectByType<PlayerLoadout>();
        foreach (ShopItem arma in Armas())
        {
            bool tiene = equipo != null && (equipo.Primary == arma || equipo.Secondary == arma);
            string lugar = arma.kind == ShopItemKind.PrimaryWeapon ? "principal" : "secundaria";
            if (GUILayout.Button($"{Nombre(arma)}  ·  {lugar}", tiene ? botonActivo : boton, GUILayout.Height(44f)) && equipo != null)
                equipo.Recibir(arma);
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();

        // Columna 3: cartas.
        GUILayout.BeginArea(new Rect(x + 2f * (col + 20f), y, col, alto), caja);
        GUILayout.Label("CARTAS", seccion);
        GUILayout.Label("Te la suma como si la hubieras elegido en el puesto.", texto);
        scrollCartas = GUILayout.BeginScrollView(scrollCartas);
        foreach (CartaDeInvasion carta in CartaDeInvasion.Todas)
        {
            int veces = EleccionDeInvasion.Veces(carta);
            bool llena = veces >= carta.Maximo;
            Color antes = GUI.contentColor;
            GUI.contentColor = CartaDeInvasion.ColorDe(carta.rareza);
            GUI.enabled = !llena && EleccionDeInvasion.Actual != null;
            if (GUILayout.Button($"{L10n.Source(carta.nombre)}  ·  {CartaDeInvasion.NombreDe(carta.rareza)}  ({veces}/{carta.Maximo})",
                    veces > 0 ? botonActivo : boton, GUILayout.Height(44f)))
                EleccionDeInvasion.Actual.Dar(carta);
            GUI.enabled = true;
            GUI.contentColor = antes;
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private bool Interruptor(string nombre, bool prendido) =>
        GUILayout.Button((prendido ? "●  " : "○  ") + nombre + (prendido ? "  ·  SÍ" : "  ·  NO"), prendido ? botonActivo : boton, GUILayout.Height(44f));

    private void Estilos()
    {
        fondo = Color1(new Color(0.03f, 0.04f, 0.06f, 0.88f));
        fondoCaja = Color1(new Color(0.09f, 0.11f, 0.15f, 0.95f));
        fondoBoton = Color1(new Color(0.16f, 0.19f, 0.25f, 1f));
        fondoBotonHover = Color1(new Color(0.22f, 0.26f, 0.34f, 1f));
        fondoActivo = Color1(new Color(0.85f, 0.45f, 0.12f, 1f));
        Font fuente = null;
        titulo = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, font = fuente };
        titulo.normal.textColor = new Color(1f, 0.6f, 0.2f);
        seccion = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, margin = new RectOffset(0, 0, 4, 8) };
        seccion.normal.textColor = new Color(1f, 0.6f, 0.2f);
        texto = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
        texto.normal.textColor = new Color(0.8f, 0.83f, 0.88f);
        caja = new GUIStyle(GUI.skin.box) { padding = new RectOffset(18, 18, 16, 16) };
        caja.normal.background = fondoCaja;
        boton = new GUIStyle(GUI.skin.button) { fontSize = 18, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(14, 10, 6, 6), margin = new RectOffset(0, 0, 3, 3) };
        boton.normal.background = fondoBoton;
        boton.hover.background = fondoBotonHover;
        boton.active.background = fondoActivo;
        boton.normal.textColor = boton.hover.textColor = boton.active.textColor = Color.white;
        botonActivo = new GUIStyle(boton);
        botonActivo.normal.background = botonActivo.hover.background = fondoActivo;
        campo = new GUIStyle(GUI.skin.textField) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
        campo.normal.background = campo.focused.background = fondoBoton;
        campo.normal.textColor = campo.focused.textColor = Color.white;
    }

    private static Texture2D Color1(Color c)
    {
        var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }
}
