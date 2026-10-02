using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// Un arma en el piso (US 184): el modelo a tamaño real (ModeloReal, CA9) con las balas que tenía (CA3). La levanta
// solo el jugador que pasa por encima con ese espacio vacío (SoltarArmas).
// Cae con física: rebota y queda apoyada en lo que encuentre (una rampa, una escalera), sin chocar con los personajes
// y en la capa Ignore Raycast, así las balas la atraviesan. En multijugador cada computadora tiene la suya con el
// mismo Id: todas la tiran igual y, cuando la de quien la soltó se queda quieta, las demás se ponen en ese mismo
// lugar (CA10).
public class ArmaEnPiso : MonoBehaviour
{
    private const int CapaSinBalas = 2;         // Ignore Raycast: WeaponFire no la ve
    private const float CasiQuieta = 0.08f;     // m/s: por debajo de esto, se está quedando quieta...
    private const float QuietaDurante = 0.4f;   // ...y si sigue así este tiempo, ya quedó
    private const float SinMoverseMaximo = 10f; // a los 10 s se da por quieta aunque se siga moviendo
    private const float GolpeMinimo = 1f;       // velocidad del choque para que suene

    private static readonly Dictionary<int, ArmaEnPiso> todas = new Dictionary<int, ArmaEnPiso>();

    public int Id { get; private set; }
    public ShopItem Item { get; private set; }
    public int Cargador { get; private set; }
    public int Reserva { get; private set; }
    // La soltó el jugador de esta computadora: cuando se queda quieta, avisa dónde quedó.
    public bool Propia { get; private set; }
    // Ya se pidió levantarla y se espera la respuesta del dueño de la sala (CA10). Si no llega, se vuelve a pedir.
    public float PedidaHasta { get; set; }
    public bool Pedida => Time.time < PedidaHasta;
    // Centro del arma.
    public Vector3 Centro => transform.position;

    public static IEnumerable<ArmaEnPiso> Todas => todas.Values;

    // Grupo SFX del mezclador por el que suenan (lo pone SoltarArmas, con el de las armas del jugador).
    public static AudioMixerGroup Grupo;

    // La propia se quedó quieta en este lugar (JugadorEnRed se lo manda a los demás).
    public static event System.Action<ArmaEnPiso> Quieta;

    private Rigidbody cuerpo;
    private float creada, quietaDesde = -1f;
    private bool asentada, sono;

    public static ArmaEnPiso Buscar(int id) => todas.TryGetValue(id, out ArmaEnPiso arma) && arma != null ? arma : null;

    // Crea el arma en "desde", mirando hacia "yaw", y la tira con "velocidad". El giro sale del id, así es igual en
    // todas las computadoras. Si acá no está el modelo de esa ficha, igual queda (sin verse) para poder levantarla.
    public static ArmaEnPiso Crear(int id, ShopItem item, Vector3 desde, Vector3 velocidad, float yaw, int cargador, int reserva, bool propia)
    {
        Quitar(id);
        GameObject go = ModeloReal.Crear(item, ModeloReal.Molde(item), out Bounds limites);
        go.name = "Arma en el piso: " + ModeloReal.NombreDe(item);
        go.transform.SetPositionAndRotation(desde, Quaternion.Euler(0f, yaw, 0f));
        foreach (Transform parte in go.GetComponentsInChildren<Transform>(true)) parte.gameObject.layer = CapaSinBalas;

        var caja = go.AddComponent<BoxCollider>();
        caja.size = Vector3.Max(limites.size, Vector3.one * 0.04f);
        caja.sharedMaterial = new PhysicsMaterial("Arma en el piso")
        {
            bounciness = 0.15f,
            dynamicFriction = 0.7f,
            staticFriction = 0.8f,
            bounceCombine = PhysicsMaterialCombine.Minimum,
        };
        // No choca con los personajes: se puede pasar por encima para levantarla.
        foreach (HealthSystem personaje in FindObjectsByType<HealthSystem>())
            foreach (Collider parte in personaje.GetComponentsInChildren<Collider>(true))
                Physics.IgnoreCollision(caja, parte);

        var arma = go.AddComponent<ArmaEnPiso>();
        arma.Id = id;
        arma.Item = item;
        arma.Cargador = cargador;
        arma.Reserva = reserva;
        arma.Propia = propia;
        arma.creada = Time.time;
        todas[id] = arma;

        var random = new System.Random(id);
        arma.cuerpo = go.AddComponent<Rigidbody>();
        arma.cuerpo.mass = 2f;
        arma.cuerpo.angularDamping = 0.6f;
        arma.cuerpo.interpolation = RigidbodyInterpolation.Interpolate;
        arma.cuerpo.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        arma.cuerpo.linearVelocity = velocidad;
        arma.cuerpo.angularVelocity = new Vector3((float)random.NextDouble() * 4f - 2f, (float)random.NextDouble() * 2f - 1f,
            (float)random.NextDouble() * 6f - 3f);
        return arma;
    }

