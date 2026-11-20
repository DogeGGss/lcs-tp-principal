using UnityEngine;

// F35 (Skins de armas), 19/11/2026: lo que las skins del cuchillo agregan al modelo, además de su material:
// - Dónde está la hoja: el shader de sus legendarias (Art/Skins/SkinCuchillo) distingue la hoja del mango por el largo,
//   así que a cada pieza con malla se le pasa el eje del cuchillo hacia la punta y su largo (_Eje, por la caja de la malla:
//   la punta es la punta más lejos del origen, que está en la empuñadura).
// - Sus partículas (Diseno.particulas), que salen de toda el arma (las brasas del Dragón Abisal) o de la hoja: puntitos de
//   luz (Art/Skins/EfectoBrasa) del color de la skin. Quedan en el mundo (al mover el arma dejan una estela) pero heredan
//   su movimiento (caminando no se quedan atrás). El tamaño se toma del arma tal como se ve (en primera persona, en la
//   mano de los demás o en la vista previa del perfil, que la agranda); al cortar (CalorDeSkin) salen más, y algunas
//   largan una tanda de golpe (las chispas, las esquirlas de hielo).
// SkinDeArma lo pone o lo saca con la skin; las partículas van en un hijo de la malla más grande del arma.
public class CuchilloDeSkin : MonoBehaviour
{
    public const string Nombre = "Particulas (skin)";
    private const float EmpiezaLaHoja = 0.34f; // (parte del largo, desde la empuñadura; como _Hoja del shader)

    private static readonly int Eje = Shader.PropertyToID("_Eje");

    private ParticleSystem ps;
    private CatalogoDeSkins.Particulas tipo;
    private Transform malla;
    private Vector3 tamano; // de la caja de las partículas, en el espacio de la malla
    private float largoDelArma; // en el espacio de la malla
    private float largoPuesto = -1f, calorPuesto = -1f, calorAntes;
    private CalorDeSkin calor;

    /// <summary>Le pone al arma lo de la skin (o se lo saca, si la skin no tiene; también saca las partículas de una
    /// copia de otra arma, que vienen sin script).</summary>
    public static void Poner(GameObject arma, CatalogoDeSkins.Skin skin)
    {
        if (arma == null) return;
        foreach (Transform t in arma.GetComponentsInChildren<Transform>(true))
            if (t != null && (t.name == Nombre || t.name == "Brasas (skin)"))
            {
                t.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(t.gameObject);
                else DestroyImmediate(t.gameObject);
            }
        if (skin == null || !skin.arma.cuerpoACuerpo || skin.diseno == null) return;
        foreach (MeshFilter f in arma.GetComponentsInChildren<MeshFilter>(true)) MarcarLaHoja(f);
        if (skin.diseno.particulas == CatalogoDeSkins.Particulas.Ninguna) return;
        MeshFilter filtro = MallaMasGrande(arma);
        if (filtro == null) return;
        var go = new GameObject(Nombre) { layer = filtro.gameObject.layer };
        go.transform.SetParent(filtro.transform, false);
        go.AddComponent<CuchilloDeSkin>().Armar(filtro, skin.diseno);
    }

    // El eje del cuchillo hacia la punta y su largo, para el shader (en un bloque de propiedades de esa pieza).
    private static void MarcarLaHoja(MeshFilter f)
    {
        Renderer r = f.GetComponent<Renderer>();
        if (r == null || f.sharedMesh == null) return;
        Medir(f.sharedMesh.bounds, out Vector3 eje, out float largo);
        var bloque = new MaterialPropertyBlock();
        r.GetPropertyBlock(bloque);
        bloque.SetVector(Eje, new Vector4(eje.x, eje.y, eje.z, largo));
        r.SetPropertyBlock(bloque);
    }

