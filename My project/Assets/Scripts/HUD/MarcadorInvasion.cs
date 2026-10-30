using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using static ShopUIKit;
using Img = UnityEngine.UI.Image;
using VertexHelper = UnityEngine.UI.VertexHelper;

// F08 (Modo Invasión), US 150: el marcador propio del modo, según la guía de diseño (#marcador-zombie). Es otra
// pantalla, no el marcador de Táctico y Deathmatch (US 057): se juega solo contra la horda, así que no hay rivales,
// lista de bajas ni tabla con Tab. Lo agrega PartidaDeInvasion y lee las oleadas de la US 144 (OleadasDeInvasion).
// - CA1: arriba al centro, "Oleada N de 10" (en la 10, con la etiqueta "Jefe"; después, "Oleada N · modo infinito") y
//   debajo el recorrido de las 10 oleadas como una línea de tren: las pasadas en verde, la actual con un pulso, la 10
//   (el jefe) con forma de rombo y después la estación ∞ del modo infinito.
// - CA2: "Quedan N invasores", contando los que todavía no aparecieron, con una barra blanca que se vacía.
// - CA3: en el descanso, "Próxima oleada en 0:20" en verde, con una barra verde que se llena. Muestra la oleada que viene.
// - CA4: los puntos (la billetera, US 147) abajo a la izquierda, en el lugar de la plata del Táctico. Cada vez que suben
//   aparece lo que se sumó ("+60"), en naranja si la baja fue a la cabeza.
// - CA5: una banderita naranja marca en la vía la oleada récord guardada de la dificultad (US 151). Al pasarla aparece
//   "¡Nuevo récord! · Oleada N" durante 3 s, una sola vez por partida.
// - CA6: las fuentes y colores del HUD de combate (US 176), y se oculta con la tienda abierta.
public class MarcadorInvasion : MonoBehaviour
{
    /// <summary>El verde del modo (el del andén de Invasión en la selección de modo).</summary>
    public static readonly Color Verde = Hex(0x7DE05A);

    private const float Ancho = 620f, Alto = 200f, Arriba = 22f; // el panel
    private const float AnchoBarra = 520f;
    private const float DuracionRecord = 3f, Entrada = 0.25f;     // CA5
    private const float DuracionSalto = 0.9f, Subida = 44f;       // CA4: lo que suma cada baja
    private const float PuntosX = 34f, PuntosY = 170f;            // desde abajo a la izquierda, donde está la plata

    private class Salto { public TextMeshProUGUI texto; public float desde; public int cuadro; }

    private OleadasDeInvasion oleadas;
    private PlayerWallet billetera;
    private int record;            // CA5: la oleada récord al empezar la partida (0: todavía no hay)
    private bool avisoRecord;      // CA5: ya salió el aviso
    private float recordDesde = -10f;
    private int puntosAntes;
    private float relleno;         // ancho que se ve de la barra (va hacia el real de a poco)

    private TMP_FontAsset display, label, mono;
    private CanvasGroup grupo;
    private RectTransform panel, chipJefe, puntos, pildora;
    private TextMeshProUGUI titulo, deCuantas, rotulo, numero, unidad, puntosNumero, puntosRotulo, pildoraTexto, banderita;
    private Img barra;
    private ViaDeOleadas via;
    private readonly TextMeshProUGUI[] estaciones = new TextMeshProUGUI[11];
    private CanvasGroup pildoraGrupo;
    private readonly List<Salto> saltos = new List<Salto>();
    private string cabezaAntes, infoAntes; // para acomodar los textos solo cuando cambian

    private void OnEnable()
    {
        WeaponFire.Killed += AlMatar;
        OleadasDeInvasion.Empezo += AlEmpezar;
    }

    private void OnDisable()
    {
        WeaponFire.Killed -= AlMatar;
        OleadasDeInvasion.Empezo -= AlEmpezar;
        if (billetera != null) billetera.MoneyChanged -= AlCambiarPuntos;
    }

