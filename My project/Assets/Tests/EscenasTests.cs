using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Tests que abren escenas reales. Las escenas tienen que estar en File > Build Profiles > Scene List.
// Unity hace fallar el test si aparece un Debug.LogError o una excepción: eso es lo que busca el "smoke test".
// PruebasAisladas hace que el juego no toque el guardado real ni el servicio online mientras corren.

[PrebuildSetup(typeof(PruebasAisladas)), PostBuildCleanup(typeof(PruebasAisladas))]
public class EscenasTests
{
    [UnityTest, Category("Smoke"), Description("Regresión: la escena abre y queda 2 s andando sin errores en consola")]
    public IEnumerator La_escena_abre_sin_errores([Values("MenuPrincipal", "Mapa_modo_tactico", "Mapa_deathmatch")] string escena)
    {
        if (!Application.CanStreamedLevelBeLoaded(escena))
            Assert.Ignore($"La escena {escena} no está en el Scene List del build.");
        yield return SceneManager.LoadSceneAsync(escena);
        yield return new WaitForSeconds(2f);
        Assert.AreEqual(escena, SceneManager.GetActiveScene().name);
    }

    [UnityTest, Category("US 119"), Description("TC-873: en el campo de práctica todo es gratis y el jugador es invulnerable (US 121)")]
    public IEnumerator Campo_de_practica_armas_gratis_y_jugador_invulnerable()
    {
        if (!Application.CanStreamedLevelBeLoaded("Mapa_practica"))
            Assert.Ignore("Mapa_practica no está en el Scene List del build.");
        yield return SceneManager.LoadSceneAsync("Mapa_practica");
        yield return new WaitForSeconds(1.5f);

        Assert.IsTrue(CampoDePractica.Activo, "CampoDePractica no está activo en la escena");
        PlayerLoadout tienda = Object.FindAnyObjectByType<PlayerLoadout>();
        Assert.IsNotNull(tienda, "No hay jugador con PlayerLoadout");
        Assert.IsNotNull(tienda.Catalog, "El jugador no tiene catálogo de tienda asignado");

        HealthSystem vida = tienda.GetComponent<HealthSystem>();
        int antes = vida.currentHealth;
        vida.TakeDamage(50);
        Assert.AreEqual(antes, vida.currentHealth, "El jugador recibió daño en el campo de práctica");

        foreach (ShopItem item in tienda.Catalog.items)
            if (item != null && item.IsWeapon)
                Assert.AreEqual(0, tienda.CostOf(item), "Cuesta plata: " + item.displayName);

        int plata = tienda.Wallet.Money;
        ShopItem arma = tienda.Catalog.items.Find(i => i != null && i.IsWeapon && !tienda.IsEquipped(i));
        Assert.IsNotNull(arma);
        Assert.AreEqual(ShopResult.Ok, tienda.Buy(arma));
        Assert.AreEqual(plata, tienda.Wallet.Money, "Cambiar de arma gastó plata");
    }
}
