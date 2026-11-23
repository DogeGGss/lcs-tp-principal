using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;
using static IdiomaEnPantallaTests;
using Object = UnityEngine.Object;

// F30 (Idiomas), US 350: regresión automática de portugués, francés e italiano.
// Cada test recorre una pantalla (o un momento de la partida) primero en español, como referencia, y después en
// pt-BR, fr e it. En cada idioma deja una captura y la lista de textos (US-350_<pantalla>_<idioma>) y falla si:
// - CA2 a CA4: queda algún texto en español o en inglés que en ese idioma tiene traducción.
// - CA5: algún texto se corta o se sale de su recuadro o de su botón, y en español entraba.
// CA1 revisa que el selector de Opciones muestre los cinco idiomas (el del launcher lo prueba Launcher/Tests).
// CA6 son las pruebas de US 349 (TraduccionesCompletasTests), que ya recorren todos los idiomas.
[PrebuildSetup(typeof(PruebasAisladas)), PostBuildCleanup(typeof(PruebasAisladas))]
public class IdiomasNuevosTests
{
    private static readonly string[] Recorrido = { "es", "pt-BR", "fr", "it" };
    private static readonly string[] PestanasDeOpciones = { "BtnGraficos", "BtnControles", "BtnSonido", "BtnMira", "BtnIdioma" };
    private static readonly string[] Nombres = { "Español", "English", "Português (Brasil)", "Français", "Italiano" };

    private readonly Dictionary<string, HashSet<string>> enEspanol = new Dictionary<string, HashSet<string>>();
    private readonly Dictionary<string, Dictionary<string, string>> textosEnEspanol = new Dictionary<string, Dictionary<string, string>>();
    private readonly List<string> fallas = new List<string>();
    private string idioma = "es";

    [UnitySetUp]
    public IEnumerator Preparar()
    {
        LogAssert.ignoreFailingMessages = true;
        yield return LocalizationSettings.InitializationOperation;
        // Con un nombre ya puesto no se abre la ventana "¿Cómo te llamás?", que taparía las capturas.
        if (!PlayerProfile.HasName) PlayerProfile.TrySetName("Riftwalker", out _);
        enEspanol.Clear();
        textosEnEspanol.Clear();
        fallas.Clear();
    }

    [UnityTearDown]
    public IEnumerator VolverAlEspanol()
    {
        Time.timeScale = 1f;
        L10n.SetLanguage("es");
        yield return null;
    }

    // ---------- CA1: selector ----------

    [UnityTest, Category("US 350"), Description("CA1: Opciones muestra Español, English, Português, Français e Italiano")]
    public IEnumerator US350_CA1_Selector_de_idioma()
    {
        L10n.SetLanguage("es");
        Assert.AreEqual(string.Join(", ", Nombres), string.Join(", ", L10n.Languages.Select(l => l.name)), "Idiomas del catálogo");
        yield return Menu();
        LogAssert.ignoreFailingMessages = true;
        MenuUI().MostrarOpciones();
        yield return Esperar(1f);
        var pestana = Pestanas().FirstOrDefault(b => b.name == "BtnIdioma");
        Assert.IsNotNull(pestana, "Opciones no tiene la pestaña Idioma");
        pestana.onClick.Invoke();
        yield return Esperar(1f);
        yield return new WaitForEndOfFrame();
        RevisorDeTextos.Guardar("US-350_CA1_Selector_de_idioma", "es", new List<string>(), new List<RevisorDeTextos.NoEntra>(), null);
        var textos = DetectorDeIdioma.TextosVisibles().Select(DetectorDeIdioma.Visible).ToList();
        var faltan = Nombres.Where(n => !textos.Contains(n)).ToList();
        Assert.IsEmpty(faltan, "El selector no muestra: " + string.Join(", ", faltan) + ". En pantalla: " + string.Join(" | ", textos));
    }

    // ---------- CA2 a CA5: menús y perfil ----------

    [UnityTest, Category("US 350"), Description("CA2-CA5: el menú principal en portugués, francés e italiano")]
    public IEnumerator US350_Menu_principal() => EnCadaIdioma(MenuPrincipal);
    private IEnumerator MenuPrincipal() { yield return Menu(); yield return Mirar("Menu_principal"); }

