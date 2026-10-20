using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// Regresión de sonidos (pedido del PO, 20/10/2026). Cada test hace una acción del jugador como si apretara la tecla
// (KeyBindings.Apretar, solo con ModoPruebas) y comprueba que en ese momento suene su sonido, por el canal del
// mezclador que corresponde (SFX, Interfaz o Musica) y, si su historia lo pide, con su alcance. Los de los volúmenes
// (RS-29 a RS-32) comprueban que con cada volumen de Opciones en 0 su canal no se escucha.
// Corren en el campo de práctica (sin red) y en el menú principal. Lo que solo pasa en una partida online (el
// dispositivo, la música de cada modo, la reaparición) no está acá.

[PrebuildSetup(typeof(PruebasAisladas)), PostBuildCleanup(typeof(PruebasAisladas))]
public class RegresionSonidosTests
{
    private PlayerMovement jugador;
    private readonly List<Sonido> oidos = new List<Sonido>();

    [TearDown]
    public void Limpiar()
    {
        KeyBindings.SoltarTodo();
        oidos.Clear();
    }

    // =====================================================================
    // Armas
    // =====================================================================

    [UnityTest, Category("Regresión de sonidos"), Category("US 012"), Description("RS-01: disparo del Mitre, por SFX")]
    public IEnumerator RS01_Disparo_del_Mitre()
    {
        yield return Practica();
        yield return EnLaMano(GameAction.ArmaPrincipal);
        yield return EscucharTecla(GameAction.Disparar, DelJugador);
        SinDireccion(Sono("SFX"));
    }

    [UnityTest, Category("Regresión de sonidos"), Category("US 011"), Description("RS-02: recarga del Mitre, por SFX")]
    public IEnumerator RS02_Recarga_del_Mitre()
    {
        yield return Practica();
        yield return EnLaMano(GameAction.ArmaPrincipal);
        yield return Tocar(GameAction.Disparar);
        yield return Silencio();
        yield return EscucharTecla(GameAction.Recargar, DelJugador);
        SinDireccion(Sono("SFX"));
    }

    [UnityTest, Category("Regresión de sonidos"), Category("US 055"), Description("RS-03: sacar otra arma, por SFX")]
    public IEnumerator RS03_Cambiar_de_arma()
    {
        yield return Practica();
        yield return EnLaMano(GameAction.ArmaPrincipal);
        yield return EscucharTecla(GameAction.ArmaSecundaria, DelJugador);
        SinDireccion(Sono("SFX"));
    }

    [UnityTest, Category("Regresión de sonidos"), Category("US 010"), Description("RS-04: disparo de La Porteña, por SFX")]
    public IEnumerator RS04_Disparo_de_La_Porteña()
    {
        yield return Practica();
        yield return EnLaMano(GameAction.ArmaSecundaria);
        yield return EscucharTecla(GameAction.Disparar, DelJugador);
        SinDireccion(Sono("SFX"));
    }

    [UnityTest, Category("Regresión de sonidos"), Category("US 256"), Description("RS-05: sacar el cuchillo, por SFX")]
    public IEnumerator RS05_Sacar_el_cuchillo()
    {
        yield return Practica();
        yield return EnLaMano(GameAction.ArmaPrincipal);
        yield return EscucharTecla(GameAction.Cuchillo, DelJugador);
        SinDireccion(Sono("SFX"));
    }

    [UnityTest, Category("Regresión de sonidos"), Category("US 256"), Description("RS-06: ataque con el cuchillo, por SFX")]
    public IEnumerator RS06_Ataque_con_el_cuchillo()
    {
        yield return Practica();
        yield return EnLaMano(GameAction.Cuchillo);
        yield return EscucharTecla(GameAction.Disparar, DelJugador);
        Sono("SFX");
    }

    [UnityTest, Category("Regresión de sonidos"), Category("US 070"), Description("RS-07: disparo de la Roca (escopeta), por SFX")]
    public IEnumerator RS07_Disparo_de_la_Roca()
    {
        yield return Practica();
        yield return Comprar(i => i.alias == "Roca");
        yield return EnLaMano(GameAction.ArmaPrincipal);
        yield return EscucharTecla(GameAction.Disparar, DelJugador);
        SinDireccion(Sono("SFX"));
    }

