using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using static ShopUIKit;

// F08 (Modo Invasión), US 293: elegir 1 de 3 cuando se abre el puesto de suministros (US 292). Lo agrega
// PartidaDeInvasion. PROVISIONAL: la pantalla es para ver cómo queda; las cartas todavía no tienen efecto (US 295 y
// US 296) y las armas siguen la regla de la US 294.
// - CA1: 3 opciones, cada una con su nombre, su rareza, qué hace y con qué armas rinde. Se elige con el mouse o con
//   1, 2 y 3. Mientras se elige el respiro no corre (OleadasDeInvasion espera a TerminoLaEleccion). El color dice qué
//   es: blanca común, azul rara, violeta épica y roja un arma.
// - CA2: al abrirse, el puesto le suma a cada arma la mitad de su munición máxima (toda, con Logística: US 314).
// La pantalla no aparece de golpe: espera a que se vaya el cartel de la oleada, oscurece de a poco y las cartas
// entran de a una (también al volver a tirar).
// - CA3: la rareza de cada carta según la oleada (CartaDeInvasion.RarezaAlAzar).
// - CA4: «Volver a tirar» (R) cambia las 3 opciones por 150 puntos; en el mismo puesto, cada vez 100 más.
// - CA5: las cartas duran toda la partida y, si se repiten, se suman (se ven con Tab: MarcadorInvasion.Cartas), como
//   mucho 3 veces: después de la tercera esa carta no vuelve a salir. Las de una sola vez no vuelven a salir. Cada
//   carta sabe cuántas veces se tiene (Veces) para hacer más efecto la segunda y la tercera.
// - CA6: después de elegir arranca el respiro de 20 s.
// US 294 (provisional): en 1 de cada 3 tiradas una de las opciones es un arma que no se tiene, que reemplaza la del
// mismo lugar y viene con la munición llena.
// US 303: al superar una oleada del Jefe la elección se abre siempre, aunque el puesto no esté cargado (CA2), recién
// después de "Seguir" (CA4): es la elección del Jefe, sin comunes y con más chance de raras, épicas y legendarias (vale
// también al volver a tirar, solo en esa elección). Las legendarias no salen antes del primer Jefe (CA3).
// US 332, Entrega inmediata: con la carta, la elección se abre apenas el puesto llega al 100 % peleando, sin esperar
// al final de la oleada (no en las oleadas del Jefe, que tienen la suya). Jugando solo el juego se pausa mientras se
// elige. La munición del puesto se sigue dando al terminar la oleada, cuando el puesto se abre (ya sin elección).
// F31 (US 356), en Invasión cooperativa:
// - CA2: cada uno elige lo suyo y la partida no se pausa (tampoco con Entrega inmediata). El descanso arranca cuando
//   eligieron todos (OleadasDeInvasion.EsperarATodos).
// - CA3: arriba se ve un punto por cada jugador que elige, con su nombre; se pinta de verde cuando eligió.
// - CA4: cada uno tiene 30 s, con la cuenta a la vista; si se terminan, se elige al azar una de sus opciones (nunca
//   "Descartar una carta" ni "Seguir sin elegir").
// - CA5: se ve "La partida sigue".
// US 360 (límite de 8 cartas, F29):
// - CA1: 8 lugares; cada carta distinta ocupa uno (también las legendarias). Repetirla (mejorarla) y las armas no ocupan.
// - CA2: abajo se ven los lugares ocupados ("6 / 8") y las cartas que se tienen, con cuántas veces.
// - CA3: con los 8 ocupados salen solo mejoras de las que se tienen y armas (si no alcanzan, menos de 3). Si no hay
//   ninguna, quedan "Descartar una carta" y "Seguir sin elegir".
// - CA4: en la elección del Jefe la legendaria sale aunque no haya lugar, apagada ("Sin lugar"): no se puede elegir.
// - CA5: en cualquier elección se puede "Descartar una carta" (con la pantalla de las cartas, US 361): se pierde con sus
//   mejoras y esa vez no se elige. CA6: su efecto termina en el momento (CartaDeInvasion.quitar).
public class EleccionDeInvasion : MonoBehaviour
{
    public const int PrimeraTirada = 150, MasPorTirada = 100; // CA4
    public const int Lugares = 8; // US 360, CA1
    private const float AnchoCarta = 360f, AltoCarta = 470f, Separacion = 36f;

    /// <summary>La pantalla de la elección está abierta.</summary>
    public static bool Abierta { get; private set; }
    /// <summary>La elección de la partida en curso (o null).</summary>
    public static EleccionDeInvasion Actual { get; private set; }

    /// <summary>CA5: cuántas veces se eligió esa carta en la partida (0 si no se tiene).</summary>
    public static int Veces(CartaDeInvasion carta) =>
        Actual != null && carta != null && Actual.elegidas.TryGetValue(carta, out int n) ? n : 0;

    /// <summary>US 360: cuántos lugares están ocupados (cartas distintas que se tienen).</summary>
    public static int Ocupados => Actual != null ? Actual.elegidas.Count : 0;

    /// <summary>US 361: las cartas que se tienen, en el orden en que se eligieron.</summary>
    public static IReadOnlyList<CartaDeInvasion> Tenidas => Actual != null ? Actual.orden : (IReadOnlyList<CartaDeInvasion>)new CartaDeInvasion[0];

