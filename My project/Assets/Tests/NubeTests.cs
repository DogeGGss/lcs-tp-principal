using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;

// Lo que se sube a la nube (US 162, CA3, y US 269, CA2). ProgresoNube arma el documento con el progreso del jugador; las
// opciones se quedan en la PC y las monedas tienen su propio saldo en la base (US 272), fuera de este documento. Corre con
// el guardado de prueba (PruebasAisladas): las monedas que suma no son reales.

[PrebuildSetup(typeof(PruebasAisladas)), PostBuildCleanup(typeof(PruebasAisladas))]
public class NubeTests
{
    private static readonly string[] Progreso = { "minijuego.", "estadisticas.", "zombie.record.", "perfil." };
    private static readonly string[] NoSeSuben = { "credito", "boleto", "cristal", "moneda", "mira", "volumen", "opcion", "sensibilidad" };

    [Test, Category("US 162"), Description("TC-1021 CA3: se sube el progreso; las opciones y las monedas no")]
    public void Opciones_y_monedas_no_se_suben()
    {
        Assert.IsTrue(ModoPruebas.Activo, "tiene que correr con el guardado de prueba");
        PerfilDatos.SumarDebug(); // 1000 créditos, 10 boletos y 500 cristales
        Guardado.SetInt("Mira_Tipo", 2);
        Guardado.SetFloat("Volumen_General", 0.4f);

        ProgresoNube.Documento doc = ProgresoNube.Armar();
        var claves = new List<string>();
        var numeros = new List<ProgresoNube.Numero>(doc.contadores);
        numeros.AddRange(doc.minimos);
        foreach (ProgresoNube.Numero n in numeros) claves.Add(n.clave);
        foreach (ProgresoNube.Conjunto c in doc.conjuntos) claves.Add(c.clave);
        foreach (ProgresoNube.Texto t in doc.valores) claves.Add(t.clave);

        CollectionAssert.Contains(claves, "perfil.obtenidos", "los cosméticos obtenidos se suben");
        foreach (string clave in claves)
        {
            Assert.IsTrue(System.Array.Exists(Progreso, p => clave.StartsWith(p)), "se sube algo que no es progreso: " + clave);
            foreach (string no in NoSeSuben)
                StringAssert.DoesNotContain(no, clave.ToLowerInvariant(), "se sube: " + clave);
        }
        foreach (ProgresoNube.Numero n in numeros)
            CollectionAssert.DoesNotContain(new long[] { PerfilDatos.Creditos, PerfilDatos.Cristales, PerfilDatos.Boletos }, n.valor,
                "un número del documento coincide con las monedas: " + n.clave);
    }

    [Test, Category("US 269"), Description("CA2: las cápsulas abiertas y el pity llegan de la nube y se vuelven a subir")]
    public void Pity_en_otra_PC()
    {
        PerfilDatos.Reiniciar();
        var nube = new PerfilDatos.Nube { aperturas = 15, ultimoEpico = 12 }; // 3 cápsulas desde el último épico
        PerfilDatos.FusionarNube(nube);
        Assert.AreEqual(15, PerfilDatos.Aperturas);
        Assert.AreEqual(PerfilDatos.Garantia - 3, PerfilDatos.FaltanParaGarantia, "el pity sigue donde quedó en la otra PC");

        PerfilDatos.FusionarNube(new PerfilDatos.Nube { aperturas = 10, ultimoEpico = 4 }); // lo de una PC atrasada no baja nada
        Assert.AreEqual(15, PerfilDatos.Aperturas);
        Assert.AreEqual(PerfilDatos.Garantia - 3, PerfilDatos.FaltanParaGarantia);

        var contadores = new Dictionary<string, long>();
        foreach (ProgresoNube.Numero n in ProgresoNube.Armar().contadores) contadores[n.clave] = n.valor;
        Assert.AreEqual(15, contadores["perfil.aperturas"]);
        Assert.AreEqual(12, contadores["perfil.ultimo_epico"]);
    }

    [Test, Category("US 269"), Description("CA2: en una PC nueva, lo de entrada no pisa lo equipado en la nube")]
    public void Equipado_en_otra_PC()
    {
        PerfilDatos.Reiniciar(); // como una PC nueva: todavía no llegó nada de la nube
        Assert.IsFalse(Tiene(ProgresoNube.Armar(), "perfil.avatar"), "antes de traer la nube no se sube lo equipado");

        var nube = new PerfilDatos.Nube();
        nube.obtenidos.Add("av:astra-pop");
        nube.equipados["avatar"] = "astra-pop";
        nube.recientes.Add("av:astra-pop");
        PerfilDatos.FusionarNube(nube);
        Assert.AreEqual("astra-pop", PerfilDatos.Avatar.id, "se equipa lo de la otra PC");
        CollectionAssert.AreEqual(new[] { "av:astra-pop" }, PerfilDatos.Recientes);
        Assert.IsTrue(Tiene(ProgresoNube.Armar(), "perfil.avatar"), "con la nube ya traída, lo equipado se sube");

        PerfilDatos.Equipar(PerfilDatos.Buscar(PerfilDatos.Tipo.Avatar, "riftwalker"));
        nube.equipados["avatar"] = "astra-pop";
        PerfilDatos.FusionarNube(nube);
        Assert.AreEqual("riftwalker", PerfilDatos.Avatar.id, "lo que el jugador equipa en esta PC no se pisa");
    }

    [Test, Category("US 269"), Description("CA2: las compras y la oferta de bienvenida llegan de la otra PC")]
    public void Compras_en_otra_PC()
    {
        PerfilDatos.Reiniciar();
        var nube = new PerfilDatos.Nube();
        nube.compras.Add("2026-10-30T10:00|Bolsa de cristales|1200");
        nube.packs.Add("bienvenida");
        PerfilDatos.FusionarNube(nube);
        var compras = new List<string>();
        foreach (var (pack, precio, fecha) in PerfilDatos.Compras) compras.Add($"{fecha} {pack} {precio}");
        CollectionAssert.AreEqual(new[] { "30/10 10:00 Bolsa de cristales 1200" }, compras);
        Assert.IsFalse(PerfilDatos.PackDisponible(PerfilDatos.Packs[0]), "la oferta de bienvenida ya se compró en la otra PC");
        Assert.AreEqual(0, PerfilDatos.Creditos + PerfilDatos.Boletos + PerfilDatos.Cristales, "un perfil nuevo arranca sin monedas (US 237, CA3)");
    }

    private static bool Tiene(ProgresoNube.Documento doc, string clave) => doc.valores.Exists(t => t.clave == clave);
}
