using System.Collections.Generic;
using NUnit.Framework;
using Riftwalker.Releases;
using UnityEngine;

// Tests de lógica "pura": no necesitan escena. Cada test dice en [Description] a qué TC / CA corresponde,
// así la planilla del plan de pruebas puede apuntar al nombre del test.

public class VersionesTests
{
    [Test, Category("US 201"), Description("TC 843 CA3: comparar versiones por número, no por texto")]
    public void Compara_por_numero_y_no_por_texto()
    {
        Assert.Greater(ReleaseVersion.Parse("0.1.10").CompareTo(ReleaseVersion.Parse("0.1.9")), 0);
    }

    [Test, Category("US 201")]
    public void Una_estable_es_mayor_que_su_experimental()
    {
        Assert.Greater(ReleaseVersion.Parse("0.2.0").CompareTo(ReleaseVersion.Parse("0.2.0 exp.10")), 0);
    }

    [Test, Category("US 201")]
    public void Experimental_2_es_menor_que_experimental_10()
    {
        Assert.Less(ReleaseVersion.Parse("0.2.0 exp.2").CompareTo(ReleaseVersion.Parse("0.2.0 exp.10")), 0);
    }

    [TestCase("sprint 1")]
    [TestCase("experimental 1")]
    [TestCase("1.0")]
    [TestCase("")]
    [Category("US 201")]
    public void Los_formatos_invalidos_se_rechazan(string texto)
    {
        Assert.IsFalse(ReleaseVersion.TryParse(texto, out _));
    }

    [Test, Category("US 201")]
    public void Acepta_la_v_adelante_y_detecta_experimental()
    {
        Assert.IsTrue(ReleaseVersion.TryParse("v0.3.0 exp.1", out ReleaseVersion v));
        Assert.IsTrue(v.IsExperimental);
        Assert.AreEqual("0.3.0 exp.1", v.ToString());
    }
}

public class DanioDeArmasTests
{
    private readonly List<Object> creados = new List<Object>();

    [TearDown]
    public void Limpiar()
    {
        foreach (Object o in creados) if (o != null) Object.DestroyImmediate(o);
        creados.Clear();
    }

    private ShopItem Arma(int pellets = 1)
    {
        ShopItem a = ScriptableObject.CreateInstance<ShopItem>();
        creados.Add(a);
        a.pellets = pellets;
        a.spread = new Vector2(1f, 3f);
        a.bands = new[]
        {
            new DamageBand { upTo = 20f, head = 100, body = 40, legs = 30 },
            new DamageBand { upTo = 0f, head = 80, body = 30, legs = 20 }, // 0 = sin límite
        };
        return a;
    }

    [Test, Category("US 165"), Description("Daño por zona")]
    public void El_danio_depende_de_la_zona()
    {
        ShopItem a = Arma();
        Assert.AreEqual(100, a.HitDamage(BodyZone.Head, 5f));
        Assert.AreEqual(40, a.HitDamage(BodyZone.Body, 5f));
        Assert.AreEqual(30, a.HitDamage(BodyZone.Legs, 5f));
    }

    [Test, Category("US 165"), Description("Daño por distancia: el tramo cambia después de upTo")]
    public void El_danio_depende_de_la_distancia()
    {
        ShopItem a = Arma();
        Assert.AreEqual(40, a.HitDamage(BodyZone.Body, 20f));
        Assert.AreEqual(30, a.HitDamage(BodyZone.Body, 20.1f));
        Assert.AreEqual(30, a.HitDamage(BodyZone.Body, 500f));
    }

    [Test, Category("US 165")]
    public void La_escopeta_suma_todos_los_perdigones()
    {
        ShopItem a = Arma(pellets: 12);
        Assert.AreEqual(12 * 40, a.ShotDamage(a.bands[0], BodyZone.Body));
    }

    [Test, Category("US 165"), Description("Tiros para matar con vida 100 y escudo liviano/completo")]
    public void Tiros_para_matar()
    {
        Assert.AreEqual(1, DamageMath.ShotsToKill(100, 100));
        Assert.AreEqual(3, DamageMath.ShotsToKill(40, 100));
        Assert.AreEqual(2, DamageMath.ShotsToKill(100, 150));
        Assert.AreEqual(int.MaxValue, DamageMath.ShotsToKill(0, 100));
    }

    [Test, Category("US 167"), Description("Dispersión según quieto / moviéndose")]
    public void Dispersion_quieto_y_en_movimiento()
    {
        ShopItem a = Arma();
        Assert.AreEqual(1f, WeaponAim.SpreadFor(a, 0f, true, false, false), 0.001f);
        Assert.AreEqual(3f, WeaponAim.SpreadFor(a, 1f, true, false, false), 0.001f);
    }

