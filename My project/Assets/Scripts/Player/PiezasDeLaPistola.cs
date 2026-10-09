using System.Collections.Generic;
using UnityEngine;

// Los detalles de La Porteña en primera persona: la corredera que va y vuelve en cada tiro (y queda atrás sin balas),
// el fogonazo en la boca del caño, la vaina que sale volando y el cargador que se cae y entra uno nuevo al recargar.
// Lo maneja AnimacionPrimeraPersona.
// El modelo (Pistol_2.fbx) está hecho de piezas sueltas: las de arriba (la corredera, con el caño) se separan en otra
// malla para poder moverlas. Las medidas de abajo están en las coordenadas del modelo en Blender (x hacia la boca,
// z hacia arriba, y hacia la izquierda); Unity da vuelta la x al importarlo, así que en la malla un punto (x, y, z) del
// modelo está en (-x, y, z) * escala. Para separar las piezas el modelo tiene que tener Read/Write prendido.
public class PiezasDeLaPistola
{
    private const float LargoModelo = 0.990f; // de punta a punta en x (de -0.167 a 0.823)
    private static readonly Vector3 Boca = new Vector3(0.823f, 0f, 0.195f);
    private static readonly Vector3 Atras = new Vector3(-0.167f, 0f, 0.195f);
    private static readonly Vector3 Ventana = new Vector3(0.45f, -0.06f, 0.215f); // por donde sale la vaina, a la derecha
    private static readonly Vector3 PieDelCargador = new Vector3(-0.124f, 0f, -0.098f); // abajo de la empuñadura
    private static readonly Vector3 EjeDelCargador = new Vector3(-0.657f, 0f, -0.754f); // a lo largo de la empuñadura, hacia abajo
    private static readonly Vector3 TamanoCargador = new Vector3(0.065f, 0.24f, 0.075f); // ancho, largo, grosor
    private const float RecorridoCorredera = 0.11f;
    private const float VueltaCorredera = 0.03f, IdaCorredera = 0.07f; // segundos

    private readonly Transform arma;     // la que tiene la malla
    private readonly Transform camara;
    private readonly MeshRenderer render;
    private float escala;                // de las medidas del modelo a la malla
    private bool armada, sinPiezas;

    private Transform corredera;
    private float ultimoDisparo = -10f;
    private bool trabada;                // sin balas: la corredera queda atrás hasta recargar

    private Transform cargador;
    private MeshRenderer cargadorVista;
    private bool soltado;
    private float recargaAnterior = -1f;

    private struct Suelta { public Transform t; public Vector3 velocidad, eje; public float giro, hasta; }
    private readonly List<Suelta> sueltas = new List<Suelta>();
    private static Material materialVaina;

    public float escalaFogonazo = 0.35f;

    public PiezasDeLaPistola(GameObject pistola, Transform camara)
    {
        this.camara = camara;
        MeshFilter filtro = pistola.GetComponent<MeshFilter>();
        if (filtro == null) filtro = pistola.GetComponentInChildren<MeshFilter>(true);
        arma = filtro != null ? filtro.transform : pistola.transform;
        render = filtro != null ? filtro.GetComponent<MeshRenderer>() : null;
    }

    // Para juntar los vértices en piezas (solo mientras se arma).
    private int[] padre;
    private int Raiz(int a) { while (padre[a] != a) { padre[a] = padre[padre[a]]; a = padre[a]; } return a; }
    private void Unir(int a, int b) { a = Raiz(a); b = Raiz(b); if (a != b) padre[a] = b; }

    private Vector3 Punto(Vector3 p) => new Vector3(-p.x, p.y, p.z) * escala;
    private static Vector3 Direccion(Vector3 d) => new Vector3(-d.x, d.y, d.z).normalized;
    private int Capa => arma.gameObject.layer;

