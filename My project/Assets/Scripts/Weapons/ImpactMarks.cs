using UnityEngine;

// Marcas de impacto en el escenario (US 170): cada bala que pega en una pared o en el piso deja un
// agujero con una nube de polvo. Duran 10 s y se desvanecen, con un máximo de 64 en el mapa (al
// superarlo se reutiliza la más vieja). Sobre personajes no quedan marcas: esos impactos se ven con
// el marcador de impacto (US 165). Se crea sola la primera vez que se la usa; no hace falta ponerla
// en la escena.
public class ImpactMarks : MonoBehaviour
{
    public const int MaxMarks = 64;
    public const float Lifetime = 10f;
    private const float FadeTime = 1f;
    private const string PrefabPath = "Efectos/ImpactoBala";

    // En el campo de práctica (US 118) las marcas no se desvanecen hasta que se reinician los
    // blancos (US 120): esa escena pone Permanent en true en su Start y llama a Clear() al
    // reiniciar. Vuelve a false en cada cambio de escena, así no queda prendido en los mapas.
    public static bool Permanent { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        instance = null;
        Permanent = false;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        Permanent = false;
    }

    private class Mark
    {
        public GameObject root;
        public Transform hole;
        public Renderer holeRenderer;
        public ParticleSystem dust;
        public float bornAt;
        public bool active;
    }

    private static ImpactMarks instance;
    private static GameObject prefab;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private readonly Mark[] marks = new Mark[MaxMarks];
    private int next;
    private MaterialPropertyBlock block;
    private Color baseColor = Color.white;

    // Deja una marca donde pegó la bala, salvo que sea un personaje o una zona invisible.
    public static void Spawn(RaycastHit hit)
    {
        if (hit.collider == null || hit.collider.isTrigger) return;
        if (hit.collider.GetComponentInParent<HealthSystem>() != null) return;

        ImpactMarks marks = Instance();
        if (marks != null) marks.Place(hit.point, hit.normal);
    }

    // Borra todas las marcas (por ejemplo, al reiniciar los blancos del campo de práctica).
    public static void Clear()
    {
        if (instance == null) return;
        foreach (Mark mark in instance.marks)
        {
            if (mark == null) continue;
            mark.active = false;
            mark.root.SetActive(false);
        }
    }

    // Cantidad de marcas visibles ahora.
    public static int ActiveCount
    {
        get
        {
            if (instance == null) return 0;
            int count = 0;
            foreach (Mark mark in instance.marks) if (mark != null && mark.active) count++;
            return count;
        }
    }

    private static ImpactMarks Instance()
    {
        if (instance != null) return instance;
        if (prefab == null) prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"ImpactMarks: no se encontró Resources/{PrefabPath}.");
            return null;
        }
        instance = new GameObject("MarcasDeImpacto").AddComponent<ImpactMarks>();
        return instance;
    }

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        Renderer holeRenderer = prefab.GetComponentInChildren<MeshRenderer>();
        if (holeRenderer != null && holeRenderer.sharedMaterial.HasProperty(BaseColorId))
            baseColor = holeRenderer.sharedMaterial.GetColor(BaseColorId);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Place(Vector3 point, Vector3 normal)
    {
        Mark mark = marks[next];
        if (mark == null)
        {
            GameObject root = Instantiate(prefab, transform);
            MeshRenderer holeRenderer = root.GetComponentInChildren<MeshRenderer>();
            mark = marks[next] = new Mark
            {
                root = root,
                hole = holeRenderer.transform,
                holeRenderer = holeRenderer,
                dust = root.GetComponentInChildren<ParticleSystem>()
            };
        }
        next = (next + 1) % MaxMarks;

        // El +Z de la marca apunta hacia adentro de la superficie: el agujero queda mirando hacia afuera,
        // apenas separado para que no parpadee, con giro y tamaño al azar para que no se vean iguales.
        mark.root.transform.SetPositionAndRotation(point + normal * 0.003f, Quaternion.LookRotation(-normal));
        mark.hole.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        mark.hole.localScale = Vector3.one * Random.Range(0.055f, 0.08f);
        SetAlpha(mark, 1f);

        mark.bornAt = Time.time;
        mark.active = true;
        mark.root.SetActive(true);
        if (mark.dust != null)
        {
            mark.dust.Clear();
            mark.dust.Play();
        }
    }

    private void Update()
    {
        if (Permanent) return;

        float now = Time.time;
        foreach (Mark mark in marks)
        {
            if (mark == null || !mark.active) continue;
            float age = now - mark.bornAt;
            if (age >= Lifetime + FadeTime)
            {
                mark.active = false;
                mark.root.SetActive(false);
            }
            else if (age > Lifetime)
            {
                SetAlpha(mark, 1f - (age - Lifetime) / FadeTime);
            }
        }
    }

    private void SetAlpha(Mark mark, float alpha)
    {
        Color color = baseColor;
        color.a *= alpha;
        mark.holeRenderer.GetPropertyBlock(block);
        block.SetColor(BaseColorId, color);
        mark.holeRenderer.SetPropertyBlock(block);
    }
}
