using TMPro;
using UnityEngine;
using static ShopUIKit;
using Img = UnityEngine.UI.Image;
using Slider = UnityEngine.UI.Slider;

// Pestaña "Mira" de las opciones (US 172): cambia cómo se ve la mira del HUD (US 171).
// - CA1: largo, grosor y hueco, y punto central y borde sí o no. Además se elige el tipo: cruz, círculo o punto.
// - CA2: blanco, verde, amarillo, celeste, rojo o un color propio (rojo, verde y azul).
// - CA3: mientras se cambia, la mira se ve de muestra sobre un fondo claro y uno oscuro, al tamaño del juego.
// - CA4: "Se abre con la dispersión": la mira se separa según la dispersión del arma (lo hace CombatHud).
// - CA5: se guarda con "Aplicar" (MiraConfig, en PlayerPrefs) y se usa al volver a entrar al juego.
// - CA6: "Restablecer" vuelve a la mira por defecto.
// En el menú principal la crea OpcionesPantalla (pestaña nueva, sin tocar la escena); en la pausa, PauseMenu (Setup).
public class MiraUIController : MonoBehaviour, OpcionesPantalla.ISeccion
{
    private static readonly string[] NoSi = { "No", "Sí" };
    private static readonly string[] FijaAbre = { "Fija", "Se abre" };

    private MiraConfig.Mira guardado, pendiente;
    private OpcionesPantalla pantalla;
    private bool listo;

    // Uso en el menú de pausa (Setup)
    private bool enPausa;
    private TMP_FontAsset fuenteDisplay, fuenteLabel, fuenteBody;
    private Sprite redondeado;
    private System.Action alVolver;

    // Controles
    private OpcionesKit.Segmentos segTipo, segPunto, segBorde, segDinamica;
    private Slider sliderLargo, sliderGrosor, sliderHueco, sliderR, sliderG, sliderB;
    private TextMeshProUGUI textoLargo, textoGrosor, textoHueco, textoR, textoG, textoB, textoPropio;
    private readonly Img[] marcos = new Img[MiraConfig.ColorPropio + 1];
    private Img fondoPropio;
    private readonly MiraDibujo[] muestras = new MiraDibujo[2];

    private void Start()
    {
        pantalla = OpcionesPantalla.De(this);
        if (pantalla != null) pantalla.Registrar(this);

        guardado = MiraConfig.Actual;
        pendiente = guardado;
        Armar((RectTransform)transform);
        listo = true;
        Mostrar();
    }

    /// <summary>Para usar la pestaña en el menú de pausa: con estas tipografías y un botón "Volver".</summary>
    public void Setup(TMP_FontAsset display, TMP_FontAsset label, TMP_FontAsset body, Sprite sprite, System.Action onBack)
    {
        enPausa = true;
        fuenteDisplay = display;
        fuenteLabel = label;
        fuenteBody = body;
        redondeado = sprite;
        alVolver = onBack;
    }

    private void OnEnable()
    {
        if (!listo) return;
        if (!HayCambios)
        {
            guardado = MiraConfig.Actual;
            pendiente = guardado;
        }
        Mostrar();
    }

    // =========================================================
    // Armado
    // =========================================================