    [UnityTest, Category("Regresión de sonidos"), Category("US 246"), Description("RS-08: disparo del Urquiza, por SFX")]
    public IEnumerator RS08_Disparo_del_Urquiza()
    {
        yield return Practica();
        yield return Comprar(i => i.alias == "Urquiza");
        yield return EnLaMano(GameAction.ArmaPrincipal);
        yield return EscucharTecla(GameAction.Disparar, DelJugador);
        SinDireccion(Sono("SFX"));
    }

    [UnityTest, Category("Regresión de sonidos"), Category("US 073"), Description("RS-09: tirar una granada, por SFX y hasta 40 m")]
    public IEnumerator RS09_Tirar_una_granada()
    {
        yield return Practica();
        yield return SacarGranada(GrenadeType.Frag);
        yield return EscucharTecla(GameAction.Disparar);
        SeEscuchaHasta(Sono("SFX", s => s.alcance < 100f), 40f);
    }

    [UnityTest, Category("Regresión de sonidos"), Category("US 073"), Description("RS-10: explosión de la granada, por SFX y hasta 120 m")]
    public IEnumerator RS10_Explosion_de_la_granada()
    {
        yield return Practica();
        yield return SacarGranada(GrenadeType.Frag);
        yield return EscucharTecla(GameAction.Disparar, null, 6f); // la mecha tarda unos segundos
        SeEscuchaHasta(Sono("SFX", s => s.alcance > 100f), 120f);
    }

    // =====================================================================
    // Jugador
    // =====================================================================

    [UnityTest, Category("Regresión de sonidos"), Category("US 001"), Description("RS-11: pasos al caminar, por SFX")]
    public IEnumerator RS11_Pasos()
    {
        yield return Practica();
        yield return Escuchar(() => KeyBindings.Apretar(GameAction.Adelante), DelJugador, 1.5f);
        KeyBindings.Soltar(GameAction.Adelante);
        SinDireccion(Sono("SFX"));
    }

    [UnityTest, Category("Regresión de sonidos"), Category("US 003"), Description("RS-12: salto, por SFX")]
    public IEnumerator RS12_Salto()
    {
        yield return Practica();
        yield return EscucharTecla(GameAction.Saltar, DelJugador);
        SinDireccion(Sono("SFX"));
    }

    // =====================================================================
    // Habilidades
    // =====================================================================

    [UnityTest, Category("Regresión de sonidos"), Category("US 019"), Description("RS-13: Trasbordo, el que salta lo oye sin dirección, por SFX")]
    public IEnumerator RS13_Trasbordo_propio()
    {
        yield return Practica();
        Prestar("Trasbordo");
        yield return EscucharTecla(GameAction.Habilidad, Llamado("Trasbordo"));
        SinDireccion(Sono("SFX"));
    }

    [UnityTest, Category("Regresión de sonidos"), Category("US 019"), Description("RS-14: Trasbordo, los demás lo oyen hasta 25 m, por SFX")]
    public IEnumerator RS14_Trasbordo_para_los_demas()
    {
        yield return Practica();
        Vector3 aca = jugador.transform.position;
        yield return Escuchar(() => EfectosTrasbordo.Reproducir(aca, aca + jugador.transform.forward * 9f, jugador.transform, false), Llamado("Trasbordo"));
        SeEscuchaHasta(Sono("SFX"), 25f);
    }

    [UnityTest, Category("Regresión de sonidos"), Category("US 020"), Description("RS-15: la Barrera al levantarse, por SFX y hasta 25 m")]
    public IEnumerator RS15_Barrera_al_levantarse()
    {
        yield return Practica();
        yield return LevantarBarrera();
        SeEscuchaHasta(Sono("SFX"), 25f);
    }

    [UnityTest, Category("Regresión de sonidos"), Category("US 020"), Description("RS-16: la Barrera al romperse, por SFX y hasta 25 m")]
    public IEnumerator RS16_Barrera_al_romperse()
    {
        yield return Practica();
        yield return LevantarBarrera();
        Barrera barrera = Object.FindAnyObjectByType<Barrera>();
        Assert.IsNotNull(barrera, "No se levantó la barrera");
        yield return new WaitForSeconds(1.5f);
        oidos.Clear();
        yield return Escuchar(() => barrera.AplicarDanio(Barrera.VidaMax, barrera.transform.position + Vector3.up), Llamado("Barrera"));
        SeEscuchaHasta(Sono("SFX"), 25f);
    }

