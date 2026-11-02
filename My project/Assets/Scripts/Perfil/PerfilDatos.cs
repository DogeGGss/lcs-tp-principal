using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Datos fijos del menú principal para el Perfil, la Estación del Rift
// (cápsulas) y la tienda de cristales. Todavía no hay historias refinadas: catálogo, rarezas, probabilidades, precios y
// estadísticas son de ejemplo. Se guarda con el progreso del jugador (Guardado, clave "Perfil.Prueba", US 162). Con una
// cuenta todo va también a la nube: los cosméticos, las cápsulas abiertas, el pity, las compras y los últimos premios con
// el resto del progreso (ProgresoNube), y las monedas con su propio saldo en la base (US 272, LogrosOnline.Billetera).
// Las cuentas arrancan de cero (US 237, CA3): sin monedas ni cosméticos, salvo los de entrada.
//
// Las tres monedas simulan el negocio de un juego gratuito; no se cobra nada:
// - Créditos: se ganan jugando (al terminar una partida). Se cambian por boletos.
// - Boletos: abren cápsulas en la Estación del Rift.
// - Cristales: la moneda "de pago". Se "compran" en la tienda con una compra simulada.
public static class PerfilDatos
{
    public enum Tipo { Avatar, Banner, Titulo, Marco }
    public enum Rareza { Inicial, Comun, Raro, Epico, Legendario }

    public class Cosmetico
    {
        public string id, nombre, estilo, personaje;
        public Tipo tipo;
        public Rareza rareza;
        public string Clave => (tipo == Tipo.Avatar ? "av:" : tipo == Tipo.Banner ? "bn:" : tipo == Tipo.Marco ? "mc:" : "ti:") + id;
        public string Recurso => tipo == Tipo.Avatar ? "Perfil/Avatares/" + id : tipo == Tipo.Banner ? "Perfil/Banners/" + id :
            tipo == Tipo.Marco && !string.IsNullOrEmpty(id) ? "Perfil/Marcos/" + id : null;
    }

    public class Personaje
    {
        public string id, nombre, rol, color;
    }

    public class Pack
    {
        public string id, nombre, detalle;
        public int cristales, bonus, boletos, precio; // precio en pesos (simulado)
        public bool unaVez;
    }

    public class Resultado
    {
        public Cosmetico cosmetico;
        public bool nuevo, garantia;
        public int creditos; // por repetido
    }

    // cuando: "2026-11-01T14:03", para ordenar las compras de varias PCs (las viejas solo tienen fecha, "01/11 14:03").
    [Serializable]
    private class Compra { public string pack; public int precio; public string fecha, cuando; }

    /// <summary>US 272: lo que cambia el saldo (negativo, un gasto). Con cuenta, cada uno tiene su id: la base no aplica
    /// dos veces el mismo aunque se reenvíe.</summary>
    [Serializable]
    public class Movimiento { public string id, motivo; public int creditos, boletos, cristales; }

    [Serializable]
    private class Estado
    {
        public int creditos, boletos, cristales; // con cuenta: el saldo de la base más los movimientos que esperan subir
        public List<string> obtenidos = new List<string>();
        public string avatar = "riftwalker", banner = "anden-riftwalker", titulo = "";
        public string marco = "anden-riftwalker"; // el del conjunto inicial: equipado de entrada para que se vea
        public int aperturas, gastado;
        public int sinEpico;          // solo para leer guardados viejos: ahora sale de aperturas - ultimoEpico
        public int ultimoEpico = -1;  // aperturas cuando salió el último épico (sube siempre: se junta con el máximo en la nube)
        public List<Compra> compras = new List<Compra>();
        public List<string> recientes = new List<string>();
        public List<string> nuevos = new List<string>();
        public List<string> packsUsados = new List<string>();
        public List<Movimiento> pendientes = new List<Movimiento>(); // con cuenta: ganado y todavía no subido
        public bool nubeRecibida;    // ya llegó lo de la nube a esta PC
        public bool equipadoElegido; // el jugador equipó algo en esta PC
    }

    public const string Clave = "Perfil.Prueba"; // la misma que en PlayerPrefs: así se migra solo (Guardado)

    // ----- Reglas de ejemplo -----
    public static readonly int[] Probabilidad = { 0, 55, 30, 12, 3 }; // por Rareza (Inicial no sale)
    public const int Garantia = 10;              // épico o superior asegurado cada 10 cápsulas
    public const int CreditosPorBoleto = 600;
    public const int CristalesPorBoleto = 100;
    public const int CristalesPorDiez = 900;
    public static readonly int[] CreditosPorRepetido = { 0, 50, 120, 300, 800 };
    public const int CreditosVictoria = 150, CreditosDerrota = 75;

    public static readonly List<Cosmetico> Catalogo = new List<Cosmetico>();
    public static readonly List<Personaje> Personajes = new List<Personaje>
    {
        new Personaje { id = "astra", nombre = "Soledad", rol = "Duelista", color = "#4CC3FF" },
        new Personaje { id = "pesado", nombre = "Unzué", rol = "Centinela", color = "#F2B544" },
        new Personaje { id = "guerrero", nombre = "Goya", rol = "Iniciador", color = "#FF5C5C" },
        new Personaje { id = "sigiloso", nombre = "Necochea", rol = "Explorador", color = "#3DDC97" },
    };

