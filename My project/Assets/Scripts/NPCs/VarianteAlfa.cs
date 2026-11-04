using UnityEngine;
using UnityEngine.Rendering;

// F11 (NPCs), US 301: el alfa, una variante más fuerte de cualquier tipo (salvo el Jefe), sin modelos nuevos. Cuándo
// sale lo decide el modo (Invasión: OleadasDeInvasion, con los números en ConfigInvasion); Npc.Crear lo arma así:
// - CA2: más grande que su tipo (escala), con las marcas y los ojos violetas, más opacos y más brillantes
//   (MarcasQueBrillan), y un aura de partículas violetas que suben desde todo el cuerpo (AuraDeAlfa), como los alfas
//   de ARK: se ve de lejos y de espaldas, y en los Watchers, que no tienen marcas, también.
// - CA3: 2,5 veces la vida de su tipo y un 50 % más de daño, también con las bolas y el rayo de los Watchers.
// - CA4: 300 puntos al morir (MuerteDeNpc); adentro del anillo cargan el puesto como cualquier punto (US 292).
[System.Serializable]
public class VarianteAlfa
{
    [Tooltip("CA3: cuántas veces la vida de su tipo.")]
    [Min(1f)] public float vida = 2.5f;
    [Tooltip("CA3: cuántas veces el daño de su tipo (golpes, bolas y rayo).")]
    [Min(1f)] public float danio = 1.5f;
    [Tooltip("CA2: cuántas veces el tamaño de su tipo.")]
    [Min(1f)] public float escala = 1.25f;
    [Tooltip("CA4: los puntos al morir (con o sin baja a la cabeza).")]
    [Min(0)] public int puntos = 300;
    [Tooltip("CA2: el color de las marcas, los ojos y el aura (no lo usa ningún tipo).")]
    [ColorUsage(false, false)] public Color color = new Color(0.62f, 0.25f, 1f);
    [Tooltip("CA2: la opacidad mínima de las marcas (las de los tipos son de 0,13 a 0,15: casi no se ven).")]
    [Range(0f, 1f)] public float opacidadDeMarcas = 0.6f;
    [Tooltip("CA2: cuántas veces más brillan las marcas y los ojos que los de su tipo.")]
    [Min(1f)] public float masBrillo = 1.6f;
    [Tooltip("CA2: partículas del aura por segundo, por cada metro de alto del bicho.")]
    [Min(0f)] public float aura = 22f;
}

// CA2: el aura del alfa. Partículas violetas que salen de la superficie del modelo animado (así siguen la forma de
// cualquier bicho), suben despacio y se apagan; quedan en el mundo, así deja una estela al moverse. Al morir deja de
// soltar. Se dibujan con Efectos/ParticulaBrillante, sumando luz, como los destellos de los ataques a distancia.
public class AuraDeAlfa : MonoBehaviour
{
    private Npc npc;
    private ParticleSystem[] sistemas;
    private bool apagada;

    /// <summary>Le pone el aura al modelo del alfa (alto: el alto del bicho en m).</summary>
    public static void Poner(Npc npc, GameObject modelo, VarianteAlfa alfa, float alto)
    {
        if (npc == null || modelo == null || alfa == null || alfa.aura <= 0f) return;
        Material material = Destellos.Material(alfa.color);
        if (material == null) return;
        alto = Mathf.Max(0.3f, alto);

        var sistemas = new System.Collections.Generic.List<ParticleSystem>();
        Renderer[] partes = modelo.GetComponentsInChildren<Renderer>(true);
        var pieles = new System.Collections.Generic.List<Renderer>();
        foreach (Renderer r in partes)
            if (r is SkinnedMeshRenderer || (r is MeshRenderer && r.GetComponent<MeshFilter>() != null)) pieles.Add(r);
        float porParte = alfa.aura * alto / Mathf.Max(1, pieles.Count);
        foreach (Renderer r in pieles) sistemas.Add(Sistema(r, material, alto, porParte));
        if (sistemas.Count == 0) return;

        var aura = npc.gameObject.AddComponent<AuraDeAlfa>();
        aura.npc = npc;
        aura.sistemas = sistemas.ToArray();
    }

    private static ParticleSystem Sistema(Renderer r, Material material, float alto, float porSegundo)
    {
        var go = new GameObject("Aura de alfa");
        go.transform.SetParent(r.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var m = ps.main;
        m.playOnAwake = false;
        m.loop = true;
        m.simulationSpace = ParticleSystemSimulationSpace.World; // la estela queda atrás al moverse
        m.scalingMode = ParticleSystemScalingMode.Shape;         // el tamaño en m, sin el del modelo (el Rápido está a 0,3)
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        m.startSpeed = 0f;
        m.startSize = new ParticleSystem.MinMaxCurve(alto * 0.05f, alto * 0.11f);
        m.startColor = new Color(0.55f, 0.55f, 0.55f, 1f); // suma luz: más bajo, más suave
        m.maxParticles = 80;

        var e = ps.emission;
        e.rateOverTime = porSegundo;

        var forma = ps.shape;
        if (r is SkinnedMeshRenderer piel)
        {
            forma.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
            forma.skinnedMeshRenderer = piel;
        }
        else
        {
            forma.shapeType = ParticleSystemShapeType.MeshRenderer;
            forma.meshRenderer = (MeshRenderer)r;
        }
        forma.meshShapeType = ParticleSystemMeshShapeType.Triangle;

        var sube = ps.velocityOverLifetime;
        sube.enabled = true;
        sube.space = ParticleSystemSimulationSpace.World;
        sube.x = new ParticleSystem.MinMaxCurve(-0.05f * alto, 0.05f * alto);
        sube.y = new ParticleSystem.MinMaxCurve(0.25f * alto, 0.55f * alto);
        sube.z = new ParticleSystem.MinMaxCurve(-0.05f * alto, 0.05f * alto);

        var c = ps.colorOverLifetime;
        c.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        c.color = g;
        var t = ps.sizeOverLifetime;
        t.enabled = true;
        t.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

        var render = go.GetComponent<ParticleSystemRenderer>();
        render.sharedMaterial = material;
        render.shadowCastingMode = ShadowCastingMode.Off;
        render.receiveShadows = false;
        ps.Play();
        return ps;
    }

    private void Update()
    {
        if (apagada || npc == null || npc.Vivo) return;
        apagada = true; // al morir deja de soltar (las que ya salieron se apagan solas)
        foreach (ParticleSystem ps in sistemas)
            if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}
