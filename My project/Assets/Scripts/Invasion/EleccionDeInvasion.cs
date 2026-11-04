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
// - CA2: al abrirse, el puesto le suma a cada arma la mitad de su munición máxima.
// - CA3: la rareza de cada carta según la oleada (CartaDeInvasion.RarezaAlAzar).
// - CA4: «Volver a tirar» (R) cambia las 3 opciones por 150 puntos; en el mismo puesto, cada vez 100 más.
// - CA5: las cartas duran toda la partida y, si se repiten, se suman (se ven con Tab: MarcadorInvasion.Cartas). Las
//   de una sola vez no vuelven a salir.
// - CA6: después de elegir arranca el respiro de 20 s.
// US 294 (provisional): en 1 de cada 3 tiradas una de las opciones es un arma que no se tiene, que reemplaza la del
// mismo lugar y viene con la munición llena.
// De a varios (US 289) cada uno va a elegir lo suyo, con un tope de 30 s; por ahora se juega solo y no hay tope.
public class EleccionDeInvasion : MonoBehaviour
{
    public const int PrimeraTirada = 150, MasPorTirada = 100; // CA4
    private const float AnchoCarta = 360f, AltoCarta = 470f, Separacion = 36f;

    /// <summary>La pantalla de la elección está abierta.</summary>
    public static bool Abierta { get; private set; }

    private class Opcion
    {
        public CartaDeInvasion carta;
        public ShopItem arma;
    }

    private OleadasDeInvasion oleadas;
    private PlayerLoadout equipo;
    private bool pendiente;           // el puesto se abrió en la oleada 10: se elige al seguir en modo infinito
    private int costo = PrimeraTirada;
    private readonly List<Opcion> opciones = new List<Opcion>();
    private readonly HashSet<CartaDeInvasion> usadas = new HashSet<CartaDeInvasion>(); // CA5: las de una sola vez
    private readonly Dictionary<CartaDeInvasion, int> elegidas = new Dictionary<CartaDeInvasion, int>();
    private readonly List<CartaDeInvasion> orden = new List<CartaDeInvasion>();
    private readonly List<Behaviour> bloqueados = new List<Behaviour>();

    private RectTransform lienzo, fila;
    private TextMeshProUGUI tirarTexto, puntosTexto;
    private UnityEngine.UI.Image tirarFondo;
    private TMP_FontAsset display, label;
    private Sprite redondeado;

    private void OnEnable()
    {
        PuestoDeSuministros.Abrio += AlAbrirElPuesto;
        OleadasDeInvasion.Empezo += AlEmpezar;
    }

    private void OnDisable()
    {
        PuestoDeSuministros.Abrio -= AlAbrirElPuesto;
        OleadasDeInvasion.Empezo -= AlEmpezar;
    }

    private void OnDestroy()
    {
        if (Abierta) Cerrar();
        if (lienzo != null) Destroy(lienzo.gameObject);
    }

    private void AlAbrirElPuesto()
    {
        if (oleadas == null) oleadas = GetComponent<OleadasDeInvasion>();
        // En la oleada 10 primero sale "¡Sobreviviste!": la elección espera a que se siga en modo infinito.
        if (oleadas != null && oleadas.Oleada == OleadasDeInvasion.Ultima && !oleadas.Infinito) { pendiente = true; return; }
        Abrir();
    }

    // Ir a la oleada 10 (prueba) puede arrancar una oleada con la elección abierta.
    private void AlEmpezar(int oleada)
    {
        pendiente = false;
        if (Abierta) Cerrar();
    }

    /// <summary>Lo llama OleadasDeInvasion antes de arrancar el respiro: true si tiene que esperar a la elección.</summary>
    public bool Retener()
    {
        if (Abierta) return true;
        if (!pendiente) return false;
        pendiente = false;
        Abrir();
        return Abierta;
    }

    // ---------- Abrir y cerrar ----------

    private void Abrir()
    {
        if (MatchHud.Instance == null) return;
        if (equipo == null) equipo = FindFirstObjectByType<PlayerLoadout>();
        if (lienzo == null) Armar();

        // CA2: la mitad de la munición máxima de cada arma.
        WeaponSwitcher armas = equipo != null ? equipo.GetComponentInChildren<WeaponSwitcher>(true) : null;
        if (armas != null) armas.RecargarMitad();

        Abierta = true;
        costo = PrimeraTirada;
        lienzo.gameObject.SetActive(true);
        Bloquear();
        Tirar();
    }

