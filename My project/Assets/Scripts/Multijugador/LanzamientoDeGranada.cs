using UnityEngine;

// US 259, CA2: el lanzamiento de la granada que ven los demás jugadores. El brazo va hacia atrás y la arroja con la
// animación de Mixamo "LanzarGranada" (Personajes/Animaciones), en la capa "Granada" del Animator del personaje: una
// capa que solo mueve los brazos (máscara BrazosGranada) y que está apagada (peso 0) salvo mientras lanza. La capa y la
// máscara las arma el editor solo (AnimacionesTerceraPersona) cuando está la animación.
// Mientras lanza, el IK de los brazos de ArmaEnTerceraPersona se suelta (ManosLibres) para que mande la animación.
// Empieza cuando el dueño hace clic (RpcLanzandoGranada) y suelta la granada al final del movimiento hacia adelante:
// la granada de muestra que llega por la red (que el dueño suelta a los 0,34 s) espera a ese momento (FaltaParaSoltar).
// Lo usan las copias de los jugadores remotos (JugadorEnRed) y el muñeco espejo (EspejoTerceraPersona).
public class LanzamientoDeGranada : MonoBehaviour
{
    public const string Capa = "Granada", Estado = "LanzarGranada";

    // La animación (3,2 s): arranca en 0,65 s (antes está quieto) y suelta la granada en 1,83 s, cuando la mano llega
    // arriba y adelante. Va a 2,4 veces su velocidad (la del estado, en el Animator): suelta a los 0,49 s del clic.
    public const float Largo = 3.2f, Velocidad = 2.4f;
    public const float Inicio = 0.65f / Largo, Suelta = 1.83f / Largo;
    private const float Entrar = 0.08f, Salir = 0.3f, SinGranada = 0.35f;

    /// <summary>Segundos desde que empieza hasta que suelta la granada.</summary>
    public static float HastaSoltar => (Suelta - Inicio) * Largo / Velocidad;
    private static float Dura => (1f - Inicio) * Largo / Velocidad;

    private Animator animador;
    private ArmaEnTerceraPersona arma;
    private int capa = -1;
    private float desde = -10f;

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
        animador.Play(Estado, capa, Inicio);
        desde = Time.time;
    }

    private void Update()
    {
        if (animador == null || capa < 0) return;
        float t = Pasado;
        float peso = 0f;
        if (t < Dura)
            peso = Suave(t / Entrar) * (1f - Suave((t - (Dura - Salir)) / Salir));
        animador.SetLayerWeight(capa, peso);
        if (arma != null) arma.ManosLibres = peso;
    }

    private static float Suave(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
