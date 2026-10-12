using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Brazos de primera persona pegados a la cámara: uno por arma de fuego, otro para el cuchillo y otro para las granadas.
// Los brazos del cuerpo (los que arma HideOwnBody) cuelgan de los hombros, que no se inclinan al mirar arriba o abajo:
// el IK no llega al arma y se estiran o desaparecen según el ángulo. Acá, por cada arma, se saca una "foto" de esos
// brazos agarrándola como si la cámara mirara derecho y se pega a la cámara, así se ven igual en cualquier ángulo, como
// en los demás FPS. Las fotos se sacan todas al empezar, sin que se vean los brazos del cuerpo; con un arma en la mano
// se ve su foto.
// Cada arma dice dónde van las manos con dos hijos, Gun_ref_rightHand y Gun_ref_leftHand (los de la Línea A son los
// objetivos del IK de los brazos). Para la foto de otra arma, el IK se lleva a sus agarres; después se corren arma y
// brazos juntos (Corrimiento). El brazo derecho va aparte en la foto y sigue al arma si se mueve (la puñalada del
// cuchillo); el izquierdo queda quieto. El arma y sus agarres se ubican a mano en Play, con "Ajustar a mano" (US 173).
// WeaponSwitcher lo agrega solo; si el jugador no tiene brazos de primera persona, no hace nada.
[DefaultExecutionOrder(100)] // después del LateUpdate de HideOwnBody, que prende los brazos del cuerpo
public class BrazosEnCamara : MonoBehaviour
{
    public const string AgarreDerecho = "Gun_ref_rightHand", AgarreIzquierdo = "Gun_ref_leftHand";
    private const string NombreBrazos = "Brazos (primera persona)"; // los que crea HideOwnBody
    private const int CuadrosAlEmpezar = 3; // que el IK ya esté armado antes de la primera foto
    private const int CuadrosParaElIK = 2;  // cuadros entre mover los objetivos del IK y sacar la foto
    // Con la foto, arma y brazos van 12 cm más adelante: la cámara de las armas dibuja el arma entera y quedaba
    // encima de la cara. Pasando los 16 cm se empieza a ver dónde termina el brazo izquierdo, abajo al centro.
    private static readonly Vector3 Corrimiento = new Vector3(0f, 0f, 0.12f);

    [Tooltip("Para ubicar a mano, en Play, el arma en la mano y sus agarres (Gun_ref_rightHand y Gun_ref_leftHand). " +
             "Prendido, en vez de la foto se ven los brazos en vivo agarrando el arma, tal como va a quedar la foto: " +
             "al mover el arma o los agarres se ve al momento. Hay que mirar derecho. Al apagarlo se sacan las fotos de nuevo.")]
    public bool ajustarAMano;

    private WeaponSwitcher switcher;
    private HealthSystem vida;
    private Transform camara;
    private SkinnedMeshRenderer brazosCuerpo;
    private Transform objetivoDerecho, objetivoIzquierdo; // los del IK: los agarres de la Línea A
    private Vector3 derechoLocal, izquierdoLocal;
    private Quaternion derechoGiro, izquierdoGiro;
    private int capa;
    private Animator animador;
    private AnimatorCullingMode cullingOriginal;

    private readonly Dictionary<GameObject, MeshRenderer> fotos = new Dictionary<GameObject, MeshRenderer>();
    private readonly Dictionary<GameObject, MeshRenderer> brazoDerecho = new Dictionary<GameObject, MeshRenderer>(); // hijo de la foto
    private readonly Dictionary<GameObject, Vector3> armaEnFoto = new Dictionary<GameObject, Vector3>(); // dónde estaba el arma al sacar su foto
    private bool[] verticeDerecho; // qué vértices de los brazos son del brazo derecho
    private readonly Dictionary<GameObject, Vector3> lugarOriginal = new Dictionary<GameObject, Vector3>();
    private GameObject preparando;
    private int fotoEnCuadro, listoEnCuadro;
    private bool faltanFotos;

    // Ajuste a mano
    private bool ajustando;
    private bool prestados; // los objetivos del IK están en los agarres de otra arma
    private GameObject ajustada;
    private int capaBrazos;
    private Transform camaraArmas;
    private Vector3 camaraArmasLugar;

