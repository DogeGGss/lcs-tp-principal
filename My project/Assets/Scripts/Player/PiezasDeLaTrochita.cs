using System.Collections.Generic;
using UnityEngine;

// Lo que hace AnimacionPrimeraPersona con las piezas del arma: cada disparo, cada cuadro y al guardarla.
public interface IPiezasDelArma
{
    void Disparo(int balasQueQuedan);
    void Actualizar(float recarga, int balas);
    void Quieta();
    void ActualizarSueltas(float dt);
}

// Los detalles de La Trochita en primera persona (US 244): el tambor de arriba gira un sexto de vuelta con cada tiro
// (el arma tiene 6 balas) y da una vuelta entera al terminar de recargar, y el cargador cae de la empuñadura y entra
// uno nuevo al recargar. Lo maneja AnimacionPrimeraPersona.
// El modelo (Pistol_1.fbx) es una sola malla hecha de piezas sueltas: el tambor se separa en otra malla para poder
// girarlo. Las medidas de abajo están en las coordenadas del modelo (x hacia la boca, z hacia arriba, y hacia la
// izquierda); Unity da vuelta la x al importarlo, así que en la malla un punto (x, y, z) del modelo está en
// (-x, y, z) * escala. Para separar el tambor el modelo tiene que tener Read/Write prendido.
public class PiezasDeLaTrochita : IPiezasDelArma
{
    private const float LargoModelo = 0.934f; // de punta a punta en x (de -0.119 a 0.815)
    private static readonly Vector3 Boca = new Vector3(0.815f, 0f, 0.33f);
    private static readonly Vector3 Atras = new Vector3(-0.119f, 0f, 0.33f);
    private static readonly Vector3 PieDelCargador = new Vector3(-0.045f, 0f, -0.125f); // abajo de la empuñadura
    private static readonly Vector3 EjeDelCargador = new Vector3(-0.57f, 0f, -0.82f);  // a lo largo de la empuñadura, hacia abajo
    private static readonly Vector3 TamanoCargador = new Vector3(0.06f, 0.2f, 0.07f);  // ancho, largo, grosor
    private const float GiroPorTiro = 60f;      // grados: 6 balas por vuelta
    private const float TiempoDeGiro = 0.12f;   // s que tarda el tambor en girar después de un tiro

    private readonly Transform arma;     // la que tiene la malla
    private readonly Transform camara;
    private readonly MeshRenderer render;
    private float escala;                // de las medidas del modelo a la malla
    private bool armada, sinPiezas;

    private Transform tambor;
    private Vector3 ejeDelTambor = Vector3.up;
    private float anguloDesde, anguloHasta, giroDesde = -10f;
    private bool vueltaDeRecarga;

    private Transform cargador;
    private MeshRenderer cargadorVista;
    private bool soltado;
    private float recargaAnterior = -1f;

    private struct Suelta { public Transform t; public Vector3 velocidad, eje; public float giro, hasta; }
    private readonly List<Suelta> sueltas = new List<Suelta>();

    public PiezasDeLaTrochita(GameObject trochita, Transform camara)
    {
        this.camara = camara;
        MeshFilter filtro = trochita.GetComponent<MeshFilter>();
        if (filtro == null) filtro = trochita.GetComponentInChildren<MeshFilter>(true);
        arma = filtro != null ? filtro.transform : trochita.transform;
        render = filtro != null ? filtro.GetComponent<MeshRenderer>() : null;
    }

    private int[] padre;
    private int Raiz(int a) { while (padre[a] != a) { padre[a] = padre[padre[a]]; a = padre[a]; } return a; }
    private void Unir(int a, int b) { a = Raiz(a); b = Raiz(b); if (a != b) padre[a] = b; }

    private Vector3 Punto(Vector3 p) => new Vector3(-p.x, p.y, p.z) * escala;
    private static Vector3 Direccion(Vector3 d) => new Vector3(-d.x, d.y, d.z).normalized;
    private int Capa => arma.gameObject.layer;

    private float Largo => (camara.InverseTransformPoint(arma.TransformPoint(Punto(Boca))) -
                            camara.InverseTransformPoint(arma.TransformPoint(Punto(Atras)))).magnitude;

