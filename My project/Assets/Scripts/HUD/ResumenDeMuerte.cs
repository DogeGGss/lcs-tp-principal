using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using static ShopUIKit;

// Resumen al morir (F34, US 375). En Táctico y Deathmatch, al morir aparece una tarjeta con lo que pasó en el
// enfrentamiento, mientras se espera para reaparecer o mientras dura la ronda (modo espectador). Va a la izquierda, así
// no tapa la mira ni el nombre del jugador que se mira (CA1).
// - Junta, durante cada vida, el daño que recibió el jugador de cada rival (con la zona: cabeza, cuerpo o piernas; lo
//   anota JugadorEnRed.RpcDanio) y el que él les hizo (la copia de cada rival en esta computadora, DamageRedirect), más
//   todo el daño recibido de cualquier cosa (HealthSystem.Damaged), para las muertes sin asesino (CA5).
// - Al morir, JugadorEnRed.MostrarResumen arma la Muerte: quién lo mató, su personaje, el arma, la distancia y la vida y
//   el escudo que le quedaron (CA2). Al reaparecer (o al empezar la ronda) todo vuelve a cero y la tarjeta se va.
// Lo que se ve son números enteros del daño de cada golpe, antes del escudo, como en la tienda.
public static class ResumenDeMuerte
{
    public sealed class Registro
    {
        public int danio, impactos, cabeza, cuerpo, piernas;
    }

    /// <summary>Lo que se sabe al morir. Sin asesino (asesino 0), causa dice qué pasó (CA5).</summary>
    public sealed class Muerte
    {
        public int asesino;
        public string nombre = "", personaje = "", arma = "", causa;
        public float distancia;
        public int vida, escudo;
        public bool conVida, cabeza;
    }

    /// <summary>La tarjeta armada: lo de la muerte más el daño de los dos y el de los demás.</summary>
    public sealed class Resumen
    {
        public Muerte muerte;
        public Registro recibidoDelAsesino = new Registro(), hechoAlAsesino = new Registro();
        public List<(string nombre, int danio)> otros = new List<(string, int)>();
        public int recibidoTotal;
    }

    private static readonly Dictionary<int, Registro> recibido = new Dictionary<int, Registro>();
    private static readonly Dictionary<int, Registro> hecho = new Dictionary<int, Registro>();
    private static int total;
    private static HealthSystem seguida;
    private static bool escuchando;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar()
    {
        recibido.Clear();
        hecho.Clear();
        total = 0;
        seguida = null;
        escuchando = false;
    }

    /// <summary>La vida del jugador de esta computadora, para sumar todo el daño que recibe.</summary>
    public static void Seguir(HealthSystem vida)
    {
        seguida = vida;
        NuevaVida();
        if (escuchando) return;
        escuchando = true;
        HealthSystem.Damaged += (hs, danio, _) => { if (hs != null && hs == seguida) total += Mathf.Max(0, danio); };
    }

    /// <summary>CA3 y CA4: un rival le hizo daño al jugador de esta computadora.</summary>
    public static void Recibido(int actor, int danio, BodyZone zona) => Sumar(recibido, actor, danio, zona);

    /// <summary>CA3: el jugador de esta computadora le hizo daño a un rival.</summary>
    public static void Hecho(int actor, int danio) => Sumar(hecho, actor, danio, BodyZone.Body);

    private static void Sumar(Dictionary<int, Registro> d, int actor, int danio, BodyZone zona)
    {
        if (actor <= 0 || danio <= 0) return;
        if (!d.TryGetValue(actor, out Registro r)) d[actor] = r = new Registro();
        r.danio += danio;
        r.impactos++;
        if (zona == BodyZone.Head) r.cabeza += danio;
        else if (zona == BodyZone.Legs) r.piernas += danio;
        else r.cuerpo += danio;
    }

    /// <summary>Reapareció o empezó otra ronda: la tarjeta se va y la vida nueva empieza de cero.</summary>
    public static void NuevaVida()
    {
        recibido.Clear();
        hecho.Clear();
        total = 0;
        TarjetaDeMuerte.Esconder();
    }