    private void Awake()
    {
        switcher = GetComponentInParent<WeaponSwitcher>();
        vida = GetComponentInParent<HealthSystem>();
        camara = switcher != null ? switcher.transform : transform;
        capa = LayerMask.NameToLayer("ArmaEnMano");
#if UNITY_EDITOR
        ajustarAMano = UnityEditor.EditorPrefs.GetBool(PreferenciaAjuste, false); // sigue prendido entre un Play y otro
#endif
    }

    private void LateUpdate()
    {
        if (switcher == null || switcher.pistolObj == null) return;
        if (brazosCuerpo == null && !Preparar()) return;

        bool vivo = vida == null || vida.currentHealth > 0;
        GameObject enMano = vivo ? EnMano() : null;

        if (ajustarAMano != ajustando)
        {
#if UNITY_EDITOR
            UnityEditor.EditorPrefs.SetBool(PreferenciaAjuste, ajustarAMano);
#endif
            if (ajustarAMano) EmpezarAjuste(); else TerminarAjuste();
            if (brazosCuerpo == null) return;
        }
        if (ajustando)
        {
            Ajustar(enMano);
            return;
        }

        // Las fotos se sacan al empezar, una tras otra, primero la del arma en la mano.
        if (preparando != null)
        {
            LlevarIKDerecho(preparando); // la pose sigue siendo la de la cámara derecha aunque el jugador mire a otro lado
            if (Time.frameCount >= fotoEnCuadro) SacarFoto(preparando);
        }
        else if (Time.frameCount >= listoEnCuadro)
        {
            GameObject falta = enMano != null && !fotos.ContainsKey(enMano) && TieneAgarres(enMano) ? enMano : null;
            if (falta == null && faltanFotos)
            {
                falta = SinFoto();
                faltanFotos = falta != null;
            }
            if (falta != null) PrepararFoto(falta);
        }

        foreach (KeyValuePair<GameObject, MeshRenderer> foto in fotos)
        {
            bool ver = foto.Key == enMano;
            if (foto.Value != null) foto.Value.enabled = ver;
            if (brazoDerecho.TryGetValue(foto.Key, out MeshRenderer derecho) && derecho != null) derecho.enabled = ver;
        }

        // Los brazos del cuerpo no se ven nunca sueltos: mientras se saca una foto siguen prendidos para el IK, pero sin
        // dibujarse, y con un arma que todavía no tiene la suya tampoco se dibujan. Con su foto, se apagan (HideOwnBody los
        // vuelve a prender; acá se apagan después). Solo se ven sin un arma con agarres en la mano.
        bool conFoto = enMano != null && fotos.ContainsKey(enMano);
        if (preparando != null) brazosCuerpo.enabled = true;
        else if (conFoto) brazosCuerpo.enabled = false;
        brazosCuerpo.forceRenderingOff = preparando != null || (enMano != null && !conFoto && TieneAgarres(enMano));

        // El brazo que agarra el arma la acompaña si se movió desde la foto (la puñalada del cuchillo la lleva para adelante).
        if (conFoto && preparando == null)
        {
            MeshRenderer vista = fotos[enMano];
            Vector3 movida = enMano.transform.localPosition - armaEnFoto[enMano];
            if (movida != Vector3.zero)
            {
                Transform brazo = brazoDerecho.TryGetValue(enMano, out MeshRenderer derecho) && derecho != null ? derecho.transform : vista.transform;
                brazo.position += camara.TransformVector(movida);
                armaEnFoto[enMano] = enMano.transform.localPosition;
            }
        }
    }

    // El arma de fuego en la mano, la granada (el lugar donde se arma, con sus agarres) o el cuchillo.
    private GameObject EnMano()
    {
        if (switcher.HeldPrimary != null) return switcher.HeldPrimary;
        if (switcher.HeldSecondary != null) return switcher.HeldSecondary;
        if (switcher.Granadas != null && switcher.Granadas.IsHolding)
        {
            Transform granada = camara.Find(GrenadeThrower.NombreMano);
            if (granada != null) return granada.gameObject;
        }
        GameObject cuchillo = switcher.meleeScript != null ? switcher.meleeScript.CurrentViewModel : null;
        return cuchillo != null && cuchillo.activeInHierarchy ? cuchillo : null;
    }

