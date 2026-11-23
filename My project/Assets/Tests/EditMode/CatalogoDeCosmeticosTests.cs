using System.Collections.Generic;
using NUnit.Framework;
using Origen = OrigenDeCosmeticos.Origen;
using Rareza = PerfilDatos.Rareza;
using Tipo = PerfilDatos.Tipo;

// US 392 (F36): el catálogo de las cápsulas, el pase y la tienda.
public class CatalogoDeCosmeticosTests
{
    private static IEnumerable<Premio> Todos()
    {
        foreach (PerfilDatos.Cosmetico c in PerfilDatos.Catalogo) yield return Premio.De(c);
        foreach (CatalogoDeSkins.Skin s in CatalogoDeSkins.Skins) yield return Premio.De(s);
        foreach (CatalogoDeEmotes.Emote e in CatalogoDeEmotes.Actual.emotes) yield return Premio.De(e);
    }

    [Test, Category("US 392"), Description("CA1: avatares, banners, marcos y títulos llegan como mucho a raros")]
    public void CA1_El_perfil_llega_hasta_raro()
    {
        foreach (PerfilDatos.Cosmetico c in PerfilDatos.Catalogo)
            Assert.LessOrEqual((int)c.rareza, (int)Rareza.Raro, c.tipo + " " + c.nombre);
        Assert.IsTrue(PerfilDatos.Catalogo.Exists(c => c.tipo == Tipo.Titulo && c.rareza == Rareza.Comun));
        Assert.IsTrue(PerfilDatos.Catalogo.Exists(c => c.tipo == Tipo.Titulo && c.rareza == Rareza.Raro));
    }

    [Test, Category("US 392"), Description("CA2: cada cosmético sale de un lugar, y el conjunto (avatar, banner y marco) va junto")]
    public void CA2_Un_lugar_por_cosmetico()
    {
        foreach (Premio p in Todos())
            Assert.AreEqual(p.Rareza == Rareza.Inicial, OrigenDeCosmeticos.De(p) == Origen.Inicial, p.Clave);
        foreach (PerfilDatos.Cosmetico b in PerfilDatos.Catalogo.FindAll(c => c.tipo == Tipo.Banner && c.rareza != Rareza.Inicial))
        {
            PerfilDatos.Cosmetico marco = PerfilDatos.Buscar(Tipo.Marco, b.id);
            if (marco != null) Assert.AreEqual(OrigenDeCosmeticos.De(b), OrigenDeCosmeticos.De(marco), b.id);
        }
        Assert.AreEqual(Origen.Tienda, OrigenDeCosmeticos.De(PerfilDatos.Buscar(Tipo.Avatar, "no-muerto")));
        Assert.AreEqual(Origen.Tienda, OrigenDeCosmeticos.De(PerfilDatos.Buscar(Tipo.Banner, "ultima-oleada")));
        Assert.AreEqual(Origen.Pase, OrigenDeCosmeticos.De(PerfilDatos.Buscar(Tipo.Avatar, "astra-pop")));

        // Los tres lugares tienen comunes o raros, y épicos y legendarios de skins y emotes.
        foreach (Origen o in new[] { Origen.Capsulas, Origen.Pase, Origen.Tienda })
        {
            var lista = new List<Premio>(Todos()).FindAll(p => OrigenDeCosmeticos.De(p) == o);
            Assert.IsTrue(lista.Exists(p => p.Rareza <= Rareza.Raro), o + ": comunes o raros");
            Assert.IsTrue(lista.Exists(p => p.Rareza == Rareza.Epico && p.emote != null), o + ": un emote épico");
            Assert.IsTrue(lista.Exists(p => p.Rareza == Rareza.Epico && p.skin != null), o + ": una skin épica");
            Assert.IsTrue(lista.Exists(p => p.Rareza == Rareza.Legendario), o + ": algo legendario");
        }
    }

    [Test, Category("US 392"), Description("CA3: el reparto por tipo de las cápsulas suma 100 y tiene emotes")]
    public void CA3_Las_capsulas_reparten_por_tipo()
    {
        int total = 0;
        foreach (int p in PerfilDatos.ProbabilidadTipo) total += p;
        Assert.AreEqual(100, total);
        CollectionAssert.AreEqual(new[] { 20, 15, 20, 15, 20, 10 }, PerfilDatos.ProbabilidadTipo);
        foreach (Rareza r in new[] { Rareza.Comun, Rareza.Raro, Rareza.Epico, Rareza.Legendario })
            Assert.Greater(PerfilDatos.EnLasCapsulas(r), 0, r.ToString());
    }

