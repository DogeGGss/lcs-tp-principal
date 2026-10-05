using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using static ShopUIKit;
using Img = UnityEngine.UI.Image;

// Minimapa (US 194), como el radar de Counter-Strike y Valorant. Primera parte:
// - CA1: arriba a la izquierda (debajo del indicador de FPS y ping), el mapa desde arriba unos 40 m alrededor del jugador.
// - CA2: el jugador es una flecha en el centro y el mapa gira con su mirada.
// - CA3: los compañeros vivos son puntos azules.
// - CA5: en el Táctico, las zonas de plantado con su letra y el dispositivo en el piso o plantado; el portador se ve
//   con el ícono del dispositivo solo para sus compañeros.
// - CA7: el dibujo de cada mapa se arma solo al empezar, a partir de las paredes y el piso del mapa (no hay que
//   preparar una imagen por mapa): se recorre el mapa en una grilla y se mira dónde hay piso y dónde hay pared.
// - CA8: funciona en Táctico, Deathmatch y Zombie (en Zombie, el jugador y el mapa).
// Pendiente (segunda parte): rivales vistos por el equipo (CA4) y mapa completo con una tecla (CA6).
// Lo crea CombatHud sobre su lienzo.
public class Minimapa : MonoBehaviour
{
    private const float Lado = 260f, X = 24f, Y = 40f; // px en 1920 x 1080
    private const float Radio = 40f;                   // metros que se ven hacia cada lado (CA1)
    private const float Escala = Lado / (Radio * 2f);  // px por metro
    private const float Celda = 0.4f;                  // metros por punto del dibujo
    private const int MaximoPuntos = 1024;
    private const float MapaMaximo = 400f;             // m: mapas más grandes se recortan alrededor del jugador
    private const float AlturaDeCorte = 2.5f;          // m sobre el jugador: lo que está más arriba (techos) no se dibuja
    private const float AlturaDePared = 1.2f;          // m sobre el piso donde se mira si hay pared
    private const float AlturaMinimaDePared = 0.9f;    // m: más bajo que esto no cuenta como obstáculo
    private const float AlturaParaPasar = 1.7f;        // m libres bajo un techo para que cuente como lugar para caminar

    private static readonly Color32 ColorPared = new Color32(74, 84, 100, 255), ColorPiso = new Color32(30, 36, 46, 255),
        ColorVacio = new Color32(14, 17, 23, 255);

    private TMP_FontAsset fuente;
    private Sprite redondeado;
    private RectTransform giro, mapa;
    private UnityEngine.UI.RawImage imagen;
    private Texture2D dibujo;
    private Bounds limites;
    private bool armado;
    private Transform jugador, vista;

    private readonly Dictionary<JugadorEnRed, Punto> companeros = new Dictionary<JugadorEnRed, Punto>();
    private readonly List<JugadorEnRed> sobran = new List<JugadorEnRed>();
    private readonly List<(ZonaDePlantado zona, RectTransform rect, RectTransform letra)> zonas =
        new List<(ZonaDePlantado, RectTransform, RectTransform)>();
    private RectTransform dispositivo;
    private Img luzDispositivo;

    private class Punto
    {
        public RectTransform rect;
        public GameObject icono;
    }

