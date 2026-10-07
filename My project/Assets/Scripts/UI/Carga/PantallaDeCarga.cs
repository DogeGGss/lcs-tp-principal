using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using static ShopUIKit;
using Img = UnityEngine.UI.Image;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// Pantalla de carga (US 196): tapa el juego mientras se carga un mapa o el menú principal.
// - CA1: aparece al iniciar una partida (Táctico, Deathmatch o Zombie) hasta que el mapa está listo, y al volver al menú.
// - CA2: nombre del mapa y del modo, imagen del mapa (ConfigRed, campo "imagen"; si no tiene, fondo liso) y barra de avance.
// - CA3: un consejo al azar del modo o de los controles, con las teclas que tiene configuradas el jugador (US 155).
// - CA4: online muestra cuántos jugadores ya cargaron ("3 / 8 listos"). La partida empieza cuando cargaron todos
//   (o a los 20 s, para que un jugador colgado no deje a todos esperando).
// - CA5: el mapa se carga de fondo y el ícono de carga sigue girando.
// - CA6: tipografías, colores y vía del resto del juego (ShopUIKit).
// Se crea sola la primera vez que se usa y sigue entre escenas: no hay que ponerla en ninguna.
public class PantallaDeCarga : MonoBehaviour
{
    private const string PropListo = "pc.ok";   // propiedad de cada jugador: la escena que ya terminó de cargar
    private const string PropEscenaPhoton = "curScn"; // la escena que Photon les manda cargar a todos
    private const string EscenaMenu = "MenuPrincipal";
    private const float EsperaMaxima = 20f, TiempoMinimo = 0.8f;
    private const int Durmientes = 96, Rayos = 12;

    private static PantallaDeCarga actual;
    private static TMP_FontAsset displayFont, labelFont, bodyFont;
    private static Sprite rounded;

    private GameObject raiz;
    private Img imagenMapa;
    private UnityEngine.UI.AspectRatioFitter ajusteMapa;
    private TextMeshProUGUI modoTexto, mapaTexto, detalleTexto, consejoTexto, estadoTexto, listosTexto, pieTexto;
    private GameObject consejoCaja;
    private RectTransform relleno, cursor;
    private readonly Img[] durmientes = new Img[Durmientes], rayos = new Img[Rayos];

    private bool online, publicado;
    private string escena;
    private float avance, desde, cargadoEn = -1f;
    private readonly List<Behaviour> trabados = new List<Behaviour>();
    private string ultimaPartida;

    /// <summary>Si la pantalla de carga está tapando el juego.</summary>
    public static bool Visible => actual != null && actual.raiz != null && actual.raiz.activeSelf;

    /// <summary>CA4: todavía se está esperando a que carguen los demás (el anfitrión no arranca la partida).</summary>
    public static bool EsperandoJugadores => Visible && actual.online;

    /// <summary>Las tipografías del juego: las pasa el menú (o la pausa) al arrancar.</summary>
    public static void Registrar(TMP_FontAsset display, TMP_FontAsset label, TMP_FontAsset body, Sprite sprite)
    {
        if (display != null) displayFont = display;
        if (label != null) labelFont = label;
        if (body != null) bodyFont = body;
        if (sprite != null) rounded = sprite;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Crear()
    {
        if (actual != null) return;
        var go = new GameObject("PantallaDeCarga", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        DontDestroyOnLoad(go);
        actual = go.AddComponent<PantallaDeCarga>();
    }

    // =====================================================================
    // Pedidos
    // =====================================================================

    /// <summary>Carga una escena sin conexión (Zombie o el menú principal) con la pantalla de carga puesta.</summary>
    public static void Cargar(string escena, string modo, string mapa, Sprite imagen = null, GameMode? consejosDe = null)
    {
        Crear();
        if (!Application.CanStreamedLevelBeLoaded(escena))
        {
            Debug.LogError($"PantallaDeCarga: la escena \"{escena}\" no está en File > Build Profiles (Scene List).");
            return;
        }
        actual.Mostrar(escena, modo, mapa, "", imagen, consejosDe, false);
        actual.StartCoroutine(actual.CargarLocal(escena));
    }

    /// <summary>Partida online: el mapa lo carga Photon en todas las computadoras; acá se muestra el avance.</summary>
    public static void MostrarOnline(string escena, GameMode modo, string mapa, Sprite imagen, string sala)
    {
        Crear();
        string nombreModo = modo == GameMode.Tactico ? "Modo Táctico" : modo == GameMode.Deathmatch ? "Deathmatch" : "Modo Zombie";
        actual.Mostrar(escena, nombreModo, mapa, string.IsNullOrEmpty(sala) ? "" : "Sala " + sala, imagen, modo, true);
        if (PhotonNetwork.CurrentRoom != null) actual.ultimaPartida = PhotonNetwork.CurrentRoom.Name + "/" + escena;
        // Lo que quedó de una partida anterior no cuenta.
        if (PhotonNetwork.LocalPlayer != null)
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { PropListo, "" } });
    }

