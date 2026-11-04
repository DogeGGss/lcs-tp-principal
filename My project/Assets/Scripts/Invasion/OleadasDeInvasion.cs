using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// F08 (Modo Invasión), US 144: las oleadas de enemigos. La crea PartidaDeInvasion.
// - El descanso de 20 s antes de cada oleada es la fase de compra de la escena (BuyPhase): al terminar arranca la
//   oleada y, al superarla, vuelve a empezar.
// - CA1: la oleada N trae 6 + 3 × (N − 1) enemigos (6 en la 1, 9 en la 2, 33 en la 10).
// - CA2: como máximo 12 vivos a la vez; los que faltan aparecen a medida que mueren los anteriores.
// US 299 (más invasores) reemplaza CA1 y CA2: la oleada N trae 10 + 5 × (N − 1) invasores en Normal (10 en la 1, 55 en
// la 10, 105 en la 20) y el tope de vivos a la vez es 15 en la 1 y sube de a uno por oleada hasta 30. Las cifras están
// en ConfigInvasion para ajustarlas probando.
// - CA3: en la 10 aparece el Jefe (US 146) cuando cayó la mitad de la oleada: va aparte (no cuenta en el tope de vivos).
// US 300 reemplaza los tipos por oleada (CA3): cada oleada trae su mezcla, en % (ConfigInvasion.mezcla): en la 1 y la
//   2, solo Comunes; en la 3, 75 % Comunes y 25 % Rápidos; en la 4 y la 5, 60 % Comunes, 25 % Rápidos, 10 % Watchers
//   tiradores (US 286) y 5 % Tanques; de la 6 a la 10, 45 % Comunes, 25 % Rápidos, 10 % Tanques, 10 % tiradores y 10 %
//   francotiradores (US 285); desde la 11, 35 %, 25 %, 15 %, 15 % y 10 %. Los Rápidos salen en jaurías de 4 a 6, todos
//   por el mismo portal y uno atrás del otro (CA2).
// - CA4 (US 269): salen de portales (PortalDeEnemigos) que se abren en lugares al azar del NavMesh entre 15 m y 50 m
//   del jugador, conectados con él caminando y, si se puede, fuera de su vista. Cada vez se mandan de 3 a 5 enemigos a
//   un portal, que los larga de a uno (con el tope de 12 vivos). Hay como mucho 3: los que ya están abiertos se vuelven
//   a usar y quedan abiertos hasta que termina la oleada. El Jefe sale solo de un portal grande.
//   Si no se encuentra lugar (o todavía no hay NavMesh), el portal se abre en los puntos de aparición del mapa (US 187,
//   AparicionDeEnemigos), de reserva, y si no hay, alrededor del jugador.
// - CA5: la oleada termina cuando mueren todos (en la 10, también el Jefe): "Oleada N superada" y descanso.
// US 303 reemplaza el final de la 10 (CA6) y el modo infinito (CA7):
// - CA4: al superar cada oleada del Jefe, "Jefe derrotado" con "Seguir" (la elección del Jefe, EleccionDeInvasion, y
//   el respiro) y "Retirarse" (el resultado y el récord, US 151).
// - CA1: cada Jefe que cae hace que todos los invasores que salen después (salvo el Jefe, que tiene lo de su vuelta)
//   tengan un 25 % más de vida y peguen un 10 % más, y se acumula (los números en ConfigInvasion).
// US 161, CA3: la dificultad cambia la cantidad de cada oleada (la vida de cada enemigo la ajusta Npc).
// US 301, CA1: desde la oleada 4, 1 de cada 15 invasores que salen (de cualquier tipo, salvo el Jefe) es un alfa
// (VarianteAlfa, con los números en ConfigInvasion). La cuenta sigue de una oleada a la otra; si un alfa se pierde sin
// morir, el que sale en su lugar también es alfa.
// US 302: el Jefe sale en las oleadas 10, 20, 30 y así cada 10 (en cada una, como en la 10: a mitad de la oleada, de un
// portal grande), cada vez más duro (los números en ConfigInvasion; la vida, en Normal, y la dificultad la cambia):
// - CA2: en la 20, 3.500 de vida, se enfurece al 75 % y salen con él 2 Tanques de escolta por su portal.
// - CA3: en la 30, 5.000 de vida, arranca enfurecido y embiste dos veces seguidas.
// - CA4: desde la 40, 1.500 de vida más cada vez, y sus nubes de veneno más grandes y sus vuelos más seguidos.
// Lo de cada vuelta se suma a lo de las anteriores (la escolta y las embestidas dobles siguen después).
// El marcador (la oleada, cuántos quedan, el descanso y los puntos) es la US 150: MarcadorInvasion.
public class OleadasDeInvasion : MonoBehaviour
{
    public const int Ultima = 10;                         // CA6
    private const float Cerca = 15f, Lejos = 50f;         // CA4: m del jugador

