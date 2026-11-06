using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using static ShopUIKit;

// F08 (Modo Invasión), US 293: elegir 1 de 3 cuando se abre el puesto de suministros (US 292). Lo agrega
// PartidaDeInvasion. PROVISIONAL: la pantalla es para ver cómo queda; las cartas todavía no tienen efecto (US 295 y
// US 296) y las armas siguen la regla de la US 294.
// - CA1: 3 opciones, cada una con su nombre, su rareza, qué hace y con qué armas rinde. Se elige con el mouse o con
//   1, 2 y 3. Mientras se elige el respiro no corre (OleadasDeInvasion espera a TerminoLaEleccion). El color dice qué
//   es: blanca común, azul rara, violeta épica y roja un arma.
// - CA2: al abrirse, el puesto le suma a cada arma la mitad de su munición máxima (toda, con Logística: US 314).
// La pantalla no aparece de golpe: espera a que se vaya el cartel de la oleada, oscurece de a poco y las cartas
// entran de a una (también al volver a tirar).
// - CA3: la rareza de cada carta según la oleada (CartaDeInvasion.RarezaAlAzar).
// - CA4: «Volver a tirar» (R) cambia las 3 opciones por 150 puntos; en el mismo puesto, cada vez 100 más.
// - CA5: las cartas duran toda la partida y, si se repiten, se suman (se ven con Tab: MarcadorInvasion.Cartas), como
//   mucho 3 veces: después de la tercera esa carta no vuelve a salir. Las de una sola vez no vuelven a salir. Cada
//   carta sabe cuántas veces se tiene (Veces) para hacer más efecto la segunda y la tercera.
// - CA6: después de elegir arranca el respiro de 20 s.
// US 294 (provisional): en 1 de cada 3 tiradas una de las opciones es un arma que no se tiene, que reemplaza la del
// mismo lugar y viene con la munición llena.
// US 303: al superar una oleada del Jefe la elección se abre siempre, aunque el puesto no esté cargado (CA2), recién
// después de "Seguir" (CA4): es la elección del Jefe, sin comunes y con más chance de raras, épicas y legendarias (vale
// también al volver a tirar, solo en esa elección). Las legendarias no salen antes del primer Jefe (CA3).
// US 332, Entrega inmediata: con la carta, la elección se abre apenas el puesto llega al 100 % peleando, sin esperar
// al final de la oleada (no en las oleadas del Jefe, que tienen la suya). Jugando solo el juego se pausa mientras se
// elige. La munición del puesto se sigue dando al terminar la oleada, cuando el puesto se abre (ya sin elección).
// De a varios, si se pausa o no todavía no está decidido (el multijugador entra en otro sprint).
// De a varios (US 289) cada uno va a elegir lo suyo, con un tope de 30 s; por ahora se juega solo y no hay tope.
public class EleccionDeInvasion : MonoBehaviour
{
    public const int PrimeraTirada = 150, MasPorTirada = 100; // CA4
    private const float AnchoCarta = 360f, AltoCarta = 470f, Separacion = 36f;

    /// <summary>La pantalla de la elección está abierta.</summary>
    public static bool Abierta { get; private set; }
    /// <summary>La elección de la partida en curso (o null).</summary>
    public static EleccionDeInvasion Actual { get; private set; }

    /// <summary>CA5: cuántas veces se eligió esa carta en la partida (0 si no se tiene).</summary>
    public static int Veces(CartaDeInvasion carta) =>
        Actual != null && carta != null && Actual.elegidas.TryGetValue(carta, out int n) ? n : 0;

    /// <summary>CA5: cuántas veces se eligió la carta con ese nombre en la partida (0 si no se tiene).</summary>
    public static int Veces(string nombre)
    {
        foreach (CartaDeInvasion c in CartaDeInvasion.Todas) if (c.nombre == nombre) return Veces(c);
        return 0;
    }

    private class Opcion
    {
        public CartaDeInvasion carta;
        public ShopItem arma;
    }

