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
// (el arma tiene 6 balas). Como el tambor es liso, tiene 6 rayitas en cada cara que apuntan al centro, una por bala:
// naranjas las cargadas y oscuras las vacías; así se ve el giro y cuántas balas quedan. La recarga es como el
// enfriamiento de un arma que se recalienta: AnimacionPrimeraPersona levanta el arma de costado mostrando el tambor, que
// gira rápido y va frenando (3 vueltas) mientras las rayitas se prenden de a una. Lo maneja AnimacionPrimeraPersona.
// El modelo (Pistol_1.fbx) es una sola malla hecha de piezas sueltas: el tambor se separa en otra malla para poder
// girarlo. Las medidas de abajo están en las coordenadas del modelo (x hacia la boca, z hacia arriba, y hacia la
// izquierda); Unity da vuelta la x al importarlo, así que en la malla un punto (x, y, z) del modelo está en
// (-x, y, z) * escala. Para separar el tambor el modelo tiene que tener Read/Write prendido.
public class PiezasDeLaTrochita : IPiezasDelArma
{
    private const float LargoModelo = 0.934f; // de punta a punta en x (de -0.119 a 0.815)
    private const float GiroPorTiro = 60f;      // grados: 6 balas por vuelta
    private const float TiempoDeGiro = 0.12f;   // s que tarda el tambor en girar después de un tiro
    private const int VueltasDeRecarga = 3;     // vueltas enteras: las rayitas quedan en el mismo lugar
    private const float EmpiezaGiro = 0.12f, TerminaGiro = 0.85f; // parte de la recarga en que gira el tambor

    private readonly Transform arma;     // la que tiene la malla
    private readonly MeshRenderer render;
    private float escala;                // de las medidas del modelo a la malla
    private bool armada, sinPiezas;

    private Transform tambor;
    private Vector3 ejeDelTambor = Vector3.up;
    private float anguloDesde, anguloHasta, giroDesde = -10f;
    private float anguloAlRecargar;
    private bool recargando;

    // Marcas de las balas en las dos caras del tambor: una por bala (6), naranjas las cargadas y oscuras las vacías.
    // La de arriba es la que dispara: con cada tiro se apaga y el tambor la lleva hacia atrás, así se ve que gira.
    private const int Balas = 6;
    private MeshRenderer[] marcas;       // cara izquierda (0 a 5) y derecha (6 a 11)
    private int disparos;                // tiros desde que se armó: qué marca está arriba
    private int balasAlRecargar;
    private static Material cargada, vacia;

    public PiezasDeLaTrochita(GameObject trochita, Transform camara)
    {
        MeshFilter filtro = trochita.GetComponent<MeshFilter>();
        if (filtro == null) filtro = trochita.GetComponentInChildren<MeshFilter>(true);
        arma = filtro != null ? filtro.transform : trochita.transform;
        render = filtro != null ? filtro.GetComponent<MeshRenderer>() : null;
    }

    private int[] padre;
    private int Raiz(int a) { while (padre[a] != a) { padre[a] = padre[padre[a]]; a = padre[a]; } return a; }
    private void Unir(int a, int b) { a = Raiz(a); b = Raiz(b); if (a != b) padre[a] = b; }

    private int Capa => arma.gameObject.layer;