    // Separa el tambor de la malla (una sola vez). Sin Read/Write en el modelo, solo se ve el cargador.
    private bool Armar()
    {
        if (armada) return !sinPiezas;
        armada = true;
        MeshFilter filtro = arma.GetComponent<MeshFilter>();
        Mesh malla = filtro != null ? filtro.sharedMesh : null;
        if (malla == null || render == null) { sinPiezas = true; return false; }
        escala = malla.bounds.size.x / LargoModelo;
        if (!malla.isReadable)
        {
            Debug.LogWarning("PiezasDeLaTrochita: el modelo de La Trochita no tiene Read/Write prendido, el tambor no gira.");
            sinPiezas = true;
            return false;
        }

        // Piezas: vértices unidos por triángulos o por estar en el mismo lugar.
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

        // El tambor es la pieza de arriba y atrás: toda por encima de z = 0,16 y entre x = -0,12 y x = 0,2 (el caño y
        // el armazón de arriba llegan más adelante o más abajo).
        var bajo = new Dictionary<int, float>();
        var adelante = new Dictionary<int, float>();
        var atras = new Dictionary<int, float>();
        for (int i = 0; i < v.Length; i++)
        {
            int r = Raiz(i);
            float z = v[i].z / escala, x = -v[i].x / escala;
            bajo[r] = bajo.TryGetValue(r, out float a) ? Mathf.Min(a, z) : z;
            adelante[r] = adelante.TryGetValue(r, out float b) ? Mathf.Max(b, x) : x;
            atras[r] = atras.TryGetValue(r, out float c) ? Mathf.Min(c, x) : x;
        }
        System.Func<int, bool> EsTambor = vertice => { int r = Raiz(vertice); return bajo[r] > 0.16f && adelante[r] < 0.2f && atras[r] > -0.12f; };

        Mesh resto = Object.Instantiate(malla);
        Mesh soloTambor = Object.Instantiate(malla);
        resto.name = malla.name + " (sin tambor)";
        soloTambor.name = malla.name + " (tambor)";
        int enTambor = 0;
        var queda = new List<int>();
        var gira = new List<int>();
        for (int s = 0; s < malla.subMeshCount; s++)
        {
            int[] t = malla.GetTriangles(s);
            queda.Clear();
            gira.Clear();
            for (int i = 0; i < t.Length; i += 3)
            {
                List<int> lista = EsTambor(t[i]) ? gira : queda;
                lista.Add(t[i]); lista.Add(t[i + 1]); lista.Add(t[i + 2]);
            }
            enTambor += gira.Count;
            resto.SetTriangles(queda, s);
            soloTambor.SetTriangles(gira, s);
        }
        padre = null;
        if (enTambor == 0)
        {
            Object.Destroy(resto);
            Object.Destroy(soloTambor);
            sinPiezas = true;
            return false;
        }
        resto.RecalculateBounds();
        soloTambor.RecalculateBounds();
        filtro.sharedMesh = resto;

        // El tambor gira alrededor de su centro, sobre su lado más corto (es una rueda).
        Bounds caja = soloTambor.bounds;
        Vector3 tam = caja.size;
        ejeDelTambor = tam.x <= tam.y && tam.x <= tam.z ? Vector3.right : tam.y <= tam.z ? Vector3.up : Vector3.forward;

        var eje = new GameObject("Tambor");
        eje.layer = Capa;
        tambor = eje.transform;
        tambor.SetParent(arma, false);
        tambor.localPosition = caja.center;
        var pieza = new GameObject("Tambor (malla)");
        pieza.layer = Capa;
        pieza.transform.SetParent(tambor, false);
        pieza.transform.localPosition = -caja.center;
        pieza.AddComponent<MeshFilter>().sharedMesh = soloTambor;
        MeshRenderer vista = pieza.AddComponent<MeshRenderer>();
        vista.sharedMaterials = render.sharedMaterials;
        vista.shadowCastingMode = render.shadowCastingMode;
        vista.receiveShadows = render.receiveShadows;
        return true;
    }

    /// <summary>Un tiro: el tambor gira un sexto de vuelta.</summary>
    public void Disparo(int balasQueQuedan)
    {
        if (!Armar()) return;
        Girar(GiroPorTiro, TiempoDeGiro);
    }

    private void Girar(float grados, float duracion)
    {
        anguloDesde = Angulo();
        anguloHasta += grados;
        giroDesde = Time.time;
        duracionGiro = Mathf.Max(0.01f, duracion);
    }

    private float duracionGiro = TiempoDeGiro;
    private float Angulo() => Mathf.Lerp(anguloDesde, anguloHasta, Mathf.SmoothStep(0f, 1f, (Time.time - giroDesde) / duracionGiro));

