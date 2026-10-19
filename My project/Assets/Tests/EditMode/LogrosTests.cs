using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

// Logros (F13): los de cometas (US 040 a US 045 y US 160) y los de combate (US 238). Trabajan sobre progresos sueltos,
// nunca sobre el guardado real del jugador.

public class LogrosTests
{
    [Test, Category("US 040"), Category("US 238"), Description("25 logros: 9 de cometas y 16 de combate")]
    public void El_catalogo_tiene_9_de_cometas_y_16_de_combate()
    {
        Assert.AreEqual(25, LogrosCometas.Catalogo.Length);
        Assert.AreEqual(9, Array.FindAll(LogrosCometas.Catalogo, d => d.EsCometa).Length);
    }

    [Test, Category("US 041"), Category("US 042"), Description("10, 50 y 100 de cada color, una sola vez")]
    public void Umbrales_de_los_cometas()
    {
        var p = new LogrosCometas.Progreso();
        Assert.AreEqual(0, LogrosCometas.Aplicar(p, 0, 9, "a").Count, "9 todavía no alcanza");
        Assert.AreEqual(1, LogrosCometas.Aplicar(p, 0, 10, "b").Count, "primer umbral");
        Assert.AreEqual(0, LogrosCometas.Aplicar(p, 0, 10, "c").Count, "no se repite");
        Assert.AreEqual("b", p.fechas[0], "conserva la fecha del primero");
        Assert.AreEqual(2, LogrosCometas.Aplicar(p, 0, 100, "d").Count, "saltar varias metas las desbloquea todas");
        Assert.AreEqual(0, LogrosCometas.Aplicar(p, 0, 2, "e").Count);
        Assert.AreEqual(100, p.aciertos[0], "el contador no baja");
        Assert.AreEqual(0, p.aciertos[1], "cada color por separado");
        Assert.IsNull(p.fechas[3]);
        Assert.AreEqual(2, LogrosCometas.Aplicar(p, 1, 50, "f").Count, "verdes");
        Assert.AreEqual(3, LogrosCometas.Aplicar(p, 2, 100, "g").Count, "dorados");
        Assert.AreEqual(1, LogrosCometas.Aplicar(p, 1, 100, "h").Count, "último umbral verde");
    }

    [Test, Category("US 045"), Description("El progreso vuelve igual del JSON")]
    public void Progreso_ida_y_vuelta_por_json()
    {
        var p = new LogrosCometas.Progreso();
        LogrosCometas.Aplicar(p, 2, 100, "g");
        var copia = JsonUtility.FromJson<LogrosCometas.Progreso>(JsonUtility.ToJson(p));
        Assert.AreEqual(100, copia.aciertos[2]);
        Assert.AreEqual("g", copia.fechas[8]);
    }

    [Test, Category("US 045"), Category("US 163"), Description("CA4: guardados con menos o más logros se ajustan al catálogo")]
    public void Guardados_de_otras_versiones_se_ajustan_al_catalogo()
    {
        var viejo = LogrosCometas.Validar(JsonUtility.FromJson<LogrosCometas.Progreso>(
            "{\"version\":1,\"aciertos\":[12,0,3],\"fechas\":[\"x\",\"\"]}"));
        Assert.AreEqual(LogrosCometas.Catalogo.Length, viejo.fechas.Length);
        Assert.AreEqual("x", viejo.fechas[0]);
        Assert.AreEqual(12, viejo.aciertos[0]);

        var nuevo = LogrosCometas.Validar(JsonUtility.FromJson<LogrosCometas.Progreso>(
            "{\"version\":1,\"aciertos\":[1,2,3],\"fechas\":[\"a\",\"b\",\"c\",\"d\",\"e\",\"f\",\"g\",\"h\",\"i\",\"j\",\"k\"]}"));
        Assert.AreEqual(LogrosCometas.Catalogo.Length, nuevo.fechas.Length);
        Assert.AreEqual("i", nuevo.fechas[8]);
    }

