using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Primera persona: el cuerpo propio no se dibuja (solo su sombra), salvo los brazos, que siguen agarrando el arma.
// Así al mirar hacia abajo se ven los brazos y no el torso ni las piernas.
// Los brazos son una copia de la malla del cuerpo con solo los triángulos que mueven los huesos de los brazos (del
// brazo a los dedos), sobre los mismos huesos: la animación y el agarre de las armas los mueven igual que al cuerpo.
// La malla del personaje necesita Read/Write activado en la importación para poder copiarla en el juego compilado.
public class HideOwnBody : MonoBehaviour
{
    [SerializeField] private Camera ownCamera;

    private readonly List<Renderer> brazos = new List<Renderer>();
    private HealthSystem vida;

    // Con multijugador, esto tiene que correr solo en el jugador local: los demas si tienen que ver este cuerpo.
    private void Start()
    {
        vida = GetComponent<HealthSystem>();
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (ownCamera != null && r.transform.IsChildOf(ownCamera.transform)) continue;
            r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            if (r is SkinnedMeshRenderer cuerpo) CrearBrazos(cuerpo);
        }
    }

    // Muerto, los brazos no quedan flotando donde estaba el cuerpo (el resto ya no se ve).
    private void LateUpdate()
    {
        bool vivo = vida == null || vida.currentHealth > 0;
        foreach (Renderer r in brazos)
            if (r != null && r.enabled != vivo) r.enabled = vivo;
    }

    private void CrearBrazos(SkinnedMeshRenderer cuerpo)
    {
        Mesh malla = cuerpo.sharedMesh;
        Animator animador = cuerpo.GetComponentInParent<Animator>();
        if (malla == null || animador == null || !animador.isHuman) return;
        if (!malla.isReadable)
        {
            Debug.LogWarning($"HideOwnBody: la malla {malla.name} no tiene Read/Write activado, no se ven los brazos en primera persona.");
            return;
        }

        // Huesos de los brazos: desde el brazo (sin el hombro, que va con el torso) hasta la punta de los dedos.
        Transform izquierdo = animador.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        Transform derecho = animador.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform[] huesos = cuerpo.bones;
        var esBrazo = new bool[huesos.Length];
        for (int i = 0; i < huesos.Length; i++)
        {
            Transform h = huesos[i];
            esBrazo[i] = h != null && ((izquierdo != null && h.IsChildOf(izquierdo)) || (derecho != null && h.IsChildOf(derecho)));
        }

        // Un vértice es del brazo si se mueve sobre todo con esos huesos.
        BoneWeight[] pesos = malla.boneWeights;
        var vertice = new bool[pesos.Length];
        for (int v = 0; v < pesos.Length; v++)
        {
            BoneWeight p = pesos[v];
            float peso = (esBrazo[p.boneIndex0] ? p.weight0 : 0f) + (esBrazo[p.boneIndex1] ? p.weight1 : 0f)
                       + (esBrazo[p.boneIndex2] ? p.weight2 : 0f) + (esBrazo[p.boneIndex3] ? p.weight3 : 0f);
            vertice[v] = peso >= 0.5f;
        }

        // Quedan los triángulos con los tres vértices en el brazo.
        Mesh copia = Instantiate(malla);
        copia.name = malla.name + " (brazos)";
        var quedan = new List<int>();
        for (int s = 0; s < malla.subMeshCount; s++)
        {
            int[] triangulos = malla.GetTriangles(s);
            quedan.Clear();
            for (int t = 0; t < triangulos.Length; t += 3)
            {
                int a = triangulos[t], b = triangulos[t + 1], c = triangulos[t + 2];
                if (vertice[a] && vertice[b] && vertice[c]) { quedan.Add(a); quedan.Add(b); quedan.Add(c); }
            }
            copia.SetTriangles(quedan, s, false);
        }

        var go = new GameObject("Brazos (primera persona)");
        go.layer = cuerpo.gameObject.layer;
        go.transform.SetParent(cuerpo.transform, false);
        var smr = go.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = copia;
        smr.bones = huesos;
        smr.rootBone = cuerpo.rootBone;
        smr.localBounds = cuerpo.localBounds;
        smr.quality = cuerpo.quality;
        smr.sharedMaterials = cuerpo.sharedMaterials;
        smr.shadowCastingMode = ShadowCastingMode.Off; // la sombra ya la tira el cuerpo entero
        brazos.Add(smr);
    }
}