    /// <summary>CA5: cuántas veces se eligió la carta con ese nombre en la partida (0 si no se tiene).</summary>
    public static int Veces(string nombre)
    {
        foreach (CartaDeInvasion c in CartaDeInvasion.Todas) if (c.nombre == nombre) return Veces(c);
        return 0;
    }

    private class Opcion
    {
        public CartaDeInvasion carta;
        public ShopItem arma;
        public bool sinLugar; // US 360, CA4: una legendaria nueva con los lugares llenos (se ve, no se elige)
    }

    private OleadasDeInvasion oleadas;
    private PlayerLoadout equipo;
    private bool pendiente;           // US 303: superó una oleada del Jefe: se elige al seguir
    private bool delJefe;             // US 303, CA2: la que está abierta es la elección del Jefe
    private bool inmediata;           // US 332: la que está abierta es en medio de la oleada (Entrega inmediata)
    private float escalaDeTiempo = 1f; // US 332: la del juego antes de pausarlo
    private TextMeshProUGUI antetitulo;
    private int costo = PrimeraTirada;
    private readonly List<Opcion> opciones = new List<Opcion>();
    private readonly Dictionary<CartaDeInvasion, int> elegidas = new Dictionary<CartaDeInvasion, int>();
    private readonly List<CartaDeInvasion> orden = new List<CartaDeInvasion>();
    private readonly List<Behaviour> bloqueados = new List<Behaviour>();

    private const float Espera = 0.6f, Fundido = 0.35f, EntradaCarta = 0.4f, Escalon = 0.12f;
    private bool visible;
    private float mostrarEn, mostradaEn, cartasDesde;
    private CanvasGroup grupo, cabezaGrupo;
    private RectTransform cabeza;
    private readonly List<(CanvasGroup grupo, RectTransform rect, float x)> cartas = new List<(CanvasGroup, RectTransform, float)>();
    private RectTransform lienzo, fila;
    private TextMeshProUGUI tirarTexto, puntosTexto, tusCartas, vacia, enRed;
    public const float TiempoParaElegir = 30f; // US 356, CA4
    private RectTransform descartarBoton, seguirBoton;
    private UnityEngine.UI.Image tirarFondo;
    private TMP_FontAsset display, label;
    private Sprite redondeado;

    private void OnEnable()
    {
        Actual = this;
        PuestoDeSuministros.Abrio += AlAbrirElPuesto;
        PuestoDeSuministros.SeCargo += AlCargarseElPuesto;
        OleadasDeInvasion.Empezo += AlEmpezar;
        OleadasDeInvasion.Supero += AlSuperar;
    }

    private void OnDisable()
    {
        if (Actual == this) Actual = null;
        PuestoDeSuministros.Abrio -= AlAbrirElPuesto;
        PuestoDeSuministros.SeCargo -= AlCargarseElPuesto;
        OleadasDeInvasion.Empezo -= AlEmpezar;
        OleadasDeInvasion.Supero -= AlSuperar;
    }

    private void OnDestroy()
    {
        if (Abierta) Cerrar();
        if (lienzo != null) Destroy(lienzo.gameObject);
    }

    private void AlAbrirElPuesto()
    {
        if (oleadas == null) oleadas = GetComponent<OleadasDeInvasion>();
        // En las oleadas del Jefe la elección es la del Jefe, que se abre siempre (AlSuperar).
        if (oleadas != null && oleadas.DeJefe) return;
        // US 332: la elección ya se hizo apenas se cargó (Entrega inmediata): ahora solo la munición.
        if (PuestoDeSuministros.Actual != null && PuestoDeSuministros.Actual.Entregado) { DarMunicion(); return; }
        Abrir(false);
    }

    // US 332: con Entrega inmediata, la elección se abre apenas se carga el puesto (salvo en las oleadas del Jefe).
    private void AlCargarseElPuesto(PuestoDeSuministros puesto)
    {
        if (Abierta || Veces("Entrega inmediata") <= 0) return;
        if (oleadas == null) oleadas = GetComponent<OleadasDeInvasion>();
        if (oleadas != null && oleadas.DeJefe) return;
        inmediata = true; // (antes de abrir: la munición se da al terminar la oleada)
        Abrir(false);
        if (!Abierta) { inmediata = false; return; }
        escalaDeTiempo = Time.timeScale > 0f ? Time.timeScale : 1f;
        if (!CooperativoDeInvasion.Activo) Time.timeScale = 0f; // jugando solo, el juego se pausa mientras se elige (US 356: de a varios, no)
    }

    // US 303, CA2 y CA4: al superar una oleada del Jefe primero sale "Jefe derrotado"; la elección del Jefe espera a
    // que se siga (Retener), esté o no cargado el puesto.
    private void AlSuperar(int oleada)
    {
        if (OleadasDeInvasion.EsDeJefe(oleada)) pendiente = true;
    }

    // Ir a la oleada 10 (prueba) puede arrancar una oleada con la elección abierta.
    private void AlEmpezar(int oleada)
    {
        pendiente = false;
        if (inmediata) { inmediata = false; Time.timeScale = escalaDeTiempo; } // (el puesto nuevo no se entrega)
        if (Abierta) Cerrar();
    }