    private OleadasDeInvasion oleadas;
    private PlayerLoadout equipo;
    private bool pendiente;           // US 303: superó una oleada del Jefe: se elige al seguir
    private bool delJefe;             // US 303, CA2: la que está abierta es la elección del Jefe
    private bool inmediata;           // US 332: la que está abierta es en medio de la oleada (Entrega inmediata)
    private float escalaDeTiempo = 1f; // US 332: la del juego antes de pausarlo
    private TextMeshProUGUI antetitulo;
    private int costo = PrimeraTirada;
    private readonly List<Opcion> opciones = new List<Opcion>();
    private readonly Dictionary<CartaDeInvasion, int> elegidas = new Dictionary<CartaDeInvasion, int>();
    private readonly List<CartaDeInvasion> orden = new List<CartaDeInvasion>();
    private readonly List<Behaviour> bloqueados = new List<Behaviour>();

    private const float Espera = 0.6f, Fundido = 0.35f, EntradaCarta = 0.4f, Escalon = 0.12f;
    private bool visible;
    private float mostrarEn, mostradaEn, cartasDesde;
    private CanvasGroup grupo, cabezaGrupo;
    private RectTransform cabeza;
    private readonly List<(CanvasGroup grupo, RectTransform rect, float x)> cartas = new List<(CanvasGroup, RectTransform, float)>();
    private RectTransform lienzo, fila;
    private TextMeshProUGUI tirarTexto, puntosTexto;
    private UnityEngine.UI.Image tirarFondo;
    private TMP_FontAsset display, label;
    private Sprite redondeado;

    private void OnEnable()
    {
        Actual = this;
        PuestoDeSuministros.Abrio += AlAbrirElPuesto;
        PuestoDeSuministros.SeCargo += AlCargarseElPuesto;
        OleadasDeInvasion.Empezo += AlEmpezar;
        OleadasDeInvasion.Supero += AlSuperar;
    }

    private void OnDisable()
    {
        if (Actual == this) Actual = null;
        PuestoDeSuministros.Abrio -= AlAbrirElPuesto;
        PuestoDeSuministros.SeCargo -= AlCargarseElPuesto;
        OleadasDeInvasion.Empezo -= AlEmpezar;
        OleadasDeInvasion.Supero -= AlSuperar;
    }

    private void OnDestroy()
    {
        if (Abierta) Cerrar();
        if (lienzo != null) Destroy(lienzo.gameObject);
    }

    private void AlAbrirElPuesto()
    {
        if (oleadas == null) oleadas = GetComponent<OleadasDeInvasion>();
        // En las oleadas del Jefe la elección es la del Jefe, que se abre siempre (AlSuperar).
        if (oleadas != null && oleadas.DeJefe) return;
        // US 332: la elección ya se hizo apenas se cargó (Entrega inmediata): ahora solo la munición.
        if (PuestoDeSuministros.Actual != null && PuestoDeSuministros.Actual.Entregado) { DarMunicion(); return; }
        Abrir(false);
    }

    // US 332: con Entrega inmediata, la elección se abre apenas se carga el puesto (salvo en las oleadas del Jefe).
    private void AlCargarseElPuesto(PuestoDeSuministros puesto)
    {
        if (Abierta || Veces("Entrega inmediata") <= 0) return;
        if (oleadas == null) oleadas = GetComponent<OleadasDeInvasion>();
        if (oleadas != null && oleadas.DeJefe) return;
        inmediata = true; // (antes de abrir: la munición se da al terminar la oleada)
        Abrir(false);
        if (!Abierta) { inmediata = false; return; }
        escalaDeTiempo = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f; // jugando solo, el juego se pausa mientras se elige
    }

    // US 303, CA2 y CA4: al superar una oleada del Jefe primero sale "Jefe derrotado"; la elección del Jefe espera a
    // que se siga (Retener), esté o no cargado el puesto.
    private void AlSuperar(int oleada)
    {
        if (OleadasDeInvasion.EsDeJefe(oleada)) pendiente = true;
    }

    // Ir a la oleada 10 (prueba) puede arrancar una oleada con la elección abierta.
    private void AlEmpezar(int oleada)
    {
        pendiente = false;
        if (inmediata) { inmediata = false; Time.timeScale = escalaDeTiempo; } // (el puesto nuevo no se entrega)
        if (Abierta) Cerrar();
    }

