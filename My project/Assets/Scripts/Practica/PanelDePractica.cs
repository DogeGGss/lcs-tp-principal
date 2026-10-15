using System.Collections.Generic;
using TMPro;
using UnityEngine;

// US 120 · Panel del campo de práctica, en la pared derecha al lado de la línea de tiro (lo pone el menú Riftwalker > Mapa de práctica).
// - Pone un bot (BotDePractica, el muñeco de Scenes/Pruebas_Armas) en cada lugar de "lugares": los de la zona de blancos.
// - Zona de distancias: un muñeco quieto en cada lugar de "lugaresDistancia", con un cartel arriba que dice a cuántos
//   metros está, cuánto daño le hizo el último tiro y a qué zona (cabeza, cuerpo o piernas), y cuánta vida le queda.
//   El cartel es más grande cuanto más lejos, así se lee igual desde la línea de tiro.
// - Botones (mirarlos de cerca y apretar E, la tecla de Plantar, o pegarles un tiro):
//   * "Mover bots" / "Detener bots": los bots del piso caminan de costado o se quedan quietos.
//   * "Reacción": se esconden los bots de la zona de blancos y aparecen de a uno, en un lugar al azar, 10 veces. Se mide cuánto se
//     tarda en pegarle a cada uno (si en 3 s no le pegó, cuenta como fallo). Al terminar, el marcador muestra el
//     promedio y el mejor tiempo. Apretarlo de nuevo lo corta.
//   * "Reiniciar" (CA2): levanta a todos los muñecos en su lugar, borra las marcas de bala (CA4) y pone el marcador en 0.
// - Marcador: cuántos bots bajó el jugador y cuántos con un tiro a la cabeza (o el resultado de la reacción).
public class PanelDePractica : MonoBehaviour
{
    [Tooltip("El muñeco que hace de bot (Prefabs/MuniecoPrueba).")]
    public GameObject munieco;
    [Tooltip("Dónde va cada bot (sus pies). Se pueden mover en la escena. Los que no están en el piso no caminan.")]
    public Transform[] lugares = { };
    [Tooltip("Dónde va cada muñeco de la zona de distancias (sus pies). No caminan; muestran el daño de cada tiro.")]
    public Transform[] lugaresDistancia = { };
    [Tooltip("Entre qué x caminan los bots (los bordes de la zona de blancos).")]
    public float x0 = 2.1f, x1 = 10.9f;
    public TextMeshPro marcador, ayuda;
    [Tooltip("A cuántos metros se puede usar un botón.")]
    public float alcance = 4f;

    private static readonly Color Amarillo = new Color(1f, 0.82f, 0.1f);
    private const int Rondas = 10;
    private const float Espera = 3f; // segundos que se queda un muñeco de la reacción si no le pegan

    private static readonly RaycastHit[] golpes = new RaycastHit[16];
    private static readonly IComparer<RaycastHit> porDistancia =
        Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

    // Un muñeco de la zona de distancias, con su cartel.
    private class Distancia
    {
        public BotDePractica bot;
        public TextMeshPro cartel;
        public int metros, ultimo;
        public BodyZone zona;
        public bool golpeado;
    }

    private readonly List<BotDePractica> bots = new List<BotDePractica>();
    private readonly List<Distancia> distancias = new List<Distancia>();
    private readonly List<BotDePractica> todos = new List<BotDePractica>(); // los bots y los muñecos de distancias
    private Camera camara;
    private BotonDePractica[] botones;
    private BotonDePractica mirado;
    private bool moverse;
    private int bajas, cabezas;

    // Reacción
    private bool reaccion;
    private int ronda, fallos;
    private readonly List<float> tiempos = new List<float>();
    private BotDePractica activo, anterior, aEsconder;
    private float aparecioEn, proximoEn;
    private string resultado = "";

    private void Awake() => botones = GetComponentsInChildren<BotonDePractica>(true);

    private void OnEnable() => WeaponFire.Impacto += AlImpactar;
    private void OnDisable() => WeaponFire.Impacto -= AlImpactar;

    private void Start()
    {
        if (munieco != null)
        {
            Transform grupo = new GameObject("Bots de práctica").transform;
            foreach (Transform lugar in lugares)
            {
                if (lugar == null) continue;
                bool enElPiso = lugar.position.y < 0.05f;
                BotDePractica bot = BotDePractica.Crear(munieco, grupo, lugar.position, enElPiso, x0, x1);
                bot.Murio += AlMorirBot;
                bots.Add(bot);
                todos.Add(bot);
            }
            foreach (Transform lugar in lugaresDistancia)
            {
                if (lugar == null) continue;
                Vector3 pie = lugar.position;
                BotDePractica bot = BotDePractica.Crear(munieco, grupo, pie, false, pie.x, pie.x);
                bot.Murio += AlMorirBot;
                todos.Add(bot);
                distancias.Add(new Distancia { bot = bot, metros = Mathf.RoundToInt(pie.z), cartel = Cartel(pie, pie.z) });
            }
        }
        else Debug.LogWarning("PanelDePractica: falta el muñeco (Prefabs/MuniecoPrueba).");
        Mostrar();
    }

