using UnityEngine;
using UnityEngine.Rendering;

// US 267, CA3 y CA4: cómo se ve la bola de veneno del Jefe, hecha con partículas (sin assets):
// - En vuelo (Bola): un halo verde que brilla y late alrededor de la bola, una estela de gotas que caen y un humo verde
//   que queda en el aire, así se ve venir de lejos.
// - Al tocar el piso o al jugador (Salpicar): un fogonazo verde, gotas que saltan y caen, y un anillo de vapor que se
//   abre por el piso (después se arma la nube o el charco).
// Las partículas usan Efectos/ParticulaBrillante: redondas y suaves, y las que suman luz pasan el umbral del bloom.
public static class EfectoDeVeneno
{
    private static readonly Color Verde = new Color(0.45f, 1f, 0.2f);
    private static Material brillo, humo;

    private static Material Brillo()
    {
        if (brillo == null) brillo = Crear("Veneno (brillo)", Verde * 2.6f, BlendMode.One, BlendMode.One, 0.7f);
        return brillo;
    }

    private static Material Humo()
    {
        if (humo == null) humo = Crear("Veneno (humo)", new Color(0.3f, 0.7f, 0.12f, 1f), BlendMode.One, BlendMode.OneMinusSrcAlpha, 0.9f);
        return humo;
    }

    private static Material Crear(string nombre, Color color, BlendMode origen, BlendMode destino, float suave)
    {
        Shader shader = Resources.Load<Shader>("Efectos/ParticulaBrillante");
        if (shader == null) shader = Shader.Find("Efectos/ParticulaBrillante");
        if (shader == null) return Veneno.Material(new Color(Verde.r, Verde.g, Verde.b, 0.8f)); // sin el shader, algo se ve
        var m = new Material(shader) { name = nombre };
        m.SetColor("_Color", color);
        m.SetFloat("_Suave", suave);
        m.SetFloat("_SrcBlend", (float)origen);
        m.SetFloat("_DstBlend", (float)destino);
        return m;
    }

