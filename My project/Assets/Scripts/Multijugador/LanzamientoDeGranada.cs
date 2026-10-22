using UnityEngine;

// US 259, CA2: el lanzamiento de la granada que ven los demás jugadores. El brazo va hacia atrás y la arroja con la
// animación de Mixamo "LanzarGranada" (Personajes/Animaciones), en la capa "Granada" del Animator del personaje: una
// capa que mueve el torso, la cabeza y los brazos, no las piernas (máscara BrazosGranada), y que está apagada (peso 0)
// salvo mientras lanza. La capa y la máscara las arma el editor solo (AnimacionesTerceraPersona) cuando está la animación.
// Mientras lanza, el IK de los brazos de ArmaEnTerceraPersona se suelta (ManosLibres) para que mande la animación.
// Empieza cuando el dueño hace clic (RpcLanzandoGranada) y suelta la granada al final del movimiento hacia adelante:
// la granada de muestra que llega por la red (que el dueño suelta a los 0,34 s) espera a ese momento (FaltaParaSoltar).
// Con una granada en la mano (ConGranada), la misma capa lo deja quieto en la pose lista para lanzar de la animación
// (estado SostenerGranada, en 1 s): la granada arriba junto a la cabeza y la otra mano adelante, en vez de la pose de
// rifle (la animación de Mixamo empieza y termina con un rifle en las manos: esa parte no se usa).
// Lo usan las copias de los jugadores remotos (JugadorEnRed) y el muñeco espejo (EspejoTerceraPersona).
public class LanzamientoDeGranada : MonoBehaviour
{
    public const string Capa = "Granada", Estado = "LanzarGranada", Sostener = "SostenerGranada";

    // La animación (3,2 s, en segundos del clip): hasta 1 s saca la granada de un rifle; en 1 s la tiene lista (la pose
    // de sostenerla), la lleva atrás y la suelta en 1,83 s, cuando la mano llega arriba y adelante; en 2,3 s ya terminó
    // el envión (después vuelve al rifle: no se usa). Va a 2,4 veces su velocidad (la del estado, en el Animator): suelta
    // a los 0,35 s del clic, casi a la vez que el dueño (0,34 s).
    public const float Largo = 3.2f, Velocidad = 2.4f;
    public const float Pose = 1f, Inicio = 1f, Suelta = 1.83f, Fin = 2.3f;
    private const float Entrar = 0.1f, Salir = 0.3f, Volver = 0.2f, SinGranada = 0.35f;

    /// <summary>Segundos desde que empieza hasta que suelta la granada.</summary>
    public static float HastaSoltar => (Suelta - Inicio) / Velocidad;
    private static float Dura => (Fin - Inicio) / Velocidad;

    private Animator animador;
    private ArmaEnTerceraPersona arma;
    private int capa = -1;
    private float desde = -10f;
    private float peso;
    private static readonly int hashSostener = Animator.StringToHash(Sostener);

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
        // Arranca en la misma pose de sostenerla.
        animador.Play(Estado, capa, Inicio / Largo);
        desde = Time.time;
    }

    private void Update()
    {
        if (animador == null) return;
        if (capa < 0) capa = animador.GetLayerIndex(Capa);
        if (capa < 0) return;
        float t = Pasado;
        bool lanzando = t < Dura;
        // Con una granada en la mano y sin lanzar (también al terminar de lanzar, con la siguiente ya en la mano): la
        // pose de sostenerla. Se revisa cada cuadro, así vuelve a ella pase lo que pase en la capa.
        if (!lanzando && ConGranada)
        {
            bool yendo = animador.IsInTransition(capa) && animador.GetNextAnimatorStateInfo(capa).shortNameHash == hashSostener;
            if (!yendo && animador.GetCurrentAnimatorStateInfo(capa).shortNameHash != hashSostener)
            {
                if (peso > 0.01f) animador.CrossFadeInFixedTime(hashSostener, Volver, capa, Pose); // termina de lanzar
                else animador.Play(hashSostener, capa, Pose / Largo);                              // la saca
            }
        }
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