    private void OnDestroy()
    {
        if (grupo != null) Destroy(grupo.gameObject);
    }

    private void Start()
    {
        record = MatchSettings.ZombieRecord(MatchSettings.Difficulty);
    }

    private void Update()
    {
        if (oleadas == null) oleadas = GetComponent<OleadasDeInvasion>();
        if (grupo == null)
        {
            if (oleadas == null || MatchHud.Instance == null) return; // el HUD de combate todavía no se armó
            Armar();
        }
        if (billetera == null) SeguirBilletera();

        MatchHud.HideCenter(); // el centro de la US 057 no se usa en este modo (ni en el descanso, que es una fase de compra)
        grupo.alpha = ShopUI.IsOpen || oleadas.EnElFinal ? 0f : 1f; // CA6
        ActualizarPanel();
        ActualizarSaltos();
        ActualizarPildora();
    }

    // ---------- Armado ----------

    private void Armar()
    {
        display = MatchHud.Instance.DisplayFont;
        label = MatchHud.Instance.LabelFont;
        CombatHud combate = FindAnyObjectByType<CombatHud>();
        mono = combate != null && combate.MonoFont != null ? combate.MonoFont : label;

        GameObject lienzoObj = new GameObject("Marcador de Invasión", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        lienzoObj.layer = 5;
        Canvas lienzo = lienzoObj.GetComponent<Canvas>();
        lienzo.renderMode = RenderMode.ScreenSpaceOverlay;
        lienzo.sortingOrder = 41; // sobre el HUD de combate (40) y debajo de la tienda (50)
        lienzo.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
        var escala = lienzoObj.GetComponent<UnityEngine.UI.CanvasScaler>();
        escala.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand; // igual que el HUD de combate
        RectTransform raiz = (RectTransform)lienzoObj.transform;
        grupo = lienzoObj.AddComponent<CanvasGroup>();
        grupo.blocksRaycasts = false;
        grupo.interactable = false;

        ArmarPanel(raiz);
        ArmarPuntos(raiz);
        ArmarPildora(raiz);
    }

    private void ArmarPanel(RectTransform raiz)
    {
        panel = Node("Panel", raiz);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 1f);
        panel.pivot = new Vector2(0.5f, 1f);
        panel.anchoredPosition = new Vector2(0f, -Arriba);
        panel.sizeDelta = new Vector2(Ancho, Alto);
        Figura<FormaRedondeada>(Stretch(Node("Fondo", panel)))
            .Pintar(Rgb(6, 10, 6, DarkAlpha(0.74f)), WithAlpha(Verde, FadeAlpha(0.28f)), 10f, 1f);

        // La línea verde de abajo, con su brillo.
        Image(Place(Node("Brillo", panel), Ancho / 2f - 90f, Alto - 8f, 180f, 17f), MatchHud.Instance.Rounded, WithAlpha(Verde, FadeAlpha(0.18f)), 8f);
        Image(Place(Node("Linea", panel), Ancho / 2f - 75f, Alto - 1f, 150f, 3f), null, Verde);

        // CA1: el título.
        titulo = Text(Node("Oleada", panel), display, 44f, Ink, TextAlignmentOptions.BaselineLeft, 6f, true);
        deCuantas = Text(Node("DeCuantas", panel), display, 20f, Mute, TextAlignmentOptions.BaselineLeft, 14f, true);
        chipJefe = Node("Jefe", panel);
        Image(chipJefe, MatchHud.Instance.Rounded, Verde, 4f);
        TextMeshProUGUI jefe = Text(Stretch(Node("Texto", chipJefe)), display, 15f, Hex(0x10180C), TextAlignmentOptions.Center, 16f, true);
        jefe.text = "Jefe";

        // CA1 y CA5: la vía.
        RectTransform viaRect = Place(Node("Via", panel), 26f, 64f, 568f, 62f);
        via = Figura<ViaDeOleadas>(viaRect);
        via.raycastTarget = false;
        for (int i = 0; i <= OleadasDeInvasion.Ultima; i++)
        {
            float x = ViaDeOleadas.X(i);
            estaciones[i] = Text(Place(Node("Estacion" + (i + 1), viaRect), x - 20f, 52f, 40f, 16f), mono, 12f, Mute, TextAlignmentOptions.Baseline);
            estaciones[i].text = i == OleadasDeInvasion.Ultima ? "∞" : (i + 1).ToString();
        }
        banderita = Text(Node("Record", viaRect), display, 11f, Accent, TextAlignmentOptions.BaselineLeft, 12f, true);
        banderita.text = "Récord";
        int r = record > 0 ? Mathf.Min(record, OleadasDeInvasion.Ultima + 1) - 1 : -1;
        via.record = r;
        banderita.gameObject.SetActive(r >= 0);
        if (r >= 0)
        {
            float x = ViaDeOleadas.X(r);
            bool alFinal = r >= OleadasDeInvasion.Ultima - 1; // en las últimas estaciones va a la izquierda del palito
            banderita.alignment = alFinal ? TextAlignmentOptions.BaselineRight : TextAlignmentOptions.BaselineLeft;
            Place(banderita.rectTransform, alFinal ? x - 5f - 120f : x + 15f, 3f, 120f, 16f);
        }

        // CA2 y CA3: lo que queda (o el descanso) y la barra.
        rotulo = Text(Node("Rotulo", panel), display, 16f, Mute, TextAlignmentOptions.BaselineLeft, 16f, true);
        numero = Text(Node("Numero", panel), display, 38f, Ink, TextAlignmentOptions.BaselineLeft, 2f);
        unidad = Text(Node("Unidad", panel), display, 16f, Mute, TextAlignmentOptions.BaselineLeft, 16f, true);
        RectTransform pista = Place(Node("Barra", panel), (Ancho - AnchoBarra) / 2f, 176f, AnchoBarra, 6f);
        Image(pista, MatchHud.Instance.Rounded, White(FadeAlpha(0.12f)), 3f);
        barra = Image(Place(Node("Relleno", pista), 0f, 0f, 0f, 6f), MatchHud.Instance.Rounded, Ink, 3f);
    }