    [Test, Category("US 167"), Description("Aire x4, agachado x0,75, apuntando x0,7")]
    public void Dispersion_multiplicadores()
    {
        ShopItem a = Arma();
        Assert.AreEqual(4f, WeaponAim.SpreadFor(a, 0f, false, false, false), 0.001f);
        Assert.AreEqual(0.75f, WeaponAim.SpreadFor(a, 0f, true, true, false), 0.001f);
        Assert.AreEqual(0.7f, WeaponAim.SpreadFor(a, 0f, true, false, true), 0.001f);
    }

    [Test, Category("US 167"), Description("US 068: el francotirador siempre cuenta como apuntando")]
    public void Francotirador_siempre_apunta()
    {
        ShopItem a = Arma();
        a.category = ShopCategory.Snipers;
        Assert.AreEqual(0.7f, WeaponAim.SpreadFor(a, 0f, true, false, false), 0.001f);
    }

    [Test, Category("US 167"), Description("Tirar seguido suma dispersión extra")]
    public void Dispersion_extra_por_tirar_seguido()
    {
        ShopItem a = Arma();
        Assert.AreEqual(1.5f, WeaponAim.SpreadFor(a, 0f, true, false, false, 0.5f), 0.001f);
    }

    [Test, Category("US 168"), Description("La primera bala sale exacta")]
    public void Retroceso_la_primera_bala_es_exacta()
    {
        ShopItem a = Arma();
        a.recoilKick = 1f;
        Assert.AreEqual(Vector2.zero, WeaponAim.PatternOffset(a, 0f));
    }

    [Test, Category("US 168")]
    public void Retroceso_sube_por_bala_y_respeta_el_tope()
    {
        ShopItem a = Arma();
        a.recoilKick = 1f;
        Assert.AreEqual(3f, WeaponAim.PatternOffset(a, 3f).x, 0.001f);
        a.recoilMaxClimb = 10f;
        Assert.AreEqual(10f, WeaponAim.PatternOffset(a, 50f).x, 0.001f);
    }

    [Test, Category("US 168")]
    public void Sin_retroceso_la_mira_no_se_mueve()
    {
        ShopItem a = Arma();
        a.recoilKick = 0f;
        Assert.AreEqual(Vector2.zero, WeaponAim.PatternOffset(a, 10f));
    }
}

public class GranadaDanioTests
{
    [Test, Category("US 073"), Description("CA4: 100 hasta 1,5 m, baja en línea recta hasta 25 a 5 m, y 0 más lejos")]
    public void Danio_de_la_metralla_segun_la_distancia()
    {
        GameObject go = new GameObject("Granada");
        Grenade1 g = go.AddComponent<Grenade1>();
        try
        {
            Assert.AreEqual(100, g.DamageAt(0f));
            Assert.AreEqual(100, g.DamageAt(1.5f));
            Assert.AreEqual(68, g.DamageAt(3f));
            Assert.AreEqual(25, g.DamageAt(5f));
            Assert.AreEqual(0, g.DamageAt(5.1f));
        }
        finally { Object.DestroyImmediate(go); }
    }
}

public class NombreDeJugadorTests
{
    [Test, Category("US 164"), Description("CA2: de 3 a 16 caracteres")]
    public void Valida_el_largo()
    {
        Assert.IsNull(PlayerProfile.Validate("Luka"));
        StringAssert.Contains("Falta 1", PlayerProfile.Validate("ab"));
        StringAssert.Contains("Faltan 3", PlayerProfile.Validate(""));
        StringAssert.Contains("Sobran 1", PlayerProfile.Validate(new string('a', 17)));
        Assert.IsNull(PlayerProfile.Validate(new string('a', 16)));
    }

    [Test, Category("US 164"), Description("CA2: solo letras, números, espacio, guion y guion bajo")]
    public void Rechaza_caracteres_raros()
    {
        StringAssert.Contains("No se puede usar", PlayerProfile.Validate("Luka*"));
        Assert.IsNull(PlayerProfile.Validate("Luka_G 2"));
    }

    [Test, Category("US 164")]
    public void Limpia_los_espacios_de_mas()
    {
        Assert.AreEqual("Luka G", PlayerProfile.Clean("  Luka    G  "));
    }

    [Test, Category("US 164"), Description("CA6: nombre repetido en la sala")]
    public void Agrega_un_numero_si_el_nombre_ya_esta_en_la_sala()
    {
        Assert.AreEqual("Luka", PlayerProfile.UniqueInRoom("Luka", new[] { "Ana" }));
        Assert.AreEqual("Luka (2)", PlayerProfile.UniqueInRoom("Luka", new[] { "luka" }));
        Assert.AreEqual("Luka (3)", PlayerProfile.UniqueInRoom("Luka", new[] { "Luka", "Luka (2)" }));
    }
}

