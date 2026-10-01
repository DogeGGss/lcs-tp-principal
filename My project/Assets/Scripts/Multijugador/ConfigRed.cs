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
    }

    [Tooltip("Player.prefab: cada computadora arma con él las copias de los demás jugadores.")]
    public GameObject jugador;

    [Tooltip("Mapas que el anfitrión puede elegir en la sala (US 027, CA4).")]
    public Mapa[] mapas = new Mapa[0];

    [Tooltip("Material de la trazadora de los disparos de los demás jugadores (US 028).")]
    public Material trazadora;

    [Tooltip("Material del contorno rojo de los rivales (US 031, CA5), con el shader Efectos/ContornoRival.")]
    public Material contornoRival;

    [Header("Deathmatch (US 030 y US 137)")]
    [Tooltip("Segundos entre la muerte y la reaparición.")]
    public float reaparicion = 3f;
    [Tooltip("Segundos sin recibir daño al reaparecer. Se cortan si dispara.")]
    public float invulnerabilidad = 2f;

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
