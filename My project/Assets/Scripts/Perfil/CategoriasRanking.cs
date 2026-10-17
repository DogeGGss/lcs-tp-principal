using System;
using System.Globalization;

// Las categorías del ranking global (US 226): cada una ordena a todos los jugadores por un dato que ya se guarda en la
// nube. Las calcula la base (rw_stat_board, con los mismos ids); "Logros" es el ranking por logros obtenidos
// (rw_achievement_board). Los porcentajes y promedios piden un mínimo para no premiar a quien jugó una sola partida.
public static class CategoriasRanking
{
    public enum Formato { Entero, Decimal, Porcentaje, Horas, Segundos }

    public sealed class Categoria
    {
        public string id, grupo, nombre, requisito;
        public Formato formato;
    }

    public const string Logros = "logros";
    public static readonly string[] Grupos = { "General", "Táctico", "Deathmatch", "Minijuego", "Logros" };

    private const string CincoPartidas = "Mínimo 5 partidas terminadas", CincuentaDisparos = "Mínimo 50 disparos a cometas";

    public static readonly Categoria[] Todas =
    {
        C("partidas", "General", "Partidas"),
        C("victorias", "General", "Victorias"),
        C("porcentaje_victorias", "General", "% de victorias", Formato.Porcentaje, CincoPartidas),
        C("bajas", "General", "Bajas"),
        C("muertes", "General", "Muertes"),
        C("asistencias", "General", "Asistencias"),
        C("cabezas", "General", "Bajas a la cabeza"),
        C("kda", "General", "KDA", Formato.Decimal, CincoPartidas),
        C("tiros_cabeza", "General", "Tiros a la cabeza", Formato.Porcentaje, CincoPartidas),
        C("horas", "General", "Horas jugadas", Formato.Horas),
        C("tactico_partidas", "Táctico", "Partidas"),
        C("tactico_victorias", "Táctico", "Victorias"),
        C("rondas_ganadas", "Táctico", "Rondas ganadas"),
        C("plantados", "Táctico", "Plantados"),
        C("desactivados", "Táctico", "Desactivados"),
        C("mvp", "Táctico", "MVP"),
        C("deathmatch_partidas", "Deathmatch", "Partidas"),
        C("primer_puesto", "Deathmatch", "Primer puesto"),
        C("deathmatch_bajas", "Deathmatch", "Bajas"),
        C("mejor_racha", "Deathmatch", "Mejor racha"),
        C("cometas", "Minijuego", "Cometas destruidos"),
        C("cometas_azules", "Minijuego", "Azules"),
        C("cometas_verdes", "Minijuego", "Verdes"),
        C("cometas_dorados", "Minijuego", "Dorados"),
        C("minijuego_puntos", "Minijuego", "Puntos"),
        C("record_puntos", "Minijuego", "Récord en una fase"),
        C("precision", "Minijuego", "Precisión", Formato.Porcentaje, CincuentaDisparos),
        C("mejor_reaccion", "Minijuego", "Mejor reacción", Formato.Segundos, CincuentaDisparos + " · gana el menor tiempo"),
        C(Logros, "Logros", "Logros obtenidos"),
    };

    private static Categoria C(string id, string grupo, string nombre, Formato formato = Formato.Entero, string requisito = null) =>
        new Categoria { id = id, grupo = grupo, nombre = nombre, formato = formato, requisito = requisito };

    public static Categoria Buscar(string id) => Array.Find(Todas, c => c.id == id);

    /// <summary>La primera categoría de un grupo (al elegir el grupo).</summary>
    public static Categoria Primera(string grupo) => Array.Find(Todas, c => c.grupo == grupo);

    /// <summary>El valor como se muestra: "1.204", "1,79", "55,6 %", "8,1 h" o "0,65 s".</summary>
    public static string Mostrar(Categoria c, double valor)
    {
        CultureInfo ci = CultureInfo.InvariantCulture;
        switch (c?.formato ?? Formato.Entero)
        {
            case Formato.Decimal: return valor.ToString("0.00", ci).Replace('.', ',');
            case Formato.Porcentaje: return valor.ToString("0.#", ci).Replace('.', ',') + " %";
            case Formato.Horas: return valor.ToString("0.#", ci).Replace('.', ',') + " h";
            case Formato.Segundos: return (valor / 1000.0).ToString("0.00", ci).Replace('.', ',') + " s";
            default: return PerfilDatos.Numero((int)Math.Min(int.MaxValue, Math.Round(valor)));
        }
    }
}
