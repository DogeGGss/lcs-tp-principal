using System.Collections.Generic;
using TMPro;
using UnityEngine;

// US 120 · Panel del campo de práctica, en el medio de la pared derecha de la zona de salida (lo pone el menú Riftwalker > Mapa de
// práctica). Hay una sola zona de tiro: el panel elige qué se practica y pone ahí los muñecos (BotDePractica, el muñeco de
// Scenes/Pruebas_Armas). Al cambiar de modo se sacan los del modo anterior.
// - Botones (mirarlos de cerca y apretar E, la tecla de Plantar, o pegarles un tiro):
//   * "Quietos": bots parados a distintas distancias, costados y alturas (algunos arriba de las cajas).
//   * "En movimiento": una fila de bots a cada distancia, caminando de costado con frenadas y cambios de lado al azar.
//   * "Distancias": un muñeco quieto a 10, 15, 20, 25, 30, 35, 40 y 50 m, con un cartel arriba que dice a cuántos metros
//     está, cuánto daño le hizo el último tiro y a qué zona (cabeza, cuerpo o piernas), y cuánta vida le queda. El cartel
//     es más grande cuanto más lejos, así se lee igual desde la línea de tiro.
//   * "Reacción": aparece un bot a la vez, en un lugar al azar, 10 veces. Se mide cuánto se tarda en pegarle a cada uno
//     (si en 3 s no le pegó, cuenta como fallo). Al terminar, el marcador muestra el promedio y el mejor tiempo.
//   * "Tutorial" (US 166, CA6): vuelve a abrir la guía de controles (TutorialDePractica) desde el primer paso.
//   * "Reiniciar" (CA2): levanta los muñecos del modo en su lugar (o vuelve a empezar la reacción), borra las marcas de
//     bala (CA4) y pone el marcador en 0.
// - Marcador: cuántos bots bajó el jugador y cuántos con un tiro a la cabeza (o el resultado de la reacción).
public class PanelDePractica : MonoBehaviour
{
    public enum Modo { Quietos, Movimiento, Distancias, Reaccion }

    [Tooltip("El muñeco que hace de bot (Prefabs/MuniecoPrueba).")]
    public GameObject munieco;
    [Tooltip("Entre qué x puede haber muñecos (de pared a pared, sin el relieve de las paredes).")]
    public float x0 = -10.5f, x1 = 10.5f;
    public TextMeshPro marcador, ayuda;
    [Tooltip("A cuántos metros se puede usar un botón.")]
    public float alcance = 4f;

    private static readonly Color Amarillo = new Color(1f, 0.82f, 0.1f);
    private const int Rondas = 10;
    private const float Espera = 3f; // segundos que se queda el bot de la reacción si no le pegan

    // Dónde va cada muñeco, en x y z (metros desde la línea de tiro). La altura la da el piso o la caja que haya ahí.
    private static readonly Vector2[] LugaresQuietos =
    {
        new Vector2(-8f, 8f), new Vector2(3f, 9f), new Vector2(-2f, 13f), new Vector2(9f, 17f), new Vector2(-6f, 18f),
        new Vector2(1f, 21f), new Vector2(5f, 24f), new Vector2(-9f, 27f), new Vector2(-2f, 32f), new Vector2(7f, 35f),
        new Vector2(0f, 45f),
    };
    private static readonly float[] FilasMovimiento = { 10f, 15.5f, 21f, 27f, 36f };
    private static readonly int[] Metros = { 10, 15, 20, 25, 30, 35, 40, 50 };

    private static readonly RaycastHit[] golpes = new RaycastHit[16];
    private static readonly IComparer<RaycastHit> porDistancia =
        Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

    // Un muñeco del modo Distancias, con su cartel.
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
    private Transform grupo;
    private Camera camara;
    private BotonDePractica[] botones;
    private BotonDePractica mirado;
    private Modo modo = Modo.Quietos;
    private int bajas, cabezas;

    // Reacción
    private bool reaccion;
    private int ronda, fallos;
    private readonly List<float> tiempos = new List<float>();
    private BotDePractica activo;
    private Vector3 lugarAnterior;
    private float aparecioEn, proximoEn;
    private string resultado = "";

    private void Awake() => botones = GetComponentsInChildren<BotonDePractica>(true);

    private void OnEnable() => WeaponFire.Impacto += AlImpactar;
    private void OnDisable() => WeaponFire.Impacto -= AlImpactar;

    private void Start()
    {
        if (munieco == null) Debug.LogWarning("PanelDePractica: falta el muñeco (Prefabs/MuniecoPrueba).");
        Empezar(Modo.Quietos);
    }

