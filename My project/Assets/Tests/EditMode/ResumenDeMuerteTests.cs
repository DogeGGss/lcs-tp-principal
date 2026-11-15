using System.Collections.Generic;
using NUnit.Framework;
using Registro = ResumenDeMuerte.Registro;

// Resumen al morir (F34, US 375). Prueban cómo se arma la tarjeta con el daño de una vida, sin partida ni red.
public class ResumenDeMuerteTests
{
    private static string Nombre(int actor) => "J" + actor;

    [Test, Category("US 375"), Description("CA3: el daño de los dos, y el recibido separado por zona")]
    public void Dano_de_los_dos()
    {
        var recibidos = new Dictionary<int, Registro> { [2] = new Registro { danio = 120, impactos = 4, cabeza = 60, cuerpo = 40, piernas = 20 } };
        var hechos = new Dictionary<int, Registro> { [2] = new Registro { danio = 84, impactos = 3, cuerpo = 84 } };
        var r = ResumenDeMuerte.Armar(new ResumenDeMuerte.Muerte { asesino = 2, nombre = "J2" }, recibidos, hechos, 120, Nombre);
        Assert.AreEqual(120, r.recibidoDelAsesino.danio);
        Assert.AreEqual(60, r.recibidoDelAsesino.cabeza);
        Assert.AreEqual(20, r.recibidoDelAsesino.piernas);
        Assert.AreEqual(84, r.hechoAlAsesino.danio);
        Assert.AreEqual(3, r.hechoAlAsesino.impactos);
        Assert.IsEmpty(r.otros);
    }

    [Test, Category("US 375"), Description("CA4: los demás que lo dañaron, de más a menos daño, sin el que lo mató")]
    public void Otros_que_lo_danaron()
    {
        var recibidos = new Dictionary<int, Registro>
        {
            [2] = new Registro { danio = 100, impactos = 3 },
            [3] = new Registro { danio = 15, impactos = 1 },
            [4] = new Registro { danio = 40, impactos = 2 },
        };
        var r = ResumenDeMuerte.Armar(new ResumenDeMuerte.Muerte { asesino = 2 }, recibidos, new Dictionary<int, Registro>(), 155, Nombre);
        Assert.AreEqual(2, r.otros.Count);
        Assert.AreEqual(("J4", 40), r.otros[0]);
        Assert.AreEqual(("J3", 15), r.otros[1]);
    }

    [Test, Category("US 375"), Description("CA5: sin asesino, la tarjeta muestra todo el daño recibido")]
    public void Sin_asesino_muestra_el_dano_recibido()
    {
        var recibidos = new Dictionary<int, Registro> { [3] = new Registro { danio = 30, impactos = 1 } };
        var r = ResumenDeMuerte.Armar(new ResumenDeMuerte.Muerte { asesino = 0, causa = "Te caíste del mapa" }, recibidos,
            new Dictionary<int, Registro>(), 130, Nombre);
        Assert.AreEqual(130, r.recibidoTotal, "la caída más el daño del jugador");
        Assert.AreEqual(0, r.recibidoDelAsesino.danio);
        Assert.AreEqual(1, r.otros.Count, "el que lo dañó antes de caerse aparece abajo");
    }

    [Test, Category("US 375"), Description("El total nunca es menor que lo que hicieron los jugadores")]
    public void Total_al_menos_lo_de_los_jugadores()
    {
        var recibidos = new Dictionary<int, Registro> { [2] = new Registro { danio = 90, impactos = 2 } };
        var r = ResumenDeMuerte.Armar(new ResumenDeMuerte.Muerte { asesino = 2 }, recibidos, new Dictionary<int, Registro>(), 0, Nombre);
        Assert.AreEqual(90, r.recibidoTotal);
    }
}