    /// <summary>La oleada en curso (0 antes de la primera).</summary>
    public int Oleada { get; private set; }
    /// <summary>Está en el descanso entre oleadas.</summary>
    public bool Descanso => fase == Fase.Descanso;
    /// <summary>Enemigos que quedan en la oleada, contando los que todavía no aparecieron (US 150, CA2).</summary>
    public int Quedan => Mathf.Max(0, total - muertos) + (jefeVivo ? 1 : 0);
    /// <summary>US 303: los Jefes que ya cayeron en la partida.</summary>
    public int JefesDerrotados { get; private set; }
    /// <summary>Ya cayó algún Jefe (siguió después de la 10).</summary>
    public bool Infinito => JefesDerrotados > 0;
    /// <summary>Enemigos de la oleada en curso, con el Jefe en las de jefe (US 150: la barra de los que quedan).</summary>
    public int Total => total + (DeJefe && tipoJefe != null ? 1 : 0);
    /// <summary>US 302, CA1: la oleada en curso trae al Jefe (la 10, la 20, la 30...).</summary>
    public bool DeJefe => EsDeJefe(Oleada);
    /// <summary>US 302, CA1: esa oleada trae al Jefe.</summary>
    public static bool EsDeJefe(int oleada) => oleada > 0 && oleada % Ultima == 0;
    /// <summary>US 302: qué vuelta es esa oleada (1 hasta la 10, 2 hasta la 20...).</summary>
    public static int Vuelta(int oleada) => Mathf.Max(1, (oleada + Ultima - 1) / Ultima);
    /// <summary>Está la pantalla de "¡Sobreviviste!" (US 150: el marcador se oculta).</summary>
    public bool EnElFinal => fase == Fase.Final;

    /// <summary>Avisa cada oleada que empieza y cada oleada superada (para el marcador y los puntos).</summary>
    public static event System.Action<int> Empezo, Supero;

    /// <summary>Avisa cuando sale el cartel de "¡Llegó el jefe!" (US 298: el marcador se muestra un momento).</summary>
    public static event System.Action LlegoElJefe;

    private enum Fase { Descanso, Oleada, Final }

    private Fase fase = Fase.Descanso;
    private int total, muertos;
    private int salidosParaAlfa, alfasPerdidos; // US 301, CA1
    private int escolta, escoltaPorSalir; // US 302, CA2: los Tanques que salen con el Jefe (cuántos en la oleada y cuántos faltan)
    // Los que faltan mandar a un portal, en grupos: uno solo, o una jauría de Rápidos (US 300, CA2), que va entera a un portal.
    private readonly Queue<List<TipoDeNpc>> porAparecer = new Queue<List<TipoDeNpc>>();
    private readonly List<PortalDeEnemigos> portales = new List<PortalDeEnemigos>(); // US 269
    private PortalDeEnemigos portalDelJefe;
    private float proximoPortal;
    private const int MaximoPortales = 3, PorPortalMin = 3, PorPortalMax = 5; // US 269, CA3
    private const float EntrePortales = 1.2f, EntreSalidas = 0.8f;             // s
    private const float EntreSalidasDeJauria = 0.3f;                           // s: la jauría sale junta (US 300, CA2)
    private readonly List<Npc> vivos = new List<Npc>();
    private readonly Dictionary<Npc, TipoDeNpc> tipos = new Dictionary<Npc, TipoDeNpc>(); // de qué tipo es cada uno
    private bool jefeSalio, jefeVivo, jefeMuerto;
    private readonly Dictionary<Npc, float> aparecio = new Dictionary<Npc, float>();
    private Transform jugadorAhora;
    private Npc jefe;
    private TipoDeNpc comun, rapido, tanque, tirador, francotirador, tipoJefe;
    private BuyPhase descanso;
    private GameObject finalUi;

    private void Start()
    {
        // Una sola por partida (si por algo se creara otra, las dos llevarían oleadas distintas y se pisarían).
        foreach (OleadasDeInvasion otra in FindObjectsByType<OleadasDeInvasion>(FindObjectsSortMode.None))
            if (otra != this && otra.isActiveAndEnabled) { Destroy(this); return; }
        comun = TipoDeNpc.Buscar("Comun");
        rapido = TipoDeNpc.Buscar("Rapido");
        tanque = TipoDeNpc.Buscar("Tanque");
        tirador = TipoDeNpc.Buscar("Tirador");             // US 286
        francotirador = TipoDeNpc.Buscar("Francotirador"); // US 285
        tipoJefe = System.Array.Find(TipoDeNpc.Todos(), t => t.esJefe);
        if (AparicionDeEnemigos.Todos.Count == 0) Debug.LogWarning("[Invasión] El mapa no tiene puntos de aparición (Riftwalker > Invasión > Crear 3 puntos de aparición): los enemigos aparecen en el NavMesh alrededor del jugador.");

        descanso = BuyPhase.Current;
        if (descanso != null)
        {
            descanso.SinRondasDePrueba(); // la tienda solo se abre en el descanso, no en medio de la oleada
            descanso.Ended += AlTerminarDescanso;
        }
        else Empezar(); // sin fase de compra en la escena: arranca directo
    }

