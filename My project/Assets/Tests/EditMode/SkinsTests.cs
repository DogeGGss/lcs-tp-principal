using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

// F35 (Skins de armas): el catálogo de skins (US 378).
public class SkinsTests
{
    [Test, Category("US 378"), Description("CA1 y CA5: la primera tanda, cinco combinaciones comunes en cada arma, y los diseños")]
    public void La_primera_tanda_son_comunes_en_todas_las_armas()
    {
        int porArma = CatalogoDeSkins.Combinaciones.Count + CatalogoDeSkins.Disenos.Count;
        Assert.AreEqual(CatalogoDeSkins.Armas.Count * porArma, CatalogoDeSkins.Skins.Count);
        foreach (CatalogoDeSkins.Arma a in CatalogoDeSkins.Armas)
            Assert.AreEqual(porArma, CatalogoDeSkins.DelArma(a.id).Count, a.nombre);
        foreach (CatalogoDeSkins.Skin s in CatalogoDeSkins.Skins)
            Assert.AreEqual(s.colores != null ? PerfilDatos.Rareza.Comun : s.diseno.rareza, s.rareza, s.nombre);
    }

    [Test, Category("US 378"), Description("CA2: Código binario es épica, en los paneles y el acento, con su material")]
    public void Codigo_binario_es_epica()
    {
        CatalogoDeSkins.Skin s = CatalogoDeSkins.De("mitre-codigo-binario");
        Assert.IsNotNull(s);
        Assert.AreEqual(PerfilDatos.Rareza.Epico, s.rareza);
        Assert.AreEqual("Mitre · Código binario", s.nombre);
        Assert.IsTrue(CatalogoDeSkins.EsParteDelDiseno(s, "White"));
        Assert.IsTrue(CatalogoDeSkins.EsParteDelDiseno(s, "Main (Instance)"));
        Assert.IsFalse(CatalogoDeSkins.EsParteDelDiseno(s, "Black"));
        Assert.IsNull(CatalogoDeSkins.ColorPara(s, "White"));
        foreach (CatalogoDeSkins.Diseno d in CatalogoDeSkins.Disenos)
            Assert.IsNotNull(Resources.Load<ModelosDeArmas>("Skins/ModelosDeArmas").MaterialDe(d.id), d.id);
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
        Assert.AreEqual(s.colores.cuerpo, CatalogoDeSkins.ColorPara(s, "Black"));
        Assert.AreEqual(s.colores.paneles, CatalogoDeSkins.ColorPara(s, "White"));
        Assert.AreEqual(s.colores.detalles, CatalogoDeSkins.ColorPara(s, "Grey (Instance)"));
        Assert.AreEqual(s.colores.acento, CatalogoDeSkins.ColorPara(s, "Main"));
        Assert.IsNull(CatalogoDeSkins.ColorPara(s, "Mira"));
    }

    [Test, Category("US 378"), Description("Cada arma de fuego del catálogo tiene su ficha en la tienda")]
    public void Las_fichas_existen()
    {
        foreach (CatalogoDeSkins.Arma a in CatalogoDeSkins.Armas)
            if (!string.IsNullOrEmpty(a.ficha))
                Assert.IsNotNull(UnityEditor.AssetDatabase.LoadAssetAtPath<ShopItem>("Assets/Data/Tienda/" + a.ficha + ".asset"), a.ficha);
    }
}