    private void Mostrar(string escenaNueva, string modo, string mapa, string detalle, Sprite imagen, GameMode? consejosDe, bool enRed)
    {
        if (raiz == null) Armar();
        escena = escenaNueva;
        online = enRed;
        publicado = false;
        avance = 0f;
        cargadoEn = -1f;
        desde = Time.unscaledTime;
        trabados.Clear();

        modoTexto.text = modo;
        mapaTexto.text = mapa;
        detalleTexto.text = detalle;
        imagenMapa.sprite = imagen;
        imagenMapa.enabled = imagen != null;
        if (imagen != null) ajusteMapa.aspectRatio = imagen.rect.width / imagen.rect.height;
        consejoCaja.SetActive(consejosDe.HasValue);
        if (consejosDe.HasValue) consejoTexto.text = Consejo(consejosDe.Value); // CA3
        listosTexto.text = "";
        pieTexto.text = "";
        PonerAvance(0f);
        raiz.SetActive(true);
    }

    private void Ocultar()
    {
        foreach (Behaviour c in trabados) if (c != null) c.enabled = true;
        trabados.Clear();
        raiz.SetActive(false);
        online = false;
    }

    // =====================================================================
    // Carga sin conexión
    // =====================================================================

    private IEnumerator CargarLocal(string nombre)
    {
        yield return null; // un cuadro para que la pantalla se llegue a dibujar
        AsyncOperation carga = SceneManager.LoadSceneAsync(nombre);
        while (carga != null && !carga.isDone)
        {
            PonerAvance(carga.progress / 0.9f);
            yield return null;
        }
        PonerAvance(1f);
        while (Time.unscaledTime - desde < TiempoMinimo) yield return null;
        Ocultar();
    }

    // =====================================================================
    // Cuadro a cuadro
    // =====================================================================

