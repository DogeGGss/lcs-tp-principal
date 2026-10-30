using UnityEngine;
using UnityEngine.SceneManagement;

// F08 (Modo Invasión), US 143: la partida de Invasión arranca sola al cargar un mapa del modo (el que tiene MapMode
// en Invasión, el de la universidad). Para un jugador, sin sala ni conexión (CA1): la selección de modo carga la escena
// directo (ModeSelectScreen, con la dificultad y el personaje elegidos).
// - CA2: el mapa es el Mapa 3, el de la universidad (lo pone la selección de modo: PanelSeleccionModo, zombieScene).
// - CA3: el jugador arranca con 100 de vida, sin escudo, La Porteña y el cuchillo (lo inicial de la tienda) y 500
//   puntos (la billetera, que en este modo son puntos: US 147).
// - CA4: antes de la oleada 1 hay un descanso de 20 s con la tienda (la fase de compra de la escena, BuyPhase).
// - CA5: Esc pausa el juego (PauseMenu: sin conexión, el tiempo se detiene y los enemigos también).
// - CA6: suenan la música y el ambiente del modo, en loop por el grupo Música (ConfigRed: musicaInvasion y
//   ambienteInvasion). Sin clips cargados no suena nada.
// Las oleadas son la US 144 (OleadasDeInvasion), el resultado la US 151 (ResultadoDeInvasion), los puntos la US 147 (PuntosDeInvasion), el marcador la US 150 (MarcadorInvasion) y la regeneración de vida,
// la US 149 (RegeneracionDeVida, se le suma al jugador). Se crea sola: no hace falta ponerla en la escena.
public class PartidaDeInvasion : MonoBehaviour
{
    public const int PuntosIniciales = 500; // CA3
    public const int VidaInicial = 100;      // CA3

    /// <summary>La partida de Invasión en curso, o null si el mapa no es de Invasión.</summary>
    public static PartidaDeInvasion Actual { get; private set; }

    // Sin recarga de scripts al entrar al Play (Enter Play Mode Options): se vuelve a enganchar una sola vez.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Enganchar()
    {
        Actual = null;
        SceneManager.sceneLoaded -= AlCargar;
        SceneManager.sceneLoaded += AlCargar;
    }

    private static void AlCargar(Scene escena, LoadSceneMode modo)
    {
        if (modo != LoadSceneMode.Single) return;
        if (MapMode.DeEstaEscena != GameMode.Zombie) return; // no es un mapa de Invasión
        if (Actual != null) return;
        new GameObject("Partida de Invasión").AddComponent<PartidaDeInvasion>();
    }

    private bool armada;
    private HealthSystem vidaJugador;

    /// <summary>US 151, CA3: cuándo empezó la partida (Time.time: con la pausa no corre).</summary>
    public float Inicio { get; private set; }

    private void Awake() => Actual = this;

    private void OnDestroy()
    {
        if (Actual == this) Actual = null;
        if (vidaJugador != null) vidaJugador.Died -= AlMorirElJugador;
    }

    private void Start()
    {
        Inicio = Time.time;
        ArrancarMusica();
        gameObject.AddComponent<OleadasDeInvasion>(); // US 144
        gameObject.AddComponent<MarcadorInvasion>();  // US 150
        gameObject.AddComponent<PuntosDeInvasion>();  // US 147
    }

    // CA3: el estado inicial del jugador, en cuanto está (aparece con la escena).
    private void Update()
    {
        if (armada) return;
        PlayerWallet billetera = FindFirstObjectByType<PlayerWallet>();
        if (billetera == null) return;
        armada = true;
        billetera.SinTope(); // US 147, CA5
        billetera.Set(PuntosIniciales);
        HealthSystem vida = billetera.GetComponent<HealthSystem>();
        if (vida != null)
        {
            vida.maxHealth = VidaInicial;
            vida.SetState(VidaInicial, 0);
            if (vida.GetComponent<RegeneracionDeVida>() == null) vida.gameObject.AddComponent<RegeneracionDeVida>(); // US 149
            vidaJugador = vida;
            vida.Died += AlMorirElJugador; // US 151, CA1 y CA6 (caerse del mapa también lo mata: FueraDelMapa)
        }
    }

    // US 151, CA1: no hay segunda vida, la partida termina.
    private void AlMorirElJugador() => ResultadoDeInvasion.Mostrar(true);

    // CA6
    private void ArrancarMusica()
    {
        ConfigRed config = ConfigRed.Actual;
        if (config == null) return;
        Fuente(config.musicaInvasion, config.volumenMusica, config.grupoMusica);
        Fuente(config.ambienteInvasion, config.volumenAmbiente, config.grupoMusica);
    }

    private void Fuente(AudioClip clip, float volumen, UnityEngine.Audio.AudioMixerGroup grupo)
    {
        if (clip == null) return;
        AudioSource fuente = gameObject.AddComponent<AudioSource>();
        fuente.clip = clip;
        fuente.loop = true;
        fuente.playOnAwake = false;
        fuente.spatialBlend = 0f;
        fuente.volume = volumen;
        fuente.outputAudioMixerGroup = grupo;
        fuente.Play();
    }
}