    public static readonly List<Pack> Packs = new List<Pack>
    {
        new Pack { id = "bienvenida", nombre = "Oferta de bienvenida", detalle = "5 boletos + 200 cristales", cristales = 200, boletos = 5, precio = 1990, unaVez = true },
        new Pack { id = "c80", nombre = "Bolsa de cristales", cristales = 80, precio = 1200 },
        new Pack { id = "c400", nombre = "Cofre de cristales", cristales = 400, bonus = 40, precio = 5500 },
        new Pack { id = "c1000", nombre = "Caja de cristales", cristales = 1000, bonus = 150, precio = 12900 },
        new Pack { id = "c2500", nombre = "Bóveda de cristales", cristales = 2500, bonus = 500, precio = 29900 },
    };

    public static event Action Cambio;

    private static Estado e;

    static PerfilDatos()
    {
        void A(string id, string nombre, string personaje, string estilo, Rareza r) =>
            Catalogo.Add(new Cosmetico { id = id, nombre = nombre, personaje = personaje, estilo = estilo, tipo = Tipo.Avatar, rareza = r });
        void B(string id, string nombre, string personaje, string estilo, Rareza r) =>
            Catalogo.Add(new Cosmetico { id = id, nombre = nombre, personaje = personaje, estilo = estilo, tipo = Tipo.Banner, rareza = r });

        A("riftwalker", "Riftwalker", "", "Ícono", Rareza.Inicial);
        A("astra-celeste", "Soledad · Celeste", "astra", "Original", Rareza.Comun);
        A("pesado-ambar", "Unzué · Ámbar", "pesado", "Original", Rareza.Comun);
        A("guerrero-carmesi", "Goya · Carmesí", "guerrero", "Original", Rareza.Comun);
        A("sigiloso-esmeralda", "Necochea · Esmeralda", "sigiloso", "Original", Rareza.Comun);
        A("pesado-realista", "Guardia de acero", "pesado", "Hiperrealismo", Rareza.Raro);
        A("guerrero-synthwave", "Turno nocturno", "guerrero", "Synthwave", Rareza.Raro);
        A("sigiloso-manga", "Entre sombras", "sigiloso", "Manga", Rareza.Raro);
        A("no-muerto", "No muerto", "", "Modo Invasión", Rareza.Raro);
        A("sigiloso-8bit", "Sombra de 8 bits", "sigiloso", "8 bits", Rareza.Raro);
        A("astra-pop", "Soledad eléctrica", "astra", "Pop art", Rareza.Epico);
        A("guerrero-fauve", "Furia de color", "guerrero", "Fauvismo", Rareza.Epico);
        A("sigiloso-tinta", "Silencio de tinta", "sigiloso", "Tinta", Rareza.Epico);
        A("corazon-del-rift", "Corazón del Rift", "", "Cristal", Rareza.Epico);
        A("astra-acuarela", "Marea celeste", "astra", "Acuarela", Rareza.Epico);
        A("pesado-tinta-china", "Bastión de tinta", "pesado", "Tinta china", Rareza.Epico);
        A("astra-nouveau", "Órbita floral", "astra", "Art nouveau", Rareza.Legendario);
        A("pesado-cubista", "Blindaje fragmentado", "pesado", "Cubismo", Rareza.Legendario);
        A("guerrero-puntillismo", "Pulso de puntos", "guerrero", "Puntillismo", Rareza.Legendario);

        B("anden-riftwalker", "Andén Riftwalker", "", "Ícono", Rareza.Inicial);
        B("umbral-celeste", "Umbral celeste", "astra", "Original", Rareza.Comun);
        B("forja-ambar", "Forja ámbar", "pesado", "Original", Rareza.Comun);
        B("pulso-carmesi", "Pulso carmesí", "guerrero", "Original", Rareza.Comun);
        B("velo-esmeralda", "Velo esmeralda", "sigiloso", "Original", Rareza.Comun);
        B("ultima-oleada", "Última oleada", "", "Modo Invasión", Rareza.Raro);
        B("nucleo-del-rift", "Núcleo del Rift", "", "Cristal", Rareza.Raro);
        B("pesado-realista", "Guardia de acero", "pesado", "Hiperrealismo", Rareza.Raro);
        B("guerrero-synthwave", "Turno nocturno", "guerrero", "Synthwave", Rareza.Raro);
        B("sigiloso-8bit", "Sombra de 8 bits", "sigiloso", "8 bits", Rareza.Raro);
        B("sigiloso-manga", "Entre sombras", "sigiloso", "Manga", Rareza.Epico);
        B("astra-pop", "Soledad eléctrica", "astra", "Pop art", Rareza.Epico);
        B("guerrero-fauve", "Furia de color", "guerrero", "Fauvismo", Rareza.Epico);
        B("sigiloso-tinta", "Silencio de tinta", "sigiloso", "Tinta", Rareza.Epico);
        B("astra-acuarela", "Marea celeste", "astra", "Acuarela", Rareza.Epico);
        B("pesado-tinta-china", "Bastión de tinta", "pesado", "Tinta china", Rareza.Epico);
        B("astra-nouveau", "Órbita floral", "astra", "Art nouveau", Rareza.Legendario);
        B("pesado-cubista", "Blindaje fragmentado", "pesado", "Cubismo", Rareza.Legendario);
        B("guerrero-puntillismo", "Pulso de puntos", "guerrero", "Puntillismo", Rareza.Legendario);

        // Marcos de la cabecera del Perfil (y más adelante de la tabla con Tab): uno por conjunto, con el nombre,
        // el estilo y la rareza de su banner.
        Catalogo.Add(new Cosmetico { id = "", nombre = "Sin marco", tipo = Tipo.Marco, rareza = Rareza.Inicial });
        foreach (Cosmetico b in Catalogo.FindAll(c => c.tipo == Tipo.Banner && RielesMarco.ContainsKey(c.id)))
            Catalogo.Add(new Cosmetico { id = b.id, nombre = b.nombre, personaje = b.personaje, estilo = b.estilo, tipo = Tipo.Marco, rareza = b.rareza });

        Catalogo.Add(new Cosmetico { id = "", nombre = "Sin título", tipo = Tipo.Titulo, rareza = Rareza.Inicial });
        string[] titulos =
        {
            "Último pasajero", "Fumador", "Exterminador de zombies", "ACE", "Viajero del Rift", "Próxima estación: caos", "Sin boleto",
            "Maquinista del apocalipsis", "Primera clase", "Último vagón", "Fuera de horario", "Vía muerta", "Expreso al infierno",
            "Pasajero fantasma", "Destino desconocido", "Cambio de vía", "Sin retorno", "Jefe de estación", "Perdí el tren",
            "Servicio interrumpido", "Una bala alcanza", "Cero munición", "Dedo en el gatillo", "A quemarropa", "Tiro de gracia",
            "El del clutch", "Último en pie", "Sin cobertura", "Fuego cruzado", "Recargando…", "Apunté sin querer", "Una ronda más",
            "Pesadilla de la horda", "No era un rasguño", "Sobreviví de pedo", "Coleccionista de cabezas", "Otra vez los zombies",
            "Hasta la última oleada", "El que quedó atrás", "Todavía respiro", "Entre dimensiones", "Error en la realidad",
            "Del otro lado", "Grieta viviente", "Perdido en el tiempo", "No debí cruzar", "Cazador de cometas",
            "Mala puntería, buena actitud", "Vine por la cursada", "Final pendiente", "Era lag", "Claramente era hacker",
            "Lo tenía calculado", "Disparo de advertencia", "MVP del equipo contrario", "Especialista en reaparecer",
            "Cobertura decorativa", "Estoy calentando", "Mi mouse no anda", "Casi le pego", "Estrategia cuestionable", "Vine a mirar",
            "Me bajé en la estación equivocada", "El tren tampoco me espera", "Puntual para morir", "Boleto de ida al respawn",
            "Alimentando a la horda", "El zombie tenía prioridad", "Aprobado por lástima", "Esto no entraba en el parcial",
        };
        // Rarezas de ejemplo: la mitad comunes, y unos pocos fijos.
        var fijos = new Dictionary<string, Rareza>
        {
            { "ACE", Rareza.Legendario }, { "Último pasajero", Rareza.Epico }, { "Exterminador de zombies", Rareza.Epico },
            { "Jefe de estación", Rareza.Epico }, { "El del clutch", Rareza.Legendario }, { "Grieta viviente", Rareza.Legendario },
            { "Viajero del Rift", Rareza.Raro }, { "Fumador", Rareza.Raro },
        };
        for (int i = 0; i < titulos.Length; i++)
        {
            int k = i % 20;
            Rareza r = fijos.TryGetValue(titulos[i], out Rareza f) ? f : k < 10 ? Rareza.Comun : k < 16 ? Rareza.Raro : k < 19 ? Rareza.Epico : Rareza.Legendario;
            Catalogo.Add(new Cosmetico { id = "t" + (i + 1), nombre = titulos[i], tipo = Tipo.Titulo, rareza = r });
        }
    }

