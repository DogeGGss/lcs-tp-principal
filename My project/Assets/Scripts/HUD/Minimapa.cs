using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static ShopUIKit;
using Img = UnityEngine.UI.Image;

// Minimapa (US 194), como el radar de Counter-Strike y Valorant.
// - CA1: arriba a la izquierda (debajo del indicador de FPS y ping), el mapa desde arriba unos 40 m alrededor del jugador.
// - CA2: el jugador es una flecha en el centro y el mapa gira con su mirada.
// - CA3: los compañeros vivos son puntos azules.
// - CA4: un rival es un punto rojo solo mientras algún compañero (o uno mismo) lo está viendo, y se borra 2 s después
//   de que dejan de verlo (lo calcula RivalesVistos).
// - CA5: en el Táctico, las zonas de plantado con su letra y el dispositivo en el piso o plantado; el portador se ve
//   con el ícono del dispositivo solo para sus compañeros.
// - CA6: mientras se mantiene la tecla del mapa (H, reasignable), se ve el mapa completo en el centro de la pantalla,
//   con el norte arriba y la flecha del jugador girando.
// - CA7: el dibujo de cada mapa se arma solo al empezar, a partir de las paredes y el piso del mapa (no hay que
//   preparar una imagen por mapa): se recorre el mapa en una grilla y se mira dónde hay piso y dónde hay pared.
// - CA8: funciona en Táctico, Deathmatch y Zombie (en Zombie, el jugador y el mapa).
// Lo crea CombatHud sobre su lienzo. Hay dos vistas del mismo mapa: la chica (el radar) y la grande (el mapa completo).
public class Minimapa : MonoBehaviour
{
    private const float Lado = 260f, X = 24f, Y = 40f; // px en 1920 x 1080
    private const float Radio = 40f;                   // metros que se ven hacia cada lado (CA1)
    private const float Escala = Lado / (Radio * 2f);  // px por metro del radar
    private const float LadoGrande = 780f;             // CA6: el mapa completo
    private const float Celda = 0.4f;                  // metros por punto del dibujo
    private const int MaximoPuntos = 1024;
    private const float MapaMaximo = 400f;             // m: mapas más grandes se recortan alrededor del jugador
    private const float AlturaDeCorte = 2.5f;          // m sobre el jugador: lo que está más arriba (techos) no se dibuja
    private const float AlturaDePared = 1.2f;          // m sobre el piso donde se mira si hay pared
    private const float AlturaMinimaDePared = 0.9f;    // m: más bajo que esto no cuenta como obstáculo
    private const float AlturaParaPasar = 1.7f;        // m libres bajo un techo para que cuente como lugar para caminar

    private static readonly Color32 ColorPared = new Color32(74, 84, 100, 255), ColorPiso = new Color32(30, 36, 46, 255),
        ColorVacio = new Color32(14, 17, 23, 255);

    private static TMP_FontAsset fuente;
    private static Sprite redondeado;
    private Texture2D dibujo;
    private Bounds limites;
    private Vector2 medidaDelMapa;
    private bool armado;
    private Transform jugador, vista;
    private Vista chico, grande;
    private TextMeshProUGUI ayuda;
    private string teclaMostrada;

    // =====================================================================
    // Una vista del mapa: el radar o el mapa completo. Las dos dibujan lo mismo con otra escala.
    // =====================================================================

    private class Vista
    {
        public RectTransform caja, giro, mapa, flecha, dispositivo;
        public UnityEngine.UI.RawImage imagen;
        public Img luzDispositivo;
        public float escala;
        public readonly Dictionary<JugadorEnRed, RectTransform> companeros = new Dictionary<JugadorEnRed, RectTransform>();
        public readonly Dictionary<JugadorEnRed, GameObject> iconos = new Dictionary<JugadorEnRed, GameObject>();
        public readonly List<RectTransform> rivales = new List<RectTransform>();
        public readonly List<(ZonaDePlantado zona, RectTransform rect, RectTransform letra)> zonas =
            new List<(ZonaDePlantado, RectTransform, RectTransform)>();
        private readonly List<JugadorEnRed> sobran = new List<JugadorEnRed>();

        public Vista(RectTransform caja, float lado)
        {
            this.caja = caja;
            Image(caja, redondeado, ColorVacio, 10f);
            caja.gameObject.AddComponent<UnityEngine.UI.RectMask2D>(); // lo que gira no se sale del cuadro

            giro = Centrado(Node("Giro", caja), 0f);
            mapa = Centrado(Node("Mapa", giro), 0f);
            imagen = mapa.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            imagen.raycastTarget = false;
            imagen.enabled = false;

            // CA2: el jugador, una flecha.
            flecha = Centrado(Node("Jugador", caja), 0f);
            Ala(flecha, -32f, new Vector2(-3.6f, -1f), new Color(0f, 0f, 0f, 0.8f), 5f, 17f);
            Ala(flecha, 32f, new Vector2(3.6f, -1f), new Color(0f, 0f, 0f, 0.8f), 5f, 17f);
            Ala(flecha, -32f, new Vector2(-3.6f, -1f), Ink, 3f, 15f);
            Ala(flecha, 32f, new Vector2(3.6f, -1f), Ink, 3f, 15f);

            Color linea = White(0.16f);
            Image(Place(Node("BordeArriba", caja), 0f, 0f, lado, 1f), null, linea);
            Image(Place(Node("BordeAbajo", caja), 0f, lado - 1f, lado, 1f), null, linea);
            Image(Place(Node("BordeIzquierda", caja), 0f, 0f, 1f, lado), null, linea);
            Image(Place(Node("BordeDerecha", caja), lado - 1f, 0f, 1f, lado), null, linea);
        }