    private void ArmarPuntos(RectTransform raiz)
    {
        puntos = Node("Puntos", raiz);
        puntos.anchorMin = puntos.anchorMax = puntos.pivot = Vector2.zero;
        puntos.anchoredPosition = new Vector2(PuntosX, PuntosY);
        puntos.sizeDelta = new Vector2(600f, 48f);
        puntosNumero = Text(Node("Numero", puntos), display, 44f, Ink, TextAlignmentOptions.BaselineLeft);
        Sombra(puntosNumero);
        puntosRotulo = Text(Node("Rotulo", puntos), display, 16f, Mute, TextAlignmentOptions.BaselineLeft, 14f, true);
        puntosRotulo.text = "Pts";
        Sombra(puntosRotulo);
        MostrarPuntos(0);
    }

    private void ArmarPildora(RectTransform raiz)
    {
        pildora = Node("NuevoRecord", raiz);
        pildora.anchorMin = pildora.anchorMax = pildora.pivot = new Vector2(0.5f, 1f);
        pildoraGrupo = pildora.gameObject.AddComponent<CanvasGroup>();
        RectTransform halo = Node("Brillo", pildora);
        halo.anchorMin = Vector2.zero;
        halo.anchorMax = Vector2.one;
        halo.pivot = new Vector2(0f, 1f);
        halo.offsetMin = new Vector2(-10f, -10f);
        halo.offsetMax = new Vector2(10f, 10f);
        Figura<FormaRedondeada>(halo).Pintar(WithAlpha(Verde, FadeAlpha(0.12f)), Color.clear, 32f, 0f);
        Figura<FormaRedondeada>(Stretch(Node("Fondo", pildora)))
            .Pintar(Rgb(10, 24, 8, DarkAlpha(0.9f)), Verde, 22f, 1f);
        pildoraTexto = Text(Stretch(Node("Texto", pildora)), display, 24f, Verde, TextAlignmentOptions.Center, 10f, true);
        pildora.gameObject.SetActive(false);
    }

