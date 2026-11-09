using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// F30 (Idiomas): regresión automática de US 347 (menús y perfil en inglés) y US 348 (partidas en inglés).
// Cada test pone el juego en inglés, abre una pantalla o un momento de la partida como lo vería el jugador y falla si
// queda algún texto en español (DetectorDeIdioma). Deja una captura y la lista de textos con el nombre del caso.
// No busca errores de consola (eso lo hacen los smoke tests): sin red, algunas escenas avisan que no hay sala.

[PrebuildSetup(typeof(PruebasAisladas)), PostBuildCleanup(typeof(PruebasAisladas))]
public class IdiomaEnPantallaTests
{
    private readonly List<string> enEspanol = new List<string>();

    [UnitySetUp]
    public IEnumerator EnIngles()
    {
        LogAssert.ignoreFailingMessages = true;
        yield return LocalizationSettings.InitializationOperation;
        L10n.SetLanguage("en");
    }

    [UnityTearDown]
    public IEnumerator VolverAlEspanol()
    {
        Time.timeScale = 1f;
        L10n.SetLanguage("es");
        yield return null;
    }

    // ---------- US 347: menús y perfil ----------

    [UnityTest, Category("US 347"), Description("CA1: el menú principal se ve en inglés")]
    public IEnumerator US347_CA1_Menu_principal()
    {
        yield return Menu();
        yield return Comprobar("US-347_CA1_Menu_principal");
    }

    [UnityTest, Category("US 347"), Description("CA2: Opciones se ve en inglés en todas sus pestañas")]
    public IEnumerator US347_CA2_Opciones()
    {
        yield return Menu();
        MenuUI().MostrarOpciones();
        yield return Esperar(1f);
        OpcionesPantalla opciones = Object.FindAnyObjectByType<OpcionesPantalla>();
        Assert.IsNotNull(opciones, "No se abrió Opciones");
        var pestanas = opciones.GetComponentsInChildren<UnityEngine.UI.Button>(true).Where(b => b.name.StartsWith("Btn")).ToList();
        Assert.IsNotEmpty(pestanas, "Opciones no tiene pestañas");
        var todos = new List<string>();
        foreach (var pestana in pestanas)
        {
            pestana.onClick.Invoke();
            yield return Esperar(0.6f);
            yield return DetectorDeIdioma.Revisar("US-347_CA2_Opciones_" + pestana.name.Substring(3), enEspanol);
            todos.AddRange(enEspanol.Select(t => pestana.name.Substring(3) + ": " + t));
        }
        Assert.IsEmpty(todos, DetectorDeIdioma.Mensaje(todos));
    }

    [UnityTest, Category("US 347"), Description("CA3: el Perfil se ve en inglés")]
    public IEnumerator US347_CA3_Perfil()
    {
        yield return Pantalla(m => m.Perfil);
        yield return Comprobar("US-347_CA3_Perfil");
    }

    [UnityTest, Category("US 347"), Description("CA4: Logros se ve en inglés")]
    public IEnumerator US347_CA4_Logros()
    {
        yield return Pantalla(m => m.Logros);
        yield return Comprobar("US-347_CA4_Logros");
    }

    [UnityTest, Category("US 347"), Description("CA5: la Estación del Rift y la Tienda se ven en inglés")]
    public IEnumerator US347_CA5_Estacion_y_Tienda()
    {
        yield return Pantalla(m => m.Estacion);
        yield return DetectorDeIdioma.Revisar("US-347_CA5_Estacion", enEspanol);
        var todos = new List<string>(enEspanol);
        Object.FindAnyObjectByType<MenuRiftwalker>().Abrir(Object.FindAnyObjectByType<MenuRiftwalker>().Tienda);
        yield return Esperar(1f);
        yield return DetectorDeIdioma.Revisar("US-347_CA5_Tienda", enEspanol);
        todos.AddRange(enEspanol);
        Assert.IsEmpty(todos, DetectorDeIdioma.Mensaje(todos));
    }

    [UnityTest, Category("US 347"), Description("CA6: la selección de modo se ve en inglés")]
    public IEnumerator US347_CA6_Seleccion_de_modo()
    {
        yield return Menu();
        MenuUI().MostrarSeleccionModo();
        yield return Esperar(1.5f);
        yield return Comprobar("US-347_CA6_Seleccion_de_modo");
    }

    [UnityTest, Category("US 347"), Description("CA7: los créditos se ven en inglés")]
    public IEnumerator US347_CA7_Creditos()
    {
        yield return Menu();
        MenuUI().MostrarCreditos();
        yield return Esperar(1.5f);
        yield return Comprobar("US-347_CA7_Creditos");
    }

    [UnityTest, Category("US 347"), Description("CA8: el menú de pausa se ve en inglés")]
    public IEnumerator US347_CA8_Pausa()
    {
        yield return Escena("Mapa_practica", GameMode.Tactico, 2f);
        Assert.IsNotNull(PauseMenu.Instance, "No hay menú de pausa en el campo de práctica");
        PauseMenu.Instance.Pause();
        yield return Esperar(1f);
        yield return Comprobar("US-347_CA8_Pausa");
        PauseMenu.Instance.Resume();
    }

