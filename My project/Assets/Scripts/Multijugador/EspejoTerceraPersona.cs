using UnityEngine;

// Muñeco espejo (solo en el editor, para ajustar F26 sin otro jugador): una copia del cuerpo del jugador de esta
// computadora como la verían los demás jugadores, con la misma arma en la mano (ArmaEnTerceraPersona), que la dispara,
// la recarga y la cambia igual que él.
// Aparece a 2,5 m adelante del jugador, mirándolo, y queda parado ahí (para recorrerlo y verlo desde todos lados),
// pero apunta como el jugador: si él mira arriba o abajo, el muñeco también, y si gira a un costado, el muñeco gira
// hacia el mismo lado. En el Inspector de este componente: sin "Seguir la mira" queda mirando fijo ("Mirar arriba o
// abajo" lo inclina), "Copiar movimientos" además lo hace caminar, saltar y agacharse siguiendo al jugador, y "Traer
// adelante" lo vuelve a poner adelante del jugador.
// Manijas: con "Ajustar con manijas" aparecen, en la vista Scene, tres manijas (hijas del muñeco): "Arma", "Mano
// derecha" y "Mano izquierda". Se seleccionan (en la jerarquía o haciendo clic en la bolita) y se mueven y giran con
// las herramientas de siempre (W y E); la del arma también se escala (R) para cambiar el tamaño. Lo que se mueva pasa
// al ajuste de esa arma (lugar, giro, escala y las manos) y queda guardado. Un arma sin ajuste se agrega con "Agregar
// el arma en la mano".
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
    [Tooltip("Apunta como el jugador (arriba, abajo y a los costados), sin moverse de su lugar.")]
    public bool seguirLaMira = true;
    [Tooltip("Sin seguir la mira: hacia dónde mira el muñeco (grados: negativo arriba, positivo abajo).")]
    [Range(-80f, 80f)] public float mirarArribaOAbajo = 0f;
    [Tooltip("Prendido, camina, salta, se agacha y mira como el jugador (y lo sigue a 2,5 m).")]
    public bool copiarMovimientos;
    [Tooltip("Lo vuelve a poner adelante del jugador, mirándolo.")]
    public bool traerAdelante;
    [Tooltip("Muestra las manijas del arma y de las manos para moverlas en la vista Scene.")]
    public bool ajustarConManijas = true;
    [Tooltip("Agrega a los ajustes el arma que tiene en la mano, si todavía no tiene (empieza como La Porteña).")]
    public bool agregarArmaEnMano;

    private ManijaDeAjuste manijaArma, manijaDerecha, manijaIzquierda;
    private Vector3[] ultimoLugar = new Vector3[3];
    private Quaternion[] ultimoGiro = new Quaternion[3];
    private Vector3 ultimaEscala;

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
#if UNITY_EDITOR
        // Lo ajustado queda escrito en el asset al salir de Play (si no, Unity lo guarda recién al guardar el proyecto).
        AgarresTerceraPersona lista = AgarresTerceraPersona.Actual;
        if (lista != null && UnityEditor.EditorUtility.IsDirty(lista)) UnityEditor.AssetDatabase.SaveAssetIfDirty(lista);
