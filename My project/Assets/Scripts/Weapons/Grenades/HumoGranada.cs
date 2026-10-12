using System.Collections.Generic;
using UnityEngine;

// Nube de la granada de humo (US 074). La crea Grenade1.PlayExplosion cuando la granada se activa, en todas las
// computadoras (la de verdad y la de muestra que manda la red), así que no hace falta mandar nada más por la red.
//   CA3: 4 m de radio (effectRadius de la ficha) y 15 s (effectDuration). Tarda CrecerEn en armarse y los últimos
//        DisiparEn (2 s) se va disipando.
//   CA4: no se ve a través de ella desde ninguna dirección:
//        - desde afuera, muchas partículas grandes y opacas que se pisan entre sí (material de humo de War FX);
//        - desde adentro, la pantalla se pone gris (debajo del HUD, como en CS);
//        - el contorno rojo de los rivales (ContornoRival) no se dibuja si la nube está en el medio (TapaVista).
//   CA5: no hace daño (Grenade1 solo daña con la de metralla).
// Con la pausa local (timeScale 0) la nube se congela, igual que la mecha. Al empezar una ronda del Táctico se
// borran todas (RondasTacticas.EmpezarRonda llama a DisiparTodas).
public class HumoGranada : MonoBehaviour
{
    public const float RadioPorDefecto = 4f, DuracionPorDefecto = 15f;
    public const float CrecerEn = 0.8f;   // segundos hasta que la nube llega a su tamaño
    public const float DisiparEn = 2f;    // CA3: los últimos 2 s se va disipando

    private const int Particulas = 70;
    private const float AlturaSobreElPiso = 1.4f; // si cae al piso, la nube se levanta para tapar a una persona parada
    private const float OpacidadQueTapa = 0.35f;  // por debajo de esto (al armarse o al disiparse) ya se ve a través
    private const float RadioQueTapa = 0.9f;       // parte del radio que tapa de verdad (los bordes son más ralos)

    private static readonly List<HumoGranada> activas = new List<HumoGranada>();

    private Vector3 centro;
    private float radio, duracion, tiempo;

    // Cuánto tapa la nube ahora (0 a 1): crece al principio y baja en los últimos DisiparEn segundos.
    public float Opacidad
    {
        get
        {
            float crecer = Mathf.Clamp01(tiempo / CrecerEn);
            float disipar = Mathf.Clamp01((duracion - tiempo) / DisiparEn);
            return Mathf.Min(crecer, disipar);
        }
    }

    // La granada de humo se activó en "lugar": arma la nube con los números de la ficha (radio y duración).
    public static HumoGranada Crear(Vector3 lugar, ShopItem ficha, Material material)
    {
        float radio = ficha != null && ficha.effectRadius > 0f ? ficha.effectRadius : RadioPorDefecto;
        float duracion = ficha != null && ficha.effectDuration > 0f ? ficha.effectDuration : DuracionPorDefecto;
        Color color = ficha != null ? ficha.tint : new Color(0.72f, 0.75f, 0.8f);

        if (material == null)
            Debug.LogWarning("HumoGranada: la granada de humo no tiene material de humo (Grenade1.smokeMaterial en el prefab).");

        var go = new GameObject("Humo de granada");
        HumoGranada humo = go.AddComponent<HumoGranada>();
        humo.radio = radio;
        humo.duracion = duracion;
        humo.centro = Levantar(lugar);
        go.transform.position = humo.centro;
        humo.ArmarParticulas(material, color);
        activas.Add(humo);
        PantallaDeHumo.Asegurar();
        return humo;
    }

    // ¿La nube tapa la línea entre "desde" y "hasta"? Sirve para cualquier cosa que se vea a través del humo
    // (por ejemplo, el contorno de los rivales). Si uno de los dos puntos está adentro de la nube, también tapa.
    public static bool TapaVista(Vector3 desde, Vector3 hasta)
    {
        foreach (HumoGranada humo in activas)
        {
            if (humo == null || humo.Opacidad < OpacidadQueTapa) continue;
            float r = humo.radio * RadioQueTapa;
            if (DistanciaAlSegmento(humo.centro, desde, hasta) <= r) return true;
        }
        return false;
    }

    // Al empezar una ronda nueva no queda humo de la anterior.
    public static void DisiparTodas()
    {
        foreach (HumoGranada humo in activas.ToArray())
            if (humo != null) Destroy(humo.gameObject);
        activas.Clear();
    }

    // Cuánto gris tiene la pantalla de quien mira desde "ojo" (0 afuera, 1 bien adentro de alguna nube).
    public static float GrisEn(Vector3 ojo)
    {
        float gris = 0f;
        foreach (HumoGranada humo in activas)
        {
            if (humo == null) continue;
            float distancia = Vector3.Distance(ojo, humo.centro);
            float adentro = 1f - Mathf.InverseLerp(humo.radio * 0.65f, humo.radio * RadioQueTapa, distancia);
            gris = Mathf.Max(gris, adentro * humo.Opacidad);
        }
        return gris;
    }

    private void Update()
    {
        tiempo += Time.deltaTime; // con la pausa local (timeScale 0) se congela
        if (tiempo >= duracion) Destroy(gameObject);
    }

    private void OnDestroy()
    {
        activas.Remove(this);
    }

    // Si la granada quedó en el piso (o cerca), la nube se levanta para que tape de los pies a la cabeza.
    private static Vector3 Levantar(Vector3 lugar)
    {
        if (Physics.Raycast(lugar + Vector3.up * 0.1f, Vector3.down, out RaycastHit piso, AlturaSobreElPiso + 0.1f,
                ~0, QueryTriggerInteraction.Ignore))
        {
            float alto = piso.point.y + AlturaSobreElPiso;
            if (lugar.y < alto) lugar.y = alto;
        }
        return lugar;
    }

