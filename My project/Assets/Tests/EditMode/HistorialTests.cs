using NUnit.Framework;
using UnityEngine;
using Partida = HistorialDePartidas.Partida;

// Historial de partidas (F34, US 374). Trabajan sobre listas sueltas, nunca sobre el historial guardado: no tocan el
// progreso real. Lo que se ve en pantalla (ListaDeHistorial) se prueba con las funciones de texto, sin abrir el menú.
public class HistorialTests
{
    private static Partida P(string id, long fecha, string modo = "tactico") => new Partida { id = id, fecha = fecha, modo = modo };

    [Test, Category("US 374"), Description("CA1: de la más nueva a la más vieja, aunque lleguen desordenadas")]
    public void Ordena_de_la_mas_nueva_a_la_mas_vieja()
    {
        var l = new HistorialDePartidas.Lista();
        Assert.IsTrue(HistorialDePartidas.Agregar(l, P("b", 200)));
        Assert.IsTrue(HistorialDePartidas.Agregar(l, P("a", 100)));
        Assert.IsTrue(HistorialDePartidas.Agregar(l, P("c", 300)));
        CollectionAssert.AreEqual(new[] { "c", "b", "a" }, l.partidas.ConvertAll(p => p.id));
    }

    [Test, Category("US 374"), Description("CA1: quedan las últimas 20; una más vieja que todas no entra")]
    public void Guarda_solo_las_ultimas_20()
    {
        var l = new HistorialDePartidas.Lista();
        for (int i = 1; i <= 25; i++) HistorialDePartidas.Agregar(l, P("p" + i, 1000 + i));
        Assert.AreEqual(HistorialDePartidas.Maximo, l.partidas.Count);
        Assert.AreEqual("p25", l.partidas[0].id);
        Assert.AreEqual("p6", l.partidas[19].id);
        Assert.IsFalse(HistorialDePartidas.Agregar(l, P("vieja", 1)), "más vieja que las 20 guardadas");
    }

    [Test, Category("US 374"), Description("CA3: la misma partida (por ejemplo, al volver después de un corte) no aparece dos veces")]
    public void No_repite_una_partida()
    {
        var l = new HistorialDePartidas.Lista();
        Assert.IsTrue(HistorialDePartidas.Agregar(l, P("KX7QM:123", 500)));
        Assert.IsFalse(HistorialDePartidas.Agregar(l, P("KX7QM:123", 600)));
        Assert.IsFalse(HistorialDePartidas.Agregar(l, P("", 700)), "sin id no vale");
        Assert.AreEqual(1, l.partidas.Count);
    }

    [Test, Category("US 374"), Description("CA4: dos juegos abiertos en la PC juntan sus historiales sin perder lo subido")]
    public void Juntar_dos_historiales()
    {
        var a = new HistorialDePartidas.Lista();
        HistorialDePartidas.Agregar(a, P("x", 100));
        HistorialDePartidas.Agregar(a, P("y", 300));
        var b = new HistorialDePartidas.Lista();
        Partida xSubida = P("x", 100); xSubida.subida = true;
        HistorialDePartidas.Agregar(b, xSubida);
        HistorialDePartidas.Agregar(b, P("z", 200));
        var junta = HistorialDePartidas.Leer(HistorialDePartidas.Juntar(JsonUtility.ToJson(a), JsonUtility.ToJson(b)));
        CollectionAssert.AreEqual(new[] { "y", "z", "x" }, junta.partidas.ConvertAll(p => p.id));
        Assert.IsTrue(junta.partidas.Find(p => p.id == "x").subida, "lo que ya estaba en la nube sigue marcado");
    }

    [Test, Category("US 374"), Description("Un guardado dañado o vacío da un historial vacío, sin errores")]
    public void Leer_guardado_danado()
    {
        Assert.AreEqual(0, HistorialDePartidas.Leer("").partidas.Count);
        Assert.AreEqual(0, HistorialDePartidas.Leer("{no es json").partidas.Count);
    }

    [Test, Category("US 374"), Description("El id va con los caracteres que acepta la base (rw_add_matches)")]
    public void Id_valido_para_la_base()
    {
        Assert.AreEqual("KX7QM:-123", HistorialDePartidas.IdValido("KX7QM:-123"));
        Assert.AreEqual("sala_con_espacio_y__:9", HistorialDePartidas.IdValido("sala con espacio y ñ:9"));
        Assert.AreEqual(96, HistorialDePartidas.IdValido(new string('a', 200)).Length);
    }

    [Test, Category("US 374"), Description("CA2: el resultado, lo que hizo en la partida y la duración")]
    public void Textos_de_cada_partida()
    {
        L10n.SetLanguage("es");
        var t = new Partida { id = "t", modo = "tactico", gano = true, bajas = 12, muertes = 4, asistencias = 3, segundos = 905 };
        Assert.AreEqual("Victoria", ListaDeHistorial.Resultado(t).texto);
        Assert.AreEqual("12 / 4 / 3 · KDA 3,75", ListaDeHistorial.Detalle(t));
        Assert.AreEqual("15:05", ListaDeHistorial.Duracion(t.segundos));

        var dm = new Partida { id = "d", modo = "deathmatch", puesto = 3, bajas = 8, muertes = 10 };
        Assert.AreEqual("3.º puesto", ListaDeHistorial.Resultado(dm).texto);

        var inv = new Partida { id = "i", modo = "invasion", gano = false, dificultad = "dificil", oleada = 14, coop = true };
        Assert.AreEqual("Derrota", ListaDeHistorial.Resultado(inv).texto);
        Assert.AreEqual("Difícil · oleada 14 · cooperativo", ListaDeHistorial.Detalle(inv));
        Assert.AreEqual("1:02:03", ListaDeHistorial.Duracion(3723));
    }
}