    // El cartel arriba del muñeco de la zona de distancias, de frente a la línea de tiro. Más grande cuanto más lejos.
    private TextMeshPro Cartel(Vector3 pie, float metros)
    {
        var go = new GameObject("Cartel de daño");
        go.transform.SetParent(transform.parent, false);
        go.transform.SetPositionAndRotation(pie + Vector3.up * 2.05f, Quaternion.identity);
        TextMeshPro t = go.AddComponent<TextMeshPro>();
        t.rectTransform.pivot = new Vector2(0.5f, 0f);
        t.rectTransform.sizeDelta = new Vector2(4f, 1f);
        t.alignment = TextAlignmentOptions.Bottom;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.fontSize = 1.5f + metros * 0.25f;
        t.fontStyle = FontStyles.Bold;
        return t;
    }

    private void Update()
    {
        Mirar();
        if (mirado != null && KeyBindings.Down(GameAction.Plantar)) mirado.Presionar();
        if (reaccion) Reaccion();
        foreach (Distancia d in distancias) MostrarDistancia(d);
    }

    // La cámara del jugador (la misma con la que apunta el arma).
    private Camera Camara()
    {
        if (camara != null && camara.isActiveAndEnabled) return camara;
        PlayerMovement jugador = FindAnyObjectByType<PlayerMovement>();
        camara = jugador != null ? jugador.GetComponentInChildren<Camera>() : null;
        if (camara == null) camara = Camera.main;
        return camara;
    }