    // ================= Estado =================

    private static Estado E
    {
        get
        {
            if (e != null) return e;
            string json = Guardado.GetString(Clave, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { e = JsonUtility.FromJson<Estado>(json); } catch { e = null; }
            }
            if (e == null) e = new Estado(); // de cero: los cosméticos de entrada y sin monedas (US 237, CA3)
            if (e.ultimoEpico < 0) e.ultimoEpico = Mathf.Max(0, e.aperturas - e.sinEpico);
            return e;
        }
    }

    private static void Guardar()
    {
        Guardado.SetString(Clave, JsonUtility.ToJson(E));
        Guardado.Save();
        Cambio?.Invoke();
    }

    /// <summary>Vuelve todo a como arranca la prueba.</summary>
    public static void Reiniciar()
    {
        Guardado.DeleteKey(Clave);
        e = null;
        Guardar();
    }

    /// <summary>Vuelve a leer lo guardado (lo cambió otro juego abierto en esta PC).</summary>
    public static void Recargar()
    {
        e = null;
        Cambio?.Invoke();
    }

    // ---------- Nube (ProgresoNube, US 162 y US 163) ----------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReiniciarSesion() => e = null; // se vuelve a leer del guardado en cada Play (el editor no recarga el dominio)

    /// <summary>
    /// Si lo equipado y los últimos premios de esta PC ya se pueden subir. En una PC nueva (o recién instalada) primero
    /// se trae lo de la nube: si no, lo de entrada pisaría lo que el jugador tenía equipado en la otra. Lo que equipa a
    /// mano en esta PC también cuenta.
    /// </summary>
    public static bool EquipadoPropio => E.nubeRecibida || E.equipadoElegido;