    private void Update()
    {
        if (!Visible)
        {
            VerSiEmpiezaLaPartida();
            return;
        }

        Girar();
        if (!online) return;

        // Se cortó la conexión o se salió de la sala: no hay nada que esperar.
        if (!PhotonNetwork.InRoom) { Ocultar(); return; }

        bool enMapa = SceneManager.GetActiveScene().name == escena;
        PartidaEnRed partida = PartidaEnRed.Actual;
        bool listo = enMapa && partida != null && partida.Lista;

        if (!listo)
        {
            PonerAvance(enMapa ? 1f : Mathf.Max(avance, PhotonNetwork.LevelLoadingProgress));
            // Si la carga no avanza nunca (por ejemplo, la escena no está en el build), no se queda para siempre.
            if (Time.unscaledTime - desde > EsperaMaxima * 2f) Ocultar();
            return;
        }

        // CA4: este jugador ya cargó. Se avisa a la sala y se espera a los demás, sin poder moverse.
        if (!publicado)
        {
            publicado = true;
            cargadoEn = Time.unscaledTime;
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { PropListo, escena } });
        }
        Trabar(partida);
        PonerAvance(1f);

        int total = PhotonNetwork.CurrentRoom.PlayerCount, listos = 0;
        foreach (Player p in PhotonNetwork.PlayerList)
            if (p.CustomProperties.TryGetValue(PropListo, out object v) && v is string s && s == escena) listos++;
        listos = Mathf.Max(listos, 1);
        listosTexto.text = $"<color=#3DDC97>{listos} / {total}</color> listos";
        pieTexto.text = listos < total ? "Esperando a los demás jugadores  ·  la partida empieza cuando cargaron todos" : "";

        bool tiempoMinimo = Time.unscaledTime - desde >= TiempoMinimo;
        if ((listos >= total && tiempoMinimo) || Time.unscaledTime - cargadoEn > EsperaMaxima) Ocultar();
    }

    // En las computadoras que no son el anfitrión, la partida empieza cuando Photon manda cargar el mapa.
    private void VerSiEmpiezaLaPartida()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || SceneManager.GetActiveScene().name != EscenaMenu) return;
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(PropEscenaPhoton, out object v) || !(v is string destino)) return;
        if (string.IsNullOrEmpty(destino) || destino == EscenaMenu) return;
        // Una sola vez por sala: si la carga falló y la pantalla ya se sacó, no vuelve a aparecer.
        string clave = PhotonNetwork.CurrentRoom.Name + "/" + destino;
        if (clave == ultimaPartida) return;
        ultimaPartida = clave;
        Multijugador red = Multijugador.Instancia;
        if (red == null) return;
        ConfigRed.Mapa mapa = red.MapaActual;
        MostrarOnline(destino, red.ModoSala, mapa != null ? mapa.nombre : destino, mapa != null ? mapa.imagen : null, red.Codigo);
    }

    // Mientras se espera a los demás, el jugador no se mueve, no mira ni dispara.
    private void Trabar(PartidaEnRed partida)
    {
        if (trabados.Count > 0 || partida.Local == null) return;
        foreach (Behaviour c in partida.Local.GetComponentsInChildren<Behaviour>(true))
            if (c.enabled && (c is PlayerMovement || c is CameraLook || c is Pistola || c is Mitre || c is ArmaDeFuego ||
                              c is MeleeAttack || c is WeaponSwitcher || c is PlayerAbility))
            {
                c.enabled = false;
                trabados.Add(c);
            }
    }

    // =====================================================================
    // Consejos (CA3)
    // =====================================================================

    private static readonly string[] ConsejosGenerales =
    {
        "Agachate con {Agacharse} antes de tirar: las balas se desvían menos.",
        "Recargá con {Recargar} antes de asomarte, no en medio del tiroteo.",
        "Disparar en movimiento abre la dispersión: frená antes de tirar.",
        "Los tiros a la cabeza hacen más daño.",
        "Usá la habilidad de tu personaje con {Habilidad}.",
        "Las teclas, la sensibilidad y la mira se cambian en Opciones o desde la pausa.",
        "Mantené apretada la tecla Tab para ver la tabla de jugadores."
    };

    private static readonly string[] ConsejosTactico =
    {
        "Mantené apretada {Plantar} sobre una zona para plantar el dispositivo. Si te movés, se corta.",
        "Abrí la tienda con {Tienda} durante la fase de compra, dentro de tu base.",
        "Soltá tu arma con {Soltar} para pasársela a un compañero.",
        "Si llevás el dispositivo, sacalo con {Dispositivo}.",
        "Al morir no reaparecés hasta la ronda siguiente: cuidá la vida.",
        "Perder varias rondas seguidas da más plata para la siguiente.",
        "Los defensores desactivan el dispositivo manteniendo apretada {Plantar} al lado."
    };

    private static readonly string[] ConsejosDeathmatch =
    {
        "En Deathmatch reaparecés a los pocos segundos: no te quedes quieto.",
        "Cambiá de arma con {ArmaPrincipal}, {ArmaSecundaria} y {Cuchillo}."
    };

    private static readonly string[] ConsejosZombie =
    {
        "Cada oleada es más difícil que la anterior: guardá balas.",
        "Si te rodean, pasá al cuchillo con {Cuchillo} en vez de recargar."
    };

    private static string Consejo(GameMode modo)
    {
        var lista = new List<string>(ConsejosGenerales);
        lista.AddRange(modo == GameMode.Tactico ? ConsejosTactico : modo == GameMode.Deathmatch ? ConsejosDeathmatch : ConsejosZombie);
        string texto = lista[Random.Range(0, lista.Count)];
        // {Recargar} se cambia por la tecla que el jugador tiene para esa acción.
        foreach (GameAction accion in KeyBindings.All)
        {
            string marca = "{" + accion + "}";
            if (texto.Contains(marca)) texto = texto.Replace(marca, $"<color=#F29A38><b>{KeyBindings.Label(accion)}</b></color>");
        }
        return texto;
    }

    // =====================================================================
    // Armado
    // =====================================================================

    private void Armar()
    {
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250; // arriba de todo el juego y los menús; debajo del indicador de FPS
        var escala = GetComponent<UnityEngine.UI.CanvasScaler>();
        escala.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;

        RectTransform todo = Stretch(Node("Pantalla", transform));
        raiz = todo.gameObject;
        Image(todo, null, Rgb(8, 10, 14), 0f, true); // tapa el juego y frena los clics
        imagenMapa = Image(Stretch(Node("Mapa", todo)), null, Color.white);
        // La captura cubre toda la pantalla sin deformarse: si la pantalla no es 16:9, se recorta lo que sobra en los bordes.
        ajusteMapa = imagenMapa.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
        ajusteMapa.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent;
        ajusteMapa.aspectRatio = 16f / 9f;
        Image(Stretch(Node("Oscurecido", todo)), null, Rgb(8, 10, 14, 0.72f));

        RectTransform c = Node("Contenido", todo);
        c.anchorMin = c.anchorMax = c.pivot = new Vector2(0.5f, 0.5f);
        c.sizeDelta = new Vector2(1920f, 1080f);

        // Modo y mapa
        modoTexto = Text(Place(Node("Modo", c), 96f, 90f, 1000f, 30f), labelFont, 22f, Accent, TextAlignmentOptions.MidlineLeft, 24f, true);
        mapaTexto = Text(Place(Node("Mapa", c), 92f, 122f, 1600f, 160f), displayFont, 150f, Ink, TextAlignmentOptions.MidlineLeft, 2f, true);
        Image(Place(Node("Linea", c), 96f, 286f, 180f, 4f), null, Accent);
        detalleTexto = Text(Place(Node("Detalle", c), 96f, 306f, 1000f, 28f), labelFont, 20f, Mute, TextAlignmentOptions.MidlineLeft, 16f, true);

        // Ícono que gira (CA5)
        RectTransform giro = Place(Node("Cargando", c), 1768f, 92f, 56f, 56f);
        for (int i = 0; i < Rayos; i++)
        {
            RectTransform rayo = Node("Rayo", giro);
            rayo.anchorMin = rayo.anchorMax = new Vector2(0.5f, 0.5f);
            rayo.pivot = new Vector2(0.5f, 0f);
            rayo.sizeDelta = new Vector2(5f, 12f);
            float angulo = -i * 360f / Rayos;
            rayo.localRotation = Quaternion.Euler(0f, 0f, angulo);
            rayo.anchoredPosition = Quaternion.Euler(0f, 0f, angulo) * new Vector2(0f, 14f);
            rayos[i] = Image(rayo, null, Accent);
        }

        // Consejo
        RectTransform caja = Place(Node("Consejo", c), 96f, 716f, 820f, 136f);
        consejoCaja = caja.gameObject;
        Image(caja, rounded, Rgb(10, 12, 17, 0.86f), 8f);
        Image(Place(Node("Borde", caja), 0f, 0f, 3f, 136f), null, Accent);
        Text(Place(Node("Titulo", caja), 28f, 18f, 400f, 20f), labelFont, 15f, Mute, TextAlignmentOptions.MidlineLeft, 16f, true).text = "Consejo";
        consejoTexto = Text(Place(Node("Texto", caja), 28f, 46f, 764f, 76f), bodyFont, 23f, Soft, TextAlignmentOptions.TopLeft);
        consejoTexto.textWrappingMode = TextWrappingModes.Normal;
        consejoTexto.lineSpacing = 12f;

        // Barra con forma de vía
        estadoTexto = Text(Place(Node("Estado", c), 96f, 906f, 900f, 30f), labelFont, 20f, Ink, TextAlignmentOptions.MidlineLeft, 16f, true);
        listosTexto = Text(Place(Node("Listos", c), 924f, 906f, 900f, 30f), labelFont, 20f, Mute, TextAlignmentOptions.MidlineRight, 16f, true);
        RectTransform via = Place(Node("Via", c), 96f, 952f, 1728f, 6f);
        Image(via, null, Rgb(42, 47, 57));
        relleno = Place(Node("Tramo", via), 0f, 0f, 0f, 6f);
        Image(relleno, null, Ink);
        cursor = Place(Node("Cursor", via), 0f, -10f, 8f, 26f);
        Image(cursor, null, Accent);
        float paso = 1728f / Durmientes;
        for (int i = 0; i < Durmientes; i++)
            durmientes[i] = Image(Place(Node("Durmiente", c), 96f + i * paso, 972f, 3f, 10f), null, White(0.16f));
        pieTexto = Text(Place(Node("Pie", c), 96f, 998f, 1728f, 24f), labelFont, 15f, Mute, TextAlignmentOptions.MidlineLeft, 12f, true);

        raiz.SetActive(false);
    }

    private void PonerAvance(float valor)
    {
        avance = Mathf.Clamp01(valor);
        relleno.sizeDelta = new Vector2(1728f * avance, 6f);
        cursor.anchoredPosition = new Vector2(Mathf.Max(0f, 1728f * avance - 4f), 10f);
        int hasta = Mathf.RoundToInt(avance * Durmientes);
        for (int i = 0; i < Durmientes; i++) durmientes[i].color = i < hasta ? Ink : White(0.16f);
        string que = escena == EscenaMenu ? "Cargando el menú" : "Cargando el mapa";
        estadoTexto.text = avance >= 1f && online ? "Mapa cargado" : $"{que} <color=#F29A38>{Mathf.RoundToInt(avance * 100f)} %</color>";
    }

    private void Girar()
    {
        int cabeza = Mathf.FloorToInt(Time.unscaledTime * 12f) % Rayos;
        for (int i = 0; i < Rayos; i++)
        {
            int atras = (cabeza - i + Rayos) % Rayos;
            rayos[i].color = WithAlpha(Accent, Mathf.Lerp(1f, 0.12f, atras / (float)(Rayos - 1)));
        }
    }
}
