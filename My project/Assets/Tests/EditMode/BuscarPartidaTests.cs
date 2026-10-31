using NUnit.Framework;
using Arranque = Multijugador.Arranque;

// Buscar partida (F28, US 276): cuándo arranca sola la partida, sin conexión. Multijugador.QueHacer es lo que decide el
// anfitrión de la sala pública en cada cuadro, con la hora del servidor guardada en la sala.

public class BuscarPartidaTests
{
    [Test, Category("US 276"), Description("CA1: con 8 jugadores empieza la cuenta regresiva, sin esperar")]
    public void Sala_llena()
    {
        Assert.AreEqual(Arranque.EmpezarCuenta, Multijugador.QueHacer(8, 2, false, 0f, 0f));
        Assert.AreEqual(Arranque.EmpezarCuenta, Multijugador.QueHacer(8, 4, false, 3f, 0f));
        Assert.AreEqual(Arranque.Nada, Multijugador.QueHacer(7, 2, false, 10f, 0f), "7 todavía espera al mínimo de 45 s");
    }

    [Test, Category("US 276"), Description("CA2: con el mínimo, la cuenta empieza si pasan 45 s sin que entre nadie")]
    public void Con_el_minimo()
    {
        if (!PruebaSolo.Activa) // con Riftwalker > Prueba solo prendida, alcanza con uno
        {
            Assert.AreEqual(2, Multijugador.MinimoPara(GameMode.Deathmatch));
            Assert.AreEqual(4, Multijugador.MinimoPara(GameMode.Tactico), "2 contra 2");
        }
        Assert.AreEqual(Arranque.Nada, Multijugador.QueHacer(2, 2, false, 44.9f, 0f), "todavía no pasaron 45 s");
        Assert.AreEqual(Arranque.EmpezarCuenta, Multijugador.QueHacer(2, 2, false, 45f, 0f));
        Assert.AreEqual(Arranque.Nada, Multijugador.QueHacer(3, 4, false, 120f, 0f), "en Táctico, 3 no alcanzan");
        Assert.AreEqual(Arranque.EmpezarCuenta, Multijugador.QueHacer(4, 4, false, 45f, 0f));
    }

    [Test, Category("US 276"), Description("CA3: si sale alguien y quedan menos que el mínimo, la cuenta se cancela")]
    public void Durante_la_cuenta()
    {
        Assert.AreEqual(Arranque.CancelarCuenta, Multijugador.QueHacer(1, 2, true, 0f, 5f));
        Assert.AreEqual(Arranque.CancelarCuenta, Multijugador.QueHacer(3, 4, true, 0f, 9f));
        Assert.AreEqual(Arranque.Nada, Multijugador.QueHacer(5, 4, true, 0f, 6f), "si entra alguien sigue la cuenta");
    }

    [Test, Category("US 276"), Description("CA1 y CA5: a los 10 s de la cuenta empieza la partida, sin que nadie toque «Iniciar»")]
    public void Fin_de_la_cuenta()
    {
        Assert.AreEqual(Arranque.Nada, Multijugador.QueHacer(6, 2, true, 0f, 9.9f));
        Assert.AreEqual(Arranque.Iniciar, Multijugador.QueHacer(6, 2, true, 0f, 10f));
        Assert.AreEqual(Arranque.Iniciar, Multijugador.QueHacer(8, 4, true, 0f, 12f));
    }
}