    /// <summary>Claves de los cosméticos obtenidos ("av:...", "bn:...", "ti:...", "mc:...").</summary>
    public static IEnumerable<string> ObtenidosIds => E.obtenidos;
    public static int UltimoEpico => E.ultimoEpico;
    public static IEnumerable<string> PacksUsados => E.packsUsados;

    /// <summary>Las compras para la nube, cada una en un texto: "2026-11-01T14:03|Bolsa de cristales|1200".</summary>
    public static IEnumerable<string> ComprasNube
    {
        get { foreach (Compra c in E.compras) yield return CuandoDe(c) + "|" + c.pack + "|" + c.precio; }
    }

    private static string CuandoDe(Compra c)
    {
        if (!string.IsNullOrEmpty(c.cuando)) return c.cuando;
        // Las de antes solo tienen día y hora ("01/11 14:03"): se toman del año actual.
        return DateTime.TryParseExact(c.fecha, "dd/MM HH:mm", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out DateTime d) ? d.ToString("yyyy-MM-ddTHH:mm") : "0000-00-00T00:00";
    }

    /// <summary>Lo que volvió de la nube para el Perfil (ProgresoNube.Fusionar).</summary>
    public sealed class Nube
    {
        public readonly List<string> obtenidos = new List<string>(), packs = new List<string>(), compras = new List<string>();
        public readonly Dictionary<string, string> equipados = new Dictionary<string, string>();
        public readonly List<string> recientes = new List<string>();
        public int aperturas, ultimoEpico, gastado;
    }

    /// <summary>Lo equipado de cada tipo: avatar, banner, título y marco.</summary>
    public static IEnumerable<(string tipo, string id)> Equipados
    {
        get
        {
            yield return ("avatar", E.avatar);
            yield return ("banner", E.banner);
            yield return ("titulo", E.titulo);
            yield return ("marco", E.marco);
        }
    }

    /// <summary>
    /// Junta lo que vuelve de la nube. Suma los cosméticos, los packs de una sola vez y las compras que falten, y se
    /// queda con lo más alto de las cápsulas abiertas, el último épico (el pity) y el total gastado. La primera vez en
    /// esta PC, además, equipa lo de la nube (solo lo que el jugador tiene) y trae los últimos premios. Nunca quita nada.
    /// </summary>
    public static void FusionarNube(Nube nube)
    {
        Estado s = E;
        bool cambio = false;
        foreach (string clave in nube.obtenidos)
            if (!string.IsNullOrEmpty(clave) && DesdeClave(clave) != null && !s.obtenidos.Contains(clave)) { s.obtenidos.Add(clave); cambio = true; }
        foreach (string id in nube.packs)
            if (Packs.Exists(p => p.id == id) && !s.packsUsados.Contains(id)) { s.packsUsados.Add(id); cambio = true; }
        if (nube.aperturas > s.aperturas) { s.aperturas = nube.aperturas; cambio = true; }
        if (nube.ultimoEpico > s.ultimoEpico) { s.ultimoEpico = nube.ultimoEpico; cambio = true; }
        if (nube.gastado > s.gastado) { s.gastado = nube.gastado; cambio = true; }

        var mias = new HashSet<string>(ComprasNube);
        bool compras = false;
        foreach (string texto in nube.compras)
        {
            string[] partes = texto?.Split('|');
            if (partes == null || partes.Length != 3 || mias.Contains(texto) || !int.TryParse(partes[2], out int precio)) continue;
            string fecha = DateTime.TryParse(partes[0], System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out DateTime d) ? d.ToString("dd/MM HH:mm") : "";
            s.compras.Add(new Compra { pack = partes[1], precio = precio, fecha = fecha, cuando = partes[0] });
            compras = true;
        }
        if (compras)
        {
            s.compras.Sort((a, b) => string.CompareOrdinal(CuandoDe(b), CuandoDe(a))); // la más nueva primero
            if (s.compras.Count > 100) s.compras.RemoveRange(100, s.compras.Count - 100);
            cambio = true;
        }

        if (!EquipadoPropio)
        {
            cambio |= Equipar(Tipo.Avatar, nube.equipados, "avatar", ref s.avatar);
            cambio |= Equipar(Tipo.Banner, nube.equipados, "banner", ref s.banner);
            cambio |= Equipar(Tipo.Titulo, nube.equipados, "titulo", ref s.titulo);
            cambio |= Equipar(Tipo.Marco, nube.equipados, "marco", ref s.marco);
            var recientes = nube.recientes.FindAll(clave => DesdeClave(clave) != null);
            if (recientes.Count > 0) { s.recientes = recientes; cambio = true; }
        }
        if (!s.nubeRecibida) { s.nubeRecibida = true; cambio = true; }
        if (cambio) Guardar();
    }

