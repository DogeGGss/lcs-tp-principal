using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Tests que crean objetos de Unity (jugador, billetera, tienda) en una escena vacía.
// [UnityTest] con "yield return null" espera un cuadro: ahí corre el Start() de cada componente.

public class BilleteraTests
{
    private GameObject go;
    private PlayerWallet w;

    [SetUp] public void Crear() { go = new GameObject("Jugador"); w = go.AddComponent<PlayerWallet>(); }
    [TearDown] public void Limpiar() { Object.DestroyImmediate(go); }

    [Test, Category("US 135"), Description("CA1: arranca con $ 800")]
    public void Empieza_con_800() => Assert.AreEqual(800, w.Money);

    [Test, Category("US 135")]
    public void Cobrar_suma()
    {
        w.Add(300);
        Assert.AreEqual(1100, w.Money);
    }

    [Test, Category("US 135"), Description("CA7: nadie puede tener más de $ 9.000")]
    public void Tope_de_9000()
    {
        w.Add(100000);
        Assert.AreEqual(9000, w.Money);
    }

    [Test, Category("US 135")]
    public void No_se_puede_gastar_mas_de_lo_que_se_tiene()
    {
        Assert.IsFalse(w.Spend(801));
        Assert.AreEqual(800, w.Money);
    }

    [Test, Category("US 135")]
    public void Gastar_resta_y_avisa()
    {
        int avisado =  1;
        w.MoneyChanged += m => avisado = m;
        Assert.IsTrue(w.Spend(300));
        Assert.AreEqual(500, w.Money);
        Assert.AreEqual(500, avisado);
    }

    [Test, Category("US 032"), Description("CA6: muerte súbita, $ 5.000 para todos")]
    public void Set_deja_la_plata_en_un_monto_fijo()
    {
        w.Set(5000);
        Assert.AreEqual(5000, w.Money);
        w.Set( 5);
        Assert.AreEqual(0, w.Money);
    }
}

public class VidaTests
{
    private GameObject go;
    private HealthSystem h;

    [UnitySetUp]
    public IEnumerator Preparar()
    {
        go = new GameObject("Jugador");
        h = go.AddComponent<HealthSystem>();
        yield return null; // Start(): vida 100, escudo 0
    }

    [TearDown] public void Limpiar() { Object.DestroyImmediate(go); }

    [Test, Category("US 029")]
    public void Arranca_con_100_de_vida_y_sin_escudo()
    {
        Assert.AreEqual(100, h.currentHealth);
        Assert.AreEqual(0, h.currentShield);
    }

    [Test, Category("US 029")]
    public void El_danio_baja_la_vida()
    {
        h.TakeDamage(30);
        Assert.AreEqual(70, h.currentHealth);
    }

    [Test, Category("US 080"), Description("El escudo absorbe primero y lo que sobra va a la vida")]
    public void El_escudo_absorbe_primero()
    {
        h.AddShield(50);
        h.TakeDamage(30);
        Assert.AreEqual(20, h.currentShield);
        Assert.AreEqual(100, h.currentHealth);
        h.TakeDamage(30);
        Assert.AreEqual(0, h.currentShield);
        Assert.AreEqual(90, h.currentHealth);
    }

    [Test, Category("US 029"), Description("CA4: la vida nunca baja de 0")]
    public void La_vida_no_baja_de_cero_y_avisa_la_muerte()
    {
        bool murio = false;
        h.Died += () => murio = true;
        h.TakeDamage(500);
        Assert.AreEqual(0, h.currentHealth);
        Assert.IsTrue(murio);
    }

    [Test, Category("US 137"), Description("TC 818 CA3: invulnerable no recibe daño")]
    public void Invulnerable_no_recibe_danio()
    {
        h.Invulnerable = true;
        h.TakeDamage(50);
        Assert.AreEqual(100, h.currentHealth);
    }

    [Test, Category("US 165")]
    public void WouldDie_avisa_si_el_golpe_mata()
    {
        Assert.IsTrue(h.WouldDie(100));
        Assert.IsFalse(h.WouldDie(99));
        h.AddShield(50);
        Assert.IsFalse(h.WouldDie(100));
        Assert.IsTrue(h.WouldDie(150));
    }

    [Test, Category("US 030")]
    public void Revive_deja_vida_completa_y_sin_escudo()
    {
        h.AddShield(50);
        h.TakeDamage(500);
        h.Revive();
        Assert.AreEqual(100, h.currentHealth);
        Assert.AreEqual(0, h.currentShield);
    }
}

public class TiendaTests
{
    private GameObject go;
    private HealthSystem vida;
    private PlayerLoadout tienda;
    private PlayerWallet plata;
    private readonly System.Collections.Generic.List<Object> creados = new System.Collections.Generic.List<Object>();

    [UnitySetUp]
    public IEnumerator Preparar()
    {
        MatchSettings.Mode = GameMode.Tactico;
        go = new GameObject("Jugador");
        vida = go.AddComponent<HealthSystem>();
        tienda = go.AddComponent<PlayerLoadout>(); // agrega solo la billetera (RequireComponent)
        plata = go.GetComponent<PlayerWallet>();
        yield return null; // Start()
    }

    [TearDown]
    public void Limpiar()
    {
        MatchSettings.Mode = GameMode.Tactico;
        Object.DestroyImmediate(go);
        foreach (Object o in creados) if (o != null) Object.DestroyImmediate(o);
        creados.Clear();
    }

    private ShopItem Item(ShopItemKind tipo, int precio, int escudo = 0, int maxCarry = 1)
    {
        ShopItem i = ScriptableObject.CreateInstance<ShopItem>();
        creados.Add(i);
        i.displayName = tipo.ToString();
        i.kind = tipo;
        i.price = precio;
        i.shieldPoints = escudo;
        i.maxCarry = maxCarry;
        return i;
    }