        public void PonerMapa(Texture2D dibujo, Vector2 metros)
        {
            imagen.texture = dibujo;
            imagen.enabled = true;
            mapa.sizeDelta = metros * escala;
        }

        // centro: el punto del mundo que queda en el medio de la vista. giroMapa: cuánto gira el mapa (grados).
        public void Dibujar(Vector3 centro, float giroMapa, Bounds limites, Transform jugador, float mirada)
        {
            giro.localRotation = Quaternion.Euler(0f, 0f, giroMapa);
            mapa.anchoredPosition = new Vector2(limites.center.x - centro.x, limites.center.z - centro.z) * escala;
            Quaternion derecho = Quaternion.Euler(0f, 0f, -giroMapa); // letras e íconos no giran con el mapa

            // La flecha: en el radar queda en el centro mirando para arriba; en el mapa completo, donde está el jugador.
            Vector2 lugarJugador = Quaternion.Euler(0f, 0f, giroMapa) * (Vector3)EnMapa(jugador.position, centro);
            flecha.anchoredPosition = lugarJugador;
            flecha.localRotation = Quaternion.Euler(0f, 0f, giroMapa - mirada);

            Companeros(centro, derecho);
            Rivales(centro);
            Zonas(centro, derecho);
            Dispositivo(centro, derecho);
        }

        private Vector2 EnMapa(Vector3 lugar, Vector3 centro) => new Vector2(lugar.x - centro.x, lugar.z - centro.z) * escala;

        // CA3 y CA5: compañeros vivos en azul; el que lleva el dispositivo, con su ícono.
        private void Companeros(Vector3 centro, Quaternion derecho)
        {
            PartidaEnRed partida = PartidaEnRed.Actual;
            JugadorEnRed local = partida != null ? partida.Local : null;
            bool hayEquipos = local != null && EquiposTacticos.HayEquipos;

            sobran.Clear();
            foreach (KeyValuePair<JugadorEnRed, RectTransform> par in companeros)
                if (par.Key == null) { if (par.Value != null) Object.Destroy(par.Value.gameObject); sobran.Add(par.Key); }
            foreach (JugadorEnRed j in sobran) { companeros.Remove(j); iconos.Remove(j); }

            if (!hayEquipos)
            {
                foreach (RectTransform p in companeros.Values) p.gameObject.SetActive(false);
                return;
            }

            int portador = MarcadorTactico.Portador;
            foreach (JugadorEnRed j in partida.Jugadores)
            {
                if (j == null || j == local) continue;
                bool ver = j.Vivo && EquiposTacticos.SonAliados(local.Actor, j.Actor);
                if (!companeros.TryGetValue(j, out RectTransform punto))
                {
                    if (!ver) continue;
                    punto = Marca("Compañero", 14f, new Color(0f, 0f, 0f, 0.75f));
                    Image(Centrado(Node("Color", punto), 10f), redondeado, MatchHud.TeamColor, 5f);
                    RectTransform icono = Centrado(Node("Dispositivo", punto), 28f);
                    icono.localScale = Vector3.one * 0.6f;
                    icono.anchoredPosition = new Vector2(0f, 14f);
                    MatchHud.DeviceGlyph(icono, Accent, redondeado, out Img _).anchoredPosition = Vector2.zero;
                    companeros[j] = punto;
                    iconos[j] = icono.gameObject;
                }
                punto.gameObject.SetActive(ver);
                if (!ver) continue;
                punto.anchoredPosition = EnMapa(j.transform.position, centro);
                punto.localRotation = derecho;
                bool lleva = portador != 0 && j.Actor == portador;
                GameObject marca = iconos[j];
                if (marca.activeSelf != lleva) marca.SetActive(lleva);
            }
        }

        // CA4: rivales vistos por el equipo, en rojo.
        private void Rivales(Vector3 centro)
        {
            IReadOnlyList<Vector3> lugares = RivalesVistos.Lugares;
            for (int i = 0; i < lugares.Count; i++)
            {
                if (i >= rivales.Count)
                {
                    RectTransform punto = Marca("Rival", 14f, new Color(0f, 0f, 0f, 0.75f));
                    Image(Centrado(Node("Color", punto), 10f), redondeado, MatchHud.RivalColor, 5f);
                    rivales.Add(punto);
                }
                rivales[i].gameObject.SetActive(true);
                rivales[i].anchoredPosition = EnMapa(lugares[i], centro);
            }
            for (int i = lugares.Count; i < rivales.Count; i++) rivales[i].gameObject.SetActive(false);
        }

