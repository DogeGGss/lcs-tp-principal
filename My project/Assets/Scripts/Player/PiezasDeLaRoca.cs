using System.Collections.Generic;
using UnityEngine;

// Los detalles de la Roca (escopeta de bombeo) en primera persona (US 248): después de cada tiro, el guardamanos (la
// pieza de abajo del caño, donde va la mano izquierda) va hacia atrás y vuelve: el bombeo. Coincide con el sonido del
// bombeo de ArmaDeFuego (pumpDelay, 0,35 s) y termina antes de poder volver a disparar. La mano izquierda lo acompaña
// (AnimacionPrimeraPersona la corre con ManoIzquierda). Lo maneja AnimacionPrimeraPersona.
// El modelo (Grenade_1.fbx, a pesar del nombre) es una sola malla de piezas sueltas: el guardamanos se separa en otra
// malla para poder moverlo; para eso el modelo tiene que tener Read/Write prendido. Las medidas están en las
// coordenadas del modelo (x hacia la boca, z hacia arriba); Unity da vuelta la x al importarlo: en la malla un punto
// (x, y, z) del modelo está en (-x, y, z) * escala.
public class PiezasDeLaRoca : IPiezasDelArma
{
    private const float LargoModelo = 1.808f;    // de punta a punta en x (de -1.057 a 0.751)
    private const float Recorrido = 0.12f;       // cuánto va hacia atrás el guardamanos
    private const float Empieza = 0.22f, Atras = 0.35f, Termina = 0.5f; // s después del tiro

    private readonly Transform arma;     // la que tiene la malla
    private readonly MeshRenderer render;
    private float escala;
    private bool armada, sinPiezas;
    private Transform guardamanos;
    private float ultimoTiro = -10f;
    private bool bombea;

    public PiezasDeLaRoca(GameObject roca, Transform camara)
    {
        MeshFilter filtro = roca.GetComponent<MeshFilter>();
        if (filtro == null) filtro = roca.GetComponentInChildren<MeshFilter>(true);
        arma = filtro != null ? filtro.transform : roca.transform;
        render = filtro != null ? filtro.GetComponent<MeshRenderer>() : null;
    }

    private int[] padre;
    private int Raiz(int a) { while (padre[a] != a) { padre[a] = padre[padre[a]]; a = padre[a]; } return a; }
    private void Unir(int a, int b) { a = Raiz(a); b = Raiz(b); if (a != b) padre[a] = b; }

    /// <summary>Cuánto está corrido el guardamanos hacia atrás ahora (0 a 1).</summary>
    public float Bombeo
    {
        get
        {
            if (!bombea) return 0f;
            float t = Time.time - ultimoTiro;
            if (t < Empieza || t >= Termina) return 0f;
            return t < Atras ? Mathf.SmoothStep(0f, 1f, (t - Empieza) / (Atras - Empieza))
                             : 1f - Mathf.SmoothStep(0f, 1f, (t - Atras) / (Termina - Atras));
        }
    }

    /// <summary>Cuánto se corrió el guardamanos, en el espacio de esa cámara: la mano izquierda lo sigue.</summary>
    public Vector3 ManoIzquierda(Transform camara)
    {
        float b = Bombeo;
        if (b <= 0f) return Vector3.zero;
        Escala();
        return camara.InverseTransformVector(arma.TransformVector(Vector3.right * (Recorrido * escala * b)));
    }

    private void Escala()
    {
        if (escala > 0f) return;
        MeshFilter filtro = arma.GetComponent<MeshFilter>();
        Mesh malla = filtro != null ? filtro.sharedMesh : null;
        escala = malla != null ? malla.bounds.size.x / LargoModelo : 0.01f;
    }