    // El botón que el jugador tiene en la mira, de cerca. Se saltea lo del propio jugador (su cuerpo, sus armas), como
    // los disparos (WeaponFire).
    private void Mirar()
    {
        BotonDePractica nuevo = null;
        Camera cam = Camara();
        if (cam != null && !PauseMenu.IsPaused && !ShopUI.IsOpen)
        {
            Transform jugador = cam.transform.root;
            int cantidad = Physics.RaycastNonAlloc(cam.transform.position, cam.transform.forward, golpes, alcance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(golpes, 0, cantidad, porDistancia);
            for (int i = 0; i < cantidad; i++)
            {
                if (golpes[i].collider.transform.IsChildOf(jugador)) continue;
                nuevo = golpes[i].collider.GetComponentInParent<BotonDePractica>();
                break;
            }
        }
        if (nuevo == mirado) return;
        if (mirado != null) mirado.Resaltar(false);
        mirado = nuevo;
        if (mirado != null) mirado.Resaltar(true);
        Mostrar();
    }

    public void Presionar(BotonDePractica.Accion accion)
    {
        switch (accion)
        {
            case BotonDePractica.Accion.MoverBots:
                if (reaccion) TerminarReaccion();
                moverse = !moverse;
                foreach (BotDePractica bot in bots) if (bot != null) bot.Moverse = moverse;
                break;
            case BotonDePractica.Accion.Reaccion:
                if (reaccion) TerminarReaccion();
                else EmpezarReaccion();
                break;
            case BotonDePractica.Accion.Reiniciar:
                if (reaccion) TerminarReaccion();
                foreach (BotDePractica bot in todos) if (bot != null) bot.Revivir(true);
                foreach (Distancia d in distancias) d.golpeado = false;
                ImpactMarks.Clear();
                bajas = cabezas = 0;
                resultado = "";
                break;
        }
        Mostrar();
    }

    private void AlMorirBot(BotDePractica bot, bool cabeza)
    {
        bajas++;
        if (cabeza) cabezas++;
        Mostrar();
    }

    // Cada tiro que pega en un personaje (WeaponFire): el cartel del muñeco de distancias y la reacción.
    private void AlImpactar(HealthSystem quien, int dano, BodyZone zona)
    {
        foreach (Distancia d in distancias)
            if (d.bot != null && d.bot.Vida == quien)
            {
                d.ultimo = dano;
                d.zona = zona;
                d.golpeado = true;
            }

        if (reaccion && activo != null && activo.Vida == quien)
        {
            tiempos.Add(Time.time - aparecioEn);
            // Se esconde en el próximo cuadro: este mismo tiro todavía le está haciendo el daño.
            aEsconder = activo;
            activo = null;
            SiguienteRonda();
        }
    }

    private void MostrarDistancia(Distancia d)
    {
        if (d.cartel == null || d.bot == null) return;
        bool visible = d.bot.gameObject.activeSelf;
        if (d.cartel.gameObject.activeSelf != visible) d.cartel.gameObject.SetActive(visible);
        if (!visible) return;
        HealthSystem vida = d.bot.Vida;
        string golpe = d.golpeado ? $"<color=#{ColorUtility.ToHtmlStringRGB(Amarillo)}>{d.ultimo}</color> {NombreZona(d.zona)}" : "Tirale";
        string queda = vida == null ? "" : vida.currentHealth > 0 ? $"Vida {vida.currentHealth}" : "Abatido";
        d.cartel.text = $"{d.metros} m\n{golpe}\n<size=70%>{queda}</size>";
    }

    private static string NombreZona(BodyZone zona) =>
        zona == BodyZone.Head ? "cabeza" : zona == BodyZone.Legs ? "piernas" : "cuerpo";

    // ---------- Reacción ----------

    private void EmpezarReaccion()
    {
        if (bots.Count == 0) return;
        moverse = false;
        foreach (BotDePractica bot in bots)
        {
            if (bot == null) continue;
            bot.Moverse = false;
            bot.Revivir(true);
            bot.gameObject.SetActive(false);
        }
        reaccion = true;
        ronda = fallos = 0;
        tiempos.Clear();
        activo = anterior = aEsconder = null;
        proximoEn = Time.time + 1.5f;
        resultado = "";
    }

    private void Reaccion()
    {
        if (aEsconder != null)
        {
            aEsconder.gameObject.SetActive(false);
            aEsconder = null;
        }
        if (activo != null && Time.time - aparecioEn > Espera)
        {
            activo.gameObject.SetActive(false);
            activo = null;
            fallos++;
            SiguienteRonda();
        }
        if (!reaccion || activo != null || Time.time < proximoEn) return;

        // Uno al azar, que no sea el de recién.
        BotDePractica elegido;
        int intentos = 0;
        do elegido = bots[Random.Range(0, bots.Count)];
        while ((elegido == null || elegido == anterior) && bots.Count > 1 && ++intentos < 20);
        if (elegido == null) return;
        elegido.gameObject.SetActive(true);
        elegido.Revivir(true);
        activo = anterior = elegido;
        aparecioEn = Time.time;
        Mostrar();
    }

    private void SiguienteRonda()
    {
        ronda++;
        if (ronda >= Rondas)
        {
            if (tiempos.Count > 0)
            {
                float suma = 0f, mejor = float.MaxValue;
                foreach (float t in tiempos) { suma += t; mejor = Mathf.Min(mejor, t); }
                resultado = $"Reacción: promedio {suma / tiempos.Count:0.00} s · mejor {mejor:0.00} s";
                if (fallos > 0) resultado += $" · {fallos} sin pegar";
            }
            else resultado = "Reacción: no le pegaste a ninguno";
            TerminarReaccion();
        }
        else proximoEn = Time.time + Random.Range(0.6f, 1.8f);
        Mostrar();
    }

    // Vuelven todos los muñecos, cada uno en su lugar.
    private void TerminarReaccion()
    {
        reaccion = false;
        activo = aEsconder = null;
        foreach (BotDePractica bot in todos)
        {
            if (bot == null) continue;
            bot.gameObject.SetActive(true);
            bot.Revivir(true);
        }
    }

    private string Nombre(BotonDePractica.Accion accion)
    {
        switch (accion)
        {
            case BotonDePractica.Accion.Reiniciar: return "Reiniciar";
            case BotonDePractica.Accion.Reaccion: return reaccion ? "Cortar" : "Reacción";
            default: return moverse ? "Detener bots" : "Mover bots";
        }
    }

    private void Mostrar()
    {
        if (botones != null)
            foreach (BotonDePractica boton in botones)
                if (boton != null && boton.texto != null) boton.texto.text = Nombre(boton.accion).ToUpperInvariant();
        if (marcador != null)
            marcador.text = reaccion ? $"Reacción: {Mathf.Min(ronda + 1, Rondas)} de {Rondas}"
                          : resultado.Length > 0 ? resultado
                          : $"Bajas: {bajas}     A la cabeza: {cabezas}";
        if (ayuda != null)
        {
            string tecla = $"<color=#{ColorUtility.ToHtmlStringRGB(Amarillo)}>{KeyBindings.Label(GameAction.Plantar)}</color>";
            ayuda.text = mirado != null ? $"{tecla}  {Nombre(mirado.accion)}" : $"Mirá un botón y apretá {tecla}, o pegale un tiro";
        }
    }

    // En el editor se ve dónde va cada muñeco.
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
        foreach (Transform[] lista in new[] { lugares, lugaresDistancia })
            if (lista != null)
                foreach (Transform lugar in lista)
                    if (lugar != null) Gizmos.DrawWireCube(lugar.position + Vector3.up * 0.9f, new Vector3(0.6f, 1.8f, 0.4f));
    }
}
