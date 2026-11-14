using NUnit.Framework;
using UnityEngine;

// F37 (Emotes coleccionables): los bailes y su rareza en el catálogo.
public class EmotesColeccionablesTests
{
    private static CatalogoDeEmotes Catalogo => Resources.Load<CatalogoDeEmotes>("Emotes");

    [Test, Category("US 387"), Description("Son 15 bailes, con su animación, y ya no está el Breakdance")]
    public void Son_15_bailes()
    {
        Assert.AreEqual(15, Catalogo.emotes.Count);
        foreach (CatalogoDeEmotes.Emote e in Catalogo.emotes)
        {
            Assert.IsNotNull(e.clip, e.estado);
            Assert.IsFalse(string.IsNullOrEmpty(e.nombre), e.estado);
        }
        Assert.IsNull(Catalogo.emotes.Find(e => e.estado == "Breakdance"));
    }

    [Test, Category("US 388"), Description("CA1 y CA2: el Saludo es el único de entrada; 5 comunes, 4 raros, 3 épicos y 2 legendarios")]
    public void Rarezas_de_los_bailes()
    {
        var cuenta = new int[5];
        foreach (CatalogoDeEmotes.Emote e in Catalogo.emotes) cuenta[(int)e.rareza]++;
        Assert.AreEqual(1, cuenta[(int)PerfilDatos.Rareza.Inicial]);
        Assert.AreEqual(PerfilDatos.Rareza.Inicial, Catalogo.emotes.Find(e => e.nombre == "Saludo").rareza);
        Assert.AreEqual(5, cuenta[(int)PerfilDatos.Rareza.Comun]);
        Assert.AreEqual(4, cuenta[(int)PerfilDatos.Rareza.Raro]);
        Assert.AreEqual(3, cuenta[(int)PerfilDatos.Rareza.Epico]);
        Assert.AreEqual(2, cuenta[(int)PerfilDatos.Rareza.Legendario]);
    }
}