    // Las figuras viven en este archivo con otro nombre y Unity no les agrega solo el CanvasRenderer (sin él no se ven).
    private static T Figura<T>(RectTransform rect) where T : UIShape
    {
        rect.gameObject.AddComponent<CanvasRenderer>();
        return rect.gameObject.AddComponent<T>();
    }

    // ---------- CA1, CA2 y CA3: el panel ----------

    private void ActualizarPanel()
    {
        bool descanso = oleadas.Descanso;
        int n = Mathf.Max(1, descanso ? oleadas.Oleada + 1 : oleadas.Oleada); // en el descanso, la que viene
        bool infinito = n > OleadasDeInvasion.Ultima;

        string cabeza = $"{n}|{infinito}";
        if (cabeza != cabezaAntes)
        {
            cabezaAntes = cabeza;
            titulo.text = $"Oleada {n}";
            deCuantas.text = infinito ? "· modo infinito" : $"de {OleadasDeInvasion.Ultima}";
            bool jefe = n == OleadasDeInvasion.Ultima;
            chipJefe.gameObject.SetActive(jefe);
            float w1 = Width(titulo, titulo.text), w2 = Width(deCuantas, deCuantas.text), w3 = 0f;
            if (jefe) w3 = Width(chipJefe.GetComponentInChildren<TextMeshProUGUI>(), "Jefe") + 16f;
            float total = w1 + 12f + w2 + (jefe ? 12f + w3 : 0f), x = (Ancho - total) / 2f;
            Linea(titulo, x, w1, 50f);
            Linea(deCuantas, x + w1 + 12f, w2, 50f);
            if (jefe) Place(chipJefe, x + w1 + 12f + w2 + 12f, 23.5f, w3, 25f);

            int actual = Mathf.Min(n, OleadasDeInvasion.Ultima + 1) - 1;
            via.actual = actual;
            for (int i = 0; i < estaciones.Length; i++) estaciones[i].color = i == actual ? Ink : Mute;
        }
        via.pulso = Mathf.Repeat(Time.time, 1.6f) / 1.6f;
        via.Refrescar();

        float fraccion;
        string izquierda, centro, derecha;
        if (descanso)
        {
            BuyPhase fase = BuyPhase.Current;
            float queda = fase != null ? fase.TimeLeft : 0f;
            int s = Mathf.Max(0, Mathf.CeilToInt(queda));
            izquierda = "Próxima oleada en";
            centro = fase != null ? $"{s / 60}:{s % 60:00}" : "—";
            derecha = "";
            fraccion = fase != null && fase.Duration > 0f ? 1f - queda / fase.Duration : 0f;
        }
        else
        {
            int quedan = oleadas.Quedan;
            izquierda = "Quedan";
            centro = quedan.ToString();
            derecha = quedan == 1 ? "invasor" : "invasores";
            fraccion = oleadas.Total > 0 ? (float)quedan / oleadas.Total : 0f;
        }

        string info = $"{izquierda}|{centro}|{derecha}";
        if (info != infoAntes)
        {
            bool cambioDeFase = infoAntes == null || infoAntes.StartsWith("Quedan") == descanso;
            infoAntes = info;
            rotulo.text = izquierda;
            numero.text = centro;
            unidad.text = derecha;
            numero.color = descanso ? Verde : Ink;
            barra.color = descanso ? Verde : Ink;
            float w1 = Width(rotulo, izquierda), w2 = Width(numero, centro), w3 = derecha.Length > 0 ? Width(unidad, derecha) : 0f;
            float total = w1 + 10f + w2 + (w3 > 0f ? 10f + w3 : 0f), x = (Ancho - total) / 2f;
            Linea(rotulo, x, w1, 159f);
            Linea(numero, x + w1 + 10f, w2, 159f);
            Linea(unidad, x + w1 + 10f + w2 + 10f, Mathf.Max(1f, w3), 159f);
            if (cambioDeFase) relleno = Mathf.Clamp01(fraccion) * AnchoBarra; // la barra salta al cambiar de oleada a descanso
        }
        relleno = Mathf.Lerp(relleno, Mathf.Clamp01(fraccion) * AnchoBarra, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
        Place(barra.rectTransform, 0f, 0f, relleno, 6f);
    }

    // Ubica un texto con su línea de base a la altura y (desde arriba del panel), así los tamaños distintos se alinean.
    private static void Linea(TextMeshProUGUI texto, float x, float ancho, float y) =>
        Place(texto.rectTransform, x, y - 30f, ancho + 4f, 60f);

    // ---------- CA4: los puntos ----------

    private void SeguirBilletera()
    {
        PlayerWallet b = FindFirstObjectByType<PlayerWallet>();
        if (b == null) return;
        billetera = b;
        puntosAntes = b.Money;
        b.MoneyChanged += AlCambiarPuntos;
        MostrarPuntos(b.Money);
    }

    private void AlCambiarPuntos(int ahora)
    {
        int suma = ahora - puntosAntes;
        puntosAntes = ahora;
        MostrarPuntos(ahora);
        if (suma > 0 && oleadas != null && oleadas.Oleada > 0) Saltar(suma); // los puntos del arranque no son una baja
    }

    private void MostrarPuntos(int valor)
    {
        if (puntosNumero == null) return;
        puntosNumero.text = valor.ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".");
        float w = Width(puntosNumero, puntosNumero.text);
        Place(puntosNumero.rectTransform, 0f, 10f, w + 4f, 60f);
        Place(puntosRotulo.rectTransform, w + 10f, 10f, 80f, 60f);
    }