    private void Update()
    {
        Mirar();
        if (mirado != null && KeyBindings.Down(GameAction.Plantar)) mirado.Presionar();
        if (reaccion) Reaccion();
        foreach (Distancia d in distancias) MostrarDistancia(d);
    }

    public void Presionar(BotonDePractica.Accion accion)
    {
        switch (accion)
        {
            case BotonDePractica.Accion.Quietos: Empezar(Modo.Quietos); break;
            case BotonDePractica.Accion.Movimiento: Empezar(Modo.Movimiento); break;
            case BotonDePractica.Accion.Distancias: Empezar(Modo.Distancias); break;
            case BotonDePractica.Accion.Reaccion: Empezar(Modo.Reaccion); break;
            case BotonDePractica.Accion.Reiniciar: Empezar(modo); break;
            // US 166, CA6: vuelve a abrir la guía de controles desde el primer paso.
            case BotonDePractica.Accion.Tutorial: if (TutorialDePractica.Actual != null) TutorialDePractica.Actual.Empezar(); break;
        }
    }

    // ---------- Modos ----------

    // Saca los muñecos que haya, borra las marcas de bala, pone el marcador en 0 y arma el modo.
    private void Empezar(Modo nuevo)
    {
        modo = nuevo;
        if (grupo != null) Destroy(grupo.gameObject);
        grupo = new GameObject("Muñecos de práctica").transform;
        bots.Clear();
        distancias.Clear();
        reaccion = false;
        activo = null;
        ImpactMarks.Clear();
        bajas = cabezas = 0;
        resultado = "";

        if (munieco != null)
        {
            switch (modo)
            {
                case Modo.Quietos:
                    foreach (Vector2 l in LugaresQuietos) Poner(Suelo(l.x, l.y), false);
                    break;
                case Modo.Movimiento:
                    foreach (float z in FilasMovimiento) Poner(Suelo(Random.Range(x0, x1), z), true).Moverse = true;
                    break;
                case Modo.Distancias:
                    for (int i = 0; i < Metros.Length; i++)
                    {
                        // En diagonal, así el de adelante no tapa al de atrás.
                        float x = Mathf.Lerp(x0 + 1.5f, x1 - 1.5f, Metros.Length > 1 ? i / (Metros.Length - 1f) : 0.5f);
                        Vector3 pie = Suelo(x, Metros[i]);
                        distancias.Add(new Distancia { bot = Poner(pie, false), metros = Metros[i], cartel = Cartel(pie, Metros[i]) });
                    }
                    break;
                case Modo.Reaccion:
                    reaccion = true;
                    ronda = fallos = 0;
                    tiempos.Clear();
                    proximoEn = Time.time + 1.5f;
                    break;
            }
        }
        Mostrar();
    }

    private BotDePractica Poner(Vector3 pie, bool puedeMoverse)
    {
        BotDePractica bot = BotDePractica.Crear(munieco, grupo, pie, puedeMoverse, x0, x1);
        bot.Murio += AlMorirBot;
        bots.Add(bot);
        return bot;
    }

    // El piso (o la caja) que hay en ese lugar de la zona de tiro.
    private static Vector3 Suelo(float x, float z)
    {
        Vector3 arriba = new Vector3(x, 10f, z);
        int cantidad = Physics.RaycastNonAlloc(arriba, Vector3.down, golpes, 20f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float y = 0f, mas = float.MaxValue;
        for (int i = 0; i < cantidad; i++)
        {
            if (golpes[i].collider.GetComponentInParent<HealthSystem>() != null) continue;
            if (golpes[i].distance < mas) { mas = golpes[i].distance; y = golpes[i].point.y; }
        }
        return new Vector3(x, y, z);
    }

    // El cartel arriba del muñeco de Distancias, de frente a la línea de tiro. Más grande cuanto más lejos.
    private TextMeshPro Cartel(Vector3 pie, float metros)
    {
        var go = new GameObject("Cartel de daño");
        go.transform.SetParent(grupo, false);
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

    private void AlMorirBot(BotDePractica bot, bool cabeza)
    {
        bajas++;
        if (cabeza) cabezas++;
        Mostrar();
    }

    // Cada tiro que pega en un personaje (WeaponFire): el cartel de Distancias y la reacción.
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
            Destroy(activo.gameObject); // se va al final del cuadro: este mismo tiro todavía le está haciendo el daño
            activo = null;
            SiguienteRonda();
        }
    }

