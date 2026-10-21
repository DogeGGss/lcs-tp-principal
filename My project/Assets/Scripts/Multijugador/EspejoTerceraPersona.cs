using UnityEngine;

// Muñeco espejo (solo en el editor, para ajustar F26 sin otro jugador): una copia del cuerpo del jugador de esta
// computadora, parada a 2,5 m adelante y mirándolo, que hace lo mismo que él como lo verían los demás jugadores:
// camina, salta y se agacha igual, mira arriba y abajo igual, tiene la misma arma en la mano (ArmaEnTerceraPersona) y
// la dispara y recarga igual.
// Se prende y apaga con Riftwalker > Muñeco espejo (tercera persona). Con el juego andando, los ajustes de cada arma
// se cambian en el asset Resources/AgarresTerceraPersona (el campo "Agarres" de este componente lo muestra): se ven al
// momento en el muñeco y quedan guardados.
[DefaultExecutionOrder(40)] // antes de ArmaEnTerceraPersona
public class EspejoTerceraPersona : MonoBehaviour
{
    public const string Preferencia = "Riftwalker.MuniecoEspejo";
    private const float Distancia = 2.5f;

    [Tooltip("Los ajustes de cada arma en tercera persona: se cambian acá con el juego andando y quedan guardados.")]
    public AgarresTerceraPersona agarres;

    private Transform jugador;
    private Camera camara;
    private WeaponSwitcher switcher;
    private Animator original;
    private Transform raiz, ojos, soporte;
    private Animator copia;
    private ArmaEnTerceraPersona arma;
    private GameObject enMano, modeloEnMano;
    private float listoEn;

    /// <summary>Lo prende o lo apaga con el juego andando (lo llama el menú).</summary>
    public static void Cambiar(bool prender)
    {
        EspejoTerceraPersona hay = FindAnyObjectByType<EspejoTerceraPersona>();
        if (!prender) { if (hay != null) Destroy(hay); return; }
        if (hay == null) new GameObject("Muñeco espejo (tercera persona)").AddComponent<EspejoTerceraPersona>();
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AlEmpezar()
    {
        if (UnityEditor.EditorPrefs.GetBool(Preferencia, false)) Cambiar(true);
    }
#endif

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        agarres = AgarresTerceraPersona.Actual;
        listoEn = Time.time + 1f; // que el jugador ya tenga el cuerpo de su personaje
        WeaponFire.Fired += AlDisparar;
    }

    private void OnDestroy()
    {
        WeaponFire.Fired -= AlDisparar;
        if (raiz != null) Destroy(raiz.gameObject);
        if (modeloEnMano != null) Destroy(modeloEnMano);
    }

    private void AlDisparar(Transform tirador, Vector3 origen, Vector3[] direcciones)
    {
        if (arma != null && tirador == jugador) arma.Disparo();
    }

    private bool BuscarJugador()
    {
        camara = Camera.main;
        switcher = camara != null ? camara.GetComponent<WeaponSwitcher>() : null;
        PlayerMovement movimiento = camara != null ? camara.GetComponentInParent<PlayerMovement>() : null;
        if (switcher == null || movimiento == null) return false;
        jugador = movimiento.transform;
        original = null;
        foreach (Animator a in jugador.GetComponentsInChildren<Animator>())
            if (a.isHuman) { original = a; break; }
        return original != null;
    }

