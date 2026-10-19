using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

// Estadísticas del jugador (F24: US 222 y US 223) y categorías del ranking global (US 226). Trabajan sobre totales
// sueltos, nunca sobre EstadisticasJugador.Datos: no tocan el progreso real.

public class EstadisticasTests
{
    private static EstadisticasJugador.Partida Tactico(string id, bool gano, int bajas, int muertes, int asistencias) =>
        new EstadisticasJugador.Partida
        {
            id = id, gano = gano, bajas = bajas, muertes = muertes, asistencias = asistencias, cabezas = 1,
            rondasGanadas = gano ? 7 : 3, plantados = 1, desactivados = 0, mvp = gano, segundos = 900,
        };

    private static EstadisticasJugador.Partida Deathmatch(string id, bool gano, int bajas, int muertes, int racha) =>
        new EstadisticasJugador.Partida
        {
            id = id, deathmatch = true, gano = gano, bajas = bajas, muertes = muertes, asistencias = 5, cabezas = 6,
            mejorRacha = racha, segundos = 480,
        };

    [Test, Category("US 223"), Description("CA3: sin partidas y sin muertes nunca da NaN ni infinito")]
    public void Kda_sin_datos_y_sin_muertes()
    {
        var t = new EstadisticasJugador.Totales();
        Assert.IsTrue(t.SinDatos);
        Assert.AreEqual(0f, t.Kda);
        Assert.AreEqual(8f, EstadisticasJugador.Kda(5, 3, 0), "sin muertes divide por 1");
        Assert.IsFalse(float.IsNaN(EstadisticasJugador.Kda(0, 0, 0)));
        Assert.IsFalse(float.IsInfinity(EstadisticasJugador.Kda(10, 0, 0)));
    }

    [Test, Category("US 223"), Description("CA4: (bajas + asistencias) / muertes")]
    public void Kda_formula()
    {
        Assert.AreEqual(3f, EstadisticasJugador.Kda(10, 2, 4), 0.0001f);
    }

    [Test, Category("US 222"), Category("US 223"), Description("CA1: cada modo suma lo suyo y el General es la suma")]
    public void Suma_por_modo_y_general()
    {
        var t = new EstadisticasJugador.Totales();
        Assert.IsTrue(EstadisticasJugador.Sumar(t, Tactico("ABCDE:100", true, 12, 4, 3)));
        Assert.IsTrue(EstadisticasJugador.Sumar(t, Deathmatch("FGHIJ:200", false, 20, 10, 4)));

        Assert.AreEqual(1, t.tactico.partidas);
        Assert.AreEqual(1, t.tactico.victorias);
        Assert.AreEqual(1, t.mvp);
        Assert.AreEqual(7, t.rondasGanadas);
        Assert.AreEqual(1, t.deathmatch.partidas);
        Assert.AreEqual(0, t.deathmatch.victorias);
        Assert.AreEqual(4, t.mejorRacha);

        Assert.AreEqual(2, t.Partidas);
        Assert.AreEqual(32, t.Bajas);
        Assert.AreEqual(14, t.Muertes);
        Assert.AreEqual(8, t.Asistencias);
        Assert.AreEqual(7, t.Cabezas);
        Assert.AreEqual(1380, t.Segundos);
        Assert.AreEqual(40f / 14f, t.Kda, 0.0001f, "el KDA sale de los totales, no del promedio de las partidas");
    }

    [Test, Category("US 222"), Description("CA3: la misma partida no se cuenta dos veces")]
    public void La_misma_partida_no_se_suma_dos_veces()
    {
        var t = new EstadisticasJugador.Totales();
        EstadisticasJugador.Sumar(t, Tactico("ABCDE:100", true, 12, 4, 3));
        Assert.IsFalse(EstadisticasJugador.Sumar(t, Tactico("ABCDE:100", true, 12, 4, 3)));
        Assert.AreEqual(1, t.tactico.partidas);
        Assert.AreEqual(12, t.tactico.bajas);
        Assert.IsFalse(EstadisticasJugador.Sumar(t, Tactico("", true, 1, 1, 1)), "sin id no se suma");
        Assert.IsFalse(EstadisticasJugador.Sumar(t, null));
    }

    [Test, Category("US 222"), Description("Mejor racha en Deathmatch: el máximo, no la suma")]
    public void La_mejor_racha_es_el_maximo()
    {
        var t = new EstadisticasJugador.Totales();
        EstadisticasJugador.Sumar(t, Deathmatch("A:1", false, 20, 10, 4));
        EstadisticasJugador.Sumar(t, Deathmatch("A:2", true, 25, 3, 3));
        Assert.AreEqual(4, t.mejorRacha);
        Assert.AreEqual(1, t.deathmatch.victorias);
    }

