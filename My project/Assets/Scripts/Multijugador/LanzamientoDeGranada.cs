using UnityEngine;

// US 259, CA2: el lanzamiento de la granada que ven los demás jugadores. El brazo va hacia atrás y la arroja con la
// animación de Mixamo "LanzarGranada" (Personajes/Animaciones), en la capa "Granada" del Animator del personaje: una
// capa que mueve el torso, la cabeza y los brazos, no las piernas (máscara BrazosGranada), y que está apagada (peso 0)
// salvo mientras lanza. La capa y la máscara las arma el editor solo (AnimacionesTerceraPersona) cuando está la animación.
// Mientras lanza, el IK de los brazos de ArmaEnTerceraPersona se suelta (ManosLibres) para que mande la animación.
// Empieza cuando el dueño hace clic (RpcLanzandoGranada) y suelta la granada al final del movimiento hacia adelante:
// la granada de muestra que llega por la red (que el dueño suelta a los 0,34 s) espera a ese momento (FaltaParaSoltar).
// Con una granada en la mano (ConGranada), la misma capa lo deja en la pose del principio de la animación (estado
// SostenerGranada, quieto): la granada a la altura del pecho, en vez de la pose de rifle.
// Lo usan las copias de los jugadores remotos (JugadorEnRed) y el muñeco espejo (EspejoTerceraPersona).
public class LanzamientoDeGranada : MonoBehaviour
{
    public const string Capa = "Granada", Estado = "LanzarGranada", Sostener = "SostenerGranada";

    // La animación (3,2 s): arranca en 0,65 s (antes está quieto) y suelta la granada en 1,83 s, cuando la mano llega
    // arriba y adelante. Va a 2,4 veces su velocidad (la del estado, en el Animator): suelta a los 0,49 s del clic.
    public const float Largo = 3.2f, Velocidad = 2.4f;
    public const float Inicio = 0.65f / Largo, Suelta = 1.83f / Largo;
    private const float Entrar = 0.1f, Salir = 0.3f, SinGranada = 0.35f;

    /// <summary>Segundos desde que empieza hasta que suelta la granada.</summary>
    public static float HastaSoltar => (Suelta - Inicio) * Largo / Velocidad;
    private static float Dura => (1f - Inicio) * Largo / Velocidad;

    private Animator animador;
    private ArmaEnTerceraPersona arma;
    private int capa = -1;
    private float desde = -10f;
    private float peso;
    private bool sosteniendo;

    /// <summary>Tiene una granada en la mano (lo pone el que maneja la copia).</summary>
    public bool ConGranada { get; set; }

    public static LanzamientoDeGranada Crear(GameObject personaje, Animator animador, ArmaEnTerceraPersona arma)
    {
        if (personaje == null || animador == null) return null;
        LanzamientoDeGranada l = personaje.AddComponent<LanzamientoDeGranada>();
        l.animador = animador;
        l.arma = arma;
        return l;
    }

    private float Pasado => Time.time - desde;
    private bool Lanzando => Pasado < Dura;

    /// <summary>Lo que falta para que suelte la granada (0 si ya la soltó o no está lanzando).</summary>
    public float FaltaParaSoltar => Lanzando ? Mathf.Max(0f, HastaSoltar - Pasado) : 0f;

    /// <summary>Ya la soltó y todavía no sacó la siguiente: la mano está vacía.</summary>
    public bool ManoVacia => Pasado >= HastaSoltar && Pasado < HastaSoltar + SinGranada;

    /// <summary>Empieza a lanzar (el dueño hizo clic).</summary>
    public void Lanzar()
    {
        if (animador == null) return;
        if (capa < 0) capa = animador.GetLayerIndex(Capa);
        if (capa < 0)
        {
            Debug.LogWarning("LanzamientoDeGranada: el Animator no tiene la capa \"" + Capa + "\" (se arma sola en el editor cuando está la animación LanzarGranada).");
            return;
        }
        // Desde la pose de sostenerla, pasa al lanzamiento en un instante (sin saltar de golpe).
        if (peso > 0.01f && !Lanzando) animador.CrossFadeInFixedTime(Estado, 0.08f, capa, Inicio * Largo);
        else animador.Play(Estado, capa, Inicio);
        desde = Time.time;
        sosteniendo = false;
    }

    private void Update()
    {
        if (animador == null) return;
        if (capa < 0) capa = animador.GetLayerIndex(Capa);
        if (capa < 0) return;
        float t = Pasado;
        bool lanzando = t < Dura;
        // Al terminar de lanzar, si sigue con una granada (sacó la siguiente), vuelve a sostenerla.
        if (!lanzando && ConGranada && !sosteniendo)
        {
            animador.CrossFadeInFixedTime(Sostener, peso > 0.01f ? 0.15f : 0f, capa, 0f);
            sosteniendo = true;
        }
        if (!ConGranada) sosteniendo = false;
        // Prendida mientras lanza o la sostiene; se apaga suave al terminar de lanzar la última o al cambiar de arma.
        bool prendida = (lanzando && t < Dura - Salir) || (!lanzando && ConGranada) || (lanzando && ConGranada);
        float rapidez = prendida ? 1f / Entrar : 1f / Salir;
        peso = Mathf.MoveTowards(peso, prendida ? 1f : 0f, rapidez * Time.deltaTime);
        float visto = Suave(peso);
        animador.SetLayerWeight(capa, visto);
        if (arma != null) arma.ManosLibres = visto;
    }

    private static float Suave(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
