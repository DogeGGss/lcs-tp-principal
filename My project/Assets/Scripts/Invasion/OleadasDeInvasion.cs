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
// - CA3: en las oleadas 1 y 2 solo Comunes; desde la 3, el 25 % Rápidos; desde la 5, además, el 10 % Tanques. En la 10
//   aparece el Jefe (US 146) cuando cayó la mitad de la oleada: va aparte (no cuenta entre los 33 ni en el tope de 12).
// - CA4: aparecen en los puntos de aparición del mapa (US 187, AparicionDeEnemigos) que están entre 15 m y 50 m del
//   jugador, fuera de su vista y conectados con él por el NavMesh. Si no hay ninguno así, se aflojan las condiciones
//   (cualquier distancia fuera de la vista, después cualquiera), y sin puntos aparecen alrededor del jugador.
// - CA5: la oleada termina cuando mueren todos (en la 10, también el Jefe): "Oleada N superada" y descanso.
// - CA6: al superar la 10, "¡Sobreviviste!" con "Seguir" (modo infinito) y "Terminar" (el resultado es la US 151).
// - CA7: desde la 11, la misma regla de cantidad y los enemigos con 10 % más de vida por cada oleada después de la 10.
// El marcador (la oleada, cuántos quedan, el descanso y los puntos) es la US 150: MarcadorInvasion.
public class OleadasDeInvasion : MonoBehaviour
{
    public const int Ultima = 10;                         // CA6
    private const int Base = 6, PorOleada = 3;            // CA1
    private const int MaximoVivos = 12;                   // CA2
    private const float CadaCuanto = 0.6f;                // s entre apariciones
    private const float Cerca = 15f, Lejos = 50f;         // CA4: m del jugador
    private const float VidaExtra = 0.1f;                 // CA7: por oleada después de la 10

    /// <summary>La oleada en curso (0 antes de la primera).</summary>
    public int Oleada { get; private set; }
    /// <summary>Está en el descanso entre oleadas.</summary>
    public bool Descanso => fase == Fase.Descanso;
    /// <summary>Enemigos que quedan en la oleada, contando los que todavía no aparecieron (US 150, CA2).</summary>
    public int Quedan => Mathf.Max(0, total - muertos) + (jefeVivo ? 1 : 0);
    /// <summary>Juega en modo infinito (siguió después de la 10).</summary>
    public bool Infinito { get; private set; }
    /// <summary>Enemigos de la oleada en curso, con el Jefe en la 10 (US 150: la barra de los que quedan).</summary>
    public int Total => total + (Oleada == Ultima && tipoJefe != null ? 1 : 0);
    /// <summary>Está la pantalla de "¡Sobreviviste!" (US 150: el marcador se oculta).</summary>
    public bool EnElFinal => fase == Fase.Final;

    /// <summary>Avisa cada oleada que empieza y cada oleada superada (para el marcador y los puntos).</summary>
    public static event System.Action<int> Empezo, Supero;

    private enum Fase { Descanso, Oleada, Final }

    private Fase fase = Fase.Descanso;
    private int total, muertos;
    private readonly Queue<TipoDeNpc> porAparecer = new Queue<TipoDeNpc>();
    private readonly List<Npc> vivos = new List<Npc>();
    private readonly Dictionary<Npc, TipoDeNpc> tipos = new Dictionary<Npc, TipoDeNpc>(); // de qué tipo es cada uno
    private float proximaAparicion;
    private bool jefeSalio, jefeVivo, jefeMuerto;
    private readonly Dictionary<Npc, float> aparecio = new Dictionary<Npc, float>();
    private Transform jugadorAhora;
    private Npc jefe;
    private TipoDeNpc comun, rapido, tanque, tipoJefe;
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
        tipoJefe = System.Array.Find(TipoDeNpc.Todos(), t => t.esJefe);
        if (AparicionDeEnemigos.Todos.Count == 0) Debug.LogWarning("[Invasión] El mapa no tiene puntos de aparición (Riftwalker > Invasión > Crear 3 puntos de aparición): los enemigos aparecen en el NavMesh alrededor del jugador.");

