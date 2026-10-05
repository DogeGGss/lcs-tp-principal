using System;
using System.Collections.Generic;
using UnityEngine;

// Datos del multijugador (F06) que se eligen en el Inspector: el jugador del que salen las copias de los demás,
// los mapas que puede elegir el anfitrión en cada modo y los tiempos de reaparición. Está en Resources/ConfigRed.
[CreateAssetMenu(menuName = "Riftwalker/Configuración del multijugador", fileName = "ConfigRed")]
public class ConfigRed : ScriptableObject
{
    [Serializable]
    public class Mapa
    {
        public string nombre = "Mapa 1";
        [Tooltip("Nombre de la escena. Tiene que estar en la lista de escenas del build.")]
        public string escena;
        public bool tactico = true;
        public bool deathmatch = true;
        [Tooltip("Captura del mapa para el fondo de la pantalla de carga (US 196). Sin imagen, el fondo queda liso.")]
        public Sprite imagen;
    }

    [Tooltip("Player.prefab: cada computadora arma con él las copias de los demás jugadores.")]
    public GameObject jugador;

    [Tooltip("Mapas que el anfitrión puede elegir en la sala (US 027, CA4).")]
    public Mapa[] mapas = new Mapa[0];

    [Tooltip("Material de la trazadora de los disparos de los demás jugadores (US 028).")]
    public Material trazadora;

    [Tooltip("Material del contorno rojo de los rivales (US 031, CA5), con el shader Efectos/ContornoRival.")]
    public Material contornoRival;

    [Header("Armas en el piso (US 184)")]
    [Tooltip("Golpe del arma contra el piso al soltarla (CA12).")]
    public AudioClip sonidoSoltarArma;
    [Tooltip("Sonido de sacar el arma al levantarla del piso (CA13).")]
    public AudioClip sonidoLevantarArma;

    [Header("Dispositivo (US 130 a US 132)")]
    [Tooltip("Modelo del dispositivo: el mismo en la mano, en el piso y plantado. Si tiene un hijo \"Pantalla\" con " +
             "TextMeshPro, muestra la cuenta; si tiene un hijo \"Luz\", titila con cada pitido.")]
    public GameObject modeloDispositivo;
    [Tooltip("Efecto de la explosión (US 131, CA6).")]
    public GameObject efectoExplosionDispositivo;
    [Tooltip("Cuenta regresiva del dispositivo plantado. Se acomoda para que termine justo cuando explota.")]
    public AudioClip sonidoCuentaDispositivo;
    [Tooltip("Segundo del sonido de la cuenta en el que tiene que explotar (donde termina el sonido fuerte).")]
    public float finCuentaDispositivo = 35.6f;
    [Range(0f, 1f)] public float volumenCuentaDispositivo = 0.8f;
    [Tooltip("Pitido del dispositivo plantado: uno por segundo, más seguido al final.")]
    public AudioClip sonidoBipDispositivo;
    [Tooltip("Teclas mientras se planta (US 131).")]
    public AudioClip sonidoPlantando;
    [Tooltip("Aviso de dispositivo plantado: lo escuchan todos (US 131, CA4).")]
    public AudioClip sonidoPlantado;
    [Tooltip("Sonido distinto mientras un defensor lo desactiva (US 132, CA3). Hace loop.")]
    public AudioClip sonidoDesactivando;
    [Tooltip("Se desactivó (US 132, CA4).")]
    public AudioClip sonidoDesactivado;
    [Tooltip("Explosión (US 131, CA6).")]
    public AudioClip sonidoExplosionDispositivo;

    [Header("Deathmatch (US 030 y US 137)")]
    [Tooltip("Segundos entre la muerte y la reaparición.")]
    public float reaparicion = 3f;
    [Tooltip("Segundos sin recibir daño al reaparecer. Se cortan si dispara.")]
    public float invulnerabilidad = 2f;

    [Tooltip("Música del Modo Deathmatch (US 136, CA6). Suena en loop por el grupo Música. Sin clip, no suena nada.")]
    public AudioClip musicaDeathmatch;
    [Tooltip("Ambiente del Modo Deathmatch (US 136, CA6). Suena en loop, más bajo que la música.")]
    public AudioClip ambienteDeathmatch;

    [Header("Táctico (US 032)")]
    [Tooltip("Música del Modo Táctico (CA8). Suena en loop por el grupo de abajo.")]
    public AudioClip musicaTactico;
    [Tooltip("Ambiente del Modo Táctico (CA8). Suena en loop, más bajo que la música.")]
    public AudioClip ambienteTactico;
    [Tooltip("Grupo Musica del Audio Mixer: así lo regulan General y Música de Opciones (US 154).")]
    public UnityEngine.Audio.AudioMixerGroup grupoMusica;
    [Range(0f, 1f)] public float volumenMusica = 0.35f;
    [Range(0f, 1f)] public float volumenAmbiente = 0.5f;

    private static ConfigRed actual;

    public static ConfigRed Actual
    {
        get
        {
            if (actual == null) actual = Resources.Load<ConfigRed>("ConfigRed");
            return actual;
        }
    }

    public List<Mapa> MapasDe(GameMode modo)
    {
        var lista = new List<Mapa>();
        foreach (Mapa mapa in mapas)
            if (mapa != null && !string.IsNullOrEmpty(mapa.escena) &&
                (modo == GameMode.Tactico ? mapa.tactico : modo == GameMode.Deathmatch && mapa.deathmatch))
                lista.Add(mapa);
        return lista;
    }
}
