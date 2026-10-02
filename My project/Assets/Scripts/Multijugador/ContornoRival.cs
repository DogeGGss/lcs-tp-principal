using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Contorno rojo de los rivales (US 031, CA5), como en Valorant: es la forma de reconocer a un rival. No se ve el nombre
// ni la vida de nadie arriba de su cabeza. Lo agregan:
// - JugadorEnRed, en la copia de cada jugador remoto: en Táctico solo lo tienen los del otro equipo; en Deathmatch,
//   todos (EquiposTacticos.EsRival). Muerto no tiene contorno.
// - EnemyChaser, en los enemigos del campo de práctica, mientras están vivos.
// El contorno son dos copias de la malla del personaje que se mueven con sus mismos huesos, con el shader
// Efectos/ContornoRival (material ContornoRival): una máscara que marca lo que se ve del personaje y, después, el
// contorno, un poco más grande, que se dibuja solo fuera de esa marca. Así queda solo la silueta. No se ve a través
// de las paredes.
public class ContornoRival : MonoBehaviour
{
    private static Material mascara;

    private System.Func<bool> esRival;
    private Renderer[] partes;
    private bool visible = true;

    // esRival dice en cada cuadro si se tiene que ver el contorno (por ejemplo, vivo y del otro equipo).
    public static void Crear(GameObject personaje, Transform modelo, Material material, System.Func<bool> esRival)
    {
        if (personaje == null || modelo == null || material == null || esRival == null) return;
        Material marca = Mascara(material);

        var lista = new List<Renderer>();
        foreach (Renderer original in modelo.GetComponentsInChildren<Renderer>(true))
        {
            Renderer copia = Copiar(original, marca, "ContornoMascara");
            if (copia == null) continue;
            lista.Add(copia);
            lista.Add(Copiar(original, material, "Contorno"));
        }

        ContornoRival contorno = personaje.AddComponent<ContornoRival>();
        contorno.esRival = esRival;
        contorno.partes = lista.ToArray();
        contorno.Mostrar(false);
    }

    // La máscara sale del mismo material: sin ancho, sin color, marcando el stencil, y dibujada justo antes del contorno.
    private static Material Mascara(Material contorno)
    {
        if (mascara != null && mascara.shader == contorno.shader) return mascara;
        mascara = new Material(contorno) { name = contorno.name + " (máscara)", renderQueue = contorno.renderQueue - 1 };
        mascara.SetFloat("_Ancho", 0f);
        mascara.SetFloat("_Cull", (float)CullMode.Back);
        mascara.SetFloat("_ColorMask", 0f);
        mascara.SetFloat("_ZWrite", 0f);
        mascara.SetFloat("_Offset", -1f);
        mascara.SetFloat("_StencilComp", (float)CompareFunction.Always);
        mascara.SetFloat("_StencilPass", (float)StencilOp.Replace);
        return mascara;
    }

    // Copia del renderer con el material dado en todas sus partes (una por submalla).
    private static Renderer Copiar(Renderer original, Material material, string nombre)
    {
        var go = new GameObject(nombre);
        go.layer = original.gameObject.layer;
        go.transform.SetParent(original.transform, false);

        Renderer copia;
        int submallas;
        if (original is SkinnedMeshRenderer piel)
        {
            if (piel.sharedMesh == null) { Destroy(go); return null; }
            var nueva = go.AddComponent<SkinnedMeshRenderer>();
            nueva.sharedMesh = piel.sharedMesh;
            nueva.bones = piel.bones;
            nueva.rootBone = piel.rootBone;
            nueva.localBounds = piel.localBounds;
            nueva.updateWhenOffscreen = piel.updateWhenOffscreen;
            copia = nueva;
            submallas = piel.sharedMesh.subMeshCount;
        }
        else if (original is MeshRenderer && original.TryGetComponent(out MeshFilter filtro) && filtro.sharedMesh != null)
        {
            go.AddComponent<MeshFilter>().sharedMesh = filtro.sharedMesh;
            copia = go.AddComponent<MeshRenderer>();
            submallas = filtro.sharedMesh.subMeshCount;
        }
        else
        {
            Destroy(go);
            return null;
        }

        var materiales = new Material[Mathf.Max(1, submallas)];
        for (int i = 0; i < materiales.Length; i++) materiales[i] = material;
        copia.sharedMaterials = materiales;
        copia.shadowCastingMode = ShadowCastingMode.Off;
        copia.receiveShadows = false;
        copia.lightProbeUsage = LightProbeUsage.Off;
        copia.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return copia;
    }

    private void LateUpdate()
    {
        // Si Unity recarga los scripts con el juego andando, se pierde esRival (no se guarda): sin ella, el contorno
        // se apaga en vez de tirar un error en cada cuadro.
        if (esRival == null)
        {
            if (partes == null) partes = BuscarPartes(); // también se pierde la lista: se buscan por nombre
            Mostrar(false);
            enabled = false;
            return;
        }
        Mostrar(esRival());
    }

    private Renderer[] BuscarPartes()
    {
        var lista = new List<Renderer>();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            if (r.name == "Contorno" || r.name == "ContornoMascara") lista.Add(r);
        return lista.ToArray();
    }

    private void Mostrar(bool ver)
    {
        if (ver == visible || partes == null) return;
        visible = ver;
        foreach (Renderer r in partes)
            if (r != null) r.enabled = ver;
    }
}
