using System.Collections.Generic;
using UnityEngine;
using Rareza = PerfilDatos.Rareza;

// F35 (Skins de armas), US 378: las skins que hay en el juego. Cada skin es de un arma ("Mitre · Rift") y tiene una
// rareza, con la misma escala y los mismos colores que los cosméticos del Perfil y las cápsulas (PerfilDatos.Rareza,
// PerfilUI.ColorRareza):
// - Común: solo cambia los colores del arma (la primera tanda: las combinaciones de abajo en todas las armas).
// - Rara: otro material, sin animar. Épica: material animado. Legendaria: además, su fogonazo y su proyectil (US 381).
// Las armas de fuego tienen los mismos cuatro materiales, así que una combinación sirve para todas. Por lo que tapa cada
// uno (medido en los modelos): Black es el cuerpo (la mitad del arma), White los paneles (un tercio), Grey los detalles y
// Main el acento (unas franjas). El cuchillo (otro modelo, con textura) por ahora no tiene skins.
// Las demás rarezas ponen un material en algunas partes del arma (Diseno): el material está en
// Resources/Skins/ModelosDeArmas, con el id del diseño. Épicas: Código binario (13/11/2026), en todas las armas.
// El id de cada skin ("mitre-rift") es el que se guarda (SkinsDeArmas) y el que va por la red: no se cambia.
public static class CatalogoDeSkins
{
    public class Arma
    {
        public string id, nombre;
        /// <summary>La ficha de la tienda (Data/Tienda), para saber de qué arma es cada una. Vacío: un arma sin ficha (como el cuchillo).</summary>
        public string ficha;
        /// <summary>Se tiñe con el acento (un modelo con textura, sin los cuatro materiales).</summary>
        public bool tenir;
    }

    /// <summary>Los cuatro colores de una skin común.</summary>
    public class Colores
    {
        public string id, nombre;
        /// <summary>Material Black (la mitad del arma), White (un tercio), Grey y Main (unas franjas).</summary>
        public Color cuerpo, paneles, detalles, acento;
    }

    /// <summary>Una skin con material (raras, épicas y legendarias): qué material va en qué partes del arma.</summary>
    public class Diseno
    {
        public string id, nombre;
        public Rareza rareza;
        /// <summary>Los materiales del arma que cambia (por nombre); los demás quedan como vienen.</summary>
        public string[] partes;
        /// <summary>Un color que lo representa, para la barra de colores del perfil.</summary>
        public Color muestra;
        /// <summary>Otras partes, con otro material o un color (por ejemplo, el cuerpo de madera del cobre).</summary>
        public Extra[] extras = new Extra[0];
    }

    /// <summary>Una parte del arma con otro material (el id en ModelosDeArmas) o solo otro color.</summary>
    public class Extra
    {
        public string parte, material;
        public Color color;
        /// <summary>Para la barra del perfil.</summary>
        public Color muestra;
    }

    public class Skin
    {
        public string id, nombre;
        public Arma arma;
        public Rareza rareza;
        /// <summary>Las comunes: sus colores.</summary>
        public Colores colores;
        /// <summary>Las de material: su diseño.</summary>
        public Diseno diseno;

        /// <summary>El nombre de la combinación o del diseño ("Rift", "Código binario").</summary>
        public string Estilo => colores != null ? colores.nombre : diseno != null ? diseno.nombre : nombre;

        /// <summary>El nombre que ve el jugador, en su idioma (los nombres de las armas no se traducen).</summary>
        public string Texto => L10n.Source(arma.nombre, "profile.") + " · " + L10n.Source(Estilo, "profile.");
    }

    public const string Main = "Main", Grey = "Grey", Black = "Black", White = "White";