    private void Saltar(int suma)
    {
        if (grupo == null) return;
        RectTransform r = Node("Suma", grupo.transform);
        r.anchorMin = r.anchorMax = r.pivot = Vector2.zero;
        r.sizeDelta = new Vector2(200f, 30f);
        r.anchoredPosition = new Vector2(PuntosX + 2f, PuntosY + 54f);
        TextMeshProUGUI t = Text(r, display, 24f, Ink, TextAlignmentOptions.BottomLeft, 6f);
        t.text = $"+{suma}";
        Sombra(t);
        saltos.Add(new Salto { texto = t, desde = Time.unscaledTime, cuadro = Time.frameCount });
    }

    // La baja llega después de los puntos (el arma avisa al terminar el disparo): el último salto se pinta de naranja.
    private void AlMatar(bool cabeza)
    {
        if (!cabeza || saltos.Count == 0) return;
        Salto ultimo = saltos[saltos.Count - 1];
        if (Time.frameCount - ultimo.cuadro <= 1) ultimo.texto.color = Accent;
    }

    private void ActualizarSaltos()
    {
        for (int i = saltos.Count - 1; i >= 0; i--)
        {
            Salto s = saltos[i];
            float t = (Time.unscaledTime - s.desde) / DuracionSalto;
            if (t >= 1f || s.texto == null)
            {
                if (s.texto != null) Destroy(s.texto.gameObject);
                saltos.RemoveAt(i);
                continue;
            }
            float e = 1f - (1f - t) * (1f - t); // ease-out
            s.texto.rectTransform.anchoredPosition = new Vector2(PuntosX + 2f, PuntosY + 54f + Subida * e);
            s.texto.alpha = 1f - e;
        }
    }

    // ---------- CA5: el récord ----------

    private void AlEmpezar(int oleada)
    {
        if (avisoRecord || record <= 0 || oleada <= record || pildora == null) return;
        avisoRecord = true;
        recordDesde = Time.unscaledTime;
        pildoraTexto.text = $"¡Nuevo récord! · Oleada {oleada}";
        pildora.sizeDelta = new Vector2(Width(pildoraTexto, pildoraTexto.text) + 44f, 44f);
        pildora.gameObject.SetActive(true);
    }

