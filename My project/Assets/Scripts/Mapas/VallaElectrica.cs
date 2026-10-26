using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// Valla eléctrica del borde del Mapa 1 (US 240, CA6). Va en el objeto que agrupa las vallas ("Vallas (bordes)"): todo
// lo que tiene adentro (las paredes invisibles, los postes, los cables y los carteles) electrocuta.
// - El jugador que la toca recibe 5 de daño, como mucho una vez por segundo mientras la siga tocando.
// - Saltan chispas y rayos celestes donde la tocó, con un chispazo de luz y un zumbido eléctrico; quien la tocó ve
//   además un destello celeste en la pantalla. Los demás jugadores ven y escuchan las chispas (JugadorEnRed).
// Lo detecta el jugador de esta computadora al chocar con la valla (PlayerMovement.OnControllerColliderHit). Las
// chispas, los rayos y el sonido se arman por código: no hace falta ningún asset.
public class VallaElectrica : MonoBehaviour
{
    public const int Danio = 5;
    public const string Causa = "Valla eléctrica";
    private const float Espera = 1f;           // s entre un daño y el siguiente
    private static readonly Color Celeste = new Color(0.45f, 0.85f, 1f);

    private static float proximo;
    private static Material material;
    private static AudioClip zumbido;

    /// <summary>El jugador de esta computadora chocó con algo: si es la valla, lo electrocuta.</summary>
    public static void Tocar(Transform jugador, Collider tocado, Vector3 punto)
    {
        if (tocado == null || tocado.GetComponentInParent<VallaElectrica>() == null) return;
        if (Time.time < proximo) return;
        HealthSystem vida = jugador.GetComponent<HealthSystem>();
        if (vida == null || vida.currentHealth <= 0) return;
        proximo = Time.time + Espera;

        JugadorEnRed enRed = jugador.GetComponent<JugadorEnRed>();
        if (enRed != null && PartidaEnRed.Actual != null)
        {
            if (!enRed.Electrocutar(Danio, punto)) return; // avisa a los demás y se saca la vida
        }
        else
        {
            HealthSystem.DamageSource = Causa;
            HealthSystem.DamageOrigin = punto;
            vida.TakeDamage(Danio);
            HealthSystem.DamageSource = null;
        }
        Efecto(punto, jugador, true);
    }

    /// <summary>Chispas, rayos, luz y zumbido donde alguien tocó la valla. propio: es el jugador de esta computadora.</summary>
    public static void Efecto(Vector3 punto, Transform jugador, bool propio)
    {
        var go = new GameObject("Chispazo");
        go.transform.position = punto;
        go.AddComponent<VallaElectrica.Chispazo>().Armar(propio, Grupo(jugador));
        if (propio) Destello.Mostrar();
    }

    private static AudioMixerGroup Grupo(Transform jugador)
    {
        if (jugador == null) return null;
        Pistola pistola = jugador.GetComponentInChildren<Pistola>(true);
        return pistola != null ? pistola.sfxGroup : null;
    }

    private static Material Material()
    {
        if (material != null) return material;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        Color color = Celeste * 5f; // más fuerte que 1: con el Bloom brilla
        material = new Material(shader) { name = "Chispas (valla)", color = color };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        return material;
    }

