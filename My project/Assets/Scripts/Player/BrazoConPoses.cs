using System.Collections.Generic;
using UnityEngine;

// US 395: el brazo derecho de la foto de la katana (BrazosEnCamara), con una foto más por cada pose de ataque (la
// katana de muestra "Pose Tajo", etc.: MovimientoDeKatana). Cada foto es el mismo brazo doblado de otra manera (los
// vértices son los mismos, en otro lugar), así que entre la quieta y las poses se pasa mezclando los vértices: el brazo
// se dobla de a poco y la mano acompaña a la katana. Lo pone BrazosEnCamara; lo mueve MovimientoDeKatana (Mezclar).
public class BrazoConPoses : MonoBehaviour
{
    private Mesh malla;
    private Vector3[] quieto, normalesQuieto, ahora, normalesAhora;
    private readonly Dictionary<string, Vector3[]> poses = new Dictionary<string, Vector3[]>();
    private readonly Dictionary<string, Vector3[]> normalesPoses = new Dictionary<string, Vector3[]>();
    private bool movido;
    private int[] cambian = new int[0]; // los vértices que se mueven en alguna pose (los del brazo derecho)

    public void Iniciar(Mesh malla)
    {
        this.malla = malla;
        malla.MarkDynamic();
        quieto = malla.vertices;
        normalesQuieto = malla.normals;
        ahora = (Vector3[])quieto.Clone();
        normalesAhora = (Vector3[])normalesQuieto.Clone();
        // Que no desaparezca al moverse fuera de su caja original.
        Bounds caja = malla.bounds;
        caja.Expand(1.5f);
        malla.bounds = caja;
    }

    /// <summary>La foto del brazo en esa pose (los vértices, en el espacio de este brazo).</summary>
    public void AgregarPose(string nombre, Vector3[] vertices, Vector3[] normales)
    {
        if (quieto == null || vertices == null || vertices.Length != quieto.Length) return;
        poses[nombre] = vertices;
        normalesPoses[nombre] = normales != null && normales.Length == vertices.Length ? normales : normalesQuieto;
        var lista = new HashSet<int>(cambian);
        for (int i = 0; i < vertices.Length; i++) if ((vertices[i] - quieto[i]).sqrMagnitude > 1e-8f) lista.Add(i);
        cambian = new int[lista.Count];
        lista.CopyTo(cambian);
    }

    /// <summary>US 395 (espectador): el mismo brazo con sus poses en otra copia ("destino"), con una malla propia
    /// para que mezclar las poses ahí no mueva el brazo de este.</summary>
    public BrazoConPoses CopiarEn(MeshFilter destino)
    {
        if (malla == null || destino == null) return null;
        Mesh propia = Instantiate(malla);
        propia.name = malla.name + " (copia)";
        propia.SetVertices(quieto);
        propia.SetNormals(normalesQuieto);
        destino.sharedMesh = propia;
        BrazoConPoses copia = destino.gameObject.AddComponent<BrazoConPoses>();
        copia.malla = propia;
        copia.mallaPropia = true;
        propia.MarkDynamic();
        copia.quieto = quieto;
        copia.normalesQuieto = normalesQuieto;
        copia.ahora = (Vector3[])quieto.Clone();
        copia.normalesAhora = (Vector3[])normalesQuieto.Clone();
        foreach (KeyValuePair<string, Vector3[]> p in poses) copia.poses[p.Key] = p.Value;
        foreach (KeyValuePair<string, Vector3[]> p in normalesPoses) copia.normalesPoses[p.Key] = p.Value;
        copia.cambian = cambian;
        return copia;
    }

    private bool mallaPropia;
    private void OnDestroy()
    {
        if (mallaPropia && malla != null) Destroy(malla);
    }

    public bool Tiene(string nombre) => poses.ContainsKey(nombre);
    public int Cuantas => poses.Count;

    /// <summary>Pone el brazo en la mezcla de esas poses (peso de 0 a 1 de cada una; lo que falta es la quieta).</summary>
    public void Mezclar(IReadOnlyList<KeyValuePair<string, float>> pesos)
    {
        if (malla == null) return;
        bool alguna = false;
        foreach (KeyValuePair<string, float> p in pesos) if (p.Value > 0.001f && poses.ContainsKey(p.Key)) { alguna = true; break; }
        if (!alguna)
        {
            if (!movido) return;
            malla.SetVertices(quieto);
            malla.SetNormals(normalesQuieto);
            movido = false;
            return;
        }
        foreach (int i in cambian) { ahora[i] = quieto[i]; normalesAhora[i] = normalesQuieto[i]; }
        foreach (KeyValuePair<string, float> p in pesos)
        {
            float w = p.Value;
            if (w <= 0.001f || !poses.TryGetValue(p.Key, out Vector3[] pose)) continue;
            Vector3[] normales = normalesPoses[p.Key];
            foreach (int i in cambian)
            {
                ahora[i] += (pose[i] - quieto[i]) * w;
                normalesAhora[i] += (normales[i] - normalesQuieto[i]) * w;
            }
        }
        malla.SetVertices(ahora);
        malla.SetNormals(normalesAhora);
        movido = true;
    }
}
