using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using static ShopUIKit;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Img = UnityEngine.UI.Image;

// F31 (Invasión cooperativa), US 354: ver a los compañeros. La agrega PartidaDeInvasion en cooperativo. Con las
// fuentes, colores y sombra del HUD de combate (US 176).
// - CA1: a la izquierda, debajo de «Tus cartas», cada compañero con su nombre y una barra con su vida; si está caído
//   (US 357) o muerto, se marca (y la barra se apaga).
// - CA2: sobre cada compañero, su nombre, también a través de las paredes (es parte de la pantalla, no del mundo). Un
//   compañero caído se marca con un ícono rojo con una cruz y los metros; si está fuera de la pantalla, el ícono queda
//   pegado al borde del lado donde está, así se lo encuentra.
// - CA3: los compañeros en el minimapa: Minimapa (azules; los caídos, rojos).
// - CA4: con Tab, la tabla de todos los jugadores con sus bajas y sus puntos (MarcadorInvasion la pide acá). Cada uno
//   publica los suyos como propiedad del jugador en la sala.
public class CompanerosEnInvasion : MonoBehaviour
{
    private const string PropBajas = "inv.bajas", PropPuntos = "inv.pts";
    private const float CadaPublicar = 0.5f;
    private const float ListaX = 24f, ListaY = 430f, AnchoFila = 260f, AltoFila = 46f, EntreFilas = 6f; // CA1
    private const float AlturaDelNombre = 2.15f, AlturaCaido = 0.9f;                                   // CA2 (m)
    private static readonly Color Rojo = Hex(0xFF5C5C);

    private class Fila
    {
        public RectTransform caja;
        public TextMeshProUGUI nombre, estado;
        public RectTransform barra;
        public Img relleno;
    }

    private class Marca
    {
        public RectTransform caja, icono;
        public TextMeshProUGUI nombre;
        public CanvasGroup grupo;
    }

    private RectTransform lienzo, lista;
    private CanvasGroup grupo;
    private TMP_FontAsset display, label;
    private Sprite redondeado;
    private readonly Dictionary<int, Fila> filas = new Dictionary<int, Fila>();
    private readonly Dictionary<int, Marca> marcas = new Dictionary<int, Marca>();
    private readonly List<int> sobran = new List<int>();
    private readonly Dictionary<int, JugadorEnRed> copias = new Dictionary<int, JugadorEnRed>();
    private PuntosDeInvasion puntos;
    private int bajasPublicadas = -1, puntosPublicados = -1;
    private float proximoPublicar;

    private void Update()
    {
        if (!CooperativoDeInvasion.Activo) return;
        Publicar();
        if (lienzo == null)
        {
            if (MatchHud.Instance == null) return;
            Armar();
        }
        bool ver = !ShopUI.IsOpen && ResultadoDeInvasion.Actual == null && !PauseMenu.IsPaused;
        grupo.alpha = ver ? 1f : 0f;
        if (!ver) return;
        BuscarCopias();
        Lista();
        Marcas();
    }

    private void OnDestroy()
    {
        if (lienzo != null) Destroy(lienzo.gameObject);
    }

    // ---------- Armar ----------