    [Test, Category("US 392"), Description("CA4: 40 niveles, 2 skins y un emote por página, de menor a mayor rareza")]
    public void CA4_El_pase()
    {
        IReadOnlyList<Premio> pase = OrigenDeCosmeticos.Pase;
        Assert.AreEqual(PaseDeBatalla.Niveles, pase.Count);
        Rareza ultima = Rareza.Comun;
        for (int pagina = 0; pagina < PaseDeBatalla.Paginas; pagina++)
        {
            int skins = 0, emotes = 0;
            for (int i = pagina * PaseDeBatalla.PorPagina; i < (pagina + 1) * PaseDeBatalla.PorPagina; i++)
            {
                Premio p = pase[i];
                Assert.IsNotNull(p, "nivel " + (i + 1));
                Assert.AreEqual(0, p.creditos, "el pase no da créditos (nivel " + (i + 1) + ")");
                if (!p.EsMoneda) Assert.AreEqual(Origen.Pase, OrigenDeCosmeticos.De(p), p.Clave);
                if (p.skin == null && p.emote == null) continue;
                if (p.skin != null) skins++; else emotes++;
                Assert.GreaterOrEqual((int)p.Rareza, (int)ultima, "de menor a mayor rareza (nivel " + (i + 1) + ")");
                ultima = p.Rareza;
            }
            Assert.AreEqual(2, skins, "página " + (pagina + 1));
            Assert.AreEqual(1, emotes, "página " + (pagina + 1));
        }
        Assert.AreEqual("mitre-tormenta", pase[39].skin.id);
    }

    [Test, Category("US 392"), Description("CA5: 550 cristales, 100 gratis, de a 100 y uno de 150")]
    public void CA5_Los_cristales_del_pase()
    {
        int gratis = 0, total = 0;
        for (int n = 1; n <= PaseDeBatalla.Niveles; n++)
        {
            Premio p = OrigenDeCosmeticos.Pase[n - 1];
            if (p.cristales <= 0) continue;
            Assert.IsTrue(p.cristales == 100 || p.cristales == 150, "nivel " + n);
            total += p.cristales;
            if (PaseDeBatalla.EsGratis(n)) gratis += p.cristales;
        }
        Assert.AreEqual(PaseDeBatalla.CristalesConPase, total);
        Assert.AreEqual(PaseDeBatalla.CristalesGratis, gratis);
        Assert.AreEqual(100, OrigenDeCosmeticos.Pase[10].cristales, "los gratis, en el nivel 11");
    }

    [Test, Category("US 392"), Description("CA6: las ofertas son de la tienda, con una skin y un emote")]
    public void CA6_Las_ofertas_de_la_tienda()
    {
        for (int dia = 20261116; dia < 20261146; dia++)
        {
            List<Premio> ofertas = PantallaTiendaDirecta.OfertasDe(dia);
            Assert.AreEqual(8, ofertas.Count, dia.ToString());
            Assert.IsTrue(ofertas.Exists(p => p.skin != null), dia + ": una skin");
            Assert.IsTrue(ofertas.Exists(p => p.emote != null), dia + ": un emote");
            foreach (Premio p in ofertas) Assert.AreEqual(Origen.Tienda, OrigenDeCosmeticos.De(p), p.Clave);
        }
    }

    [Test, Category("US 392"), Description("CA9: dónde se consigue cada cosmético")]
    public void CA9_Donde_se_consigue()
    {
        Assert.AreEqual(28, OrigenDeCosmeticos.NivelDelPase(Premio.ClaveEmote + "Floss"));
        Assert.AreEqual(0, OrigenDeCosmeticos.NivelDelPase(Premio.ClaveEmote + "Flair"));
        Assert.AreEqual(PestanaTienda.Pase, OrigenDeCosmeticos.PestanaDe(Premio.DesdeClave(Premio.ClaveEmote + "Floss")));
        Assert.AreEqual(PestanaTienda.Capsulas, OrigenDeCosmeticos.PestanaDe(Premio.DesdeClave(Premio.ClaveEmote + "Flair")));
        Assert.AreEqual(PestanaTienda.Tienda, OrigenDeCosmeticos.PestanaDe(Premio.DesdeClave("sk:roca-tormenta")));
        Assert.AreEqual(Origen.Pase, OrigenDeCosmeticos.De(Premio.DesdeClave("sk:mitre-tormenta")));
    }
}