    // ---------- US 348: partidas ----------

    [UnityTest, Category("US 348"), Description("CA1: Invasión: el HUD y el anuncio de la oleada se ven en inglés")]
    public IEnumerator US348_CA1_Invasion_HUD_y_oleada()
    {
        yield return Escena("universidadMAPA", GameMode.Zombie, 3f);
        yield return Comprobar("US-348_CA1_Invasion_HUD_y_oleada");
    }

    [UnityTest, Category("US 348"), Description("CA2: Invasión: la elección del puesto (cartas) se ve en inglés")]
    public IEnumerator US348_CA2_Invasion_eleccion_de_cartas()
    {
        yield return Escena("universidadMAPA", GameMode.Zombie, 3f);
        EleccionDeInvasion eleccion = EleccionDeInvasion.Actual != null ? EleccionDeInvasion.Actual : Object.FindAnyObjectByType<EleccionDeInvasion>();
        Assert.IsNotNull(eleccion, "No hay EleccionDeInvasion en la escena");
        typeof(EleccionDeInvasion).GetMethod("Abrir", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(eleccion, new object[] { false });
        yield return EsperarHasta(() => EleccionDeInvasion.Abierta, 30f);
        Assert.IsTrue(EleccionDeInvasion.Abierta, "No se abrió la elección del puesto");
        yield return Esperar(1.5f);
        yield return Comprobar("US-348_CA2_Invasion_eleccion_de_cartas");
    }

    [UnityTest, Category("US 348"), Description("CA3: Invasión: la pantalla de resultado se ve en inglés")]
    public IEnumerator US348_CA3_Invasion_resultado()
    {
        yield return Escena("universidadMAPA", GameMode.Zombie, 3f);
        ResultadoDeInvasion.Mostrar(true);
        yield return Esperar(2.5f);
        yield return Comprobar("US-348_CA3_Invasion_resultado");
    }

    [UnityTest, Category("US 348"), Description("CA4: Deathmatch: el marcador y la tabla se ven en inglés")]
    public IEnumerator US348_CA4_Deathmatch()
    {
        yield return Escena("Mapa_deathmatch", GameMode.Deathmatch, 3f);
        yield return Comprobar("US-348_CA4_Deathmatch");
    }

    [UnityTest, Category("US 348"), Description("CA5: Táctico: el marcador y la fase de compra se ven en inglés")]
    public IEnumerator US348_CA5_Tactico()
    {
        yield return Escena("Mapa_modo_tactico", GameMode.Tactico, 3f);
        yield return Comprobar("US-348_CA5_Tactico");
    }

    [UnityTest, Category("US 348"), Description("CA6: Práctica: el tutorial y la tienda se ven en inglés")]
    public IEnumerator US348_CA6_Practica_tutorial_y_tienda()
    {
        yield return Escena("Mapa_practica", GameMode.Tactico, 2.5f);
        yield return DetectorDeIdioma.Revisar("US-348_CA6_Practica_tutorial", enEspanol);
        var todos = new List<string>(enEspanol);
        KeyBindings.Apretar(GameAction.Tienda);
        yield return Esperar(1.5f);
        yield return DetectorDeIdioma.Revisar("US-348_CA6_Practica_tienda", enEspanol);
        todos.AddRange(enEspanol);
        Assert.IsEmpty(todos, DetectorDeIdioma.Mensaje(todos));
    }

    // ---------- Ayudas ----------

    private IEnumerator Comprobar(string caso)
    {
        yield return DetectorDeIdioma.Revisar(caso, enEspanol);
        Assert.IsEmpty(enEspanol, DetectorDeIdioma.Mensaje(enEspanol));
    }

    private static IEnumerator Menu()
    {
        yield return Escena("MenuPrincipal", MatchSettings.Mode, 2f);
    }

    private static IEnumerator Pantalla(System.Func<MenuRiftwalker, PantallaBase> cual)
    {
        yield return Menu();
        MenuRiftwalker menu = Object.FindAnyObjectByType<MenuRiftwalker>();
        Assert.IsNotNull(menu, "No está el menú de Riftwalker");
        menu.Abrir(cual(menu));
        yield return Esperar(1f);
    }

    private static MenuUIController MenuUI()
    {
        MenuUIController ui = Object.FindAnyObjectByType<MenuUIController>();
        Assert.IsNotNull(ui, "No hay MenuUIController en el menú");
        return ui;
    }

    private static IEnumerator Escena(string escena, GameMode modo, float espera)
    {
        if (!Application.CanStreamedLevelBeLoaded(escena)) Assert.Ignore($"La escena {escena} no está en el Scene List del build.");
        MatchSettings.Mode = modo;
        yield return SceneManager.LoadSceneAsync(escena);
        yield return Esperar(espera);
    }

    private static IEnumerator Esperar(float segundos)
    {
        float hasta = Time.realtimeSinceStartup + segundos;
        while (Time.realtimeSinceStartup < hasta) yield return null;
    }

    private static IEnumerator EsperarHasta(System.Func<bool> condicion, float segundos)
    {
        float hasta = Time.realtimeSinceStartup + segundos;
        while (!condicion() && Time.realtimeSinceStartup < hasta) yield return null;
    }
}