    /// <summary>Lo llama OleadasDeInvasion antes de arrancar el respiro: true si tiene que esperar a la elección.</summary>
    public bool Retener()
    {
        if (Abierta) return true;
        if (!pendiente) return false;
        pendiente = false;
        Abrir(true);
        return Abierta;
    }

    // ---------- Abrir y cerrar ----------

    private void Abrir(bool delJefe)
    {
        this.delJefe = delJefe;
        if (MatchHud.Instance == null) return;
        if (equipo == null) equipo = FindFirstObjectByType<PlayerLoadout>();
        if (lienzo == null) Armar();

        if (!inmediata) DarMunicion(); // US 332: con Entrega inmediata, al terminar la oleada

        Abierta = true; // el respiro ya espera, aunque la pantalla todavía no se vea
        visible = false;
        costo = PrimeraTirada;
        mostrarEn = Time.unscaledTime + Espera;
    }

    // CA2: la mitad de la munición máxima de cada arma (con Logística, toda: US 314).
    private void DarMunicion()
    {
        if (equipo == null) equipo = FindFirstObjectByType<PlayerLoadout>();
        WeaponSwitcher armas = equipo != null ? equipo.GetComponentInChildren<WeaponSwitcher>(true) : null;
        if (armas == null) return;
        if (Veces("Logística") > 0) armas.RellenarTodas();
        else armas.RecargarMitad();
    }

    // Cuando se fue el cartel de "Oleada N superada" (y un momento más), aparece la pantalla.
    private void Mostrar()
    {
        visible = true;
        mostradaEn = Time.unscaledTime;
        grupo.alpha = 0f;
        lienzo.gameObject.SetActive(true);
        if (antetitulo != null) antetitulo.text = delJefe ? "Elección del Jefe" : inmediata ? "Entrega inmediata" : "Puesto de suministros";
        Bloquear();
        Tirar();
    }

