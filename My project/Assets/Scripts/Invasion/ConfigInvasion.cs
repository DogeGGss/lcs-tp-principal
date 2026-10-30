using UnityEngine;

// F08 (Modo Invasión): lo que se ajusta del modo desde el editor, en Resources/Invasion/ConfigInvasion. Por ahora, los
// portales de donde salen los enemigos (US 269).
[CreateAssetMenu(menuName = "Riftwalker/Invasión/Configuración", fileName = "ConfigInvasion")]
public class ConfigInvasion : ScriptableObject
{
    [Header("Portales (US 269)")]
    [Tooltip("El portal de donde salen los enemigos (PortalOne). Vacío: un disco rojo.")]
    public GameObject portal;
    [Tooltip("Tamaño del portal de los enemigos.")]
    [Min(0.1f)] public float escala = 1f;
    [Tooltip("Tamaño del portal del Jefe (CA5).")]
    [Min(0.1f)] public float escalaJefe = 2.5f;
    [Tooltip("Giro del modelo (grados) para que el frente del portal mire hacia afuera, hacia el jugador.")]
    public float giro;
    [Tooltip("m que salen los enemigos por delante del portal.")]
    [Min(0f)] public float salida = 1.2f;
    [Tooltip("Sonido al abrirse. Vacío: Sonidos/Resources/Mapas/portal_tp.")]
    public AudioClip sonidoAbrir;
    [Tooltip("Zumbido mientras está abierto. Vacío: Sonidos/Resources/Mapas/portal_loop.")]
    public AudioClip sonidoAbierto;

    private static ConfigInvasion actual;

    /// <summary>La configuración del modo (Resources/Invasion/ConfigInvasion), o una con los valores por defecto.</summary>
    public static ConfigInvasion Actual
    {
        get
        {
            if (actual == null) actual = Resources.Load<ConfigInvasion>("Invasion/ConfigInvasion");
            if (actual == null) actual = CreateInstance<ConfigInvasion>();
            return actual;
        }
    }
}