    /// <summary>Murió: arma la tarjeta con lo juntado en esta vida y la muestra (CA1).</summary>
    public static void Mostrar(Muerte m)
    {
        TarjetaDeMuerte.Mostrar(Armar(m, recibido, hecho, total, NombreDe));
    }

    private static string NombreDe(int actor)
    {
        Player p = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.GetPlayer(actor) : null;
        return p != null ? p.NickName : L10n.T("gameplay.ui.player");
    }

    /// <summary>Puro (para las pruebas): junta la muerte con el daño de esta vida.</summary>
    public static Resumen Armar(Muerte m, Dictionary<int, Registro> recibidos, Dictionary<int, Registro> hechos, int totalRecibido,
        System.Func<int, string> nombreDe)
    {
        var r = new Resumen { muerte = m, recibidoTotal = totalRecibido };
        if (m.asesino > 0)
        {
            if (recibidos.TryGetValue(m.asesino, out Registro de)) r.recibidoDelAsesino = de;
            if (hechos.TryGetValue(m.asesino, out Registro a)) r.hechoAlAsesino = a;
        }
        // CA4: los demás que lo dañaron, de más a menos daño.
        foreach (KeyValuePair<int, Registro> kv in recibidos)
            if (kv.Key != m.asesino && kv.Value.danio > 0) r.otros.Add((nombreDe(kv.Key), kv.Value.danio));
        r.otros.Sort((x, y) => y.danio.CompareTo(x.danio));
        int deJugadores = 0;
        foreach (Registro x in recibidos.Values) deJugadores += x.danio;
        r.recibidoTotal = Mathf.Max(totalRecibido, deJugadores);
        return r;
    }
}

// La tarjeta del resumen al morir (US 375), en su propio canvas, arriba del HUD y debajo de la tabla y el resultado.
public class TarjetaDeMuerte : MonoBehaviour
{
    private static TarjetaDeMuerte actual;
    private const float X = 40f, Y = 330f, W = 480f; // debajo del minimapa

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar() => actual = null;