    private void Animar()
    {
        float ahora = Time.unscaledTime;
        grupo.alpha = Suave((ahora - mostradaEn) / Fundido);
        float c = Suave((ahora - mostradaEn - 0.1f) / Fundido);
        cabeza.anchoredPosition = new Vector2(0f, -110f + 24f * (1f - c));
        cabezaGrupo.alpha = c;
        for (int i = 0; i < cartas.Count; i++)
        {
            var (g, r, x) = cartas[i];
            if (g == null) continue;
            float t = Suave((ahora - cartasDesde - i * Escalon) / EntradaCarta);
            g.alpha = t;
            r.anchoredPosition = new Vector2(x, -70f * (1f - t));
            r.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, t);
        }
    }

    private static float Suave(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t) * (1f - t); // frena al llegar
    }

    // Se puede elegir cuando ya entraron las cartas.
    private bool Lista => visible && Time.unscaledTime - cartasDesde >= Escalon * 2f + EntradaCarta * 0.8f;

    private void Cerrar()
    {
        if (inmediata) // US 332: sigue la oleada
        {
            inmediata = false;
            Time.timeScale = escalaDeTiempo;
            if (PuestoDeSuministros.Actual != null) PuestoDeSuministros.Actual.Entregar();
        }
        Abierta = false;
        visible = false;
        if (lienzo != null) lienzo.gameObject.SetActive(false);
        Desbloquear();
    }

    // Como la tienda: con la elección abierta el mouse maneja la pantalla y no se mira ni se dispara.
    private void Bloquear()
    {
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

    private void Bloquear<T>(T[] componentes) where T : Behaviour
    {
        foreach (T c in componentes)
        {
            if (!c.enabled) continue;
            c.enabled = false;
            bloqueados.Add(c);
        }
    }

    private void Desbloquear()
    {
        foreach (Behaviour c in bloqueados) if (c != null) c.enabled = true;
        bloqueados.Clear();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ---------- Las opciones ----------

    private void Tirar()
    {
        opciones.Clear();
        int oleada = oleadas != null ? oleadas.Oleada : 1;

        // US 294, CA3: como mucho un arma, en 1 de cada 3 tiradas.
        ShopItem arma = Random.value < 1f / 3f ? ArmaAlAzar() : null;
        int lugarArma = arma != null ? Random.Range(0, 3) : -1;
        var yaSalieron = new HashSet<CartaDeInvasion>();
        for (int i = 0; i < 3; i++)
        {
            if (i == lugarArma) { opciones.Add(new Opcion { arma = arma }); continue; }
            CartaDeInvasion carta = CartaAlAzar(oleada, yaSalieron);
            if (carta == null) continue;
            yaSalieron.Add(carta);
            opciones.Add(new Opcion { carta = carta });
        }
        Dibujar();
    }

    // CA3 (y US 303, CA2 y CA3): una carta de la rareza que tocó; si no hay ninguna de esa, de la más cercana (con
    // empate, la de abajo). Sin legendarias antes del primer Jefe y sin comunes en la elección del Jefe.
    private CartaDeInvasion CartaAlAzar(int oleada, HashSet<CartaDeInvasion> yaSalieron)
    {
        bool despuesDelJefe = oleadas != null && oleadas.JefesDerrotados > 0;
        RarezaDeCarta buscada = CartaDeInvasion.RarezaAlAzar(oleada, despuesDelJefe, delJefe);
        var posibles = new List<CartaDeInvasion>();
        int mejor = int.MaxValue;
        foreach (CartaDeInvasion c in CartaDeInvasion.Todas)
        {
            if (yaSalieron.Contains(c) || Veces(c) >= c.Maximo) continue; // CA5: ya la tiene todas las veces que se puede
            if (c.puedeSalir != null && !c.puedeSalir()) continue;          // US 331: Sin respaldo y Acople, por ejemplo
            if (c.rareza == RarezaDeCarta.Legendaria && !despuesDelJefe) continue; // US 303, CA3
            if (c.rareza == RarezaDeCarta.Comun && delJefe) continue;              // US 303, CA2
            int d = (int)c.rareza - (int)buscada;
            int lejos = d >= 0 ? d * 2 : -d * 2 - 1; // la de abajo gana el empate
            if (lejos > mejor) continue;
            if (lejos < mejor) { mejor = lejos; posibles.Clear(); }
            posibles.Add(c);
        }
        return posibles.Count > 0 ? posibles[Random.Range(0, posibles.Count)] : null;
    }

    // US 294, CA1 y CA2: un arma del catálogo que no se tiene (o una de las que solo existen en Invasión, como el Gran
    // Capitán: US 287).
    private ShopItem ArmaAlAzar()
    {
        if (equipo == null || equipo.Catalog == null) return null;
        var posibles = new List<ShopItem>();
        var armas = new List<ShopItem>(equipo.Catalog.items);
        armas.AddRange(ConfigInvasion.Actual.armasSoloInvasion);
        foreach (ShopItem item in armas)
        {
            if (item == null || equipo.IsEquipped(item)) continue;
            if (item.kind != ShopItemKind.PrimaryWeapon && item.kind != ShopItemKind.SecondaryWeapon) continue;
            if (item == equipo.Catalog.starterSecondary) continue; // la del arranque no es una novedad
            if (item.kind == ShopItemKind.SecondaryWeapon && Veces("Sin respaldo") > 0) continue; // US 331: ya no tiene secundaria
            posibles.Add(item);
        }
        return posibles.Count > 0 ? posibles[Random.Range(0, posibles.Count)] : null;
    }

    private void Elegir(int i)
    {
        if (!Abierta || !Lista || i < 0 || i >= opciones.Count) return;
        Opcion o = opciones[i];
        if (o.arma != null) equipo.Recibir(o.arma); // US 294, CA4
        else if (o.carta != null)
        {
            int veces = elegidas.TryGetValue(o.carta, out int n) ? n + 1 : 1; // CA5: se suman
            elegidas[o.carta] = veces;
            if (!orden.Contains(o.carta)) orden.Add(o.carta);
            o.carta.aplicar?.Invoke(equipo != null ? equipo.gameObject : null, veces);
            ActualizarCartas();
        }
        bool enLaOleada = inmediata;
        Cerrar();
        if (!enLaOleada && oleadas != null) oleadas.TerminoLaEleccion(); // CA6: arranca el respiro (US 332: en medio de la oleada no)
    }

    /// <summary>Para probar (Riftwalker > Invasión > Darme una carta): la suma como si se la hubiera elegido.</summary>
    public void Dar(CartaDeInvasion carta)
    {
        if (carta == null || Veces(carta) >= carta.Maximo) { Debug.Log($"Ya tenés {carta?.nombre} todas las veces que se puede."); return; }
        if (equipo == null) equipo = FindFirstObjectByType<PlayerLoadout>(); // (sin haber abierto el puesto todavía)
        int veces = Veces(carta) + 1;
        elegidas[carta] = veces;
        if (!orden.Contains(carta)) orden.Add(carta);
        carta.aplicar?.Invoke(equipo != null ? equipo.gameObject : null, veces);
        ActualizarCartas();
        Debug.Log($"Carta de prueba: {carta.nombre} ({veces} de {carta.Maximo}).");
    }

    // US 298: la lista de «Tus cartas» (con Tab), con las repetidas sumadas.
    private void ActualizarCartas()
    {
        MarcadorInvasion.Cartas.Clear();
        foreach (CartaDeInvasion c in orden)
        {
            int n = elegidas[c];
            MarcadorInvasion.Cartas.Add(n > 1 ? $"{c.nombre} ×{n}" : c.nombre);
        }
    }

    private void VolverATirar()
    {
        if (!Abierta || !Lista || equipo == null || equipo.Wallet == null) return;
        if (equipo.Wallet.Money < costo) { MatchHud.Warn($"Te faltan puntos para volver a tirar ({costo})", 2f); return; }
        equipo.Wallet.Spend(costo);
        costo += MasPorTirada; // CA4
        Tirar();
    }

    private void Update()
    {
        if (!Abierta) return;
        if (!visible)
        {
            if (MatchHud.BannerVisible) mostrarEn = Mathf.Max(mostrarEn, Time.unscaledTime + 0.3f);
            if (Time.unscaledTime >= mostrarEn) Mostrar();
            return;
        }
        Animar();
        if (!Lista) return;
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) Elegir(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) Elegir(1);
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) Elegir(2);
        else if (Input.GetKeyDown(KeyCode.R)) VolverATirar();
        if (Abierta && Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        if (Abierta) ActualizarTirar();
    }

    // ---------- Pantalla ----------

    private void Armar()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        display = MatchHud.Instance.DisplayFont;
        label = MatchHud.Instance.LabelFont;
        redondeado = MatchHud.Instance.Rounded;

        var go = new GameObject("Elección del puesto", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        go.layer = 5;
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 48; // sobre el HUD y el marcador, debajo de la tienda (50) y la pausa
        var escala = go.GetComponent<UnityEngine.UI.CanvasScaler>();
        escala.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        lienzo = (RectTransform)go.transform;

        grupo = go.AddComponent<CanvasGroup>();
        Image(Stretch(Node("Fondo", lienzo)), null, new Color(0f, 0f, 0f, DarkAlpha(0.72f)), 0f, true);

        cabeza = Node("Titulo", lienzo);
        cabezaGrupo = cabeza.gameObject.AddComponent<CanvasGroup>();
        cabeza.anchorMin = cabeza.anchorMax = cabeza.pivot = new Vector2(0.5f, 1f);
        cabeza.anchoredPosition = new Vector2(0f, -110f);
        cabeza.sizeDelta = new Vector2(1200f, 110f);
        antetitulo = Text(Place(Node("Antetitulo", cabeza), 0f, 0f, 1200f, 26f), label, 20f, MarcadorInvasion.Verde, TextAlignmentOptions.Center, 16f, true);
        antetitulo.text = "Puesto de suministros";
        Text(Place(Node("Elegi", cabeza), 0f, 30f, 1200f, 64f), display, 58f, Ink, TextAlignmentOptions.Center, 4f, true)
            .text = "Elegí una";

        fila = Node("Opciones", lienzo);
        fila.anchorMin = fila.anchorMax = fila.pivot = new Vector2(0.5f, 0.5f);
        fila.anchoredPosition = new Vector2(0f, -10f);
        fila.sizeDelta = new Vector2(AnchoCarta * 3f + Separacion * 2f, AltoCarta);

        // CA4: volver a tirar.
        RectTransform tirar = Node("VolverATirar", lienzo);
        tirar.anchorMin = tirar.anchorMax = tirar.pivot = new Vector2(0.5f, 0f);
        tirar.anchoredPosition = new Vector2(0f, 120f);
        tirar.sizeDelta = new Vector2(420f, 52f);
        tirarFondo = Image(tirar, redondeado, ChipColor, 8f, true);
        var boton = tirar.gameObject.AddComponent<UnityEngine.UI.Button>();
        boton.targetGraphic = tirarFondo;
        boton.onClick.AddListener(VolverATirar);
        tirarTexto = Text(Stretch(Node("Texto", tirar)), label, 20f, Ink, TextAlignmentOptions.Center, 8f, true);
        puntosTexto = Text(Node("Puntos", lienzo), label, 17f, Mute, TextAlignmentOptions.Center, 8f, true);
        RectTransform pr = puntosTexto.rectTransform;
        pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 0f);
        pr.anchoredPosition = new Vector2(0f, 84f);
        pr.sizeDelta = new Vector2(600f, 26f);

        lienzo.gameObject.SetActive(false);
    }

    private void ActualizarTirar()
    {
        int puntos = equipo != null && equipo.Wallet != null ? equipo.Wallet.Money : 0;
        bool alcanza = puntos >= costo;
        string texto = $"R  ·  Volver a tirar  ·  {costo} pts";
        if (tirarTexto.text != texto) tirarTexto.text = texto;
        tirarTexto.color = alcanza ? Ink : Mute;
        string tenes = $"Tenés {puntos} pts";
        if (puntosTexto.text != tenes) puntosTexto.text = tenes;
    }

    private void Dibujar()
    {
        for (int i = fila.childCount - 1; i >= 0; i--) Destroy(fila.GetChild(i).gameObject);
        cartas.Clear();
        cartasDesde = Time.unscaledTime + (Time.unscaledTime - mostradaEn < 0.2f ? 0.25f : 0f); // la primera vez, después del fondo
        for (int i = 0; i < opciones.Count; i++)
        {
            float x = (opciones.Count - 1) * -0.5f * (AnchoCarta + Separacion) + i * (AnchoCarta + Separacion);
            DibujarCarta(i, opciones[i], x);
        }
        ActualizarTirar();
    }

    // US 334: avisa cuando la carta queda sin uso con Alimentación directa (o, si es Alimentación directa, a cuáles de
    // las que ya se tienen deja sin uso). null: no hay nada que avisar.
    private static string AvisoDeAlimentacion(CartaDeInvasion carta)
    {
        if (carta == null) return null;
        if (carta.nombre == CartaDeInvasion.AlimentacionDirecta)
        {
            var anuladas = new List<string>();
            foreach (string n in CartaDeInvasion.SinUsoConAlimentacion) if (Veces(n) > 0) anuladas.Add(n);
            return anuladas.Count > 0 ? "Deja sin uso: " + string.Join(", ", anuladas) : null;
        }
        if (Veces(CartaDeInvasion.AlimentacionDirecta) > 0 && System.Array.IndexOf(CartaDeInvasion.SinUsoConAlimentacion, carta.nombre) >= 0)
            return "Sin uso con Alimentación directa";
        return null;
    }

    private void DibujarCarta(int i, Opcion o, float x)
    {
        bool esArma = o.arma != null;
        Color color = esArma ? CartaDeInvasion.ColorArma : CartaDeInvasion.ColorDe(o.carta.rareza);
        string rareza = esArma ? "Arma" : CartaDeInvasion.NombreDe(o.carta.rareza);
        string nombre = esArma ? NombreDelArma(o.arma) : o.carta.nombre;
        string efecto = esArma ? QueReemplaza(o.arma) : o.carta.efecto;
        string rinde = esArma ? (o.arma.kind == ShopItemKind.PrimaryWeapon ? "Arma principal" : "Arma secundaria")
                              : "Rinde con: " + o.carta.rindeCon;
        if (!esArma) rinde = AvisoDeAlimentacion(o.carta) ?? rinde; // US 334

        RectTransform carta = Node("Opcion" + (i + 1), fila);
        carta.anchorMin = carta.anchorMax = carta.pivot = new Vector2(0.5f, 0.5f);
        carta.anchoredPosition = new Vector2(x, 0f);
        carta.sizeDelta = new Vector2(AnchoCarta, AltoCarta);
        CanvasGroup grupoCarta = carta.gameObject.AddComponent<CanvasGroup>();
        grupoCarta.alpha = 0f;
        cartas.Add((grupoCarta, carta, x));
        MarcadorInvasion.Figura<FormaRedondeada>(Stretch(Node("Fondo", carta)))
            .Pintar(Rgb(12, 14, 19, 0.96f), color, 16f, 3f);

        // El clic en toda la carta.
        UnityEngine.UI.Image zona = Image(Stretch(Node("Zona", carta)), null, new Color(1f, 1f, 1f, 0f), 0f, true);
        var boton = zona.gameObject.AddComponent<UnityEngine.UI.Button>();
        boton.targetGraphic = zona;
        var colores = boton.colors;
        colores.highlightedColor = new Color(1f, 1f, 1f, 0.06f);
        colores.pressedColor = new Color(1f, 1f, 1f, 0.12f);
        colores.normalColor = new Color(1f, 1f, 1f, 0f);
        boton.colors = colores;
        int indice = i;
        boton.onClick.AddListener(() => Elegir(indice));

        // La franja de arriba con la rareza.
        Image(Place(Node("Franja", carta), 3f, 3f, AnchoCarta - 6f, 46f), redondeado, WithAlpha(color, 0.18f), 12f);
        Text(Place(Node("Rareza", carta), 0f, 3f, AnchoCarta, 46f), label, 18f, color, TextAlignmentOptions.Center, 18f, true).text = rareza;

        // El ícono del arma, si es un arma.
        float y = 76f;
        if (esArma && o.arma.icon != null)
        {
            UnityEngine.UI.Image icono = Image(Place(Node("Icono", carta), 40f, y, AnchoCarta - 80f, 90f), o.arma.icon, Ink);
            icono.preserveAspect = true;
            y += 104f;
        }

        TextMeshProUGUI titulo = Text(Place(Node("Nombre", carta), 24f, y, AnchoCarta - 48f, 92f), display, 36f, Ink, TextAlignmentOptions.Top, 4f, true);
        titulo.text = nombre;
        titulo.textWrappingMode = TextWrappingModes.Normal;
        y += 100f;
        Image(Place(Node("Linea", carta), AnchoCarta / 2f - 40f, y, 80f, 3f), null, color);
        y += 20f;
        TextMeshProUGUI detalle = Text(Place(Node("Efecto", carta), 28f, y, AnchoCarta - 56f, AltoCarta - y - 90f), label, 21f, Soft, TextAlignmentOptions.Top);
        detalle.text = efecto;
        detalle.textWrappingMode = TextWrappingModes.Normal;

        Text(Place(Node("Rinde", carta), 24f, AltoCarta - 84f, AnchoCarta - 48f, 24f), label, 16f, Mute, TextAlignmentOptions.Center, 6f, true).text = rinde;

        // La tecla.
        RectTransform tecla = Place(Node("Tecla", carta), AnchoCarta / 2f - 16f, AltoCarta - 50f, 32f, 32f);
        Image(tecla, redondeado, color, 6f);
        TextMeshProUGUI numero = Text(Stretch(Node("Numero", tecla)), display, 18f, KeyInk, TextAlignmentOptions.Center);
        numero.text = (i + 1).ToString();
    }

    private static string NombreDelArma(ShopItem arma) =>
        !string.IsNullOrEmpty(arma.displayName) ? arma.displayName : !string.IsNullOrEmpty(arma.alias) ? arma.alias : arma.name;

    // US 294, CA4: la opción dice a cuál reemplaza.
    private string QueReemplaza(ShopItem arma)
    {
        ShopItem actual = equipo == null ? null : arma.kind == ShopItemKind.PrimaryWeapon ? equipo.Primary : equipo.Secondary;
        string lugar = arma.kind == ShopItemKind.PrimaryWeapon ? "principal" : "secundaria";
        return actual != null
            ? $"Reemplaza a tu {NombreDelArma(actual)}. Viene con la munición llena."
            : $"Va en tu espacio de arma {lugar}, que está vacío. Viene con la munición llena.";
    }
}