    // Las demás computadoras la ponen donde quedó la de quien la soltó (CA10).
    public void Asentar(Vector3 posicion, Quaternion giro)
    {
        if (cuerpo != null) cuerpo.isKinematic = true;
        transform.SetPositionAndRotation(posicion, giro);
        asentada = true;
    }

    public static void Quitar(int id)
    {
        ArmaEnPiso arma = Buscar(id);
        todas.Remove(id);
        if (arma != null) Destroy(arma.gameObject);
    }

    // CA11: al terminar la ronda no queda ninguna.
    public static void QuitarTodas()
    {
        foreach (ArmaEnPiso arma in todas.Values)
            if (arma != null) Destroy(arma.gameObject);
        todas.Clear();
    }

    private void OnDestroy()
    {
        if (todas.TryGetValue(Id, out ArmaEnPiso arma) && arma == this) todas.Remove(Id);
    }

    private void FixedUpdate()
    {
        if (asentada || cuerpo == null) return;
        // Quieta de verdad: dormida, o casi sin moverse un rato (así no se congela en el aire a mitad de un rebote).
        bool despacio = cuerpo.linearVelocity.magnitude < CasiQuieta && cuerpo.angularVelocity.magnitude < CasiQuieta * 4f;
        if (!despacio) quietaDesde = -1f;
        else if (quietaDesde < 0f) quietaDesde = Time.time;
        bool quieta = cuerpo.IsSleeping() || (despacio && Time.time - quietaDesde >= QuietaDurante) ||
                      Time.time - creada > SinMoverseMaximo;
        if (!quieta) return;
        // La de quien la soltó manda dónde quedó; las demás la esperan (y si no llega, se quedan donde están).
        if (Propia) { Asentar(transform.position, transform.rotation); Quieta?.Invoke(this); }
        else if (Time.time - creada > SinMoverseMaximo + 2f) Asentar(transform.position, transform.rotation);
    }

    // CA12: al tocar el piso suena el golpe (una vez).
    private void OnCollisionEnter(Collision choque)
    {
        if (sono || choque.relativeVelocity.magnitude < GolpeMinimo) return;
        sono = true;
        Sonar(ConfigRed.Actual != null ? ConfigRed.Actual.sonidoSoltarArma : null, choque.GetContact(0).point, Grupo);
    }

    // Sonido 3D en un punto, por el grupo del mezclador; el objeto se borra cuando termina.
    public static void Sonar(AudioClip clip, Vector3 en, AudioMixerGroup grupo)
    {
        if (clip == null) return;
        var go = new GameObject("Sonido " + clip.name);
        go.transform.position = en;
        AudioSource fuente = go.AddComponent<AudioSource>();
        fuente.clip = clip;
        fuente.outputAudioMixerGroup = grupo;
        fuente.spatialBlend = 1f;
        fuente.rolloffMode = AudioRolloffMode.Linear;
        fuente.minDistance = 2f;
        fuente.maxDistance = 30f;
        fuente.dopplerLevel = 0f;
        fuente.Play();
        Destroy(go, clip.length + 0.1f);
    }
}