    private void ActualizarPildora()
    {
        if (!pildora.gameObject.activeSelf) return;
        float t = Time.unscaledTime - recordDesde;
        if (t >= DuracionRecord) { pildora.gameObject.SetActive(false); return; }
        float entra = Mathf.Clamp01(t / Entrada), sale = Mathf.Clamp01((DuracionRecord - t) / Entrada);
        pildoraGrupo.alpha = Mathf.Min(entra, sale);
        pildora.anchoredPosition = new Vector2(0f, -252f - 10f * (1f - entra));
    }

    private static void Sombra(TextMeshProUGUI texto)
    {
        Material m = texto.fontMaterial;
        m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.6f));
        m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.5f);
        m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.6f);
    }
}

// La vía de las oleadas (US 150, CA1 y CA5): 11 estaciones cada 52 px sobre una línea, como en la guía de diseño.
public class ViaDeOleadas : UIShape
{
    private const float Y = 34f, Trazo = 2.5f;

    /// <summary>Estación actual (0 a 10; la 10 es ∞).</summary>
    public int actual;
    /// <summary>Estación del récord, o -1 si no hay.</summary>
    public int record = -1;
    /// <summary>Parte del pulso de la estación actual (0 a 1, cada 1,6 s).</summary>
    public float pulso;

    private static readonly Color Apagada = Hex(0x11160F);

    public static float X(int i) => 24f + i * 52f;

    public void Refrescar() => SetVerticesDirty();

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Color verde = MarcadorInvasion.Verde;
        Color linea = White(FadeAlpha(0.18f)), trazo = White(FadeAlpha(0.35f));

        Tramo(vh, X(0), X(OleadasDeInvasion.Ultima), linea);
        if (actual > 0) Tramo(vh, X(0), X(actual), verde);

        for (int i = 0; i <= OleadasDeInvasion.Ultima; i++)
        {
            Vector2 c = new Vector2(X(i), Y);
            bool pasada = i < actual, ahora = i == actual;
            Color relleno = ahora ? Ink : pasada ? verde : Apagada, borde = ahora || pasada ? verde : trazo;
            if (ahora)
            {
                // El pulso: el anillo crece de 1 a 2,4 veces y se apaga.
                float e = 1f - (1f - pulso) * (1f - pulso);
                float escala = 1f + 1.4f * e;
                Anillo(vh, c, 10f * escala, 2f * escala, WithAlpha(verde, 0.9f * (1f - e)));
            }
            if (i == OleadasDeInvasion.Ultima - 1)
            {
                // El jefe: un cuadrado de 16 px girado 45°.
                Rombo(vh, c, 8f + Trazo / 2f, borde);
                Rombo(vh, c, 8f - Trazo / 2f, relleno);
            }
            else
            {
                float r = ahora ? 10f : 7f;
                Disc(vh, c, r + Trazo / 2f, borde, 28);
                Disc(vh, c, r - Trazo / 2f, relleno, 28);
            }
        }