    // El eje más largo de la caja, hacia el lado más lejos del origen (la punta), y cuánto mide del origen a la punta.
    private static void Medir(Bounds b, out Vector3 eje, out float largo)
    {
        int a = b.size.x >= b.size.y && b.size.x >= b.size.z ? 0 : b.size.y >= b.size.z ? 1 : 2;
        float centro = b.center[a];
        float signo = centro < 0f ? -1f : 1f;
        eje = Vector3.zero;
        eje[a] = signo;
        largo = Mathf.Max(0.0001f, Mathf.Abs(centro) + b.extents[a]);
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

    private void Armar(MeshFilter filtro, CatalogoDeSkins.Diseno diseno)
    {
        malla = filtro.transform;
        tipo = diseno.particulas;
        Bounds b = filtro.sharedMesh.bounds;
        Medir(b, out Vector3 eje, out largoDelArma);
        // De dónde salen: las brasas, de todo el cuchillo; lo demás, de la hoja.
        if (tipo == CatalogoDeSkins.Particulas.Brasas)
        {
            transform.localPosition = b.center;
            tamano = b.size * 0.85f;
        }
        else
        {
            Vector3 lado = Vector3.Scale(b.size, Vector3.one - new Vector3(Mathf.Abs(eje.x), Mathf.Abs(eje.y), Mathf.Abs(eje.z))) * 0.6f;
            float desde = largoDelArma * EmpiezaLaHoja, hasta = largoDelArma * 0.97f;
            Vector3 centro = b.center;
            int a = eje.x != 0f ? 0 : eje.y != 0f ? 1 : 2;
            centro[a] = (eje[a] * (desde + hasta) * 0.5f);
            transform.localPosition = centro;
            tamano = lado + new Vector3(Mathf.Abs(eje.x), Mathf.Abs(eje.y), Mathf.Abs(eje.z)) * (hasta - desde);
        }

        Color color = diseno.muestra;
        Color claro = Color.Lerp(color, Color.white, 0.6f);
        Color a1 = color, a2 = claro;
        if (tipo == CatalogoDeSkins.Particulas.Estrellas || tipo == CatalogoDeSkins.Particulas.Escarcha) { a1 = claro; a2 = Color.white; }
        if (tipo == CatalogoDeSkins.Particulas.Gotas) { a1 = color * 0.7f; a1.a = 1f; a2 = color; }

        ps = gameObject.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startSpeed = 0f;
        main.startColor = new ParticleSystem.MinMaxGradient(a1, a2);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape; // la caja, del tamaño del arma; lo demás se mide en Medir
        main.maxParticles = 60;
        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Box;
        forma.scale = tamano;
        var hereda = ps.inheritVelocity; // caminando no se quedan atrás
        hereda.enabled = true;
        hereda.mode = ParticleSystemInheritVelocityMode.Current;
        hereda.curve = 0.85f;
        var col = ps.colorOverLifetime; // aparecen, brillan y se apagan (las estrellas titilan)
        col.enabled = true;
        var g = new Gradient();
        if (tipo == CatalogoDeSkins.Particulas.Estrellas)
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.3f, 0.4f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        else
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, tipo == CatalogoDeSkins.Particulas.Gotas ? 0.8f : 0.3f)));
        var ruido = ps.noise; // que bailen un poco
        ruido.enabled = tipo != CatalogoDeSkins.Particulas.Gotas;
        ruido.frequency = 2.5f;
        ruido.scrollSpeed = 1.5f;
        var r = GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = ModelosDeArmas.Actual.MaterialDe("efecto-brasa");
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        MedirParticulas();
        ps.Play();
    }

    private void Update()
    {
        if (calor == null) calor = GetComponentInParent<CalorDeSkin>();
        float c = calor != null ? calor.Valor : 0f;
        if (c > calorAntes + 0.1f) Tanda(); // cortó
        calorAntes = c;
        MedirParticulas();
    }

    private float Largo
    {
        get
        {
            if (malla == null) return 0f;
            Vector3 s = malla.lossyScale;
            return largoDelArma * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        }
    }

    // Al cortar: las chispas y las esquirlas saltan de golpe para todos lados; las demás, unas cuantas más.
    private void Tanda()
    {
        if (ps == null) return;
        float largo = Largo;
        bool saltan = tipo == CatalogoDeSkins.Particulas.Chispas || tipo == CatalogoDeSkins.Particulas.Escarcha;
        int cuantas = saltan ? 12 : 6;
        for (int i = 0; i < cuantas; i++)
        {
            var e = new ParticleSystem.EmitParams();
            if (saltan)
            {
                e.velocity = Random.onUnitSphere * largo * Random.Range(1.5f, 3f);
                e.startLifetime = Random.Range(0.2f, 0.4f);
            }
            ps.Emit(e, 1);
        }
    }

    // Los tamaños y las velocidades, del largo del arma tal como se ve (en metros), y cuántas salen según su calor.
    private void MedirParticulas()
    {
        if (malla == null || ps == null) return;
        float largo = Largo;
        float c = calor != null ? calor.Valor : 0f;
        if (largo <= 0f || (Mathf.Abs(largo - largoPuesto) < largoPuesto * 0.02f && Mathf.Abs(c - calorPuesto) < 0.02f)) return;
        largoPuesto = largo;
        calorPuesto = c;
        float aviva = 1f + 2f * c;
        var main = ps.main;
        var emision = ps.emission;
        var mueve = ps.velocityOverLifetime;
        mueve.enabled = true;
        mueve.space = ParticleSystemSimulationSpace.World;
        var ruido = ps.noise;
        main.gravityModifier = 0f;
        switch (tipo)
        {
            case CatalogoDeSkins.Particulas.Brasas: // suben bailando
                Poner(main, emision, mueve, 0.5f, 1.1f, 0.015f, 0.035f, 12f * aviva, largo, new Vector2(-0.3f, 0.3f), new Vector2(0.4f, 1.2f));
                ruido.strength = 0.3f * largo;
                break;
            case CatalogoDeSkins.Particulas.Chispas: // chispitas rápidas para cualquier lado
                Poner(main, emision, mueve, 0.15f, 0.35f, 0.01f, 0.02f, 14f * aviva, largo, new Vector2(-1.2f, 1.2f), new Vector2(-1.2f, 1.2f));
                ruido.strength = 0.1f * largo;
                break;
            case CatalogoDeSkins.Particulas.Estrellas: // flotan despacio y titilan
                Poner(main, emision, mueve, 1f, 2f, 0.012f, 0.03f, 7f * aviva, largo, new Vector2(-0.1f, 0.1f), new Vector2(-0.1f, 0.1f));
                ruido.strength = 0.15f * largo;
                break;
            case CatalogoDeSkins.Particulas.Escarcha: // el frío baja
                Poner(main, emision, mueve, 0.8f, 1.5f, 0.012f, 0.03f, 10f * aviva, largo, new Vector2(-0.1f, 0.1f), new Vector2(-0.45f, -0.15f));
                ruido.strength = 0.1f * largo;
                break;
            case CatalogoDeSkins.Particulas.Gotas: // caen
                Poner(main, emision, mueve, 0.5f, 0.9f, 0.015f, 0.03f, 5f * aviva, largo, Vector2.zero, Vector2.zero);
                main.gravityModifier = 0.8f * largo;
                break;
            default: // píxeles: suben despacio
                Poner(main, emision, mueve, 0.6f, 1.2f, 0.01f, 0.02f, 10f * aviva, largo, new Vector2(-0.05f, 0.05f), new Vector2(0.1f, 0.3f));
                ruido.strength = 0.05f * largo;
                break;
        }
    }

    private static void Poner(ParticleSystem.MainModule main, ParticleSystem.EmissionModule emision, ParticleSystem.VelocityOverLifetimeModule mueve,
        float vidaMin, float vidaMax, float tamMin, float tamMax, float porSegundo, float largo, Vector2 lados, Vector2 arriba)
    {
        main.startLifetime = new ParticleSystem.MinMaxCurve(vidaMin, vidaMax);
        main.startSize = new ParticleSystem.MinMaxCurve(tamMin * largo, tamMax * largo);
        emision.rateOverTime = porSegundo;
        mueve.x = new ParticleSystem.MinMaxCurve(lados.x * largo, lados.y * largo);
        mueve.y = new ParticleSystem.MinMaxCurve(arriba.x * largo, arriba.y * largo);
        mueve.z = new ParticleSystem.MinMaxCurve(lados.x * largo, lados.y * largo);
    }
}