    public void Iniciar(RectTransform raiz, TMP_FontAsset label, Sprite sprite)
    {
        fuente = label;
        redondeado = sprite;

        RectTransform caja = Place(Node("Minimapa", raiz), X, Y, Lado, Lado);
        Image(caja, redondeado, ColorVacio, 10f);
        caja.gameObject.AddComponent<UnityEngine.UI.RectMask2D>(); // lo que gira no se sale del cuadro

        giro = Node("Giro", caja);
        giro.anchorMin = giro.anchorMax = giro.pivot = new Vector2(0.5f, 0.5f);
        giro.anchoredPosition = Vector2.zero;
        giro.sizeDelta = Vector2.zero;

        mapa = Node("Mapa", giro);
        mapa.anchorMin = mapa.anchorMax = mapa.pivot = new Vector2(0.5f, 0.5f);
        imagen = mapa.gameObject.AddComponent<UnityEngine.UI.RawImage>();
        imagen.raycastTarget = false;
        imagen.enabled = false;

        // CA2: el jugador, una flecha fija en el centro que apunta para arriba.
        RectTransform flecha = Node("Jugador", caja);
        flecha.anchorMin = flecha.anchorMax = flecha.pivot = new Vector2(0.5f, 0.5f);
        flecha.anchoredPosition = Vector2.zero;
        Ala(flecha, -32f, new Vector2(-3.6f, -1f), new Color(0f, 0f, 0f, 0.8f), 5f, 17f);
        Ala(flecha, 32f, new Vector2(3.6f, -1f), new Color(0f, 0f, 0f, 0.8f), 5f, 17f);
        Ala(flecha, -32f, new Vector2(-3.6f, -1f), Ink, 3f, 15f);
        Ala(flecha, 32f, new Vector2(3.6f, -1f), Ink, 3f, 15f);

        // Borde fino
        Color linea = White(0.16f);
        Image(Place(Node("BordeArriba", caja), 0f, 0f, Lado, 1f), null, linea);
        Image(Place(Node("BordeAbajo", caja), 0f, Lado - 1f, Lado, 1f), null, linea);
        Image(Place(Node("BordeIzquierda", caja), 0f, 0f, 1f, Lado), null, linea);
        Image(Place(Node("BordeDerecha", caja), Lado - 1f, 0f, 1f, Lado), null, linea);
    }

    private static void Ala(RectTransform padre, float angulo, Vector2 lugar, Color color, float ancho, float largo)
    {
        RectTransform ala = Node("Ala", padre);
        ala.anchorMin = ala.anchorMax = ala.pivot = new Vector2(0.5f, 0.5f);
        ala.anchoredPosition = lugar;
        ala.sizeDelta = new Vector2(ancho, largo);
        ala.localRotation = Quaternion.Euler(0f, 0f, angulo);
        Image(ala, null, color);
    }

    /// <summary>El jugador de esta computadora: al conocerlo se arma el dibujo del mapa.</summary>
    public void Seguir(Transform quien)
    {
        if (quien == null || quien == jugador) return;
        jugador = quien;
        Camera camara = quien.GetComponentInChildren<Camera>();
        vista = camara != null ? camara.transform : quien;
        if (!armado) StartCoroutine(Armar());
    }

    // =====================================================================
    // Dibujo del mapa (CA7)
    // =====================================================================

