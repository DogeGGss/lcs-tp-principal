using System.Collections.Generic;
using Cosmetico = PerfilDatos.Cosmetico;
using Rareza = PerfilDatos.Rareza;
using Tipo = PerfilDatos.Tipo;

// US 392 (F36): de dónde sale cada cosmético. Cada uno sale de un solo lugar: las cápsulas, el pase de batalla o la tienda
// directa (los iniciales los tienen todos de entrada).
// - CA2: los avatares, banners y marcos van por conjunto (el mismo estilo va junto); los títulos, las skins y los emotes,
//   uno por uno. Las skins se reparten por diseño (o combinación de colores), salvo las 8 del pase, que son de un arma.
// - CA4 y CA5: el pase, nivel por nivel (Pase): 35 cosméticos y 5 niveles con cristales (550 en total), sin créditos.
// - CA9: dónde se consigue, para el Perfil (DondeSeConsigue).
public static class OrigenDeCosmeticos
{
    public enum Origen { Inicial, Capsulas, Pase, Tienda }

    // ---------- Conjuntos (por el id del banner y del marco) ----------

    private static readonly Dictionary<string, Origen> Conjuntos = new Dictionary<string, Origen>
    {
        { "umbral-celeste", Origen.Capsulas }, { "forja-ambar", Origen.Capsulas }, { "pulso-carmesi", Origen.Capsulas },
        { "pesado-realista", Origen.Capsulas }, { "guerrero-synthwave", Origen.Capsulas }, { "sigiloso-manga", Origen.Capsulas },
        { "sigiloso-8bit", Origen.Capsulas }, { "pesado-tinta-china", Origen.Capsulas },
        { "astra-pop", Origen.Pase }, { "guerrero-fauve", Origen.Pase }, { "sigiloso-tinta", Origen.Pase }, { "astra-acuarela", Origen.Pase },
        { "velo-esmeralda", Origen.Tienda }, { "astra-nouveau", Origen.Tienda }, { "pesado-cubista", Origen.Tienda },
        { "guerrero-puntillismo", Origen.Tienda }, { "ultima-oleada", Origen.Tienda }, { "nucleo-del-rift", Origen.Tienda },
    };

    // Los avatares cuyo banner (y marco) del conjunto tiene otro id.
    private static readonly Dictionary<string, string> BannerDelAvatar = new Dictionary<string, string>
    {
        { "astra-celeste", "umbral-celeste" }, { "pesado-ambar", "forja-ambar" }, { "guerrero-carmesi", "pulso-carmesi" },
        { "sigiloso-esmeralda", "velo-esmeralda" }, { "no-muerto", "ultima-oleada" }, { "corazon-del-rift", "nucleo-del-rift" },
    };

    // ---------- Títulos (por el nombre; los demás, de las cápsulas) ----------

    private static readonly HashSet<string> TitulosDelPase = new HashSet<string>
    {
        "Último vagón", "Vía muerta", "Fuera de horario", "Expreso al infierno", "Pasajero fantasma", "Destino desconocido",
        "Servicio interrumpido", "Perdí el tren", "Sin retorno", "Maquinista del apocalipsis", "Cambio de vía",
    };

    private static readonly HashSet<string> TitulosDeLaTienda = new HashSet<string>
    {
        "ACE", "El del clutch", "Grieta viviente", "Último pasajero", "Exterminador de zombies", "Jefe de estación",
        "Claramente era hacker", "Era lag", "Sobreviví de pedo", "Mi mouse no anda", "MVP del equipo contrario",
        "Aprobado por lástima", "Esto no entraba en el parcial", "Vine por la cursada",
    };

    // ---------- Skins (por diseño o combinación; las del pase, de un arma) ----------

    private static readonly Dictionary<string, Origen> Estilos = new Dictionary<string, Origen>
    {
        { "rift", Origen.Capsulas }, { "brasa", Origen.Capsulas }, { "artico", Origen.Capsulas },
        { "albiceleste", Origen.Tienda }, { "desierto", Origen.Tienda },
        { "marmol-de-retiro", Origen.Capsulas }, { "cristal-del-rift", Origen.Capsulas }, { "azulejos-del-subte", Origen.Tienda },
        { "codigo-binario", Origen.Capsulas }, { "proxima-estacion", Origen.Capsulas }, { "mapa-del-subte", Origen.Tienda },
        { "dragon-abisal", Origen.Capsulas }, { "tormenta", Origen.Tienda },
    };

    // ---------- Emotes (por su estado) ----------

    private static readonly Dictionary<string, Origen> Emotes = new Dictionary<string, Origen>
    {
        { "Silly", Origen.Capsulas }, { "Rumba", Origen.Capsulas }, { "HipHop3", Origen.Capsulas }, { "Shuffle", Origen.Capsulas },
        { "Flair", Origen.Capsulas },
        { "Dancing", Origen.Pase }, { "HipHop2", Origen.Pase }, { "Floss", Origen.Pase }, { "Twerk", Origen.Pase },
        { "Twist", Origen.Tienda }, { "HipHop", Origen.Tienda }, { "PunchingBag", Origen.Tienda }, { "NorthernSoul", Origen.Tienda },
        { "DefaultDance", Origen.Tienda },
    };

    // ---------- El pase (CA4 y CA5) ----------