    private bool Armar()
    {
        if (armada) return !sinPiezas;
        armada = true;
        MeshFilter filtro = arma.GetComponent<MeshFilter>();
        Mesh malla = filtro != null ? filtro.sharedMesh : null;
        if (malla == null || render == null) { sinPiezas = true; return false; }
        Escala();
        if (!malla.isReadable)
        {
            Debug.LogWarning("PiezasDeLaRoca: el modelo de la Roca no tiene Read/Write prendido, el guardamanos no se mueve.");
            sinPiezas = true;
            return false;
        }

        Vector3[] v = malla.vertices;
        padre = new int[v.Length];
        for (int i = 0; i < v.Length; i++) padre[i] = i;
        var porLugar = new Dictionary<Vector3Int, int>();
        float paso = 1f / Mathf.Max(1e-8f, escala * 0.0005f);
        for (int i = 0; i < v.Length; i++)
        {
            Vector3Int clave = Vector3Int.RoundToInt(v[i] * paso);
            if (porLugar.TryGetValue(clave, out int otro)) Unir(i, otro); else porLugar[clave] = i;
        }
        for (int s = 0; s < malla.subMeshCount; s++)
        {
            int[] t = malla.GetTriangles(s);
            for (int i = 0; i < t.Length; i += 3) { Unir(t[i], t[i + 1]); Unir(t[i], t[i + 2]); }
        }

        // El guardamanos: la pieza de abajo del caño, adelante del gatillo (x entre 0,25 y 0,6; z entre 0,09 y 0,21).
        var minX = new Dictionary<int, float>(); var maxX = new Dictionary<int, float>();
        var minZ = new Dictionary<int, float>(); var maxZ = new Dictionary<int, float>();
        for (int i = 0; i < v.Length; i++)
        {
            int r = Raiz(i);
            float x = -v[i].x / escala, z = v[i].z / escala;
            minX[r] = minX.TryGetValue(r, out float a) ? Mathf.Min(a, x) : x;
            maxX[r] = maxX.TryGetValue(r, out float b) ? Mathf.Max(b, x) : x;
            minZ[r] = minZ.TryGetValue(r, out float c) ? Mathf.Min(c, z) : z;
            maxZ[r] = maxZ.TryGetValue(r, out float d) ? Mathf.Max(d, z) : z;
        }
        System.Func<int, bool> EsGuardamanos = vertice =>
        {
            int r = Raiz(vertice);
            return minX[r] > 0.25f && maxX[r] < 0.6f && minZ[r] > 0.09f && maxZ[r] < 0.21f;
        };

        Mesh resto = Object.Instantiate(malla);
        Mesh pieza = Object.Instantiate(malla);
        resto.name = malla.name + " (sin guardamanos)";
        pieza.name = malla.name + " (guardamanos)";
        int enPieza = 0;
        var queda = new List<int>();
        var mueve = new List<int>();
        for (int s = 0; s < malla.subMeshCount; s++)
        {
            int[] t = malla.GetTriangles(s);
            queda.Clear();
            mueve.Clear();
            for (int i = 0; i < t.Length; i += 3)
            {
                List<int> lista = EsGuardamanos(t[i]) ? mueve : queda;
                lista.Add(t[i]); lista.Add(t[i + 1]); lista.Add(t[i + 2]);
            }
            enPieza += mueve.Count;
            resto.SetTriangles(queda, s);
            pieza.SetTriangles(mueve, s);
        }
        padre = null;
        if (enPieza == 0)
        {
            Object.Destroy(resto);
            Object.Destroy(pieza);
            sinPiezas = true;
            return false;
        }
        resto.RecalculateBounds();
        pieza.RecalculateBounds();
        filtro.sharedMesh = resto;

        var go = new GameObject("Guardamanos");
        go.layer = arma.gameObject.layer;
        guardamanos = go.transform;
        guardamanos.SetParent(arma, false);
        go.AddComponent<MeshFilter>().sharedMesh = pieza;
        MeshRenderer vista = go.AddComponent<MeshRenderer>();
        vista.sharedMaterials = render.sharedMaterials;
        vista.shadowCastingMode = render.shadowCastingMode;
        vista.receiveShadows = render.receiveShadows;
        return true;
    }

    /// <summary>Un tiro: si le quedan cartuchos, después bombea (como el sonido de ArmaDeFuego).</summary>
    public void Disparo(int balasQueQuedan)
    {
        Armar();
        ultimoTiro = Time.time;
        bombea = balasQueQuedan > 0;
    }

    public void Actualizar(float recarga, int balas)
    {
        if (!Armar() || guardamanos == null) return;
        guardamanos.localPosition = Vector3.right * (Recorrido * escala * Bombeo);
        if (guardamanos.gameObject.layer != arma.gameObject.layer) guardamanos.gameObject.layer = arma.gameObject.layer;
    }

    public void Quieta()
    {
        bombea = false;
        if (guardamanos != null) guardamanos.localPosition = Vector3.zero;
    }

    public void ActualizarSueltas(float dt) { }
}
