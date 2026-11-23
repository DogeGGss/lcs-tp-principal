using TMPro;
using UnityEngine;
using static ShopUIKit;
using static PerfilUI;
using Cosmetico = PerfilDatos.Cosmetico;
using Img = UnityEngine.UI.Image;
using Rareza = PerfilDatos.Rareza;
using Tipo = PerfilDatos.Tipo;

// F36 y F32: lo que se compra en la tienda directa (US 386) o se gana en el pase de batalla (US 363): un cosmético del
// Perfil (avatar, banner, marco o título), una skin de arma (F35), un emote (US 392) o cristales. Sabe si el jugador ya
// lo tiene, lo da y lo dibuja en una carta (US 392, CA7: la skin con el arma girando y el emote con un personaje
// bailando).
public sealed class Premio
{
    public Cosmetico cosmetico;
    public CatalogoDeSkins.Skin skin;
    public CatalogoDeEmotes.Emote emote; // US 392
    public int cristales; // solo cristales (sin cosmético)
    public int creditos;  // solo créditos (pase de batalla)

    public static Premio De(Cosmetico c) => c != null ? new Premio { cosmetico = c } : null;
    public static Premio De(CatalogoDeSkins.Skin s) => s != null ? new Premio { skin = s } : null;
    public static Premio De(CatalogoDeEmotes.Emote e) => e != null ? new Premio { emote = e } : null;

    /// <summary>US 392: el emote con ese estado (el nombre de su animación), o null.</summary>
    public static CatalogoDeEmotes.Emote EmoteDe(string estado) =>
        CatalogoDeEmotes.Actual.emotes.Find(x => x != null && x.estado == estado);

    /// <summary>"av:id", "bn:id", "mc:id", "ti:id" (cosméticos del Perfil), "sk:id" (skins) o "em:estado" (emotes).</summary>
    public string Clave => skin != null ? "sk:" + skin.id : emote != null ? ClaveEmote + emote.estado : cosmetico != null ? cosmetico.Clave
        : creditos > 0 ? "cd:" + creditos : "cr:" + cristales;

    public const string ClaveEmote = "em:";

    public static Premio DesdeClave(string clave)
    {
        if (string.IsNullOrEmpty(clave)) return null;
        if (clave.StartsWith("sk:")) return De(CatalogoDeSkins.De(clave.Substring(3)));
        if (clave.StartsWith(ClaveEmote)) return De(EmoteDe(clave.Substring(ClaveEmote.Length)));
        if (clave.StartsWith("cr:")) return int.TryParse(clave.Substring(3), out int n) ? new Premio { cristales = n } : null;
        if (clave.StartsWith("cd:")) return int.TryParse(clave.Substring(3), out int m) ? new Premio { creditos = m } : null;
        return De(PerfilDatos.DesdeClave(clave));
    }

    public Rareza Rareza => skin != null ? skin.rareza : emote != null ? emote.rareza : cosmetico != null ? cosmetico.rareza : Rareza.Comun;
    public bool EsMoneda => skin == null && cosmetico == null && emote == null;

    public string Nombre => skin != null ? skin.Texto : emote != null ? L10n.Source(emote.nombre, "profile.")
        : cosmetico != null ? L10n.Source(cosmetico.nombre, "profile.")
        : creditos > 0 ? PerfilDatos.Numero(creditos) + " " + L10n.Source("créditos", "profile.")
        : PerfilDatos.Numero(cristales) + " " + L10n.Source("cristales", "profile.");

    public string NombreTipo => skin != null ? L10n.Source("Skin de arma", "profile.") : emote != null ? L10n.T("profile.type.emote") : cosmetico != null
        ? L10n.Source(cosmetico.tipo == Tipo.Banner ? "Tarjeta de presentación" : PerfilUI.NombreTipo(cosmetico.tipo), "profile.")
        : L10n.Source(creditos > 0 ? "Créditos" : "Cristales", "profile.");

    public bool Tiene => skin != null ? SkinsDeArmas.Tiene(skin) : emote != null ? ColeccionDeEmotes.Tiene(emote)
        : cosmetico != null && PerfilDatos.Tiene(cosmetico);

    /// <summary>
    /// Lo suma a la colección (o al saldo, si son cristales). Si ya lo tenía, se cambia por créditos como un repetido de
    /// las cápsulas. Devuelve los créditos de un repetido (0 si era nuevo).
    /// </summary>
    public int Dar(string motivo)
    {
        if (EsMoneda)
        {
            if (creditos > 0) PerfilDatos.SumarCreditos(motivo, creditos); else PerfilDatos.SumarCristales(motivo, cristales);
            return 0;
        }
        bool nuevo = skin != null ? SkinsDeArmas.Desbloquear(skin.id) : emote != null ? ColeccionDeEmotes.Desbloquear(emote.estado)
            : PerfilDatos.Dar(cosmetico);
        return nuevo ? 0 : PerfilDatos.CreditosPorRepetir(Rareza);
    }

