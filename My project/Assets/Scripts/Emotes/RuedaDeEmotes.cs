using TMPro;
using UnityEngine;
using UnityEngine.UI;

// F27 (Emotes), US 261: la rueda de emotes, como la de Fortnite. Manteniendo apretada la tecla de Emotes (T, se cambia en
// Opciones) aparece en el centro de la pantalla un anillo partido en 4 porciones, una por emote equipado
// (EmotesEquipados): arriba, derecha, abajo e izquierda, cada una con su ícono (o su nombre, si no tiene). En el centro,
// el nombre del marcado, y una flecha en el borde de adentro apunta hacia donde va el mouse.
// - CA1: mientras está abierta la cámara no gira (CameraLook.Quieta) y no se dispara ni se apunta
//   (KeyBindings.SinDisparar); moverse sí.
// - CA2: el mouse marca el espacio hacia el que se lo mueve (un puntero invisible que sale del centro); al soltar la tecla
//   se hace el emote marcado (Emotes.Hacer). Con el puntero en el centro no se hace nada.
// - CA3: Esc o el clic derecho la cierran sin hacer nada (Esc no abre la pausa).
// - CA4: no se abre muerto, en el aire ni con la tienda o la pausa abiertas.
// - CA5: un espacio vacío se ve vacío y no se puede marcar; sin ningún emote equipado, la rueda avisa que se equipan
//   desde el perfil.
// Se arma sola al cargar la escena y solo se abre con un jugador en la partida.
public class RuedaDeEmotes : MonoBehaviour
{
    private const float Afuera = 300f, Adentro = 130f, Hueco = 3f, Centro = 0.35f, Sensibilidad = 0.012f;
    private static readonly Color Porcion = new Color(0.05f, 0.06f, 0.09f, 0.78f);
    private static readonly Color PorcionMarcada = new Color(0.95f, 0.6f, 0.22f, 0.9f);
    private static readonly Color PorcionVacia = new Color(0.05f, 0.06f, 0.09f, 0.45f);
    private static readonly Vector2[] Direcciones = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };

    private static RuedaDeEmotes unica;
    private static int cerradaConEscEn = -1;

    /// <summary>La rueda está abierta.</summary>
    public static bool Abierta { get; private set; }

    /// <summary>Esc es de la rueda en este cuadro (la cierra): la pausa no se abre.</summary>
    public static bool UsaElEsc => Abierta || cerradaConEscEn == Time.frameCount;

    private RectTransform raiz;
    private Image[] fondos;
    private Image[] iconos;
    private TextMeshProUGUI[] nombres;
    private TextMeshProUGUI aviso, elegido, ayuda;
    private RectTransform flecha;
    private Vector2 puntero;
    private int marcado = -1;
    private float enElAireDesde = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Crear()
    {
        if (unica != null) return;
        var go = new GameObject("Rueda de emotes", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        DontDestroyOnLoad(go);
        unica = go.AddComponent<RuedaDeEmotes>();
    }

    private void Awake()
    {
        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        CanvasScaler escala = GetComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.matchWidthOrHeight = 0.5f;
        Armar();
        raiz.gameObject.SetActive(false);
    }

    // ---------- Armado ----------

    private void Armar()
    {
        raiz = Nodo("Rueda", (RectTransform)transform);
        raiz.anchorMin = raiz.anchorMax = new Vector2(0.5f, 0.5f);
        raiz.sizeDelta = new Vector2(2f * Afuera, 2f * Afuera);

        // Las 4 porciones del anillo (con un hueco entre una y otra), cada una con su ícono y su nombre en el medio.
        Sprite anillo = Anillo(Adentro / Afuera);
        fondos = new Image[EmotesEquipados.Espacios];
        iconos = new Image[EmotesEquipados.Espacios];
        nombres = new TextMeshProUGUI[EmotesEquipados.Espacios];
        float porcion = 360f / EmotesEquipados.Espacios;
        float medio = (Afuera + Adentro) * 0.5f;
        for (int i = 0; i < EmotesEquipados.Espacios; i++)
        {
            RectTransform r = Nodo("Espacio " + (i + 1), raiz);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(2f * Afuera, 2f * Afuera);
            Image fondo = r.gameObject.AddComponent<Image>();
            fondo.sprite = anillo;
            fondo.type = Image.Type.Filled;
            fondo.fillMethod = Image.FillMethod.Radial360;
            fondo.fillOrigin = (int)Image.Origin360.Top;
            fondo.fillClockwise = true;
            fondo.fillAmount = (porcion - 2f * Hueco) / 360f;
            fondo.raycastTarget = false;
            // Empieza (en el sentido del reloj, desde arriba) medio espacio antes de su dirección.
            r.localRotation = Quaternion.Euler(0f, 0f, -(i * porcion - porcion * 0.5f + Hueco));
            fondos[i] = fondo;

            Image icono = Caja("Ícono", raiz, Direcciones[i] * medio, new Vector2(96f, 96f), Color.white);
            icono.sprite = null;
            icono.type = Image.Type.Simple;
            icono.preserveAspect = true;
            iconos[i] = icono;
            nombres[i] = Texto("Nombre", raiz, Direcciones[i] * medio, new Vector2(Afuera - Adentro - 20f, 40f), 26f);
        }

        // El centro: un círculo con el nombre del marcado.
        Image centro = Caja("Centro", raiz, Vector2.zero, new Vector2(2f * Adentro - 24f, 2f * Adentro - 24f), new Color(0.03f, 0.04f, 0.06f, 0.82f));
        centro.sprite = Anillo(0f);
        centro.type = Image.Type.Simple;
        elegido = Texto("Elegido", raiz, new Vector2(0f, 10f), new Vector2(2f * Adentro - 50f, 50f), 34f);
        if (PantallaDeCarga.FuenteTitulo != null) elegido.font = PantallaDeCarga.FuenteTitulo;
        ayuda = Texto("Ayuda", raiz, new Vector2(0f, -30f), new Vector2(2f * Adentro - 50f, 30f), 20f);
        ayuda.color = ShopUIKit.Mute;

        // La flecha: un triángulo chico en el borde de adentro, que gira hacia donde va el mouse.
        flecha = Nodo("Flecha", raiz);
        flecha.anchorMin = flecha.anchorMax = new Vector2(0.5f, 0.5f);
        flecha.sizeDelta = Vector2.zero;
        RectTransform punta = Nodo("Punta", flecha);
        punta.anchorMin = punta.anchorMax = new Vector2(0.5f, 0.5f);
        punta.anchoredPosition = new Vector2(0f, Adentro - 14f);
        punta.sizeDelta = new Vector2(22f, 22f);
        punta.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Image p = punta.gameObject.AddComponent<Image>();
        p.color = ShopUIKit.Accent;
        p.raycastTarget = false;

        aviso = Texto("Aviso", raiz, new Vector2(0f, -Afuera - 36f), new Vector2(800f, 40f), 26f);
        aviso.color = ShopUIKit.Hot;
    }

    // Un anillo blanco (o un círculo, con adentro 0) de bordes suaves, para pintar con el color de cada porción.
    private static Sprite Anillo(float adentro)
    {
        const int Lado = 256;
        var tex = new Texture2D(Lado, Lado, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Anillo de la rueda" };
        var pixeles = new Color32[Lado * Lado];
        float c = (Lado - 1) * 0.5f, borde = 1.5f / Lado;
        for (int y = 0; y < Lado; y++)
            for (int x = 0; x < Lado; x++)
            {
                float r = new Vector2((x - c) / c, (y - c) / c).magnitude;
                float a = Mathf.Clamp01((1f - r) / borde) * (adentro > 0f ? Mathf.Clamp01((r - adentro) / borde) : 1f);
                pixeles[y * Lado + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(pixeles);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, Lado, Lado), new Vector2(0.5f, 0.5f), 100f);
    }

    private static RectTransform Nodo(string nombre, RectTransform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        var r = (RectTransform)go.transform;
        r.SetParent(padre, false);
        return r;
    }

    private static Image Caja(string nombre, RectTransform padre, Vector2 lugar, Vector2 tamano, Color color)
    {
        RectTransform r = Nodo(nombre, padre);
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = lugar;
        r.sizeDelta = tamano;
        Image img = r.gameObject.AddComponent<Image>();
        if (PantallaDeCarga.Redondeado != null) { img.sprite = PantallaDeCarga.Redondeado; img.type = Image.Type.Sliced; }
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI Texto(string nombre, RectTransform padre, Vector2 lugar, Vector2 tamano, float letra)
    {
        RectTransform r = Nodo(nombre, padre);
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = lugar;
        r.sizeDelta = tamano;
        TextMeshProUGUI t = r.gameObject.AddComponent<TextMeshProUGUI>();
        if (PantallaDeCarga.FuenteEtiqueta != null) t.font = PantallaDeCarga.FuenteEtiqueta;
        t.fontSize = letra;
        t.alignment = TextAlignmentOptions.Center;
        t.color = ShopUIKit.Ink;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        return t;
    }

    // ---------- Abrir, elegir y cerrar ----------

    private void Update()
    {
        if (!Emotes.Disponibles) return; // entrega del Sprint 3: sin emotes, la T no abre la rueda
        if (!Abierta)
        {
            ActualizarEnElAire();
            if (KeyBindings.Down(GameAction.Emotes) && SePuedeAbrir()) Abrir();
            return;
        }

        // CA3: Esc o clic derecho, sin hacer nada.
        if (Input.GetKeyDown(KeyCode.Escape)) { cerradaConEscEn = Time.frameCount; Cerrar(); return; }
        if (Input.GetMouseButtonDown(1)) { Cerrar(); return; }
        // Si deja de poder (muere, se abre la tienda...), se cierra sola.
        if (!SePuedeAbrir()) { Cerrar(); return; }

        // CA2: el puntero sale del centro con el mouse; marca el espacio hacia el que apunta (si no está vacío).
        puntero += new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * (Sensibilidad * KeyBindings.Sensitivity * 10f);
        puntero = Vector2.ClampMagnitude(puntero, 1f);
        int hacia = -1;
        if (puntero.magnitude >= Centro)
        {
            float mejor = -2f;
            for (int i = 0; i < Direcciones.Length; i++)
            {
                float d = Vector2.Dot(puntero.normalized, Direcciones[i]);
                if (d > mejor) { mejor = d; hacia = i; }
            }
            if (EmotesEquipados.En(hacia) < 0) hacia = -1; // CA5: los vacíos no se marcan
        }
        if (hacia != marcado) { marcado = hacia; Pintar(); }
        flecha.gameObject.SetActive(puntero.magnitude >= Centro * 0.5f);
        if (puntero.sqrMagnitude > 1e-6f) flecha.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-puntero.x, puntero.y) * Mathf.Rad2Deg);

        if (!KeyBindings.Held(GameAction.Emotes))
        {
            int emote = marcado >= 0 ? EmotesEquipados.En(marcado) : -1;
            Cerrar();
            if (emote >= 0) Emotes.Hacer(emote);
        }
    }

    // CA4: no muerto, no en el aire, sin la tienda ni la pausa (y con un jugador en la partida).
    private bool SePuedeAbrir()
    {
        if (ShopUI.IsOpen || PauseMenu.IsPaused) return false;
        PlayerMovement jugador = Jugador();
        if (jugador == null) return false;
        HealthSystem vida = jugador.GetComponent<HealthSystem>();
        if (vida != null && vida.currentHealth <= 0) return false;
        return enElAireDesde < 0f || Time.time - enElAireDesde < 0.15f; // un instante sin piso no cuenta (escalones)
    }

    private void ActualizarEnElAire()
    {
        PlayerMovement jugador = Jugador();
        CharacterController cuerpo = jugador != null ? jugador.GetComponent<CharacterController>() : null;
        if (cuerpo == null || cuerpo.isGrounded) enElAireDesde = -1f;
        else if (enElAireDesde < 0f) enElAireDesde = Time.time;
    }

    private static PlayerMovement Jugador()
    {
        Camera camara = Camera.main;
        return camara != null ? camara.GetComponentInParent<PlayerMovement>() : null;
    }

    private void Abrir()
    {
        Abierta = true;
        CameraLook.Quieta = true;
        KeyBindings.SinDisparar = true;
        puntero = Vector2.zero;
        marcado = -1;
        CatalogoDeEmotes catalogo = CatalogoDeEmotes.Actual;
        for (int i = 0; i < EmotesEquipados.Espacios; i++)
        {
            CatalogoDeEmotes.Emote e = catalogo.De(EmotesEquipados.En(i));
            bool conIcono = e != null && e.icono != null;
            nombres[i].text = e == null ? "Vacío" : conIcono ? "" : e.nombre; // con ícono, el nombre va en el centro
            iconos[i].sprite = conIcono ? e.icono : null;
            iconos[i].enabled = conIcono;
        }
        aviso.text = EmotesEquipados.Alguno ? "" : "No tenés emotes equipados: equipalos desde tu perfil.";
        flecha.gameObject.SetActive(false);
        Pintar();
        raiz.gameObject.SetActive(true);
    }

    private void Cerrar()
    {
        Abierta = false;
        CameraLook.Quieta = false;
        KeyBindings.SinDisparar = false;
        raiz.gameObject.SetActive(false);
    }

    // El marcado, naranja y un poco más grande; los vacíos, más transparentes. En el centro, su nombre.
    private void Pintar()
    {
        for (int i = 0; i < fondos.Length; i++)
        {
            bool vacio = EmotesEquipados.En(i) < 0;
            bool esMarcado = i == marcado;
            fondos[i].color = esMarcado ? PorcionMarcada : vacio ? PorcionVacia : Porcion;
            fondos[i].rectTransform.localScale = Vector3.one * (esMarcado ? 1.04f : 1f);
            iconos[i].rectTransform.localScale = Vector3.one * (esMarcado ? 1.15f : 1f);
            nombres[i].color = esMarcado ? ShopUIKit.AccentInk : vacio ? ShopUIKit.Mute : ShopUIKit.Ink;
        }
        CatalogoDeEmotes.Emote e = marcado >= 0 ? CatalogoDeEmotes.Actual.De(EmotesEquipados.En(marcado)) : null;
        elegido.text = e != null ? e.nombre : "";
        ayuda.text = e != null ? "Soltá " + KeyBindings.Label(GameAction.Emotes) : "Mové el mouse";
    }

    private void OnDisable()
    {
        if (!Abierta) return;
        Abierta = false;
        CameraLook.Quieta = false;
        KeyBindings.SinDisparar = false;
    }
}