        descanso = BuyPhase.Current;
        if (descanso != null) descanso.Ended += AlTerminarDescanso;
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
        total = Base + PorOleada * (Oleada - 1); // CA1 (y CA7)
        muertos = 0;
        vivos.Clear();
        tipos.Clear();
        aparecio.Clear();
        jefeSalio = jefeVivo = jefeMuerto = false;
        jefe = null;
        porAparecer.Clear();
        foreach (TipoDeNpc t in Composicion(Oleada, total)) porAparecer.Enqueue(t);
        proximaAparicion = Time.time + 1f;
        MatchHud.ShowBanner("Modo Invasión", $"Oleada {Oleada}", ShopUIKit.Bad, Oleada == Ultima ? "Llega el jefe" : $"{total} enemigos", null, 2.5f);
        Empezo?.Invoke(Oleada);
    }

    // CA3: los tipos de la oleada, mezclados.
    private List<TipoDeNpc> Composicion(int n, int cuantos)
    {
        int rapidos = n >= 3 ? Mathf.RoundToInt(cuantos * 0.25f) : 0;
        int tanques = n >= 5 ? Mathf.RoundToInt(cuantos * 0.10f) : 0;
        var lista = new List<TipoDeNpc>();
        for (int i = 0; i < cuantos; i++)
            lista.Add(i < rapidos ? rapido : i < rapidos + tanques ? tanque : comun);
        for (int i = lista.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (lista[i], lista[j]) = (lista[j], lista[i]);
        }
        lista.RemoveAll(t => t == null);
        return lista;
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
                if (tipos.TryGetValue(n, out TipoDeNpc t) && t != null) porAparecer.Enqueue(t);
                if (n != null) Destroy(n.gameObject);
            }
            tipos.Remove(n);
            aparecio.Remove(n);
        }
        Transform jugador = jugadorAhora;
        if (jugador == null) return;

        // CA2: de a uno, sin pasar de 12 vivos.
        if (porAparecer.Count > 0 && vivos.Count < MaximoVivos && Time.time >= proximaAparicion)
        {
            proximaAparicion = Time.time + CadaCuanto;
            TipoDeNpc tipo = porAparecer.Peek();
            if (Lugar(jugador, out Vector3 lugar))
            {
                porAparecer.Dequeue();
                Npc npc = Aparecer(tipo, lugar, jugador);
                if (npc != null) { vivos.Add(npc); tipos[npc] = tipo; aparecio[npc] = Time.time; }
                else total--; // el tipo no tiene modelo: no cuenta
            }
        }

        // CA3 (US 146, CA1): el Jefe, a mitad de la oleada 10.
        if (Oleada == Ultima && !jefeSalio && tipoJefe != null && muertos >= total / 2 && Lugar(jugador, out Vector3 dondeJefe))
        {
            jefeSalio = true;
            jefe = Aparecer(tipoJefe, dondeJefe, jugador);
            jefeVivo = jefe != null;
            if (jefe != null) aparecio[jefe] = Time.time;
            MatchHud.ShowBanner("Oleada 10", "¡Llegó el jefe!", ShopUIKit.Bad, "", null, 3f);
        }
        // El jefe: si murió, listo; si desapareció sin morir, vuelve a aparecer.
        if (jefeVivo && jefe != null && !jefe.Vivo) { jefeVivo = false; jefeMuerto = true; }
        else if (jefeVivo && (jefe == null || Perdido(jefe))) { if (jefe != null) Destroy(jefe.gameObject); jefe = null; jefeVivo = false; jefeSalio = false; }

        // CA5: superada (en la 10, solo con el jefe muerto).
        bool faltaJefe = Oleada == Ultima && tipoJefe != null && !jefeMuerto;
        if (porAparecer.Count == 0 && vivos.Count == 0 && !faltaJefe) Superar();
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
        Npc npc = Npc.Crear(tipo, lugar, hacia.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(hacia) : Quaternion.identity);
        if (npc == null) return null;
        // CA7: más vida en el modo infinito.
        if (Oleada > Ultima && npc.Vida != null)
        {
            int vida = Mathf.RoundToInt(tipo.vida * (1f + VidaExtra * (Oleada - Ultima)));
            npc.Vida.maxHealth = vida;
            npc.Vida.SetState(vida, 0);
        }
        return npc;
    }

    private void Superar()
    {
        Supero?.Invoke(Oleada);
        if (Oleada == Ultima && !Infinito)
        {
            fase = Fase.Final;
            Final(); // CA6
            return;
        }
        fase = Fase.Descanso;
        MatchHud.ShowBanner("Modo Invasión", $"Oleada {Oleada} superada", ShopUIKit.Ok, "Descanso de 20 s: abrí la tienda", null, 3f);
        if (descanso != null) descanso.Begin(); else Invoke(nameof(Empezar), 20f);
    }

    // ---------- CA4: dónde aparece ----------

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

    // ---------- CA6: ¡Sobreviviste! ----------

    private void Final()
    {
        MatchHud.ClearCenter();
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        CameraLook.Quieta = true;
        if (PauseMenu.Instance != null) PauseMenu.Instance.AllowPause = false;

        finalUi = new GameObject("Invasión · ¡Sobreviviste!");
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
        Texto(t, "¡SOBREVIVISTE!", titulo, 84f, ShopUIKit.Ok);
        TextMeshProUGUI s = Pieza<TextMeshProUGUI>("Detalle", finalUi.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(900f, 40f));
        Texto(s, $"Superaste las {Ultima} oleadas. ¿Seguís en modo infinito para hacer récord?", texto, 26f, ShopUIKit.Soft);
        Boton("Seguir", new Vector2(-150f, -80f), ShopUIKit.Accent, ShopUIKit.AccentInk, texto, Seguir);
        Boton("Terminar", new Vector2(150f, -80f), ShopUIKit.ChipColor, ShopUIKit.Ink, texto, Terminar);
    }

    private void Seguir()
    {
        Infinito = true;
        CerrarFinal();
        fase = Fase.Descanso;
        if (descanso != null) descanso.Begin(); else Invoke(nameof(Empezar), 20f);
    }

    // El resultado de la partida es la US 151: por ahora vuelve al menú.
    private void Terminar()
    {
        CerrarFinal();
        Guardado.Save();
        PantallaDeCarga.Cargar("MenuPrincipal", "", "Menú principal");
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