    private IEnumerator Armar()
    {
        armado = true;
        yield return null;
        yield return null; // la partida online acomoda al jugador en su base en los primeros cuadros

        // Qué parte del mundo se dibuja: todo lo que tiene colisión, sin los personajes.
        Vector3 centro = jugador.position;
        bool hay = false;
        foreach (Collider c in FindObjectsByType<Collider>(FindObjectsSortMode.None))
        {
            if (!EsDelMapa(c)) continue;
            if (!hay) { limites = c.bounds; hay = true; }
            else limites.Encapsulate(c.bounds);
        }
        if (!hay) yield break;

        // Un piso enorme no agranda el dibujo: se recorta alrededor del jugador.
        Vector3 min = limites.min, max = limites.max;
        min.x = Mathf.Max(min.x, centro.x - MapaMaximo * 0.5f); max.x = Mathf.Min(max.x, centro.x + MapaMaximo * 0.5f);
        min.z = Mathf.Max(min.z, centro.z - MapaMaximo * 0.5f); max.z = Mathf.Min(max.z, centro.z + MapaMaximo * 0.5f);
        limites.SetMinMax(min, max);

        float celda = Mathf.Max(Celda, Mathf.Max(limites.size.x, limites.size.z) / MaximoPuntos);
        int ancho = Mathf.Clamp(Mathf.CeilToInt(limites.size.x / celda), 8, MaximoPuntos);
        int alto = Mathf.Clamp(Mathf.CeilToInt(limites.size.z / celda), 8, MaximoPuntos);
        float corte = centro.y + AlturaDeCorte;
        float fondo = corte - limites.min.y + 2f;
        float cielo = limites.max.y + 1f, hastaAbajo = limites.size.y + 3f;
        int capas = Physics.DefaultRaycastLayers;
        // Del ancho de la celda: una pared fina siempre toca alguna celda y la línea no queda cortada.
        Vector3 medio = new Vector3(celda * 0.5f, 0.15f, celda * 0.5f);

        // 0 = vacío, 1 = piso, 2 = pared. Se guarda también la altura del piso para detectar escalones altos.
        var tipo = new byte[ancho * alto];
        var altura = new float[ancho * alto];
        float proximaPausa = Time.realtimeSinceStartup + 0.006f;
        for (int z = 0; z < alto; z++)
        {
            for (int x = 0; x < ancho; x++)
            {
                int i = z * ancho + x;
                float px = limites.min.x + (x + 0.5f) * celda, pz = limites.min.z + (z + 0.5f) * celda;
                // Lo más alto que hay en ese lugar (mirando desde el cielo) y el piso (mirando desde la altura de
                // corte, así los techos no tapan los pasillos).
                bool hayAlgo = Physics.Raycast(new Vector3(px, cielo, pz), Vector3.down, out RaycastHit arriba, hastaAbajo, capas, QueryTriggerInteraction.Ignore)
                               && EsDelMapa(arriba.collider);
                bool hayPiso = Physics.Raycast(new Vector3(px, corte, pz), Vector3.down, out RaycastHit piso, fondo, capas, QueryTriggerInteraction.Ignore);
                if (!hayPiso)
                {
                    // Sin piso a la vista pero con algo encima: un bloque sólido más alto que el corte.
                    tipo[i] = (byte)(hayAlgo ? 2 : 0);
                    altura[i] = float.MaxValue;
                    continue;
                }

                float h = piso.point.y;
                altura[i] = h;
                bool pared = false;
                if (hayAlgo && arriba.point.y - h > AlturaMinimaDePared)
                {
                    // Hay algo arriba de este piso. Si mirando para arriba se ve un cielorraso con lugar para pasar, es
                    // un lugar techado. Si no se ve nada, este punto está adentro de algo sólido: una pared.
                    bool cielorraso = Physics.Raycast(new Vector3(px, h + 0.1f, pz), Vector3.up, out RaycastHit techo,
                        arriba.point.y - h + 0.5f, capas, QueryTriggerInteraction.Ignore);
                    pared = !cielorraso || techo.distance < AlturaParaPasar;
                }
                // Paredes finas (una chapa, una reja): algo sólido a la altura del pecho.
                if (!pared)
                {
                    Vector3 pecho = new Vector3(px, h + AlturaDePared, pz);
                    pared = pecho.y < corte + 1f && Physics.CheckBox(pecho, medio, Quaternion.identity, capas, QueryTriggerInteraction.Ignore)
                            && !EsUnPersonaje(pecho, medio, capas);
                }
                tipo[i] = (byte)(pared ? 2 : 1);
            }
            // De a poco, para no trabar el juego al empezar.
            if (Time.realtimeSinceStartup >= proximaPausa)
            {
                yield return null;
                proximaPausa = Time.realtimeSinceStartup + 0.006f;
            }
        }

        // Cajas, muros bajos y escalones altos: un piso bastante más alto que el de al lado también es un obstáculo.
        var puntos = new Color32[ancho * alto];
        for (int z = 0; z < alto; z++)
            for (int x = 0; x < ancho; x++)
            {
                int i = z * ancho + x;
                if (tipo[i] == 1)
                {
                    float vecino = float.MaxValue;
                    for (int dz = -2; dz <= 2; dz++)
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            int nx = x + dx, nz = z + dz;
                            if (nx < 0 || nz < 0 || nx >= ancho || nz >= alto) continue;
                            int n = nz * ancho + nx;
                            if (tipo[n] == 1 && altura[n] < vecino) vecino = altura[n];
                        }
                    if (altura[i] - vecino > AlturaMinimaDePared) { puntos[i] = ColorPared; continue; }
                }
                puntos[i] = tipo[i] == 2 ? ColorPared : tipo[i] == 1 ? ColorPiso : ColorVacio;
            }