    /// <summary>Lo que da cada nivel del pase (1 a 40): una clave de Premio ("cr:100" cristales; "av:", "bn:", "mc:", "sk:" y
    /// "em:" un cosmético) o, sin los dos puntos, un título por su nombre.</summary>
    private static readonly string[] Niveles =
    {
        "Último vagón", "Vía muerta", "sk:belgrano-sur-albiceleste", "av:astra-pop", "cr:100",
        "bn:astra-pop", "em:Dancing", "mc:astra-pop", "Fuera de horario", "sk:la-portena-azulejos-del-subte",
        "cr:100", "Expreso al infierno", "av:guerrero-fauve", "em:HipHop2", "cr:100",
        "bn:guerrero-fauve", "sk:la-trochita-marmol-de-retiro", "mc:guerrero-fauve", "Pasajero fantasma", "sk:ultimo-tren-codigo-binario",
        "av:sigiloso-tinta", "sk:urquiza-mapa-del-subte", "bn:sigiloso-tinta", "mc:sigiloso-tinta", "cr:100",
        "Destino desconocido", "Servicio interrumpido", "em:Floss", "Perdí el tren", "sk:roca-proxima-estacion",
        "av:astra-acuarela", "bn:astra-acuarela", "Sin retorno", "mc:astra-acuarela", "cr:150",
        "Maquinista del apocalipsis", "sk:ultimo-tren-dragon-abisal", "Cambio de vía", "em:Twerk", "sk:mitre-tormenta",
    };

    private static Premio[] pase;
    private static Dictionary<string, int> nivelDe;

    /// <summary>Los 40 premios del pase, en orden (el del nivel n es Pase[n - 1]).</summary>
    public static IReadOnlyList<Premio> Pase
    {
        get
        {
            if (pase != null) return pase;
            pase = new Premio[Niveles.Length];
            nivelDe = new Dictionary<string, int>();
            for (int i = 0; i < Niveles.Length; i++)
            {
                string clave = Niveles[i];
                if (clave.IndexOf(':') < 0) // un título, por su nombre
                    pase[i] = Premio.De(PerfilDatos.Catalogo.Find(c => c.tipo == Tipo.Titulo && c.nombre == clave));
                else pase[i] = Premio.DesdeClave(clave);
                if (pase[i] != null && !pase[i].EsMoneda) nivelDe[pase[i].Clave] = i + 1;
            }
            return pase;
        }
    }

    /// <summary>En qué nivel del pase está ese premio (por su clave), o 0 si no está en el pase.</summary>
    public static int NivelDelPase(string clave)
    {
        if (Pase == null || string.IsNullOrEmpty(clave)) return 0;
        return nivelDe.TryGetValue(clave, out int n) ? n : 0;
    }

    // ---------- De dónde sale ----------

    public static Origen De(Cosmetico c)
    {
        if (c == null || c.rareza == Rareza.Inicial) return Origen.Inicial;
        if (c.tipo == Tipo.Titulo)
            return TitulosDelPase.Contains(c.nombre) ? Origen.Pase : TitulosDeLaTienda.Contains(c.nombre) ? Origen.Tienda : Origen.Capsulas;
        string conjunto = c.tipo == Tipo.Avatar && BannerDelAvatar.TryGetValue(c.id, out string b) ? b : c.id;
        return Conjuntos.TryGetValue(conjunto, out Origen o) ? o : Origen.Capsulas;
    }

    public static Origen De(CatalogoDeSkins.Skin s)
    {
        if (s == null) return Origen.Inicial;
        if (NivelDelPase(PerfilDatos.ClaveSkin + s.id) > 0) return Origen.Pase;
        string estilo = s.colores != null ? s.colores.id : s.diseno != null ? s.diseno.id : "";
        return Estilos.TryGetValue(estilo, out Origen o) ? o : Origen.Capsulas;
    }

    public static Origen De(CatalogoDeEmotes.Emote e)
    {
        if (e == null || e.rareza == Rareza.Inicial) return Origen.Inicial;
        return Emotes.TryGetValue(e.estado, out Origen o) ? o : Origen.Capsulas;
    }

    public static Origen De(Premio p) =>
        p == null || p.EsMoneda ? Origen.Inicial : p.skin != null ? De(p.skin) : p.emote != null ? De(p.emote) : De(p.cosmetico);

    /// <summary>CA9: la pestaña de la tienda donde se consigue (las cápsulas, el pase o la tienda directa).</summary>
    public static PestanaTienda PestanaDe(Premio p)
    {
        Origen o = De(p);
        return o == Origen.Pase ? PestanaTienda.Pase : o == Origen.Tienda ? PestanaTienda.Tienda : PestanaTienda.Capsulas;
    }

    /// <summary>CA9: "Se consigue en las cápsulas", "Se consigue en la tienda" o "Se consigue en el pase (nivel N)".</summary>
    public static string DondeSeConsigue(Premio p)
    {
        switch (De(p))
        {
            case Origen.Capsulas: return L10n.T("profile.origin.capsules");
            case Origen.Tienda: return L10n.T("profile.origin.store");
            case Origen.Pase: return L10n.T("profile.origin.pass_fmt", NivelDelPase(p.Clave));
            default: return "";
        }
    }
}