    private void OnDestroy()
    {
        if (descanso != null) descanso.Ended -= AlTerminarDescanso;
        if (finalUi != null) Destroy(finalUi);
    }

    private void AlTerminarDescanso() => Empezar();

    // ---------- La oleada ----------

    private void Empezar()
    {
        if (fase != Fase.Descanso) return; // solo desde el descanso: una oleada no corta a la anterior
        Oleada++;
        fase = Fase.Oleada;
        total = Mathf.Max(1, Mathf.RoundToInt(ConfigInvasion.Actual.Invasores(Oleada) * DificultadDeInvasion.Cantidad)); // US 299, CA1 (y CA7), US 161
        // US 302, CA2: desde la 20, el Jefe llega con su escolta (cuentan en la oleada).
        escolta = DeJefe && Vuelta(Oleada) >= 2 && tanque != null && tipoJefe != null ? ConfigInvasion.Actual.escoltaDelJefe : 0;
        escoltaPorSalir = escolta;
        total += escolta;
        muertos = 0;
        vivos.Clear();
        tipos.Clear();
        aparecio.Clear();
        jefeSalio = jefeVivo = jefeMuerto = false;
        jefe = null;
        porAparecer.Clear();
        foreach (List<TipoDeNpc> grupo in Composicion(Oleada, total - escolta)) porAparecer.Enqueue(grupo);
        proximoPortal = Time.time + 1f;
        MatchHud.ShowBanner("Modo Invasión", $"Oleada {Oleada}", ShopUIKit.Bad, DeJefe ? "Llega el jefe" : $"{total} enemigos", null, 2.5f);
        Empezo?.Invoke(Oleada);
    }

    // US 300, CA1 y CA2: los invasores de la oleada según su mezcla, mezclados, con los Rápidos en jaurías (cada jauría es
    // un grupo). Las cantidades se redondean sin perder ninguno: los que faltan van a los que tienen la parte más grande
    // que sobra. Lo de un tipo que no está (y lo que no llega a 100) va a los Comunes.
    private List<List<TipoDeNpc>> Composicion(int n, int cuantos)
    {
        ConfigInvasion config = ConfigInvasion.Actual;
        MezclaDeOleada m = config.MezclaDe(n);
        TipoDeNpc[] tiposDeLaMezcla = { comun, rapido, tanque, tirador, francotirador };
        float[] partes = { m.comunes, m.rapidos, m.tanques, m.tiradores, m.francotiradores };
        for (int k = 1; k < partes.Length; k++)
            if (tiposDeLaMezcla[k] == null) { partes[0] += partes[k]; partes[k] = 0f; }
        float suma = 0f;
        foreach (float p in partes) suma += Mathf.Max(0f, p);
        if (suma < 100f) partes[0] += 100f - suma; // lo que falta, Comunes
        suma = Mathf.Max(100f, suma);

        var cantidad = new int[partes.Length];
        var sobra = new float[partes.Length];
        int usados = 0;
        for (int k = 0; k < partes.Length; k++)
        {
            float exacto = cuantos * Mathf.Max(0f, partes[k]) / suma;
            cantidad[k] = Mathf.FloorToInt(exacto);
            sobra[k] = exacto - cantidad[k];
            usados += cantidad[k];
        }
        for (; usados < cuantos; usados++)
        {
            int mayor = 0;
            for (int k = 1; k < partes.Length; k++) if (sobra[k] > sobra[mayor]) mayor = k;
            cantidad[mayor]++;
            sobra[mayor] = -1f;
        }

        var grupos = new List<List<TipoDeNpc>>();
        for (int k = 0; k < partes.Length; k++)
        {
            if (k == 1) continue; // los Rápidos, en jaurías
            for (int i = 0; i < cantidad[k]; i++) grupos.Add(new List<TipoDeNpc> { tiposDeLaMezcla[k] });
        }
        grupos.AddRange(Jaurias(cantidad[1], config));

        for (int i = grupos.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (grupos[i], grupos[j]) = (grupos[j], grupos[i]);
        }
        grupos.RemoveAll(g => g.Count == 0 || g[0] == null);
        return grupos;
    }

    // US 300, CA2: los Rápidos repartidos en jaurías de 4 a 6, parejas. Si no alcanzan para que todas lleguen a 4 (por
    // ejemplo, 7), alguna sale más chica: nunca pasan de 6.
    private List<List<TipoDeNpc>> Jaurias(int rapidos, ConfigInvasion config)
    {
        var jaurias = new List<List<TipoDeNpc>>();
        if (rapidos <= 0 || rapido == null) return jaurias;
        int minima = Mathf.Max(1, config.jauriaMinima), maxima = Mathf.Max(minima, config.jauriaMaxima);
        int pocas = Mathf.CeilToInt(rapidos / (float)maxima);          // las menos jaurías posibles (de hasta 6)
        int muchas = Mathf.Max(pocas, rapidos / minima);                // las más (de al menos 4)
        int cuantas = Random.Range(pocas, muchas + 1);
        for (int i = 0; i < cuantas; i++)
        {
            int tam = rapidos / cuantas + (i < rapidos % cuantas ? 1 : 0);
            var jauria = new List<TipoDeNpc>(tam);
            for (int r = 0; r < tam; r++) jauria.Add(rapido);
            jaurias.Add(jauria);
        }
        return jaurias;
    }