        dibujo = new Texture2D(ancho, alto, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        dibujo.SetPixels32(puntos);
        dibujo.Apply(false, true);
        imagen.texture = dibujo;
        imagen.enabled = true;
        mapa.sizeDelta = new Vector2(ancho * celda, alto * celda) * Escala;
        // El dibujo cubre desde limites.min: se guarda ese rectángulo exacto.
        limites.SetMinMax(limites.min, new Vector3(limites.min.x + ancho * celda, limites.max.y, limites.min.z + alto * celda));
    }

    private static bool EsDelMapa(Collider c) =>
        c != null && c.enabled && !c.isTrigger && c.GetComponentInParent<HealthSystem>() == null &&
        c.GetComponentInParent<CharacterController>() == null && c.attachedRigidbody == null;

    private static readonly Collider[] tocados = new Collider[8];

    // Si lo único sólido en ese lugar es un personaje (el jugador, un enemigo), no es una pared.
    private static bool EsUnPersonaje(Vector3 lugar, Vector3 medio, int capas)
    {
        int n = Physics.OverlapBoxNonAlloc(lugar, medio, tocados, Quaternion.identity, capas, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++) if (EsDelMapa(tocados[i])) return false;
        return true;
    }

    private void OnDestroy()
    {
        if (dibujo != null) Destroy(dibujo);
    }

    // =====================================================================
    // Cuadro a cuadro
    // =====================================================================

    private void LateUpdate()
    {
        if (jugador == null || giro == null) return;

        // CA2: el mapa gira con la mirada y se corre para que el jugador quede en el centro.
        float mirada = vista != null ? vista.eulerAngles.y : jugador.eulerAngles.y;
        giro.localRotation = Quaternion.Euler(0f, 0f, mirada);
        Vector3 yo = jugador.position;
        if (imagen.enabled)
            mapa.anchoredPosition = new Vector2(limites.center.x - yo.x, limites.center.z - yo.z) * Escala;

        Quaternion derecho = Quaternion.Euler(0f, 0f, -mirada); // para que letras e íconos no giren con el mapa
        Companeros(yo, derecho);
        Zonas(yo, derecho);
        Dispositivo(yo, derecho);
    }

    private static Vector2 EnMapa(Vector3 lugar, Vector3 yo) => new Vector2(lugar.x - yo.x, lugar.z - yo.z) * Escala;

    // CA3 y CA5: compañeros vivos en azul; el que lleva el dispositivo, con su ícono.
    private void Companeros(Vector3 yo, Quaternion derecho)
    {
        PartidaEnRed partida = PartidaEnRed.Actual;
        JugadorEnRed local = partida != null ? partida.Local : null;
        bool hayEquipos = local != null && EquiposTacticos.HayEquipos;

        sobran.Clear();
        foreach (KeyValuePair<JugadorEnRed, Punto> par in companeros)
            if (par.Key == null) { if (par.Value.rect != null) Destroy(par.Value.rect.gameObject); sobran.Add(par.Key); }
        foreach (JugadorEnRed j in sobran) companeros.Remove(j);

        if (!hayEquipos)
        {
            foreach (Punto p in companeros.Values) p.rect.gameObject.SetActive(false);
            return;
        }

        int portador = MarcadorTactico.Portador;
        foreach (JugadorEnRed j in partida.Jugadores)
        {
            if (j == null || j == local) continue;
            bool ver = j.Vivo && EquiposTacticos.SonAliados(local.Actor, j.Actor);
            if (!companeros.TryGetValue(j, out Punto punto))
            {
                if (!ver) continue;
                punto = new Punto { rect = Marca("Compañero", 14f, new Color(0f, 0f, 0f, 0.75f)) };
                Image(Centrado(Node("Color", punto.rect), 10f), redondeado, MatchHud.TeamColor, 5f);
                RectTransform icono = Centrado(Node("Dispositivo", punto.rect), 28f);
                icono.localScale = Vector3.one * 0.6f;
                icono.anchoredPosition = new Vector2(0f, 14f);
                MatchHud.DeviceGlyph(icono, Accent, redondeado, out Img _).anchoredPosition = Vector2.zero;
                punto.icono = icono.gameObject;
                companeros[j] = punto;
            }
            punto.rect.gameObject.SetActive(ver);
            if (!ver) continue;
            punto.rect.anchoredPosition = EnMapa(j.transform.position, yo);
            punto.rect.localRotation = derecho;
            bool lleva = portador != 0 && j.Actor == portador;
            if (punto.icono.activeSelf != lleva) punto.icono.SetActive(lleva);
        }
    }

