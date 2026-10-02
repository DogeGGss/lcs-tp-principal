using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Brazos de primera persona pegados a la cámara, uno por arma de fuego.
// Los brazos del cuerpo (los que arma HideOwnBody) cuelgan de los hombros, que no se inclinan al mirar arriba o abajo:
// el IK no llega al arma y se estiran o desaparecen según el ángulo. Acá, por cada arma, se saca una "foto" de esos
// brazos agarrándola con la cámara derecha y se pega a la cámara, así se ven igual en cualquier ángulo, como en los
// demás FPS. Con esa arma en la mano se ve su foto y no los brazos del cuerpo.
// Cada arma dice dónde van las manos con dos hijos, Gun_ref_rightHand y Gun_ref_leftHand (los de la Línea A son los
// objetivos del IK de los brazos). Para la foto de otra arma, el IK se lleva a sus agarres y el arma se acerca hasta
// donde llegan los brazos; después se corren arma y brazos juntos (Corrimiento).
// WeaponSwitcher lo agrega solo; si el jugador no tiene brazos de primera persona, no hace nada.
[DefaultExecutionOrder(100)] // después del LateUpdate de HideOwnBody, que prende los brazos del cuerpo
public class BrazosEnCamara : MonoBehaviour
{
    public const string AgarreDerecho = "Gun_ref_rightHand", AgarreIzquierdo = "Gun_ref_leftHand";
    private const string NombreBrazos = "Brazos (primera persona)"; // los que crea HideOwnBody
    private const float InclinacionMaxima = 10f; // grados: la foto se saca con la cámara casi derecha
    private const float EsperaInicial = 0.5f;    // que el IK ya haya puesto las manos en el arma
    private const int CuadrosParaElIK = 2;       // cuadros entre mover los objetivos del IK y sacar la foto
    // Con la foto, arma y brazos van 12 cm más adelante: la cámara de las armas dibuja el arma entera y quedaba
    // encima de la cara. Pasando los 16 cm se empieza a ver dónde termina el brazo izquierdo, abajo al centro.
    private static readonly Vector3 Corrimiento = new Vector3(0f, 0f, 0.12f);

    private WeaponSwitcher switcher;
    private HealthSystem vida;
    private Transform camara;
    private SkinnedMeshRenderer brazosCuerpo;
    private Transform objetivoDerecho, objetivoIzquierdo; // los del IK: los agarres de la Línea A
    private Vector3 derechoLocal, izquierdoLocal;
    private Quaternion derechoGiro, izquierdoGiro;
    private Vector3 agarreBase; // dónde queda la mano derecha en la Línea A, visto desde la cámara: ahí llegan los brazos
    private int capa;
    private float desde;

    private readonly Dictionary<GameObject, MeshRenderer> fotos = new Dictionary<GameObject, MeshRenderer>();
    private readonly Dictionary<GameObject, Vector3> lugarOriginal = new Dictionary<GameObject, Vector3>();
    private GameObject preparando;
    private int fotoEnCuadro;

    private void Awake()
    {
        switcher = GetComponentInParent<WeaponSwitcher>();
        vida = GetComponentInParent<HealthSystem>();
        camara = switcher != null ? switcher.transform : transform;
        capa = LayerMask.NameToLayer("ArmaEnMano");
    }

    private void Start()
    {
        desde = Time.time;
    }

    private void LateUpdate()
    {
        if (switcher == null || switcher.pistolObj == null) return;
        if (brazosCuerpo == null && !Preparar()) return;

        bool vivo = vida == null || vida.currentHealth > 0;
        GameObject enMano = !vivo ? null : switcher.HeldPrimary != null ? switcher.HeldPrimary : switcher.HeldSecondary;

        if (enMano != null && !fotos.ContainsKey(enMano) && TieneAgarres(enMano))
        {
            if (preparando != enMano)
            {
                if (Time.time - desde > EsperaInicial && Mathf.Abs(Inclinacion()) < InclinacionMaxima) PrepararFoto(enMano);
            }
            else if (Time.frameCount >= fotoEnCuadro) SacarFoto(enMano);
        }

        foreach (KeyValuePair<GameObject, MeshRenderer> foto in fotos)
            if (foto.Value != null) foto.Value.enabled = foto.Key == enMano;
        // Con su foto, no se ven los brazos del cuerpo (HideOwnBody los vuelve a prender; acá se apagan después).
        if (enMano != null && fotos.ContainsKey(enMano) && preparando == null) brazosCuerpo.enabled = false;
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
        agarreBase = camara.InverseTransformPoint(objetivoDerecho.position);
        if (capa < 0) capa = brazosCuerpo.gameObject.layer;
        return true;
    }