    /// <summary>Lo llama OleadasDeInvasion antes de arrancar el respiro: true si tiene que esperar a la elección.</summary>
    public bool Retener()
    {
        if (Abierta) return true;
        if (!pendiente) return false;
        pendiente = false;
        Abrir(true);
        return Abierta;
    }

    // ---------- Abrir y cerrar ----------

    private void Abrir(bool delJefe)
    {
        this.delJefe = delJefe;
        if (MatchHud.Instance == null) return;
        // US 356, CA2: en cooperativo, el que está muerto (o se retiró) no elige esa vez: vuelve en la oleada siguiente.
        if (CooperativoDeInvasion.Activo && CaidaEnInvasion.YoAfuera) return;
        if (equipo == null) equipo = FindFirstObjectByType<PlayerLoadout>();
        if (lienzo == null) Armar();

        if (!inmediata) DarMunicion(); // US 332: con Entrega inmediata, al terminar la oleada

        Abierta = true; // el respiro ya espera, aunque la pantalla todavía no se vea
        visible = false;
        costo = PrimeraTirada;
        mostrarEn = Time.unscaledTime + Espera;
    }

    // CA2: la mitad de la munición máxima de cada arma (con Logística, toda: US 314).
    private void DarMunicion()
    {
        // F31 (US 353, CA6): en cooperativo, la recarga es para todos siempre que haya al menos uno adentro del anillo.
        PuestoDeSuministros puesto = PuestoDeSuministros.Actual;
        if (CooperativoDeInvasion.Activo && puesto != null && puesto.Hay && !puesto.AlguienAdentro()) return;
        if (equipo == null) equipo = FindFirstObjectByType<PlayerLoadout>();
        WeaponSwitcher armas = equipo != null ? equipo.GetComponentInChildren<WeaponSwitcher>(true) : null;
        if (armas == null) return;
        if (Veces("Logística") > 0) armas.RellenarTodas();
        else armas.RecargarMitad();
    }

    // Cuando se fue el cartel de "Oleada N superada" (y un momento más), aparece la pantalla.
    private void Mostrar()
    {
        visible = true;
        mostradaEn = Time.unscaledTime;
        grupo.alpha = 0f;
        lienzo.gameObject.SetActive(true);
        if (antetitulo != null) antetitulo.text = delJefe ? L10n.T("gameplay.ui.boss_choice") : inmediata ? "Entrega inmediata" : L10n.T("gameplay.ui.supply_station");
        Bloquear();
        Tirar();
    }