    // Un arma con agarres (hija de la cámara) que todavía no tiene foto.
    private GameObject SinFoto()
    {
        foreach (Transform hijo in camara)
            if (!fotos.ContainsKey(hijo.gameObject) && TieneAgarres(hijo.gameObject)) return hijo.gameObject;
        return null;
    }

    // Busca los brazos de primera persona y los objetivos del IK (los agarres de la Línea A).
    private bool Preparar()
    {
        Transform raiz = vida != null ? vida.transform : transform.root;
        foreach (SkinnedMeshRenderer r in raiz.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (r.name == NombreBrazos) brazosCuerpo = r;
        objetivoDerecho = switcher.pistolObj.transform.Find(AgarreDerecho);
        objetivoIzquierdo = switcher.pistolObj.transform.Find(AgarreIzquierdo);
        if (brazosCuerpo == null || objetivoDerecho == null || objetivoIzquierdo == null) { brazosCuerpo = null; return false; }
        derechoLocal = objetivoDerecho.localPosition;
        derechoGiro = objetivoDerecho.localRotation;
        izquierdoLocal = objetivoIzquierdo.localPosition;
        izquierdoGiro = objetivoIzquierdo.localRotation;
        if (capa < 0) capa = brazosCuerpo.gameObject.layer;
        animador = brazosCuerpo.GetComponentInParent<Animator>();
        if (animador != null) cullingOriginal = animador.cullingMode;
        verticeDerecho = VerticesDelBrazoDerecho();
        listoEnCuadro = Time.frameCount + CuadrosAlEmpezar;
        faltanFotos = true;
        return true;
    }

    // Un vértice es del brazo derecho si se mueve sobre todo con sus huesos (del brazo a los dedos). Sin esqueleto
    // humanoide o sin poder leer la malla, null: la foto queda entera.
    private bool[] VerticesDelBrazoDerecho()
    {
        Mesh malla = brazosCuerpo.sharedMesh;
        if (animador == null || !animador.isHuman || malla == null || !malla.isReadable) return null;
        Transform brazo = animador.GetBoneTransform(HumanBodyBones.RightUpperArm);
        if (brazo == null) return null;
        Transform[] huesos = brazosCuerpo.bones;
        var esDerecho = new bool[huesos.Length];
        for (int i = 0; i < huesos.Length; i++) esDerecho[i] = huesos[i] != null && huesos[i].IsChildOf(brazo);
        BoneWeight[] pesos = malla.boneWeights;
        var vertices = new bool[pesos.Length];
        for (int v = 0; v < pesos.Length; v++)
        {
            BoneWeight p = pesos[v];
            float peso = (esDerecho[p.boneIndex0] ? p.weight0 : 0f) + (esDerecho[p.boneIndex1] ? p.weight1 : 0f)
                       + (esDerecho[p.boneIndex2] ? p.weight2 : 0f) + (esDerecho[p.boneIndex3] ? p.weight3 : 0f);
            vertices[v] = peso >= 0.5f;
        }
        return vertices;
    }

    // Pasa los triángulos del brazo derecho de "malla" a una malla aparte, que devuelve; en "malla" queda el izquierdo.
    private Mesh SepararBrazoDerecho(Mesh malla)
    {
        if (verticeDerecho == null || verticeDerecho.Length != malla.vertexCount) return null;
        Mesh derecha = Instantiate(malla);
        derecha.name = malla.name + " (brazo derecho)";
        var izquierdos = new List<int>();
        var derechos = new List<int>();
        for (int s = 0; s < malla.subMeshCount; s++)
        {
            int[] triangulos = malla.GetTriangles(s);
            izquierdos.Clear();
            derechos.Clear();
            for (int t = 0; t < triangulos.Length; t += 3)
            {
                List<int> lado = verticeDerecho[triangulos[t]] ? derechos : izquierdos;
                lado.Add(triangulos[t]);
                lado.Add(triangulos[t + 1]);
                lado.Add(triangulos[t + 2]);
            }
            malla.SetTriangles(izquierdos, s);
            derecha.SetTriangles(derechos, s);
        }
        return derecha;
    }

    private static bool TieneAgarres(GameObject arma) =>
        arma.transform.Find(AgarreDerecho) != null && arma.transform.Find(AgarreIzquierdo) != null;

    // La cámara en su lugar pero sin inclinación, mirando derecho.
    private Matrix4x4 CamaraDerecha()
    {
        Vector3 giro = camara.localEulerAngles;
        Matrix4x4 local = Matrix4x4.TRS(camara.localPosition, Quaternion.Euler(0f, giro.y, giro.z), camara.localScale);
        return camara.parent != null ? camara.parent.localToWorldMatrix * local : local;
    }

    // Lleva el IK a los agarres del arma, que queda donde está (se ubica a mano); la foto sale unos cuadros después.
    // Los brazos del cuerpo quedan prendidos para el IK pero no se dibujan, y el Animator anima aunque no se vean.
    private void PrepararFoto(GameObject arma)
    {
        if (!lugarOriginal.ContainsKey(arma)) lugarOriginal[arma] = arma.transform.localPosition;
        arma.transform.localPosition = lugarOriginal[arma];
        LlevarIKDerecho(arma);
        brazosCuerpo.enabled = true;
        brazosCuerpo.forceRenderingOff = true;
        if (animador != null) animador.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        preparando = arma;
        fotoEnCuadro = Time.frameCount + CuadrosParaElIK;
    }

    // Los objetivos del IK van a los agarres del arma como si la cámara mirara derecho: los hombros no se inclinan con
    // la cámara, así la foto sale igual mire donde mire el jugador. De la Línea A valen sus agarres guardados, porque
    // son los mismos objetivos.
    private void LlevarIKDerecho(GameObject arma)
    {
        Matrix4x4 derecha = CamaraDerecha();
        bool lineaA = arma == switcher.pistolObj;
        Transform derecho = arma.transform.Find(AgarreDerecho), izquierdo = arma.transform.Find(AgarreIzquierdo);
        Ubicar(objetivoDerecho, arma.transform, lineaA ? derechoLocal : derecho.localPosition, lineaA ? derechoGiro : derecho.localRotation, derecha);
        Ubicar(objetivoIzquierdo, arma.transform, lineaA ? izquierdoLocal : izquierdo.localPosition, lineaA ? izquierdoGiro : izquierdo.localRotation, derecha);
    }

    // Pone "objetivo" donde estaría el agarre (lugar y giro dentro del arma) con la cámara en "derecha".
    private void Ubicar(Transform objetivo, Transform arma, Vector3 lugar, Quaternion giro, Matrix4x4 derecha)
    {
        Vector3 enCamara = camara.InverseTransformPoint(arma.TransformPoint(lugar));
        Quaternion giroEnCamara = Quaternion.Inverse(camara.rotation) * arma.rotation * giro;
        objetivo.SetPositionAndRotation(derecha.MultiplyPoint3x4(enCamara), derecha.rotation * giroEnCamara);
    }

    // Copia fija de los brazos tal como están ahora (agarrando el arma), como hija de la cámara. Después, arma y
    // brazos se corren juntos y los objetivos del IK vuelven a la Línea A.
    private void SacarFoto(GameObject arma)
    {
        var malla = new Mesh { name = "Brazos con " + arma.name };
        brazosCuerpo.BakeMesh(malla, true); // con la escala: la copia queda a escala 1
        Mesh derecha = SepararBrazoDerecho(malla);

        // Los dibuja la cámara de las armas: la principal no dibuja nada a menos de 30 cm y la mano quedaba cortada.
        // El arma va a esa misma cámara, así la mano y el arma se tapan bien entre sí.
        var go = new GameObject("Brazos con " + arma.name + " (cámara)");
        go.layer = capa;
        foreach (Transform parte in arma.GetComponentsInChildren<Transform>(true)) parte.gameObject.layer = capa;
        // Los brazos agarran el arma como si la cámara mirara derecho: se pegan a la cámara desde esa pose.
        Matrix4x4 camaraDerecha = CamaraDerecha();
        go.transform.SetParent(camara, false);
        go.transform.localPosition = camaraDerecha.inverse.MultiplyPoint3x4(brazosCuerpo.transform.position) + Corrimiento;
        go.transform.localRotation = Quaternion.Inverse(camaraDerecha.rotation) * brazosCuerpo.transform.rotation;
        Vector3 escala = camara.lossyScale;
        go.transform.localScale = new Vector3(1f / escala.x, 1f / escala.y, 1f / escala.z);
        arma.transform.localPosition += Corrimiento;

        go.AddComponent<MeshFilter>().sharedMesh = malla;
        MeshRenderer foto = go.AddComponent<MeshRenderer>();
        foto.sharedMaterials = brazosCuerpo.sharedMaterials;
        foto.shadowCastingMode = ShadowCastingMode.Off; // la sombra la sigue tirando el cuerpo
        foto.lightProbeUsage = brazosCuerpo.lightProbeUsage;
        fotos[arma] = foto;
        armaEnFoto[arma] = arma.transform.localPosition;

        // El brazo derecho, hijo de la foto en el mismo lugar: el espectador copia la foto con sus hijos.
        if (derecha != null)
        {
            var brazo = new GameObject("Brazo derecho");
            brazo.layer = capa;
            brazo.transform.SetParent(go.transform, false);
            brazo.AddComponent<MeshFilter>().sharedMesh = derecha;
            MeshRenderer vistaBrazo = brazo.AddComponent<MeshRenderer>();
            vistaBrazo.sharedMaterials = brazosCuerpo.sharedMaterials;
            vistaBrazo.shadowCastingMode = ShadowCastingMode.Off;
            vistaBrazo.lightProbeUsage = brazosCuerpo.lightProbeUsage;
            brazoDerecho[arma] = vistaBrazo;
        }

        VolverALineaA();
        if (animador != null) animador.cullingMode = cullingOriginal;
        preparando = null;
    }

    // Los objetivos del IK (los agarres de la Línea A) van a los agarres de otra arma.
    private void LlevarIK(GameObject arma)
    {
        Transform derecho = arma.transform.Find(AgarreDerecho), izquierdo = arma.transform.Find(AgarreIzquierdo);
        objetivoDerecho.SetPositionAndRotation(derecho.position, derecho.rotation);
        objetivoIzquierdo.SetPositionAndRotation(izquierdo.position, izquierdo.rotation);
    }

    private void VolverALineaA()
    {
        objetivoDerecho.localPosition = derechoLocal;
        objetivoDerecho.localRotation = derechoGiro;
        objetivoIzquierdo.localPosition = izquierdoLocal;
        objetivoIzquierdo.localRotation = izquierdoGiro;
    }

    // Los agarres de la Línea A tal como están ahora (en el ajuste a mano se pueden haber movido).
    private void GuardarLineaA()
    {
        derechoLocal = objetivoDerecho.localPosition;
        derechoGiro = objetivoDerecho.localRotation;
        izquierdoLocal = objetivoIzquierdo.localPosition;
        izquierdoGiro = objetivoIzquierdo.localRotation;
    }

    // ---------- Ajuste a mano (US 173) ----------

    // Sin fotos, cada arma en su lugar sin el corrimiento, y los brazos del cuerpo dibujados por la cámara de las
    // armas, como la foto. Esa cámara se corre para atrás lo mismo que se corren arma y brazos con la foto: se ve igual.
    private void EmpezarAjuste()
    {
        TirarFotos();
        lugarOriginal.Clear(); // al terminar, el lugar de cada arma es donde haya quedado
        capaBrazos = brazosCuerpo.gameObject.layer;
        brazosCuerpo.gameObject.layer = capa;
        foreach (Camera c in camara.GetComponentsInChildren<Camera>(true))
            if (c.transform != camara && (c.cullingMask & (1 << capa)) != 0) { camaraArmas = c.transform; break; }
        if (camaraArmas != null)
        {
            camaraArmasLugar = camaraArmas.localPosition;
            camaraArmas.position -= camara.TransformVector(Corrimiento);
        }
        ajustada = null;
        ajustando = true;
    }

    // Cada cuadro: los brazos del cuerpo agarran en vivo el arma en la mano, por donde estén sus agarres.
    private void Ajustar(GameObject enMano)
    {
        bool otra = enMano != null && enMano != switcher.pistolObj && TieneAgarres(enMano);
        if (otra)
        {
            if (!prestados) GuardarLineaA();
            prestados = true;
            LlevarIK(enMano);
        }
        else if (prestados)
        {
            VolverALineaA();
            prestados = false;
        }
        if (enMano != ajustada && enMano != null)
            foreach (Transform parte in enMano.GetComponentsInChildren<Transform>(true)) parte.gameObject.layer = capa;
        ajustada = enMano;
        if (enMano != null) brazosCuerpo.enabled = true;
#if UNITY_EDITOR
        GuardarAjuste(false);
#endif
    }

    // Todo vuelve como estaba y se sacan las fotos de nuevo, con el arma y los agarres donde quedaron.
    private void TerminarAjuste()
    {
#if UNITY_EDITOR
        GuardarAjuste(true);
#endif
        if (prestados) VolverALineaA(); else GuardarLineaA();
        prestados = false;
        brazosCuerpo.gameObject.layer = capaBrazos;
        if (camaraArmas != null) camaraArmas.localPosition = camaraArmasLugar;
        ajustando = false;
        Rehacer();
    }

    /// <summary>US 133: la foto de los brazos agarrando esa arma, o null. El espectador la copia para mostrar la mano.</summary>
    public MeshRenderer FotoDe(GameObject arma) => arma != null && fotos.TryGetValue(arma, out MeshRenderer foto) ? foto : null;

    // Tira las fotos y vuelve cada arma a su lugar: se sacan de nuevo (por ejemplo, después de mover los agarres, o
    // al cambiar el cuerpo por el del personaje elegido, con los brazos nuevos que arma HideOwnBody.Rehacer).
    public void Rehacer()
    {
        TirarFotos();
        brazosCuerpo = null; // vuelve a tomar los agarres de la Línea A tal como estén ahora
    }

    private void TirarFotos()
    {
        foreach (KeyValuePair<GameObject, MeshRenderer> foto in fotos)
            if (foto.Value != null) Destroy(foto.Value.gameObject);
        fotos.Clear();
        brazoDerecho.Clear();
        armaEnFoto.Clear();
        foreach (KeyValuePair<GameObject, Vector3> lugar in lugarOriginal)
            if (lugar.Key != null) lugar.Key.transform.localPosition = lugar.Value;
        if (objetivoDerecho != null) VolverALineaA();
        if (animador != null && preparando != null) animador.cullingMode = cullingOriginal;
        if (brazosCuerpo != null) brazosCuerpo.forceRenderingOff = false;
        preparando = null;
    }

#if UNITY_EDITOR
    // ---------- Guardado del ajuste (solo en el editor) ----------
    // Lo que se mueve en Play se pierde al salir, o si Unity se cierra. Mientras se ajusta, cada medio segundo se guarda
    // dónde quedó cada arma con sus agarres; AjusteDeManos (Assets/Editor) lo pasa al Player.prefab al salir del Play.

    public const string ArchivoAjuste = "Library/AjusteDeManos.json";
    public const string PreferenciaAjuste = "Riftwalker.AjustarManos";

    [System.Serializable]
    public class Agarres
    {
        public string arma;
        public Vector3 lugar, escala, derecho, izquierdo;
        public Quaternion giro, derechoGiro, izquierdoGiro;
    }

    [System.Serializable]
    public class Ajuste
    {
        public bool aplicado; // ya está en el prefab
        public List<Agarres> armas = new List<Agarres>();
    }

    private float guardado = -10f;
    private string ultimoGuardado;

    private void GuardarAjuste(bool ya)
    {
        if (!ya && Time.unscaledTime - guardado < 0.5f) return;
        guardado = Time.unscaledTime;
        var ajuste = new Ajuste();
        foreach (Transform arma in camara)
        {
            Transform derecho = arma.Find(AgarreDerecho), izquierdo = arma.Find(AgarreIzquierdo);
            if (derecho == null || izquierdo == null) continue;
            // Los agarres de la Línea A pueden estar prestados a otra arma: valen los guardados.
            bool prestado = prestados && arma.gameObject == switcher.pistolObj;
            ajuste.armas.Add(new Agarres
            {
                arma = arma.name,
                lugar = arma.localPosition,
                giro = arma.localRotation,
                escala = arma.localScale,
                derecho = prestado ? derechoLocal : derecho.localPosition,
                derechoGiro = prestado ? derechoGiro : derecho.localRotation,
                izquierdo = prestado ? izquierdoLocal : izquierdo.localPosition,
                izquierdoGiro = prestado ? izquierdoGiro : izquierdo.localRotation
            });
        }
        string json = JsonUtility.ToJson(ajuste, true);
        if (json == ultimoGuardado) return;
        ultimoGuardado = json;
        System.IO.File.WriteAllText(ArchivoAjuste, json);
    }
#endif
}