    /// <summary>Cada cuadro, con La Trochita en la mano. "recarga" es el avance de la recarga (de 0 a 1), o -1.</summary>
    public void Actualizar(float recarga, int balas)
    {
        if (Armar() && tambor != null)
        {
            // Al final de la recarga (0,65), el tambor da una vuelta entera: el arma queda armada.
            if (recarga >= 0.65f && !vueltaDeRecarga) { vueltaDeRecarga = true; Girar(360f, 0.3f); }
            if (recarga < 0f) vueltaDeRecarga = false;
            tambor.localRotation = Quaternion.AngleAxis(Angulo(), ejeDelTambor);
            if (tambor.gameObject.layer != Capa) foreach (Transform t in tambor.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Capa;
        }
        Cargador(recarga);
    }

    /// <summary>Sin La Trochita en la mano: el tambor termina su giro y el cargador vuelve adentro.</summary>
    public void Quieta()
    {
        anguloDesde = anguloHasta;
        if (tambor != null) tambor.localRotation = Quaternion.AngleAxis(anguloHasta, ejeDelTambor);
        if (cargadorVista != null) cargadorVista.enabled = false;
        recargaAnterior = -1f;
        soltado = false;
        vueltaDeRecarga = false;
    }

    /// <summary>El cargador vacío que se soltó: cae y se borra. Cada cuadro, haya o no arma en la mano.</summary>
    public void ActualizarSueltas(float dt)
    {
        if (sueltas.Count == 0) return;
        float largo = escala > 0f ? Largo : 0.3f;
        for (int i = sueltas.Count - 1; i >= 0; i--)
        {
            Suelta s = sueltas[i];
            if (s.t == null || Time.time >= s.hasta)
            {
                if (s.t != null) Object.Destroy(s.t.gameObject);
                sueltas.RemoveAt(i);
                continue;
            }
            s.velocidad += camara.InverseTransformDirection(Vector3.down) * (14f * largo * dt);
            s.t.localPosition += s.velocidad * dt;
            s.t.Rotate(s.eje, s.giro * dt, Space.Self);
            sueltas[i] = s;
        }
    }

    // ---------- Cargador ----------

    // Sale (de 0,12 a 0,30 de la recarga), se suelta y cae; entra uno nuevo (de 0,40 a 0,60, cuando el arma da el
    // empujón). Escondido adentro de la empuñadura no se ve.
    private void Cargador(float recarga)
    {
        if (recarga < 0f || recarga < recargaAnterior) soltado = false; // terminó, o empezó otra
        recargaAnterior = recarga;
        if (recarga < 0f || escala <= 0f)
        {
            if (cargadorVista != null) cargadorVista.enabled = false;
            if (escala <= 0f) EscalaSinPiezas();
            return;
        }
        if (cargador == null && !CrearCargador()) return;

        float largoCargador = TamanoCargador.y * escala;
        float fuera = 0f;
        if (recarga >= 0.12f && recarga < 0.30f) fuera = Mathf.SmoothStep(0f, 1f, (recarga - 0.12f) / 0.18f);
        else if (recarga >= 0.30f && !soltado)
        {
            soltado = true;
            UbicarCargador(largoCargador);
            Soltar();
        }
        if (recarga >= 0.40f && recarga < 0.60f) fuera = 1f - Mathf.SmoothStep(0f, 1f, (recarga - 0.40f) / 0.20f);

        UbicarCargador(largoCargador * fuera);
        cargadorVista.enabled = fuera > 0.02f;
        if (cargador.gameObject.layer != Capa) cargador.gameObject.layer = Capa;
    }

    // Sin poder separar el tambor igual hace falta la escala para el cargador.
    private void EscalaSinPiezas()
    {
        MeshFilter filtro = arma.GetComponent<MeshFilter>();
        if (filtro != null && filtro.sharedMesh != null) escala = filtro.sharedMesh.bounds.size.x / LargoModelo;
    }

    private bool CrearCargador()
    {
        if (render == null) return false;
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(go.GetComponent<Collider>());
        go.name = "Cargador";
        go.layer = Capa;
        cargadorVista = go.GetComponent<MeshRenderer>();
        Material oscuro = render.sharedMaterial;
        foreach (Material m in render.sharedMaterials)
            if (m != null && (m.name.ToLowerInvariant().Contains("black") || m.name.ToLowerInvariant().Contains("dark"))) { oscuro = m; break; }
        cargadorVista.sharedMaterial = oscuro;
        cargadorVista.enabled = false;
        cargador = go.transform;
        cargador.SetParent(arma, false);
        Vector3 abajo = Direccion(EjeDelCargador);
        cargador.localRotation = Quaternion.LookRotation(Vector3.up, -abajo); // largo a lo largo de la empuñadura
        cargador.localScale = TamanoCargador * escala;
        return true;
    }

    // "fuera": cuánto asoma por abajo de la empuñadura (0 = adentro).
    private void UbicarCargador(float fuera)
    {
        Vector3 abajo = Direccion(EjeDelCargador);
        cargador.localPosition = Punto(PieDelCargador) - abajo * (TamanoCargador.y * escala * 0.5f) + abajo * fuera;
    }

    private void Soltar()
    {
        GameObject vacio = Object.Instantiate(cargador.gameObject, cargador.position, cargador.rotation);
        vacio.name = "Cargador vacío";
        Transform t = vacio.transform;
        t.localScale = cargador.lossyScale;
        t.SetParent(camara, true);
        vacio.GetComponent<MeshRenderer>().enabled = true;
        float largo = Largo;
        Vector3 caida = camara.InverseTransformDirection(arma.TransformDirection(Direccion(EjeDelCargador)));
        sueltas.Add(new Suelta
        {
            t = t,
            velocidad = caida * (1.2f * largo),
            eje = Vector3.right,
            giro = Random.Range(-120f, 120f),
            hasta = Time.time + 0.7f,
        });
    }
}