    private bool navMeshPedido;

    private void Update()
    {
        // El NavMesh del mapa se arma con el juego andando (NavMeshEnVivo) recién cuando alguien lo pide: se pide ya,
        // durante el primer descanso, así está listo cuando aparecen los enemigos (sin él no se podía elegir dónde).
        if (!navMeshPedido)
        {
            Transform yo = Jugador();
            if (yo != null) { navMeshPedido = true; NavMeshEnVivo.Preparar(yo.position); }
        }

        if (fase != Fase.Oleada) return;

        jugadorAhora = Jugador();
        // Los que cayeron cuentan como muertos. Los que desaparecieron sin morir (se borraron, se cayeron del mapa o
        // no pudieron llegar al NavMesh) vuelven a la fila: la oleada no se termina sin matarlos.
        for (int i = vivos.Count - 1; i >= 0; i--)
        {
            Npc n = vivos[i];
            if (n != null && n.Vivo && !Perdido(n)) continue;
            vivos.RemoveAt(i);
            if (n != null && !n.Vivo) muertos++;
            else
            {
                if (tipos.TryGetValue(n, out TipoDeNpc t) && t != null) porAparecer.Enqueue(new List<TipoDeNpc> { t });
                if (n != null && n.Alfa != null) alfasPerdidos++; // US 301: vuelve a salir alfa
                if (n != null) Destroy(n.gameObject);
            }
            tipos.Remove(n);
            aparecio.Remove(n);
        }
        Transform jugador = jugadorAhora;
        if (jugador == null) return;

        // US 269: los portales. Cuando hacen falta más enemigos de los que ya esperan en los portales, se manda un grupo
        // a uno: uno nuevo si hay menos de 3 abiertos y se encuentra lugar; si no, el abierto que menos tiene en fila.
        portales.RemoveAll(p => p == null || p.Cerrando);
        int esperando = 0;
        foreach (PortalDeEnemigos p in portales) esperando += p.Fila.Count;
        int maximoVivos = ConfigInvasion.Actual.TopeDeVivos(Oleada); // US 299, CA2
        if (porAparecer.Count > 0 && Time.time >= proximoPortal && vivos.Count + esperando < maximoVivos)
        {
            PortalDeEnemigos portal = null;
            if (portales.Count < MaximoPortales && LugarDePortal(jugador, out Vector3 lugar))
            {
                portal = PortalDeEnemigos.Abrir(lugar, jugador.position, ConfigInvasion.Actual.escala);
                portal.Proximo = Time.time + 0.6f; // que termine de abrirse
                portales.Add(portal);
            }
            else foreach (PortalDeEnemigos p in portales) if (portal == null || p.Fila.Count < portal.Fila.Count) portal = p;
            if (portal != null)
            {
                proximoPortal = Time.time + EntrePortales;
                // Una jauría va entera a un portal (US 300, CA2); si no, de 3 a 5 sueltos (sin cortar una jauría).
                if (porAparecer.Peek().Count > 1) foreach (TipoDeNpc t in porAparecer.Dequeue()) portal.Fila.Enqueue(t);
                else
                {
                    int cuantos = Random.Range(PorPortalMin, PorPortalMax + 1);
                    for (int i = 0; i < cuantos && porAparecer.Count > 0 && porAparecer.Peek().Count == 1; i++)
                        portal.Fila.Enqueue(porAparecer.Dequeue()[0]);
                }
            }
        }

        // CA2: cada portal larga los suyos de a uno, sin pasar del tope de vivos (US 299). Quedan abiertos hasta que termina la oleada.
        foreach (PortalDeEnemigos portal in portales)
        {
            if (portal.Fila.Count == 0) continue;
            if (!portal.Listo || vivos.Count >= maximoVivos || Time.time < portal.Proximo) continue;
            TipoDeNpc tipo = portal.Fila.Dequeue();
            // La jauría sale junta: el Rápido que sigue a otro, enseguida (US 300, CA2).
            portal.Proximo = Time.time + (tipo == rapido && portal.Fila.Count > 0 && portal.Fila.Peek() == rapido ? EntreSalidasDeJauria : EntreSalidas);
            Npc npc = Aparecer(tipo, portal.Salida, jugador);
            if (npc != null) { vivos.Add(npc); tipos[npc] = tipo; aparecio[npc] = Time.time; }
            else total--; // el tipo no tiene modelo: no cuenta
        }

        // CA3 (US 146, CA1 y US 269, CA5): el Jefe, a mitad de la oleada 10 (US 302: y de la 20, la 30...), de un portal
        // grande (que queda abierto hasta el final de la oleada).
        Vector3 dondeJefe = Vector3.zero;
        if (DeJefe && !jefeSalio && (portalDelJefe == null || portalDelJefe.Fila.Count == 0) && tipoJefe != null
            && muertos >= (total - escolta) / 2 && (portalDelJefe != null || LugarDePortal(jugador, out dondeJefe)))
        {
            if (portalDelJefe == null) portalDelJefe = PortalDeEnemigos.Abrir(dondeJefe, jugador.position, ConfigInvasion.Actual.escalaJefe);
            portalDelJefe.Fila.Enqueue(tipoJefe); // (si el jefe se perdió, vuelve a salir por el mismo portal)
            MatchHud.ShowBanner($"Oleada {Oleada}", "¡Llegó el jefe!", ShopUIKit.Bad, escoltaPorSalir > 0 ? "Con su escolta" : "", null, 3f);
            LlegoElJefe?.Invoke();
        }
        if (portalDelJefe != null && !portalDelJefe.Cerrando && portalDelJefe.Listo && portalDelJefe.Fila.Count > 0
            && Time.time >= portalDelJefe.Proximo)
        {
            portalDelJefe.Fila.Dequeue();
            jefeSalio = true;
            jefe = Aparecer(tipoJefe, portalDelJefe.Salida, jugador);
            jefeVivo = jefe != null;
            if (jefe != null) { aparecio[jefe] = Time.time; Endurecer(jefe); }
            portalDelJefe.Proximo = Time.time + 1.5f;
        }
        // US 302, CA2: la escolta sale del mismo portal, después del Jefe, de a uno (sin tope de vivos: viene con él).
        if (jefeSalio && escoltaPorSalir > 0 && portalDelJefe != null && !portalDelJefe.Cerrando && portalDelJefe.Listo
            && Time.time >= portalDelJefe.Proximo)
        {
            escoltaPorSalir--;
            portalDelJefe.Proximo = Time.time + 1f;
            Npc t = Aparecer(tanque, portalDelJefe.Salida, jugador);
            if (t != null) { vivos.Add(t); tipos[t] = tanque; aparecio[t] = Time.time; }
            else total--;
        }

        // El jefe: si murió, listo; si desapareció sin morir, vuelve a aparecer.
        if (jefeVivo && jefe != null && !jefe.Vivo) { jefeVivo = false; jefeMuerto = true; JefesDerrotados++; } // US 303, CA1
        else if (jefeVivo && (jefe == null || Perdido(jefe))) { if (jefe != null) Destroy(jefe.gameObject); jefe = null; jefeVivo = false; jefeSalio = false; }

        // CA5: superada (en las del Jefe, solo con el jefe muerto y su escolta afuera).
        bool faltaJefe = DeJefe && tipoJefe != null && (!jefeMuerto || escoltaPorSalir > 0);
        int pendientes = 0;
        foreach (PortalDeEnemigos p in portales) if (p != null && !p.Cerrando) pendientes += p.Fila.Count;
        if (portalDelJefe != null) pendientes += portalDelJefe.Fila.Count;
        if (porAparecer.Count == 0 && pendientes == 0 && vivos.Count == 0 && !faltaJefe) Superar();
    }

