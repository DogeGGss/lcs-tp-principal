using System.Collections.Generic;
using UnityEngine;

// F11 (NPCs), US 283: las marcas bioluminiscentes de los enemigos, para que se vean de noche (el mapa es oscuro y los
// bichos también). Cada tipo tiene las suyas (TipoDeNpc: patrón, color, cantidad, tamaño, brillo y pulso):
// - Común (Catfish): aros verdes. Tanque (Lizard): manchas naranjas, como de vaca. Rápido (Wolf Boss): rayas amarillas
//   en el lomo.
// - Respiran despacio (un pulso que recorre el cuerpo), destellan al recibir un golpe y se apagan al morir.
// Se dibujan con Efectos/MarcasQueBrillan como una pasada más sobre el material del bicho (no se toca su material).
// El patrón sale de la pose quieta del modelo: al armarlo se guarda en la malla (TEXCOORD3 y 4) dónde está cada punto
// en esa pose, medido en el largo del cuerpo, así las marcas quedan pegadas a la piel cuando camina o ataca.
// Lo que no es piel no lleva marcas: las partes de la malla que mueven los huesos del tipo en huesosSinMarcas (la
// lanza del Catfish, que la lleva el hueso "BN_ R_ Hand").
// - Los ojos brillan del mismo color: la zona de la textura donde están (TipoDeNpc.ojoEnLaTextura y radioDelOjo), con el
//   dibujo de la textura (la pupila oscura no brilla). Con delineado (TipoDeNpc.delineadoDeOjos), un anillo de
//   manchitas que brillan alrededor de cada ojo. Con ojosOscuros brilla lo negro de esa zona en vez del iris: los
//   huecos de la cabeza del Jefe, su punto débil (sin marcas en el cuerpo).
// Lo agrega Npc.Armar si el tipo tiene marcas.
[RequireComponent(typeof(Npc))]
public class MarcasQueBrillan : MonoBehaviour
{
    private const float DuraElDestello = 0.35f, TardaEnApagarse = 1.5f;

    private static readonly int ColorId = Shader.PropertyToID("_ColorDeMarcas"), PatronId = Shader.PropertyToID("_Patron"),
        CantidadId = Shader.PropertyToID("_Cantidad"), TamanioId = Shader.PropertyToID("_Tamanio"),
        BrilloOjosId = Shader.PropertyToID("_BrilloOjos"), OjosOscurosId = Shader.PropertyToID("_OjosOscuros"),
        IrisId = Shader.PropertyToID("_Iris"), BaseMapId = Shader.PropertyToID("_BaseMap"),
        HuecosId = Shader.PropertyToID("_Huecos"), BrilloId = Shader.PropertyToID("_Brillo"),
        OpacidadId = Shader.PropertyToID("_Opacidad"),
        SeparacionId = Shader.PropertyToID("_Separacion"), AnchoRayasId = Shader.PropertyToID("_AnchoRayas"),
        LargoRayasId = Shader.PropertyToID("_LargoRayas"), IrregularId = Shader.PropertyToID("_Irregular"),
        PulsoId = Shader.PropertyToID("_Pulso"), DestelloId = Shader.PropertyToID("_Destello"),
        ApagadoId = Shader.PropertyToID("_Apagado");

    // Las mallas con la pose quieta guardada, una por malla original (las comparten los bichos del mismo tipo).
    private static readonly Dictionary<(Mesh, string), Mesh> conPose = new Dictionary<(Mesh, string), Mesh>();
    // Dónde están los ojos de cada malla copiada, en la pose quieta (xyz) y qué tan grandes son (w); hasta dos.
    private static readonly Dictionary<Mesh, Vector4[]> ojosDe = new Dictionary<Mesh, Vector4[]>();
    private static readonly int Ojo1Id = Shader.PropertyToID("_Ojo1"), Ojo2Id = Shader.PropertyToID("_Ojo2"),
        DelineadoId = Shader.PropertyToID("_Delineado");
    private static Shader shader;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar() { conPose.Clear(); ojosDe.Clear(); } // sin recarga de dominio

    private Npc npc;
    private Material material;
    private float golpeEn = -10f, murioEn = -1f;

