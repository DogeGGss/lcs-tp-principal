using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static ShopUIKit;
using Img = UnityEngine.UI.Image;

// Selección de personaje antes de la partida (US 015).
// - CA1: aparece antes de entrar a la partida. Hoy la abre el Zombie después de elegir la dificultad; para el Táctico
//   y el Deathmatch se llama desde la sala cuando exista el multijugador (F06).
// - CA2: cada tarjeta muestra retrato, nombre, rol, descripción y la habilidad (nombre, tecla y recarga).
// - CA3: se elige con clic o con 1 a N y las flechas; se confirma con Enter, con "Confirmar" o con un segundo clic.
// - CA4: con tiempo límite (multijugador), al terminarse se asigna el último personaje usado, o uno al azar.
// - CA5: el último elegido aparece preseleccionado.
// - CA6: lo aplica PlayerAbility al empezar la partida (CharacterRoster.Selected).
// US 016 (Táctico): con LockedBy, los personajes que ya eligió un compañero aparecen bloqueados ("Elegido por …");
// con ConfirmHandler, confirmar no cierra enseguida: espera a que el servidor diga si se lo quedó (Accept) o si
// otro llegó primero (Reject, "Ya lo eligió …").
// Se arma por código con las medidas de una pantalla de 1920 x 1080, como la tienda y la selección de modo.
public class CharacterSelectScreen : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    /// <summary>Frame en que se cerró: así el Esc que la cierra no lo toma también la pantalla de atrás.</summary>
    public static int ClosedFrame { get; private set; } = -1;

    private const float CardW = 340f, CardH = 560f, Gap = 24f, CardTop = 250f, PortraitH = 290f;

    private class Card
    {
        public CharacterData data;
        public RectTransform rect;
        public Img frame, body;
        public bool hovered;
        public GameObject lockLayer;      // US 016
        public TextMeshProUGUI lockText;
        public string lockedBy;
    }

    /// <summary>US 016: nombre del compañero que ya eligió ese personaje, o null si está libre.</summary>
    public System.Func<CharacterData, string> LockedBy;

    /// <summary>US 016: si está, confirmar le pasa el personaje y espera Accept() o Reject().</summary>
    public System.Action<CharacterData> ConfirmHandler;

    private bool waiting, timedOut;
    private TextMeshProUGUI warnText;
    private float warnUntil;

    private readonly List<Card> cards = new List<Card>();
    private TMP_FontAsset displayFont, labelFont, bodyFont;
    private Sprite rounded;
    private System.Action onConfirm, onCancel;
    private float timeLimit, openedAt;
    private int selected = -1, lastUsed = -1;
    private TextMeshProUGUI timerText, hintText;
    private Img confirmBg;

    /// <summary>
    /// Abre la selección. eyebrow: texto chico arriba del título (por ejemplo "Modo Zombie · Normal").
    /// timeLimit: segundos para elegir (0 = sin límite). onCancel: null si no se puede volver.
    /// </summary>
    public static CharacterSelectScreen Show(TMP_FontAsset display, TMP_FontAsset label, TMP_FontAsset body, Sprite roundedSprite,
        string eyebrow, float timeLimit, System.Action onConfirm, System.Action onCancel)
    {
        if (CharacterRoster.All.Count == 0)
        {
            Debug.LogWarning("No hay personajes en Resources/Personajes: se sigue sin elegir.");
            onConfirm?.Invoke();
            return null;
        }

        GameObject go = new GameObject("SeleccionPersonaje", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        go.layer = 5;
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 130; // por encima del menú, del nombre de jugador y de la nota para el PO
        var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;

        CharacterSelectScreen screen = go.AddComponent<CharacterSelectScreen>();
        screen.displayFont = display;
        screen.labelFont = label;
        screen.bodyFont = body;
        screen.rounded = roundedSprite;
        screen.onConfirm = onConfirm;
        screen.onCancel = onCancel;
        screen.timeLimit = timeLimit;
        screen.Build((RectTransform)go.transform, eyebrow);
        return screen;
    }

    // =====================================================================
    // Armado
    // =====================================================================

    private void Build(RectTransform canvasRoot, string eyebrow)
    {
        IsOpen = true;
        openedAt = Time.unscaledTime;
        lastUsed = CharacterRoster.SelectedIndex;
        selected = lastUsed; // CA5

        Image(Stretch(Node("Oscurecido", canvasRoot)), null, new Color(0f, 0f, 0f, DarkAlpha(0.9f)), 0f, true);

        RectTransform root = Node("Pantalla", canvasRoot);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(1920f, 1080f);

        // Encabezado
        Text(Place(Node("Contexto", root), 96f, 84f, 900f, 24f), labelFont, 20f, Accent, TextAlignmentOptions.MidlineLeft, 16f, true).text = eyebrow;
        Text(Place(Node("Titulo", root), 96f, 110f, 1200f, 80f), displayFont, 76f, Ink, TextAlignmentOptions.MidlineLeft, 2f, true).text = "Elegí tu personaje";
        Image(Place(Node("Linea", root), 96f, 196f, 160f, 4f), null, Accent);
        timerText = Text(Place(Node("Tiempo", root), 1920f - 96f - 300f, 110f, 300f, 80f), displayFont, 64f, Ink, TextAlignmentOptions.MidlineRight, 2f);
        timerText.gameObject.SetActive(timeLimit > 0f);

        // Tarjetas centradas
        int n = CharacterRoster.All.Count;
        float total = n * CardW + (n - 1) * Gap;
        float x0 = (1920f - total) / 2f;
        for (int i = 0; i < n; i++) cards.Add(BuildCard(root, CharacterRoster.All[i], i, x0 + i * (CardW + Gap)));

        // Pie
        float footY = 1080f - 96f - 56f;
        hintText = Text(Place(Node("Ayuda", root), 96f, footY, 1000f, 56f), labelFont, 18f, Mute, TextAlignmentOptions.MidlineLeft, 10f, true);
        hintText.richText = true;
        string keys = n > 1 ? $"1–{n}" : "1";
        hintText.text = $"<color=#F3F4F6>{keys}</color>  Elegir     <color=#F3F4F6>← →</color>  Mover     <color=#F3F4F6>Enter</color>  Confirmar" +
                        (onCancel != null ? "     <color=#F3F4F6>Esc</color>  Volver" : "");

        warnText = Text(Place(Node("Aviso", root), 0f, CardTop + CardH + 40f, 1920f, 34f), labelFont, 24f, Bad, TextAlignmentOptions.Center, 8f, true);
        warnText.gameObject.SetActive(false);

        float bx = 1920f - 96f - 240f;
        confirmBg = Button(root, bx, footY, 240f, 56f, "Confirmar", true, Confirm);
        if (onCancel != null) Button(root, bx - 16f - 170f, footY, 170f, 56f, "Volver", false, Cancel);

        Refresh();
    }

    private Card BuildCard(RectTransform root, CharacterData data, int index, float x)
    {
        Card card = new Card { data = data };
        card.rect = Place(Node(data.name, root), x, CardTop, CardW, CardH);
        card.frame = Image(card.rect, rounded, White(0.1f), 12f, true);

        RectTransform bodyRect = Place(Node("Cuerpo", card.rect), 3f, 3f, CardW - 6f, CardH - 6f);
        card.body = Image(bodyRect, rounded, Rgb(12, 14, 19), 10f, true);
        float w = CardW - 6f;

        // Retrato (o la inicial sobre el color del personaje)
        RectTransform portrait = Place(Node("Retrato", bodyRect), 0f, 0f, w, PortraitH);
        Image(portrait, rounded, Over(WithAlpha(data.color, 0.22f), Rgb(12, 14, 19)), 10f);
        if (data.portrait != null)
        {
            Img pic = Image(Stretch(Node("Imagen", portrait)), data.portrait, Color.white);
            pic.preserveAspect = true;
        }
        else
        {
            string initial = string.IsNullOrEmpty(data.displayName) ? "?" : data.displayName.Substring(0, 1);
            Text(Stretch(Node("Inicial", portrait)), displayFont, 220f, WithAlpha(data.color, 0.55f), TextAlignmentOptions.Center, 0f, true).text = initial;
        }
        Image(Place(Node("Franja", bodyRect), 0f, PortraitH, w, 4f), null, data.color);

        // Número para elegir con el teclado
        RectTransform chip = Place(Node("Tecla", bodyRect), 16f, 16f, 40f, 40f);
        Image(chip, rounded, new Color(0f, 0f, 0f, DarkAlpha(0.6f)), 6f);
        Text(Stretch(Node("Texto", chip)), displayFont, 22f, Ink, TextAlignmentOptions.Center).text = (index + 1).ToString();

        if (data.provisional)
        {
            RectTransform tag = Place(Node("Provisorio", bodyRect), w - 16f - 110f, 16f, 110f, 28f);
            Image(tag, rounded, new Color(0f, 0f, 0f, DarkAlpha(0.6f)), 4f);
            Text(Stretch(Node("Texto", tag)), labelFont, 13f, Mute, TextAlignmentOptions.Center, 10f, true).text = "Provisorio";
        }
        if (index == lastUsed)
        {
            RectTransform tag = Place(Node("Ultimo", bodyRect), 16f, PortraitH - 16f - 28f, 140f, 28f);
            Image(tag, rounded, new Color(0f, 0f, 0f, DarkAlpha(0.6f)), 4f);
            Text(Stretch(Node("Texto", tag)), labelFont, 13f, Soft, TextAlignmentOptions.Center, 10f, true).text = "Último elegido";
        }

        // Nombre y rol
        float y = PortraitH + 20f;
        Text(Place(Node("Nombre", bodyRect), 22f, y, w - 44f, 46f), displayFont, 42f, Ink, TextAlignmentOptions.MidlineLeft, 3f, true).text = data.displayName;
        y += 46f;
        Text(Place(Node("Rol", bodyRect), 22f, y, w - 44f, 22f), labelFont, 16f, data.color, TextAlignmentOptions.MidlineLeft, 14f, true).text = data.role;
        y += 34f;

        // Habilidad
        AbilityData ability = data.ability;
        RectTransform icon = Place(Node("Icono", bodyRect), 22f, y, 48f, 48f);
        Image(icon, rounded, Over(WithAlpha(data.color, 0.25f), Rgb(12, 14, 19)), 6f);
        if (ability != null && ability.icon != null)
        {
            Img pic = Image(Place(Node("Imagen", icon), 8f, 8f, 32f, 32f), ability.icon, Ink);
            pic.preserveAspect = true;
        }
        Text(Place(Node("Habilidad", bodyRect), 84f, y - 2f, w - 106f, 28f), labelFont, 21f, Ink, TextAlignmentOptions.MidlineLeft, 4f, true).text =
            ability != null ? ability.displayName : "Sin habilidad";
        Text(Place(Node("Recarga", bodyRect), 84f, y + 26f, w - 106f, 22f), labelFont, 15f, Mute, TextAlignmentOptions.MidlineLeft, 8f, true).text =
            ability != null ? $"{KeyBindings.Label(GameAction.Habilidad)} · Recarga {Number(ability.cooldown)} s" : "";
        y += 64f;

        // Descripción (del personaje; si no tiene, la de la habilidad)
        string text = !string.IsNullOrEmpty(data.description) ? data.description : ability != null ? ability.description : "";
        TextMeshProUGUI description = Text(Place(Node("Descripcion", bodyRect), 22f, y, w - 44f, CardH - 6f - y - 18f), bodyFont, 17f, Soft, TextAlignmentOptions.TopLeft);
        description.textWrappingMode = TextWrappingModes.Normal;
        description.overflowMode = TextOverflowModes.Ellipsis;
        description.text = text;

        // US 016: capa de "Elegido por …" (se prende cuando un compañero lo elige).
        RectTransform lockRect = Stretch(Node("Bloqueado", card.rect));
        Image(lockRect, rounded, new Color(0f, 0f, 0f, DarkAlpha(0.72f)), 12f, true);
        RectTransform lockTag = Place(Node("Etiqueta", lockRect), 20f, CardH / 2f - 30f, CardW - 40f, 60f);
        Image(lockTag, rounded, Rgb(10, 12, 17, DarkAlpha(0.92f)), 6f);
        card.lockText = Text(Stretch(Node("Texto", lockTag)), labelFont, 19f, Ink, TextAlignmentOptions.Center, 8f, true);
        card.lockLayer = lockRect.gameObject;
        card.lockLayer.SetActive(false);

        ShopPointerTarget pointer = card.rect.gameObject.AddComponent<ShopPointerTarget>();
        pointer.Hovered = () => { card.hovered = true; Refresh(); };
        pointer.Exited = () => { card.hovered = false; Refresh(); };
        pointer.Clicked = () => { if (selected == index) Confirm(); else Select(index); };
        return card;
    }

    private Img Button(RectTransform parent, float x, float y, float w, float h, string label, bool primary, System.Action onClick)
    {
        RectTransform rect = Place(Node(label, parent), x, y, w, h);
        Color normal = primary ? Accent : ChipColor;
        Img bg = Image(rect, rounded, normal, 6f, true);
        Text(Stretch(Node("Texto", rect)), displayFont, 24f, primary ? KeyInk : Ink, TextAlignmentOptions.Center, 10f, true).text = label;
        ShopPointerTarget pointer = rect.gameObject.AddComponent<ShopPointerTarget>();
        pointer.Hovered = () => { if (!primary || selected >= 0) bg.color = primary ? Hot : ChipDim; };
        pointer.Exited = () => Refresh();
        pointer.Clicked = onClick;
        return bg;
    }

    // =====================================================================
    // Uso
    // =====================================================================

    private void Update()
    {
        if (timeLimit > 0f)
        {
            float left = Mathf.Max(0f, timeLimit - (Time.unscaledTime - openedAt));
            int s = Mathf.CeilToInt(left);
            timerText.text = $"{s / 60}:{s % 60:00}";
            timerText.color = left <= 5f ? Bad : Ink;
            if (left <= 0f) { if (!timedOut) TimeUp(); return; }
        }
        UpdateLocks();
        if (warnText.gameObject.activeSelf && Time.unscaledTime >= warnUntil) warnText.gameObject.SetActive(false);
        if (waiting) return;

        if (Input.GetKeyDown(KeyCode.Escape) && onCancel != null) { Cancel(); return; }
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { Confirm(); return; }
        if (Input.GetKeyDown(KeyCode.RightArrow)) Select(selected < 0 ? 0 : Mathf.Min(cards.Count - 1, selected + 1));
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) Select(selected < 0 ? cards.Count - 1 : Mathf.Max(0, selected - 1));
        for (int i = 0; i < cards.Count && i < 9; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) Select(i);
    }

    private void Select(int index)
    {
        if (waiting || IsLocked(index)) return;
        selected = index;
        Refresh();
    }

    private bool IsLocked(int index) => index >= 0 && index < cards.Count && cards[index].lockedBy != null;

    // US 016: prende o apaga el "Elegido por …" de cada tarjeta según lo que diga LockedBy.
    private void UpdateLocks()
    {
        if (LockedBy == null) return;
        bool changed = false;
        foreach (Card c in cards)
        {
            string who = LockedBy(c.data);
            if (who == c.lockedBy) continue;
            c.lockedBy = who;
            c.lockLayer.SetActive(who != null);
            c.lockText.text = who != null ? $"Elegido por {who}" : "";
            changed = true;
        }
        if (!changed) return;
        if (!waiting && IsLocked(selected)) selected = -1;
        Refresh();
    }

    /// <summary>US 016: el servidor confirmó que el personaje es de este jugador.</summary>
    public void Accept()
    {
        if (!waiting || selected < 0) return;
        waiting = false;
        CharacterRoster.Selected = cards[selected].data;
        Close();
        onConfirm?.Invoke();
    }

    /// <summary>US 016: otro compañero lo eligió primero (CA3). Si ya se terminó el tiempo, se elige otro solo.</summary>
    public void Reject(string message)
    {
        waiting = false;
        selected = -1;
        warnText.text = message;
        warnText.gameObject.SetActive(true);
        warnUntil = Time.unscaledTime + 3f;
        UpdateLocks();
        Refresh();
        if (timedOut) TimeUp();
    }

    private void Refresh()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            Card c = cards[i];
            bool on = i == selected;
            c.frame.color = on ? c.data.color : c.hovered ? White(0.28f) : White(0.1f);
            c.body.color = on ? Over(WithAlpha(c.data.color, 0.08f), Rgb(12, 14, 19)) : Rgb(12, 14, 19);
            c.rect.anchoredPosition = new Vector2(c.rect.anchoredPosition.x, -(CardTop - (on ? 14f : c.hovered ? 6f : 0f)));
        }
        if (confirmBg != null) confirmBg.color = selected >= 0 && !waiting ? Accent : ChipColor;
    }

    private void Confirm()
    {
        if (selected < 0 || waiting || IsLocked(selected)) return; // todavía no eligió ninguno (o está bloqueado)
        if (ConfirmHandler != null)
        {
            // US 016: espera la respuesta del servidor.
            waiting = true;
            Refresh();
            ConfirmHandler(cards[selected].data);
            return;
        }
        CharacterRoster.Selected = cards[selected].data;
        Close();
        onConfirm?.Invoke();
    }

    // CA4: se terminó el tiempo sin confirmar. US 016, CA4: el último usado si está libre, si no uno libre al azar.
    private void TimeUp()
    {
        timedOut = true;
        if (waiting) return;
        if (selected < 0 || IsLocked(selected))
        {
            var free = new List<int>();
            for (int i = 0; i < cards.Count; i++) if (!IsLocked(i)) free.Add(i);
            if (lastUsed >= 0 && !IsLocked(lastUsed)) selected = lastUsed;
            else selected = free.Count > 0 ? free[Random.Range(0, free.Count)] : Random.Range(0, cards.Count);
        }
        if (IsLocked(selected)) { CharacterRoster.Selected = cards[selected].data; Close(); onConfirm?.Invoke(); return; } // todos ocupados
        Confirm();
    }

    /// <summary>US 016: la selección terminó por el reloj del anfitrión; si todavía no eligió, se elige solo.</summary>
    public void ForceTimeUp()
    {
        if (!timedOut) TimeUp();
    }

    private void Cancel()
    {
        Close();
        onCancel?.Invoke();
    }

    private void Close()
    {
        IsOpen = false;
        ClosedFrame = Time.frameCount;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (IsOpen) { IsOpen = false; ClosedFrame = Time.frameCount; }
    }
}
