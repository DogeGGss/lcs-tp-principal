using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;

// Lo que se sube a la nube (US 162, CA3). ProgresoNube arma el documento con el progreso del jugador; las opciones y las
// monedas se quedan en la PC. Corre con el guardado de prueba (PruebasAisladas): las monedas que suma no son reales.

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
}
