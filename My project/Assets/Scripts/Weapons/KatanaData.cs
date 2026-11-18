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

    [Header("En la mano de los demás (tercera persona)")]
    [Tooltip("Ya se ajustó con el muñeco espejo (Riftwalker > Muñeco espejo, manija \"Katana\"). Sin ajustar, va como el cuchillo.")]
    public bool ajustadaEnLaMano;
    [Tooltip("Dónde va el mango respecto de la mano derecha (metros, en el espacio del hueso de la mano).")]
    public Vector3 lugarEnLaMano;
    [Tooltip("Giro de la katana respecto de la mano derecha (grados).")]
    public Vector3 giroEnLaMano;
    [Tooltip("Dónde agarra la mano izquierda el mango, en el espacio de la katana (metros: z hacia la punta; negativo, más abajo que la derecha).")]
    public Vector3 manoIzquierdaEnElMango = new Vector3(0f, 0f, -0.09f);
    [Tooltip("Giro de la muñeca izquierda en el mango (grados, respecto de hacia dónde mira el personaje).")]
    public Vector3 giroManoIzquierda = new Vector3(-29.2f, 0f, 13.16f);

    // Los puntos de la mano derecha en tercera persona, como los del cuchillo (AgarresTerceraPersona) pero propios: así
    // ajustar la katana con el muñeco espejo no cambia el cuchillo. Metros respecto del pecho y de hacia dónde mira.
    [Tooltip("Dónde va la mano derecha en guardia.")]
    public Vector3 manoDerecha = new Vector3(0.143f, -0.0547f, 0.2948f);
    [Tooltip("Giro de la muñeca derecha en guardia (grados).")]
    public Vector3 giroManoDerecha = new Vector3(-48.13f, 0f, 0f);
    [Tooltip("Adónde va la mano derecha al prepararse para el tajo.")]
    public Vector3 manoAtras = new Vector3(0.196f, -0.089f, 0.222f);
    public Vector3 giroManoAtras;
    [Tooltip("Dónde termina la mano derecha el tajo.")]
    public Vector3 manoCorte = new Vector3(0.394f, -0.08f, 0.251f);
    public Vector3 giroManoCorte;
    [Tooltip("Color de la energía (la estela, los destellos y el brillo del filo).")]
    [ColorUsage(false, true)] public Color energia = new Color(0.45f, 0.85f, 1.6f);

    /// <summary>La katana del juego (Resources/Katana/KatanaDatos), o null.</summary>
    public static KatanaData Cargar()
    {
        if (cargada == null) cargada = Resources.Load<KatanaData>("Katana/KatanaDatos");
        return cargada;
    }
    private static KatanaData cargada;

    /// <summary>CA9: dónde va la katana (su mango) con la mano derecha ahí. Sin ajustar, en el lugar del cuchillo y
    /// apuntando como él.</summary>
    public void PoseEnLaMano(Transform mano, out Vector3 lugar, out Quaternion giro)
    {
        if (ajustadaEnLaMano)
        {
            lugar = mano.position + mano.rotation * lugarEnLaMano;
            giro = mano.rotation * Quaternion.Euler(giroEnLaMano);
            return;
        }
        AgarresTerceraPersona.Actual.PoseDeCuchillo(mano, out lugar, out giro);
        giro *= Quaternion.Euler(-45f, -5f, -6f); // como el cuchillo en su modelo de primera persona
    }

    /// <summary>Pasa al ajuste dónde quedó la manija de la katana (muñeco espejo).</summary>
    public void DesdeManija(Transform mano, Transform manija)
    {
        Quaternion inversa = Quaternion.Inverse(mano.rotation);
        lugarEnLaMano = inversa * (manija.position - mano.position);
        Vector3 e = (inversa * manija.rotation).eulerAngles;
        giroEnLaMano = new Vector3(Mathf.DeltaAngle(0f, e.x), Mathf.DeltaAngle(0f, e.y), Mathf.DeltaAngle(0f, e.z));
        escalaEnTerceraPersona = Mathf.Clamp(manija.localScale.x, 0.5f, 2f);
        ajustadaEnLaMano = true;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    /// <summary>CA9: la katana para la mano de los demás (y el muñeco espejo): su malla y el filo de energía, en su
    /// propio espacio (la hoja hacia +z), con el mango en el origen y a su tamaño (escalaEnTerceraPersona).</summary>
    public GameObject Crear(string nombre)
    {
        Transform modelo = viewModelPrefab != null ? viewModelPrefab.transform.Find("Modelo") : null;
        MeshFilter filtro = modelo != null ? modelo.GetComponent<MeshFilter>() : null;
        MeshRenderer vista = modelo != null ? modelo.GetComponent<MeshRenderer>() : null;
        var raiz = new GameObject(nombre);
        raiz.SetActive(false); // (el filo se arma al prenderla, con su ajuste)
        if (filtro == null || vista == null) return raiz;
        var hoja = new GameObject("Modelo");
        hoja.transform.SetParent(raiz.transform, false);
        hoja.AddComponent<MeshFilter>().sharedMesh = filtro.sharedMesh;
        MeshRenderer r = hoja.AddComponent<MeshRenderer>();
        r.sharedMaterials = vista.sharedMaterials;
        Transform agarre = modelo.Find("Agarre");
        float e = escalaEnTerceraPersona > 0f ? escalaEnTerceraPersona : 1f;
        hoja.transform.localScale = Vector3.one * e;
        hoja.transform.localPosition = -(agarre != null ? agarre.localPosition : Vector3.zero) * e;
        FiloDeEnergia filo = viewModelPrefab.GetComponent<FiloDeEnergia>();
        if (filo != null) JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(filo), raiz.AddComponent<FiloDeEnergia>());
        return raiz;
    }

    /// <summary>CA4: si el golpe número "golpe" de una seguidilla (1, 2, 3...) es el tajo fuerte.</summary>
    public static bool EsTajoFuerte(int golpe, int golpesDelCombo) => golpesDelCombo > 1 && golpe > 0 && golpe % golpesDelCombo == 0;
}