    /// <summary>Le pone al modelo las marcas de su tipo (si tiene).</summary>
    public static void Poner(Npc npc, GameObject modelo, TipoDeNpc tipo)
    {
        if (npc == null || modelo == null || tipo == null) return;
        if (tipo.marcas == PatronDeMarcas.Ninguno && tipo.radioDelOjo <= 0f) return; // ni marcas ni ojos
        if (shader == null)
        {
            shader = Resources.Load<Shader>("Efectos/MarcasQueBrillan");
            if (shader == null) shader = Shader.Find("Efectos/MarcasQueBrillan");
            if (shader == null) return;
        }

        var material = new Material(shader) { name = "Marcas que brillan (" + tipo.nombre + ")" };
        material.SetColor(ColorId, tipo.colorDeMarcas);
        material.SetFloat(PatronId, (float)tipo.marcas);
        material.SetFloat(CantidadId, tipo.cantidadDeMarcas);
        material.SetFloat(TamanioId, tipo.tamanioDeMarcas);
        material.SetFloat(HuecosId, tipo.huecosEntreMarcas);
        material.SetFloat(IrregularId, tipo.irregularidadDeMarcas);
        material.SetFloat(BrilloId, tipo.brilloDeMarcas);
        material.SetFloat(OpacidadId, tipo.opacidadDeMarcas);
        material.SetFloat(PulsoId, tipo.pulsoDeMarcas);
        material.SetFloat(SeparacionId, tipo.separacionDeRayas);
        material.SetFloat(AnchoRayasId, tipo.anchoDeRayas);
        material.SetFloat(LargoRayasId, tipo.largoDeRayas);
        material.SetFloat(BrilloOjosId, tipo.radioDelOjo > 0f ? tipo.brilloDeOjos : 0f);
        material.SetFloat(OjosOscurosId, tipo.ojosOscuros ? 1f : 0f);
        material.SetVector(IrisId, new Vector4(tipo.centroDelIris.x, tipo.centroDelIris.y, tipo.radioDelIris, 0f));

        bool alguna = false;
        foreach (Renderer r in modelo.GetComponentsInChildren<Renderer>(true))
        {
            var piel = r as SkinnedMeshRenderer;
            MeshFilter filtro = piel == null && r is MeshRenderer ? r.GetComponent<MeshFilter>() : null;
            Mesh original = piel != null ? piel.sharedMesh : filtro != null ? filtro.sharedMesh : null;
            if (original == null) continue;
            Mesh malla = ConPose(original, r.transform, modelo.transform, piel, tipo.huesosSinMarcas, tipo.giroDeMarcas,
                tipo.ojoEnLaTextura, tipo.radioDelOjo);
            if (malla == null) continue;
            if (ojosDe.TryGetValue(malla, out Vector4[] centros) && tipo.delineadoDeOjos > 0f && tipo.radioDelOjo > 0f)
            {
                material.SetVector(Ojo1Id, centros[0]);
                material.SetVector(Ojo2Id, centros[1]);
                material.SetFloat(DelineadoId, tipo.delineadoDeOjos);
            }
            if (piel != null) piel.sharedMesh = malla;
            else filtro.sharedMesh = malla;

            // Una pasada más al final: la malla copiada tiene una parte con todo el cuerpo para eso.
            Material[] mats = r.sharedMaterials;
            // La textura del bicho, para el dibujo de los ojos.
            if (mats.Length > 0 && mats[0] != null && mats[0].mainTexture != null && material.GetTexture(BaseMapId) == null)
            {
                material.SetTexture(BaseMapId, mats[0].mainTexture);
                material.SetTextureScale(BaseMapId, mats[0].mainTextureScale);
                material.SetTextureOffset(BaseMapId, mats[0].mainTextureOffset);
            }
            var con = new Material[malla.subMeshCount];
            for (int i = 0; i < con.Length - 1; i++) con[i] = i < mats.Length ? mats[i] : mats[mats.Length - 1];
            con[con.Length - 1] = material;
            r.sharedMaterials = con;
            alguna = true;
        }
        if (!alguna) { Destroy(material); return; }

        var marcas = npc.gameObject.AddComponent<MarcasQueBrillan>();
        marcas.material = material;
    }