    private void Armar(RectTransform panel)
    {
        OpcionesKit kit = enPausa
            ? OpcionesKit.ArmarCon(panel, "Mira", fuenteDisplay, fuenteLabel, fuenteBody, redondeado)
            : OpcionesKit.Armar(panel, "Mira");

        // Con lugar (menú principal) van dos columnas: controles a la izquierda y vista previa a la derecha.
        // En la pausa la columna es angosta: la vista previa va arriba y todo lo demás abajo.
        bool ancho = kit.width >= 1000f;
        float y = 114f;

        if (ancho)
        {
            float mitad = (kit.width - OpcionesKit.Pad * 2f - 48f) / 2f;
            kit.Columna(OpcionesKit.Pad + mitad + 48f, mitad);
            float yDerecha = y;
            VistaPrevia(kit, ref yDerecha, 200f);
            yDerecha += 10f;
            kit.Grupo("Color propio", ref yDerecha);
            ColorPropio(kit, ref yDerecha);
            kit.Columna(OpcionesKit.Pad, mitad);
        }
        else
        {
            VistaPrevia(kit, ref y, 120f);
            y += 6f;
        }

        kit.Grupo("Forma", ref y);
        segTipo = kit.FilaSegmentos("Tipo", ref y, MiraConfig.NombresTipo, i => { pendiente.tipo = i; Mostrar(); });
        sliderLargo = Deslizador(kit, "Largo", ref y, MiraConfig.LargoMin, MiraConfig.LargoMax, out textoLargo, v => pendiente.largo = v);
        sliderGrosor = Deslizador(kit, "Grosor", ref y, MiraConfig.GrosorMin, MiraConfig.GrosorMax, out textoGrosor, v => pendiente.grosor = v);
        sliderHueco = Deslizador(kit, "Hueco", ref y, MiraConfig.HuecoMin, MiraConfig.HuecoMax, out textoHueco, v => pendiente.hueco = v);
        segPunto = kit.FilaSegmentos("Punto central", ref y, NoSi, i => { pendiente.punto = i == 1; Mostrar(); });
        segBorde = kit.FilaSegmentos("Borde", ref y, NoSi, i => { pendiente.borde = i == 1; Mostrar(); });

        y += 6f;
        kit.Grupo("Color y dispersión", ref y);
        FilaColor(kit, ref y);
        if (!ancho) ColorPropio(kit, ref y);
        segDinamica = kit.FilaSegmentos("Dispersión", ref y, FijaAbre, i => { pendiente.dinamica = i == 1; Mostrar(); });

        float yPie = y + 12f;
        kit.Pie(ref y, AplicarConfiguracion, RestablecerConfiguracion);
        if (alVolver != null) kit.Boton(kit.Left + 352f, yPie, 130f, 40f, "Volver", false, alVolver);
    }

    private Slider Deslizador(OpcionesKit kit, string titulo, ref float y, float min, float max, out TextMeshProUGUI valor,
        System.Action<float> alCambiar)
    {
        Slider slider = kit.FilaSlider(titulo, ref y, min, max, out valor);
        slider.wholeNumbers = true;
        slider.onValueChanged.AddListener(v => { alCambiar(v); Mostrar(); });
        return slider;
    }

    // CA3: la mira de muestra sobre un fondo claro y uno oscuro.
    private void VistaPrevia(OpcionesKit kit, ref float y, float alto)
    {
        kit.Grupo("Vista previa", ref y);
        float w = (kit.Inner - 16f) / 2f;
        Color[] fondos = { Rgb(214, 222, 228), Rgb(18, 22, 28) };
        string[] nombres = { "Fondo claro", "Fondo oscuro" };
        for (int i = 0; i < 2; i++)
        {
            RectTransform caja = Place(Node(nombres[i], kit.root), kit.Left + i * (w + 16f), y, w, alto);
            Image(caja, kit.Rounded, fondos[i], 6f);
            muestras[i] = new MiraDibujo(caja);
            Text(Place(Node("Nombre", kit.root), kit.Left + i * (w + 16f), y + alto + 4f, w, 16f), kit.LabelFont, 13f, Mute,
                TextAlignmentOptions.Center, 14f, true).text = nombres[i];
        }
        y += alto + 24f;
    }

    // CA2: los cinco colores fijos y "Propio".
    private void FilaColor(OpcionesKit kit, ref float y)
    {
        kit.Etiqueta("Color", y);
        float x = kit.ControlX, top = y + (OpcionesKit.RowH - 30f) / 2f;
        for (int i = 0; i < MiraConfig.Colores.Length; i++)
        {
            int indice = i;
            marcos[i] = Image(Place(Node("Marco", kit.root), x - 3f, top - 3f, 36f, 36f), kit.Rounded, Accent, 5f);
            RectTransform muestra = Place(Node("Color" + i, kit.root), x, top, 30f, 30f);
            Image(muestra, kit.Rounded, MiraConfig.Colores[i], 4f, true);
            muestra.gameObject.AddComponent<ShopPointerTarget>().Clicked = () => { pendiente.color = indice; Mostrar(); };
            x += 38f;
        }
        x += 6f;
        float w = Mathf.Min(90f, kit.ControlX + kit.ControlW - x);
        marcos[MiraConfig.ColorPropio] = Image(Place(Node("Marco", kit.root), x - 3f, top - 3f, w + 6f, 36f), kit.Rounded, Accent, 5f);
        RectTransform propio = Place(Node("Propio", kit.root), x, top, w, 30f);
        fondoPropio = Image(propio, kit.Rounded, ChipColor, 3f, true);
        textoPropio = Text(Stretch(Node("Texto", propio)), kit.LabelFont, 16f, Mute, TextAlignmentOptions.Center, 6f, true);
        textoPropio.text = "Propio";
        propio.gameObject.AddComponent<ShopPointerTarget>().Clicked = () => { pendiente.color = MiraConfig.ColorPropio; Mostrar(); };
        y += OpcionesKit.RowH;
    }