    // Separa el tambor de la malla (una sola vez). Sin Read/Write en el modelo, el arma se anima sin el tambor.
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
        bool hayCaja = false;
        Bounds caja = default;
        var queda = new List<int>();
        var gira = new List<int>();
        for (int s = 0; s < malla.subMeshCount; s++)
        {
            int[] t = malla.GetTriangles(s);
            queda.Clear();
            gira.Clear();
            for (int i = 0; i < t.Length; i += 3)
            {
                bool delTambor = EsTambor(t[i]);
                List<int> lista = delTambor ? gira : queda;
                lista.Add(t[i]); lista.Add(t[i + 1]); lista.Add(t[i + 2]);
                // Caja del tambor, solo con sus vértices (la malla copiada guarda todos los del arma).
                if (delTambor)
                    for (int k = 0; k < 3; k++)
                    {
                        if (!hayCaja) { caja = new Bounds(v[t[i + k]], Vector3.zero); hayCaja = true; }
                        else caja.Encapsulate(v[t[i + k]]);
                    }
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
        CrearMarcas(tam);
        return true;
    }

    // Las 6 marcas de cada cara: rayitas que apuntan al centro, repartidas en círculo. La marca s queda a -60° * s: después de s
    // tiros (s sextos de vuelta) está arriba.
    private void CrearMarcas(Vector3 tam)
    {
        Vector3 eje = ejeDelTambor;
        Vector3 u = eje == Vector3.up ? Vector3.forward : Vector3.up;   // "arriba" en la cara del tambor
        Vector3 w = Vector3.Cross(eje, u);
        float grosor = Vector3.Scale(tam, eje).magnitude;
        float radio = (tam.x + tam.y + tam.z - grosor) * 0.25f;          // la mitad del promedio de los otros dos lados
        float largo = radio * 0.38f, ancho = radio * 0.09f;  // rayitas que apuntan al centro
        Mesh barra = Barra();
        marcas = new MeshRenderer[Balas * 2];
        for (int cara = 0; cara < 2; cara++)
            for (int s = 0; s < Balas; s++)
            {
                float a = -s * GiroPorTiro * Mathf.Deg2Rad;
                Vector3 haciaAfuera = u * Mathf.Cos(a) + w * Mathf.Sin(a);
                Vector3 lugar = haciaAfuera * (radio * 0.62f) + eje * ((cara == 0 ? 1f : -1f) * grosor * 0.5f);
                var go = new GameObject("Marca de bala " + (s + 1));
                go.layer = Capa;
                go.transform.SetParent(tambor, false);
                go.transform.localPosition = lugar;
                // El largo (z del cubo) va del centro hacia el borde; el grosor (y), a lo largo del eje del tambor.
                go.transform.localRotation = Quaternion.LookRotation(haciaAfuera, eje);
                go.transform.localScale = new Vector3(ancho, grosor * 0.03f, largo);
                go.AddComponent<MeshFilter>().sharedMesh = barra;
                MeshRenderer r = go.AddComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                marcas[cara * Balas + s] = r;
            }
    }

    private static Mesh barra;
    private static Mesh Barra()
    {
        if (barra != null) return barra;
        GameObject temporal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        barra = temporal.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(temporal);
        return barra;
    }

    private static Material MaterialDeMarca(bool encendida)
    {
        if (encendida ? cargada != null : vacia != null) return encendida ? cargada : vacia;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        // Naranja más fuerte que 1, como los detalles del arma: con el Bloom brilla. Vacía: casi negra.
        UnityEngine.Color c = encendida ? new UnityEngine.Color(1f, 0.5f, 0.12f) * 3f : new UnityEngine.Color(0.06f, 0.06f, 0.07f);
        var m = new Material(shader) { name = encendida ? "Bala cargada (La Trochita)" : "Bala vacía (La Trochita)", color = c };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (encendida) cargada = m; else vacia = m;
        return m;
    }

    // Prende las "cuantas" marcas que siguen a la de arriba (la de arriba incluida).
    private void Marcas(int cuantas)
    {
        if (marcas == null) return;
        for (int i = 0; i < marcas.Length; i++)
        {
            int s = i % Balas;
            bool encendida = ((s - disparos) % Balas + Balas) % Balas < cuantas;
            Material m = MaterialDeMarca(encendida);
            if (marcas[i] != null && marcas[i].sharedMaterial != m) marcas[i].sharedMaterial = m;
        }
    }

    /// <summary>Un tiro: el tambor gira un sexto de vuelta.</summary>
    public void Disparo(int balasQueQuedan)
    {
        if (!Armar()) return;
        disparos++;
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
        if (!Armar() || tambor == null) return;

        int encendidas = balas;
        if (recarga >= 0f)
        {
            // Recarga: el tambor gira rápido y va frenando, como enfriándose; las rayitas se prenden de a una.
            if (!recargando)
            {
                recargando = true;
                anguloAlRecargar = Angulo();
                balasAlRecargar = balas;
            }
            float avance = Mathf.InverseLerp(EmpiezaGiro, TerminaGiro, recarga);
            float frenando = 1f - (1f - avance) * (1f - avance) * (1f - avance); // rápido al principio, lento al final
            float angulo = anguloAlRecargar + 360f * VueltasDeRecarga * frenando;
            anguloDesde = anguloHasta = anguloAlRecargar;
            tambor.localRotation = Quaternion.AngleAxis(angulo, ejeDelTambor);
            encendidas = Mathf.Max(balasAlRecargar, Mathf.FloorToInt(avance * Balas + 0.0001f));
        }
        else
        {
            if (recargando) TerminarRecarga();
            tambor.localRotation = Quaternion.AngleAxis(Angulo(), ejeDelTambor);
        }
        Marcas(Mathf.Clamp(encendidas, 0, Balas));
        if (tambor.gameObject.layer != Capa) foreach (Transform t in tambor.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Capa;
    }

    // Terminó (o se cortó) la recarga: el tambor vuelve a donde estaba (dio vueltas enteras, las rayitas no cambian).
    private void TerminarRecarga()
    {
        recargando = false;
        anguloDesde = anguloHasta = anguloAlRecargar;
        giroDesde = -10f;
    }

    /// <summary>Sin La Trochita en la mano: el tambor termina su giro.</summary>
    public void Quieta()
    {
        if (recargando) TerminarRecarga();
        anguloDesde = anguloHasta;
        if (tambor != null) tambor.localRotation = Quaternion.AngleAxis(anguloHasta, ejeDelTambor);
    }

    /// <summary>La Trochita no suelta nada (no tiene cargador que se caiga).</summary>
    public void ActualizarSueltas(float dt) { }
}