    // Largo del arma visto desde la cámara: las vainas y el cargador vuelan en proporción.
    private float Largo => (camara.InverseTransformPoint(arma.TransformPoint(Punto(Boca))) -
                            camara.InverseTransformPoint(arma.TransformPoint(Punto(Atras)))).magnitude;

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
            Debug.LogWarning("PiezasDeLaPistola: el modelo de la pistola no tiene Read/Write prendido, la corredera no se mueve.");
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
            int otro;
            if (porLugar.TryGetValue(clave, out otro)) Unir(i, otro); else porLugar[clave] = i;
        }
        for (int s = 0; s < malla.subMeshCount; s++)
        {
            int[] t = malla.GetTriangles(s);
            for (int i = 0; i < t.Length; i += 3) { Unir(t[i], t[i + 1]); Unir(t[i], t[i + 2]); }
        }

        // Caja de cada pieza, en las medidas del modelo. La corredera es lo de arriba: empieza arriba del gatillo y
        // llega hasta el lomo (el armazón, la empuñadura, el guardamonte y el gatillo quedan abajo).
        var minimo = new Dictionary<int, float>();
        var maximo = new Dictionary<int, float>();
        for (int i = 0; i < v.Length; i++)
        {
            int r = Raiz(i);
            float z = v[i].z / escala;
            float a, b;
            minimo[r] = minimo.TryGetValue(r, out a) ? Mathf.Min(a, z) : z;
            maximo[r] = maximo.TryGetValue(r, out b) ? Mathf.Max(b, z) : z;
        }

        Mesh resto = Object.Instantiate(malla);
        Mesh arriba = Object.Instantiate(malla);
        resto.name = malla.name + " (armazón)";
        arriba.name = malla.name + " (corredera)";
        int enCorredera = 0;
        var abajo = new List<int>();
        var encima = new List<int>();
        for (int s = 0; s < malla.subMeshCount; s++)
        {
            int[] t = malla.GetTriangles(s);
            abajo.Clear();
            encima.Clear();
            for (int i = 0; i < t.Length; i += 3)
            {
                int r = Raiz(t[i]);
                List<int> lista = maximo[r] > 0.15f && minimo[r] > 0.04f ? encima : abajo;
                lista.Add(t[i]); lista.Add(t[i + 1]); lista.Add(t[i + 2]);
            }
            enCorredera += encima.Count;
            resto.SetTriangles(abajo, s);
            arriba.SetTriangles(encima, s);
        }
        if (enCorredera == 0)
        {
            Object.Destroy(resto);
            Object.Destroy(arriba);
            sinPiezas = true;
            return false;
        }
        padre = null;
        resto.RecalculateBounds();
        arriba.RecalculateBounds();
        filtro.sharedMesh = resto;

        var go = new GameObject("Corredera");
        go.layer = Capa;
        corredera = go.transform;
        corredera.SetParent(arma, false);
        go.AddComponent<MeshFilter>().sharedMesh = arriba;
        MeshRenderer vista = go.AddComponent<MeshRenderer>();
        vista.sharedMaterials = render.sharedMaterials;
        vista.shadowCastingMode = render.shadowCastingMode;
        vista.receiveShadows = render.receiveShadows;
        return true;
    }

    /// <summary>Un tiro: la corredera va atrás, fogonazo y vaina.</summary>
    public void Disparo(int balasQueQuedan, GameObject fogonazo)
    {
        if (!Armar() && escala <= 0f) return;
        ultimoDisparo = Time.time;
        trabada = balasQueQuedan <= 0;
        Fogonazo(fogonazo);
        Vaina();
    }

    /// <summary>Cada cuadro, con la pistola en la mano. "recarga" es el avance de la recarga (de 0 a 1), o -1.</summary>
    public void Actualizar(float recarga, int balas)
    {
        if (!Armar() || corredera == null) { Cargador(recarga); return; }

        // Se suelta la corredera al terminar la recarga (o si volvió a tener balas sin recargar).
        if (trabada && (recarga >= 0.85f || (recarga < 0f && balas > 0)))
        {
            trabada = false;
            ultimoDisparo = Time.time - VueltaCorredera;
        }
        float atras;
        if (trabada) atras = 1f;
        else
        {
            float t = Time.time - ultimoDisparo;
            atras = t < VueltaCorredera ? t / VueltaCorredera : 1f - Mathf.SmoothStep(0f, 1f, (t - VueltaCorredera) / IdaCorredera);
        }
        corredera.localPosition = Punto(new Vector3(-RecorridoCorredera * Mathf.Clamp01(atras), 0f, 0f));
        if (corredera.gameObject.layer != Capa) corredera.gameObject.layer = Capa;

        Cargador(recarga);
    }

    /// <summary>Sin detalles o sin la pistola en la mano: todo en su lugar.</summary>
    public void Quieta()
    {
        if (corredera != null) corredera.localPosition = Vector3.zero;
        if (cargadorVista != null) cargadorVista.enabled = false;
        recargaAnterior = -1f;
        soltado = false;
    }

    /// <summary>Lo que salió volando (vainas, el cargador vacío): cae y se borra. Cada cuadro, haya o no pistola.</summary>
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

    // ---------- Fogonazo ----------

    private void Fogonazo(GameObject modelo)
    {
        if (modelo == null || escala <= 0f) return;
        Vector3 boca = arma.TransformPoint(Punto(Boca));
        Vector3 adelante = arma.TransformDirection(Direccion(Vector3.right));
        GameObject f = Object.Instantiate(modelo, boca, Quaternion.LookRotation(adelante, camara.up), camara);
        f.name = "Fogonazo (primera persona)";
        f.transform.localScale = Vector3.one * escalaFogonazo;
        foreach (Transform t in f.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Capa;
        // La luz ya la pone Trazadora.Fogonazo. Se apaga el objeto entero para que su script de parpadeo no falle.
        foreach (Light luz in f.GetComponentsInChildren<Light>(true))
            if (luz.GetComponent<ParticleSystem>() == null) luz.gameObject.SetActive(false); else luz.enabled = false;
        // Viene hecho para un arma automática (dispara en loop): acá sale un solo destello.
        foreach (ParticleSystem ps in f.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Emit(1);
        }
        Object.Destroy(f, 0.3f);
    }

    // ---------- Vaina ----------

    private void Vaina()
    {
        if (escala <= 0f) return;
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(go.GetComponent<Collider>());
        go.name = "Vaina";
        go.layer = Capa;
        MeshRenderer vista = go.GetComponent<MeshRenderer>();
        if (materialVaina == null)
        {
            materialVaina = new Material(vista.sharedMaterial) { name = "Vaina (bronce)" };
            materialVaina.color = new Color(0.86f, 0.66f, 0.28f);
            if (materialVaina.HasProperty("_Metallic")) materialVaina.SetFloat("_Metallic", 0.85f);
            if (materialVaina.HasProperty("_Smoothness")) materialVaina.SetFloat("_Smoothness", 0.6f);
        }
        vista.sharedMaterial = materialVaina;
        vista.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Transform t = go.transform;
        t.SetParent(arma, false);
        t.localPosition = Punto(Ventana);
        t.localRotation = Quaternion.Euler(0f, 0f, 90f); // acostada, a lo largo del caño
        t.localScale = new Vector3(0.045f, 0.045f, 0.045f) * escala; // 4,5 de ancho y 9 de largo, en medidas del modelo
        t.SetParent(camara, true);

        float largo = Largo;
        sueltas.Add(new Suelta
        {
            t = t,
            velocidad = new Vector3(Random.Range(2.6f, 3.4f), Random.Range(2.2f, 3f), Random.Range(-1f, 0f)) * largo,
            eje = Random.onUnitSphere,
            giro = Random.Range(700f, 1100f),
            hasta = Time.time + 0.6f,
        });
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

    private bool CrearCargador()
    {
        if (render == null) return false;
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(go.GetComponent<Collider>());
        go.name = "Cargador";
        go.layer = Capa;
        cargadorVista = go.GetComponent<MeshRenderer>();
        Material negro = render.sharedMaterial;
        foreach (Material m in render.sharedMaterials)
            if (m != null && m.name.ToLowerInvariant().Contains("black")) { negro = m; break; }
        cargadorVista.sharedMaterial = negro;
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
