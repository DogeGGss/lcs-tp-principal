using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// F29, US 394: la carta Escolta (el dron).
public class EscoltaTests
{
    private static CartaDeInvasion Escolta() =>
        new List<CartaDeInvasion>(CartaDeInvasion.Todas).Find(c => c.nombre == EscoltaDeInvasion.Carta);

    [Test, Category("US 394"), Description("CA1: legendaria, de una sola vez y rinde con cualquier arma")]
    public void CA1_La_carta()
    {
        CartaDeInvasion c = Escolta();
        Assert.IsNotNull(c, "la Escolta está entre las cartas");
        Assert.AreEqual(RarezaDeCarta.Legendaria, c.rareza);
        Assert.IsTrue(c.unaVez, "no se acumula");
        Assert.AreEqual(1, c.Maximo);
        Assert.AreEqual("Todas", c.rindeCon);
        Assert.IsTrue(L10n.TrySourceKey(c.nombre, out string clave), "el nombre está en los catálogos");
        Assert.AreEqual("gameplay.card.escort", clave);
        Assert.IsTrue(L10n.TrySourceKey(c.efecto, out string efecto), "el efecto está en los catálogos");
        Assert.AreEqual("gameplay.card.fx.escort", efecto);
    }

    [Test, Category("US 394"), Description("CA3: el rayo arranca en 40 por segundo y llega a 80 a los 3 s")]
    public void CA3_El_rayo_carga()
    {
        Assert.AreEqual(40f, DronEscolta.DanioPorSegundo(0f), 0.01f);
        Assert.AreEqual(60f, DronEscolta.DanioPorSegundo(1.5f), 0.01f);
        Assert.AreEqual(80f, DronEscolta.DanioPorSegundo(3f), 0.01f);
        Assert.AreEqual(80f, DronEscolta.DanioPorSegundo(30f), 0.01f, "no pasa de 80");
        Assert.AreEqual(20f, DronEscolta.Alcance);
    }

    [Test, Category("US 394"), Description("CA4: los arcos van al grupo más grande y saltan hasta 4 invasores a menos de 6 m")]
    public void CA4_Los_arcos()
    {
        var lugares = new List<Vector3>
        {
            new Vector3(0, 0, 0),                                       // 0: solo
            new Vector3(20, 0, 0), new Vector3(23, 0, 0), new Vector3(26, 0, 0), // 1 a 3: en fila, a 3 m
            new Vector3(29, 0, 0), new Vector3(32, 0, 0),               // 4 y 5: siguen la fila
            new Vector3(50, 0, 0),                                      // 6: lejos
        };
        List<int> cadena = DronEscolta.Cadena(lugares, DronEscolta.ArcosSaltos, DronEscolta.ArcosEntre);
        Assert.AreEqual(4, cadena.Count, "hasta 4");
        Assert.IsFalse(cadena.Contains(0), "el que está solo no");
        Assert.IsFalse(cadena.Contains(6), "el lejano no");
        CollectionAssert.AllItemsAreUnique(cadena);
        for (int i = 1; i < cadena.Count; i++)
            Assert.LessOrEqual(Vector3.Distance(lugares[cadena[i - 1]], lugares[cadena[i]]), DronEscolta.ArcosEntre, "cada salto, a menos de 6 m");

        CollectionAssert.AreEqual(new[] { 0 }, DronEscolta.Cadena(new[] { Vector3.zero }, 4, 6f), "con uno solo, le pega a ese");
        Assert.AreEqual(0, DronEscolta.Cadena(new Vector3[0], 4, 6f).Count, "sin nadie, nada");
    }

    [Test, Category("US 394"), Description("CA4: 35 de daño y 1,5 s de parálisis cada 8 s")]
    public void CA4_Numeros_de_los_arcos()
    {
        Assert.AreEqual(8f, DronEscolta.ArcosCada);
        Assert.AreEqual(35, DronEscolta.ArcosDanio);
        Assert.AreEqual(1.5f, DronEscolta.ArcosParaliza);
        Assert.AreEqual(4, DronEscolta.ArcosSaltos);
    }

    [Test, Category("US 394"), Description("CA7: el modelo del dron está en Resources")]
    public void CA7_El_modelo()
    {
        Assert.IsNotNull(Resources.Load<GameObject>("Escolta/Escolta"), "Resources/Escolta/Escolta.obj");
        foreach (string m in new[] { "EscoltaCasco", "EscoltaTapa", "EscoltaAro", "EscoltaOjo", "EscoltaLuces", "EscoltaAdentro" })
            Assert.IsNotNull(Resources.Load<Material>("Escolta/" + m), m);
        Assert.IsNotNull(Resources.Load<Material>("Escolta/EscoltaCasco").GetTexture("_BaseMap"), "el casco con las franjas celestes");
        foreach (string s in new[] { "Zumbido", "Rayo", "Arcos1", "Arcos2", "Arcos3" })
            Assert.IsNotNull(Resources.Load<AudioClip>("Escolta/Sonidos/" + s), "el sonido " + s);
    }
}