    // Vivo pero perdido: se cayó del mapa (muy por debajo del jugador) o, a los 10 s de aparecer, todavía no pudo
    // subirse al NavMesh (quedó adentro de algo).
    private bool Perdido(Npc n)
    {
        if (jugadorAhora != null && n.transform.position.y < jugadorAhora.position.y - 40f) return true;
        if (aparecio.TryGetValue(n, out float desde) && Time.time - desde > 10f)
        {
            PersecucionDeNpc p = n.GetComponent<PersecucionDeNpc>();
            if (p != null && p.Agente == null) return true;
        }
        return false;
    }

    private Npc Aparecer(TipoDeNpc tipo, Vector3 lugar, Transform jugador)
    {
        Vector3 hacia = jugador.position - lugar;
        hacia.y = 0f;
        VarianteAlfa alfa = tipo.esJefe ? null : SaleAlfa();
        Npc npc = Npc.Crear(tipo, lugar, hacia.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(hacia) : Quaternion.identity, true, alfa);
        if (npc == null) return null;
        // US 303, CA1: más vida y más daño por cada Jefe que cayó (sobre la de la dificultad, que ya pone Npc, US 161; y la
        // del alfa, US 301). El Jefe no: tiene lo de su vuelta (US 302, Endurecer).
        if (JefesDerrotados > 0 && npc.Vida != null && !tipo.esJefe)
        {
            ConfigInvasion config = ConfigInvasion.Actual;
            int vida = Mathf.RoundToInt(DificultadDeInvasion.VidaDeEnemigo(tipo.vida) * (1f + config.vidaPorJefe * JefesDerrotados)
                                        * (npc.Alfa != null ? npc.Alfa.vida : 1f));
            npc.Vida.maxHealth = vida;
            npc.Vida.SetState(vida, 0);
            npc.MasDanioPorJefes = 1f + config.danioPorJefe * JefesDerrotados;
        }
        return npc;
    }