    private static bool Equipar(Tipo tipo, IReadOnlyDictionary<string, string> equipados, string nombre, ref string actualId)
    {
        if (!equipados.TryGetValue(nombre, out string id) || id == actualId) return false;
        Cosmetico c = Buscar(tipo, id);
        if (c == null || !Tiene(c)) return false; // US 163: no se equipa lo que no está en el catálogo o no se tiene
        actualId = id;
        return true;
    }

    public static int Creditos => E.creditos;
    public static int Boletos => E.boletos;
    public static int Cristales => E.cristales;
    public static int Aperturas => E.aperturas;
    public static int FaltanParaGarantia => Garantia - SinEpico;
    private static int SinEpico => Mathf.Max(0, E.aperturas - E.ultimoEpico); // cápsulas desde el último épico
    public static int Gastado => E.gastado;
    public static IReadOnlyList<string> Recientes => E.recientes;

    public static IEnumerable<(string pack, int precio, string fecha)> Compras
    {
        get { foreach (Compra c in E.compras) yield return (c.pack, c.precio, c.fecha); }
    }

    public static Cosmetico Buscar(Tipo tipo, string id) => Catalogo.Find(c => c.tipo == tipo && c.id == id);
    public static Cosmetico Avatar => Buscar(Tipo.Avatar, E.avatar) ?? Buscar(Tipo.Avatar, "riftwalker");
    public static Cosmetico Banner => Buscar(Tipo.Banner, E.banner) ?? Buscar(Tipo.Banner, "anden-riftwalker");
    public static Cosmetico Titulo => Buscar(Tipo.Titulo, E.titulo) ?? Buscar(Tipo.Titulo, "");
    public static Cosmetico Marco => Buscar(Tipo.Marco, E.marco) ?? Buscar(Tipo.Marco, "anden-riftwalker");

    /// <summary>Ancho de las imágenes originales de los marcos (3840 × 1280). Las medidas de abajo están en píxeles de esas
    /// imágenes; Unity las importa a 2048 y PerfilUI las escala al tamaño importado.</summary>
    public const float AnchoMarcos = 3840f;

    /// <summary>
    /// Distancia desde cada borde de la imagen del marco hasta el centro de su riel (izquierda, arriba, derecha, abajo),
    /// en píxeles de la imagen original: ese centro se apoya sobre el borde de la cabecera. Medido en la parte opaca de los
    /// tramos lisos de cada imagen (17/10/2026); hacia afuera ninguno sale más de 25 px en 1080p.
    /// </summary>
    public static readonly Dictionary<string, Vector4> RielesMarco = new Dictionary<string, Vector4>
    {
        { "anden-riftwalker", new Vector4(56f, 67f, 55f, 68f) },
        { "umbral-celeste", new Vector4(45f, 90f, 45f, 89f) },
        { "forja-ambar", new Vector4(67f, 95f, 67f, 98f) },
        { "pulso-carmesi", new Vector4(57f, 78f, 58f, 76f) },
        { "velo-esmeralda", new Vector4(64f, 82f, 65f, 81f) },
        { "ultima-oleada", new Vector4(64f, 85f, 63f, 86f) },
        { "nucleo-del-rift", new Vector4(51f, 57f, 50f, 57f) },
        { "pesado-realista", new Vector4(57f, 71f, 58f, 72f) },
        { "guerrero-synthwave", new Vector4(57f, 81f, 58f, 72f) },
        { "sigiloso-manga", new Vector4(77f, 113f, 77f, 114f) },
        { "astra-pop", new Vector4(54f, 67f, 53f, 68f) },
        { "guerrero-fauve", new Vector4(62f, 63f, 62f, 63f) },
        { "sigiloso-tinta", new Vector4(56f, 104f, 54f, 78f) },
        { "astra-nouveau", new Vector4(46f, 100f, 47f, 66f) },
        { "pesado-cubista", new Vector4(46f, 61f, 48f, 60f) },
        { "sigiloso-8bit", new Vector4(65f, 75f, 65f, 74f) },
        { "astra-acuarela", new Vector4(40f, 47f, 40f, 47f) },
        { "pesado-tinta-china", new Vector4(72f, 92f, 72f, 90f) },
        { "guerrero-puntillismo", new Vector4(69f, 82f, 69f, 81f) },
    };