    // CA5: zonas de plantado, con su letra.
    private void Zonas(Vector3 yo, Quaternion derecho)
    {
        IReadOnlyList<ZonaDePlantado> todas = ZonaDePlantado.Todas;
        if (zonas.Count != todas.Count)
        {
            foreach (var vieja in zonas) if (vieja.rect != null) Destroy(vieja.rect.gameObject);
            zonas.Clear();
            foreach (ZonaDePlantado zona in todas)
            {
                RectTransform rect = Node("Zona " + zona.nombre, giro);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.SetSiblingIndex(1); // arriba del mapa, debajo de los puntos
                Image(rect, redondeado, WithAlpha(Accent, 0.2f), 3f);
                RectTransform letra = Centrado(Node("Letra", rect), 30f);
                TextMeshProUGUI texto = Text(letra, fuente, 20f, Accent, TextAlignmentOptions.Center);
                texto.fontStyle = FontStyles.Bold;
                texto.text = zona.nombre;
                zonas.Add((zona, rect, letra));
            }
        }
        foreach (var entrada in zonas)
        {
            ZonaDePlantado zona = entrada.zona;
            RectTransform rect = entrada.rect, letra = entrada.letra;
            if (zona == null || rect == null) continue;
            Bounds b = zona.Limites;
            rect.anchoredPosition = EnMapa(b.center, yo);
            rect.sizeDelta = new Vector2(Mathf.Max(b.size.x * Escala, 14f), Mathf.Max(b.size.z * Escala, 14f));
            letra.localRotation = derecho;
        }
    }

    // CA5: el dispositivo en el piso (naranja) o plantado (rojo, con la luz que titila).
    private void Dispositivo(Vector3 yo, Quaternion derecho)
    {
        RondasTacticas rondas = RondasTacticas.Actual;
        DispositivoTactico tactico = DispositivoTactico.Actual;
        Vector3? lugar = null;
        bool plantado = rondas != null && rondas.HayDispositivo;
        if (plantado) lugar = rondas.LugarDelDispositivo;
        else if (tactico != null) lugar = tactico.LugarEnPiso;

        if (!lugar.HasValue)
        {
            if (dispositivo != null) dispositivo.gameObject.SetActive(false);
            return;
        }
        if (dispositivo == null)
        {
            dispositivo = Marca("Dispositivo", 24f, new Color(0f, 0f, 0f, 0.75f));
            luzDispositivo = Image(Centrado(Node("Color", dispositivo), 18f), redondeado, Accent, 4f);
            Image(Centrado(Node("Centro", dispositivo), 6f), redondeado, Rgb(14, 10, 12), 3f);
        }
        dispositivo.gameObject.SetActive(true);
        dispositivo.SetAsLastSibling();
        dispositivo.anchoredPosition = EnMapa(lugar.Value, yo);
        dispositivo.localRotation = derecho;
        luzDispositivo.color = !plantado ? Accent
            : WithAlpha(MatchHud.RivalColor, 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)));
    }

    private RectTransform Marca(string nombre, float lado, Color fondo)
    {
        RectTransform rect = Centrado(Node(nombre, giro), lado);
        Image(rect, redondeado, fondo, lado * 0.5f);
        return rect;
    }

    private static RectTransform Centrado(RectTransform rect, float lado)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(lado, lado);
        return rect;
    }
}
