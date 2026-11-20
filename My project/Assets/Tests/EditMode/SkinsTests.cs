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
        int total = 0;
        foreach (CatalogoDeSkins.Arma a in CatalogoDeSkins.Armas)
        {
            int deEsta = a.cuerpoACuerpo ? a.disenos.Count : porArma; // el cuchillo, solo sus diseños
            Assert.AreEqual(deEsta, CatalogoDeSkins.DelArma(a.id).Count, a.nombre);
            total += deEsta;
        }
        Assert.AreEqual(total, CatalogoDeSkins.Skins.Count);
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
        var modelos = Resources.Load<ModelosDeArmas>("Skins/ModelosDeArmas");
        foreach (CatalogoDeSkins.Diseno d in CatalogoDeSkins.Disenos)
        {
            Assert.IsNotNull(modelos.MaterialDe(d.id), d.id);
            foreach (CatalogoDeSkins.Extra x in d.extras)
                if (!string.IsNullOrEmpty(x.material)) Assert.IsNotNull(modelos.MaterialDe(x.material), x.material);
        }
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

    [Test, Category("US 378"), Description("CA2: el mármol lleva el cuerpo de mármol negro y las franjas de oro")]
    public void Las_raras_tienen_sus_otras_partes()
    {
        CatalogoDeSkins.Skin marmol = CatalogoDeSkins.De("mitre-marmol-de-retiro");
        Assert.AreEqual(PerfilDatos.Rareza.Raro, marmol.rareza);
        Assert.AreEqual("marmol-negro", CatalogoDeSkins.ExtraPara(marmol, "Black").material);
        Assert.AreEqual("oro", CatalogoDeSkins.ExtraPara(marmol, "Main (Instance)").material);
        Assert.AreEqual("marmol-negro", CatalogoDeSkins.ExtraPara(marmol, "Grey").material); // los detalles, como el cuerpo
        Assert.IsNull(CatalogoDeSkins.De("mitre-oro-pulido"));
        Assert.IsNull(CatalogoDeSkins.De("mitre-fibra-de-carbono"));
        Assert.IsNull(CatalogoDeSkins.De("mitre-cobre-oxidado"));
    }

    [Test, Category("US 381"), Description("Dragón Abisal: legendaria, con su llamarada, su estela con brasas y sus materiales")]
    public void Dragon_abisal_es_legendaria()
    {
        CatalogoDeSkins.Skin s = CatalogoDeSkins.De("mitre-dragon-abisal");
        Assert.IsNotNull(s);
        Assert.AreEqual(PerfilDatos.Rareza.Legendario, s.rareza);
        CatalogoDeSkins.Efectos e = EfectosDeSkin.De(s);
        Assert.IsNotNull(e);
        Assert.IsTrue(e.llamarada && e.brasas);
        Assert.IsNull(EfectosDeSkin.De(CatalogoDeSkins.De("mitre-codigo-binario"))); // las demás, los efectos de siempre
        var modelos = Resources.Load<ModelosDeArmas>("Skins/ModelosDeArmas");
        Assert.IsNotNull(modelos.MaterialDe("efecto-llamarada"));
        Assert.IsNotNull(modelos.MaterialDe("efecto-brasa"));
    }

    [Test, Category("US 381"), Description("Tormenta: legendaria, con el tiro como un rayo, chispas, arquitos y sus materiales")]
    public void Tormenta_es_legendaria()
    {
        CatalogoDeSkins.Skin s = CatalogoDeSkins.De("la-portena-tormenta");
        Assert.IsNotNull(s);
        Assert.AreEqual(PerfilDatos.Rareza.Legendario, s.rareza);
        CatalogoDeSkins.Efectos e = EfectosDeSkin.De(s);
        Assert.IsNotNull(e);
        Assert.IsTrue(e.rayo && e.arcos && e.chispas);
        Assert.IsFalse(e.llamarada || e.humo);
        var modelos = Resources.Load<ModelosDeArmas>("Skins/ModelosDeArmas");
        foreach (string id in new[] { "tormenta", "tormenta-nubes", "tormenta-corriente", "efecto-rayo" })
            Assert.IsNotNull(modelos.MaterialDe(id), id);
    }

    [Test, Category("US 382"), Description("Cápsulas: las skins son un tipo de premio más (20 %, US 392) y hay de todas las rarezas")]
    public void Las_capsulas_traen_skins()
    {
        int total = 0;
        foreach (int p in PerfilDatos.ProbabilidadTipo) total += p;
        Assert.AreEqual(100, total);
        Assert.AreEqual(20, PerfilDatos.ProbabilidadTipo[PerfilDatos.TipoSkin]);
        foreach (PerfilDatos.Rareza r in new[] { PerfilDatos.Rareza.Comun, PerfilDatos.Rareza.Raro, PerfilDatos.Rareza.Epico, PerfilDatos.Rareza.Legendario })
            Assert.IsTrue(CatalogoDeSkins.Skins.Exists(s => s.rareza == r), r.ToString());
        // Los últimos premios guardan las skins con su clave, sin confundirlas con un cosmético del Perfil.
        Assert.AreEqual("mitre-tormenta", PerfilDatos.SkinDeClave(PerfilDatos.ClaveSkin + "mitre-tormenta").id);
        Assert.IsNull(PerfilDatos.DesdeClave(PerfilDatos.ClaveSkin + "mitre-tormenta"));
        Assert.IsNull(PerfilDatos.SkinDeClave("av:astra-pop"));
    }

    [Test, Category("US 384"), Description("Épicas nuevas: Próxima estación y Mapa del subte, en todas las armas y con sus materiales")]
    public void Proxima_estacion_y_mapa_del_subte_son_epicas()
    {
        foreach (CatalogoDeSkins.Arma a in CatalogoDeSkins.Armas)
            foreach (string id in new[] { "proxima-estacion", "mapa-del-subte" })
            {
                if (a.cuerpoACuerpo) continue; // (el cuchillo tiene sus propios diseños)
                CatalogoDeSkins.Skin s = CatalogoDeSkins.De(a.id + "-" + id);
                Assert.IsNotNull(s, a.id + "-" + id);
                Assert.AreEqual(PerfilDatos.Rareza.Epico, s.rareza);
                Assert.IsNull(EfectosDeSkin.De(s)); // las épicas, con los efectos de disparo de siempre
            }
        var modelos = Resources.Load<ModelosDeArmas>("Skins/ModelosDeArmas");
        foreach (string id in new[] { "proxima-estacion", "proxima-estacion-franjas", "mapa-del-subte", "mapa-franjas" })
            Assert.IsNotNull(modelos.MaterialDe(id), id);
    }

    [Test, Category("F35"), Description("El cuchillo: solo legendarias (el Dragón Abisal y las suyas), cada una con su material y sus partículas")]
    public void El_cuchillo_tiene_sus_legendarias()
    {
        List<CatalogoDeSkins.Skin> delCuchillo = CatalogoDeSkins.DelArma("cuchillo");
        Assert.AreEqual(6, delCuchillo.Count);
        var modelosDelCuchillo = Resources.Load<ModelosDeArmas>("Skins/ModelosDeArmas");
        foreach (CatalogoDeSkins.Skin k in delCuchillo)
        {
            Assert.AreEqual(PerfilDatos.Rareza.Legendario, k.rareza, k.id);
            Assert.AreNotEqual(CatalogoDeSkins.Particulas.Ninguna, k.diseno.particulas, k.id);
            Assert.IsNotNull(modelosDelCuchillo.MaterialDe(CatalogoDeSkins.ExtraPara(k, "M_Knife").material), k.id);
        }
        foreach (string id in new[] { "hoja-de-plasma", "rift-abierto", "cristal-de-hielo", "sangre-de-invasor", "holograma" })
            Assert.IsNotNull(CatalogoDeSkins.De("cuchillo-" + id), id);
        CatalogoDeSkins.Skin s = CatalogoDeSkins.De("cuchillo-dragon-abisal");
        Assert.IsNotNull(s);
        Assert.AreEqual("Cuchillo · Dragón Abisal", s.nombre);
        Assert.AreEqual(PerfilDatos.Rareza.Legendario, s.rareza);
        Assert.IsNull(CatalogoDeSkins.De("cuchillo-rift")); // sin las comunes
        Assert.AreEqual("dragon-cuchillo", CatalogoDeSkins.ExtraPara(s, "M_Knife").material);
        Assert.IsNull(CatalogoDeSkins.ExtraPara(s, "White"));
        Assert.IsNull(EfectosDeSkin.De(s)); // (no dispara: sin fogonazo ni estela)
        Assert.AreEqual(CatalogoDeSkins.Particulas.Brasas, s.diseno.particulas);
        Assert.AreEqual(CatalogoDeSkins.Particulas.Ninguna, CatalogoDeSkins.De("mitre-dragon-abisal").diseno.particulas); // las armas de fuego, no
        Assert.AreEqual(OrigenDeCosmeticos.Origen.Capsulas, OrigenDeCosmeticos.De(s)); // como el de las armas de fuego
        var modelos = Resources.Load<ModelosDeArmas>("Skins/ModelosDeArmas");
        Assert.IsNotNull(modelos.De("cuchillo"));
        Assert.IsNotNull(modelos.MaterialDe("dragon-cuchillo"));
        Assert.IsNotNull(modelos.MaterialDe("efecto-brasa"));
        // La katana (US 395), que va en el lugar del cuchillo, no lleva su skin.
        var katana = ScriptableObject.CreateInstance<KatanaData>();
        Assert.IsFalse(SkinsEnLasArmas.EsElCuchillo(katana));
        Object.DestroyImmediate(katana);
    }
}