    /// <summary>
    /// Distancia desde cada borde de la imagen del marco hasta el borde INTERIOR de la parte opaca de su riel (izquierda, arriba, derecha,
    /// abajo), en píxeles de la imagen original. Hasta ahí llega el banner cuando hay marco (PerfilUI.InteriorMarco).
    /// </summary>
    public static readonly Dictionary<string, Vector4> InterioresMarco = new Dictionary<string, Vector4>
    {
        { "anden-riftwalker", new Vector4(81f, 100f, 80f, 101f) },
        { "umbral-celeste", new Vector4(63f, 114f, 63f, 113f) },
        { "forja-ambar", new Vector4(92f, 130f, 92f, 132f) },
        { "pulso-carmesi", new Vector4(81f, 110f, 83f, 108f) },
        { "velo-esmeralda", new Vector4(84f, 115f, 85f, 115f) },
        { "ultima-oleada", new Vector4(93f, 124f, 92f, 125f) },
        { "nucleo-del-rift", new Vector4(75f, 86f, 73f, 85f) },
        { "pesado-realista", new Vector4(86f, 105f, 87f, 106f) },
        { "guerrero-synthwave", new Vector4(88f, 125f, 88f, 116f) },
        { "sigiloso-manga", new Vector4(95f, 137f, 96f, 139f) },
        { "astra-pop", new Vector4(78f, 101f, 78f, 103f) },
        { "guerrero-fauve", new Vector4(96f, 95f, 96f, 96f) },
        { "sigiloso-tinta", new Vector4(75f, 129f, 73f, 103f) },
        { "astra-nouveau", new Vector4(62f, 121f, 63f, 86f) },
        { "pesado-cubista", new Vector4(64f, 86f, 66f, 86f) },
        { "sigiloso-8bit", new Vector4(92f, 107f, 92f, 107f) },
        { "astra-acuarela", new Vector4(54f, 67f, 54f, 67f) },
        { "pesado-tinta-china", new Vector4(87f, 113f, 87f, 111f) },
        { "guerrero-puntillismo", new Vector4(98f, 116f, 97f, 115f) },
    };

    /// <summary>
    /// Cómo se estira cada marco en sus dos mitades (píxeles de la imagen original): ancho de las esquinas y medio ancho del
    /// adorno del centro, que no se deforman, y si los tramos del riel se repiten en vez de estirarse (así los durmientes,
    /// los puntos y las rayas no se alargan).
    /// </summary>
    public static readonly Dictionary<string, (float esquina, float centro, bool repetir)> CortesMarco = new Dictionary<string, (float, float, bool)>
    {
        { "anden-riftwalker", (230f, 211f, true) },
        { "umbral-celeste", (192f, 173f, false) },
        { "forja-ambar", (307f, 326f, false) },
        { "pulso-carmesi", (192f, 173f, false) },
        { "velo-esmeralda", (307f, 192f, true) },
        { "ultima-oleada", (250f, 422f, true) },
        { "nucleo-del-rift", (211f, 0f, false) },
        { "pesado-realista", (192f, 307f, false) },
        { "guerrero-synthwave", (192f, 134f, false) },
        { "sigiloso-manga", (269f, 211f, false) },
        { "astra-pop", (154f, 0f, true) },
        { "guerrero-fauve", (154f, 0f, true) },
        { "sigiloso-tinta", (192f, 154f, false) },
        { "astra-nouveau", (307f, 422f, false) },
        { "pesado-cubista", (192f, 0f, true) },
        { "sigiloso-8bit", (192f, 0f, false) },
        { "astra-acuarela", (192f, 0f, false) },
        { "pesado-tinta-china", (230f, 230f, false) },
        { "guerrero-puntillismo", (154f, 0f, true) },
    };

    public static (float esquina, float centro, bool repetir) Cortes(string id) =>
        CortesMarco.TryGetValue(id, out var c) ? c : (300f, 0f, false);

    public static bool Tiene(Cosmetico c) => c.rareza == Rareza.Inicial || E.obtenidos.Contains(c.Clave);
    public static bool EsNuevo(Cosmetico c) => E.nuevos.Contains(c.Clave);
    public static int CantidadNuevos(Tipo tipo) => Catalogo.FindAll(c => c.tipo == tipo && EsNuevo(c)).Count;

    public static bool Equipado(Cosmetico c) =>
        c.tipo == Tipo.Avatar ? E.avatar == c.id : c.tipo == Tipo.Banner ? E.banner == c.id : c.tipo == Tipo.Marco ? E.marco == c.id : E.titulo == c.id;

    public static int Obtenidos(Tipo tipo) => Catalogo.FindAll(c => c.tipo == tipo && Tiene(c)).Count;
    public static int Total(Tipo tipo) => Catalogo.FindAll(c => c.tipo == tipo).Count;

    public static void Visto(Cosmetico c)
    {
        if (E.nuevos.Remove(c.Clave)) Guardar();
    }

    public static bool Equipar(Cosmetico c)
    {
        if (!Tiene(c)) return false;
        if (c.tipo == Tipo.Avatar) E.avatar = c.id;
        else if (c.tipo == Tipo.Banner) E.banner = c.id;
        else if (c.tipo == Tipo.Marco) E.marco = c.id;
        else E.titulo = c.id;
        E.nuevos.Remove(c.Clave);
        E.equipadoElegido = true;
        Guardar();
        return true;
    }