    // Todas las partículas salen juntas y viven lo mismo que la nube: el tamaño (crecer) y la transparencia
    // (disiparse) los manejan las curvas sobre la vida de la partícula.
    private void ArmarParticulas(Material material, Color color)
    {
        ParticleSystem sistema = gameObject.AddComponent<ParticleSystem>();
        sistema.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // arranca andando: se para para configurarlo

        ParticleSystem.MainModule main = sistema.main;
        main.duration = duracion;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = duracion;
        main.startSpeed = 0f;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Particulas;

        ParticleSystem.EmissionModule emision = sistema.emission;
        emision.enabled = false;
        ParticleSystem.ShapeModule forma = sistema.shape;
        forma.enabled = false;

        // Crece de golpe al principio (CrecerEn) y después se queda del mismo tamaño.
        float finCrecer = Mathf.Clamp01(CrecerEn / duracion);
        ParticleSystem.SizeOverLifetimeModule tamano = sistema.sizeOverLifetime;
        tamano.enabled = true;
        tamano.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.3f), new Keyframe(finCrecer, 1f), new Keyframe(1f, 1.08f)));

        // CA3: opaca hasta los últimos DisiparEn segundos y después se va.
        float empiezaDisipar = Mathf.Clamp01((duracion - DisiparEn) / duracion);
        var transparencia = new Gradient();
        transparencia.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, empiezaDisipar), new GradientAlphaKey(0f, 1f) });
        ParticleSystem.ColorOverLifetimeModule colorEnElTiempo = sistema.colorOverLifetime;
        colorEnElTiempo.enabled = true;
        colorEnElTiempo.color = transparencia;

        // Gira despacio, para que no se vea quieta.
        ParticleSystem.RotationOverLifetimeModule giro = sistema.rotationOverLifetime;
        giro.enabled = true;
        giro.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

        ParticleSystemRenderer dibujo = GetComponent<ParticleSystemRenderer>();
        dibujo.renderMode = ParticleSystemRenderMode.Billboard;
        dibujo.sortMode = ParticleSystemSortMode.Distance; // con transparencia, las de atrás se dibujan primero
        dibujo.maxParticleSize = 10f; // sin esto, de cerca se achican (tope de media pantalla) y se ve a través
        dibujo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        dibujo.receiveShadows = false;
        if (material != null) dibujo.sharedMaterial = material;

        sistema.Play();

        var parametros = new ParticleSystem.EmitParams { applyShapeToPosition = false, startLifetime = duracion };
        for (int i = 0; i < Particulas; i++)
        {
            parametros.position = PuntoAdentro();
            parametros.startSize = radio * Random.Range(0.8f, 1.15f);
            parametros.rotation = Random.Range(0f, 360f);
            float tono = Random.Range(0.86f, 1f); // un poco de variación para que no sea un gris parejo
            parametros.startColor = new Color(color.r * tono, color.g * tono, color.b * tono, 1f);
            sistema.Emit(parametros, 1);
        }
    }

    // Lugar al azar dentro de la nube (un poco aplastada), sin pasar al otro lado de una pared o del piso.
    private Vector3 PuntoAdentro()
    {
        Vector3 desvio = Random.insideUnitSphere * radio * 0.7f;
        desvio.y *= 0.7f;
        float largo = desvio.magnitude;
        if (largo < 0.01f) return centro;

        Vector3 direccion = desvio / largo;
        if (Physics.Raycast(centro, direccion, out RaycastHit pared, largo + 0.6f, ~0, QueryTriggerInteraction.Ignore)
            && pared.collider.GetComponentInParent<HealthSystem>() == null)
            largo = Mathf.Max(0f, pared.distance - 0.6f);
        return centro + direccion * largo;
    }

    private static float DistanciaAlSegmento(Vector3 punto, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float largo2 = ab.sqrMagnitude;
        float t = largo2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(punto - a, ab) / largo2) : 0f;
        return Vector3.Distance(punto, a + ab * t);
    }

    // Capa gris sobre la vista (debajo del HUD) mientras la cámara está adentro de una nube: así no se ve hacia afuera
    // aunque entre las partículas quede algún hueco. Una sola para todas las nubes; la crea la primera.
    private class PantallaDeHumo : MonoBehaviour
    {
        private const int Orden = 38; // debajo de la mira telescópica (39) y del HUD de combate (40)
        private static readonly Color Gris = new Color(0.62f, 0.64f, 0.68f); // el gris de la nube vista de cerca
        private static PantallaDeHumo instancia;

        private Canvas canvas;
        private UnityEngine.UI.Image gris;

        public static void Asegurar()
        {
            if (instancia != null) return;
            var go = new GameObject("Humo (pantalla gris)");
            instancia = go.AddComponent<PantallaDeHumo>();
            instancia.Armar();
        }

        private void Armar()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = Orden;
            var rect = new GameObject("Gris", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            gris = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            gris.raycastTarget = false;
            gris.color = Color.clear;
            canvas.enabled = false;
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            float cuanto = cam != null ? GrisEn(cam.transform.position) : 0f;
            canvas.enabled = cuanto > 0f;
            if (!canvas.enabled) return;
            gris.color = new Color(Gris.r, Gris.g, Gris.b, cuanto);
        }

        private void OnDestroy()
        {
            if (instancia == this) instancia = null;
        }
    }
}