    // ---------- Dibujo ----------

    /// <summary>
    /// El arte del premio dentro del recuadro (x, y, w, h) de su padre: la imagen del avatar o del banner, el marco sobre el
    /// banner equipado, el título escrito, el arma con la skin (girando), un personaje haciendo el emote o el cristal.
    /// </summary>
    public void DibujarArte(RectTransform padre, float x, float y, float w, float h, bool apagado = false)
    {
        RectTransform caja = Place(Node("Arte", padre), x, y, w, h);
        caja.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        Image(Stretch(Node("Fondo", caja)), null, Hex(0x0B0E14));
        Color colorR = ColorRareza(Rareza);
        if (skin != null)
        {
            // El arma girando con la skin, como en las cápsulas (VistaPreviaDeSkin), con su nombre abajo.
            const float rotulo = 26f;
            float ah = h - rotulo, aw = Mathf.Min(w - 16f, ah * 1.5f); // la vista es de 3 × 2
            ah = aw / 1.5f;
            var imagen = Node("Arma", caja).gameObject.AddComponent<UnityEngine.UI.RawImage>();
            Place(imagen.rectTransform, (w - aw) / 2f, (h - rotulo - ah) / 2f, aw, ah);
            imagen.raycastTarget = false;
            VistaPreviaDeSkin.Mostrar(imagen, skin);
            T(caja, "Arma", 14f, h - rotulo, w - 28f, 22f, FRotulo, 16f, Soft, skin.arma.nombre, TextAlignmentOptions.MidlineLeft, 10f, true);
        }
        else if (emote != null)
        {
            // US 392, CA7: uno de los cuatro personajes (sorteado al abrir la pantalla) haciendo el baile, con su nombre abajo.
            const float rotulo = 26f;
            float ah = h - rotulo - 8f, aw = Mathf.Min(w - 16f, ah * VistaPreviaDeEmote.Proporcion);
            ah = aw / VistaPreviaDeEmote.Proporcion;
            var imagen = Node("Baile", caja).gameObject.AddComponent<UnityEngine.UI.RawImage>();
            Place(imagen.rectTransform, (w - aw) / 2f, 4f + (h - rotulo - 8f - ah) / 2f, aw, ah);
            imagen.raycastTarget = false;
            VistaPreviaDeEmote.Mostrar(imagen, emote);
            T(caja, "Emote", 14f, h - rotulo, w - 28f, 22f, FRotulo, 16f, Soft, L10n.T("profile.type.emote"), TextAlignmentOptions.MidlineLeft, 10f, true);
        }
        else if (cosmetico != null && cosmetico.tipo == Tipo.Titulo)
        {
            Image(Place(Node("Rareza", caja), 0f, 0f, 6f, h), null, colorR);
            TextMeshProUGUI t = T(caja, "Titulo", 16f, 0f, w - 28f, h, FTitulo, 26f, Ink, cosmetico.nombre, TextAlignmentOptions.Center, 2f, true);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.enableAutoSizing = true; t.fontSizeMin = 14f; t.fontSizeMax = 30f;
        }
        else if (cosmetico != null && cosmetico.tipo == Tipo.Marco)
        {
            float mh = Mathf.Min(h - 16f, (w - 16f) / 3f);
            Vector4 d = InteriorMarco(cosmetico, mh, true);
            RectTransform zona = Place(Node("Banner", caja), 8f + d.x, (h - mh) / 2f + d.y, w - 16f - d.x - d.z, mh - d.y - d.w);
            Cubrir(zona, PerfilDatos.Imagen(PerfilDatos.Banner));
            RectTransform m = null;
            Enmarcar(ref m, caja, 8f, (h - mh) / 2f, w - 16f, mh, cosmetico, true);
        }
        else if (cosmetico != null)
        {
            Cubrir(caja, PerfilDatos.Imagen(cosmetico));
        }
        else
        {
            IconoMoneda(caja, creditos > 0 ? 0 : 2, w / 2f, h / 2f - 12f, Mathf.Min(w, h) * 0.3f);
            T(caja, "Cantidad", 0f, h / 2f + Mathf.Min(w, h) * 0.18f, w, 34f, FTitulo, 30f, creditos > 0 ? ColorCreditos : ColorCristales,
                "+" + PerfilDatos.Numero(creditos > 0 ? creditos : cristales), TextAlignmentOptions.Center);
        }
        if (apagado) Image(Stretch(Node("Velo", caja)), null, new Color(0f, 0f, 0f, DarkAlpha(0.55f)));
    }
}