        // CA5: zonas de plantado, con su letra.
        private void Zonas(Vector3 centro, Quaternion derecho)
        {
            IReadOnlyList<ZonaDePlantado> todas = ZonaDePlantado.Todas;
            if (zonas.Count != todas.Count)
            {
                foreach (var vieja in zonas) if (vieja.rect != null) Object.Destroy(vieja.rect.gameObject);
                zonas.Clear();
                foreach (ZonaDePlantado zona in todas)
                {
                    RectTransform rect = Centrado(Node("Zona " + zona.nombre, giro), 0f);
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
                if (entrada.zona == null || entrada.rect == null) continue;
                Bounds b = entrada.zona.Limites;
                entrada.rect.anchoredPosition = EnMapa(b.center, centro);
                entrada.rect.sizeDelta = new Vector2(Mathf.Max(b.size.x * escala, 14f), Mathf.Max(b.size.z * escala, 14f));
                entrada.letra.localRotation = derecho;
            }
        }

        // CA5: el dispositivo en el piso (naranja) o plantado (rojo, con la luz que titila).
        private void Dispositivo(Vector3 centro, Quaternion derecho)
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
            dispositivo.anchoredPosition = EnMapa(lugar.Value, centro);
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
    }

    private static void Ala(RectTransform padre, float angulo, Vector2 lugar, Color color, float ancho, float largo)
    {
        RectTransform ala = Centrado(Node("Ala", padre), 0f);
        ala.anchoredPosition = lugar;
        ala.sizeDelta = new Vector2(ancho, largo);
        ala.localRotation = Quaternion.Euler(0f, 0f, angulo);
        Image(ala, null, color);
    }

    private static RectTransform Centrado(RectTransform rect, float lado)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(lado, lado);
        return rect;
    }

    // =====================================================================
    // Armado
    // =====================================================================

    public void Iniciar(RectTransform raiz, TMP_FontAsset label, Sprite sprite)
    {
        fuente = label;
        redondeado = sprite;

        // El radar (CA1)
        chico = new Vista(Place(Node("Minimapa", raiz), X, Y, Lado, Lado), Lado) { escala = Escala };
        ayuda = Text(Place(Node("Ayuda", raiz), X, Y + Lado + 4f, Lado, 18f), fuente, 13f, Mute, TextAlignmentOptions.MidlineLeft, 10f, true);

        // El mapa completo (CA6), en el centro; se ve solo con la tecla apretada.
        RectTransform fondo = Centrado(Node("MapaCompleto", raiz), LadoGrande + 40f);
        Image(fondo, redondeado, Rgb(8, 10, 14, 0.82f), 12f);
        RectTransform caja = Centrado(Node("Mapa", fondo), LadoGrande);
        grande = new Vista(caja, LadoGrande) { escala = 1f };
        fondo.gameObject.SetActive(false);
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

    /// <summary>
    /// Espectador (US 133): el radar se centra en otro jugador y gira con su mirada. El dibujo del mapa es el mismo.
    /// </summary>
    public void Seguir(Transform quien, Transform mirada)
    {
        if (quien == null) return;
        jugador = quien;
        vista = mirada != null ? mirada : quien;
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
        medidaDelMapa = new Vector2(ancho * celda, alto * celda);
        // El dibujo cubre desde limites.min: se guarda ese rectángulo exacto.
        limites.SetMinMax(limites.min, new Vector3(limites.min.x + ancho * celda, limites.max.y, limites.min.z + alto * celda));
        chico.PonerMapa(dibujo, medidaDelMapa);
        grande.PonerMapa(dibujo, medidaDelMapa);
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
        if (dibujo != null) Object.Destroy(dibujo);
    }

    // =====================================================================
    // Cuadro a cuadro
    // =====================================================================

    private void LateUpdate()
    {
        if (jugador == null || chico == null) return;

        string tecla = KeyBindings.Label(GameAction.Mapa);
        if (tecla != teclaMostrada)
        {
            teclaMostrada = tecla;
            ayuda.text = $"<color=#F3F4F6>{tecla}</color>  Mapa";
        }

        // CA2: el radar gira con la mirada y el jugador queda en el centro.
        float mirada = vista != null ? vista.eulerAngles.y : jugador.eulerAngles.y;
        chico.Dibujar(jugador.position, mirada, limites, jugador, mirada);

        // CA6: el mapa completo, con el norte arriba, mientras se mantiene la tecla.
        bool abierto = dibujo != null && KeyBindings.Held(GameAction.Mapa) && !PauseMenu.IsPaused && !ShopUI.IsOpen;
        GameObject fondo = grande.caja.parent.gameObject;
        if (fondo.activeSelf != abierto) fondo.SetActive(abierto);
        if (abierto)
        {
            float escala = LadoGrande / Mathf.Max(1f, Mathf.Max(medidaDelMapa.x, medidaDelMapa.y));
            if (!Mathf.Approximately(escala, grande.escala))
            {
                grande.escala = escala;
                grande.PonerMapa(dibujo, medidaDelMapa);
            }
            grande.Dibujar(limites.center, 0f, limites, jugador, mirada);
        }
    }
}