    // La copia del cuerpo: el mismo modelo con su Animator, sin scripts ni choques, y dibujado entero (el del jugador
    // solo dibuja su sombra).
    private void Armar()
    {
        raiz = new GameObject("Muñeco espejo").transform;
        var apagado = new GameObject("(copiando)");
        apagado.SetActive(false);
        GameObject cuerpo = Instantiate(original.gameObject, apagado.transform);
        for (int vuelta = 0; vuelta < 2; vuelta++)
            foreach (MonoBehaviour m in cuerpo.GetComponentsInChildren<MonoBehaviour>(true)) if (m != null) DestroyImmediate(m);
        foreach (Collider c in cuerpo.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        foreach (Rigidbody rb in cuerpo.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(rb);
        foreach (Transform t in cuerpo.GetComponentsInChildren<Transform>(true))
            if (t != null && t != cuerpo.transform && (t.name.StartsWith("Brazos") || t.name.StartsWith("Contorno")))
                DestroyImmediate(t.gameObject);
        foreach (Renderer r in cuerpo.GetComponentsInChildren<Renderer>(true))
        {
            r.enabled = true;
            r.forceRenderingOff = false;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
        foreach (Transform t in cuerpo.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
        cuerpo.name = "Cuerpo";
        cuerpo.transform.SetParent(raiz, false);
        cuerpo.transform.localPosition = jugador.InverseTransformPoint(original.transform.position);
        cuerpo.transform.localRotation = Quaternion.Inverse(jugador.rotation) * original.transform.rotation;
        cuerpo.transform.localScale = original.transform.lossyScale;
        Destroy(apagado);

        ojos = new GameObject("Ojos").transform;
        ojos.SetParent(raiz, false);
        ojos.localPosition = jugador.InverseTransformPoint(camara.transform.position);
        soporte = new GameObject("Arma en la mano").transform;
        soporte.SetParent(raiz, false);

        copia = cuerpo.GetComponent<Animator>();
        copia.applyRootMotion = false;
        copia.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        arma = ArmaEnTerceraPersona.Crear(raiz.gameObject, copia, ojos);
        Ubicar();
    }

    private void Ubicar()
    {
        Vector3 adelante = jugador.forward;
        adelante.y = 0f;
        adelante = adelante.sqrMagnitude > 0.001f ? adelante.normalized : Vector3.forward;
        raiz.SetPositionAndRotation(jugador.position + adelante * Distancia, Quaternion.LookRotation(-adelante));
    }

    private void Update()
    {
        if (raiz == null)
        {
            if (Time.time < listoEn || !BuscarJugador()) return;
            Armar();
        }
        if (jugador == null || original == null) { Destroy(gameObject); return; }
        Ubicar();

        // Mira arriba o abajo como el jugador.
        float pitch = Mathf.DeltaAngle(0f, camara.transform.localEulerAngles.x);
        ojos.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        // Camina, corre, salta y se agacha como él.
        foreach (AnimatorControllerParameter p in original.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Float) copia.SetFloat(p.nameHash, original.GetFloat(p.nameHash));
            else if (p.type == AnimatorControllerParameterType.Bool) copia.SetBool(p.nameHash, original.GetBool(p.nameHash));
        }

        // La misma arma en la mano.
        GameObject ahora = switcher.HeldPrimary != null ? switcher.HeldPrimary : switcher.HeldSecondary;
        if (ahora != enMano)
        {
            enMano = ahora;
            GameObject sale = ahora;
            arma.Cambiar(() => Mostrar(sale));
        }
        IHudWeapon datos = enMano != null ? enMano.GetComponentInChildren<IHudWeapon>() : null;
        arma.Recarga = datos != null ? datos.ReloadProgress : -1f;
    }

    // Un arma sin ajuste todavía: como la ven hoy los demás, en la mano derecha y apuntando hacia donde mira.
    private void LateUpdate()
    {
        if (arma == null || arma.Manejando || modeloEnMano == null) return;
        Transform mano = copia.GetBoneTransform(HumanBodyBones.RightHand);
        if (mano != null) soporte.position = mano.position;
        soporte.rotation = ojos.rotation;
    }

    private void Mostrar(GameObject primeraPersona)
    {
        if (modeloEnMano != null) Destroy(modeloEnMano);
        modeloEnMano = null;
        if (primeraPersona != null)
        {
            ShopItem ficha = WeaponSwitcher.FichaDe(primeraPersona);
            float largo = ficha != null && ficha.realLength > 0f ? ficha.realLength : ModeloReal.LargoSinFicha;
            modeloEnMano = ModeloReal.Crear(primeraPersona, largo, out Bounds _, primeraPersona.name + " (espejo)");
            modeloEnMano.transform.SetParent(soporte, false);
            foreach (Transform t in modeloEnMano.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
        }
        float sacar = WeaponSwitcher.FichaDe(primeraPersona) is ShopItem f && f.equipTime > 0f ? Mathf.Min(f.equipTime, 0.6f) : 0.3f;
        arma.Poner(modeloEnMano, primeraPersona, sacar);
    }
}