    private void MostrarDistancia(Distancia d)
    {
        if (d.cartel == null || d.bot == null) return;
        HealthSystem vida = d.bot.Vida;
        string golpe = d.golpeado ? $"<color=#{ColorUtility.ToHtmlStringRGB(Amarillo)}>{d.ultimo}</color> {NombreZona(d.zona)}" : "Tirale";
        string queda = vida == null ? "" : vida.currentHealth > 0 ? $"Vida {vida.currentHealth}" : "Abatido";
        d.cartel.text = $"{d.metros} m\n{golpe}\n<size=70%>{queda}</size>";
    }

    private static string NombreZona(BodyZone zona) =>
        zona == BodyZone.Head ? "cabeza" : zona == BodyZone.Legs ? "piernas" : "cuerpo";

    // ---------- Reacción ----------

    private void Reaccion()
    {
        if (activo != null && Time.time - aparecioEn > Espera)
        {
            Destroy(activo.gameObject);
            activo = null;
            fallos++;
            SiguienteRonda();
        }
        if (!reaccion || activo != null || Time.time < proximoEn || munieco == null) return;

        // Un lugar al azar en la zona de tiro, lejos del anterior.
        Vector3 pie;
        int intentos = 0;
        do pie = Suelo(Random.Range(x0 + 0.5f, x1 - 0.5f), Random.Range(7f, 35f));
        while (Vector3.Distance(pie, lugarAnterior) < 5f && ++intentos < 20);
        lugarAnterior = pie;
        activo = Poner(pie, false);
        aparecioEn = Time.time;
        Mostrar();
    }

    private void SiguienteRonda()
    {
        ronda++;
        if (ronda >= Rondas)
        {
            reaccion = false;
            if (tiempos.Count > 0)
            {
                float suma = 0f, mejor = float.MaxValue;
                foreach (float t in tiempos) { suma += t; mejor = Mathf.Min(mejor, t); }
                resultado = $"Reacción: promedio {suma / tiempos.Count:0.00} s · mejor {mejor:0.00} s";
                if (fallos > 0) resultado += $" · {fallos} sin pegar";
            }
            else resultado = "Reacción: no le pegaste a ninguno";
        }
        else proximoEn = Time.time + Random.Range(0.6f, 1.8f);
        Mostrar();
    }

    // ---------- Botones ----------

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
        if (mirado != null) mirado.Resaltar(mirado.accion == AccionDelModo());
        mirado = nuevo;
        if (mirado != null) mirado.Resaltar(true);
        Mostrar();
    }

    private BotonDePractica.Accion AccionDelModo()
    {
        switch (modo)
        {
            case Modo.Movimiento: return BotonDePractica.Accion.Movimiento;
            case Modo.Distancias: return BotonDePractica.Accion.Distancias;
            case Modo.Reaccion: return BotonDePractica.Accion.Reaccion;
            default: return BotonDePractica.Accion.Quietos;
        }
    }

    private static string Nombre(BotonDePractica.Accion accion)
    {
        switch (accion)
        {
            case BotonDePractica.Accion.Movimiento: return "En movimiento";
            case BotonDePractica.Accion.Distancias: return "Distancias";
            case BotonDePractica.Accion.Reaccion: return "Reacción";
            case BotonDePractica.Accion.Reiniciar: return "Reiniciar";
            case BotonDePractica.Accion.Tutorial: return "Tutorial";
            default: return "Quietos";
        }
    }

    private void Mostrar()
    {
        // El botón del modo elegido queda iluminado (y el que se mira).
        if (botones != null)
            foreach (BotonDePractica boton in botones)
            {
                if (boton == null) continue;
                if (boton.texto != null) boton.texto.text = Nombre(boton.accion).ToUpperInvariant();
                boton.Resaltar(boton == mirado || boton.accion == AccionDelModo());
            }
        if (marcador != null)
            marcador.text = reaccion ? $"Reacción: {Mathf.Min(ronda + 1, Rondas)} de {Rondas}"
                          : resultado.Length > 0 ? resultado
                          : $"{Nombre(AccionDelModo())} · Bajas: {bajas} · A la cabeza: {cabezas}";
        if (ayuda != null)
        {
            string tecla = $"<color=#{ColorUtility.ToHtmlStringRGB(Amarillo)}>{KeyBindings.Label(GameAction.Plantar)}</color>";
            ayuda.text = mirado != null ? $"{tecla}  {Nombre(mirado.accion)}" : $"Elegí un modo: mirá un botón y apretá {tecla}, o pegale un tiro";
        }
    }
}
