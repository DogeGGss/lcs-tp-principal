using System.Collections.Generic;
using NUnit.Framework;

// F28 (Buscar partida), US 280: los equipos parejos por nivel del Táctico de buscar partida (EquiposTacticos.Parejos).
public class EquiposParejosTests
{
    private static int Suma(List<int> niveles, bool[] enEquipo0, bool equipo0)
    {
        int s = 0;
        for (int i = 0; i < niveles.Count; i++) if (enEquipo0[i] == equipo0) s += niveles[i];
        return s;
    }

    private static int Cuantos(bool[] enEquipo0, bool equipo0)
    {
        int n = 0;
        foreach (bool b in enEquipo0) if (b == equipo0) n++;
        return n;
    }

    [Test, Category("US 280"), Description("CA1: con 1400, 1200, 1000 y 800, quedan 1400 + 800 contra 1200 + 1000 (2200 a 2200)")]
    public void Cuatro_jugadores_quedan_parejos()
    {
        var niveles = new List<int> { 1400, 1200, 1000, 800 };
        bool[] r = EquiposTacticos.Parejos(niveles);
        Assert.AreEqual(2200, Suma(niveles, r, true));
        Assert.AreEqual(2200, Suma(niveles, r, false));
        Assert.AreEqual(r[0], r[3], "1400 y 800 van juntos");
        Assert.AreEqual(r[1], r[2], "1200 y 1000 van juntos");
    }

    [Test, Category("US 280"), Description("CA1: con 5 jugadores (1600 y cuatro de 1000), el de 1600 va en el equipo de 2: 2600 contra 3000")]
    public void Con_cinco_el_mejor_va_en_el_equipo_chico()
    {
        var niveles = new List<int> { 1000, 1600, 1000, 1000, 1000 };
        bool[] r = EquiposTacticos.Parejos(niveles);
        bool suEquipo = r[1];
        Assert.AreEqual(2, Cuantos(r, suEquipo));
        Assert.AreEqual(400, System.Math.Abs(Suma(niveles, r, true) - Suma(niveles, r, false)));
    }

    [Test, Category("US 280"), Description("CA2: con 4 quedan 2 y 2, con 5 quedan 3 y 2, con 8 quedan 4 y 4")]
    public void Misma_cantidad_o_uno_de_diferencia()
    {
        foreach (int n in new[] { 2, 3, 4, 5, 6, 7, 8 })
        {
            var niveles = new List<int>();
            for (int i = 0; i < n; i++) niveles.Add(1000 + 37 * i);
            bool[] r = EquiposTacticos.Parejos(niveles);
            int a = Cuantos(r, true), b = Cuantos(r, false);
            Assert.LessOrEqual(System.Math.Abs(a - b), 1, $"{n} jugadores: {a} y {b}");
            Assert.LessOrEqual(a, EquiposTacticos.MaxPorEquipo, $"{n} jugadores");
            Assert.LessOrEqual(b, EquiposTacticos.MaxPorEquipo, $"{n} jugadores");
        }
    }

    [Test, Category("US 280"), Description("CA3: un jugador que no publicó su nivel cuenta como 1000")]
    public void Sin_nivel_publicado_cuenta_1000()
    {
        Assert.AreEqual(1000, NivelDeJugador.De(null));
        Assert.AreEqual(1000, NivelDeJugador.Inicial);
    }
}