    // Rojo, verde y azul del color propio: al mover uno, se pasa a usar el color propio.
    private void ColorPropio(OpcionesKit kit, ref float y)
    {
        sliderR = Canal(kit, "Rojo", ref y, out textoR, v => pendiente.propio.r = v);
        sliderG = Canal(kit, "Verde", ref y, out textoG, v => pendiente.propio.g = v);
        sliderB = Canal(kit, "Azul", ref y, out textoB, v => pendiente.propio.b = v);
    }

    private Slider Canal(OpcionesKit kit, string titulo, ref float y, out TextMeshProUGUI valor, System.Action<byte> alCambiar)
    {
        Slider slider = kit.FilaSlider(titulo, ref y, 0f, 255f, out valor);
        slider.wholeNumbers = true;
        slider.onValueChanged.AddListener(v =>
        {
            alCambiar((byte)Mathf.RoundToInt(v));
            pendiente.color = MiraConfig.ColorPropio;
            Mostrar();
        });
        return slider;
    }

    // =========================================================
    // Mostrar lo pendiente
    // =========================================================

    private void Mostrar()
    {
        if (segTipo == null) return;
        MiraConfig.Mira m = pendiente;
        segTipo.Marcar(m.tipo);
        segPunto.Marcar(m.punto ? 1 : 0);
        segBorde.Marcar(m.borde ? 1 : 0);
        segDinamica.Marcar(m.dinamica ? 1 : 0);
        Poner(sliderLargo, textoLargo, m.largo);
        Poner(sliderGrosor, textoGrosor, m.grosor);
        Poner(sliderHueco, textoHueco, m.hueco);
        Poner(sliderR, textoR, m.propio.r);
        Poner(sliderG, textoG, m.propio.g);
        Poner(sliderB, textoB, m.propio.b);

        for (int i = 0; i < marcos.Length; i++) marcos[i].enabled = i == m.color;
        bool propio = m.color == MiraConfig.ColorPropio;
        fondoPropio.color = propio ? (Color)m.propio : ChipColor;
        // Letra oscura o clara según qué tan claro es el color propio.
        float luz = 0.299f * m.propio.r + 0.587f * m.propio.g + 0.114f * m.propio.b;
        textoPropio.color = !propio ? Mute : luz > 140f ? KeyInk : Ink;
    }

    private static void Poner(Slider slider, TextMeshProUGUI texto, float valor)
    {
        slider.SetValueWithoutNotify(valor);
        texto.text = Mathf.RoundToInt(valor).ToString();
    }

    // CA3: la vista previa se dibuja al tamaño que tiene la mira en el juego (el HUD usa 1080 de alto como referencia).
    // Con la mira dinámica, la de muestra se abre y se cierra sola para ver cómo queda.
    private void Update()
    {
        if (!listo) return;
        float apertura = pendiente.dinamica ? Mathf.PingPong(Time.unscaledTime * 14f, 12f) : 0f;
        float escalaHud = Screen.height / 1080f;
        foreach (MiraDibujo muestra in muestras)
        {
            if (muestra == null) continue;
            float padre = muestra.raiz.parent.lossyScale.x;
            if (padre > 0f) muestra.raiz.localScale = Vector3.one * (escalaHud / padre);
            muestra.Aplicar(pendiente, apertura);
        }
    }

    // =========================================================
    // Aplicar, descartar y restablecer
    // =========================================================

    public bool HayCambios => listo && !pendiente.IgualA(guardado);

    private void AplicarConfiguracion()
    {
        Aplicar();
        if (pantalla != null) pantalla.AvisarGuardado();
    }

    public void Aplicar()
    {
        if (!listo) return;
        MiraConfig.Guardar(pendiente); // CA5
        guardado = pendiente;
    }

    public void Descartar()
    {
        if (!listo) return;
        pendiente = guardado;
        Mostrar();
    }

    private void RestablecerConfiguracion()
    {
        if (pantalla != null) pantalla.ConfirmarRestablecer("la mira", RestablecerAhora);
        else RestablecerAhora();
    }

    // CA6
    private void RestablecerAhora()
    {
        pendiente = MiraConfig.Defecto;
        Mostrar();
        Aplicar();
    }
}