    [UnityTest, Category("US 350"), Description("CA2-CA5: Opciones, en todas sus pestañas")]
    public IEnumerator US350_Opciones() => EnCadaIdioma(Opciones);
    private IEnumerator Opciones()
    {
        yield return Menu();
        MenuUI().MostrarOpciones();
        yield return Esperar(1f);
        foreach (var pestana in Pestanas())
        {
            pestana.onClick.Invoke();
            yield return Esperar(0.6f);
            yield return Mirar("Opciones_" + pestana.name.Substring(3));
        }
    }

    [UnityTest, Category("US 350"), Description("CA2-CA5: el Perfil, en sus cinco pestañas")]
    public IEnumerator US350_Perfil() => EnCadaIdioma(Perfil);
    private IEnumerator Perfil()
    {
        yield return Pantalla(m => m.Perfil);
        PantallaPerfil perfil = Object.FindAnyObjectByType<MenuRiftwalker>().Perfil;
        MethodInfo elegir = typeof(PantallaPerfil).GetMethod("ElegirPestana", BindingFlags.Instance | BindingFlags.NonPublic);
        string[] pestanas = { "Estadisticas", "Personalizacion", "Emotes", "Historial", "Cuenta" }; // Historial: US 374
        for (int i = 0; i < pestanas.Length; i++)
        {
            elegir?.Invoke(perfil, new object[] { i });
            yield return Esperar(0.6f);
            yield return Mirar("Perfil_" + pestanas[i]);
        }
    }

    [UnityTest, Category("US 350"), Description("CA2-CA5: Logros")]
    public IEnumerator US350_Logros() => EnCadaIdioma(Logros);
    private IEnumerator Logros() { yield return Pantalla(m => m.Logros); yield return Mirar("Logros"); }

    [UnityTest, Category("US 350"), Description("CA2-CA5: la Estación del Rift y la Tienda, en sus cuatro pestañas")]
    public IEnumerator US350_Estacion_y_Tienda() => EnCadaIdioma(EstacionYTienda);
    private IEnumerator EstacionYTienda()
    {
        yield return Pantalla(m => m.Estacion);
        yield return Mirar("Estacion");
        MenuRiftwalker menu = Object.FindAnyObjectByType<MenuRiftwalker>();
        foreach (PestanaTienda p in Enum.GetValues(typeof(PestanaTienda)))
        {
            menu.AbrirTienda(p);
            yield return Esperar(1f);
            yield return Mirar("Tienda_" + p);
        }
    }

    [UnityTest, Category("US 350"), Description("CA2-CA5: la selección de modo")]
    public IEnumerator US350_Seleccion_de_modo() => EnCadaIdioma(SeleccionDeModo);
    private IEnumerator SeleccionDeModo()
    {
        yield return Menu();
        MenuUI().MostrarSeleccionModo();
        yield return Esperar(1.5f);
        yield return Mirar("Seleccion_de_modo");
    }

    [UnityTest, Category("US 350"), Description("CA2-CA5: los créditos")]
    public IEnumerator US350_Creditos() => EnCadaIdioma(Creditos);
    private IEnumerator Creditos()
    {
        yield return Menu();
        MenuUI().MostrarCreditos();
        yield return Esperar(1.5f);
        yield return Mirar("Creditos");
    }

    [UnityTest, Category("US 350"), Description("CA2-CA5: el menú de pausa")]
    public IEnumerator US350_Pausa() => EnCadaIdioma(Pausa);
    private IEnumerator Pausa()
    {
        yield return Escena("Mapa_practica", GameMode.Tactico, 2f);
        Assert.IsNotNull(PauseMenu.Instance, "No hay menú de pausa en el campo de práctica");
        PauseMenu.Instance.Pause();
        yield return Esperar(1f);
        yield return Mirar("Pausa");
        PauseMenu.Instance.Resume();
    }

    // ---------- CA2 a CA5: partidas ----------

