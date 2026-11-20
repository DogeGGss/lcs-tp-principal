using UnityEngine;

// F35 (Skins de armas), 19/11/2026: un arma prendida fuego (las skins con Efectos.prendida: el cuchillo Dragón Abisal).
// Lenguas de fuego chicas que salen de toda el arma y suben (Art/Skins/EfectoLlama: SkinEfectos.shader, modo 4) y unas
// brasas que saltan (EfectoBrasa), del color del fuego de la skin. Las llamas van pegadas al arma (se mueven con ella y
// suben hacia arriba del mundo); las brasas quedan en el mundo, como una estela al moverla. El tamaño se toma del arma
// tal como se ve (en primera persona, en la mano de los demás o en la vista previa del perfil, que la agranda), y al
// cortar se aviva (CalorDeSkin).
// SkinDeArma lo pone o lo saca con la skin; va en un hijo de la malla más grande del arma.
public class FuegoDeSkin : MonoBehaviour
{
    public const string Nombre = "Fuego (skin)";

    private ParticleSystem llamas, brasas;
    private Transform malla;
    private Vector3 tamano; // de la malla, en su espacio
    private float largoPuesto = -1f, calorPuesto = -1f;
    private CalorDeSkin calor;

    /// <summary>Lo prende o lo apaga según la skin (también saca el de una copia de otra arma, que viene sin script).</summary>
    public static void Poner(GameObject arma, CatalogoDeSkins.Skin skin)
    {
        if (arma == null) return;
        foreach (Transform t in arma.GetComponentsInChildren<Transform>(true))
            if (t != null && t.name == Nombre)
            {
                t.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(t.gameObject);
                else DestroyImmediate(t.gameObject);
            }
        CatalogoDeSkins.Efectos e = EfectosDeSkin.De(skin);
        if (e == null || !e.prendida) return;
        MeshFilter filtro = MallaMasGrande(arma);
        if (filtro == null) return;
        var go = new GameObject(Nombre) { layer = filtro.gameObject.layer };
        go.transform.SetParent(filtro.transform, false);
        Bounds b = filtro.sharedMesh.bounds;
        go.transform.localPosition = b.center;
        go.AddComponent<FuegoDeSkin>().Armar(filtro.transform, b.size, e);
    }

    private static MeshFilter MallaMasGrande(GameObject go)
    {
        MeshFilter mejor = null;
        float mayor = -1f;
        foreach (MeshFilter f in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (f.sharedMesh == null || f.GetComponent<MeshRenderer>() == null) continue;
            Vector3 t = f.sharedMesh.bounds.size;
            float v = t.x * t.y * t.z;
            if (v > mayor) { mayor = v; mejor = f; }
        }
        return mejor;
    }

    private void Armar(Transform deLaMalla, Vector3 tamanoDeLaMalla, CatalogoDeSkins.Efectos e)
    {
        malla = deLaMalla;
        tamano = tamanoDeLaMalla;
        ModelosDeArmas modelos = ModelosDeArmas.Actual;
        Color fuego = e.fuego, claro = Color.Lerp(e.fuego, e.nucleo, 0.35f);
        llamas = Sistema(gameObject, modelos.MaterialDe("efecto-llama"), fuego, claro, 160, ParticleSystemSimulationSpace.Local);
        var hijo = new GameObject("Brasas") { layer = gameObject.layer };
        hijo.transform.SetParent(transform, false);
        brasas = Sistema(hijo, modelos.MaterialDe("efecto-brasa"), claro, e.nucleo, 30, ParticleSystemSimulationSpace.World);
        Medir();
        llamas.Play();
        brasas.Play();
    }

    // Un sistema de partículas que sale de toda la malla (una caja de su tamaño) y sube.
    private ParticleSystem Sistema(GameObject go, Material material, Color a, Color b, int maximo, ParticleSystemSimulationSpace espacio)
    {
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startSpeed = 0f;
        main.startColor = new ParticleSystem.MinMaxGradient(a, b);
        main.simulationSpace = espacio;
        main.scalingMode = ParticleSystemScalingMode.Shape; // la caja, del tamaño del arma; lo demás se mide en Medir
        main.maxParticles = maximo;
        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Box;
        forma.scale = tamano * 0.85f;
        var hereda = ps.inheritVelocity; // (las del mundo: caminando no se quedan atrás)
        hereda.enabled = espacio == ParticleSystemSimulationSpace.World;
        hereda.mode = ParticleSystemInheritVelocityMode.Current;
        hereda.curve = 0.85f;
        var color = ps.colorOverLifetime;
        color.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
        color.color = g;
        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.25f)));
        var ruido = ps.noise;
        ruido.enabled = true;
        ruido.frequency = 2.5f;
        ruido.scrollSpeed = 1.5f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = material;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    private void Update()
    {
        if (calor == null) calor = GetComponentInParent<CalorDeSkin>();
        Medir();
    }

    // Los tamaños y las velocidades, del largo del arma tal como se ve (L, en metros) y de su calor (al cortar).
    private void Medir()
    {
        if (malla == null || llamas == null || brasas == null) return;
        Vector3 s = malla.lossyScale;
        float largo = Mathf.Max(Mathf.Abs(tamano.x * s.x), Mathf.Abs(tamano.y * s.y), Mathf.Abs(tamano.z * s.z));
        float c = calor != null ? calor.Valor : 0f;
        if (largo <= 0f || (Mathf.Abs(largo - largoPuesto) < largoPuesto * 0.02f && Mathf.Abs(c - calorPuesto) < 0.02f)) return;
        largoPuesto = largo;
        calorPuesto = c;
        float aviva = 1f + c;

        var main = llamas.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f * largo * (1f + 0.4f * c), 0.15f * largo * (1f + 0.4f * c));
        var emision = llamas.emission;
        emision.rateOverTime = 110f * aviva;
        var sube = llamas.velocityOverLifetime;
        sube.enabled = true;
        sube.space = ParticleSystemSimulationSpace.World;
        sube.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        sube.y = new ParticleSystem.MinMaxCurve(0.4f * largo * aviva, 0.7f * largo * aviva);
        sube.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var ruido = llamas.noise;
        ruido.strength = 0.08f * largo;

        main = brasas.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f * largo, 0.045f * largo);
        emision = brasas.emission;
        emision.rateOverTime = 14f * aviva;
        sube = brasas.velocityOverLifetime;
        sube.enabled = true;
        sube.space = ParticleSystemSimulationSpace.World;
        sube.x = new ParticleSystem.MinMaxCurve(-0.3f * largo, 0.3f * largo);
        sube.y = new ParticleSystem.MinMaxCurve(0.6f * largo * aviva, 1.5f * largo * aviva);
        sube.z = new ParticleSystem.MinMaxCurve(-0.3f * largo, 0.3f * largo);
        ruido = brasas.noise;
        ruido.strength = 0.4f * largo;
    }
}