    [UnityTest, Category("Regresión de sonidos"), Category("US 020"), Description("RS-17: la Barrera al terminar su tiempo, por SFX y hasta 25 m")]
    public IEnumerator RS17_Barrera_al_terminar()
    {
        yield return Practica();
        yield return LevantarBarrera();
        Assert.IsNotNull(Object.FindAnyObjectByType<Barrera>(), "No se levantó la barrera");
        yield return new WaitForSeconds(Barrera.Duracion - 1.5f);
        oidos.Clear();
        yield return Escuchar(() => { }, Llamado("Barrera"), 4f);
        SeEscuchaHasta(Sono("SFX"), 25f);
    }

    [UnityTest, Category("Regresión de sonidos"), Category("US 021"), Description("RS-18: Vista aérea al activarla, por SFX")]
    public IEnumerator RS18_Vista_aerea_al_activar()
    {
        yield return Practica();
        Prestar("VistaAerea");
        yield return EscucharTecla(GameAction.Habilidad, Llamado("Vista aérea"));
        SinDireccion(Sono("SFX"));
    }

    [UnityTest, Category("Regresión de sonidos"), Category("US 021"), Description("RS-19: Vista aérea al terminar, por SFX")]
    public IEnumerator RS19_Vista_aerea_al_terminar()
    {
        yield return Practica();
        Prestar("VistaAerea");
        yield return Tocar(GameAction.Habilidad);
        yield return new WaitForSeconds(1.5f);
        yield return EscucharTecla(GameAction.Habilidad, Llamado("Vista aérea"));
        SinDireccion(Sono("SFX"));
    }

    [UnityTest, Category("Regresión de sonidos"), Category("US 021"), Description("RS-20: Vista aérea, el aviso a los rivales, por SFX")]
    public IEnumerator RS20_Vista_aerea_aviso_a_los_rivales()
    {
        yield return Practica();
        yield return Escuchar(SonidoVistaAerea.Observado, Llamado("Vista aérea"));
        SinDireccion(Sono("SFX"));
    }

    [UnityTest, Category("Regresión de sonidos"), Category("US 022"), Description("RS-21: Callejero al soltar el perro, por SFX")]
    public IEnumerator RS21_Callejero_al_soltarlo()
    {
        yield return Practica();
        Prestar("Callejero");
        yield return EscucharTecla(GameAction.Habilidad, Llamado("Perro"));
        Sono("SFX");
    }

    [UnityTest, Category("Regresión de sonidos"), Category("US 022"), Description("RS-22: Callejero, ladridos mientras corre, por SFX y hasta 20 m")]
    public IEnumerator RS22_Callejero_ladridos()
    {
        yield return Practica();
        Prestar("Callejero");
        yield return EscucharTecla(GameAction.Habilidad, f => f.GetComponent<Perro>() != null, 3f);
        SeEscuchaHasta(Sono("SFX"), SonidoPerro.Alcance);
    }

    [UnityTest, Category("Regresión de sonidos"), Category("US 022"), Description("RS-23: Callejero, la mordida, por SFX y hasta 20 m")]
    public IEnumerator RS23_Callejero_mordida()
    {
        yield return Practica();
        UnityEngine.Audio.AudioMixerGroup sfx = jugador.GetComponentInChildren<Pistola>(true).sfxGroup;
        yield return Escuchar(() => SonidoPerro.Tocar(SonidoPerro.Tipo.Mordida, jugador.transform.position + jugador.transform.forward * 5f, sfx),
            Llamado("Perro"));
        SeEscuchaHasta(Sono("SFX"), SonidoPerro.Alcance);
    }

