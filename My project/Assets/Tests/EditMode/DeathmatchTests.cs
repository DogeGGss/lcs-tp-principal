using NUnit.Framework;

// Bajas, muertes y ganador del Deathmatch (US 140). Prueban las reglas de PartidaDeathmatch sin sala de Photon: el
// anfitrión las usa para anotar cada baja (ContarBaja) y para ordenar la tabla y elegir al ganador (Puestos).

public class DeathmatchTests
{
    private static PartidaDeathmatch.Puesto P(int actor, int bajas, int muertes, int hace) =>
        new PartidaDeathmatch.Puesto { actor = actor, bajas = bajas, muertes = muertes, hace = hace };

    [Test, Category("US 140"), Description("TC-825 CA1: eliminar a otro suma baja; caerse del mapa o matarse solo, no")]
    public void Quien_suma_la_baja()
    {
        Assert.IsTrue(PartidaDeathmatch.SumaBaja(5, 3), "eliminar a otro jugador");
        Assert.IsFalse(PartidaDeathmatch.SumaBaja(0, 3), "caerse del mapa: nadie suma");
        Assert.IsFalse(PartidaDeathmatch.SumaBaja(3, 3), "matarse solo: no suma");
    }

    [Test, Category("US 140"), Description("TC-826 CA2: gana el primero que llega a 25 bajas")]
    public void Gana_al_llegar_a_25_bajas()
    {
        Assert.IsFalse(PartidaDeathmatch.GanaPorBajas(24));
        Assert.IsTrue(PartidaDeathmatch.GanaPorBajas(25));
        Assert.AreEqual(8f * 60f, PartidaDeathmatch.DuracionCombate, "si nadie llega, se termina a los 8 minutos");
    }

    [Test, Category("US 140"), Description("TC-826 CA2: al terminar el tiempo gana el que tiene más bajas")]
    public void Mas_bajas_va_primero()
    {
        Assert.Less(PartidaDeathmatch.CompararPuestos(P(2, 12, 9, 0), P(1, 10, 0, 50000)), 0);
        Assert.Greater(PartidaDeathmatch.CompararPuestos(P(1, 10, 0, 50000), P(2, 12, 9, 0)), 0);
    }

    [Test, Category("US 140"), Description("TC-826 CA2: con las mismas bajas, gana el que tiene menos muertes")]
    public void Empate_en_bajas_desempata_por_muertes()
    {
        Assert.Less(PartidaDeathmatch.CompararPuestos(P(2, 10, 3, 0), P(1, 10, 5, 90000)), 0);
    }

    [Test, Category("US 140"), Description("TC-826 CA2: con las mismas bajas y muertes, gana el que llegó primero")]
    public void Empate_total_gana_el_que_llego_primero()
    {
        // hace: hace cuánto llegó a esas bajas. El que lleva más tiempo con ellas llegó antes.
        Assert.Less(PartidaDeathmatch.CompararPuestos(P(2, 10, 3, 40000), P(1, 10, 3, 1000)), 0);
        Assert.Less(PartidaDeathmatch.CompararPuestos(P(1, 10, 3, 1000), P(2, 10, 3, 1000)), 0, "todo igual: el orden de la sala");
        Assert.AreEqual(0, PartidaDeathmatch.CompararPuestos(P(1, 10, 3, 1000), P(1, 10, 3, 1000)));
    }

    [Test, Category("US 140"), Description("TC-826 CA2: la tabla completa queda en orden")]
    public void Ordena_la_tabla()
    {
        var tabla = new System.Collections.Generic.List<PartidaDeathmatch.Puesto>
        {
            P(1, 8, 2, 0), P(2, 14, 6, 0), P(3, 14, 4, 0), P(4, 8, 2, 30000),
        };
        tabla.Sort(PartidaDeathmatch.CompararPuestos);
        CollectionAssert.AreEqual(new[] { 3, 2, 4, 1 }, tabla.ConvertAll(p => p.actor));
    }
}
