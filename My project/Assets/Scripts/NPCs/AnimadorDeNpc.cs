using System.Collections.Generic;
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
// Todas las animaciones del tipo se conectan una sola vez, al armarlo (Preparar), y después solo se cambia cuánto pesa
// cada una: conectar una animación nueva mientras juega hace que Unity vuelva a enlazar los huesos, y por un instante
// el bicho quedaba en la pose de su modelo (más estirado y más grande). Cada clip tiene dos entradas, así se puede
// volver a empezar uno que está sonando (dos ataques iguales seguidos) fundiéndolo con donde estaba.
public class AnimadorDeNpc
{
    private const float FundidoPorDefecto = 0.2f;

    private class Entrada
    {
        public AnimationClip clip;
        public AnimationClipPlayable reproductor;
        public int indice;   // su entrada en la mezcla
        public float peso;   // cuánto pesa ahora
        public float desde;  // cuánto pesaba al empezar el fundido
    }

    private PlayableGraph grafo;
    private AnimationMixerPlayable mezcla;
    private readonly List<Entrada> entradas = new List<Entrada>();
    private readonly Dictionary<AnimationClip, Entrada[]> porClip = new Dictionary<AnimationClip, Entrada[]>();
    private Entrada actual;      // la que sube
    private bool enLoop;
    private float fundido, duracionFundido;
    private float velocidad = 1f;

    // La capa del golpe recibido (US 038, CA3): una mezcla aparte, con una entrada por clip de golpe.
    private const float GolpeSube = 0.06f;   // s hasta que se ve entera
    private const float GolpeDuraMax = 0.55f; // s: si el clip es más largo, se acelera
    private AnimationLayerMixerPlayable capas;
    private AnimationMixerPlayable golpes;
    private readonly Dictionary<AnimationClip, int> golpePorClip = new Dictionary<AnimationClip, int>();
    private int golpeActual = -1;
    private float golpeT = -1f, golpeDura, golpePeso;

    /// <summary>El clip que está sonando (o null).</summary>
    public AnimationClip Clip => actual != null ? actual.clip : null;

    /// <summary>Cuánto va del clip actual (0 a 1; sin loop, 1 al terminar).</summary>
    public float Avance
    {
        get
        {
            if (actual == null || actual.clip.length <= 0f) return 1f;
            return Mathf.Clamp01((float)actual.reproductor.GetTime() / actual.clip.length);
        }
    }

    public AnimadorDeNpc(Animator animador)
    {
        animador.runtimeAnimatorController = null; // el de muestra (encadena las animaciones solo)
        grafo = PlayableGraph.Create("NPC · " + animador.name);
        grafo.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        var salida = AnimationPlayableOutput.Create(grafo, "Animaciones", animador);
        mezcla = AnimationMixerPlayable.Create(grafo, 0);
        golpes = AnimationMixerPlayable.Create(grafo, 0);
        capas = AnimationLayerMixerPlayable.Create(grafo, 2);
        grafo.Connect(mezcla, 0, capas, 0);
        grafo.Connect(golpes, 0, capas, 1);
        capas.SetInputWeight(0, 1f);
        capas.SetInputWeight(1, 0f);
        AvatarMask torso = MascaraDelTorso(animador.transform);
        if (torso != null) capas.SetLayerMaskFromAvatarMask(1, torso);
        salida.SetSourcePlayable(capas);
        grafo.Play();
    }

    /// <summary>Conecta de una vez todas las animaciones del tipo (antes de que se vea), así cambiar de una a otra no
    /// vuelve a enlazar los huesos.</summary>
    public void Preparar(TipoDeNpc tipo)
    {
        if (tipo == null || !grafo.IsValid()) return;
        foreach (AnimationClip clip in new[] { tipo.quieto, tipo.quietoEnCombate, tipo.caminar, tipo.correr, tipo.morir,
                     tipo.aturdido, tipo.preparaEmbestida, tipo.embestida, tipo.volar, tipo.escupir, tipo.rugido,
                     tipo.animacionADistancia })
            Entradas(clip);
        if (tipo.ataques != null) foreach (AnimationClip clip in tipo.ataques) Entradas(clip);
        if (tipo.golpeRecibido != null && tipo.golpeRecibido.length > 0f) EntradaDeGolpe(tipo.golpeRecibido);
    }