#endif
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

    private float giroInicialJugador, giroInicialMunieco;

    private void Ubicar()
    {
        Vector3 adelante = jugador.forward;
        adelante.y = 0f;
        adelante = adelante.sqrMagnitude > 0.001f ? adelante.normalized : Vector3.forward;
        raiz.SetPositionAndRotation(jugador.position + adelante * Distancia, Quaternion.LookRotation(-adelante));
        giroInicialJugador = jugador.eulerAngles.y;
        giroInicialMunieco = raiz.eulerAngles.y;
    }

    private void Update()
    {
        if (raiz == null)
        {
            if (Time.time < listoEn || !BuscarJugador()) return;
            Armar();
        }
        if (jugador == null || original == null) { Destroy(gameObject); return; }
        if (traerAdelante) { traerAdelante = false; Ubicar(); }

        if (copiarMovimientos)
        {
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
        }
        else
        {
            if (seguirLaMira)
            {
                // Parado en su lugar, pero apunta como el jugador: gira a los costados lo mismo que él y mira arriba o abajo igual.
                float giro = Mathf.DeltaAngle(giroInicialJugador, jugador.eulerAngles.y);
                raiz.rotation = Quaternion.Euler(0f, giroInicialMunieco + giro, 0f);
                ojos.localRotation = Quaternion.Euler(Mathf.DeltaAngle(0f, camara.transform.localEulerAngles.x), 0f, 0f);
            }
            // Quieto, mirando fijo.
            else ojos.localRotation = Quaternion.Euler(mirarArribaOAbajo, 0f, 0f);
            foreach (AnimatorControllerParameter p in copia.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Float) copia.SetFloat(p.nameHash, 0f);
                else if (p.type == AnimatorControllerParameterType.Bool) copia.SetBool(p.nameHash, false);
            }
        }

        if (agregarArmaEnMano) { agregarArmaEnMano = false; AgregarArma(); }
        LeerManijas();

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
        arma.Balas = datos != null ? datos.Ammo : 6;
    }

    // Un arma sin ajuste todavía: como la ven hoy los demás, en la mano derecha y apuntando hacia donde mira.
    private void LateUpdate()
    {
        PonerManijas();
        if (arma == null || arma.Manejando || modeloEnMano == null) return;
        Transform mano = copia.GetBoneTransform(HumanBodyBones.RightHand);
        if (mano != null) soporte.position = mano.position;
        soporte.rotation = ojos.rotation;
    }

    // ---------- Manijas ----------

    private ManijaDeAjuste Manija(string nombre, Color color, float tamano)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(raiz, false);
        ManijaDeAjuste m = go.AddComponent<ManijaDeAjuste>();
        m.color = color;
        m.tamano = tamano;
        return m;
    }

    // Lo que se movió con las manijas desde el cuadro anterior pasa al ajuste del arma.
    private void LeerManijas()
    {
        if (manijaArma == null || arma == null || !arma.Manejando) return;
        Transform a = manijaArma.transform, d = manijaDerecha.transform, i = manijaIzquierda.transform;
        bool movioArma = Movida(a, 0) || (a.localScale - ultimaEscala).sqrMagnitude > 1e-8f;
        bool movioDer = Movida(d, 1), movioIzq = Movida(i, 2);
        if (!movioArma && !movioDer && !movioIzq) return;
        arma.DesdeManijas(a, d, i, movioArma, movioDer, movioIzq);
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(AgarresTerceraPersona.Actual); // así queda guardado en el asset
#endif
    }

    private bool Movida(Transform t, int cual) =>
        (t.position - ultimoLugar[cual]).sqrMagnitude > 1e-8f || Quaternion.Angle(t.rotation, ultimoGiro[cual]) > 0.01f;

    // Las manijas van donde está el arma quieta y las manos, según el ajuste (también si se cambió en el Inspector).
    private void PonerManijas()
    {
        bool mostrar = ajustarConManijas && arma != null && arma.Manejando;
        if (!mostrar)
        {
            if (manijaArma != null)
            {
                Destroy(manijaArma.gameObject);
                Destroy(manijaDerecha.gameObject);
                Destroy(manijaIzquierda.gameObject);
                manijaArma = manijaDerecha = manijaIzquierda = null;
            }
            return;
        }
        bool nuevas = manijaArma == null;
        if (nuevas)
        {
            manijaArma = Manija("Manija: Arma", Color.yellow, 0.05f);
            manijaDerecha = Manija("Manija: Mano derecha", Color.cyan, 0.035f);
            manijaIzquierda = Manija("Manija: Mano izquierda", Color.magenta, 0.035f);
        }
        if (!arma.PoseQuieta(out Vector3 p, out Quaternion g, out float escala, out Vector3 dp, out Quaternion dg,
                out Vector3 ip, out Quaternion ig)) return;
        Fijar(manijaArma.transform, 0, p, g);
        manijaArma.transform.localScale = ultimaEscala = Vector3.one * escala;
        Fijar(manijaDerecha.transform, 1, dp, dg);
        Fijar(manijaIzquierda.transform, 2, ip, ig);
#if UNITY_EDITOR
        if (nuevas) UnityEditor.Selection.activeGameObject = manijaArma.gameObject;
#endif
    }

    private void Fijar(Transform t, int cual, Vector3 lugar, Quaternion giro)
    {
        t.SetPositionAndRotation(lugar, giro);
        ultimoLugar[cual] = lugar;
        ultimoGiro[cual] = giro;
    }

    // Agrega el arma en la mano a los ajustes, con los valores de la Porteña (o los de siempre).
    private void AgregarArma()
    {
        if (enMano == null) return;
        AgarresTerceraPersona lista = AgarresTerceraPersona.Actual;
        if (lista.Buscar(enMano) != null) return;
        AgarresTerceraPersona.Agarre molde = lista.armas.Count > 0 ? lista.armas[0] : null;
        var nuevo = molde != null ? JsonUtility.FromJson<AgarresTerceraPersona.Agarre>(JsonUtility.ToJson(molde))
                                  : new AgarresTerceraPersona.Agarre();
        nuevo.arma = enMano.name;
        nuevo.nombre = enMano.name;
        nuevo.manoDerecha = nuevo.manoIzquierda = nuevo.giroManoDerecha = nuevo.giroManoIzquierda = Vector3.zero;
        lista.armas.Add(nuevo);
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(lista);
#endif
        GameObject sale = enMano;
        arma.Cambiar(() => Mostrar(sale)); // la vuelve a poner, ahora con su ajuste
    }

    // El arma de primera persona como viene del prefab, en el mismo lugar: la de la mano puede tener piezas ya separadas
    // por la animación de primera persona (el tambor de La Trochita), y la copia del muñeco las separa de nuevo.
    // "borrar": lo que hay que borrar después de copiarla (null si se usa la misma).
    private static GameObject Original(GameObject primeraPersona, out GameObject borrar)
    {
        borrar = null;
#if UNITY_EDITOR
        GameObject molde = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(primeraPersona);
        Transform padre = primeraPersona.transform.parent;
        if (molde == null || padre == null) return primeraPersona;
        var apagado = new GameObject("(copiando arma)");
        apagado.SetActive(false); // así no arranca ningún script de la copia
        apagado.transform.SetParent(padre, false);
        GameObject copia = Instantiate(molde, apagado.transform);
        copia.name = primeraPersona.name;
        Transform t = primeraPersona.transform;
        copia.transform.localPosition = t.localPosition;
        copia.transform.localRotation = t.localRotation;
        copia.transform.localScale = t.localScale;
        borrar = apagado;
        return copia;
#else
        return primeraPersona;
#endif
    }

    private void Mostrar(GameObject primeraPersona)
    {
        if (modeloEnMano != null) Destroy(modeloEnMano);
        modeloEnMano = null;
        if (primeraPersona != null)
        {
            ShopItem ficha = WeaponSwitcher.FichaDe(primeraPersona);
            float largo = ficha != null && ficha.realLength > 0f ? ficha.realLength : ModeloReal.LargoSinFicha;
            modeloEnMano = ModeloReal.Crear(Original(primeraPersona, out GameObject borrar), largo, out Bounds _, primeraPersona.name + " (espejo)");
            if (borrar != null) Destroy(borrar);
            modeloEnMano.transform.SetParent(soporte, false);
            foreach (Transform t in modeloEnMano.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
        }
        float sacar = WeaponSwitcher.FichaDe(primeraPersona) is ShopItem f && f.equipTime > 0f ? Mathf.Min(f.equipTime, 0.6f) : 0.3f;
        arma.Poner(modeloEnMano, primeraPersona, sacar);
    }
}
