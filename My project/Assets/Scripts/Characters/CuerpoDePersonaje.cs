using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

// Cambia el cuerpo de un jugador por el modelo de su personaje (CharacterData.modelo), con el juego andando.
// Se queda el Animator del cuerpo original (con su controlador, así las animaciones siguen igual) y se le cambia el
// avatar y el esqueleto: la malla y los huesos del modelo nuevo pasan a colgar de ese Animator.
// Todo lo que el prefab del jugador le agregó a los huesos (zonas de impacto, el arma en la mano, los agarres del IK)
// se pasa al hueso equivalente del modelo nuevo, y el IK de los brazos se vuelve a armar con los huesos nuevos.
// El modelo tiene que ser humanoide (Rig > Animation Type: Humanoid), como el de Mixamo que ya usa el jugador.
public static class CuerpoDePersonaje
{
    /// <summary>Pone el modelo en el cuerpo que maneja ese Animator. Devuelve false si no se pudo (y no cambia nada).</summary>
    public static bool Cambiar(Animator animador, GameObject modelo)
    {
        if (animador == null || modelo == null || !animador.isHuman) return false;

        GameObject nuevo = Object.Instantiate(modelo);
        nuevo.SetActive(true);
        Animator animadorNuevo = nuevo.GetComponentInChildren<Animator>();
        if (animadorNuevo == null || animadorNuevo.avatar == null || !animadorNuevo.avatar.isHuman)
        {
            Debug.LogWarning($"CuerpoDePersonaje: {modelo.name} no es humanoide (Rig > Animation Type: Humanoid). Queda el cuerpo de siempre.");
            Object.Destroy(nuevo);
            return false;
        }

        Transform raiz = animador.transform;
        Transform raizNueva = animadorNuevo.transform;

        // Hueso viejo -> hueso nuevo: primero por el avatar (cadera, brazos, manos...) y después por nombre, sin el
        // prefijo de Mixamo ("mixamorig:" o "mixamorig1:"), para los que el avatar no usa (puntas de los dedos).
        var equivalente = new Dictionary<Transform, Transform>();
        for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
        {
            Transform viejo = animador.GetBoneTransform((HumanBodyBones)i);
            Transform nuevoHueso = animadorNuevo.GetBoneTransform((HumanBodyBones)i);
            if (viejo != null && nuevoHueso != null) equivalente[viejo] = nuevoHueso;
        }
        var porNombre = new Dictionary<string, Transform>();
        foreach (Transform t in raizNueva.GetComponentsInChildren<Transform>(true))
            if (t != raizNueva) porNombre[Nombre(t.name)] = t;

        Transform hipsViejo = animador.GetBoneTransform(HumanBodyBones.Hips);
        var huesosViejos = new List<Transform>();
        if (hipsViejo != null)
            foreach (Transform t in hipsViejo.GetComponentsInChildren<Transform>(true))
                if (!equivalente.ContainsKey(t) && porNombre.TryGetValue(Nombre(t.name), out Transform n)) equivalente[t] = n;
        foreach (Transform t in equivalente.Keys) huesosViejos.Add(t);

        // Lo agregado a los huesos viejos (no es parte del esqueleto) pasa al hueso nuevo, con la misma posición local.
        foreach (Transform hueso in huesosViejos)
        {
            var hijos = new List<Transform>();
            foreach (Transform hijo in hueso) if (!equivalente.ContainsKey(hijo)) hijos.Add(hijo);
            foreach (Transform hijo in hijos) hijo.SetParent(equivalente[hueso], false);
        }

        // El IK de los brazos (agarre del arma) apuntaba a los huesos viejos.
        foreach (TwoBoneIKConstraint ik in raiz.GetComponentsInChildren<TwoBoneIKConstraint>(true))
        {
            TwoBoneIKConstraintData d = ik.data;
            d.root = Equivalente(equivalente, d.root);
            d.mid = Equivalente(equivalente, d.mid);
            d.tip = Equivalente(equivalente, d.tip);
            d.hint = Equivalente(equivalente, d.hint);
            ik.data = d;
        }

        // Se va el cuerpo viejo (sus mallas y su esqueleto) y entran la malla y el esqueleto nuevos.
        var viejos = new List<GameObject>();
        foreach (Transform hijo in raiz)
            if (hijo == hipsViejo || hijo.GetComponent<SkinnedMeshRenderer>() != null || (hipsViejo != null && hipsViejo.IsChildOf(hijo)))
                viejos.Add(hijo.gameObject);
        var entran = new List<Transform>();
        foreach (Transform hijo in raizNueva) entran.Add(hijo);
        int capa = raiz.gameObject.layer;
        foreach (Transform hijo in entran)
        {
            hijo.SetParent(raiz, false);
            foreach (Transform t in hijo.GetComponentsInChildren<Transform>(true))
                if (t.GetComponent<Renderer>() != null) t.gameObject.layer = capa;
        }
        foreach (GameObject viejo in viejos)
        {
            // Se suelta ya, así nada que recorra el cuerpo en este mismo cuadro (el contorno, por ejemplo) lo encuentra.
            viejo.SetActive(false);
            viejo.transform.SetParent(null, false);
            Object.Destroy(viejo);
        }

        animador.avatar = animadorNuevo.avatar;
        Object.Destroy(nuevo);
        animador.Rebind();

        RigBuilder rig = raiz.GetComponentInChildren<RigBuilder>(true);
        if (rig != null) rig.Build();
        return true;
    }

    private static Transform Equivalente(Dictionary<Transform, Transform> equivalente, Transform viejo) =>
        viejo != null && equivalente.TryGetValue(viejo, out Transform nuevo) ? nuevo : viejo;

    // "mixamorig:LeftArm" y "mixamorig1:LeftArm" son el mismo hueso.
    private static string Nombre(string nombre)
    {
        int dos = nombre.LastIndexOf(':');
        return dos >= 0 ? nombre.Substring(dos + 1) : nombre;
    }
}