    // Las dos entradas de ese clip (las crea la primera vez).
    private Entrada[] Entradas(AnimationClip clip)
    {
        if (clip == null) return null;
        if (porClip.TryGetValue(clip, out Entrada[] par)) return par;
        par = new Entrada[2];
        for (int i = 0; i < 2; i++)
        {
            var reproductor = AnimationClipPlayable.Create(grafo, clip);
            reproductor.SetApplyFootIK(false);
            reproductor.Pause(); // sin peso, quieta
            int indice = mezcla.AddInput(reproductor, 0, 0f);
            par[i] = new Entrada { clip = clip, reproductor = reproductor, indice = indice };
            entradas.Add(par[i]);
        }
        porClip[clip] = par;
        return par;
    }

    private int EntradaDeGolpe(AnimationClip clip)
    {
        if (golpePorClip.TryGetValue(clip, out int i)) return i;
        var reproductor = AnimationClipPlayable.Create(grafo, clip);
        reproductor.SetApplyFootIK(false);
        reproductor.Pause();
        i = golpes.AddInput(reproductor, 0, 0f);
        golpePorClip[clip] = i;
        return i;
    }

    /// <summary>
    /// La reacción a un golpe, encima de lo que esté haciendo (US 038, CA3), con ese peso (0 a 1). Con muchos tiros
    /// seguidos no la reinicia a cada rato: solo cuando ya va por la mitad, así no tiembla.
    /// </summary>
    public void Golpe(AnimationClip clip, float peso = 0.85f)
    {
        if (clip == null || !grafo.IsValid() || clip.length <= 0f) return;
        if (golpeT >= 0f && golpeT < golpeDura * 0.5f) return;
        int i = EntradaDeGolpe(clip);
        if (golpeActual >= 0 && golpeActual != i)
        {
            golpes.SetInputWeight(golpeActual, 0f);
            golpes.GetInput(golpeActual).Pause();
        }
        golpeActual = i;
        golpes.SetInputWeight(i, 1f);
        Playable reproductor = golpes.GetInput(i);
        golpeDura = Mathf.Min(clip.length, GolpeDuraMax);
        reproductor.SetSpeed(clip.length / golpeDura);
        reproductor.SetTime(0);
        reproductor.Play();
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
        if (actual != null && actual.clip == clip && !reiniciar) { enLoop = loop; return; }
        Entrada[] par = Entradas(clip);
        // De las dos entradas del clip, la que no está sonando (para volver a empezarlo fundido con donde estaba), o la
        // que menos se ve.
        Entrada nueva = par[0] == actual ? par[1] : par[1] == actual ? par[0] : par[0].peso <= par[1].peso ? par[0] : par[1];
        nueva.reproductor.SetTime(0);
        nueva.reproductor.SetSpeed(velocidad);
        nueva.reproductor.Play();
        enLoop = loop;
        bool habiaAlgo = actual != null;
        actual = nueva;

        if (!habiaAlgo || fundidoSegundos <= 0f)
        {
            foreach (Entrada e in entradas) Pesar(e, e == actual ? 1f : 0f);
            duracionFundido = 0f;
        }
        else
        {
            // Lo que sonaba baja parejo mientras la nueva sube: los pesos siempre suman 1.
            foreach (Entrada e in entradas) e.desde = e.peso;
            duracionFundido = fundidoSegundos;
            fundido = 0f;
        }
    }

    private void Pesar(Entrada e, float peso)
    {
        e.peso = peso;
        mezcla.SetInputWeight(e.indice, peso);
        if (peso <= 0f && e != actual) e.reproductor.Pause();
    }

    /// <summary>Velocidad de la animación actual (1: la del clip). Para que los pies acompañen al moverse.</summary>
    public void Velocidad(float v)
    {
        velocidad = Mathf.Max(0f, v);
        if (actual != null) actual.reproductor.SetSpeed(velocidad);
    }

    /// <summary>Lo llama su NPC en cada cuadro: el fundido y el loop.</summary>
    public void Actualizar(float dt)
    {
        if (actual == null || !grafo.IsValid()) return;
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
            foreach (Entrada e in entradas)
            {
                if (e != actual && e.desde <= 0f) continue;
                Pesar(e, e == actual ? Mathf.Lerp(e.desde, 1f, fundido) : e.desde * (1f - fundido));
            }
            if (fundido >= 1f) duracionFundido = 0f;
        }
        AnimationClip clip = actual.clip;
        if (clip.length <= 0f) return;
        double t = actual.reproductor.GetTime();
        if (enLoop && t >= clip.length) actual.reproductor.SetTime(t % clip.length);
        else if (!enLoop && t > clip.length) actual.reproductor.SetTime(clip.length); // queda en el último cuadro
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
