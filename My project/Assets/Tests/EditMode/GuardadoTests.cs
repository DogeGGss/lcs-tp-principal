using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

// Guardado del jugador (US 159, US 162 y US 163). Cada prueba usa una carpeta temporal propia y una clave de PlayerPrefs
// que se borra al terminar: el progreso real del jugador no se toca.

public class GuardadoTests
{
    private const string ClavePrefs = "Prueba.GuardadoTests.Entero";
    private string carpeta;
    private string ruta;

    [SetUp]
    public void Preparar()
    {
        carpeta = Path.Combine(Path.GetTempPath(), "riftwalker-guardado-" + Guid.NewGuid());
        Directory.CreateDirectory(carpeta);
        ruta = Path.Combine(carpeta, "jugador.json");
    }

    [TearDown]
    public void Limpiar()
    {
        PlayerPrefs.DeleteKey(ClavePrefs);
        if (Directory.Exists(carpeta)) Directory.Delete(carpeta, true);
    }

    private Guardado.Almacen Abrir(string archivo = null, bool migrar = false)
    {
        var a = new Guardado.Almacen(archivo ?? ruta, migrar);
        a.Cargar();
        return a;
    }

    [Test, Category("US 162"), Description("Enteros, decimales y textos vuelven iguales del archivo")]
    public void Ida_y_vuelta_de_los_tres_tipos()
    {
        var a = Abrir();
        Assert.IsTrue(a.Nuevo);
        a.SetInt("n", 42); a.SetFloat("f", 0.25f); a.SetString("s", "Ñandú");
        Assert.IsTrue(a.Guardar());
        Assert.IsTrue(File.Exists(ruta));

        var b = Abrir();
        Assert.AreEqual(42, b.GetInt("n", 0));
        Assert.AreEqual(0.25f, b.GetFloat("f", 0f), 0.0001f);
        Assert.AreEqual("Ñandú", b.GetString("s", ""));
    }

    [Test, Category("US 162"), Description("Pedir una clave con otro tipo devuelve el valor por defecto, como PlayerPrefs")]
    public void Tipo_distinto_devuelve_el_valor_por_defecto()
    {
        var a = Abrir();
        a.SetInt("n", 42); a.SetString("s", "texto"); a.Guardar();
        var b = Abrir();
        Assert.AreEqual(-1, b.GetInt("s", -1));
        Assert.AreEqual("x", b.GetString("n", "x"));
    }

    [Test, Category("US 162"), Description("CA2: dos juegos abiertos guardan sin pisarse; el progreso registrado conserva el máximo")]
    public void Dos_juegos_no_se_pisan()
    {
        Guardado.RegistrarMaximo("Prueba.GuardadoTests.");
        var uno = Abrir();
        var dos = Abrir();
        uno.SetString("de-uno", "1"); uno.SetInt("Prueba.GuardadoTests.record", 5); uno.Guardar();
        dos.SetString("de-dos", "2"); dos.SetInt("Prueba.GuardadoTests.record", 3); dos.Guardar();

        var c = Abrir();
        Assert.AreEqual("1", c.GetString("de-uno", ""));
        Assert.AreEqual("2", c.GetString("de-dos", ""));
        Assert.AreEqual(5, c.GetInt("Prueba.GuardadoTests.record", 0), "el segundo juego tenía un récord menor");
    }

    [Test, Category("US 162"), Description("Borrar una clave sobrevive al guardado y a la mezcla con el archivo")]
    public void Borrar_una_clave_persiste()
    {
        var a = Abrir();
        a.SetString("de-uno", "1"); a.SetString("de-dos", "2"); a.Guardar();
        var b = Abrir();
        b.DeleteKey("de-uno"); b.Guardar();

        var c = Abrir();
        Assert.IsFalse(c.HasKey("de-uno"));
        Assert.IsTrue(c.HasKey("de-dos"));
    }

    [Test, Category("US 163"), Description("Escribir guarda primero una copia del archivo anterior")]
    public void Escritura_atomica_con_copia()
    {
        Guardado.EscribirAtomico(ruta, "primero");
        Guardado.EscribirAtomico(ruta, "segundo");
        Assert.AreEqual("segundo", File.ReadAllText(ruta));
        Assert.AreEqual("primero", File.ReadAllText(ruta + ".bak"));
    }

    [Test, Category("US 163"), Description("CA3: archivo dañado, se recupera de la copia y guarda el dañado aparte")]
    public void Archivo_danado_se_recupera_de_la_copia()
    {
        var a = Abrir();
        a.SetString("de-dos", "2"); a.Guardar();
        a.SetString("otra", "x"); a.Guardar();
        File.WriteAllText(ruta, "{ esto no es json");

        var b = Abrir();
        Assert.IsNotNull(b.Aviso, "avisa al jugador");
        Assert.IsTrue(b.Pendiente, "lo recuperado se vuelve a guardar");
        Assert.IsTrue(b.HasKey("de-dos"));
        Assert.AreEqual(1, Directory.GetFiles(carpeta, "jugador.danado-*.json").Length, "el dañado queda aparte");
    }

