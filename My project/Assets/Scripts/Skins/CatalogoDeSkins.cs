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

    // "Main (Instance)" → "Main".
    private static string Parte(string material)
    {
        int corte = material.IndexOf(' ');
        return corte > 0 ? material.Substring(0, corte) : material;
    }
}
