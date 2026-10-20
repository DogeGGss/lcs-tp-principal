using System.Collections.Generic;
using UnityEngine;

// Los detalles del Mitre (fusil) en primera persona (US 250). El modelo (AR_1.fbx) no tiene un cargador aparte: el
// cargador es la pieza de abajo del caño, adelante del gatillo, donde va la mano izquierda. Al recargar la mano lo saca
// para abajo, lo suelta (cae), sale de la pantalla a buscar otro y lo trae; después tira de la palanca de carga (la
// varilla de arriba, atrás) y el arma da un tirón. Lo maneja AnimacionPrimeraPersona, que mueve la mano izquierda con
// el cargador (ManoIzquierda).
// Las dos piezas se separan en otras mallas para poder moverlas; para eso el modelo tiene que tener Read/Write prendido.
// Las medidas están en las coordenadas del modelo (x hacia la boca, z hacia arriba); Unity da vuelta la x al
// importarlo: en la malla un punto (x, y, z) del modelo está en (-x, y, z) * escala.
public class PiezasDelMitre : IPiezasDelArma
{
    private const float LargoModelo = 1.891f;   // de punta a punta en x (de -0.905 a 0.986)
    private const float Saca = 0.2f;            // cuánto baja el cargador para salir entero (mide 0,13)
    private const float Lejos = 1.0f;           // hasta dónde baja la mano a buscar otro (sale de la pantalla)
    private const float RecorridoPalanca = 0.12f;

    // Momentos de la recarga (0 a 1, 2,5 s en total).
    public const float SacaDesde = 0.12f, SacaHasta = 0.28f;   // lo saca
    public const float BuscaHasta = 0.40f, TraeDesde = 0.44f;  // baja a buscar otro y lo trae
    public const float Llega = 0.56f, Entra = 0.62f;           // lo acerca y lo mete (empujón a los 0,6)
    public const float Palanca = 0.78f;                         // tira de la palanca de carga

    private readonly Transform arma;     // la que tiene la malla
    private readonly Transform camara;
    private readonly MeshRenderer render;
    private float escala;
    private bool armada, sinPiezas, soltado;
    private Transform cargador, palanca;
    private Renderer vistaCargador;
    private float recargaAnterior = -1f;
    private float bajada;                // cuánto está abajo la mano izquierda ahora (en el modelo)

    private struct Suelta { public Transform t; public Vector3 velocidad, eje; public float giro, hasta; }
    private readonly List<Suelta> sueltas = new List<Suelta>();

    public PiezasDelMitre(GameObject mitre, Transform camara)
    {
        this.camara = camara;
        MeshFilter filtro = mitre.GetComponent<MeshFilter>();
        if (filtro == null) filtro = mitre.GetComponentInChildren<MeshFilter>(true);
        arma = filtro != null ? filtro.transform : mitre.transform;
        render = filtro != null ? filtro.GetComponent<MeshRenderer>() : null;
    }

    private int[] padre;
    private int Raiz(int a) { while (padre[a] != a) { padre[a] = padre[padre[a]]; a = padre[a]; } return a; }
    private void Unir(int a, int b) { a = Raiz(a); b = Raiz(b); if (a != b) padre[a] = b; }

    private void Escala()
    {
        if (escala > 0f) return;
        MeshFilter filtro = arma.GetComponent<MeshFilter>();
        Mesh malla = filtro != null ? filtro.sharedMesh : null;
        escala = malla != null ? malla.bounds.size.x / LargoModelo : 0.01f;
    }

    /// <summary>Cuánto se corrió la mano izquierda (con el cargador), en el espacio de esa cámara.</summary>
    public Vector3 ManoIzquierda(Transform camara)
    {
        if (bajada <= 0f) return Vector3.zero;
        Escala();
        return camara.InverseTransformVector(arma.TransformVector(Vector3.back * (bajada * escala)));
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
            Debug.LogWarning("PiezasDelMitre: el modelo del Mitre no tiene Read/Write prendido, el cargador no se mueve.");
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
        // 0 = queda, 1 = cargador (abajo del caño: x de 0,32 a 0,75; z de -0,04 a 0,08),
        // 2 = palanca (la varilla de arriba, atrás: x de -0,54 a -0,13; z de 0,22 a 0,24).
        System.Func<int, int> Pieza = vertice =>
        {
            int r = Raiz(vertice);
            if (minX[r] > 0.3f && maxX[r] < 0.76f && minZ[r] > -0.06f && maxZ[r] < 0.09f) return 1;
            if (minX[r] > -0.55f && maxX[r] < -0.12f && minZ[r] > 0.21f && maxZ[r] < 0.25f) return 2;
            return 0;
        };

        Mesh[] mallas = { Object.Instantiate(malla), Object.Instantiate(malla), Object.Instantiate(malla) };
        mallas[0].name = malla.name + " (sin cargador ni palanca)";
        mallas[1].name = malla.name + " (cargador)";
        mallas[2].name = malla.name + " (palanca)";
        int[] cuantos = new int[3];
        var listas = new[] { new List<int>(), new List<int>(), new List<int>() };
        for (int s = 0; s < malla.subMeshCount; s++)
        {
            int[] t = malla.GetTriangles(s);
            foreach (List<int> l in listas) l.Clear();
            for (int i = 0; i < t.Length; i += 3)
            {
                List<int> lista = listas[Pieza(t[i])];
                lista.Add(t[i]); lista.Add(t[i + 1]); lista.Add(t[i + 2]);
            }
            for (int p = 0; p < 3; p++) { cuantos[p] += listas[p].Count; mallas[p].SetTriangles(listas[p], s); }
        }
        padre = null;
        if (cuantos[1] == 0)
        {
            foreach (Mesh m in mallas) Object.Destroy(m);
            sinPiezas = true;
            return false;
        }
        foreach (Mesh m in mallas) m.RecalculateBounds();
        filtro.sharedMesh = mallas[0];

        cargador = Crear("Cargador", mallas[1]);
        vistaCargador = cargador.GetComponent<Renderer>();
        if (cuantos[2] > 0) palanca = Crear("Palanca de carga", mallas[2]);
        else Object.Destroy(mallas[2]);
        return true;
    }

