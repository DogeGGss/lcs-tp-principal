using UnityEngine;

// El estallido de un cometa del minijuego al recibir un disparo (F21, US 156, CA5): un destello que se abre y unas
// chispas de su color que salen para todos lados, se frenan y se apagan.
public class Chispas : MonoBehaviour
{
    private const int Cantidad = 16;
    private const float Duracion = 1.1f, DuracionDestello = 0.35f;

    private Transform destello;
    private SpriteRenderer vistaDestello;
    private Transform[] chispas;
    private SpriteRenderer[] vistas;
    private Vector3[] velocidades;
    private float tamano, desde;

    /// <param name="diametro">Diámetro del núcleo del cometa: el estallido es proporcional.</param>
    /// <param name="arrastre">Parte de la velocidad del cometa que se llevan las chispas.</param>
    public static void Soltar(Vector3 punto, Color color, Color nucleo, float diametro, Vector3 arrastre, Transform padre)
    {
        var go = new GameObject("Chispas");
        go.transform.SetParent(padre, false);
        go.transform.position = punto;
        var c = go.AddComponent<Chispas>();
        c.tamano = diametro;
        c.desde = Time.time;

        c.vistaDestello = Cometa.Brillo("Destello", go.transform, nucleo, 2);
        c.destello = c.vistaDestello.transform;

        c.chispas = new Transform[Cantidad];
        c.vistas = new SpriteRenderer[Cantidad];
        c.velocidades = new Vector3[Cantidad];
        for (int i = 0; i < Cantidad; i++)
        {
            SpriteRenderer vista = Cometa.Brillo("Chispa", go.transform, i % 4 == 0 ? nucleo : color, 1);
            c.vistas[i] = vista;
            c.chispas[i] = vista.transform;
            c.velocidades[i] = Random.onUnitSphere * (diametro * Random.Range(3f, 6f)) + arrastre;
        }
    }

    private void Update()
    {
        float t = (Time.time - desde) / Duracion;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        float dt = Time.deltaTime;
        Camera camara = Camera.main;
        Quaternion mira = camara != null ? camara.transform.rotation : Quaternion.identity;

        // El destello se abre rápido y se apaga.
        float d = Mathf.Clamp01((Time.time - desde) / DuracionDestello);
        destello.gameObject.SetActive(d < 1f);
        destello.rotation = mira;
        destello.localScale = Vector3.one * (tamano * Mathf.Lerp(1.5f, 6f, 1f - (1f - d) * (1f - d)));
        Color cd = vistaDestello.color;
        cd.a = 1f - d;
        vistaDestello.color = cd;

        // Las chispas salen y se frenan (en el espacio no caen), achicándose hasta apagarse.
        for (int i = 0; i < Cantidad; i++)
        {
            velocidades[i] *= 1f - Mathf.Min(1f, 2.5f * dt);
            chispas[i].position += velocidades[i] * dt;
            chispas[i].rotation = mira;
            chispas[i].localScale = Vector3.one * (tamano * 0.45f * (1f - t));
            Color c = vistas[i].color;
            c.a = 1f - t * t;
            vistas[i].color = c;
        }
    }
}
