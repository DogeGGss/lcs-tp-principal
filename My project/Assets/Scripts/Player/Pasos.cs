using UnityEngine;

// Pasos del personaje (US 001, CA8 y US 002, CA7) y caminar en silencio (US 198).
// - Suena un paso cada "zancada" metros recorridos sobre el piso: corriendo se recorre más rápido, así que suenan más
//   seguido. Quieto no suena.
// - No suenan caminando despacio (Shift en el Táctico) ni agachado (US 198, CA3 y CA4).
// - Al saltar suena el salto (US 003, CA6) y al caer al piso, un paso más fuerte, aunque camine despacio (US 198, CA5).
// - Los propios se escuchan sin dirección; los de los demás jugadores, desde donde están, hasta unos 25 m.
// Salen por el grupo SFX del mezclador (US 154). Los sonidos están en Resources/SonidosJugador.
// Al jugador local lo agrega PlayerMovement. A las copias de los demás las agrega JugadorEnRed, que le pasa lo que
// llega por la red (agachado, despacio, en el aire, muerto).
public class Pasos : MonoBehaviour
{
    private const float VelocidadMinima = 1f;   // m/s: más despacio no suena (por ejemplo, frenando)
    private const float VelocidadMaxima = 15f;  // más rápido es un teletransporte (reaparecer), no un paso
    private const float AireMinimo = 0.25f;     // s en el aire para que la caída suene (no cada escalón)

    // Copia de otro jugador: lo que llega por la red.
    public bool Agachado { get; set; }
    public bool Despacio { get; set; }
    public bool EnElAire { get; set; }
    public bool Muerto { get; set; }

    private PlayerMovement movimiento;
    private CharacterController cuerpo;
    private SonidosJugador sonidos;
    private AudioSource fuente;
    private bool remoto;
    private Vector3 anterior;
    private float recorrido, enElAireDesde = -1f;
    private int ultimo = -1;

    private void Awake()
    {
        movimiento = GetComponent<PlayerMovement>();
        cuerpo = GetComponent<CharacterController>();
        sonidos = SonidosJugador.Actual;
        fuente = gameObject.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.spatialBlend = 0f;
        fuente.dopplerLevel = 0f;
        if (sonidos != null) fuente.outputAudioMixerGroup = sonidos.grupo;
        anterior = transform.position;
        if (movimiento != null) movimiento.Jumped += Salto;
    }

    private void OnDestroy()
    {
        if (movimiento != null) movimiento.Jumped -= Salto;
    }

    /// <summary>US 003, CA6: el sonido del salto. En las copias lo llama JugadorEnRed cuando su dueño salta.</summary>
    public void Salto()
    {
        if (sonidos == null || sonidos.salto == null) return;
        fuente.pitch = Random.Range(0.97f, 1.03f);
        fuente.PlayOneShot(sonidos.salto, sonidos.volumenSalto * (remoto ? sonidos.volumenDemas : 1f));
    }

    /// <summary>Es la copia de otro jugador: sus pasos se escuchan desde donde está.</summary>
    public void ComoCopia()
    {
        remoto = true;
        movimiento = null;
        fuente.spatialBlend = 1f;
        fuente.rolloffMode = AudioRolloffMode.Linear;
        fuente.minDistance = 2f;
        fuente.maxDistance = sonidos != null ? sonidos.alcanceDemas : 25f;
    }

    private void Update()
    {
        Vector3 ahora = transform.position;
        Vector3 paso = ahora - anterior;
        paso.y = 0f;
        anterior = ahora;
        float dt = Time.deltaTime;
        if (sonidos == null || sonidos.pasos.Length == 0 || dt <= 0f) return;
        float velocidad = paso.magnitude / dt;

        bool enElAire, silencio;
        if (remoto)
        {
            enElAire = EnElAire;
            silencio = Agachado || Despacio || Muerto;
        }
        else
        {
            enElAire = cuerpo != null && !cuerpo.isGrounded;
            silencio = movimiento == null || !movimiento.enabled || movimiento.Agachado || movimiento.CaminandoDespacio;
        }

        // Al caer al piso suena un paso, más fuerte (aunque camine despacio).
        if (enElAire)
        {
            if (enElAireDesde < 0f) enElAireDesde = Time.time;
            return;
        }
        if (enElAireDesde >= 0f)
        {
            bool cayo = Time.time - enElAireDesde >= AireMinimo;
            enElAireDesde = -1f;
            if (cayo && !(remoto && Muerto) && (remoto || movimiento == null || movimiento.enabled))
            {
                Sonar(1.3f);
                recorrido = 0f;
                return;
            }
        }

        // Quieto, el primer paso suena enseguida al arrancar.
        if (velocidad < VelocidadMinima) { recorrido = sonidos.zancada * 0.7f; return; }
        if (silencio || velocidad > VelocidadMaxima) return;

        recorrido += paso.magnitude;
        if (recorrido < sonidos.zancada) return;
        recorrido = 0f;
        Sonar(1f);
    }

    // Uno al azar, sin repetir el anterior, con un poco de variación de tono.
    private void Sonar(float fuerza)
    {
        int cantidad = sonidos.pasos.Length;
        int i = cantidad == 1 ? 0 : Random.Range(0, cantidad - 1);
        if (cantidad > 1 && ultimo >= 0 && i >= ultimo) i++;
        ultimo = i;
        AudioClip clip = sonidos.pasos[i];
        if (clip == null) return;
        fuente.pitch = Random.Range(0.94f, 1.06f);
        fuente.PlayOneShot(clip, Mathf.Clamp01(fuerza * (remoto ? sonidos.volumenDemas : sonidos.volumenPropios)));
    }
}
