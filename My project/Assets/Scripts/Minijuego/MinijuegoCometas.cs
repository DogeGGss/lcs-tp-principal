using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Minijuego de los cometas (F21): durante la fase de compra del Táctico pasan cometas a lo lejos, frente a la base, y
// el jugador les puede disparar, como un aim lab. Lo agrega RondasTacticas y lo prende y apaga en cada cambio de fase.
// - US 156: los cometas (Cometa). Solo en la compra (CA1); entran por un costado de la vista y cruzan en línea recta
//   frente al mirador de la base (MiradorCometas), a 150-300 m y a la altura de la vista (CA2); uno o una lluvia de 3 a
//   5 comunes o raros cada 1 a 2 s (CA3); los épicos son 1 de cada 6 (CA4). Son de cada jugador: no pasan por la red
//   (CA6). 3 s antes del combate dejan de salir y los que quedan se alejan, así no distraen cuando empieza la ronda.
// - US 157: mientras está activo, WeaponFire solo le pega a los cometas y las armas no gastan balas. Las granadas las
//   traba RondasTacticas y el cuchillo no hace daño (MeleeAttack).
// - US 158: cada acierto muestra "+puntos" en el lugar del impacto (blanco; el épico en naranja), que sube y se
//   desvanece en 0,8 s, con la marca de impacto de la mira y un sonido propio. No hay contador en el HUD.
// - US 159: los resultados de cada fase (EstaFase) se suman a las estadísticas guardadas al terminar la fase
//   (EstadisticasMinijuego), y se ven en el Perfil. Sesion suma lo de mientras el juego está abierto.
// - US 160: cada cometa destruido cuenta para los logros (LogrosCometas).
public class MinijuegoCometas : MonoBehaviour
{
    private const float DistanciaMinima = 150f, DistanciaMaxima = 300f; // m del mirador (CA2)
    private const float AlturaMinima = -10f, AlturaMaxima = 25f;        // m respecto del piso del mirador (CA2)
    // Grados a cada lado del frente por donde entran y salen. Los cometas se encienden y se apagan en las puntas del
    // recorrido (Cometa): bien encendidos se ven más o menos entre -45° y 45°, y aparecen y desaparecen más afuera y lejos.
    private const float Abertura = 60f;
    private const float ProbabilidadLluvia = 0.3f;
    private const float ProbabilidadAzul = 0.75f; // Entre comunes y raros, tanto solos como en lluvias.
    private const float Despejar = 3f;                                  // s antes del combate en que se van todos
    private const float AlcanceMinimo = 600f;                           // m: con cualquier arma se llega a los cometas
    private const float DuracionPuntaje = 0.8f, SubidaPuntaje = 46f, TamanoPuntaje = 40f; // US 158, CA2

    /// <summary>Lo que se registra del minijuego en una fase (US 159, CA1).</summary>
    public sealed class Resultados
    {
        public int disparos, puntos;
        public readonly int[] aciertos = new int[3]; // por tipo de cometa
        public float reaccionTotal, mejorReaccion = float.PositiveInfinity;

        public int Aciertos => aciertos[0] + aciertos[1] + aciertos[2];
        public float Precision => disparos > 0 ? (float)Aciertos / disparos : 0f;
        public float ReaccionPromedio => Aciertos > 0 ? reaccionTotal / Aciertos : 0f;

        internal void Sumar(TipoCometa tipo, int puntos, float reaccion)
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

    /// <summary>Hay cometas: es la fase de compra del Táctico (US 157).</summary>
    public static bool Activo => actual != null && actual.activo;
    private static MinijuegoCometas actual;

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
    private int soltados, epicos;
    private readonly List<Cometa> cometas = new List<Cometa>();
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
        // Si la partida termina en medio de la compra (se sale o se corta), lo de esta fase también cuenta (US 159).
        if (activo) EstadisticasMinijuego.SumarFase(EstaFase);
        if (actual == this) actual = null;
    }