    [Test, Category("US 222"), Description("Recuerda las últimas 30 partidas, pero las nuevas se siguen sumando")]
    public void Recuerda_solo_las_ultimas_30_partidas()
    {
        var t = new EstadisticasJugador.Totales();
        for (int i = 0; i < 40; i++) EstadisticasJugador.Sumar(t, Tactico("SALA" + i + ":1", false, 0, 1, 0));
        Assert.AreEqual(30, t.contadas.Count);
        Assert.AreEqual(40, t.tactico.partidas);
    }

    [Test, Category("US 222"), Category("US 162"), Description("Dos juegos, o la PC y la nube: el máximo de cada total")]
    public void Unir_toma_el_maximo_y_junta_las_partidas()
    {
        var a = new EstadisticasJugador.Totales();
        var b = new EstadisticasJugador.Totales();
        EstadisticasJugador.Sumar(a, Tactico("A:1", true, 10, 2, 1));
        EstadisticasJugador.Sumar(b, Tactico("B:1", false, 3, 9, 0));
        EstadisticasJugador.Sumar(b, Deathmatch("B:2", false, 7, 0, 5));

        Assert.IsTrue(EstadisticasJugador.Unir(a, b));
        Assert.AreEqual(10, a.tactico.bajas);
        Assert.AreEqual(9, a.tactico.muertes);
        Assert.AreEqual(7, a.deathmatch.bajas);
        Assert.AreEqual(5, a.mejorRacha);
        CollectionAssert.IsSupersetOf(a.contadas, new[] { "A:1", "B:1", "B:2" });
        Assert.IsFalse(EstadisticasJugador.Unir(a, b), "unir dos veces no cambia nada");
    }

    [Test, Category("US 222"), Category("US 163"), Description("CA6: datos rotos o de una versión vieja arrancan en cero")]
    public void Leer_datos_rotos_o_viejos()
    {
        var a = new EstadisticasJugador.Totales();
        EstadisticasJugador.Sumar(a, Tactico("A:1", true, 10, 2, 1));
        var vuelta = EstadisticasJugador.Leer(JsonUtility.ToJson(a));
        Assert.AreEqual(10, vuelta.tactico.bajas);
        Assert.AreEqual(1, vuelta.contadas.Count);

        Assert.IsTrue(EstadisticasJugador.Leer("basura").SinDatos);
        Assert.IsTrue(EstadisticasJugador.Leer("").SinDatos);
        var viejo = EstadisticasJugador.Leer("{\"version\":1}");
        Assert.IsNotNull(viejo.tactico);
        Assert.IsNotNull(viejo.deathmatch);
        Assert.IsNotNull(viejo.contadas);

        string mio = JsonUtility.ToJson(a);
        Assert.AreEqual(mio, EstadisticasJugador.Juntar(mio, mio), "juntar sin cambios deja el mismo texto");
    }

    [Test, Category("US 226"), Description("Cada categoría del juego existe en la base (rw_stat_board) y su grupo no está vacío")]
    public void Cada_categoria_del_ranking_existe_en_la_base()
    {
        string sql = File.ReadAllText(Path.Combine(Application.dataPath, "../../Backend/Supabase/logros.sql"));
        foreach (CategoriasRanking.Categoria c in CategoriasRanking.Todas)
        {
            Assert.GreaterOrEqual(Array.IndexOf(CategoriasRanking.Grupos, c.grupo), 0, "grupo desconocido: " + c.grupo);
            if (c.id != CategoriasRanking.Logros) StringAssert.Contains("'" + c.id + "'", sql, "falta en logros.sql: " + c.id);
        }
        foreach (string g in CategoriasRanking.Grupos) Assert.IsNotNull(CategoriasRanking.Primera(g), "grupo vacío: " + g);
    }

    [TestCase("bajas", 1204, "1.204")]
    [TestCase("kda", 1.789, "1,79")]
    [TestCase("precision", 55.55, "55,6 %")]
    [TestCase("mejor_reaccion", 650, "0,65 s")]
    [Category("US 226"), Description("Cómo se muestra cada formato del ranking")]
    public void Formatos_del_ranking(string categoria, double valor, string esperado)
    {
        Assert.AreEqual(esperado, CategoriasRanking.Mostrar(CategoriasRanking.Buscar(categoria), valor));
    }
}