    [Test, Category("US 163"), Description("CA3: archivo y copia dañados, arranca de cero y avisa")]
    public void Archivo_y_copia_danados_arranca_de_cero()
    {
        var a = Abrir();
        a.SetString("de-dos", "2"); a.Guardar();
        File.WriteAllText(ruta, "{ esto no es json");
        File.WriteAllText(ruta + ".bak", "también roto");

        var b = Abrir();
        Assert.IsTrue(b.Nuevo);
        Assert.IsNotNull(b.Aviso);
        Assert.IsFalse(b.HasKey("de-dos"));
    }

    [Test, Category("US 163"), Description("CA4: un guardado de una versión más nueva no se pisa")]
    public void Formato_mas_nuevo_queda_intacto()
    {
        File.WriteAllText(ruta, "{\"version\":99,\"valores\":[{\"clave\":\"x\",\"tipo\":2,\"valor\":\"nuevo\"}]}");
        var a = Abrir();
        a.SetString("x", "viejo");
        Assert.IsTrue(a.SoloLectura);
        Assert.IsFalse(a.Guardar());
        StringAssert.Contains("nuevo", File.ReadAllText(ruta));
    }

    [Test, Category("US 162"), Description("Lo de PlayerPrefs se lee una vez y pasa al archivo")]
    public void Migracion_desde_PlayerPrefs()
    {
        PlayerPrefs.SetInt(ClavePrefs, 7);
        string migrado = Path.Combine(carpeta, "migracion.json");
        var a = Abrir(migrado, true);
        Assert.AreEqual(7, a.GetInt(ClavePrefs, 0));
        Assert.IsTrue(a.Pendiente);
        a.Guardar();
        StringAssert.Contains(ClavePrefs, File.ReadAllText(migrado));
    }

    [Test, Category("US 159"), Description("Minijuego: totales por máximo y mejor reacción por mínimo")]
    public void Estadisticas_del_minijuego_se_unen()
    {
        var mio = new EstadisticasMinijuego.Totales { disparos = 10, puntos = 300, mejorReaccionMs = 900 };
        mio.aciertos[0] = 4;
        var otro = new EstadisticasMinijuego.Totales { disparos = 7, puntos = 500, mejorReaccionMs = 700 };
        otro.aciertos[2] = 1;

        Assert.IsTrue(EstadisticasMinijuego.Unir(mio, otro));
        Assert.AreEqual(10, mio.disparos);
        Assert.AreEqual(500, mio.puntos);
        Assert.AreEqual(4, mio.aciertos[0]);
        Assert.AreEqual(1, mio.aciertos[2]);
        Assert.AreEqual(700, mio.mejorReaccionMs);
        Assert.IsFalse(EstadisticasMinijuego.Unir(mio, otro), "unir dos veces no cambia nada");
        Assert.IsTrue(EstadisticasMinijuego.Leer("basura").SinDatos);
        Assert.IsTrue(EstadisticasMinijuego.Leer("").SinDatos);
    }

    [Test, Category("US 045"), Description("Los logros guardados por dos juegos se juntan por máximo")]
    public void Logros_se_juntan_en_el_guardado()
    {
        string junto = LogrosCometas.JuntarJson("{\"version\":1,\"aciertos\":[12,0,0],\"fechas\":[\"a\"]}",
            "{\"version\":1,\"aciertos\":[3,9,0],\"fechas\":[]}");
        var p = JsonUtility.FromJson<LogrosCometas.Progreso>(junto);
        Assert.AreEqual(12, p.aciertos[0]);
        Assert.AreEqual(9, p.aciertos[1]);
    }

    [Test, Category("US 162"), Description("El documento de la nube vuelve igual del JSON que manda LogrosOnline")]
    public void Documento_de_la_nube_ida_y_vuelta()
    {
        var doc = new ProgresoNube.Documento();
        doc.contadores.Add(new ProgresoNube.Numero { clave = "minijuego.disparos", valor = 12 });
        doc.conjuntos.Add(new ProgresoNube.Conjunto { clave = "perfil.obtenidos", elementos = new[] { "av:a" } });
        doc.valores.Add(new ProgresoNube.Texto { clave = "perfil.titulo", valor = "" });

        var vuelta = JsonUtility.FromJson<ProgresoNube.Documento>(JsonUtility.ToJson(doc));
        Assert.AreEqual(12, vuelta.contadores[0].valor);
        Assert.AreEqual("av:a", vuelta.conjuntos[0].elementos[0]);
        Assert.AreEqual("", vuelta.valores[0].valor);
    }

    [Test, Description("Con RIFTWALKER_PRUEBAS=1 el juego guarda en un archivo aparte, nunca en el del jugador")]
    public void El_modo_pruebas_usa_otro_archivo()
    {
        string antes = Environment.GetEnvironmentVariable(ModoPruebas.Variable);
        try
        {
            Environment.SetEnvironmentVariable(ModoPruebas.Variable, null);
            string real = Guardado.Ruta;
            Environment.SetEnvironmentVariable(ModoPruebas.Variable, "1");
            Assert.AreEqual(Guardado.RutaPruebas, Guardado.Ruta);
            Assert.AreNotEqual(real, Guardado.Ruta);
        }
        finally { Environment.SetEnvironmentVariable(ModoPruebas.Variable, antes); }
    }
}
