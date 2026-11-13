using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

// F35 (Skins de armas): el catálogo de skins (US 378).
public class SkinsTests
{
    [Test, Category("US 378"), Description("CA1 y CA5: la primera tanda, cinco combinaciones comunes en cada arma")]
    public void La_primera_tanda_son_comunes_en_todas_las_armas()
    {
        Assert.AreEqual(CatalogoDeSkins.Armas.Count * CatalogoDeSkins.Combinaciones.Count, CatalogoDeSkins.Skins.Count);
        foreach (CatalogoDeSkins.Arma a in CatalogoDeSkins.Armas)
            Assert.AreEqual(CatalogoDeSkins.Combinaciones.Count, CatalogoDeSkins.DelArma(a.id).Count, a.nombre);
        foreach (CatalogoDeSkins.Skin s in CatalogoDeSkins.Skins)
            Assert.AreEqual(PerfilDatos.Rareza.Comun, s.rareza, s.nombre);
    }

    [Test, Category("US 378"), Description("CA1: cada skin se llama con el arma y su nombre")]
    public void El_nombre_lleva_el_arma()
    {
        Assert.AreEqual("Mitre · Rift", CatalogoDeSkins.De("mitre-rift").nombre);
        foreach (CatalogoDeSkins.Skin s in CatalogoDeSkins.Skins) StringAssert.StartsWith(s.arma.nombre + " · ", s.nombre);
    }

    [Test, Category("US 378"), Description("CA6: los ids no se repiten y entran en las claves de la nube")]
    public void Los_ids_sirven_para_la_nube()
    {
        var vistos = new HashSet<string>();
        var clave = new Regex("^[a-z0-9_.-]{1,64}$");
        foreach (CatalogoDeSkins.Skin s in CatalogoDeSkins.Skins)
        {
            Assert.IsTrue(vistos.Add(s.id), "repetido: " + s.id);
            Assert.IsTrue(clave.IsMatch(s.id), s.id);
        }
        foreach (CatalogoDeSkins.Arma a in CatalogoDeSkins.Armas) Assert.IsTrue(clave.IsMatch("skins.equipada." + a.id), a.id);
    }

    [Test, Category("US 378"), Description("CA2: las comunes cambian solo los colores, por el nombre del material")]
    public void Cada_material_lleva_su_color()
    {
        CatalogoDeSkins.Skin s = CatalogoDeSkins.De("mitre-brasa");
        Assert.AreEqual(s.colores.main, CatalogoDeSkins.ColorPara(s, "Main"));
        Assert.AreEqual(s.colores.grey, CatalogoDeSkins.ColorPara(s, "Grey (Instance)"));
        Assert.AreEqual(s.colores.black, CatalogoDeSkins.ColorPara(s, "Black"));
        Assert.AreEqual(s.colores.white, CatalogoDeSkins.ColorPara(s, "White"));
        Assert.IsNull(CatalogoDeSkins.ColorPara(s, "Mira"));
        // El cuchillo es otro modelo: lo tiñe el acento.
        CatalogoDeSkins.Skin c = CatalogoDeSkins.De("cuchillo-brasa");
        Assert.AreEqual(c.colores.main, CatalogoDeSkins.ColorPara(c, "M_Knife"));
    }

    [Test, Category("US 378"), Description("Cada arma de fuego del catálogo tiene su ficha en la tienda")]
    public void Las_fichas_existen()
    {
        foreach (CatalogoDeSkins.Arma a in CatalogoDeSkins.Armas)
            if (!string.IsNullOrEmpty(a.ficha))
                Assert.IsNotNull(UnityEditor.AssetDatabase.LoadAssetAtPath<ShopItem>("Assets/Data/Tienda/" + a.ficha + ".asset"), a.ficha);
    }
}