public class MiraConfigTests
{
    [Test, Category("US 172"), Description("CA6: la mira por defecto")]
    public void La_mira_por_defecto_es_la_de_la_US_171()
    {
        MiraConfig.Mira m = MiraConfig.Defecto;
        Assert.AreEqual(MiraConfig.Cruz, m.tipo);
        Assert.AreEqual(8f, m.largo);
        Assert.AreEqual(2f, m.grosor);
        Assert.AreEqual(5f, m.hueco);
        Assert.IsTrue(m.borde);
        Assert.IsFalse(m.punto);
        Assert.IsFalse(m.dinamica);
        Assert.AreEqual(Color.white, m.Color);
    }

    [Test, Category("US 172"), Description("CA2: cinco colores fijos más uno propio")]
    public void Hay_cinco_colores_y_uno_propio()
    {
        Assert.AreEqual(5, MiraConfig.Colores.Length);
        Assert.AreEqual(5, MiraConfig.ColorPropio);
    }

    [Test, Category("US 172")]
    public void Dos_miras_iguales_se_reconocen_y_un_cambio_se_detecta()
    {
        MiraConfig.Mira a = MiraConfig.Defecto, b = MiraConfig.Defecto;
        Assert.IsTrue(a.IgualA(b));
        b.largo = 12f;
        Assert.IsFalse(a.IgualA(b));
    }
}

// Si alguien cambia una regla del juego por error (o a propósito), estos tests avisan.
// Si fue a propósito: actualizar el número acá Y el criterio de aceptación en el plan de pruebas.
public class ReglasDelJuegoTests
{
    [Test, Category("US 032")]
    public void Rondas_del_Tactico()
    {
        Assert.AreEqual(20f, RondasTacticas.DuracionCompra);
        Assert.AreEqual(100f, RondasTacticas.DuracionCombate);
        Assert.AreEqual(5f, RondasTacticas.DuracionCartel);
        Assert.AreEqual(7, MarcadorTactico.RondasParaGanar);
        Assert.AreEqual(13, RondasTacticas.RondaMuerteSubita);
        Assert.AreEqual(7, EquiposTacticos.RondaDeCambio);
        Assert.AreEqual(4, EquiposTacticos.MaxPorEquipo);
    }

    [Test, Category("US 135")]
    public void Economia_del_Tactico()
    {
        Assert.AreEqual(800, RondasTacticas.PlataInicial);
        Assert.AreEqual(5000, RondasTacticas.PlataMuerteSubita);
        Assert.AreEqual(200, RondasTacticas.PremioBaja);
        Assert.AreEqual(3000, RondasTacticas.PremioGanada);
        Assert.AreEqual(300, RondasTacticas.PremioPlantar);
    }

    [Test, Category("US 131")]
    public void Dispositivo()
    {
        Assert.AreEqual(4f, DispositivoTactico.TiempoPlantar);
        Assert.AreEqual(7f, DispositivoTactico.TiempoDesactivar);
        Assert.AreEqual(1.5f, DispositivoTactico.AlcanceDesactivar);
        Assert.AreEqual(45f, RondasTacticas.DuracionDispositivo);
        Assert.AreEqual(12f, RondasTacticas.RadioExplosion);
    }

    [Test, Category("US 136"), Category("US 140")]
    public void Deathmatch()
    {
        Assert.AreEqual(10f, PartidaDeathmatch.DuracionCuenta);
        Assert.AreEqual(480f, PartidaDeathmatch.DuracionCombate);
        Assert.AreEqual(25, PartidaDeathmatch.BajasParaGanar);
        Assert.AreEqual(20, PartidaDeathmatch.BajasDeAviso);
        Assert.AreEqual(60f, PartidaDeathmatch.SegundosDeAviso);
    }

    [Test, Category("US 139"), Description("TC 823: cada baja cura 50")]
    public void Cada_baja_cura_50_en_deathmatch()
    {
        Assert.AreEqual(50, JugadorEnRed.VidaPorBaja);
    }

    [Test, Category("US 019"), Category("US 020"), Category("US 021"), Category("US 022")]
    public void Habilidades()
    {
        Assert.AreEqual(9f, HabilidadTrasbordo.Distancia);
        Assert.AreEqual(1f, HabilidadTrasbordo.Minimo);
        Assert.AreEqual(800, Barrera.VidaMax);
        Assert.AreEqual(15f, Barrera.Duracion);
        Assert.AreEqual(4f, Barrera.Ancho);
        Assert.AreEqual(2.2f, Barrera.Alto);
        Assert.AreEqual(6f, HabilidadVistaAerea.Duracion);
        Assert.AreEqual(30f, HabilidadVistaAerea.Radio);
        Assert.AreEqual(60, Perro.VidaMax);
    }

    [Test, Category("US 074"), Category("US 075")]
    public void Granadas_de_humo_y_flash()
    {
        Assert.AreEqual(4f, HumoGranada.RadioPorDefecto);
        Assert.AreEqual(15f, HumoGranada.DuracionPorDefecto);
        Assert.AreEqual(15f, FlashBlind.DefaultRange);
        Assert.AreEqual(60f, FlashBlind.FrontAngle);
        Assert.AreEqual(110f, FlashBlind.SideAngle);
    }
}