    // Copia de la malla con la pose quieta en TEXCOORD3 (posición) y TEXCOORD4 (normal), medidas en el espacio del
    // modelo (y arriba, z adelante) y en el largo del cuerpo, con cuánto lleva marcas cada punto (w); y una parte más
    // con todos los triángulos, para la pasada.
    // El giro de las marcas (TipoDeNpc.giroDeMarcas) gira ese espacio: cambia hacia dónde corren las rayas y qué es "arriba"
    // (el lomo), para los modelos que vienen acostados de otra manera.
    // En la w de la normal va si es ojo (1) o no (0).
    private static Mesh ConPose(Mesh original, Transform render, Transform modelo, SkinnedMeshRenderer piel, string[] sinMarcas,
        Vector3 giro, Vector2 ojo, float radioOjo)
    {
        string clave = (sinMarcas == null ? "" : string.Join("|", sinMarcas)) + "|" + giro + "|" + ojo + "|" + radioOjo;
        if (conPose.TryGetValue((original, clave), out Mesh hecha) && hecha != null) return hecha;
        if (!original.isReadable)
        {
            Debug.LogWarning("[Marcas que brillan] " + original.name + " no se puede leer: activá Read/Write en su modelo.");
            return null;
        }

        Matrix4x4 aModelo = Matrix4x4.Rotate(Quaternion.Inverse(Quaternion.Euler(giro))) *
                            modelo.worldToLocalMatrix * render.localToWorldMatrix;
        Vector3[] puntos = original.vertices;
        Vector3[] normales = original.normals;
        var pose = new Vector3[puntos.Length];
        var normalesPose = new Vector3[puntos.Length];
        Bounds caja = new Bounds();
        for (int i = 0; i < puntos.Length; i++)
        {
            pose[i] = aModelo.MultiplyPoint3x4(puntos[i]);
            normalesPose[i] = i < normales.Length ? aModelo.MultiplyVector(normales[i]).normalized : Vector3.up;
            if (i == 0) caja = new Bounds(pose[i], Vector3.zero); else caja.Encapsulate(pose[i]);
        }
        float largo = Mathf.Max(0.0001f, Mathf.Max(caja.size.x, caja.size.y, caja.size.z));
        for (int i = 0; i < pose.Length; i++) pose[i] = (pose[i] - caja.center) / largo;

        // Cuánto lleva marcas cada punto: lo que mueven los huesos sin marcas, nada.
        var lleva = new float[puntos.Length];
        for (int i = 0; i < lleva.Length; i++) lleva[i] = 1f;
        var fuera = new HashSet<int>();
        if (piel != null && sinMarcas != null && sinMarcas.Length > 0)
        {
            Transform[] huesos = piel.bones;
            for (int b = 0; b < huesos.Length; b++)
                if (huesos[b] != null && System.Array.IndexOf(sinMarcas, huesos[b].name) >= 0) fuera.Add(b);
        }
        if (fuera.Count > 0)
        {
            BoneWeight[] pesos = original.boneWeights;
            for (int i = 0; i < pesos.Length && i < lleva.Length; i++)
            {
                BoneWeight p = pesos[i];
                float sin = (fuera.Contains(p.boneIndex0) ? p.weight0 : 0f) + (fuera.Contains(p.boneIndex1) ? p.weight1 : 0f) +
                            (fuera.Contains(p.boneIndex2) ? p.weight2 : 0f) + (fuera.Contains(p.boneIndex3) ? p.weight3 : 0f);
                lleva[i] = Mathf.Clamp01(1f - sin * 2f); // lo que se mueve sobre todo con ese hueso, sin marcas
            }
        }
        var conLleva = new List<Vector4>(pose.Length);
        for (int i = 0; i < pose.Length; i++) conLleva.Add(new Vector4(pose[i].x, pose[i].y, pose[i].z, lleva[i]));

        Mesh copia = Instantiate(original);
        copia.name = original.name + " (marcas)";
        copia.SetUVs(3, conLleva);
        float[] esOjo = Ojos(original, ojo, radioOjo);
        var normalesYOjos = new List<Vector4>(normalesPose.Length);
        for (int i = 0; i < normalesPose.Length; i++)
            normalesYOjos.Add(new Vector4(normalesPose[i].x, normalesPose[i].y, normalesPose[i].z, esOjo[i]));
        copia.SetUVs(4, normalesYOjos);
        int partes = copia.subMeshCount;
        var todos = new List<int>();
        for (int i = 0; i < partes; i++) todos.AddRange(copia.GetTriangles(i));
        copia.subMeshCount = partes + 1;
        copia.SetTriangles(todos, partes, false);
        copia.UploadMeshData(false);
        conPose[(original, clave)] = copia;
        ojosDe[copia] = Centros(pose, esOjo);
        return copia;
    }

