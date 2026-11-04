using UnityEngine;

// F08 (Modo Invasión): lo que se ajusta del modo desde el editor, en Resources/Invasion/ConfigInvasion: los portales de
// donde salen los enemigos (US 269), cuántos invasores trae cada oleada (US 299), el puesto de suministros (US 292) y
// los alfas (US 301) y cómo se endurece el Jefe en cada vuelta (US 302).
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

    [Header("Cantidad de invasores (US 299)")]
    [Tooltip("CA1: invasores de la oleada 1 en Normal (la dificultad los multiplica, US 161).")]
    [Min(1)] public int invasoresAlEmpezar = 10;
    [Tooltip("CA1: cuántos invasores más trae cada oleada.")]
    [Min(0)] public int invasoresPorOleada = 5;
    [Tooltip("CA2: tope de invasores vivos a la vez en la oleada 1.")]
    [Min(1)] public int vivosAlEmpezar = 15;
    [Tooltip("CA2: cuánto sube el tope de vivos a la vez por oleada.")]
    [Min(0)] public int vivosPorOleada = 1;
    [Tooltip("CA2 y CA3: el tope de vivos a la vez nunca pasa de este número.")]
    [Min(1)] public int vivosMaximo = 30;

    [Header("Puesto de suministros (US 292)")]
    [Tooltip("CA3: radio del anillo alrededor del puesto, en metros.")]
    [Min(1f)] public float radioPuesto = 15f;
    [Tooltip("CA5: qué parte de los puntos de la oleada entera hace falta para cargar el puesto (0,4 = 40 %).")]
    [Range(0.05f, 1f)] public float metaPuesto = 0.4f;
    [Tooltip("CA5: los puntos que da en promedio cada invasor (la baja, 60, y unos 4 impactos de 10). Con esto se calcula cuánto da la oleada entera.")]
    [Min(1)] public int puntosPorInvasor = 100;
    [Tooltip("La caja que cae. Vacía: un cajón verde hecho con cubos.")]
    public GameObject cajaPuesto;
    [Tooltip("Tamaño de la caja que cae.")]
    [Min(0.1f)] public float escalaCajaPuesto = 1.3f;

    [Header("Alfas (US 301)")]
    [Tooltip("CA1: desde qué oleada salen alfas.")]
    [Min(1)] public int alfasDesdeLaOleada = 4;
    [Tooltip("CA1: 1 de cada tantos invasores es un alfa (de cualquier tipo, salvo el Jefe).")]
    [Min(1)] public int unAlfaCada = 15;
    [Tooltip("CA2 a CA4: cómo es el alfa (tamaño, vida, daño, puntos, color y aura).")]
    public VarianteAlfa alfa = new VarianteAlfa();

    [Header("Jefe cada 10 oleadas (US 302)")]
    [Tooltip("CA2 a CA4: vida que suma el Jefe en cada vuelta (en Normal: 2.000 en la 10, 3.500 en la 20, 5.000 en la 30...).")]
    [Min(0)] public int vidaDelJefePorVuelta = 1500;
    [Tooltip("CA2: desde la oleada 20, con qué parte de la vida se enfurece (en la 10, la mitad).")]
    [Range(0.05f, 1f)] public float furiaDesdeLaVuelta2 = 0.75f;
    [Tooltip("CA2: Tanques que lo escoltan desde la oleada 20 (salen del portal del Jefe).")]
    [Min(0)] public int escoltaDelJefe = 2;
    [Tooltip("CA3: embestidas seguidas desde la oleada 30 (que además arranca enfurecido).")]
    [Min(1)] public int embestidasSeguidas = 2;
    [Tooltip("CA4: desde la oleada 40, cuánto más grandes son sus nubes de veneno en cada vuelta (0,25 = 25 % más).")]
    [Min(0f)] public float nubesMasGrandesPorVuelta = 0.25f;
    [Tooltip("CA4: desde la oleada 40, por cuánto se multiplica el tiempo entre sus vuelos en cada vuelta (0,8 = 20 % más seguidos).")]
    [Range(0.3f, 1f)] public float vuelosPorVuelta = 0.8f;

    /// <summary>US 299, CA1: los invasores de la oleada (sin la dificultad).</summary>
    public int Invasores(int oleada) => invasoresAlEmpezar + invasoresPorOleada * (Mathf.Max(1, oleada) - 1);

    /// <summary>US 299, CA2: el tope de invasores vivos a la vez en la oleada.</summary>
    public int TopeDeVivos(int oleada) => Mathf.Min(vivosMaximo, vivosAlEmpezar + vivosPorOleada * (Mathf.Max(1, oleada) - 1));

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
