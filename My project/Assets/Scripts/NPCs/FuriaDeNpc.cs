using UnityEngine;

// F11 (NPCs), US 268: la furia del jefe (los tipos con TipoDeNpc.furia), a la mitad de la vida.
// - CA1: al llegar a la mitad de la vida (1.000 de 2.000) se frena y ruge 2 s con su animación de rugido; mientras
//   ruge sigue recibiendo daño. Si en ese momento está embistiendo o volando, ruge al terminar.
// - CA2: se ve furioso: el cuerpo toma un tono rojo que late y una luz roja en la cabeza; en su barra de vida (US 146)
//   la vida se pone roja y aparece "FURIA" al lado del nombre.
// - CA3: camina un 80 % más rápido y golpea un 50 % más rápido (la animación, el daño y la cadencia).
// - CA4: embiste cada 6 s (en vez de 10, US 266) y vuela cada 15 s (en vez de 25, US 267) escupiendo 5 bolas.
// - CA5: empieza una sola vez y dura hasta que muere.
// Lo agrega Npc.Crear.
[RequireComponent(typeof(Npc))]
public class FuriaDeNpc : MonoBehaviour
{
    private const float Mitad = BarraDeJefe.Furia;      // CA1: la marca de la barra
    private const float Ruge = 2f;                      // CA1: s
    private const float MasRapido = 1.8f;               // CA3: al caminar
    private const float GolpeMasRapido = 1.5f;          // CA3: los golpes
    private const float EmbisteCada = 6f;               // CA4
    private const float VuelaCada = 15f;                // CA4
    private const int BolasEnFuria = 5;                 // CA4
    private static readonly Color Rojo = new Color(1f, 0.42f, 0.38f); // CA2: el tono del cuerpo

    /// <summary>Está furioso (CA5: desde la mitad de la vida hasta que muere).</summary>
    public bool Furioso { get; private set; }

    private Npc npc;
    private PersecucionDeNpc persecucion;
    private bool pendiente, rugiendo;
    private float rugeHasta;
    private Renderer[] partes;
    private MaterialPropertyBlock bloque;
    private Light luz;

    private void Awake()
    {
        npc = GetComponent<Npc>();
        persecucion = GetComponent<PersecucionDeNpc>();
    }

    private void Update()
    {
        if (persecucion == null) persecucion = GetComponent<PersecucionDeNpc>();
        if (npc == null || npc.Vida == null) return;
        if (!npc.Vivo) { Apagar(); enabled = false; return; }

        // CA1: llegó a la mitad.
        if (!Furioso && !pendiente && npc.Vida.currentHealth <= npc.Vida.maxHealth * Mitad) pendiente = true;
        if (pendiente && !rugiendo && (persecucion == null || !persecucion.Ocupado)) Rugir();

        if (rugiendo && Time.time >= rugeHasta)
        {
            rugiendo = false;
            if (persecucion != null) { persecucion.Ocupado = false; persecucion.Controlado = false; }
        }
        if (Furioso) Latir();
    }

    private void Rugir()
    {
        pendiente = false;
        rugiendo = true;
        rugeHasta = Time.time + Ruge;
        if (persecucion != null) { persecucion.Ocupado = true; persecucion.Controlado = false; } // quieto, mirándolo
        if (npc.Tipo.rugido != null && npc.Animaciones != null)
        {
            npc.Animaciones.Poner(npc.Tipo.rugido, false, 0.15f, true);
            npc.Animaciones.Velocidad(npc.Tipo.rugido.length > Ruge ? npc.Tipo.rugido.length / Ruge : 1f);
        }
        SonidoDeNpc.Uno(npc.Tipo.sonidoDeRugido, transform, 60f, 1f); // CA1: con su sonido
        Enfurecer();
    }

    // CA2 a CA4.
    private void Enfurecer()
    {
        Furioso = true;
        npc.MasVelocidad = MasRapido;
        npc.MasVelocidadDeAtaque = GolpeMasRapido;
        EmbestidaDeNpc embestida = GetComponent<EmbestidaDeNpc>();
        if (embestida != null) embestida.MasSeguido(EmbisteCada);
        VueloDeNpc vuelo = GetComponent<VueloDeNpc>();
        if (vuelo != null) vuelo.MasSeguido(VuelaCada, BolasEnFuria);
        if (npc.Barra != null) npc.Barra.Enfurecer();

        // El cuerpo en rojo (sin tocar el material compartido: un bloque de propiedades por pieza).
        partes = npc.Modelo != null ? npc.Modelo.GetComponentsInChildren<Renderer>() : new Renderer[0];
        bloque = new MaterialPropertyBlock();
        // Una luz roja en la cabeza.
        Transform cabeza = null;
        foreach (HitZone z in GetComponentsInChildren<HitZone>())
            if (z.zone == BodyZone.Head) { cabeza = z.transform; break; }
        var go = new GameObject("Furia (luz)");
        go.transform.SetParent(cabeza != null ? cabeza : (npc.Modelo != null ? npc.Modelo : transform), false);
        luz = go.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = new Color(1f, 0.15f, 0.1f);
        luz.range = 7f;
        luz.shadows = LightShadows.None;
    }

    // CA2: el rojo late un poco, para que se note.
    private void Latir()
    {
        float pulso = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);
        Color tono = Color.Lerp(Color.white, Rojo, 0.75f + 0.25f * pulso);
        foreach (Renderer r in partes)
        {
            if (r == null) continue;
            r.GetPropertyBlock(bloque);
            bloque.SetColor("_BaseColor", tono);
            bloque.SetColor("_Color", tono);
            r.SetPropertyBlock(bloque);
        }
        if (luz != null) luz.intensity = 2f + 2f * pulso;
    }

    private void Apagar()
    {
        if (luz != null) Destroy(luz.gameObject);
        luz = null;
    }
}