    private void Animar()
    {
        float ahora = Time.unscaledTime;
        grupo.alpha = Suave((ahora - mostradaEn) / Fundido);
        float c = Suave((ahora - mostradaEn - 0.1f) / Fundido);
        cabeza.anchoredPosition = new Vector2(0f, -110f + 24f * (1f - c));
        cabezaGrupo.alpha = c;
        for (int i = 0; i < cartas.Count; i++)
        {
            var (g, r, x) = cartas[i];
            if (g == null) continue;
            float t = Suave((ahora - cartasDesde - i * Escalon) / EntradaCarta);
            g.alpha = t;
            r.anchoredPosition = new Vector2(x, -70f * (1f - t));
            r.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, t);
        }
    }

    private static float Suave(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t) * (1f - t); // frena al llegar
    }

    // Se puede elegir cuando ya entraron las cartas.
    private bool Lista => visible && Time.unscaledTime - cartasDesde >= Escalon * 2f + EntradaCarta * 0.8f;

    private void Cerrar()
    {
        if (inmediata) // US 332: sigue la oleada
        {
            inmediata = false;
            Time.timeScale = escalaDeTiempo;
            if (PuestoDeSuministros.Actual != null) PuestoDeSuministros.Actual.Entregar();
        }
        Abierta = false;
        visible = false;
        if (lienzo != null) lienzo.gameObject.SetActive(false);
        Desbloquear();
    }

    // Como la tienda: con la elección abierta el mouse maneja la pantalla y no se mira ni se dispara.
    private void Bloquear()
    {
        bloqueados.Clear();
        Bloquear(FindObjectsByType<CameraLook>(FindObjectsSortMode.None));
        Bloquear(FindObjectsByType<MeleeAttack>(FindObjectsSortMode.None));
        Bloquear(FindObjectsByType<Pistola>(FindObjectsSortMode.None));
        Bloquear(FindObjectsByType<Mitre>(FindObjectsSortMode.None));
        Bloquear(FindObjectsByType<ArmaDeFuego>(FindObjectsSortMode.None));
        Bloquear(FindObjectsByType<WeaponSwitcher>(FindObjectsSortMode.None));
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Bloquear<T>(T[] componentes) where T : Behaviour
    {
        foreach (T c in componentes)
        {
            if (!c.enabled) continue;
            c.enabled = false;
            bloqueados.Add(c);
        }
    }

    private void Desbloquear()
    {
        foreach (Behaviour c in bloqueados) if (c != null) c.enabled = true;
        bloqueados.Clear();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ---------- Las opciones ----------

    private void Tirar()
    {
        opciones.Clear();
        int oleada = oleadas != null ? oleadas.Oleada : 1;

        // US 294, CA3: como mucho un arma, en 1 de cada 3 tiradas.
        ShopItem arma = Random.value < 1f / 3f ? ArmaAlAzar() : null;
        int lugarArma = arma != null ? Random.Range(0, 3) : -1;
        var yaSalieron = new HashSet<CartaDeInvasion>();
        bool lleno = elegidas.Count >= Lugares; // US 360, CA3
        for (int i = 0; i < 3; i++)
        {
            if (i == lugarArma) { opciones.Add(new Opcion { arma = arma }); continue; }
            CartaDeInvasion carta = CartaAlAzar(oleada, yaSalieron, lleno);
            if (carta == null) continue;
            yaSalieron.Add(carta);
            opciones.Add(new Opcion { carta = carta, sinLugar = lleno && Veces(carta) == 0 });
        }
        Dibujar();
    }

    // CA3 (y US 303, CA2 y CA3): una carta de la rareza que tocó; si no hay ninguna de esa, de la más cercana (con
    // empate, la de abajo). Sin legendarias antes del primer Jefe y sin comunes en la elección del Jefe.
    // US 360, CA3 y CA4: con los lugares llenos, solo las que ya se tienen (mejoras); en la elección del Jefe, también
    // una legendaria nueva, que se ve pero no se puede elegir.
    private CartaDeInvasion CartaAlAzar(int oleada, HashSet<CartaDeInvasion> yaSalieron, bool lleno)
    {
        bool despuesDelJefe = oleadas != null && oleadas.JefesDerrotados > 0;
        RarezaDeCarta buscada = CartaDeInvasion.RarezaAlAzar(oleada, despuesDelJefe, delJefe);
        var posibles = new List<CartaDeInvasion>();
        int mejor = int.MaxValue;
        foreach (CartaDeInvasion c in CartaDeInvasion.Todas)
        {
            if (yaSalieron.Contains(c) || Veces(c) >= c.Maximo) continue; // CA5: ya la tiene todas las veces que se puede
            if (lleno && Veces(c) == 0 && !(delJefe && c.rareza == RarezaDeCarta.Legendaria)) continue; // US 360, CA3
            if (c.puedeSalir != null && !c.puedeSalir()) continue;          // US 331: Sin respaldo y Acople, por ejemplo
            if (c.rareza == RarezaDeCarta.Legendaria && !despuesDelJefe) continue; // US 303, CA3
            if (c.rareza == RarezaDeCarta.Legendaria && !CartaDeInvasion.LegendariasDisponibles) continue; // Sprint 4: todavía no hay (US 352)
            if (c.rareza == RarezaDeCarta.Comun && delJefe) continue;              // US 303, CA2
            int d = (int)c.rareza - (int)buscada;
            int lejos = d >= 0 ? d * 2 : -d * 2 - 1; // la de abajo gana el empate
            if (lejos > mejor) continue;
            if (lejos < mejor) { mejor = lejos; posibles.Clear(); }
            posibles.Add(c);
        }
        return posibles.Count > 0 ? posibles[Random.Range(0, posibles.Count)] : null;
    }

    // US 294, CA1 y CA2: un arma del catálogo que no se tiene (o una de las que solo existen en Invasión, como el Gran
    // Capitán: US 287).
    private ShopItem ArmaAlAzar()
    {
        if (equipo == null || equipo.Catalog == null) return null;
        var posibles = new List<ShopItem>();
        var armas = new List<ShopItem>(equipo.Catalog.items);
        armas.AddRange(ConfigInvasion.Actual.armasSoloInvasion);
        foreach (ShopItem item in armas)
        {
            if (item == null || equipo.IsEquipped(item)) continue;
            if (item.kind != ShopItemKind.PrimaryWeapon && item.kind != ShopItemKind.SecondaryWeapon) continue;
            if (item == equipo.Catalog.starterSecondary) continue; // la del arranque no es una novedad
            if (item.kind == ShopItemKind.SecondaryWeapon && Veces("Sin respaldo") > 0) continue; // US 331: ya no tiene secundaria
            posibles.Add(item);
        }
        return posibles.Count > 0 ? posibles[Random.Range(0, posibles.Count)] : null;
    }

    private void Elegir(int i)
    {
        if (!Abierta || !Lista || i < 0 || i >= opciones.Count || MisCartasInvasion.Abierta) return;
        Opcion o = opciones[i];
        if (o.sinLugar) { MatchHud.Warn(L10n.T("gameplay.card.no_room"), 2f); return; } // US 360, CA4
        if (o.arma != null) equipo.Recibir(o.arma); // US 294, CA4
        else if (o.carta != null)
        {
            int veces = elegidas.TryGetValue(o.carta, out int n) ? n + 1 : 1; // CA5: se suman
            elegidas[o.carta] = veces;
            if (!orden.Contains(o.carta)) orden.Add(o.carta);
            o.carta.aplicar?.Invoke(equipo != null ? equipo.gameObject : null, veces);
            ActualizarCartas();
        }
        Terminar();
    }

    // CA6: arranca el respiro (US 332: en medio de la oleada no).
    private void Terminar()
    {
        bool enLaOleada = inmediata;
        Cerrar();
        if (!enLaOleada && oleadas != null) oleadas.TerminoLaEleccion();
    }

    // US 360, CA3: sin opciones, se sigue sin elegir nada.
    private void SeguirSinElegir()
    {
        if (!Abierta || !Lista || MisCartasInvasion.Abierta) return;
        Terminar();
    }

    // US 360, CA5: abre la pantalla de las cartas (US 361) para elegir cuál se descarta.
    private void AbrirDescarte()
    {
        if (!Abierta || !Lista || MisCartasInvasion.Abierta || orden.Count == 0) return;
        MisCartasInvasion.AbrirParaDescartar(AlDescartar);
    }

    // US 360, CA5: se descartó una carta: esa vez no se elige.
    private void AlDescartar(CartaDeInvasion carta)
    {
        if (!Abierta || carta == null || !elegidas.ContainsKey(carta)) return;
        Descartar(carta);
        Terminar();
    }

    /// <summary>US 360, CA5 y CA6: la carta se pierde con sus mejoras, su lugar queda libre y su efecto termina.</summary>
    public void Descartar(CartaDeInvasion carta)
    {
        if (carta == null || !elegidas.Remove(carta)) return;
        orden.Remove(carta);
        if (equipo == null) equipo = FindFirstObjectByType<PlayerLoadout>();
        carta.quitar?.Invoke(equipo != null ? equipo.gameObject : null);
        ActualizarCartas();
        MatchHud.Warn(L10n.T("gameplay.card.discarded_fmt", L10n.Source(carta.nombre)), 2f);
    }

    /// <summary>Para probar (Riftwalker > Invasión > Darme una carta): la suma como si se la hubiera elegido.</summary>
    public void Dar(CartaDeInvasion carta)
    {
        if (carta == null || Veces(carta) >= carta.Maximo) { Debug.Log($"Ya tenés {carta?.nombre} todas las veces que se puede."); return; }
        if (Veces(carta) == 0 && orden.Count >= Lugares) { Debug.Log($"No hay lugar para {carta.nombre}: tenés {Lugares} cartas. Descartá una."); return; }
        if (equipo == null) equipo = FindFirstObjectByType<PlayerLoadout>(); // (sin haber abierto el puesto todavía)
        int veces = Veces(carta) + 1;
        elegidas[carta] = veces;
        if (!orden.Contains(carta)) orden.Add(carta);
        carta.aplicar?.Invoke(equipo != null ? equipo.gameObject : null, veces);
        ActualizarCartas();
        Debug.Log($"Carta de prueba: {carta.nombre} ({veces} de {carta.Maximo}).");
    }

    // US 298: la lista de «Tus cartas» (con Tab), con las repetidas sumadas.
    private void ActualizarCartas()
    {
        MarcadorInvasion.Cartas.Clear();
        foreach (CartaDeInvasion c in orden)
        {
            int n = elegidas[c];
            MarcadorInvasion.Cartas.Add(n > 1 ? $"{L10n.Source(c.nombre)} ×{n}" : L10n.Source(c.nombre));
        }
    }

    private void VolverATirar()
    {
        if (!Abierta || !Lista || equipo == null || equipo.Wallet == null) return;
        if (equipo.Wallet.Money < costo) { MatchHud.Warn(L10n.T("gameplay.card.reroll_missing_fmt", costo), 2f); return; }
        equipo.Wallet.Spend(costo);
        costo += MasPorTirada; // CA4
        Tirar();
    }

    private void Update()
    {
        if (!Abierta) return;
        if (!visible)
        {
            if (MatchHud.BannerVisible) mostrarEn = Mathf.Max(mostrarEn, Time.unscaledTime + 0.3f);
            if (Time.unscaledTime >= mostrarEn) Mostrar();
            return;
        }
        Animar();
        if (!Lista) return;
        if (CooperativoDeInvasion.Activo && EnRed()) return; // US 356, CA4: se terminó el tiempo y se eligió al azar
        if (MisCartasInvasion.Abierta) { } // US 361: descartando, las teclas son de esa pantalla
        else if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) Elegir(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) Elegir(1);
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) Elegir(2);
        else if (Input.GetKeyDown(KeyCode.R)) VolverATirar();
        if (Abierta && Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        if (Abierta) ActualizarTirar();
        if (Abierta && lienzo != null) lienzo.gameObject.SetActive(!MisCartasInvasion.Abierta); // la de las cartas va encima
    }

    // ---------- F31 (US 356): de a varios ----------

    // CA3 a CA5: los puntos de los que eligen, la cuenta y "La partida sigue". Devuelve true si se terminó el tiempo (y
    // se eligió al azar).
    private bool EnRed()
    {
        float queda = TiempoParaElegir - (Time.unscaledTime - mostradaEn);
        if (queda <= 0f)
        {
            MisCartasInvasion.CerrarSiEstaAbierta();
            var posibles = new List<int>();
            for (int i = 0; i < opciones.Count; i++) if (!opciones[i].sinLugar) posibles.Add(i);
            if (posibles.Count > 0) Elegir(posibles[Random.Range(0, posibles.Count)]);
            else Terminar(); // sin nada para elegir: sigue sin elegir
            return true;
        }
        if (enRed == null) return false;
        var sb = new System.Text.StringBuilder();
        if (!inmediata && oleadas != null)
        {
            foreach (Photon.Realtime.Player p in InvasoresEnRed.QuienesEligen())
            {
                bool listo = InvasoresEnRed.Listo(oleadas.Oleada, p.ActorNumber);
                sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(listo ? MarcadorInvasion.Verde : Mute)).Append(">●</color> ")
                  .Append(string.IsNullOrEmpty(p.NickName) ? "?" : p.NickName).Append("     ");
            }
        }
        sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(queda <= 10f ? Bad : Ink)).Append('>')
          .Append(Mathf.CeilToInt(queda)).Append(" s</color>     ")
          .Append(L10n.T("gameplay.card.match_continues"));
        enRed.text = sb.ToString();
        return false;
    }

    // ---------- Pantalla ----------

    private void Armar()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        display = MatchHud.Instance.DisplayFont;
        label = MatchHud.Instance.LabelFont;
        redondeado = MatchHud.Instance.Rounded;

        var go = new GameObject("Elección del puesto", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        go.layer = 5;
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 48; // sobre el HUD y el marcador, debajo de la tienda (50) y la pausa
        var escala = go.GetComponent<UnityEngine.UI.CanvasScaler>();
        escala.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        lienzo = (RectTransform)go.transform;

        grupo = go.AddComponent<CanvasGroup>();
        Image(Stretch(Node("Fondo", lienzo)), null, new Color(0f, 0f, 0f, DarkAlpha(0.72f)), 0f, true);

        cabeza = Node("Titulo", lienzo);
        cabezaGrupo = cabeza.gameObject.AddComponent<CanvasGroup>();
        cabeza.anchorMin = cabeza.anchorMax = cabeza.pivot = new Vector2(0.5f, 1f);
        cabeza.anchoredPosition = new Vector2(0f, -110f);
        cabeza.sizeDelta = new Vector2(1200f, 110f);
        antetitulo = Text(Place(Node("Antetitulo", cabeza), 0f, 0f, 1200f, 26f), label, 20f, MarcadorInvasion.Verde, TextAlignmentOptions.Center, 16f, true);
        antetitulo.text = L10n.T("gameplay.ui.supply_station");
        Text(Place(Node("Elegi", cabeza), 0f, 30f, 1200f, 64f), display, 58f, Ink, TextAlignmentOptions.Center, 4f, true)
            .text = L10n.T("gameplay.card.pick_one");

        fila = Node("Opciones", lienzo);
        fila.anchorMin = fila.anchorMax = fila.pivot = new Vector2(0.5f, 0.5f);
        fila.anchoredPosition = new Vector2(0f, -10f);
        fila.sizeDelta = new Vector2(AnchoCarta * 3f + Separacion * 2f, AltoCarta);

        // CA4: volver a tirar.
        RectTransform tirar = Node("VolverATirar", lienzo);
        tirar.anchorMin = tirar.anchorMax = tirar.pivot = new Vector2(0.5f, 0f);
        tirar.anchoredPosition = new Vector2(0f, 120f);
        tirar.sizeDelta = new Vector2(420f, 52f);
        tirarFondo = Image(tirar, redondeado, ChipColor, 8f, true);
        var boton = tirar.gameObject.AddComponent<UnityEngine.UI.Button>();
        boton.targetGraphic = tirarFondo;
        boton.onClick.AddListener(VolverATirar);
        tirarTexto = Text(Stretch(Node("Texto", tirar)), label, 20f, Ink, TextAlignmentOptions.Center, 8f, true);
        puntosTexto = Text(Node("Puntos", lienzo), label, 17f, Mute, TextAlignmentOptions.Center, 8f, true);
        RectTransform pr = puntosTexto.rectTransform;
        pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 0f);
        pr.anchoredPosition = new Vector2(0f, 84f);
        pr.sizeDelta = new Vector2(600f, 26f);

        // US 360, CA5 y CA3: «Descartar una carta» (a la izquierda de volver a tirar) y «Seguir sin elegir» (a la derecha,
        // solo si no hay opciones).
        descartarBoton = BotonAbajo("Descartar", -400f, L10n.T("gameplay.card.discard"), AbrirDescarte);
        seguirBoton = BotonAbajo("SeguirSinElegir", 400f, L10n.T("gameplay.card.skip"), SeguirSinElegir);
        // US 360, CA2: los lugares ocupados y las cartas que se tienen.
        tusCartas = Text(Node("TusCartas", lienzo), label, 18f, Soft, TextAlignmentOptions.Center, 4f);
        RectTransform tc = tusCartas.rectTransform;
        tc.anchorMin = tc.anchorMax = tc.pivot = new Vector2(0.5f, 0f);
        tc.anchoredPosition = new Vector2(0f, 196f);
        tc.sizeDelta = new Vector2(1500f, 30f);
        // US 360, CA3: sin opciones.
        vacia = Text(Node("SinOpciones", lienzo), label, 26f, Mute, TextAlignmentOptions.Center, 2f);
        RectTransform va = vacia.rectTransform;
        va.anchorMin = va.anchorMax = va.pivot = new Vector2(0.5f, 0.5f);
        va.anchoredPosition = new Vector2(0f, -10f);
        va.sizeDelta = new Vector2(1000f, 120f);
        vacia.textWrappingMode = TextWrappingModes.Normal;
        vacia.text = L10n.T("gameplay.card.no_options");

        // US 356, CA3 a CA5: quién eligió, la cuenta y "La partida sigue" (solo en cooperativo).
        if (CooperativoDeInvasion.Activo)
        {
            enRed = Text(Node("EnRed", lienzo), label, 19f, Soft, TextAlignmentOptions.Center, 4f);
            RectTransform er = enRed.rectTransform;
            er.anchorMin = er.anchorMax = er.pivot = new Vector2(0.5f, 1f);
            er.anchoredPosition = new Vector2(0f, -232f);
            er.sizeDelta = new Vector2(1500f, 30f);
        }

        lienzo.gameObject.SetActive(false);
    }

    private RectTransform BotonAbajo(string nombre, float x, string texto, UnityEngine.Events.UnityAction alHacerClic)
    {
        RectTransform rect = Node(nombre, lienzo);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(x, 120f);
        rect.sizeDelta = new Vector2(320f, 52f);
        UnityEngine.UI.Image fondo = Image(rect, redondeado, ChipColor, 8f, true);
        var boton = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        boton.targetGraphic = fondo;
        boton.onClick.AddListener(alHacerClic);
        Text(Stretch(Node("Texto", rect)), label, 20f, Ink, TextAlignmentOptions.Center, 8f, true).text = texto;
        return rect;
    }

    // US 360, CA2: "Tus cartas 6 / 8 · Mochila ×2 · Filo…", cada una del color de su rareza.
    private void PintarTusCartas()
    {
        var sb = new System.Text.StringBuilder(L10n.T("gameplay.card.slots_fmt", orden.Count, Lugares));
        foreach (CartaDeInvasion c in orden)
        {
            int n = elegidas[c];
            sb.Append("  ·  <color=#").Append(ColorUtility.ToHtmlStringRGB(CartaDeInvasion.ColorDe(c.rareza))).Append('>')
              .Append(L10n.Source(c.nombre)).Append(n > 1 ? " ×" + n : "").Append("</color>");
        }
        tusCartas.text = sb.ToString();
    }

    private void ActualizarTirar()
    {
        int puntos = equipo != null && equipo.Wallet != null ? equipo.Wallet.Money : 0;
        bool alcanza = puntos >= costo;
        string texto = L10n.T("gameplay.card.reroll_fmt", costo);
        if (tirarTexto.text != texto) tirarTexto.text = texto;
        tirarTexto.color = alcanza ? Ink : Mute;
        string tenes = L10n.T("gameplay.card.points_fmt", puntos);
        if (puntosTexto.text != tenes) puntosTexto.text = tenes;
    }

    private void Dibujar()
    {
        for (int i = fila.childCount - 1; i >= 0; i--) Destroy(fila.GetChild(i).gameObject);
        cartas.Clear();
        cartasDesde = Time.unscaledTime + (Time.unscaledTime - mostradaEn < 0.2f ? 0.25f : 0f); // la primera vez, después del fondo
        for (int i = 0; i < opciones.Count; i++)
        {
            float x = (opciones.Count - 1) * -0.5f * (AnchoCarta + Separacion) + i * (AnchoCarta + Separacion);
            DibujarCarta(i, opciones[i], x);
        }
        ActualizarTirar();
        // US 360: sin opciones, el aviso y «Seguir sin elegir»; «Descartar» si tiene alguna carta.
        vacia.gameObject.SetActive(opciones.Count == 0);
        seguirBoton.gameObject.SetActive(opciones.Count == 0);
        descartarBoton.gameObject.SetActive(orden.Count > 0);
        PintarTusCartas();
    }

    // US 334: avisa cuando la carta queda sin uso con Alimentación directa (o, si es Alimentación directa, a cuáles de
    // las que ya se tienen deja sin uso). null: no hay nada que avisar.
    private static string AvisoDeAlimentacion(CartaDeInvasion carta)
    {
        if (carta == null) return null;
        if (carta.nombre == CartaDeInvasion.AlimentacionDirecta)
        {
            var anuladas = new List<string>();
            foreach (string n in CartaDeInvasion.SinUsoConAlimentacion) if (Veces(n) > 0) anuladas.Add(L10n.Source(n));
            return anuladas.Count > 0 ? L10n.T("gameplay.card.disables_fmt", string.Join(", ", anuladas)) : null;
        }
        if (Veces(CartaDeInvasion.AlimentacionDirecta) > 0 && System.Array.IndexOf(CartaDeInvasion.SinUsoConAlimentacion, carta.nombre) >= 0)
            return L10n.T("gameplay.card.unused_direct_feed");
        return null;
    }

    private void DibujarCarta(int i, Opcion o, float x)
    {
        bool esArma = o.arma != null;
        Color color = esArma ? CartaDeInvasion.ColorArma : CartaDeInvasion.ColorDe(o.carta.rareza);
        string rareza = esArma ? L10n.T("gameplay.card.weapon") : L10n.Source(CartaDeInvasion.NombreDe(o.carta.rareza));
        string nombre = esArma ? NombreDelArma(o.arma) : L10n.Source(o.carta.nombre);
        string efecto = esArma ? QueReemplaza(o.arma) : L10n.Source(o.carta.EfectoParaLaProxima(Veces(o.carta))); // repetida: lo que da en total
        string rinde = esArma ? L10n.T(o.arma.kind == ShopItemKind.PrimaryWeapon ? "gameplay.card.primary" : "gameplay.card.secondary")
                              : L10n.T("gameplay.card.works_with_fmt", L10n.Source(o.carta.rindeCon));
        if (!esArma) rinde = AvisoDeAlimentacion(o.carta) ?? rinde; // US 334
        if (o.sinLugar) rinde = L10n.T("gameplay.card.no_room_short"); // US 360, CA4

        RectTransform carta = Node("Opcion" + (i + 1), fila);
        carta.anchorMin = carta.anchorMax = carta.pivot = new Vector2(0.5f, 0.5f);
        carta.anchoredPosition = new Vector2(x, 0f);
        carta.sizeDelta = new Vector2(AnchoCarta, AltoCarta);
        CanvasGroup grupoCarta = carta.gameObject.AddComponent<CanvasGroup>();
        grupoCarta.alpha = 0f;
        cartas.Add((grupoCarta, carta, x));
        MarcadorInvasion.Figura<FormaRedondeada>(Stretch(Node("Fondo", carta)))
            .Pintar(Rgb(12, 14, 19, 0.96f), color, 16f, 3f);

        // El clic en toda la carta.
        UnityEngine.UI.Image zona = Image(Stretch(Node("Zona", carta)), null, new Color(1f, 1f, 1f, 0f), 0f, true);
        var boton = zona.gameObject.AddComponent<UnityEngine.UI.Button>();
        boton.targetGraphic = zona;
        var colores = boton.colors;
        colores.highlightedColor = new Color(1f, 1f, 1f, 0.06f);
        colores.pressedColor = new Color(1f, 1f, 1f, 0.12f);
        colores.normalColor = new Color(1f, 1f, 1f, 0f);
        boton.colors = colores;
        int indice = i;
        boton.onClick.AddListener(() => Elegir(indice));

        // La franja de arriba con la rareza.
        Image(Place(Node("Franja", carta), 3f, 3f, AnchoCarta - 6f, 46f), redondeado, WithAlpha(color, 0.18f), 12f);
        Text(Place(Node("Rareza", carta), 0f, 3f, AnchoCarta, 46f), label, 18f, color, TextAlignmentOptions.Center, 18f, true).text = rareza;

        // El ícono del arma, si es un arma.
        float y = 76f;
        if (esArma && o.arma.icon != null)
        {
            UnityEngine.UI.Image icono = Image(Place(Node("Icono", carta), 40f, y, AnchoCarta - 80f, 90f), o.arma.icon, Ink);
            icono.preserveAspect = true;
            y += 104f;
        }

        TextMeshProUGUI titulo = Text(Place(Node("Nombre", carta), 24f, y, AnchoCarta - 48f, 92f), display, 36f, Ink, TextAlignmentOptions.Top, 4f, true);
        titulo.text = nombre;
        titulo.textWrappingMode = TextWrappingModes.Normal;
        y += 100f;
        Image(Place(Node("Linea", carta), AnchoCarta / 2f - 40f, y, 80f, 3f), null, color);
        y += 20f;
        TextMeshProUGUI detalle = Text(Place(Node("Efecto", carta), 28f, y, AnchoCarta - 56f, AltoCarta - y - 90f), label, 21f, Soft, TextAlignmentOptions.Top);
        detalle.text = efecto;
        detalle.textWrappingMode = TextWrappingModes.Normal;

        Text(Place(Node("Rinde", carta), 24f, AltoCarta - 84f, AnchoCarta - 48f, 24f), label, 16f, Mute, TextAlignmentOptions.Center, 6f, true).text = rinde;

        // La tecla.
        RectTransform tecla = Place(Node("Tecla", carta), AnchoCarta / 2f - 16f, AltoCarta - 50f, 32f, 32f);
        Image(tecla, redondeado, color, 6f);
        TextMeshProUGUI numero = Text(Stretch(Node("Numero", tecla)), display, 18f, KeyInk, TextAlignmentOptions.Center);
        numero.text = (i + 1).ToString();

        // US 360, CA4: sin lugar, la carta se ve apagada (no se puede elegir).
        if (o.sinLugar)
        {
            Image(Stretch(Node("SinLugar", carta)), null, new Color(0f, 0f, 0f, DarkAlpha(0.55f)), 0f).raycastTarget = false;
            tecla.gameObject.SetActive(false);
        }
    }

    private static string NombreDelArma(ShopItem arma) => L10n.Source(
        !string.IsNullOrEmpty(arma.displayName) ? arma.displayName : !string.IsNullOrEmpty(arma.alias) ? arma.alias : arma.name);

    // US 294, CA4: la opción dice a cuál reemplaza.
    private string QueReemplaza(ShopItem arma)
    {
        ShopItem actual = equipo == null ? null : arma.kind == ShopItemKind.PrimaryWeapon ? equipo.Primary : equipo.Secondary;
        string lugar = L10n.T(arma.kind == ShopItemKind.PrimaryWeapon ? "gameplay.card.slot.primary" : "gameplay.card.slot.secondary");
        return actual != null
            ? L10n.T("gameplay.card.replaces_fmt", NombreDelArma(actual))
            : L10n.T("gameplay.card.empty_slot_fmt", lugar);
    }
}
