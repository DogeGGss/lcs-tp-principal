using TMPro;
using UnityEngine;
using UnityEngine.UI;

// F27 (Emotes), US 261: la rueda de emotes. Manteniendo apretada la tecla de Emotes (T, se cambia en Opciones) aparece en
// el centro de la pantalla con los 4 espacios de los emotes equipados (EmotesEquipados): arriba, derecha, abajo e
// izquierda, cada uno con su ícono y su nombre.
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
    private const float Radio = 190f, Ancho = 170f, Alto = 120f, Centro = 0.35f, Sensibilidad = 0.012f;
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
    private TextMeshProUGUI aviso;
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
        raiz.sizeDelta = new Vector2(2f * Radio + Ancho, 2f * Radio + Alto);

        // El centro: un círculo chico.
        Image centro = Caja("Centro", raiz, Vector2.zero, new Vector2(56f, 56f), ShopUIKit.HudColor);
        centro.gameObject.AddComponent<Outline>().effectColor = ShopUIKit.LineColor;

        fondos = new Image[EmotesEquipados.Espacios];
        iconos = new Image[EmotesEquipados.Espacios];
        nombres = new TextMeshProUGUI[EmotesEquipados.Espacios];
        for (int i = 0; i < EmotesEquipados.Espacios; i++)
        {
            Image fondo = Caja("Espacio " + (i + 1), raiz, Direcciones[i] * Radio, new Vector2(Ancho, Alto), ShopUIKit.PanelColor);
            fondo.gameObject.AddComponent<Outline>().effectColor = ShopUIKit.LineColor;
            fondos[i] = fondo;
            Image icono = Caja("Ícono", fondo.rectTransform, new Vector2(0f, 14f), new Vector2(64f, 64f), Color.white);
            icono.preserveAspect = true;
            iconos[i] = icono;
            nombres[i] = Texto("Nombre", fondo.rectTransform, new Vector2(0f, -38f), new Vector2(Ancho - 12f, 30f), 24f);
        }
        aviso = Texto("Aviso", raiz, new Vector2(0f, -Radio - Alto * 0.5f - 40f), new Vector2(700f, 40f), 26f);
        aviso.color = ShopUIKit.Hot;
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
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Ellipsis;
        return t;
    }

    // ---------- Abrir, elegir y cerrar ----------

    private void Update()
    {
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
            nombres[i].text = e != null ? e.nombre : "Vacío";
            iconos[i].sprite = e != null ? e.icono : null;
            iconos[i].enabled = e != null && e.icono != null;
        }
        aviso.text = EmotesEquipados.Alguno ? "" : "No tenés emotes equipados: equipalos desde tu perfil.";
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

    // El marcado, naranja y un poco más grande; los vacíos, apagados.
    private void Pintar()
    {
        for (int i = 0; i < fondos.Length; i++)
        {
            bool vacio = EmotesEquipados.En(i) < 0;
            bool esMarcado = i == marcado;
            fondos[i].color = esMarcado ? ShopUIKit.SelectedColor : ShopUIKit.PanelColor;
            fondos[i].GetComponent<Outline>().effectColor = esMarcado ? ShopUIKit.Accent : ShopUIKit.LineColor;
            fondos[i].rectTransform.localScale = Vector3.one * (esMarcado ? 1.08f : 1f);
            nombres[i].color = esMarcado ? ShopUIKit.Accent : vacio ? ShopUIKit.Mute : ShopUIKit.Ink;
        }
    }

    private void OnDisable()
    {
        if (!Abierta) return;
        Abierta = false;
        CameraLook.Quieta = false;
        KeyBindings.SinDisparar = false;
    }
}