    private static bool TieneAgarres(GameObject arma) =>
        arma.transform.Find(AgarreDerecho) != null && arma.transform.Find(AgarreIzquierdo) != null;

    // Inclinación de la cámara (positivo, mirando abajo).
    private float Inclinacion()
    {
        float x = camara.localEulerAngles.x;
        return x > 180f ? x - 360f : x;
    }

    // Acerca el arma hasta donde llegan los brazos y lleva el IK a sus agarres; la foto sale unos cuadros después.
    private void PrepararFoto(GameObject arma)
    {
        if (!lugarOriginal.ContainsKey(arma)) lugarOriginal[arma] = arma.transform.localPosition;
        arma.transform.localPosition = lugarOriginal[arma];
        Transform derecho = arma.transform.Find(AgarreDerecho), izquierdo = arma.transform.Find(AgarreIzquierdo);
        arma.transform.localPosition += agarreBase - camara.InverseTransformPoint(derecho.position);
        if (arma != switcher.pistolObj)
        {
            objetivoDerecho.SetPositionAndRotation(derecho.position, derecho.rotation);
            objetivoIzquierdo.SetPositionAndRotation(izquierdo.position, izquierdo.rotation);
        }
        brazosCuerpo.enabled = true;
        preparando = arma;
        fotoEnCuadro = Time.frameCount + CuadrosParaElIK;
    }

    // Copia fija de los brazos tal como están ahora (agarrando el arma), como hija de la cámara. Después, arma y
    // brazos se corren juntos y los objetivos del IK vuelven a la Línea A.
    private void SacarFoto(GameObject arma)
    {
        var malla = new Mesh { name = "Brazos con " + arma.name };
        brazosCuerpo.BakeMesh(malla, true); // con la escala: la copia queda a escala 1

        // Los dibuja la cámara de las armas: la principal no dibuja nada a menos de 30 cm y la mano quedaba cortada.
        // El arma va a esa misma cámara, así la mano y el arma se tapan bien entre sí.
        var go = new GameObject("Brazos con " + arma.name + " (cámara)");
        go.layer = capa;
        foreach (Transform parte in arma.GetComponentsInChildren<Transform>(true)) parte.gameObject.layer = capa;
        go.transform.SetPositionAndRotation(brazosCuerpo.transform.position, brazosCuerpo.transform.rotation);
        go.transform.SetParent(camara, true);
        Vector3 escala = camara.lossyScale;
        go.transform.localScale = new Vector3(1f / escala.x, 1f / escala.y, 1f / escala.z);
        go.transform.localPosition += Corrimiento;
        arma.transform.localPosition += Corrimiento;

        go.AddComponent<MeshFilter>().sharedMesh = malla;
        MeshRenderer foto = go.AddComponent<MeshRenderer>();
        foto.sharedMaterials = brazosCuerpo.sharedMaterials;
        foto.shadowCastingMode = ShadowCastingMode.Off; // la sombra la sigue tirando el cuerpo
        foto.lightProbeUsage = brazosCuerpo.lightProbeUsage;
        fotos[arma] = foto;

        objetivoDerecho.localPosition = derechoLocal;
        objetivoDerecho.localRotation = derechoGiro;
        objetivoIzquierdo.localPosition = izquierdoLocal;
        objetivoIzquierdo.localRotation = izquierdoGiro;
        preparando = null;
    }

    // Tira las fotos y vuelve cada arma a su lugar: se sacan de nuevo (por ejemplo, después de mover los agarres).
    public void Rehacer()
    {
        foreach (KeyValuePair<GameObject, MeshRenderer> foto in fotos)
            if (foto.Value != null) Destroy(foto.Value.gameObject);
        fotos.Clear();
        foreach (KeyValuePair<GameObject, Vector3> lugar in lugarOriginal)
            if (lugar.Key != null) lugar.Key.transform.localPosition = lugar.Value;
        if (objetivoDerecho != null)
        {
            objetivoDerecho.localPosition = derechoLocal;
            objetivoDerecho.localRotation = derechoGiro;
            objetivoIzquierdo.localPosition = izquierdoLocal;
            objetivoIzquierdo.localRotation = izquierdoGiro;
        }
        preparando = null;
        brazosCuerpo = null; // vuelve a tomar los agarres de la Línea A tal como estén ahora
    }
}