    // Los puntos de los ojos: los triángulos de la textura cerca de ojo (dentro de radio), conectados con los que tocan
    // ese punto (así no se agarra algo vecino de la textura que no es el ojo). Los dos ojos suelen compartir la misma zona.
    private static float[] Ojos(Mesh malla, Vector2 ojo, float radio)
    {
        var es = new float[malla.vertexCount];
        if (radio <= 0f) return es;
        Vector2[] uv = malla.uv;
        int[] tri = malla.triangles;
        if (uv == null || uv.Length != es.Length) return es;

        int n = tri.Length / 3;
        var cerca = new List<int>();
        var semillas = new List<int>();
        for (int t = 0; t < n; t++)
        {
            Vector2 a = uv[tri[3 * t]], b = uv[tri[3 * t + 1]], c = uv[tri[3 * t + 2]];
            if (((a + b + c) / 3f - ojo).magnitude > radio) continue;
            cerca.Add(t);
            if (Adentro(ojo, a, b, c)) semillas.Add(t);
        }

        // Se juntan los triángulos cercanos que comparten puntos, desde los que tocan el ojo.
        var porPunto = new Dictionary<int, List<int>>();
        foreach (int t in cerca)
            for (int k = 0; k < 3; k++)
            {
                if (!porPunto.TryGetValue(tri[3 * t + k], out List<int> l)) porPunto[tri[3 * t + k]] = l = new List<int>();
                l.Add(t);
            }
        var visto = new HashSet<int>(semillas);
        var cola = new Queue<int>(semillas);
        while (cola.Count > 0)
        {
            int t = cola.Dequeue();
            for (int k = 0; k < 3; k++)
            {
                int p = tri[3 * t + k];
                es[p] = 1f;
                foreach (int otro in porPunto[p]) if (visto.Add(otro)) cola.Enqueue(otro);
            }
        }
        return es;
    }

    // Los dos ojos: los puntos de ojo partidos en dos grupos por donde más se separan (suelen ser izquierdo y derecho);
    // de cada uno, el centro y hasta dónde llega. Sin ojos: radio 0.
    private static Vector4[] Centros(Vector3[] pose, float[] esOjo)
    {
        var puntos = new List<Vector3>();
        for (int i = 0; i < pose.Length; i++) if (esOjo[i] > 0.5f) puntos.Add(pose[i]);
        var res = new Vector4[2];
        if (puntos.Count == 0) return res;
        Vector3 min = puntos[0], max = puntos[0];
        foreach (Vector3 p in puntos) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
        Vector3 tam = max - min, medio = (min + max) * 0.5f;
        int eje = tam.x >= tam.y && tam.x >= tam.z ? 0 : tam.y >= tam.z ? 1 : 2;
        var grupos = new[] { new List<Vector3>(), new List<Vector3>() };
        foreach (Vector3 p in puntos) grupos[p[eje] < medio[eje] ? 0 : 1].Add(p);
        for (int g = 0; g < 2; g++)
        {
            List<Vector3> l = grupos[g].Count > 0 ? grupos[g] : grupos[1 - g];
            Vector3 c = Vector3.zero;
            foreach (Vector3 p in l) c += p;
            c /= l.Count;
            float r = 0f;
            foreach (Vector3 p in l) r = Mathf.Max(r, (p - c).magnitude);
            res[g] = new Vector4(c.x, c.y, c.z, Mathf.Max(r, 0.002f));
        }
        return res;
    }

    private static bool Adentro(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
        float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
        float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    private void Awake() => npc = GetComponent<Npc>();

    private void OnEnable() => HealthSystem.Damaged += AlRecibirDanio;
    private void OnDisable() => HealthSystem.Damaged -= AlRecibirDanio;

    private void AlRecibirDanio(HealthSystem vida, int danio, Vector3? desde)
    {
        if (npc != null && vida == npc.Vida) golpeEn = Time.time;
    }

    private void Update()
    {
        if (material == null) return;
        float destello = 1f - Mathf.Clamp01((Time.time - golpeEn) / DuraElDestello);
        material.SetFloat(DestelloId, destello * destello);

        if (murioEn < 0f && npc != null && !npc.Vivo) murioEn = Time.time;
        material.SetFloat(ApagadoId, murioEn < 0f ? 0f : Mathf.Clamp01((Time.time - murioEn) / TardaEnApagarse));
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
