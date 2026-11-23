using NUnit.Framework;
using UnityEngine;

// US 395: la katana (Invasión).
public class KatanaTests
{
    private static KatanaData Katana()
    {
        KatanaData k = KatanaData.Cargar();
        Assert.IsNotNull(k, "la katana está en Resources/Katana/KatanaDatos");
        return k;
    }

    [Test, Category("US 395"), Description("CA2: va en el lugar del cuchillo, con su modelo y su ícono")]
    public void CA2_En_el_lugar_del_cuchillo()
    {
        KatanaData k = Katana();
        Assert.IsTrue(k is MeleeWeaponData, "es un arma cuerpo a cuerpo");
        Assert.AreEqual("Katana", k.weaponName);
        Assert.IsNotNull(k.viewModelPrefab, "tiene modelo de primera persona");
        Assert.IsNotNull(k.viewModelPrefab.transform.Find(BrazosEnCamara.AgarreDerecho), "dice dónde va la mano derecha");
        Assert.IsNotNull(k.viewModelPrefab.transform.Find(BrazosEnCamara.AgarreIzquierdo), "dice dónde va la mano izquierda");
        Assert.IsNotNull(k.icono, "tiene ícono");
    }

    [Test, Category("US 395"), Description("CA3: un tajo en abanico de 90 de daño hasta 3 m y 100°, cada 0,9 s, un 10 % más rápido")]
    public void CA3_El_corte()
    {
        KatanaData k = Katana();
        Assert.AreEqual(90, k.damage);
        Assert.AreEqual(3f, k.range, 0.001f);
        Assert.AreEqual(100f, k.attackAngle, 0.001f);
        Assert.AreEqual(0.9f, k.cooldown, 0.001f);
        Assert.AreEqual(1.1f, k.moveSpeedMultiplier, 0.001f);
        Assert.GreaterOrEqual(k.cooldown, MeleeAttack.Total, "el corte termina antes de poder volver a atacar");
    }

    [Test, Category("US 395"), Description("CA4: el tercer golpe seguido es el tajo fuerte, al doble de daño")]
    public void CA4_El_combo()
    {
        KatanaData k = Katana();
        Assert.AreEqual(3, k.golpesDelCombo);
        Assert.AreEqual(1.2f, k.ventanaDelCombo, 0.001f);
        Assert.AreEqual(2f, k.danioDelTajoFuerte, 0.001f);
        Assert.IsFalse(KatanaData.EsTajoFuerte(1, 3));
        Assert.IsFalse(KatanaData.EsTajoFuerte(2, 3));
        Assert.IsTrue(KatanaData.EsTajoFuerte(3, 3));
        Assert.IsFalse(KatanaData.EsTajoFuerte(4, 3), "después del tajo fuerte vuelve a empezar");
        Assert.IsTrue(KatanaData.EsTajoFuerte(6, 3));
    }

    [Test, Category("US 395"), Description("CA5: la estocada sale a los 0,6 s, avanza 5 m, hace 120 y espera 6 s")]
    public void CA5_La_estocada()
    {
        KatanaData k = Katana();
        Assert.AreEqual(0.6f, k.cargaDeLaEstocada, 0.001f);
        Assert.AreEqual(5f, k.largoDeLaEstocada, 0.001f);
        Assert.AreEqual(120, k.danioDeLaEstocada);
        Assert.AreEqual(6f, k.esperaDeLaEstocada, 0.001f);
    }

    [Test, Category("US 395"), Description("CA6 y CA7: la guardia frena y camina más lento; cada baja cura 10")]
    public void CA6_CA7_Guardia_y_cura()
    {
        KatanaData k = Katana();
        Assert.Less(k.velocidadEnGuardia, 1f, "en guardia camina más lento");
        Assert.Greater(k.anguloDeLaGuardia, 0f);
        Assert.AreEqual(10, k.curaPorBaja);
        Assert.IsFalse(MeleeAttack.FrenaDisparo(null, Vector3.zero), "sin nadie en guardia no frena nada");
    }

    [Test, Category("US 395"), Description("CA9: los sonidos de la katana (las grabaciones de Resources/Katana/Sonidos)")]
    public void CA9_Sonidos()
    {
        Assert.IsNotNull(SonidosDeKatana.Corte(false));
        Assert.IsNotNull(SonidosDeKatana.Corte(true));
        Assert.IsNotNull(SonidosDeKatana.Estocada());
        Assert.IsNotNull(SonidosDeKatana.Golpe());
        Assert.IsNotNull(SonidosDeKatana.Freno());
        Assert.AreNotSame(SonidosDeKatana.Corte(false), SonidosDeKatana.Corte(false), "los tajos se turnan entre las grabaciones");
        AudioClip freno = SonidosDeKatana.Freno();
        Assert.AreNotSame(freno, SonidosDeKatana.Freno(), "el freno no repite el mismo sonido dos veces seguidas");
    }
}
