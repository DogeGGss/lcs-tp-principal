using UnityEngine;

// US 395: la katana, el arma cuerpo a cuerpo que solo existe en el Modo Invasión. Ocupa el lugar del cuchillo
// (MeleeWeaponHolder) y suma lo propio: el tajo en abanico que pega a todos los que tiene delante, el combo de tres, la
// estocada (manteniendo el ataque), la guardia (clic derecho) y la cura por baja. Lo usa MeleeAttack. Los números son
// tentativos: se ajustan probando.
[CreateAssetMenu(fileName = "Katana", menuName = "Armas/Katana")]
public class KatanaData : MeleeWeaponData
{
    [Header("Combo (CA4)")]
    [Tooltip("El golpe número tantos seguido es el tajo fuerte.")]
    [Min(2)] public int golpesDelCombo = 3;
    [Tooltip("Cuánto puede pasar entre un golpe y el siguiente para que cuente como seguido (s).")]
    public float ventanaDelCombo = 1.2f;
    [Tooltip("Cuánto más daño hace el tajo fuerte (2 = el doble).")]
    public float danioDelTajoFuerte = 2f;
    [Tooltip("Cuánto empuja el tajo fuerte a los invasores que toca (metros).")]
    public float empujonDelTajoFuerte = 1.5f;

    [Header("Estocada (CA5)")]
    [Tooltip("Cuánto hay que mantener el ataque para que salga la estocada (s).")]
    public float cargaDeLaEstocada = 0.6f;
    [Tooltip("Cuánto avanza el jugador (metros).")]
    public float largoDeLaEstocada = 5f;
    [Tooltip("Cuánto tarda en recorrerla (s).")]
    public float duracionDeLaEstocada = 0.25f;
    [Tooltip("Daño a cada invasor que cruza.")]
    public int danioDeLaEstocada = 120;
    [Tooltip("A qué distancia del camino tiene que estar un invasor para que lo toque (metros).")]
    public float anchoDeLaEstocada = 1.2f;
    [Tooltip("Cuánto tarda en poder volver a hacerla (s).")]
    public float esperaDeLaEstocada = 6f;

    [Header("Guardia (CA6)")]
    [Tooltip("Hasta qué ángulo desde donde mira frena los disparos (grados, de lado a lado).")]
    [Range(10f, 180f)] public float anguloDeLaGuardia = 100f;
    [Tooltip("Qué parte de la velocidad le queda mientras está en guardia.")]
    [Range(0.1f, 1f)] public float velocidadEnGuardia = 0.55f;

    [Header("Sed de sangre (CA7)")]
    [Tooltip("Vida que cura cada baja con la katana.")]
    public int curaPorBaja = 10;

    [Header("Cómo se ve (CA9)")]
    [Tooltip("El ícono del HUD (en lugar del del cuchillo).")]
    public Sprite icono;
    [Tooltip("Tamaño del modelo en la mano de los demás (1 = el del modelo).")]
    public float escalaEnTerceraPersona = 1.1f;
    [Tooltip("Color de la energía (la estela, los destellos y el brillo del filo).")]
    [ColorUsage(false, true)] public Color energia = new Color(0.45f, 0.85f, 1.6f);

    /// <summary>La katana del juego (Resources/Katana/KatanaDatos), o null.</summary>
    public static KatanaData Cargar()
    {
        if (cargada == null) cargada = Resources.Load<KatanaData>("Katana/KatanaDatos");
        return cargada;
    }
    private static KatanaData cargada;

    /// <summary>CA4: si el golpe número "golpe" de una seguidilla (1, 2, 3...) es el tajo fuerte.</summary>
    public static bool EsTajoFuerte(int golpe, int golpesDelCombo) => golpesDelCombo > 1 && golpe > 0 && golpe % golpesDelCombo == 0;
}
