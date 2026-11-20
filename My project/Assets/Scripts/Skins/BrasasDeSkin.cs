using UnityEngine;

// F35 (Skins de armas), 19/11/2026: las brasitas que se le salen a un arma (las skins con Diseno.brasitas: el cuchillo
// Dragón Abisal). Puntitos de fuego (Art/Skins/EfectoBrasa) del color de la skin que saltan de toda el arma, suben un
// poco y se apagan; quedan en el mundo (al mover el arma dejan una estela), pero heredan su movimiento (caminando no se
// quedan atrás). El tamaño se toma del arma tal como se ve (en primera persona, en la mano de los demás o en la vista
// previa del perfil, que la agranda), y al cortar salen más (CalorDeSkin).
// SkinDeArma las pone o las saca con la skin; van en un hijo de la malla más grande del arma.
public class BrasasDeSkin : MonoBehaviour
{
    public const string Nombre = "Brasas (skin)";

    private ParticleSystem brasas;
    private Transform malla;
    private Vector3 tamano; // de la malla, en su espacio
    private float largoPuesto = -1f, calorPuesto = -1f;
    private CalorDeSkin calor;

    /// <summary>Las pone o las saca según la skin (también saca las de una copia de otra arma, que vienen sin script).</summary>
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
        if (skin?.diseno == null || !skin.diseno.brasitas) return;
        MeshFilter filtro = MallaMasGrande(arma);
        if (filtro == null) return;
        var go = new GameObject(Nombre) { layer = filtro.gameObject.layer };
        go.transform.SetParent(filtro.transform, false);
        Bounds b = filtro.sharedMesh.bounds;
        go.transform.localPosition = b.center;
        go.AddComponent<BrasasDeSkin>().Armar(filtro.transform, b.size, skin.diseno.muestra);
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

    private void Armar(Transform deLaMalla, Vector3 tamanoDeLaMalla, Color fuego)
    {
        malla = deLaMalla;
        tamano = tamanoDeLaMalla;
        brasas = gameObject.AddComponent<ParticleSystem>();
        brasas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = brasas.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startSpeed = 0f;
        main.startColor = new ParticleSystem.MinMaxGradient(fuego, Color.Lerp(fuego, Color.white, 0.6f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape; // la caja, del tamaño del arma; lo demás se mide en Medir
        main.maxParticles = 40;
        var forma = brasas.shape;
        forma.shapeType = ParticleSystemShapeType.Box;
        forma.scale = tamano * 0.85f;
        var hereda = brasas.inheritVelocity; // caminando no se quedan atrás
        hereda.enabled = true;
        hereda.mode = ParticleSystemInheritVelocityMode.Current;
        hereda.curve = 0.85f;
        var color = brasas.colorOverLifetime; // se prenden rápido y se van apagando
        color.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
        color.color = g;
        var tam = brasas.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.3f)));
        var ruido = brasas.noise; // que bailen un poco
        ruido.enabled = true;
        ruido.frequency = 2.5f;
        ruido.scrollSpeed = 1.5f;
        var r = GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = ModelosDeArmas.Actual.MaterialDe("efecto-brasa");
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        Medir();
        brasas.Play();
    }

    private void Update()
    {
        if (calor == null) calor = GetComponentInParent<CalorDeSkin>();
        Medir();
    }

    // Los tamaños y las velocidades, del largo del arma tal como se ve (en metros), y cuántas salen según su calor.
    private void Medir()
    {
        if (malla == null || brasas == null) return;
        Vector3 s = malla.lossyScale;
        float largo = Mathf.Max(Mathf.Abs(tamano.x * s.x), Mathf.Abs(tamano.y * s.y), Mathf.Abs(tamano.z * s.z));
        float c = calor != null ? calor.Valor : 0f;
        if (largo <= 0f || (Mathf.Abs(largo - largoPuesto) < largoPuesto * 0.02f && Mathf.Abs(c - calorPuesto) < 0.02f)) return;
        largoPuesto = largo;
        calorPuesto = c;
        float aviva = 1f + 2f * c;
        var main = brasas.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f * largo, 0.035f * largo);
        var emision = brasas.emission;
        emision.rateOverTime = 12f * aviva;
        var sube = brasas.velocityOverLifetime;
        sube.enabled = true;
        sube.space = ParticleSystemSimulationSpace.World;
        sube.x = new ParticleSystem.MinMaxCurve(-0.3f * largo, 0.3f * largo);
        sube.y = new ParticleSystem.MinMaxCurve(0.4f * largo, 1.2f * largo);
        sube.z = new ParticleSystem.MinMaxCurve(-0.3f * largo, 0.3f * largo);
        var ruido = brasas.noise;
        ruido.strength = 0.3f * largo;
    }
}