    [Test, Category("US 076"), Category("US 077")]
    public void Compra_un_arma_si_alcanza_la_plata()
    {
        ShopItem fusil = Item(ShopItemKind.PrimaryWeapon, 2900);
        plata.Set(9000);
        Assert.AreEqual(ShopResult.Ok, tienda.Buy(fusil));
        Assert.AreSame(fusil, tienda.Primary);
        Assert.AreEqual(6100, plata.Money);
    }

    [Test, Category("US 077")]
    public void Sin_plata_no_compra_y_no_pierde_nada()
    {
        ShopItem fusil = Item(ShopItemKind.PrimaryWeapon, 2900);
        Assert.AreEqual(ShopResult.NotEnoughMoney, tienda.Buy(fusil));
        Assert.IsNull(tienda.Primary);
        Assert.AreEqual(800, plata.Money);
    }

    [Test, Category("US 077")]
    public void No_se_compra_dos_veces_la_misma_arma()
    {
        ShopItem pistola = Item(ShopItemKind.SecondaryWeapon, 500);
        Assert.AreEqual(ShopResult.Ok, tienda.Buy(pistola));
        Assert.AreEqual(ShopResult.AlreadyEquipped, tienda.Buy(pistola));
    }

    [Test, Category("US 076"), Description("Vender lo comprado en esta fase devuelve la plata")]
    public void Vender_devuelve_la_plata()
    {
        ShopItem fusil = Item(ShopItemKind.PrimaryWeapon, 700);
        tienda.Buy(fusil);
        Assert.AreEqual(100, plata.Money);
        Assert.AreEqual(ShopResult.Ok, tienda.Sell(fusil));
        Assert.AreEqual(800, plata.Money);
        Assert.IsNull(tienda.Primary);
    }

    [Test, Category("US 076"), Description("CA10: Deshacer devuelve todo lo comprado")]
    public void Deshacer_devuelve_todo()
    {
        plata.Set(5000);
        tienda.Buy(Item(ShopItemKind.PrimaryWeapon, 1000));
        tienda.Buy(Item(ShopItemKind.Shield, 500, escudo: 25));
        Assert.IsTrue(tienda.HasPurchases);
        tienda.UndoPurchases();
        Assert.AreEqual(5000, plata.Money);
        Assert.IsNull(tienda.Primary);
        Assert.AreEqual(0, tienda.Shield);
    }

    [Test, Category("US 080"), Description("CA3: cambiar de arma en la misma fase reembolsa la anterior")]
    public void Cambiar_de_arma_reembolsa_la_anterior()
    {
        plata.Set(5000);
        tienda.Buy(Item(ShopItemKind.PrimaryWeapon, 1000));
        ShopItem b = Item(ShopItemKind.PrimaryWeapon, 2000);
        tienda.Buy(b);
        Assert.AreSame(b, tienda.Primary);
        Assert.AreEqual(3000, plata.Money);
    }

    [Test, Category("US 080")]
    public void El_escudo_se_pone_y_no_se_compra_si_ya_esta_lleno()
    {
        ShopItem escudo = Item(ShopItemKind.Shield, 1000, escudo: 50);
        plata.Set(5000);
        Assert.AreEqual(ShopResult.Ok, tienda.Buy(escudo));
        Assert.AreEqual(50, vida.currentShield);
        Assert.AreEqual(ShopResult.ShieldFull, tienda.Buy(escudo));
    }

    [Test, Category("US 079"), Description("Máximo de granadas por tipo")]
    public void Granadas_respetan_el_maximo()
    {
        ShopItem humo = Item(ShopItemKind.Grenade, 200, maxCarry: 1);
        Assert.AreEqual(ShopResult.Ok, tienda.Buy(humo));
        Assert.AreEqual(1, tienda.Count(humo));
        Assert.AreEqual(ShopResult.MaxReached, tienda.Buy(humo));
    }

    [Test, Category("US 076"), Description("Al morir se pierde el equipo pero no la plata")]
    public void Al_morir_pierde_el_equipo_y_conserva_la_plata()
    {
        plata.Set(5000);
        tienda.Buy(Item(ShopItemKind.PrimaryWeapon, 1000));
        vida.TakeDamage(1000);
        Assert.IsNull(tienda.Primary);
        Assert.AreEqual(4000, plata.Money);
    }

    [UnityTest, Category("US 076"), Description("CA9: terminada la fase de compra no se puede comprar")]
    public IEnumerator Terminada_la_fase_de_compra_no_se_compra()
    {
        GameObject fase = new GameObject("Fase");
        BuyPhase compra = fase.AddComponent<BuyPhase>();
        yield return null;
        compra.Terminar();
        ShopItem fusil = Item(ShopItemKind.PrimaryWeapon, 100);
        Assert.AreEqual(ShopResult.BuyPhaseOver, tienda.Buy(fusil));
        Object.DestroyImmediate(fase); // así BuyPhase.Current vuelve a null
    }

    [Test, Category("US 138"), Description("Deathmatch: todo gratis, solo armas")]
    public void En_deathmatch_las_armas_son_gratis_y_no_hay_escudos()
    {
        MatchSettings.Mode = GameMode.Deathmatch;
        ShopItem fusil = Item(ShopItemKind.PrimaryWeapon, 2900);
        Assert.AreEqual(ShopResult.Ok, tienda.Buy(fusil));
        Assert.AreEqual(800, plata.Money);
        Assert.AreEqual(ShopResult.NotSellable, tienda.Buy(Item(ShopItemKind.Shield, 1000, escudo: 50)));
    }
}