    [UnityTest, Category("US 350"), Description("CA2-CA5: Invasión: HUD, elección del puesto y resultado")]
    public IEnumerator US350_Invasion() => EnCadaIdioma(Invasion);
    private IEnumerator Invasion()
    {
        yield return Escena("universidadMAPA", GameMode.Zombie, 3f);
        yield return Mirar("Invasion_HUD");
        EleccionDeInvasion eleccion = EleccionDeInvasion.Actual != null ? EleccionDeInvasion.Actual : Object.FindAnyObjectByType<EleccionDeInvasion>();
        Assert.IsNotNull(eleccion, "No hay EleccionDeInvasion en la escena");
        typeof(EleccionDeInvasion).GetMethod("Abrir", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(eleccion, new object[] { false });
        yield return EsperarHasta(() => EleccionDeInvasion.Abierta, 30f);
        Assert.IsTrue(EleccionDeInvasion.Abierta, "No se abrió la elección del puesto");
        yield return Esperar(1.5f);
        yield return Mirar("Invasion_cartas");
        yield return Escena("universidadMAPA", GameMode.Zombie, 3f);
        ResultadoDeInvasion.Mostrar(true);
        yield return Esperar(2.5f);
        yield return Mirar("Invasion_resultado");
    }

    [UnityTest, Category("US 350"), Description("CA2-CA5: Deathmatch: marcador y tabla")]
    public IEnumerator US350_Deathmatch() => EnCadaIdioma(Deathmatch);
    private IEnumerator Deathmatch() { yield return Escena("Mapa_deathmatch", GameMode.Deathmatch, 3f); yield return Mirar("Deathmatch"); }

    [UnityTest, Category("US 350"), Description("CA2-CA5: Táctico: marcador y fase de compra")]
    public IEnumerator US350_Tactico() => EnCadaIdioma(Tactico);
    private IEnumerator Tactico() { yield return Escena("Mapa_modo_tactico", GameMode.Tactico, 3f); yield return Mirar("Tactico"); }

    [UnityTest, Category("US 350"), Description("CA2-CA5: Práctica: tutorial y tienda")]
    public IEnumerator US350_Practica() => EnCadaIdioma(Practica);
    private IEnumerator Practica()
    {
        yield return Escena("Mapa_practica", GameMode.Tactico, 2.5f);
        yield return Mirar("Practica_tutorial");
        KeyBindings.Apretar(GameAction.Tienda);
        yield return Esperar(1.5f);
        yield return Mirar("Practica_tienda");
    }

    // ---------- Ayudas ----------

    private IEnumerator EnCadaIdioma(Func<IEnumerator> recorrer)
    {
        foreach (string i in Recorrido)
        {
            LogAssert.ignoreFailingMessages = true; // los errores de consola no son de esta historia (sin red, avisos de sala)
            idioma = i;
            L10n.SetLanguage(i);
            yield return recorrer();
        }
        Assert.IsEmpty(fallas, string.Join("\n", fallas.Take(25)) + (fallas.Count > 25 ? $"\n(y {fallas.Count - 25} más)" : ""));
    }

    // Mira la pantalla en el idioma actual: en español guarda qué textos ya no entraban; en los otros, anota las fallas.
    private IEnumerator Mirar(string pantalla)
    {
        LogAssert.ignoreFailingMessages = true;
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        string caso = $"US-350_{pantalla}_{idioma}";
        List<RevisorDeTextos.NoEntra> noEntran = RevisorDeTextos.NoEntran();
        if (idioma == "es")
        {
            enEspanol[pantalla] = new HashSet<string>(noEntran.Select(n => n.clave));
            textosEnEspanol[pantalla] = RevisorDeTextos.TextosPorRuta();
            RevisorDeTextos.Guardar(caso, idioma, new List<string>(), noEntran, null);
            yield break;
        }
        enEspanol.TryGetValue(pantalla, out HashSet<string> referencia);
        var nuevos = noEntran.Where(n => referencia == null || !referencia.Contains(n.clave)).ToList();
        var yaEstaban = noEntran.Except(nuevos).ToList();
        textosEnEspanol.TryGetValue(pantalla, out Dictionary<string, string> textosEs);
        List<string> sinTraducir = RevisorDeTextos.SinTraducir(idioma, textosEs);
        RevisorDeTextos.Guardar(caso, idioma, sinTraducir, nuevos, yaEstaban);
        foreach (string t in sinTraducir) fallas.Add($"{pantalla} · {idioma}: sin traducir «{t}»");
        foreach (var n in nuevos) fallas.Add($"{pantalla} · {idioma}: {n.motivo} «{n.texto}» ({n.objeto})");
    }

    private static List<UnityEngine.UI.Button> Pestanas()
    {
        OpcionesPantalla opciones = Object.FindAnyObjectByType<OpcionesPantalla>();
        Assert.IsNotNull(opciones, "No se abrió Opciones");
        var pestanas = opciones.GetComponentsInChildren<UnityEngine.UI.Button>(true).Where(b => PestanasDeOpciones.Contains(b.name)).ToList();
        Assert.IsNotEmpty(pestanas, "Opciones no tiene pestañas");
        return pestanas;
    }
}
