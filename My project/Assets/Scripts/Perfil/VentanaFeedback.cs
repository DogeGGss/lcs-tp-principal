using TMPro;
using UnityEngine;
using static ShopUIKit;
using static PerfilUI;
using Img = UnityEngine.UI.Image;

// "Danos tu opinión", del menú principal. Tipo (opinión, sugerencia o error), valoración opcional de 1 a 5 y un
// comentario de hasta 500 caracteres. Es una simulación: se guarda en un archivo de la PC y no se envía.
public class VentanaFeedback
{
    private const int Maximo = 500;
    private static readonly string[] Tipos = { "Opinión", "Sugerencia", "Error" };
    private static readonly string[] Valoraciones = { "Muy mala", "Mala", "Regular", "Buena", "Muy buena" };

    private readonly MenuRiftwalker menu;
    private int tipo, valoracion;
    private TMP_InputField campo;
    private TextMeshProUGUI pregunta, contador;
    private Boton enviar;
    private readonly Img[] fondosTipo = new Img[3], fondosValor = new Img[5];
    private readonly TextMeshProUGUI[] textosTipo = new TextMeshProUGUI[3], textosValor = new TextMeshProUGUI[5], numerosValor = new TextMeshProUGUI[5];

    public VentanaFeedback(MenuRiftwalker menu) => this.menu = menu;

    public void Abrir()
    {
        tipo = 0;
        valoracion = 0;
        contador = null; // los de la vez anterior ya no existen
        enviar = null;
        campo = null;
        RectTransform p = menu.AbrirModal(780f, 760f);
        T(p, "Rotulo", 48f, 38f, 680f, 22f, FRotulo, 18f, Accent, "Ayudanos a mejorar", TextAlignmentOptions.MidlineLeft, 16f, true);
        T(p, "Titulo", 46f, 62f, 680f, 54f, FTitulo, 48f, Ink, "Tu opinión importa", TextAlignmentOptions.MidlineLeft, 3f, true);

        T(p, "PreguntaTipo", 48f, 138f, 680f, 28f, FTexto, 22f, Soft, "¿Qué querés contarnos?");
        for (int i = 0; i < 3; i++)
        {
            int k = i;
            RectTransform b = Place(Node(Tipos[i], p), 48f + i * 190f, 174f, 176f, 50f);
            fondosTipo[i] = Image(b, Rounded, PanelColor, 6f, true);
            textosTipo[i] = T(b, "Texto", 0f, 0f, 176f, 50f, FRotulo, 22f, Soft, Tipos[i], TextAlignmentOptions.Center, 6f, true);
            Clickeable(b, () => { tipo = k; Pintar(); });
        }

        T(p, "PreguntaValor", 48f, 246f, 680f, 28f, FTexto, 22f, Soft, "¿Cómo fue tu experiencia? <color=#8E96A3><size=18>(opcional)</size></color>");
        for (int i = 0; i < 5; i++)
        {
            int k = i + 1;
            RectTransform b = Place(Node(Valoraciones[i], p), 48f + i * 138f, 282f, 128f, 84f);
            fondosValor[i] = Image(b, Rounded, PanelColor, 6f, true);
            numerosValor[i] = T(b, "Numero", 0f, 6f, 128f, 46f, FTitulo, 42f, Soft, k.ToString(), TextAlignmentOptions.Center);
            textosValor[i] = T(b, "Texto", 0f, 52f, 128f, 24f, FRotulo, 15f, Mute, Valoraciones[i], TextAlignmentOptions.Center, 6f, true);
            Clickeable(b, () => { valoracion = valoracion == k ? 0 : k; Pintar(); });
        }

        pregunta = T(p, "PreguntaTexto", 48f, 388f, 680f, 28f, FTexto, 22f, Soft, "");
        campo = Campo(p, 48f, 424f, 684f, 170f);
        contador = T(p, "Contador", 532f, 600f, 200f, 22f, FMono, 16f, Mute, "", TextAlignmentOptions.MidlineRight);
        T(p, "Nota", 48f, 600f, 480f, 22f, FTexto, 16f, Mute, "Simulación: se guarda en tu PC y no se envía. Sin datos personales, por favor.");

        CrearBoton(p, 780f - 48f - 290f - 16f - 190f, 760f - 48f - 64f, 190f, 64f, "Cancelar", false, menu.CerrarModal, 26f);
        enviar = CrearBoton(p, 780f - 48f - 290f, 760f - 48f - 64f, 290f, 64f, "Enviar opinión", true, Enviar, 26f);
        Pintar();
        campo.ActivateInputField();
    }

    private TMP_InputField Campo(RectTransform parent, float x, float y, float w, float h)
    {
        RectTransform caja = Place(Node("Campo", parent), x, y, w, h);
        caja.gameObject.SetActive(false);
        Image(caja, Rounded, new Color(0f, 0f, 0f, DarkAlpha(0.5f)), 8f, true);
        Borde(caja, 8f, White(0.3f));
        RectTransform area = Stretch(Node("Area", caja));
        area.offsetMin = new Vector2(18f, 14f);
        area.offsetMax = new Vector2(-18f, -14f);
        area.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        TextMeshProUGUI ejemplo = Text(Stretch(Node("Ejemplo", area)), FTexto, 22f, White(0.3f), TextAlignmentOptions.TopLeft);
        ejemplo.text = "Contanos con tus palabras…";
        ejemplo.textWrappingMode = TextWrappingModes.Normal;
        TextMeshProUGUI valor = Text(Stretch(Node("Texto", area)), FTexto, 22f, Ink, TextAlignmentOptions.TopLeft);
        valor.textWrappingMode = TextWrappingModes.Normal;

        TMP_InputField input = caja.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = area;
        input.textComponent = valor;
        input.placeholder = ejemplo;
        input.fontAsset = FTexto;
        input.pointSize = 22f;
        input.lineType = TMP_InputField.LineType.MultiLineNewline;
        input.characterLimit = Maximo;
        input.customCaretColor = true;
        input.caretColor = Accent;
        input.caretWidth = 2;
        input.selectionColor = WithAlpha(Accent, 0.35f);
        input.onValueChanged.AddListener(_ => Pintar());
        caja.gameObject.SetActive(true);
        return input;
    }

    private void Pintar()
    {
        for (int i = 0; i < 3; i++)
        {
            bool on = i == tipo;
            fondosTipo[i].color = on ? Accent : ChipColor;
            textosTipo[i].color = on ? AccentInk : Soft;
        }
        for (int i = 0; i < 5; i++)
        {
            bool on = i + 1 == valoracion;
            fondosValor[i].color = on ? Over(WithAlpha(Accent, 0.22f), PanelBase) : ChipColor;
            numerosValor[i].color = on ? Accent : Soft;
            textosValor[i].color = on ? Accent : Mute;
        }
        pregunta.text = tipo == 2 ? "¿Qué pasó?" : tipo == 1 ? "¿Qué te gustaría que agreguemos o cambiemos?" : "Contanos qué te pareció";
        int largo = campo != null ? campo.text.Length : 0;
        if (contador != null) contador.text = $"{largo} / {Maximo}";
        enviar?.Habilitar(campo != null && campo.text.Trim().Length > 0);
    }

    private void Enviar()
    {
        if (campo == null || campo.text.Trim().Length == 0) return;
        PerfilDatos.GuardarFeedback(Tipos[tipo], valoracion, campo.text.Trim());
        menu.CerrarModal();
        menu.Avisar("¡Gracias! Tu opinión quedó guardada (simulado)");
    }
}