    [Test, Category("US 045"), Description("CA2: un contador negativo se rechaza en vez de guardarse")]
    public void Un_contador_negativo_se_rechaza()
    {
        Assert.Throws<InvalidDataException>(() => LogrosCometas.Validar(
            JsonUtility.FromJson<LogrosCometas.Progreso>("{\"version\":1,\"aciertos\":[1,-2,3]}")));
    }

    [Test, Category("US 045"), Description("CA2: dos juegos en la misma PC no se pisan el progreso")]
    public void Juntar_toma_el_maximo_y_conserva_las_fechas()
    {
        var mio = new LogrosCometas.Progreso(); mio.aciertos[0] = 15; mio.aciertos[1] = 2;
        var otro = new LogrosCometas.Progreso(); otro.aciertos[0] = 12; otro.aciertos[1] = 9; otro.fechas[0] = "t";
        Assert.IsTrue(LogrosCometas.Juntar(mio, otro));
        Assert.AreEqual(15, mio.aciertos[0]);
        Assert.AreEqual(9, mio.aciertos[1]);
        Assert.AreEqual("t", mio.fechas[0]);
        Assert.IsFalse(LogrosCometas.Juntar(mio, otro), "juntar dos veces no cambia nada");
    }

    [Test, Category("US 238"), Description("CA2: los cometas no desbloquean logros de combate")]
    public void Los_cometas_no_desbloquean_logros_de_combate()
    {
        var p = new LogrosCometas.Progreso();
        Assert.IsTrue(LogrosCometas.Aplicar(p, 2, 1000, "z").TrueForAll(d => d.EsCometa));
    }

    [Test, Category("US 238"), Description("CA2: umbrales de combate con lo de los dos modos, una sola vez")]
    public void Umbrales_de_combate()
    {
        var p = new LogrosCometas.Progreso();
        var e = new EstadisticasJugador.Totales();
        e.tactico.bajas = 6; e.deathmatch.bajas = 4; e.tactico.cabezas = 10; e.mejorRacha = 5;

        var nuevos = LogrosCometas.AplicarEstadisticas(p, e, "k");
        Assert.AreEqual(3, nuevos.Count);
        Assert.IsTrue(nuevos.Exists(d => d.id == "bajas_10"), "6 + 4 bajas entre los dos modos");
        Assert.IsTrue(nuevos.Exists(d => d.id == "cabezas_10"));
        Assert.IsTrue(nuevos.Exists(d => d.id == "racha_5"));
        Assert.AreEqual(0, LogrosCometas.AplicarEstadisticas(p, e, "l").Count, "no se repiten");

        e.tactico.victorias = 1; e.mvp = 1;
        Assert.AreEqual(2, LogrosCometas.AplicarEstadisticas(p, e, "m").Count, "primera victoria y primer MVP");
    }

    [Test, Category("US 226"), Category("US 238"), Description("Cada logro y cada estadística existen en logros.sql")]
    public void Cada_logro_existe_en_la_base()
    {
        string sql = File.ReadAllText(Path.Combine(Application.dataPath, "../../Backend/Supabase/logros.sql"));
        var e = new EstadisticasJugador.Totales();
        foreach (var d in LogrosCometas.Catalogo)
        {
            StringAssert.Contains("('" + d.id + "',", sql, "falta el logro en logros.sql: " + d.id);
            if (d.EsCometa) continue;
            Assert.DoesNotThrow(() => LogrosCometas.ValorEstadistica(d.estadistica, e), "estadística desconocida: " + d.estadistica);
            StringAssert.Contains("when '" + d.estadistica + "' then", sql, "falta en rw_stat_value: " + d.estadistica);
        }
    }

    [TestCase("sb_secret_fake", false)]
    [TestCase("eyJ.legacy.jwt", false)]
    [TestCase("sb_publishable_example", true)]
    [Category("US 226"), Description("El juego nunca acepta una clave secreta de Supabase")]
    public void Solo_acepta_la_clave_publicable(string clave, bool valida)
    {
        var config = ScriptableObject.CreateInstance<LogrosOnlineConfig>();
        try
        {
            config.habilitado = true;
            config.url = "https://example.supabase.co";
            config.clavePublica = clave;
            Assert.AreEqual(valida, config.Valido);
        }
        finally { UnityEngine.Object.DestroyImmediate(config); }
    }
}
