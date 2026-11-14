using TMPro;
using UnityEngine;
using static ShopUIKit;
using static PerfilUI;
using Cosmetico = PerfilDatos.Cosmetico;
using Img = UnityEngine.UI.Image;
using Rareza = PerfilDatos.Rareza;
using Tipo = PerfilDatos.Tipo;

// F36 y F32: lo que se compra en la tienda directa (US 386) o se gana en el pase de batalla (US 363): un cosmético del
// Perfil (avatar, banner, marco o título), una skin de arma (F35) o cristales. Sabe si el jugador ya lo tiene, lo da y
// lo dibuja en una carta.
public sealed class Premio
{
    public Cosmetico cosmetico;
    public CatalogoDeSkins.Skin skin;
    public int cristales; // solo cristales (sin cosmético)
    public int creditos;  // solo créditos (pase de batalla)

    public static Premio De(Cosmetico c) => c != null ? new Premio { cosmetico = c } : null;
    public static Premio De(CatalogoDeSkins.Skin s) => s != null ? new Premio { skin = s } : null;

    /// <summary>"av:id", "bn:id", "mc:id", "ti:id" (cosméticos del Perfil) o "sk:id" (skins).</summary>
    public string Clave => skin != null ? "sk:" + skin.id : cosmetico != null ? cosmetico.Clave : creditos > 0 ? "cd:" + creditos : "cr:" + cristales;

    public static Premio DesdeClave(string clave)
    {
        if (string.IsNullOrEmpty(clave)) return null;
        if (clave.StartsWith("sk:")) return De(CatalogoDeSkins.De(clave.Substring(3)));
        if (clave.StartsWith("cr:")) return int.TryParse(clave.Substring(3), out int n) ? new Premio { cristales = n } : null;
        if (clave.StartsWith("cd:")) return int.TryParse(clave.Substring(3), out int m) ? new Premio { creditos = m } : null;
        return De(PerfilDatos.DesdeClave(clave));
    }

    public Rareza Rareza => skin != null ? skin.rareza : cosmetico != null ? cosmetico.rareza : Rareza.Comun;
    public bool EsMoneda => skin == null && cosmetico == null;

    public string Nombre => skin != null ? skin.Texto : cosmetico != null ? L10n.Source(cosmetico.nombre, "profile.")
        : creditos > 0 ? PerfilDatos.Numero(creditos) + " " + L10n.Source("créditos", "profile.")
        : PerfilDatos.Numero(cristales) + " " + L10n.Source("cristales", "profile.");

    public string NombreTipo => skin != null ? L10n.Source("Skin de arma", "profile.") : cosmetico != null
        ? L10n.Source(cosmetico.tipo == Tipo.Banner ? "Tarjeta de presentación" : PerfilUI.NombreTipo(cosmetico.tipo), "profile.")
        : L10n.Source(creditos > 0 ? "Créditos" : "Cristales", "profile.");

    public bool Tiene => skin != null ? SkinsDeArmas.Tiene(skin) : cosmetico != null && PerfilDatos.Tiene(cosmetico);

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
        bool nuevo = skin != null ? SkinsDeArmas.Desbloquear(skin.id) : PerfilDatos.Dar(cosmetico);
        return nuevo ? 0 : PerfilDatos.CreditosPorRepetir(Rareza);
    }

    // ---------- Dibujo ----------

    /// <summary>
    /// El arte del premio dentro del recuadro (x, y, w, h) de su padre: la imagen del avatar o del banner, el marco sobre el
    /// banner equipado, el título escrito, los colores de la skin o el cristal.
    /// </summary>
    public void DibujarArte(RectTransform padre, float x, float y, float w, float h, bool apagado = false)
    {
        RectTransform caja = Place(Node("Arte", padre), x, y, w, h);
        caja.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        Image(Stretch(Node("Fondo", caja)), null, Hex(0x0B0E14));
        Color colorR = ColorRareza(Rareza);
        if (skin != null)
        {
            // Los cuatro colores de la skin, como en la colección del Perfil.
            Color[] colores = CatalogoDeSkins.ColoresDeBarra(skin);
            float[] partes = { 0.5f, 0.3f, 0.12f, 0.08f };
            float bh = Mathf.Min(48f, h * 0.36f), bx = 14f, bw = w - 28f;
            for (int i = 0; i < 4; i++)
            {
                Image(Place(Node("Color" + i, caja), bx, (h - bh) / 2f, bw * partes[i], bh), null, colores[i]);
                bx += bw * partes[i];
            }
            T(caja, "Arma", 14f, (h + bh) / 2f + 6f, w - 28f, 24f, FRotulo, 16f, Soft, skin.arma.nombre, TextAlignmentOptions.MidlineLeft, 10f, true);
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