    private Transform Crear(string nombre, Mesh malla)
    {
        var go = new GameObject(nombre);
        go.layer = arma.gameObject.layer;
        go.transform.SetParent(arma, false);
        go.AddComponent<MeshFilter>().sharedMesh = malla;
        MeshRenderer vista = go.AddComponent<MeshRenderer>();
        vista.sharedMaterials = render.sharedMaterials;
        vista.shadowCastingMode = render.shadowCastingMode;
        vista.receiveShadows = render.receiveShadows;
        return go.transform;
    }

    public void Disparo(int balasQueQuedan) { }

    public void Actualizar(float recarga, int balas)
    {
        if (recarga < 0f || recarga < recargaAnterior) soltado = false; // terminó, o empezó otra
        recargaAnterior = recarga;
        if (recarga < 0f) { Quieta(); return; }
        if (!Armar()) { bajada = 0f; return; }

        // Dónde está la mano izquierda (hacia abajo, en el modelo) y si tiene un cargador.
        float mano = 0f;
        bool conCargador = true;
        if (recarga >= SacaDesde && recarga < SacaHasta) mano = Saca * Mathf.SmoothStep(0f, 1f, (recarga - SacaDesde) / (SacaHasta - SacaDesde));
        else if (recarga >= SacaHasta && recarga < TraeDesde)
        {
            if (!soltado) { soltado = true; Soltar(); }
            conCargador = false;
            mano = Mathf.Lerp(Saca, Lejos, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SacaHasta, BuscaHasta, recarga)));
        }
        else if (recarga >= TraeDesde && recarga < Llega) mano = Mathf.Lerp(Lejos, Saca, Mathf.SmoothStep(0f, 1f, (recarga - TraeDesde) / (Llega - TraeDesde)));
        else if (recarga >= Llega && recarga < Entra) mano = Saca * (1f - Mathf.SmoothStep(0f, 1f, (recarga - Llega) / (Entra - Llega)));
        bajada = mano;

        cargador.localPosition = Vector3.back * (mano * escala);
        if (vistaCargador != null && vistaCargador.enabled != conCargador) vistaCargador.enabled = conCargador;
        if (palanca != null)
        {
            float tiron = Mathf.Exp(-Mathf.Pow((recarga - Palanca) / 0.045f, 2f));
            palanca.localPosition = Vector3.right * (RecorridoPalanca * escala * tiron);
        }
        Capa(cargador);
        Capa(palanca);
    }

    private void Capa(Transform t)
    {
        if (t != null && t.gameObject.layer != arma.gameObject.layer) t.gameObject.layer = arma.gameObject.layer;
    }

    public void Quieta()
    {
        bajada = 0f;
        recargaAnterior = -1f;
        soltado = false;
        if (cargador != null) cargador.localPosition = Vector3.zero;
        if (vistaCargador != null) vistaCargador.enabled = true;
        if (palanca != null) palanca.localPosition = Vector3.zero;
    }

    // El cargador vacío: una copia que cae y se borra.
    private void Soltar()
    {
        if (cargador == null) return;
        GameObject vacio = Object.Instantiate(cargador.gameObject, cargador.position, cargador.rotation);
        vacio.name = "Cargador vacío";
        Transform t = vacio.transform;
        t.localScale = cargador.lossyScale;
        t.SetParent(camara, true);
        Renderer r = vacio.GetComponent<Renderer>();
        if (r != null) r.enabled = true;
        float largo = Largo();
        Vector3 caida = camara.InverseTransformDirection(arma.TransformDirection(Vector3.back));
        sueltas.Add(new Suelta
        {
            t = t,
            velocidad = caida * (0.5f * largo),
            eje = Vector3.right,
            giro = Random.Range(-90f, 90f),
            hasta = Time.time + 0.8f,
        });
    }

    private float Largo()
    {
        Escala();
        return camara.InverseTransformVector(arma.TransformVector(Vector3.right * LargoModelo * escala)).magnitude;
    }

    public void ActualizarSueltas(float dt)
    {
        if (sueltas.Count == 0) return;
        float largo = Largo();
        for (int i = sueltas.Count - 1; i >= 0; i--)
        {
            Suelta s = sueltas[i];
            if (s.t == null || Time.time >= s.hasta)
            {
                if (s.t != null) Object.Destroy(s.t.gameObject);
                sueltas.RemoveAt(i);
                continue;
            }
            s.velocidad += camara.InverseTransformDirection(Vector3.down) * (9f * largo * dt);
            s.t.localPosition += s.velocidad * dt;
            s.t.Rotate(s.eje, s.giro * dt, Space.Self);
            sueltas[i] = s;
        }
    }
}
