using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

// Granada flash (US 075): cuando explota, enceguece al jugador de esta computadora según hacia dónde mira.
//   CA3: con la flash delante de la vista (a menos de 60° de donde mira) y a menos de 15 m, la pantalla queda en
//        blanco effectDuration (2,5 s) y tarda recovery (1 s) en volver.
//   CA4: de costado (entre 60° y 110°), la mitad: 1,25 s en blanco y 0,5 s para volver.
//   CA5: de espaldas (más de 110°), la pantalla solo se aclara y se va en 0,5 s.
//   CA6: una pared en el medio o más de 15 m: nada. Los personajes no tapan. Le pasa también a quien la tiró y a
//        sus compañeros, porque no se mira de quién es.
//   Si lo enceguece (de frente o de costado) le suena el pitido de oído mientras dura, y un poco más.
// Cada computadora lo calcula para su propio jugador (su cámara) cuando la flash explota ahí, sea la de verdad o
// la de muestra que manda la red (Grenade1.PlayExplosion). Lo agrega solo la primera flash: no va en la escena.
public class FlashBlind : MonoBehaviour
{
    public const float DefaultRange = 15f;
    public const float FrontAngle = 60f, SideAngle = 110f; // grados entre donde mira y la flash
    private const float BackFade = 0.5f, BackPeak = 0.55f; // de espaldas: se aclara a la mitad y se va en 0,5 s
    private const float LightRange = 12f, LightIntensity = 12f, LightTime = 0.25f;
    // Pitido: sigue RingAfter segundos después de recuperar la vista y se apaga en los últimos RingFade.
    private const float RingAfter = 1f, RingFade = 1.5f, RingVolume = 0.6f;

    private static FlashBlind instance;

    private Canvas canvas;
    private UnityEngine.UI.Image white;
    private float peak, fullUntil, fadeUntil;
    private AudioSource ring;
    private float ringUntil;

    // Blanco que tiene la pantalla ahora (0 a 1).
    public static float Amount => instance != null ? instance.Current(Time.time) : 0f;

    // La flash explotó en "center": se ve el destello y, si corresponde, enceguece a quien mira por Camera.main.
    // Si lo enceguece (de frente o de costado), le suena el pitido de oído (ringSound) por el grupo del mezclador.
    public static void Apply(Vector3 center, ShopItem item, AudioClip ringSound = null, AudioMixerGroup group = null)
    {
        FlashBlind blind = Instance();
        blind.StartCoroutine(blind.Destello(center));

        Camera cam = Camera.main;
        if (cam == null) return;
        Transform eye = cam.transform;
        Vector3 toFlash = center - eye.position;
        float range = item != null && item.effectRadius > 0f ? item.effectRadius : DefaultRange;
        if (toFlash.magnitude > range) return;      // CA6: lejos
        if (Blocked(center, eye.position, eye)) return; // CA6: pared en el medio

        float full = item != null && item.effectDuration > 0f ? item.effectDuration : 2.5f;
        float recovery = item != null ? item.recovery : 1f;
        float angle = Vector3.Angle(eye.forward, toFlash);
        if (angle <= FrontAngle) blind.Blind(full, recovery, 1f);                 // CA3
        else if (angle <= SideAngle) blind.Blind(full * 0.5f, recovery * 0.5f, 1f); // CA4
        else { blind.Blind(0f, BackFade, BackPeak); return; }                      // CA5: de espaldas no aturde
        blind.Ring(ringSound, group);
    }

    private static FlashBlind Instance()
    {
        if (instance != null) return instance;
        var go = new GameObject("Flash (pantalla en blanco)");
        instance = go.AddComponent<FlashBlind>();
        instance.Build();
        return instance;
    }

    // Una capa blanca sobre todo (también sobre el HUD, como en CS), apagada mientras no hay flash.
    private void Build()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var rect = new GameObject("Blanco", typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        white = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        white.raycastTarget = false;
        white.color = new Color(1f, 1f, 1f, 0f);
        canvas.enabled = false;
    }

    // Queda en "peak" durante "full" segundos y vuelve a 0 en "fade". Si ya estaba más encandilado, no se acorta.
    private void Blind(float full, float fade, float level)
    {
        float now = Time.time;
        if (level < Current(now) && now + full + fade <= fadeUntil) return;
        peak = Mathf.Max(level, Current(now));
        fullUntil = now + full;
        fadeUntil = fullUntil + fade;
    }

    private float Current(float now)
    {
        if (now < fullUntil) return peak;
        if (now >= fadeUntil) return 0f;
        float t = (now - fullUntil) / Mathf.Max(0.0001f, fadeUntil - fullUntil);
        return peak * (1f - Mathf.SmoothStep(0f, 1f, t));
    }

    // El pitido de oído, sin posición (suena "adentro de la cabeza"), en bucle hasta que termina el encandilamiento.
    private void Ring(AudioClip clip, AudioMixerGroup group)
    {
        if (clip == null) return;
        if (ring == null)
        {
            ring = gameObject.AddComponent<AudioSource>();
            ring.playOnAwake = false;
            ring.loop = true;
            ring.spatialBlend = 0f;
        }
        ring.clip = clip;
        ring.outputAudioMixerGroup = group;
        ringUntil = Mathf.Max(ringUntil, fadeUntil + RingAfter);
        if (!ring.isPlaying) ring.Play();
    }

    private void Update()
    {
        float amount = Current(Time.time);
        canvas.enabled = amount > 0f;
        white.color = new Color(1f, 1f, 1f, amount);

        if (ring != null && ring.isPlaying)
        {
            float left = ringUntil - Time.time;
            if (left <= 0f) ring.Stop();
            else ring.volume = RingVolume * Mathf.Clamp01(left / RingFade);
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // ¿Hay una pared entre la flash y los ojos? Los triggers, los personajes y las granadas no tapan.
    private static bool Blocked(Vector3 from, Vector3 to, Transform viewer)
    {
        Vector3 direction = to - from;
        float distance = direction.magnitude - 0.1f;
        if (distance <= 0f) return false;
        foreach (RaycastHit hit in Physics.RaycastAll(from, direction.normalized, distance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<HealthSystem>() != null) continue;
            if (hit.collider.GetComponentInParent<Grenade1>() != null) continue;
            if (hit.collider.GetComponentInParent<ArmaEnPiso>() != null) continue; // un arma tirada no tapa
            if (hit.collider.transform.IsChildOf(viewer.root)) continue;
            return true;
        }
        return false;
    }

    // El destello: una luz fuerte que ilumina alrededor y se apaga enseguida.
    private IEnumerator Destello(Vector3 at)
    {
        var go = new GameObject("Destello flash");
        go.transform.position = at;
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.97f, 0.9f);
        light.range = LightRange;
        light.shadows = LightShadows.None;
        for (float t = 0f; t < LightTime; t += Time.deltaTime)
        {
            light.intensity = LightIntensity * (1f - t / LightTime);
            yield return null;
        }
        Destroy(go);
    }
}