    // US 302, CA2 a CA4: el Jefe de esta vuelta.
    private void Endurecer(Npc j)
    {
        int vuelta = Vuelta(Oleada);
        if (vuelta <= 1 || j.Vida == null) return; // la 10: el Jefe de siempre
        ConfigInvasion config = ConfigInvasion.Actual;
        int vida = DificultadDeInvasion.VidaDeEnemigo(j.Tipo.vida + config.vidaDelJefePorVuelta * (vuelta - 1)); // US 161
        j.Vida.maxHealth = vida;
        j.Vida.SetState(vida, 0);

        FuriaDeNpc furia = j.GetComponent<FuriaDeNpc>();
        float umbral = vuelta >= 3 ? 1f : config.furiaDesdeLaVuelta2; // CA3: desde la 30 arranca enfurecido
        if (furia != null) furia.Umbral = umbral;
        if (j.Barra != null) j.Barra.MarcaDeFuria(umbral);

        EmbestidaDeNpc embestida = j.GetComponent<EmbestidaDeNpc>();
        if (embestida != null && vuelta >= 3) embestida.Seguidas = config.embestidasSeguidas; // CA3

        VueloDeNpc vuelo = j.GetComponent<VueloDeNpc>();
        if (vuelo != null && vuelta >= 4) // CA4
        {
            int extra = vuelta - 3;
            vuelo.TamanioDeNubes = 1f + config.nubesMasGrandesPorVuelta * extra;
            vuelo.EntreVuelos = Mathf.Pow(config.vuelosPorVuelta, extra);
        }
    }

    // US 301, CA1: si el que sale ahora es un alfa (el que reemplaza a un alfa perdido, o 1 de cada 15 desde la oleada 4).
    private VarianteAlfa SaleAlfa()
    {
        ConfigInvasion config = ConfigInvasion.Actual;
        if (Oleada < config.alfasDesdeLaOleada) return null;
        if (alfasPerdidos > 0) { alfasPerdidos--; return config.alfa; }
        salidosParaAlfa++;
        if (salidosParaAlfa < Mathf.Max(1, config.unAlfaCada)) return null;
        salidosParaAlfa = 0;
        return config.alfa;
    }

    private void Superar()
    {
        CerrarPortales();
        Supero?.Invoke(Oleada);
        if (DeJefe)
        {
            fase = Fase.Final;
            Final(); // US 303, CA4
            return;
        }
        MatchHud.ShowBanner("Modo Invasión", $"Oleada {Oleada} superada", ShopUIKit.Ok, "Descanso de 20 s", null, 3f);
        ArrancarDescanso();
    }

    // US 293, CA1 y CA6: si se abrió el puesto, el respiro arranca recién después de elegir.
    private bool esperandoEleccion;

    private void ArrancarDescanso()
    {
        fase = Fase.Descanso;
        EleccionDeInvasion eleccion = GetComponent<EleccionDeInvasion>();
        if (eleccion != null && eleccion.Retener()) { esperandoEleccion = true; return; }
        if (descanso != null) descanso.Begin(); else Invoke(nameof(Empezar), 20f);
    }

    /// <summary>US 293, CA6: se eligió en el puesto, arranca el respiro.</summary>
    public void TerminoLaEleccion()
    {
        if (!esperandoEleccion || fase != Fase.Descanso) return;
        esperandoEleccion = false;
        ArrancarDescanso();
    }

    /// <summary>Para probar (Riftwalker > Invasión > Ir a la oleada 10): saca a los enemigos que quedan y arranca ya la
    /// oleada n.</summary>
    public void IrA(int n)
    {
        if (fase == Fase.Final) return;
        foreach (Npc v in vivos) if (v != null) Destroy(v.gameObject);
        if (jefe != null) Destroy(jefe.gameObject);
        CerrarPortales();
        vivos.Clear();
        tipos.Clear();
        aparecio.Clear();
        porAparecer.Clear();
        jefe = null;
        jefeVivo = false;
        escolta = escoltaPorSalir = 0;
        Oleada = Mathf.Max(0, n - 1);
        JefesDerrotados = Oleada / Ultima; // como si hubiera bajado a los Jefes de antes (US 303)
        esperandoEleccion = false;
        fase = Fase.Descanso;
        if (descanso != null && descanso.IsActive) descanso.Terminar(); // al cerrar la compra arranca la oleada
        else Empezar();
    }

    private void CerrarPortales()
    {
        foreach (PortalDeEnemigos p in portales) if (p != null) p.Cerrar();
        portales.Clear();
        if (portalDelJefe != null) portalDelJefe.Cerrar();
        portalDelJefe = null;
    }

    // ---------- US 269, CA1: dónde se abre un portal ----------

