using System.Collections.Generic;
using UnityEngine;
using Rareza = PerfilDatos.Rareza;

// F35 (Skins de armas), US 378: las skins que hay en el juego. Cada skin es de un arma ("Mitre · Rift") y tiene una
// rareza, con la misma escala y los mismos colores que los cosméticos del Perfil y las cápsulas (PerfilDatos.Rareza,
// PerfilUI.ColorRareza):
// - Común: solo cambia los colores del arma (la primera tanda: las combinaciones de abajo en todas las armas).
// - Rara: otro material, sin animar. Épica: material animado. Legendaria: además, su fogonazo y su proyectil (US 381).
// Las armas de fuego tienen los mismos cuatro materiales (Main: el acento, Grey: el cuerpo, Black: los detalles y White:
// el metal), así que una combinación sirve para todas. El cuchillo es otro modelo, con textura: lo tiñe el acento.
// El id de cada skin ("mitre-rift") es el que se guarda (SkinsDeArmas) y el que va por la red: no se cambia.
public static class CatalogoDeSkins
{
    public class Arma
    {
        public string id, nombre;
        /// <summary>La ficha de la tienda (Data/Tienda), para saber de qué arma es cada una. Vacío: el cuchillo.</summary>
        public string ficha;
        /// <summary>Se tiñe con el acento (un modelo con textura, sin los cuatro materiales).</summary>
        public bool tenir;
    }

    /// <summary>Los cuatro colores de una skin común.</summary>
    public class Colores
    {
        public string id, nombre;
        public Color main, grey, black, white;
    }

    public class Skin
    {
        public string id, nombre;
        public Arma arma;
        public Rareza rareza;
        /// <summary>Las comunes: sus colores.</summary>
        public Colores colores;

        /// <summary>El nombre que ve el jugador, en su idioma (los nombres de las armas no se traducen).</summary>
        public string Texto => L10n.Source(arma.nombre, "profile.") + " · " + L10n.Source(colores != null ? colores.nombre : nombre, "profile.");
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
        new Arma { id = "cuchillo", nombre = "Cuchillo", ficha = "", tenir = true },
    };

    // La primera tanda (13/11/2026): cinco combinaciones, comunes, en todas las armas. Main / Grey / Black / White.
    public static readonly List<Colores> Combinaciones = new List<Colores>
    {
        C("rift", "Rift", 0x8B3DFF, 0x1E1630, 0x0B0812, 0xB9A7E8),
        C("albiceleste", "Albiceleste", 0x74ACDF, 0xE8EEF4, 0x1C2633, 0xF6B40E),
        C("brasa", "Brasa", 0xD7261E, 0x121212, 0x050505, 0x3A3A3A),
        C("artico", "Ártico", 0x5FD3E8, 0xDDE3E8, 0x2A2F35, 0xA7B0B8),
        C("desierto", "Desierto", 0xC8A26B, 0x6B5A40, 0x2B241A, 0xD9C9A8),
    };

    public static readonly List<Skin> Skins = new List<Skin>();
    private static readonly Dictionary<string, Skin> porId = new Dictionary<string, Skin>();

    static CatalogoDeSkins()
    {
        foreach (Arma a in Armas)
            foreach (Colores c in Combinaciones)
                Sumar(new Skin { id = a.id + "-" + c.id, nombre = a.nombre + " · " + c.nombre, arma = a, rareza = Rareza.Comun, colores = c });
    }

    private static void Sumar(Skin s)
    {
        Skins.Add(s);
        porId[s.id] = s;
    }

    private static Colores C(string id, string nombre, int main, int grey, int black, int white) =>
        new Colores { id = id, nombre = nombre, main = Hex(main), grey = Hex(grey), black = Hex(black), white = Hex(white) };

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
        if (skin.arma.tenir) return skin.colores.main;
        string n = Parte(material);
        return n == Main ? skin.colores.main : n == Grey ? skin.colores.grey : n == Black ? skin.colores.black :
            n == White ? skin.colores.white : (Color?)null;
    }

    // "Main (Instance)" → "Main".
    private static string Parte(string material)
    {
        int corte = material.IndexOf(' ');
        return corte > 0 ? material.Substring(0, corte) : material;
    }
}
