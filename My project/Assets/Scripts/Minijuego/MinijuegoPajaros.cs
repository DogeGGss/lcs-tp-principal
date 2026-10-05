using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Minijuego de los pájaros (F21): durante la fase de compra del Táctico cruzan pájaros por el cielo y el jugador les
// puede disparar, como un aim lab. Lo agrega RondasTacticas y lo prende y apaga en cada cambio de fase.
// - US 156: los pájaros (Pajaro). Solo en la compra (CA1); entran por un borde y cruzan sobre la base del equipo, entre
//   15 y 40 m de altura (CA2); uno o una bandada de 3 a 5 palomas o gorriones cada 1 a 2 s (CA3); las golondrinas son
//   1 de cada 6 (CA4). Son de cada jugador: no pasan por la red (CA6). 3 s antes del combate dejan de salir y los que
//   quedan se van, así el cielo está despejado cuando empieza la ronda y no distraen.
// - US 157: mientras está activo, WeaponFire solo le pega a los pájaros y las armas no gastan balas. Las granadas las
//   traba RondasTacticas y el cuchillo no hace daño (MeleeAttack).
// - US 158: cada acierto muestra "+puntos" en el lugar del impacto (blanco; la golondrina en naranja), que sube y se
//   desvanece en 0,8 s, con la marca de impacto de la mira y un sonido propio. No hay contador en el HUD.
// Los resultados se van sumando mientras el juego está abierto (Sesion y EstaFase), pero todavía no se guardan ni se
// muestran: eso es de la US 159, y los logros de la US 160.
public class MinijuegoPajaros : MonoBehaviour
{
    private const float Radio = 50f;                              // m de la base al borde por donde entran (CA2)
    private const float Desvio = 20f;                             // m a los costados de la base
    private const float AlturaMinima = 15f, AlturaMaxima = 40f;   // m sobre el piso de la base (CA2)
    private const float ProbabilidadBandada = 0.3f;
    private const float CieloDespejado = 3f;                      // s antes del combate en que se van todos
    private const float AlcanceMinimo = 150f;                     // m: con cualquier arma se llega a los pájaros
    private const float DuracionPuntaje = 0.8f, SubidaPuntaje = 46f, TamanoPuntaje = 40f; // US 158, CA2

    /// <summary>Lo que se registra del minijuego (US 159, CA1). Todavía no se guarda.</summary>
    public sealed class Resultados
    {
        public int disparos, puntos;
        public readonly int[] aciertos = new int[3]; // por tipo de pájaro
        public float reaccionTotal, mejorReaccion = float.PositiveInfinity;

        public int Aciertos => aciertos[0] + aciertos[1] + aciertos[2];
        public float Precision => disparos > 0 ? (float)Aciertos / disparos : 0f;
        public float ReaccionPromedio => Aciertos > 0 ? reaccionTotal / Aciertos : 0f;

        internal void Sumar(TipoPajaro tipo, int puntos, float reaccion)
        {
            aciertos[(int)tipo]++;
            this.puntos += puntos;
            reaccionTotal += reaccion;
            mejorReaccion = Mathf.Min(mejorReaccion, reaccion);
        }
    }

    /// <summary>Lo sumado desde que se abrió el juego.</summary>
    public static readonly Resultados Sesion = new Resultados();
    /// <summary>Lo de la fase de compra actual, o de la última.</summary>
    public static Resultados EstaFase { get; private set; } = new Resultados();

    /// <summary>Hay pájaros: es la fase de compra del Táctico (US 157).</summary>
    public static bool Activo => actual != null && actual.activo;
    private static MinijuegoPajaros actual;

    private static readonly RaycastHit[] golpes = new RaycastHit[32];

    private class Puntaje
    {
        public RectTransform rect;
        public CanvasGroup grupo;
        public Vector3 punto;
        public float desde;
    }

    private PartidaEnRed partida;
    private bool activo, despejando;
    private float proximo;
    private int soltados, golondrinas;
    private readonly List<Pajaro> pajaros = new List<Pajaro>();
    private readonly List<Puntaje> puntajes = new List<Puntaje>();
    private RectTransform lienzo;
    private AudioSource fuente;
    private readonly AudioClip[] sonidos = new AudioClip[3];

    public void Iniciar(PartidaEnRed partida)
    {
        this.partida = partida;
        actual = this;
    }

    private void OnDestroy()
    {
        if (actual == this) actual = null;
    }

    /// <summary>Lo llama RondasTacticas en cada cambio de fase: solo hay pájaros durante la compra (US 156, CA1).</summary>
    public void Cambiar(bool compra)
    {
        if (compra == activo) return;
        activo = compra;
        if (compra)
        {
            EstaFase = new Resultados();
            proximo = Time.time + 0.5f;
            despejando = false;
            return;
        }
        foreach (Pajaro pajaro in pajaros) if (pajaro != null) pajaro.Irse();
        pajaros.Clear();
        if (EstaFase.disparos > 0)
            Debug.Log($"Minijuego: {EstaFase.Aciertos} pájaros, {EstaFase.puntos} puntos, precisión {EstaFase.Precision:P0} " +
                      $"(en total: {Sesion.Aciertos} pájaros, {Sesion.puntos} puntos).");
    }