    [UnityTest, Category("Regresión de sonidos"), Category("US 022"), Description("RS-24: Callejero, el perro que desaparece sin encontrar a nadie, por SFX y hasta 20 m")]
    public IEnumerator RS24_Callejero_al_desaparecer()
    {
        yield return Practica();
        Prestar("Callejero");
        yield return Tocar(GameAction.Habilidad);
        yield return new WaitForSeconds(1f);
        // Sin rivales corre unos metros, se sienta, espera y desaparece (CA6); si no llega a sentarse, desaparece a los
        // 15 s (CA10).
        yield return Escuchar(() => { }, Llamado("Perro"), Perro.Duracion + 3f);
        SeEscuchaHasta(Sono("SFX", s => s.es3D), SonidoPerro.Alcance);
    }

    // =====================================================================
    // Interfaz y música
    // =====================================================================

    [UnityTest, Category("Regresión de sonidos"), Category("US 076"), Description("RS-25: abrir la tienda, por Interfaz")]
    public IEnumerator RS25_Tienda_al_abrir()
    {
        yield return Practica();
        yield return EscucharTecla(GameAction.Tienda, f => f.GetComponent<ShopUI>() != null);
        SinDireccion(Sono("Interfaz"));
        yield return Tocar(GameAction.Tienda); // cerrarla
    }

    [UnityTest, Category("Regresión de sonidos"), Category("US 230"), Description("RS-26: pasar el mouse por un botón del menú, por Interfaz")]
    public IEnumerator RS26_Boton_al_pasar_el_mouse()
    {
        yield return Menu();
        SonidoAlPasar boton = Object.FindAnyObjectByType<SonidoAlPasar>();
        Assert.IsNotNull(boton, "No hay botones con sonido en el menú");
        yield return Escuchar(() => ExecuteEvents.Execute(boton.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler));
        SinDireccion(Sono("Interfaz"));
    }

    [UnityTest, Category("Regresión de sonidos"), Category("US 043"), Description("RS-27: logro desbloqueado, por Interfaz")]
    public IEnumerator RS27_Logro_desbloqueado()
    {
        yield return Menu();
        yield return Escuchar(SonidoInterfaz.Logro);
        SinDireccion(Sono("Interfaz"));
    }

    [UnityTest, Category("Regresión de sonidos"), Category("US 154"), Description("RS-28: la música del menú, por Musica")]
    public IEnumerator RS28_Musica_del_menu()
    {
        yield return Menu();
        AudioSource musica = Array.Find(Object.FindObjectsByType<AudioSource>(), f => Oido.Suena(f) && f.outputAudioMixerGroup != null && f.outputAudioMixerGroup.name == "Musica");
        Assert.IsNotNull(musica, "En el menú no suena nada por el canal Musica");
    }

    // =====================================================================
    // Escenas, acciones y comprobaciones
    // =====================================================================

    private IEnumerator Practica()
    {
        if (!Application.CanStreamedLevelBeLoaded("Mapa_practica")) Assert.Ignore("Mapa_practica no está en el Scene List del build.");
        yield return SceneManager.LoadSceneAsync("Mapa_practica");
        float limite = Time.realtimeSinceStartup + 10f;
        while ((jugador = Object.FindAnyObjectByType<PlayerMovement>()) == null && Time.realtimeSinceStartup < limite) yield return null;
        Assert.IsNotNull(jugador, "No hay jugador en el campo de práctica");
        yield return new WaitForSeconds(1.5f);
    }

    private IEnumerator Menu()
    {
        yield return SceneManager.LoadSceneAsync("MenuPrincipal");
        yield return new WaitForSeconds(1f);
    }

    /// <summary>Saca esa arma (tecla 1 a 4) y espera a que esté lista.</summary>
    private IEnumerator EnLaMano(GameAction tecla)
    {
        yield return Tocar(tecla);
        yield return new WaitForSeconds(1.5f);
        yield return Silencio();
    }

    /// <summary>En el campo de práctica el jugador tiene todas las granadas (US 166): la tecla 4 pasa de una a otra.</summary>
    private IEnumerator SacarGranada(GrenadeType tipo)
    {
        GrenadeThrower lanzador = jugador.GetComponentInChildren<GrenadeThrower>(true);
        Assert.IsNotNull(lanzador, "El jugador no puede tirar granadas");
        for (int i = 0; i < 4 && (lanzador.Selected == null || lanzador.Selected.grenadeType != tipo); i++)
        {
            yield return Tocar(GameAction.Granadas);
            yield return new WaitForSeconds(0.3f);
        }
        Assert.IsNotNull(lanzador.Selected, "No tiene granadas en la mano");
        Assert.AreEqual(tipo, lanzador.Selected.grenadeType, "No tiene esa granada");
        yield return new WaitForSeconds(1f);
        yield return Silencio();
    }

