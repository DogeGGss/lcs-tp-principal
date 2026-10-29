using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// F11 (NPCs): las animaciones de un NPC, puestas desde el código. Los modelos traen un Animator Controller de muestra
// que pasa solo de una animación a la otra, en cadena: no sirve. Acá se saca y las animaciones de su tipo (TipoDeNpc)
// se reproducen directo, con un fundido corto entre una y otra. Las de loop (caminar, correr, quieto) se repiten
// aunque el clip no esté marcado como loop en su importación; las demás (atacar, morir) quedan en el último cuadro.
// El tiempo es el del juego: en pausa (Time.timeScale 0) se frenan.
public class AnimadorDeNpc
{
    private const float FundidoPorDefecto = 0.2f;

    private PlayableGraph grafo;
    private AnimationMixerPlayable mezcla;
    private readonly AnimationClipPlayable[] entradas = new AnimationClipPlayable[2];
    private readonly AnimationClip[] clips = new AnimationClip[2];
    private int actual = -1;      // la entrada que sube
    private bool enLoop;
    private float fundido, duracionFundido;
    private float velocidad = 1f;

    /// <summary>El clip que está sonando (o null).</summary>
    public AnimationClip Clip => actual >= 0 ? clips[actual] : null;

    /// <summary>Cuánto va del clip actual (0 a 1; sin loop, 1 al terminar).</summary>
    public float Avance
    {
        get
        {
            if (actual < 0 || clips[actual] == null || clips[actual].length <= 0f) return 1f;
            return Mathf.Clamp01((float)entradas[actual].GetTime() / clips[actual].length);
        }
    }

    public AnimadorDeNpc(Animator animador)
    {
        animador.runtimeAnimatorController = null; // el de muestra (encadena las animaciones solo)
        grafo = PlayableGraph.Create("NPC · " + animador.name);
        grafo.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        var salida = AnimationPlayableOutput.Create(grafo, "Animaciones", animador);
        mezcla = AnimationMixerPlayable.Create(grafo, 2);
        salida.SetSourcePlayable(mezcla);
        grafo.Play();
    }

    /// <summary>Pasa a ese clip (si ya está sonando, no lo reinicia salvo que se pida).</summary>
    public void Poner(AnimationClip clip, bool loop, float fundidoSegundos = FundidoPorDefecto, bool reiniciar = false)
    {
        if (clip == null || !grafo.IsValid()) return;
        if (actual >= 0 && clips[actual] == clip && !reiniciar) { enLoop = loop; return; }
        int nueva = actual < 0 ? 0 : 1 - actual;
        if (entradas[nueva].IsValid())
        {
            grafo.Disconnect(mezcla, nueva);
            entradas[nueva].Destroy();
        }
        entradas[nueva] = AnimationClipPlayable.Create(grafo, clip);
        entradas[nueva].SetApplyFootIK(false);
        entradas[nueva].SetSpeed(velocidad);
        grafo.Connect(entradas[nueva], 0, mezcla, nueva);
        clips[nueva] = clip;
        enLoop = loop;
        if (actual < 0 || fundidoSegundos <= 0f)
        {
            mezcla.SetInputWeight(nueva, 1f);
            mezcla.SetInputWeight(1 - nueva, 0f);
            duracionFundido = 0f;
        }
        else
        {
            duracionFundido = fundidoSegundos;
            fundido = 0f;
        }
        actual = nueva;
    }

    /// <summary>Velocidad de la animación actual (1: la del clip). Para que los pies acompañen al moverse.</summary>
    public void Velocidad(float v)
    {
        velocidad = Mathf.Max(0f, v);
        if (actual >= 0 && entradas[actual].IsValid()) entradas[actual].SetSpeed(velocidad);
    }

    /// <summary>Lo llama su NPC en cada cuadro: el fundido y el loop.</summary>
    public void Actualizar(float dt)
    {
        if (actual < 0 || !grafo.IsValid()) return;
        if (duracionFundido > 0f)
        {
            fundido = Mathf.Min(1f, fundido + dt / duracionFundido);
            mezcla.SetInputWeight(actual, fundido);
            mezcla.SetInputWeight(1 - actual, 1f - fundido);
            if (fundido >= 1f) duracionFundido = 0f;
        }
        AnimationClip clip = clips[actual];
        if (clip == null || clip.length <= 0f) return;
        double t = entradas[actual].GetTime();
        if (enLoop && t >= clip.length) entradas[actual].SetTime(t % clip.length);
        else if (!enLoop && t > clip.length) entradas[actual].SetTime(clip.length); // queda en el último cuadro
    }

    /// <summary>Aplica ya la pose de la animación puesta (sin esperar al cuadro siguiente).</summary>
    public void Evaluar()
    {
        if (grafo.IsValid()) grafo.Evaluate(0f);
    }

    public void Destruir()
    {
        if (grafo.IsValid()) grafo.Destroy();
    }
}