    private void Cerrar()
    {
        Abierta = false;
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

    private CartaDeInvasion CartaAlAzar(int oleada, HashSet<CartaDeInvasion> yaSalieron)
    {
        RarezaDeCarta buscada = CartaDeInvasion.RarezaAlAzar(oleada);
        var mismas = new List<CartaDeInvasion>();
        var otras = new List<CartaDeInvasion>();
        foreach (CartaDeInvasion c in CartaDeInvasion.Todas)
        {
            if (c.rareza == RarezaDeCarta.Legendaria || yaSalieron.Contains(c) || (c.unaVez && usadas.Contains(c))) continue;
            (c.rareza == buscada ? mismas : otras).Add(c);
        }
        List<CartaDeInvasion> de = mismas.Count > 0 ? mismas : otras;
        return de.Count > 0 ? de[Random.Range(0, de.Count)] : null;
    }

    // US 294, CA1 y CA2: un arma del catálogo que no se tiene.
    private ShopItem ArmaAlAzar()
    {
        if (equipo == null || equipo.Catalog == null) return null;
        var posibles = new List<ShopItem>();
        foreach (ShopItem item in equipo.Catalog.items)
        {
            if (item == null || equipo.IsEquipped(item)) continue;
            if (item.kind != ShopItemKind.PrimaryWeapon && item.kind != ShopItemKind.SecondaryWeapon) continue;
            if (item == equipo.Catalog.starterSecondary) continue; // la del arranque no es una novedad
            posibles.Add(item);
        }
        return posibles.Count > 0 ? posibles[Random.Range(0, posibles.Count)] : null;
    }

    private void Elegir(int i)
    {
        if (!Abierta || i < 0 || i >= opciones.Count) return;
        Opcion o = opciones[i];
        if (o.arma != null) equipo.Recibir(o.arma); // US 294, CA4
        else if (o.carta != null)
        {
            elegidas[o.carta] = elegidas.TryGetValue(o.carta, out int n) ? n + 1 : 1; // CA5: se suman
            if (!orden.Contains(o.carta)) orden.Add(o.carta);
            if (o.carta.unaVez) usadas.Add(o.carta);
            o.carta.aplicar?.Invoke(equipo != null ? equipo.gameObject : null);
            ActualizarCartas();
        }
        Cerrar();
        if (oleadas != null) oleadas.TerminoLaEleccion(); // CA6: arranca el respiro
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
        if (!Abierta || equipo == null || equipo.Wallet == null) return;
        if (equipo.Wallet.Money < costo) { MatchHud.Warn($"Te faltan puntos para volver a tirar ({costo})", 2f); return; }
        equipo.Wallet.Spend(costo);
        costo += MasPorTirada; // CA4
        Tirar();
    }

    private void Update()
    {
        if (!Abierta) return;
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

        Image(Stretch(Node("Fondo", lienzo)), null, new Color(0f, 0f, 0f, DarkAlpha(0.72f)), 0f, true);

        RectTransform cabeza = Node("Titulo", lienzo);
        cabeza.anchorMin = cabeza.anchorMax = cabeza.pivot = new Vector2(0.5f, 1f);
        cabeza.anchoredPosition = new Vector2(0f, -110f);
        cabeza.sizeDelta = new Vector2(1200f, 110f);
        Text(Place(Node("Antetitulo", cabeza), 0f, 0f, 1200f, 26f), label, 20f, MarcadorInvasion.Verde, TextAlignmentOptions.Center, 16f, true)
            .text = "Puesto de suministros";
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
        for (int i = 0; i < opciones.Count; i++)
        {
            float x = (opciones.Count - 1) * -0.5f * (AnchoCarta + Separacion) + i * (AnchoCarta + Separacion);
            DibujarCarta(i, opciones[i], x);
        }
        ActualizarTirar();
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

        RectTransform carta = Node("Opcion" + (i + 1), fila);
        carta.anchorMin = carta.anchorMax = carta.pivot = new Vector2(0.5f, 0.5f);
        carta.anchoredPosition = new Vector2(x, 0f);
        carta.sizeDelta = new Vector2(AnchoCarta, AltoCarta);
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
