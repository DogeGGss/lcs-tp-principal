using System.Collections.Generic;
using UnityEngine;

// Copia solo visual de un arma a tamaño real (US 182 y US 184): sus mallas con sus materiales, sin scripts, sonidos
// ni colliders. La usan el arma en la mano de los demás jugadores y el arma en el piso, así las dos son el mismo
// modelo que se ve en primera persona (US 179).
// Sale orientada como en primera persona: el caño hacia adelante (forward) y el lomo hacia arriba (up), con el centro
// del arma en el origen. El largo sale de la ficha (ShopItem.realLength).
public static class ModeloReal
{
    // Si la ficha no dice el largo, el arma queda de este largo (en metros).
    public const float LargoSinFicha = 0.7f;

    // Las armas de primera persona del jugador local, por ficha: de acá salen los modelos de las armas del piso.
    private static readonly Dictionary<ShopItem, GameObject> moldes = new Dictionary<ShopItem, GameObject>();

    public static void Registrar(ShopItem item, GameObject arma)
    {
        if (item != null && arma != null) moldes[item] = arma;
    }

    // Arma de primera persona de esa ficha, o null si no hay ninguna en la escena.
    public static GameObject Molde(ShopItem item)
    {
        if (item == null || !moldes.TryGetValue(item, out GameObject arma)) return null;
        if (arma == null) moldes.Remove(item); // la escena ya no existe
        return arma;
    }

    // Arma el modelo a tamaño real de "arma" (un arma de primera persona, hija de la cámara). "limites" es su caja
    // en el espacio del modelo: el caño está en (0, 0, limites.max.z).
    public static GameObject Crear(GameObject arma, float largo, out Bounds limites, string nombre = null)
    {
        limites = new Bounds();
        if (arma == null) return null;
        // El marco es la cámara: así "adelante" es hacia donde apunta el caño en primera persona.
        Transform marco = arma.transform.parent != null ? arma.transform.parent : arma.transform;

        var raiz = new GameObject(nombre ?? arma.name + " (tamaño real)");
        var piezas = new GameObject("Piezas").transform;
        piezas.SetParent(raiz.transform, false);

        bool hayCaja = false;
        Bounds caja = new Bounds();
        foreach (MeshRenderer original in arma.GetComponentsInChildren<MeshRenderer>(true))
        {
            MeshFilter filtro = original.GetComponent<MeshFilter>();
            if (filtro == null || filtro.sharedMesh == null || !original.enabled) continue;
            if (original.gameObject != arma && !original.gameObject.activeSelf) continue;

            Matrix4x4 m = marco.worldToLocalMatrix * original.transform.localToWorldMatrix;
            var pieza = new GameObject(original.name).transform;
            pieza.SetParent(piezas, false);
            pieza.localPosition = m.GetPosition();
            pieza.localRotation = m.rotation;
            pieza.localScale = m.lossyScale;
            pieza.gameObject.AddComponent<MeshFilter>().sharedMesh = filtro.sharedMesh;
            pieza.gameObject.AddComponent<MeshRenderer>().sharedMaterials = original.sharedMaterials;

            Bounds b = filtro.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 esquina = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = m.MultiplyPoint3x4(esquina);
                if (!hayCaja) { caja = new Bounds(p, Vector3.zero); hayCaja = true; }
                else caja.Encapsulate(p);
            }
        }
        if (!hayCaja) return raiz;

        // Del largo del modelo de primera persona al real, con el centro en el origen.
        float escala = (largo > 0f ? largo : LargoSinFicha) / Mathf.Max(0.0001f, caja.size.z);
        piezas.localScale = Vector3.one * escala;
        piezas.localPosition = -caja.center * escala;
        limites = new Bounds(Vector3.zero, caja.size * escala);
        return raiz;
    }

    // El de una ficha, con el largo que dice la ficha.
    public static GameObject Crear(ShopItem item, GameObject arma, out Bounds limites)
    {
        return Crear(arma, item != null ? item.realLength : 0f, out limites, item != null ? NombreDe(item) : null);
    }

    public static string NombreDe(ShopItem item) =>
        item == null ? "Arma" : !string.IsNullOrEmpty(item.alias) ? item.alias : item.displayName;
}