    // Zumbido eléctrico de 0,4 s: ruido cortado por un zumbido de 110 Hz que se apaga rápido.
    private static AudioClip Zumbido()
    {
        if (zumbido != null) return zumbido;
        const int frecuencia = 44100;
        int muestras = (int)(frecuencia * 0.4f);
        var datos = new float[muestras];
        var azar = new System.Random(7);
        float ruido = 0f;
        for (int i = 0; i < muestras; i++)
        {
            float t = i / (float)frecuencia;
            ruido = Mathf.Lerp(ruido, (float)(azar.NextDouble() * 2 - 1), 0.6f);
            float zum = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 110f * t)) * 0.5f + Mathf.Sin(2f * Mathf.PI * 220f * t) * 0.3f;
            float chasquido = azar.NextDouble() < 0.002 ? 1f : 0f;
            float envolvente = Mathf.Exp(-t * 9f) * Mathf.Clamp01(t * 400f);
            datos[i] = Mathf.Clamp((ruido * 0.6f * (0.5f + 0.5f * zum) + chasquido) * envolvente, -1f, 1f) * 0.8f;
        }
        zumbido = AudioClip.Create("Zumbido valla", muestras, 1, frecuencia, false);
        zumbido.SetData(datos, 0);
        return zumbido;
    }

    // ---------- El chispazo: chispas, rayos y luz que duran un instante ----------

    private class Chispazo : MonoBehaviour
    {
        private const float Duracion = 0.35f;
        private readonly List<LineRenderer> rayos = new List<LineRenderer>();
        private Light luz;
        private float inicio, proximoRayo;

        public void Armar(bool propio, AudioMixerGroup grupo)
        {
            inicio = Time.time;

            // Chispas: puntitos estirados que saltan para todos lados y caen.
            var chispas = gameObject.AddComponent<ParticleSystem>();
            chispas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var principal = chispas.main;
            principal.duration = 0.2f;
            principal.loop = false;
            principal.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            principal.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 7f);
            principal.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            principal.gravityModifier = 1.2f;
            principal.simulationSpace = ParticleSystemSimulationSpace.World;
            principal.maxParticles = 80;
            var emision = chispas.emission;
            emision.rateOverTime = 0f;
            emision.SetBursts(new[] { new ParticleSystem.Burst(0f, 35), new ParticleSystem.Burst(0.12f, 15) });
            var forma = chispas.shape;
            forma.shapeType = ParticleSystemShapeType.Sphere;
            forma.radius = 0.05f;
            var render = GetComponent<ParticleSystemRenderer>();
            render.renderMode = ParticleSystemRenderMode.Stretch;
            render.velocityScale = 0.04f;
            render.lengthScale = 1f;
            render.sharedMaterial = Material();
            render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            chispas.Play();

            // Rayos: 3 líneas quebradas que salen del punto y cambian de forma varias veces.
            for (int i = 0; i < 3; i++)
            {
                var hijo = new GameObject("Rayo");
                hijo.transform.SetParent(transform, false);
                var linea = hijo.AddComponent<LineRenderer>();
                linea.useWorldSpace = true;
                linea.positionCount = 7;
                linea.widthCurve = new AnimationCurve(new Keyframe(0f, 0.035f), new Keyframe(1f, 0.005f));
                linea.sharedMaterial = Material();
                linea.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rayos.Add(linea);
            }
            Rayos();

            var l = new GameObject("Luz");
            l.transform.SetParent(transform, false);
            luz = l.AddComponent<Light>();
            luz.type = LightType.Point;
            luz.color = Celeste;
            luz.range = 4f;
            luz.intensity = 6f;
            luz.shadows = LightShadows.None;

            var fuente = gameObject.AddComponent<AudioSource>();
            fuente.clip = Zumbido();
            fuente.outputAudioMixerGroup = grupo;
            fuente.spatialBlend = propio ? 0.5f : 1f;
            fuente.minDistance = 2f;
            fuente.maxDistance = 35f;
            fuente.rolloffMode = AudioRolloffMode.Linear;
            fuente.pitch = Random.Range(0.9f, 1.1f);
            fuente.volume = propio ? 0.9f : 0.75f;
            fuente.Play();

            Destroy(gameObject, 1f);
        }

        private void Rayos()
        {
            proximoRayo = Time.time + 0.05f;
            Vector3 centro = transform.position;
            foreach (LineRenderer linea in rayos)
            {
                Vector3 hacia = Random.onUnitSphere * Random.Range(0.4f, 0.9f);
                for (int i = 0; i < linea.positionCount; i++)
                {
                    float f = i / (float)(linea.positionCount - 1);
                    Vector3 p = centro + hacia * f;
                    if (i > 0) p += Random.insideUnitSphere * 0.12f * (1f - f * 0.5f);
                    linea.SetPosition(i, p);
                }
            }
        }

        private void Update()
        {
            float t = (Time.time - inicio) / Duracion;
            bool visibles = t < 1f;
            foreach (LineRenderer linea in rayos) linea.enabled = visibles && Random.value > 0.25f; // titilan
            if (visibles && Time.time >= proximoRayo) Rayos();
            if (luz != null) luz.intensity = visibles ? 6f * (1f - t) * Random.Range(0.6f, 1f) : 0f;
        }
    }

    // ---------- Destello celeste en la pantalla de quien la tocó ----------

    private class Destello : MonoBehaviour
    {
        private const float Pico = 0.35f, Baja = 0.3f;
        private static Destello instancia;
        private UnityEngine.UI.Image velo;
        private Coroutine actual;

        public static void Mostrar()
        {
            if (instancia == null)
            {
                var go = new GameObject("Destello valla");
                DontDestroyOnLoad(go);
                var lienzo = go.AddComponent<Canvas>();
                lienzo.renderMode = RenderMode.ScreenSpaceOverlay;
                lienzo.sortingOrder = 900;
                instancia = go.AddComponent<Destello>();
                var imagen = new GameObject("Velo").AddComponent<UnityEngine.UI.Image>();
                imagen.transform.SetParent(go.transform, false);
                imagen.raycastTarget = false;
                RectTransform r = imagen.rectTransform;
                r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
                instancia.velo = imagen;
            }
            if (instancia.actual != null) instancia.StopCoroutine(instancia.actual);
            instancia.actual = instancia.StartCoroutine(instancia.Bajar());
        }

        private IEnumerator Bajar()
        {
            for (float t = 0f; t < Baja; t += Time.unscaledDeltaTime)
            {
                float a = Pico * (1f - t / Baja) * (Random.value > 0.3f ? 1f : 0.5f); // parpadea
                velo.color = new Color(Celeste.r, Celeste.g, Celeste.b, a);
                yield return null;
            }
            velo.color = Color.clear;
            actual = null;
        }
    }
}