    public static void Mostrar(ResumenDeMuerte.Resumen r)
    {
        Esconder();
        MatchHud hud = MatchHud.Instance;
        if (hud == null || hud.DisplayFont == null) return;
        var go = new GameObject("ResumenAlMorir", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        actual = go.AddComponent<TarjetaDeMuerte>();
        actual.Armar(r, hud);
    }

    public static void Esconder()
    {
        if (actual != null) Destroy(actual.gameObject);
        actual = null;
    }

    private void OnDestroy()
    {
        if (actual == this) actual = null;
    }

    // Con la pausa o la tienda (elegir equipo en Deathmatch) abiertas, no se superpone con ellas.
    private Canvas lienzo;

    private void Update()
    {
        if (lienzo == null) lienzo = GetComponent<Canvas>();
        bool ver = !PauseMenu.IsPaused && !ShopUI.IsOpen;
        if (lienzo.enabled != ver) lienzo.enabled = ver;
    }

    private void Armar(ResumenDeMuerte.Resumen r, MatchHud hud)
    {
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120; // arriba del HUD; la tabla con Tab (150) y el resultado (200) la tapan
        var escala = GetComponent<UnityEngine.UI.CanvasScaler>();
        escala.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;

        TMP_FontAsset titulo = hud.DisplayFont, rotulo = hud.LabelFont;
        Sprite redondeado = hud.Rounded;
        RectTransform raiz = Node("Contenido", transform);
        raiz.anchorMin = raiz.anchorMax = raiz.pivot = new Vector2(0f, 0.5f); // pegada a la izquierda, a media altura
        raiz.anchoredPosition = Vector2.zero;
        raiz.sizeDelta = new Vector2(1920f, 1080f);

        RectTransform tarjeta = Place(Node("Tarjeta", raiz), X, Y, W, 100f);
        var filas = new List<System.Action<float>>();
        float y = 18f;

        ResumenDeMuerte.Muerte m = r.muerte;
        bool conAsesino = m.asesino > 0;

        Texto(tarjeta, 22f, y, W - 44f, 18f, rotulo, 14f, Accent, L10n.T(conAsesino ? "gameplay.death.card.killed_by" : "gameplay.death.card.title"), true, 12f);
        if (conAsesino && m.cabeza)
        {
            TextMeshProUGUI chip = Texto(tarjeta, W - 22f - 150f, y - 1f, 150f, 20f, rotulo, 13f, Accent, L10n.T("gameplay.death.card.headshot"), true, 10f);
            chip.alignment = TextAlignmentOptions.MidlineRight;
        }
        y += 22f;

        if (conAsesino)
        {
            // CA2: quién, con qué personaje y arma, a qué distancia y lo que le quedó.
            TextMeshProUGUI n = Texto(tarjeta, 22f, y - 4f, W - 44f, 56f, titulo, 40f, Ink, m.nombre, false, 2f);
            n.richText = false;
            n.overflowMode = TextOverflowModes.Ellipsis;
            y += 48f;
            string detalle = string.IsNullOrEmpty(m.personaje) ? m.arma : string.IsNullOrEmpty(m.arma) ? m.personaje : m.personaje + " · " + m.arma;
            if (!string.IsNullOrEmpty(detalle)) { Texto(tarjeta, 22f, y - 3f, W - 44f, 32f, titulo, 22f, Soft, detalle, false, 1f); y += 28f; }
            y += 6f;
            float mitad = (W - 44f) / 2f;
            Dato(tarjeta, 22f, y, mitad, rotulo, titulo, L10n.T("gameplay.death.card.distance"),
                m.distancia > 0f ? L10n.T("gameplay.death.card.distance_fmt", Mathf.RoundToInt(m.distancia)) : "—");
            Dato(tarjeta, 22f + mitad, y, mitad, rotulo, titulo, L10n.T("gameplay.death.card.remaining"),
                m.conVida ? L10n.T("gameplay.death.card.hp_fmt", m.vida, m.escudo) : "—");
            y += 54f;
            Linea(tarjeta, y); y += 14f;

            // CA3: el daño de los dos; el que recibió, por zona.
            Texto(tarjeta, 22f, y, W - 44f, 18f, rotulo, 13f, Mute, L10n.T("gameplay.death.card.you_dealt"), true, 10f);
            Texto(tarjeta, 22f, y + 18f, W - 44f, 28f, titulo, 26f, Ink, Danio(r.hechoAlAsesino), false, 1f);
            y += 54f;
            Texto(tarjeta, 22f, y, W - 44f, 18f, rotulo, 13f, Mute, L10n.T("gameplay.death.card.they_dealt"), true, 10f);
            Texto(tarjeta, 22f, y + 18f, W - 44f, 28f, titulo, 26f, Ink, Danio(r.recibidoDelAsesino), false, 1f);
            y += 52f;
            y = Zonas(tarjeta, y, rotulo, titulo, redondeado, r.recibidoDelAsesino);
        }
        else
        {
            // CA5: no lo mató un jugador: qué pasó y el daño que recibió.
            TextMeshProUGUI c = Texto(tarjeta, 22f, y - 4f, W - 44f, 50f, titulo, 34f, Ink, m.causa, false, 2f);
            c.overflowMode = TextOverflowModes.Ellipsis;
            y += 46f;
            Dato(tarjeta, 22f, y, W - 44f, rotulo, titulo, L10n.T("gameplay.death.card.received"), r.recibidoTotal.ToString());
            y += 54f;
        }

        // CA4: los demás que lo dañaron en esta vida.
        if (r.otros.Count > 0)
        {
            Linea(tarjeta, y); y += 14f;
            Texto(tarjeta, 22f, y, W - 44f, 18f, rotulo, 13f, Mute, L10n.T("gameplay.death.card.others"), true, 10f);
            y += 24f;
            int mostrados = Mathf.Min(4, r.otros.Count);
            for (int i = 0; i < mostrados; i++)
            {
                TextMeshProUGUI on = Texto(tarjeta, 22f, y - 3f, W - 44f - 90f, 32f, titulo, 22f, Soft, r.otros[i].nombre, false, 1f);
                on.richText = false;
                on.overflowMode = TextOverflowModes.Ellipsis;
                Texto(tarjeta, W - 22f - 90f, y - 3f, 90f, 32f, titulo, 22f, Ink, r.otros[i].danio.ToString(), false, 0f).alignment = TextAlignmentOptions.MidlineRight;
                y += 28f;
            }
            if (r.otros.Count > mostrados) { Texto(tarjeta, 22f, y, W - 44f, 22f, rotulo, 14f, Mute, "+" + (r.otros.Count - mostrados), false, 0f); y += 24f; }
        }
        y += 10f;

        tarjeta.sizeDelta = new Vector2(W, y);
        // Fondo detrás de todo (se crea al final con el alto justo y se manda atrás).
        var fondo = Image(Place(Node("Fondo", tarjeta), 0f, 0f, W, y), redondeado, Rgb(10, 12, 17, 0.97f), 10f);
        fondo.transform.SetAsFirstSibling();
        var borde = Image(Place(Node("Color", tarjeta), 0f, 10f, 4f, y - 20f), null, conAsesino ? MatchHud.RivalColor : Mute);
        borde.transform.SetSiblingIndex(1);
    }

    private static string Danio(ResumenDeMuerte.Registro r) =>
        r.impactos == 0 ? L10n.T("gameplay.death.card.no_damage") : L10n.Plural("gameplay.death.card.hits", r.impactos, r.danio);

    private static float Zonas(RectTransform padre, float y, TMP_FontAsset rotulo, TMP_FontAsset titulo, Sprite redondeado, ResumenDeMuerte.Registro r)
    {
        (string nombre, int valor)[] zonas =
        {
            (L10n.T("gameplay.practice.zone.head"), r.cabeza), (L10n.T("gameplay.practice.zone.body"), r.cuerpo), (L10n.T("gameplay.practice.zone.legs"), r.piernas),
        };
        float ancho = (W - 44f - 2f * 10f) / 3f;
        for (int i = 0; i < zonas.Length; i++)
        {
            float x = 22f + i * (ancho + 10f);
            Image(Place(Node("Zona", padre), x, y, ancho, 52f), redondeado, White(0.05f), 6f);
            Texto(padre, x + 10f, y + 6f, ancho - 20f, 16f, rotulo, 12f, Mute, zonas[i].nombre, true, 10f);
            Texto(padre, x + 10f, y + 22f, ancho - 20f, 26f, titulo, 24f, zonas[i].valor > 0 ? Ink : Mute, zonas[i].valor.ToString(), false, 0f);
        }
        return y + 62f;
    }

    private static void Dato(RectTransform padre, float x, float y, float w, TMP_FontAsset rotulo, TMP_FontAsset titulo, string nombre, string valor)
    {
        Texto(padre, x, y, w - 8f, 18f, rotulo, 13f, Mute, nombre, true, 10f);
        Texto(padre, x, y + 18f, w - 8f, 30f, titulo, 26f, Ink, valor, false, 1f);
    }

    private static void Linea(RectTransform padre, float y) => Image(Place(Node("Linea", padre), 22f, y, W - 44f, 1f), null, White(0.08f));

    private static TextMeshProUGUI Texto(RectTransform padre, float x, float y, float w, float h, TMP_FontAsset fuente, float tam, Color color,
        string texto, bool mayusculas, float espacio)
    {
        TextMeshProUGUI t = Text(Place(Node("Texto", padre), x, y, w, h), fuente, tam, color, TextAlignmentOptions.MidlineLeft, espacio, mayusculas);
        t.text = texto ?? "";
        return t;
    }
}