    public static readonly List<Arma> Armas = new List<Arma>
    {
        new Arma { id = "mitre", nombre = "Mitre", ficha = "Fusil" },
        new Arma { id = "urquiza", nombre = "Urquiza", ficha = "Subfusil" },
        new Arma { id = "belgrano-sur", nombre = "Belgrano Sur", ficha = "FusilRafaga" },
        new Arma { id = "roca", nombre = "Roca", ficha = "Escopeta" },
        new Arma { id = "ultimo-tren", nombre = "Último Tren", ficha = "Francotirador" },
        new Arma { id = "gran-capitan", nombre = "El Gran Capitán", ficha = "GranCapitan" },
        new Arma { id = "la-portena", nombre = "La Porteña", ficha = "Pistola" },
        new Arma { id = "la-trochita", nombre = "La Trochita", ficha = "PistolaPesada" },
        // El cuchillo, por ahora sin skins: teñido con un color no quedó bien (13/11/2026). Con tenir = true lo tiñen los paneles.
    };

    // La primera tanda (13/11/2026): cinco combinaciones, comunes, en todas las armas. Cuerpo / paneles / detalles / acento.
    public static readonly List<Colores> Combinaciones = new List<Colores>
    {
        C("rift", "Rift", 0xB9A7E8, 0x6E3FCF, 0x2A2140, 0xF2ECFF),
        C("albiceleste", "Albiceleste", 0x74ACDF, 0xF2F5F8, 0x2B3A4A, 0xF6B40E),
        C("brasa", "Brasa", 0x1B1B1B, 0x4A4A4A, 0x0A0A0A, 0xE0281F),
        C("artico", "Ártico", 0xDDE3E8, 0x5FD3E8, 0x2A2F35, 0xA7B0B8),
        C("desierto", "Desierto", 0xC8A26B, 0x6B5A40, 0x2B241A, 0xD9C9A8),
    };

    // Raras (13/11/2026): materiales sin animar (Art/Skins/SkinPatron.shader, un material por diseño).
    // Épicas (13/11/2026): el código binario verde que cae, animado (Art/Skins/SkinMatrix.shader), en los paneles y el acento.
    public static readonly List<Diseno> Disenos = new List<Diseno>
    {
        // Cobre con óxido en los paneles, el cuerpo de madera oscura y las franjas marrones.
        new Diseno { id = "cobre-oxidado", nombre = "Cobre oxidado", rareza = Rareza.Raro, partes = new[] { White }, muestra = Hex(0xC27A43),
            extras = new[] { new Extra { parte = Black, material = "madera-oscura", muestra = Hex(0x6B4122) },
                             new Extra { parte = Main, color = Hex(0x6E4527), muestra = Hex(0x6E4527) } } },
        // Mármol blanco en los paneles, el cuerpo de mármol negro con vetas doradas y las franjas de oro (con brillo de metal).
        new Diseno { id = "marmol-de-retiro", nombre = "Mármol de Retiro", rareza = Rareza.Raro, partes = new[] { White }, muestra = Hex(0xEEEDE8),
            extras = new[] { new Extra { parte = Black, material = "marmol-negro", muestra = Hex(0x16171A) },
                             new Extra { parte = Main, material = "oro", muestra = Hex(0xE8B84A) } } },
        // Azulejos celestes y blancos en los paneles, el cuerpo blanco como una pared y las franjas grises.
        new Diseno { id = "azulejos-del-subte", nombre = "Azulejos del subte", rareza = Rareza.Raro, partes = new[] { White }, muestra = Hex(0x7FB6D9),
            extras = new[] { new Extra { parte = Black, color = Hex(0xECEBE6), muestra = Hex(0xECEBE6) },
                             new Extra { parte = Main, color = Hex(0x8A8D92), muestra = Hex(0x8A8D92) } } },
        new Diseno { id = "cristal-del-rift", nombre = "Cristal del Rift", rareza = Rareza.Raro, partes = new[] { White, Main }, muestra = Hex(0x7B3FE4) },
        new Diseno { id = "codigo-binario", nombre = "Código binario", rareza = Rareza.Epico, partes = new[] { White, Main }, muestra = Hex(0x26FF59) },
    };

    /// <summary>Los colores de las armas como vienen: cuerpo, paneles, detalles y acento.</summary>
    public static readonly Color[] Originales = { Hex(0x070707), Hex(0x757575), Hex(0x1A1A1A), Hex(0x9D3607) };