    public static Sprite Imagen(Cosmetico c) => c?.Recurso != null ? Resources.Load<Sprite>(c.Recurso) : null;
    public static Sprite Fondo(string nombre) => Resources.Load<Sprite>("Perfil/Fondos/" + nombre);

    // ================= Cápsulas =================

    public static bool PuedeAbrir(int cantidad) => E.boletos >= cantidad;

    /// <summary>
    /// Abre cápsulas: gasta un boleto por cada una y después las sortea. listo recibe los premios, o null y el motivo si
    /// no se pudo (no alcanzan los boletos o, con cuenta, no hay conexión: la base confirma el gasto, US 272).
    /// </summary>
    public static void Abrir(int cantidad, Action<List<Resultado>, string> listo)
    {
        string falta = cantidad == 1 ? "No tenés boletos: canjeá créditos o cristales" : $"Te faltan boletos para abrir {cantidad}";
        Gastar(Mover("capsula", boletos: -cantidad), falta, error => listo?.Invoke(error == null ? Sortear(cantidad) : null, error));
    }

    // Las cápsulas ya pagadas.
    private static List<Resultado> Sortear(int cantidad)
    {
        var lista = new List<Resultado>();
        int repetidos = 0;
        for (int i = 0; i < cantidad; i++)
        {
            bool garantia = SinEpico >= Garantia - 1;
            E.aperturas++;
            Rareza r = Sortear();
            if (garantia && r < Rareza.Epico) r = Rareza.Epico;
            if (r >= Rareza.Epico) E.ultimoEpico = E.aperturas;

            // Dentro de la rareza, primero el tipo (si no, casi todo serían títulos: son 71 de 129) y después el objeto.
            Tipo tipo = SortearTipo(r);
            List<Cosmetico> posibles = Catalogo.FindAll(c => c.rareza == r && c.tipo == tipo);
            Cosmetico premio = posibles[UnityEngine.Random.Range(0, posibles.Count)];
            var res = new Resultado { cosmetico = premio, garantia = garantia, nuevo = !Tiene(premio) };
            if (res.nuevo)
            {
                E.obtenidos.Add(premio.Clave);
                if (!E.nuevos.Contains(premio.Clave)) E.nuevos.Add(premio.Clave);
            }
            else
            {
                res.creditos = CreditosPorRepetido[(int)r];
                repetidos += res.creditos;
            }
            E.recientes.Insert(0, premio.Clave);
            if (E.recientes.Count > 8) E.recientes.RemoveAt(E.recientes.Count - 1);
            lista.Add(res);
        }
        if (repetidos > 0) Sumar(Mover("repetido", creditos: repetidos)); // Sumar guarda
        else Guardar();
        return lista;
    }

    public static readonly int[] ProbabilidadTipo = { 35, 25, 25, 15 }; // Avatar, Banner, Título, Marco

    private static Tipo SortearTipo(Rareza r)
    {
        int total = 0;
        for (int t = 0; t < ProbabilidadTipo.Length; t++) if (Catalogo.Exists(c => c.rareza == r && (int)c.tipo == t)) total += ProbabilidadTipo[t];
        int tiro = UnityEngine.Random.Range(0, total), acumulado = 0;
        for (int t = 0; t < ProbabilidadTipo.Length; t++)
        {
            if (!Catalogo.Exists(c => c.rareza == r && (int)c.tipo == t)) continue;
            acumulado += ProbabilidadTipo[t];
            if (tiro < acumulado) return (Tipo)t;
        }
        return Tipo.Titulo;
    }

    private static Rareza Sortear()
    {
        // 0 a 99: los primeros números son legendarios, después épicos, raros y el resto comunes.
        int tiro = UnityEngine.Random.Range(0, 100), acumulado = 0;
        for (int r = (int)Rareza.Legendario; r >= (int)Rareza.Comun; r--)
        {
            acumulado += Probabilidad[r];
            if (tiro < acumulado) return (Rareza)r;
        }
        return Rareza.Comun;
    }

    public static Cosmetico DesdeClave(string clave)
    {
        if (string.IsNullOrEmpty(clave) || clave.Length < 3) return null;
        Tipo t = clave.StartsWith("av:") ? Tipo.Avatar : clave.StartsWith("bn:") ? Tipo.Banner : clave.StartsWith("mc:") ? Tipo.Marco : Tipo.Titulo;
        return Buscar(t, clave.Substring(3));
    }

    // ================= Monedas =================

    /// <summary>Canjea créditos por un boleto. listo recibe null si se pudo, o el motivo.</summary>
    public static void CanjearCreditos(Action<string> listo) =>
        Gastar(Mover("canje_creditos", creditos: -CreditosPorBoleto, boletos: 1), "Te faltan créditos: se ganan jugando partidas", listo);

    /// <summary>Canjea cristales por boletos (10 juntos salen más baratos). listo recibe null si se pudo, o el motivo.</summary>
    public static void CanjearCristales(int boletos, Action<string> listo)
    {
        int costo = boletos >= 10 ? CristalesPorDiez : CristalesPorBoleto * boletos;
        Gastar(Mover("canje_cristales", cristales: -costo, boletos: boletos), "Te faltan cristales", listo);
    }