    /// <summary>Le pone a la bola su halo y su estela.</summary>
    public static void Bola(GameObject bola, float tam)
    {
        // Halo: unas pocas partículas grandes pegadas a la bola, que laten.
        ParticleSystem halo = Sistema("Halo", bola.transform, Brillo(), false);
        var m = halo.main;
        m.startLifetime = 0.35f;
        m.startSpeed = 0f;
        m.startSize = new ParticleSystem.MinMaxCurve(tam * 2.2f, tam * 3f);
        m.startColor = new Color(1f, 1f, 1f, 0.55f);
        m.simulationSpace = ParticleSystemSimulationSpace.Local;
        m.maxParticles = 20;
        var e = halo.emission;
        e.rateOverTime = 30f;
        var t = halo.sizeOverLifetime;
        t.enabled = true;
        t.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.7f)));
        Apagar(halo);
        halo.Play();

        // Gotas: caen de la bola mientras vuela.
        ParticleSystem gotas = Sistema("Gotas", bola.transform, Brillo(), true);
        m = gotas.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1f);
        m.startSize = new ParticleSystem.MinMaxCurve(tam * 0.15f, tam * 0.35f);
        m.gravityModifier = 1f;
        m.maxParticles = 80;
        e = gotas.emission;
        e.rateOverTime = 45f;
        var forma = gotas.shape;
        forma.shapeType = ParticleSystemShapeType.Sphere;
        forma.radius = tam * 0.4f;
        Apagar(gotas);
        gotas.Play();

        // Humo: queda atrás en el aire y se agranda.
        ParticleSystem estela = Sistema("Humo", bola.transform, Humo(), true);
        m = estela.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.3f);
        m.startSize = new ParticleSystem.MinMaxCurve(tam * 0.8f, tam * 1.3f);
        m.startColor = new Color(1f, 1f, 1f, 0.5f);
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        m.maxParticles = 60;
        e = estela.emission;
        e.rateOverTime = 35f;
        forma = estela.shape;
        forma.shapeType = ParticleSystemShapeType.Sphere;
        forma.radius = tam * 0.3f;
        t = estela.sizeOverLifetime;
        t.enabled = true;
        t.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 2f));
        Apagar(estela);
        estela.Play();
    }

    /// <summary>Suelta la estela de la bola (antes de borrarla): deja de salir y lo que ya salió se apaga solo.</summary>
    public static void Soltar(GameObject bola)
    {
        if (bola == null) return;
        foreach (ParticleSystem ps in bola.GetComponentsInChildren<ParticleSystem>())
        {
            if (ps.main.simulationSpace != ParticleSystemSimulationSpace.World) continue; // el halo se va con la bola
            ps.transform.SetParent(null, true);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Object.Destroy(ps.gameObject, 1.5f);
        }
    }

    /// <summary>La bola revienta en ese lugar.</summary>
    public static void Salpicar(Vector3 lugar)
    {
        var go = new GameObject("Veneno (salpicón)");
        go.transform.position = lugar + Vector3.up * 0.1f;

        // Fogonazo: una partícula grande que se abre y se apaga rápido.
        ParticleSystem flash = Sistema("Fogonazo", go.transform, Brillo(), true);
        var m = flash.main;
        m.startLifetime = 0.25f;
        m.startSpeed = 0f;
        m.startSize = 2.6f;
        m.startColor = new Color(1f, 1f, 1f, 0.9f);
        Rafaga(flash, 1);
        var t = flash.sizeOverLifetime;
        t.enabled = true;
        t.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.2f));
        Apagar(flash);

        // Gotas que saltan hacia arriba y afuera y caen.
        ParticleSystem gotas = Sistema("Gotas", go.transform, Brillo(), true);
        m = gotas.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6.5f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        m.gravityModifier = 1.6f;
        Rafaga(gotas, 30);
        var forma = gotas.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 55f;
        forma.radius = 0.2f;
        gotas.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // el cono, para arriba
        Apagar(gotas);

        // Anillo de vapor que se abre por el piso.
        ParticleSystem vapor = Sistema("Vapor", go.transform, Humo(), true);
        m = vapor.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 2.8f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        m.startColor = new Color(1f, 1f, 1f, 0.6f);
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        Rafaga(vapor, 16);
        forma = vapor.shape;
        forma.shapeType = ParticleSystemShapeType.Circle;
        forma.radius = 0.3f;
        vapor.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // el círculo, acostado sobre el piso
        var frena = vapor.limitVelocityOverLifetime;
        frena.enabled = true;
        frena.limit = 0.4f;
        frena.dampen = 0.15f;
        t = vapor.sizeOverLifetime;
        t.enabled = true;
        t.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 2.2f));
        Apagar(vapor);

        foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>()) ps.Play();
        Object.Destroy(go, 2f);
    }

    private static ParticleSystem Sistema(string nombre, Transform padre, Material material, bool mundo)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main;
        m.loop = true;
        m.playOnAwake = false;
        m.simulationSpace = mundo ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
        m.startColor = Color.white;
        var forma = ps.shape;
        forma.enabled = true;
        forma.shapeType = ParticleSystemShapeType.Sphere;
        forma.radius = 0.01f;
        var render = go.GetComponent<ParticleSystemRenderer>();
        render.sharedMaterial = material;
        render.shadowCastingMode = ShadowCastingMode.Off;
        render.receiveShadows = false;
        return ps;
    }

    private static void Rafaga(ParticleSystem ps, int cuantas)
    {
        var m = ps.main;
        m.loop = false;
        m.duration = 0.1f;
        m.maxParticles = cuantas;
        var e = ps.emission;
        e.rateOverTime = 0f;
        e.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)cuantas) });
    }

    // Se van apagando: transparentes al final de su vida.
    private static void Apagar(ParticleSystem ps)
    {
        var c = ps.colorOverLifetime;
        c.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
        c.color = g;
    }
}