        if (record >= 0)
        {
            // La banderita: un palito de la estación para arriba y el banderín con la muesca.
            float x = X(record);
            Color32 naranja = Accent;
            Quad(vh, new Vector2(x - 1f, 3f), new Vector2(x + 1f, 3f), new Vector2(x + 1f, Y - 12f), new Vector2(x - 1f, Y - 12f), naranja, naranja, naranja, naranja);
            Vector2 a = new Vector2(x, 3f), b = new Vector2(x + 11f, 3f), m = new Vector2(x + 7.5f, 7f), d = new Vector2(x + 11f, 11f), e = new Vector2(x, 11f);
            Tri(vh, a, b, m, naranja, naranja, naranja);
            Tri(vh, a, m, e, naranja, naranja, naranja);
            Tri(vh, e, m, d, naranja, naranja, naranja);
        }
    }

    private static void Tramo(VertexHelper vh, float x0, float x1, Color32 color) =>
        Quad(vh, new Vector2(x0, Y - 2f), new Vector2(x1, Y - 2f), new Vector2(x1, Y + 2f), new Vector2(x0, Y + 2f), color, color, color, color);

    private static void Rombo(VertexHelper vh, Vector2 c, float medio, Color32 color)
    {
        float d = medio * Mathf.Sqrt(2f);
        Quad(vh, c + new Vector2(0f, -d), c + new Vector2(d, 0f), c + new Vector2(0f, d), c + new Vector2(-d, 0f), color, color, color, color);
    }

    private static void Anillo(VertexHelper vh, Vector2 c, float radio, float grosor, Color32 color, int segmentos = 40)
    {
        float afuera = radio + grosor / 2f, adentro = Mathf.Max(0f, radio - grosor / 2f);
        for (int s = 0; s < segmentos; s++)
        {
            float a0 = s * Mathf.PI * 2f / segmentos, a1 = (s + 1) * Mathf.PI * 2f / segmentos;
            Vector2 u0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)), u1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
            Quad(vh, c + u0 * afuera, c + u1 * afuera, c + u1 * adentro, c + u0 * adentro, color, color, color, color);
        }
    }
}

// Rectángulo con esquinas redondeadas, relleno y borde fino (el panel y la píldora del récord de la US 150). Se dibuja
// con su propia malla: un borde semitransparente sobre un fondo semitransparente no se superponen.
public class FormaRedondeada : UIShape
{
    private Color fondo, borde;
    private float radio, grosor;
    private readonly List<Vector2> afuera = new List<Vector2>(), adentro = new List<Vector2>();

    public void Pintar(Color colorFondo, Color colorBorde, float radioEsquinas, float grosorBorde)
    {
        fondo = colorFondo;
        borde = colorBorde;
        radio = radioEsquinas;
        grosor = grosorBorde;
        raycastTarget = false;
        SetVerticesDirty();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        float w = r.width, h = r.height;
        if (w <= 0f || h <= 0f) return;
        float rad = Mathf.Min(radio, w / 2f, h / 2f);
        Contorno(afuera, 0f, 0f, w, h, rad);
        Contorno(adentro, grosor, grosor, w - 2f * grosor, h - 2f * grosor, Mathf.Max(0f, rad - grosor));

        // El fondo, en abanico desde el centro (la forma es convexa).
        Color32 f = fondo;
        int c = vh.currentVertCount;
        vh.AddVert(V(new Vector2(w / 2f, h / 2f)), f, Vector2.zero);
        foreach (Vector2 p in adentro) vh.AddVert(V(p), f, Vector2.zero);
        for (int i = 0; i < adentro.Count; i++) vh.AddTriangle(c, c + 1 + i, c + 1 + (i + 1) % adentro.Count);

        if (grosor <= 0f || borde.a <= 0f) return;
        Color32 b = borde;
        for (int i = 0; i < afuera.Count; i++)
        {
            int j = (i + 1) % afuera.Count;
            Quad(vh, afuera[i], afuera[j], adentro[j], adentro[i], b, b, b, b);
        }
    }

    // Los puntos del contorno, desde arriba a la izquierda y con la y hacia abajo; 8 tramos por esquina.
    private static void Contorno(List<Vector2> puntos, float x, float y, float w, float h, float r)
    {
        puntos.Clear();
        Esquina(puntos, new Vector2(x + r, y + r), r, 180f);
        Esquina(puntos, new Vector2(x + w - r, y + r), r, 270f);
        Esquina(puntos, new Vector2(x + w - r, y + h - r), r, 0f);
        Esquina(puntos, new Vector2(x + r, y + h - r), r, 90f);
    }

    private static void Esquina(List<Vector2> puntos, Vector2 centro, float r, float desde)
    {
        for (int s = 0; s <= 8; s++)
        {
            float a = (desde + s * 90f / 8f) * Mathf.Deg2Rad;
            puntos.Add(centro + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
        }
    }
}