    // Al azar en el NavMesh a 15–50 m, conectado con el jugador, con lugar para el portal y, primero, fuera de su vista;
    // si no, a la vista; si no hay NavMesh o no se encuentra, en los puntos de aparición (US 187) o alrededor del jugador.
    private bool LugarDePortal(Transform jugador, out Vector3 lugar)
    {
        Camera cam = Camera.main;
        if (NavMeshEnVivo.Listo || NavMesh.SamplePosition(jugador.position, out _, 5f, NavMesh.AllAreas))
        {
            for (int intento = 0; intento < 30; intento++)
            {
                bool escondido = intento < 20;
                Vector2 c = Random.insideUnitCircle.normalized;
                Vector3 p = jugador.position + new Vector3(c.x, 0f, c.y) * Random.Range(Cerca, Lejos);
                if (!Conectado(p, jugador.position, out Vector3 q)) continue;
                float d = Vector3.Distance(q, jugador.position);
                if (d < Cerca * 0.8f || d > Lejos * 1.2f) continue;
                if (escondido && LoVe(cam, q)) continue;
                if (!HayLugar(q)) continue;
                lugar = q;
                return true;
            }
        }
        return Lugar(jugador, out lugar);
    }

    // Lugar para el portal parado: nada sólido del mapa en un cilindro de 1,2 m de radio y 3 m de alto.
    private static bool HayLugar(Vector3 piso)
    {
        foreach (Collider c in Physics.OverlapCapsule(piso + Vector3.up * 1.3f, piso + Vector3.up * 2.6f, 1.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (c.GetComponentInParent<Npc>() != null || c.GetComponentInParent<PlayerMovement>() != null) continue;
            return false;
        }
        return true;
    }

    // ---------- CA4: dónde aparece (de reserva) ----------

    private bool Lugar(Transform jugador, out Vector3 lugar)
    {
        Camera cam = Camera.main;
        // De más estricto a más flojo: 15–50 m fuera de la vista; a cualquier distancia fuera de la vista; cualquiera.
        if (Buscar(jugador, cam, Cerca, Lejos, true, out lugar)) return true;
        if (Buscar(jugador, cam, 5f, float.MaxValue, true, out lugar)) return true;
        if (Buscar(jugador, cam, 5f, float.MaxValue, false, out lugar)) return true;
        return AlrededorDelJugador(jugador, out lugar);
    }

    private readonly List<Vector3> candidatos = new List<Vector3>();

    private bool Buscar(Transform jugador, Camera cam, float desde, float hasta, bool escondido, out Vector3 lugar)
    {
        lugar = Vector3.zero;
        if (AparicionDeEnemigos.Todos.Count == 0) return false;
        candidatos.Clear();
        foreach (AparicionDeEnemigos punto in AparicionDeEnemigos.Todos)
        {
            Vector3 p = punto.transform.position;
            float d = Vector3.Distance(p, jugador.position);
            if (d < desde || d > hasta) continue;
            if (escondido && LoVe(cam, p)) continue;
            candidatos.Add(p);
        }
        // Al azar, hasta encontrar uno que el NavMesh conecte con el jugador.
        for (int intento = 0; intento < 8 && candidatos.Count > 0; intento++)
        {
            int i = Random.Range(0, candidatos.Count);
            Vector3 p = candidatos[i];
            candidatos.RemoveAt(i);
            if (Conectado(p, jugador.position, out Vector3 sobreNavMesh)) { lugar = sobreNavMesh; return true; }
        }
        return false;
    }

    // Sin puntos (o ninguno sirve): un lugar del NavMesh detrás del jugador, a unos 20 m.
    private bool AlrededorDelJugador(Transform jugador, out Vector3 lugar)
    {
        lugar = Vector3.zero;
        for (int i = 0; i < 10; i++)
        {
            Vector2 c = Random.insideUnitCircle.normalized;
            Vector3 p = jugador.position + new Vector3(c.x, 0f, c.y) * Random.Range(18f, 30f);
            if (Conectado(p, jugador.position, out lugar)) return true;
        }
        return false;
    }

    // Lo ve: adelante de la cámara, en pantalla y sin nada en el medio.
    private static bool LoVe(Camera cam, Vector3 p)
    {
        if (cam == null) return false;
        Vector3 cuerpo = p + Vector3.up * 1f;
        Vector3 v = cam.WorldToViewportPoint(cuerpo);
        if (v.z <= 0f || v.x < -0.05f || v.x > 1.05f || v.y < -0.05f || v.y > 1.05f) return false;
        Vector3 desde = cam.transform.position;
        return !Physics.Linecast(desde, cuerpo, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }

    // Sobre el NavMesh y con camino completo hasta el jugador (así no aparece en un techo ni en un lugar cerrado). Si
    // todavía no hay NavMesh (se está armando), el punto sirve tal cual: el enemigo se sube cuando esté listo.
    private static bool Conectado(Vector3 p, Vector3 jugador, out Vector3 sobreNavMesh)
    {
        sobreNavMesh = p;
        int tipo = NavMeshEnVivo.Listo ? NavMeshEnVivo.Tipo : NavMesh.GetSettingsByIndex(0).agentTypeID;
        var filtro = new NavMeshQueryFilter { agentTypeID = tipo, areaMask = NavMesh.AllAreas };
        if (!NavMeshEnVivo.Listo && !NavMesh.SamplePosition(jugador, out _, 5f, filtro)) return true; // sin NavMesh todavía
        if (!NavMesh.SamplePosition(p, out NavMeshHit aca, 2f, filtro)) return false;
        if (!NavMesh.SamplePosition(jugador, out NavMeshHit alla, 3f, filtro)) { sobreNavMesh = aca.position; return true; }
        var camino = new NavMeshPath();
        if (!NavMesh.CalculatePath(aca.position, alla.position, filtro, camino) || camino.status != NavMeshPathStatus.PathComplete) return false;
        sobreNavMesh = aca.position;
        return true;
    }

    private static Transform Jugador()
    {
        PlayerMovement p = FindFirstObjectByType<PlayerMovement>();
        if (p == null) return null;
        HealthSystem vida = p.GetComponent<HealthSystem>();
        return vida != null && vida.currentHealth <= 0 ? null : p.transform;
    }

    // ---------- US 303, CA4: Jefe derrotado: seguir o retirarse ----------

    private void Final()
    {
        MatchHud.ClearCenter();
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        CameraLook.Quieta = true;
        if (PauseMenu.Instance != null) PauseMenu.Instance.AllowPause = false;

        finalUi = new GameObject("Invasión · Jefe derrotado");
        var lienzo = finalUi.AddComponent<Canvas>();
        lienzo.renderMode = RenderMode.ScreenSpaceOverlay;
        lienzo.sortingOrder = 60;
        var escala = finalUi.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.matchWidthOrHeight = 0.5f;
        finalUi.AddComponent<GraphicRaycaster>();

        MatchHud hud = MatchHud.Instance;
        TMP_FontAsset titulo = hud != null ? hud.DisplayFont : null, texto = hud != null ? hud.LabelFont : null;
        Image fondo = Pieza<Image>("Fondo", finalUi.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        fondo.color = new Color(0f, 0f, 0f, 0.7f);
        TextMeshProUGUI t = Pieza<TextMeshProUGUI>("Título", finalUi.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(900f, 120f));
        Texto(t, "JEFE DERROTADO", titulo, 84f, ShopUIKit.Ok);
        ConfigInvasion config = ConfigInvasion.Actual;
        int masVida = Mathf.RoundToInt(config.vidaPorJefe * JefesDerrotados * 100f), masDanio = Mathf.RoundToInt(config.danioPorJefe * JefesDerrotados * 100f);
        TextMeshProUGUI s = Pieza<TextMeshProUGUI>("Detalle", finalUi.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(1100f, 80f));
        Texto(s, $"Superaste la oleada {Oleada}. Si seguís, te espera la elección del Jefe, pero desde ahora los invasores " +
                 $"tienen {masVida} % más de vida y pegan {masDanio} % más.", texto, 26f, ShopUIKit.Soft);
        Boton("Seguir", new Vector2(-150f, -90f), ShopUIKit.Accent, ShopUIKit.AccentInk, texto, Seguir);
        Boton("Retirarse", new Vector2(150f, -90f), ShopUIKit.ChipColor, ShopUIKit.Ink, texto, Retirarse);
    }

    // US 303, CA4: sigue; antes del respiro se abre la elección del Jefe (EleccionDeInvasion la tiene pendiente).
    private void Seguir()
    {
        CerrarFinal();
        ArrancarDescanso();
    }

    // US 303, CA4 y US 151, CA2: se retira con el resultado y el récord (la oleada del Jefe que bajó, CA5).
    private void Retirarse()
    {
        CerrarFinal();
        ResultadoDeInvasion.Mostrar(false);
    }

    private void CerrarFinal()
    {
        if (finalUi != null) Destroy(finalUi);
        finalUi = null;
        Time.timeScale = 1f;
        CameraLook.Quieta = false;
        if (PauseMenu.Instance != null) PauseMenu.Instance.AllowPause = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static T Pieza<T>(string nombre, Transform padre, Vector2 min, Vector2 max, Vector2 pos, Vector2 tam) where T : Component
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = min;
        r.anchorMax = max;
        r.anchoredPosition = pos;
        r.sizeDelta = tam;
        return go.AddComponent<T>();
    }

    private static void Texto(TextMeshProUGUI t, string texto, TMP_FontAsset fuente, float tam, Color color)
    {
        if (fuente != null) t.font = fuente;
        t.text = texto;
        t.fontSize = tam;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
    }

    private void Boton(string texto, Vector2 pos, Color fondo, Color tinta, TMP_FontAsset fuente, UnityEngine.Events.UnityAction alApretar)
    {
        Image img = Pieza<Image>("Botón " + texto, finalUi.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(260f, 64f));
        img.color = fondo;
        Button b = img.gameObject.AddComponent<Button>();
        b.onClick.AddListener(alApretar);
        TextMeshProUGUI t = Pieza<TextMeshProUGUI>("Texto", img.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Texto(t, texto.ToUpperInvariant(), fuente, 26f, tinta);
    }
}