    private void Armar()
    {
        display = MatchHud.Instance.DisplayFont;
        label = MatchHud.Instance.LabelFont;
        redondeado = MatchHud.Instance.Rounded;
        var go = new GameObject("Compañeros (Invasión)", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        go.layer = 5;
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 39; // debajo del HUD de combate (40)
        var escala = go.GetComponent<UnityEngine.UI.CanvasScaler>();
        escala.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        lienzo = (RectTransform)go.transform;
        grupo = go.AddComponent<CanvasGroup>();
        grupo.blocksRaycasts = false;
        grupo.interactable = false;
        lista = Place(Node("Lista", lienzo), ListaX, ListaY, AnchoFila, 400f);
    }

    private Fila NuevaFila(int actor)
    {
        var f = new Fila();
        f.caja = Place(Node("Compañero " + actor, lista), 0f, 0f, AnchoFila, AltoFila);
        MarcadorInvasion.Figura<FormaRedondeada>(Stretch(Node("Fondo", f.caja))).Pintar(Rgb(10, 12, 17, 0.72f), White(0.08f), 8f, 1f);
        f.nombre = Text(Place(Node("Nombre", f.caja), 12f, 6f, AnchoFila - 100f, 20f), label, 17f, Ink, TextAlignmentOptions.MidlineLeft, 1f);
        f.estado = Text(Place(Node("Estado", f.caja), AnchoFila - 96f, 6f, 84f, 20f), label, 13f, Rojo, TextAlignmentOptions.MidlineRight, 8f, true);
        RectTransform fondo = Place(Node("Barra", f.caja), 12f, 31f, AnchoFila - 24f, 7f);
        Image(fondo, redondeado, White(0.12f), 3f);
        f.barra = Place(Node("Vida", fondo), 0f, 0f, AnchoFila - 24f, 7f);
        f.relleno = Image(f.barra, redondeado, MarcadorInvasion.Verde, 3f);
        Sombra(f.nombre);
        return f;
    }

    private Marca NuevaMarca(int actor)
    {
        var m = new Marca();
        m.caja = Node("Marca " + actor, lienzo);
        m.caja.anchorMin = m.caja.anchorMax = new Vector2(0f, 0f);
        m.caja.pivot = new Vector2(0.5f, 0f);
        m.caja.sizeDelta = new Vector2(220f, 70f);
        m.grupo = m.caja.gameObject.AddComponent<CanvasGroup>();
        m.nombre = Text(Place(Node("Nombre", m.caja), 0f, 0f, 220f, 22f), label, 16f, MatchHud.TeamColor, TextAlignmentOptions.Bottom, 1f);
        Sombra(m.nombre);
        // El ícono de caído: un círculo rojo con una cruz blanca.
        m.icono = Node("Caído", m.caja);
        m.icono.anchorMin = m.icono.anchorMax = m.icono.pivot = new Vector2(0.5f, 1f);
        m.icono.anchoredPosition = new Vector2(0f, -24f);
        m.icono.sizeDelta = new Vector2(34f, 34f);
        Image(Stretch(Node("Círculo", m.icono)), redondeado, Rojo, 17f);
        RectTransform h = Node("Horizontal", m.icono);
        h.anchorMin = h.anchorMax = h.pivot = new Vector2(0.5f, 0.5f);
        h.sizeDelta = new Vector2(18f, 5f);
        Image(h, null, Color.white);
        RectTransform v = Node("Vertical", m.icono);
        v.anchorMin = v.anchorMax = v.pivot = new Vector2(0.5f, 0.5f);
        v.sizeDelta = new Vector2(5f, 18f);
        Image(v, null, Color.white);
        return m;
    }

    private static void Sombra(TextMeshProUGUI texto)
    {
        texto.fontMaterial.EnableKeyword("UNDERLAY_ON");
        texto.fontMaterial.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.8f));
        texto.fontMaterial.SetFloat("_UnderlayOffsetY", -0.6f);
        texto.fontMaterial.SetFloat("_UnderlaySoftness", 0.4f);
    }

    // ---------- Los compañeros ----------

    private void BuscarCopias()
    {
        copias.Clear();
        PartidaEnRed partida = PartidaEnRed.Actual;
        if (partida == null) return;
        foreach (JugadorEnRed j in partida.Jugadores)
            if (j != null && j.photonView != null && !j.photonView.IsMine) copias[j.Actor] = j;
    }

    // CA1: la lista de la izquierda.
    private void Lista()
    {
        sobran.Clear();
        foreach (int actor in filas.Keys) if (PhotonNetwork.CurrentRoom.GetPlayer(actor) == null) sobran.Add(actor);
        foreach (int actor in sobran) { Destroy(filas[actor].caja.gameObject); filas.Remove(actor); }

        float y = 0f;
        foreach (Player p in PhotonNetwork.PlayerListOthers)
        {
            if (!filas.TryGetValue(p.ActorNumber, out Fila f)) filas[p.ActorNumber] = f = NuevaFila(p.ActorNumber);
            f.caja.anchoredPosition = new Vector2(0f, -y);
            y += AltoFila + EntreFilas;
            CaidaEnInvasion.Estado estado = CaidaEnInvasion.EstadoDe(p);
            f.nombre.text = string.IsNullOrEmpty(p.NickName) ? "?" : p.NickName;
            float vida = 0f;
            if (copias.TryGetValue(p.ActorNumber, out JugadorEnRed j) && j != null)
            {
                HealthSystem v = j.GetComponent<HealthSystem>();
                if (v != null && v.maxHealth > 0) vida = Mathf.Clamp01((float)v.currentHealth / v.maxHealth);
            }
            if (estado != CaidaEnInvasion.Estado.Vivo || p.IsInactive) vida = 0f;
            f.barra.sizeDelta = new Vector2((AnchoFila - 24f) * vida, 7f);
            f.relleno.color = vida > 0.3f ? MarcadorInvasion.Verde : Rojo;
            f.relleno.enabled = vida > 0f;
            if (p.IsInactive) { f.estado.text = L10n.T("gameplay.mates.disconnected"); f.estado.color = Mute; }
            else if (estado == CaidaEnInvasion.Estado.Caido) { f.estado.text = L10n.T("gameplay.mates.down"); f.estado.color = Rojo; }
            else if (estado == CaidaEnInvasion.Estado.Muerto) { f.estado.text = L10n.T("gameplay.mates.dead"); f.estado.color = Mute; }
            else f.estado.text = "";
            f.nombre.color = estado == CaidaEnInvasion.Estado.Muerto || p.IsInactive ? Mute : Ink;
        }
    }

    // CA2: el nombre arriba de cada uno (y el ícono de caído).
    private void Marcas()
    {
        Camera cam = Camera.main;
        sobran.Clear();
        foreach (KeyValuePair<int, Marca> par in marcas) if (!copias.ContainsKey(par.Key)) sobran.Add(par.Key);
        foreach (int actor in sobran) { Destroy(marcas[actor].caja.gameObject); marcas.Remove(actor); }
        if (cam == null) return;

        foreach (KeyValuePair<int, JugadorEnRed> par in copias)
        {
            JugadorEnRed j = par.Value;
            if (!marcas.TryGetValue(par.Key, out Marca m)) marcas[par.Key] = m = NuevaMarca(par.Key);
            bool caido = j.EstaCaido;
            bool ver = j.Vivo;
            if (m.caja.gameObject.activeSelf != ver) m.caja.gameObject.SetActive(ver);
            if (!ver) continue;

            Vector3 arriba = j.transform.position + Vector3.up * (caido ? AlturaCaido : AlturaDelNombre);
            Vector3 pantalla = cam.WorldToScreenPoint(arriba);
            bool atras = pantalla.z <= 0f;
            bool afuera = atras || pantalla.x < 0f || pantalla.x > Screen.width || pantalla.y < 0f || pantalla.y > Screen.height;
            if (afuera && !caido) { m.grupo.alpha = 0f; continue; } // (los vivos, solo en la pantalla)
            if (afuera) pantalla = AlBorde(pantalla, atras);
            m.grupo.alpha = 1f;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(lienzo, pantalla, null, out Vector2 local);
            m.caja.anchoredPosition = local + lienzo.rect.size * 0.5f;
            Player p = j.photonView.Owner;
            string nombre = p != null && !string.IsNullOrEmpty(p.NickName) ? p.NickName : "?";
            if (caido)
            {
                float metros = Vector3.Distance(cam.transform.position, j.transform.position);
                m.nombre.text = $"{nombre} · {metros:0} m";
                m.nombre.color = Rojo;
            }
            else
            {
                m.nombre.text = nombre;
                m.nombre.color = MatchHud.TeamColor;
            }
            if (m.icono.gameObject.activeSelf != caido) m.icono.gameObject.SetActive(caido);
        }
    }

    // Pegado al borde de la pantalla, del lado donde está (como la marca del puesto, US 335).
    private static Vector3 AlBorde(Vector3 pantalla, bool atras)
    {
        if (atras) pantalla = new Vector3(Screen.width - pantalla.x, Screen.height - pantalla.y, 0f);
        Vector2 medio = new Vector2(Screen.width, Screen.height) * 0.5f;
        Vector2 dir = (Vector2)pantalla - medio;
        if (atras && dir.y > 0f) dir.y = -dir.y;
        if (dir.sqrMagnitude < 1f) dir = Vector2.down;
        float margen = 70f * Screen.height / 1080f;
        float k = Mathf.Min((medio.x - margen) / Mathf.Max(Mathf.Abs(dir.x), 0.001f), (medio.y - margen) / Mathf.Max(Mathf.Abs(dir.y), 0.001f));
        return medio + dir * k;
    }

    // ---------- CA4: la tabla con Tab ----------

    // Los míos, para la tabla de los demás.
    private void Publicar()
    {
        if (Time.unscaledTime < proximoPublicar || !PhotonNetwork.InRoom) return;
        proximoPublicar = Time.unscaledTime + CadaPublicar;
        if (puntos == null) puntos = FindFirstObjectByType<PuntosDeInvasion>();
        if (puntos == null) return;
        if (puntos.Bajas == bajasPublicadas && puntos.PuntosGanados == puntosPublicados) return;
        bajasPublicadas = puntos.Bajas;
        puntosPublicados = puntos.PuntosGanados;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { PropBajas, bajasPublicadas }, { PropPuntos, puntosPublicados } });
    }

    private static int Numero(Player p, string clave) => p.CustomProperties.TryGetValue(clave, out object v) && v is int n ? n : 0;

    /// <summary>CA4: la tabla con Tab (la pide MarcadorInvasion en cooperativo).</summary>
    public static MatchHud.Table Tabla()
    {
        if (!PhotonNetwork.InRoom) return null;
        var jugadores = new List<Player>(PhotonNetwork.PlayerList);
        jugadores.Sort((a, b) => Numero(b, PropPuntos).CompareTo(Numero(a, PropPuntos)));
        OleadasDeInvasion oleadas = FindFirstObjectByType<OleadasDeInvasion>();
        var tabla = new MatchHud.Table
        {
            corner = oleadas != null ? L10n.T("gameplay.wave.title_fmt", Mathf.Max(1, oleadas.Oleada)) : "",
            columns = new[] { L10n.T("gameplay.mates.player"), L10n.T("gameplay.mates.kills"), L10n.T("gameplay.mates.points"), L10n.T("gameplay.mates.state") },
            widths = new[] { 400f, 120f, 140f, 140f },
        };
        var seccion = new MatchHud.Section();
        foreach (Player p in jugadores)
        {
            CaidaEnInvasion.Estado e = CaidaEnInvasion.EstadoDe(p);
            string estado = p.IsInactive ? L10n.T("gameplay.mates.disconnected")
                : e == CaidaEnInvasion.Estado.Caido ? L10n.T("gameplay.mates.down")
                : e == CaidaEnInvasion.Estado.Muerto ? L10n.T("gameplay.mates.dead") : "";
            seccion.rows.Add(new MatchHud.Row
            {
                cells = new[] { string.IsNullOrEmpty(p.NickName) ? "?" : p.NickName, Numero(p, PropBajas).ToString(), Numero(p, PropPuntos).ToString(), estado },
                highlight = p.IsLocal,
                dim = e == CaidaEnInvasion.Estado.Muerto || p.IsInactive,
            });
        }
        tabla.sections.Add(seccion);
        return tabla;
    }
}
