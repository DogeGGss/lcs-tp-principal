using System.Collections.Generic;
using UnityEngine;

// Los detalles del Urquiza en primera persona (US 246): al recargar el cargador sale por abajo, se suelta y cae, y
// entra uno nuevo. Lo maneja AnimacionPrimeraPersona (que además hace el tirón de la palanca de carga al final).
// El cargador ya es una pieza aparte en el prefab (el hijo "Cargador", con la malla Magazine_AR_2 del AR_3.fbx), así
// que se mueve entero, sin separar mallas. Las medidas están en las coordenadas del modelo (x hacia la boca, z hacia
// arriba); Unity da vuelta la x al importarlo: en la malla un punto (x, y, z) del modelo está en (-x, y, z) * escala.
public class PiezasDelUrquiza : IPiezasDelArma
{
    private const float LargoModelo = 1.549f;  // de punta a punta en x (de -0.721 a 0.828)
    private const float Recorrido = 0.42f;     // cuánto baja el cargador para salir entero (mide 0,37)
    private static readonly Vector3 EjeDelCargador = new Vector3(-0.033f, 0f, -1f); // hacia abajo, casi derecho

    private readonly Transform arma;
    private readonly Transform camara;
    private readonly Transform cargador;
    private readonly Renderer[] vistas;
    private readonly Vector3 lugarCargador;
    private float escala = -1f;
    private bool soltado;
    private float recargaAnterior = -1f;

    private struct Suelta { public Transform t; public Vector3 velocidad, eje; public float giro, hasta; }
    private readonly List<Suelta> sueltas = new List<Suelta>();

    public PiezasDelUrquiza(GameObject urquiza, Transform camara)
    {
        this.camara = camara;
        arma = urquiza.transform;
        cargador = arma.Find("Cargador");
        if (cargador != null)
        {
            lugarCargador = cargador.localPosition;
            vistas = cargador.GetComponentsInChildren<Renderer>(true);
        }
    }

    private float Escala()
    {
        if (escala > 0f) return escala;
        MeshFilter filtro = arma.GetComponent<MeshFilter>();
        escala = filtro != null && filtro.sharedMesh != null ? filtro.sharedMesh.bounds.size.x / LargoModelo : 0.01f;
        return escala;
    }

    private Vector3 Abajo => new Vector3(-EjeDelCargador.x, EjeDelCargador.y, EjeDelCargador.z).normalized;

    public void Disparo(int balasQueQuedan) { }

    // Sale (de 0,12 a 0,30 de la recarga), se suelta y cae; entra uno nuevo (de 0,40 a 0,60, cuando el arma da el
    // empujón). Entre medio el arma queda sin cargador.
    public void Actualizar(float recarga, int balas)
    {
        if (cargador == null) return;
        if (recarga < 0f || recarga < recargaAnterior) soltado = false; // terminó, o empezó otra
        recargaAnterior = recarga;
        if (recarga < 0f) { Quieta(); return; }

        float largo = Recorrido * Escala();
        float fuera = 0f;
        bool visible = true;
        if (recarga >= 0.12f && recarga < 0.30f) fuera = Mathf.SmoothStep(0f, 1f, (recarga - 0.12f) / 0.18f);
        else if (recarga >= 0.30f && recarga < 0.40f)
        {
            if (!soltado) { soltado = true; Ubicar(largo); Soltar(); }
            visible = false;
        }
        else if (recarga >= 0.40f && recarga < 0.60f) fuera = 1f - Mathf.SmoothStep(0f, 1f, (recarga - 0.40f) / 0.20f);
        Ubicar(largo * fuera);
        Mostrar(visible);
    }

    private void Ubicar(float fuera) => cargador.localPosition = lugarCargador + Abajo * fuera;

    private void Mostrar(bool si)
    {
        if (vistas == null) return;
        foreach (Renderer r in vistas) if (r != null && r.enabled != si) r.enabled = si;
    }

    public void Quieta()
    {
        if (cargador == null) return;
        cargador.localPosition = lugarCargador;
        Mostrar(true);
        recargaAnterior = -1f;
        soltado = false;
    }

    // El cargador vacío: una copia que cae y se borra.
    private void Soltar()
    {
        GameObject vacio = Object.Instantiate(cargador.gameObject, cargador.position, cargador.rotation);
        vacio.name = "Cargador vacío";
        Transform t = vacio.transform;
        t.localScale = cargador.lossyScale;
        t.SetParent(camara, true);
        foreach (Renderer r in vacio.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
        Vector3 caida = camara.InverseTransformDirection(arma.TransformDirection(Abajo));
        float largo = camara.InverseTransformVector(arma.TransformVector(Vector3.right * LargoModelo * Escala())).magnitude;
        sueltas.Add(new Suelta
        {
            t = t,
            velocidad = caida * (0.8f * largo),
            eje = Vector3.right,
            giro = Random.Range(-90f, 90f),
            hasta = Time.time + 0.8f,
        });
    }

    public void ActualizarSueltas(float dt)
    {
        if (sueltas.Count == 0) return;
        float largo = camara.InverseTransformVector(arma.TransformVector(Vector3.right * LargoModelo * Escala())).magnitude;
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