    /// <summary>Lo llama RondasTacticas en cada cambio de fase: solo hay cometas durante la compra (US 156, CA1).</summary>
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
        foreach (Cometa cometa in cometas) if (cometa != null) cometa.Irse();
        cometas.Clear();
        EstadisticasMinijuego.SumarFase(EstaFase); // US 159, CA1
        if (EstaFase.disparos > 0)
            Debug.Log($"Minijuego: {EstaFase.Aciertos} cometas, {EstaFase.puntos} puntos, precisión {EstaFase.Precision:P0} " +
                      $"(en total: {Sesion.Aciertos} cometas, {Sesion.puntos} puntos).");
    }

    private void Update()
    {
        if (!activo) return;
        cometas.RemoveAll(c => c == null);
        // Los últimos segundos de la compra: no salen más y los que quedan se alejan.
        float restante = RondasTacticas.Actual != null ? RondasTacticas.Actual.Restante : 0f;
        if (restante > 0f && restante <= Despejar)
        {
            if (!despejando) foreach (Cometa cometa in cometas) cometa.Irse();
            despejando = true;
            return;
        }
        if (Time.time < proximo) return;
        proximo = Time.time + Random.Range(1f, 2f); // CA3
        Soltar();
    }

    private void LateUpdate() => MoverPuntajes();

    // ---------- Cometas (US 156) ----------

    private void Soltar()
    {
        if (!Mirador(out Vector3 origen, out Vector3 frente)) return;

        // CA2: cruza de un costado al otro de la vista, a una distancia y una altura al azar, con una leve inclinación.
        float distancia = Random.Range(DistanciaMinima, DistanciaMaxima);
        var costado = Vector3.Cross(Vector3.up, frente).normalized;
        float ancho = distancia * Mathf.Tan(Abertura * Mathf.Deg2Rad);
        float lado = Random.value < 0.5f ? 1f : -1f;
        Vector3 centro = origen + frente * distancia;
        centro.y = origen.y + Random.Range(AlturaMinima, AlturaMaxima);
        Vector3 desde = centro - costado * (ancho * lado) + frente * Random.Range(-40f, 40f);
        Vector3 hasta = centro + costado * (ancho * lado) + frente * Random.Range(-40f, 40f) + Vector3.up * Random.Range(-10f, 10f);
        Vector3 direccion = (hasta - desde).normalized;
        float recorrido = Vector3.Distance(desde, hasta);

        // CA3: una lluvia de 3 a 5 comunes o raros, uno detrás del otro y un poco corridos.
        if (Random.value < ProbabilidadLluvia)
        {
            TipoCometa tipo = Random.value < ProbabilidadAzul ? TipoCometa.Comun : TipoCometa.Raro;
            int cantidad = Random.Range(3, 6);
            float separacion = Cometa.DiametroDe(tipo) * 4f;
            Vector3 arriba = Vector3.Cross(direccion, costado).normalized;
            for (int i = 0; i < cantidad; i++)
            {
                float atras = i * separacion + Random.Range(0f, separacion * 0.5f);
                Vector3 corrimiento = arriba * Random.Range(-separacion, separacion) + frente * Random.Range(-separacion, separacion);
                Nuevo(tipo, desde - direccion * atras + corrimiento, direccion, recorrido + atras);
            }
            return;
        }

        // CA4: los épicos van solos y son 1 de cada 6 cometas.
        TipoCometa solo = (epicos + 1) * 6 <= soltados + 1 ? TipoCometa.Epico
            : Random.value < ProbabilidadAzul ? TipoCometa.Comun : TipoCometa.Raro;
        Nuevo(solo, desde, direccion, recorrido);
    }

    private void Nuevo(TipoCometa tipo, Vector3 desde, Vector3 direccion, float recorrido)
    {
        cometas.Add(Cometa.Crear(tipo, desde, direccion, recorrido, transform));
        soltados++;
        if (tipo == TipoCometa.Epico) epicos++;
    }

    // Desde dónde se mira: el mirador de la base de este jugador. Si el mapa no tiene, el centro de su base mirando
    // hacia afuera del mapa; y si tampoco tiene zonas de compra, donde está el jugador, mirando hacia adelante.
    private bool Mirador(out Vector3 origen, out Vector3 frente)
    {
        MiradorCometas mirador = MiradorCometas.De(EquiposTacticos.LadoLocal);
        if (mirador != null)
        {
            origen = mirador.transform.position;
            frente = mirador.Frente;
            return true;
        }
        if (BuyZone.Area(EquiposTacticos.LadoLocal, out Bounds zona))
        {
            origen = new Vector3(zona.center.x, zona.min.y, zona.center.z);
            frente = new Vector3(origen.x, 0f, origen.z);
            frente = frente.sqrMagnitude > 0.01f ? frente.normalized : Vector3.forward;
            return true;
        }
        Transform local = partida != null && partida.Local != null ? partida.Local.transform : null;
        origen = local != null ? local.position : Vector3.zero;
        frente = local != null ? Vector3.ProjectOnPlane(local.forward, Vector3.up).normalized : Vector3.forward;
        if (frente.sqrMagnitude < 0.01f) frente = Vector3.forward;
        return local != null;
    }

    // ---------- Disparos (US 157; los llama WeaponFire) ----------

    /// <summary>Hasta dónde llega el disparo: cualquier arma llega a los cometas.</summary>
    public static float Alcance(float alcance) => Mathf.Max(alcance, AlcanceMinimo);

    public static void ContarDisparo()
    {
        if (!Activo) return;
        Sesion.disparos++;
        EstaFase.disparos++;
    }

    /// <summary>CA4: el cometa más cercano en la línea del disparo. Las paredes y los jugadores no frenan la bala.</summary>
    public static Cometa Buscar(Vector3 origen, Vector3 direccion, float alcance, out Vector3 punto)
    {
        punto = default;
        Cometa mejor = null;
        float cerca = float.MaxValue;
        int cantidad = Physics.RaycastNonAlloc(origen, direccion, golpes, alcance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < cantidad; i++)
        {
            Cometa cometa = golpes[i].collider.GetComponent<Cometa>();
            if (cometa == null || cometa.Estallo || golpes[i].distance >= cerca) continue;
            mejor = cometa;
            cerca = golpes[i].distance;
            punto = golpes[i].point;
        }
        return mejor;
    }

    /// <summary>Le pegó a un cometa: estalla, suma y muestra su puntaje. Devuelve false si ya había estallado.</summary>
    public static bool Acertar(Cometa cometa, Vector3 punto)
    {
        if (!Activo || cometa == null || cometa.Estallo) return false;
        float reaccion = Time.time - cometa.Aparecio; // US 159, CA1: desde que apareció hasta que le pegó
        Sesion.Sumar(cometa.Tipo, cometa.Puntos, reaccion);
        EstaFase.Sumar(cometa.Tipo, cometa.Puntos, reaccion);
        cometa.Acertado(punto);
        LogrosCometas.Registrar(cometa.Tipo);
        actual.MostrarPuntaje(cometa.Tipo, punto);
        actual.Sonar(cometa.Tipo);
        return true;
    }

    // ---------- Puntaje en pantalla (US 158) ----------

    private void MostrarPuntaje(TipoCometa tipo, Vector3 punto)
    {
        if (lienzo == null) ArmarLienzo();
        TMP_FontAsset letra = MatchHud.Instance != null ? MatchHud.Instance.DisplayFont : null;
        string texto = "+" + Cometa.PuntosDe(tipo);

        var rect = new GameObject(texto, typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        rect.SetParent(lienzo, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(200f, 60f);
        // CA3: blanco; el del épico, naranja. Con una sombra para que se lea contra el espacio.
        Texto(rect, "Sombra", letra, texto, new Color(0f, 0f, 0f, 0.55f), new Vector2(2f, -2f));
        Texto(rect, "Numero", letra, texto, tipo == TipoCometa.Epico ? ShopUIKit.Accent : Color.white, Vector2.zero);
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

    // Un "plin" corto y propio del minijuego, más agudo cuanto más raro el cometa. Va por el canal de efectos.
    private void Sonar(TipoCometa tipo)
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
        if (sonidos[i] == null) sonidos[i] = Plin(tipo == TipoCometa.Comun ? 880f : tipo == TipoCometa.Raro ? 1175f : 1568f);
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