    /// <summary>En el campo de práctica todo es gratis (US 119): se compra como en la tienda.</summary>
    private IEnumerator Comprar(Predicate<ShopItem> cual)
    {
        PlayerLoadout tienda = jugador.GetComponent<PlayerLoadout>();
        ShopItem item = tienda.Catalog.items.Find(i => i != null && cual(i));
        Assert.IsNotNull(item, "No está en el catálogo");
        Assert.AreEqual(ShopResult.Ok, tienda.Buy(item), "No se pudo comprar " + item.displayName);
        yield return new WaitForSeconds(0.5f);
    }

    /// <summary>Le presta la habilidad al jugador, como el tutorial (US 166).</summary>
    private void Prestar(string habilidad)
    {
#if UNITY_EDITOR
        AbilityData datos = UnityEditor.AssetDatabase.LoadAssetAtPath<AbilityData>($"Assets/Data/Habilidades/{habilidad}.asset");
#else
        AbilityData datos = null;
#endif
        Assert.IsNotNull(datos, "Falta Assets/Data/Habilidades/" + habilidad);
        PlayerAbility habilidadDelJugador = jugador.GetComponent<PlayerAbility>();
        if (habilidadDelJugador == null) habilidadDelJugador = jugador.gameObject.AddComponent<PlayerAbility>();
        habilidadDelJugador.Prestar(datos);
    }

    /// <summary>Barrera: mantener la tecla (vista previa) y soltarla (CA1 a CA3).</summary>
    private IEnumerator LevantarBarrera()
    {
        Prestar("Barrera");
        KeyBindings.Apretar(GameAction.Habilidad);
        for (int i = 0; i < 15; i++) yield return null;
        yield return Escuchar(() => KeyBindings.Soltar(GameAction.Habilidad), Llamado("Barrera"));
    }

    private IEnumerator Tocar(GameAction tecla)
    {
        KeyBindings.Apretar(tecla);
        yield return null;
        yield return null;
        KeyBindings.Soltar(tecla);
        yield return null;
    }

    private IEnumerator Escuchar(Action accion, Func<AudioSource, bool> filtro = null, float segundos = 0.5f) =>
        Oido.Escuchar(accion, oidos, filtro, segundos);

    private IEnumerator EscucharTecla(GameAction tecla, Func<AudioSource, bool> filtro = null, float segundos = 0.5f)
    {
        yield return Escuchar(() => KeyBindings.Apretar(tecla), filtro, segundos);
        KeyBindings.Soltar(tecla);
    }

    /// <summary>Espera a que el jugador no esté sonando, así el próximo sonido se distingue del anterior.</summary>
    private IEnumerator Silencio()
    {
        float limite = Time.realtimeSinceStartup + 4f;
        while (Time.realtimeSinceStartup < limite && Array.Exists(jugador.GetComponentsInChildren<AudioSource>(), Oido.Suena)) yield return null;
    }

    private bool DelJugador(AudioSource fuente) => jugador != null && fuente.transform.IsChildOf(jugador.transform);

    private static Func<AudioSource, bool> Llamado(string nombre) => fuente => fuente.name.StartsWith(nombre);

    private Sonido Sono(string canal, Func<Sonido, bool> cual = null)
    {
        Sonido sonido = oidos.Find(o => o.canal == canal && (cual == null || cual(o)));
        Assert.IsNotNull(sonido, $"No sonó nada por el canal {canal}. Sonó: {Oido.Lista(oidos)}");
        return sonido;
    }

    private static void SinDireccion(Sonido sonido) =>
        Assert.IsFalse(sonido.es3D, "El que lo hace tendría que oírlo sin dirección: " + sonido);

    private static void SeEscuchaHasta(Sonido sonido, float metros)
    {
        Assert.IsTrue(sonido.es3D, "Tendría que oírse desde donde pasa: " + sonido);
        Assert.AreEqual(metros, sonido.alcance, 0.5f, "Alcance: " + sonido);
    }
}