    public static readonly List<Skin> Skins = new List<Skin>();
    private static readonly Dictionary<string, Skin> porId = new Dictionary<string, Skin>();

    static CatalogoDeSkins()
    {
        foreach (Arma a in Armas)
        {
            foreach (Colores c in Combinaciones)
                Sumar(new Skin { id = a.id + "-" + c.id, nombre = a.nombre + " · " + c.nombre, arma = a, rareza = Rareza.Comun, colores = c });
            if (a.tenir) continue; // el cuchillo no tiene los cuatro materiales
            foreach (Diseno d in Disenos)
                Sumar(new Skin { id = a.id + "-" + d.id, nombre = a.nombre + " · " + d.nombre, arma = a, rareza = d.rareza, diseno = d });
        }
    }

    private static void Sumar(Skin s)
    {
        Skins.Add(s);
        porId[s.id] = s;
    }

    private static Colores C(string id, string nombre, int cuerpo, int paneles, int detalles, int acento) =>
        new Colores { id = id, nombre = nombre, cuerpo = Hex(cuerpo), paneles = Hex(paneles), detalles = Hex(detalles), acento = Hex(acento) };

    private static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);

    /// <summary>La skin con ese id, o null (no existe o ya no está en el catálogo).</summary>
    public static Skin De(string id) => id != null && porId.TryGetValue(id, out Skin s) ? s : null;

    public static Arma ArmaDe(string id) => Armas.Find(a => a.id == id);

    /// <summary>El arma de una ficha de la tienda (por el nombre del asset), o null.</summary>
    public static Arma ArmaDeFicha(string ficha) => string.IsNullOrEmpty(ficha) ? null : Armas.Find(a => a.ficha == ficha);

    public static List<Skin> DelArma(string arma) => Skins.FindAll(s => s.arma.id == arma);

    /// <summary>El color que lleva cada material de un arma con esa skin (por el nombre del material), o null si queda
    /// como está.</summary>
    public static Color? ColorPara(Skin skin, string material)
    {
        if (skin?.colores == null || string.IsNullOrEmpty(material)) return null;
        if (skin.arma.tenir) return skin.colores.paneles;
        string n = Parte(material);
        return n == Black ? skin.colores.cuerpo : n == White ? skin.colores.paneles : n == Grey ? skin.colores.detalles :
            n == Main ? skin.colores.acento : (Color?)null;
    }

    /// <summary>Los cuatro colores de la barra del perfil (cuerpo, paneles, detalles y acento): los de la combinación o,
    /// con un diseño, su color en las partes que cambia.</summary>
    public static Color[] ColoresDeBarra(Skin skin)
    {
        if (skin == null) return Originales;
        if (skin.colores != null) return new[] { skin.colores.cuerpo, skin.colores.paneles, skin.colores.detalles, skin.colores.acento };
        string[] orden = { Black, White, Grey, Main };
        var barra = new Color[4];
        for (int i = 0; i < 4; i++)
        {
            Extra extra = skin.diseno != null ? System.Array.Find(skin.diseno.extras, x => x.parte == orden[i]) : null;
            barra[i] = extra != null ? extra.muestra
                : skin.diseno != null && System.Array.IndexOf(skin.diseno.partes, orden[i]) >= 0 ? skin.diseno.muestra : Originales[i];
        }
        return barra;
    }

    /// <summary>Lo que el diseño de la skin le pone a esa otra parte del arma (otro material o un color), o null.</summary>
    public static Extra ExtraPara(Skin skin, string material) =>
        skin?.diseno == null || string.IsNullOrEmpty(material) ? null : System.Array.Find(skin.diseno.extras, x => x.parte == Parte(material));

    /// <summary>Si ese material del arma cambia con el diseño de la skin.</summary>
    public static bool EsParteDelDiseno(Skin skin, string material) =>
        skin?.diseno != null && !string.IsNullOrEmpty(material) && System.Array.IndexOf(skin.diseno.partes, Parte(material)) >= 0;

    // "Main (Instance)" → "Main".
    private static string Parte(string material)
    {
        int corte = material.IndexOf(' ');
        return corte > 0 ? material.Substring(0, corte) : material;
    }
}
