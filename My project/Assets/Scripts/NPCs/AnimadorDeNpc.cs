using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// F11 (NPCs): las animaciones de un NPC, puestas desde el código. Los modelos traen un Animator Controller de muestra
// que pasa solo de una animación a la otra, en cadena: no sirve. Acá se saca y las animaciones de su tipo (TipoDeNpc)
// se reproducen directo, con un fundido corto entre una y otra. Las de loop (caminar, correr, quieto) se repiten
// aunque el clip no esté marcado como loop en su importación; las demás (atacar, morir) quedan en el último cuadro.
// El tiempo es el del juego: en pausa (Time.timeScale 0) se frenan.
// Encima, en otra capa, la de recibir un golpe (Golpe): solo del torso para arriba, mezclada con lo que esté haciendo,
// así sigue caminando mientras se dobla por el impacto (US 038, CA3).
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

    // La capa del golpe recibido (US 038, CA3).
    private const float GolpeSube = 0.06f;   // s hasta que se ve entera
    private const float GolpeDuraMax = 0.55f; // s: si el clip es más largo, se acelera
    private AnimationLayerMixerPlayable capas;
    private AnimationClipPlayable golpe;
    private float golpeT = -1f, golpeDura, golpePeso;

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
        capas = AnimationLayerMixerPlayable.Create(grafo, 2);
        grafo.Connect(mezcla, 0, capas, 0);
        capas.SetInputWeight(0, 1f);
        capas.SetInputWeight(1, 0f);
        AvatarMask torso = MascaraDelTorso(animador.transform);
        if (torso != null) capas.SetLayerMaskFromAvatarMask(1, torso);
        salida.SetSourcePlayable(capas);
        grafo.Play();
    }

    /// <summary>
    /// La reacción a un golpe, encima de lo que esté haciendo (US 038, CA3), con ese peso (0 a 1). Con muchos tiros
    /// seguidos no la reinicia a cada rato: solo cuando ya va por la mitad, así no tiembla.
    /// </summary>
    public void Golpe(AnimationClip clip, float peso = 0.85f)
    {
        if (clip == null || !grafo.IsValid() || clip.length <= 0f) return;
        if (golpeT >= 0f && golpeT < golpeDura * 0.5f) return;
        if (!golpe.IsValid() || golpe.GetAnimationClip() != clip)
        {
            if (golpe.IsValid()) { grafo.Disconnect(capas, 1); golpe.Destroy(); }
            golpe = AnimationClipPlayable.Create(grafo, clip);
            golpe.SetApplyFootIK(false);
            grafo.Connect(golpe, 0, capas, 1);
        }
        golpeDura = Mathf.Min(clip.length, GolpeDuraMax);
        golpe.SetSpeed(clip.length / golpeDura);
        golpe.SetTime(0);
        golpePeso = Mathf.Clamp01(peso);
        golpeT = 0f;
    }

    /// <summary>Saca la reacción al golpe (por ejemplo, al morir).</summary>
    public void CortarGolpe()
    {
        golpeT = -1f;
        if (capas.IsValid()) capas.SetInputWeight(1, 0f);
    }

    // Del primer hueso de la columna para arriba (torso, brazos y cabeza): las patas siguen con lo de abajo.
    private static AvatarMask MascaraDelTorso(Transform raiz)
    {
        Transform columna = null;
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            if (t.name.EndsWith("Spine")) { columna = t; break; }
        if (columna == null) return null; // sin columna conocida: el golpe va a todo el cuerpo
        Transform[] todos = raiz.GetComponentsInChildren<Transform>(true);
        var mascara = new AvatarMask { name = "Torso" };
        mascara.transformCount = todos.Length;
        for (int i = 0; i < todos.Length; i++)
        {
            mascara.SetTransformPath(i, Camino(raiz, todos[i]));
            mascara.SetTransformActive(i, todos[i] == columna || todos[i].IsChildOf(columna));
        }
        return mascara;
    }

    private static string Camino(Transform raiz, Transform t)
    {
        if (t == raiz) return "";
        string camino = t.name;
        for (Transform p = t.parent; p != null && p != raiz; p = p.parent) camino = p.name + "/" + camino;
        return camino;
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
        // El golpe: sube enseguida y se va apagando hasta el final.
        if (golpeT >= 0f)
        {
            golpeT += dt;
            float peso = golpeT >= golpeDura ? 0f
                : golpePeso * Mathf.Min(1f, golpeT / GolpeSube) * Mathf.Clamp01((golpeDura - golpeT) / (golpeDura * 0.6f));
            capas.SetInputWeight(1, peso);
            if (golpeT >= golpeDura) golpeT = -1f;
        }
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
