using UnityEngine;
using UnityEngine.Audio;

// Sonidos del personaje que no son de un arma: los pasos (US 001, US 002 y US 198), el salto (US 003) y el marcador
// de impacto (US 165). Está en Resources/SonidosJugador, así lo encuentran el jugador, las copias de los demás y el
// HUD sin tocar el Player.prefab ni las escenas.
[CreateAssetMenu(menuName = "Riftwalker/Sonidos del jugador", fileName = "SonidosJugador")]
public class SonidosJugador : ScriptableObject
{
    [Tooltip("Pasos: en cada paso suena uno al azar, sin repetir el anterior.")]
    public AudioClip[] pasos = new AudioClip[0];

    [Tooltip("Grupo SFX del Audio Mixer: lo regulan General y Efectos de Opciones (US 154).")]
    public AudioMixerGroup grupo;

    [Tooltip("Volumen de los pasos propios.")]
    [Range(0f, 1f)] public float volumenPropios = 0.7f;

    [Tooltip("Volumen de los pasos de los demás jugadores, a menos de 2 m.")]
    [Range(0f, 1f)] public float volumenDemas = 1f;

    [Tooltip("Metros entre un paso y el siguiente. Corriendo se recorren más rápido, así que suenan más seguido.")]
    public float zancada = 2.1f;

    [Tooltip("Hasta cuántos metros se escuchan los pasos de los demás.")]
    public float alcanceDemas = 25f;

    [Tooltip("Al saltar (US 003, CA6). Los de los demás se escuchan desde donde están, como sus pasos.")]
    public AudioClip salto;
    [Range(0f, 1f)] public float volumenSalto = 0.6f;

    [Tooltip("Marcador de impacto (US 165): al acertar. A la cabeza suena más agudo y en una baja, más grave.")]
    public AudioClip marcadorImpacto;

    private static SonidosJugador actual;

    public static SonidosJugador Actual
    {
        get
        {
            if (actual == null) actual = Resources.Load<SonidosJugador>("SonidosJugador");
            return actual;
        }
    }
}