    public static bool PackDisponible(Pack p) => !p.unaVez || !E.packsUsados.Contains(p.id);

    /// <summary>Compra SIMULADA: no se cobra nada, solo suma los cristales y anota la compra.</summary>
    public static bool ComprarSimulado(Pack p)
    {
        if (!PackDisponible(p)) return false;
        E.gastado += p.precio;
        if (p.unaVez) E.packsUsados.Add(p.id);
        DateTime ahora = DateTime.Now;
        E.compras.Insert(0, new Compra { pack = p.nombre, precio = p.precio, fecha = ahora.ToString("dd/MM HH:mm"), cuando = ahora.ToString("yyyy-MM-ddTHH:mm") });
        Sumar(Mover("compra", cristales: p.cristales + p.bonus, boletos: p.boletos));
        return true;
    }

    public const int DebugCreditos = 1000, DebugBoletos = 10, DebugCristales = 500;

    /// <summary>DEBUG (F9 en el menú): suma de las tres monedas para probar sin jugar ni comprar.</summary>
    public static void SumarDebug() => Sumar(Mover("debug", DebugCreditos, DebugBoletos, DebugCristales));

    /// <summary>Créditos por jugar: se llama al terminar una partida. Devuelve cuántos sumó.</summary>
    public static int GanarPorPartida(bool gano)
    {
        int creditos = gano ? CreditosVictoria : CreditosDerrota;
        Sumar(Mover("partida", creditos: creditos));
        return creditos;
    }

    // ----- Saldo de la cuenta (US 272) -----
    // Sin cuenta, las monedas son de la PC, como antes. Con cuenta, el saldo lo lleva la base (Backend/Supabase/monedas.sql):
    // lo ganado se suma al toque y queda en la fila hasta que LogrosOnline lo sube (también lo ganado sin conexión, CA4);
    // para gastar, primero lo confirma la base (CA3 y CA4: si se gastó en otra PC, la base no deja gastar de más).

    /// <summary>Hay una cuenta abierta: las monedas son las de la base.</summary>
    public static bool ConCuenta => Guardado.CuentaActiva != null;

    public static bool HayPendientes => E.pendientes.Count > 0;

    /// <summary>Copia de lo ganado que todavía no subió.</summary>
    public static List<Movimiento> Pendientes => new List<Movimiento>(E.pendientes);

    public static Movimiento Mover(string motivo, int creditos = 0, int boletos = 0, int cristales = 0) =>
        new Movimiento { id = Guid.NewGuid().ToString(), motivo = motivo, creditos = creditos, boletos = boletos, cristales = cristales };

    private static bool Alcanza(Movimiento m) => E.creditos + m.creditos >= 0 && E.boletos + m.boletos >= 0 && E.cristales + m.cristales >= 0;

    private static void Aplicar(Movimiento m)
    {
        E.creditos = Mathf.Max(0, E.creditos + m.creditos);
        E.boletos = Mathf.Max(0, E.boletos + m.boletos);
        E.cristales = Mathf.Max(0, E.cristales + m.cristales);
    }

    // Se aplica ya y, con cuenta, espera en la fila para subir (lo ganado, o un gasto que no pudo confirmar la base
    // porque todavía no tiene monedas.sql: cuando la tenga, lo repite en orden).
    private static void Sumar(Movimiento m)
    {
        Aplicar(m);
        if (ConCuenta) E.pendientes.Add(m);
        Guardar();
    }

    // Un gasto: sin cuenta (o sin el servicio online), al instante; con cuenta, la base lo confirma antes.
    private static void Gastar(Movimiento m, string falta, Action<string> listo)
    {
        if (!Alcanza(m)) { listo?.Invoke(falta); return; }
        LogrosOnline online = LogrosOnline.Instancia;
        if (!ConCuenta || online == null || !online.BilleteraEnLaNube)
        {
            Sumar(m);
            listo?.Invoke(null);
            return;
        }
        online.Gastar(m, falta, listo);
    }

    /// <summary>
    /// El saldo que devolvió la base después de aplicar lo que se le mandó: es el de la cuenta. Salen de la fila los que
    /// ya llegaron (aplicados o rechazados) y se vuelven a sumar los que se ganaron mientras tanto.
    /// </summary>
    public static void SaldoDeLaNube(int creditos, int boletos, int cristales, ICollection<string> enviados)
    {
        E.pendientes.RemoveAll(m => enviados.Contains(m.id));
        E.creditos = creditos; E.boletos = boletos; E.cristales = cristales;
        foreach (Movimiento m in E.pendientes) Aplicar(m);
        Guardar();
    }

    /// <summary>Sin el servicio online actualizado (falta correr monedas.sql), un gasto con cuenta se hace en la PC y
    /// queda en la fila.</summary>
    public static void GastarEnLaPC(Movimiento m) => Sumar(m);

    /// <summary>«Reiniciar datos de prueba» es solo sin cuenta: la nube devolvería todo.</summary>
    public static bool PuedeReiniciar => !ConCuenta;

    public static string Pesos(int monto) => "$ " + monto.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", ".");
    public static string Numero(int n) => n.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", ".");
}