    private void Update()
    {
        if (!activo) return;
        pajaros.RemoveAll(p => p == null);
        // Los últimos segundos de la compra: no salen más y los que quedan se van.
        float restante = RondasTacticas.Actual != null ? RondasTacticas.Actual.Restante : 0f;
        if (restante > 0f && restante <= CieloDespejado)
        {
            if (!despejando) foreach (Pajaro pajaro in pajaros) pajaro.Irse();
            despejando = true;
            return;
        }
        if (Time.time < proximo) return;
        proximo = Time.time + Random.Range(1f, 2f); // CA3
        Soltar();
    }

    private void LateUpdate() => MoverPuntajes();

    // ---------- Pájaros (US 156) ----------

    private void Soltar()
    {
        if (!Base(out Vector3 centro, out float piso)) return;
        float angulo = Random.value * Mathf.PI * 2f;
        var direccion = new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo));
        var costado = new Vector3(-direccion.z, 0f, direccion.x);
        Vector3 desde = centro - direccion * Radio + costado * Random.Range(-Desvio, Desvio);
        desde.y = piso + Random.Range(AlturaMinima, AlturaMaxima);

        // CA3: una bandada de 3 a 5 palomas o gorriones, en V detrás del primero.
        if (Random.value < ProbabilidadBandada)
        {
            TipoPajaro tipo = Random.value < 0.5f ? TipoPajaro.Paloma : TipoPajaro.Gorrion;
            int cantidad = Random.Range(3, 6);
            float separacion = Pajaro.EnvergaduraDe(tipo) * 1.6f;
            for (int i = 0; i < cantidad; i++)
            {
                int fila = (i + 1) / 2;
                float lado = i % 2 == 0 ? 1f : -1f;
                Nuevo(tipo, desde - direccion * (fila * separacion) + costado * (lado * fila * separacion)
                            + Vector3.up * Random.Range(-0.4f, 0.4f), direccion);
            }
            return;
        }

        // CA4: las golondrinas van solas y son 1 de cada 6 pájaros.
        TipoPajaro solo = (golondrinas + 1) * 6 <= soltados + 1 ? TipoPajaro.Golondrina
            : Random.value < 0.5f ? TipoPajaro.Paloma : TipoPajaro.Gorrion;
        Nuevo(solo, desde, direccion);
    }

    private void Nuevo(TipoPajaro tipo, Vector3 desde, Vector3 direccion)
    {
        pajaros.Add(Pajaro.Crear(tipo, desde, direccion, Radio * 2f + 10f, transform));
        soltados++;
        if (tipo == TipoPajaro.Golondrina) golondrinas++;
    }

    // La base del equipo de este jugador; si el mapa no tiene zonas de compra, donde está el jugador.
    private bool Base(out Vector3 centro, out float piso)
    {
        if (BuyZone.Area(EquiposTacticos.LadoLocal, out Bounds zona))
        {
            centro = zona.center;
            piso = zona.min.y;
            return true;
        }
        Transform local = partida != null && partida.Local != null ? partida.Local.transform : null;
        centro = local != null ? local.position : Vector3.zero;
        piso = centro.y;
        return local != null;
    }

    // ---------- Disparos (US 157; los llama WeaponFire) ----------

    /// <summary>Hasta dónde llega el disparo: cualquier arma llega a los pájaros.</summary>
    public static float Alcance(float alcance) => Mathf.Max(alcance, AlcanceMinimo);

    public static void ContarDisparo()
    {
        if (!Activo) return;
        Sesion.disparos++;
        EstaFase.disparos++;
    }

    /// <summary>CA4: el pájaro más cercano en la línea del disparo. Las paredes y los jugadores no frenan la bala.</summary>
    public static Pajaro Buscar(Vector3 origen, Vector3 direccion, float alcance, out Vector3 punto)
    {
        punto = default;
        Pajaro mejor = null;
        float cerca = float.MaxValue;
        int cantidad = Physics.RaycastNonAlloc(origen, direccion, golpes, alcance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < cantidad; i++)
        {
            Pajaro pajaro = golpes[i].collider.GetComponent<Pajaro>();
            if (pajaro == null || pajaro.Cayendo || golpes[i].distance >= cerca) continue;
            mejor = pajaro;
            cerca = golpes[i].distance;
            punto = golpes[i].point;
        }
        return mejor;
    }

    /// <summary>Le pegó a un pájaro: cae, suma y muestra su puntaje. Devuelve false si ya estaba cayendo.</summary>
    public static bool Acertar(Pajaro pajaro, Vector3 punto)
    {
        if (!Activo || pajaro == null || pajaro.Cayendo) return false;
        float reaccion = Time.time - pajaro.Aparecio; // US 159, CA1: desde que apareció hasta que le pegó
        Sesion.Sumar(pajaro.Tipo, pajaro.Puntos, reaccion);
        EstaFase.Sumar(pajaro.Tipo, pajaro.Puntos, reaccion);
        pajaro.Acertado(punto);
        actual.MostrarPuntaje(pajaro.Tipo, punto);
        actual.Sonar(pajaro.Tipo);
        return true;
    }

    // ---------- Puntaje en pantalla (US 158) ----------

    private void MostrarPuntaje(TipoPajaro tipo, Vector3 punto)
    {
        if (lienzo == null) ArmarLienzo();
        TMP_FontAsset letra = MatchHud.Instance != null ? MatchHud.Instance.DisplayFont : null;
        string texto = "+" + Pajaro.PuntosDe(tipo);

        var rect = new GameObject(texto, typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        rect.SetParent(lienzo, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(200f, 60f);
        // CA3: blanco; el de la golondrina, naranja. Con una sombra para que se lea contra el cielo.
        Texto(rect, "Sombra", letra, texto, new Color(0f, 0f, 0f, 0.55f), new Vector2(2f, -2f));
        Texto(rect, "Numero", letra, texto, tipo == TipoPajaro.Golondrina ? ShopUIKit.Accent : Color.white, Vector2.zero);
        puntajes.Add(new Puntaje { rect = rect, grupo = rect.GetComponent<CanvasGroup>(), punto = punto, desde = Time.time });
    }

    private static void Texto(RectTransform padre, string nombre, TMP_FontAsset letra, string texto, Color color, Vector2 corrimiento)
    {
        var rect = new GameObject(nombre, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(padre, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = corrimiento;
        ShopUIKit.Text(rect, letra, TamanoPuntaje, color, TextAlignmentOptions.Center).text = texto;
    }

    // CA2: sube un poco y se desvanece en 0,8 s, siguiendo el lugar del impacto en la pantalla.
    private void MoverPuntajes()
    {
        if (puntajes.Count == 0 || lienzo == null) return;
        Camera camara = Camera.main;
        Vector2 medida = lienzo.rect.size;
        for (int i = puntajes.Count - 1; i >= 0; i--)
        {
            Puntaje p = puntajes[i];
            float t = (Time.time - p.desde) / DuracionPuntaje;
            if (t >= 1f || p.rect == null || camara == null)
            {
                if (p.rect != null) Destroy(p.rect.gameObject);
                puntajes.RemoveAt(i);
                continue;
            }
            Vector3 v = camara.WorldToViewportPoint(p.punto);
            p.rect.gameObject.SetActive(v.z > 0f);
            float subida = SubidaPuntaje * (1f - (1f - t) * (1f - t));
            p.rect.anchoredPosition = new Vector2((v.x - 0.5f) * medida.x, (v.y - 0.5f) * medida.y + 24f + subida);
            p.rect.localScale = Vector3.one * (1f + 0.25f * Mathf.Pow(1f - t, 4f));
            p.grupo.alpha = t < 0.45f ? 1f : 1f - (t - 0.45f) / 0.55f;
        }
    }

    private void ArmarLienzo()
    {
        var go = new GameObject("Puntajes del minijuego", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        go.layer = 5;
        go.transform.SetParent(transform, false);
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 36; // debajo del HUD (40), la tienda y la pausa
        var escala = go.GetComponent<UnityEngine.UI.CanvasScaler>();
        escala.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        lienzo = (RectTransform)go.transform;
    }

    // ---------- Sonido (US 158, CA6) ----------

    // Un "plin" corto y propio del minijuego, más agudo cuanto más chico el pájaro. Va por el canal de efectos.
    private void Sonar(TipoPajaro tipo)
    {
        if (fuente == null)
        {
            fuente = gameObject.AddComponent<AudioSource>();
            fuente.playOnAwake = false;
            fuente.spatialBlend = 0f;
            Pistola pistola = partida != null && partida.Local != null ? partida.Local.GetComponentInChildren<Pistola>(true) : null;
            if (pistola != null) fuente.outputAudioMixerGroup = pistola.sfxGroup;
        }
        int i = (int)tipo;
        if (sonidos[i] == null) sonidos[i] = Plin(tipo == TipoPajaro.Paloma ? 880f : tipo == TipoPajaro.Gorrion ? 1175f : 1568f);
        fuente.PlayOneShot(sonidos[i], 0.55f);
    }

    private static AudioClip Plin(float frecuencia)
    {
        const int Muestreo = 44100;
        int muestras = Mathf.RoundToInt(Muestreo * 0.18f);
        var datos = new float[muestras];
        float fase = 0f;
        for (int i = 0; i < muestras; i++)
        {
            float t = (float)i / Muestreo;
            float envolvente = Mathf.Min(1f, t * 500f) * Mathf.Exp(-t * 24f);
            fase += 2f * Mathf.PI * frecuencia * (1f + 0.3f * (1f - Mathf.Exp(-t * 35f))) / Muestreo;
            datos[i] = envolvente * (0.75f * Mathf.Sin(fase) + 0.25f * Mathf.Sin(2f * fase)) * 0.6f;
        }
        AudioClip clip = AudioClip.Create("Acierto del minijuego", muestras, 1, Muestreo, false);
        clip.SetData(datos, 0);
        return clip;
    }
}
